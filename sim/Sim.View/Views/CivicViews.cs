using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The watchman's post, community hall and playground plots' sheets (spec 023 §3.7, US-148 part 2), as previewed to the owner (Preview 5): the
    /// cost, upkeep, wage, reach and effect before it is commissioned from the town's cash; then who keeps it, who pays, the service, the reach and
    /// capacity, and what it does. <b>Post the job</b> and <b>Let him go</b> are the appointments. Every row has a *Why?* (the <c>civic</c> source,
    /// id = building × 100 + row).
    /// </summary>
    public static class CivicSheet
    {
        public static readonly string[] Ids = { "watchman", "hall", "playground" };

        /// <summary>A row's *Why?* id within a building (<see cref="LineVm.WhyId"/> = building × 100 + this).</summary>
        public const int Status = 0, Cost = 1, Upkeep = 2, Wage = 3, Who = 4, Reach = 5, Capacity = 6, Effect = 7, Pays = 8, Service = 9, Cash = 10, Keeper = 11;

        private const string Why = "civic";

        public static int OfPlot(string kind)
        {
            for (int i = 0; i < Ids.Length; i++) if (kind == Ids[i] + "_plot") return i;
            return -1;
        }

        private static LineVm Row(ViewContext ctx, int civic, string key, string value, int row)
        {
            return new LineVm(ctx.Strings.Get(key), value, Why, civic * 100 + row);
        }

        private static string K(int civic, string part) { return "civic." + Ids[civic] + "." + part; }

        internal static void Fill(ViewContext ctx, SheetVm vm, int i)
        {
            Simulation sim = ctx.Sim;
            if (!CivicRules.On(sim)) return;
            var s = ctx.Strings;
            CivicDef d = CivicRules.Def(sim, i);
            CivicPost p = CivicRules.Post(sim, i);
            if (!p.Built) { Plot(ctx, vm, i, d); return; }
            vm.Icon = Ids[i] == "watchman" ? "watchman_post" : Ids[i] == "hall" ? "community_hall" : "playground";
            vm.Title = s.Get(K(i, "title"));
            bool post = d.HasPost, inPost = p.Status == PostStatus.InPost;
            bool leftUnpaid = p.Hired > 0 && p.LeftUnpaid && p.Status == PostStatus.Vacant;
            vm.Subtitle = !post ? s.Get(K(i, "lineOpen")) : inPost ? s.Get(K(i, "lineIn"), "name", PeopleWords.Name(ctx, p.StaffName))
                : p.Status == PostStatus.Coming ? s.Get("civic.lineComing") : leftUnpaid ? s.Get(K(i, "lineLeft"), "name", PeopleWords.Name(ctx, p.StaffName)) : s.Get(K(i, "lineEmpty"));
            bool unpaid = inPost && p.UnpaidCloses > 0;
            vm.Lines.Add(LineVm.Header(s.Get(K(i, "hdrPost"))));
            if (post)
            {
                string who = inPost ? s.Get("civic.keeperValue", "name", PeopleWords.Name(ctx, p.StaffName), "season", FieldSheets.SeasonName(ctx, p.SinceSeason))
                    : p.Status == PostStatus.Coming ? s.Get("civic.keeperComing", "time", ctx.Fmt.Duration(p.ArrivesAt - ctx.Now))
                    : leftUnpaid ? s.Get("civic.keeperLeft", "season", FieldSheets.SeasonName(ctx, p.LeftSeason)) : s.Get("civic.keeperNobody");
                vm.Lines.Add(Row(ctx, i, K(i, "keeper"), who, Keeper));
                if (inPost)
                {
                    vm.Lines.Add(Row(ctx, i, "civic.pays", s.Get("civic.paysValue", "amount", ctx.Fmt.Coins(CivicRules.Upkeep(sim, i) + CivicRules.Wage(sim, i))), Pays));
                    vm.Lines.Add(Row(ctx, i, "civic.hisWage", s.Get("civic.hisWageValue", "amount", ctx.Fmt.Coins(CivicRules.Wage(sim, i))), Wage));
                    if (unpaid) vm.Lines.Add(new LineVm(s.Get(K(i, "unpaidNote")), null) { Banner = true, NoteTone = Tone.Warn });
                }
                else
                {
                    vm.Lines.Add(Row(ctx, i, K(i, "wage"), ctx.Fmt.Coins(CivicRules.Wage(sim, i)), Wage));
                    if (p.Status == PostStatus.Vacant) vm.Lines.Add(new LineVm(s.Get(leftUnpaid ? K(i, "leftNote") : K(i, "emptyNote")), null) { Banner = leftUnpaid, NoteTone = leftUnpaid ? Tone.Warn : Tone.Plain });
                    else vm.Lines.Add(new LineVm(s.Get("civic.comingNote"), null));
                }
            }
            else vm.Lines.Add(Row(ctx, i, "civic.pays", s.Get("civic.paysValue", "amount", ctx.Fmt.Coins(CivicRules.Upkeep(sim, i))), Pays));
            Fixed condition = CivicRules.Condition(sim);
            if (inPost || !post)
            {
                LineVm service = Row(ctx, i, "civic.service", ctx.Fmt.Percent(condition), Service);
                if (condition < Fixed.One) { service.Note = s.Get("civic.serviceNote"); service.NoteTone = condition < Fixed.Parse("0.5") ? Tone.Warn : Tone.Plain; }
                vm.Lines.Add(service);
            }
            ReachAndCapacity(ctx, vm, i, d, inPost || !post);
            vm.Lines.Add(LineVm.Header(s.Get("civic.hdrDoes")));
            vm.Lines.Add(Row(ctx, i, K(i, "effect"), EffectText(ctx, i, d, CivicRules.Staffed(sim, i)), Effect));
            if (post)
            {
                if (p.Status == PostStatus.Vacant) vm.Actions.Add(ActionVm.Checked(ctx, "postJob", s.Get("civic.postJob"), s.Get("civic.postJobDetail"), ActionKind.Primary, at => new PostCivicJob(at, i)));
                if (unpaid || leftUnpaid) vm.Actions.Add(ActionVm.Open("budget", s.Get("civic.openBudget"), new SheetRequest(TreasurySheet.Kind, 0, TreasurySheet.BudgetTab)));
                if (inPost) vm.Actions.Add(ActionVm.Checked(ctx, "dismiss", s.Get("civic.letGo"), s.Get(K(i, "letGoDetail")), ActionKind.Secondary, at => new DismissStaff(at, i)));
            }
        }

        private static void ReachAndCapacity(ViewContext ctx, SheetVm vm, int i, CivicDef d, bool working)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (working)
                vm.Lines.Add(Row(ctx, i, "civic.reach", s.Get("civic.reachValue", "m", ctx.Fmt.Integer(d.Reach), "in", ctx.Fmt.Integer(HomesInReach(sim, i)), "of", ctx.Fmt.Integer(sim.State.Citizens.Households.Count)), Reach));
            else vm.Lines.Add(Row(ctx, i, "civic.reach", s.Get("civic.reachShort", "m", ctx.Fmt.Integer(d.Reach)), Reach));
            vm.Lines.Add(Row(ctx, i, "civic.capacity", working ? s.Get(K(i, "capacityValue"), "cap", ctx.Fmt.Integer(d.Capacity), "n", ctx.Fmt.Integer(CivicRules.Served(sim, i))) : s.Get(K(i, "capacityShort"), "cap", ctx.Fmt.Integer(d.Capacity)), Capacity));
        }

        private static int HomesInReach(Simulation sim, int i)
        {
            int n = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b == null || CivicRules.Covered(sim, i, b.TileId)) n++;
            }
            return n;
        }

        /// <summary>The effect at today's strength: the need's value for a home in reach, and what it is without the building.</summary>
        private static string EffectText(ViewContext ctx, int i, CivicDef d, bool staffed)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!staffed) return s.Get(K(i, "effectOff"), "pct", ctx.Fmt.Percent(Without(sim, i)));
            Fixed with = Without(sim, i) + d.Gain * CivicRules.Condition(sim) * CivicRules.Scale(sim, i);
            if (with > Fixed.One) with = Fixed.One;
            return s.Get(K(i, "effectOn"), "pct", ctx.Fmt.Percent(with), "without", ctx.Fmt.Percent(Without(sim, i)));
        }

        /// <summary>The need's value for a home with no help from this building.</summary>
        internal static Fixed Without(Simulation sim, int i)
        {
            PopulationDefs p = sim.Config.Game.Population;
            return i == CivicDef.Watchman ? sim.Config.Game.Township.SafetyBase : i == CivicDef.Hall ? p.Belonging : p.Leisure;
        }

        // ---- the plot, before it is commissioned ----

        private static void Plot(ViewContext ctx, SheetVm vm, int i, CivicDef d)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            bool open = TreasuryRules.IsOpen(sim);
            long cost = CivicRules.Cost(sim, i);
            string work = d.Work;
            if (!open)
            {
                vm.Subtitle = s.Get(K(i, "lockedLine"));
                vm.Lines.Add(Row(ctx, i, K(i, "status"), s.Get("civic.notYet"), Status));
                vm.Lines.Add(new LineVm(s.Get("civic.lockedNote"), null));
                vm.Actions.Add(ActionVm.Checked(ctx, "commission", s.Get(K(i, "commission")), s.Get("civic.fromTown", "amount", ctx.Fmt.Coins(cost)), ActionKind.Primary, at => new CommissionWorks(at, work)));
                return;
            }
            long cash = sim.State.Treasury.Cash;
            bool poor = cash < cost;
            vm.Lines.Add(LineVm.Header(s.Get(K(i, "hdrPost"))));
            vm.Lines.Add(Row(ctx, i, "civic.cost", ctx.Fmt.Coins(cost), Cost));
            vm.Lines.Add(Row(ctx, i, "civic.upkeep", ctx.Fmt.Coins(CivicRules.Upkeep(sim, i)), Upkeep));
            if (d.HasPost)
            {
                vm.Lines.Add(Row(ctx, i, K(i, "wage"), ctx.Fmt.Coins(CivicRules.Wage(sim, i)), Wage));
                vm.Lines.Add(Row(ctx, i, K(i, "who"), s.Get("civic.whoValue"), Who));
            }
            else vm.Lines.Add(Row(ctx, i, "civic.noStaff", s.Get("civic.noStaffValue"), Who));
            ReachAndCapacity(ctx, vm, i, d, false);
            vm.Lines.Add(LineVm.Header(s.Get("civic.hdrDoes")));
            vm.Lines.Add(Row(ctx, i, K(i, "effect"), s.Get(K(i, "effectWill"), "pct", ctx.Fmt.Percent(Without(sim, i)), "to", ctx.Fmt.Percent(System.Math.Min(1, 1) == 1 && Without(sim, i) + d.Gain > Fixed.One ? Fixed.One : Without(sim, i) + d.Gain)), Effect));
            LineVm town = Row(ctx, i, "civic.cash", ctx.Fmt.Coins(cash), Cash);
            if (poor) town.NoteTone = Tone.Warn;
            vm.Lines.Add(town);
            vm.Lines.Add(new LineVm(s.Get(poor ? "civic.poorNote" : "civic.townNote"), null));
            if (poor)
                vm.Actions.Add(new ActionVm("commission", s.Get(K(i, "commission")), s.Get("civic.fromTown", "amount", ctx.Fmt.Coins(cost)), ActionKind.Primary, false,
                    s.Get("civic.poorReason", "cash", ctx.Fmt.Coins(cash), "cost", ctx.Fmt.Coins(cost)), at => new CommissionWorks(at, work)));
            else
                vm.Actions.Add(ActionVm.Checked(ctx, "commission", s.Get(K(i, "commission")), s.Get("civic.fromTown", "amount", ctx.Fmt.Coins(cost)), ActionKind.Primary, at => new CommissionWorks(at, work)));
            if (poor) vm.Actions.Add(ActionVm.Open("give", s.Get("clinic.give"), new SheetRequest(TreasurySheet.GiveKind, 0), s.Get("treasury.giveDetail")));
        }

        /// <summary>The commission card's budget guard (spec 023 §3.7): what the building adds a season to Safety and whether the town would be short.</summary>
        internal static void Guard(ViewContext ctx, ConfirmCardVm vm, string workId)
        {
            int i = CivicRules.OfWork(ctx.Sim, workId);
            if (i < 0) return;
            var s = ctx.Strings;
            CivicRules.Guard(ctx.Sim, workId, out long added, out long shortBy);
            string head = s.Get(CivicRules.Def(ctx.Sim, i).HasPost ? "civic.guardAddsPost" : "civic.guardAdds", "amount", ctx.Fmt.Coins(added),
                "upkeep", ctx.Fmt.Coins(CivicRules.Upkeep(ctx.Sim, i)), "wage", ctx.Fmt.Coins(CivicRules.Wage(ctx.Sim, i)));
            vm.Warning = head + " " + (shortBy > 0 ? s.Get("civic.guardShort", "amount", ctx.Fmt.Coins(shortBy)) : s.Get("civic.guardFine"));
            vm.Reassure = shortBy == 0;
        }
    }

    public static partial class WhyView
    {
        private static WhyVm CivicWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int i = (int)(id / 100), row = (int)(id % 100);
            CivicDef d = CivicRules.Def(sim, i);
            CivicPost p = CivicRules.Post(sim, i);
            string n = CivicSheet.Ids[i];
            WhyVm vm;
            switch (row)
            {
                case CivicSheet.Cost:
                    vm = New(ctx, "why.civic.cost.title", Money(ctx, CivicRules.Cost(sim, i)));
                    Row(vm, ctx, "why.civic.cost.base", Money(ctx, BaseOf(sim, d.Work)));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.civic.cost.text");
                    return vm;
                case CivicSheet.Upkeep:
                    vm = New(ctx, "why.civic.upkeep.title", Money(ctx, CivicRules.Upkeep(sim, i)));
                    Row(vm, ctx, "why.civic.upkeep.base", Money(ctx, TreasuryRules.Def(sim).Lines[d.UpkeepLine].Amount));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.civic.upkeep.text");
                    return vm;
                case CivicSheet.Wage:
                    vm = New(ctx, "why.civic.wage.title", Money(ctx, CivicRules.Wage(sim, i)));
                    Row(vm, ctx, "why.civic.wage.base", Money(ctx, TreasuryRules.Def(sim).Lines[d.WageLine].Amount));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.civic.wage.text");
                    return vm;
                case CivicSheet.Who:
                    vm = New(ctx, "why.civic.who.title", s.Get(d.HasPost ? "civic.whoValue" : "civic.noStaffValue"));
                    Text(vm, ctx, d.HasPost ? "why.civic.who.text" : "why.civic.who.none");
                    return vm;
                case CivicSheet.Keeper:
                    vm = New(ctx, "why.civic.keeper.title", p.Status == PostStatus.InPost ? s.Get("clinic.inPost") : s.Get("civic.keeperNobody"));
                    Text(vm, ctx, "why.civic.keeper.text", "leave", ctx.Fmt.Integer(d.LeaveCloses), "hours", ctx.Fmt.Integer(d.StaffHours));
                    return vm;
                case CivicSheet.Pays:
                    vm = New(ctx, "why.civic.pays.title", Money(ctx, CivicRules.Upkeep(sim, i) + (p.Status == PostStatus.InPost ? CivicRules.Wage(sim, i) : 0)));
                    Row(vm, ctx, "why.civic.pays.upkeep", Money(ctx, CivicRules.Upkeep(sim, i)));
                    if (d.HasPost) Row(vm, ctx, "why.civic.pays.wage", Money(ctx, CivicRules.Wage(sim, i)));
                    Text(vm, ctx, "why.civic.pays.text");
                    return vm;
                case CivicSheet.Service:
                    vm = New(ctx, "why.civic.service.title", Pct(ctx, CivicRules.Condition(sim)));
                    Row(vm, ctx, "why.civic.service.now", Pct(ctx, CivicRules.Condition(sim)));
                    Text(vm, ctx, "why.civic.service.text");
                    return vm;
                case CivicSheet.Reach:
                    vm = New(ctx, "why.civic.reach.title", s.Get("civic.reachShort", "m", ctx.Fmt.Integer(d.Reach)));
                    Row(vm, ctx, "why.civic.reach.reach", s.Get("civic.reachShort", "m", ctx.Fmt.Integer(d.Reach)));
                    Text(vm, ctx, "why.civic.reach.text");
                    return vm;
                case CivicSheet.Capacity:
                    vm = New(ctx, "why.civic.capacity.title", s.Get("civic." + n + ".capacityValue", "cap", ctx.Fmt.Integer(d.Capacity), "n", ctx.Fmt.Integer(CivicRules.Served(sim, i))));
                    Row(vm, ctx, "why.civic.capacity.scale", Pct(ctx, CivicRules.Scale(sim, i)));
                    Text(vm, ctx, "why.civic.capacity.text");
                    return vm;
                case CivicSheet.Effect:
                    vm = New(ctx, "why.civic." + n + ".effect.title", Pct(ctx, CivicSheet.Without(sim, i)));
                    Row(vm, ctx, "why.civic.effect.without", Pct(ctx, CivicSheet.Without(sim, i)));
                    Row(vm, ctx, "why.civic.effect.gain", Pct(ctx, d.Gain));
                    Row(vm, ctx, "why.civic.effect.service", Pct(ctx, CivicRules.Condition(sim)));
                    Row(vm, ctx, "why.civic.effect.scale", Pct(ctx, CivicRules.Scale(sim, i)));
                    Text(vm, ctx, "why.civic." + n + ".effect.text");
                    return vm;
                case CivicSheet.Cash:
                    vm = New(ctx, "why.civic.cash.title", Money(ctx, sim.State.Treasury.Cash));
                    Text(vm, ctx, "why.civic.cash.text");
                    return vm;
                default:
                    vm = New(ctx, "why.civic.status.title", s.Get("civic.notYet"));
                    Text(vm, ctx, "why.civic.status.text");
                    return vm;
            }
        }

        private static long BaseOf(Simulation sim, string work)
        {
            foreach (TownWorkDef w in TreasuryRules.Def(sim).Works) if (w.Id == work) return w.Cost;
            return 0;
        }
    }
}
