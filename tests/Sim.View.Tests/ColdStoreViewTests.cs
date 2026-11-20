using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Market;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-242 (spec 025 §3.6, Preview 7): the Store's button, the cold store's sheet before and after renting, what each good keeps, whether it pays, and every number's Why?.</summary>
    public class ColdStoreViewTests
    {
        private static Simulation Township()
        {
            Simulation sim = Situations.Township();
            Situations.SetCash(sim, 5_000_000_00);
            return sim;
        }

        private static SheetVm Sheet(Simulation sim) { return ColdStoreSheet.Build(ViewKit2.Context(sim)); }

        private static ActionVm Action(SheetVm vm, string id) { return vm.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).FirstOrDefault(a => a.Id == id); }

        private static void Run(Simulation sim, ActionVm a)
        {
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Enabled, Is.True, a.DisabledReason);
            Assert.That(sim.Execute(a.Create(sim.State.Clock.SimTime)).Accepted, Is.True, a.Label);
        }

        private static LineVm Row(SheetVm vm, string label) { return vm.Lines.Single(l => l.Label == label); }

        [Test]
        public void StoreButton_FromTheTownship()
        {
            Simulation early = ViewKit.NewGame();
            Assert.That(StoreSheet.Build(ViewKit2.Context(early)).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Any(a => a.Id == "coldStore"), Is.False, "not before the Township");
            Simulation sim = Township();
            ActionVm button = StoreSheet.Build(ViewKit2.Context(sim)).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(a => a.Id == "coldStore");
            Assert.That(button.Label, Is.EqualTo("Cold store in town"));
            Assert.That(button.Opens.Value.Kind, Is.EqualTo(ColdStoreSheet.Kind));
            Assert.That(button.Detail, Is.EqualTo("Keep potatoes and fruit"));
        }

        [Test]
        public void Sheet_BeforeRenting_SaysWhatKeepsAndWhatPays()
        {
            Simulation sim = Township();
            SheetVm vm = Sheet(sim);
            Assert.That(vm.Title, Is.EqualTo("Cold store in the mandi town"));
            Assert.That(Row(vm, "Rented").Value, Is.EqualTo("None"));
            Assert.That(Action(vm, "coldMore").Detail, Does.Contain("a season"));
            Assert.That(Action(vm, "coldStop"), Is.Null, "nothing to stop");
            Assert.That(Row(vm, "Potato").Note, Is.EqualTo("Keeps 99 of every 100 kg over a season (89 in the hut)").Or.EqualTo("Keeps 99 of every 100 kg over a season (88 in the hut)"));
            Assert.That(Row(vm, "Mango").Note, Does.StartWith("Keeps 81 of every 100 kg"));
            Assert.That(vm.Lines.Any(l => l.Label == "Seed potato"), Is.False, "seed potato is left out (spoils 0.1% a day)");
            Assert.That(vm.Lines.Any(l => l.Label == "Potato, Spring to Monsoon" && l.Value.StartsWith("+")), Is.True, "the worked answer");
            Assert.That(vm.Lines.Any(l => l.Label == "Mango, Summer to Monsoon" && l.Value.StartsWith("+")), Is.True);
            LineVm guava = vm.Lines.Single(l => l.Label == "Guava" && l.WhyKey == "coldWorth");
            Assert.That(guava.Value, Is.EqualTo("Not worth it"));
        }

        [Test]
        public void Renting_ShowsTheStatus_AndStopKeepWork()
        {
            Simulation sim = Township();
            Run(sim, Action(Sheet(sim), "coldMore"));
            SheetVm vm = Sheet(sim);
            Assert.That(Row(vm, "Rented").Value, Is.EqualTo("1 t · empty"));
            Assert.That(Row(vm, "At the next close").Value, Does.StartWith("Renews"));
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] = 600_000;
            vm = Sheet(sim);
            Assert.That(Row(vm, "Rented").Value, Is.EqualTo("1 t · 600 kg stored"));
            Assert.That(Row(vm, "Potato").Value, Is.EqualTo("600 kg here"));
            Assert.That(Action(vm, "coldStop").Enabled, Is.False, "the potatoes need the space: it can't be stopped");
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] = 0;
            Run(sim, Action(Sheet(sim), "coldStop"));
            vm = Sheet(sim);
            Assert.That(Row(vm, "At the next close").Value, Does.StartWith("Ends"));
            Run(sim, Action(vm, "coldKeep"));
            Assert.That(Row(Sheet(sim), "At the next close").Value, Does.StartWith("Renews"));
            sim.State.Godown.ColdRentNextTonnes = 0; // as if booked down before the potatoes came in
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] = 600_000;
            Assert.That(Row(Sheet(sim), "At the next close").Value, Is.EqualTo("Renews anyway"));
        }

        [Test]
        public void Store_SaysWhatIsInTheColdStore()
        {
            Simulation sim = Township();
            Run(sim, Action(Sheet(sim), "coldMore"));
            int potato = sim.Config.Game.Market.IndexOf("potato");
            sim.State.Market.Stock[potato] = 300_000;
            LineVm row = StoreSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Potato" && l.Opens.HasValue);
            Assert.That(row.Note, Is.EqualTo("300 kg of it is in the cold store in town"));
        }

        [Test]
        public void EveryNumberHasWhy()
        {
            Simulation sim = Township();
            Run(sim, Action(Sheet(sim), "coldMore"));
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] = 600_000;
            ViewContext ctx = ViewKit2.Context(sim);
            int n = 0;
            foreach (LineVm l in Sheet(sim).Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
                foreach (WhyRowVm r in why.Rows) Assert.That((r.Label ?? "") + (r.Value ?? ""), Does.Not.Contain("{"), l.WhyKey);
                n++;
            }
            Assert.That(n, Is.EqualTo(7), "the rent, three goods keeps, three worked answers");
        }

        [Test]
        public void Why_Potato_WorkedAnswer()
        {
            Simulation sim = Township();
            int potato = sim.Config.Game.Market.IndexOf("potato");
            WhyVm why = WhyView.Build(ViewKit2.Context(sim), "coldWorth", potato);
            Assert.That(why.Title, Is.EqualTo("Potato, Spring to Monsoon"));
            Assert.That(why.Value, Does.StartWith("+"));
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("What you keep"));
            Assert.That(why.Rows.Select(r => r.Label).Any(l => l.StartsWith("Rent for 2 seasons")), Is.True);
        }

        [Test]
        public void SeasonDue_NamesTheColdStore()
        {
            Simulation sim = Township();
            Run(sim, Action(Sheet(sim), "coldMore"));
            Assert.That(SeasonBook.Build(ViewKit2.Context(sim), false).Due.Any(l => l.Key == "coldStore" && l.Label == "Cold store in town"), Is.True);
        }

        [Test]
        public void Godown_PointsHere()
        {
            Simulation sim = Township();
            Assert.That(GodownSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Stay in the hut").Note, Does.Contain("cold store"));
        }
    }
}
