using System;

namespace MassRPG.EditorCore.World
{
    /// <summary>
    /// Explicit values keep editor layouts/preferences stable when new authoring modes are added.
    /// Selection was added after the original modes and therefore intentionally uses value 12.
    /// </summary>
    public enum WorldEditorMode
    {
        Terrain = 0,
        Elevation = 1,
        Water = 2,
        Edges = 3,
        Selection = 12,
        Roads = 4,
        Objects = 5,
        Doodads = 6,
        Resources = 7,
        Creatures = 8,
        Regions = 9,
        Pathing = 10,
        PointsOfInterest = 11
    }

    [Flags]
    public enum WorldEditorOverlay
    {
        None = 0,
        Grid = 1 << 0,
        Elevation = 1 << 1,
        Regions = 1 << 2,
        CreatureSpawns = 1 << 3,
        Resources = 1 << 4,
        Pathing = 1 << 5,
        NoBuild = 1 << 6,
        Water = 1 << 7,
        PointsOfInterest = 1 << 8,
        StoragePages = 1 << 9,
        RenderChunks = 1 << 10,
        Edges = 1 << 11
    }

    public enum BrushShape
    {
        Square = 0,
        Circle = 1
    }
}
