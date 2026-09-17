using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;
using MassRPG.Core.Interactions;
using MassRPG.Core.Resources;
using MassRPG.Core.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class InteractionIntentFactoryTests
    {
        private readonly Guid _characterId = Guid.NewGuid();

        [Test]
        public void WalkHereBuildsMoveRequestFromLogicalLocation()
        {
            var location = Location(10, 12);
            var target = new InteractionTarget("ground", InteractionTargetKind.MovementTile, location, "Ground");
            var option = new InteractionOption(target, InteractionActionKind.WalkHere, "Walk here");
            var requestId = Guid.NewGuid();

            var built = InteractionIntentFactory.TryCreateRequest(option, requestId, _characterId, out var request, out var failure);

            Assert.IsTrue(built);
            Assert.AreEqual(string.Empty, failure);
            Assert.IsInstanceOf<MoveToRequest>(request);
            Assert.AreEqual(location, ((MoveToRequest)request).Destination);
            Assert.AreEqual(requestId, request.RequestId);
        }

        [Test]
        public void AttackUsesTypedCreatureInstanceId()
        {
            var creatureId = Guid.NewGuid();
            var target = new InteractionTarget(
                "creature:display-key",
                InteractionTargetKind.CombatCreature,
                Location(20, 21),
                "Wolf",
                hostile: true,
                instanceId: creatureId);
            var option = new InteractionOption(target, InteractionActionKind.Attack, "Attack Wolf");

            var built = InteractionIntentFactory.TryCreateRequest(
                option, Guid.NewGuid(), _characterId, out var request, out var failure);

            Assert.IsTrue(built);
            Assert.AreEqual(string.Empty, failure);
            Assert.IsInstanceOf<AttackCreatureRequest>(request);
            Assert.AreEqual(creatureId, ((AttackCreatureRequest)request).CreatureInstanceId);
        }

        [Test]
        public void GatherUsesTypedResourceNodeKey()
        {
            var location = Location(30, 31);
            var node = new ResourceNodeKey(new ContentId("ore.copper"), location, 2);
            var target = new InteractionTarget(
                "resource:visual-key",
                InteractionTargetKind.Resource,
                location,
                "Copper rock",
                resourceNode: node);
            var option = new InteractionOption(target, InteractionActionKind.Gather, "Mine Copper rock");

            var built = InteractionIntentFactory.TryCreateRequest(
                option, Guid.NewGuid(), _characterId, out var request, out var failure);

            Assert.IsTrue(built);
            Assert.AreEqual(string.Empty, failure);
            Assert.IsInstanceOf<GatherResourceRequest>(request);
            Assert.AreEqual(node, ((GatherResourceRequest)request).Node);
        }

        [Test]
        public void TakeUsesTypedGroundItemInstanceId()
        {
            var groundItemId = Guid.NewGuid();
            var target = new InteractionTarget(
                "ground-item:visual-key",
                InteractionTargetKind.GroundItem,
                Location(40, 41),
                "Coins",
                instanceId: groundItemId);
            var option = new InteractionOption(target, InteractionActionKind.Take, "Take Coins");

            var built = InteractionIntentFactory.TryCreateRequest(
                option, Guid.NewGuid(), _characterId, out var request, out var failure);

            Assert.IsTrue(built);
            Assert.AreEqual(string.Empty, failure);
            Assert.IsInstanceOf<TakeGroundItemRequest>(request);
            Assert.AreEqual(groundItemId, ((TakeGroundItemRequest)request).GroundItemId);
        }

        [Test]
        public void MissingTypedIdentityIsRejectedInsteadOfParsingDisplayId()
        {
            var target = new InteractionTarget(
                "creature:not-a-guid",
                InteractionTargetKind.CombatCreature,
                Location(50, 51),
                "Goblin");
            var option = new InteractionOption(target, InteractionActionKind.Attack, "Attack Goblin");

            var built = InteractionIntentFactory.TryCreateRequest(
                option, Guid.NewGuid(), _characterId, out var request, out var failure);

            Assert.IsFalse(built);
            Assert.IsNull(request);
            Assert.AreEqual("interaction_identity_missing", failure);
        }

        [Test]
        public void PresentationOnlyActionDoesNotBecomeGameplayRequest()
        {
            var target = new InteractionTarget("npc", InteractionTargetKind.Npc, Location(60, 61), "Guide");
            var option = new InteractionOption(target, InteractionActionKind.Examine, "Examine Guide");

            var built = InteractionIntentFactory.TryCreateRequest(
                option, Guid.NewGuid(), _characterId, out var request, out var failure);

            Assert.IsFalse(built);
            Assert.IsNull(request);
            Assert.AreEqual("unsupported_interaction_action", failure);
        }

        private static GridLocation Location(int x, int y)
            => new GridLocation(new GridCoord(x, y), 0, 0);
    }
}
