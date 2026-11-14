using System;
using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;

namespace Sim.Core.Town
{
    /// <summary>Which tax an intake is for (spec 021 §3.3; the rates are spec 022's).</summary>
    public enum TaxKind
    {
        House = 0,
        Business = 1,
    }

    /// <summary>
    /// The town treasury (spec 021, ADR 0108): a second wallet with its own books, fed by grants, pitch rents, taxes and gifts, paying
    /// seven departments in the order they are listed, and never paying the founder or his businesses.
    /// </summary>
    public static class TreasuryRules
    {
        public static TreasuryDef Def(Simulation sim) { return sim.Config.Game?.Treasury; }

        /// <summary>The treasury's state, or null in a town whose content has none.</summary>
        public static TreasuryState State(Simulation sim) { return sim.State.Treasury; }

        public static bool IsOpen(Simulation sim) { return sim.State.Treasury != null && sim.State.Treasury.Opened; }

        /// <summary>
        /// The treasury has taken over from the founder (spec 021 §3.1): from the first close after it opened, the school's upkeep is its
        /// Education line and the shops' pitch rent is its income. Nothing earlier changes.
        /// </summary>
        public static bool TakenOver(Simulation sim)
        {
            if (!IsOpen(sim)) return false;
            long season = sim.Clock.SecondsPerSeason;
            return sim.State.Clock.SimTime >= (State(sim).OpenedAt / season + 1) * season;
        }

        /// <summary>The Township arrived (spec 021 §3.1): the treasury opens once, with the district's start-up grant and nothing from your wallet.</summary>
        internal static void OnEra(Simulation sim)
        {
            TreasuryDef d = Def(sim);
            TreasuryState s = State(sim);
            if (d == null || s == null || s.Opened || sim.State.Era == null || sim.State.Era.Current < d.Era) return;
            long now = sim.State.Clock.SimTime;
            s.OpenedAt = now;
            long grant = MarketRules.Indexed(sim, d.StartUpGrant);
            if (grant > 0) s.Ledger.Post(now, sim.Period, "ledger.townStartUpGrant", "district", new Posting(Account.Cash, grant), new Posting(Account.StartUpGrant, -grant));
            s.SeasonOpeningCash = 0; // the grant is the first season's first income
            s.LineSeen = new bool[d.Lines.Count];
            for (int i = 0; i < d.Lines.Count; i++)
                if (LineActive(sim, d.Lines[i])) { s.LineSeen[i] = true; AddToBudget(sim, d.Lines[i]); } // the first year's budget is its needs (spec 021 §3.6)
            s.Window = BudgetWindow.Set;
            s.WindowPeriod = sim.Period;
            s.YearBudgeted = (int)(sim.Period / ClockYear(sim));
            sim.Emit(new TreasuryOpened(now, grant));
        }

        private static long ClockYear(Simulation sim) { return Definitions.ClockDef.SeasonsPerYear; }

        /// <summary>A new line joins its department's budget at its needs at once (spec 021 §3.6), and its needs count as known when the budget was set.</summary>
        private static void AddToBudget(Simulation sim, LineDef line)
        {
            TreasuryState s = State(sim);
            long amount = LineAmount(sim, line);
            s.Budget[line.Department] += amount;
            s.BudgetNeeds[line.Department] += amount;
        }

        // ---- the budget (spec 021 §3.6) ----

        /// <summary>The most a department's budget may be: <c>budgetMaxOfNeeds</c> of its needs now (spec 021 §3.6).</summary>
        public static long MaxBudget(Simulation sim, int department)
        {
            return (Def(sim).BudgetMaxOfNeeds * Fixed.FromInt(Needs(sim, department))).RoundToLong();
        }

        /// <summary>
        /// The proposal filled in for the player (spec 021 §3.6): last year's actuals at today's prices. A department's needs now, times the
        /// share of its needs it was paid over the last four closes; with no closes yet, its needs.
        /// </summary>
        public static long Proposal(Simulation sim, int department)
        {
            TreasuryState s = State(sim);
            long needs = Needs(sim, department), sumPaid = 0, sumNeeds = 0;
            for (int i = Math.Max(0, s.Reports.Count - Definitions.ClockDef.SeasonsPerYear); i < s.Reports.Count; i++)
            {
                DepartmentRow row = s.Reports[i].Rows[department];
                sumPaid += row.Paid;
                sumNeeds += row.Needs;
            }
            if (needs <= 0 || sumNeeds <= 0) return needs;
            return Math.Min(needs, needs * sumPaid / sumNeeds);
        }

