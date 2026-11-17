using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;

namespace Sim.Core.Town
{
    /// <summary>
    /// The village clinic (spec 023 §3.1, §3.2, §3.9, US-148, ADR 0116): from the Village the district builds it for your community share and
    /// you build the doctor's quarters; once both stand a migrant doctor arrives and the clinic works. It cuts the sick days, the health lost
    /// to hunger, speeds recovery, lengthens elders' lives and meets the <i>Health care</i> need, each by <c>e = condition × covered × scale</c>.
    /// </summary>
    public static class ClinicRules
    {
        public const string BuildJob = "clinic.build", QuartersJob = "clinic.quarters", ArrivalJob = "clinic.doctor";

        /// <summary>The Health department's place in the treasury's payment order (spec 021 §3.4).</summary>
        private const int HealthDept = 1;

        public static ClinicDef Def(Simulation sim) { return sim.Config.Game?.Township?.Clinic; }

        public static QuartersDef QuartersDef(Simulation sim) { return sim.Config.Game?.Township?.Quarters; }

        public static DoctorDef DoctorDef(Simulation sim) { return sim.Config.Game?.Township?.Doctor; }

        public static ClinicState State(Simulation sim) { return sim.State.Township?.Clinic; }

        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Township != null && sim.State.Citizens != null; }

        public static bool Unlocked(Simulation sim) { return On(sim) && (sim.State.Era == null || sim.State.Era.Current >= Def(sim).UnlockEra); }

        public static bool IsOpen(Simulation sim) { return On(sim) && State(sim).Status == ClinicStatus.Open; }

        public static bool QuartersStand(Simulation sim) { return On(sim) && State(sim).Quarters == QuartersStatus.Built; }

        /// <summary>A doctor is in post at an open clinic: the clinic works and the tied grant is paid (spec 021 §3.3).</summary>
        public static bool InPost(Simulation sim) { return IsOpen(sim) && State(sim).Doctor == DoctorStatus.InPost; }

        /// <summary>The share at today's prices (a gift, indexed at the press) and the quarters' cost, rent and the doctor's salary.</summary>
        public static long Price(Simulation sim) { return MarketRules.Indexed(sim, Def(sim).Share); }

        public static long QuartersPrice(Simulation sim)
        {
            return MarketRules.Indexed(sim, sim.Config.Game.Buildings[QuartersDef(sim).Building].Value);
        }

        public static long Rent(Simulation sim) { return MarketRules.Indexed(sim, QuartersDef(sim).Rent); }

        public static long Salary(Simulation sim) { return MarketRules.Indexed(sim, DoctorDef(sim).Salary); }

        /// <summary>Why the clinic can't be started now, or None (before the loan's and the cash's checks).</summary>
        public static Commands.RejectReason CanBuildClinic(Simulation sim)
        {
            if (!On(sim)) return Commands.RejectReason.InvalidArgument;
            if (!Unlocked(sim)) return Commands.RejectReason.EraLocked;
            return State(sim).Status != ClinicStatus.None ? Commands.RejectReason.AlreadyBuilt : Commands.RejectReason.None;
        }

        public static Commands.RejectReason CanBuildQuarters(Simulation sim)
        {
            if (!On(sim)) return Commands.RejectReason.InvalidArgument;
            if (!Unlocked(sim)) return Commands.RejectReason.EraLocked;
            return State(sim).Quarters != QuartersStatus.None ? Commands.RejectReason.AlreadyBuilt : Commands.RejectReason.None;
        }

        // ---- the doctor's household ----

        /// <summary>The household of the doctor in post, or null.</summary>
        public static Household DoctorHousehold(Simulation sim)
        {
            ClinicState s = State(sim);
            return s == null || s.Doctor != DoctorStatus.InPost ? null : sim.State.Citizens.Household(s.HouseholdId);
        }

        /// <summary>This household is the doctor's: the district pays his salary and the quarters are let to him (spec 023 §3.1).</summary>
        public static bool IsDoctorHome(Simulation sim, Household x)
        {
            ClinicState s = State(sim);
            return s != null && s.Doctor == DoctorStatus.InPost && s.HouseholdId == x.Id;
        }

        /// <summary>This building is the doctor's quarters.</summary>
        public static bool IsQuarters(Simulation sim, Building b)
        {
            return b != null && sim.Config.Game.Township != null && b.Def == QuartersDef(sim).Building;
        }

