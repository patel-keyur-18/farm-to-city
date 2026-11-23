using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// The tuition centre's sheets (US-244, spec 028 §3.5, ADR 0161, Preview 18): its market research, its evening and its fee. Every number has a *Why?* (the <c>tuition</c>
    /// source: the research rows are the row itself, the business's rows are business × 100 + row).
    /// </summary>
    public static class TuitionSheet
    {
        public const string Why = "tuition";
        public const int Kids = 1, FeeRef = 2, Places = 3, Pupils = 4, Batches = 5, FeesIn = 6, Stars = 7, Seasons = 8, FeeNow = 9, Cost = 10;

        internal static void AddResearch(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TuitionDef d = t.Tuition;
            long fee = TuitionRules.FeeAt(sim, d, Fixed.One);
            vm.Lines.Add(new LineVm(s.Get("research.tuitionKids"), s.Get("research.tuitionKidsValue", "n", ctx.Fmt.Integer(TuitionRules.Expected(sim, d, Fixed.One)), "ages", d.AgeMin + "–" + d.AgeMax), Why, Kids) { Icon = "school" });
            vm.Lines.Add(new LineVm(s.Get("research.tuitionFee"), s.Get("research.tuitionFeeValue", "fee", ctx.Fmt.Coins(fee)), Why, FeeRef) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("research.tuitionPlaces"), s.Get("research.tuitionPlacesValue", "n", ctx.Fmt.Integer(d.Capacity), "batches", ctx.Fmt.Integer(d.Batches), "size", ctx.Fmt.Integer(d.BatchSize)), Why, Places) { Icon = "school" });
            vm.Lines.Add(new LineVm(s.Get("research.wage", "role", s.Get("business.role." + t.Worker)), s.Get("research.wageValue", "wage", ctx.Fmt.Coins(BusinessRules.Wage(sim, t))), "businessWage", type)
                { Icon = "person_hand_m" });
            vm.Lines.Add(new LineVm(s.Get("research.tuitionHours"), BusinessWords.Hours(ctx, t.OpenHour, t.CloseHour), Why, Places) { Icon = "school" });
        }

        /// <summary>The centre's evening: pupils (yours and the old village's), the batches, the fees, the stars, and how far its pupils have come.</summary>
        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TuitionDef d = t.Tuition;
            long id = b.Id * 100;
            vm.Lines.Add(new LineVm(s.Get("tuition.pupils"), s.Get("tuition.pupilsValue", "n", ctx.Fmt.Integer(TuitionRules.Pupils(b)), "of", ctx.Fmt.Integer(d.Capacity)), Why, id + Pupils)
                { Icon = "school", Note = s.Get("tuition.pupilsNote", "yours", ctx.Fmt.Integer(b.Pupils.Count), "village", ctx.Fmt.Integer(b.CohortPupils)) });
            for (int k = 0; k < d.Batches; k++)
                vm.Lines.Add(new LineVm(s.Get("tuition.batch", "n", ctx.Fmt.Integer(k + 1), "from", ctx.Fmt.Integer(b.OpenHour + k * TuitionRules.BatchHours(b, d)), "to", ctx.Fmt.Integer(b.OpenHour + (k + 1) * TuitionRules.BatchHours(b, d))),
                    s.Get("tuition.batchValue", "n", ctx.Fmt.Integer(TuitionRules.InBatch(b, d, k)), "of", ctx.Fmt.Integer(d.BatchSize)), Why, id + Batches) { Icon = "school" });
            vm.Lines.Add(new LineVm(s.Get("tuition.feesIn"), ctx.Fmt.Coins(b.TuitionFees), Why, id + FeesIn) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("tuition.stars"), ShopWords.Stars(ctx, b.Stars), Why, id + Stars) { Icon = "star", Note = s.Get("tuition.starsNote", "pct", ctx.Fmt.Percent(TuitionRules.TakeUp(d, b.Stars))) });
            if (b.Pupils.Count > 0)
                vm.Lines.Add(new LineVm(s.Get("tuition.seasons"), s.Get("tuition.seasonsValue", "n", ctx.Fmt.Decimal1(AverageSeasons(sim, b)), "of", ctx.Fmt.Integer(d.GainMax)), Why, id + Seasons) { Icon = "school" });
        }

        /// <summary>The average seasons of tuition your pupils here have sat.</summary>
        public static Fixed AverageSeasons(Simulation sim, Business b)
        {
            if (b.Pupils.Count == 0) return Fixed.Zero;
            long n = 0;
            foreach (long p in b.Pupils) n += sim.State.Citizens?.Citizen(p)?.TuitionSeasons ?? 0;
            return Fixed.FromRatio(n, b.Pupils.Count);
        }

        /// <summary>The fee setting: its preset cycles on a tap, and says when it takes effect.</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            TuitionDef d = t.Tuition;
            long id = b.Id;
            int pi = 0;
            for (int i = 0; i < d.Presets.Count; i++) if (d.Presets[i] == b.PriceFactor) pi = i;
            int next = (pi + 1) % d.Presets.Count;
            var fee = new LineVm(s.Get("tuition.fee"), Preset(ctx, d, ctx.Sim, b.PriceFactor), Why, id * 100 + FeeNow) { Icon = "coin", Note = s.Get("tuition.feeNote", "fee", ctx.Fmt.Coins(TuitionRules.FeeNow(ctx.Sim, b))) };
            fee.Action = ActionVm.Checked(ctx, "fee", s.Get("business.change"), Preset(ctx, d, ctx.Sim, d.Presets[next]), ActionKind.Secondary, at => new SetTuitionFee(at, id, next));
            vm.Lines.Add(fee);
        }

        /// <summary>A preset as words: "Reference" or "10% dearer" / "10% cheaper".</summary>
        public static string Preset(ViewContext ctx, TuitionDef d, Simulation sim, Fixed factor)
        {
            var s = ctx.Strings;
            if (factor == Fixed.One) return s.Get("tuition.presetRef");
            return factor < Fixed.One ? s.Get("tuition.presetLow", "pct", ctx.Fmt.Percent(Fixed.One - factor)) : s.Get("tuition.presetHigh", "pct", ctx.Fmt.Percent(factor - Fixed.One));
        }
    }
}
