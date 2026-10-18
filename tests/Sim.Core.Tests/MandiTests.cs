using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class MandiTests
    {
        private static MandiLoad[] Potato(long grams) { return new[] { new MandiLoad("potato", grams) }; }

        private static Simulation WithPotato(long grams, ulong seed = 42)
        {
            Simulation sim = NewFarm(seed, game: Quiet(NoSpoilage));
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += grams;
            return sim;
        }

        /// <summary>Day-labour hours delivered between t0 and t1 at 100% day labour, with or without a trip at <paramref name="depart"/>.</summary>
        private static Fixed DayLabourHours(long depart, bool trip)
        {
            Simulation sim = WithPotato(1_000_000);
            Assert.That(sim.Execute(new SetLabourSplit(0, 100)).Accepted, Is.True);
            if (trip) Assert.That(sim.Execute(new GoToMandi(depart, Potato(1_000_000))).Accepted, Is.True);
            Fixed hours = Fixed.Zero;
            sim.AdvanceTo(3 * Day);
            foreach (DayLabourPaid p in sim.DrainEvents().OfType<DayLabourPaid>()) hours += p.Hours;
            return hours;
        }

        // Spec 004 AC-21 (ADR 0034): pays the regional price less 3%, cart hire at departure, back in 6 h, 0.5 h of labour.
        [Test]
        public void Trip_PaysAndCosts()
        {
            Simulation sim = WithPotato(1_000_000);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new GoToMandi(8 * Hour, Potato(1_000_000))).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(potato), Is.EqualTo(0), "the goods leave at departure");
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(800_00));
            Assert.That(sim.State.Ledger.Balance(Account.TransportAndSelling), Is.EqualTo(800_00));
            Assert.That(sim.UpcomingJobs(Day).Single(j => j.Type == MarketSystem.MandiJob).DueTime, Is.EqualTo(14 * Hour));

            sim.DrainEvents();
            sim.AdvanceTo(14 * Hour);
            long gross = Money.Value(1_000_000, MarketRules.RegionalPrice(sim, potato), 1000);
            Assert.That(gross, Is.EqualTo(6_800_00));
            MandiTripReturned back = sim.DrainEvents().OfType<MandiTripReturned>().Single();
            Assert.That(back.SimTime, Is.EqualTo(14 * Hour));
            Assert.That((back.Gross, back.Commission, back.Net), Is.EqualTo((6_800_00L, 204_00L, 6_596_00L)));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(6_596_00 - 800_00)); // spec 004 §3.6: 5,796 for the trip
            Assert.That(sim.State.Ledger.Balance(Account.TransportAndSelling), Is.EqualTo(1_004_00));
            Assert.That(sim.State.Ledger.Balance(Account.CropSales), Is.EqualTo(-6_800_00));
            Assert.That(sim.State.Market.Trip, Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // Exactly 0.5 h of delivered labour, whatever the departure time (time-weighted), across midnight too.
            Fixed without = DayLabourHours(0, false);
            foreach (long depart in new[] { 1 * Hour, 17 * Hour, 21 * Hour })
                Assert.That(System.Math.Abs((without - DayLabourHours(depart, true) - Fixed.FromRatio(1, 2)).Raw), Is.LessThanOrEqualTo(16),
                    "departing at " + depart / Hour + ":00 (exact to Fixed rounding)");
        }

        // Spec 004 AC-22.
        [Test]
        public void Trip_Rejections()
        {
            Simulation sim = WithPotato(1_500_000);
            void Rejects(GoToMandi cmd, RejectReason reason)
            {
                ulong before = sim.State.ComputeHash();
                Assert.That(sim.Execute(cmd).Reason, Is.EqualTo(reason));
                Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), reason.ToString());
            }
            Rejects(new GoToMandi(0, Potato(1_000_001)), RejectReason.OverCartCapacity);
            Rejects(new GoToMandi(0, new[] { new MandiLoad("potato", 600_000), new MandiLoad("wheat", 400_001) }), RejectReason.InsufficientStock);
            Rejects(new GoToMandi(0, new[] { new MandiLoad("potato", 950_000), new MandiLoad("wheat", 60_000) }), RejectReason.OverCartCapacity);
            Rejects(new GoToMandi(0, Potato(1_600_000)), RejectReason.InsufficientStock);
            Rejects(new GoToMandi(0, new[] { new MandiLoad("seed_potato", 1_000) }), RejectReason.NotProduce);
            Assert.That(sim.Execute(new Offer(0, "potato", 1_000_000, Fixed.FromInt(100))).Accepted, Is.True);
            Rejects(new GoToMandi(0, Potato(600_000)), RejectReason.InsufficientStock); // only 500 kg isn't on offer

            Assert.That(sim.Execute(new GoToMandi(0, Potato(500_000))).Accepted, Is.True);
            sim.AdvanceTo(Hour);
            Rejects(new GoToMandi(Hour, new[] { new MandiLoad("wheat", 10_000) }), RejectReason.TripUnderway);
            sim.AdvanceTo(7 * Hour);
            Rejects(new GoToMandi(7 * Hour, new[] { new MandiLoad("wheat", 10_000) }), RejectReason.AlreadyWentToday);
            Assert.That(sim.Execute(new GoToMandi(Day, new[] { new MandiLoad("wheat", 10_000) })).Accepted, Is.True);

            Simulation sick = WithPotato(100_000);
            sick.State.Farm.Founder.SickToday = true;
            sim = sick;
            Rejects(new GoToMandi(0, Potato(100_000)), RejectReason.FounderSick);
            Simulation broke = WithPotato(100_000);
            broke.State.Ledger.Post(0, 0, "test", null, new Posting(Account.Cash, -broke.State.Ledger.Cash), new Posting(Account.LivingCosts, broke.State.Ledger.Cash));
            sim = broke;
            Rejects(new GoToMandi(0, Potato(100_000)), RejectReason.InsufficientFunds);
        }
    }
}
