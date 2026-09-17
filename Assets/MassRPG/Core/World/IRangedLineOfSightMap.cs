namespace MassRPG.Core.World
{
    /// <summary>
    /// Static/dynamic world contract for ranged and magic line of sight. Actors are deliberately
    /// not represented as blockers. Trees/fences simply return false unless authored otherwise.
    /// </summary>
    public interface IRangedLineOfSightMap
    {
        bool IsRangedLineOfSightBlockingTile(GridLocation location);
        bool IsRangedLineOfSightBlockedCardinalEdge(GridLocation from, GridLocation to);
    }
}
