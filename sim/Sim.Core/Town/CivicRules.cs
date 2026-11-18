using System;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;

namespace Sim.Core.Town
{
    /// <summary>
    /// The three civic buildings (spec 023 §3.7, US-148 part 2): the watchman's post, the community hall and the playground, commissioned by the
    /// treasury on fixed public plots. Each works by the one coverage rule: <c>e = condition × covered × scale</c>, with the Safety department's
    /// condition, and only with its post filled. A post is filled by an appointed villager of the old village, paid as a Safety line and as the
    /// old village's income; unpaid at <c>leaveCloses</c> closes running he leaves.
    /// </summary>
    public static class CivicRules
    {
        public const string ArrivalJob = "civic.post";

        /// <summary>The Safety department's place in the treasury's payment order (spec 021 §3.4).</summary>
        private const int SafetyDept = 4;

        public static System.Collections.Generic.IReadOnlyList<CivicDef> Defs(Simulation sim) { return sim.Config.Game?.Township?.Civic; }

        public static CivicDef Def(Simulation sim, int i) { return sim.Config.Game.Township.Civic[i]; }

        public static bool On(Simulation sim) { return Defs(sim) != null && Defs(sim).Count == CivicDef.Count && sim.State.Township != null && sim.State.Citizens != null; }

        public static CivicPost Post(Simulation sim, int i) { return sim.State.Township.Civic[i]; }

        public static bool Built(Simulation sim, int i) { return On(sim) && Post(sim, i).Built; }

        /// <summary>Built, and its post filled if it has one (the playground has none).</summary>
        public static bool Staffed(Simulation sim, int i)
        {
            return Built(sim, i) && (!Def(sim, i).HasPost || Post(sim, i).Status == PostStatus.InPost);
        }

        /// <summary>The building commissioned as this work, or -1.</summary>
        public static int OfWork(Simulation sim, string workId)
        {
            if (!On(sim)) return -1;
            for (int i = 0; i < CivicDef.Count; i++) if (Def(sim, i).Work == workId) return i;
            return -1;
        }

        public static long Cost(Simulation sim, int i)
        {
            foreach (TownWorkDef w in TreasuryRules.Def(sim).Works) if (w.Id == Def(sim, i).Work) return Market.MarketRules.Indexed(sim, w.Cost);
            return 0;
        }

        public static long Upkeep(Simulation sim, int i) { return TreasuryRules.LineAmount(sim, TreasuryRules.Def(sim).Lines[Def(sim, i).UpkeepLine]); }

        public static long Wage(Simulation sim, int i)
        {
            return Def(sim, i).HasPost ? TreasuryRules.LineAmount(sim, TreasuryRules.Def(sim).Lines[Def(sim, i).WageLine]) : 0;
        }

        // ---- reach, capacity and the effect (spec 023 §3.6, §3.7) ----

        /// <summary>The Safety department's condition: the service level of everything it pays for.</summary>
        public static Fixed Condition(Simulation sim)
        {
            return TreasuryRules.IsOpen(sim) ? TreasuryRules.State(sim).Condition[SafetyDept] : Fixed.One;
        }

        /// <summary>Children in town: yours, and the old village's.</summary>
        public static int Children(Simulation sim)
        {
            int n = 0;
            foreach (Citizen c in sim.State.Citizens.Citizens) if (c.Living && HouseholdRules.IsChild(sim, c)) n++;
            return n + (int)(Fixed.FromInt(CohortRules.People(sim)) * CohortRules.Def(sim).ChildShare).FloorToLong();
        }

        /// <summary>People a building serves: the playground its children, the others everybody.</summary>
        public static int Served(Simulation sim, int i)
        {
            return i == CivicDef.Playground ? Children(sim) : ClinicRules.Served(sim);
        }

        /// <summary><c>min(1, capacity ÷ people served)</c> (spec 023 §3.6).</summary>
        public static Fixed Scale(Simulation sim, int i)
        {
            int served = Served(sim, i), capacity = Def(sim, i).Capacity;
            return served <= capacity ? Fixed.One : Fixed.FromRatio(capacity, served);
        }

        public static bool Covered(Simulation sim, int i, int homeTile) { return CoverageRules.Within(sim, homeTile, Def(sim, i).Tile, Def(sim, i).Reach); }

