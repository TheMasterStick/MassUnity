using System;
using MassRPG.Core.World;
using MassRPG.Data.World;

namespace MassRPG.EditorCore.World
{
    /// <summary>
    /// Undoable authoring operations for exact cardinal tile edges. Normal elevation differences
    /// remain cliffs; ramps are explicit +1/-1 transitions rather than smoothed terrain.
    /// </summary>
    public static class WorldEdgeEditing
    {
        public static bool TryPlaceRamp(WorldEditSession session, GridLocation from, GridLocation to)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!TryGetAdjacentCells(session, from, to, out var fromCell, out var toCell, out var fromEdge, out var toEdge))
                return false;
            if (Math.Abs(fromCell.Elevation - toCell.Elevation) != 1) return false;

            fromCell = WithEdges(
                fromCell,
                fromCell.MovementBlockedEdges & ~fromEdge,
                fromCell.LineOfSightBlockedEdges,
                fromCell.ElevationTransitionEdges | fromEdge);
            toCell = WithEdges(
                toCell,
                toCell.MovementBlockedEdges & ~toEdge,
                toCell.LineOfSightBlockedEdges,
                toCell.ElevationTransitionEdges | toEdge);

            return session.Apply("Place ramp", new[]
            {
                new WorldCellEdit(from, fromCell),
                new WorldCellEdit(to, toCell)
            }) > 0;
        }

        public static bool TryRemoveRamp(WorldEditSession session, GridLocation from, GridLocation to)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!TryGetAdjacentCells(session, from, to, out var fromCell, out var toCell, out var fromEdge, out var toEdge))
                return false;

            var hadTransition = (fromCell.ElevationTransitionEdges & fromEdge) != 0
                || (toCell.ElevationTransitionEdges & toEdge) != 0;
            if (!hadTransition) return false;

            fromCell = WithEdges(
                fromCell,
                fromCell.MovementBlockedEdges,
                fromCell.LineOfSightBlockedEdges,
                fromCell.ElevationTransitionEdges & ~fromEdge);
            toCell = WithEdges(
                toCell,
                toCell.MovementBlockedEdges,
                toCell.LineOfSightBlockedEdges,
                toCell.ElevationTransitionEdges & ~toEdge);

            return session.Apply("Remove ramp", new[]
            {
                new WorldCellEdit(from, fromCell),
                new WorldCellEdit(to, toCell)
            }) > 0;
        }

        public static bool SetBarrier(
            WorldEditSession session,
            GridLocation from,
            GridLocation to,
            bool movementBlocked,
            bool lineOfSightBlocked)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!TryGetAdjacentCells(session, from, to, out var fromCell, out var toCell, out var fromEdge, out var toEdge))
                return false;

            fromCell = WithEdges(
                fromCell,
                SetBit(fromCell.MovementBlockedEdges, fromEdge, movementBlocked),
                SetBit(fromCell.LineOfSightBlockedEdges, fromEdge, lineOfSightBlocked),
                fromCell.ElevationTransitionEdges);
            toCell = WithEdges(
                toCell,
                SetBit(toCell.MovementBlockedEdges, toEdge, movementBlocked),
                SetBit(toCell.LineOfSightBlockedEdges, toEdge, lineOfSightBlocked),
                toCell.ElevationTransitionEdges);

            return session.Apply("Edit barrier", new[]
            {
                new WorldCellEdit(from, fromCell),
                new WorldCellEdit(to, toCell)
            }) > 0;
        }

        private static bool TryGetAdjacentCells(
            WorldEditSession session,
            GridLocation from,
            GridLocation to,
            out AuthoredTileCell fromCell,
            out AuthoredTileCell toCell,
            out CardinalEdgeMask fromEdge,
            out CardinalEdgeMask toEdge)
        {
            fromCell = default;
            toCell = default;
            fromEdge = CardinalEdgeMask.None;
            toEdge = CardinalEdgeMask.None;
            if (!from.SameLayer(to)) return false;

            try { fromEdge = CardinalEdges.Between(from.Tile, to.Tile); }
            catch (ArgumentException) { return false; }
            toEdge = CardinalEdges.Opposite(fromEdge);

            session.Store.GetOrCreatePage(from);
            session.Store.GetOrCreatePage(to);
            return session.Store.TryGetCell(from, out fromCell) && session.Store.TryGetCell(to, out toCell);
        }

        private static AuthoredTileCell WithEdges(
            AuthoredTileCell cell,
            CardinalEdgeMask movement,
            CardinalEdgeMask los,
            CardinalEdgeMask transitions)
            => new AuthoredTileCell(cell.GroundId, cell.Elevation, cell.Flags, movement, los, transitions);

        private static CardinalEdgeMask SetBit(CardinalEdgeMask current, CardinalEdgeMask edge, bool enabled)
            => enabled ? current | edge : current & ~edge;
    }
}