        /// <summary>The doctor himself (the first in his household), or null.</summary>
        public static Citizen Doctor(Simulation sim)
        {
            Household x = DoctorHousehold(sim);
            return x == null || x.Members.Count == 0 ? null : sim.State.Citizens.Citizen(x.Members[0]);
        }

        // ---- reach, capacity and staff (spec 023 §3.6) ----

        /// <summary>The clinic's condition: the Health department's service level once the treasury is open, else the district's, in full.</summary>
        public static Fixed Condition(Simulation sim)
        {
            return TreasuryRules.IsOpen(sim) ? TreasuryRules.State(sim).Condition[HealthDept] : Fixed.One;
        }

        /// <summary>People the clinic serves: every citizen of your households (the old village's cohort joins them by spec 023 §3.5).</summary>
        public static int Served(Simulation sim) { return sim.State.Citizens?.Population ?? 0; }

        /// <summary><c>min(1, capacity ÷ people served)</c>: a slow decline, never a cliff (spec 023 §3.6).</summary>
        public static Fixed Scale(Simulation sim)
        {
            int served = Served(sim), capacity = Def(sim).Capacity;
            return served <= capacity ? Fixed.One : Fixed.FromRatio(capacity, served);
        }

        /// <summary>A home's tile is within the clinic's reach (spec 023 §3.6).</summary>
        public static bool Covered(Simulation sim, int homeTile) { return CoverageRules.Within(sim, homeTile, Def(sim).Tile, Def(sim).Reach); }

        /// <summary>
        /// A household's effect (spec 023 §3.2): <c>condition × covered × scale</c> with a doctor in post at an open clinic, else 0. Its home is
        /// the building it lives in.
        /// </summary>
        public static Fixed Effect(Simulation sim, Household x)
        {
            if (x == null || !InPost(sim)) return Fixed.Zero;
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            return b != null && !Covered(sim, b.TileId) ? Fixed.Zero : Condition(sim) * Scale(sim);
        }

        /// <summary>The effect for the founder, hands and workers (the farm and the village lie inside the reach): <c>condition × scale</c>.</summary>
        public static Fixed EffectTown(Simulation sim)
        {
            return InPost(sim) ? Condition(sim) * Scale(sim) : Fixed.Zero;
        }

        /// <summary>The chance of a sick day with the clinic's cut: <c>chance × (1 − sickCut × e)</c>.</summary>
        public static Fixed SickChance(Simulation sim, Fixed chance, Fixed e)
        {
            return e == Fixed.Zero ? chance : chance * (Fixed.One - Def(sim).SickCut * e);
        }

        /// <summary>The household's <i>Health care</i> need met (spec 023 §3.2): the chemist's share, and the rest by <c>e</c>.</summary>
        public static Fixed HealthCareMet(Simulation sim, Household x, Fixed chemist)
        {
            Fixed e = Effect(sim, x);
            return e == Fixed.Zero ? chemist : chemist + (Fixed.One - chemist) * e;
        }

        // ---- the benefit's counts (spec 023 §3.2) ----

        internal static void CountSickAvoided(Simulation sim) { State(sim).SickAvoidedThis++; }

        internal static void CountRegained(Simulation sim) { State(sim).RegainedThis++; }

        internal static void CountElderWell(Simulation sim) { State(sim).EldersWellThis++; }

        // ---- the buildings and the doctor's arrival ----

        /// <summary>Both stand and nobody is in post or on his way: a doctor comes <c>doctorArrivesHours</c> later (spec 023 §3.1).</summary>
        internal static void MaybeCallDoctor(Simulation sim)
        {
            ClinicState s = State(sim);
            if (s.Status != ClinicStatus.Open || s.Quarters != QuartersStatus.Built) return;
            if (s.Doctor == DoctorStatus.InPost || s.Doctor == DoctorStatus.Coming) return;
            long t = sim.State.Clock.SimTime;
            s.Doctor = DoctorStatus.Coming;
            s.ArrivesAt = t + Def(sim).DoctorArrivesHours * 3600L;
            s.ArrivalJobId = sim.ScheduleJob(ArrivalJob, 0, s.ArrivesAt).Id;
            sim.Emit(new DoctorComing(t, s.ArrivesAt));
        }

        /// <summary>The clinic is finished (spec 023 §3.1): it stands, and with the quarters the doctor is called.</summary>
        internal sealed class Build : IJobHandler
        {
            public string JobType { get { return BuildJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                ClinicState s = State(sim);
                s.Status = ClinicStatus.Open;
                s.JobId = 0;
                sim.CompleteJob(job);
                sim.Emit(new ClinicBuilt(sim.State.Clock.SimTime));
                MaybeCallDoctor(sim);
            }
        }

