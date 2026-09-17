using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Creatures;
using MassRPG.Data.Loot;
using MassRPG.Server.Creatures;
using MassRPG.Server.Items;
using MassRPG.Server.Loot;
using MassRPG.Server.Parties;

namespace MassRPG.Server.Combat
{
    public sealed class CombatKillSettlementResult
    {
        private readonly List<Guid> _spawnedGroundItemIds;

        private CombatKillSettlementResult(
            bool success,
            string code,
            CombatRewardPlan rewardPlan,
            CombatExperienceSettlementResult experience,
            LootRollResult loot,
            Guid? partyLootPoolId,
            List<Guid> spawnedGroundItemIds)
        {
            Success = success;
            Code = code ?? string.Empty;
            RewardPlan = rewardPlan;
            Experience = experience;
            Loot = loot;
            PartyLootPoolId = partyLootPoolId;
            _spawnedGroundItemIds = spawnedGroundItemIds ?? new List<Guid>();
        }

        public bool Success { get; }
        public string Code { get; }
        public CombatRewardPlan RewardPlan { get; }
        public CombatExperienceSettlementResult Experience { get; }
        public LootRollResult Loot { get; }
        public Guid? PartyLootPoolId { get; }
        public IReadOnlyList<Guid> SpawnedGroundItemIds => _spawnedGroundItemIds;

        internal static CombatKillSettlementResult Fail(string code)
            => new CombatKillSettlementResult(false, code, null, null, null, null, new List<Guid>());

        internal static CombatKillSettlementResult Ok(
            CombatRewardPlan rewardPlan,
            CombatExperienceSettlementResult experience,
            LootRollResult loot,
            Guid? partyLootPoolId,
            List<Guid> spawnedGroundItemIds)
            => new CombatKillSettlementResult(true, "ok", rewardPlan, experience, loot, partyLootPoolId, spawnedGroundItemIds);
    }

    /// <summary>
    /// Settles one ordinary creature death from authoritative contribution facts. Combat XP is
    /// granted to all eligible reward groups, but item/money claim follows the settled first-engager
    /// rule. A formal party receives one shared classic loot pool using its selected loot mode;
    /// money is split evenly first and only the indivisible remainder enters that shared pool.
    /// Solo claim loot becomes protected ground items. No personal-loot copies are created.
    /// </summary>
    public sealed class CombatKillSettlementService
    {
        private static readonly ContentId CoinId = new ContentId("coins");

        private readonly ICreatureDefinitionSource _creatures;
        private readonly ILootTableSource _lootTables;
        private readonly IItemRuleSource _items;
        private readonly CombatContributionLedger _contributions;
        private readonly CombatRewardPlanner _rewardPlanner;
        private readonly CombatExperienceSettlementService _experience;
        private readonly PartyRegistry _parties;
        private readonly PartyLootPoolService _partyLoot;
        private readonly GroundItemService _groundItems;
        private readonly long _lootPublicDelayMilliseconds;
        private readonly long _lootLifetimeMilliseconds;

