using System;
using System.Collections.Generic;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using MassRPG.Server.Death;
using MassRPG.Server.Economy;
using MassRPG.Server.Farming;
using MassRPG.Server.Firemaking;
using MassRPG.Server.Items;
using MassRPG.Server.Production;
using MassRPG.Server.Resources;

namespace MassRPG.Server.Authority
{
    /// <summary>
    /// First authoritative simulation host. Runs locally/in-process now, but the Unity client
    /// must still submit requests instead of mutating player state directly.
    /// </summary>
    public sealed class LocalGameAuthority : IGameAuthority
    {
        private readonly Dictionary<Guid, PlayerState> _players = new Dictionary<Guid, PlayerState>();
        private readonly IItemRuleSource _itemRules;
        private readonly IGridTraversalMap _movementMap;
        private readonly GatheringService _gathering;
        private readonly CombatTargetingService _combatTargeting;
        private readonly CombatSimulationService _combatSimulation;
        private readonly ProductionService _production;
        private readonly CreatureRegistry _creatures;
        private readonly CreatureCombatSimulationService _creatureCombat;
        private readonly CreaturePopulationService _creaturePopulations;
        private readonly BankService _banking;
        private readonly CharacterBankRegistry _banks;
        private readonly ShopService _shops;
        private readonly IEconomyAccessSource _economyAccess;
        private readonly FarmingService _farming;
        private readonly FiremakingService _firemaking;
        private readonly GroundItemService _groundItems;
        private readonly FoodConsumptionService _food;
        private readonly PlayerDeathService _death;
        private readonly CombatKillSettlementCoordinator _killSettlement;
        private readonly RangedAmmunitionService _rangedAmmunition;

        public LocalGameAuthority(
            IItemRuleSource itemRules,
            IGridTraversalMap movementMap = null,
            GatheringService gathering = null,
            CombatTargetingService combatTargeting = null,
            CombatSimulationService combatSimulation = null,
            ProductionService production = null,
            CreatureRegistry creatures = null,
            CreatureCombatSimulationService creatureCombat = null,
            CreaturePopulationService creaturePopulations = null,
            BankService banking = null,
            CharacterBankRegistry banks = null,
            ShopService shops = null,
            IEconomyAccessSource economyAccess = null,
            FarmingService farming = null,
            FiremakingService firemaking = null,
            GroundItemService groundItems = null,
            FoodConsumptionService food = null,
            PlayerDeathService death = null,
            CombatKillSettlementCoordinator killSettlement = null,
            RangedAmmunitionService rangedAmmunition = null)
        {
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
            _movementMap = movementMap;
            _gathering = gathering;
            _combatTargeting = combatTargeting;
            _combatSimulation = combatSimulation;
            _production = production;
            _creatures = creatures;
            _creatureCombat = creatureCombat;
            _creaturePopulations = creaturePopulations;
            _banking = banking;
            _banks = banks;
            _shops = shops;
            _economyAccess = economyAccess;
            _farming = farming;
            _firemaking = firemaking;
            _groundItems = groundItems;
            _food = food;
            _death = death;
            _killSettlement = killSettlement;
            _rangedAmmunition = rangedAmmunition;
        }

        /// <summary>
        /// Raised synchronously after a lethal player attack is handed to the configured kill
        /// settlement coordinator. Presentation/telemetry can observe the immutable result without
        /// becoming responsible for XP, loot or population removal.
        /// </summary>
        public event Action<Guid, CombatKillSettlementResult> CombatKillSettled;

        public void RegisterPlayer(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            _players[player.CharacterId] = player;
        }

        public bool TryGetPlayer(Guid characterId, out PlayerState player) => _players.TryGetValue(characterId, out player);

        public AuthorityDecision Submit(GameRequest request)
            => Submit(request, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public AuthorityDecision Submit(GameRequest request, long nowUnixMilliseconds)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!_players.TryGetValue(request.CharacterId, out var player))
                return AuthorityDecision.Reject(request.RequestId, "unknown_character", "The character is not registered with this authority.");

            if (request is MoveInventorySlotRequest move)
                return FromInventoryResult(request.RequestId, InventoryRules.MoveSlot(player.Inventory, _itemRules, move.FromIndex, move.ToIndex));

