using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;

namespace Sim.View.Views
{
    /// <summary>
    /// The savings deposit's sheet (spec 019 §3.5, ADR 0101 §2): what's in the bank, the interest and the prices beside it, put in and take out
    /// (the steps business cash uses, one tap each), and the standing choice for a close that comes up short.
    /// </summary>
    public static class SavingsSheet
    {
        public const string Kind = "Savings";
        private static readonly long[] Steps = { 5_000_00, 10_000_00, 25_000_00, 50_000_00 };

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long balance = SavingsRules.Balance(sim), cash = sim.State.Ledger.Cash, min = sim.Config.Game.Ledger.SavingsMinimum;
            var vm = new SheetVm { Icon = "savings", Title = s.Get("savings.title"), Subtitle = s.Get("savings.subtitle") };

            vm.Lines.Add(new LineVm(s.Get("savings.balance"), ctx.Fmt.Coins(balance), StatementSheet.HubWhy, HubSheet.Savings) { Icon = "savings" });
            vm.Lines.Add(new LineVm(s.Get("savings.interest"), s.Get("savings.interestValue", "rate", ctx.Fmt.Percent(sim.Config.Game.Ledger.SavingsInterest)), StatementSheet.HubWhy, HubSheet.Savings)
                { Note = balance > 0 ? s.Get("savings.interestNext", "amount", ctx.Fmt.Coins(SavingsRules.NextInterest(sim))) : s.Get("savings.interestNone") });
            int rose = Hub.PricesRose(sim, out int seasons);
            vm.Lines.Add(new LineVm(s.Get("savings.prices"), seasons == 0 ? s.Get("savings.pricesNone") : s.Get("savings.pricesValue", "pct", ctx.Fmt.Integer(rose), "seasons", s.Plural("why.seasons", seasons)),
                StatementSheet.HubWhy, HubSheet.Prices) { Note = s.Get("savings.pricesNote") });

            vm.Lines.Add(LineVm.Header(s.Get("savings.putIn", "cash", ctx.Fmt.Coins(cash))));
            int shown = 0;
            foreach (long a in Steps)
            {
                long amount = a;
                if (amount > cash || amount < min) continue;
                shown++;
                vm.Lines.Add(new LineVm(ctx.Fmt.Coins(amount), null)
                    { Action = ActionVm.Checked(ctx, "in:" + amount, s.Get("savings.putButton"), null, ActionKind.Primary, at => new DepositSavings(at, amount)) });
            }
            if (shown == 0) vm.Lines.Add(new LineVm(s.Get("savings.putNone"), ctx.Fmt.Coins(cash)));

            vm.Lines.Add(LineVm.Header(s.Get("savings.takeOut")));
            shown = 0;
            var outs = new List<long>();
            foreach (long a in Steps) if (a < balance) outs.Add(a);
            if (balance > 0) outs.Add(balance); // all of it
            foreach (long a in outs)
            {
                long amount = a;
                shown++;
                vm.Lines.Add(new LineVm(amount == balance ? s.Get("savings.all", "amount", ctx.Fmt.Coins(amount)) : ctx.Fmt.Coins(amount), null)
                    { Action = ActionVm.Checked(ctx, "out:" + amount, s.Get("savings.takeButton"), null, ActionKind.Secondary, at => new WithdrawSavings(at, amount)) });
            }
            if (shown == 0) vm.Lines.Add(new LineVm(s.Get("savings.takeNone"), ctx.Fmt.Coins(0)));

            vm.Lines.Add(LineVm.Header(s.Get("savings.standing")));
            bool always = SavingsRules.Always(sim);
            vm.Lines.Add(StandingRow(ctx, "ask", !always, false));
            vm.Lines.Add(StandingRow(ctx, "always", always, true));
            vm.Lines.Add(new LineVm(s.Get("savings.standingNote"), null, StatementSheet.HubWhy, HubSheet.Standing));
            return vm;
        }

        private static LineVm StandingRow(ViewContext ctx, string id, bool chosen, bool always)
        {
            var s = ctx.Strings;
            var row = new LineVm(s.Get("savings.standing." + id), null) { Note = s.Get("savings.standing." + id + ".note") };
            if (chosen) row.Badge = s.Get("savings.chosen");
            else row.Action = ActionVm.Checked(ctx, "standing:" + id, s.Get("savings.choose"), null, ActionKind.Secondary, at => new SetSavingsCover(at, always));
            return row;
        }
    }

    /// <summary>The lever that lets the savings pay a short close: <em>Pay from my savings</em>, or <em>Take it back</em> once you've said yes (spec 019 §3.5).</summary>
    public static class SavingsCover
    {
        /// <summary>
        /// <em>Next season, already due</em>'s row when the close would be short and there are savings (spec 019 §3.5): what they could cover, with
        /// the lever; null without a deposit.
        /// </summary>
        public static LineVm Row(ViewContext ctx, long shortBy)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long deposit = SavingsRules.Balance(sim);
            if (deposit == 0) return null;
            var row = new LineVm(s.Get("season.savings"), ctx.Fmt.Coins(deposit), StatementSheet.HubWhy, HubSheet.Savings) { Icon = "savings", NoteTone = Tone.Good };
            long pays = System.Math.Min(deposit, shortBy);
            if (SavingsRules.Always(sim)) row.Note = s.Get("season.savingsAlways", "amount", ctx.Fmt.Coins(pays));
            else if (SavingsRules.Consented(sim)) row.Note = s.Get("season.savingsYes", "amount", ctx.Fmt.Coins(pays));
            else { row.Note = s.Get("season.savingsCould", "amount", ctx.Fmt.Coins(pays)); row.NoteTone = Tone.Warn; }
            row.Action = Lever(ctx, shortBy);
            return row;
        }

        /// <summary>Null when there is no deposit, or when the standing choice already pays without asking.</summary>
        public static ActionVm Lever(ViewContext ctx, long shortBy)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long deposit = SavingsRules.Balance(sim);
            if (deposit == 0 || SavingsRules.Always(sim)) return null;
            if (SavingsRules.Consented(sim))
                return ActionVm.Checked(ctx, "uncover", s.Get("savings.takeBack"), null, ActionKind.Secondary, at => new TakeBackSavingsCover(at));
            long pays = System.Math.Min(deposit, shortBy);
            string detail = pays == deposit ? s.Get("savings.coverAll", "amount", ctx.Fmt.Coins(deposit)) : s.Get("savings.coverDetail", "amount", ctx.Fmt.Coins(pays), "deposit", ctx.Fmt.Coins(deposit));
            return ActionVm.Checked(ctx, "cover", s.Get("savings.cover"), detail, ActionKind.Primary, at => new CoverFromSavings(at));
        }
    }
}
