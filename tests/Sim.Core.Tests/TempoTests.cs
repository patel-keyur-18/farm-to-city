using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-245 (spec 028 §3.6, ADR 0162): the tempo, a vehicle for the mandi: its price, legs, the cheapest ride, and letting the idle legs out to neighbours.</summary>
    public class TempoTests
    {
        private static long Coins(long c) { return c * Coin; }

        private static int Kind(Simulation sim) { return sim.Config.Game.Equipment.IndexOf("tempo"); }

        /// <summary>A Township whose machines never break by chance or by a day's event (a test of legs must not depend on a seed's luck).</summary>
        private static Simulation Township(bool breaks = false)
        {
            Simulation sim = TownshipTests.Start(cohort: true, edit: breaks ? null : (System.Func<string, string, string>)((f, j) => f == "farming.json" ? j.Replace("\"breakdownMaintained\": 0.005, \"breakdownSkipped\": 0.04", "\"breakdownMaintained\": 0, \"breakdownSkipped\": 0")
                : f == "events.json" ? j.Replace("\"minorChance\": 0.9", "\"minorChance\": 0").Replace("\"majorChance\": 0.0625", "\"majorChance\": 0") : j));
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            return sim;
        }

        /// <summary>A Township with stock in the store, a hand to drive, and a bought tempo; the clock at the start of a season's day.</summary>
        private static Simulation Farm(bool hand = true, bool lease = false, bool breaks = false)
        {
            Simulation sim = Township(breaks);
            EquipmentTests.Give(sim, 1_00_00_000);
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 40_000_000;
            if (hand) HireHand(sim);
            if (lease) Ok(sim, new LeaseMachine(sim.State.Clock.SimTime, "tempo")); else Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "tempo"));
            sim.AdvanceTo(GodownRules.SeasonEnd(sim) + 2 * Hour); // the start of a season: the next days have no close in them
            return sim;
        }

        private static Machine Tempo(Simulation sim) { return EquipmentRules.OfKind(sim, Kind(sim)); }

        private static GoToMandi Send(Simulation sim, long grams) { return new GoToMandi(sim.State.Clock.SimTime, new[] { new MandiLoad("potato", grams) }); }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-15: the content: a Township machine with its trip numbers, never hired for a day; bought or leased; sold at its resale.
        [Test]
        public void Machine()
        {
            Simulation early = Start();
            Assert.That(early.Execute(new BuyMachine(early.State.Clock.SimTime, "tempo")).Reason, Is.EqualTo(RejectReason.EraLocked), "it comes with the Township");
            Simulation sim = Farm();
            Machine m = Tempo(sim);
            var d = sim.Config.Game.Equipment.Machines[Kind(sim)];
            Assert.That((d.Trip.LoadKg, d.Trip.Hours, d.Trip.LegsPerDay, d.Acres, d.LifeSeasons), Is.EqualTo((1000L, 4, 2, 0, 48)));
            Assert.That(m.Cost, Is.EqualTo(EquipmentRules.Price(sim, d)));
            Assert.That(sim.Execute(new HireMachine(sim.State.Clock.SimTime, "tempo")).Reason, Is.Not.EqualTo(RejectReason.None), "never hired for a day");
            Assert.That(EquipmentRules.Acres(sim, Kind(sim)), Is.EqualTo(0), "it works no field");
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new SellMachine(sim.State.Clock.SimTime, m.Id));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(EquipmentRules.Resale(sim, m)));
            Reconciles(sim);
        }

        // AC-15: a leg carries 1,000 kg in 4 hours for 300 (diesel 200, wear 100); a send over 1,000 kg is two legs, 8 hours, 600; the tempo has two legs a day.
        [Test]
        public void Trips()
        {
            Simulation sim = Farm();
            Machine m = Tempo(sim);
            long cash = sim.State.Ledger.Cash;
            Ok(sim, Send(sim, 800_000));
            Assert.That((sim.State.Market.Trip.Legs, sim.State.Market.Trip.ReturnAt - sim.State.Market.Trip.DepartedAt, cash - sim.State.Ledger.Cash), Is.EqualTo((1, 4L * Hour, Coins(300))));
            Assert.That(sim.State.Market.Trip.CartHire, Is.EqualTo(0), "no trolley hired");
            Assert.That(TempoRules.LegsLeft(sim, m), Is.EqualTo(1));
            Assert.That(sim.Execute(Send(sim, 100_000)).Reason, Is.EqualTo(RejectReason.TripUnderway), "one trip at a time");
            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * Hour);
            Ok(sim, Send(sim, 1_000_000)); // a second send the same day: the tempo's second leg
            Assert.That(TempoRules.LegsLeft(sim, m), Is.EqualTo(0));
            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * Hour);
            Assert.That(sim.Execute(Send(sim, 100_000)).Reason, Is.EqualTo(RejectReason.AlreadyWentToday), "two legs a day");
            sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day + Hour);
            cash = sim.State.Ledger.Cash;
            CommandResult go = sim.Execute(Send(sim, 1_500_000));
            Assert.That(go.Reason, Is.EqualTo(RejectReason.None), "sent 1,500 kg");
            Assert.That((sim.State.Market.Trip.Legs, sim.State.Market.Trip.ReturnAt - sim.State.Market.Trip.DepartedAt, cash - sim.State.Ledger.Cash), Is.EqualTo((2, 8L * Hour, Coins(600))), "1,500 kg is two legs back to back");
            Assert.That(sim.Execute(Send(sim, 1_000_000)).Reason, Is.EqualTo(RejectReason.TripUnderway));
            Assert.That(m.OwnLegs, Is.EqualTo(4));
            Reconciles(sim);
        }

        // AC-15: the cheapest ride: your own cart or tractor-trolley (free), then the tempo (300 a leg), then the hired trolley (1,000); a broken or absent tempo falls back.
        [Test]
        public void Cheapest_Vehicle()
        {
            Simulation sim = Farm();
            Assert.That(TempoRules.Choose(sim, 800_000).Ride, Is.EqualTo(Ride.Tempo));
            Assert.That(TempoRules.Choose(sim, 1_800_000).Legs, Is.EqualTo(2));
            Assert.That(TempoRules.Choose(sim, 2_100_000).Ride, Is.EqualTo(Ride.Hired), "more than two legs: the trolley");
            Machine m = Tempo(sim);
            m.BrokenUntil = sim.State.Clock.SimTime + Day;
            Assert.That(TempoRules.Choose(sim, 800_000).Ride, Is.EqualTo(Ride.Hired), "a broken tempo");
            Assert.That(TempoRules.Choose(sim, 800_000).Cost, Is.EqualTo(Coins(1000)));
            m.BrokenUntil = 0;
            Ok(sim, Send(sim, 1_200_000)); // two legs
            Assert.That(TempoRules.Choose(sim, 400_000).Ride, Is.EqualTo(Ride.Hired), "no leg left today");
            Simulation none = Township();
            Assert.That(TempoRules.Choose(none, 800_000).Ride, Is.EqualTo(Ride.Hired), "no tempo, the trolley as before");
            Simulation cart = Farm();
            Ok(cart, new BuyMachine(cart.State.Clock.SimTime, "tractor") ); // the era allows it in a Township
            Assert.That(TempoRules.Choose(cart, 1_800_000).Ride, Is.EqualTo(Ride.Own), "the tractor-trolley is free");
        }

        // The founder gives each leg half an hour of his day.
        [Test]
        public void Founders_Time()
        {
            Simulation sim = Farm();
            Ok(sim, Send(sim, 1_800_000));
            Fixed cut = Sim.Core.Farm.FounderRules.MandiCut(sim);
            Assert.That(cut, Is.EqualTo(sim.Config.Game.Market.Mandi.FounderHours.MulRatio(24 * 2, 8)), "two legs, 8 hours: an hour of his day");
        }

        // AC-16: let out, the legs you did not use go to neighbours at midnight: 600 a leg, diesel and wear yours; yours always come first.
        [Test]
        public void LetOut()
        {
            Simulation sim = Farm();
            Machine m = Tempo(sim);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
            sim.DrainEvents();
            long income = -sim.State.Ledger.Balance(Account.MachineHire);
            sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day + Hour); // an idle day: two legs
            TempoLetOut e = sim.PendingEvents.OfType<TempoLetOut>().Last();
            Assert.That((e.Legs, e.Pay, e.Cost), Is.EqualTo((2, Coins(1200), Coins(600))), "two legs: 600 each, 300 of diesel and wear each");
            Assert.That(-sim.State.Ledger.Balance(Account.MachineHire) - income, Is.EqualTo(Coins(1200)), "farm income, not passive");
            Assert.That(m.LetOutLegs, Is.EqualTo(2));
            Ok(sim, Send(sim, 700_000)); // you use one leg
            sim.DrainEvents();
            sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day + Hour);
            Assert.That(sim.PendingEvents.OfType<TempoLetOut>().Single().Legs, Is.EqualTo(1), "one idle leg for neighbours");
            sim.AdvanceTo(sim.State.Clock.SimTime + 8 * Hour);
            Ok(sim, Send(sim, 1_600_000)); // two legs yours
            sim.DrainEvents();
            sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day + Hour);
            Assert.That(sim.PendingEvents.OfType<TempoLetOut>().Count(), Is.EqualTo(0), "a day you used both: nothing for neighbours");
            Reconciles(sim);
        }

        // Letting it out needs a hand to drive it and a tempo you bought, and stops while it is broken.
        [Test]
        public void LetOut_Needs()
        {
            Simulation sim = Farm(hand: false);
            Machine m = Tempo(sim);
            Assert.That(sim.Execute(new SetHireOut(sim.State.Clock.SimTime, m.Id, true)).Reason, Is.EqualTo(RejectReason.NoHandToDrive));
            Simulation leased = Farm(lease: true);
            Assert.That(leased.Execute(new SetHireOut(leased.State.Clock.SimTime, Tempo(leased).Id, true)).Reason, Is.EqualTo(RejectReason.LeasedMachine));
            Simulation broke = Farm();
            Machine b = Tempo(broke);
            Ok(broke, new SetHireOut(broke.State.Clock.SimTime, b.Id, true));
            b.BrokenUntil = broke.State.Clock.SimTime + 2 * Day;
            long cash = broke.State.Ledger.Cash;
            broke.AdvanceTo((broke.State.Clock.SimTime / Day + 1) * Day + Hour);
            Assert.That(b.LetOutLegs, Is.EqualTo(0));
        }

        // Is it worth it: a leg you use saves 700 against the trolley, a let leg nets 300; a season is 4 days of 2 legs.
        [Test]
        public void Worth()
        {
            Simulation sim = Farm();
            Machine m = Tempo(sim);
            var t = TempoRules.Trip(sim, m);
            Assert.That((TempoRules.SavesPerLeg(sim, t), TempoRules.NetPerLetLeg(sim, t)), Is.EqualTo((Coins(700), Coins(300))));
            long best = TempoRules.BestPerSeason(sim, m);
            Assert.That(best, Is.EqualTo(Coins(700) * 8 - TempoRules.StandingPerSeason(sim, m)));
            Assert.That(best, Is.GreaterThan(0), "used every day it pays");
            Assert.That(m.Cost / best, Is.InRange(5, 15), "payback in a few seasons, not the 40 of the spec's price");
            Assert.That(TempoRules.LetOutPerSeason(sim, m), Is.LessThan(best), "neighbours' legs pay less than your own saving");
        }

        // The save keeps the legs and a trip's legs; the hash is the same after a round trip.
        [Test]
        public void Saves()
        {
            Simulation sim = Farm();
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, Tempo(sim).Id, true));
            Ok(sim, Send(sim, 1_200_000));
            SimConfig config = sim.Config;
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Market.Trip.Legs, Is.EqualTo(2));
            Assert.That(r.State.Equipment.Machines.Single(x => x.Kind == Kind(sim)).OwnLegs, Is.EqualTo(2));
        }

        [Test]
        public void Content_Is_Checked()
        {
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "farming.json" ? j.Replace("\"legsPerDay\": 2", "\"legsPerDay\": 7") : j));
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "farming.json" ? j.Replace("\"hours\": 4, \"diesel\"", "\"hours\": 13, \"diesel\"") : j), "two legs must fit in a day");
        }
    }
}
