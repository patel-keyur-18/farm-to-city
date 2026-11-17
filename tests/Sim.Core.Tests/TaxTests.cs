using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Business;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-140 (spec 022 §3.1–§3.4, ADR 0109): the house tax, the business tax, owed tax, rates from the next season and the office's collection.</summary>
    public class TaxTests
    {
        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }

        /// <summary>A Township town with the default taxes, a family in a house of yours and the grants of a resident.</summary>
        private static Simulation Start(System.Func<string, string, string> edit = null)
        {
            Simulation sim = NewFarm(game: Game(edit), options: new SimOptions { DevCommandsEnabled = true });
            TreasuryTests.Era(sim, "village");
            TreasuryTests.Family(sim);
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        private static long Value(Simulation sim, string building) { return MarketRules.Indexed(sim, sim.Config.Game.Buildings.First(b => b.Id == building).Value); }

        // AC-1: each house is billed value × rate ÷ 4 a season in arrears, your home as a kutcha house, huts exempt; posted on both sides.
        [Test]
        public void HouseTax()
        {
            Simulation sim = Start();
            Assert.That((T(sim).HouseRate, T(sim).BusinessRate), Is.EqualTo((Fixed.Parse("0.016"), Fixed.Parse("0.10"))), "the defaults at the opening");
            long cash = sim.State.Ledger.Cash;
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            long each = Money.MulRate(Value(sim, "family_house"), Fixed.Parse("0.016")) / 4;
            JournalEntry e = sim.State.Ledger.Entries.Single(x => x.DescriptionKey == "ledger.houseTax");
            Assert.That(e.Reference, Is.EqualTo("houses:2"), "the family house and your own home; the hands' huts pay nothing");
            Assert.That(e.Postings.Single(p => p.Account == Account.HouseTax).Amount, Is.EqualTo(2 * each));
            TaxBilled billed = sim.PendingEvents.OfType<TaxBilled>().Single(b => b.Payer == "you");
            Assert.That((billed.Billed, billed.Collected), Is.EqualTo((2 * each, 2 * each)));
            Assert.That(T(sim).Reports.Last().Balance(Account.HouseTaxIncome), Is.EqualTo(-2 * each), "the treasury's row, income of the season that ended");
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-2: a house's tax is never over 15% of its rent (a let house's actual rent), and the loader refuses content that could break it.
        [Test]
        public void HouseTax_Guard()
        {
            Simulation sim = Start();
            Household x = sim.State.Citizens.Households.First(h => HouseholdRules.InFamilyHouse(sim, h));
            Building house = sim.State.Land.Buildings.First(b => b.Id == x.HomeId);
            x.LeaseRent = 1_000 * Coin;
            Assert.That(TaxRules.HouseTax(sim, house, Fixed.Parse("0.024")), Is.EqualTo(150 * Coin), "15% of the actual rent, not 360");
            x.LeaseRent = 2_500 * Coin;
            Assert.That(TaxRules.HouseTax(sim, house, Fixed.Parse("0.024")), Is.EqualTo(360 * Coin), "the top rate is under the cap at the usual rent");
            Assert.Throws<ContentException>(() => NewFarm(game: Game((f, j) => f == "treasury.json" ? j.Replace("\"max\": 0.024", "\"max\": 0.05").Replace("\"rate\": 0.016", "\"rate\": 0.04") : j)),
                "content whose top rate would pass 15% of the base rent is refused");
        }

        // AC-3: a business pays its positive season profit × the rate from its own cash; a loss pays nothing; the village's shops pay on a reference profit.
        [Test]
        public void BusinessTax()
        {
            Simulation sim = NewFarm(game: Game(), options: new SimOptions { DevCommandsEnabled = true });
            Sim.Core.Business.Business b = ShopTests.Shop(sim, "tea_stall");
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            long now = sim.State.Clock.SimTime;
            b.Ledger.Post(now, sim.Period, "test", "test", new Posting(Account.Cash, 20_000 * Coin), new Posting(Account.Sales, -20_000 * Coin));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            BusinessReport r = b.Reports.Last();
            long profit = Business.BusinessBooks.NetProfit(r) + r.Balance(Account.BusinessTax);
            TaxBilled billed = sim.PendingEvents.OfType<TaxBilled>().Single(x => x.Payer == "business:" + b.Id);
            Assert.That(billed.Billed, Is.EqualTo(Money.MulRate(profit, Fixed.Parse("0.10"))));
            Assert.That(r.Balance(Account.BusinessTax), Is.EqualTo(billed.Collected), "the report reads profit before tax, then the tax");
            Assert.That(billed.Billed, Is.GreaterThan(0));

            // The village's shops: the rate × their reference profit, from outside the model.
            TaxBilled grocer = sim.PendingEvents.OfType<TaxBilled>().Single(x => x.Payer == "shop:grocer");
            Assert.That(grocer.Billed, Is.EqualTo(Money.MulRate(MarketRules.Indexed(sim, 15_000 * Coin), Fixed.Parse("0.10"))));
            Assert.That(sim.PendingEvents.OfType<TaxBilled>().Any(x => x.Payer == "shop:village_tea"), Is.True);
            Assert.That(sim.PendingEvents.OfType<TaxBilled>().Any(x => x.Payer == "shop:town_halwai"), Is.False, "the town's halwai isn't the village's");

            // A loss pays nothing.
            sim.DrainEvents();
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Repairs, 50_000 * Coin), new Posting(Account.Cash, -50_000 * Coin));
            Assert.That(b.Ledger.Cash, Is.GreaterThan(0));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(sim.PendingEvents.OfType<TaxBilled>().Any(x => x.Payer == "business:" + b.Id), Is.False, "a loss pays nothing and none is carried");
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
        }

        // AC-4: a tax that can't be paid is carried as owed (2400) and paid first at the next close; the founder's cash before a close doesn't reduce it.
        [Test]
        public void Owed_NoDodge()
        {
            Simulation sim = Start();
            sim.AdvanceTo(TreasuryTests.NextClose(sim) - 3600);
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.LivingCosts, cash - 10 * Coin), new Posting(Account.Cash, -(cash - 10 * Coin))); // all but 10 coins an hour before the close
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            long owed = TaxRules.YouOwe(sim);
            Assert.That(owed, Is.GreaterThan(0), "what the cash couldn't pay is carried");
            Assert.That(sim.PendingEvents.OfType<TaxOwed>().Single().Amount, Is.EqualTo(owed));
            long received = -T(sim).Reports.Last().Balance(Account.HouseTaxIncome);
            Assert.That(received, Is.LessThan(2 * Money.MulRate(Value(sim, "family_house"), Fixed.Parse("0.016")) / 4), "the treasury got only what was paid");
            Assert.That(sim.State.Ledger.Cash, Is.GreaterThanOrEqualTo(0));

            CashTo(sim, 500_000 * Coin);
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(TaxRules.YouOwe(sim), Is.EqualTo(0), "paid first at the next close");
            Assert.That(T(sim).Reports.Last().Balance(Account.HouseTaxIncome), Is.LessThan(0));
        }

        // AC-5: a rate change applies from the next season; the close bills at the rate in force; pending can be undone; a cut before a close saves nothing.
        [Test]
        public void RateFromNextSeason()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetTaxRate(t, TaxKind.House, Fixed.Parse("0.03"))).Reason, Is.EqualTo(RejectReason.OutOfRange));
            Assert.That(sim.Execute(new SetTaxRate(t, TaxKind.Business, Fixed.Parse("0.25"))).Reason, Is.EqualTo(RejectReason.OutOfRange));
            Assert.That(sim.Execute(new UndoTaxRate(t, TaxKind.House)).Reason, Is.EqualTo(RejectReason.NothingPending));
            Ok(sim, new SetTaxRate(t, TaxKind.House, Fixed.Parse("0.024")));
            Assert.That((TaxRules.Rate(sim, TaxKind.House), TaxRules.Pending(sim, TaxKind.House)), Is.EqualTo((Fixed.Parse("0.016"), Fixed.Parse("0.024"))), "pending, not in force");
            Ok(sim, new UndoTaxRate(t, TaxKind.House));
            Assert.That(TaxRules.Pending(sim, TaxKind.House), Is.EqualTo(Fixed.FromInt(-1)));

            Ok(sim, new SetTaxRate(t, TaxKind.House, Fixed.Zero)); // a cut the minute before the close
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            long each = Money.MulRate(Value(sim, "family_house"), Fixed.Parse("0.016")) / 4;
            Assert.That(sim.PendingEvents.OfType<TaxBilled>().Single(b => b.Payer == "you").Billed, Is.EqualTo(2 * each), "the season that ended was billed at the rate in force");
            Assert.That(TaxRules.Rate(sim, TaxKind.House), Is.EqualTo(Fixed.Zero), "and the new rate is in force now");
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(sim.PendingEvents.OfType<TaxBilled>().Any(b => b.Payer == "you"), Is.False, "from the next season: nothing billed at 0");
        }

        // AC-6: the office collects officeBase + officeWeight × its condition of the bill; the payer pays only that.
        [Test]
        public void Collection()
        {
            Simulation sim = Start();
            T(sim).Condition[0] = Fixed.Zero;
            long each = Money.MulRate(Value(sim, "family_house"), Fixed.Parse("0.016")) / 4;
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            TaxBilled b = sim.PendingEvents.OfType<TaxBilled>().Single(x => x.Payer == "you");
            Assert.That(b.Billed, Is.EqualTo(2 * each));
            Assert.That(b.Collected, Is.EqualTo(Money.MulRate(2 * each, Fixed.Parse("0.70"))), "the office at 0% service collects 70%");
            Assert.That(sim.State.Ledger.Entries.Single(e => e.DescriptionKey == "ledger.houseTax").Postings.Single(p => p.Account == Account.HouseTax).Amount, Is.EqualTo(b.Collected), "the payer pays only what is collected");
        }

        // AC-16: rates, a pending rate and owed tax survive a save, and the replay agrees.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start();
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.House, Fixed.Parse("0.02")));
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.Business, Fixed.Parse("0.15")));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.House, Fixed.Parse("0.01")));
            byte[] bytes = Saves.SaveCodec.Write(sim.State, sim.Config, Wall0);
            Saves.LoadResult loaded = Saves.SaveCodec.Read(bytes, sim.Config);
            Assert.That(loaded.Error, Is.EqualTo(Saves.SaveError.None), loaded.Detail);
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            TreasuryState back = loaded.State.Treasury;
            Assert.That((back.HouseRate, back.BusinessRate, back.PendingHouse), Is.EqualTo((Fixed.Parse("0.02"), Fixed.Parse("0.15"), Fixed.Parse("0.01"))));
            Assert.That(Saves.SaveCodec.Write(loaded.State, sim.Config, Wall0), Is.EqualTo(bytes));
        }
    }
}
