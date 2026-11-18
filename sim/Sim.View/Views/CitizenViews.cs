using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

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
            rows.Add(Row(ctx, x.Id, "needs.food", x.FoodMet, x.FoodMet >= Fixed.One ? s.Get("needs.foodMet")
                : s.Get("needs.foodShort", "pct", ctx.Fmt.Percent(x.FoodMet))));
            int capacity = HappinessRules.Capacity(sim, x);
            rows.Add(Row(ctx, x.Id, "needs.shelter", HappinessRules.Shelter(sim, x), x.Members.Count <= capacity ? s.Get("needs.shelterMet")
                : s.Get("needs.shelterShort", "n", ctx.Fmt.Integer(x.Members.Count), "cap", ctx.Fmt.Integer(capacity))));
            rows.Add(Row(ctx, x.Id, "needs.water", Fixed.One, s.Get("needs.waterMet")));

            rows.Add(LineVm.Header(s.Get("needs.security")));
            HappinessRules.Jobs(sim, x, out int inTown, out int adults);
            Fixed job = HappinessRules.Job(sim, x);
            rows.Add(Row(ctx, x.Id, "needs.job", job, adults == 0 ? s.Get("needs.jobNone")
                : inTown == adults ? s.Get("needs.jobMet", "n", ctx.Fmt.Integer(inTown), "of", ctx.Fmt.Integer(adults))
                : s.Get("needs.jobShort", "n", ctx.Fmt.Integer(inTown), "of", ctx.Fmt.Integer(adults))));
            rows.Add(Row(ctx, x.Id, "needs.healthCare", ClinicRules.HealthCareMet(sim, x, sim.Config.Game.Population.Chemist), HealthCareNote(ctx, x)));
            rows.Add(Row(ctx, x.Id, "needs.rent", x.RentBehind == 0 ? Fixed.One : Fixed.Zero, x.RentBehind == 0 ? s.Get("needs.rentMet")
                : s.Plural("needs.rentBehind", x.RentBehind)));

            if (CivicRules.On(sim) && CivicRules.SafetyCounts(sim))
                rows.Add(Row(ctx, x.Id, "needs.safety", CivicRules.Safety(sim, x), CivicNote(ctx, x, CivicDef.Watchman)));

            rows.Add(LineVm.Header(s.Get("needs.belonging")));
            rows.Add(Row(ctx, x.Id, "needs.worship", sim.Config.Game.Population.Belonging, s.Get("needs.worshipNotYet")));
            if (CivicRules.On(sim) && CivicRules.Built(sim, CivicDef.Hall))
                rows.Add(Row(ctx, x.Id, "needs.hall", CivicRules.Belonging(sim, x, sim.Config.Game.Population.Belonging), CivicNote(ctx, x, CivicDef.Hall)));

            rows.Add(LineVm.Header(s.Get("needs.growth")));
            if (SchoolRules.IsOpen(sim) && !SchoolRules.Met(sim, x))
            {
                Sim.Core.Land.Building far = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
                rows.Add(Row(ctx, x.Id, "needs.school", Fixed.Zero, s.Get("needs.schoolFar", "m", ctx.Fmt.Integer(CoverageRules.Metres(sim, far.TileId, SchoolRules.Def(sim).Tile)), "reach", ctx.Fmt.Integer(SchoolRules.Def(sim).Reach)))); // out of the school's reach: the children get no school year (spec 023 §3.6)
            }
            else if (SchoolRules.IsOpen(sim)) rows.Add(Row(ctx, x.Id, "needs.school", Fixed.One, s.Get("needs.schoolMet"))); // spec 018 §3.2
            else rows.Add(new LineVm(s.Get("needs.school"), s.Get("needs.notCounted")) { Note = s.Get("needs.schoolNotYet") });
            rows.Add(Row(ctx, x.Id, "needs.leisure", x.Leisure > 0 ? Fixed.One : Fixed.Zero, x.Leisure > 0 ? s.Get("needs.leisureMet") : s.Get("needs.leisureShort")));
            if (CivicRules.On(sim) && CivicRules.Built(sim, CivicDef.Playground) && HasChildren(sim, x))
                rows.Add(Row(ctx, x.Id, "needs.play", CivicRules.Leisure(sim, x, Fixed.Zero), CivicNote(ctx, x, CivicDef.Playground)));
            return rows;
        }

        private static bool HasChildren(Simulation sim, Household x)
        {
            foreach (long id in x.Members) if (HouseholdRules.IsChild(sim, sim.State.Citizens.Citizen(id))) return true;
            return false;
        }

        /// <summary>A civic building's row words (spec 023 §3.6, §3.7): nobody in post, out of reach, a service that has slipped, or at work.</summary>
        private static string CivicNote(ViewContext ctx, Household x, int civic)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            string id = CivicSheet.Ids[civic];
            if (!CivicRules.Staffed(sim, civic)) return s.Get("needs." + id + "NoPost");
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home != null && !CivicRules.Covered(sim, civic, home.TileId))
                return s.Get("needs.civicFar", "what", s.Get("civic." + id + ".name"), "m", ctx.Fmt.Integer(CoverageRules.Metres(sim, home.TileId, CivicRules.Def(sim, civic).Tile)), "reach", ctx.Fmt.Integer(CivicRules.Def(sim, civic).Reach));
            return CivicRules.Condition(sim) < Fixed.One ? s.Get("needs.civicSlipped", "what", s.Get("civic." + id + ".name"), "pct", ctx.Fmt.Percent(CivicRules.Condition(sim))) : s.Get("needs." + id + "Works");
        }

        /// <summary>The Health care row's words (spec 023 §3.6): the chemist alone, the clinic with no doctor, the clinic out of reach, or the clinic at work.</summary>
        private static string HealthCareNote(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!ClinicRules.On(sim) || !ClinicRules.IsOpen(sim)) return s.Get(ClinicRules.On(sim) && ClinicRules.Unlocked(sim) ? "needs.healthCareNoClinic" : "needs.healthCareNotYet");
            if (!ClinicRules.InPost(sim)) return s.Get("needs.healthCareNoDoctor");
            Sim.Core.Land.Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home != null && !ClinicRules.Covered(sim, home.TileId))
                return s.Get("needs.healthCareFar", "m", ctx.Fmt.Integer(CoverageRules.Metres(sim, home.TileId, ClinicRules.Def(sim).Tile)), "reach", ctx.Fmt.Integer(ClinicRules.Def(sim).Reach));
            return s.Get("needs.healthCareClinic");
        }

        // Each need's Why? is the household's happiness, layer by layer.
        private static LineVm Row(ViewContext ctx, long household, string key, Fixed met, string lever)
        {
            return new LineVm(ctx.Strings.Get(key), ctx.Fmt.Percent(met), "household.happiness", household) { Note = lever, NoteTone = met >= Fixed.Parse("0.8") ? Tone.Good : Tone.Warn };
        }
    }
}
