using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;

namespace Sim.View.Views
{
    /// <summary>
    /// The Businesses hub (spec 030 §3.3, US-267): every business you own on one sheet, with what each earned this season so far and the
    /// cash inside it, and the way to take cash out or see its card. View only: it reads the books and the ledger and changes nothing.
    /// </summary>
    public static class BusinessHub
    {
        public const string Kind = "BusinessHub";

        /// <summary>The businesses the hub lists: open first, then being built, each by id. Closed ones are not listed.</summary>
        public static List<Business> Listed(Simulation sim)
        {
            var list = new List<Business>();
            if (sim.State.Businesses == null) return list;
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status == BusinessStatus.Open) list.Add(b);
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status == BusinessStatus.Building) list.Add(b);
            return list;
        }

        /// <summary>Whether it needs you: open with nobody working it, or its overdraft used to the limit so its inputs stop (<see cref="BusinessBooks.Short"/>).</summary>
        public static bool NeedsYou(Simulation sim, Business b) { return BusinessBooks.Short(sim, b); }

        /// <summary>How many of your businesses need you: the HUD button's badge.</summary>
        public static int NeedsCount(Simulation sim)
        {
            int n = 0;
            foreach (Business b in Listed(sim)) if (NeedsYou(sim, b)) n++;
            return n;
        }

        /// <summary>The button shows once a business is open or being built.</summary>
        public static bool Shown(Simulation sim) { return Listed(sim).Count > 0; }

        /// <summary>Its profit this season so far: the figure its Books tab shows.</summary>
        public static long Profit(Simulation sim, Business b) { return BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period)); }

        public static long TotalProfit(Simulation sim)
        {
            long sum = 0;
            foreach (Business b in Listed(sim)) sum += Profit(sim, b);
            return sum;
        }

        public static long TotalCash(Simulation sim)
        {
            long sum = 0;
            foreach (Business b in Listed(sim)) sum += b.Ledger.Cash;
            return sum;
        }

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "flour_mill", Title = s.Get("bhub.title"), Subtitle = s.Get("bhub.subtitle") };
            List<Business> rows = Listed(sim);
            vm.Lines.Add(new LineVm(s.Get("bhub.profit"), ctx.Fmt.Coins(TotalProfit(sim)), "hubProfit", 0));
            vm.Lines.Add(new LineVm(s.Get("bhub.cash"), ctx.Fmt.Coins(TotalCash(sim)), "hubCash", 0));
            vm.Lines.Add(new LineVm(s.Get("bhub.start"), null) { Note = s.Get("bhub.startNote"), Action = ActionVm.Open("start", s.Get("bhub.startButton"), new SheetRequest("Businesses", 0)) });
            vm.Lines.Add(LineVm.Header(s.Get("bhub.yours")));
            if (rows.Count == 0) vm.Lines.Add(new LineVm(s.Get("bhub.none"), s.Get("bhub.noneNote")));
            foreach (Business b in rows) AddBusiness(ctx, vm, b);
            return vm;
        }

        private static void AddBusiness(ViewContext ctx, SheetVm vm, Business b)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            bool building = b.Status == BusinessStatus.Building;
            var head = new LineVm(BusinessWords.Name(ctx, t), null)
            {
                Icon = building ? t.Id + "_building" : t.Id,
                Note = s.Get("bhub.status", "status", BusinessSheet.StatusText(ctx, b), "who", BusinessWords.Worker(ctx, b)),
                Action = ActionVm.Open("see:" + b.Id, s.Get("action.see"), new SheetRequest("Business", b.Id)),
            };
            if (NeedsYou(sim, b)) head.Tag = s.Get("bhub.needsYou");
            if (!building && ShopRules.Rated(t) && ShopRules.Defs(sim) != null) head = WithStars(ctx, head, b);
            vm.Lines.Add(head);
            if (NeedsYou(sim, b))
                vm.Lines.Add(new LineVm(s.Get(b.Worker == WorkerKind.None ? "business.needsWorkerHire" : "bhub.overdraft"), null) { Banner = true, NoteTone = Tone.Warn });
            vm.Lines.Add(new LineVm(s.Get("bhub.rowProfit"), ctx.Fmt.Coins(Profit(sim, b)), "hubProfit", b.Id));
            long drawable = BusinessBooks.Drawable(b);
            var take = drawable > 0
                ? ActionVm.Open("take:" + b.Id, s.Get("business.takeCash"), new SheetRequest("BusinessCash", b.Id, BusinessCashSheet.Take))
                : new ActionVm("take:" + b.Id, s.Get("business.takeCash"), null, ActionKind.Open, false, s.Get("bhub.nothingOut"), null, new SheetRequest("BusinessCash", b.Id, BusinessCashSheet.Take));
            vm.Lines.Add(new LineVm(s.Get("bhub.rowCash", "name", BusinessWords.InSentence(ctx, t)), ctx.Fmt.Coins(b.Ledger.Cash), "businessCash", b.Id) { Action = take });
        }

        private static LineVm WithStars(ViewContext ctx, LineVm head, Business b)
        {
            return new LineVm(head.Label, ShopWords.Stars(ctx, b.Stars), "shopStars", b.Id) { Icon = head.Icon, Note = head.Note, Action = head.Action, Tag = head.Tag };
        }
    }
}
