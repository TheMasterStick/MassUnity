using System;
using System.Collections.Generic;

namespace MassRPG.Server.Construction
{
    /// <summary>Persistent modular-building contents for one player plot.</summary>
    public sealed class PlotBuildingState
    {
        private readonly Dictionary<Guid, PlacedBuildPiece> _pieces = new Dictionary<Guid, PlacedBuildPiece>();

        public PlotBuildingState(Guid plotId)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            PlotId = plotId;
        }

        public Guid PlotId { get; }
        public IEnumerable<PlacedBuildPiece> Pieces => _pieces.Values;
        public int Count => _pieces.Count;

        public bool TryGet(Guid instanceId, out PlacedBuildPiece piece) => _pieces.TryGetValue(instanceId, out piece);

        internal void Add(PlacedBuildPiece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (_pieces.ContainsKey(piece.InstanceId)) throw new InvalidOperationException("Duplicate build piece instance id.");
            _pieces.Add(piece.InstanceId, piece);
        }

        internal bool Remove(Guid instanceId, out PlacedBuildPiece piece)
        {
            if (!_pieces.TryGetValue(instanceId, out piece)) return false;
            _pieces.Remove(instanceId);
            return true;
        }
    }
}