            if (request is EquipInventoryItemRequest equip)
                return HandleEquipRequest(request.RequestId, player, equip);

            if (request is UnequipItemRequest unequip)
            {
                if (player.Combat.IsActive && !CanSwapDuringCombat(unequip.Slot))
                    return AuthorityDecision.Reject(request.RequestId, "equipment_locked_in_combat", "Armor and accessories cannot be changed during combat.");
                return FromInventoryResult(request.RequestId,
                    InventoryRules.Unequip(player.Inventory, player.Equipment, _itemRules, unequip.Slot));
            }

            if (request is EatFoodRequest eat)
            {
                if (_food == null)
                    return AuthorityDecision.Reject(request.RequestId, "food_unavailable", "Food consumption is not initialized.");
                return FromFoodResult(request.RequestId, _food.Eat(player, eat.InventorySlot));
            }

            if (request is DropInventoryItemRequest drop)
            {
                if (_groundItems == null)
                    return AuthorityDecision.Reject(request.RequestId, "ground_items_unavailable", "Ground items are not initialized.");
                return FromGroundItemResult(request.RequestId,
                    _groundItems.DropFromInventory(player, drop.InventorySlot, drop.Quantity, nowUnixMilliseconds));
            }

            if (request is TakeGroundItemRequest take)
            {
                if (_groundItems == null)
                    return AuthorityDecision.Reject(request.RequestId, "ground_items_unavailable", "Ground items are not initialized.");
                return FromGroundItemResult(request.RequestId,
                    _groundItems.Take(player, take.GroundItemId, nowUnixMilliseconds));
            }

            if (request is MoveToRequest moveTo)
                return HandleMoveTo(request.RequestId, player, moveTo.Destination);

            if (request is CancelMovementRequest)
            {
                player.Movement.Clear();
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is GatherResourceRequest gather)
            {
                if (_gathering == null)
                    return AuthorityDecision.Reject(request.RequestId, "gathering_unavailable", "Gathering is not initialized.");
                return FromGatheringResult(request.RequestId, _gathering.TryGather(player, gather.Node, nowUnixMilliseconds));
            }

            if (request is PlantCropRequest plant)
            {
                if (_farming == null)
                    return AuthorityDecision.Reject(request.RequestId, "farming_unavailable", "Farming is not initialized.");
                return FromFarmingResult(request.RequestId, _farming.Plant(player, plant.Patch, plant.CropId, nowUnixMilliseconds));
            }

            if (request is HarvestCropRequest harvest)
            {
                if (_farming == null)
                    return AuthorityDecision.Reject(request.RequestId, "farming_unavailable", "Farming is not initialized.");
                return FromFarmingResult(request.RequestId, _farming.Harvest(player, harvest.Patch, nowUnixMilliseconds));
            }

            if (request is LightFireRequest lightFire)
            {
                if (_firemaking == null)
                    return AuthorityDecision.Reject(request.RequestId, "firemaking_unavailable", "Firemaking is not initialized.");
                return FromFiremakingResult(request.RequestId, _firemaking.Light(player, lightFire.LogItemId, nowUnixMilliseconds));
            }

            if (request is DepositBankItemRequest deposit)
            {
                if (_banking == null || _banks == null || _economyAccess == null)
                    return AuthorityDecision.Reject(request.RequestId, "banking_unavailable", "Banking is not initialized.");
                if (!_economyAccess.CanUseBank(player, deposit.BankLocation))
                    return AuthorityDecision.Reject(request.RequestId, "bank_out_of_range", "You must be beside a valid bank to do that.");
                return FromBankResult(request.RequestId, _banking.Deposit(
                    player, _banks.GetOrCreate(player.CharacterId), deposit.InventorySlot, deposit.Quantity));
            }

            if (request is WithdrawBankItemRequest withdraw)
            {
                if (_banking == null || _banks == null || _economyAccess == null)
                    return AuthorityDecision.Reject(request.RequestId, "banking_unavailable", "Banking is not initialized.");
                if (!_economyAccess.CanUseBank(player, withdraw.BankLocation))
                    return AuthorityDecision.Reject(request.RequestId, "bank_out_of_range", "You must be beside a valid bank to do that.");
                return FromBankResult(request.RequestId, _banking.Withdraw(
                    player, _banks.GetOrCreate(player.CharacterId), withdraw.ItemId, withdraw.Quantity));
            }

