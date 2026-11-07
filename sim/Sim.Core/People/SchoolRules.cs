using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Scheduling;

namespace Sim.Core.People
{
    /// <summary>
    /// The village's first school (spec 018 §3.1, ADR 0077, 0095): the founder builds it on the school plot and gives it to the district,
    /// which posts and pays the teacher; its cost, its upkeep and adult classes are gifts to the village, outside farm profit.
    /// </summary>
    public static class SchoolRules
    {
        public const string Job = "school.build";

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
            {
                if (!c.Living || c.Education > 0) continue;
                int age = CitizenRules.Age(sim, c);
                if (age >= d.PupilAgeMin && age <= d.PupilAgeMax) list.Add(c);
            }
            return list;
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
            }
        }
    }
}
