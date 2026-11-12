using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    /// <summary>One milestone reached: which, and the season it happened in.</summary>
    public sealed class MilestoneEntry
    {
        public string Id { get; }
        public long Period { get; }

        public MilestoneEntry(string id, long period) { Id = id; Period = period; }
    }

    /// <summary>The wealth milestones reached (spec 019 §3.6, ADR 0101 §7) and the inheritance's value, found once and kept.</summary>
    public sealed class MilestonesState
    {
        public List<MilestoneEntry> Reached { get; } = new List<MilestoneEntry>();

        /// <summary>The net worth of a fresh game from this town's seed and content, at the start's prices; 0 until the first close.</summary>
        public long InheritedWorth { get; internal set; }

        internal bool IsDefault { get { return Reached.Count == 0 && InheritedWorth == 0; } }

        public MilestoneEntry Find(string id) { return Reached.Find(m => m.Id == id); }

        internal MilestonesState Clone()
        {
            var c = new MilestonesState { InheritedWorth = InheritedWorth };
            c.Reached.AddRange(Reached); // entries are immutable
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(InheritedWorth);
            h.WriteInt(Reached.Count);
            foreach (MilestoneEntry m in Reached) { h.WriteString(m.Id); h.WriteLong(m.Period); }
        }

        internal static MilestonesState ReadFrom(SaveReader r)
        {
            var s = new MilestonesState { InheritedWorth = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Reached.Add(new MilestoneEntry(r.ReadString(), r.ReadLong()));
            return s;
        }
    }

    /// <summary>
    /// The five wealth milestones, each once (spec 019 §3.6, ADR 0101 §7). Checked at each close from the report just built, and from the
    /// reports kept before (an older town's first close after the update finds what they show, with their real seasons). Never a reward.
    /// </summary>
    public static class MilestoneRules
    {
        public const string FirstLakh = "firstLakh", Twice = "twiceInherited", FirstRent = "firstRent", DebtFree = "debtFree", Passive = "passiveCoversLiving";

        /// <summary>In the order the sheet lists them.</summary>
        public static readonly string[] Ids = { FirstLakh, Twice, FirstRent, DebtFree, Passive };

        public static MilestonesState State(Simulation sim) { return sim.State.Finance?.Milestones; }

        public static bool Enabled(Simulation sim) { return sim.Config.Game?.Ledger.Milestones != null && sim.State.Finance != null; }

        /// <summary>What the inheritance was worth, at the start's prices: the stored value, or a fresh game's net worth before the first close has stored it.</summary>
        public static long InheritedAtStart(Simulation sim)
        {
            long stored = State(sim).InheritedWorth;
            return stored > 0 ? stored : FreshWorth(sim);
        }

        /// <summary>The inheritance in the coins of the close of <paramref name="period"/> (prices rise, so twice the inheritance moves with them).</summary>
        public static long Inherited(Simulation sim, long period)
        {
            Fixed start = Hub.IndexAtClose(sim, 0);
            return Hub.InTodaysCoins(InheritedAtStart(sim), start, Hub.IndexAtClose(sim, period));
        }

        /// <summary>The inheritance in today's coins.</summary>
        public static long InheritedNow(Simulation sim)
        {
            return Hub.InTodaysCoins(InheritedAtStart(sim), Hub.IndexAtClose(sim, 0), sim.State.Market.PriceIndex);
        }

        /// <summary>Net worth of a new game of this town's seed and content, the moment it starts (computed once; ADR 0101 §7).</summary>
        private static long FreshWorth(Simulation sim)
        {
            Simulation fresh = Simulation.NewGame(sim.Config, new NewGame(sim.State.Seed, 0, 0));
            return System.Math.Max(1, Reporting.NetWorth(fresh).Total);
        }

        /// <summary>Cash and savings less what you owe at a close: borrowed cash isn't savings.</summary>
        public static long Saved(NetWorthSnapshot n) { return n.Cash + n.Deposit - Hub.Owed(n); }

        /// <summary>No open loan of your own, and the last you took was repaid (a foreclosure's write-off doesn't count; businesses' loans are on their books).</summary>
        public static bool DebtFreeNow(Simulation sim)
        {
            if (sim.State.Credit == null) return false;
            Loan last = null;
            foreach (Loan l in sim.State.Credit.Loans)
            {
                if (l.BusinessId != 0) continue;
                if (l.IsOpen) return false;
                if (last == null || l.Id > last.Id) last = l;
            }
            return last != null && last.State == LoanState.Repaid;
        }

        /// <summary>At the close, after the statements and the settlement.</summary>
        internal static void Check(Simulation sim, long closing)
        {
            if (!Enabled(sim)) return;
            MilestonesDef def = sim.Config.Game.Ledger.Milestones;
            MilestonesState ms = State(sim);
            bool firstClose = ms.InheritedWorth == 0;
            if (firstClose) ms.InheritedWorth = FreshWorth(sim);
            List<SeasonReport> reports = sim.State.Finance.Reports;
            bool debtFreeNow = DebtFreeNow(sim);
            bool firstScan = firstClose && reports.Count > 1; // an older town: the loans' history before its kept reports is unknown, so a debt-free town waits for its next close
            for (int i = 0; i < reports.Count; i++)
            {
                SeasonReport r = reports[i];
                if (ms.Reached.Count == Ids.Length) break;
                NetWorthSnapshot n = r.NetWorth;
                if (Saved(n) >= def.Savings) Mark(sim, ms, FirstLakh, r.Period, closing);
                if (n.Total >= def.NetWorthTimes * Inherited(sim, r.Period)) Mark(sim, ms, Twice, r.Period, closing);
                if (HasRent(r)) Mark(sim, ms, FirstRent, r.Period, closing);
                bool newest = i == reports.Count - 1;
                if (debtFreeNow && ((newest && !firstScan) || RepaidIn(reports, i))) Mark(sim, ms, DebtFree, r.Period, closing);
            }
            if (Hub.PassivePercent(sim) >= def.PassivePercent) Mark(sim, ms, Passive, closing, closing);
        }

        private static bool HasRent(SeasonReport r)
        {
            foreach (KeyValuePair<Account, long> kv in r.ByAccount)
                if (kv.Key == Account.RentReceived && kv.Value > 0) return true;
            return false;
        }

        /// <summary>From the kept reports alone: you owed at the one before and owe nothing at this (the loan was repaid, or an early repayment cleared it).</summary>
        private static bool RepaidIn(List<SeasonReport> reports, int i)
        {
            if (i == 0) return false;
            CreditSummary before = reports[i - 1].Credit, now = reports[i].Credit;
            return before != null && now != null && before.PrincipalOutstanding > 0 && now.PrincipalOutstanding == 0;
        }

        private static void Mark(Simulation sim, MilestonesState ms, string id, long period, long closing)
        {
            if (ms.Find(id) != null) return;
            ms.Reached.Add(new MilestoneEntry(id, period));
            sim.Emit(new MilestoneReached(sim.State.Clock.SimTime, id, period, period < closing));
        }
    }
}
