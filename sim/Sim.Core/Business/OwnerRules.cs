using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Business
{
    /// <summary>
    /// Investors, partners and buy-outs (spec 028 §3.4, ADR 0159). At a close a business that made a profit may be offered one thing (an investor's cash for a share, a partner joining, a rival
    /// offering to buy it); you answer in 48 hours or 24 for a buy-out, and no answer is no. A share is a liability of the business, is paid its part of each season's profit, is bought back at
    /// the worth × the share and is settled first when the business is sold or closed.
    /// </summary>
    public static class OwnerRules
    {
        private const long Hour = 3600;

        // ---- reading ----

        /// <summary>The share of the business held by outside owners together.</summary>
        public static Fixed Outside(Business b)
        {
            Fixed sum = Fixed.Zero;
            foreach (Stake k in b.Stakes) sum += k.Share;
            return sum;
        }

        /// <summary>What outside owners put in and the business still owes them, on its books.</summary>
        public static long Liability(Business b) { return -b.Ledger.Balance(Account.OutsideEquity); }

        public static Stake Of(Business b, StakeKind kind) { return b.Stakes.Find(x => x.Kind == kind); }

        /// <summary>What it costs to buy a share back: the worth now × the share, to the coin.</summary>
        public static long BuyBackPrice(Simulation sim, Business b, Stake k) { return Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, k.Share)); }

        /// <summary>Seasons still to wait before an investor's share may be bought back (a partner is bought out any time).</summary>
        public static long SeasonsLeft(Simulation sim, Stake k)
        {
            if (k.Kind == StakeKind.Partner) return 0;
            return System.Math.Max(0, k.Period + ExitRules.Defs(sim).BuyBackSeasons - sim.Period);
        }

        public static DealOffer OfferFor(Simulation sim, long businessId) { return ExitRules.State(sim)?.Offers.Find(x => x.BusinessId == businessId); }

        public static DealOffer Find(Simulation sim, long id) { return ExitRules.State(sim)?.Offers.Find(x => x.Id == id); }

        /// <summary>The household a partner is, or null (she may have moved on).</summary>
        public static Household Home(Simulation sim, Stake k) { return k.Kind == StakeKind.Partner ? sim.State.Citizens?.Household(k.Who) : null; }

        // ---- the close ----

        /// <summary>
        /// After the tax at a close: each outside share is paid its part of the season's profit (what the books show after the tax), from the business's cash and never when it lost.
        /// A distribution, not an expense. A partner's share is paid into her household's savings.
        /// </summary>
        internal static void PayProfit(Simulation sim, Business b, long closing)
        {
            if (b.Stakes.Count == 0 || b.Status != BusinessStatus.Open) return;
            long profit = BusinessBooks.NetProfit(BusinessBooks.Report(b, closing));
            long now = sim.State.Clock.SimTime;
            foreach (Stake k in b.Stakes)
            {
                long share = profit > 0 ? System.Math.Min(Money.MulRate(profit, k.Share), System.Math.Max(0, b.Ledger.Cash)) : 0;
                k.Paid = share;
                if (share <= 0) continue;
                b.Ledger.Post(now, closing, "ledger.outsideShare", "business:" + b.Id, new Posting(Account.OutsideDrawings, share), new Posting(Account.Cash, -share));
                Household x = Home(sim, k);
                if (x != null) x.Savings += share;
            }
        }

        /// <summary>At the season close, live (never while away): a business that made a profit may be offered one thing, once at a time (ADR 0159).</summary>
        internal static void AtClose(Simulation sim) { AtClose(sim, sim.Period); }

        internal static void AtClose(Simulation sim, long period)
        {
            if (!ExitRules.On(sim) || sim.State.Clock.IsAway) return;
            ExitDef d = ExitRules.Defs(sim);
            ExitState x = sim.State.Township.Exit;
            long now = sim.State.Clock.SimTime;
            ulong seed = sim.State.Seed, turn = (ulong)period * 8;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status != BusinessStatus.Open || b.Reports.Count == 0 || BusinessBooks.NetProfit(b.Reports[b.Reports.Count - 1]) <= 0) continue;
                if (OfferFor(sim, b.Id) != null) continue;
                ulong key = (ulong)b.Id | (1UL << 40);
                if ((long)Rng.Below(Rng.Next(seed, RngStream.Exit, key, turn + 1), 1000) >= Money.MulRate(1000, d.OfferChance)) continue;
                Worth w = ExitRules.Valuation(sim, b);
                Fixed room = d.MaxOutside - Outside(b);
                var kinds = new List<DealKind>();
                if (!b.Listed && w.Value >= d.InvestorValueMin && Of(b, StakeKind.Investor) == null && room >= d.InvestorShares[0]) kinds.Add(DealKind.Investor);
                Household partner = null;
                if (!b.Listed && Of(b, StakeKind.Partner) == null && room >= d.PartnerProfit)
                {
                    partner = PartnerFor(sim, Money.RoundToCoin(Money.MulRate(w.Value, d.PartnerStake)), Rng.Next(seed, RngStream.Exit, key, turn + 2));
                    if (partner != null) kinds.Add(DealKind.Partner);
                }
                if (!b.Listed && (b.BoughtOutOfferedAt == 0 || period + 1 - b.BoughtOutOfferedAt >= d.BuyOutGapSeasons)) kinds.Add(DealKind.BuyOut);
                if (kinds.Count == 0) continue;
                DealKind kind = kinds[(int)Rng.Below(Rng.Next(seed, RngStream.Exit, key, turn + 3), (ulong)kinds.Count)];
                var o = new DealOffer { Id = x.NextOfferId++, Kind = kind, BusinessId = b.Id, RaisedAt = now };
                if (kind == DealKind.Investor)
                {
                    var fit = new List<Fixed>();
                    foreach (Fixed s in d.InvestorShares) if (s <= room) fit.Add(s);
                    o.Share = fit[(int)Rng.Below(Rng.Next(seed, RngStream.Exit, key, turn + 4), (ulong)fit.Count)];
                    o.Who = (long)Rng.Below(Rng.Next(seed, RngStream.Exit, key, turn + 5), (ulong)d.Investors.Count);
                    o.Cash = Money.RoundToCoin(Money.MulRate(w.Value, o.Share));
                    o.ExpiresAt = now + d.OfferHours * Hour;
                }
                else if (kind == DealKind.Partner)
                {
                    o.Share = d.PartnerProfit;
                    o.Who = partner.Id;
                    o.Cash = Money.RoundToCoin(Money.MulRate(w.Value, d.PartnerStake));
                    o.ExpiresAt = now + d.OfferHours * Hour;
                }
                else
                {
                    Fixed span = d.BuyOutHigh - d.BuyOutLow;
                    o.Share = d.BuyOutLow + span * Fixed.FromRatio((long)Rng.Below(Rng.Next(seed, RngStream.Exit, key, turn + 4), 1001), 1000);
                    o.Cash = ExitRules.PriceAt(w, o.Share);
                    o.Who = BuyerOf(sim, b);
                    long worth = Reporting.NetWorth(sim).Total;
                    o.ExpiresAt = now + (worth > 0 && w.Value >= Money.MulRate(worth, d.BuyOutBigShare) ? d.BuyOutBigHours : d.BuyOutHours) * Hour;
                    b.BoughtOutOfferedAt = period + 1;
                }
                x.Offers.Add(o);
                sim.Emit(new DealOffered(now, o.Id, b.Id, (int)kind));
            }
        }

        /// <summary>A household of yours with savings enough to put in <paramref name="cash"/>, picked by the roll; null when none has.</summary>
        private static Household PartnerFor(Simulation sim, long cash, ulong roll)
        {
            var able = new List<Household>();
            foreach (Household h in Town.TownLifeRules.Yours(sim)) if (h.Savings >= cash && cash > 0) able.Add(h);
            return able.Count == 0 ? null : able[(int)Rng.Below(roll, (ulong)able.Count)];
        }

        /// <summary>A shop's buyer is a rival; any other kind finds a regional buyer.</summary>
        private static int BuyerOf(Simulation sim, Business b)
        {
            return IndexOf(ExitRules.Defs(sim), BusinessRules.TypeOf(sim, b).IsShop ? "rival" : "regional");
        }

        private static int IndexOf(ExitDef d, string kind)
        {
            for (int i = 0; i < d.Buyers.Count; i++) if (d.Buyers[i] == kind) return i;
            return 0;
        }

        /// <summary>Hourly, live or away: an offer not answered in time is no.</summary>
        internal static void Hourly(Simulation sim)
        {
            ExitState x = ExitRules.State(sim);
            if (x == null || x.Offers.Count == 0) return;
            long now = sim.State.Clock.SimTime;
            foreach (DealOffer o in x.Offers.ToArray())
                if (now >= o.ExpiresAt) Answer(sim, o, false, true);
        }

        /// <summary>A business that is gone takes its offers with it, quietly.</summary>
        internal static void Gone(Simulation sim, long businessId)
        {
            ExitState x = ExitRules.State(sim);
            x?.Offers.RemoveAll(o => o.BusinessId == businessId);
        }

        private static void Answer(Simulation sim, DealOffer o, bool accepted, bool lapsed)
        {
            sim.State.Township.Exit.Offers.Remove(o);
            sim.Emit(new DealAnswered(sim.State.Clock.SimTime, o.Id, o.BusinessId, (int)o.Kind, accepted, lapsed));
        }

        // ---- your answers ----

        internal static CommandResult Decline(Simulation sim, long offerId)
        {
            DealOffer o = Find(sim, offerId);
            if (o == null) return CommandResult.Reject(RejectReason.UnknownOffer);
            Answer(sim, o, false, false);
            return CommandResult.Ok;
        }

        /// <summary>An investor's cash or a partner's savings come into the business and the share is theirs.</summary>
        internal static CommandResult Accept(Simulation sim, long offerId)
        {
            DealOffer o = Find(sim, offerId);
            if (o == null || o.Kind == DealKind.BuyOut) return CommandResult.Reject(RejectReason.UnknownOffer);
            Business b = sim.State.Businesses.Find(o.BusinessId);
            if (b == null || b.Status != BusinessStatus.Open) return CommandResult.Reject(RejectReason.NotOpen);
            ExitDef d = ExitRules.Defs(sim);
            StakeKind kind = o.Kind == DealKind.Investor ? StakeKind.Investor : StakeKind.Partner;
            if (Of(b, kind) != null) return CommandResult.Reject(RejectReason.HasStake);
            if (Outside(b) + o.Share > d.MaxOutside) return CommandResult.Reject(RejectReason.OutsideCap);
            Household from = null;
            if (kind == StakeKind.Partner)
            {
                from = sim.State.Citizens?.Household(o.Who);
                if (from == null || from.Savings < o.Cash) return CommandResult.Reject(RejectReason.InsufficientFunds);
                from.Savings -= o.Cash;
            }
            long now = sim.State.Clock.SimTime;
            b.Ledger.Post(now, sim.Period, "ledger.outsideIn", "business:" + b.Id, new Posting(Account.Cash, o.Cash), new Posting(Account.OutsideEquity, -o.Cash));
            BusinessBooks.Sweep(sim, b);
            b.Stakes.Add(new Stake { Kind = kind, Who = o.Who, Share = o.Share, Put = o.Cash, Period = sim.Period, At = now });
            Answer(sim, o, true, false);
            return CommandResult.Ok;
        }

        /// <summary>A rival's offer is taken: the business is sold today at that price, never under what it holds (§3.3), the outside shares paid first.</summary>
        internal static CommandResult AcceptBuyOut(Simulation sim, long offerId)
        {
            DealOffer o = Find(sim, offerId);
            if (o == null || o.Kind != DealKind.BuyOut) return CommandResult.Reject(RejectReason.UnknownOffer);
            Business b = sim.State.Businesses.Find(o.BusinessId);
            if (b == null || b.Status != BusinessStatus.Open) return CommandResult.Reject(RejectReason.NotOpen);
            Answer(sim, o, true, false);
            b.CompletesAt = 0; // a listing is over: this buyer has come
            ExitRules.SellNow(sim, b, o.Share, (int)o.Who, o.Cash);
            return CommandResult.Ok;
        }

        /// <summary>You buy a share back at the worth now × the share (an investor's after the seasons it must stand, a partner's any time): the cash goes out, the share becomes yours.</summary>
        internal static CommandResult BuyBack(Simulation sim, long businessId, StakeKind kind)
        {
            if (!ExitRules.On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            Stake k = Of(b, kind);
            if (k == null) return CommandResult.Reject(RejectReason.NoStake);
            if (SeasonsLeft(sim, k) > 0) return CommandResult.Reject(RejectReason.BuyBackTooEarly);
            long price = BuyBackPrice(sim, b, k);
            if (!sim.State.Ledger.CanAfford(price)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            var mine = new List<Posting> { new Posting(Account.Cash, -price), new Posting(Account.InvestmentInBusinesses, k.Put) };
            if (price > k.Put) mine.Add(new Posting(Account.LossOnAssetSales, price - k.Put));
            else if (price < k.Put) mine.Add(new Posting(Account.GainOnAssetSales, price - k.Put));
            sim.State.Ledger.Post(now, sim.Period, "ledger.shareBoughtBack", reference, mine.ToArray());
            b.Ledger.Post(now, sim.Period, "ledger.shareBoughtBack", reference, new Posting(Account.OutsideEquity, k.Put), new Posting(Account.OwnersCapital, -k.Put));
            Household x = Home(sim, k);
            if (x != null) x.Savings += price;
            b.Stakes.Remove(k);
            sim.Emit(new ShareBoughtBack(now, b.Id, (int)kind, price));
            return CommandResult.Ok;
        }

        // ---- a sale or a closing ----

        /// <summary>
        /// The business is sold or closed: each outside share is paid its part of the cash left after its debts, before the tax, so the tax falls on your gain alone. Returns what was paid.
        /// The difference from what was put in is a gain or a loss on the sale.
        /// </summary>
        internal static long Settle(Simulation sim, Business b)
        {
            if (b.Stakes.Count == 0) return 0;
            Ledger l = b.Ledger;
            long now = sim.State.Clock.SimTime, cash = System.Math.Max(0, l.Cash), paid = 0;
            foreach (Stake k in b.Stakes)
            {
                long pay = System.Math.Min(Money.MulRate(cash, k.Share), System.Math.Max(0, l.Cash));
                var parts = new List<Posting>();
                if (k.Put != 0) parts.Add(new Posting(Account.OutsideEquity, k.Put));
                if (pay != 0) parts.Add(new Posting(Account.Cash, -pay));
                if (pay > k.Put) parts.Add(new Posting(Account.LossOnAssetSales, pay - k.Put));
                else if (pay < k.Put) parts.Add(new Posting(Account.GainOnAssetSales, pay - k.Put));
                if (parts.Count > 0) l.Post(now, sim.Period, "ledger.outsideSettled", "business:" + b.Id, parts.ToArray());
                Household x = Home(sim, k);
                if (x != null) x.Savings += pay;
                paid += pay;
            }
            b.Stakes.Clear();
            return paid;
        }
    }
}
