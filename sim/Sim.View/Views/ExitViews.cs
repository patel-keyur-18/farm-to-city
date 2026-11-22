using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// What a business is worth, and selling or branching it (spec 028 §3.3, Preview 17): a Settings section, the Sell or expand sheet, and the card that says where
    /// every coin of a sale went.
    /// </summary>
    public static class ExitSheet
    {
        public const string Kind = "SellExpand", ReceiptKind = "SaleReceipt";

        public static bool Applies(ViewContext ctx, Business b) { return ExitRules.On(ctx.Sim) && b.Status != BusinessStatus.Closed; }

        /// <summary>The worth in a sentence: "4.5 times a year's profit of 40,000 coins", or "what it holds" when that is more.</summary>
        private static string Basis(ViewContext ctx, Worth w)
        {
            var s = ctx.Strings;
            if (w.Value == w.Book) return s.Get("exit.basisHolds");
            return s.Get("exit.basisEarns", "times", ctx.Fmt.Decimal1(w.Multiple), "profit", ctx.Fmt.Coins(w.Profit));
        }

        /// <summary>Settings: the worth with its ?, and the way to sell or expand.</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b)
        {
            if (!Applies(ctx, b)) return;
            var s = ctx.Strings;
            Worth w = ExitRules.Valuation(ctx.Sim, b);
            vm.Lines.Add(LineVm.Header(s.Get("exit.section")));
            vm.Lines.Add(new LineVm(s.Get("exit.worth"), ctx.Fmt.Coins(w.Value), "businessWorth", b.Id) { Icon = "coin", Note = b.Listed ? s.Get("exit.listedNote") : Basis(ctx, w) });
            vm.Lines.Add(new LineVm(s.Get("exit.sellRow"), s.Get("exit.sellRowWhy")) { Icon = "coin", Action = ActionVm.Open("sellExpand", s.Get("exit.sell"), new SheetRequest(Kind, b.Id)) });
        }

        public static SheetVm Build(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null || !Applies(ctx, b)) return new SheetVm { Icon = "coin", Title = s.Get("reject.UnknownBusiness") };
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            ExitDef d = ExitRules.Defs(sim);
            Worth w = ExitRules.Valuation(sim, b);
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("exit.title"), Subtitle = BusinessWords.Name(ctx, t) };
            vm.Lines.Add(LineVm.Header(s.Get("exit.worthHeader")));
            vm.Lines.Add(new LineVm(s.Get("exit.worth"), ctx.Fmt.Coins(w.Value), "businessWorth", id) { Icon = "coin", Note = Basis(ctx, w) });
            vm.Lines.Add(new LineVm(s.Get("exit.holds"), ctx.Fmt.Coins(w.Book), "businessHolds", id) { Icon = "coin", Note = s.Get("exit.holdsNote") });
            vm.Lines.Add(LineVm.Header(s.Get("exit.sellHeader")));
            long low = ExitRules.PriceAt(w, d.BuyerLow), high = ExitRules.PriceAt(w, d.BuyerHigh), rushed = ExitRules.PriceAt(w, d.RushedShare);
            if (b.Listed)
            {
                long left = b.CompletesAt - ctx.Now;
                var listed = new LineVm(s.Get("exit.listed", "ago", ctx.Fmt.Duration(ctx.Now - b.ListedAt)), b.Rushed ? s.Get("exit.listedRushed", "left", ctx.Fmt.Duration(System.Math.Max(0, left))) : s.Get("exit.listedWait", "left", ctx.Fmt.Duration(System.Math.Max(0, left))), "businessSale", id)
                    { Icon = "coin", Note = s.Get("exit.runsUntil", "name", BusinessWords.InSentence(ctx, t)) };
                long idc = id;
                listed.Action = ActionVm.Checked(ctx, "cancelSale", s.Get("exit.cancel"), null, ActionKind.Secondary, at => new CancelBusinessSale(at, idc));
                vm.Lines.Add(listed);
            }
            else
            {
                long idc = id;
                var list = new LineVm(s.Get("exit.list"), s.Get("exit.listWhy", "low", ctx.Fmt.Coins(low), "high", ctx.Fmt.Coins(high), "days", s.Get("exit.days", "min", ctx.Fmt.Integer(d.SaleDaysMin), "max", ctx.Fmt.Integer(d.SaleDaysMax)),
                    "lo", ctx.Fmt.Percent(d.BuyerLow), "hi", ctx.Fmt.Percent(d.BuyerHigh)), "businessSale", id) { Icon = "coin" };
                list.Action = ActionVm.Checked(ctx, "list", s.Get("exit.listButton"), null, ActionKind.Secondary, at => new ListBusiness(at, idc, false));
                vm.Lines.Add(list);
                var rush = new LineVm(s.Get("exit.rush"), s.Get("exit.rushWhy", "pct", ctx.Fmt.Percent(d.RushedShare), "price", ctx.Fmt.Coins(rushed)), "businessSale", id) { Icon = "coin" };
                rush.Action = ActionVm.Checked(ctx, "rush", s.Get("exit.rushButton"), null, ActionKind.Secondary, at => new ListBusiness(at, idc, true));
                vm.Lines.Add(rush);
            }
            vm.Lines.Add(LineVm.Header(s.Get("exit.branchHeader")));
            int branches = ExitRules.Branches(sim, b.Type);
            RejectReason full = branches >= d.MaxBranches ? RejectReason.TooManyBranches : RejectReason.None;
            string cost = ctx.Fmt.Coins(BusinessRules.Cost(sim, t, t.IsShop ? 0 : -1));
            var branch = new LineVm(s.Get("exit.branchRow", "name", BusinessWords.InSentence(ctx, t)), s.Get("exit.branchWhy", "cost", cost, "n", ctx.Fmt.Integer(branches), "max", ctx.Fmt.Integer(d.MaxBranches)), "businessBranch", b.Type) { Icon = t.Id };
            branch.Action = full == RejectReason.None
                ? ActionVm.Open("branch", s.Get("exit.branchButton"), new SheetRequest("BusinessResearch", b.Type))
                : new ActionVm("branch", s.Get("exit.branchButton"), null, ActionKind.Secondary, false, ctx.Reject(full), null);
            vm.Lines.Add(branch);
            return vm;
        }

        /// <summary>The card of a sale: the price as a share of the worth, then where every coin went.</summary>
        public static SheetVm Receipt(ViewContext ctx, long saleId)
        {
            var s = ctx.Strings;
            SaleRecord r = ExitRules.State(ctx.Sim)?.Sales.Find(x => x.Id == saleId);
            if (r == null) return new SheetVm { Icon = "coin", Title = s.Get("exit.receiptNone") };
            ExitDef d = ExitRules.Defs(ctx.Sim);
            string type = ctx.Sim.Config.Game.Businesses.Types[ctx.Sim.Config.Game.Businesses.IndexOfType(r.TypeId)].NameKey;
            string name = s.Get(type).ToLowerInvariant();
            var vm = new SheetVm
            {
                Icon = r.TypeId, Title = s.Get("exit.soldTitle", "name", name),
                Subtitle = s.Get("exit.soldSubtitle", "buyer", s.Get("exit.buyer." + d.Buyers[r.Buyer]), "price", ctx.Fmt.Coins(r.Price), "pct", ctx.Fmt.Percent(r.Worth <= 0 ? r.Share : Fixed.FromRatio(r.Price, r.Worth))), // the share it came to, which the floor at what it held can raise
            };
            vm.Lines.Add(new LineVm(s.Get("exit.receiptPrice"), ctx.Fmt.Coins(r.Price), "businessSale", 0) { Note = s.Get("exit.receiptPriceNote", "worth", ctx.Fmt.Coins(r.Worth)) });
            vm.Lines.Add(new LineVm(s.Get("exit.receiptTax", "gain", ctx.Fmt.Coins(r.Gain)), ctx.Fmt.Coins(-r.Tax), "businessSale", 0));
            if (r.Severance > 0) vm.Lines.Add(new LineVm(s.Get("exit.receiptSeverance"), ctx.Fmt.Coins(-r.Severance), "businessSale", 0));
            vm.Lines.Add(new LineVm(s.Get("exit.receiptToYou"), ctx.Fmt.Coins(r.ToYou), "businessSale", 0) { Note = s.Get("exit.receiptNote") });
            return vm;
        }
    }
}
