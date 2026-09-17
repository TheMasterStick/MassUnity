using System;
using MassRPG.Client.Actors;
using MassRPG.Client.Camera;
using MassRPG.Client.World;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Server.Authority;
using UnityEngine;

namespace MassRPG.Client.Testing
{
    /// <summary>
    /// Lightweight in-process play session used by the World Editor's Play From Here command.
    /// Movement still crosses the LocalGameAuthority request boundary, while reusable client actor
    /// interpolation, authored-page streaming and floating-origin components provide presentation.
    /// This keeps the editor test harness on the same path as the production Unity client instead of
    /// maintaining a second bespoke terrain renderer that could silently drift from the real game.
    /// </summary>
    public sealed class LocalPlayTestSession : MonoBehaviour
    {
        private const float MovementStepSeconds = 0.12f;
        private const int MaximumMovementCatchupSteps = 8;

        private AuthoredWorldPageStore _world;
        private LocalGameAuthority _authority;
        private PlayerState _player;
        private GridPresentationSpace _presentation;
        private LogicalActorView _playerView;
        private LogicalTerrainChunkStreamer _terrainStreamer;
        private Transform _destinationMarker;
        private BoundedObliqueCameraRig _cameraRig;
        private Material _terrainMaterial;
        private Material _playerMaterial;
        private Material _markerMaterial;
        private float _nextMovementStep;
        private string _lastDecision = "Click authored terrain to move.";
        private bool _legacyInputAvailable = true;

        public PlayerState Player => _player;
        public LocalGameAuthority Authority => _authority;
        public AuthoredWorldPageStore World => _world;
        public int LoadedRenderChunkCount => _terrainStreamer != null ? _terrainStreamer.ActiveChunkCount : 0;

        public void Initialize(AuthoredWorldPageStore world, GridLocation spawnLocation)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!WorldConstants.IsInsideWorld(spawnLocation.Tile))
                throw new ArgumentOutOfRangeException(nameof(spawnLocation));

            _world = world;
            _authority = new LocalGameAuthority(new ItemCatalog(), world);
            _player = new PlayerState(Guid.NewGuid(), "Editor Test Character")
            {
                Location = spawnLocation
            };
            _authority.RegisterPlayer(_player);

            gameObject.name = "MassRPG Play From Here Session";
            CreatePresentationSpace(spawnLocation.Tile);
            CreateMaterials();
            CreatePlayerView();
            CreateTerrainStreamer();
            CreateDestinationMarker();
            CreateCamera();
            SyncPlayerView(true);
            _terrainStreamer.SyncToFocusNow(true);
            SyncDestinationMarker();

