using System;
using MassRPG.Core.World;
using UnityEngine;

namespace MassRPG.Client.World
{
    /// <summary>
    /// Converts stable 180k-world logical coordinates into a small Unity-local coordinate space.
    /// The logical grid remains authoritative; changing the presentation origin never changes a
    /// character's real GridCoord and is therefore safe for floating-origin streaming.
    /// </summary>
    public sealed class GridPresentationSpace : MonoBehaviour
    {
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private float elevationStepHeight = 1f;
        [SerializeField] private float buildingStoreyHeight = 3f;
        [SerializeField] private int originTileX;
        [SerializeField] private int originTileY;

        public float TileSize => Mathf.Max(0.01f, tileSize);
        public float ElevationStepHeight => Mathf.Max(0.01f, elevationStepHeight);
        public float BuildingStoreyHeight => Mathf.Max(0.01f, buildingStoreyHeight);
        public GridCoord OriginTile => new GridCoord(originTileX, originTileY);

        /// <summary>
        /// Presentation-only origin change. Subscribers should rebuild/refresh their Transform state;
        /// authoritative logical locations do not change.
        /// </summary>
        public event Action<GridCoord, GridCoord> OriginChanged;

        public void SetOrigin(GridCoord origin)
        {
            var before = OriginTile;
            if (before == origin) return;
            originTileX = origin.X;
            originTileY = origin.Y;
            OriginChanged?.Invoke(before, origin);
        }

        public Vector3 ToLocalPosition(GridLocation location, int logicalElevation)
        {
            var dx = location.Tile.X - originTileX;
            var dy = location.Tile.Y - originTileY;
            var height = logicalElevation * ElevationStepHeight + location.Storey * BuildingStoreyHeight;
            return new Vector3(dx * TileSize, height, dy * TileSize);
        }

        public Vector3 ToWorldPosition(GridLocation location, int logicalElevation)
            => transform.TransformPoint(ToLocalPosition(location, logicalElevation));

        public GridCoord LocalPositionToTile(Vector3 localPosition)
        {
            var x = originTileX + Mathf.RoundToInt(localPosition.x / TileSize);
            var y = originTileY + Mathf.RoundToInt(localPosition.z / TileSize);
            return new GridCoord(x, y);
        }

        public GridCoord WorldPositionToTile(Vector3 worldPosition)
            => LocalPositionToTile(transform.InverseTransformPoint(worldPosition));

        public Vector3 TileCenterWorld(GridCoord tile, float worldHeight)
        {
            var local = new Vector3(
                (tile.X - originTileX) * TileSize,
                0f,
                (tile.Y - originTileY) * TileSize);
            var world = transform.TransformPoint(local);
            world.y = worldHeight;
            return world;
        }
    }
}
