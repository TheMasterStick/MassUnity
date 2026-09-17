using System;
using MassRPG.Core.Authority;
using MassRPG.Server.Combat;
using MassRPG.Server.Construction;
using MassRPG.Server.Items;
using MassRPG.Server.Quests;
using MassRPG.Server.Travel;

namespace MassRPG.Server.Authority
{
    /// <summary>
    /// Composition layer for migrated systems that were added after the original LocalGameAuthority
    /// request switch. Unity should bind its gameplay input to this IGameAuthority gateway rather
    /// than mutating travel/ammunition/potion/quest/construction state directly. The inner authority
    /// remains the simulation host for movement, combat, skilling, inventory and economy while this
    /// gateway intercepts newer request families. The same split can later become network command routing.
    /// </summary>
    public sealed class LocalAuthorityGateway : IGameAuthority
    {
        private readonly LocalGameAuthority _inner;
        private readonly FastTravelService _fastTravel;
        private readonly FastTravelStateRegistry _travelStates;
        private readonly RangedAmmunitionService _ammunition;
        private readonly PotionConsumptionService _potions;
        private readonly QuestService _quests;
        private readonly PlotConstructionService _construction;
        private readonly PlotUpkeepService _plotUpkeep;

        public LocalAuthorityGateway(
            LocalGameAuthority inner,
            FastTravelService fastTravel = null,
            FastTravelStateRegistry travelStates = null,
            RangedAmmunitionService ammunition = null,
            PotionConsumptionService potions = null,
            QuestService quests = null,
            PlotConstructionService construction = null,
            PlotUpkeepService plotUpkeep = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _fastTravel = fastTravel;
            _travelStates = travelStates;
            _ammunition = ammunition;
            _potions = potions;
            _quests = quests;
            _construction = construction;
            _plotUpkeep = plotUpkeep;
        }

        public LocalGameAuthority Inner => _inner;

        public AuthorityDecision Submit(GameRequest request)
            => Submit(request, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public AuthorityDecision Submit(GameRequest request, long nowUnixMilliseconds)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!_inner.TryGetPlayer(request.CharacterId, out var player))
                return AuthorityDecision.Reject(request.RequestId, "unknown_character", "The character is not registered with this authority.");

            if (request is SelectRangedAmmunitionRequest selectAmmo)
            {
                if (_ammunition == null)
                    return AuthorityDecision.Reject(request.RequestId, "ammunition_unavailable", "Ranged ammunition is not initialized.");
                var result = _ammunition.Select(player, selectAmmo.AmmunitionItemId);
                return result.Success
                    ? AuthorityDecision.Accept(request.RequestId)
                    : AuthorityDecision.Reject(request.RequestId, result.Code, "The requested ammunition could not be selected.");
            }

            if (request is ClearRangedAmmunitionRequest)
            {
                if (_ammunition == null)
                    return AuthorityDecision.Reject(request.RequestId, "ammunition_unavailable", "Ranged ammunition is not initialized.");
                _ammunition.ClearSelection(player);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is DrinkPotionRequest drinkPotion)
            {
                if (_potions == null)
                    return AuthorityDecision.Reject(request.RequestId, "potions_unavailable", "Potion consumption is not initialized.");
                var result = _potions.Drink(player, drinkPotion.InventorySlot, nowUnixMilliseconds);
                if (!result.Success)
                    return AuthorityDecision.Reject(request.RequestId, result.Code, "The requested potion could not be consumed.");

                EndArrivalProtectionIfNeeded(request);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is PlaceBuildPieceRequest placeBuildPiece)
            {
                if (_construction == null)
                    return AuthorityDecision.Reject(request.RequestId, "construction_unavailable", "Construction is not initialized.");

                var result = _construction.TryPlace(
                    player,
                    placeBuildPiece.PlotId,
                    placeBuildPiece.DefinitionId,
                    placeBuildPiece.Anchor,
                    placeBuildPiece.Edge,
                    placeBuildPiece.RotationQuarterTurns);
                if (!result.Success)
                    return AuthorityDecision.Reject(request.RequestId, result.Code, "The requested build piece could not be placed.");

                EndArrivalProtectionIfNeeded(request);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is DemolishBuildPieceRequest demolishBuildPiece)
            {
                if (_construction == null)
                    return AuthorityDecision.Reject(request.RequestId, "construction_unavailable", "Construction is not initialized.");

                if (!_construction.TryDemolish(player, demolishBuildPiece.PlotId, demolishBuildPiece.PieceInstanceId))
                    return AuthorityDecision.Reject(request.RequestId, "demolish_rejected", "The requested build piece could not be demolished.");

                EndArrivalProtectionIfNeeded(request);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is PayPlotUpkeepRequest payPlotUpkeep)
            {
                if (_plotUpkeep == null)
                    return AuthorityDecision.Reject(request.RequestId, "plot_upkeep_unavailable", "Plot upkeep is not initialized.");

                var result = _plotUpkeep.Pay(player, payPlotUpkeep.PlotId, payPlotUpkeep.OfferedGold, nowUnixMilliseconds);
                if (!result.Success)
                    return AuthorityDecision.Reject(request.RequestId, result.Code, "The plot upkeep payment was rejected.");

                EndArrivalProtectionIfNeeded(request);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is StartQuestRequest startQuest)
            {
                if (_quests == null)
                    return AuthorityDecision.Reject(request.RequestId, "quests_unavailable", "Quest progression is not initialized.");
                return FromQuest(request.RequestId, _quests.TryStart(player, startQuest.QuestId));
            }

