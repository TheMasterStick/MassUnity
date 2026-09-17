using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Construction;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Construction;
using MassRPG.Server.Production;

namespace MassRPG.Server.Construction
{
    public readonly struct BuildPlacementResult
    {
        private BuildPlacementResult(bool success, string code, PlacedBuildPiece piece)
        {
            Success = success;
            Code = code ?? string.Empty;
            Piece = piece;
        }

        public bool Success { get; }
        public string Code { get; }
        public PlacedBuildPiece Piece { get; }

        public static BuildPlacementResult Ok(PlacedBuildPiece piece) => new BuildPlacementResult(true, "ok", piece);
        public static BuildPlacementResult Fail(string code) => new BuildPlacementResult(false, code, null);
    }

    /// <summary>
    /// Authoritative modular construction for persistent shared-world player plots. The first
    /// support model is deliberately simple: three usable storeys, no unsupported upper floors,
    /// and no floating fixtures/roofs. It is data/policy driven enough to be refined without
    /// changing the persistence model.
    /// </summary>
    public sealed partial class PlotConstructionService : IProductionStationSource
    {
        public const int MaximumUsableStorey = 2;

        private readonly PlayerPlotRegistry _plots;
        private readonly IBuildPieceDefinitionSource _definitions;
        private readonly IItemRuleSource _items;
        private readonly Dictionary<Guid, PlotBuildingState> _buildings = new Dictionary<Guid, PlotBuildingState>();

        public PlotConstructionService(
            PlayerPlotRegistry plots,
            IBuildPieceDefinitionSource definitions,
            IItemRuleSource items)
        {
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public PlotBuildingState GetOrCreateState(Guid plotId)
        {
            if (!_plots.TryGet(plotId, out _)) throw new KeyNotFoundException("Unknown player plot.");
            if (!_buildings.TryGetValue(plotId, out var state))
            {
                state = new PlotBuildingState(plotId);
                _buildings.Add(plotId, state);
            }
            return state;
        }

        public bool TryGetState(Guid plotId, out PlotBuildingState state) => _buildings.TryGetValue(plotId, out state);

        public BuildPlacementResult TryPlace(
            PlayerState player,
            Guid plotId,
            ContentId definitionId,
            GridLocation anchor,
            CardinalEdgeMask edge = CardinalEdgeMask.None,
            int rotationQuarterTurns = 0,
            Guid? instanceId = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_plots.TryGet(plotId, out var plot)) return BuildPlacementResult.Fail("unknown_plot");
            if ((plot.PermissionsFor(player.CharacterId) & PlotPermission.Build) == 0)
                return BuildPlacementResult.Fail("no_build_permission");
            if (!_definitions.TryGet(definitionId, out var definition)) return BuildPlacementResult.Fail("unknown_build_piece");
            if (player.Skills.GetLevel(SkillId.Construction) < definition.ConstructionLevel)
                return BuildPlacementResult.Fail("construction_level");
            if (anchor.Plane != plot.Plane || anchor.Storey < 0 || anchor.Storey > MaximumUsableStorey)
                return BuildPlacementResult.Fail("invalid_storey");
            if (!definition.AllowsRotation && NormalizeRotation(rotationQuarterTurns) != 0)
                return BuildPlacementResult.Fail("rotation_not_allowed");
            if (!ValidatePlacementMode(definition, edge)) return BuildPlacementResult.Fail("invalid_placement_mode");

            var state = GetOrCreateState(plotId);
            var occupiedTiles = FootprintTiles(anchor, definition, rotationQuarterTurns);
            for (var i = 0; i < occupiedTiles.Count; i++)
            {
                if (!plot.Contains(occupiedTiles[i].Tile)) return BuildPlacementResult.Fail("outside_claimed_plot");
            }

            if (OverlapsExisting(state, definition, anchor, edge, rotationQuarterTurns))
                return BuildPlacementResult.Fail("occupied_build_slot");
            if (!HasRequiredSupport(state, definition, anchor, rotationQuarterTurns))
                return BuildPlacementResult.Fail("insufficient_support");
            if (!HasMaterials(player, definition)) return BuildPlacementResult.Fail("missing_materials");

            for (var i = 0; i < definition.Costs.Count; i++)
            {
                var cost = definition.Costs[i];
                if (!InventoryRules.RemoveItem(player.Inventory, cost.ItemId, cost.Quantity))
                    throw new InvalidOperationException("Construction material validation changed during atomic placement.");
            }

            var piece = new PlacedBuildPiece(
                instanceId ?? Guid.NewGuid(),
                definition.Id,
                anchor,
                edge,
                rotationQuarterTurns);
            state.Add(piece);
            player.Skills.AddXp(SkillId.Construction, definition.ConstructionXp);
            return BuildPlacementResult.Ok(piece);
        }