            if (request is BuyShopItemRequest buy)
            {
                if (_shops == null || _economyAccess == null)
                    return AuthorityDecision.Reject(request.RequestId, "shops_unavailable", "Shops are not initialized.");
                if (!_economyAccess.CanUseShop(player, buy.ShopId, buy.ShopLocation))
                    return AuthorityDecision.Reject(request.RequestId, "shop_out_of_range", "You must be beside the requested shop to do that.");
                return FromShopResult(request.RequestId, _shops.Buy(player, buy.ShopId, buy.ItemId, buy.Quantity));
            }

            if (request is SellShopItemRequest sell)
            {
                if (_shops == null || _economyAccess == null)
                    return AuthorityDecision.Reject(request.RequestId, "shops_unavailable", "Shops are not initialized.");
                if (!_economyAccess.CanUseShop(player, sell.ShopId, sell.ShopLocation))
                    return AuthorityDecision.Reject(request.RequestId, "shop_out_of_range", "You must be beside the requested shop to do that.");
                return FromShopResult(request.RequestId, _shops.Sell(player, sell.ShopId, sell.InventorySlot, sell.Quantity));
            }

            if (request is StartProductionRequest startProduction)
            {
                if (_production == null)
                    return AuthorityDecision.Reject(request.RequestId, "production_unavailable", "Production is not initialized.");
                return FromProductionResult(request.RequestId, _production.TryStart(
                    player,
                    startProduction.RecipeId,
                    startProduction.Quantity,
                    startProduction.StationLocation,
                    nowUnixMilliseconds));
            }

            if (request is CancelProductionRequest)
            {
                if (_production != null) _production.Cancel(player);
                else player.Production.Clear();
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is SelectCombatStyleRequest selectCombatStyle)
            {
                player.CombatStyle = selectCombatStyle.Style;
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is SelectMeleeTrainingStyleRequest selectMeleeTraining)
            {
                player.MeleeTrainingStyle = selectMeleeTraining.Style;
                player.CombatStyle = CombatStyle.Melee;
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is SelectRangedAmmunitionRequest selectAmmunition)
            {
                if (_rangedAmmunition == null)
                    return AuthorityDecision.Reject(request.RequestId, "ranged_ammunition_unavailable", "Ranged ammunition selection is not initialized.");
                return FromRangedAmmunitionResult(request.RequestId,
                    _rangedAmmunition.Select(player, selectAmmunition.AmmunitionItemId));
            }

            if (request is ClearRangedAmmunitionRequest)
            {
                if (_rangedAmmunition == null)
                    return AuthorityDecision.Reject(request.RequestId, "ranged_ammunition_unavailable", "Ranged ammunition selection is not initialized.");
                _rangedAmmunition.ClearSelection(player);
                return AuthorityDecision.Accept(request.RequestId);
            }

            if (request is AttackCreatureRequest attack)
            {
                if (_combatTargeting == null)
                    return AuthorityDecision.Reject(request.RequestId, "combat_unavailable", "Combat targeting is not initialized.");
                player.Production.Clear();
                return FromCombatTargetingResult(request.RequestId, _combatTargeting.BeginAttack(player, attack.CreatureInstanceId));
            }

            if (request is StopCombatRequest)
            {
                if (_combatTargeting != null) _combatTargeting.Stop(player);
                else
                {
                    player.Combat.End();
                    player.Movement.Clear();
                }
                return AuthorityDecision.Accept(request.RequestId);
            }

            return AuthorityDecision.Reject(request.RequestId, "unsupported_request", "This request type is not implemented by the local authority yet.");
        }

        public bool AdvanceMovementOneStep(Guid characterId)
        {
            if (_movementMap == null) return false;
            if (!_players.TryGetValue(characterId, out var player)) return false;
            if (!player.Movement.TryPeekNext(out var next)) return false;

            if (!GridTraversal.CanStep(_movementMap, player.Location, next))
            {
                player.Movement.Clear();
                return false;
            }

            player.Movement.TryConsumeNext(out next);
            player.Location = next;
            return true;
        }

