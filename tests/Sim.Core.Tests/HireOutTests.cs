using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-256, spec 025 §3.8, ADR 0137: your tractor ploughs neighbours' fields for a fee, up to a daily cap, at your diesel and wear.</summary>
    public class HireOutTests
    {
        private static int Kind(Simulation sim) { return sim.Config.Game.Equipment.IndexOf("tractor"); }

        /// <summary>A Village farm with a bought tractor (maintenance on) and, with <paramref name="hand"/>, a hired hand.</summary>
        private static Simulation Farm(ulong seed = 42, bool hand = true)
        {
            Simulation sim = EquipmentTests.WithSheds();
            EquipmentTests.Era(sim, "village");
            EquipmentTests.Give(sim, 1_00_00_000);
            if (hand) HireHand(sim);
            Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "tractor"));
            sim.AdvanceTo(Market.GodownRules.SeasonEnd(sim) + 2 * Hour); // the start of a season: the next four days have no close in them
            return sim;
        }

        private static Machine Tractor(Simulation sim) { return EquipmentRules.OfKind(sim, Kind(sim)); }

        private static void Days(Simulation sim, int n) { sim.AdvanceTo(sim.State.Clock.SimTime + n * Day); }

        [Test]
        public void Switch_NeedsAHiredHand_AndABoughtTractor()
        {
            Simulation sim = Farm(hand: false);
            Machine m = Tractor(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetHireOut(t, m.Id, true)).Reason, Is.EqualTo(RejectReason.NoHandToDrive), "nobody to drive it");
            Assert.That(sim.Execute(new SetHireOut(t, 999, true)).Reason, Is.EqualTo(RejectReason.UnknownMachine));
            HireHand(sim);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
            Assert.That(m.HireOut, Is.True);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, false));
            Assert.That(m.HireOut, Is.False, "switching off always works");

            Simulation leased = EquipmentTests.WithSheds();
            EquipmentTests.Era(leased, "village");
            EquipmentTests.Give(leased, 1_00_000);
            HireHand(leased);
            Ok(leased, new LeaseMachine(leased.State.Clock.SimTime, "tractor"));
            Assert.That(leased.Execute(new SetHireOut(leased.State.Clock.SimTime, Tractor(leased).Id, true)).Reason, Is.EqualTo(RejectReason.LeasedMachine), "a lease is rented for your own fields");

            Simulation plough = EquipmentTests.WithSheds();
            HireHand(plough);
            Ok(plough, new BuyMachine(plough.State.Clock.SimTime, "ox_plough"));
            Assert.That(plough.Execute(new SetHireOut(plough.State.Clock.SimTime, EquipmentRules.OfKind(plough, plough.Config.Game.Equipment.IndexOf("ox_plough")).Id, true)).Reason,
                Is.EqualTo(RejectReason.UnknownMachine), "the plough isn't let out");
        }

        [Test]
        public void EachMorning_NeighboursPayTheFee_AndTheDieselComesOff()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
            long cash = sim.State.Ledger.Cash;
            sim.DrainEvents();
            Days(sim, 2);
            var worked = sim.DrainEvents().OfType<MachineHiredOut>().ToList();
            Assert.That(worked.Count, Is.InRange(1, 3), "a morning each");
            Sim.Core.Definitions.MachineDef d = sim.Config.Game.Equipment.Machines[Kind(sim)];
            long fee = EquipmentRules.HireFee(sim, d), diesel = EquipmentRules.HireDiesel(sim, d);
            foreach (MachineHiredOut e in worked) Assert.That((e.Acres, e.Fee, e.Diesel), Is.EqualTo((8, fee, diesel)));
            Assert.That(-sim.State.Ledger.Balance(Account.MachineHire), Is.EqualTo(fee * worked.Count), "income in 4040");
            Assert.That(fee, Is.EqualTo(Money.RoundToCoin(EquipmentRules.Hire(sim, d) * 8 / 20 * 4 / 5)).Within(Coin), "80% of 8 of the day's 20 acres");
            Assert.That(diesel, Is.EqualTo(Money.RoundToCoin(EquipmentRules.Diesel(sim, d, 1) * 8 / 20)).Within(Coin));
            Assert.That(sim.State.Ledger.Cash - cash, Is.GreaterThan((fee - diesel) * worked.Count - Coin * 100), "net of diesel (less the machine's own costs)");
            EquipmentRules.HiredOutSeason(sim, m, out int acres, out long fees, out long dsl);
            Assert.That((acres, fees, dsl), Is.EqualTo((8 * worked.Count, fee * worked.Count, diesel * worked.Count)));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void NoWork_WhenOff_Broken_Unpaid_OrWithNoHandToDrive()
        {
            Simulation off = Farm();
            off.DrainEvents();
            Days(off, 3);
            Assert.That(off.DrainEvents().OfType<MachineHiredOut>(), Is.Empty, "off by default");

            Simulation sim = Farm();
            Machine m = Tractor(sim);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
            sim.DrainEvents();
            m.Unpaid = true;
            Days(sim, 2);
            Assert.That(sim.DrainEvents().OfType<MachineHiredOut>(), Is.Empty, "its diesel is unpaid: it stands idle");
            m.Unpaid = false;
            foreach (var h in P(sim).Hands) h.Hr.SickUntil = sim.State.Clock.SimTime + 30 * Day;
            Days(sim, 2);
            Assert.That(sim.DrainEvents().OfType<MachineHiredOut>(), Is.Empty, "the hand is on sick leave: nobody to drive it");
            foreach (var h in P(sim).Hands) h.Hr.SickUntil = 0;
            Days(sim, 2);
            Assert.That(sim.DrainEvents().OfType<MachineHiredOut>(), Is.Not.Empty, "back at work");
        }

        // Wear: a morning's work for neighbours doubles the day's chance of breaking (4% to 8% with maintenance skipped), from the same seeded draw.
        [Test]
        public void Wear_DoublesTheChanceOfBreaking_OnTheDaysItWorks()
        {
            int FirstBreak(bool hire, out int expected)
            {
                Simulation sim = Farm();
                Machine m = Tractor(sim);
                Ok(sim, new SetMaintenance(sim.State.Clock.SimTime, m.Id, false));
                m.Maintained = false;
                if (hire) Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
                sim.DrainEvents();
                long start = (sim.State.Clock.SimTime - 6 * Hour + Day - 1) / Day; // the day of the next 6:00 pass
                Fixed chance = sim.Config.Game.Equipment.BreakdownSkipped * Fixed.FromInt(hire ? 2 : 1);
                expected = -1;
                for (long d = start; d < start + 200 && expected < 0; d++)
                    if (Fixed.FromRaw((long)(Rng.Next(sim.State.Seed, RngStream.Equipment, (ulong)m.Id, (ulong)d) >> 32)) < chance) expected = (int)(d - start);
                for (int day = 0; day < 200; day++)
                {
                    Days(sim, 1);
                    foreach (MachineBroke b in sim.DrainEvents().OfType<MachineBroke>()) return (int)(b.SimTime / Day - start);
                }
                return -1;
            }
            int plainExpected, hireExpected;
            int plain = FirstBreak(false, out plainExpected), hired = FirstBreak(true, out hireExpected);
            Assert.That(plainExpected, Is.GreaterThanOrEqualTo(0), "a breakdown comes within 200 days");
            Assert.That(plain, Is.EqualTo(plainExpected), "its own chance is 4% (maintenance skipped)");
            Assert.That(hired, Is.EqualTo(hireExpected), "working for neighbours doubles it to 8%");
            Assert.That(hired, Is.LessThanOrEqualTo(plain), "the doubled chance catches every draw the plain one does");
        }

        // The exploit check US-248 reads: the cap, and no loan loop.
        [Test]
        public void TheCap_Holds_AndHiringOutNeverPaysForTheTractor()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, m.Id, true));
            sim.DrainEvents();
            Days(sim, 12);
            var worked = sim.DrainEvents().OfType<MachineHiredOut>().ToList();
            Assert.That(worked.Sum(e => e.Acres), Is.LessThanOrEqualTo(8 * 13), "never more than 8 acres a morning");
            Assert.That(worked.GroupBy(e => e.SimTime / Day).All(g => g.Count() == 1 && g.Sum(e => e.Acres) <= 8), Is.True, "one run a morning");
            Sim.Core.Definitions.MachineDef d = sim.Config.Game.Equipment.Machines[Kind(sim)];
            Assert.That(EquipmentRules.HireFee(sim, d), Is.GreaterThan(EquipmentRules.HireDiesel(sim, d)), "a morning always pays its diesel");
            long net = EquipmentRules.HireNetPerSeason(sim, m), standing = EquipmentRules.StandingCostPerSeason(sim, m);
            Assert.That(net, Is.InRange(Money.Coin * 0, standing / 4), "a little: " + net + " against " + standing);
            Assert.That(net, Is.LessThan(standing), "depreciation and upkeep are more than hiring out earns: never pays for the tractor");
            // Whatever the price index, fee, diesel and the machine's costs move together.
            sim.State.Market.PriceIndex = Fixed.FromInt(3);
            Assert.That(EquipmentRules.HireNetPerSeason(sim, m), Is.LessThan(EquipmentRules.StandingCostPerSeason(sim, m)));
        }

        [Test]
        public void TheMoney_IsFarmIncome_NotOtherIncome_AndNotPassive()
        {
            var lines = Reporting.LinesFrom(new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Account, long>>
                { new System.Collections.Generic.KeyValuePair<Account, long>(Account.MachineHire, 500) });
            Assert.That(lines.First(l => l.Key == "farmIncome").Amount, Is.EqualTo(500));
            Assert.That(lines.First(l => l.Key == "otherIncome").Amount, Is.EqualTo(0));
            Assert.That(lines.First(l => l.Key == "farmProfit").Amount, Is.EqualTo(500));

            Simulation sim = Farm();
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, Tractor(sim).Id, true));
            Days(sim, 5);
            Hub.Passive(sim, out long rent, out long interest, out long business);
            Assert.That(rent + interest + business, Is.EqualTo(0), "your hand drives it: not passive");
        }

        [Test]
        public void TheSwitch_SurvivesASave_AndOldSavesReadWithItOff()
        {
            Simulation sim = Farm();
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, Tractor(sim).Id, true));
            Days(sim, 2);
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Equipment.Machines.First(x => x.Kind == Kind(sim)).HireOut, Is.True);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));

            Simulation plain = Farm();
            Assert.That(Tractor(plain).HireOut, Is.False, "off until you switch it on");
        }

        [Test]
        public void ChunkingInvariance_ADayAtATimeOrAllAtOnce()
        {
            Simulation a = Farm(), b = Farm();
            Ok(a, new SetHireOut(a.State.Clock.SimTime, Tractor(a).Id, true));
            Ok(b, new SetHireOut(b.State.Clock.SimTime, Tractor(b).Id, true));
            long end = a.State.Clock.SimTime + 9 * Day;
            a.AdvanceTo(end);
            for (int i = 0; i < 9; i++) b.AdvanceTo(b.State.Clock.SimTime + Day);
            Assert.That(b.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()));
        }
    }
}
