using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Business
{
    /// <summary>One of your households a shop has as a regular (spec 028 §3.10, ADR 0167): the shop of its kind that attracts it most.</summary>
    public sealed class KhataRegular
    {
        public Household Household { get; internal set; }
        public int People { get; internal set; }

        /// <summary>It may take goods on credit now (not cut off, room under the limit) and its income is under the khata line, so khata lifts its score and its kilos.</summary>
        public bool Eligible { get; internal set; }
        public bool Low { get; internal set; }
    }

    /// <summary>A shop's khata at this hour: its regulars, the people they are, and the score and kilos khata lifts for the town (spec 028 §3.10).</summary>
    public sealed class KhataStanding
    {
        public List<KhataRegular> Regulars { get; } = new List<KhataRegular>();
        public long RegularPeople, EligiblePeople, LowPeople;

        /// <summary>The share of the town's people who are low-income regulars with room to owe: the score and the extra kilos apply to them only.</summary>
        public Fixed Cover;
    }

    /// <summary>
    /// A shop's marketing and its khata (spec 028 §3.10, US-260): the budget paid at each close that lifts the score while it is spent, and
    /// credit sales to regular households that are collected at the close, with what is never paid written off as bad debt.
    /// </summary>
    public static class KhataRules
    {
        private static ShopDefs Defs(Simulation sim) { return sim.Config.Game.Shops; }

        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.Businesses != null && sim.State.Township != null && Town.TownshipRules.Reached(sim); }

        /// <summary>The kinds that sell to households on credit: the kirana, the vegetable stall and the tea stall.</summary>
        public static bool CanKhata(BusinessTypeDef t) { return t.Kind == ShopKind.Veg || t.Kind == ShopKind.Chai || t.Kind == ShopKind.Grocery; }

        /// <summary>Any shop that sells over a counter can spend on marketing (the mill, the bakery and the dairy sell by their own rounds).</summary>
        public static bool CanMarket(BusinessTypeDef t) { return t.IsShop; }

        // ---- the settings ----

        private static CommandResult Check(Simulation sim, long businessId, bool khata, out Business b)
        {
            b = sim.State.Businesses?.Find(businessId);
            if (!On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            if (khata ? !CanKhata(t) : !CanMarket(t)) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            return CommandResult.Ok;
        }

        internal static CommandResult SetMarketing(Simulation sim, long businessId, long budget)
        {
            CommandResult c = Check(sim, businessId, false, out Business b);
            if (!c.Accepted) return c;
            if (!System.Linq.Enumerable.Contains(Defs(sim).MarketBudgets, budget)) return CommandResult.Reject(RejectReason.InvalidArgument);
            b.Marketing = budget; // paid at the next close, for the season after it
            return CommandResult.Ok;
        }

        internal static CommandResult SetKhata(Simulation sim, long businessId, bool on)
        {
            CommandResult c = Check(sim, businessId, true, out Business b);
            if (!c.Accepted) return c;
            b.Khata = on;
            if (on && b.KhataLimit == 0) b.KhataLimit = Defs(sim).KhataLimits[0];
            return CommandResult.Ok;
        }

        internal static CommandResult SetLimit(Simulation sim, long businessId, long limit)
        {
            CommandResult c = Check(sim, businessId, true, out Business b);
            if (!c.Accepted) return c;
            if (!System.Linq.Enumerable.Contains(Defs(sim).KhataLimits, limit)) return CommandResult.Reject(RejectReason.InvalidArgument);
            b.KhataLimit = limit;
            return CommandResult.Ok;
        }

        internal static CommandResult CutOff(Simulation sim, long businessId, long householdId)
        {
            CommandResult c = Check(sim, businessId, true, out Business b);
            if (!c.Accepted) return c;
            KhataAccount a = AccountOf(b, householdId);
            if (a == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            a.CutOff = true; // until the close has collected what it owes
            return CommandResult.Ok;
        }

        // ---- what a household owes ----

        public static KhataAccount AccountOf(Business b, long household) { return b.Credit.Find(a => a.Household == household); }

        /// <summary>What a household may owe a shop now, at today's prices.</summary>
        public static long Limit(Simulation sim, Business b) { return MarketRules.Indexed(sim, b.KhataLimit); }

        /// <summary>Everything the shop is owed on khata (the part of last season paid a season late included).</summary>
        public static long Owed(Business b)
        {
            long n = 0;
            foreach (KhataAccount a in b.Credit) n += a.Balance + a.Late;
            return n;
        }

        /// <summary>What a household's account has room for: the limit less what it owes, nothing if it is cut off.</summary>
        public static long Room(Simulation sim, Business b, KhataAccount a)
        {
            long limit = Limit(sim, b);
            if (a == null) return limit;
            return a.CutOff ? 0 : System.Math.Max(0, limit - a.Balance - a.Late);
        }

        /// <summary>A household's season income as khata judges it, at today's prices: under the line it is a "low-income" household (its score and kilos lift, and its losses are higher).</summary>
        public static bool IsLow(Simulation sim, Household h) { return h.Income < MarketRules.Indexed(sim, Defs(sim).KhataLowIncome); }

        /// <summary>The share of what a household owes that is never paid, by its income (8% under 10,000, 4% under 20,000, 1% above).</summary>
        public static Fixed LossRate(Simulation sim, long income)
        {
            IReadOnlyList<KeyValuePair<long, Fixed>> tiers = Defs(sim).KhataLoss;
            foreach (KeyValuePair<long, Fixed> t in tiers)
                if (t.Key == 0 || income < MarketRules.Indexed(sim, t.Key)) return t.Value;
            return tiers[tiers.Count - 1].Value;
        }

        // ---- the hour ----

        /// <summary>
        /// For the shops open this hour with khata on: each household of yours is a regular of the shop of this kind that attracts it most (ties: the
        /// first, your shops by id before the rivals), measured from its home without khata's own lift. The score and kilos lift cover the low-income
        /// regulars who have room to owe; every regular's credit follows from its people's share of what the shop sold the town (<see cref="Credit"/>).
        /// </summary>
        internal static void Prepare(Simulation sim, List<ShopRules.Outlet> outlets)
        {
            bool any = false;
            foreach (ShopRules.Outlet o in outlets)
            {
                o.Standing = null;
                if (o.B != null)
                {
                    o.B.KhataLiftNow = Fixed.Zero;
                    o.B.KhataCover = Fixed.Zero;
                    if (o.B.Khata) { o.Standing = new KhataStanding(); any = true; }
                }
            }
            if (!any || sim.State.Citizens == null) return;
            int pop = System.Math.Max(1, sim.State.Citizens.Population);
            foreach (Household h in sim.State.Citizens.Households)
            {
                Land.Building home = sim.State.Land.Buildings.Find(x => x.Id == h.HomeId);
                if (home == null) continue;
                ShopRules.Outlet best = null;
                Fixed top = Fixed.Zero;
                foreach (ShopRules.Outlet o in outlets)
                {
                    Fixed a = ShopRules.AttractFrom(sim, o, home.TileId);
                    if (best == null || a > top) { best = o; top = a; }
                }
                if (best == null || best.Standing == null) continue;
                long room = Room(sim, best.B, AccountOf(best.B, h.Id));
                var reg = new KhataRegular { Household = h, People = h.Members.Count, Eligible = room > 0, Low = IsLow(sim, h) };
                KhataStanding s = best.Standing;
                s.Regulars.Add(reg);
                s.RegularPeople += reg.People;
                if (reg.Eligible) s.EligiblePeople += reg.People;
                if (reg.Eligible && reg.Low) s.LowPeople += reg.People;
            }
            foreach (ShopRules.Outlet o in outlets)
                if (o.Standing != null)
                {
                    o.Standing.Cover = Fixed.FromRatio(o.Standing.LowPeople, pop);
                    o.B.KhataCover = o.Standing.Cover;
                    o.B.KhataLiftNow = Defs(sim).KhataScore * o.Standing.Cover;
                }
        }

        /// <summary>
        /// The hour's sale on khata (spec 028 §3.10, ADR 0167 gap 6): what the shop sold the town goes to its regulars by their people, and a regular
        /// with room takes its part on credit up to its limit (the rest, cash): Dr Owed by buyers, Cr Cash on the shop's books.
        /// </summary>
        internal static void Credit(Simulation sim, ShopRules.Outlet o)
        {
            Business b = o.B;
            KhataStanding s = o.Standing;
            if (s == null || o.TownAmount <= 0 || s.RegularPeople <= 0) return;
            long now = sim.State.Clock.SimTime, total = 0;
            foreach (KhataRegular r in s.Regulars)
            {
                if (!r.Eligible) continue;
                KhataAccount a = AccountOf(b, r.Household.Id);
                long take = System.Math.Min((long)((System.Numerics.BigInteger)o.TownAmount * r.People / s.RegularPeople), Room(sim, b, a));
                if (take <= 0) continue;
                if (a == null)
                {
                    a = new KhataAccount { Household = r.Household.Id };
                    b.Credit.Add(a);
                    sim.Emit(new KhataGranted(now, b.Id, a.Household, take));
                }
                a.Balance += take;
                total += take;
            }
            if (total <= 0) return;
            b.CreditSeason += total;
            b.Ledger.Post(now, sim.Period, "ledger.khataSale", "business:" + b.Id, new Posting(Account.OwedByBuyers, total), new Posting(Account.Cash, -total));
        }

        // ---- the close ----

        /// <summary>
        /// The close's collection (spec 028 §3.10, ADR 0167 gap 9), in the closing season's books: last season's late part is paid; of what is owed now
        /// the household's loss rate is never paid (written off as Bad debt), <c>late</c> (5%) is paid a season late and the rest is collected.
        /// A household that has gone is a full loss. Then the season's honest value is kept for the sheet.
        /// </summary>
        internal static void AtClose(Simulation sim, Business b, long closing)
        {
            if (!b.GrowthUsed) return;
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            long lost = 0;
            var kept = new List<KhataAccount>();
            foreach (KhataAccount a in b.Credit)
            {
                Household h = sim.State.Citizens?.Households.Find(x => x.Id == a.Household);
                long owed = a.Balance + a.Late;
                long loss = h == null ? a.Balance + a.Late : Money.MulRate(a.Balance, LossRate(sim, h.Income));
                long late = h == null ? 0 : Money.MulRate(a.Balance, Defs(sim).KhataLate);
                long paid = owed - loss - late;
                if (paid > 0) b.Ledger.Post(now, closing, "ledger.khataPaid", reference, new Posting(Account.Cash, paid), new Posting(Account.OwedByBuyers, -paid));
                if (loss > 0)
                {
                    b.Ledger.Post(now, closing, "ledger.badDebt", reference, new Posting(Account.BadDebt, loss), new Posting(Account.OwedByBuyers, -loss));
                    a.WrittenOff += loss;
                    lost += loss;
                    sim.Emit(new KhataWrittenOff(now, b.Id, a.Household, loss));
                }
                a.Balance = 0;
                a.Late = late;
                a.CutOff = false;
                if (late > 0 && h != null) kept.Add(a);
            }
            b.Credit = kept;
            BusinessBooks.Sweep(sim, b);
            ShopsAtClose(sim, b, lost);
        }

        private static void ShopsAtClose(Simulation sim, Business b, long lost)
        {
            ShopDefs d = Defs(sim);
            b.LastCredit = b.CreditSeason;
            b.LastGain = (d.KhataKilos / (Fixed.One + d.KhataKilos)).MulRatio(b.CreditSeason, 1).RoundToLong();
            b.LastLost = lost;
            b.CreditSeason = 0;
            b.LastDrew = b.MarketLift == Fixed.Zero ? 0 : (b.MarketLift / ShopRules.Attract(sim, b, ShopGroup.Town).Total).MulRatio(b.SeasonServed, 1).RoundToLong();
        }

        /// <summary>
        /// The new season's marketing (spec 028 §3.10), paid upfront like the pitch's rent: the budget lifts the score by its share of 1,000s up to the
        /// cap; unpaid (cash short) or stopped, the lift fades by a fifth.
        /// </summary>
        internal static void PayMarketing(Simulation sim, Business b, long period)
        {
            ShopDefs d = Defs(sim);
            if (d == null || !BusinessRules.TypeOf(sim, b).IsShop) return;
            long budget = b.Marketing, spend = MarketRules.Indexed(sim, budget);
            b.LastSpend = 0;
            if (spend > 0 && BusinessBooks.CanSpend(sim, b) >= spend && b.Status == BusinessStatus.Open)
            {
                BusinessBooks.Draw(sim, b, spend, period);
                b.Ledger.Post(sim.State.Clock.SimTime, period, "ledger.marketing", "business:" + b.Id, new Posting(Account.Marketing, spend), new Posting(Account.Cash, -spend));
                Fixed lift = d.MarketPer1000.MulRatio(budget, 100000); // budget is in 1/100 coin: per 1,000 coins
                b.MarketLift = lift > d.MarketCap ? d.MarketCap : lift;
                b.LastSpend = spend;
                return;
            }
            if (spend > 0) sim.Emit(new ObligationUnpaid(sim.State.Clock.SimTime, "Marketing", "business:" + b.Id, spend));
            Fixed faded = b.MarketLift * (Fixed.One - d.MarketFade);
            b.MarketLift = faded < Fixed.FromRatio(1, 1000) ? Fixed.Zero : faded;
        }
    }
}