        public int AdvanceAllMovementOneStep()
        {
            var moved = 0;
            foreach (var player in _players.Values)
                if (AdvanceMovementOneStep(player.CharacterId)) moved++;
            return moved;
        }

        public CombatAdvanceResult AdvanceCombat(Guid characterId, long nowUnixMilliseconds, Func<double> random01)
        {
            if (_combatSimulation == null)
                return CombatAdvanceResult.State(CombatAdvanceKind.Failed, "combat_simulation_unavailable");
            if (!_players.TryGetValue(characterId, out var player))
                return CombatAdvanceResult.State(CombatAdvanceKind.Failed, "unknown_character");
            if (random01 == null) throw new ArgumentNullException(nameof(random01));

            var targetBeforeAttack = player.Combat.TargetActorId;
            var result = _combatSimulation.AdvancePlayerAttack(player, nowUnixMilliseconds, random01);
            if (result.Kind == CombatAdvanceKind.TargetKilled && targetBeforeAttack.HasValue)
                HandleCreatureKilled(targetBeforeAttack.Value, nowUnixMilliseconds, random01);
            return result;
        }

        public int AdvanceAllCombat(long nowUnixMilliseconds, Func<double> random01)
        {
            if (_combatSimulation == null) return 0;
            if (random01 == null) throw new ArgumentNullException(nameof(random01));

            var attacksResolved = 0;
            foreach (var player in _players.Values)
            {
                var targetBeforeAttack = player.Combat.TargetActorId;
                var result = _combatSimulation.AdvancePlayerAttack(player, nowUnixMilliseconds, random01);
                if (result.DidAttack) attacksResolved++;
                if (result.Kind == CombatAdvanceKind.TargetKilled && targetBeforeAttack.HasValue)
                    HandleCreatureKilled(targetBeforeAttack.Value, nowUnixMilliseconds, random01);
            }
            return attacksResolved;
        }

        public CreatureAdvanceResult AdvanceCreatureCombat(Guid creatureInstanceId, long nowUnixMilliseconds, Func<double> random01)
        {
            if (_creatureCombat == null || _creatures == null)
                return new CreatureAdvanceResult(CreatureAdvanceKind.Failed, "creature_combat_unavailable");
            if (!_creatures.TryGet(creatureInstanceId, out var creature))
                return new CreatureAdvanceResult(CreatureAdvanceKind.Failed, "unknown_creature");

            if (!creature.TargetCharacterId.HasValue)
            {
                var acquisition = _creatureCombat.TryAcquireAggro(creature, _players.Values);
                if (acquisition.Kind == CreatureAdvanceKind.AcquiredTarget) return acquisition;
            }

            if (!creature.TargetCharacterId.HasValue)
                return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "no_target");
            if (!_players.TryGetValue(creature.TargetCharacterId.Value, out var target))
            {
                creature.TargetCharacterId = null;
                return new CreatureAdvanceResult(CreatureAdvanceKind.GaveUp, "target_unavailable");
            }

