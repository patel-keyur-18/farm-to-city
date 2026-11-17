using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The council hall's *Roads* section (spec 023 §3.3, US-146): the petition for the paved link road with its numbers, "Petition" from
    /// cash or "With a loan", the build's countdown and, once built, what it earned you. Every row has a *Why?* (the <c>road</c> source).
    /// </summary>
    public static class RoadSheet
    {
        public const string LoanKind = "RoadLoan";

        /// <summary>A row's *Why?* id (<see cref="LineVm.WhyId"/>).</summary>
        public const int Status = 0, Households = 1, Share = 2, Days = 3, Effect = 4, Earned = 5;

        /// <summary>Adds the Roads section to the council hall's place sheet, from the Village.</summary>
        internal static void Fill(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!RoadRules.On(sim) || !RoadRules.Unlocked(sim)) return;
            var s = ctx.Strings;
            RoadState st = RoadRules.State(sim);
            RoadDef d = RoadRules.Def(sim);
            vm.Lines.Add(LineVm.Header(s.Get("road.header")));
            vm.Lines.Add(new LineVm(s.Get("road.link"), s.Get(st.Status == RoadStatus.None ? "road.linkNone" : st.Status == RoadStatus.Building ? "road.linkBuilding" : "road.linkBuilt"), "road", Status));
            if (st.Status == RoadStatus.None)
            {
                int have = RoadRules.Households(sim);
                vm.Lines.Add(new LineVm(s.Get("road.households"), s.Get("road.householdsValue", "n", ctx.Fmt.Integer(have), "need", ctx.Fmt.Integer(d.PetitionHouseholds)), "road", Households));
                vm.Lines.Add(new LineVm(s.Get("road.share"), ctx.Fmt.Coins(RoadRules.Price(sim)), "road", Share));
                vm.Lines.Add(new LineVm(s.Get("road.days"), s.Plural("road.daysValue", d.Days), "road", Days));
                vm.Lines.Add(new LineVm(s.Get("road.effect"), EffectText(ctx), "road", Effect));
                vm.Lines.Add(new LineVm(s.Get("road.note"), null));
                long price = RoadRules.Price(sim);
                vm.Actions.Add(ActionVm.Checked(ctx, "petitionRoad", s.Get("road.petition"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new PetitionRoad(at)));
                vm.Actions.Add(WithLoan(ctx));
            }
            else if (st.Status == RoadStatus.Building)
            {
                vm.Lines.Add(new LineVm(s.Get("road.readyIn"), ctx.Fmt.Duration(st.ReadyAt - ctx.Now), "road", Days));
                vm.Lines.Add(new LineVm(s.Get("road.shareGave"), ctx.Fmt.Coins(st.Share), "road", Share));
                vm.Lines.Add(new LineVm(s.Get("road.effect"), EffectText(ctx), "road", Effect));
                vm.Lines.Add(new LineVm(s.Get("road.buildingNote"), null));
            }
            else
            {
                vm.Lines.Add(new LineVm(s.Get("road.earned"), ctx.Fmt.Coins(st.EarnedThis), "road", Earned));
                vm.Lines.Add(new LineVm(s.Get("road.effect"), EffectText(ctx), "road", Effect));
                vm.Lines.Add(new LineVm(s.Get("road.upkeep"), s.Get("road.upkeepValue")));
                vm.Lines.Add(new LineVm(s.Get("road.builtNote"), null));
            }
        }

        /// <summary>"+0.50 a kg": what the road adds to what the trader pays at the farm gate (what it will add, before it is built).</summary>
        internal static string EffectText(ViewContext ctx)
        {
            RegionDef region = ctx.Sim.Config.Game.Market.Region;
            Fixed? road = region.CostOfTier(RoadRules.Def(ctx.Sim).Tier);
            Fixed gain = road == null ? Fixed.Zero : region.TransportCostPerKg - road.Value;
            return "+" + ctx.Fmt.PricePerKg(gain);
        }

        /// <summary>Most the co-op bank lends toward the share: its room on what you already have (the road isn't yours), in whole thousands, at most the share.</summary>
        public static long Loan(Simulation sim)
        {
            if (sim.State.Credit == null) return 0;
            int bank = LandLoanSheet.Bank(sim);
            if (bank < 0) return 0;
            long loan = System.Math.Min(RoadRules.Price(sim), CreditRules.Limit(sim, bank).Limit) / (1000 * Money.Coin) * (1000 * Money.Coin);
            return loan < sim.Config.Game.Credit.Lenders[bank].MinAmount ? 0 : loan;
        }

        private static ActionVm WithLoan(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            long price = RoadRules.Price(sim), loan = Loan(sim);
            RejectReason why = RoadRules.CanPetition(sim);
            string reason = why != RejectReason.None ? ctx.Reject(why) : loan == 0 ? ctx.Strings.Get("road.noLoan")
                : !sim.State.Ledger.CanAfford(price - loan) ? ctx.Reject(RejectReason.InsufficientFunds) : null;
            return new ActionVm("roadLoan", ctx.Strings.Get("road.petitionLoan"), loan == 0 ? null : ctx.Strings.Get("houseLoan.down", "amount", ctx.Fmt.Coins(price - loan)),
                ActionKind.Open, reason == null, reason, null, new SheetRequest(LoanKind, 0));
        }

        /// <summary>The petition with a co-op loan: what it lends, the down payment, one row per term.</summary>
        public static SheetVm LoanSheet(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long price = RoadRules.Price(sim), loan = Loan(sim);
            int bank = LandLoanSheet.Bank(sim);
            var vm = new SheetVm
            {
                Icon = "place_council_hall", Title = s.Get("road.loanTitle"),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            vm.Lines.Add(new LineVm(s.Get("road.loanWhy"), null));
            if (loan == 0)
            {
                vm.Lines.Add(new LineVm(s.Get("road.noLoan"), null));
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
                    Action = ActionVm.Checked(ctx, "roadLoan:" + term, s.Get("road.petition"), ctx.Fmt.Coins(price - loan), ActionKind.Primary,
                        at => new PetitionRoad(at, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }
    }

    public static partial class WhyView
    {
        /// <summary>The paved link road's rows (spec 023 §3.3): id = <see cref="RoadSheet"/>'s Status, Households, Share, Days, Effect, Earned.</summary>
        private static WhyVm RoadWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            RoadDef d = RoadRules.Def(sim);
            RoadState st = RoadRules.State(sim);
            RegionDef region = sim.Config.Game.Market.Region;
            Fixed track = region.TransportCostPerKg, paved = region.CostOfTier(d.Tier) ?? track;
            WhyVm vm;
            switch ((int)id)
            {
                case RoadSheet.Households:
                    vm = New(ctx, "why.road.households.title", s.Get("road.householdsValue", "n", ctx.Fmt.Integer(RoadRules.Households(sim)), "need", ctx.Fmt.Integer(d.PetitionHouseholds)));
                    Row(vm, ctx, "why.road.households.yours", ctx.Fmt.Integer(RoadRules.Households(sim)));
                    Row(vm, ctx, "why.road.households.need", ctx.Fmt.Integer(d.PetitionHouseholds));
                    Text(vm, ctx, "why.road.households.text");
                    return vm;
                case RoadSheet.Share:
                    vm = New(ctx, "why.road.share.title", Money(ctx, st.Status == RoadStatus.None ? RoadRules.Price(sim) : st.Share));
                    Row(vm, ctx, "why.road.share.base", Money(ctx, d.Share));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.road.share.text");
                    vm.Footnote = s.Get("why.road.lesson");
                    return vm;
                case RoadSheet.Days:
                    vm = New(ctx, "why.road.days.title", s.Plural("road.daysValue", d.Days));
                    Row(vm, ctx, "why.road.days.build", s.Plural("road.daysValue", d.Days));
                    if (st.Status == RoadStatus.Building) Row(vm, ctx, "why.road.days.left", ctx.Fmt.Duration(st.ReadyAt - ctx.Now));
                    Text(vm, ctx, "why.road.days.text");
                    return vm;
                case RoadSheet.Effect:
                    vm = New(ctx, "why.road.effect.title", RoadSheet.EffectText(ctx));
                    Row(vm, ctx, "why.road.effect.track", Price(ctx, track));
                    Row(vm, ctx, "why.road.effect.paved", Price(ctx, paved));
                    Row(vm, ctx, "why.road.effect.gain", Price(ctx, track - paved));
                    foreach (string good in new[] { "potato", "wheat" })
                    {
                        int g = GoodIndex(sim, good);
                        if (g < 0) continue;
                        Fixed floor = MarketRules.Floor(sim, g), gain = (track - paved) * sim.Config.Game.Market.Goods[g].WeightKg * sim.State.Market.PriceIndex;
                        if (floor <= Fixed.Zero) continue;
                        Row(vm, ctx, "why.road.effect.good", "+" + ctx.Fmt.Percent(gain / floor), "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey));
                    }
                    Text(vm, ctx, "why.road.effect.text");
                    return vm;
                case RoadSheet.Earned:
                    vm = New(ctx, "why.road.earned.title", Money(ctx, st.EarnedThis));
                    Row(vm, ctx, "why.road.earned.this", Money(ctx, st.EarnedThis));
                    Row(vm, ctx, "why.road.earned.last", Money(ctx, st.EarnedLast));
                    Text(vm, ctx, "why.road.earned.text");
                    return vm;
                default:
                    vm = New(ctx, "why.road.status.title", s.Get(st.Status == RoadStatus.None ? "road.linkNone" : st.Status == RoadStatus.Building ? "road.linkBuilding" : "road.linkBuilt"));
                    Text(vm, ctx, "why.road.status.text");
                    Text(vm, ctx, st.Status == RoadStatus.Built ? "why.road.status.built" : "why.road.status.not");
                    return vm;
            }
        }

        private static int GoodIndex(Simulation sim, string id)
        {
            var goods = sim.Config.Game.Market.Goods;
            for (int i = 0; i < goods.Count; i++) if (goods[i].Id == id) return i;
            return -1;
        }

        /// <summary>Names the road in a trader price's *Why?* (spec 023 §3.3): carting on the paved road, or what a petition would change.</summary>
        internal static void RoadNote(ViewContext ctx, WhyVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!RoadRules.On(sim)) return;
            if (RoadRules.IsBuilt(sim)) Text(vm, ctx, "why.price.roadBuilt", "paved", Price(ctx, RoadRules.TransportPerKg(sim)), "track", Price(ctx, sim.Config.Game.Market.Region.TransportCostPerKg));
            else if (RoadRules.Unlocked(sim)) Text(vm, ctx, "why.price.roadNot", "track", Price(ctx, sim.Config.Game.Market.Region.TransportCostPerKg));
        }
    }
}
