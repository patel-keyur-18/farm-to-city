using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.CreditKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 005 §3.9–3.10, ADR 0038: the final notice, foreclosure, what is seized and what is protected (AC-25 to AC-28, AC-34).</summary>
    public class ForeclosureTests
    {
        private static int ParcelOf(Simulation sim, string id) { return sim.Config.Game.Map.IndexOfParcel(id); }

        private static long Price(Simulation sim, string id) { return LandRules.ParcelPrice(sim, sim.State.Land.Parcels[ParcelOf(sim, id)]); }

        /// <summary>A founder who owns w1 (financed by the bank with <paramref name="bank"/> coins), owes the moneylender, and has no cash.</summary>
        private static Simulation Indebted(long bank = 60_000, long moneylender = 20_000)
        {
            Simulation sim = NewSim(cash: 1_000_000, freeBank: false);
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", Coins(bank), 8))).Accepted, Is.True);
            if (moneylender > 0) Assert.That(Take(sim, "moneylender", moneylender, 1).Accepted, Is.True);
            return sim;
        }

        // AC-25: bought land (with its crop and tree) is seized at quick-sale value, the inherited farm is untouched,
        // the bank is repaid first, the rest is written off, every loan is closed, the score is 350, the books balance.
        [Test]
        public void SeizeRepayWriteOff()
        {
            Simulation sim = Indebted(bank: 60_000, moneylender: 20_000);
            int w1 = ParcelOf(sim, "w1");
            Assert.That(sim.Execute(new Sow(0, 82, "potato", SeedSource.Certified, "none")).Accepted, Is.True, "a crop on the bought land");
            Assert.That(sim.Execute(new Sow(0, 86, "potato", SeedSource.Certified, "none")).Accepted, Is.True, "a crop on the inherited land");
            TreeSlot boughtSlot = sim.State.Land.Slots.First(s => s.TileId == 82 && s.Kind == SlotKind.FieldEdge);
            TreeSlot yardSlot = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard);
            Assert.That(sim.Execute(new PlantTree(0, boughtSlot.Id, "banana")).Accepted, Is.True);
            Assert.That(sim.Execute(new PlantTree(0, yardSlot.Id, "mango")).Accepted, Is.True);
            Burn(sim);

            sim.AdvanceTo(2 * S); // the moneylender's loan is two closes unpaid: default
            Assert.That(sim.State.Credit.PendingForeclosure, Is.True);
            Assert.That(sim.State.Land.Parcels[w1].Owned, Is.True, "not yet: the final notice runs 24 live hours");
            sim.DrainEvents();
            sim.AdvanceTo(2 * S + 24 * 3600);

            Assert.That(sim.State.Credit.PendingForeclosure, Is.False);
            var seizure = sim.DrainEvents();
            ForeclosureCompleted done = Last<ForeclosureCompleted>(seizure);
            long quickSale = seizure.OfType<ParcelSeized>().Single().QuickSaleValue;
            Assert.That(quickSale, Is.GreaterThan(0));
            Assert.That(done, Is.Not.Null);
            Assert.That(done.Parcels, Is.EqualTo(new[] { "w1" }));
            Assert.That(done.NewScore, Is.EqualTo(350));
            Assert.That(sim.State.Credit.Score, Is.EqualTo(350));
            Assert.That(sim.State.Land.Parcels[w1].Owned, Is.False);
            Assert.That(sim.State.Farm.CropOn(82), Is.Null, "the crop on bought land is lost");
            Assert.That(sim.State.Farm.TreeIn(boughtSlot.Id), Is.Null, "so is its tree");
            Assert.That(sim.State.Farm.CropOn(86), Is.Not.Null, "the inherited acre keeps its crop");
            Assert.That(sim.State.Farm.TreeIn(yardSlot.Id), Is.Not.Null, "and the yard its tree");
            Assert.That(sim.State.Land.Parcels.Where(p => p.Protected).All(p => p.Owned), Is.True);
            Assert.That(sim.State.Credit.Loans.All(l => !l.IsOpen), Is.True);
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Balance(Account.MoneylenderLoans), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Balance(Account.InterestPayable), Is.EqualTo(0));
            ReconcileOk(sim);

            // The land covered both debts: the bank and the moneylender are repaid in full and nothing is written off.
            Assert.That(sim.State.Credit.Loans.All(l => l.State == LoanState.Repaid), Is.True);
            Assert.That(done.Proceeds, Is.EqualTo(quickSale), "land at quick-sale value; no produce was needed");
            Assert.That(done.Produce, Is.Empty);
            Assert.That(done.WrittenOff, Is.EqualTo(0));
        }

        // AC-25: when the sale doesn't cover the debts, the bank (secured) is paid first and the moneylender gets nothing;
        // the rest is written off and recorded as 4900.
        [Test]
        public void BankFirst_ThenWriteOff()
        {
            Simulation sim = NewSim(cash: 1_000_000, freeBank: false);
            for (int k = 0; k < 4; k++) FakeReport(sim, Coins(400_000)); // a big farm income lifts the bank's limit to the price
            long price = Price(sim, "w1");
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", price - price % Coins(1), 16))).Accepted, Is.True, "100% financed");
            Assert.That(Take(sim, "moneylender", 20_000, 1).Accepted, Is.True);
            Burn(sim);
            sim.AdvanceTo(2 * S);
            sim.DrainEvents();
            sim.AdvanceTo(2 * S + 24 * 3600);
            var events = sim.DrainEvents();
            ForeclosureCompleted done = Last<ForeclosureCompleted>(events);
            Assert.That(sim.State.Credit.Loans[0].State, Is.EqualTo(LoanState.WrittenOff).Or.EqualTo(LoanState.Repaid), "the bank, paid first");
            Loan bank = sim.State.Credit.Loans[0], lender = sim.State.Credit.Loans[1];
            Assert.That(events.OfType<LoanRepayment>().Select(r => r.LoanId).First(), Is.EqualTo(bank.Id), "the bank is paid first");
            Assert.That(done.WrittenOff, Is.GreaterThan(0));
            Assert.That(sim.State.Ledger.Balance(Account.DebtWrittenOff), Is.EqualTo(-done.WrittenOff));
            Assert.That(lender.State, Is.EqualTo(LoanState.WrittenOff), "nothing was left for the moneylender");
            ReconcileOk(sim);
        }


        // AC-26: proceeds above all debts: the surplus stays in the founder's cash.
        [Test]
        public void Surplus_ReturnsToFounder()
        {
            Simulation sim = NewSim(cash: 1_000_000, freeBank: false);
            Assert.That(sim.Execute(new BuyParcel(0, "w1")).Accepted, Is.True);
            Assert.That(Take(sim, "moneylender", 1_000, 1).Accepted, Is.True);
            Burn(sim);
            sim.AdvanceTo(2 * S);
            sim.DrainEvents();
            sim.AdvanceTo(2 * S + 24 * 3600);
            var events = sim.DrainEvents();
            ForeclosureCompleted done = Last<ForeclosureCompleted>(events);
            long quickSale = events.OfType<ParcelSeized>().Single().QuickSaleValue;
            Assert.That(done.WrittenOff, Is.EqualTo(0));
            Assert.That(sim.State.Credit.Loans[0].State, Is.EqualTo(LoanState.Repaid));
            long owed = Coins(1_000) + sim.State.Finance.Reports.Sum(r => -r.ByAccount.Where(a => a.Key == Account.Interest || a.Key == Account.PenaltyInterest).Sum(a => a.Value));
            Assert.That(Cash(sim), Is.GreaterThan(quickSale - owed - Coins(200)), "the surplus is paid into cash");
            Assert.That(Cash(sim), Is.LessThanOrEqualTo(quickSale));
            ReconcileOk(sim);
        }

        // AC-27: every default raises a final notice; foreclosure waits for 24 hours of live play and away time doesn't count;
        // curing the arrears cancels it.
        [Test]
        public void FinalNotice_LiveHours()
        {
            Simulation sim = Indebted(bank: 60_000, moneylender: 20_000);
            Burn(sim);
            sim.AdvanceTo(2 * S - 1);
            sim.DrainEvents();
            sim.AdvanceTo(3 * S - 1, AdvanceMode.Away); // the default is reached while away (at 2 S) and a day passes
            var events = sim.DrainEvents();
            Assert.That(events.OfType<LoanDefaulted>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<FinalNotice>().Count(), Is.GreaterThanOrEqualTo(1), "announced again on return");
            Assert.That(sim.State.Credit.PendingForeclosure, Is.True);
            Assert.That(sim.State.Credit.NoticeLiveSeconds, Is.EqualTo(0), "away time doesn't count");
            Assert.That(sim.State.Land.Parcels[ParcelOf(sim, "w1")].Owned, Is.True);

            sim.AdvanceTo(sim.State.Clock.SimTime + 23 * 3600);
            Assert.That(sim.State.Credit.PendingForeclosure, Is.True, "23 live hours are not enough");
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(sim.State.Credit.PendingForeclosure, Is.False, "the 24th hour forecloses");
            Assert.That(sim.State.Land.Parcels[ParcelOf(sim, "w1")].Owned, Is.False);

            // Cured in time: the notice is cancelled and nothing is seized.
            Simulation cured = Indebted(bank: 60_000, moneylender: 20_000);
            Burn(cured);
            cured.AdvanceTo(2 * S);
            Assert.That(cured.State.Credit.PendingForeclosure, Is.True);
            AddCash(cured, 100_000);
            Loan late = cured.State.Credit.Loans.Single(l => l.State == LoanState.Defaulted);
            Assert.That(cured.Execute(new RepayLoan(2 * S, late.Id, CreditRules.OwedNow(cured, late))).Accepted, Is.True);
            Assert.That(cured.State.Credit.PendingForeclosure, Is.False, "paid in full: the notice is cancelled");
            cured.AdvanceTo(2 * S + 48 * 3600);
            Assert.That(cured.State.Land.Parcels[ParcelOf(cured, "w1")].Owned, Is.True);
            Assert.That(cured.State.Credit.Foreclosures, Is.EqualTo(0));
            ReconcileOk(cured);
        }

        // AC-34: with no bought land a default takes stored produce at the Floor, only until the debt is covered; inputs,
        // field crops and the inherited farm stay.
        [Test]
        public void SeizesProduce_NoShelterInStore()
        {
            Simulation sim = NewSim(cash: 0, freeBank: false, edit: TestSim.NoSpoilage);
            Assert.That(Take(sim, "moneylender", 20_000, 1).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 86, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            var market = sim.Config.Game.Market;
            int potato = market.IndexOf("potato"), seed = market.IndexOf("seed_potato"), wheat = market.IndexOf("wheat");
            sim.State.Market.Stock[potato] = 6_000_000; // 6,000 kg: more than the debt needs
            long seedBefore = sim.State.Market.Stock[seed];
            long wheatBefore = sim.State.Market.Stock[wheat];
            Burn(sim);
            sim.AdvanceTo(2 * S);
            sim.AdvanceTo(2 * S + 24 * 3600);
            ForeclosureCompleted done = Last<ForeclosureCompleted>(sim.DrainEvents());
            Assert.That(done, Is.Not.Null);
            Assert.That(done.Parcels, Is.Empty);
            Assert.That(done.WrittenOff, Is.EqualTo(0), "the potatoes covered the debt");
            Assert.That(done.Produce.Select(p => p.Key), Is.EqualTo(new[] { "potato" }), "highest value first, only until covered");
            Assert.That(sim.State.Market.Stock[potato], Is.GreaterThan(0).And.LessThan(6_000_000));
            Assert.That(sim.State.Market.Stock[wheat], Is.EqualTo(wheatBefore), "no more than needed is taken");
            Assert.That(sim.State.Market.Stock[seed], Is.EqualTo(seedBefore), "inputs are not seized");
            Assert.That(sim.State.Farm.CropOn(86), Is.Not.Null, "field crops on the inherited acres stay");
            Assert.That(sim.State.Credit.Score, Is.EqualTo(350));
            ReconcileOk(sim);

            // A store too small for the debt is emptied of produce and the rest is written off.
            Simulation poor = NewSim(cash: 0, freeBank: false);
            Take(poor, "moneylender", 20_000, 1);
            Burn(poor);
            poor.AdvanceTo(2 * S);
            poor.AdvanceTo(2 * S + 24 * 3600);
            ForeclosureCompleted done2 = Last<ForeclosureCompleted>(poor.DrainEvents());
            Assert.That(done2.WrittenOff, Is.GreaterThan(0));
            foreach (int g in Enumerable.Range(0, market.Goods.Count).Where(g => market.Goods[g].Kind == GoodKind.Produce))
                Assert.That(poor.State.Market.Stock[g], Is.EqualTo(0), market.Goods[g].Id);
            Assert.That(poor.State.Market.Stock[market.IndexOf("seed_potato")], Is.GreaterThan(0), "the seed stays");
            ReconcileOk(poor);
        }
    }
}