        public bool TryDemolish(PlayerState player, Guid plotId, Guid pieceInstanceId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_plots.TryGet(plotId, out var plot)) return false;
            if ((plot.PermissionsFor(player.CharacterId) & PlotPermission.Demolish) == 0) return false;
            return _buildings.TryGetValue(plotId, out var state) && state.Remove(pieceInstanceId, out _);
        }

        public bool IsStationAt(ContentId stationId, GridLocation location)
        {
            foreach (var state in _buildings.Values)
            {
                foreach (var piece in state.Pieces)
                {
                    if (piece.Anchor != location) continue;
                    if (!_definitions.TryGet(piece.DefinitionId, out var definition)) continue;
                    if (definition.StationId.HasValue && definition.StationId.Value == stationId) return true;
                }
            }
            return false;
        }

        private bool HasMaterials(PlayerState player, BuildPieceDefinition definition)
        {
            for (var i = 0; i < definition.Costs.Count; i++)
            {
                var cost = definition.Costs[i];
                if (player.Inventory.CountItem(cost.ItemId) < cost.Quantity) return false;
            }
            return true;
        }

        private bool HasRequiredSupport(
            PlotBuildingState state,
            BuildPieceDefinition definition,
            GridLocation anchor,
            int rotationQuarterTurns)
        {
            var requirement = definition.SupportRequirement;
            if ((requirement & BuildSupportRequirement.GroundOnly) != 0 && anchor.Storey != 0) return false;

            var tiles = FootprintTiles(anchor, definition, rotationQuarterTurns);

            if (definition.Kind == BuildPieceKind.Floor && anchor.Storey > 0)
            {
                // A deliberately simple anti-floating rule: every upper-floor tile needs at least
                // two wall/door support edges on the storey beneath it. One isolated pillar cannot
                // hold up an entire floating floor plate.
                for (var i = 0; i < tiles.Count; i++)
                    if (CountSupportingEdges(state, tiles[i], anchor.Storey - 1) < 2) return false;
            }

            if ((requirement & BuildSupportRequirement.GroundOrFloor) != 0 && anchor.Storey > 0)
            {
                for (var i = 0; i < tiles.Count; i++)
                    if (!HasFloorAt(state, tiles[i])) return false;
            }

            if ((requirement & BuildSupportRequirement.FloorBelow) != 0 && anchor.Storey > 0)
            {
                for (var i = 0; i < tiles.Count; i++)
                    if (CountSupportingEdges(state, tiles[i], anchor.Storey - 1) < 2) return false;
            }

            if ((requirement & BuildSupportRequirement.AdjacentWall) != 0)
            {
                for (var i = 0; i < tiles.Count; i++)
                    if (CountSupportingEdges(state, tiles[i], anchor.Storey) < 1) return false;
            }

            if (definition.Kind == BuildPieceKind.Stairs && anchor.Storey >= MaximumUsableStorey)
                return false;

            return true;
        }

        private bool HasFloorAt(PlotBuildingState state, GridLocation location)
        {
            foreach (var piece in state.Pieces)
            {
                if (!_definitions.TryGet(piece.DefinitionId, out var definition)) continue;
                if (definition.Kind != BuildPieceKind.Floor || piece.Anchor.Storey != location.Storey || piece.Anchor.Plane != location.Plane)
                    continue;
                var tiles = FootprintTiles(piece.Anchor, definition, piece.RotationQuarterTurns);
                for (var i = 0; i < tiles.Count; i++)
                    if (tiles[i] == location) return true;
            }
            return false;
        }

        private int CountSupportingEdges(PlotBuildingState state, GridLocation tile, int storey)
        {
            var count = 0;
            foreach (var piece in state.Pieces)
            {
                if (piece.Anchor.Plane != tile.Plane || piece.Anchor.Storey != storey) continue;
                if (!_definitions.TryGet(piece.DefinitionId, out var definition)) continue;
                if (definition.Kind != BuildPieceKind.Wall && definition.Kind != BuildPieceKind.Doorway) continue;
                if (piece.Edge == CardinalEdgeMask.None) continue;
                if (EdgeTouchesTile(piece.Anchor.Tile, piece.Edge, tile.Tile)) count++;
            }
            return count;
        }

