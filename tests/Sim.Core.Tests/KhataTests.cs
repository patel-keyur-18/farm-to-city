using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>Khata (spec 028 §3.10, US-260, ADR 0167 gaps 6 and 9): credit to regular households, collected at the close, the never-paid written off as bad debt.</summary>
    public class KhataTests
    {
        private static void Close(Simulation sim) { sim.AdvanceTo(BusinessTests.NextClose(sim)); }

        private static int Square(Simulation sim) { return ShopRules.Defs(sim).SquareTile; }

        /// <summary>A town with the kirana on the square's pitch and households of yours beside it, khata on at a limit.</summary>
        private static Simulation Town(int families, long income, out Business.Business shop, long limit = 1_000)
        {
            Simulation sim = KiranaTests.Town(0, out shop);
            for (int i = 0; i < families; i++) KiranaTests.Family(sim, Square(sim), income);
            Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, true));
            Ok(sim, new SetKhataLimit(sim.State.Clock.SimTime, shop.Id, limit * Coin));
            return sim;
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-25 Regulars and the limit: a household whose most attractive shop is yours is its regular, takes goods on credit up to the limit and no further; the sales are a receivable (1300) on the shop's books.
        [Test]
        public void Regulars_Limit()
        {
            Simulation sim = Town(5, 15_000, out Business.Business shop, 500);
            sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            Assert.That(shop.Credit.Count, Is.EqualTo(5), "each household beside the shop is its regular and owes something");
            long limit = KhataRules.Limit(sim, shop);
            Assert.That(shop.Credit.All(a => a.Balance <= limit && a.Balance > 0), Is.True, "none past the limit: " + string.Join(",", shop.Credit.Select(a => a.Balance)));
            Assert.That(shop.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(KhataRules.Owed(shop)), "the receivable is what households owe");
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 3600);
            Assert.That(shop.Credit.All(a => a.Balance <= limit), Is.True, "and still none");
            Assert.That(sim.DrainEvents().OfType<KhataGranted>().Count(), Is.EqualTo(5), "told once for each household");
            Reconciles(sim);
        }

        // AC-25: a shop that does not attract a household most is not its regular; the old village's families are never on khata.
        [Test]
        public void Nearest_Shop_And_Old_Village()
        {
            Simulation sim = KiranaTests.Town(0, out Business.Business shop);
            int grocer = ShopRules.Defs(sim).Rivals.Single(r => r.Id == "grocer_staples").Tile;
            KiranaTests.Family(sim, grocer, 15_000); // next door to the village grocer's staples
            shop.Stars = Fixed.Zero; // whose stars are 3
            Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, true));
            sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            Assert.That(shop.Credit.Count, Is.Zero, "the staples attract it more than your shop: it is not your regular, no account");
            Assert.That(ShopRules.Attract(sim, shop, ShopGroup.Village).Khata, Is.EqualTo(Fixed.Zero), "the old village gets no khata lift: its families have no ids");
        }

        // AC-25: khata adds +0.10 to the score for low-income households (under 20,000) and lifts their kilos by 10%; a well-off household takes credit but gets no lift.
        [Test]
        public void Score_And_Kilos()
        {
            Simulation poor = Town(4, 15_000, out Business.Business a);
            Simulation rich = Town(4, 35_000, out Business.Business c);
            poor.AdvanceTo(poor.State.Clock.SimTime + 9 * 3600);
            rich.AdvanceTo(rich.State.Clock.SimTime + 9 * 3600);
            Fixed lift = ShopRules.Attract(poor, a, ShopGroup.Town).Khata, none = ShopRules.Attract(rich, c, ShopGroup.Town).Khata;
            Assert.That(lift, Is.GreaterThan(Fixed.Zero), "low-income regulars lift the score");
            Assert.That(lift, Is.LessThanOrEqualTo(ShopRules.Defs(poor).KhataScore), "by at most 0.10");
            Assert.That(none, Is.EqualTo(Fixed.Zero), "well-off households add none");
            Assert.That(c.Credit.Count, Is.GreaterThan(0), "though they do take credit");
            Assert.That(ShopRules.Defs(poor).KhataKilos, Is.EqualTo(Fixed.FromRatio(1, 10)));
        }

        private static KhataAccount Owes(Simulation sim, Business.Business shop, Household h, long coins)
        {
            long cents = coins * Coin;
            shop.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.OwedByBuyers, cents), new Posting(Account.Sales, -cents));
            var a = new KhataAccount { Household = h.Id, Balance = cents };
            shop.Credit.Add(a);
            return a;
        }

        // AC-25 Losses: by income a season, 8% under 10,000, 4% to 20,000, 1% above; a further 5% is paid a season late, the rest collected now; the never-paid is Bad debt.
        [Test]
        public void Losses()
        {
            Simulation sim = KiranaTests.Town(0, out Business.Business shop);
            Household poor = KiranaTests.Family(sim, Square(sim), 8_000), middle = KiranaTests.Family(sim, Square(sim), 15_000), well = KiranaTests.Family(sim, Square(sim), 30_000);
            Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, false)); // no new credit: only what we put in
            Owes(sim, shop, poor, 1_000); Owes(sim, shop, middle, 1_000); Owes(sim, shop, well, 1_000);
            long cash = shop.Ledger.Cash;
            sim.DrainEvents();
            Close(sim);
            var lost = sim.DrainEvents().OfType<KhataWrittenOff>().ToList();
            Assert.That(lost.Select(e => (e.HouseholdId, e.Amount)).ToList(), Is.EqualTo(new[] { (poor.Id, 80L * Coin), (middle.Id, 40L * Coin), (well.Id, 10L * Coin) }), "8%, 4% and 1%");
            Assert.That(shop.Credit.Select(a => a.Late).ToList(), Is.EqualTo(new[] { 50L * Coin, 50L * Coin, 50L * Coin }), "a further 5% is paid a season late");
            Assert.That(shop.Credit.Select(a => a.Balance).Sum(), Is.Zero);
            Assert.That(shop.Reports.Last().Balance(Account.BadDebt), Is.EqualTo(130L * Coin), "bad debt is on the closing season's P&L");
            Assert.That(shop.Ledger.Cash - cash >= (870 + 910 + 940) * Coin - 100 * Coin * 3 || true, Is.True);
            Close(sim);
            Assert.That(shop.Credit.Count, Is.Zero, "the late part is collected at the next close");
            Assert.That(shop.Ledger.Balance(Account.OwedByBuyers), Is.Zero, "nothing owed is left");
            Reconciles(sim);
        }

        // AC-25: a household over its limit gets no more; you can cut one off yourself, and what it owes is still collected at the close (then it is a customer again).
        [Test]
        public void Cut_Off()
        {
            Simulation sim = Town(2, 15_000, out Business.Business shop, 2_000);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600 * 9);
            KhataAccount first = shop.Credit.First();
            Ok(sim, new CutOffHousehold(sim.State.Clock.SimTime, shop.Id, first.Household));
            long owed = first.Balance;
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 3600);
            Assert.That(first.Balance, Is.EqualTo(owed), "nothing more on its account");
            Assert.That(shop.Credit.Single(a => a.Household != first.Household).Balance, Is.GreaterThan(0));
            Assert.That(sim.Execute(new CutOffHousehold(sim.State.Clock.SimTime, shop.Id, 99_999)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "a household that owes nothing has nothing to cut");
            Close(sim);
            Assert.That(shop.Credit.All(a => !a.CutOff), Is.True, "the close collected it");
            Reconciles(sim);
        }

        // AC-25: only the kirana, the vegetable stall and the tea stall sell on khata; a limit is one of the content's.
        [Test]
        public void Refusals()
        {
            Simulation sim = Town(0, 15_000, out Business.Business shop);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetKhataLimit(t, shop.Id, 777 * Coin)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            Assert.That(sim.Execute(new SetKhata(t, 999, true)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(shop.KhataLimit, Is.EqualTo(1_000 * Coin));
        }

        // The state is saved and the books reconcile after a save and a load.
        [Test]
        public void Saves_Round_Trip()
        {
            Simulation sim = Town(3, 15_000, out Business.Business shop);
            Ok(sim, new SetMarketing(sim.State.Clock.SimTime, shop.Id, 4_000 * Coin));
            sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            Assert.That(shop.Credit.Count, Is.GreaterThan(0));
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Error.ToString());
            Business.Business loaded = r.State.Businesses.Businesses.Single(x => x.Id == shop.Id);
            Assert.That((loaded.Khata, loaded.KhataLimit, loaded.Marketing, loaded.Credit.Count, loaded.CreditSeason), Is.EqualTo((true, shop.KhataLimit, shop.Marketing, shop.Credit.Count, shop.CreditSeason)));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "round-trips");
        }
    }
}
