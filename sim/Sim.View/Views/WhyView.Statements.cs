using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>The season page's <em>Why?</em>s (spec 019 §3.1, ADR 0101 §4): each row says what is behind it, and a cash row lists every movement.</summary>
    public static partial class WhyView
    {
        private static WhyVm StatementWhy(ViewContext ctx, string line, long id)
        {
            switch (line)
            {
                case "acct": return AccountWhy(ctx, id / 10000, (Account)(int)(id % 10000));
                case "cashrow": return CashRowWhy(ctx, StatementSheet.PeriodOf(id), StatementSheet.IndexOf(id));
                case "bs": return BalanceWhy(ctx, StatementSheet.PeriodOf(id), StatementSheet.IndexOf(id));
                case "hub": return HubWhy(ctx, (int)id);
                case "year": return YearWhy(ctx, StatementSheet.PeriodOf(id), StatementSheet.IndexOf(id));
                default: return PeopleWhy(ctx, StatementSheet.PeriodOf(id), StatementSheet.IndexOf(id));
            }
        }

        private static SeasonReport ReportOf(ViewContext ctx, long period)
        {
            return StatementSheet.Find(ctx.Sim, period) ?? ctx.Sim.State.Finance.Reports[ctx.Sim.State.Finance.Reports.Count - 1];
        }

        /// <summary>This season, the same season last year and the change: the way every statement row explains itself.</summary>
        private static void Compare(WhyVm vm, ViewContext ctx, SeasonReport r, SeasonReport prev, long now, long then)
        {
            vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("stmt.why.now", "season", FieldSheets.SeasonName(ctx, r.Period)), StatementSheet.Amount(ctx, now)));
            if (prev == null) { Text(vm, ctx, "stmt.why.firstYear", "season", FieldSheets.SeasonName(ctx, r.Period)); return; }
            vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("stmt.why.then", "season", FieldSheets.SeasonName(ctx, r.Period)), StatementSheet.Amount(ctx, then)));
            vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("stmt.why.change"), MoneySheet.Signed(ctx, now - then)));
        }

        private static WhyVm AccountWhy(ViewContext ctx, long period, Account a)
        {
            SeasonReport r = ReportOf(ctx, period);
            long now = StatementSheet.AccountAmount(r, a);
            WhyVm vm = new WhyVm { Title = ctx.Strings.Get("account." + a), Value = StatementSheet.Amount(ctx, now) };
            Compare(vm, ctx, r, StatementSheet.LastYear(ctx.Sim, r.Period), now, StatementSheet.AccountAmount(StatementSheet.LastYear(ctx.Sim, r.Period), a));
            Text(vm, ctx, "stmt.why.account");
            return vm;
        }

        private static WhyVm BalanceWhy(ViewContext ctx, long period, int index)
        {
            SeasonReport r = ReportOf(ctx, period);
            string key = StatementSheet.KeyAt(index);
            SeasonReport prev = StatementSheet.LastYear(ctx.Sim, r.Period);
            bool owe = key == "loans" || key == "interest" || key == "owe";
            long now = StatementSheet.Item(r.NetWorth, key) * (owe ? -1 : 1);
            WhyVm vm = new WhyVm { Title = ctx.Strings.Get("stmt.bs." + key), Value = StatementSheet.Amount(ctx, now) };
            Compare(vm, ctx, r, prev, now, prev == null ? 0 : StatementSheet.Item(prev.NetWorth, key) * (owe ? -1 : 1));
            Text(vm, ctx, "stmt.bs." + key + ".why");
            return vm;
        }

        /// <summary>What changed most from the same season last year, in words: the account (or the part of net worth) that moved the most.</summary>
        private static void BiggestChange(WhyVm vm, ViewContext ctx, SeasonReport r, string line)
        {
            SeasonReport prev = StatementSheet.LastYear(ctx.Sim, r.Period);
            string season = FieldSheets.SeasonName(ctx, r.Period);
            if (prev == null) { Text(vm, ctx, "stmt.why.firstYear", "season", season); return; }
            string what = null;
            long best = 0;
            if (line == "worth")
            {
                foreach (string key in StatementSheet.OwnKeys)
                {
                    long d = StatementSheet.Item(r.NetWorth, key) - StatementSheet.Item(prev.NetWorth, key);
                    if (System.Math.Abs(d) > System.Math.Abs(best)) { best = d; what = ctx.Strings.Get("stmt.bs." + key); }
                }
                foreach (string key in StatementSheet.OweKeys)
                {
                    long d = StatementSheet.Item(prev.NetWorth, key) - StatementSheet.Item(r.NetWorth, key); // owing less is a gain
                    if (System.Math.Abs(d) > System.Math.Abs(best)) { best = d; what = ctx.Strings.Get("stmt.bs." + key); }
                }
            }
            else
            {
                ReportLine own = r.Line(line);
                foreach (Account a in Accounts.All)
                {
                    if (own != null && own.Accounts.Count > 0 && !System.Linq.Enumerable.Contains(own.Accounts, a)) continue;
                    long d = StatementSheet.AccountAmount(r, a) - StatementSheet.AccountAmount(prev, a);
                    if (System.Math.Abs(d) > System.Math.Abs(best)) { best = d; what = ctx.Strings.Get("account." + a); }
                }
            }
            if (what == null || best == 0) Text(vm, ctx, "stmt.why.noChange", "season", season);
            else Text(vm, ctx, "stmt.why.biggest", "season", season, "what", what, "change", MoneySheet.Signed(ctx, best));
        }

        private static WhyVm CashRowWhy(ViewContext ctx, long period, int index)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            SeasonReport r = ReportOf(ctx, period);
            if (index >= r.CashRows.Count) return new WhyVm { Title = s.Get("stmt.tab.cash"), Value = "" };
            CashRow row = r.CashRows[index];
            WhyVm vm = new WhyVm { Title = s.Get(row.LabelKey), Value = StatementSheet.Amount(ctx, row.Amount) };
            List<CashMovement> moves = CashRows.MovementsOf(sim.State.Ledger, r, row);
            if (moves.Count == 0) { Text(vm, ctx, "stmt.why.dropped"); return vm; }
            long season = sim.Clock.SecondsPerSeason;
            // Entries of one day, one kind and one party read as one line (inputs are bought many times a day).
            long lastDay = -1, sum = 0;
            int count = 0;
            string lastKey = null, lastText = null, lastNote = null;
            void Flush()
            {
                if (count == 0) return;
                vm.Rows.Add(new WhyRowVm(count > 1 ? s.Get("stmt.why.times", "what", lastText, "n", ctx.Fmt.Integer(count)) : lastText, StatementSheet.Amount(ctx, sum), lastNote));
                count = 0; sum = 0;
            }
            foreach (CashMovement m in moves)
            {
                long day = m.Entry.SimTime / ClockDef.SecondsPerDay;
                string key = day + "|" + m.Entry.DescriptionKey + "|" + m.Entry.Reference;
                if (key != lastKey) { Flush(); lastKey = key; lastDay = day; lastText = s.Get("stmt.why.day", "season", FieldSheets.SeasonName(ctx, m.Entry.SimTime / season), "day", ctx.Fmt.Integer(m.Entry.SimTime % season / ClockDef.SecondsPerDay + 1), "what", s.Get(m.Entry.DescriptionKey)); lastNote = Party(ctx, m.Entry.Reference); }
                sum += m.Amount;
                count++;
            }
            Flush();
            Text(vm, ctx, "stmt.why.cashRow");
            return vm;
        }

        /// <summary>Whom an entry was with, when its reference says (a hand, a business, a lender); else null.</summary>
        private static string Party(ViewContext ctx, string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            Simulation sim = ctx.Sim;
            int colon = reference.IndexOf(':');
            if (colon < 0 || !long.TryParse(reference.Substring(colon + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long id)) return null;
            switch (reference.Substring(0, colon))
            {
                case "hand":
                    Hand h = sim.State.People?.Hand(id);
                    return h == null ? null : PeopleWords.Name(ctx, h.Name);
                case "business":
                    return sim.State.Businesses?.Find(id) == null ? null : StatementSheet.PlaceName(ctx, id);
                case "loan":
                    Loan loan = sim.State.Credit?.Find(id);
                    return loan == null ? null : ctx.Strings.Get(sim.Config.Game.Credit.Lenders[loan.Lender].NameKey);
                default: return null;
            }
        }

        private static WhyVm PeopleWhy(ViewContext ctx, long period, int index)
        {
            var s = ctx.Strings;
            SeasonReport r = ReportOf(ctx, period);
            if (index == 999 || index >= r.People.Count)
            {
                WhyVm rep = new WhyVm { Title = s.Get("stmt.people.rep"), Value = ctx.Fmt.Integer(r.ReputationAfter) };
                Row(rep, ctx, "stmt.people.repBefore", ctx.Fmt.Integer(r.ReputationBefore));
                Row(rep, ctx, "stmt.people.repAfter", ctx.Fmt.Integer(r.ReputationAfter));
                Text(rep, ctx, "stmt.people.rep.why");
                return rep;
            }
            PeopleRow p = r.People[index];
            WhyVm vm = new WhyVm { Title = PeopleWords.Name(ctx, p.Name), Value = ctx.Fmt.Coins(p.Paid) };
            Row(vm, ctx, "stmt.people.whyPaid", ctx.Fmt.Coins(p.Paid));
            Row(vm, ctx, "stmt.people.whyAsked", ctx.Fmt.Coins(p.Asked));
            Row(vm, ctx, "stmt.people.whySkill", PeopleWords.Band(ctx, p.Skill));
            Text(vm, ctx, "stmt.people.outcome.why." + (int)p.Outcome);
            return vm;
        }

        /// <summary><em>Your year</em>'s row: the four seasons behind it, in order.</summary>
        private static WhyVm YearWhy(ViewContext ctx, long winter, int row)
        {
            var s = ctx.Strings;
            List<SeasonReport> year = YearSheet.Seasons(ctx.Sim, winter);
            string[] titles = { "year.farmProfit", "year.netWorth", "year.best", "year.worst", "year.biggestCost" };
            WhyVm vm = new WhyVm { Title = s.Get(titles[System.Math.Min(row, 4)]), Value = "" };
            Account top = Account.Cash;
            if (row == YearSheet.BiggestCost) top = YearSheet.BiggestCostOf(year, out _);
            foreach (SeasonReport r in year)
            {
                long v = row == YearSheet.FarmProfit ? r.FarmProfit : row == YearSheet.NetWorth ? r.NetWorth.Total
                    : row == YearSheet.BiggestCost ? -StatementSheet.AccountAmount(r, top) : r.NetResult;
                vm.Rows.Add(new WhyRowVm(ReportSheet.PeriodName(ctx, r.Period), StatementSheet.Amount(ctx, v)));
            }
            Text(vm, ctx, "year.why." + System.Math.Min(row, 4));
            return vm;
        }
    }
}