        /// <summary>
        /// A window opens after a close (spec 021 §3.6): at each Spring close the year's proposal is in force at once and the set window opens;
        /// at the Monsoon close, if a budget was set earlier this year and not yet revised, the revision window opens. A window lasts until it
        /// is accepted or the next close.
        /// </summary>
        private static void OpenWindows(Simulation sim)
        {
            TreasuryState s = State(sim);
            long now = sim.State.Clock.SimTime;
            int seasons = Definitions.ClockDef.SeasonsPerYear;
            int season = (int)(sim.Period % seasons), year = (int)(sim.Period / seasons);
            s.Window = BudgetWindow.None;
            if (season == 0)
            {
                for (int dept = 0; dept < TreasuryState.Departments; dept++)
                {
                    s.Budget[dept] = Proposal(sim, dept);
                    s.BudgetNeeds[dept] = Needs(sim, dept);
                }
                s.Window = BudgetWindow.Set;
                s.WindowPeriod = sim.Period;
                s.WindowSeen = false;
                s.YearBudgeted = year;
                s.Revised = false;
                sim.Emit(new BudgetWindowOpened(now, false));
            }
            else if (season == 2 && s.YearBudgeted == year && !s.Revised)
            {
                s.Window = BudgetWindow.Revise;
                s.WindowPeriod = sim.Period;
                s.WindowSeen = false;
                s.Revised = true;
                sim.Emit(new BudgetWindowOpened(now, true));
            }
        }

        /// <summary>One department's budget changed in an open window (spec 021 §3.6): 0 to <see cref="MaxBudget"/>.</summary>
        internal static Commands.RejectReason SetBudget(Simulation sim, int department, long amount)
        {
            if (!IsOpen(sim)) return Commands.RejectReason.NoTreasury;
            TreasuryState s = State(sim);
            if (s.Window == BudgetWindow.None) return Commands.RejectReason.NoWindow;
            if (department < 0 || department >= TreasuryState.Departments || amount < 0 || amount > MaxBudget(sim, department)) return Commands.RejectReason.OutOfRange;
            s.Budget[department] = amount;
            s.BudgetNeeds[department] = Needs(sim, department);
            s.WindowSeen = true;
            sim.Emit(new BudgetSet(sim.State.Clock.SimTime, department, amount));
            return Commands.RejectReason.None;
        }

        /// <summary>The budget taken as it stands (spec 021 §3.6): the window is marked seen and closes until the next one.</summary>
        internal static Commands.RejectReason AcceptBudget(Simulation sim)
        {
            if (!IsOpen(sim)) return Commands.RejectReason.NoTreasury;
            TreasuryState s = State(sim);
            if (s.Window == BudgetWindow.None) return Commands.RejectReason.NoWindow;
            s.WindowSeen = true;
            s.Window = BudgetWindow.None;
            sim.Emit(new BudgetAccepted(sim.State.Clock.SimTime));
            return Commands.RejectReason.None;
        }

        // ---- what the departments need ----

        /// <summary>A line's amount a season at today's prices (spec 021 §3.4).</summary>
        public static long LineAmount(Simulation sim, LineDef line)
        {
            TreasuryDef d = Def(sim);
            long baseAmount;
            switch (line.Source)
            {
                case Definitions.LineAmount.ClerkWage: baseAmount = d.ClerkWage; break;
                case Definitions.LineAmount.DoctorAllowance: baseAmount = d.DoctorAllowance; break;
                case Definitions.LineAmount.SchoolUpkeep: return SchoolRules.Upkeep(sim);
                default: baseAmount = line.Amount; break;
            }
            return MarketRules.Indexed(sim, baseAmount);
        }

        /// <summary>Whether a line exists now (spec 021 §3.4's "Opens with").</summary>
        public static bool LineActive(Simulation sim, LineDef line)
        {
            switch (line.Requires)
            {
                case LineRequires.School: return SchoolRules.IsOpen(sim);
                case LineRequires.Clinic: return ClinicStaffed(sim);
                default: return true;
            }
        }

        /// <summary>
        /// A doctor is in post at a clinic (spec 021 §3.3). The clinic comes with spec 023; until then it counts as not staffed, so the
        /// town gets the untied grant only and the Health department has no line.
        /// </summary>
        public static bool ClinicStaffed(Simulation sim) { return false; }

