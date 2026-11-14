using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The town treasury's <em>Why?</em>s (spec 021 §3.2): every number, with the movements or figures behind it. Never an "other" row.</summary>
    public static partial class WhyView
    {
        private static TreasuryState Town(ViewContext ctx) { return ctx.Sim.State.Treasury; }

        private static WhyVm TreasuryBalance(ViewContext ctx, long id)
        {
            TreasuryState st = Town(ctx);
            WhyVm vm = New(ctx, "why.treasury.balance.title", Money(ctx, st.Cash));
            foreach (JournalEntry e in st.Ledger.Entries)
            {
                if (e.Period < ctx.Sim.Period - 1) continue; // this season and the one before: the book of what moved the balance lately
                long cash = 0;
                foreach (Posting p in e.Postings) if (p.Account == Account.Cash) cash += p.Amount;
                if (cash != 0) vm.Rows.Add(new WhyRowVm(EntryLine(ctx, e), Signed(ctx, cash)));
            }
            Text(vm, ctx, "why.treasury.balance.text");
            return vm;
        }

        private static string Signed(ViewContext ctx, long v) { return v > 0 ? "+" + Money(ctx, v) : v < 0 ? "−" + Money(ctx, -v) : Money(ctx, 0); }

        /// <summary>"Day 12 · District grant, untied": when, what and from or to whom.</summary>
        private static string EntryLine(ViewContext ctx, JournalEntry e)
        {
            long day = e.SimTime % ctx.Sim.Clock.SecondsPerSeason / Sim.Core.Definitions.ClockDef.SecondsPerDay + 1;
            return ctx.Strings.Get("why.treasury.entry", "day", ctx.Fmt.Integer(day), "what", EntryName(ctx, e));
        }

        private static string EntryName(ViewContext ctx, JournalEntry e)
        {
            var s = ctx.Strings;
            string r = e.Reference ?? "";
            int colon = r.IndexOf(':');
            string kind = colon < 0 ? r : r.Substring(0, colon);
            switch (kind)
            {
                case "line": return s.Get("treasury.line." + r.Substring(5).Replace(":2", ""));
                case "business": return s.Get("why.treasury.fromShop", "n", r.Substring(9));
                case "works": return s.Get("why.treasury.works", "name", r.Substring(6));
                case "tax": return s.Get("why.treasury.tax", "who", r.Substring(4));
                case "fee": return s.Get("why.treasury.fee", "who", r.Substring(4));
                case "founder": return s.Get("why.treasury.gift");
                default: return s.Get(e.DescriptionKey.StartsWith("ledger.townGrantUntied") ? "account.GrantUntied" : e.DescriptionKey.StartsWith("ledger.townGrantTied") ? "account.GrantTied" : "account.StartUpGrant");
            }
        }

        private static WhyVm TreasuryUntied(ViewContext ctx, long id)
        {
            Simulation_(ctx, out TreasuryDef d);
            TreasuryRules.GrantsNow(ctx.Sim, out long untied, out _, out _);
            WhyVm vm = New(ctx, "why.treasury.untied.title", Money(ctx, untied));
            Row(vm, ctx, "why.treasury.residents", ctx.Fmt.Integer(TreasuryRules.Residents(ctx.Sim)));
            Row(vm, ctx, "why.treasury.perResident", Money(ctx, Sim.Core.Market.MarketRules.Indexed(ctx.Sim, d.GrantUntied)));
            Text(vm, ctx, "why.treasury.untied.text");
            return vm;
        }

        private static WhyVm TreasuryTied(ViewContext ctx, long id)
        {
            Simulation_(ctx, out TreasuryDef d);
            TreasuryRules.GrantsNow(ctx.Sim, out _, out long tied, out long withheld);
            WhyVm vm = New(ctx, "why.treasury.tied.title", Money(ctx, tied));
            Row(vm, ctx, "why.treasury.residents", ctx.Fmt.Integer(TreasuryRules.Residents(ctx.Sim)));
            Row(vm, ctx, "why.treasury.perResident", Money(ctx, Sim.Core.Market.MarketRules.Indexed(ctx.Sim, d.GrantTied)));
            Row(vm, ctx, "why.treasury.clinic", ctx.Strings.Get(TreasuryRules.ClinicStaffed(ctx.Sim) ? "why.treasury.clinicYes" : "why.treasury.clinicNo"));
            if (withheld > 0) Row(vm, ctx, "why.treasury.withheld", Money(ctx, withheld));
            Text(vm, ctx, "why.treasury.tied.text");
            return vm;
        }

        private static void Simulation_(ViewContext ctx, out TreasuryDef d) { d = ctx.Sim.Config.Game.Treasury; }

        private static WhyVm TreasuryRents(ViewContext ctx, long id)
        {
            TreasuryState st = Town(ctx);
            WhyVm vm = New(ctx, "why.treasury.rents.title", Money(ctx, -st.Ledger.Balance(Account.PitchRents)));
            AccountRows(ctx, vm, st.Ledger, ctx.Sim.Period, Account.PitchRents);
            Text(vm, ctx, "why.treasury.rents.text");
            return vm;
        }

        private static WhyVm TreasuryGifts(ViewContext ctx, long id)
        {
            TreasuryState st = Town(ctx);
            WhyVm vm = New(ctx, "why.treasury.gifts.title", Money(ctx, -st.Ledger.Balance(Account.GiftsFromFounder)));
            AccountRows(ctx, vm, st.Ledger, ctx.Sim.Period, Account.GiftsFromFounder);
            if (vm.Rows.Count == 0) Text(vm, ctx, "why.treasury.noGifts");
            Text(vm, ctx, "why.treasury.gifts.text");
            return vm;
        }

        private static WhyVm TreasuryDept(ViewContext ctx, long id)
        {
            int dept = (int)id;
            TreasuryState st = Town(ctx);
            TreasuryDef d = ctx.Sim.Config.Game.Treasury;
            long needs = TreasuryRules.Needs(ctx.Sim, dept);
            WhyVm vm = New(ctx, "why.treasury.dept.title", Money(ctx, needs), "dept", TreasurySheet.Department(ctx, dept));
            foreach (LineDef l in d.Lines)
                if (l.Department == dept && TreasuryRules.LineActive(ctx.Sim, l))
                    vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("treasury.line." + l.Id), Money(ctx, TreasuryRules.LineAmount(ctx.Sim, l))));
            Row(vm, ctx, "why.treasury.budget", Money(ctx, st.Budget[dept]));
            Row(vm, ctx, "why.treasury.service", ctx.Fmt.Percent(st.Condition[dept]));
            Text(vm, ctx, "why.treasury.dept." + d.Departments[dept].Id);
            Text(vm, ctx, "why.treasury.dept.text");
            return vm;
        }

        private static WhyVm TreasuryDue(ViewContext ctx, long id)
        {
            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(ctx.Sim);
            WhyVm vm = New(ctx, "why.treasury.due.title", Money(ctx, f.Due));
            for (int i = 0; i < f.Needs.Length; i++)
                if (f.Needs[i] > 0) vm.Rows.Add(new WhyRowVm(TreasurySheet.Department(ctx, i), Money(ctx, f.Needs[i])));
            Row(vm, ctx, "why.treasury.due.cash", Money(ctx, f.Cash));
            Row(vm, ctx, "why.treasury.due.income", Money(ctx, f.Income));
            Row(vm, ctx, f.Short > 0 ? "why.treasury.due.short" : "why.treasury.due.covered", Money(ctx, f.Short));
            Text(vm, ctx, "why.treasury.due.text");
            return vm;
        }

        /// <summary>One account of one closed season (<paramref name="id"/> = period × 10,000 + account): every movement behind it, by day.</summary>
        private static WhyVm TreasuryAccount(ViewContext ctx, long id)
        {
            long period = id / 10000;
            var account = (Account)(int)(id % 10000);
            TreasuryState st = Town(ctx);
            long value = 0;
            TreasuryReport r = TreasurySheet.Find(ctx.Sim, period);
            if (r != null) value = System.Math.Abs(r.Balance(account));
            WhyVm vm = New(ctx, "why.treasury.account.title", Money(ctx, value), "name", AccountName(ctx, account));
            AccountRows(ctx, vm, st.Ledger, period, account);
            if (vm.Rows.Count == 0) Text(vm, ctx, "why.treasury.noMoves");
            return vm;
        }

        private static string AccountName(ViewContext ctx, Account a)
        {
            if (a >= Account.DeptOffice && a <= Account.DeptPolicies) return TreasurySheet.Department(ctx, (int)(a - Account.DeptOffice) / 10);
            return ctx.Strings.Get("account." + a);
        }

        private static void AccountRows(ViewContext ctx, WhyVm vm, Ledger ledger, long period, Account account)
        {
            foreach (JournalEntry e in ledger.Entries)
            {
                if (e.Period != period || e.DescriptionKey == "ledger.closeSeason") continue; // the sweep to the fund balance isn't a movement
                foreach (Posting p in e.Postings)
                    if (p.Account == account) vm.Rows.Add(new WhyRowVm(EntryLine(ctx, e), Money(ctx, System.Math.Abs(p.Amount))));
            }
        }

        /// <summary>A season's result and what the town has (<paramref name="id"/> = the period).</summary>
        private static WhyVm TreasuryFund(ViewContext ctx, long id)
        {
            TreasuryReport r = TreasurySheet.Find(ctx.Sim, id);
            WhyVm vm = New(ctx, "why.treasury.fund.title", Money(ctx, r == null ? 0 : TreasurySheet.FundBalance(r)));
            if (r != null)
            {
                Row(vm, ctx, "why.treasury.fund.opening", Money(ctx, r.Opening));
                Row(vm, ctx, "why.treasury.fund.in", Money(ctx, r.Inflows));
                Row(vm, ctx, "why.treasury.fund.out", Money(ctx, r.Outflows));
                Row(vm, ctx, "why.treasury.fund.closing", Money(ctx, r.Closing));
            }
            Text(vm, ctx, "why.treasury.fund.text");
            return vm;
        }
    }
}
