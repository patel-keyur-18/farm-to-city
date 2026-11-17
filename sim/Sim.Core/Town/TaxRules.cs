using System;
using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// The two taxes (spec 022 §3.1–§3.4, ADR 0109): your house tax as the owner of the town's houses, each business's tax on its season
    /// profit, the village's shops' tax on a reference profit. Rates apply from the next season; the office collects its share of what is
    /// billed and the payer pays only that; what a payer can't pay is carried and paid first at the next close.
    /// </summary>
    public static class TaxRules
    {
        public static TaxDef Def(Simulation sim) { return sim.Config.Game?.Treasury?.Taxes; }
        private static TreasuryState State(Simulation sim) { return sim.State.Treasury; }

        /// <summary>The home hut's building id in the content: assessed as a kutcha house (spec 022 §3.1).</summary>
        public const string HomeBuildingId = "hut";

        /// <summary>The rate in force for the season now.</summary>
        public static Fixed Rate(Simulation sim, TaxKind kind) { return kind == TaxKind.House ? State(sim).HouseRate : State(sim).BusinessRate; }

        /// <summary>The rate set for the next season, or −1 for none.</summary>
        public static Fixed Pending(Simulation sim, TaxKind kind) { return kind == TaxKind.House ? State(sim).PendingHouse : State(sim).PendingBusiness; }

        public static Fixed Bound(Simulation sim, TaxKind kind) { return kind == TaxKind.House ? Def(sim).HouseMax : Def(sim).BusinessMax; }

        /// <summary>The rate the next close's bills will use (the one in force), and the one after it (pending, else the same).</summary>
        public static Fixed Next(Simulation sim, TaxKind kind) { Fixed p = Pending(sim, kind); return p < Fixed.Zero ? Rate(sim, kind) : p; }

        // ---- setting a rate (§3.4) ----

        internal static RejectReason SetRate(Simulation sim, TaxKind kind, Fixed rate)
        {
            if (!TreasuryRules.IsOpen(sim)) return RejectReason.NoTreasury;
            if (kind != TaxKind.House && kind != TaxKind.Business) return RejectReason.OutOfRange;
            if (rate < Fixed.Zero || rate > Bound(sim, kind)) return RejectReason.OutOfRange;
            TreasuryState s = State(sim);
            Fixed from = Next(sim, kind);
            Fixed inForce = Rate(sim, kind);
            Fixed pending = rate == inForce ? Fixed.FromInt(-1) : rate; // back to the rate in force leaves nothing pending
            if (kind == TaxKind.House) s.PendingHouse = pending; else s.PendingBusiness = pending;
            sim.Emit(new TaxRateSet(sim.State.Clock.SimTime, (int)kind, from, rate));
            return RejectReason.None;
        }

        internal static RejectReason UndoRate(Simulation sim, TaxKind kind)
        {
            if (!TreasuryRules.IsOpen(sim)) return RejectReason.NoTreasury;
            if (kind != TaxKind.House && kind != TaxKind.Business) return RejectReason.NothingPending;
            TreasuryState s = State(sim);
            if (Pending(sim, kind) < Fixed.Zero) return RejectReason.NothingPending;
            Fixed from = Next(sim, kind);
            if (kind == TaxKind.House) s.PendingHouse = Fixed.FromInt(-1); else s.PendingBusiness = Fixed.FromInt(-1);
            sim.Emit(new TaxRateSet(sim.State.Clock.SimTime, (int)kind, from, Rate(sim, kind)));
            return RejectReason.None;
        }

        /// <summary>The close rolls a rate set for the next season into force (after this close's bills were made at the old one).</summary>
        internal static void RollRates(Simulation sim)
        {
            TreasuryState s = State(sim);
            if (s.PendingHouse >= Fixed.Zero) { s.HouseRate = s.PendingHouse; s.PendingHouse = Fixed.FromInt(-1); }
            if (s.PendingBusiness >= Fixed.Zero) { s.BusinessRate = s.PendingBusiness; s.PendingBusiness = Fixed.FromInt(-1); }
        }

        // ---- the house tax (§3.1) ----

        /// <summary>A standard kutcha house's tax a season at a rate, for the sheet's caption ("240 a house a season").</summary>
        public static long StandardHouseTax(Simulation sim, Fixed rate)
        {
            GameDefs g = sim.Config.Game;
            long value = MarketRules.Indexed(sim, g.Buildings[g.Population.House].Value);
            return Math.Min(Money.MulRate(value, rate) / 4, Money.MulRate(MarketRules.Indexed(sim, g.Population.BaseRent), Def(sim).MaxShareOfRent));
        }


        /// <summary>One house's tax a season at a rate, with the owner's guard: never more than <c>maxShareOfRent</c> of its rent (a let house's actual rent, else its kind's base rent).</summary>
        public static long HouseTax(Simulation sim, Building b, Fixed rate)
        {
            TaxDef d = Def(sim);
            GameDefs g = sim.Config.Game;
            BuildingDef def = g.Buildings[b.Def];
            BuildingDef assessed;
            if (HouseRules.IsHouse(sim, b)) assessed = def;
            else if (def.Id == HomeBuildingId) { assessed = null; foreach (BuildingDef bd in g.Buildings) if (bd.Id == d.HomeAssessedAs) assessed = bd; }
            else return 0; // a hand's hut and every other building: exempt
            if (assessed == null) return 0;
            long value = MarketRules.Indexed(sim, assessed.Value);
            long tax = Money.MulRate(value, rate) / 4;
            long rent = MarketRules.Indexed(sim, g.Population.BaseRent);
            Household x = sim.State.Citizens?.Households.Find(h => h.HomeId == b.Id);
            if (x != null && x.LeaseRent > 0 && HouseholdRules.InFamilyHouse(sim, x)) rent = x.LeaseRent;
            long cap = Money.MulRate(rent, d.MaxShareOfRent);
            return Math.Min(tax, cap);
        }

        /// <summary>What you would be billed for your houses at a rate: the houses counted and the tax (§3.1).</summary>
        public static long YourHouseBill(Simulation sim, Fixed rate, out int houses)
        {
            houses = 0;
            long sum = 0;
            foreach (Building b in sim.State.Land.Buildings)
            {
                long t = HouseTax(sim, b, rate);
                if (!IsTaxed(sim, b)) continue;
                houses++;
                sum += t;
            }
            return sum;
        }

        private static bool IsTaxed(Simulation sim, Building b)
        {
            return HouseRules.IsHouse(sim, b) || sim.Config.Game.Buildings[b.Def].Id == HomeBuildingId;
        }

        /// <summary>The office's share of what is billed (spec 021 §3.3): the payer pays only this.</summary>
        public static long Collected(Simulation sim, long billed)
        {
            if (billed <= 0 || !TreasuryRules.IsOpen(sim)) return 0;
            TreasuryDef d = TreasuryRules.Def(sim);
            Fixed factor = d.OfficeBase + d.OfficeWeight * State(sim).Condition[0];
            return Math.Min(billed, Money.MulRate(billed, factor));
        }

        // ---- the village's shops (§3.2) ----

        /// <summary>Each village shop's tax at a rate: its reference profit (indexed) × the rate; id order.</summary>
        public static List<KeyValuePair<string, long>> ShopBills(Simulation sim, Fixed rate)
        {
            var list = new List<KeyValuePair<string, long>>();
            ShopsState shops = sim.State.Shops;
            ShopDefs sd = sim.Config.Game.Shops;
            if (shops == null || sd == null) return list;
            TaxDef d = Def(sim);
            var all = new List<Rival>(shops.Rivals);
            all.AddRange(shops.Newcomers); // a newcomer pays on its own reference profit too
            foreach (Rival r in all)
            {
                string key = r.Newcomer ? "newcomer" : r.Id;
                if (!d.ReferenceProfit.TryGetValue(key, out long profit)) continue; // the town's halwai is not the village's
                long tax = Money.MulRate(MarketRules.Indexed(sim, profit), rate);
                if (tax > 0) list.Add(new KeyValuePair<string, long>(r.Newcomer ? "newcomer:" + r.OpenedAt : r.Id, tax));
            }
            return list;
        }

        // ---- billing at the close (§3.8) ----

        /// <summary>
        /// The founder's house tax at the close: first what he still owes, then this season's bill at the rate in force. The office collects
        /// its share; he pays what his cash allows and the rest is carried (2400). The treasury receives what is paid.
        /// </summary>
        internal static void BillFounder(Simulation sim)
        {
            if (!TreasuryRules.IsOpen(sim)) return;
            Ledger l = sim.State.Ledger;
            long now = sim.State.Clock.SimTime;
            PayOwed(sim, l, "you", TaxKind.House, now);
            long billed = YourHouseBill(sim, Rate(sim, TaxKind.House), out int houses);
            if (billed <= 0) return;
            long collected = Collected(sim, billed), pay = Math.Min(collected, Math.Max(0, l.Cash));
            if (collected <= 0) return;
            var postings = new List<Posting> { new Posting(Account.HouseTax, collected) };
            if (pay > 0) postings.Add(new Posting(Account.Cash, -pay));
            if (collected > pay) postings.Add(new Posting(Account.TaxOwed, -(collected - pay)));
            l.Post(now, sim.Period, "ledger.houseTax", "houses:" + houses, postings.ToArray());
            TreasuryRules.ReceiveTax(sim, TaxKind.House, "you", pay);
            sim.Emit(new TaxBilled(now, (int)TaxKind.House, "you", billed, collected));
            if (collected > pay) sim.Emit(new TaxOwed(now, "you", collected - pay));
        }

        /// <summary>A business's tax on its season's profit before tax, from its own cash; owed first, then the new bill. Runs before its books close.</summary>
        internal static void BillBusiness(Simulation sim, Business.Business b, long closing)
        {
            if (!TreasuryRules.IsOpen(sim)) return;
            Ledger l = b.Ledger;
            long now = sim.State.Clock.SimTime;
            string payer = "business:" + b.Id;
            PayOwed(sim, l, payer, TaxKind.Business, now, closing);
            long profit = 0;
            foreach (Account a in Accounts.All)
            {
                AccountGroup g = Accounts.GroupOf(a);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) profit -= l.Balance(a);
            }
            if (profit <= 0) return;
            long billed = Money.MulRate(profit, Rate(sim, TaxKind.Business));
            long collected = Collected(sim, billed), pay = Math.Min(collected, Math.Max(0, l.Cash));
            if (collected <= 0) return;
            var postings = new List<Posting> { new Posting(Account.BusinessTax, collected) };
            if (pay > 0) postings.Add(new Posting(Account.Cash, -pay));
            if (collected > pay) postings.Add(new Posting(Account.TaxOwed, -(collected - pay)));
            l.Post(now, closing, "ledger.businessTax", payer, postings.ToArray());
            TreasuryRules.ReceiveTax(sim, TaxKind.Business, payer, pay);
            sim.Emit(new TaxBilled(now, (int)TaxKind.Business, payer, billed, collected));
            if (collected > pay) sim.Emit(new TaxOwed(now, payer, collected - pay));
        }

        /// <summary>The village's shops pay the rate × their reference profit from outside the model: the treasury is paid what the office collects.</summary>
        internal static void BillShops(Simulation sim)
        {
            if (!TreasuryRules.IsOpen(sim)) return;
            long now = sim.State.Clock.SimTime;
            foreach (KeyValuePair<string, long> bill in ShopBills(sim, Rate(sim, TaxKind.Business)))
            {
                long collected = Collected(sim, bill.Value);
                if (collected <= 0) continue;
                TreasuryRules.ReceiveTax(sim, TaxKind.Business, "shop:" + bill.Key, collected);
                sim.Emit(new TaxBilled(now, (int)TaxKind.Business, "shop:" + bill.Key, bill.Value, collected));
            }
        }

        /// <summary>What a payer still owes the town is paid first, from cash (spec 022 §3.1); drawings before a close never reduce it.</summary>
        internal static void PayOwed(Simulation sim, Ledger l, string payer, TaxKind kind, long now, long period = -1)
        {
            long owed = -l.Balance(Account.TaxOwed);
            if (owed <= 0) return;
            long pay = Math.Min(owed, Math.Max(0, l.Cash));
            if (pay <= 0) return;
            l.Post(now, period < 0 ? sim.Period : period, "ledger.taxOwedPaid", payer, new Posting(Account.TaxOwed, pay), new Posting(Account.Cash, -pay));
            TreasuryRules.ReceiveTax(sim, kind, payer, pay);
        }

        /// <summary>What you owe the town now (house tax carried from a close you couldn't pay).</summary>
        public static long YouOwe(Simulation sim) { return -sim.State.Ledger.Balance(Account.TaxOwed); }

        /// <summary>Your house tax the next close will take (collected share of the bill at the rate in force), for <em>Next season, already due</em>.</summary>
        public static long NextHouseDue(Simulation sim, out int houses)
        {
            houses = 0;
            if (!TreasuryRules.IsOpen(sim)) return 0;
            return Collected(sim, YourHouseBill(sim, Rate(sim, TaxKind.House), out houses)) + YouOwe(sim);
        }

        /// <summary>
        /// The tax money the close will bring in, as the forecast and the sheet see it (spec 021 §3.11, spec 022 §7): your houses, the village's
        /// shops and, for your businesses, what last season's profit would have paid at the rate (an estimate: this season's isn't known yet).
        /// </summary>
        public static long Expected(Simulation sim, Fixed houseRate, Fixed businessRate, out long yours, out long shops, out long houseYou, out long businessYou)
        {
            houseYou = Collected(sim, YourHouseBill(sim, houseRate, out _));
            businessYou = 0;
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                {
                    if (b.Status == BusinessStatus.Closed || b.Reports.Count == 0) continue;
                    BusinessReport last = b.Reports[b.Reports.Count - 1];
                    long before = BusinessBooks.NetProfit(last) + last.Balance(Account.BusinessTax); // the report's result is after its tax
                    if (before > 0) businessYou += Collected(sim, Money.MulRate(before, businessRate));
                }
            shops = 0;
            foreach (KeyValuePair<string, long> bill in ShopBills(sim, businessRate)) shops += Collected(sim, bill.Value);
            yours = houseYou + businessYou;
            return houseYou + businessYou + shops;
        }

        /// <summary>The tax certain by the close: your houses (collected share) and the village's shops (spec 021 §3.11).</summary>
        public static long Certain(Simulation sim)
        {
            if (!TreasuryRules.IsOpen(sim)) return 0;
            long shops = 0;
            foreach (KeyValuePair<string, long> bill in ShopBills(sim, Rate(sim, TaxKind.Business))) shops += Collected(sim, bill.Value);
            return Collected(sim, YourHouseBill(sim, Rate(sim, TaxKind.House), out _)) + shops;
        }
    }
}
