using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>Insurance's <em>Why?</em>s (spec 027 §7): a premium's parts, a claim's luck and threshold, what each policy covers. Words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        private static WhyVm InsuranceCropWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            CropInsuranceDef d = sim.Config.Game.Insurance.Crop;
            bool on = InsuranceRules.State(sim).CropOn;
            WhyVm vm = New(ctx, "why.insCrop.title", s.Get(on ? "insurance.on" : "insurance.off"));
            Row(vm, ctx, "why.insCrop.policy", s.Get("why.insCrop.policyValue"));
            Row(vm, ctx, "why.insCrop.pays", s.Get("why.insCrop.paysValue", "excess", Pct(ctx, Fixed.One - d.Threshold)), "excess", Pct(ctx, Fixed.One - d.Threshold));
            Row(vm, ctx, "why.insCrop.premium", s.Get("why.insCrop.premiumValue", "pct", Pct(ctx, InsuranceSheet.AverageRate(sim))));
            Text(vm, ctx, "why.insCrop.text");
            return vm;
        }

        private static WhyVm InsurancePropertyWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            PropertyInsuranceDef d = sim.Config.Game.Insurance.Property;
            WhyVm vm = New(ctx, "why.insProp.title", Money(ctx, InsuranceRules.PropertyPremium(sim)));
            Row(vm, ctx, "why.insProp.worth", Money(ctx, InsuranceRules.PropertyValue(sim)));
            Row(vm, ctx, "why.insProp.rate", Money(ctx, InsuranceRules.PropertyPremium(sim)), "pct", Pct(ctx, d.Rate));
            Row(vm, ctx, "why.insProp.pays", Pct(ctx, Fixed.One - d.Excess));
            Row(vm, ctx, "why.insProp.carry", Pct(ctx, d.Excess));
            Text(vm, ctx, "why.insProp.text");
            return vm;
        }

        private static WhyVm InsuranceBusinessWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null) return New(ctx, "why.insBiz.title", "");
            BusinessInsuranceDef d = sim.Config.Game.Insurance.Business;
            long profit = InsuranceRules.LastProfit(b);
            WhyVm vm = New(ctx, "why.insBiz.title", Money(ctx, InsuranceRules.BusinessPremium(sim, b)));
            Row(vm, ctx, "why.insBiz.worth", Money(ctx, InsuranceRules.BusinessBook(b)));
            Row(vm, ctx, "why.insBiz.rate", Money(ctx, Money_(InsuranceRules.BusinessBook(b), d.Rate)), "pct", Pct(ctx, d.Rate));
            Row(vm, ctx, "why.insBiz.profit", Money(ctx, System.Math.Max(0, profit)));
            Row(vm, ctx, "why.insBiz.profitPart", Money(ctx, Money_(System.Math.Max(0, profit), d.ProfitShare)), "pct", Pct(ctx, d.ProfitShare));
            Row(vm, ctx, "why.insBiz.total", s.Get("why.insBiz.totalValue", "amount", Money(ctx, InsuranceRules.BusinessPremium(sim, b))));
            Row(vm, ctx, "why.insBiz.fire", Pct(ctx, Fixed.One - d.Excess));
            Row(vm, ctx, "why.insBiz.day", Money(ctx, InsuranceRules.LostPaid(sim, b, sim.Clock.SecondsPerSeason / 4)), "pct", Pct(ctx, d.DailyProfitShare));
            Text(vm, ctx, "why.insBiz.text");
            return vm;
        }

        private static long Money_(long amount, Fixed rate) { return Sim.Core.Finance.Money.MulRate(amount, rate); }

        /// <summary>A claim: what hit, what was insured, and the sum it came to.</summary>
        private static WhyVm InsuranceClaimWhy(ViewContext ctx, long index)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            InsuranceState st = InsuranceRules.State(sim);
            if (st == null || index < 0 || index >= st.Claims.Count) return New(ctx, "why.insClaim.title", "");
            Claim c = st.Claims[(int)index];
            InsuranceDef d = sim.Config.Game.Insurance;
            WhyVm vm = New(ctx, "why.insClaim.title", Money(ctx, c.Amount));
            Row(vm, ctx, "why.insClaim.what", InsuranceSheet.ClaimLabel(ctx, c));
            if (c.Policy == Claim.CropPolicy)
            {
                Row(vm, ctx, "why.insClaim.luck", s.Get("why.insClaim.outOf100", "n", ctx.Fmt.Integer(c.Points)));
                Row(vm, ctx, "why.insClaim.under", s.Get("why.insClaim.outOf100", "n", ctx.Fmt.Integer((int)(d.Crop.Threshold * Fixed.FromInt(100)).RoundToLong())));
                Row(vm, ctx, "why.insClaim.sum", Money(ctx, c.Amount), "sum", Money(ctx, c.Basis), "gap", ctx.Fmt.Integer((int)(d.Crop.Threshold * Fixed.FromInt(100)).RoundToLong() - c.Points));
                Text(vm, ctx, "why.insClaim.textCrop");
            }
            else
            {
                long repair = Sim.Core.Finance.Money.MulRate(c.Basis, Fixed.FromRatio(c.Points, 100));
                if (c.Cause > 0 && sim.Config.Game.Events.Events[c.Cause - 1].Id == "supplier_strike")
                {
                    Row(vm, ctx, "why.insClaim.pause", Money(ctx, c.Amount), "days", ctx.Fmt.Integer(c.Days), "pct", ctx.Fmt.Integer(c.Points));
                }
                else
                {
                    Row(vm, ctx, "why.insClaim.bill", Money(ctx, c.Basis));
                    Row(vm, ctx, "why.insClaim.share", Money(ctx, repair), "pct", ctx.Fmt.Integer(c.Points));
                    if (c.Days > 0 && c.Amount > repair) Row(vm, ctx, "why.insClaim.lost", Money(ctx, c.Amount - repair), "days", ctx.Fmt.Integer(c.Days));
                }
                Text(vm, ctx, "why.insClaim.textFire");
            }
            return vm;
        }

        /// <summary>A crop's premium on the sow sheet: the harvest's value, the pest part, the event part and the loading.</summary>
        private static WhyVm InsurancePremiumWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            InsuranceSheet.PremiumParts(id, out int tileId, out int ci, out int fi);
            Tile tile = sim.State.Land.Tiles[tileId];
            FarmDefs farm = sim.Config.Game.Farm;
            CropDef crop = farm.Crops[ci];
            long sum = FarmRules.Estimate(sim, tile, crop, farm.Fertilisers[fi]).Get;
            CropInsuranceDef d = sim.Config.Game.Insurance.Crop;
            int repeats = PestRules.RepeatsAt(sim, tileId, ci);
            Fixed pest = InsuranceRules.PestPart(sim, ci, repeats);
            long pestMoney = Sim.Core.Finance.Money.MulRate(sum, pest), eventMoney = Sim.Core.Finance.Money.MulRate(sum, d.EventPart);
            long premium = InsuranceRules.CropPremium(sim, sum, ci, repeats);
            WhyVm vm = New(ctx, "why.insPremium.title", Money(ctx, premium));
            Row(vm, ctx, "why.insPremium.sum", Money(ctx, sum));
            Row(vm, ctx, "why.insPremium.pests", Money(ctx, pestMoney));
            Row(vm, ctx, "why.insPremium.events", Money(ctx, eventMoney), "pct", Pct(ctx, d.EventPart));
            Row(vm, ctx, "why.insPremium.loading", s.Get("why.insPremium.loadingValue", "pct", Pct(ctx, d.Loading - Fixed.One)));
            Row(vm, ctx, "why.insPremium.total", Money(ctx, premium));
            Text(vm, ctx, "why.insPremium.text");
            return vm;
        }

        /// <summary>The property premium among the next close's dues.</summary>
        private static WhyVm InsuranceDueWhy(ViewContext ctx, long id) { return InsurancePropertyWhy(ctx, id); }
    }
}
