using System;
using System.Collections.Generic;
using MassRPG.Core.Construction;
using MassRPG.Core.World;

namespace MassRPG.Server.Construction
{
    /// <summary>
    /// Authoritative persistent player plot. Current claimed cells are kept separate from the
    /// maximum reserved envelope so nearby plot placement can guarantee room for future Large-tier
    /// expansion without committing the exact upgrade shape up front.
    /// </summary>
    public sealed class PlayerPlotState
    {
        private readonly HashSet<GridCoord> _claimedTiles = new HashSet<GridCoord>();
        private readonly HashSet<GridCoord> _reservedTiles = new HashSet<GridCoord>();
        private readonly Dictionary<Guid, PlotAccessRuleSet> _ruleSets = new Dictionary<Guid, PlotAccessRuleSet>();
        private readonly Dictionary<Guid, Guid> _playerRuleAssignments = new Dictionary<Guid, Guid>();
        private readonly HashSet<Guid> _blockedPlayers = new HashSet<Guid>();

        public PlayerPlotState(Guid plotId, Guid ownerCharacterId, int plane, PlotTier tier = PlotTier.Small)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            if (ownerCharacterId == Guid.Empty) throw new ArgumentException("Owner id cannot be empty.", nameof(ownerCharacterId));
            PlotId = plotId;
            OwnerCharacterId = ownerCharacterId;
            Plane = plane;
            Tier = tier;
        }

        public Guid PlotId { get; }
        public Guid OwnerCharacterId { get; }
        public int Plane { get; }
        public PlotTier Tier { get; private set; }
        public IReadOnlyCollection<GridCoord> ClaimedTiles => _claimedTiles;
        public IReadOnlyCollection<GridCoord> ReservedTiles => _reservedTiles;
        public IReadOnlyCollection<Guid> BlockedPlayers => _blockedPlayers;
        public IEnumerable<PlotAccessRuleSet> RuleSets => _ruleSets.Values;

        public bool Contains(GridCoord tile) => _claimedTiles.Contains(tile);
        public bool Reserves(GridCoord tile) => _reservedTiles.Contains(tile);

        internal void SetClaimedTiles(IEnumerable<GridCoord> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _claimedTiles.Clear();
            foreach (var tile in tiles)
            {
                if (!_reservedTiles.Contains(tile))
                    throw new InvalidOperationException("Claimed plot tiles must stay inside the reserved Large-tier envelope.");
                _claimedTiles.Add(tile);
            }
            if (_claimedTiles.Count == 0) throw new InvalidOperationException("A plot must claim at least one tile.");
        }

        internal void InitializeReservedTiles(IEnumerable<GridCoord> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (_reservedTiles.Count != 0) throw new InvalidOperationException("The reserved plot envelope is immutable after placement.");
            foreach (var tile in tiles) _reservedTiles.Add(tile);
            if (_reservedTiles.Count == 0) throw new InvalidOperationException("A plot must reserve at least one tile.");
        }

        internal void ApplyUpgrade(PlotTier tier, IEnumerable<GridCoord> claimedTiles)
        {
            SetClaimedTiles(claimedTiles);
            Tier = tier;
        }

        public void UpsertRuleSet(PlotAccessRuleSet ruleSet)
        {
            if (ruleSet == null) throw new ArgumentNullException(nameof(ruleSet));
            _ruleSets[ruleSet.RuleSetId] = ruleSet;
        }

        public bool AssignRuleSet(Guid characterId, Guid ruleSetId)
        {
            if (!_ruleSets.ContainsKey(ruleSetId)) return false;
            _playerRuleAssignments[characterId] = ruleSetId;
            return true;
        }

        public void Block(Guid characterId)
        {
            if (characterId == OwnerCharacterId) throw new InvalidOperationException("The owner cannot block themselves from their plot.");
            _blockedPlayers.Add(characterId);
        }

        public void Unblock(Guid characterId) => _blockedPlayers.Remove(characterId);
        public bool IsBlocked(Guid characterId) => _blockedPlayers.Contains(characterId);

        public PlotPermission PermissionsFor(Guid characterId)
        {
            if (characterId == OwnerCharacterId) return PlotPermission.Full;
            if (IsBlocked(characterId)) return PlotPermission.None;
            if (!_playerRuleAssignments.TryGetValue(characterId, out var ruleSetId)) return PlotPermission.Enter;
            return _ruleSets.TryGetValue(ruleSetId, out var ruleSet) ? ruleSet.Permissions : PlotPermission.Enter;
        }

        public bool CanEnter(Guid characterId)
        {
            if (characterId == OwnerCharacterId) return true;
            if (IsBlocked(characterId)) return false;
            return (PermissionsFor(characterId) & PlotPermission.Enter) != 0;
        }
    }
}
