using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
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
            vm.Lines.Add(PutIn(ctx, cash));
            vm.Lines.Add(LineVm.Header(s.Get("savings.takeOut", "amount", ctx.Fmt.Coins(balance))));
            vm.Lines.Add(TakeOut(ctx, balance));

            vm.Lines.Add(LineVm.Header(s.Get("savings.standing")));
            bool always = SavingsRules.Always(sim);
            vm.Lines.Add(StandingRow(ctx, "ask", !always, false));
            vm.Lines.Add(StandingRow(ctx, "always", always, true));
            vm.Lines.Add(new LineVm(s.Get("savings.standingNote"), null, StatementSheet.HubWhy, HubSheet.Standing));
            return vm;
        }

        /// <summary>
        /// Put in: drag or type 1,000 to 1,00,000 (never more than your cash). It starts at the cash the next close won't need, marked in amber,
        /// and the line under it says what's left in cash against what the close takes (owner, 2026-11-13).
        /// </summary>
        private static LineVm PutIn(ViewContext ctx, long cash)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LedgerDef d = sim.Config.Game.Ledger;
            long max = System.Math.Min(cash, d.SavingsMaxPerMove);
            if (max < d.SavingsMinimum)
                return new LineVm(s.Get("savings.putNone", "min", ctx.Fmt.Coins(d.SavingsMinimum)), null) { Dim = true };
            long due = 0;
            foreach (CloseDue c in CloseDues.Next(sim)) due += c.Amount;
            long spare = cash - due;
            var amount = new AmountVm("in", d.SavingsMinimum, max, d.SavingsStep, spare / d.SavingsStep * d.SavingsStep, a => PutPick(ctx, a, cash, due, max), ctx.Fmt.Coins)
            {
                MinCaption = ctx.Fmt.Amount(d.SavingsMinimum),
                MaxCaption = s.Get(max == cash ? "savings.maxCash" : "savings.maxMove", "amount", ctx.Fmt.Amount(max)),
            };
            if (due > 0 && spare > d.SavingsMinimum && spare < max)
            {
                amount.Mark = spare;
                amount.MarkCaption = s.Get("savings.keepMark", "amount", ctx.Fmt.Amount(due));
            }
            return new LineVm(s.Get("savings.range", "min", ctx.Fmt.Amount(d.SavingsMinimum), "max", ctx.Fmt.Amount(d.SavingsMaxPerMove)), null) { Amount = amount };
        }

        private static AmountPick PutPick(ViewContext ctx, long amount, long cash, long due, long max)
        {
            var s = ctx.Strings;
            LedgerDef d = ctx.Sim.Config.Game.Ledger;
            string bad = amount < d.SavingsMinimum ? s.Get("savings.tooLittle", "min", ctx.Fmt.Coins(d.SavingsMinimum))
                : amount > cash ? s.Get("savings.moreThanCash", "max", ctx.Fmt.Coins(max))
                : amount > d.SavingsMaxPerMove ? s.Get("savings.tooMuch", "max", ctx.Fmt.Coins(d.SavingsMaxPerMove)) : null;
            if (bad != null) return Refused("in", s.Get("savings.putButton"), bad);
            long left = cash - amount;
            var pick = new AmountPick
            {
                Action = ActionVm.Checked(ctx, "in", s.Get("savings.putAmount", "amount", ctx.Fmt.Amount(amount)), null, ActionKind.Primary, at => new DepositSavings(at, amount)),
            };
            if (due == 0) { pick.Note = s.Get("savings.leavesCash", "left", ctx.Fmt.Coins(left)); return pick; }
            bool enough = left >= due;
            pick.Note = enough ? s.Get("savings.leavesEnough", "left", ctx.Fmt.Coins(left), "due", ctx.Fmt.Coins(due))
                : s.Get("savings.leavesShort", "left", ctx.Fmt.Coins(left), "short", ctx.Fmt.Coins(due - left));
            pick.Tone = enough ? Tone.Good : Tone.Warn;
            return pick;
        }

        /// <summary>Take out: drag or type 1,000 to 1,00,000 (never more than the deposit), starting at 1,000; under 1,000 left, all of it at once.</summary>
        private static LineVm TakeOut(ViewContext ctx, long balance)
        {
            var s = ctx.Strings;
            LedgerDef d = ctx.Sim.Config.Game.Ledger;
            if (balance <= 0) return new LineVm(s.Get("savings.takeNone"), ctx.Fmt.Coins(0)) { Dim = true };
            if (balance < d.SavingsMinimum)
                return new LineVm(ctx.Fmt.Coins(balance), null)
                {
                    Note = s.Get("savings.allSmall"),
                    Action = ActionVm.Checked(ctx, "out:all", s.Get("savings.takeAll", "amount", ctx.Fmt.Amount(balance)), null, ActionKind.Secondary, at => new WithdrawSavings(at, balance)),
                };
            long max = System.Math.Min(balance, d.SavingsMaxPerMove);
            var amount = new AmountVm("out", d.SavingsMinimum, max, d.SavingsStep, d.SavingsMinimum, a => TakePick(ctx, a, balance, max), ctx.Fmt.Coins)
            {
                MinCaption = ctx.Fmt.Amount(d.SavingsMinimum),
                MaxCaption = s.Get(max == balance ? "savings.maxSavings" : "savings.maxMove", "amount", ctx.Fmt.Amount(max)),
            };
            return new LineVm(s.Get("savings.range", "min", ctx.Fmt.Amount(d.SavingsMinimum), "max", ctx.Fmt.Amount(d.SavingsMaxPerMove)), null) { Amount = amount };
        }

        private static AmountPick TakePick(ViewContext ctx, long amount, long balance, long max)
        {
            var s = ctx.Strings;
            LedgerDef d = ctx.Sim.Config.Game.Ledger;
            string bad = amount < d.SavingsMinimum ? s.Get("savings.tooLittle", "min", ctx.Fmt.Coins(d.SavingsMinimum))
                : amount > balance ? s.Get("savings.moreThanSavings", "max", ctx.Fmt.Coins(max))
                : amount > d.SavingsMaxPerMove ? s.Get("savings.tooMuch", "max", ctx.Fmt.Coins(d.SavingsMaxPerMove)) : null;
            if (bad != null) return Refused("out", s.Get("savings.takeButton"), bad);
            return new AmountPick
            {
                Action = ActionVm.Checked(ctx, "out", s.Get("savings.takeAmount", "amount", ctx.Fmt.Amount(amount)), null, ActionKind.Secondary, at => new WithdrawSavings(at, amount)),
                Note = s.Get("savings.leavesSavings", "left", ctx.Fmt.Coins(balance - amount)),
            };
        }

        private static AmountPick Refused(string id, string label, string why)
        {
            return new AmountPick { Action = new ActionVm(id, label, null, ActionKind.Primary, false, null, null), Note = why, Tone = Tone.Warn };
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
