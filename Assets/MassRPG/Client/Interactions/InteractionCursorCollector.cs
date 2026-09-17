using System;
using System.Collections.Generic;
using MassRPG.Client.World;
using MassRPG.Core.Interactions;
using MassRPG.Core.World;
using UnityEngine;

namespace MassRPG.Client.Interactions
{
    public interface IClientInteractionOptionSource
    {
        /// <summary>
        /// Adds actions currently valid for a logical target. The implementation should derive
        /// options from current logical/game state rather than trusting Unity collider metadata.
        /// </summary>
        void CollectOptions(InteractionTarget target, List<InteractionOption> options);
    }

    public sealed class InteractionCursorSnapshot
    {
        internal InteractionCursorSnapshot(
            GridLocation tile,
            IReadOnlyList<InteractionTarget> targets,
            IReadOnlyList<InteractionOption> rawOptions,
            IReadOnlyList<InteractionOption> contextStack,
            InteractionOption? defaultOption)
        {
            Tile = tile;
            Targets = targets ?? Array.Empty<InteractionTarget>();
            RawOptions = rawOptions ?? Array.Empty<InteractionOption>();
            ContextStack = contextStack ?? Array.Empty<InteractionOption>();
            DefaultOption = defaultOption;
        }

        public GridLocation Tile { get; }
        public IReadOnlyList<InteractionTarget> Targets { get; }
        public IReadOnlyList<InteractionOption> RawOptions { get; }
        public IReadOnlyList<InteractionOption> ContextStack { get; }
        public InteractionOption? DefaultOption { get; }
    }

    /// <summary>
    /// Unity-side cursor adapter. Physics is used only to discover presentation views and a world
    /// point. Logical target IDs/actions and ordering are delegated to Core interaction rules so a
    /// crowded tile cannot change behavior merely because a different collider happened to be first.
    /// </summary>
    public static class InteractionCursorCollector
    {
        public static bool TryCollect(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            GridPresentationSpace presentation,
            int plane,
            int storey,
            IClientInteractionOptionSource optionSource,
            out InteractionCursorSnapshot snapshot,
            float maximumDistance = 1000f,
            int layerMask = Physics.DefaultRaycastLayers)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (maximumDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(maximumDistance));

            var ray = camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, maximumDistance, layerMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                snapshot = null;
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var tile = presentation.WorldPositionToTile(hits[0].point);
            if (!WorldConstants.IsInsideWorld(tile))
            {
                snapshot = null;
                return false;
            }

            var location = new GridLocation(tile, plane, storey);
            var targets = new List<InteractionTarget>();
            var seenTargets = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null) continue;
                var view = collider.GetComponentInParent<InteractionTargetView>();
                if (view == null || !view.HasLogicalTarget) continue;
                var target = view.LogicalTarget;

                // A large model/collider may cover multiple cursor tiles; only include it when the
                // logical target itself belongs to this logical layer. Footprint-aware presenters can
                // expose more than one view marker later while keeping the same stable target ID.
                if (!target.Location.SameLayer(location)) continue;
                if (!seenTargets.Add(target.TargetId)) continue;
                targets.Add(target);
            }

            var options = new List<InteractionOption>();
            if (optionSource != null)
            {
                for (var i = 0; i < targets.Count; i++)
                    optionSource.CollectOptions(targets[i], options);
            }

            // Terrain movement remains available even when actors/objects overlap the tile. Whether
            // the authoritative movement request ultimately accepts the destination is still decided
            // by the server-side pathing map.
            var groundTarget = new InteractionTarget(
                "tile:" + tile.X + ":" + tile.Y + ":" + plane + ":" + storey,
                InteractionTargetKind.MovementTile,
                location,
                "Ground");
            options.Add(new InteractionOption(groundTarget, InteractionActionKind.WalkHere, "Walk here"));

            InteractionOption? defaultOption = null;
            if (InteractionPriority.TryResolveDefault(options, out var resolved)) defaultOption = resolved;
            var context = InteractionPriority.BuildContextStack(options);
            snapshot = new InteractionCursorSnapshot(location, targets.ToArray(), options.ToArray(), context, defaultOption);
            return true;
        }
    }
}
