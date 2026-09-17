using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Construction;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Construction;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Server.Construction;
using MassRPG.Server.Production;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ConstructionMigrationTests
    {
        [Test]
        public void OppositeRepresentationsOfSameWallEdgeCannotStack()
        {
            var setup = CreatePlotSetup();
            Give(setup.Player, setup.Items, "plank", 10);
            Give(setup.Player, setup.Items, "nails", 10);

            var first = setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(10, 10), CardinalEdgeMask.East);
            var duplicate = setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(11, 10), CardinalEdgeMask.West);

            Assert.IsTrue(first.Success);
            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual("occupied_build_slot", duplicate.Code);
        }

        [Test]
        public void UpperFloorsRequireSensibleSupportButThreeUsableStoreysArePossible()
        {
            var setup = CreatePlotSetup();
            setup.Player.Skills.SetXp(SkillId.Construction, SkillProgression.XpForLevel(20));
            Give(setup.Player, setup.Items, "plank", 100);
            Give(setup.Player, setup.Items, "nails", 100);

            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(10, 10, 0), CardinalEdgeMask.North).Success);

            var unsupported = setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.floor_wood"), Loc(10, 10, 1));
            Assert.IsFalse(unsupported.Success);
            Assert.AreEqual("insufficient_support", unsupported.Code);

            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(10, 10, 0), CardinalEdgeMask.West).Success);
            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.floor_wood"), Loc(10, 10, 1)).Success);

            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(10, 10, 1), CardinalEdgeMask.North).Success);
            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.wall_wood"),
                Loc(10, 10, 1), CardinalEdgeMask.West).Success);
            Assert.IsTrue(setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.floor_wood"), Loc(10, 10, 2)).Success);

            var fourthFloorStairs = setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.stairs_wood"), Loc(10, 10, 2));
            Assert.IsFalse(fourthFloorStairs.Success);
            Assert.AreEqual("insufficient_support", fourthFloorStairs.Code);
        }

        [Test]
        public void PlayerBuiltFurnaceCanDriveAuthoritativeProduction()
        {
            var setup = CreatePlotSetup();
            setup.Player.Skills.SetXp(SkillId.Construction, SkillProgression.XpForLevel(20));
            setup.Player.Skills.SetXp(SkillId.Smithing, SkillProgression.XpForLevel(15));
            Give(setup.Player, setup.Items, "stone", 30);
            Give(setup.Player, setup.Items, "iron_ore", 1);

            var furnaceLocation = Loc(10, 10);
            var build = setup.Construction.TryPlace(
                setup.Player, setup.Plot.PlotId, new ContentId("build.furnace"), furnaceLocation);
            Assert.IsTrue(build.Success);
            Assert.IsTrue(setup.Construction.IsStationAt(new ContentId("station.furnace"), furnaceLocation));

            setup.Player.Location = Loc(10, 11);
            var production = new ProductionService(MigrationSeedRecipeCatalog.Create(), setup.Items, setup.Construction);
            var start = production.TryStart(
                setup.Player, new ContentId("smelt_iron_bar"), 1, furnaceLocation, 1000);
            Assert.IsTrue(start.Success);
            var finish = production.Advance(setup.Player, 2200);
            Assert.AreEqual(ProductionAdvanceKind.CompletedAll, finish.Kind);
            Assert.AreEqual(1, setup.Player.Inventory.CountItem(new ContentId("iron_bar")));
        }

        [Test]
        public void PlotUpgradeShapeCanVaryButMustRemainInsideReservedLargeEnvelope()
        {
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var placement = registry.TryPlace(
                Guid.NewGuid(), Guid.NewGuid(), WorldConstants.SurfacePlane,
                Rect(10, 10, 2, 2), Rect(8, 8, 6, 6));
            Assert.IsTrue(placement.Success);

            var medium = registry.TryUpgrade(placement.Plot.PlotId, PlotTier.Medium, Rect(9, 9, 4, 4));
            Assert.IsTrue(medium.Success);
            Assert.AreEqual(PlotTier.Medium, placement.Plot.Tier);

            var invalidLarge = registry.TryUpgrade(placement.Plot.PlotId, PlotTier.Large, Rect(7, 7, 7, 7));
            Assert.IsFalse(invalidLarge.Success);
            Assert.AreEqual("upgrade_outside_reservation", invalidLarge.Code);

            var large = registry.TryUpgrade(placement.Plot.PlotId, PlotTier.Large, Rect(8, 8, 6, 6));
            Assert.IsTrue(large.Success);
            Assert.AreEqual(PlotTier.Large, placement.Plot.Tier);
        }

        [Test]
        public void UpkeepSupportsLongPrepaymentThenDelinquentGraceAndAbandonment()
        {
            const long day = 24L * 60L * 60L * 1000L;
            var setup = CreatePlotSetup();
            Give(setup.Player, setup.Items, "coins", 1000);
            var policy = new PlotUpkeepPolicy(10, 20, 40, TimeSpan.FromDays(30));
            var upkeep = new PlotUpkeepService(setup.Registry, policy);
            upkeep.Register(setup.Plot.PlotId, 1000 + day);

            var payment = upkeep.Pay(setup.Player, setup.Plot.PlotId, 120, 1000);
            Assert.IsTrue(payment.Success);
            Assert.AreEqual(120, payment.GoldSpent);
            Assert.AreEqual(12, payment.DaysAdded);
            Assert.IsTrue(upkeep.TryGet(setup.Plot.PlotId, out var state));
            Assert.AreEqual(1000 + 13 * day, state.PaidThroughUnixMilliseconds);

            Assert.AreEqual(PlotUpkeepStatus.Delinquent, upkeep.Advance(setup.Plot.PlotId, state.PaidThroughUnixMilliseconds + day));
            Assert.AreEqual(PlotUpkeepStatus.Abandoned, upkeep.Advance(setup.Plot.PlotId, state.PaidThroughUnixMilliseconds + 30 * day));
        }

        [Test]
        public void BlueprintPiecesResolveRelativeToChosenWorldOrigin()
        {
            var blueprint = new BuildingBlueprint(
                Guid.NewGuid(), Guid.NewGuid(), "Tiny tower",
                new[]
                {
                    new BlueprintPiece(new ContentId("build.floor_wood"), 0, 0, 0),
                    new BlueprintPiece(new ContentId("build.wall_wood"), 1, -1, 1, CardinalEdgeMask.North)
                });

            Assert.AreEqual(Loc(100, 200, 0), blueprint.Pieces[0].Resolve(Loc(100, 200, 0)));
            Assert.AreEqual(Loc(101, 199, 1), blueprint.Pieces[1].Resolve(Loc(100, 200, 0)));
        }

        private static Setup CreatePlotSetup()
        {
            var items = MigrationSeedItemCatalog.Create();
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var owner = Guid.NewGuid();
            var placement = registry.TryPlace(
                Guid.NewGuid(), owner, WorldConstants.SurfacePlane,
                Rect(8, 8, 8, 8), Rect(6, 6, 12, 12));
            Assert.IsTrue(placement.Success);
            var player = new PlayerState(owner, "Builder") { Location = Loc(10, 10) };
            var construction = new PlotConstructionService(registry, MigrationSeedBuildPieceCatalog.Create(), items);
            return new Setup(items, registry, placement.Plot, player, construction);
        }

        private static void Give(PlayerState player, ItemCatalog items, string itemId, int quantity)
        {
            Assert.AreEqual(quantity, InventoryRules.AddItem(player.Inventory, items, new ContentId(itemId), quantity));
        }

        private static IEnumerable<GridCoord> Rect(int x, int y, int width, int height)
        {
            for (var yy = 0; yy < height; yy++)
                for (var xx = 0; xx < width; xx++)
                    yield return new GridCoord(x + xx, y + yy);
        }

        private static GridLocation Loc(int x, int y, int storey = 0)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, storey);

        private sealed class OpenPlacementMap : IPlotPlacementMap
        {
            public bool CanReserveForPlayerPlot(GridLocation location) => true;
        }

        private sealed class Setup
        {
            public Setup(ItemCatalog items, PlayerPlotRegistry registry, PlayerPlotState plot, PlayerState player, PlotConstructionService construction)
            {
                Items = items;
                Registry = registry;
                Plot = plot;
                Player = player;
                Construction = construction;
            }

            public ItemCatalog Items { get; }
            public PlayerPlotRegistry Registry { get; }
            public PlayerPlotState Plot { get; }
            public PlayerState Player { get; }
            public PlotConstructionService Construction { get; }
        }
    }
}
