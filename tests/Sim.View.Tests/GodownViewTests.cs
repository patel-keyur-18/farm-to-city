using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// ADR 0103 (owner, 2026-11-12): the godown sheet says how goods get there (by themselves), lists each good with where it is, and the
    /// rented space can be ended from the next season in one plain step.
    /// </summary>
    public class GodownViewTests
    {
        private const long Coin = 100, Kg = 1000;

        /// <summary>A Hamlet town with plenty of cash and no dry goods in the store.</summary>
        private static Simulation Hamlet()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            sim.State.Era.Current = sim.Config.Game.Godown.UnlockEra;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 500_000 * Coin), new Posting(Account.OpeningEquity, -500_000 * Coin));
            for (int g = 0; g < sim.Config.Game.Market.Goods.Count; g++) if (GodownRules.Holds(sim, g)) sim.State.Market.Stock[g] = 0; // the inherited store's grain and seed
            return sim;
        }

        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        private static SheetVm Sheet(Simulation sim) { return GodownSheet.Build(ViewKit2.Context(sim)); }

        private static ActionVm Action(SheetVm vm, string id)
        {
            return vm.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).FirstOrDefault(a => a.Id == id);
        }

        private static void Run(Simulation sim, ActionVm a)
        {
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Enabled, Is.True, a.DisabledReason);
            Assert.That(sim.Execute(a.Create(sim.State.Clock.SimTime)).Accepted, Is.True, a.Label);
        }

        private static LineVm Row(SheetVm vm, string label) { return vm.Lines.Single(l => l.Label == label); }

        // How it works comes first, and every dry good is listed (even at none) so the player sees what counts; potatoes are said to stay home.
        [Test]
        public void SaysHowGoodsGetThere()
        {
            Simulation sim = Hamlet();
            SheetVm vm = Sheet(sim);
            Assert.That(vm.Lines[0].Label, Is.EqualTo("How it works"));
            LineVm how = vm.Lines[1];
            Assert.That(how.Label, Is.EqualTo("Goods come here by themselves"));
            Assert.That(how.Note, Does.Contain("harvest").And.Contain("Nothing to carry"));
            Assert.That(Row(vm, "Wheat").Value, Is.EqualTo("None yet"));
            Assert.That(vm.Lines.Any(l => l.Label == "Potato"), Is.False, "a cold-store good is not listed as kept");
            Assert.That(Row(vm, "Stay in the hut").Note, Does.Contain("Potatoes").And.Contain("cold store"));
            Assert.That(vm.Lines.Any(l => l.Label != null && l.Label.StartsWith("No space yet")), Is.True, "no godown and no rented space: say what to do");
        }

        // A harvest counts: it is stock, so with space it is "here" at once, and the Store's row says so.
        [Test]
        public void HarvestGoesInByItself()
        {
            Simulation sim = Hamlet();
            int wheat = Good(sim, "wheat");
            Run(sim, Action(Sheet(sim), "rentMore"));
            Run(sim, Action(Sheet(sim), "rentMore")); // 2 t rented
            sim.State.Market.Stock[wheat] += 2_500 * Kg; // a harvest

            SheetVm vm = Sheet(sim);
            Assert.That(Row(vm, "Wheat").Value, Is.EqualTo("2,000 kg here · 500 kg in the hut"));
            Assert.That(Row(vm, "Wheat").WhyKey, Is.EqualTo("godownGood"));

            SheetVm store = StoreSheet.Build(ViewKit2.Context(sim));
            Assert.That(store.Lines.Single(l => l.Label == "Wheat").Note, Is.EqualTo("2,000 kg of it is in the godown in town"));
        }

        // Stopping: the status says "renews", one tap ends it from next season, and Keep renting undoes it.
        [Test]
        public void StopRenting_AndKeepRenting()
        {
            Simulation sim = Hamlet();
            Run(sim, Action(Sheet(sim), "rentMore"));
            Run(sim, Action(Sheet(sim), "rentMore")); // 2 t

            SheetVm vm = Sheet(sim);
            LineVm status = Row(vm, "At the next close");
            Assert.That(status.Value, Does.StartWith("Renews"));
            Assert.That(status.Note, Does.Contain("every season until you stop"));
            Assert.That(Action(vm, "keepRenting"), Is.Null);

            Run(sim, Action(vm, "stopRenting"));
            Assert.That(sim.State.Godown.RentedTonnes, Is.EqualTo(2), "this season's space is paid for and stays");
            Assert.That(sim.State.Godown.RentNextTonnes, Is.EqualTo(0));
            vm = Sheet(sim);
            Assert.That(Row(vm, "At the next close").Value, Is.EqualTo("Ends · nothing to pay"));
            Assert.That(Action(vm, "stopRenting"), Is.Null);
            Assert.That(Action(vm, "keepRenting").Label, Is.EqualTo("Keep renting 2 t"));

            Run(sim, Action(vm, "keepRenting"));
            Assert.That(sim.State.Godown.RentNextTonnes, Is.EqualTo(2));
            Assert.That(Row(Sheet(sim), "At the next close").Value, Does.StartWith("Renews"));
        }

        // With goods in the space it can't end: Stop says why, and a stop made while empty is shown as renewing anyway once goods arrive.
        [Test]
        public void StopRenting_GoodsKeepItGoing()
        {
            Simulation sim = Hamlet();
            int wheat = Good(sim, "wheat");
            Run(sim, Action(Sheet(sim), "rentMore"));
            sim.State.Market.Stock[wheat] += 800 * Kg;

            ActionVm stop = Action(Sheet(sim), "stopRenting");
            Assert.That(stop.Enabled, Is.False);
            Assert.That(stop.DisabledReason, Does.Contain("Sell or use some first"));

            // The other way round: stopped while empty, then a harvest lands before the close.
            Simulation other = Hamlet();
            Run(other, Action(Sheet(other), "rentMore"));
            Run(other, Action(Sheet(other), "stopRenting"));
            other.State.Market.Stock[Good(other, "wheat")] += 800 * Kg;
            SheetVm vm = Sheet(other);
            LineVm status = Row(vm, "At the next close");
            Assert.That(status.Value, Is.EqualTo("Renews anyway"));
            Assert.That(status.Note, Does.Contain("Your goods need 1 t").And.Contain("Sell or use them"));
            Assert.That(status.NoteTone, Is.EqualTo(Tone.Warn));
            Assert.That(GodownRules.NextCloseTonnes(other), Is.EqualTo(1));
        }

        // Spending asks (a confirm card); ending or cutting the rent spends nothing and just happens, one tap.
        [Test]
        public void StopRenting_AsksNothing()
        {
            Simulation sim = Hamlet();
            long at = sim.State.Clock.SimTime;
            ConfirmCardVm more = ConfirmCards.For(ViewKit2.Context(sim), new RentWarehouse(at, 1));
            Assert.That(more, Is.Not.Null, "renting costs coins: the tap card stays");
            Run(sim, Action(Sheet(sim), "rentMore"));
            Run(sim, Action(Sheet(sim), "rentMore"));
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new RentWarehouse(at, 0)), Is.Null, "stop renting: no card");
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new RentWarehouse(at, 1)), Is.Null, "1 t less: no card");
        }

        // A good wholly in town says so without a "0 kg in the hut".
        [Test]
        public void AllInTown_NoZeroInTheHut()
        {
            Simulation sim = Hamlet();
            Run(sim, Action(Sheet(sim), "rentMore"));
            sim.State.Market.Stock[Good(sim, "rice")] += 400 * Kg;
            Assert.That(Row(Sheet(sim), "Rice").Value, Is.EqualTo("400 kg here"));
        }
    }
}
