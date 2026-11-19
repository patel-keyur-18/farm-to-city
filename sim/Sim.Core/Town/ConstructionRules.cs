using System;
using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;

namespace Sim.Core.Town
{
    /// <summary>What starting a building costs and how long it takes, today (spec 025 §3.2): the card's rows.</summary>
    public sealed class BuildQuote
    {
        public BuildKindDef Kind { get; internal set; }
        public int Units { get; internal set; }
        public long Bricks { get; internal set; }
        public long Cement { get; internal set; }
        public long Timber { get; internal set; }

        /// <summary>The materials at today's trader's prices (1/100 coin).</summary>
        public long Materials { get; internal set; }

        /// <summary>Of the bricks, those your kiln gives at what they cost it (your own build only), and what that is (1/100 coin; part of <see cref="Materials"/>).</summary>
        public long KilnBricks { get; internal set; }
        public long KilnCost { get; internal set; }
        public int MasonDays { get; internal set; }

        /// <summary>The masons' part: at 1,000 a mason-day, or the district's gang at one and a half times.</summary>
        public long MasonPay { get; internal set; }
        public List<long> Masons { get; internal set; } = new List<long>();
        public bool Gang { get { return Masons.Count == 0; } }
        public int Hours { get; internal set; }
        public long Total { get { return Materials + MasonPay; } }
    }

    /// <summary>
    /// Construction from the Township (spec 025 §3.2, US-145). A new building is paid for in full when it starts (its materials at the trader's
    /// price, its masons by the mason-day); it then draws bricks, cement and timber from the trader's day (a limit for the whole town, yours and the
    /// treasury's first, the builders' after), and waits when short; with its materials in, its masons build it in its hours ÷ masons.
    /// </summary>
    public static class ConstructionRules
    {
        public const string Job = "build.done";
        private const ulong MasonKind = 9;

        public static ConstructionDef Def(Simulation sim) { return sim.Config.Game?.Township?.Construction; }

        /// <summary>From the Township a new building needs materials and masons; before it the Phase 2 rule (time only) stands.</summary>
        public static bool On(Simulation sim)
        {
            return Def(sim) != null && sim.State.Township != null && TownshipRules.Reached(sim);
        }

        public static List<BuildProject> Projects(Simulation sim) { return sim.State.Township.Projects; }

        public static BuildProject OnTile(Simulation sim, int tile)
        {
            TownshipState s = sim.State.Township;
            if (s == null) return null;
            foreach (BuildProject p in s.Projects) if (p.Tile == tile && p.Owner != BuildProject.Builders) return p;
            return null;
        }

        /// <summary>A civic work still being built: its plot is taken.</summary>
        public static bool Pending(Simulation sim, string workId)
        {
            TownshipState s = sim.State.Township;
            if (s == null) return false;
            foreach (BuildProject p in s.Projects) if (p.Owner == BuildProject.Town && Def(sim).Kind(p.Kind).Work == workId) return true;
            return false;
        }

        /// <summary>The civic work being built, or null.</summary>
        public static BuildProject OfWork(Simulation sim, string workId)
        {
            TownshipState s = sim.State.Township;
            if (s == null) return null;
            foreach (BuildProject p in s.Projects) if (p.Owner == BuildProject.Town && Def(sim).Kind(p.Kind).Work == workId) return p;
            return null;
        }

        /// <summary>Builds of yours or the treasury's waiting for a good, and the builders' (the sheet's queue).</summary>
        public static int BuildersProjects(Simulation sim)
        {
            int n = 0;
            foreach (BuildProject p in sim.State.Township.Projects) if (p.Owner == BuildProject.Builders) n++;
            return n;
        }

        // ---- masons ----

        /// <summary>Grown-ups of your households with no town job who have the mason's trade (a fixed draw for each) and are not kept for a build.</summary>
        public static List<long> FreeMasons(Simulation sim)
        {
            var free = new List<long>();
            CitizensState cs = sim.State.Citizens;
            ConstructionDef d = Def(sim);
            if (cs == null || d == null) return free;
            foreach (Household x in cs.Households)
                foreach (long id in x.Members)
                {
                    Citizen c = cs.Citizen(id);
                    if (HouseholdRules.IsChild(sim, c) || HouseholdRules.IsElder(sim, c)) continue;
                    if (c.Job == CitizenJob.Hand || c.Job == CitizenJob.Business) continue;
                    if (Kept(sim, id) || !IsMason(sim, id)) continue;
                    free.Add(id);
                }
            return free;
        }

