using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-243 (spec 025 §3.3, Preview 3): the kiln's market, plan, placement card, day, its bricks on a house card and Building materials, and every number's Why?.</summary>
    public class KilnViewTests
    {
        private static int TypeOf(Simulation sim) { return sim.Config.Game.Businesses.IndexOfType("brick_kiln"); }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
            }
        }

        private static void ToSeason(Simulation sim, Season season)
        {
            while (Calendar.SeasonAt(sim.State.Clock.SimTime + 1, sim.Clock) != season) sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
        }

        // The Businesses sheet has the kiln's picture button beside the others (from the Village), with its cost.
        [Test]
        public void Menu_Button()
        {
            Simulation sim = Situations.Township();
            SheetVm vm = BusinessesSheet.Build(ViewKit2.Context(sim));
            ActionVm start = vm.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(a => a.Id == "start:brick_kiln");
            Assert.That(start.Label, Is.EqualTo("Start a brick kiln"));
            Assert.That(start.Detail, Does.StartWith("Kiln and yard "));
            Assert.That(start.Enabled, Is.True);
        }

        // Step 1: the market, every row with a Why?.
        [Test]
        public void Research()
        {
            Simulation sim = Situations.Township();
            SheetVm vm = BusinessResearchSheet.Build(ViewKit2.Context(sim), TypeOf(sim));
            Assert.That(vm.Title, Is.EqualTo("A brick kiln: the market"));
            Assert.That(vm.Lines.Select(l => l.Label), Is.EqualTo(new[]
            {
                "Bricks the town's builds wait for", "The trader sells the town a day", "The trader would take your bricks a day", "Kiln fuel", "A kiln hand's wage", "It fires", "Kiln and yard",
            }));
            Assert.That(vm.Lines[1].Value, Does.StartWith("2,000 at "));
            Assert.That(vm.Lines[3].Value, Does.Contain("0.4 kg a brick"));
            Assert.That(vm.Lines[5].Value, Is.EqualTo("not in the Monsoon"));
            AssertWhys(sim, vm);
        }

        // Step 2: the plan has the season that fires, the Monsoon that costs, and a payback that counts both.
        [Test]
        public void Plan()
        {
            Simulation sim = Situations.Township();
            int type = TypeOf(sim);
            SheetVm vm = BusinessPlanSheet.Build(ViewKit2.Context(sim), type, false);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("A season that fires").And.Contain("Bricks sold to the trader").And.Contain("Fuel").And.Contain("The Monsoon, nothing fired"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Not.Contain("Grinding fees"));
            BusinessRules.Projection p = BusinessRules.Projected(sim, sim.Config.Game.Businesses.Types[type], false, 0, Sim.Core.Maths.Fixed.Zero);
            Assert.That(p.Off, Is.EqualTo(p.Wage + p.Repairs + p.Depreciation));
            long year = (3 * p.Profit - p.Off) / 4;
            Assert.That(vm.Lines.First(l => l.Label == "Pays back its cost in").Value, Is.EqualTo(sim.Config.Game.Businesses.Types[type].Kiln == null ? "" : year > 0 ? "about " + ((p.Cost + year - 1) / year) + " seasons" : "Never, at these figures"));
            AssertWhys(sim, vm);
        }

        // The confirm card of founding says what the smoke touches, counted from the map, and that the placement ghost's footprint is the same acres.
        [Test]
        public void Placement_Card()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            int type = TypeOf(sim);
            int tile = -1;
            foreach (Tile t in sim.State.Land.Tiles) if (tile < 0 && BusinessRules.Place(sim, t.Id) == RejectReason.None) tile = t.Id;
            var foot = BusinessPlacement.SmokeTiles(ctx, type, tile);
            Assert.That(foot, Does.Contain(tile));
            Assert.That(foot.Count, Is.InRange(1, 5), "the acre and the four beside it");
            ConfirmCardVm card = ConfirmCards.For(ctx, new FoundBusiness(ctx.Now, "brick_kiln", tile, 400_000_00, false));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Warning, Does.StartWith("Its smoke lowers land value on " + foot.Count + " acre"));
            Assert.That(BusinessPlacement.SmokeTiles(ctx, sim.Config.Game.Businesses.IndexOfType("flour_mill"), tile), Is.Empty, "no other business smokes");
        }

        // Step 4: the day: bricks made, in the yard, sent to your builds, sold to the trader, a brick's cost; the Monsoon's note.
        [Test]
        public void Today()
        {
            Simulation sim = Situations.Kiln();
            ToSeason(sim, Season.Winter);
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400) * 86_400 + 19 * 3600 + (sim.State.Clock.SimTime % 86_400 > 19 * 3600 ? 86_400 : 0));
            Business b = sim.State.Businesses.Businesses.Last();
            SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(vm.Title, Is.EqualTo("Brick kiln"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Bricks made today").And.Contain("Bricks in the yard").And.Contain("Sent to your builds today").And.Contain("Sold to the trader today").And.Contain("A brick costs you"));
            string ten = ViewKit2.Context(sim).Fmt.Integer(KilnRules.PerHour(sim, b) * 10);
            Assert.That(vm.Lines.First(l => l.Label == "Bricks made today").Value, Is.EqualTo(ten + " of " + ten), "ten open hours at the hand's rate");
            Assert.That(vm.Lines.First(l => l.Label == "A brick costs you").Value, Does.EndWith("in fuel"));
            Assert.That(vm.Lines.Any(l => l.Label.StartsWith("Wet clay")), Is.False);
            AssertWhys(sim, vm);

            ToSeason(sim, Season.Monsoon);
            sim.AdvanceTo(sim.State.Clock.SimTime + 12 * 3600);
            SheetVm wet = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(wet.Subtitle, Is.EqualTo("Closed for the Monsoon · fires again in Winter"));
            Assert.That(wet.Lines.Any(l => l.Label.StartsWith("Wet clay can't be fired")), Is.True);
            AssertWhys(sim, wet);
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings).Lines.Count, Is.GreaterThan(0));
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Books).Lines.Count, Is.GreaterThan(0));
        }

        // Your own house takes your yard's bricks at cost: the card says how many, what they cost and what they save; Building materials names the kiln's bricks and its yard.
        [Test]
        public void House_And_Materials()
        {
            Simulation sim = Situations.Kiln();
            ToSeason(sim, Season.Winter);
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400) * 86_400 + 19 * 3600 + (sim.State.Clock.SimTime % 86_400 > 19 * 3600 ? 86_400 : 0));
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = -1;
            foreach (Tile t in sim.State.Land.Tiles) if (tile < 0 && HouseRules.Place(sim, t.Id) == RejectReason.None) tile = t.Id;
            ConfirmCardVm card = ConfirmCards.For(ctx, new BuildFamilyHouse(ctx.Now, tile));
            Assert.That(card.Rows.Select(r => r.Label), Does.Contain("Bricks from your kiln: 2,400 at cost").And.Contain("Cement and timber from the trader"));
            Assert.That(card.Rows.Select(r => r.Label), Does.Not.Contain("Materials: bricks, cement, timber"));
            Assert.That(card.Warning, Does.Contain("more with the trader's bricks"));

            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400 + 1) * 86_400 + 7 * 3600); // the trader's morning: the kiln sells him bricks
            ctx = ViewKit2.Context(sim);
            SheetVm mats = BuildingSheet.Build(ctx);
            Assert.That(mats.Lines.Select(l => l.Label), Does.Contain("· of them from your kiln").And.Contain("Your kiln").And.Contain("Bricks in the yard"));
            AssertWhys(sim, mats);
        }
    }
}