            _lastDecision = world.IsWalkable(spawnLocation)
                ? "Spawned through local authority. Click terrain to issue a MoveTo request."
                : "Spawn tile is not walkable or its page is missing; visual inspection is still available.";
        }

        private void Update()
        {
            if (_authority == null || _player == null) return;

            AdvanceAuthoritativeMovement();
            SyncPlayerView(false);
            _terrainStreamer?.SyncToFocusNow(false);
            SyncDestinationMarker();
            HandleLegacyInput();
        }

        private void AdvanceAuthoritativeMovement()
        {
            if (!_player.Movement.IsMoving) return;

            var now = Time.unscaledTime;
            var steps = 0;
            while (_player.Movement.IsMoving
                   && now >= _nextMovementStep
                   && steps < MaximumMovementCatchupSteps)
            {
                if (!_authority.AdvanceMovementOneStep(_player.CharacterId))
                {
                    _lastDecision = "Movement stopped because the next authoritative step became invalid.";
                    break;
                }

                steps++;
                _nextMovementStep += MovementStepSeconds;
            }

            if (steps >= MaximumMovementCatchupSteps && now > _nextMovementStep + MovementStepSeconds)
                _nextMovementStep = now + MovementStepSeconds;
        }

        private void HandleLegacyInput()
        {
            if (!_legacyInputAvailable || _cameraRig == null) return;
            try
            {
                var scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.001f) _cameraRig.ApplyZoomDelta(scroll);
                if (!Input.GetMouseButtonDown(0)) return;

                var camera = UnityEngine.Camera.main;
                if (camera == null) return;
                var ray = camera.ScreenPointToRay(Input.mousePosition);
                if (!Physics.Raycast(ray, out var hit, 1000f)) return;
                var tile = _presentation.WorldPositionToTile(hit.point);
                if (!WorldConstants.IsInsideWorld(tile)) return;

                var destination = new GridLocation(tile, _player.Plane, _player.Storey);
                var decision = _authority.Submit(new MoveToRequest(Guid.NewGuid(), _player.CharacterId, destination));
                _lastDecision = decision.Accepted
                    ? $"Move accepted -> {tile.X}, {tile.Y} ({_player.Movement.RemainingSteps} step(s))."
                    : $"Move rejected: {decision.Code}.";
                if (decision.Accepted) _nextMovementStep = Time.unscaledTime;
                SyncDestinationMarker();
            }
            catch (InvalidOperationException)
            {
                // Projects configured for only the new Input System throw when legacy Input is read.
                // The session remains useful for spawn/camera/terrain validation rather than failing.
                _legacyInputAvailable = false;
                _lastDecision = "Legacy mouse input is disabled by this Unity project; Play From Here inspection remains active.";
            }
        }

        private void CreatePresentationSpace(GridCoord origin)
        {
            var root = new GameObject("Presentation Space");
            root.transform.SetParent(transform, false);
            _presentation = root.AddComponent<GridPresentationSpace>();
            _presentation.SetOrigin(origin);
            _presentation.OriginChanged += OnPresentationOriginChanged;
        }

        private void CreateMaterials()
        {
            _terrainMaterial = CreateMaterial(new Color(0.32f, 0.43f, 0.27f));
            _playerMaterial = CreateMaterial(new Color(0.72f, 0.73f, 0.78f));
            _markerMaterial = CreateMaterial(new Color(0.95f, 0.77f, 0.18f));
        }

        private void CreatePlayerView()
        {
            var actorRoot = new GameObject("Editor Test Character");
            actorRoot.transform.SetParent(transform, true);
            _playerView = actorRoot.AddComponent<LogicalActorView>();
            _playerView.Configure(_presentation);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Temporary Character Visual";
            capsule.transform.SetParent(actorRoot.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            var collider = capsule.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var renderer = capsule.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _playerMaterial;
        }

        private void CreateTerrainStreamer()
        {
            var root = new GameObject("Authored Terrain Stream");
            root.transform.SetParent(transform, true);
            _terrainStreamer = root.AddComponent<LogicalTerrainChunkStreamer>();
            _terrainStreamer.Configure(_presentation, _playerView, _world, _terrainMaterial);
        }

        private void CreateDestinationMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Move Destination";
            marker.transform.SetParent(transform, true);
            marker.transform.localScale = new Vector3(0.62f, 0.025f, 0.62f);
            var collider = marker.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _markerMaterial;
            _destinationMarker = marker.transform;
            marker.SetActive(false);
        }

        private void CreateCamera()
		{
		var existing = UnityEngine.Camera.main;
		if (existing != null)
		{
        existing.enabled = false;

        var existingListener = existing.GetComponent<AudioListener>();
        if (existingListener != null)
            existingListener.enabled = false;
		}

		var rig = new GameObject("MassRPG Test Camera Rig");
            rig.transform.SetParent(transform, false);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();

            _cameraRig = rig.AddComponent<BoundedObliqueCameraRig>();
            _cameraRig.FollowTarget = _playerView.transform;
            _cameraRig.SetNormalizedZoom(0.35f);
        }

        private void SyncPlayerView(bool snap)
        {
            if (_playerView == null || _player == null || _world == null) return;
            _playerView.ApplyAuthoritativeState(
                _player.Location,
                _world.GetLogicalElevation(_player.Location),
                snap);
        }

        private void SyncDestinationMarker()
        {
            if (_destinationMarker == null || _player == null || _presentation == null) return;
            var destination = _player.Movement.Destination;
            if (!destination.HasValue)
            {
                _destinationMarker.gameObject.SetActive(false);
                return;
            }

            var elevation = _world.GetLogicalElevation(destination.Value);
            var position = _presentation.ToWorldPosition(destination.Value, elevation);
            position.y += 0.035f;
            _destinationMarker.position = position;
            _destinationMarker.gameObject.SetActive(true);
        }

        private void OnPresentationOriginChanged(GridCoord before, GridCoord after)
        {
            SyncDestinationMarker();
        }

        private void OnGUI()
        {
            if (_player == null) return;
            var origin = _presentation != null ? _presentation.OriginTile : _player.Tile;
            var pageCount = _terrainStreamer != null ? _terrainStreamer.LoadedStoragePageCount : _world.LoadedPageCount;
            var text =
                $"MassRPG - Play From Here\n" +
                $"Tile: {_player.Tile.X}, {_player.Tile.Y}   Plane: {_player.Plane}   Floor: {_player.Storey}\n" +
                $"Render chunks: {LoadedRenderChunkCount}   Storage pages: {pageCount}   Presentation origin: {origin.X}, {origin.Y}\n" +
                $"{_lastDecision}\n" +
                "Left click: move   Mouse wheel: zoom   Stop Play Mode: return to editor";
            GUI.Box(new Rect(12f, 12f, 650f, 98f), text);
        }

        private void OnDestroy()
        {
            if (_presentation != null) _presentation.OriginChanged -= OnPresentationOriginChanged;
            if (_terrainMaterial != null) Destroy(_terrainMaterial);
            if (_playerMaterial != null) Destroy(_playerMaterial);
            if (_markerMaterial != null) Destroy(_markerMaterial);
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No suitable built-in terrain test shader was found.");
            return new Material(shader) { color = color };
        }
    }
}
