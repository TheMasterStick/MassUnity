using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Core.Characters;
using MassRPG.Core.Construction;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Construction;

namespace MassRPG.Server.Construction
{
    public readonly struct BlueprintResolvedPiece
    {
        public BlueprintResolvedPiece(
            int sourceIndex,
            ContentId definitionId,
            GridLocation anchor,
            CardinalEdgeMask edge,
            int rotationQuarterTurns)
        {
            SourceIndex = sourceIndex;
            DefinitionId = definitionId;
            Anchor = anchor;
            Edge = edge;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public int SourceIndex { get; }
        public ContentId DefinitionId { get; }
        public GridLocation Anchor { get; }
        public CardinalEdgeMask Edge { get; }
        public int RotationQuarterTurns { get; }
    }

    public readonly struct BlueprintMaterialRequirement
    {
        public BlueprintMaterialRequirement(ContentId itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public sealed class BlueprintPlacementPreview
    {
        private BlueprintPlacementPreview(
            bool success,
            string code,
            IReadOnlyList<BlueprintResolvedPiece> pieces,
            IReadOnlyList<BlueprintMaterialRequirement> materials,
            long constructionXp)
        {
            Success = success;
            Code = code ?? string.Empty;
            Pieces = pieces ?? Array.Empty<BlueprintResolvedPiece>();
            Materials = materials ?? Array.Empty<BlueprintMaterialRequirement>();
            ConstructionXp = constructionXp;
        }

        public bool Success { get; }
        public string Code { get; }
        public IReadOnlyList<BlueprintResolvedPiece> Pieces { get; }
        public IReadOnlyList<BlueprintMaterialRequirement> Materials { get; }
        public long ConstructionXp { get; }

        public static BlueprintPlacementPreview Fail(string code)
            => new BlueprintPlacementPreview(false, code, null, null, 0);

        public static BlueprintPlacementPreview Ok(
            IReadOnlyList<BlueprintResolvedPiece> pieces,
            IReadOnlyList<BlueprintMaterialRequirement> materials,
            long constructionXp)
            => new BlueprintPlacementPreview(true, "ok", pieces, materials, constructionXp);
    }

    public sealed class BlueprintPlacementResult
    {
        private BlueprintPlacementResult(bool success, string code, IReadOnlyList<PlacedBuildPiece> pieces)
        {
            Success = success;
            Code = code ?? string.Empty;
            Pieces = pieces ?? Array.Empty<PlacedBuildPiece>();
        }

        public bool Success { get; }
        public string Code { get; }
        public IReadOnlyList<PlacedBuildPiece> Pieces { get; }

        public static BlueprintPlacementResult Fail(string code)
            => new BlueprintPlacementResult(false, code, null);

        public static BlueprintPlacementResult Ok(IReadOnlyList<PlacedBuildPiece> pieces)
            => new BlueprintPlacementResult(true, "ok", pieces);
    }

    public sealed partial class PlotConstructionService
    {
        /// <summary>
        /// Resolves and validates a complete blueprint without changing inventory, XP or persistent
        /// building state. The preview uses a temporary copy of the plot's structure and repeatedly
        /// admits pieces whose support requirements are currently satisfiable, so blueprint source
        /// order does not have to be manually floor/wall/roof order.
        /// </summary>
        public BlueprintPlacementPreview PreviewBlueprint(
            PlayerState player,
            Guid plotId,
            BuildingBlueprint blueprint,
            GridLocation origin,
            int blueprintRotationQuarterTurns = 0)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (!_plots.TryGet(plotId, out var plot)) return BlueprintPlacementPreview.Fail("unknown_plot");
            if ((plot.PermissionsFor(player.CharacterId) & PlotPermission.Build) == 0)
                return BlueprintPlacementPreview.Fail("no_build_permission");
            if (origin.Plane != plot.Plane) return BlueprintPlacementPreview.Fail("invalid_storey");

            var rotation = NormalizeRotation(blueprintRotationQuarterTurns);
            var candidates = new List<BlueprintCandidate>(blueprint.Pieces.Count);
            var aggregateCosts = new Dictionary<ContentId, int>();
            long aggregateXp = 0;

            for (var i = 0; i < blueprint.Pieces.Count; i++)
            {
                var source = blueprint.Pieces[i];
                if (!_definitions.TryGet(source.DefinitionId, out var definition))
                    return BlueprintPlacementPreview.Fail("unknown_build_piece");
                if (player.Skills.GetLevel(SkillId.Construction) < definition.ConstructionLevel)
                    return BlueprintPlacementPreview.Fail("construction_level");

                var anchor = source.Resolve(origin, rotation);
                var edge = source.ResolveEdge(rotation);
                var pieceRotation = source.ResolveRotationQuarterTurns(rotation);
                if (anchor.Storey < 0 || anchor.Storey > MaximumUsableStorey)
                    return BlueprintPlacementPreview.Fail("invalid_storey");
                if (!definition.AllowsRotation && pieceRotation != 0)
                    return BlueprintPlacementPreview.Fail("rotation_not_allowed");
                if (!ValidatePlacementMode(definition, edge))
                    return BlueprintPlacementPreview.Fail("invalid_placement_mode");

                var occupiedTiles = FootprintTiles(anchor, definition, pieceRotation);
                for (var tileIndex = 0; tileIndex < occupiedTiles.Count; tileIndex++)
                    if (!plot.Contains(occupiedTiles[tileIndex].Tile))
                        return BlueprintPlacementPreview.Fail("outside_claimed_plot");

                for (var costIndex = 0; costIndex < definition.Costs.Count; costIndex++)
                {
                    var cost = definition.Costs[costIndex];
                    if (!aggregateCosts.TryGetValue(cost.ItemId, out var current)) current = 0;
                    try { aggregateCosts[cost.ItemId] = checked(current + cost.Quantity); }
                    catch (OverflowException) { return BlueprintPlacementPreview.Fail("invalid_blueprint"); }
                }

                try { aggregateXp = checked(aggregateXp + definition.ConstructionXp); }
                catch (OverflowException) { return BlueprintPlacementPreview.Fail("invalid_blueprint"); }

                candidates.Add(new BlueprintCandidate(
                    definition,
                    new BlueprintResolvedPiece(i, definition.Id, anchor, edge, pieceRotation)));
            }

            // Preview must not create even an empty persistent building record. Use a disposable empty
            // state when this plot has never had construction before.
            var currentState = _buildings.TryGetValue(plotId, out var existingState)
                ? existingState
                : new PlotBuildingState(plotId);
            var simulated = CloneState(currentState);
            var unresolved = new List<BlueprintCandidate>(candidates);

            while (unresolved.Count > 0)
            {
                var progress = false;
                for (var i = unresolved.Count - 1; i >= 0; i--)
                {
                    var candidate = unresolved[i];
                    var piece = candidate.Resolved;
                    if (OverlapsExisting(
                        simulated,
                        candidate.Definition,
                        piece.Anchor,
                        piece.Edge,
                        piece.RotationQuarterTurns))
                        return BlueprintPlacementPreview.Fail("occupied_build_slot");

                    if (!HasRequiredSupport(
                        simulated,
                        candidate.Definition,
                        piece.Anchor,
                        piece.RotationQuarterTurns))
                        continue;

                    simulated.Add(new PlacedBuildPiece(
                        Guid.NewGuid(),
                        piece.DefinitionId,
                        piece.Anchor,
                        piece.Edge,
                        piece.RotationQuarterTurns));
                    unresolved.RemoveAt(i);
                    progress = true;
                }

                if (!progress) return BlueprintPlacementPreview.Fail("insufficient_support");
            }

            foreach (var cost in aggregateCosts)
                if (player.Inventory.CountItem(cost.Key) < cost.Value)
                    return BlueprintPlacementPreview.Fail("missing_materials");

            // Return pieces in blueprint source order for deterministic preview/UI even though the
            // internal support-validation order may have been different.
            var resolved = candidates
                .OrderBy(candidate => candidate.Resolved.SourceIndex)
                .Select(candidate => candidate.Resolved)
                .ToArray();
            var materials = aggregateCosts
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new BlueprintMaterialRequirement(pair.Key, pair.Value))
                .ToArray();
            return BlueprintPlacementPreview.Ok(resolved, materials, aggregateXp);
        }

        /// <summary>
        /// Places the full blueprint as one authoritative operation. Nothing is deducted or persisted
        /// unless PreviewBlueprint succeeds for every piece and the aggregate material requirement.
        /// </summary>
        public BlueprintPlacementResult TryPlaceBlueprint(
            PlayerState player,
            Guid plotId,
            BuildingBlueprint blueprint,
            GridLocation origin,
            int blueprintRotationQuarterTurns = 0)
        {
            var preview = PreviewBlueprint(player, plotId, blueprint, origin, blueprintRotationQuarterTurns);
            if (!preview.Success) return BlueprintPlacementResult.Fail(preview.Code);

            // Re-check aggregate inventory immediately before mutation. This method performs no
            // callbacks/yields between validation and deduction, so single-authority execution is
            // atomic with respect to its own state.
            for (var i = 0; i < preview.Materials.Count; i++)
            {
                var material = preview.Materials[i];
                if (player.Inventory.CountItem(material.ItemId) < material.Quantity)
                    return BlueprintPlacementResult.Fail("missing_materials");
            }

            for (var i = 0; i < preview.Materials.Count; i++)
            {
                var material = preview.Materials[i];
                if (!InventoryRules.RemoveItem(player.Inventory, material.ItemId, material.Quantity))
                    throw new InvalidOperationException("Blueprint material validation changed during atomic placement.");
            }

            var state = GetOrCreateState(plotId);
            var placed = new PlacedBuildPiece[preview.Pieces.Count];
            for (var i = 0; i < preview.Pieces.Count; i++)
            {
                var resolved = preview.Pieces[i];
                var piece = new PlacedBuildPiece(
                    Guid.NewGuid(),
                    resolved.DefinitionId,
                    resolved.Anchor,
                    resolved.Edge,
                    resolved.RotationQuarterTurns);
                state.Add(piece);
                placed[i] = piece;
            }

            player.Skills.AddXp(SkillId.Construction, preview.ConstructionXp);
            return BlueprintPlacementResult.Ok(placed);
        }

        private static PlotBuildingState CloneState(PlotBuildingState source)
        {
            var clone = new PlotBuildingState(source.PlotId);
            foreach (var piece in source.Pieces) clone.Add(piece);
            return clone;
        }

        private sealed class BlueprintCandidate
        {
            public BlueprintCandidate(BuildPieceDefinition definition, BlueprintResolvedPiece resolved)
            {
                Definition = definition;
                Resolved = resolved;
            }

            public BuildPieceDefinition Definition { get; }
            public BlueprintResolvedPiece Resolved { get; }
        }
    }
}