        /// <summary>A department's needs now: the sum of its active lines (spec 021 §3.4).</summary>
        public static long Needs(Simulation sim, int department)
        {
            TreasuryDef d = Def(sim);
            long sum = 0;
            foreach (LineDef l in d.Lines) if (l.Department == department && LineActive(sim, l)) sum += LineAmount(sim, l);
            return sum;
        }

        /// <summary>The treasury's residents: your citizens, and the old village's cohort once it has joined (spec 023).</summary>
        public static int Residents(Simulation sim) { return sim.State.Citizens?.Population ?? 0; }

        // ---- income ----

        /// <summary>
        /// A tax bill's intake (spec 021 §3.3, for spec 022): the office collects <c>officeBase + officeWeight × condition</c> of what is
        /// billed, rounded down; the payer pays only what is collected. Returns the amount collected, which the caller charges its payer.
        /// </summary>
        public static long CollectTax(Simulation sim, TaxKind kind, string payer, long billed)
        {
            TreasuryDef d = Def(sim);
            TreasuryState s = State(sim);
            if (!IsOpen(sim) || billed <= 0) return 0;
            Fixed factor = d.OfficeBase + d.OfficeWeight * s.Condition[0];
            long collected = Math.Min(billed, (factor * Fixed.FromInt(billed)).FloorToLong());
            if (collected <= 0) return 0;
            s.Ledger.Post(sim.State.Clock.SimTime, sim.AccrualPeriod, "ledger.townTax", "tax:" + payer,
                new Posting(Account.Cash, collected), new Posting(kind == TaxKind.House ? Account.HouseTaxIncome : Account.BusinessTaxIncome, -collected));
            return collected;
        }

        /// <summary>A permit fee (spec 021 §3.3, for spec 024): straight to Fees. The payer is a builder or a townsperson, never the treasury's own.</summary>
        public static void CollectFee(Simulation sim, string payer, long amount)
        {
            if (!IsOpen(sim) || amount <= 0) return;
            State(sim).Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.townFee", "fee:" + payer, new Posting(Account.Cash, amount), new Posting(Account.Fees, -amount));
        }

        /// <summary>
        /// A shop's pitch rent really paid (spec 021 §3.3), credited in the same step as the shop's own entry. Before the first close after
        /// the opening, and for a shop that couldn't pay, nothing is credited.
        /// </summary>
        internal static void CreditPitchRent(Simulation sim, long businessId, long rent, long period)
        {
            if (!TakenOver(sim) || rent <= 0) return;
            if (State(sim).Closing) { State(sim).PendingRents.Add(new KeyValuePair<long, long>(businessId, rent)); return; } // the new season's: after the old one is shut
            State(sim).Ledger.Post(sim.State.Clock.SimTime, period, "ledger.townPitchRent", "business:" + businessId, new Posting(Account.Cash, rent), new Posting(Account.PitchRents, -rent));
        }

        // ---- gifts and works (commands) ----

        /// <summary>A gift to the town (spec 021 §3.8): yours Dr 5260 Gifts to the village, Cr Cash; the town's Dr Cash, Cr 4670, one movement.</summary>
        internal static Commands.RejectReason Give(Simulation sim, long amount)
        {
            if (!IsOpen(sim)) return Commands.RejectReason.NoTreasury;
            if (amount < Def(sim).GiftMinimum) return Commands.RejectReason.BelowGiftMinimum;
            if (!sim.State.Ledger.CanAfford(amount)) return Commands.RejectReason.InsufficientFunds;
            long now = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(now, sim.Period, "ledger.giftToTown", "treasury", new Posting(Account.GiftsToVillage, amount), new Posting(Account.Cash, -amount));
            State(sim).Ledger.Post(now, sim.Period, "ledger.townGift", "founder", new Posting(Account.Cash, amount), new Posting(Account.GiftsFromFounder, -amount));
            sim.Emit(new GiftGiven(now, amount));
            return Commands.RejectReason.None;
        }

