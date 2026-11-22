using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Managers in words (spec 028 §3.2): a style's name and its one plain sentence, a target.</summary>
    public static class ManagerWords
    {
        public static ManagersDef Defs(ViewContext ctx) { return ctx.Sim.Config.Game.Managers; }

        public static string Style(ViewContext ctx, int style) { return ctx.Strings.Get(Defs(ctx).Styles[style].NameKey); }

        /// <summary>What a style means, in one sentence.</summary>
        public static string StyleLine(ViewContext ctx, int style) { return ctx.Strings.Get("manager.styleLine." + Defs(ctx).Styles[style].Id); }

        public static string Target(ViewContext ctx, ManagerTarget t) { return ctx.Strings.Get("manager.target." + t.ToString().ToLowerInvariant()); }

        /// <summary>"4,000 coins and a tenth of the profit".</summary>
        public static string Pay(ViewContext ctx, long wage) { return ctx.Strings.Get("manager.pay", "wage", ctx.Fmt.Coins(wage)); }

        public static string Name(ViewContext ctx, Worker m) { return PeopleWords.Name(ctx, m.Name); }
    }

    /// <summary>
    /// The manager on a business (spec 028 §3.2, Preview 16): a line on Today, his section in Settings (target, pay, days mis-set, Let go), the offer
    /// that names the business, and his report at each close.
    /// </summary>
    public static class ManagerSheet
    {
        public const string OfferKind = "ManagerOffer", ReportKind = "ManagerReport";

        public static bool On(ViewContext ctx) { return ManagerRules.On(ctx.Sim); }

        /// <summary>The business's manager line for Today: his name, stars, style and pay, and his last report.</summary>
        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b)
        {
            Worker m = ManagerRules.Of(ctx.Sim, b.Id);
            if (m == null) return;
            vm.Lines.Add(ManagerLine(ctx, b, m, true));
        }

        private static LineVm ManagerLine(ViewContext ctx, Business b, Worker m, bool withReport)
        {
            var s = ctx.Strings;
            int skill = (int)m.Skill.RoundToLong();
            var line = new LineVm(s.Get("manager.line"), ManagerWords.Name(ctx, m), "managerWage", b.Id)
            {
                Icon = PeopleWords.Female(ctx, m.Name) ? "person_hand_f" : "person_hand_m", Stars = PeopleWords.Stars(ctx, skill, skill),
                Note = s.Get("manager.lineNote", "style", ManagerWords.Style(ctx, m.Mgr.Style), "pay", ManagerWords.Pay(ctx, m.Wage)),
            };
            if (HrRules.Leaves(ctx.Sim, m)) { line.Note = s.Get("staff.leaving"); line.NoteTone = Tone.Warn; }
            if (withReport && m.Mgr.ReportPeriod != 0)
                line.Action = ActionVm.Open("managerReport", s.Get("manager.report"), new SheetRequest(ReportKind, b.Id));
            return line;
        }

        /// <summary>His section in Settings, or the way to get one.</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b)
        {
            if (!On(ctx) || b.Status == BusinessStatus.Closed) return;
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            vm.Lines.Add(LineVm.Header(s.Get("manager.section")));
            Worker m = ManagerRules.Of(sim, id);
            if (m == null)
            {
                vm.Lines.Add(new LineVm(s.Get("manager.none"), b.Worker == WorkerKind.Founder ? s.Get("manager.noneYou") : s.Get("manager.noneWhy"), "managerWage", id)
                    { Icon = "person_hand_m", Action = ActionVm.Open("hireManager", s.Get("manager.hire"), new SheetRequest("Word", HiringRules.ManagerPlace)) });
                return;
            }
            vm.Lines.Add(ManagerLine(ctx, b, m, false));
            ManagerTarget now = m.Mgr.Target, next = now == ManagerTarget.Profit ? ManagerTarget.Growth : ManagerTarget.Profit;
            var target = new LineVm(s.Get("manager.target"), ManagerWords.Target(ctx, now), "managerTarget", id);
            target.Action = ActionVm.Checked(ctx, "target", s.Get("business.change"), ManagerWords.Target(ctx, next), ActionKind.Secondary, at => new SetManagerTarget(at, id, next));
            vm.Lines.Add(target);
            long goal = ManagerRules.Goal(sim, b, m);
            BusinessReport sofar = BusinessBooks.Report(b, sim.Period);
            long result = now == ManagerTarget.Growth ? ManagerRules.Sales(sofar) : BusinessBooks.NetProfit(sofar);
            vm.Lines.Add(new LineVm(s.Get("manager.goal"), s.Get(now == ManagerTarget.Growth ? "manager.goalGrowth" : "manager.goalProfit", "amount", ctx.Fmt.Coins(goal)), "managerTarget", id)
                { Note = s.Get("manager.sofar", "amount", ctx.Fmt.Coins(result)) });
            long share = result > 0 && now == ManagerTarget.Profit ? Money.MulRate(result, ManagerWords.Defs(ctx).WageProfitShare) : 0;
            vm.Lines.Add(new LineVm(s.Get("manager.payNow"), ctx.Fmt.Coins(m.Wage + share), "managerWage", id) { Note = s.Get("manager.payNowNote") });
            vm.Lines.Add(new LineVm(s.Get("manager.misDays"), s.Get("manager.misDaysValue", "n", ctx.Fmt.Integer(m.Mgr.MisDays), "of", ctx.Fmt.Integer(m.Mgr.Days)), "managerMiss", id) { Note = s.Get("manager.misDaysNote") });
            if (m.Mgr.ReportPeriod != 0)
                vm.Lines.Add(new LineVm(s.Get("manager.lastReport"), s.Get(m.Mgr.ReportMet ? "manager.met" : "manager.missed"), "managerTarget", id)
                    { Action = ActionVm.Open("managerReport", s.Get("manager.report"), new SheetRequest(ReportKind, id)), NoteTone = m.Mgr.ReportMet ? Tone.Good : Tone.Warn });
            long severance = m.SeasonsWorked > 0 ? Money.MulRate(m.Wage, sim.Config.Game.People.Hand.SeveranceShare) : 0;
            var go = new LineVm(s.Get("manager.letGoRow"), s.Get("manager.letGoWhy"));
            go.Action = ActionVm.Checked(ctx, "letGoManager", s.Get("action.letGo"), severance > 0 ? s.Get("business.letGoSeverance", "amount", ctx.Fmt.Coins(severance)) : s.Get("business.letGoDetail"),
                ActionKind.Danger, at => new DismissManager(at, id));
            go.Action.Confirm = new ConfirmVm(s.Get("business.letGoQ", "name", ManagerWords.Name(ctx, m)), s.Get("action.letGo"), s.Get("hand.keep"), at => new Command[] { new DismissManager(at, id) }, noSends: false) { YesIsDanger = true };
            vm.Lines.Add(go);
        }

        /// <summary>A line says a manager sets this each morning: its button goes, so a change you make is not undone at 6:00.</summary>
        internal static void MarkSet(ViewContext ctx, SheetVm vm, Business b)
        {
            Worker m = ManagerRules.Of(ctx.Sim, b.Id);
            if (m == null) return;
            foreach (LineVm l in vm.Lines)
            {
                string k = l.WhyKey;
                if (k != "shopPrice" && k != "shopQuality" && k != "shopPutOut" && k != "businessFee") continue;
                l.Action = null;
                l.Note = ctx.Strings.Get("manager.setBy", "name", ManagerWords.Name(ctx, m));
                l.NoteTone = Tone.Plain;
            }
        }

        // ---- the board's manager post ----

        /// <summary>The manager's tab on the board: the post's usual pay and what he does.</summary>
        internal static void AddJob(ViewContext ctx, SheetVm vm)
        {
            var s = ctx.Strings;
            long usual = HiringRules.AskedWage(ctx.Sim, HiringRules.ManagerPlace, Fixed.FromInt(50), false);
            vm.Lines.Add(new LineVm(s.Get("board.manager"), s.Get("manager.usual", "wage", ctx.Fmt.Coins(usual)), "managerWage", 0)
                { Icon = "person_hand_m", Note = s.Get("manager.boardNote") });
        }

        // ---- the offer ----

        /// <summary>Offer an applicant the manager's post (Preview 16, screen 4): which business first, then the three terms. Arg is the business chosen.</summary>
        public static SheetVm Offer(ViewContext ctx, long applicantId, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Applicant a = sim.State.People?.Applicant(applicantId);
            if (a == null) return new SheetVm { Icon = "letter", Title = s.Get("reject.UnknownApplicant") };
            var vm = new SheetVm { Icon = PeopleWords.Female(ctx, a.Name) ? "person_hand_f" : "person_hand_m" };
            long chosen = 0;
            long.TryParse(arg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out chosen);
            var open = new List<Business>();
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed) open.Add(b);
            Business pick = open.Find(b => b.Id == chosen && ManagerRules.Of(sim, b.Id) == null) ?? open.Find(b => ManagerRules.Of(sim, b.Id) == null);
            vm.Title = s.Get("board.offerTitle", "name", PeopleWords.Name(ctx, a.Name));
            vm.Subtitle = pick == null ? s.Get("manager.offerNone") : s.Get("manager.offerSubtitle", "business", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(sim, pick)));
            vm.Lines.Add(LineVm.Header(s.Get("manager.whichBusiness")));
            foreach (Business b in open)
            {
                Business x = b;
                BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                bool taken = ManagerRules.Of(sim, b.Id) != null;
                string note = taken ? s.Get("manager.hasOne") : b.Worker == WorkerKind.Founder ? s.Get("manager.youRunIt") : s.Get("manager.noManager", "profit", LastProfit(ctx, b));
                var line = new LineVm(BusinessWords.Name(ctx, t), note) { Icon = t.Id };
                if (taken) line.Badge = s.Get("manager.taken");
                else if (pick != null && pick.Id == b.Id) line.Badge = s.Get("manager.chosen");
                else line.Action = ActionVm.Open("choose:" + b.Id, s.Get("manager.choose"), new SheetRequest(OfferKind, applicantId, b.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                vm.Lines.Add(line);
            }
            if (pick == null) return vm;
            vm.Lines.Add(LineVm.Header(s.Get("manager.terms")));
            long pickId = pick.Id;
            if (a.Countered)
                vm.Lines.Add(new LineVm(s.Get("board.counterLine"), ctx.Fmt.Coins(HiringRules.OfferWage(sim, a, OfferLevel.Asked)), "managerWage", pickId) { Note = s.Get("board.counterNote"), NoteTone = Tone.Warn });
            foreach (OfferLevel level in new[] { OfferLevel.Asked, OfferLevel.Low, OfferLevel.High })
            {
                OfferLevel l = level;
                long wage = HiringRules.OfferWage(sim, a, level);
                string at = level == OfferLevel.Asked ? "manager.atAsked" : level == OfferLevel.Low ? "manager.atLow" : "manager.atHigh";
                string note = level == OfferLevel.Asked ? "board.noteAsked" : level == OfferLevel.Low ? "board.noteLow" : "board.noteHigh";
                var line = new LineVm(s.Get(at, "wage", ctx.Fmt.Coins(wage)), ChanceWord(ctx, HiringRules.AcceptChance(sim, a, level)), "managerWage", pickId) { Note = s.Get(note) };
                line.Action = ActionVm.Checked(ctx, "offerManager" + level + ":" + a.Id, s.Get("action.offer"), s.Get("board.nowBusiness", "now", ctx.Fmt.Coins(PeopleRules.RestOfSeason(sim, wage))),
                    level == OfferLevel.Asked ? ActionKind.Primary : ActionKind.Secondary, t => new HireManager(t, applicantId, pickId, l));
                vm.Lines.Add(line);
            }
            return vm;
        }

        private static string ChanceWord(ViewContext ctx, Fixed p)
        {
            string key = p >= Fixed.One ? "board.sure" : p >= Fixed.Parse("0.9") ? "board.veryLikely" : p >= Fixed.Parse("0.75") ? "board.likely" : "board.maybe";
            return ctx.Strings.Get(key);
        }

        private static string LastProfit(ViewContext ctx, Business b)
        {
            return b.Reports.Count == 0 ? ctx.Strings.Get("manager.noSeasonYet") : ctx.Fmt.Coins(BusinessBooks.NetProfit(b.Reports[b.Reports.Count - 1]));
        }

        // ---- the report ----

        /// <summary>His report at the last close (Preview 16, screen 6): the target against what the season made, what he changed and why, how the season went.</summary>
        public static SheetVm Report(ViewContext ctx, long businessId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(businessId);
            Worker m = b == null ? null : ManagerRules.Of(sim, b.Id);
            if (b == null || m == null || m.Mgr.ReportPeriod == 0) return new SheetVm { Icon = "chart", Title = s.Get("manager.reportNone") };
            ManagerRecord r = m.Mgr;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            ManagerStyleDef style = ManagerWords.Defs(ctx).Styles[r.Style];
            var vm = new SheetVm
            {
                Icon = "chart", Title = s.Get("manager.reportTitle", "name", ManagerWords.Name(ctx, m)),
                Subtitle = s.Get("manager.reportSubtitle", "business", BusinessWords.Name(ctx, t), "season", FieldSheets.SeasonName(ctx, r.ReportPeriod - 1)),
            };
            bool growth = r.ReportTarget == ManagerTarget.Growth;
            vm.Lines.Add(LineVm.Header(s.Get(growth ? "manager.reportTargetGrowth" : "manager.reportTargetProfit", "amount", ctx.Fmt.Coins(r.ReportGoal))));
            long gap = r.ReportResult - r.ReportGoal;
            var res = new LineVm(s.Get(growth ? "manager.sales" : "manager.profit"), ctx.Fmt.Coins(r.ReportResult), "managerTarget", b.Id)
            {
                Note = r.ReportMet ? s.Get("manager.beat", "amount", ctx.Fmt.Coins(gap)) : s.Get(r.Misses >= 2 ? "manager.missedTwice" : "manager.missedBy", "amount", ctx.Fmt.Coins(-gap)),
                NoteTone = r.ReportMet ? Tone.Good : Tone.Warn, Badge = s.Get(r.ReportMet ? "manager.met" : "manager.missed"),
            };
            vm.Lines.Add(res);
            vm.Lines.Add(LineVm.Header(s.Get("manager.changed")));
            string why = s.Get("manager.why." + style.Id);
            string price = style.PriceFactor == Fixed.One ? s.Get("manager.priceAt") : style.PriceFactor < Fixed.One
                ? s.Get("manager.priceUnder", "pct", ctx.Fmt.Percent(Fixed.One - style.PriceFactor)) : s.Get("manager.priceOver", "pct", ctx.Fmt.Percent(style.PriceFactor - Fixed.One));
            if (t.IsShop || t.IsTuition || (t.Input >= 0 && !t.ByRecipe && !t.IsKiln))
                vm.Lines.Add(new LineVm(s.Get(t.IsShop || t.IsTuition ? "manager.didPrice" : "manager.didFee", "price", price), why, "managerStyle", b.Id));
            if (t.IsShop)
                vm.Lines.Add(new LineVm(s.Get(style.Quality == 2 ? "manager.didGood" : "manager.didStandard"), why, "managerStyle", b.Id));
            if (ShopRules.Stocked(t))
                vm.Lines.Add(new LineVm(s.Get("manager.didStock", "n", ctx.Fmt.Integer(style.StockDays)), s.Get(style.StockDays > 1 ? "manager.whyMoreStock" : "manager.whyOneStock"), "managerStyle", b.Id));
            else if (t.ByRecipe && !t.TakesMilk && b.BuyFromMarket)
                vm.Lines.Add(new LineVm(s.Get("manager.didAhead", "n", ctx.Fmt.Integer(style.StockDays)), s.Get(style.StockDays > 1 ? "manager.whyMoreStock" : "manager.whyOneStock"), "managerStyle", b.Id));
            Worker w = sim.State.People.WorkerAt(b.Id);
            if (w != null && w.HiredAt >= m.HiredAt)
                vm.Lines.Add(new LineVm(s.Get("manager.didHire", "name", PeopleWords.Name(ctx, w.Name)), s.Get("manager.hire." + style.Id, "wage", ctx.Fmt.Coins(w.Wage)), "managerStyle", b.Id));
            vm.Lines.Add(LineVm.Header(s.Get("manager.howWent")));
            vm.Lines.Add(new LineVm(s.Get("manager.misDays"), s.Get("manager.misDaysValue", "n", ctx.Fmt.Integer(r.ReportMisDays), "of", ctx.Fmt.Integer(r.ReportDays)), "managerMiss", b.Id) { Note = s.Get("manager.misDaysNote") });
            vm.Lines.Add(new LineVm(s.Get("manager.payThen"), ctx.Fmt.Coins(m.Wage + r.ReportShare), "managerWage", b.Id) { Note = s.Get("manager.payThenNote") });
            if (r.Misses >= 2)
                vm.Lines.Add(new LineVm(s.Get("manager.sour", "name", ManagerWords.Name(ctx, m)), s.Get("manager.sourWhy"), "managerRaise", b.Id) { NoteTone = Tone.Warn });
            return vm;
        }
    }
}
