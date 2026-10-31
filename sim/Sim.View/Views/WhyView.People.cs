using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>The kinds of row on the away card (spec 008 §3.9), each with its own Why?.</summary>
    public enum AwayRow
    {
        Ready = 0,
        Sales = 1,
        DayLabour = 2,
        HomeUse = 3,
        Spoiled = 4,
        Pests = 5,
        Sick = 6,
        Seasons = 7,
        Index = 8,
        People = 9,
        Truck = 10,
        Orders = 11,
        Held = 12,

        /// <summary>The dairy: milk paid by the co-op, calves sold, fodder bought or sold standing, vet visits (spec 012).</summary>
        Dairy = 13,

        /// <summary>Your people (spec 014 §7): a hand retired, a house built, rent paid or missed, a family left.</summary>
        Town = 14,
    }

    /// <summary>The time, founder, people and away sources (specs 001, 003, 006, 008 §3.9).</summary>
    public static partial class WhyView
    {
        private static WhyVm CalendarWhy(ViewContext ctx, long id)
        {
            ClockDef c = ctx.Sim.Clock;
            CalendarDate d = Calendar.From(ctx.Now, c);
            WhyVm vm = New(ctx, "why.calendar.title", ctx.Fmt.Date(d, c.DaysPerSeason));
            Row(vm, ctx, "why.calendar.season", ctx.Strings.Plural("why.days", c.DaysPerSeason));
            Row(vm, ctx, "why.calendar.year", ctx.Strings.Plural("why.days", c.DaysPerSeason * 4));
            Text(vm, ctx, "why.calendar.clock");
            Text(vm, ctx, "why.calendar.away", "hours", ctx.Fmt.Integer(c.AwayCapHours));
            vm.Footnote = ctx.Strings.Get("why.calendar.lesson");
            return vm;
        }

        /// <summary>A forecast run (spec 001 §3.6): id = the run's first hour.</summary>
        private static WhyVm Forecast(ViewContext ctx, long id)
        {
            long ahead = id - ctx.Now / 3600;
            int bucket = ahead <= 24 ? 0 : ahead <= 48 ? 1 : 2;
            WhyVm vm = New(ctx, "why.forecast.title", null);
            Row(vm, ctx, "why.forecast.right", ctx.Fmt.Percent(ctx.Sim.Clock.ForecastAccuracy[bucket]), "day", ctx.Strings.Get("why.forecast.day." + bucket));
            Text(vm, ctx, "why.forecast.runs");
            vm.Footnote = ctx.Strings.Get("why.forecast.lesson");
            return vm;
        }

        private static WhyVm Saved(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.saved.title", null);
            Text(vm, ctx, "why.saved.when");
            Text(vm, ctx, "why.saved.backups");
            return vm;
        }

        private static WhyVm SimpleWhy(ViewContext ctx, string key)
        {
            WhyVm vm = New(ctx, "why." + key + ".title", null);
            Text(vm, ctx, "why." + key + ".what");
            return vm;
        }

        private static WhyVm ImportFiles(ViewContext ctx, long id) { return SimpleWhy(ctx, "importFiles"); }

        private static WhyVm PlayLogWhy(ViewContext ctx, long id) { return SimpleWhy(ctx, "playLog"); }

        private static WhyVm TextSize(ViewContext ctx, long id) { return SimpleWhy(ctx, "textSize"); }

        // ---- The founder ------------------------------------------------------------------------------------------------------

        private static WhyVm Labour(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LabourBreakdown b = FounderRules.Breakdown(sim);
            WhyVm vm = New(ctx, "why.labour.title", Hours(ctx, b.Total));
            Row(vm, ctx, "why.labour.hours", Hours(ctx, b.Hours));
            Row(vm, ctx, "why.labour.food", Effect(ctx, b.Nutrition));
            Row(vm, ctx, "why.labour.tired", Effect(ctx, Fixed.One - b.Fatigue));
            Row(vm, ctx, "why.labour.health", Effect(ctx, b.Health));
            if (b.Sick) Text(vm, ctx, "why.labour.sick");
            if (b.MandiCut > Fixed.Zero) Row(vm, ctx, "why.labour.mandi", "−" + Hours(ctx, b.MandiCut));
            Row(vm, ctx, "why.labour.own", Hours(ctx, b.OwnFields));
            if (b.DayLabour > Fixed.Zero) Row(vm, ctx, "why.labour.neighbours", Hours(ctx, b.DayLabour));
            vm.Footnote = ctx.Strings.Get("why.labour.lesson");
            return vm;
        }

        private static WhyVm Health(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            FounderState s = sim.State.Farm.Founder;
            FounderDef def = sim.Config.Game.Farm.Founder;
            WhyVm vm = New(ctx, "why.health.title", ctx.Rating(s.Health));
            Row(vm, ctx, "why.health.now", ctx.Fmt.Percent(s.Health));
            Row(vm, ctx, "why.health.rest", "+" + ctx.Fmt.Percent(def.RecoveryPerDay));
            Fixed food = def.GoodNutrition - FounderRules.Nutrition(sim);
            if (food > Fixed.Zero) Row(vm, ctx, "why.health.food", "−" + ctx.Fmt.Percent(def.NutritionPenalty * food));
            if (s.Fatigue > def.SafeFatigue) Row(vm, ctx, "why.health.tired", "−" + ctx.Fmt.Percent(def.FatiguePenalty * (s.Fatigue - def.SafeFatigue)));
            if (SeasonNow(ctx) == Season.Winter && sim.Config.Game.Farm.DietPlans[s.DietPlan].Id == "subsistence")
                Row(vm, ctx, "why.health.cold", "−" + ctx.Fmt.Percent(def.NoWarmClothingPenalty));
            Text(vm, ctx, "why.health.sick");
            vm.Footnote = ctx.Strings.Get("why.health.lesson");
            return vm;
        }

        private static WhyVm Fatigue(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            FounderState s = sim.State.Farm.Founder;
            WhyVm vm = New(ctx, "why.fatigue.title", ctx.Rating(Fixed.One - s.Fatigue));
            Row(vm, ctx, "why.fatigue.now", ctx.Fmt.Percent(s.Fatigue));
            foreach (WorkHoursDef w in sim.Config.Game.Farm.WorkHours)
                Row(vm, ctx, "why.fatigue.option", (w.FatigueChange > Fixed.Zero ? "+" : w.FatigueChange < Fixed.Zero ? "−" : "") + ctx.Fmt.Percent(w.FatigueChange < Fixed.Zero ? -w.FatigueChange : w.FatigueChange),
                    "option", ctx.Strings.Get("hours." + w.Id));
            vm.Footnote = ctx.Strings.Get("why.fatigue.lesson");
            return vm;
        }

        /// <summary>A food plan's row: id = the plan's index.</summary>
        private static WhyVm Diet(ViewContext ctx, long id)
        {
            DietPlanDef p = ctx.Sim.Config.Game.Farm.DietPlans[(int)id];
            WhyVm vm = New(ctx, "why.diet.title", Money(ctx, MarketRules.Indexed(ctx.Sim, p.CostPerSeason)), "plan", ctx.Strings.Get(p.NameKey));
            Row(vm, ctx, "why.diet.work", Effect(ctx, p.Nutrition));
            if (p.FoodShare > Fixed.Zero) Row(vm, ctx, "why.diet.food", ctx.Fmt.Percent(p.FoodShare));
            Text(vm, ctx, "why.diet.paid");
            vm.Footnote = ctx.Strings.Get("why.diet.lesson");
            return vm;
        }

        /// <summary>A work-hours row: id = the option's index.</summary>
        private static WhyVm WorkHoursWhy(ViewContext ctx, long id)
        {
            WorkHoursDef w = ctx.Sim.Config.Game.Farm.WorkHours[(int)id];
            WhyVm vm = New(ctx, "why.hours.title", Hours(ctx, w.Hours), "option", ctx.Strings.Get("hours." + w.Id));
            Row(vm, ctx, "why.fatigue.option", (w.FatigueChange > Fixed.Zero ? "+" : w.FatigueChange < Fixed.Zero ? "−" : "") + ctx.Fmt.Percent(w.FatigueChange < Fixed.Zero ? -w.FatigueChange : w.FatigueChange),
                "option", ctx.Strings.Get("hours." + w.Id));
            vm.Footnote = ctx.Strings.Get("why.fatigue.lesson");
            return vm;
        }

        /// <summary>A day-labour share row: id = the share in percent.</summary>
        private static WhyVm DayLabour(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Fixed hours = FounderRules.LabourRate(sim).MulRatio(id, 100);
            long wage = MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour);
            WhyVm vm = New(ctx, "why.dayLabour.title", Money(ctx, Sim.Core.Finance.Money.MulRate(wage, hours)));
            Row(vm, ctx, "why.dayLabour.wage", Money(ctx, wage));
            Row(vm, ctx, "why.dayLabour.hours", Hours(ctx, hours));
            Text(vm, ctx, "why.dayLabour.fields");
            vm.Footnote = ctx.Strings.Get("why.dayLabour.lesson");
            return vm;
        }

        private static WhyVm Medicine(ViewContext ctx, long id)
        {
            FounderDef def = ctx.Sim.Config.Game.Farm.Founder;
            WhyVm vm = New(ctx, "why.medicine.title", Money(ctx, MarketRules.Indexed(ctx.Sim, def.MedicineCost)));
            Row(vm, ctx, "why.medicine.gain", "+" + ctx.Fmt.Percent(def.MedicineHealthGain));
            Text(vm, ctx, "why.medicine.once");
            return vm;
        }

        // ---- People -----------------------------------------------------------------------------------------------------------

        /// <summary>A hire row: id = how many labourers (ADR 0049 §3).</summary>
        private static WhyVm Hire(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int count = (int)id;
            long until = PeopleRules.LabourUntil(sim);
            WhyVm vm = New(ctx, "why.hire.title", Money(ctx, PeopleRules.LabourCost(sim, count, until)));
            Row(vm, ctx, "why.hire.wage", Money(ctx, MarketRules.Indexed(sim, sim.Config.Game.People.Pool.DailyWage)));
            Row(vm, ctx, "why.hire.until", ctx.Fmt.Duration(System.Math.Max(0, until - ctx.Now)));
            Text(vm, ctx, "why.hire.care");
            Text(vm, ctx, "why.hire.leave");
            if (sim.State.Farm.Crops.Exists(c => c.State == Sim.Core.Farm.CropState.Growing && sim.Config.Game.Farm.Crops[c.Def].Fodder)) Text(vm, ctx, "why.hire.fodder");
            vm.Footnote = ctx.Strings.Get("why.care.lesson");
            return vm;
        }

        private static Hand HandOf(ViewContext ctx, long id) { return ctx.Sim.State.People.Hands.Find(h => h.Id == id); }

        private static WhyVm HandSkill(ViewContext ctx, long id)
        {
            Hand h = HandOf(ctx, id);
            int skill = h.Skill.RoundToLong() > int.MaxValue ? int.MaxValue : (int)h.Skill.RoundToLong();
            WhyVm vm = New(ctx, "why.handSkill.title", PeopleWords.Band(ctx, skill));
            Row(vm, ctx, "why.handSkill.seasons", ctx.Fmt.Integer(h.SeasonsWorked));
            Text(vm, ctx, "why.handSkill.grows");
            vm.Footnote = ctx.Strings.Get("why.handSkill.lesson");
            return vm;
        }

        private static WhyVm StaffOutput(ViewContext ctx, long id) { return Output(ctx, HandOf(ctx, id)); }

        private static WhyVm StaffOutputAt(ViewContext ctx, long id) { return Output(ctx, ctx.Sim.State.People.WorkerAt(id)); }

        /// <summary>Output = skill × motivation × (1 − tiredness) (spec 015 §3.4), each against a typical worker's, in words.</summary>
        private static WhyVm Output(ViewContext ctx, IStaff w)
        {
            Simulation sim = ctx.Sim;
            Fixed relative = WorkRules.RelativeOutput(sim, w);
            string words = w.CourseUntil != 0 ? ctx.Strings.Get("staff.awayOnCourse") : w.Hr.SickUntil != 0 ? ctx.Strings.Get("staff.offSick")
                : ctx.Strings.Get("staff.output." + StaffView.Band(ctx, relative));
            WhyVm vm = New(ctx, "why.staffOutput.title", words, "name", Name(ctx, w));
            if (w.CourseUntil != 0)
            {
                Text(vm, ctx, "why.staffOutput.course", "time", ctx.Fmt.Duration(StaffView.BackIn(ctx, w.CourseUntil)), "gain", ctx.Fmt.Integer(sim.Config.Game.People.Work.CourseGain));
                return vm;
            }
            if (w.Hr.SickUntil != 0)
            {
                Text(vm, ctx, "why.staffOutput.sick", "time", ctx.Fmt.Duration(StaffView.BackIn(ctx, w.Hr.SickUntil)));
                return vm;
            }
            HandDef h = sim.Config.Game.People.Hand;
            Fixed typical = h.ProductivityBase + h.ProductivityPerSkill * Fixed.FromInt(50).MulRatio(1, 100);
            Row(vm, ctx, "why.staffOutput.skill", Effect(ctx, WorkRules.SkillFactor(sim, w) / typical), "band", PeopleWords.Band(ctx, (int)w.Skill.RoundToLong()));
            Row(vm, ctx, "why.staffOutput.fair", Effect(ctx, Fixed.One + WorkRules.Fairness(sim, w)));
            Household home = WorkRules.HomeOf(sim, w);
            if (home != null) Row(vm, ctx, "why.staffOutput.home", Effect(ctx, Fixed.One + WorkRules.Home(sim, w)), "happiness", ctx.Fmt.Integer(home.Happiness.RoundToLong()));
            Fixed colleagues = WorkRules.Colleagues(sim, w);
            if (colleagues != Fixed.Zero) Row(vm, ctx, "why.staffOutput.colleagues", Effect(ctx, Fixed.One + colleagues));
            Fixed mood = HrRules.Mood(sim, w);
            if (mood != Fixed.Zero) Row(vm, ctx, w.Hr.RaiseRefusals > 0 && HrRules.Underpaid(sim, w) ? "why.staffOutput.raiseRefused" : "why.staffOutput.mood", Effect(ctx, Fixed.One + mood));
            Row(vm, ctx, "why.staffOutput.motivation", Effect(ctx, WorkRules.Motivation(sim, w)));
            Row(vm, ctx, "why.staffOutput.fatigue", Tired(ctx, w));
            Row(vm, ctx, "why.staffOutput.total", Effect(ctx, relative));
            Text(vm, ctx, "why.staffOutput.formula");
            vm.Footnote = ctx.Strings.Get("why.staffOutput.lesson");
            return vm;
        }

        /// <summary>"Rested", or tiredness's effect on their work ("15% less").</summary>
        private static string Tired(ViewContext ctx, IStaff w) { return w.Fatigue == Fixed.Zero ? ctx.Strings.Get("staff.rested") : Effect(ctx, Fixed.One - w.Fatigue); }

        private static string WholePct(ViewContext ctx, Fixed share) { return ctx.Fmt.Integer((share * Fixed.FromInt(100)).RoundToLong()) + "%"; }

        /// <summary>Employer reputation (spec 015 §3.8): what moves it and what it does now.</summary>
        private static WhyVm ReputationWhy(ViewContext ctx, long id)
        {
            var d = ctx.Sim.Config.Game.People.Reputation;
            int r = ctx.Sim.State.People.Reputation;
            WhyVm vm = New(ctx, "why.reputation.title", StaffView.Reputation(ctx));
            Text(vm, ctx, "why.reputation.up", "renewal", Signed(d.FairRenewal), "sick", Signed(d.SickPaid), "advance", Signed(d.Advance), "severance", Signed(d.Severance));
            Text(vm, ctx, "why.reputation.down", "low", Signed(d.LowHire), "raise", Signed(d.RaiseRefused), "probation", Signed(d.ProbationLetGo), "lapse", Signed(d.Lapse),
                "batch", Signed(d.BadBatchSold));
            Text(vm, ctx, r >= d.High ? "why.reputation.high" : r <= d.Low ? "why.reputation.low" : "why.reputation.middle", "high", ctx.Fmt.Integer(d.High), "low", ctx.Fmt.Integer(d.Low));
            vm.Footnote = ctx.Strings.Get("why.reputation.lesson");
            return vm;
        }

        private static string Signed(int n) { return (n > 0 ? "+" : n < 0 ? "−" : "") + System.Math.Abs(n); }

        private static string Name(ViewContext ctx, IStaff w) { return PeopleWords.Name(ctx, w is Hand h ? h.Name : ((Worker)w).Name); }

        /// <summary>A hand's long days: the overtime a day, and what tiredness does (spec 015 §3.4).</summary>
        private static WhyVm StaffLongDays(ViewContext ctx, long id)
        {
            Hand h = HandOf(ctx, id);
            var work = ctx.Sim.Config.Game.People.Work;
            int normal = (int)ctx.Sim.Config.Game.People.Hand.HoursPerDay.RoundToLong();
            WhyVm vm = New(ctx, "why.staffLongDays.title", ctx.Strings.Get("staff.longDaysOff", "hours", ctx.Fmt.Integer(h.LongDays ? normal + work.LongDayHours : normal)));
            Text(vm, ctx, "why.staffLongDays.pay", "hours", ctx.Fmt.Integer(work.LongDayHours), "amount", Money(ctx, WorkRules.OvertimeDay(ctx.Sim, h)));
            Text(vm, ctx, "why.staffLongDays.tired", "up", WholePct(ctx, work.FatigueUp), "down", WholePct(ctx, work.FatigueDown), "max", WholePct(ctx, work.FatigueMax));
            Row(vm, ctx, "why.staffLongDays.now", Tired(ctx, h));
            vm.Footnote = ctx.Strings.Get("why.staffLongDays.lesson");
            return vm;
        }

        private static WhyVm HandHours(ViewContext ctx, long id)
        {
            Hand h = HandOf(ctx, id);
            WhyVm vm = New(ctx, "why.handHours.title", Hours(ctx, PeopleRules.HandHours(ctx.Sim, h)));
            Text(vm, ctx, "why.handHours.skill");
            Text(vm, ctx, "why.handHours.acre");
            return vm;
        }

        private static WhyVm HandWage(ViewContext ctx, long id)
        {
            Hand h = HandOf(ctx, id);
            WhyVm vm = New(ctx, "why.handWage.title", Money(ctx, h.Wage));
            Text(vm, ctx, "why.handWage.paid");
            Text(vm, ctx, "why.handWage.market");
            // Housing in kind (spec 014 §3.6): "30,000 − 6,000 for the family house = 24,000 in cash".
            long house = MigrationRules.HousingInKind(ctx.Sim, h);
            if (h.HousingInKind > 0)
                Row(vm, ctx, "why.handWage.inKind", Money(ctx, h.Wage), "wage", Money(ctx, h.Wage + h.HousingInKind), "house", Money(ctx, h.HousingInKind));
            else if (house > 0) Text(vm, ctx, "why.handWage.inKindNext", "house", Money(ctx, house));
            vm.Footnote = ctx.Strings.Get("why.handWage.lesson");
            return vm;
        }

        private static WhyVm HandLapse(ViewContext ctx, long id)
        {
            Hand h = HandOf(ctx, id);
            WhyVm vm = New(ctx, "why.handLapse.title", Money(ctx, h.Wage));
            Text(vm, ctx, "why.handLapse.what");
            Text(vm, ctx, "why.handLapse.backPay");
            return vm;
        }

        private static WhyVm HandPrepaid(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.handPrepaid.title", null);
            Text(vm, ctx, "why.handPrepaid.what");
            vm.Footnote = ctx.Strings.Get("why.handPrepaid.lesson");
            return vm;
        }

        private static WhyVm Hut(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.hut.title", null);
            Text(vm, ctx, "why.hut.what");
            Text(vm, ctx, "why.hut.time");
            return vm;
        }

        private static WhyVm Applicant(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.applicant.title", null);
            Text(vm, ctx, "why.applicant.range");
            Text(vm, ctx, "why.applicant.interview");
            return vm;
        }

        private static WhyVm Order(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.order.title", null, "name", PeopleWords.Name(ctx, HandOf(ctx, id).Name));
            Text(vm, ctx, "why.order.what");
            Text(vm, ctx, "why.order.hour");
            Text(vm, ctx, "why.order.never");
            return vm;
        }

        /// <summary>A job's usual wage (spec 015 §3.1): the role's market wage at skill 50 and today's prices; the asked wage follows skill.</summary>
        private static WhyVm JobUsual(ViewContext ctx, long place)
        {
            WhyVm vm = New(ctx, "why.jobUsual.title", Money(ctx, HiringRules.AskedWage(ctx.Sim, place, Fixed.FromInt(50), false)), "role", HiringBoard.Role(ctx, place));
            Text(vm, ctx, place == 0 ? "why.jobUsual.hand" : "why.jobUsual.business");
            Text(vm, ctx, HiringRules.Farms(ctx.Sim, place) ? "why.jobUsual.farming" : "why.jobUsual.crafting");
            Text(vm, ctx, "why.jobUsual.skill");
            return vm;
        }

        /// <summary>An offer's wage (spec 015 §3.3): the asked wage from the skill, and what each offer does.</summary>
        private static WhyVm JobWage(ViewContext ctx, long id)
        {
            Applicant a = ctx.Sim.State.People.Applicant(id);
            if (a == null) return New(ctx, "reject.UnknownApplicant", null);
            long asked = HiringRules.OfferWage(ctx.Sim, a, OfferLevel.Asked);
            WhyVm vm = New(ctx, "why.jobWage.title", Money(ctx, asked), "name", PeopleWords.Name(ctx, a.Name));
            Row(vm, ctx, "why.jobWage.usual", Money(ctx, HiringRules.AskedWage(ctx.Sim, a.Place, Fixed.FromInt(50), false)));
            PeopleRules.SkillRange(ctx.Sim, a, out int lo, out int hi);
            Row(vm, ctx, "why.jobWage.theirSkill", PeopleWords.Range(ctx, lo, hi), "name", PeopleWords.Name(ctx, a.Name));
            Text(vm, ctx, "why.jobWage.skill");
            if (a.AboveMarket) Text(vm, ctx, "why.jobWage.above");
            Text(vm, ctx, a.CitizenId != 0 ? "why.jobWage.town" : "why.jobWage.migrant");
            Text(vm, ctx, "why.jobWage.low");
            Text(vm, ctx, "why.jobWage.high");
            vm.Footnote = ctx.Strings.Get("why.jobWage.lesson");
            return vm;
        }

        private static WhyVm JobReference(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.jobReference.title", null);
            Text(vm, ctx, "why.jobReference.what");
            Text(vm, ctx, "why.jobReference.terms");
            vm.Footnote = ctx.Strings.Get("why.jobReference.lesson");
            return vm;
        }

        private static WhyVm Post(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.post.title", null);
            Text(vm, ctx, "why.post.what");
            Text(vm, ctx, "why.post.above");
            return vm;
        }

        private static WhyVm OrderMin(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.orderMin.title", null);
            Text(vm, ctx, "why.orderMin.what");
            Text(vm, ctx, "why.orderMin.floor");
            return vm;
        }

        private static WhyVm FreeLabourers(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.free.title", ctx.Fmt.Integer(PeopleRules.FreeNow(ctx.Sim)));
            Text(vm, ctx, "why.free.pool");
            Text(vm, ctx, "why.free.season");
            return vm;
        }

        /// <summary>An away card row (spec 008 §3.9): id = the row's kind (<see cref="AwayRow"/>).</summary>
        private static WhyVm Away(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.away.title", null);
            Text(vm, ctx, "why.away." + (AwayRow)id);
            vm.Footnote = ctx.Strings.Get("why.away.lesson");
            return vm;
        }
    }
}