        private bool OverlapsExisting(
            PlotBuildingState state,
            BuildPieceDefinition definition,
            GridLocation anchor,
            CardinalEdgeMask edge,
            int rotationQuarterTurns)
        {
            foreach (var existing in state.Pieces)
            {
                if (!_definitions.TryGet(existing.DefinitionId, out var existingDefinition)) continue;
                if (existing.Anchor.Plane != anchor.Plane || existing.Anchor.Storey != anchor.Storey) continue;
                if (existingDefinition.OccupancyLayer != definition.OccupancyLayer) continue;

                if (definition.PlacementMode == BuildPlacementMode.CardinalEdge
                    && existingDefinition.PlacementMode == BuildPlacementMode.CardinalEdge)
                {
                    if (CanonicalEdge(existing.Anchor.Tile, existing.Edge).Equals(CanonicalEdge(anchor.Tile, edge))) return true;
                    continue;
                }

                if (definition.PlacementMode == BuildPlacementMode.Tile
                    && existingDefinition.PlacementMode == BuildPlacementMode.Tile)
                {
                    var a = FootprintTiles(anchor, definition, rotationQuarterTurns);
                    var b = FootprintTiles(existing.Anchor, existingDefinition, existing.RotationQuarterTurns);
                    for (var i = 0; i < a.Count; i++)
                        for (var j = 0; j < b.Count; j++)
                            if (a[i] == b[j]) return true;
                }
            }
            return false;
        }

        private static bool ValidatePlacementMode(BuildPieceDefinition definition, CardinalEdgeMask edge)
        {
            if (definition.PlacementMode == BuildPlacementMode.Tile) return edge == CardinalEdgeMask.None;
            return IsSingleCardinalEdge(edge) && definition.FootprintWidth == 1 && definition.FootprintHeight == 1;
        }

        private static List<GridLocation> FootprintTiles(
            GridLocation anchor,
            BuildPieceDefinition definition,
            int rotationQuarterTurns)
        {
            var rotation = NormalizeRotation(rotationQuarterTurns);
            var width = rotation % 2 == 0 ? definition.FootprintWidth : definition.FootprintHeight;
            var height = rotation % 2 == 0 ? definition.FootprintHeight : definition.FootprintWidth;
            var result = new List<GridLocation>(width * height);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    result.Add(new GridLocation(
                        new GridCoord(anchor.Tile.X + x, anchor.Tile.Y + y),
                        anchor.Plane,
                        anchor.Storey));
            return result;
        }

        private static bool EdgeTouchesTile(GridCoord anchor, CardinalEdgeMask edge, GridCoord tile)
        {
            if (anchor == tile) return true;
            return AdjacentAcrossEdge(anchor, edge) == tile;
        }

        private static GridCoord AdjacentAcrossEdge(GridCoord tile, CardinalEdgeMask edge)
        {
            switch (edge)
            {
                case CardinalEdgeMask.North: return new GridCoord(tile.X, tile.Y - 1);
                case CardinalEdgeMask.East: return new GridCoord(tile.X + 1, tile.Y);
                case CardinalEdgeMask.South: return new GridCoord(tile.X, tile.Y + 1);
                case CardinalEdgeMask.West: return new GridCoord(tile.X - 1, tile.Y);
                default: return tile;
            }
        }

        private static CanonicalBuildEdge CanonicalEdge(GridCoord tile, CardinalEdgeMask edge)
        {
            switch (edge)
            {
                case CardinalEdgeMask.East: return new CanonicalBuildEdge(tile, CardinalEdgeMask.East);
                case CardinalEdgeMask.West: return new CanonicalBuildEdge(new GridCoord(tile.X - 1, tile.Y), CardinalEdgeMask.East);
                case CardinalEdgeMask.South: return new CanonicalBuildEdge(tile, CardinalEdgeMask.South);
                case CardinalEdgeMask.North: return new CanonicalBuildEdge(new GridCoord(tile.X, tile.Y - 1), CardinalEdgeMask.South);
                default: return new CanonicalBuildEdge(tile, CardinalEdgeMask.None);
            }
        }

        private static bool IsSingleCardinalEdge(CardinalEdgeMask edge)
            => edge == CardinalEdgeMask.North || edge == CardinalEdgeMask.East
                || edge == CardinalEdgeMask.South || edge == CardinalEdgeMask.West;

        private static int NormalizeRotation(int quarterTurns)
        {
            var value = quarterTurns % 4;
            return value < 0 ? value + 4 : value;
        }

        private readonly struct CanonicalBuildEdge : IEquatable<CanonicalBuildEdge>
        {
            public CanonicalBuildEdge(GridCoord tile, CardinalEdgeMask orientation)
            {
                Tile = tile;
                Orientation = orientation;
            }

            public GridCoord Tile { get; }
            public CardinalEdgeMask Orientation { get; }
            public bool Equals(CanonicalBuildEdge other) => Tile == other.Tile && Orientation == other.Orientation;
            public override bool Equals(object obj) => obj is CanonicalBuildEdge other && Equals(other);
            public override int GetHashCode() => unchecked((Tile.GetHashCode() * 397) ^ (int)Orientation);
        }
    }
}
