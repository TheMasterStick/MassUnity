using MassRPG.Client.World;
using MassRPG.Core.World;
using UnityEngine;

namespace MassRPG.Client.Actors
{
    /// <summary>
    /// Presentation-only interpolation for an authoritative logical actor position. Gameplay state
    /// never reads Transform.position back as truth; the server supplies GridLocation/elevation and
    /// this component merely makes discrete logical movement look smooth in 3D.
    /// </summary>
    public sealed class LogicalActorView : MonoBehaviour
    {
        [SerializeField] private GridPresentationSpace presentationSpace;
        [SerializeField] private float movementSpeedMetersPerSecond = 4f;
        [SerializeField] private float rotationSharpness = 16f;
        [SerializeField] private bool snapOnFirstState = true;

        private Vector3 _targetWorldPosition;
        private bool _hasState;

        public GridLocation LogicalLocation { get; private set; }
        public int LogicalElevation { get; private set; }
        public bool HasAuthoritativeState => _hasState;

        public void Configure(GridPresentationSpace space)
        {
            if (presentationSpace == space) return;
            Unsubscribe();
            presentationSpace = space;
            Subscribe();
            RefreshAfterOriginShift(true);
        }

        public void ApplyAuthoritativeState(GridLocation location, int logicalElevation, bool forceSnap = false)
        {
            if (presentationSpace == null) return;
            LogicalLocation = location;
            LogicalElevation = logicalElevation;
            _targetWorldPosition = presentationSpace.ToWorldPosition(location, logicalElevation);

            if (forceSnap || (!_hasState && snapOnFirstState))
                transform.position = _targetWorldPosition;
            _hasState = true;
        }

        public void RefreshAfterOriginShift(bool snap = true)
        {
            if (!_hasState || presentationSpace == null) return;
            _targetWorldPosition = presentationSpace.ToWorldPosition(LogicalLocation, LogicalElevation);
            if (snap) transform.position = _targetWorldPosition;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (presentationSpace != null) presentationSpace.OriginChanged += OnPresentationOriginChanged;
        }

        private void Unsubscribe()
        {
            if (presentationSpace != null) presentationSpace.OriginChanged -= OnPresentationOriginChanged;
        }

        private void OnPresentationOriginChanged(GridCoord before, GridCoord after)
            => RefreshAfterOriginShift(true);

        private void Update()
        {
            if (!_hasState) return;
            var before = transform.position;
            transform.position = Vector3.MoveTowards(
                before,
                _targetWorldPosition,
                Mathf.Max(0.01f, movementSpeedMetersPerSecond) * Time.deltaTime);

            var delta = _targetWorldPosition - before;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return;
            var desired = Quaternion.LookRotation(delta.normalized, Vector3.up);
            var t = rotationSharpness <= 0f ? 1f : 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, t);
        }
    }
}