        /// <summary>A household's effect of building <paramref name="i"/>: <c>condition × covered × scale</c> when it is staffed, else 0.</summary>
        public static Fixed Effect(Simulation sim, int i, Household x)
        {
            if (!Staffed(sim, i) || x == null) return Fixed.Zero;
            Land.Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            if (b != null && !Covered(sim, i, b.TileId)) return Fixed.Zero;
            return Condition(sim) * Scale(sim, i);
        }

        /// <summary>The effect for the old village and the founder's side of town (the centre lies inside every reach).</summary>
        public static Fixed EffectTown(Simulation sim, int i) { return Staffed(sim, i) ? Condition(sim) * Scale(sim, i) : Fixed.Zero; }

        private static Fixed Cap(Fixed v) { return v > Fixed.One ? Fixed.One : v; }

        /// <summary>
        /// Whether the security layer has a fourth part (spec 023 §3.7): from the day a watchman's post stands. Before it, security is the mean of
        /// job, health care and rent as it always was, so a town that builds nothing is not made worse by the Township.
        /// </summary>
        public static bool SafetyCounts(Simulation sim) { return Built(sim, CivicDef.Watchman); }

        /// <summary>The <i>Safety</i> part: <c>safetyBase</c> (0.5) with a post built, rising to 1.0 with a watchman in reach.</summary>
        public static Fixed Safety(Simulation sim, Household x)
        {
            return Cap(sim.Config.Game.Township.SafetyBase + Def(sim, CivicDef.Watchman).Gain * Effect(sim, CivicDef.Watchman, x));
        }

        public static Fixed SafetyTown(Simulation sim)
        {
            return Cap(sim.Config.Game.Township.SafetyBase + Def(sim, CivicDef.Watchman).Gain * EffectTown(sim, CivicDef.Watchman));
        }

        /// <summary>The <i>Belonging</i> need met: the village's 0.8, and up to its gain more in reach of a staffed hall.</summary>
        public static Fixed Belonging(Simulation sim, Household x, Fixed baseline)
        {
            return !On(sim) ? baseline : Cap(baseline + Def(sim, CivicDef.Hall).Gain * Effect(sim, CivicDef.Hall, x));
        }

        public static Fixed BelongingTown(Simulation sim, Fixed baseline)
        {
            return !On(sim) ? baseline : Cap(baseline + Def(sim, CivicDef.Hall).Gain * EffectTown(sim, CivicDef.Hall));
        }

        /// <summary>A household's <i>Leisure</i> part: its own (0.30 with money for leisure), plus the playground's gain when it has children.</summary>
        public static Fixed Leisure(Simulation sim, Household x, Fixed own)
        {
            if (!On(sim) || !Built(sim, CivicDef.Playground)) return own;
            bool children = false;
            foreach (long id in x.Members) if (HouseholdRules.IsChild(sim, sim.State.Citizens.Citizen(id))) children = true;
            return children ? Cap(own + Def(sim, CivicDef.Playground).Gain * Effect(sim, CivicDef.Playground, x)) : own;
        }

        // ---- commissioning (spec 021 §3.7 and the plot) ----

        /// <summary>Why <paramref name="workId"/> can't be commissioned as a civic building now, or None: its plot is taken.</summary>
        public static Commands.RejectReason CanCommission(Simulation sim, string workId)
        {
            int i = OfWork(sim, workId);
            return i >= 0 && Post(sim, i).Built ? Commands.RejectReason.PlotTaken : Commands.RejectReason.None;
        }

        internal static void OnCommissioned(Simulation sim, string workId, long cost)
        {
            int i = OfWork(sim, workId);
            if (i < 0) return;
            CivicPost p = Post(sim, i);
            p.Built = true;
            p.BuiltAt = sim.State.Clock.SimTime;
            p.Cost = cost;
            sim.Emit(new CivicBuilt(p.BuiltAt, i));
        }

        /// <summary>
        /// The budget guard on the commission card (spec 023 §3.7): what the building adds a season to Safety (its upkeep and, with a post, the
        /// wage), and by how much the town would be short at today's income (what the close would take in) once it is staffed.
        /// </summary>
        public static void Guard(Simulation sim, string workId, out long added, out long shortBy)
        {
            int i = OfWork(sim, workId);
            added = i < 0 ? 0 : Upkeep(sim, i) + Wage(sim, i);
            long needs = 0;
            for (int d = 0; d < TreasuryState.Departments; d++) needs += TreasuryRules.Needs(sim, d);
            shortBy = Math.Max(0, needs + added - TreasuryRules.ExpectedIncome(sim));
        }

