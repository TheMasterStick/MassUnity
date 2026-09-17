namespace MassRPG.Core.World
{
    /// <summary>
    /// Minimal world contract needed by local movement/pathfinding. The authored-world storage
    /// implementation will provide this without making every tile a Unity GameObject.
    /// </summary>
    public interface IGridTraversalMap
    {
        bool IsWalkable(GridLocation location);
        int GetLogicalElevation(GridLocation location);

        /// <summary>
        /// Checks a cardinal one-tile edge. Implementations account for cliffs, explicit ramps,
        /// walls, doors, water, fences, and other movement rules attached to that edge.
        /// </summary>
        bool CanTraverseCardinalEdge(GridLocation from, GridLocation to);
    }
}
