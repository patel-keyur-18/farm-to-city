using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-144 (spec 025 §3.1, ADR 0130): the five utilities the treasury builds, their reach and capacity, upkeep, the smoke, and what going without costs.</summary>
    public class UtilityTests
    {
        private const int Tower = UtilityDef.Tower, Generator = UtilityDef.Generator, Grid = UtilityDef.Grid, Drains = UtilityDef.Drains, Collection = UtilityDef.Collection;

        private static Simulation Township(long treasuryCoins = 3_000_000, int families = 2)
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            if (families > 0) TownshipTests.Families(sim, families);
            TreasuryTests.Township(sim);
            Fund(sim, treasuryCoins * Coin);
            sim.DrainEvents();
            return sim;
        }

        private static void Fund(Simulation sim, long amount)
        {
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.GiftsFromFounder, -amount));
        }

        private static string WorkOf(Simulation sim, int u) { return UtilityRules.Def(sim, u).Work; }

        /// <summary>Commissions a utility and waits (bricks, then the masons' hours) until it stands.</summary>
        private static void Build(Simulation sim, int u)
        {
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, WorkOf(sim, u)));
            for (int i = 0; i < 400 && !UtilityRules.Built(sim, u); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(UtilityRules.Built(sim, u), Is.True, WorkOf(sim, u) + " stands");
        }

        /// <summary>A family house of the household on a tile (the test families come with no home).</summary>
        private static Building HomeAt(Simulation sim, Household x, int tile)
        {
            LandState land = sim.State.Land;
            var b = new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = tile };
            land.Buildings.Add(b);
            x.HomeId = b.Id;
            return b;
        }

        /// <summary>The first acre with no pond or well in reach and a working tower's reach over it.</summary>
        private static int TowerOnlyTile(Simulation sim)
        {
            UtilityDef t = UtilityRules.Def(sim, Tower);
            foreach (Tile x in sim.State.Land.Tiles)
                if (x.Terrain != Terrain.River && !LandValueRules.PondOrWell(sim, x.Id) && CoverageRules.Within(sim, x.Id, t.Tile, t.Reach)) return x.Id;
            Assert.Fail("no acre only the tower reaches");
            return -1;
        }

        // AC-1: each utility is a work on its own plot, once; the tower, drains and post take materials and masons, the generator and grid are fitted at once.
        [Test]
        public void Ladders()
        {
            Simulation sim = Township();
            Assert.That(UtilityRules.Defs(sim).Steps.Select(d => d.Tile).ToArray(), Is.EqualTo(new[] { 316, 300, 396, 324, 332 }), "the plots approved from the ring's preview");
            Assert.That(UtilityRules.Defs(sim).Steps.Select(d => (d.Reach, d.Capacity)).Take(2).ToArray(), Is.EqualTo(new[] { (400, 400), (600, 300) }));
            foreach (int u in new[] { Tower, Drains, Collection }) Assert.That(UtilityRules.TakesMaterials(sim, u), Is.True, "materials and masons");
            foreach (int u in new[] { Generator, Grid }) Assert.That(UtilityRules.TakesMaterials(sim, u), Is.False, "fitted the same day");

            long cash = sim.State.Treasury.Cash, cost = UtilityRules.Cost(sim, Tower);
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "water_tower"));
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(cash - cost), "paid at the start");
            Assert.That(UtilityRules.Built(sim, Tower), Is.False, "waits for materials and the masons' hours");
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "water_tower")).Reason, Is.EqualTo(RejectReason.PlotTaken), "being built");
            for (int i = 0; i < 400 && !UtilityRules.Built(sim, Tower); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(UtilityRules.Built(sim, Tower), Is.True);
            Assert.That(sim.PendingEvents.OfType<UtilityBuilt>().Single().Utility, Is.EqualTo(Tower));
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "water_tower")).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            long before = sim.State.Treasury.Cash;
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "diesel_generator"));
            Assert.That(UtilityRules.Built(sim, Generator), Is.True, "fitted the same day");
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(before - UtilityRules.Cost(sim, Generator)));
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
        }

        // AC-1: upkeep joins the Utilities department when the utility stands, wages and fuel with it; the generator's lines stop when the grid comes.
        [Test]
        public void Upkeep_And_Lines()
        {
            Simulation sim = Township();
            long utilities = TreasuryRules.Needs(sim, 5);
            Assert.That(utilities, Is.EqualTo(0), "nothing built, nothing to pay");
            Build(sim, Generator);
            long generator = UtilityRules.Runs(sim, Generator);
            Assert.That(generator, Is.EqualTo(Market.MarketRules.Indexed(sim, (2_000 + 8_000) * 100L)), "upkeep and fuel");
            Assert.That(TreasuryRules.Needs(sim, 5), Is.EqualTo(generator));
            Build(sim, Collection);
            Assert.That(TreasuryRules.Needs(sim, 5), Is.EqualTo(generator + UtilityRules.Runs(sim, Collection)), "the post's upkeep and the collector's wage");
            Build(sim, Grid);
            Assert.That(UtilityRules.SwitchedOff(sim, Generator), Is.True);
            Assert.That(TreasuryRules.Needs(sim, 5), Is.EqualTo(UtilityRules.Runs(sim, Collection) + UtilityRules.Runs(sim, Grid)), "the generator's lines stop");
        }

        // AC-1: an unpaid Utilities department decays, and under half a utility covers nobody.
        [Test]
        public void Unfunded_Decays_To_Nothing()
        {
            Simulation sim = Township();
            Build(sim, Tower);
            int tile = TowerOnlyTile(sim);
            Assert.That(UtilityRules.Covers(sim, Tower, tile), Is.True);
            TreasuryRules.State(sim).Condition[5] = Fixed.Parse("0.4");
            Assert.That(UtilityRules.Working(sim, Tower), Is.False);
            Assert.That(UtilityRules.Covers(sim, Tower, tile), Is.False, "under the floor it waters nobody");
            Assert.That(UtilityRules.HasWater(sim, tile), Is.False);
            TreasuryRules.State(sim).Condition[5] = Fixed.Parse("0.5");
            Assert.That(UtilityRules.HasWater(sim, tile), Is.True, "at the floor it works again");
        }

        // AC-2: the tower waters 400 m and counts for builders and land value; past its capacity it serves each home a little less.
        [Test]
        public void Water()
        {
            Simulation sim = Township();
            int tile = TowerOnlyTile(sim);
            Assert.That(UtilityRules.HasWater(sim, tile), Is.False, "no pond or well, no tower yet");
            Assert.That(LandValueRules.Parts(sim, tile).Exists(p => p.Key == "water"), Is.False);
            Build(sim, Tower);
            Assert.That(UtilityRules.HasWater(sim, tile), Is.True);
            Assert.That(LandValueRules.Parts(sim, tile).Exists(p => p.Key == "water"), Is.True, "land value counts the tower's reach");
            Assert.That(UtilityRules.Water(sim, tile), Is.EqualTo(Fixed.One), "condition 1, capacity left");
            Assert.That(UtilityRules.Scale(sim, Tower), Is.EqualTo(Fixed.One));
        }

        // AC-2: a home with no water near loses health a season; the tower stops it.
        [Test]
        public void Water_Health()
        {
            Simulation sim = Township();
            int tile = TowerOnlyTile(sim);
            Household x = sim.State.Citizens.Households[0];
            HomeAt(sim, x, tile);
            Fixed loss = UtilityRules.Defs(sim).WaterHealth;
            Fixed drains = UtilityRules.Defs(sim).DrainsHealth; // the old village makes it a big town: no drains costs two more
            Assert.That(UtilityRules.Strain(sim, x), Is.EqualTo(loss + drains), "four points for water, two for drains");
            Build(sim, Tower);
            Assert.That(UtilityRules.Strain(sim, x), Is.EqualTo(drains), "a home in reach of the tower keeps its four");
        }

        // AC-3: the generator takes land value and health within 250 m; the grid switches it off and the smoke goes.
        [Test]
        public void Smoke()
        {
            Simulation sim = Township();
            UtilityDef g = UtilityRules.Def(sim, Generator);
            Assert.That(SmokeRules.LandValue(sim, g.Tile, out _), Is.EqualTo(Fixed.Zero));
            Build(sim, Generator);
            Fixed lv = SmokeRules.LandValue(sim, g.Tile, out int n);
            Assert.That((lv, n), Is.EqualTo((Fixed.Parse("0.06"), 1)), "its own acre loses 0.06");
            int far = sim.State.Land.Tiles.First(t => t.Terrain != Terrain.River && CoverageRules.Metres(sim, t.Id, g.Tile) == 300).Id;
            Assert.That(SmokeRules.LandValue(sim, far, out _), Is.EqualTo(Fixed.Zero), "300 m is out of reach");
            int near = sim.State.Land.Tiles.First(t => t.Terrain != Terrain.River && CoverageRules.Metres(sim, t.Id, g.Tile) == 200).Id;
            Assert.That(SmokeRules.LandValue(sim, near, out _), Is.EqualTo(Fixed.Parse("0.06")), "200 m is in");
            Assert.That(LandValueRules.Parts(sim, near).Exists(p => p.Key == "smoke"), Is.True);

            Build(sim, Grid);
            Assert.That(SmokeRules.LandValue(sim, g.Tile, out _), Is.EqualTo(Fixed.Zero), "the generator is switched off");
            Assert.That(sim.PendingEvents.OfType<GridConnected>().Count(), Is.EqualTo(1));
        }

        // AC-3: a generator and a kiln's smoke on one acre stop at the cap.
        [Test]
        public void Smoke_Capped_With_The_Kiln()
        {
            Simulation sim = Township();
            Build(sim, Generator);
            UtilityDef g = UtilityRules.Def(sim, Generator);
            Assert.That(SmokeRules.Sources(sim).Count, Is.EqualTo(1));
            Assert.That(SmokeRules.Def(sim).LandValueCap, Is.EqualTo(Fixed.Parse("0.10")));
            Assert.That(SmokeRules.Footprint(sim, g.Tile, SmokeRules.Def(sim).Source("generator")).Count, Is.GreaterThan(5), "250 m reaches two acres out");
        }

        // The grid: no limit, replaces the generator, and says what it saves and what its extra costs back.
        [Test]
        public void Grid_Replaces_The_Generator()
        {
            Simulation sim = Township();
            Assert.That(UtilityRules.GridSaves(sim), Is.EqualTo(0), "no generator, nothing saved");
            Build(sim, Generator);
            long saves = UtilityRules.GridSaves(sim);
            Assert.That(saves, Is.EqualTo(UtilityRules.Runs(sim, Generator) - UtilityRules.Runs(sim, Grid)));
            Assert.That(UtilityRules.GridPaybackSeasons(sim), Is.InRange(15, 20), "about 17 seasons at the base index");
            UtilityRules.Guard(sim, "grid_connection", out long added, out _);
            Assert.That(added, Is.EqualTo(-saves), "the grid's card adds less than the generator it replaces");
            Build(sim, Grid);
            Assert.That(UtilityRules.Power(sim, UtilityRules.Def(sim, Generator).Tile), Is.EqualTo(Fixed.One), "the whole town");
            Assert.That(UtilityRules.Scale(sim, Grid), Is.EqualTo(Fixed.One), "no limit");
        }

        // The generator covers 600 m and 300 people: a bigger town is served a little less; leisure and evenings follow.
        [Test]
        public void Power()
        {
            Simulation sim = Township();
            UtilityDef g = UtilityRules.Def(sim, Generator);
            int inReach = sim.State.Land.Tiles.First(t => t.Terrain != Terrain.River && CoverageRules.Metres(sim, t.Id, g.Tile) <= 600).Id;
            int beyond = sim.State.Land.Tiles.First(t => CoverageRules.Metres(sim, t.Id, g.Tile) > 600).Id;
            Household x = sim.State.Citizens.Households[0];
            Fixed own = Fixed.Parse("0.30");
            Assert.That(UtilityRules.Leisure(sim, x, own), Is.EqualTo(own), "no power yet");
            Build(sim, Generator);
            Assert.That(UtilityRules.Power(sim, inReach), Is.EqualTo(Fixed.One));
            Assert.That(UtilityRules.Power(sim, beyond), Is.EqualTo(Fixed.Zero));
            HomeAt(sim, x, inReach);
            Assert.That(UtilityRules.Leisure(sim, x, own), Is.EqualTo(own + own * Fixed.Parse("0.10")), "a tenth more");
            var shop = new Business.Business { TileId = inReach };
            Assert.That(UtilityRules.EveningHours(sim, shop), Is.EqualTo(2));
            shop.TileId = beyond;
            Assert.That(UtilityRules.EveningHours(sim, shop), Is.EqualTo(0));
        }

        // AC-1, AC-2: a small town misses nothing; at 40 residents drains cap Health and cost health and land value, collection costs Belonging.
        [Test]
        public void Sanitation_And_Waste_Are_Missed_From_Forty()
        {
            Simulation sim = Township(families: 0);
            Assert.That(UtilityRules.Big(sim), Is.True, "the old village counts: 150 residents");
            sim.State.Township.Cohort.Households = 0;
            Assert.That(UtilityRules.Big(sim), Is.False, "a town of a few people misses nothing");
            Assert.That(UtilityRules.HealthCap(sim), Is.EqualTo(Fixed.One));
            Assert.That(UtilityRules.BelongingTown(sim, Fixed.Parse("0.8")), Is.EqualTo(Fixed.Parse("0.8")));
        }

        [Test]
        public void Without_Drains_Health_Is_Capped()
        {
            Simulation sim = Township();
            Assert.That(UtilityRules.Big(sim), Is.True);
            Assert.That(UtilityRules.HealthCap(sim), Is.EqualTo(Fixed.Parse("0.85")));
            Household x = sim.State.Citizens.Households[0];
            HomeAt(sim, x, TowerOnlyTile(sim));
            Build(sim, Tower);
            Fixed strain = UtilityRules.Strain(sim, x);
            Assert.That(strain, Is.GreaterThanOrEqualTo(UtilityRules.Defs(sim).DrainsHealth), "two points a season without drains");
            Build(sim, Drains);
            Assert.That(UtilityRules.HealthCap(sim), Is.EqualTo(Fixed.One));
            Assert.That(UtilityRules.Strain(sim, x), Is.LessThan(strain));
        }

        [Test]
        public void Without_Drains_A_Home_Acre_Loses_Land_Value()
        {
            Simulation sim = Township();
            int tile = HomeAt(sim, sim.State.Citizens.Households[0], TowerOnlyTile(sim)).TileId;
            Assert.That(UtilityRules.DrainsMissing(sim, tile), Is.True);
            Assert.That(LandValueRules.Parts(sim, tile).Single(p => p.Key == "drains").Value, Is.EqualTo(Fixed.Zero - Fixed.Parse("0.04")));
            int bare = sim.State.Land.Tiles.First(t => t.Terrain != Terrain.River && !sim.State.Land.Buildings.Any(b => b.TileId == t.Id)).Id;
            Assert.That(UtilityRules.DrainsMissing(sim, bare), Is.False, "an acre with no home is not hurt");
            Build(sim, Drains);
            UtilityDef d = UtilityRules.Def(sim, Drains);
            if (CoverageRules.Within(sim, tile, d.Tile, d.Reach)) Assert.That(UtilityRules.DrainsMissing(sim, tile), Is.False, "drains in reach");
        }

        [Test]
        public void Without_Collection_Belonging_Is_Lower()
        {
            Simulation sim = Township();
            Household x = sim.State.Citizens.Households[0];
            HomeAt(sim, x, TowerOnlyTile(sim));
            Fixed b = Fixed.Parse("0.8");
            Assert.That(UtilityRules.Belonging(sim, x, b), Is.EqualTo(b - Fixed.Parse("0.05")), "five points lower");
            Assert.That(UtilityRules.BelongingTown(sim, b), Is.EqualTo(b - Fixed.Parse("0.05")));
            Build(sim, Collection);
            Assert.That(UtilityRules.Belonging(sim, x, b), Is.EqualTo(b));
            Assert.That(UtilityRules.BelongingTown(sim, b), Is.EqualTo(b));
        }

        // The collector is hired by the treasury like the clerk: his wage is the old village's income.
        [Test]
        public void Collector_Is_Paid_Into_The_Old_Village()
        {
            Simulation sim = Township();
            Build(sim, Collection);
            long before = sim.State.Township.Cohort.Savings;
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + Hour);
            Assert.That(sim.State.Township.Cohort.Savings, Is.GreaterThan(before), "the wage reached the old village");
            Assert.That(sim.State.Treasury.Reports.Last().Rows[5].Paid, Is.GreaterThan(0), "the Utilities department was paid");
        }

        // The one-tap top-up: the gift and the work in one command, or neither.
        [Test]
        public void TopUp()
        {
            Simulation sim = Township(treasuryCoins: 0);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash));
            Fund(sim, 105_000 * Coin);
            long cost = UtilityRules.Cost(sim, Tower), gift = UtilityRules.TopUp(sim, cost);
            Assert.That(gift % UtilityRules.Defs(sim).GiftRound, Is.EqualTo(0), "rounded up to the ten thousand");
            Assert.That(gift, Is.GreaterThanOrEqualTo(cost - sim.State.Treasury.Cash));
            Assert.That(gift - (cost - sim.State.Treasury.Cash), Is.LessThan(UtilityRules.Defs(sim).GiftRound));
            long mine = sim.State.Ledger.Cash, town = sim.State.Treasury.Cash;

            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "water_tower")).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "short without a gift");
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "water_tower", 100 * Coin)).Reason, Is.EqualTo(RejectReason.InsufficientFunds).Or.EqualTo(RejectReason.BelowGiftMinimum), "a gift that doesn't cover it");
            Assert.That((sim.State.Ledger.Cash, sim.State.Treasury.Cash), Is.EqualTo((mine, town)), "nothing moved");

            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "water_tower", gift));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(mine - gift), "your cash gave it");
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(town + gift - cost), "the town paid the tower");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "the gift command is in the saved log");
        }

        // What stands survives a save, and the state hash matches.
        [Test]
        public void Saved()
        {
            Simulation sim = Township();
            Build(sim, Tower);
            Build(sim, Generator);
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Township.UtilitiesBuilt, Is.EqualTo((1 << Tower) | (1 << Generator)));
        }
    }
}