        /// <summary>The mason's trade: a fixed draw for the person, so the same people are masons in every game of a seed.</summary>
        public static bool IsMason(Simulation sim, long citizenId)
        {
            return (int)Rng.Below(PopulationRules.Roll(sim, RngStream.Citizens, citizenId, MasonKind, 0), 100) < Def(sim).MasonShare;
        }

        public static bool Kept(Simulation sim, long citizenId)
        {
            foreach (BuildProject p in sim.State.Township.Projects) if (p.Masons.Contains(citizenId)) return true;
            return false;
        }

        /// <summary>Every mason in town, free or kept (the hiring board's list).</summary>
        public static List<long> AllMasons(Simulation sim)
        {
            var all = new List<long>();
            CitizensState cs = sim.State.Citizens;
            if (cs == null || Def(sim) == null) return all;
            foreach (Household x in cs.Households)
                foreach (long id in x.Members)
                {
                    Citizen c = cs.Citizen(id);
                    if (HouseholdRules.IsChild(sim, c) || HouseholdRules.IsElder(sim, c)) continue;
                    if (c.Job == CitizenJob.Hand || c.Job == CitizenJob.Business) continue;
                    if (IsMason(sim, id)) all.Add(id);
                }
            return all;
        }

        // ---- the quote ----

        /// <summary>The price and time of starting <paramref name="kindId"/> now. Builders pay nothing to anyone's ledger and build in the gang's hours.</summary>
        public static BuildQuote Quote(Simulation sim, string kindId, int owner, int units)
        {
            ConstructionDef d = Def(sim);
            BuildKindDef k = d.Kind(kindId);
            var q = new BuildQuote { Kind = k, Units = units, Bricks = k.Bricks * units, Cement = k.Cement * units, Timber = k.Timber * units, MasonDays = k.MasonDays * units };
            if (owner == BuildProject.Yours && sim.State.Businesses != null)
            {
                q.KilnBricks = Math.Min(q.Bricks, KilnRules.Yards(sim)); // your own kiln's bricks first, at what they cost it (US-243)
                q.KilnCost = KilnRules.CostOf(sim, q.KilnBricks);
            }
            q.Materials = MarketRules.Indexed(sim, (q.Bricks - q.KilnBricks) * d.BrickPrice + q.Cement * d.CementPrice + q.Timber * d.TimberPrice) + q.KilnCost;
            if (owner == BuildProject.Builders)
            {
                q.Hours = (int)(Fixed.FromInt(k.Hours) * d.GangHours).RoundToLong();
                return q;
            }
            List<long> free = FreeMasons(sim);
            int n = Math.Min(d.MaxMasons, free.Count);
            for (int i = 0; i < n; i++) q.Masons.Add(free[i]);
            long basePay = MarketRules.Indexed(sim, d.MasonDay * q.MasonDays);
            q.MasonPay = n == 0 ? Money.MulRate(basePay, d.GangPrice) : basePay;
            q.Hours = n == 0 ? (int)(Fixed.FromInt(k.Hours) * d.GangHours).RoundToLong() : (k.Hours + n - 1) / n;
            return q;
        }

        // ---- the trader's day ----

        private static long DayOf(Simulation sim)
        {
            long t = sim.State.Clock.SimTime - Def(sim).RestockHour * 3600L;
            return t >= 0 ? t / 86400 : -((-t + 86399) / 86400);
        }

        private static void Rollover(Simulation sim)
        {
            TownshipState s = sim.State.Township;
            long day = DayOf(sim);
            if (s.StockDay == day) return;
            s.StockDay = day;
            s.TakenBricks = s.TakenCement = s.TakenTimber = 0;
        }

        /// <summary>The trader's day (from his hour): the key his stock and your kiln's sales to him are kept by.</summary>
        public static long TraderDay(Simulation sim) { return DayOf(sim); }

        /// <summary>Bricks your kiln sold the trader this morning: his stock for the whole town is that much more today.</summary>
        public static long KilnToday(Simulation sim)
        {
            TownshipState s = sim.State.Township;
            return s.KilnDay == DayOf(sim) ? s.KilnBricks : 0;
        }

        internal static void KilnDelivered(Simulation sim, long bricks)
        {
            TownshipState s = sim.State.Township;
            s.KilnBricks = bricks;
            s.KilnDay = DayOf(sim);
        }