        public CombatKillSettlementService(
            ICreatureDefinitionSource creatures,
            ILootTableSource lootTables,
            IItemRuleSource items,
            CombatContributionLedger contributions,
            CombatRewardPlanner rewardPlanner,
            CombatExperienceSettlementService experience,
            PartyRegistry parties,
            PartyLootPoolService partyLoot,
            GroundItemService groundItems,
            long lootPublicDelayMilliseconds,
            long lootLifetimeMilliseconds)
        {
            _creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
            _lootTables = lootTables ?? throw new ArgumentNullException(nameof(lootTables));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));
            _rewardPlanner = rewardPlanner ?? throw new ArgumentNullException(nameof(rewardPlanner));
            _experience = experience ?? throw new ArgumentNullException(nameof(experience));
            _parties = parties ?? throw new ArgumentNullException(nameof(parties));
            _partyLoot = partyLoot ?? throw new ArgumentNullException(nameof(partyLoot));
            _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
            if (lootPublicDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(lootPublicDelayMilliseconds));
            if (lootLifetimeMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(lootLifetimeMilliseconds));
            _lootPublicDelayMilliseconds = lootPublicDelayMilliseconds;
            _lootLifetimeMilliseconds = lootLifetimeMilliseconds;
        }

        public CombatKillSettlementResult Settle(
            CreatureState creature,
            IEnumerable<CombatRewardPresence> presences,
            long nowUnixMilliseconds,
            Func<double> random01,
            Func<Guid, PlayerState> resolveCharacter)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (presences == null) throw new ArgumentNullException(nameof(presences));
            if (random01 == null) throw new ArgumentNullException(nameof(random01));
            if (resolveCharacter == null) throw new ArgumentNullException(nameof(resolveCharacter));
            if (creature.IsAlive) return CombatKillSettlementResult.Fail("target_alive");
            if (!_creatures.TryGet(creature.DefinitionId, out var definition))
                return CombatKillSettlementResult.Fail("unknown_creature_definition");
            if (!_contributions.TrySnapshot(creature.InstanceId, out var snapshot))
                return CombatKillSettlementResult.Fail("no_contribution_record");

            var presenceList = new List<CombatRewardPresence>(presences);
            var inRange = new HashSet<Guid>();
            for (var i = 0; i < presenceList.Count; i++)
                if (presenceList[i].InRewardRange) inRange.Add(presenceList[i].CharacterId);

            var plan = _rewardPlanner.Build(snapshot, presenceList);
            var experience = _experience.Settle(plan, resolveCharacter);
            LootRollResult loot = null;
            Guid? partyLootPoolId = null;
            var spawned = new List<Guid>();

            if (definition.LootTableId.HasValue
                && _lootTables.TryGet(definition.LootTableId.Value, out var lootTable))
            {
                loot = LootTableRoller.Roll(lootTable, random01);
                if (!loot.IsEmpty)
                {
                    if (plan.InitialClaim.IsParty
                        && plan.InitialClaim.PartyId.HasValue
                        && _parties.TryGet(plan.InitialClaim.PartyId.Value, out var claimParty))
                    {
                        var recipients = ResolvePartyClaimRecipients(claimParty, inRange, plan.InitialClaim.FirstEngagerCharacterId);
                        if (recipients.Count > 0)
                        {
                            var poolDrops = new List<LootStack>();
                            for (var i = 0; i < loot.Stacks.Count; i++)
                            {
                                var stack = loot.Stacks[i];
                                if (stack.ItemId == CoinId)
                                    SplitPartyMoney(stack.Quantity, recipients, creature, nowUnixMilliseconds, resolveCharacter, poolDrops, spawned);
                                else
                                    poolDrops.Add(stack);
                            }

                            if (poolDrops.Count > 0)
                            {
                                var poolId = Guid.NewGuid();
                                _partyLoot.Create(poolId, creature.InstanceId, claimParty, recipients, poolDrops);
                                partyLootPoolId = poolId;
                            }
                        }
                        else
                        {
                            SpawnProtectedLoot(loot.Stacks, creature, plan.InitialClaim.FirstEngagerCharacterId, nowUnixMilliseconds, spawned);
                        }
                    }
                    else
                    {
                        SpawnProtectedLoot(loot.Stacks, creature, plan.InitialClaim.FirstEngagerCharacterId, nowUnixMilliseconds, spawned);
                    }
                }
            }

            _contributions.Remove(creature.InstanceId);
            return CombatKillSettlementResult.Ok(plan, experience, loot, partyLootPoolId, spawned);
        }

        private List<Guid> ResolvePartyClaimRecipients(PartyState party, HashSet<Guid> inRange, Guid firstEngager)
        {
            var recipients = new List<Guid>();
            for (var i = 0; i < party.Members.Count; i++)
            {
                var member = party.Members[i];
                if (inRange.Contains(member)) recipients.Add(member);
            }

            // Loot claim belongs to the first engager's group even if everyone has moved away by the
            // death tick. Protecting the drop to that engager avoids silently destroying a valid
            // first claim; the configured public timer can still release it later.
            if (recipients.Count == 0 && firstEngager != Guid.Empty && party.Contains(firstEngager))
                recipients.Add(firstEngager);
            return recipients;
        }

        private void SplitPartyMoney(
            int totalCoins,
            IReadOnlyList<Guid> recipients,
            CreatureState creature,
            long nowUnixMilliseconds,
            Func<Guid, PlayerState> resolveCharacter,
            List<LootStack> poolDrops,
            List<Guid> spawned)
        {
            if (totalCoins <= 0 || recipients.Count == 0) return;
            var perRecipient = totalCoins / recipients.Count;
            var remainder = totalCoins - perRecipient * recipients.Count;

            if (perRecipient > 0)
            {
                for (var i = 0; i < recipients.Count; i++)
                {
                    var recipient = recipients[i];
                    var player = resolveCharacter(recipient);
                    var added = player == null ? 0 : InventoryRules.AddItem(player.Inventory, _items, CoinId, perRecipient);
                    var leftover = perRecipient - added;
                    if (leftover > 0)
                        SpawnProtected(CoinId, leftover, creature, recipient, nowUnixMilliseconds, spawned);
                }
            }

            // Integer currency cannot always divide perfectly. Preserve equal base shares and let the
            // party's chosen classic loot mode resolve only the indivisible remainder.
            if (remainder > 0) poolDrops.Add(new LootStack(CoinId, remainder));
        }

        private void SpawnProtectedLoot(
            IReadOnlyList<LootStack> stacks,
            CreatureState creature,
            Guid recipient,
            long nowUnixMilliseconds,
            List<Guid> spawned)
        {
            for (var i = 0; i < stacks.Count; i++)
                SpawnProtected(stacks[i].ItemId, stacks[i].Quantity, creature, recipient, nowUnixMilliseconds, spawned);
        }

        private void SpawnProtected(
            ContentId itemId,
            int quantity,
            CreatureState creature,
            Guid recipient,
            long nowUnixMilliseconds,
            List<Guid> spawned)
        {
            var publicAt = _lootPublicDelayMilliseconds == 0
                ? 0
                : checked(nowUnixMilliseconds + _lootPublicDelayMilliseconds);
            var expiresAt = _lootLifetimeMilliseconds == 0
                ? 0
                : checked(nowUnixMilliseconds + _lootLifetimeMilliseconds);
            IEnumerable<Guid> protection = recipient == Guid.Empty ? null : new[] { recipient };
            var ground = _groundItems.Spawn(
                itemId,
                quantity,
                creature.Anchor,
                nowUnixMilliseconds,
                protection,
                publicAt,
                expiresAt);
            spawned.Add(ground.InstanceId);
        }
    }
}
