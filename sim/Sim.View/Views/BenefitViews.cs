using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// Benefits and the night shift (spec 028 §3.8, Preview 18 screen 7): a Benefits section on a business's Settings tab and on People for your hands, each benefit with its cost, effect and a
    /// switch, and the night shift's row. A benefit's <i>Why?</i> carries its "Does it pay?" in your own numbers.
    /// </summary>
    public static class BenefitSheet
    {
        public static string Key(Benefit b) { return b.ToString().ToLowerInvariant(); }

        /// <summary>The wage a worker of the employer costs a season: the first one's, else the role's.</summary>
        public static long WageOf(ViewContext ctx, long employer)
        {
            var staff = BenefitRules.Staff(ctx.Sim, employer);
            if (staff.Count > 0) return staff[0].Wage;
            return employer == 0 ? ctx.Sim.Config.Game.People.Hand.MarketWage : BusinessRules.Wage(ctx.Sim, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(employer)));
        }

        public static void AddSection(ViewContext ctx, SheetVm vm, long employer)
        {
            Simulation sim = ctx.Sim;
            if (!BenefitRules.On(sim)) return;
            var s = ctx.Strings;
            BenefitsDef d = BenefitRules.Defs(sim);
            int flags = BenefitRules.FlagsOfEmployer(sim, employer);
            string who = employer == 0 ? s.Get("benefit.forHands") : s.Get("benefit.forRole", "role", s.Get("business.role." + BusinessRules.TypeOf(sim, sim.State.Businesses.Find(employer)).Worker));
            vm.Lines.Add(LineVm.Header(s.Get("benefit.header", "who", who)));
            foreach (Benefit b in BenefitRules.All)
            {
                Benefit bit = b;
                bool on = (flags & (int)b) != 0;
                string k = Key(b);
                var line = new LineVm(s.Get("benefit." + k + ".name"), s.Get(on ? "benefit.on" : "benefit.off"), "benefit", employer * 16 + (int)b)
                    { Icon = "coin", Note = s.Get("benefit." + k + ".note", "cost", ctx.Fmt.Coins(BenefitRules.Cost(sim, b))) };
                long emp = employer;
                line.Action = ActionVm.Checked(ctx, "benefit:" + k, s.Get(on ? "benefit.stop" : "benefit.give"), null, ActionKind.Secondary, at => new SetBenefit(at, emp, bit, !on));
                vm.Lines.Add(line);
            }
            int n = BenefitRules.Count(flags);
            long cost = BenefitRules.SeasonCost(sim, flags) + ((flags & (int)Benefit.Festival) != 0 ? BenefitRules.Cost(sim, Benefit.Festival) : 0);
            string poach = ctx.Fmt.Decimal1(sim.Config.Game.People.Hr.PoachChance * BenefitRules.LoyaltyOf(sim, flags) * Fixed.FromInt(100)) + "%";
            string kept = n >= d.KeptForReputation ? s.Get("benefit.keptRep", "pct", poach) : s.Get("benefit.keptPoach", "pct", poach);
            vm.Lines.Add(new LineVm(s.Get("benefit.kept", "n", ctx.Fmt.Integer(n)), s.Get("benefit.costSeason", "coins", ctx.Fmt.Coins(cost)), "loyalty", employer) { Note = kept });
        }

        /// <summary>The night shift's row on a business's Settings (mill, bakery, dairy, sweet shop): on, or offered, or why not yet.</summary>
        public static void AddNight(ViewContext ctx, SheetVm vm, Business b)
        {
            Simulation sim = ctx.Sim;
            if (!BenefitRules.On(sim) || b.Status != BusinessStatus.Open || !NightRules.Eligible(BusinessRules.TypeOf(sim, b))) return;
            var s = ctx.Strings;
            BenefitsDef d = BenefitRules.Defs(sim);
            long id = b.Id;
            vm.Lines.Add(LineVm.Header(s.Get("night.header")));
            Worker w = NightRules.Of(sim, b);
            if (w != null)
            {
                var on = new LineVm(s.Get("night.on"), s.Get("night.onWho", "name", PeopleWords.Name(ctx, w.Name), "from", ctx.Fmt.Integer(d.NightStart), "to", ctx.Fmt.Integer(d.NightEnd)), "night", id)
                    { Icon = "coin", Note = s.Get("night.onNote", "wage", ctx.Fmt.Coins(w.Wage), "tired", ctx.Fmt.Decimal2(d.NightFatigue)) };
                if (!NightRules.Grid(sim)) { on.Note = s.Get("night.noGridNote"); on.NoteTone = Tone.Warn; }
                on.Action = ActionVm.Checked(ctx, "night:stop", s.Get("night.stop"), null, ActionKind.Secondary, at => new SetNightShift(at, id, false));
                vm.Lines.Add(on);
                return;
            }
            int shorts = NightRules.ShortCount(sim, b);
            var line = new LineVm(s.Get(NightRules.Offered(sim, b) ? "night.offered" : "night.row"), s.Get("night.short", "n", ctx.Fmt.Integer(shorts), "of", ctx.Fmt.Integer(d.NightLookBack)), "night", id)
                { Icon = "coin", Note = s.Get("night.startNote", "wage", ctx.Fmt.Coins(NightRules.Wage(sim, b)), "need", ctx.Fmt.Integer(d.NightNeedDays)) };
            line.Action = ActionVm.Checked(ctx, "night:start", s.Get("night.start"), null, ActionKind.Secondary, at => new SetNightShift(at, id, true));
            vm.Lines.Add(line);
        }
    }
}