        /// <summary>What the trader still sells today of a good (0 bricks, 1 cement, 2 timber).</summary>
        public static long Left(Simulation sim, int good)
        {
            Rollover(sim);
            ConstructionDef d = Def(sim);
            TownshipState s = sim.State.Township;
            return good == 0 ? d.DailyBricks + KilnToday(sim) - s.TakenBricks : good == 1 ? d.DailyCement - s.TakenCement : d.DailyTimber - s.TakenTimber;
        }

        public static long Limit(Simulation sim, int good)
        {
            ConstructionDef d = Def(sim);
            return good == 0 ? d.DailyBricks + KilnToday(sim) : good == 1 ? d.DailyCement : d.DailyTimber;
        }

        public static long Need(Simulation sim, BuildProject p, int good)
        {
            BuildKindDef k = Def(sim).Kind(p.Kind);
            return (good == 0 ? k.Bricks : good == 1 ? k.Cement : k.Timber) * p.Units;
        }

        public static long Got(BuildProject p, int good) { return good == 0 ? p.Bricks : good == 1 ? p.Cement : p.Timber; }

        /// <summary>Seconds until the trader's stock fills again.</summary>
        public static long SecondsToRestock(Simulation sim)
        {
            long t = sim.State.Clock.SimTime - Def(sim).RestockHour * 3600L;
            long into = t >= 0 ? t % 86400 : 86400 - ((-t) % 86400);
            return 86400 - into;
        }

        /// <summary>The projects in the order they draw: yours and the treasury's by start, then the builders'.</summary>
        public static List<BuildProject> Queue(Simulation sim)
        {
            var q = new List<BuildProject>();
            foreach (BuildProject p in sim.State.Township.Projects) if (!p.Working && p.Owner != BuildProject.Builders) q.Add(p);
            foreach (BuildProject p in sim.State.Township.Projects) if (!p.Working && p.Owner == BuildProject.Builders) q.Add(p);
            return q;
        }

        // ---- starting and drawing ----

        internal static BuildProject Start(Simulation sim, string kindId, int owner, int tile, int units, int parcel, BuildQuote q, long paid, long house)
        {
            TownshipState s = sim.State.Township;
            var p = new BuildProject
            {
                Id = ++s.NextProjectId, Kind = kindId, Owner = owner, Tile = tile, Parcel = parcel, Units = units, House = house, Paid = paid, MasonPay = q.Gang ? 0 : q.MasonPay,
                Masons = new List<long>(q.Masons), Hours = q.Hours,
            };
            s.Projects.Add(p);
            if (q.KilnBricks > 0) // the bricks come from your yard now, and your kiln is paid what they cost it
            {
                KilnRules.TakeForBuild(sim, q.KilnBricks);
                p.Bricks += q.KilnBricks;
            }
            sim.Emit(new BuildStarted(sim.State.Clock.SimTime, p.Id, kindId, owner, tile));
            Draw(sim);
            return p;
        }

        /// <summary>Each waiting build takes what it can of today's stock; one with all its materials begins.</summary>
        internal static void Draw(Simulation sim)
        {
            Rollover(sim);
            TownshipState s = sim.State.Township;
            foreach (BuildProject p in Queue(sim))
            {
                for (int g = 0; g < 3; g++)
                {
                    long take = Math.Min(Need(sim, p, g) - Got(p, g), Left(sim, g));
                    if (take <= 0) continue;
                    if (g == 0) { p.Bricks += take; s.TakenBricks += take; }
                    else if (g == 1) { p.Cement += take; s.TakenCement += take; }
                    else { p.Timber += take; s.TakenTimber += take; }
                }
                int missing = -1;
                for (int g = 2; g >= 0; g--) if (Got(p, g) < Need(sim, p, g)) missing = g;
                if (missing < 0) { Begin(sim, p); continue; }
                if (!p.Alerted && p.Owner != BuildProject.Builders)
                {
                    p.Alerted = true;
                    sim.Emit(new BuildHeld(sim.State.Clock.SimTime, p.Id, p.Kind, p.Owner, p.Tile, missing));
                }
            }
        }

