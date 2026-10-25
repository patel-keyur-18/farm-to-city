using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Your people's sources (spec 014 §7): the town's happiness and work, a household's money and needs, a citizen's age and health.</summary>
    public static partial class WhyView
    {
        private static WhyVm TownHappiness(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.townHappiness.title", Num(ctx, HappinessRules.Town(sim)));
            foreach (Household x in sim.State.Citizens.Households)
                vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("town.familyN", "name", TownWords.Household(ctx, x), "n", ctx.Fmt.Integer(x.Members.Count)), Num(ctx, x.Happiness)));
            Text(vm, ctx, "why.townHappiness.mean");
            vm.Footnote = ctx.Strings.Get("why.townHappiness.lesson");
            return vm;
        }

        private static WhyVm Employment(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.employment.title", Pct(ctx, HouseholdRules.EmploymentRate(ctx.Sim)));
            Text(vm, ctx, "why.employment.what");
            Row(vm, ctx, "why.employment.outside", Money(ctx, HouseholdRules.OutsideDayPay(ctx.Sim)));
            vm.Footnote = ctx.Strings.Get("why.employment.lesson");
            return vm;
        }

        private static Household HouseholdOf(ViewContext ctx, long id) { return ctx.Sim.State.Citizens.Household(id); }

        private static WhyVm HouseholdIncome(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdIncome.title", Money(ctx, x.Income));
            bool hut = HouseholdRules.InHut(sim, x);
            foreach (long m in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(m);
                if (c.Job == CitizenJob.Hand && sim.State.People.Hand(c.HandId) is Hand h)
                {
                    Row(vm, ctx, "why.householdIncome.wage", Money(ctx, h.Wage), "name", TownWords.Name(ctx, c));
                    if (hut) Row(vm, ctx, "why.householdIncome.remit", "−" + Money(ctx, Sim.Core.Finance.Money.MulRate(h.Wage, sim.Config.Game.Population.RemitShare)));
                }
                else if (HouseholdRules.WorksOutside(sim, c))
                    Row(vm, ctx, "why.householdIncome.outside", Money(ctx, HouseholdRules.OutsideSeasonPay(sim)), "name", TownWords.Name(ctx, c));
            }
            vm.Footnote = ctx.Strings.Get("why.householdIncome.lesson");
            return vm;
        }

        private static WhyVm HouseholdBudget(ViewContext ctx, long id)
        {
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdBudget.title", Money(ctx, x.Income));
            Text(vm, ctx, "why.householdBudget.engel");
            foreach (PieSliceVm p in HouseholdSheet.Pie(ctx, x)) vm.Rows.Add(new WhyRowVm(p.Label + " · " + ctx.Fmt.Integer(p.Percent) + "%", p.Value));
            if (x.Food < x.FoodNeed) Row(vm, ctx, "why.householdBudget.short", Money(ctx, x.FoodNeed));
            vm.Footnote = ctx.Strings.Get("why.householdBudget.lesson");
            return vm;
        }

        private static WhyVm HouseholdSavings(ViewContext ctx, long id)
        {
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdSavings.title", Money(ctx, x.Savings));
            Row(vm, ctx, "why.householdSavings.saved", Money(ctx, x.Saved));
            Text(vm, ctx, "why.householdSavings.first");
            vm.Footnote = ctx.Strings.Get("why.householdSavings.lesson");
            return vm;
        }

        private static WhyVm HouseholdRent(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdRent.title", HouseholdSheet.RentText(ctx, x));
            if (HouseholdRules.InHut(sim, x)) Text(vm, ctx, "why.householdRent.hut");
            else if (x.Rent == 0) Text(vm, ctx, "why.householdRent.inKind");
            else
            {
                Row(vm, ctx, "why.householdRent.season", Money(ctx, MarketRules.Indexed(sim, sim.Config.Game.Population.RentPerSeason)));
                Text(vm, ctx, "why.householdRent.how");
            }
            vm.Footnote = ctx.Strings.Get("why.householdRent.lesson");
            return vm;
        }

        private static WhyVm HouseholdHome(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdHome.title", TownWords.Home(ctx, x.HomeId));
            Row(vm, ctx, "why.householdHome.room", ctx.Strings.Get("why.householdHome.roomValue", "n", ctx.Fmt.Integer(x.Members.Count), "cap", ctx.Fmt.Integer(HappinessRules.Capacity(sim, x))));
            Row(vm, ctx, "household.rent", HouseholdSheet.RentText(ctx, x));
            vm.Footnote = ctx.Strings.Get("why.householdHome.lesson");
            return vm;
        }

        private static WhyVm HouseholdHappiness(ViewContext ctx, long id)
        {
            PopulationDefs p = ctx.Sim.Config.Game.Population;
            Household x = HouseholdOf(ctx, id);
            WhyVm vm = New(ctx, "why.householdHappiness.title", Num(ctx, x.Happiness));
            Row(vm, ctx, "needs.survival", Pct(ctx, x.Survival));
            Row(vm, ctx, "needs.security", Pct(ctx, x.Security));
            Row(vm, ctx, "needs.belonging", Pct(ctx, x.Belonging));
            Row(vm, ctx, "needs.growth", Pct(ctx, x.Growth));
            Text(vm, ctx, "why.householdHappiness.weights", "a", Pct(ctx, p.SurvivalWeight), "b", Pct(ctx, p.SecurityWeight), "c", Pct(ctx, p.BelongingWeight), "d", Pct(ctx, p.GrowthWeight));
            Text(vm, ctx, "why.householdHappiness.gate", "gate", Pct(ctx, p.Gate));
            vm.Footnote = ctx.Strings.Get("why.householdHappiness.lesson");
            return vm;
        }

        private static WhyVm CitizenAge(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            PopulationDefs p = sim.Config.Game.Population;
            Citizen c = sim.State.Citizens.Citizen(id);
            WhyVm vm = New(ctx, "why.citizenAge.title", ctx.Fmt.Integer(CitizenRules.Age(sim, c)));
            Text(vm, ctx, "why.citizenAge.season", "n", ctx.Fmt.Integer(p.LifeYearsPerSeason));
            Text(vm, ctx, "why.citizenAge.stages", "adult", ctx.Fmt.Integer(p.AdultFrom), "elder", ctx.Fmt.Integer(p.ElderFrom));
            Text(vm, ctx, "why.citizenAge.retire", "age", ctx.Fmt.Integer(p.RetireAge));
            vm.Footnote = ctx.Strings.Get("why.citizenAge.lesson");
            return vm;
        }

        private static WhyVm CitizenHealth(ViewContext ctx, long id)
        {
            Citizen c = ctx.Sim.State.Citizens.Citizen(id);
            WhyVm vm = New(ctx, "why.citizenHealth.title", ctx.Rating(c.Health));
            Text(vm, ctx, "why.citizenHealth.food");
            Text(vm, ctx, "why.citizenHealth.later");
            return vm;
        }

        private static WhyVm CitizenWork(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Citizen c = sim.State.Citizens.Citizen(id);
            WhyVm vm = New(ctx, "why.citizenJob.title", TownWords.Job(ctx, c));
            if (HouseholdRules.WorksOutside(sim, c))
            {
                Row(vm, ctx, "why.citizenJob.outside", Money(ctx, HouseholdRules.OutsideDayPay(sim)));
                Text(vm, ctx, "why.citizenJob.hire");
            }
            else Text(vm, ctx, HouseholdRules.IsChild(sim, c) ? "why.citizenJob.child" : "why.citizenJob.elder");
            return vm;
        }

        private static WhyVm HandHome(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Citizen me = sim.State.Citizens.OfHand(id);
            Household x = sim.State.Citizens.Household(me.HouseholdId);
            WhyVm vm = New(ctx, "why.handHome.title", TownWords.Home(ctx, x.HomeId));
            if (HouseholdRules.InHut(sim, x)) Row(vm, ctx, "why.handHome.remit", Pct(ctx, sim.Config.Game.Population.RemitShare), "village", TownWords.Village(ctx, me) ?? "");
            else Text(vm, ctx, "why.handHome.family");
            return vm;
        }
    }
}
