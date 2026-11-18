using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-148 part 1 (spec 023 §3.1, §3.2, §3.6, §3.9, ADR 0116): the clinic, the doctor's quarters, the doctor, the five effects, the doctor leaving and returning.</summary>
    public class ClinicTests
    {
        private const long Share = 48_000 * Coin, QuartersCost = 90_000 * Coin;

        /// <summary>A Village town with one family and a lot of cash; <paramref name="edit"/> changes content.</summary>
        private static Simulation Start(System.Func<string, string, string> edit = null)
        {
            Simulation sim = TreasuryTests.Start(edit);
            Gift(sim, 3_000_000 * Coin);
            TreasuryTests.Family(sim);
            sim.DrainEvents();
            return sim;
        }

        private static void Gift(Simulation sim, long amount)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
        }

        /// <summary>Both built and the doctor in post.</summary>
        private static Simulation WithDoctor(System.Func<string, string, string> edit = null)
        {
            Simulation sim = Start(edit);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildClinic(t));
            Ok(sim, new BuildDoctorQuarters(t));
            sim.AdvanceTo(t + (48 + 24) * Hour);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            return sim;
        }

        // AC-1: before the Village it is locked; the share is a gift, the clinic stands in 48 h; the quarters are an asset of yours.
        [Test]
        public void Build()
        {
            Simulation early = NewFarm(game: Game(), options: new SimOptions { DevCommandsEnabled = true });
            Assert.That(early.Execute(new BuildClinic(early.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.EraLocked));
            Assert.That(early.Execute(new BuildDoctorQuarters(early.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.EraLocked));

            Simulation sim = Start();
            long cash = sim.State.Ledger.Cash, t = sim.State.Clock.SimTime;
            Assert.That(ClinicRules.Price(sim), Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, Share)));
            Ok(sim, new BuildClinic(t));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - ClinicRules.Price(sim)));
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage), Is.EqualTo(ClinicRules.Price(sim)), "a gift to the village, outside farm profit");
            Assert.That(sim.PendingEvents.OfType<ClinicStarted>().Single().ReadyAt, Is.EqualTo(t + 48 * Hour));
            Assert.That(sim.Execute(new BuildClinic(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            long buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new BuildDoctorQuarters(t));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(ClinicRules.QuartersPrice(sim)), "the quarters are an asset at cost");
            Assert.That(ClinicRules.QuartersPrice(sim), Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, QuartersCost)));
            Assert.That(sim.Execute(new BuildDoctorQuarters(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            sim.AdvanceTo(t + 48 * Hour - 1);
            Assert.That(ClinicRules.IsOpen(sim), Is.False, "not before the 48th hour");
            sim.AdvanceTo(t + 48 * Hour);
            Assert.That(ClinicRules.IsOpen(sim), Is.True);
            Assert.That(ClinicRules.QuartersStand(sim), Is.True);
            Assert.That(sim.State.Land.Buildings.Any(b => ClinicRules.IsQuarters(sim, b)), Is.True);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void Build_NeedsTheCash()
        {
            Simulation sim = Start();
            long left = sim.State.Ledger.Cash - 100 * Coin;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.LivingCosts, left), new Posting(Account.Cash, -left));
            Assert.That(sim.Execute(new BuildClinic(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(ClinicRules.State(sim).Status, Is.EqualTo(ClinicStatus.None), "nothing started");
        }

        // AC-1: the doctor arrives 24 h after both stand, a literate household of one to three that counts as yours; the quarters' rent comes out of his salary.
        [Test]
        public void Doctor()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            int households = EraRules.Households(sim);
            Ok(sim, new BuildClinic(t));
            sim.AdvanceTo(t + 48 * Hour);
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.None), "no quarters yet: no doctor on the way");
            Ok(sim, new BuildDoctorQuarters(sim.State.Clock.SimTime));
            long both = sim.State.Clock.SimTime + 48 * Hour;
            sim.AdvanceTo(both);
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.Coming));
            Assert.That(ClinicRules.State(sim).ArrivesAt, Is.EqualTo(both + 24 * Hour));
            sim.AdvanceTo(both + 24 * Hour - 1);
            Assert.That(ClinicRules.InPost(sim), Is.False, "not before the 24th hour");
            sim.AdvanceTo(both + 24 * Hour);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            Household x = ClinicRules.DoctorHousehold(sim);
            Assert.That(x.Members.Count, Is.InRange(1, 3));
            Assert.That(EraRules.Households(sim), Is.EqualTo(households + 1), "he counts as one of your households");
            Assert.That(x.HomeId, Is.EqualTo(ClinicRules.State(sim).QuartersId));
            Citizen doc = ClinicRules.Doctor(sim);
            Assert.That(doc.Literate, Is.True);
            Assert.That(x.Rent, Is.EqualTo(ClinicRules.Rent(sim)));
            Assert.That(x.Income, Is.EqualTo(ClinicRules.Salary(sim)), "the district's pay");
            Assert.That(ClinicRules.Rent(sim), Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 3_750 * Coin)));
            Assert.That(sim.PendingEvents.OfType<DoctorArrived>().Single().Returned, Is.False);
            Assert.That(TreasuryRules.IsOpen(sim), Is.False, "before the Township the district pays his salary, his allowance and the upkeep: you owe nothing recurring");
        }

        // AC-1: the rent is paid at the close from his salary, and he is never made to leave before the Township.
        [Test]
        public void Doctor_PaysRentAndStays()
        {
            Simulation sim = WithDoctor();
            long rentBefore = sim.State.Ledger.Balance(Account.RentReceived);
            for (int i = 0; i < 6; i++) sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.InPost(sim), Is.True, "before the Township nothing can make him leave");
            Assert.That(sim.State.Ledger.Balance(Account.RentReceived), Is.LessThan(rentBefore), "rent received is a credit balance: it grew");
            Assert.That(ClinicRules.DoctorHousehold(sim).RentBehind, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-2: e = condition × covered × scale; the five effects follow it.
        [Test]
        public void Effects()
        {
            Simulation sim = Start();
            Household fam = sim.State.Citizens.Households[0];
            Assert.That(ClinicRules.Effect(sim, fam), Is.EqualTo(Fixed.Zero), "no clinic, no effect");
            Fixed chemist = sim.Config.Game.Population.Chemist;
            Assert.That(ClinicRules.HealthCareMet(sim, fam, chemist), Is.EqualTo(chemist));

            sim = WithDoctor();
            fam = sim.State.Citizens.Households.First(h => !ClinicRules.IsDoctorHome(sim, h));
            Assert.That(ClinicRules.Effect(sim, fam), Is.EqualTo(Fixed.One), "full service, in reach, under capacity");
            Assert.That(ClinicRules.HealthCareMet(sim, fam, chemist), Is.EqualTo(Fixed.One), "0.5 + 0.5 × e");
            Fixed chance = Fixed.FromRatio(1, 10);
            Assert.That(ClinicRules.SickChance(sim, chance, Fixed.One), Is.EqualTo(chance * Fixed.FromRatio(1, 2)), "sick chance × (1 − 0.5 e)");
            Assert.That(ClinicRules.SickChance(sim, chance, Fixed.Zero), Is.EqualTo(chance));

            // an elder's chance of death × (1 − 0.25 e)
            var elder = new Citizen { Id = sim.State.Citizens.NextCitizenId++, BornSeason = CitizenRules.BornFor(sim, 70), HouseholdId = fam.Id, Health = Fixed.One };
            sim.State.Citizens.Citizens.Add(elder);
            fam.Members.Add(elder.Id);
            Assert.That(PopulationRules.DeathChance(sim, elder), Is.EqualTo(PopulationRules.DeathChanceWithout(sim, elder) * Fixed.FromRatio(3, 4)));

            // a half-funded Health service halves the effect
            TreasuryTests.Township(sim);
            sim.State.Treasury.Condition[1] = Fixed.FromRatio(1, 2);
            Assert.That(ClinicRules.Effect(sim, fam), Is.EqualTo(Fixed.FromRatio(1, 2)), "the Health department's condition scales every row");
            sim.State.Treasury.Condition[1] = Fixed.One;
        }

        // AC-2 / AC-7: capacity scales the effect (400 people at full strength), a home out of reach gets nothing, no doctor covers nobody.
        [Test]
        public void Coverage()
        {
            Simulation sim = WithDoctor(delegate(string f, string j) { return f == "township.json" ? j.Replace("\"capacity\": 400", "\"capacity\": 4") : j; });
            Household fam = sim.State.Citizens.Households.First(h => !ClinicRules.IsDoctorHome(sim, h));
            int people = ClinicRules.Served(sim);
            Assert.That(people, Is.GreaterThan(4));
            Assert.That(ClinicRules.Effect(sim, fam), Is.EqualTo(Fixed.FromRatio(4, people)), "a slow decline: capacity ÷ people served");

            Simulation far = WithDoctor(delegate(string f, string j) { return f == "township.json" ? j.Replace("\"reach\": 2000", "\"reach\": 100") : j; });
            Household home = far.State.Citizens.Households.First(h => !ClinicRules.IsDoctorHome(far, h));
            Assert.That(ClinicRules.Effect(far, home), Is.EqualTo(Fixed.Zero), "out of reach: nothing");
            Assert.That(CoverageRules.Metres(far, 93, 109), Is.EqualTo(100), "an acre is 100 m, Manhattan: the clinic (13, 5) and the quarters (13, 6)");
            Assert.That(CoverageRules.Metres(far, 29, 93), Is.EqualTo((13 - 13) * 100 + (5 - 1) * 100 + 0), "the yard (13, 1) to the clinic (13, 5)");
        }

        // AC-7: the school follows the same rule: a home out of its 1,500 m reach earns its children no school year.
        [Test]
        public void SchoolReach()
        {
            Simulation sim = Start(delegate(string f, string j) { return f == "population.json" ? j.Replace("\"reach\": 1500", "\"reach\": 100") : j; });
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 49 * Hour);
            Household fam = sim.State.Citizens.Households[0];
            Assert.That(SchoolRules.HomeCovered(sim, fam), Is.True, "before the Township every home is served, as in Phases 1 and 2");
            TreasuryTests.Township(sim);
            Citizen child = new Citizen { Id = sim.State.Citizens.NextCitizenId++, BornSeason = CitizenRules.BornFor(sim, 8), HouseholdId = fam.Id, Health = Fixed.One };
            sim.State.Citizens.Citizens.Add(child);
            fam.Members.Add(child.Id);
            Assert.That(SchoolRules.Covered(sim, child), Is.False, "the family's acre is farther than 100 m from the school");
            for (int i = 0; i < 3; i++) sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(child.SchoolYears, Is.EqualTo(0), "no year counts out of reach");

            Simulation near = Start();
            Ok(near, new BuildSchool(near.State.Clock.SimTime));
            near.AdvanceTo(near.State.Clock.SimTime + 49 * Hour);
            TreasuryTests.Township(near);
            Household home = near.State.Citizens.Households[0];
            Citizen kid = new Citizen { Id = near.State.Citizens.NextCitizenId++, BornSeason = CitizenRules.BornFor(near, 8), HouseholdId = home.Id, Health = Fixed.One };
            near.State.Citizens.Citizens.Add(kid);
            home.Members.Add(kid.Id);
            Assert.That(SchoolRules.Covered(near, kid), Is.True, "every home is inside 1,500 m on today's map");
            for (int i = 0; i < 3; i++) near.AdvanceTo(TreasuryTests.NextClose(near) + 1);
            Assert.That(kid.SchoolYears, Is.GreaterThan(0));
        }

        // AC-7: a building with no doctor covers nobody.
        [Test]
        public void Staff()
        {
            Simulation sim = Start();
            Ok(sim, new BuildClinic(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 48 * Hour);
            Household fam = sim.State.Citizens.Households[0];
            Assert.That(ClinicRules.IsOpen(sim), Is.True);
            Assert.That(ClinicRules.Effect(sim, fam), Is.EqualTo(Fixed.Zero), "open but nobody in post");
            Assert.That(TreasuryRulesStaffed(sim), Is.False);
        }

        private static bool TreasuryRulesStaffed(Simulation sim) { return TreasuryRules.ClinicStaffed(sim); }

        // AC-2: the benefit is an exact count: a roll compared against both chances.
        [Test]
        public void Avoided()
        {
            Simulation sim = WithDoctor();
            ClinicState s = ClinicRules.State(sim);
            int counted = 0; // the counts roll over at each close: add up every season's, not just the last two
            for (int i = 0; i < 8; i++)
            {
                foreach (Citizen c in sim.State.Citizens.Citizens) c.Health = Fixed.Parse("0.5"); // a town that needs a doctor (a healthy one counts nothing)
                sim.State.Farm.Founder.Health = Fixed.Parse("0.6");
                sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
                counted += s.SickAvoidedLast + s.RegainedLast + s.EldersWellLast;
            }
            Assert.That(counted, Is.GreaterThan(0), "a doctor's town counts the sick days he saved, the health he regained and the elders he kept");
        }

        // Review fix: the doctor's death ends his post (the clinic is never staffed by nobody) and a new doctor is called at once.
        [Test]
        public void DoctorDies()
        {
            Simulation sim = WithDoctor();
            ClinicState s = ClinicRules.State(sim);
            Citizen doctor = sim.State.Citizens.Citizen(s.DoctorId);
            Assert.That(doctor, Is.Not.Null, "the doctor is a citizen the clinic knows");
            Household home = sim.State.Citizens.Household(s.HouseholdId);
            home.Members.Remove(doctor.Id);
            if (home.Members.Count == 0) sim.State.Citizens.Households.Remove(home);
            ClinicRules.CitizenDied(sim, doctor);
            Assert.That((s.Doctor, s.HouseholdId, s.DoctorId), Is.EqualTo((DoctorStatus.Coming, 0L, 0L)), "nobody is in post; another doctor is on his way");
            Assert.That(ClinicRules.InPost(sim), Is.False);
            sim.AdvanceTo(sim.State.Clock.SimTime + (Def(sim) + 2) * Hour);
            Assert.That(ClinicRules.InPost(sim), Is.True, "a new doctor takes the quarters");
            Assert.That(s.DoctorId, Is.Not.EqualTo(doctor.Id));
        }

        private static int Def(Simulation sim) { return ClinicRules.Def(sim).DoctorArrivesHours; }

        // AC-11: at the Township the doctor leaves at the second close in a row with Health under 50% (the third unpaid close); the quarters stay reserved.
        [Test]
        public void Leaves()
        {
            Simulation sim = WithDoctor(delegate(string f, string j) { return f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 10000") : j; });
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.InPost(sim), Is.True, "the first unpaid close: a warning");
            Assert.That(sim.PendingEvents.OfType<DoctorUnpaid>().Count(), Is.EqualTo(1));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.InPost(sim), Is.False, "he left at the third unpaid close");
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.Left));
            Assert.That(sim.PendingEvents.OfType<DoctorLeft>().Count(), Is.EqualTo(1));
            Assert.That(ClinicRules.State(sim).Quarters, Is.EqualTo(QuartersStatus.Built), "the quarters stay reserved");
            Assert.That(ClinicRules.Effect(sim, sim.State.Citizens.Households[0]), Is.EqualTo(Fixed.Zero), "the clinic covers nobody");
            Assert.That(TreasuryRules.ClinicStaffed(sim), Is.False, "the tied grant stops");
            Assert.That(sim.State.Era.Current, Is.EqualTo(sim.Config.Game.Onboarding.Eras.ToList().FindIndex(e => e.Id == "township")), "the Township stays");
        }

        // AC-11: a new doctor comes at the first close with Health at 70% or more and the cash for the allowance, 24 h later.
        [Test]
        public void Returns()
        {
            Simulation sim = WithDoctor(delegate(string f, string j) { return f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 10000") : j; });
            TreasuryTests.Township(sim);
            for (int i = 0; i < 3; i++) sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.Left));
            sim.DrainEvents();
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 400_000 * Coin));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.Left), "Health is only at about 56% after one funded close");
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(ClinicRules.State(sim).Doctor, Is.EqualTo(DoctorStatus.Coming), "at 70% with the cash a doctor is called");
            long arrives = ClinicRules.State(sim).ArrivesAt;
            sim.AdvanceTo(arrives);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            Assert.That(sim.PendingEvents.OfType<DoctorArrived>().Single().Returned, Is.True);
            Assert.That(ClinicRules.Doctor(sim), Is.Not.Null);
            Assert.That(ClinicRules.State(sim).Doctors, Is.EqualTo(2));
        }

        // AC-12: a town with all of it saves and loads to the same hash.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start();
            SimConfig config = sim.Config;
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildClinic(t));
            Ok(sim, new BuildDoctorQuarters(t));
            sim.AdvanceTo(t + 60 * Hour); // both stand, the doctor on his way
            LoadResult mid = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(mid.Error, Is.EqualTo(SaveError.None), mid.Detail);
            Assert.That(mid.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Simulation loaded = Simulation.FromState(config, mid.State);
            loaded.AdvanceTo(t + 80 * Hour);
            Assert.That(ClinicRules.InPost(loaded), Is.True, "the arrival job survived the save");
            for (int i = 0; i < 3; i++) loaded.AdvanceTo(TreasuryTests.NextClose(loaded) + 1);
            LoadResult done = SaveCodec.Read(SaveCodec.Write(loaded.State, config, Wall0), config, 3);
            Assert.That(done.Error, Is.EqualTo(SaveError.None), done.Detail);
            Assert.That(done.State.ComputeHash(), Is.EqualTo(loaded.State.ComputeHash()));
            Assert.That(done.State.Township.Clinic.DoctorName, Is.EqualTo(loaded.State.Township.Clinic.DoctorName));
        }

        // Nothing started adds nothing to the hash.
        [Test]
        public void NothingStarted_AddsNothingToTheHash()
        {
            Simulation a = Start(), b = Start();
            Assert.That(a.State.ComputeHash(), Is.EqualTo(b.State.ComputeHash()));
            Assert.That(a.State.Township.IsDefault, Is.True);
            Ok(a, new BuildClinic(a.State.Clock.SimTime));
            Assert.That(a.State.Township.IsDefault, Is.False);
        }
    }
}
