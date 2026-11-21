using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Insurance on the screens (spec 027 §7, US-155): the Money page's section with the two switches and the last claims, a business's row, and the sow sheet's line.
    /// The switches cost nothing when tapped, so they have no confirm card. Everything here reads the state.
    /// </summary>
    public static class InsuranceSheet
    {
        public const string CropWhy = "insCrop", PropertyWhy = "insProp", BusinessWhy = "insBiz", ClaimWhy = "insClaim", PremiumWhy = "insPremium", DueWhy = "insDue";

        /// <summary>The average premium share of a crop's value across the crops sowable now (for "about 4%").</summary>
        public static Fixed AverageRate(Simulation sim)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            int season = (int)Sim.Core.Time.Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            Fixed sum = Fixed.Zero;
            int n = 0;
            for (int i = 0; i < farm.Crops.Count; i++)
            {
                CropDef c = farm.Crops[i];
                if (c.Fodder || !c.SowSeasons[season]) continue;
                sum += InsuranceRules.CropRate(sim, i, 0);
                n++;
            }
            return n == 0 ? Fixed.Zero : sum / Fixed.FromInt(n);
        }

        /// <summary>The Money page's Insurance section: crop and property switches, then the last claims.</summary>
        public static void Add(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!InsuranceRules.Available(sim)) return;
            var s = ctx.Strings;
            InsuranceState st = InsuranceRules.State(sim);
            InsuranceDef d = InsuranceRules.Def(sim);
            vm.Lines.Add(LineVm.Header(s.Get("insurance.title")));

            bool crop = st.CropOn;
            vm.Lines.Add(new LineVm(s.Get("insurance.crop"), null, CropWhy, 0)
            {
                Icon = "shield", Note = crop ? s.Get("insurance.cropOn", "pct", ctx.Fmt.Percent(AverageRate(sim)), "excess", ctx.Fmt.Percent(Fixed.One - d.Crop.Threshold)) : s.Get("insurance.cropOff"),
                Action = Switch(ctx, "cropIns", crop, at => new SetCropInsurance(at, !crop)),
            });

            bool prop = st.Property.On;
            long value = InsuranceRules.PropertyValue(sim);
            int houses = InsuranceRules.Insurable(sim).Count;
            var row = new LineVm(s.Get("insurance.property"), houses == 0 ? s.Get("insurance.noBuildings") : prop ? s.Get("insurance.propertyValue", "n", ctx.Strings.Plural("insurance.buildings", houses), "premium", ctx.Fmt.Coins(InsuranceRules.PropertyPremium(sim)))
                : s.Get("insurance.propertyHas", "n", ctx.Strings.Plural("insurance.buildings", houses)), PropertyWhy, 0)
            {
                Icon = "shield", Action = Switch(ctx, "propIns", prop, at => new SetPropertyInsurance(at, !prop)),
            };
            PolicyNote(ctx, row, st.Property, prop, d.Property.Excess, "property");
            vm.Lines.Add(row);

            if (st.Claims.Count == 0) return;
            vm.Lines.Add(LineVm.Header(s.Get("insurance.claims", "n", ctx.Fmt.Integer(d.KeepClaims))));
            for (int i = st.Claims.Count - 1; i >= 0; i--)
            {
                Claim c = st.Claims[i];
                vm.Lines.Add(new LineVm(ClaimLabel(ctx, c), s.Get("insurance.claimPaid", "amount", ctx.Fmt.Coins(c.Amount)), ClaimWhy, i)
                {
                    Icon = "shield", Note = s.Get("insurance.claimNote", "when", ReportSheet.PeriodName(ctx, c.At / sim.Clock.SecondsPerSeason), "policy", s.Get("insurance.policy." + c.Policy)),
                });
            }
        }

        /// <summary>A business's row (the Today tab, under its money): the switch, what it pays, and what it costs.</summary>
        public static void AddBusiness(ViewContext ctx, SheetVm vm, Business b)
        {
            Simulation sim = ctx.Sim;
            if (!InsuranceRules.Available(sim) || b.Status != BusinessStatus.Open) return;
            var s = ctx.Strings;
            InsuranceDef d = InsuranceRules.Def(sim);
            Policy p = InsuranceRules.State(sim).BusinessPolicy(b.Id);
            bool on = p != null && p.On;
            long id = b.Id;
            vm.Lines.Add(LineVm.Header(s.Get("insurance.bizTitle")));
            var row = new LineVm(s.Get("insurance.biz"), s.Get("insurance.bizPremium", "premium", ctx.Fmt.Coins(InsuranceRules.BusinessPremium(sim, b))), BusinessWhy, b.Id)
            {
                Icon = "shield", Action = Switch(ctx, "bizIns", on, at => new SetBusinessInsurance(at, id, !on)),
            };
            PolicyNote(ctx, row, p ?? new Policy(), on, d.Business.Excess, "business");
            vm.Lines.Add(row);
        }

        /// <summary>The status under a switch: when it starts, what it pays; off, what that leaves you carrying.</summary>
        private static void PolicyNote(ViewContext ctx, LineVm row, Policy p, bool on, Fixed excess, string kind)
        {
            var s = ctx.Strings;
            if (on && !p.Covers(ctx.Sim.Period)) { row.Note = s.Get("insurance.startsNext"); row.NoteTone = Tone.Warn; }
            else if (on) { row.Note = s.Get("insurance." + kind + "On", "pct", ctx.Fmt.Percent(Fixed.One - excess), "days", ctx.Fmt.Percent(InsuranceRules.Def(ctx.Sim).Business.DailyProfitShare)); row.NoteTone = Tone.Good; }
            else row.Note = s.Get(p.PaidSeason == ctx.Sim.Period ? "insurance.offPaid" : "insurance." + kind + "Off");
        }

        /// <summary>The On / Off button: it shows the policy's state and taps to flip it (no cost, so no card).</summary>
        private static ActionVm Switch(ViewContext ctx, string id, bool on, System.Func<long, Command> make)
        {
            return ActionVm.Checked(ctx, id, ctx.Strings.Get(on ? "insurance.on" : "insurance.off"), null, on ? ActionKind.Primary : ActionKind.Secondary, make);
        }

        /// <summary>"Fire, the cattle shed", "Hail, wheat": what hit and what was hit.</summary>
        public static string ClaimLabel(ViewContext ctx, Claim c)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            string cause = c.Cause > 0 ? s.Get("insurance.cause." + sim.Config.Game.Events.Events[c.Cause - 1].Id) : s.Get(c.Cause < 0 ? "insurance.cause.pests" : "insurance.cause.weather");
            string what;
            if (c.Policy == Claim.CropPolicy) what = s.Get(sim.Config.Game.Farm.Crops[(int)c.Target].NameKey);
            else if (c.Policy == Claim.PropertyPolicy)
            {
                Sim.Core.Land.Building b = sim.State.Land.Buildings.Find(x => x.Id == c.Target);
                what = b == null ? s.Get("event.aBuilding") : s.Get(sim.Config.Game.Buildings[b.Def].NameKey);
            }
            else
            {
                Business biz = sim.State.Businesses?.Find(c.Target);
                what = biz == null ? s.Get("insurance.aBusiness") : BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, biz));
            }
            return s.Get("insurance.claimLabel", "cause", cause, "what", what.ToLowerInvariant());
        }

        /// <summary>The sow sheet's line under a crop: its premium and when it pays, or that it will go uninsured for want of cash. Null when crop insurance is off.</summary>
        public static void SowLine(ViewContext ctx, CropRowVm row, Sim.Core.Land.Tile tile, int cropIndex, long sum, long cashNeeded, int fertIndex)
        {
            Simulation sim = ctx.Sim;
            if (!InsuranceRules.Available(sim) || !InsuranceRules.State(sim).CropOn) return;
            CropDef c = sim.Config.Game.Farm.Crops[cropIndex];
            if (c.Fodder || sum <= 0) return;
            var s = ctx.Strings;
            long premium = InsuranceRules.CropPremium(sim, sum, cropIndex, PestRules.RepeatsAt(sim, tile.Id, cropIndex));
            if (premium <= 0) return;
            bool can = sim.State.Ledger.CanAfford(cashNeeded + premium);
            row.InsuranceText = can ? s.Get("sow.insured", "premium", ctx.Fmt.Coins(premium), "excess", ctx.Fmt.Percent(Fixed.One - InsuranceRules.Def(sim).Crop.Threshold)) : s.Get("sow.notInsured");
            row.InsuranceWarn = !can;
            row.InsuranceWhyKey = PremiumWhy;
            row.InsuranceWhyId = PremiumId(tile.Id, cropIndex, fertIndex);
        }

        public static long PremiumId(int tile, int crop, int fert) { return (tile * 100L + crop) * 100 + fert; }

        public static void PremiumParts(long id, out int tile, out int crop, out int fert)
        {
            fert = (int)(id % 100);
            crop = (int)(id / 100 % 100);
            tile = (int)(id / 10000);
        }
    }
}
