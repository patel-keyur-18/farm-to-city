using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// Investors, partners and buy-outs (spec 028 §3.4, Preview 17): who owns a business and what each outside owner is owed, the card of an offer (an investor, a partner or a rival) with
    /// every number and the answer that happens if you wait, and the bell for offers waiting.
    /// </summary>
    public static class OwnerSheet
    {
        public const string OwnersKind = "Owners", CardKind = "DealCard";

        private static ExitDef Defs(ViewContext ctx) { return ExitRules.Defs(ctx.Sim); }

        /// <summary>Who an outside owner is: the investor's name from the content, a partner's household from the town.</summary>
        public static string Who(ViewContext ctx, StakeKind kind, long who)
        {
            var s = ctx.Strings;
            if (kind == StakeKind.Investor) return s.Get("exit.investor." + Defs(ctx).Investors[(int)who]);
            Household x = ctx.Sim.State.Citizens?.Household(who);
            return x == null ? s.Get("owner.aTownsperson") : TownWords.Household(ctx, x);
        }

        /// <summary>"You 85% · Ramesh Seth 15%" for a Settings row.</summary>
        public static string Split(ViewContext ctx, Business b)
        {
            var s = ctx.Strings;
            string text = s.Get("owner.you", "pct", ctx.Fmt.Percent(Fixed.One - OwnerRules.Outside(b)));
            foreach (Stake k in b.Stakes) text += " · " + s.Get("owner.holder", "name", Who(ctx, k.Kind, k.Who), "pct", ctx.Fmt.Percent(k.Share));
            return text;
        }

        private static string TypeName(ViewContext ctx, Business b) { return BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, b)); }

        // ---- Settings and the owners' sheet ----

        /// <summary>Settings, under the worth: who owns it, and the way to the owners' sheet (a waiting offer is flagged).</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b)
        {
            if (!ExitSheet.Applies(ctx, b)) return;
            var s = ctx.Strings;
            DealOffer o = OwnerRules.OfferFor(ctx.Sim, b.Id);
            var row = new LineVm(s.Get("owner.row"), Split(ctx, b), "ownerRow", b.Id) { Icon = "person", Action = ActionVm.Open("owners", s.Get("owner.open"), new SheetRequest(OwnersKind, b.Id)) };
            if (o != null) { row.Note = s.Get("owner.waiting", "time", ctx.Fmt.Duration(System.Math.Max(0, o.ExpiresAt - ctx.Now))); row.NoteTone = Tone.Good; }
            vm.Lines.Add(row);
        }

        public static SheetVm Owners(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null || !ExitSheet.Applies(ctx, b)) return new SheetVm { Icon = "person", Title = s.Get("reject.UnknownBusiness") };
            ExitDef d = Defs(ctx);
            string name = TypeName(ctx, b);
            var vm = new SheetVm { Icon = "person", Title = s.Get("owner.title", "name", name), Subtitle = BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, b)) };
            vm.Lines.Add(LineVm.Header(s.Get("owner.header", "name", name)));
            vm.Lines.Add(new LineVm(s.Get("owner.youRow"), ctx.Fmt.Percent(Fixed.One - OwnerRules.Outside(b)), "ownerRow", b.Id) { Icon = "person", Note = s.Get("owner.youNote", "coins", ctx.Fmt.Coins(System.Math.Max(0, BusinessBooks.CapitalIn(b)))) });
            foreach (Stake k in b.Stakes)
            {
                StakeKind kind = k.Kind;
                long price = OwnerRules.BuyBackPrice(sim, b, k), left = OwnerRules.SeasonsLeft(sim, k);
                string note = s.Get("owner.stakeNote", "put", ctx.Fmt.Coins(k.Put), "ago", ctx.Fmt.Duration(System.Math.Max(0, ctx.Now - k.At))) + " · " + s.Get("owner.paid", "coins", ctx.Fmt.Coins(k.Paid));
                var line = new LineVm(Who(ctx, k.Kind, k.Who) + " · " + s.Get(kind == StakeKind.Investor ? "owner.investor" : "owner.partner"), ctx.Fmt.Percent(k.Share), "ownerPaid", b.Id) { Icon = "person", Note = note };
                long bid = b.Id;
                string verb = s.Get(kind == StakeKind.Partner ? "owner.buyOut" : "owner.buyBack");
                line.Action = left > 0
                    ? new ActionVm("buyback:" + kind, verb, s.Get("owner.backIn", "n", ctx.Fmt.Integer(left)), ActionKind.Secondary, false, s.Get("reject.BuyBackTooEarly"), null)
                    : ActionVm.Checked(ctx, "buyback:" + kind, verb, s.Get("owner.backFor", "coins", ctx.Fmt.Coins(price)), ActionKind.Secondary, at => new BuyBackShare(at, bid, kind));
                vm.Lines.Add(line);
            }
            if (OwnerRules.Of(b, StakeKind.Investor) == null) vm.Lines.Add(new LineVm(s.Get("owner.noInvestor"), s.Get("owner.noInvestorNote"), "investorOffer", b.Id) { Icon = "person", Dim = true });
            if (OwnerRules.Of(b, StakeKind.Partner) == null) vm.Lines.Add(new LineVm(s.Get("owner.noPartner"), s.Get("owner.noPartnerNote", "pct", ctx.Fmt.Percent(d.PartnerProfit)), "partnerOffer", b.Id) { Icon = "person", Dim = true });
            vm.Lines.Add(LineVm.Header(s.Get("owner.limitHeader")));
            vm.Lines.Add(new LineVm(s.Get("owner.limit"), s.Get("owner.limitValue", "pct", ctx.Fmt.Percent(OwnerRules.Outside(b)), "max", ctx.Fmt.Percent(d.MaxOutside)), "outsideLimit", b.Id) { Meter = (int)(OwnerRules.Outside(b) * Fixed.FromInt(100) / d.MaxOutside).RoundToLong() });
            DealOffer o = OwnerRules.OfferFor(sim, b.Id);
            if (o != null)
                vm.Lines.Add(new LineVm(Title(ctx, o), s.Get("owner.waiting", "time", ctx.Fmt.Duration(System.Math.Max(0, o.ExpiresAt - ctx.Now))), "dealOffer", o.Id) { Icon = "deed", Action = ActionVm.Open("deal:" + o.Id, s.Get("action.open"), new SheetRequest(CardKind, o.Id)) });
            vm.Lines.Add(new LineVm(s.Get("owner.backNote"), null, "shareBack", b.Id) { Dim = true });
            return vm;
        }

        // ---- the offer's card ----

        public static string Title(ViewContext ctx, DealOffer o)
        {
            var s = ctx.Strings;
            Business b = ctx.Sim.State.Businesses?.Find(o.BusinessId);
            string name = b == null ? s.Get("event.aBusiness") : TypeName(ctx, b);
            switch (o.Kind)
            {
                case DealKind.Investor: return s.Get("deal.investor.title");
                case DealKind.Partner: return s.Get("deal.partner.title");
                default: return s.Get("deal.buyout.title", "name", name);
            }
        }

        private static long Estimate(ViewContext ctx, Business b, Fixed share)
        {
            if (b.Reports.Count == 0) return 0;
            long profit = BusinessBooks.NetProfit(b.Reports[b.Reports.Count - 1]);
            return profit > 0 ? Sim.Core.Finance.Money.MulRate(profit, share) : 0;
        }

        /// <summary>One card: what is offered, every number, the answer that happens if you wait (Preview 17 screens 4 and 6).</summary>
        public static SheetVm Card(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            DealOffer o = OwnerRules.Find(sim, id);
            Business b = o == null ? null : sim.State.Businesses.Find(o.BusinessId);
            if (o == null || b == null) return new SheetVm { Icon = "deed", Title = ctx.Reject(RejectReason.UnknownOffer) };
            ExitDef d = Defs(ctx);
            string name = TypeName(ctx, b);
            var vm = new SheetVm { Icon = "deed", Title = Title(ctx, o), Subtitle = s.Get("deal.tag") };
            string max = ctx.Fmt.Percent(d.MaxOutside);
            long leftTime = System.Math.Max(0, o.ExpiresAt - ctx.Now);
            long oid = o.Id;
            string yes;
            if (o.Kind == DealKind.BuyOut)
            {
                string buyer = s.Get("exit.buyer." + d.Buyers[(int)o.Who]);
                Worth w = ExitRules.Valuation(sim, b);
                vm.Lines.Add(new LineVm(s.Get("deal.buyout.about", "buyer", buyer, "price", ctx.Fmt.Coins(o.Cash), "pct", ctx.Fmt.Percent(w.Value <= 0 ? o.Share : Fixed.FromRatio(o.Cash, w.Value)), "name", name), null, "dealBuyout", o.Id));
                vm.Lines.Add(new LineVm(s.Get("deal.offerRow"), ctx.Fmt.Coins(o.Cash), "dealBuyout", o.Id));
                CommandResult r = sim.Preview(new AcceptBuyOut(ctx.Now, o.Id), out Simulation after);
                SaleRecord rec = r.Accepted && after.State.Township.Exit.Sales.Count > 0 ? after.State.Township.Exit.Sales[after.State.Township.Exit.Sales.Count - 1] : null;
                if (rec != null)
                {
                    if (rec.Outside > 0) vm.Lines.Add(new LineVm(s.Get("deal.outsideFirst"), ctx.Fmt.Coins(-rec.Outside), "ownerPaid", b.Id));
                    vm.Lines.Add(new LineVm(s.Get("exit.receiptTax", "gain", ctx.Fmt.Coins(rec.Gain)), ctx.Fmt.Coins(-rec.Tax), "businessSale", 0));
                    if (rec.Severance > 0) vm.Lines.Add(new LineVm(s.Get("deal.severance"), ctx.Fmt.Coins(-rec.Severance), "businessSale", 0));
                    vm.Lines.Add(new LineVm(s.Get("deal.youGet"), ctx.Fmt.Coins(rec.ToYou), "businessSale", 0) { Note = s.Get("deal.buyout.note") });
                }
                yes = s.Get("deal.buyout.yes");
                vm.Lines.Add(Decline(ctx, oid, s.Get("deal.no.buyout"), s.Get("deal.buyout.wait")));
                var accept = new LineVm(yes, null) { Note = s.Get("deal.buyout.yesNote") };
                accept.Action = ActionVm.Checked(ctx, "deal:accept", yes, null, ActionKind.Primary, at => new AcceptBuyOut(at, oid));
                vm.Lines.Add(accept);
            }
            else
            {
                bool investor = o.Kind == DealKind.Investor;
                string who = Who(ctx, investor ? StakeKind.Investor : StakeKind.Partner, o.Who);
                string about = investor
                    ? s.Get("deal.investor.about", "name", who, "cash", ctx.Fmt.Coins(o.Cash), "biz", name, "pct", ctx.Fmt.Percent(o.Share))
                    : s.Get("deal.partner.about", "name", who, "cash", ctx.Fmt.Coins(o.Cash), "biz", name, "pct", ctx.Fmt.Percent(o.Share));
                vm.Lines.Add(new LineVm(about, null, investor ? "investorOffer" : "partnerOffer", b.Id));
                string why = investor ? "investorOffer" : "partnerOffer";
                vm.Lines.Add(new LineVm(s.Get(investor ? "deal.investor.cash" : "deal.partner.cash", "biz", name), "+" + ctx.Fmt.Coins(o.Cash), why, b.Id));
                long est = Estimate(ctx, b, o.Share);
                vm.Lines.Add(new LineVm(s.Get("deal.profitRow"), s.Get("deal.profitValue", "pct", ctx.Fmt.Percent(o.Share), "est", ctx.Fmt.Coins(est)), "ownerPaid", b.Id));
                vm.Lines.Add(new LineVm(s.Get(investor ? "deal.investor.back" : "deal.partner.back"), investor ? s.Get("deal.investor.backValue", "n", ctx.Fmt.Integer(d.BuyBackSeasons)) : s.Get("deal.partner.backValue", "pct", ctx.Fmt.Percent(o.Share)), "shareBack", b.Id));
                vm.Lines.Add(new LineVm(s.Get("deal.afterRow"), s.Get("deal.afterValue", "pct", ctx.Fmt.Percent(OwnerRules.Outside(b) + o.Share), "max", max), "outsideLimit", b.Id));
                vm.Lines.Add(Decline(ctx, oid, s.Get("deal.no.offer"), s.Get("deal.wait")));
                yes = s.Get(investor ? "deal.investor.yes" : "deal.partner.yes");
                var accept = new LineVm(yes, null) { Note = s.Get(investor ? "deal.investor.yesNote" : "deal.partner.yesNote") };
                accept.Action = ActionVm.Checked(ctx, "deal:accept", yes, null, ActionKind.Primary, at => new AcceptDeal(at, oid));
                vm.Lines.Add(accept);
            }
            vm.Lines.Add(new LineVm(s.Get("deal.timer", "time", ctx.Fmt.Duration(leftTime)), null) { Dim = true });
            return vm;
        }

        /// <summary>The free answer, named as what happens if you wait (as an event card's default is).</summary>
        private static LineVm Decline(ViewContext ctx, long offerId, string label, string note)
        {
            var line = new LineVm(label, null) { Note = note, Tag = ctx.Strings.Get("event.ifYouWait") };
            line.Action = ActionVm.Checked(ctx, "deal:decline", ctx.Strings.Get("deal.declineButton"), null, ActionKind.Secondary, at => new DeclineDeal(at, offerId));
            return line;
        }

        // ---- the bell ----

        /// <summary>One alert for each offer waiting, with the time left to answer.</summary>
        public static void Alerts(ViewContext ctx, List<AlertVm> list)
        {
            ExitState x = ExitRules.State(ctx.Sim);
            if (x == null) return;
            var s = ctx.Strings;
            foreach (DealOffer o in x.Offers)
            {
                Business b = ctx.Sim.State.Businesses.Find(o.BusinessId);
                if (b == null) continue;
                list.Add(new AlertVm
                {
                    Level = AlertLevel.Decision,
                    Text = s.Get("alert.deal." + o.Kind, "name", TypeName(ctx, b), "time", ctx.Fmt.Duration(System.Math.Max(0, o.ExpiresAt - ctx.Now))),
                    Action = ActionVm.Open("deal:" + o.Id, s.Get("action.see"), new SheetRequest(CardKind, o.Id)),
                });
            }
        }
    }
}
