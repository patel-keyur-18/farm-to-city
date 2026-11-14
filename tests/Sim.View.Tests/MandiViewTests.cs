using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-252 (ADR 0107 §14, spec 004 §3.7): the mandi sheet loads several crops, best gain over the trader first, within the trolley.</summary>
    public class MandiViewTests
    {
        private const long Kg = 1000;

        /// <summary>A store holding potato, rice, wheat and okra (more than a trolley carries together).</summary>
        private static Simulation Stocked(System.Func<string, string, string> edit = null)
        {
            Simulation sim = ViewKit.NewGame(seed: 5, edit: edit);
            foreach (var kv in new[] { ("potato", 1200L), ("okra", 400L), ("rice", 600L), ("wheat", 900L) })
                sim.State.Market.Stock[sim.Config.Game.Market.IndexOf(kv.Item1)] = kv.Item2 * Kg;
            return sim;
        }

        private static System.Collections.Generic.List<LineVm> Rows(SheetVm vm) { return vm.Lines.Where(l => l.Amount != null).ToList(); }

        private static string Arg(Simulation sim, params (string good, long kg)[] loads)
        {
            return string.Join(";", loads.Select(l => sim.Config.Game.Market.IndexOf(l.good) + ":" + l.kg * Kg));
        }

        // AC: every crop in the store, with what the mandi pays a kg after commission beside the trader's, best gain first.
        [Test]
        public void SeveralCrops()
        {
            Simulation sim = Stocked();
            var rows = Rows(MandiSheet.Build(ViewKit2.Context(sim), null));
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(4));
            var gains = rows.Select(r => MandiSheet.Gain(sim, int.Parse(r.Amount.Id.Substring(5)))).ToList();
            Assert.That(gains.Select(g => g.Raw), Is.Ordered.Descending, "best gain first");
            Assert.That(rows[0].Label, Is.EqualTo("Potato"));
            Assert.That(rows[0].Note, Does.Contain("after commission").And.Contain("trader").And.Contain("a kg"));
            Assert.That(rows.All(r => r.Amount.Step == 50 * Kg && r.Amount.Min == 0 && r.Amount.Then != null), Is.True);
        }

        // AC: each slider stops at the least of what's free and the room the other crops leave in the 2,000 kg trolley.
        [Test]
        public void RoomLeft()
        {
            Simulation sim = Stocked();
            ViewContext ctx = ViewKit2.Context(sim);
            var rows = Rows(MandiSheet.Build(ctx, Arg(sim, ("potato", 1200))));
            LineVm potato = rows.Single(r => r.Label == "Potato"), okra = rows.Single(r => r.Label == "Okra"), wheat = rows.Single(r => r.Label == "Wheat");
            Assert.That(potato.Amount.Max, Is.EqualTo(1200 * Kg), "its own load is what it holds already");
            Assert.That(okra.Amount.Max, Is.EqualTo(400 * Kg));
            Assert.That(wheat.Amount.Max, Is.EqualTo(800 * Kg), "2,000 less the potato's 1,200");
            Assert.That(wheat.Amount.Span, Is.EqualTo(900 * Kg), "the track is the whole stock; the rest of it is room the potato holds");
            Assert.That(wheat.Amount.MaxCaption, Is.EqualTo("most you can add: 800 kg"));

            // A load that would overfill the trolley is cut back to what fits, in the rows' order.
            rows = Rows(MandiSheet.Build(ctx, Arg(sim, ("potato", 1200), ("wheat", 900), ("okra", 400))));
            long total = 0;
            foreach (LineVm r in rows) total += r.Amount.Start;
            Assert.That(total, Is.LessThanOrEqualTo(2000 * Kg));
            Assert.That(rows.Single(r => r.Label == "Okra").Amount.Start, Is.EqualTo(400 * Kg), "okra pays more, so it keeps its place before the wheat");
            Assert.That(rows.Single(r => r.Label == "Wheat").Amount.Start, Is.LessThan(900 * Kg));
        }

        // AC: Fill loads the crops in order of gain until the trolley is full; a crop the trader pays as much for stays out; Go sends it all.
        [Test]
        public void FillBest()
        {
            Simulation sim = Stocked();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm start = MandiSheet.Build(ctx, null);
            Assert.That(start.Actions.Single().Enabled, Is.False, "nothing loaded: Go is greyed");
            ActionVm fill = start.Lines.Select(l => l.Action).Single(a => a != null && a.Id == "replace:fill");
            Assert.That(fill.Label, Is.EqualTo("Fill with what pays best"));
            SheetVm filled = MandiSheet.Build(ctx, fill.Opens.Value.Arg);
            var rows = Rows(filled);
            Assert.That(rows.Single(r => r.Label == "Potato").Amount.Start, Is.EqualTo(1200 * Kg));
            Assert.That(rows.Single(r => r.Label == "Okra").Amount.Start, Is.EqualTo(400 * Kg));
            Assert.That(rows.Sum(r => r.Amount.Start), Is.EqualTo(2000 * Kg), "full");
            GoToMandi go = (GoToMandi)filled.Actions.Single().Create(sim.State.Clock.SimTime);
            Assert.That(go.Loads.Sum(l => l.Quantity), Is.EqualTo(2000 * Kg));
            Assert.That(sim.Execute(go).Accepted, Is.True, "one trip with every load");
        }

        // A crop the trader pays as much for (here a 20% commission makes the mandi pay less than the trader for wheat) is left out of Fill.
        [Test]
        public void FillBest_LeavesOutACropTheTraderPaysAsMuchFor()
        {
            Simulation sim = Stocked((f, j) => f == "market.json" ? j.Replace("\"commissionRate\": 0.03", "\"commissionRate\": 0.2") : j);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(MandiSheet.Gain(sim, sim.Config.Game.Market.IndexOf("wheat")).Raw, Is.LessThanOrEqualTo(0));
            Assert.That(MandiSheet.Gain(sim, sim.Config.Game.Market.IndexOf("potato")).Raw, Is.GreaterThan(0));
            ActionVm fill = MandiSheet.Build(ctx, null).Lines.Select(l => l.Action).Single(a => a != null && a.Id == "replace:fill");
            var rows = Rows(MandiSheet.Build(ctx, fill.Opens.Value.Arg));
            Assert.That(rows.Single(r => r.Label == "Potato").Amount.Start, Is.EqualTo(1200 * Kg));
            Assert.That(rows.Single(r => r.Label == "Wheat").Amount.Start, Is.EqualTo(0));
            Assert.That(rows.Single(r => r.Label == "Wheat").Note, Does.Contain("the trader pays more"));
        }

        // The sum names each loaded crop and, with the truck away, no trader's figure to compare with.
        [Test]
        public void SumLines()
        {
            Simulation sim = Stocked();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = MandiSheet.Build(ctx, Arg(sim, ("potato", 700), ("okra", 300)));
            Assert.That(vm.Lines.Any(l => l.Label.StartsWith("Potato · 700 kg at")), Is.True);
            Assert.That(vm.Lines.Any(l => l.Label.StartsWith("Okra · 300 kg at")), Is.True);
            Assert.That(vm.Lines.Any(l => l.Label == "At the mandi"), Is.True);
            Assert.That(vm.Lines.Any(l => l.Label == "You get"), Is.True);
            Assert.That(vm.Lines.Single(l => l.Label == "Trolley in the trolley" || l.Label == "In the trolley").Value, Does.StartWith("1,000.0 kg of 2,000.0 kg").Or.StartWith("1,000 kg of 2,000 kg"));
        }
    }
}
