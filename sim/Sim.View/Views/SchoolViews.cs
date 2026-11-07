using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// The school plot's sheet (spec 018 §7, ADR 0095 §5): before the school, its cost as a gift, when it's ready and its upkeep, with
    /// "Build from cash" and "With a loan"; while building, when it opens; once open, the school's pupils, teacher and upkeep.
    /// </summary>
    public static class SchoolSheet
    {
        public const string LoanKind = "SchoolLoan";

        /// <summary>Fills the school plot's place sheet.</summary>
        internal static void Fill(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!SchoolRules.On(sim)) return;
            var s = ctx.Strings;
            SchoolState st = SchoolRules.State(sim);
            SchoolDef d = SchoolRules.Def(sim);
            if (st.Status == SchoolStatus.Building)
            {
                vm.Icon = "school_building";
                vm.Title = s.Get("school.buildingTitle");
                vm.Subtitle = s.Get("school.buildingLine");
                vm.Lines.Add(new LineVm(s.Get("school.readyIn"), ctx.Fmt.Duration(st.ReadyAt - ctx.Now), "schoolCost"));
                return;
            }
            if (st.Status == SchoolStatus.Open)
            {
                vm.Icon = "school";
                vm.Title = s.Get("school.title");
                vm.Subtitle = s.Get("school.line");
                int pupils = SchoolRules.Pupils(sim).Count, teachers = SchoolRules.Teachers(sim);
                vm.Lines.Add(new LineVm(s.Get("school.pupils"), s.Get("school.pupilsValue", "n", ctx.Fmt.Integer(pupils), "seats", ctx.Fmt.Integer(teachers * d.SeatsPerTeacher)),
                    "schoolPupils") { Icon = "person_boy" });
                vm.Lines.Add(new LineVm(s.Get("school.teachers"), s.Plural("school.teachersValue", teachers), "schoolPupils") { Icon = "person_town_f" });
                vm.Lines.Add(new LineVm(s.Get("school.upkeep"), ctx.Fmt.Coins(SchoolRules.Upkeep(sim)), "schoolUpkeep"));
                return;
            }
            vm.Subtitle = s.Get(SchoolRules.Unlocked(sim) ? "place.school_plot.line" : "school.lockedLine");
            long price = SchoolRules.Price(sim);
            vm.Lines.Add(new LineVm(s.Get("school.cost"), ctx.Fmt.Coins(price), "schoolCost"));
            vm.Lines.Add(new LineVm(s.Get("school.readyIn"), ctx.Fmt.Duration(d.BuildHours * 3600L), "schoolCost"));
            vm.Lines.Add(new LineVm(s.Get("school.upkeep"), ctx.Fmt.Coins(SchoolRules.Upkeep(sim)), "schoolUpkeep"));
            vm.Actions.Add(ActionVm.Checked(ctx, "buildSchool", s.Get("school.buildCash"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new BuildSchool(at)));
            vm.Actions.Add(WithLoan(ctx));
        }

        /// <summary>"With a loan": the bank's terms, greyed with the sim's reason (before the Hamlet, built) or when the bank won't lend.</summary>
        private static ActionVm WithLoan(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            long price = SchoolRules.Price(sim), loan = Loan(sim);
            RejectReason why = SchoolRules.CanBuild(sim);
            string reason = why != RejectReason.None ? ctx.Reject(why) : loan == 0 ? ctx.Strings.Get("school.noLoan")
                : !sim.State.Ledger.CanAfford(price - loan) ? ctx.Reject(RejectReason.InsufficientFunds) : null;
            return new ActionVm("schoolLoan", ctx.Strings.Get("school.buildLoan"), loan == 0 ? null : ctx.Strings.Get("houseLoan.down", "amount", ctx.Fmt.Coins(price - loan)),
                ActionKind.Open, reason == null, reason, null, new SheetRequest(LoanKind, 0));
        }

        /// <summary>
        /// The most the co-op bank lends toward the school: its room on what you already have (the school adds nothing: it's the
        /// district's), in whole thousands, at most the price; 0 below its minimum.
        /// </summary>
        public static long Loan(Simulation sim)
        {
            if (sim.State.Credit == null) return 0;
            int bank = LandLoanSheet.Bank(sim);
            if (bank < 0) return 0;
            long loan = System.Math.Min(SchoolRules.Price(sim), CreditRules.Limit(sim, bank).Limit) / (1000 * Sim.Core.Finance.Money.Coin) * (1000 * Sim.Core.Finance.Money.Coin);
            return loan < sim.Config.Game.Credit.Lenders[bank].MinAmount ? 0 : loan;
        }

        /// <summary>Building the school with a co-op loan: what it lends, the down payment, one row per term.</summary>
        public static SheetVm LoanSheet(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long price = SchoolRules.Price(sim), loan = Loan(sim);
            int bank = LandLoanSheet.Bank(sim);
            var vm = new SheetVm
            {
                Icon = "school", Title = s.Get("school.loanTitle"),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            vm.Lines.Add(new LineVm(s.Get("school.loanWhy"), null));
            if (loan == 0)
            {
                vm.Lines.Add(new LineVm(s.Get("school.noLoan"), null));
                return vm;
            }
            LenderDef l = sim.Config.Game.Credit.Lenders[bank];
            foreach (int term in BorrowSheet.Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = BorrowSheet.Plan(sim, bank, loan, term);
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]),
                    "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]), "interest", ctx.Fmt.Coins(plan.TotalInterest)), "borrow", bank * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, "schoolLoan:" + term, s.Get("school.buildCash"), ctx.Fmt.Coins(price - loan), ActionKind.Primary,
                        at => new BuildSchool(at, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }
    }

    public static partial class WhyView
    {
        /// <summary>The school's cost (spec 018 §3.1, ADR 0095 §5): the building and its books, a gift to the village, when it opens.</summary>
        private static WhyVm SchoolCost(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            SchoolDef d = SchoolRules.Def(sim);
            SchoolState st = SchoolRules.State(sim);
            long price = st.Status == SchoolStatus.None ? SchoolRules.Price(sim) : st.Cost;
            WhyVm vm = New(ctx, "why.school.title", Money(ctx, price));
            Row(vm, ctx, "why.school.building", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.BuildingCost)));
            Row(vm, ctx, "why.school.equipment", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.EquipmentCost)));
            Row(vm, ctx, "why.school.hours", ctx.Fmt.Duration(d.BuildHours * 3600L));
            Text(vm, ctx, "why.school.gift");
            Text(vm, ctx, "why.school.teacher");
            vm.Footnote = ctx.Strings.Get("why.school.lesson");
            return vm;
        }

        /// <summary>The school's upkeep: paid at each close, a gift; the district pays the teacher.</summary>
        private static WhyVm SchoolUpkeep(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.schoolUpkeep.title", Money(ctx, SchoolRules.Upkeep(sim)));
            Row(vm, ctx, "why.schoolUpkeep.season", Money(ctx, SchoolRules.Upkeep(sim)));
            Text(vm, ctx, "why.schoolUpkeep.text");
            if (SchoolRules.State(sim).UpkeepUnpaidPeriod == sim.Period) Text(vm, ctx, "why.schoolUpkeep.unpaid");
            return vm;
        }

        /// <summary>Who goes to school: the town's children of school age who can't read, a teacher for every 30.</summary>
        private static WhyVm SchoolPupils(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            SchoolDef d = SchoolRules.Def(sim);
            int pupils = SchoolRules.Pupils(sim).Count;
            WhyVm vm = New(ctx, "why.schoolPupils.title", ctx.Fmt.Integer(pupils));
            Row(vm, ctx, "why.schoolPupils.children", ctx.Fmt.Integer(pupils), "min", ctx.Fmt.Integer(d.PupilAgeMin), "max", ctx.Fmt.Integer(d.PupilAgeMax));
            Row(vm, ctx, "why.schoolPupils.seats", ctx.Fmt.Integer(d.SeatsPerTeacher));
            Row(vm, ctx, "why.schoolPupils.teachers", ctx.Fmt.Integer(SchoolRules.Teachers(sim)));
            Text(vm, ctx, "why.schoolPupils.text", "years", ctx.Fmt.Integer(d.PrimaryYears));
            return vm;
        }
    }
}
