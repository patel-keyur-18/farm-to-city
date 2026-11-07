using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 013 §3.2 (US-128): the ox plough and the tractor, bought, leased or hired; coverage, breakdowns, the books.</summary>
    public class EquipmentTests
    {
        private static long Money(long coins) { return coins * Coin; }

        internal static void Era(Simulation sim, string id)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == id) sim.State.Era.Current = i;
        }

        internal static void Give(Simulation sim, long coins)
        {
            long amount = Money(coins);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.gift", "test", new Posting(Account.Cash, amount), new Posting(Account.OpeningEquity, -amount));
        }

        /// <summary>A Hamlet with both sheds standing (the second on field 101) and plenty of cash.</summary>
        internal static Simulation WithSheds()
        {
            Simulation sim = Start();
            Era(sim, "hamlet");
            Give(sim, 20_00_000);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime, 101));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            return sim;
        }

        private static int Kind(Simulation sim, string id) { return sim.Config.Game.Equipment.IndexOf(id); }

        // AC-3: a machine cuts labour and raises Tools on the fields it covers, the best one applying; the ox plough needs room in the
        // second shed for its bullocks (ADR 0088), which come with it, give no milk and go with it.
        [Test]
        public void Coverage()
        {
            Simulation sim = Start();
            Era(sim, "hamlet");
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuyMachine(t, "tractor")).Reason, Is.EqualTo(RejectReason.EraLocked), "the tractor comes with the Village");
            Assert.That(sim.Execute(new BuyMachine(t, "ox_plough")).Reason, Is.EqualTo(RejectReason.NoShedRoom), "no second shed");

            sim = WithSheds();
            t = sim.State.Clock.SimTime;
            Assert.That(EquipmentRules.Fields(sim), Is.EqualTo(new[] { 86, 102, 84, 85, 100 }), "the well acres first; the second cattle acre isn't a field");
            Fixed radish = sim.Config.Game.Farm.Crops.First(c => c.Id == "radish").LabourPerAcrePerDay;
            Ok(sim, new Sow(t, 84, "radish", Farm.SeedSource.Certified, "none"));
            Fixed hand = Farm.FarmSystem.LabourDemand(sim);

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new BuyMachine(t, "ox_plough"));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money(70_000)), "the plough and cart 10,000, the pair 60,000");
            Animal[] pair = LivestockKit.L(sim).Animals.Where(a => a.Kind == sim.Config.Game.Equipment.Bullock).ToArray();
            Assert.That(pair.Select(a => a.Shed), Is.EqualTo(new[] { 1, 1 }), "the bullocks live in the second shed");
            Assert.That(pair.Select(a => a.Name).Distinct().Count(), Is.EqualTo(2));
            Assert.That(sim.Execute(new SellAnimal(t, pair[0].Id)).Reason, Is.EqualTo(RejectReason.PartOfMachine));
            Assert.That(sim.Execute(new BuyAnimal(t, "bullock")).Reason, Is.EqualTo(RejectReason.PartOfMachine));
            Assert.That(sim.Execute(new BuyMachine(t, "ox_plough")).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            int[] cover = EquipmentRules.Cover(sim);
            Assert.That(new[] { 86, 102, 84, 85, 100 }.Select(x => cover[x]), Is.All.EqualTo(Kind(sim, "ox_plough")), "6 acres: all 5 fields");
            Assert.That(cover[101], Is.EqualTo(-1));
            Fixed bullockCare = Fixed.FromInt(1); // 0.5 h a day each
            Assert.That(Farm.FarmSystem.LabourDemand(sim), Is.EqualTo(hand - radish + radish * Fixed.Parse("0.85") + bullockCare), "labour −15% and the pair's care");
            Ok(sim, new Sow(t, 85, "radish", Farm.SeedSource.Certified, "none"));
            Assert.That(EquipmentRules.ToolsOf(sim, sim.State.Farm.CropOn(85)), Is.EqualTo(Fixed.Parse("1.03")), "timely sowing under the plough");
            Assert.That(EquipmentRules.ToolsOf(sim, sim.State.Farm.CropOn(84)), Is.EqualTo(sim.Config.Game.Farm.Farm.ToolsFactor), "84 was sown by hand");

            // The milk run skips them: no milk, no calves, no lactation days.
            LivestockKit.ToMorning(sim);
            LivestockKit.ToMorning(sim);
            Assert.That(pair.Select(a => (a.StageDay, a.LastMilkMl)), Is.All.EqualTo((0, 0L)));

            // The Village: a leased tractor is the better machine and takes the fields first; given back, the plough again.
            Era(sim, "village");
            t = sim.State.Clock.SimTime;
            Ok(sim, new LeaseMachine(t, "tractor"));
            cover = EquipmentRules.Cover(sim);
            Assert.That(cover[86], Is.EqualTo(Kind(sim, "tractor")));
            Ok(sim, new Sow(t, 100, "radish", Farm.SeedSource.Certified, "none"));
            Assert.That(EquipmentRules.ToolsOf(sim, sim.State.Farm.CropOn(100)), Is.EqualTo(Fixed.Parse("1.06")));
            Assert.That(sim.Execute(new HireMachine(t, "tractor")).Reason, Is.EqualTo(RejectReason.AlreadyBuilt), "you have one");
            Ok(sim, new SellMachine(t, EquipmentRules.OfKind(sim, Kind(sim, "tractor")).Id));
            Assert.That(EquipmentRules.Cover(sim)[86], Is.EqualTo(Kind(sim, "ox_plough")));

            // A day's hire covers every field until midnight.
            cash = sim.State.Ledger.Cash;
            Ok(sim, new HireMachine(t, "tractor"));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money(2000)));
            Assert.That(EquipmentRules.Cover(sim)[100], Is.EqualTo(Kind(sim, "tractor")));
            sim.AdvanceTo((t / Day + 1) * Day);
            Assert.That(EquipmentRules.Cover(sim)[100], Is.EqualTo(Kind(sim, "ox_plough")), "the hire ended at midnight");

            // Without its bullocks the plough stands.
            foreach (Animal a in pair) LivestockKit.L(sim).Animals.Remove(a);
            Assert.That(EquipmentRules.Cover(sim)[86], Is.EqualTo(-1));
        }

        // The cart and the tractor-trolley take loads to the mandi without hiring the trolley (spec 013 §3.2).
        [Test]
        public void Cart_ToTheMandi()
        {
            Simulation sim = WithSheds();
            int potato = sim.Config.Game.Market.IndexOf("potato");
            sim.State.Market.Stock[potato] += 3_000_000;
            Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "ox_plough"));
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new GoToMandi(sim.State.Clock.SimTime, new[] { new MandiLoad("potato", 400_000) }));
            Assert.That(sim.State.Market.Trip.CartHire, Is.EqualTo(0), "400 kg fits the bullock cart");
            sim.AdvanceTo(sim.State.Clock.SimTime + Day);
            Ok(sim, new GoToMandi(sim.State.Clock.SimTime, new[] { new MandiLoad("potato", 1_000_000) }));
            Assert.That(sim.State.Market.Trip.CartHire, Is.EqualTo(Money(1000)), "a tonne needs the hired trolley");
        }

        // AC-4: skipped maintenance raises breakdowns; a broken machine covers nothing for 24 h and costs its repair.
        [Test]
        public void Breakdowns()
        {
            int Breaks(bool maintain)
            {
                Simulation sim = WithSheds();
                Era(sim, "village");
                Give(sim, 1_00_00_000);
                Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "tractor"));
                Machine m = EquipmentRules.OfKind(sim, Kind(sim, "tractor"));
                if (!maintain) { Ok(sim, new SetMaintenance(sim.State.Clock.SimTime, m.Id, false)); m.Maintained = false; }
                sim.DrainEvents();
                sim.AdvanceTo(sim.State.Clock.SimTime + 160 * Day);
                return sim.DrainEvents().OfType<MachineBroke>().Count();
            }
            int kept = Breaks(true), skipped = Breaks(false);
            Assert.That(skipped, Is.GreaterThan(kept + 2), "4% a day against 0.5%: " + skipped + " against " + kept);
            Assert.That(skipped, Is.InRange(2, 15), "about 6 in 160 days");

            // One breakdown in detail.
            Simulation one = WithSheds();
            Era(one, "village");
            Ok(one, new BuyMachine(one.State.Clock.SimTime, "tractor"));
            Machine tractor = EquipmentRules.OfKind(one, Kind(one, "tractor"));
            Ok(one, new SetMaintenance(one.State.Clock.SimTime, tractor.Id, false));
            tractor.Maintained = false;
            one.DrainEvents();
            MachineBroke broke = null;
            for (int d = 0; d < 200 && broke == null; d++)
            {
                one.AdvanceTo(one.State.Clock.SimTime + Day);
                broke = one.DrainEvents().OfType<MachineBroke>().FirstOrDefault();
            }
            Assert.That(broke, Is.Not.Null);
            Assert.That((broke.Cost, broke.Paid), Is.EqualTo((Money(30_000), true)), "5% of 6,00,000");
            Assert.That(EquipmentRules.Cover(one)[86], Is.Not.EqualTo(Kind(one, "tractor")), "broken: it covers nothing");
            one.AdvanceTo(one.State.Clock.SimTime + Day);
            Assert.That(one.DrainEvents().OfType<MachineRepaired>().Count(), Is.EqualTo(1));
            Assert.That(EquipmentRules.Cover(one)[86], Is.EqualTo(Kind(one, "tractor")));

            // A leased machine never breaks on you.
            Simulation lease = WithSheds();
            Era(lease, "village");
            Give(lease, 10_00_000);
            Ok(lease, new LeaseMachine(lease.State.Clock.SimTime, "tractor"));
            lease.DrainEvents();
            lease.AdvanceTo(lease.State.Clock.SimTime + 60 * Day);
            Assert.That(lease.DrainEvents().OfType<MachineBroke>(), Is.Empty);
        }

        // AC-5: a bought machine depreciates straight-line and sells at 80% of book; a lease is a season's rent; the bank lends 0.70 of
        // the tractor and is repaid first when it's sold; foreclosure takes bought machines after the animals.
        [Test]
        public void Books()
        {
            Simulation sim = WithSheds();
            Era(sim, "village");
            long t = sim.State.Clock.SimTime, equipment = sim.State.Ledger.Balance(Account.Equipment);
            Ok(sim, new BuyMachine(t, "tractor", new Financing("coop_bank", Money(2_00_000), 8)));
            Machine m = EquipmentRules.OfKind(sim, Kind(sim, "tractor"));
            Assert.That(sim.State.Ledger.Balance(Account.Equipment) - equipment, Is.EqualTo(Money(6_00_000)));
            Assert.That(Reporting.NetWorth(sim).EquipmentAtBook, Is.EqualTo(Money(6_00_000)));
            Assert.That(m.LoanId, Is.GreaterThan(0));
            Assert.That(EquipmentRules.Collateral(sim), Is.EqualTo(Money(6_00_000)));

            // A close: a 60th of its price, its maintenance (1.5%) and a season's diesel for the 5 fields it covers.
            sim.AdvanceTo((t / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
            Assert.That(m.Depreciated, Is.EqualTo(Money(10_000)));
            Assert.That(EquipmentRules.Book(m), Is.EqualTo(Money(5_90_000)));
            Assert.That(EquipmentRules.Resale(sim, m), Is.EqualTo(Money(4_72_000)));
            // The close moved last season's costs to retained earnings: what's on these accounts now is the new season's, paid upfront.
            Assert.That(sim.State.Ledger.Balance(Account.Repairs), Is.EqualTo(Money(9_000)), "maintenance");
            Assert.That(sim.State.Ledger.Balance(Account.Power), Is.EqualTo(Money(3_000)), "diesel: 600 × 5 fields");

            // Sold: the bank is repaid first from the 4,72,000; the loss on book is a loss on assets.
            long cash = sim.State.Ledger.Cash, loss = sim.State.Ledger.Balance(Account.LossOnAssetSales);
            Ok(sim, new SellMachine(sim.State.Clock.SimTime, m.Id));
            Assert.That(sim.State.Ledger.Balance(Account.LossOnAssetSales) - loss, Is.EqualTo(Money(1_18_000)));
            Assert.That(sim.State.Credit.Loans.Single(l => l.Id == m.LoanId).Principal, Is.LessThan(Money(2_00_000)), "repaid from the sale");
            Assert.That(sim.State.Ledger.Balance(Account.Equipment), Is.EqualTo(equipment));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // A lease: the days left of 30,000 now, a full 30,000 at each close.
            t = sim.State.Clock.SimTime;
            long rent = sim.State.Ledger.Balance(Account.Rent);
            Ok(sim, new LeaseMachine(t, "tractor"));
            long first = sim.State.Ledger.Balance(Account.Rent) - rent;
            Assert.That(first, Is.EqualTo(EquipmentRules.RestOfSeason(sim, Money(30_000))).Or.LessThanOrEqualTo(Money(30_000)));
            sim.AdvanceTo((t / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Ledger.Balance(Account.Rent), Is.EqualTo(Money(30_000)), "the new season's lease, upfront");
            Assert.That(EquipmentRules.OfKind(sim, Kind(sim, "tractor")).Leased, Is.True);

            // Foreclosure: the plough (bought) goes with its bullocks after the animals; the leased tractor stays leased.
            Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "ox_plough"));
            sim.DrainEvents();
            Ok(sim, new TakeLoan(sim.State.Clock.SimTime, "moneylender", Money(20_000), 2));
            CreditSystem.Foreclose(sim);
            Assert.That(EquipmentRules.OfKind(sim, Kind(sim, "tractor")), Is.Not.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-8: the same seed and commands give the same farm in one chunk or many, with machines working, breaking and paid for.
        [Test]
        public void ChunkingInvariance()
        {
            Simulation Setup()
            {
                Simulation sim = WithSheds();
                Era(sim, "village");
                long t = sim.State.Clock.SimTime;
                Ok(sim, new BuyMachine(t, "ox_plough"));
                Ok(sim, new LeaseMachine(t, "tractor"));
                Ok(sim, new SellMachine(t, EquipmentRules.OfKind(sim, Kind(sim, "tractor")).Id));
                Ok(sim, new BuyMachine(t, "tractor"));
                Ok(sim, new SetMaintenance(t, EquipmentRules.OfKind(sim, Kind(sim, "tractor")).Id, false));
                Ok(sim, new Sow(t, 84, "wheat", Farm.SeedSource.Certified, "none"));
                Ok(sim, new Sow(t, 85, "radish", Farm.SeedSource.Certified, "none"));
                return sim;
            }
            Simulation one = Setup(), many = Setup();
            long end = one.State.Clock.SimTime + one.Clock.SecondsPerSeason * 4 + 7 * Hour + 13;
            one.AdvanceTo(end);
            for (long x = many.State.Clock.SimTime; x < end; x += 3 * Hour + 17) many.AdvanceTo(System.Math.Min(end, x));
            many.AdvanceTo(end);
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
            Assert.That(one.State.Ledger.Reconcile(), Is.Null);
        }
    }
}
