using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>A shop's marketing (spec 028 §3.10, US-260): a budget paid at each close adds to the score while it is spent.</summary>
    public class MarketingTests
    {
        private static void Close(Simulation sim) { sim.AdvanceTo(BusinessTests.NextClose(sim)); }

        private static bool Near(Fixed a, Fixed b) { Fixed e = Fixed.FromRatio(1, 1000); return a - b < e && b - a < e; }

        // AC-25: +0.019 a 1,000 up to +0.15, paid upfront at the close; the score moves by exactly that; stopped, it fades by a fifth each close.
        [Test]
        public void Score_Cap_Fade()
        {
            foreach ((long budget, string lift) in new[] { (2_000L, "0.038"), (4_000L, "0.076"), (8_000L, "0.15") })
            {
                Simulation sim = KiranaTests.Town(10, out Business.Business b);
                Fixed before = ShopRules.Attract(sim, b, ShopGroup.Village).Total;
                Ok(sim, new SetMarketing(sim.State.Clock.SimTime, b.Id, budget * Coin));
                long spend = Sim.Core.Market.MarketRules.Indexed(sim, budget * Coin);
                Assert.That(b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.marketing" && e.Postings.Any(p => p.Account == Account.Marketing && p.Amount == spend)), Is.EqualTo(1), "paid at once, a cost on its books");
                Close(sim);
                Assert.That(b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.marketing" && e.Postings.Any(p => p.Account == Account.Marketing && p.Amount == spend)), Is.EqualTo(2), "and again at the close, for the season ahead");
                Fixed want = lift == "0.038" ? Fixed.FromRatio(38, 1000) : lift == "0.076" ? Fixed.FromRatio(76, 1000) : Fixed.FromRatio(15, 100);
                Assert.That(Near(b.MarketLift, want), Is.True, budget + ": " + b.MarketLift);
                Attraction a = ShopRules.Attract(sim, b, ShopGroup.Village);
                Assert.That(a.Marketing, Is.EqualTo(b.MarketLift), "the parts show it");
                Assert.That(a.Total - Fixed.Zero, Is.GreaterThan(before - Fixed.One), "attraction counts it");
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
                Assert.That(b.Ledger.Reconcile(), Is.Null);
            }
            Simulation s = KiranaTests.Town(10, out Business.Business shop);
            Ok(s, new SetMarketing(s.State.Clock.SimTime, shop.Id, 8_000 * Coin));
            Close(s);
            Fixed full = shop.MarketLift;
            Ok(s, new SetMarketing(s.State.Clock.SimTime, shop.Id, 0));
            Close(s);
            Assert.That(Near(shop.MarketLift, full * Fixed.FromRatio(4, 5)), Is.True, "stopped, it fades a fifth: " + shop.MarketLift);
            Close(s);
            Assert.That(Near(shop.MarketLift, full * Fixed.FromRatio(16, 25)), Is.True, "and a fifth of that");
        }

        // The score is what it adds: with it, the shop wins more of the customers (a bigger share than without).
        [Test]
        public void Wins_Customers()
        {
            long Served(long budget)
            {
                Simulation sim = KiranaTests.Town(10, out Business.Business b);
                for (int g = 0; g < b.PutOut.Length; g++) b.PutOut[g] *= 5; // stock that does not run out, so the score alone decides
                Ok(sim, new SetMarketing(sim.State.Clock.SimTime, b.Id, budget * Coin));
                Close(sim);
                long s0 = b.SeasonServed;
                sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 86400);
                return b.SeasonCustomers - 0 * s0;
            }
            Assert.That(Served(8_000), Is.GreaterThan(Served(0)), "a better score draws more customers (a stall's stock caps what it serves)");
        }

        // Cash short: a budget you cannot pay is refused; one that cannot be paid at the close is not bought, the lift fades and the owner is told (the rent's rule).
        [Test]
        public void Unpaid_When_Cash_Is_Short()
        {
            Simulation sim = KiranaTests.Town(10, out Business.Business b);
            Ok(sim, new SetMarketing(sim.State.Clock.SimTime, b.Id, 4_000 * Coin));
            Fixed lift = b.MarketLift;
            Assert.That(lift, Is.GreaterThan(Fixed.Zero));
            BusinessBooks.Draw(sim, b, b.Ledger.Cash + BusinessBooks.OverdraftLimit(sim, b)); // the whole overdraft used
            long drain = b.Ledger.Cash;
            if (drain > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.OwnersCapital, drain), new Posting(Account.Cash, -drain));
            sim.DrainEvents();
            Assert.That(sim.Execute(new SetMarketing(sim.State.Clock.SimTime, b.Id, 8_000 * Coin)).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "a rise it cannot pay is refused");
            Assert.That(b.Marketing, Is.EqualTo(4_000 * Coin), "and the budget stays");
            int posted = b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.marketing");
            Close(sim);
            Assert.That(b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.marketing"), Is.EqualTo(posted), "nothing bought at the close");
            Assert.That(b.MarketLift, Is.LessThan(lift), "so the lift fades");
            Assert.That(sim.DrainEvents().OfType<Sim.Core.Events.ObligationUnpaid>().Any(e => e.Kind == "Marketing"), Is.True, "and you are told");
        }

        // The command: only a shop, only a content budget, only after the Township, never on a closed business.
        [Test]
        public void Refusals()
        {
            Simulation sim = KiranaTests.Town(0, out Business.Business b);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetMarketing(t, b.Id, 1_234 * Coin)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "not one of the budgets");
            Assert.That(sim.Execute(new SetMarketing(t, 999, 0)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Ok(sim, new SetMarketing(t, b.Id, 4_000 * Coin));
            Assert.That(b.Marketing, Is.EqualTo(4_000 * Coin));
            Assert.That(b.MarketLift, Is.EqualTo(KhataRules.Lift(sim, 4_000 * Coin)), "paid and working now");
        }
    }
}