            var result = _creatureCombat.Advance(creature, target, nowUnixMilliseconds, random01);
            if (result.Kind == CreatureAdvanceKind.TargetKilled)
                _death?.ResolvePveDeath(target, nowUnixMilliseconds);
            return result;
        }

        public int AdvanceAllCreatureCombat(long nowUnixMilliseconds, Func<double> random01)
        {
            if (_creatureCombat == null || _creatures == null) return 0;
            if (random01 == null) throw new ArgumentNullException(nameof(random01));

            var meaningfulChanges = 0;
            var snapshot = new List<CreatureState>(_creatures.All);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var result = AdvanceCreatureCombat(snapshot[i].InstanceId, nowUnixMilliseconds, random01);
                if (result.Kind != CreatureAdvanceKind.Idle && result.Kind != CreatureAdvanceKind.WaitingForCooldown)
                    meaningfulChanges++;
            }
            return meaningfulChanges;
        }

        public ProductionResult AdvanceProduction(Guid characterId, long nowUnixMilliseconds)
        {
            if (_production == null) return ProductionResult.Fail("production_unavailable");
            if (!_players.TryGetValue(characterId, out var player)) return ProductionResult.Fail("unknown_character");
            return _production.Advance(player, nowUnixMilliseconds);
        }

        public int AdvanceAllProduction(long nowUnixMilliseconds)
        {
            if (_production == null) return 0;
            var completed = 0;
            foreach (var player in _players.Values)
            {
                var result = _production.Advance(player, nowUnixMilliseconds);
                completed += result.CompletedQuantity;
            }
            return completed;
        }

        private void HandleCreatureKilled(Guid creatureInstanceId, long nowUnixMilliseconds, Func<double> random01)
        {
            if (_killSettlement == null)
            {
                _creaturePopulations?.RecordKillForInstance(creatureInstanceId, nowUnixMilliseconds);
                return;
            }

            var settlement = _killSettlement.SettleKilledCreature(
                creatureInstanceId,
                _players.Values,
                nowUnixMilliseconds,
                random01);
            CombatKillSettled?.Invoke(creatureInstanceId, settlement);
        }

        private AuthorityDecision HandleEquipRequest(Guid requestId, PlayerState player, EquipInventoryItemRequest equip)
        {
            if (player.Combat.IsActive)
            {
                var stack = player.Inventory.GetSlot(equip.InventoryIndex);
                if (stack != null && _itemRules.TryGetRule(stack.ItemId, out var rule))
                {
                    var target = InventoryRules.ResolveEquipmentTargetSlot(player.Equipment, rule, equip.RequestedSlot);
                    if (target.HasValue && !CanSwapDuringCombat(target.Value))
                        return AuthorityDecision.Reject(requestId, "equipment_locked_in_combat", "Armor and accessories cannot be changed during combat.");
                }
            }

            return FromInventoryResult(requestId,
                InventoryRules.EquipFromInventory(player.Inventory, player.Equipment, _itemRules, player.Skills, equip.InventoryIndex, equip.RequestedSlot));
        }

        private AuthorityDecision HandleMoveTo(Guid requestId, PlayerState player, GridLocation destination)
        {
            if (_movementMap == null)
                return AuthorityDecision.Reject(requestId, "movement_unavailable", "No authoritative movement map is loaded.");
            if (!WorldConstants.IsInsideWorld(destination.Tile))
                return AuthorityDecision.Reject(requestId, "out_of_bounds", "The requested destination is outside the world.");
            if (!player.Location.SameLayer(destination))
                return AuthorityDecision.Reject(requestId, "transition_required", "Changing plane or building floor requires an explicit traversal connection.");

            var path = GridPathfinder.FindPath(_movementMap, player.Location, destination);
            if (!path.Success)
                return AuthorityDecision.Reject(requestId, path.Code, "No valid local path could be found to that destination.");

            player.Production.Clear();
            player.Combat.End();
            player.Movement.ReplacePath(path.Steps);
            return AuthorityDecision.Accept(requestId);
        }

        private static bool CanSwapDuringCombat(EquipmentSlot slot)
            => slot == EquipmentSlot.Weapon || slot == EquipmentSlot.Shield;

        private static AuthorityDecision FromInventoryResult(Guid requestId, InventoryOperationResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Message);

        private static AuthorityDecision FromGatheringResult(Guid requestId, GatheringResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Message);

        private static AuthorityDecision FromCombatTargetingResult(Guid requestId, CombatTargetingResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Message);

        private static AuthorityDecision FromRangedAmmunitionResult(Guid requestId, RangedAmmunitionResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromProductionResult(Guid requestId, ProductionResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromBankResult(Guid requestId, BankTransactionResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromShopResult(Guid requestId, ShopTransactionResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromFarmingResult(Guid requestId, FarmingResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromFiremakingResult(Guid requestId, FiremakingResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromGroundItemResult(Guid requestId, GroundItemResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);

        private static AuthorityDecision FromFoodResult(Guid requestId, FoodConsumptionResult result)
            => result.Success
                ? AuthorityDecision.Accept(requestId)
                : AuthorityDecision.Reject(requestId, result.Code, result.Code);
    }
}
