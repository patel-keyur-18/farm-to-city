using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Scheduling;

namespace Sim.Core.People
{
    /// <summary>
    /// The village's first school (spec 018 §3.1, ADR 0077, 0095): the founder builds it on the school plot and gives it to the district,
    /// which posts and pays the teacher; its cost, its upkeep and adult classes are gifts to the village, outside farm profit.
    /// </summary>
    public static class SchoolRules
    {
        public const string Job = "school.build", ClassJob = "school.class";

        public static SchoolDef Def(Simulation sim) { return sim.Config.Game?.School; }

        public static SchoolState State(Simulation sim) { return sim.State.Citizens?.School; }

        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Citizens != null; }

        public static bool IsOpen(Simulation sim) { return On(sim) && State(sim).Status == SchoolStatus.Open; }

        public static bool Unlocked(Simulation sim) { return On(sim) && (sim.State.Era == null || sim.State.Era.Current >= Def(sim).UnlockEra); }

        /// <summary>The building and its benches, slates and books at today's prices.</summary>
        public static long Price(Simulation sim) { return MarketRules.Indexed(sim, Def(sim).Cost); }

        public static long Upkeep(Simulation sim) { return MarketRules.Indexed(sim, Def(sim).UpkeepPerSeason); }

        /// <summary>Why the school can't be started now, or None (before the loan's and the cash's checks).</summary>
        public static RejectReason CanBuild(Simulation sim)
        {
            if (!On(sim)) return RejectReason.InvalidArgument;
            if (!Unlocked(sim)) return RejectReason.EraLocked;
            return State(sim).Status != SchoolStatus.None ? RejectReason.AlreadyBuilt : RejectReason.None;
        }

        /// <summary>Children of school age in the town who can't read yet: everyone goes, free (spec 018 §3.1).</summary>
        public static List<Citizen> Pupils(Simulation sim)
        {
            var list = new List<Citizen>();
            if (!IsOpen(sim)) return list;
            SchoolDef d = Def(sim);
            foreach (Citizen c in sim.State.Citizens.Citizens)
                if (IsPupilAge(d, c, CitizenRules.Age(sim, c))) list.Add(c);
            return list;
        }

        private static bool IsPupilAge(SchoolDef d, Citizen c, int age) { return c.Living && !c.Literate && age >= d.PupilAgeMin && age <= d.PupilAgeMax; }