            if (request is ClaimQuestRewardRequest claimQuest)
            {
                if (_quests == null)
                    return AuthorityDecision.Reject(request.RequestId, "quests_unavailable", "Quest progression is not initialized.");
                return FromQuest(request.RequestId, _quests.TryClaim(player, claimQuest.QuestId));
            }

            if (request is ActivateFastTravelNodeRequest activate)
            {
                if (_fastTravel == null)
                    return AuthorityDecision.Reject(request.RequestId, "fast_travel_unavailable", "Fast travel is not initialized.");
                return FromTravel(request.RequestId, _fastTravel.ActivateCurrentNode(player, activate.NodeId));
            }

            if (request is OpenFastTravelMapRequest open)
            {
                if (_fastTravel == null)
                    return AuthorityDecision.Reject(request.RequestId, "fast_travel_unavailable", "Fast travel is not initialized.");
                return FromTravel(request.RequestId, _fastTravel.OpenDestinationMap(player, open.OriginNodeId, nowUnixMilliseconds));
            }

            if (request is CommitFastTravelRequest commit)
            {
                if (_fastTravel == null)
                    return AuthorityDecision.Reject(request.RequestId, "fast_travel_unavailable", "Fast travel is not initialized.");
                return FromTravel(request.RequestId, _fastTravel.CommitTravel(player, commit.DestinationNodeId, nowUnixMilliseconds));
            }

            var decision = _inner.Submit(request, nowUnixMilliseconds);
            if (decision.Accepted) EndArrivalProtectionIfNeeded(request);
            return decision;
        }

        private void EndArrivalProtectionIfNeeded(GameRequest request)
        {
            if (_travelStates != null && EndsArrivalProtection(request))
                _travelStates.GetOrCreate(request.CharacterId).ClearArrivalProtection();
        }

        private static AuthorityDecision FromTravel(Guid requestId, FastTravelResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, "Fast travel request was rejected by the authority.");

        private static AuthorityDecision FromQuest(Guid requestId, QuestOperationResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, "Quest request was rejected by the authority.");

        private static bool EndsArrivalProtection(GameRequest request)
        {
            return request is MoveToRequest
                || request is AttackCreatureRequest
                || request is GatherResourceRequest
                || request is StartProductionRequest
                || request is PlantCropRequest
                || request is HarvestCropRequest
                || request is LightFireRequest
                || request is EatFoodRequest
                || request is DrinkPotionRequest
                || request is DropInventoryItemRequest
                || request is TakeGroundItemRequest
                || request is DepositBankItemRequest
                || request is WithdrawBankItemRequest
                || request is BuyShopItemRequest
                || request is SellShopItemRequest
                || request is PlaceBuildPieceRequest
                || request is DemolishBuildPieceRequest
                || request is PayPlotUpkeepRequest;
        }
    }
}