        private static void Begin(Simulation sim, BuildProject p)
        {
            long t = sim.State.Clock.SimTime;
            p.Working = true;
            p.StartedAt = t;
            p.ReadyAt = t + p.Hours * 3600L;
            p.JobId = sim.ScheduleJob(Job, p.Id, p.ReadyAt).Id;
            if (p.House != 0)
            {
                Construction c = sim.State.Citizens.Constructions.Find(x => x.Id == p.House);
                if (c != null) { c.ReadyAt = p.ReadyAt; c.JobId = p.JobId; }
            }
        }

        /// <summary>The hourly pass: the trader's stock fills again at its hour and the builds that wait draw from it.</summary>
        internal static void Hourly(Simulation sim)
        {
            if (!On(sim)) return;
            Draw(sim);
        }

        /// <summary>When a waiting build will be ready if nothing else changes, from now (seconds): the days its materials take from the trader's limit, then its hours.</summary>
        public static long EstimateReady(Simulation sim, BuildProject p)
        {
            long t = sim.State.Clock.SimTime;
            if (p.Working) return Math.Max(0, p.ReadyAt - t);
            long start = 0;
            for (int g = 0; g < 3; g++)
            {
                long remaining = Need(sim, p, g) - Got(p, g);
                if (remaining <= 0) continue;
                long ahead = 0;
                foreach (BuildProject o in Queue(sim))
                {
                    if (o == p) break;
                    ahead += Need(sim, o, g) - Got(o, g);
                }
                long days = (remaining + ahead + Limit(sim, g) - 1) / Limit(sim, g);
                if (days < 1) days = 1;
                start = Math.Max(start, SecondsToRestock(sim) + (days - 1) * 86400L);
            }
            return start + p.Hours * 3600L;
        }

        /// <summary>The good a waiting build still lacks first (0 bricks, 1 cement, 2 timber), or −1.</summary>
        public static int Missing(Simulation sim, BuildProject p)
        {
            for (int g = 0; g < 3; g++) if (Got(p, g) < Need(sim, p, g)) return g;
            return -1;
        }

        // ---- finishing ----

        /// <summary>A house being built is taken with its acre: the project goes, its masons are free (their pay was never paid out).</summary>
        internal static void Cancel(Simulation sim, long houseConstructionId)
        {
            TownshipState s = sim.State.Township;
            if (s == null) return;
            BuildProject p = s.Projects.Find(x => x.House == houseConstructionId && x.Owner == BuildProject.Yours);
            if (p == null) return;
            if (p.Working) sim.CompleteJob(sim.State.Jobs.Find(p.JobId));
            s.Projects.Remove(p);
        }

        internal static void Finish(Simulation sim, BuildProject p)
        {
            TownshipState s = sim.State.Township;
            long t = sim.State.Clock.SimTime;
            if (!p.Gang && p.MasonPay > 0)
            {
                int n = p.Masons.Count;
                long share = p.MasonPay / n, rest = p.MasonPay - share * n;
                for (int i = 0; i < n; i++)
                {
                    Citizen c = sim.State.Citizens.Citizen(p.Masons[i]);
                    Household x = c == null ? null : sim.State.Citizens.Household(c.HouseholdId);
                    if (x != null) x.Earned += share + (i == 0 ? rest : 0);
                }
            }
            s.Projects.Remove(p);
            if (p.Owner == BuildProject.Yours)
            {
                CitizensState cs = sim.State.Citizens;
                Construction c = cs.Constructions.Find(x => x.Id == p.House);
                if (c != null)
                {
                    LandState land = sim.State.Land;
                    var b = new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = c.TileId, Cost = c.Cost };
                    land.Buildings.Add(b);
                    cs.Constructions.Remove(c);
                    sim.Emit(new FamilyHouseBuilt(t, b.Id, c.TileId));
                }
            }
            else if (p.Owner == BuildProject.Town)
            {
                string work = Def(sim).Kind(p.Kind).Work;
                if (UtilityRules.OfWork(sim, work) >= 0) UtilityRules.OnBuilt(sim, work);
                else CivicRules.OnCommissioned(sim, work, p.Paid);
            }
            else ZoningRules.FinishHomes(sim, p);
            sim.Emit(new BuildFinished(t, p.Id, p.Kind, p.Owner, p.Tile));
        }

        internal sealed class Done : IJobHandler
        {
            public string JobType { get { return Job; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                BuildProject p = sim.State.Township.Projects.Find(x => x.Id == job.TargetId);
                sim.CompleteJob(job);
                if (p != null) Finish(sim, p);
            }
        }
    }
}
