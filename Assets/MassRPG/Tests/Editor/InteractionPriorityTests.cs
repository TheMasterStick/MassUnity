using System.Collections.Generic;
using MassRPG.Core.Interactions;
using MassRPG.Core.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class InteractionPriorityTests
    {
        [Test]
        public void DefaultClickUsesSettledCrowdPriority()
        {
            var tile = Loc(10, 10);
            var options = new List<InteractionOption>
            {
                Option("walk", InteractionTargetKind.MovementTile, InteractionActionKind.WalkHere, tile, "Walk here"),
                Option("player", InteractionTargetKind.OtherPlayer, InteractionActionKind.Follow, tile, "Follow player"),
                Option("coin", InteractionTargetKind.GroundItem, InteractionActionKind.Take, tile, "Take coins"),
                Option("rock", InteractionTargetKind.Resource, InteractionActionKind.Gather, tile, "Mine iron"),
                Option("chest", InteractionTargetKind.GameplayObject, InteractionActionKind.Use, tile, "Open chest"),
                Option("npc", InteractionTargetKind.Npc, InteractionActionKind.Talk, tile, "Talk-to smith"),
                Option("wolf", InteractionTargetKind.CombatCreature, InteractionActionKind.Attack, tile, "Attack wolf", hostile: true),
            };

            Assert.IsTrue(InteractionPriority.TryResolveDefault(options, out var resolved));
            Assert.AreEqual("wolf", resolved.Target.TargetId);
            Assert.AreEqual(InteractionActionKind.Attack, resolved.Action);
        }

        [Test]
        public void NpcBeatsNeutralCreatureButCurrentCombatTargetOverridesNpc()
        {
            var tile = Loc(1, 1);
            var npc = Option("npc", InteractionTargetKind.Npc, InteractionActionKind.Talk, tile, "Talk-to banker");
            var neutral = Option("cow", InteractionTargetKind.CombatCreature, InteractionActionKind.Attack, tile, "Attack cow");
            Assert.IsTrue(InteractionPriority.TryResolveDefault(new[] { neutral, npc }, out var first));
            Assert.AreEqual("npc", first.Target.TargetId);

            var targetedCow = Option(
                "cow",
                InteractionTargetKind.CombatCreature,
                InteractionActionKind.Attack,
                tile,
                "Attack cow",
                currentTarget: true);
            Assert.IsTrue(InteractionPriority.TryResolveDefault(new[] { npc, targetedCow }, out var targeted));
            Assert.AreEqual("cow", targeted.Target.TargetId);
        }

        [Test]
        public void ContextStackPreservesEveryOverlappingTargetAndPlacesWalkThenExamineLast()
        {
            var tile = Loc(5, 5);
            var wolf = Target("wolf", InteractionTargetKind.CombatCreature, tile, hostile: true);
            var npc = Target("npc", InteractionTargetKind.Npc, tile);
            var item = Target("bones", InteractionTargetKind.GroundItem, tile);
            var ground = Target("ground", InteractionTargetKind.MovementTile, tile);
            var options = new[]
            {
                new InteractionOption(npc, InteractionActionKind.Examine, "Examine NPC", false),
                new InteractionOption(item, InteractionActionKind.Take, "Take bones"),
                new InteractionOption(ground, InteractionActionKind.WalkHere, "Walk here"),
                new InteractionOption(wolf, InteractionActionKind.Examine, "Examine wolf", false),
                new InteractionOption(npc, InteractionActionKind.Talk, "Talk-to NPC"),
                new InteractionOption(wolf, InteractionActionKind.Attack, "Attack wolf"),
                new InteractionOption(item, InteractionActionKind.Examine, "Examine bones", false),
            };

            var stack = InteractionPriority.BuildContextStack(options);
            Assert.AreEqual(7, stack.Count);
            Assert.AreEqual("Attack wolf", stack[0].Label);
            Assert.AreEqual("Talk-to NPC", stack[1].Label);
            Assert.AreEqual("Take bones", stack[2].Label);
            Assert.AreEqual("Walk here", stack[3].Label);
            Assert.AreEqual(InteractionActionKind.Examine, stack[4].Action);
            Assert.AreEqual(InteractionActionKind.Examine, stack[5].Action);
            Assert.AreEqual(InteractionActionKind.Examine, stack[6].Action);
        }

        [Test]
        public void ExamineOnlyTargetIsNeverAccidentallyChosenAsDefault()
        {
            var tile = Loc(0, 0);
            var objectTarget = Target("statue", InteractionTargetKind.GameplayObject, tile);
            var ground = Target("ground", InteractionTargetKind.MovementTile, tile);
            var options = new[]
            {
                new InteractionOption(objectTarget, InteractionActionKind.Examine, "Examine statue", false),
                new InteractionOption(ground, InteractionActionKind.WalkHere, "Walk here")
            };

            Assert.IsTrue(InteractionPriority.TryResolveDefault(options, out var resolved));
            Assert.AreEqual(InteractionActionKind.WalkHere, resolved.Action);
        }

        [Test]
        public void ActorVisualOrderingMatchesSettledCrowdRule()
        {
            var tile = Loc(3, 3);
            var hostile = Target("hostile", InteractionTargetKind.CombatCreature, tile, hostile: true);
            var npc = Target("npc", InteractionTargetKind.Npc, tile);
            var local = Target("self", InteractionTargetKind.LocalPlayer, tile);
            var passive = Target("passive", InteractionTargetKind.CombatCreature, tile);
            var other = Target("other", InteractionTargetKind.OtherPlayer, tile);

            Assert.Less(InteractionPriority.ActorVisualRank(hostile), InteractionPriority.ActorVisualRank(npc));
            Assert.Less(InteractionPriority.ActorVisualRank(npc), InteractionPriority.ActorVisualRank(local));
            Assert.Less(InteractionPriority.ActorVisualRank(local), InteractionPriority.ActorVisualRank(passive));
            Assert.Less(InteractionPriority.ActorVisualRank(passive), InteractionPriority.ActorVisualRank(other));
        }

        private static InteractionOption Option(
            string id,
            InteractionTargetKind kind,
            InteractionActionKind action,
            GridLocation location,
            string label,
            bool hostile = false,
            bool currentTarget = false)
            => new InteractionOption(Target(id, kind, location, hostile, currentTarget), action, label);

        private static InteractionTarget Target(
            string id,
            InteractionTargetKind kind,
            GridLocation location,
            bool hostile = false,
            bool currentTarget = false)
            => new InteractionTarget(id, kind, location, id, hostile, false, currentTarget);

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