        /// <summary>Capital works (spec 021 §3.7): paid from the town's cash to a supplier, kept at cost at 1600 and depreciated at each close.</summary>
        internal static Commands.RejectReason Commission(Simulation sim, string workId)
        {
            if (!IsOpen(sim)) return Commands.RejectReason.NoTreasury;
            TownWorkDef work = null;
            foreach (TownWorkDef w in Def(sim).Works) if (w.Id == workId) work = w;
            if (work == null) return Commands.RejectReason.UnknownWork;
            long cost = MarketRules.Indexed(sim, work.Cost), now = sim.State.Clock.SimTime;
            TreasuryState s = State(sim);
            if (!s.Ledger.CanAfford(cost)) return Commands.RejectReason.InsufficientFunds;
            s.Ledger.Post(now, sim.Period, "ledger.townWorks", "works:" + workId, new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            s.Works.Add(new WorkRecord { WorkId = workId, Cost = cost, LifeSeasons = work.LifeSeasons, At = now });
            sim.Emit(new WorksCommissioned(now, workId, cost));
            return Commands.RejectReason.None;
        }

        // ---- what the next close will do (spec 021 §3.11) ----

        /// <summary>The next close as the sheet and the warning see it: what is due, what the town will have, and who would go unpaid.</summary>
        public sealed class CloseForecast
        {
            public long Due { get; internal set; }
            public long Cash { get; internal set; }

            /// <summary>The grants and the tax bills certain by the close (spec 021 §3.11).</summary>
            public long Income { get; internal set; }
            public long Short { get; internal set; }

            /// <summary>The first department, in payment order, that would go unpaid, or -1.</summary>
            public int FirstUnpaid { get; internal set; } = -1;
            public long[] Needs { get; internal set; }
            public long[] Paid { get; internal set; }
        }

        /// <summary>The two passes of the close, run on the departments' needs and budgets with the cash and income the town will have.</summary>
        public static CloseForecast Forecast(Simulation sim)
        {
            TreasuryState s = State(sim);
            var f = new CloseForecast { Needs = new long[TreasuryState.Departments], Paid = new long[TreasuryState.Departments] };
            GrantsNow(sim, out long untied, out long tied, out _);
            f.Cash = s.Cash;
            f.Income = untied + tied;
            long remaining = f.Cash + f.Income;
            for (int d = 0; d < f.Needs.Length; d++) { f.Needs[d] = Needs(sim, d); f.Due += f.Needs[d]; }
            for (int pass = 1; pass <= 2; pass++)
                for (int d = 0; d < f.Needs.Length; d++)
                {
                    long room = pass == 1 ? Math.Min(f.Needs[d], s.Budget[d]) : f.Needs[d] - f.Paid[d];
                    long pay = Math.Min(Math.Max(0, room), remaining);
                    f.Paid[d] += pay;
                    remaining -= pay;
                }
            for (int d = 0; d < f.Needs.Length; d++)
            {
                f.Short += f.Needs[d] - f.Paid[d];
                if (f.FirstUnpaid < 0 && f.Paid[d] < f.Needs[d]) f.FirstUnpaid = d;
            }
            return f;
        }

        // ---- the close (spec 021 §3.10) ----

        /// <summary>The season's close begins (the founder's books run first): pitch rents paid now are for the new season.</summary>
        internal static void BeginClose(Simulation sim)
        {
            if (IsOpen(sim)) State(sim).Closing = true;
        }

        /// <summary>Money in a season as the close will have it: the grants (spec 021 §3.3), for the sheet and the warning.</summary>
        public static long GrantsNow(Simulation sim, out long untied, out long tied, out long tiedWithheld)
        {
            TreasuryDef d = Def(sim);
            int residents = Residents(sim);
            untied = MarketRules.Indexed(sim, d.GrantUntied * residents);
            long fullTied = MarketRules.Indexed(sim, d.GrantTied * residents);
            tied = ClinicStaffed(sim) ? fullTied : 0;
            tiedWithheld = fullTied - tied;
            return untied + tied;
        }

        /// <summary>The season's close (spec 021 §3.10, system Treasury): income, depreciation, the two passes, conditions, the report.</summary>
        internal static void Close(Simulation sim)
        {
            if (!IsOpen(sim)) return;
            TreasuryDef d = Def(sim);
            TreasuryState s = State(sim);
            long now = sim.State.Clock.SimTime, closing = sim.Period - 1;
            Ledger ledger = s.Ledger;
            long opening = s.SeasonOpeningCash;

            // 1. Income: the grants (rents and taxes were posted as they were paid; gifts when given).
            GrantsNow(sim, out long untied, out long tied, out long withheld);
            if (untied > 0) ledger.Post(now, closing, "ledger.townGrantUntied", "district", new Posting(Account.Cash, untied), new Posting(Account.GrantUntied, -untied));
            if (tied > 0) ledger.Post(now, closing, "ledger.townGrantTied", "district", new Posting(Account.Cash, tied), new Posting(Account.GrantTied, -tied));
            sim.Emit(new GrantPaid(now, untied, tied, withheld));

            // 2. Depreciation of the works.
            foreach (WorkRecord w in s.Works)
            {
                if (w.SeasonsDepreciated >= w.LifeSeasons) continue;
                long amount = w.SeasonsDepreciated + 1 == w.LifeSeasons ? w.Cost - w.Depreciated : w.Cost / w.LifeSeasons;
                w.SeasonsDepreciated++;
                if (amount == 0) continue;
                w.Depreciated += amount;
                ledger.Post(now, closing, "ledger.townDepreciation", "works:" + w.WorkId, new Posting(Account.Depreciation, amount), new Posting(Account.AccumulatedDepreciation, -amount));
            }

            // 3. Spending: a new line joins its department's budget at its needs, then the two passes.
            var newLine = new bool[TreasuryState.Departments];
            for (int i = 0; i < d.Lines.Count; i++)
                if (!s.LineSeen[i] && LineActive(sim, d.Lines[i])) { s.LineSeen[i] = true; AddToBudget(sim, d.Lines[i]); newLine[d.Lines[i].Department] = true; }
            int n = d.Lines.Count;
            var amounts = new long[n];
            var paid = new long[n];
            var needs = new long[TreasuryState.Departments];
            for (int i = 0; i < n; i++)
            {
                if (!LineActive(sim, d.Lines[i])) continue;
                amounts[i] = LineAmount(sim, d.Lines[i]);
                needs[d.Lines[i].Department] += amounts[i];
            }
            var paid2 = new long[TreasuryState.Departments];
            for (int pass = 1; pass <= 2; pass++)
                for (int dept = 0; dept < TreasuryState.Departments; dept++)
                {
                    long room = pass == 1 ? Math.Min(needs[dept], s.Budget[dept]) : long.MaxValue;
                    foreach (int i in InPayOrder(d, dept))
                    {
                        if (room <= 0 || ledger.Cash <= 0) break;
                        long pay = Math.Min(Math.Min(amounts[i] - paid[i], room), ledger.Cash);
                        if (pay <= 0) continue;
                        ledger.Post(now, closing, "ledger.townLine", "line:" + d.Lines[i].Id + (pass == 2 ? ":2" : ""), new Posting(d.Departments[dept].Account, pay), new Posting(Account.Cash, -pay));
                        paid[i] += pay;
                        if (pass == 2) paid2[dept] += pay;
                        if (pass == 1) room -= pay;
                    }
                }

            // 4. The conditions move halfway toward each department's funding; the report records the row.
            var rows = new List<DepartmentRow>();
            for (int dept = 0; dept < TreasuryState.Departments; dept++)
            {
                long deptPaid = 0;
                for (int i = 0; i < n; i++) if (d.Lines[i].Department == dept) deptPaid += paid[i];
                var row = new DepartmentRow { Budget = s.Budget[dept], Needs = needs[dept], Paid = deptPaid, PaidInPass2 = paid2[dept] };
                row.Sentence = (int)Sentence(row, s.BudgetNeeds[dept], newLine[dept]);
                Fixed from = s.Condition[dept];
                Fixed to = from + (row.Funding - from) * d.ConditionStep;
                if (to > Fixed.One) to = Fixed.One;
                if (to < Fixed.Zero) to = Fixed.Zero;
                s.Condition[dept] = to;
                row.Condition = to;
                s.Unpaid[dept] = row.Unpaid > 0 ? s.Unpaid[dept] + 1 : 0;
                s.OverBudget[dept] = needs[dept] > s.Budget[dept] ? s.OverBudget[dept] + 1 : 0;
                rows.Add(row);
                if (needs[dept] > 0) sim.Emit(new DepartmentPaid(now, dept, deptPaid, needs[dept], paid2[dept] > 0));
                if (row.Unpaid > 0) sim.Emit(new DepartmentUnpaid(now, dept, row.Unpaid));
                if (to != from) sim.Emit(new ConditionChanged(now, dept, from, to));
            }

            // 5. The report, then income and expenses to the Fund balance, checked.
            var balances = new List<KeyValuePair<Account, long>>();
            foreach (Account a in Accounts.All)
                if (ledger.Balance(a) != 0) balances.Add(new KeyValuePair<Account, long>(a, ledger.Balance(a)));
            long inflows = 0, outflows = 0;
            foreach (JournalEntry e in ledger.Entries)
            {
                if (e.Period != closing) continue;
                foreach (Posting p in e.Postings)
                {
                    if (p.Account != Account.Cash) continue;
                    if (p.Amount > 0) inflows += p.Amount; else outflows -= p.Amount;
                }
            }
            var report = new TreasuryReport { Period = closing, Balances = balances.AsReadOnly(), Opening = opening, Inflows = inflows, Outflows = outflows, TiedWithheld = withheld, Rows = rows.AsReadOnly() };
            FinanceSystem.CloseToRetainedEarnings(ledger, now, closing);
            string failure = ledger.Reconcile();
            if (failure != null)
            {
                if (sim.Config.Options.StrictChecks) throw new InvalidOperationException("The town's books failed to reconcile: " + failure);
                sim.Emit(new LedgerReconciliationFailed(now, "treasury: " + failure));
            }
            s.Reports.Add(report);
            if (s.Reports.Count > d.RetainSeasons) s.Reports.RemoveAt(0);
            OpenWindows(sim);
            s.Closing = false;
            s.SeasonOpeningCash = ledger.Cash; // the new season opens with what the old one left; its pitch rents are its first income
            foreach (KeyValuePair<long, long> r in s.PendingRents) // the new season's pitch rents, now that the old season is shut
                ledger.Post(now, sim.Period, "ledger.townPitchRent", "business:" + r.Key, new Posting(Account.Cash, r.Value), new Posting(Account.PitchRents, -r.Value));
            s.PendingRents.Clear();
            ledger.DropEntriesBefore(closing - d.RetainSeasons + 1);
        }

        /// <summary>
        /// The one sentence that explains a department's variance (spec 021 §3.6), first that holds: a line joined this season, cash ran out, its needs
        /// passed its budget (the budget was set below its needs, else prices rose since), money was left over.
        /// </summary>
        public static VarianceSentence Sentence(DepartmentRow row, long needsWhenSet, bool newLine)
        {
            if (newLine) return VarianceSentence.NewLine;
            if (row.Unpaid > 0) return VarianceSentence.CashRanOut;
            if (row.Needs > row.Budget) return needsWhenSet > row.Budget ? VarianceSentence.BudgetBelowNeeds : VarianceSentence.PricesRose;
            if (row.Budget > row.Paid) return VarianceSentence.Unspent;
            return VarianceSentence.None;
        }

        /// <summary>
        /// Hourly, from 00:00 of a season's last day (spec 021 §3.11): once the close would leave a department unpaid, one warning; a change in
        /// the shortfall later that day warns again.
        /// </summary>
        internal static void Hourly(Simulation sim)
        {
            if (!IsOpen(sim)) return;
            TreasuryState s = State(sim);
            long season = sim.Clock.SecondsPerSeason;
            if (season - sim.State.Clock.SimTime % season > Definitions.ClockDef.SecondsPerDay) return;
            CloseForecast f = Forecast(sim);
            if (f.Short <= 0) { s.ShortWarnedAmount = 0; return; }
            if (s.ShortWarnedPeriod == sim.Period && s.ShortWarnedAmount == f.Short) return;
            s.ShortWarnedPeriod = sim.Period;
            s.ShortWarnedAmount = f.Short;
            sim.Emit(new TreasuryShortAhead(sim.State.Clock.SimTime, f.Short, f.FirstUnpaid));
        }

        /// <summary>A department's lines in payment order: wages before upkeep, each in content order (spec 021 §3.4).</summary>
        private static IEnumerable<int> InPayOrder(TreasuryDef d, int dept)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < d.Lines.Count; i++)
                    if (d.Lines[i].Department == dept && d.Lines[i].Wage == (pass == 0)) yield return i;
        }
    }

    /// <summary>The treasury's place in the processing order (spec 021 §3.10): after the civic systems, before events.</summary>
    internal sealed class TreasurySystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Treasury; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Hourly) TreasuryRules.Hourly(sim);
            if (pass == ItemClass.Seasonal) TreasuryRules.Close(sim);
        }
    }
}
