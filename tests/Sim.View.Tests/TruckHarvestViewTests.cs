using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// ADR 0105 (owner, 2026-11-12): the Store lists what the trader still takes today of this season's crops, and a ready crop can be
    /// harvested in parts up to what the store holds.
    /// </summary>
    public class TruckHarvestViewTests
    {
        private const long Hour = 3600, Kg = 1000;

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static Simulation FarmAt(int hour)
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long day = Sim.Core.Definitions.ClockDef.SecondsPerDay;
            sim.AdvanceTo((T(sim) / day + 1) * day + hour * Hour);
            return sim;
        }

        private static string Season(Simulation sim) { return Calendar.SeasonAt(T(sim), sim.Clock).ToString(); }

        // The truck is here: its room per crop sown this season, in crop order, none of the others; selling uses it up; away, no rows.
        [Test]
        public void TruckRoom_ThisSeasonsCrops()
        {
            Simulation sim = FarmAt(8);
            Assert.That(sim.State.Market.TruckHere, Is.True);
            int season = (int)Calendar.SeasonAt(T(sim), sim.Clock);
            var inSeason = sim.Config.Game.Farm.Crops.Where(c => !c.Fodder && c.SowSeasons[season]).Select(c => c.OutputGood).Distinct().ToList();
            var offSeason = sim.Config.Game.Farm.Crops.Where(c => !c.Fodder && !c.SowSeasons[season]).Select(c => c.OutputGood).Except(inSeason).ToList();
            Assert.That(inSeason, Is.Not.Empty);
            Assert.That(offSeason, Is.Not.Empty, "a season has crops that aren't sown in it");

            StrTable(sim, out List<LineVm> lines);
            var names = lines.Select(l => l.Label).ToList();
            Assert.That(lines[0].Label, Is.EqualTo("The trader still takes today"));
            foreach (int g in inSeason) Assert.That(names, Does.Contain(Name(sim, g)));
            foreach (int g in offSeason) Assert.That(names, Does.Not.Contain(Name(sim, g)), Name(sim, g));
            Assert.That(lines.Skip(1).All(l => l.Value.EndsWith(" more") || l.Value == "Full for today"), Is.True);

            // Selling to the trader uses up the room for that crop and for the truck.
            int first = inSeason[0];
            LineVm before = lines.Single(l => l.Label == Name(sim, first));
            long room = MarketRules.TraderCapacityLeft(sim, first);
            sim.State.Market.Stock[first] += 100 * Kg;
            Assert.That(sim.Execute(new SellToTrader(T(sim), sim.Config.Game.Market.Goods[first].Id, 100 * Kg)).Accepted, Is.True);
            StrTable(sim, out lines);
            Assert.That(lines.Single(l => l.Label == Name(sim, first)).Value, Is.Not.EqualTo(before.Value));
            Assert.That(MarketRules.TraderCapacityLeft(sim, first), Is.EqualTo(room - 100 * Kg));

            // Away (night): no rows.
            Simulation night = FarmAt(2);
            Assert.That(night.State.Market.TruckHere, Is.False);
            StrTable(night, out lines);
            Assert.That(lines, Is.Empty);
        }

        private static string Name(Simulation sim, int g) { return ViewKit.Strings().Get(sim.Config.Game.Market.Goods[g].NameKey); }

        /// <summary>The Store's truck-room rows (the section between the truck line and the crop rows).</summary>
        private static void StrTable(Simulation sim, out List<LineVm> lines)
        {
            lines = OrdersSheet.TruckRoomLines(ViewKit2.Context(sim));
            SheetVm store = StoreSheet.Build(ViewKit2.Context(sim));
            Assert.That(store.Lines.Select(l => l.Label).Contains("The trader still takes today"), Is.EqualTo(lines.Count > 0), "the Store shows them when there are some");
        }

        /// <summary>A farm with a ready radish on tile 85 and <paramref name="roomGrams"/> of room left in the store.</summary>
        private static Simulation ReadyRadish(long roomGrams)
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            sim.State.Ledger.Post(T(sim), sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 100_000_00), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OpeningEquity, -100_000_00));
            var def = sim.Config.Game.Farm.Crops.First(c => c.Id == "radish");
            Assert.That(sim.Execute(new Sow(T(sim), 85, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(T(sim) + 3 * Hour);
            Assert.That(sim.State.Farm.CropOn(85).State, Is.EqualTo(CropState.Ready));
            int potato = sim.Config.Game.Market.IndexOf("potato");
            sim.State.Market.Stock[potato] += Store.CapacityGrams(sim) - Store.UsedGrams(sim) - roomGrams;
            return sim;
        }

        // A full store takes what fits: the button says how much of how much, runs, and the field shows what's already in the store.
        [Test]
        public void HarvestInParts()
        {
            Simulation sim = ReadyRadish(20 * Kg);
            Crop c = sim.State.Farm.CropOn(85);
            long all = c.HarvestGrams;
            Assert.That(all, Is.GreaterThan(20 * Kg));
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), 85);
            ActionVm harvest = vm.Actions.Single(a => a.Id == "harvest");
            Assert.That(harvest.Enabled, Is.True, harvest.DisabledReason);
            Assert.That(harvest.Detail, Is.EqualTo(ViewKit.Indian().Kg(20 * Kg) + " of " + ViewKit.Indian().Kg(all)));
            Assert.That(vm.Actions.Any(a => a.Id == "harvestSell"), Is.True, "selling the rest stays an option");

            Assert.That(sim.Execute(harvest.Create(T(sim))).Accepted, Is.True);
            c = sim.State.Farm.CropOn(85);
            Assert.That((c.TakenGrams, c.HarvestGrams), Is.EqualTo((20L * Kg, all - 20 * Kg)));
            vm = FieldSheets.ForTile(ViewKit2.Context(sim), 85);
            Assert.That(vm.Lines.Single(l => l.Label == "Already in your store").Value, Is.EqualTo(ViewKit.Indian().Kg(20 * Kg)));
            Assert.That(vm.Actions.Single(a => a.Id == "harvest").Enabled, Is.False, "the store is full now");
            WhyVm why = WhyView.Build(ViewKit2.Context(sim), "yield", 85);
            Assert.That(why.Rows.Any(r => r.Label == "Already taken into the store"), Is.True);
        }

        // With room for all of it the button is the plain one.
        [Test]
        public void HarvestWhenItAllFits()
        {
            Simulation sim = ReadyRadish(10_000 * Kg);
            long all = sim.State.Farm.CropOn(85).HarvestGrams;
            ActionVm harvest = FieldSheets.ForTile(ViewKit2.Context(sim), 85).Actions.Single(a => a.Id == "harvest");
            Assert.That(harvest.Detail, Is.EqualTo(ViewKit.Indian().Kg(all)));
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), 85).Actions.Any(a => a.Id == "harvestSell"), Is.False);
        }

        // Collect all takes what fits of each field.
        [Test]
        public void CollectAll_TakesParts()
        {
            Simulation sim = ReadyRadish(20 * Kg);
            Harvest h = CollectAll.Commands(sim, T(sim)).OfType<Harvest>().Single();
            Assert.That(h.Overflow, Is.EqualTo(Overflow.StorePart));
            Assert.That(sim.Execute(h).Accepted, Is.True);
            Assert.That(sim.State.Farm.CropOn(85).TakenGrams, Is.EqualTo(20 * Kg));
        }

        // ADR 0111: a ripe field that hired labourers (or a hand's order) would harvest, and that does not all fit, is an alert with its two
        // levers and a line on the away card; with nobody to harvest it, and when all of it fits, there is nothing to say.
        [Test]
        public void StillStanding()
        {
            Simulation sim = ReadyRadish(20 * Kg);
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Any(a => a.Text.StartsWith("Store full")), Is.False, "nobody is on it");
            Assert.That(sim.Execute(new Sow(T(sim), 86, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.Execute(new HireLabourers(T(sim), 1)).Accepted, Is.True);

            long left = sim.State.Farm.CropOn(85).HarvestGrams;
            ViewContext ctx = ViewKit2.Context(sim);
            AlertVm alert = Alerts.Build(ctx).Single(a => a.Text.StartsWith("Store full"));
            Assert.That(alert.Text, Is.EqualTo("Store full: " + ctx.Fmt.Kg(left) + " of radish still standing on " + Sim.View.Views.PeopleWords.Field(ctx, 85) + ". It will be taken as room appears."));
            Assert.That(alert.Levers.Select(l => l.Label), Is.EqualTo(new[] { "Sell some" }), "radish isn't kept in a godown, so no Rent space");
            Assert.That(alert.Levers.Single().Opens.Value.Kind, Is.EqualTo("Store"));
            ((bool[])sim.Config.Game.Godown.Holds)[sim.Config.Game.Market.IndexOf("radish")] = true; // were it a grain
            alert = Alerts.Build(ViewKit2.Context(sim)).Single(a => a.Text.StartsWith("Store full"));
            Assert.That(alert.Levers.Select(l => l.Label), Is.EqualTo(new[] { "Sell some", "Rent space" }));
            Assert.That(alert.Levers.Select(l => l.Opens.Value.Kind), Is.EqualTo(new[] { "Store", "Godown" }));
            ((bool[])sim.Config.Game.Godown.Holds)[sim.Config.Game.Market.IndexOf("radish")] = false;

            AwayVm card = AwayView.People(ctx, new Sim.Core.Events.SimEvent[0]);
            Assert.That(card.Lines.Single(l => l.Label == "Still standing").Value, Is.EqualTo(ctx.Fmt.Kg(left) + " of radish on " + Sim.View.Views.PeopleWords.Field(ctx, 85) + " (the store was full)"));
            card = AwayView.People(ctx, new Sim.Core.Events.SimEvent[] { new Sim.Core.Events.OrderWaiting(T(sim), 1, Sim.Core.People.OrderTargetKind.Field, 85, Sim.Core.People.OrderWait.StoreFull) });
            Assert.That(card.Lines.Any(l => l.Label == "Orders"), Is.False, "the standing line already says it");

            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] = 0; // room again
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Any(a => a.Text.StartsWith("Store full")), Is.False, "it all fits: nothing to say");
        }
    }
}