        /// <summary>The doctor's quarters are finished: a building of yours on its plot, at its cost (spec 023 §3.1).</summary>
        internal sealed class BuildQuarters : IJobHandler
        {
            public string JobType { get { return QuartersJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                ClinicState s = State(sim);
                LandState land = sim.State.Land;
                var b = new Building { Id = land.NextBuildingId++, Def = QuartersDef(sim).Building, TileId = QuartersDef(sim).Tile, Cost = s.QuartersCost };
                land.Buildings.Add(b);
                s.Quarters = QuartersStatus.Built;
                s.QuartersId = b.Id;
                s.QuartersJobId = 0;
                sim.CompleteJob(job);
                sim.Emit(new QuartersBuilt(sim.State.Clock.SimTime));
                MaybeCallDoctor(sim);
            }
        }

        /// <summary>The doctor arrives (spec 023 §3.1): a literate household of one to three moves into the quarters and counts as yours.</summary>
        internal sealed class Arrive : IJobHandler
        {
            public string JobType { get { return ArrivalJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                ClinicState s = State(sim);
                long t = sim.State.Clock.SimTime;
                Building home = sim.State.Land.Buildings.Find(y => y.Id == s.QuartersId);
                s.ArrivalJobId = 0;
                sim.CompleteJob(job);
                if (home == null) { s.Doctor = DoctorStatus.None; return; }
                bool returned = s.Doctors > 0;
                Household x = MigrationRules.ArriveDoctor(sim, home, s.Doctors);
                s.Doctors++;
                s.Doctor = DoctorStatus.InPost;
                s.HouseholdId = x.Id;
                s.ArrivesAt = 0;
                s.DoctorName = sim.State.Citizens.Citizen(x.Members[0]).Name;
                s.SinceSeason = CitizenRules.SeasonOf(sim, t);
                s.UnderCloses = 0;
                HouseholdRules.SetBudget(sim, x); // his salary against the quarters' rent
                sim.Emit(new DoctorArrived(t, x.Members[0], x.Members.Count, returned));
                Progress.EraRules.OnTown(sim); // a household of yours moved in: the gates (spec 018 §3.6, spec 023 §3.4)
            }
        }

        // ---- the close (spec 023 §3.9, §3.10) ----

        /// <summary>
        /// The season's close after the treasury's: the clinic's benefit rolls over; at the Township the doctor leaves at the second close in a
        /// row with Health's condition under <c>healthLeaveBelow</c>, and a new one is called once it is at <c>returnCondition</c> and the cash
        /// covers the allowance. Before the Township the district pays him and nothing can make him leave.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            ClinicState s = State(sim);
            s.SickAvoidedLast = s.SickAvoidedThis; s.SickAvoidedThis = 0;
            s.RegainedLast = s.RegainedThis; s.RegainedThis = 0;
            s.EldersWellLast = s.EldersWellThis; s.EldersWellThis = 0;
            if (!TreasuryRules.TakenOver(sim)) return;
            long t = sim.State.Clock.SimTime;
            TreasuryDef td = TreasuryRules.Def(sim);
            Fixed condition = Condition(sim);
            int leave = DoctorDef(sim).LeaveCloses;
            if (s.Doctor == DoctorStatus.InPost)
            {
                s.UnderCloses = condition < td.HealthLeaveBelow ? s.UnderCloses + 1 : 0;
                if (s.UnderCloses >= leave) DoctorLeaves(sim, s);
                else if (TreasuryRules.State(sim).Unpaid[HealthDept] > 0) sim.Emit(new DoctorUnpaid(t, s.UnderCloses, leave));
            }
            else if (s.Doctor == DoctorStatus.Left && condition >= DoctorDef(sim).ReturnCondition
                && TreasuryRules.State(sim).Ledger.Cash >= MarketRules.Indexed(sim, td.DoctorAllowance))
            {
                s.Doctor = DoctorStatus.None;
                MaybeCallDoctor(sim);
            }
        }

        private static void DoctorLeaves(Simulation sim, ClinicState s)
        {
            Household x = sim.State.Citizens.Household(s.HouseholdId);
            if (x != null) CitizenRules.Leave(sim, x);
            s.Doctor = DoctorStatus.Left;
            s.HouseholdId = 0;
            s.UnderCloses = 0;
            sim.Emit(new DoctorLeft(sim.State.Clock.SimTime));
        }
    }
}
