using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The tuition centre (US-244): every number on its research, its evening and its fee explained in words.</summary>
    public static partial class WhyView
    {
        /// <summary>A child's seasons of tuition: what each adds and the cap.</summary>
        private static WhyVm CitizenTuition(ViewContext ctx, long id)
        {
            Sim.Core.People.Citizen c = ctx.Sim.State.Citizens.Citizen(id);
            TuitionDef d = System.Linq.Enumerable.First(BusinessRules.Defs(ctx.Sim).Types, x => x.IsTuition).Tuition;
            WhyVm vm = New(ctx, "why.citizenTuition.title", ctx.Strings.Plural("citizen.tuitionValue", c.TuitionSeasons));
            Text(vm, ctx, "why.citizenTuition.text", "name", TownWords.Name(ctx, c), "gain", ctx.Fmt.Integer(d.GainPerSeason), "max", ctx.Fmt.Integer(d.GainMax), "total", ctx.Fmt.Integer(c.TuitionSeasons * d.GainPerSeason));
            return vm;
        }

        /// <summary>A tuition row: the research rows (id = the row) and the business's rows (id = business × 100 + row).</summary>
        private static WhyVm TuitionWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int row = (int)(id % 100);
            Business b = id >= 100 ? BusinessOf(ctx, id / 100) : null;
            BusinessTypeDef t = b != null ? BusinessRules.TypeOf(sim, b) : System.Linq.Enumerable.First(BusinessRules.Defs(sim).Types, x => x.IsTuition);
            TuitionDef d = t.Tuition;
            long fee = TuitionRules.FeeAt(sim, d, Fixed.One);
            string budget = ctx.Fmt.Percent(sim.Config.Game.Population.EducationShare);
            switch (row)
            {
                case TuitionSheet.Kids:
                {
                    WhyVm vm = New(ctx, "why.tuitionKids.title", ctx.Fmt.Integer(TuitionRules.Expected(sim, d, Fixed.One)));
                    Text(vm, ctx, "why.tuitionKids.text", "ages", d.AgeMin + "–" + d.AgeMax, "budget", budget, "fee", ctx.Fmt.Coins(fee), "take", ctx.Fmt.Percent(TuitionRules.TakeUp(d, d.StartStars)), "stars", ctx.Fmt.Decimal1(d.StartStars));
                    return vm;
                }
                case TuitionSheet.FeeRef:
                {
                    WhyVm vm = New(ctx, "why.tuitionFeeRef.title", ctx.Fmt.Coins(fee));
                    Text(vm, ctx, "why.tuitionFeeRef.text", "budget", budget);
                    return vm;
                }
                case TuitionSheet.Places:
                {
                    WhyVm vm = New(ctx, "why.tuitionPlaces.title", ctx.Fmt.Integer(d.Capacity));
                    Text(vm, ctx, "why.tuitionPlaces.text", "size", ctx.Fmt.Integer(d.BatchSize), "batches", ctx.Fmt.Integer(d.Batches), "hours", ctx.Fmt.Integer(t.CloseHour - t.OpenHour));
                    return vm;
                }
                case TuitionSheet.Pupils:
                {
                    WhyVm vm = New(ctx, "why.tuitionPupils.title", b == null ? "0" : ctx.Fmt.Integer(TuitionRules.Pupils(b)));
                    Text(vm, ctx, "why.tuitionPupils.text", "ages", d.AgeMin + "–" + d.AgeMax, "budget", budget, "take", ctx.Fmt.Percent(TuitionRules.TakeUp(d, b == null ? d.StartStars : b.Stars)));
                    if (b != null) { Row(vm, ctx, "why.tuitionPupils.yours", ctx.Fmt.Integer(b.Pupils.Count)); Row(vm, ctx, "why.tuitionPupils.village", ctx.Fmt.Integer(b.CohortPupils)); }
                    return vm;
                }
                case TuitionSheet.Batches:
                {
                    WhyVm vm = New(ctx, "why.tuitionBatches.title", b == null ? "0" : ctx.Fmt.Integer(TuitionRules.Pupils(b)));
                    Text(vm, ctx, "why.tuitionBatches.text", "size", ctx.Fmt.Integer(d.BatchSize));
                    return vm;
                }
                case TuitionSheet.FeesIn:
                {
                    WhyVm vm = New(ctx, "why.tuitionFees.title", Money(ctx, b == null ? 0 : b.TuitionFees));
                    Text(vm, ctx, "why.tuitionFees.text", "fee", ctx.Fmt.Coins(b == null ? fee : TuitionRules.FeeAt(sim, d, b.Fee)), "days", ctx.Fmt.Integer(sim.Clock.DaysPerSeason), "books", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, d.SuppliesPerPupil)));
                    return vm;
                }
                case TuitionSheet.Stars:
                {
                    WhyVm vm = New(ctx, "why.tuitionStars.title", ShopWords.Stars(ctx, b == null ? d.StartStars : b.Stars));
                    Text(vm, ctx, "why.tuitionStars.text", "low", ctx.Fmt.Percent(d.TakeUpLow), "take", ctx.Fmt.Percent(TuitionRules.TakeUp(d, b == null ? d.StartStars : b.Stars)));
                    if (b != null)
                    {
                        Row(vm, ctx, "why.tuitionStars.full", ctx.Fmt.Percent(Fixed.FromRatio(TuitionRules.Pupils(b), d.Capacity)));
                        Row(vm, ctx, "why.tuitionStars.stayed", ctx.Fmt.Percent(TuitionRules.Stayed(sim, b)));
                        Row(vm, ctx, "why.tuitionStars.target", ShopWords.Stars(ctx, TuitionRules.Target(sim, b)));
                    }
                    return vm;
                }
                case TuitionSheet.Seasons:
                {
                    WhyVm vm = New(ctx, "why.tuitionSeasons.title", ctx.Strings.Get("tuition.seasonsShort", "n", b == null ? "0" : ctx.Fmt.Decimal1(TuitionSheet.AverageSeasons(sim, b))));
                    Text(vm, ctx, "why.tuitionSeasons.text", "gain", ctx.Fmt.Integer(d.GainPerSeason), "max", ctx.Fmt.Integer(d.GainMax));
                    return vm;
                }
                default:
                {
                    WhyVm vm = New(ctx, "why.tuitionFee.title", b == null ? ctx.Fmt.Coins(fee) : TuitionSheet.Preset(ctx, d, sim, b.PriceFactor));
                    Text(vm, ctx, "why.tuitionFee.text", "fee", ctx.Fmt.Coins(b == null ? fee : TuitionRules.FeeNow(sim, b)));
                    return vm;
                }
            }
        }
    }
}