        // ---- appointments ----

        /// <summary>Why a post can't be advertised now, or None.</summary>
        public static Commands.RejectReason CanPost(Simulation sim, int i)
        {
            if (!On(sim) || i < 0 || i >= CivicDef.Count || !Def(sim, i).HasPost) return Commands.RejectReason.NoBuilding;
            CivicPost p = Post(sim, i);
            if (!p.Built) return Commands.RejectReason.NoBuilding;
            return p.Status != PostStatus.Vacant ? Commands.RejectReason.AlreadyFilled : Commands.RejectReason.None;
        }

        internal static Commands.RejectReason PostJob(Simulation sim, int i)
        {
            Commands.RejectReason r = CanPost(sim, i);
            if (r != Commands.RejectReason.None) return r;
            CivicPost p = Post(sim, i);
            long t = sim.State.Clock.SimTime;
            p.Status = PostStatus.Coming;
            p.ArrivesAt = t + Def(sim, i).StaffHours * 3600L;
            p.JobId = sim.ScheduleJob(ArrivalJob, i, p.ArrivesAt).Id;
            sim.Emit(new PostAdvertised(t, i, p.ArrivesAt));
            return Commands.RejectReason.None;
        }

        internal static Commands.RejectReason Dismiss(Simulation sim, int i)
        {
            if (!On(sim) || i < 0 || i >= CivicDef.Count) return Commands.RejectReason.NoBuilding;
            CivicPost p = Post(sim, i);
            if (p.Status != PostStatus.InPost) return Commands.RejectReason.NoStaff;
            Vacate(sim, i, false);
            return Commands.RejectReason.None;
        }

        private static void Vacate(Simulation sim, int i, bool unpaid)
        {
            CivicPost p = Post(sim, i);
            p.Status = PostStatus.Vacant;
            p.UnpaidCloses = 0;
            p.ArrivesAt = 0;
            p.LeftUnpaid = unpaid;
            p.LeftSeason = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime);
            sim.Emit(new PostVacated(sim.State.Clock.SimTime, i, p.StaffName, unpaid));
        }

        /// <summary>A villager takes the post (spec 023 §3.7): drawn from the old village, a man's name, kept on the sheet.</summary>
        internal sealed class Arrive : IJobHandler
        {
            public string JobType { get { return ArrivalJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Job job)
            {
                int i = (int)job.TargetId;
                CivicPost p = Post(sim, i);
                p.JobId = 0;
                sim.CompleteJob(job);
                if (p.Status != PostStatus.Coming) return;
                long t = sim.State.Clock.SimTime;
                p.Hired++;
                p.Status = PostStatus.InPost;
                p.ArrivesAt = 0;
                p.UnpaidCloses = 0;
                p.StaffName = PopulationRules.Name(sim, RngStream.Migration, 2_000_000 + i * 1000L + p.Hired, false);
                p.SinceSeason = CitizenRules.SeasonOf(sim, t);
                sim.Emit(new PostFilled(t, i, p.StaffName));
            }
        }

        // ---- the close ----

        /// <summary>
        /// The treasury's close has paid the lines (spec 023 §3.7): each wage paid goes to the old village as income; a wage not paid in full for
        /// <c>leaveCloses</c> closes running sends the villager home, and the post covers nobody until it is advertised again.
        /// </summary>
        internal static void NotePaid(Simulation sim, long[] amounts, long[] paid)
        {
            if (!On(sim)) return;
            TreasuryDef td = TreasuryRules.Def(sim);
            for (int i = 0; i < CivicDef.Count; i++)
            {
                CivicDef d = Def(sim, i);
                CivicPost p = Post(sim, i);
                if (!d.HasPost) continue;
                p.WageBefore = p.WageLast;
                p.WageLast = 0;
                if (p.Status != PostStatus.InPost) continue;
                long got = paid[d.WageLine];
                p.WageLast = got;
                if (got > 0) CohortRules.Earn(sim, got);
                if (got >= amounts[d.WageLine]) { p.UnpaidCloses = 0; continue; }
                p.UnpaidCloses++;
                if (p.UnpaidCloses >= d.LeaveCloses) Vacate(sim, i, true);
                else sim.Emit(new PostUnpaid(sim.State.Clock.SimTime, i, p.UnpaidCloses, d.LeaveCloses));
            }
        }
    }
}
