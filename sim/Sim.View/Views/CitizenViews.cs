using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// A household's needs (spec 014 §3.8, §3.13): one row per need with how well it's met and, under it, what would help or that it
    /// can't be changed yet, so a low number never comes without a way to raise it.
    /// </summary>
    public static class NeedsView
    {
        public static List<LineVm> Rows(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var rows = new List<LineVm>();

            rows.Add(LineVm.Header(s.Get("needs.survival")));
            rows.Add(Row(ctx, "needs.food", x.FoodMet, x.FoodMet >= Fixed.One ? s.Get("needs.foodMet")
                : s.Get("needs.foodShort", "pct", ctx.Fmt.Percent(x.FoodMet))));
            int capacity = HappinessRules.Capacity(sim, x);
            rows.Add(Row(ctx, "needs.shelter", HappinessRules.Shelter(sim, x), x.Members.Count <= capacity ? s.Get("needs.shelterMet")
                : s.Get("needs.shelterShort", "n", ctx.Fmt.Integer(x.Members.Count), "cap", ctx.Fmt.Integer(capacity))));
            rows.Add(Row(ctx, "needs.water", Fixed.One, s.Get("needs.waterMet")));

            rows.Add(LineVm.Header(s.Get("needs.security")));
            HappinessRules.Jobs(sim, x, out int inTown, out int adults);
            Fixed job = HappinessRules.Job(sim, x);
            rows.Add(Row(ctx, "needs.job", job, adults == 0 ? s.Get("needs.jobNone")
                : inTown == adults ? s.Get("needs.jobMet", "n", ctx.Fmt.Integer(inTown), "of", ctx.Fmt.Integer(adults))
                : s.Get("needs.jobShort", "n", ctx.Fmt.Integer(inTown), "of", ctx.Fmt.Integer(adults))));
            rows.Add(Row(ctx, "needs.healthCare", sim.Config.Game.Population.Chemist, s.Get("needs.healthCareNotYet")));
            rows.Add(Row(ctx, "needs.rent", x.RentBehind == 0 ? Fixed.One : Fixed.Zero, x.RentBehind == 0 ? s.Get("needs.rentMet")
                : s.Plural("needs.rentBehind", x.RentBehind)));

            rows.Add(LineVm.Header(s.Get("needs.belonging")));
            rows.Add(Row(ctx, "needs.worship", sim.Config.Game.Population.Belonging, s.Get("needs.worshipNotYet")));

            rows.Add(LineVm.Header(s.Get("needs.growth")));
            rows.Add(new LineVm(s.Get("needs.school"), s.Get("needs.notCounted")) { Note = s.Get("needs.schoolNotYet") });
            rows.Add(Row(ctx, "needs.leisure", x.Growth, x.Leisure > 0 ? s.Get("needs.leisureMet") : s.Get("needs.leisureShort")));
            return rows;
        }

        private static LineVm Row(ViewContext ctx, string key, Fixed met, string lever)
        {
            return new LineVm(ctx.Strings.Get(key), ctx.Fmt.Percent(met)) { Note = lever, NoteTone = met >= Fixed.Parse("0.8") ? Tone.Good : Tone.Warn };
        }
    }
}