        /// <summary>
        /// Season closes until a pupil can read (spec 018 §7: "next to finish"): the years they still need, counted from this season if
        /// the school was open all through it, else from the next; −1 when they'd turn too old first (the adult class takes them).
        /// </summary>
        public static int SeasonsToFinish(Simulation sim, Citizen c)
        {
            SchoolDef d = Def(sim);
            long season = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime);
            int need = d.PrimaryYears - c.SchoolYears, k = State(sim).ReadyAt <= season * sim.Clock.SecondsPerSeason ? 0 : 1;
            int perSeason = sim.Config.Game.Population.LifeYearsPerSeason;
            for (; need > 0; k++)
            {
                if ((season + k - c.BornSeason) * perSeason > d.PupilAgeMax) return -1;
                need--;
            }
            return k;
        }

        public static long ClassCost(Simulation sim) { return MarketRules.Indexed(sim, Def(sim).ClassCost); }

        public static bool ClassRunning(Simulation sim) { return On(sim) && State(sim).ClassEndsAt > 0; }

        /// <summary>
        /// Who an adult class would take now (ADR 0095 §4): up to <c>size</c> citizens aged <c>minAge</c> or older who can't read,
        /// working-age first, then elders, each in id order. Nobody is picked by hand.
        /// </summary>
        public static List<Citizen> Learners(Simulation sim)
        {
            var list = new List<Citizen>();
            if (!On(sim)) return list;
            SchoolDef d = Def(sim);
            int elderFrom = sim.Config.Game.Population.ElderFrom;
            for (int pass = 0; pass < 2; pass++)
                foreach (Citizen c in sim.State.Citizens.Citizens)
                {
                    if (list.Count == d.ClassSize) return list;
                    if (!c.Living || c.Literate) continue;
                    int age = CitizenRules.Age(sim, c);
                    if (age >= d.ClassMinAge && (age < elderFrom) == (pass == 0)) list.Add(c);
                }
            return list;
        }

        /// <summary>Why a class can't start now, or None (before the cash check).</summary>
        public static RejectReason CanStartClass(Simulation sim)
        {
            if (!On(sim)) return RejectReason.InvalidArgument;
            if (!IsOpen(sim)) return RejectReason.NoSchool;
            if (ClassRunning(sim)) return RejectReason.ClassRunning;
            return Learners(sim).Count == 0 ? RejectReason.NobodyToTeach : RejectReason.None;
        }

        /// <summary>The literacy rate's parts (spec 018 §3.2): literate citizens aged <c>literacyFromAge</c>+ and all of them.</summary>
        public static void Literacy(Simulation sim, out int literate, out int counted)
        {
            literate = counted = 0;
            if (!On(sim)) return;
            int from = Def(sim).LiteracyFromAge;
            foreach (Citizen c in sim.State.Citizens.Citizens)
            {
                if (!c.Living || CitizenRules.Age(sim, c) < from) continue;
                counted++;
                if (c.Literate) literate++;
            }
        }

        /// <summary>The literacy rate in whole percent, rounded down (0 with nobody counted); the Township needs 60 (ADR 0107).</summary>
        public static int LiteracyRate(Simulation sim)
        {
            Literacy(sim, out int literate, out int counted);
            return counted == 0 ? 0 : literate * 100 / counted;
        }

        /// <summary>A literate citizen's outside work pays <c>literatePremium</c> more (spec 018 §3.2).</summary>
        public static long WithPremium(Simulation sim, Citizen c, long pay)
        {
            return c != null && c.Literate && Def(sim) != null ? pay + Money.MulRate(pay, Def(sim).LiteratePremium) : pay;
        }

        /// <summary>
        /// The pupils' year (spec 018 §3.7), at the close: a pupil through a whole season the school was open counts a year, by their age in
        /// that season; after <c>primaryYears</c> they can read.
        /// </summary>
        private static void CountYears(Simulation sim)
        {
            long t = sim.State.Clock.SimTime, closing = CitizenRules.SeasonOf(sim, t) - 1;
            if (State(sim).ReadyAt > t - sim.Clock.SecondsPerSeason) return; // opened part-way through the season: years count from the next
            SchoolDef d = Def(sim);
            int perSeason = sim.Config.Game.Population.LifeYearsPerSeason;
            foreach (Citizen c in sim.State.Citizens.Citizens)
            {
                if (!IsPupilAge(d, c, (int)((closing - c.BornSeason) * perSeason))) continue;
                if (++c.SchoolYears < d.PrimaryYears) continue;
                c.Education = 1;
                sim.Emit(new CitizenLiterate(t, c.Id, c.Name, LiterateHow.School));
            }
        }

        /// <summary>The district's teachers: one for every <c>seatsPerTeacher</c> pupils, at least one once open.</summary>
        public static int Teachers(Simulation sim)
        {
            if (!IsOpen(sim)) return 0;
            int n = Pupils(sim).Count, seats = Def(sim).SeatsPerTeacher;
            return System.Math.Max(1, (n + seats - 1) / seats);
        }

        /// <summary>
        /// The upkeep for the new season, at the close (spec 018 §3.1): a gift to the village. Short of cash it goes unpaid this season
        /// and the district carries the school (the teacher is the district's), so the school never closes.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            if (!IsOpen(sim)) return;
            CountYears(sim);
            long cost = Upkeep(sim), t = sim.State.Clock.SimTime;
            if (cost == 0) return;
            if (!sim.State.Ledger.CanAfford(cost))
            {
                State(sim).UpkeepUnpaidPeriod = sim.Period;
                sim.Emit(new ObligationUnpaid(t, "School", "school", cost));
                return;
            }
            sim.State.Ledger.Post(t, sim.Period, "ledger.schoolUpkeep", "school", new Posting(Account.GiftsToVillage, cost), new Posting(Account.Cash, -cost));
            sim.Emit(new ObligationPaid(t, "School", "school", cost));
        }

        /// <summary>The school is finished: the district posts its teacher and the children start (spec 018 §3.1).</summary>
        internal sealed class Build : IJobHandler
        {
            public string JobType { get { return Job; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                SchoolState s = State(sim);
                s.Status = SchoolStatus.Open;
                s.JobId = 0;
                sim.CompleteJob(job);
                sim.Emit(new SchoolOpened(sim.State.Clock.SimTime));
                Progress.EraRules.OnTown(sim); // the Village's gate (spec 018 §3.6)
            }
        }

        /// <summary>The adult class ends (ADR 0095 §4): everyone still in town and in it can read.</summary>
        internal sealed class ClassEnds : IJobHandler
        {
            public string JobType { get { return ClassJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                SchoolState s = State(sim);
                long t = sim.State.Clock.SimTime;
                foreach (long id in s.Class)
                {
                    Citizen c = sim.State.Citizens.Citizen(id);
                    if (c == null || !c.Living || c.Literate) continue; // left, died or married away: they left the class
                    c.Education = 2;
                    sim.Emit(new CitizenLiterate(t, c.Id, c.Name, LiterateHow.AdultClass));
                }
                s.Class.Clear();
                s.ClassEndsAt = 0;
                s.ClassJobId = 0;
                sim.CompleteJob(job);
            }
        }
    }
}
