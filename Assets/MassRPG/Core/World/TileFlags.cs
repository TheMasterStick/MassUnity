using System;

namespace MassRPG.Core.World
{
    /// <summary>
    /// Compact authored tile flags. Movement and ranged line of sight are intentionally separate:
    /// fences/trees can block movement without blocking ranged or magic attacks.
    /// </summary>
    [Flags]
    public enum TileFlags : byte
    {
        None = 0,
        MovementBlocked = 1 << 0,
        RangedLineOfSightBlocked = 1 << 1,
        Water = 1 << 2,
        DeepWater = 1 << 3,
        NoBuild = 1 << 4
    }
}
