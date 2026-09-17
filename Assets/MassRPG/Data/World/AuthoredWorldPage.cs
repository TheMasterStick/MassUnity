using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World
{
    /// <summary>
    /// One loaded authoring/storage page. Only loaded pages allocate per-tile arrays; the complete
    /// 180k world is never represented as a giant in-memory tile matrix.
    /// </summary>
    public sealed class AuthoredWorldPage
    {
        private readonly List<ContentId> _groundPalette = new List<ContentId>();
        private readonly Dictionary<ContentId, ushort> _groundLookup = new Dictionary<ContentId, ushort>();
        private readonly ushort[] _groundIndices;
        private readonly short[] _elevations;
        private readonly byte[] _flags;
        private readonly byte[] _movementEdges;
        private readonly byte[] _losEdges;
        private readonly byte[] _transitionEdges;

        public AuthoredWorldPage(WorldPageKey key, int pageSize, ContentId defaultGround)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (defaultGround.IsEmpty) throw new ArgumentException("A page requires a non-empty default ground id.", nameof(defaultGround));

            Key = key;
            PageSize = pageSize;
            var count = checked(pageSize * pageSize);
            _groundIndices = new ushort[count];
            _elevations = new short[count];
            _flags = new byte[count];
            _movementEdges = new byte[count];
            _losEdges = new byte[count];
            _transitionEdges = new byte[count];
            RegisterGround(defaultGround);
        }

        public WorldPageKey Key { get; }
        public int PageSize { get; }
        public int CellCount => _groundIndices.Length;
        public IReadOnlyList<ContentId> GroundPalette => _groundPalette;

        public AuthoredTileCell GetCell(int localX, int localY) => GetCellByIndex(Index(localX, localY));

        public AuthoredTileCell GetCellByIndex(int index)
        {
            if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new AuthoredTileCell(
                _groundPalette[_groundIndices[index]],
                _elevations[index],
                (TileFlags)_flags[index],
                (CardinalEdgeMask)_movementEdges[index],
                (CardinalEdgeMask)_losEdges[index],
                (CardinalEdgeMask)_transitionEdges[index]);
        }

        public void SetCell(int localX, int localY, AuthoredTileCell cell)
            => SetCellByIndex(Index(localX, localY), cell);

        public void SetCellByIndex(int index, AuthoredTileCell cell)
        {
            if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            _groundIndices[index] = RegisterGround(cell.GroundId);
            _elevations[index] = cell.Elevation;
            _flags[index] = (byte)cell.Flags;
            _movementEdges[index] = (byte)cell.MovementBlockedEdges;
            _losEdges[index] = (byte)cell.LineOfSightBlockedEdges;
            _transitionEdges[index] = (byte)cell.ElevationTransitionEdges;
        }

        public void SetGround(int localX, int localY, ContentId groundId)
        {
            var index = Index(localX, localY);
            _groundIndices[index] = RegisterGround(groundId);
        }

        public void SetElevation(int localX, int localY, short elevation)
            => _elevations[Index(localX, localY)] = elevation;

        public void SetFlags(int localX, int localY, TileFlags flags)
            => _flags[Index(localX, localY)] = (byte)flags;

        public void SetEdgeMasks(
            int localX,
            int localY,
            CardinalEdgeMask movementBlocked,
            CardinalEdgeMask lineOfSightBlocked,
            CardinalEdgeMask elevationTransitions)
        {
            var index = Index(localX, localY);
            _movementEdges[index] = (byte)movementBlocked;
            _losEdges[index] = (byte)lineOfSightBlocked;
            _transitionEdges[index] = (byte)elevationTransitions;
        }

        private ushort RegisterGround(ContentId groundId)
        {
            if (groundId.IsEmpty) throw new ArgumentException("Ground id cannot be empty.", nameof(groundId));
            if (_groundLookup.TryGetValue(groundId, out var existing)) return existing;
            if (_groundPalette.Count >= ushort.MaxValue)
                throw new InvalidOperationException("This page exceeded the 16-bit ground palette limit.");

            var index = (ushort)_groundPalette.Count;
            _groundPalette.Add(groundId);
            _groundLookup.Add(groundId, index);
            return index;
        }

        private int Index(int localX, int localY)
        {
            if (localX < 0 || localX >= PageSize) throw new ArgumentOutOfRangeException(nameof(localX));
            if (localY < 0 || localY >= PageSize) throw new ArgumentOutOfRangeException(nameof(localY));
            return localY * PageSize + localX;
        }
    }
}
