using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>An advisor's line: their portrait, who they are, and what they say.</summary>
    public sealed class AdvisorVm
    {
        public string Portrait { get; internal set; }
        public string Who { get; internal set; }
        public string Text { get; internal set; }

        /// <summary>Under an alert: one line, "Name: “…”", a small portrait. Otherwise the season report's panel under its title.</summary>
        public bool Compact { get; internal set; }
    }

    /// <summary>
    /// When the advisors speak (spec 018 §3.4, ADR 0095 §8): after each batch of events, the alert notes whose event came (a missed
    /// payment, wages missed, pests, feed low, a raise asked) and, at a season close, the season report's one line: the first note in
    /// priority order whose condition holds. A topic speaks at most once in <c>topicHours</c>; nothing during the first session. The host
    /// records them with one <see cref="HearAdvisors"/>. They warn and explain; they never act.
    /// </summary>
    public static class AdvisorWatch
    {
        public static List<AdvisorNote> Observe(ViewContext ctx, IReadOnlyList<SimEvent> events)
        {
            Simulation sim = ctx.Sim;
            var said = new List<AdvisorNote>();
            AdvisorDefs d = sim.Config.Game?.Advisors;
            if (d == null || sim.State.Guidance == null || Guide.FirstSessionActive(sim)) return said;
            long now = sim.State.Clock.SimTime;
            bool Quiet(string id) // the topic spoke within the last game day, or in this pass
            {
                AdvisorNote last = sim.State.Guidance.Said(id);
                return said.Exists(x => x.Id == id) || (last != null && now - last.At < d.TopicHours * 3600L);
            }

            SeasonClosed close = null;
            foreach (SimEvent e in events) if (e is SeasonClosed c) close = c;
            foreach (AdvisorNoteDef n in d.Notes) // in priority order: the first event of each topic
                foreach (SimEvent e in events)
                {
                    long key = System.Linq.Enumerable.Contains(n.Alerts, e.GetType().Name) && !Quiet(n.Id) ? AlertKey(sim, e) : -1;
                    if (key >= 0) said.Add(new AdvisorNote(n.Id, key, false, new long[0]));
                }
            if (close != null)
                foreach (AdvisorNoteDef n in d.Notes)
                {
                    if (!n.Report || d.IsPlanner(n) || Quiet(n.Id)) continue;
                    long[] numbers = Holds(ctx, n, close, events);
                    if (numbers == null) continue;
                    said.Add(new AdvisorNote(n.Id, close.Period, true, numbers));
                    break; // one line on the report: the first that holds
                }
            if (close != null && TreasuryRules.IsOpen(sim)) // the town planner's one line for the treasury sheet (spec 021 §3.12)
                foreach (AdvisorNoteDef n in d.Notes)
                {
                    if (!n.Report || !d.IsPlanner(n) || Quiet(n.Id)) continue;
                    long[] numbers = PlannerHolds(sim, n);
                    if (numbers == null) continue;
                    said.Add(new AdvisorNote(n.Id, close.Period, true, numbers));
                    break;
                }
            return said;
        }

        public static Command Command(long at, List<AdvisorNote> said) { return said.Count == 0 ? null : new HearAdvisors(at, said); }

        /// <summary>What an alert note is about, as the alert list keys it (<see cref="Alerts"/>); −1 when the event isn't the note's.</summary>
        private static long AlertKey(Simulation sim, SimEvent e)
        {
            switch (e)
            {
                case LoanPaymentMissed _: case FinalNotice _: return 0;
                case WagesMissed w: return w.BusinessId;
                case PestWarning p: return p.TileId;
                case FeedLow f: return sim.Config.Game.Market.IndexOf(f.Good);
                case HrEventRaised h: return h.Kind == HrEventKind.RaiseRequest ? h.EventId : -1;
                default: return -1;
            }
        }

        /// <summary>The note's numbers if its season-report condition holds at <paramref name="close"/> (ADR 0095 §8's table), else null.</summary>
        private static long[] Holds(ViewContext ctx, AdvisorNoteDef n, SeasonClosed close, IReadOnlyList<SimEvent> events)
        {
            Simulation sim = ctx.Sim;
            SeasonReport r = close.Report;
            switch (n.Id)
            {
                case "debt": // loans over the hub's risky band of what you own (finance.json)
                {
                    long debt = r.NetWorth.LoanPrincipal + r.NetWorth.InterestPayable, assets = r.NetWorth.Total + debt;
                    return debt > 0 && debt > sim.Config.Game.Ledger.DebtRiskyOver.MulRatio(assets, 1).RoundToLong() ? new[] { debt, assets } : null;
                }
                case "overdraft": // a business's overdraft near its limit
                    if (sim.State.Businesses != null)
                        foreach (Business b in sim.State.Businesses.Businesses)
                        {
                            long used = BusinessBooks.Overdrawn(b), limit = BusinessBooks.OverdraftLimit(sim, b);
                            if (b.Status != BusinessStatus.Closed && limit > 0 && used > n.Threshold.MulRatio(limit, 1).RoundToLong()) return new[] { b.Id, used, limit };
                        }
                    return null;
                case "profitNotCash": // a profit, and less cash at the end
                    if (r.NetResult <= 0 || r.CashFlow.Closing >= r.CashFlow.Opening) return null;
                    // the fall between the report's own rounded start and end, so it adds up on the sheet
                    return new[] { r.NetResult, Money.RoundToCoin(r.CashFlow.Opening) - Money.RoundToCoin(r.CashFlow.Closing), Biggest(ctx) };
                case "soil": // a field that gave more than it got this season, now low
                {
                    FieldSeasonClosed worst = null;
                    foreach (SimEvent e in events)
                        if (e is FieldSeasonClosed f && f.SimTime == close.SimTime && f.FertilityChange < Fixed.Zero && f.Fertility < n.Threshold
                            && (worst == null || f.Fertility < worst.Fertility)) worst = f;
                    return worst == null ? null : new[] { worst.TileId, worst.Fertility.Raw };
                }
                case "dry": // a rain-fed field dry under its crop, once the pump can be had
                {
                    IrrigationDef irrigation = IrrigationRules.Def(sim);
                    if (!IrrigationRules.On(sim) || !IrrigationRules.Unlocked(sim, irrigation.Pump.UnlockEra)) return null;
                    FieldSeasonClosed worst = null;
                    foreach (SimEvent e in events)
                        if (e is FieldSeasonClosed f && f.SimTime == close.SimTime && f.DryHours >= n.Threshold.RoundToLong() && RainFed(sim, f.TileId)
                            && (worst == null || f.DryHours > worst.DryHours)) worst = f;
                    return worst == null ? null : new[] { worst.TileId, worst.DryHours };
                }
                case "tired": // a worker worn out by long days
                {
                    Fixed above = sim.Config.Game.People.Hr.SickTiredAbove;
                    if (sim.State.People == null) return null;
                    foreach (Hand h in sim.State.People.Hands)
                        if (h.Contract == ContractState.Active && h.Fatigue > above) return new long[] { h.Name };
                    foreach (Worker w in sim.State.People.Workers)
                        if (w.Fatigue > above) return new long[] { w.Name };
                    return null;
                }
                case "children": // children of school age and no school yet
                {
                    if (!SchoolRules.On(sim) || !SchoolRules.Unlocked(sim) || SchoolRules.State(sim).Status != SchoolStatus.None) return null;
                    SchoolDef s = SchoolRules.Def(sim);
                    long count = 0;
                    foreach (Citizen c in sim.State.Citizens.Citizens)
                    {
                        int age = CitizenRules.Age(sim, c);
                        if (c.Living && !c.Literate && age >= s.PupilAgeMin && age <= s.PupilAgeMax) count++;
                    }
                    return count > 0 ? new[] { count } : null;
                }
                case "adultClass": // grown-ups who can't read, the school open, no class running
                {
                    if (!SchoolRules.IsOpen(sim) || SchoolRules.ClassRunning(sim)) return null;
                    int learners = SchoolRules.Learners(sim).Count;
                    return learners > 0 ? new[] { learners, SchoolRules.ClassCost(sim) } : null;
                }
                default: return null;
            }
        }

        /// <summary>
        /// The town planner's notes (spec 021 §3.12), read from the treasury's last closed season: a department over its budget two closes
        /// running, a service under the threshold, the tied grant at risk (the doctor unpaid or Health under its threshold), a balance under one
        /// season's spending with no tax or gift to make it up.
        /// </summary>
        private static long[] PlannerHolds(Simulation sim, AdvisorNoteDef n)
        {
            TreasuryState t = sim.State.Treasury;
            if (t == null || !t.Opened || t.Reports.Count == 0) return null;
            TreasuryReport last = t.Reports[t.Reports.Count - 1];
            const int Health = 1;
            switch (n.Id)
            {
                case "deptOver":
                    for (int d = 0; d < TreasuryState.Departments; d++)
                        if (t.OverBudget[d] >= 2) return new long[] { d, last.Rows[d].Needs, last.Rows[d].Budget };
                    return null;
                case "serviceLow":
                    for (int d = 0; d < TreasuryState.Departments; d++)
                        if (last.Rows[d].Needs > 0 && t.Condition[d] < n.Threshold) return new long[] { d, t.Condition[d].Raw };
                    return null;
                case "grantAtRisk":
                    return last.Rows[Health].Needs > 0 && (t.Unpaid[Health] > 0 || t.Condition[Health] < n.Threshold) ? new long[] { Health } : null;
                case "thinBalance":
                {
                    long due = TreasuryRules.Forecast(sim).Due;
                    bool made = last.Balance(Sim.Core.Finance.Account.HouseTaxIncome) != 0 || last.Balance(Sim.Core.Finance.Account.BusinessTaxIncome) != 0
                        || last.Balance(Sim.Core.Finance.Account.GiftsFromFounder) != 0;
                    return !made && due > 0 && t.Cash < due ? new long[] { t.Cash, due } : null;
                }
                default: return null;
            }
        }

        /// <summary>No pumped source, canal channel or the well's floor waters it.</summary>
        private static bool RainFed(Simulation sim, int tile)
        {
            foreach (int w in sim.Config.Game.Land.WellTiles) if (w == tile) return false;
            return IrrigationRules.SourceOf(sim, tile) == 0 && !IrrigationRules.Channelled(sim, tile);
        }

        /// <summary>The biggest use of cash beyond costs last season (its index in <see cref="SeasonBook.MovedOrder"/>), or −1.</summary>
        private static long Biggest(ViewContext ctx)
        {
            if (!SeasonBook.HasLast(ctx.Sim)) return -1;
            SeasonLineVm most = null;
            foreach (SeasonLineVm l in SeasonBook.Build(ctx, true).Moved)
                if (l.Amount < 0 && System.Array.IndexOf(AdvisorWords.Uses, l.Key) >= 0 && (most == null || l.Amount < most.Amount)) most = l;
            return most == null ? -1 : System.Array.IndexOf(SeasonBook.MovedOrder, most.Key);
        }
    }

    /// <summary>The advisors' words: who they are and each note's line with the player's numbers then.</summary>
    public static class AdvisorWords
    {
        /// <summary>The *why your cash moved* lines a profit can go into, each with its words (<c>advisor.went.&lt;key&gt;</c>).</summary>
        internal static readonly string[] Uses =
        {
            "paidAhead", "owed", "interestPaid", "repaid", "boughtBuildings", "boughtLivestock", "boughtTrees", "boughtLand", "boughtEquipment",
            "boughtInvestmentInBusinesses",
        };

        public static AdvisorVm Line(ViewContext ctx, AdvisorNote a, bool compact)
        {
            AdvisorDefs d = ctx.Sim.Config.Game.Advisors;
            int i = d.IndexOf(a.Id);
            if (i < 0) return null; // a note the content dropped
            var s = ctx.Strings;
            string who = d.Advisors[d.Notes[i].Advisor], name = s.Get("advisor." + who + ".name"), text = Text(ctx, a);
            return new AdvisorVm
            {
                Portrait = d.Portraits[d.Notes[i].Advisor], Compact = compact,
                Who = compact ? name : s.Get("advisor.who", "name", name, "role", s.Get("advisor." + who + ".role")),
                Text = compact ? s.Get("advisor.say", "name", name, "line", text) : s.Get("advisor.quote", "line", text),
            };
        }

        public static string Text(ViewContext ctx, AdvisorNote a)
        {
            var s = ctx.Strings;
            var f = ctx.Fmt;
            Simulation sim = ctx.Sim;
            long[] n = a.Numbers;
            string key = "advisor.note." + a.Id;
            switch (a.Id)
            {
                case "debt": return s.Get(key, "debt", f.Coins(n[0]), "assets", f.Coins(n[1]), "band", f.Percent(sim.Config.Game.Ledger.DebtRiskyOver));
                case "overdraft":
                {
                    Business b = sim.State.Businesses?.Find(a.Report ? n[0] : a.Key);
                    string name = b == null ? s.Get("advisor.aBusiness") : BusinessWords.InSentence(ctx, BusinessRules.TypeOf(sim, b));
                    return a.Report ? s.Get(key, "business", name, "used", f.Coins(n[1]), "limit", f.Coins(n[2])) : s.Get(key + ".wages", "business", name);
                }
                case "profitNotCash":
                    return n[2] >= 0 && n[2] < SeasonBook.MovedOrder.Length && System.Array.IndexOf(Uses, SeasonBook.MovedOrder[n[2]]) >= 0
                        ? s.Get(key, "profit", f.Coins(n[0]), "fall", f.Coins(n[1]), "biggest", s.Get("advisor.went." + SeasonBook.MovedOrder[n[2]]))
                        : s.Get(key + ".plain", "profit", f.Coins(n[0]), "fall", f.Coins(n[1]));
                case "pests":
                {
                    PestRiskDef p = sim.Config.Game.Farm.Pests;
                    return s.Get(key, "low", f.Percent(Fixed.One - p.OutbreakHealthMax), "high", f.Percent(Fixed.One - p.OutbreakHealthMin), "share", f.Percent(p.SprayDamageFactor));
                }
                case "feed":
                {
                    int good = (int)a.Key;
                    return s.Get(key, "feed", good >= 0 && good < sim.Config.Game.Market.Goods.Count ? s.Get(sim.Config.Game.Market.Goods[good].NameKey) : s.Get("advisor.theFeed"));
                }
                case "soil": return s.Get(key, "field", PeopleWords.Field(ctx, (int)n[0]), "fertility", f.Decimal1(Fixed.FromRaw(n[1])));
                case "dry": return s.Get(key, "field", PeopleWords.Field(ctx, (int)n[0]), "hours", s.Plural("advisor.hours", n[1]));
                case "tired": return s.Get(key, "name", PeopleWords.Name(ctx, (int)n[0]));
                case "children": return s.Plural(key, n[0]);
                case "adultClass":
                    return s.Get(key, "n", f.Integer(n[0]), "premium", f.Percent(sim.Config.Game.School.LiteratePremium), "cost", f.Coins(n[1]));
                case "deptOver": return s.Get(key, "dept", TreasurySheet.Department(ctx, (int)n[0]), "needs", f.Coins(n[1]), "budget", f.Coins(n[2]));
                case "serviceLow": return s.Get(key, "dept", TreasurySheet.Department(ctx, (int)n[0]), "service", f.Percent(Fixed.FromRaw(n[1])));
                case "grantAtRisk": return s.Get(key);
                case "thinBalance": return s.Get(key, "cash", f.Coins(n[0]), "due", f.Coins(n[1]));
                default: return s.Get(key);
            }
        }

        /// <summary>The town planner's line for the treasury sheet (spec 021 §3.12): what she said at the last close, if she spoke.</summary>
        public static AdvisorVm Planner(ViewContext ctx)
        {
            AdvisorDefs d = ctx.Sim.Config.Game.Advisors;
            TreasuryState t = ctx.Sim.State.Treasury;
            if (d == null || t == null || t.Reports.Count == 0) return null;
            long period = t.Reports[t.Reports.Count - 1].Period;
            AdvisorNote a = ctx.Sim.State.Guidance?.Advice.Find(x => x.Report && x.Key == period && d.IndexOf(x.Id) >= 0 && d.IsPlanner(d.Notes[d.IndexOf(x.Id)]));
            return a == null ? null : Line(ctx, a, false);
        }

        /// <summary>The season report's line for <paramref name="period"/>, if an advisor spoke at its close.</summary>
        public static LineVm Report(ViewContext ctx, long period)
        {
            AdvisorDefs ad = ctx.Sim.Config.Game.Advisors;
            AdvisorNote a = ctx.Sim.State.Guidance?.Advice.Find(x => x.Report && x.Key == period && ad != null && ad.IndexOf(x.Id) >= 0 && !ad.IsPlanner(ad.Notes[ad.IndexOf(x.Id)]));
            AdvisorVm vm = a == null || ctx.Sim.Config.Game.Advisors == null ? null : Line(ctx, a, false);
            return vm == null ? null : new LineVm(vm.Text, null) { Advisor = vm };
        }

        /// <summary>
        /// The alert list's one advisor line (spec 018 §3.4): under the alert whose note ranks first among those an advisor spoke about
        /// (same topic, same field, loan, business, feed or HR event). Its index in <paramref name="alerts"/>, or −1.
        /// </summary>
        public static int ForAlerts(ViewContext ctx, List<AlertVm> alerts, out AdvisorVm line)
        {
            line = null;
            AdvisorDefs d = ctx.Sim.Config.Game.Advisors;
            GuidanceState g = ctx.Sim.State.Guidance;
            if (d == null || g == null) return -1;
            int best = -1, rank = int.MaxValue;
            AdvisorNote said = null;
            for (int i = 0; i < alerts.Count; i++)
            {
                AlertVm al = alerts[i];
                if (al.Note == null) continue;
                AdvisorNote a = g.Advice.Find(x => !x.Report && x.Id == al.Note && x.Key == al.NoteKey);
                int k = d.IndexOf(al.Note);
                if (a == null || k < 0 || k >= rank) continue;
                best = i; rank = k; said = a;
            }
            if (said != null) line = Line(ctx, said, true);
            return line == null ? -1 : best;
        }
    }
}
