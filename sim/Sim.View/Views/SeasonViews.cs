using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>One line of *Your season*: its words, its coins (as the block counts them) and what its *Why?* lists.</summary>
    public sealed class SeasonLineVm
    {
        /// <summary>What it is, for tests and the play log: a good's id, <c>hand:3</c>, an account's name, <c>paidAhead</c>…</summary>
        public string Key { get; internal set; }
        public string Label { get; internal set; }
        public string Icon { get; internal set; }

        /// <summary>Earned and spent as positive coins; a *why your cash moved* line signed as it moves cash; a due line positive.</summary>
        public long Amount { get; internal set; }

        public List<WhyRowVm> Why { get; } = new List<WhyRowVm>();
        public string WhyText { get; internal set; }
        public string WhyTitle { get; internal set; }
    }

    /// <summary>
    /// A season earned and spent (spec 019 §3.9, ADR 0083 §7–12): earned and spent as the books count them, profit, why the cash moved
    /// differently, and (this season only) what the close will take.
    /// </summary>
    public sealed class SeasonVm
    {
        public bool Last { get; internal set; }
        public long Period { get; internal set; }
        public List<SeasonLineVm> Earned { get; } = new List<SeasonLineVm>();
        public List<SeasonLineVm> Spent { get; } = new List<SeasonLineVm>();
        public List<SeasonLineVm> Moved { get; } = new List<SeasonLineVm>();
        public List<SeasonLineVm> Due { get; } = new List<SeasonLineVm>();
        public long EarnedTotal { get; internal set; }
        public long SpentTotal { get; internal set; }
        public long Profit { get { return EarnedTotal - SpentTotal; } }

        /// <summary>The farm's own profit inside it (the Hamlet gate's, spec 009 §3.6).</summary>
        public long FarmProfit { get; internal set; }

        public long CashStart { get; internal set; }
        public long CashEnd { get; internal set; }
        public long DueTotal { get; internal set; }

        /// <summary>How much cash now falls short of what's due (0 when it covers it), and the first thing that would go unpaid.</summary>
        public long Short { get; internal set; }
        public string ShortWho { get; internal set; }
    }

    /// <summary>Builds *Your season* from the ledger's entries of the season and the sales record (spec 019 §3.9).</summary>
    public static class SeasonBook
    {
        private static readonly Account[] FarmAccounts =
        {
            Account.CropSales, Account.FruitSales, Account.MilkSales, Account.LivestockSales, Account.SeedsAndPlanting, Account.FertiliserAndSpraying,
            Account.AnimalFeed, Account.VetAndBreeding, Account.HiredLabour, Account.Rent, Account.Depreciation, Account.TransportAndSelling,
        };

        // The *why your cash moved* lines, in the order shown.
        internal static readonly string[] MovedOrder =
        {
            "paidAhead", "wear", "owed", "interestOwed", "interestPaid", "repaid", "borrowed", "boughtBuildings", "boughtLivestock", "boughtTrees",
            "boughtLand", "boughtEquipment", "boughtInvestmentInBusinesses", "sold", "fromBusiness", "other",
        };

        /// <summary>True once a season has closed: *Last season* can show.</summary>
        public static bool HasLast(Simulation sim) { return sim.State.Finance.Reports.Count > 0 && sim.State.Finance.LastSeasonStartEntry > 0; }

        public static SeasonVm Build(ViewContext ctx, bool last)
        {
            Simulation sim = ctx.Sim;
            FinanceState f = sim.State.Finance;
            last = last && HasLast(sim);
            SeasonReport report = last ? f.Reports[f.Reports.Count - 1] : null;
            long from = last ? f.LastSeasonStartEntry : f.SeasonStartEntry, to = last ? f.SeasonStartEntry : long.MaxValue;
            var vm = new SeasonVm
            {
                Last = last, Period = last ? report.Period : sim.Period,
                CashStart = last ? report.CashFlow.Opening : f.SeasonOpeningCash, CashEnd = last ? report.CashFlow.Closing : sim.State.Ledger.Cash,
            };

            // The season's entries: income and costs by account (a hand's wage by its reference), and the rest of each cash move.
            var income = new long[Accounts.All.Length];
            var wages = new SortedDictionary<long, long>();
            long labourers = 0;
            var moved = new Dictionary<string, long>();
            foreach (JournalEntry e in sim.State.Ledger.Entries)
            {
                if (e.Id < from || e.Id >= to || e.DescriptionKey == "ledger.closeSeason" || e.DescriptionKey == "ledger.opening") continue;
                // An entry moving neither cash nor profit (the cattle acre inherited by migration: land against opening equity) can't
                // explain a gap between them; its lines would only cancel out.
                if (!MovesCashOrProfit(e)) continue;
                foreach (Posting p in e.Postings)
                {
                    AccountGroup g = Accounts.GroupOf(p.Account);
                    if (g == AccountGroup.Income || g == AccountGroup.Expenses)
                    {
                        income[System.Array.IndexOf(Accounts.All, p.Account)] -= p.Amount;
                        if (p.Account != Account.HiredLabour) continue;
                        if (e.Reference != null && e.Reference.StartsWith("hand:", System.StringComparison.Ordinal))
                        {
                            long id = long.Parse(e.Reference.Substring(5), System.Globalization.CultureInfo.InvariantCulture);
                            wages[id] = (wages.TryGetValue(id, out long w) ? w : 0) + p.Amount;
                        }
                        else labourers += p.Amount;
                    }
                    else if (p.Account != Account.Cash) Add(moved, MovedKey(p), -p.Amount);
                }
            }
            long Of(Account a) { return income[System.Array.IndexOf(Accounts.All, a)]; }

            Earned(ctx, vm, last ? f.Sales.Last : f.Sales.This, Of, last ? report.HomeUse : sim.State.Farm.Founder.HomeUseSeason,
                last ? report.GardenHomeUse : sim.State.Farm.Founder.GardenHomeUseSeason);
            Spent(ctx, vm, Of, wages, labourers);
            foreach (Account a in FarmAccounts) vm.FarmProfit += Of(a);
            foreach (string key in MovedOrder)
                if (moved.TryGetValue(key, out long v) && v != 0) vm.Moved.Add(MovedLine(ctx, key, v));
            if (!last) Due(ctx, vm);
            return vm;
        }

        private static bool MovesCashOrProfit(JournalEntry e)
        {
            foreach (Posting p in e.Postings)
            {
                AccountGroup g = Accounts.GroupOf(p.Account);
                if (p.Account == Account.Cash || g == AccountGroup.Income || g == AccountGroup.Expenses) return true;
            }
            return false;
        }

        private static void Add(Dictionary<string, long> d, string key, long v) { d[key] = (d.TryGetValue(key, out long x) ? x : 0) + v; }

        /// <summary>Which *why your cash moved* line a balance-sheet posting belongs to (ADR 0083 §8, §12).</summary>
        private static string MovedKey(Posting p)
        {
            switch (p.Account)
            {
                case Account.PrepaidLivingCosts: case Account.PrepaidWages: case Account.PrepaidRent: case Account.InputsInStore: return "paidAhead";
                case Account.AccumulatedDepreciation: return p.Amount < 0 ? "wear" : "sold"; // a debit here is a sold building's wear coming off
                case Account.OwedByBuyers: return "owed";
                case Account.InterestPayable: return p.Amount < 0 ? "interestOwed" : "interestPaid";
                case Account.MoneylenderLoans: case Account.BankLoans: case Account.Overdraft: return p.Amount > 0 ? "repaid" : "borrowed";
                case Account.Buildings: case Account.Livestock: case Account.Trees: case Account.Land: case Account.Equipment:
                    return p.Amount > 0 ? "bought" + p.Account : "sold";
                case Account.InvestmentInBusinesses: return p.Amount > 0 ? "bought" + p.Account : "fromBusiness";
                default: return "other";
            }
        }

        private static void Earned(ViewContext ctx, SeasonVm vm, List<Sale> sales, System.Func<Account, long> of, long homeUse, long garden)
        {
            var s = ctx.Strings;
            // Each good by name, biggest first, with where it sold in its Why?; orders on their own line (ADR 0083 §9).
            var goods = new List<string>();
            var byGood = new Dictionary<string, SeasonLineVm>();
            SeasonLineVm orders = null;
            long recorded = 0;
            foreach (Sale x in sales)
            {
                recorded += x.Amount;
                if (x.To == SoldTo.Orders)
                {
                    orders = orders ?? new SeasonLineVm { Key = "orders", Label = s.Get("season.orders"), Icon = "basket", WhyText = s.Get("season.orders.why") };
                    orders.Amount += x.Amount;
                    orders.Why.Add(new WhyRowVm(GoodName(ctx, x.Good), ctx.Fmt.Coins(x.Amount)));
                    continue;
                }
                if (!byGood.TryGetValue(x.Good, out SeasonLineVm line))
                {
                    line = new SeasonLineVm { Key = x.Good, Label = GoodName(ctx, x.Good), Icon = x.Good, WhyText = s.Get("season.good.why") };
                    byGood[x.Good] = line;
                    goods.Add(x.Good);
                }
                line.Amount += x.Amount;
                line.Why.Add(new WhyRowVm(s.Get("season.soldTo." + x.To), ctx.Fmt.Coins(x.Amount)));
                if (x.To == SoldTo.Mandi) line.WhyText = s.Get("season.good.why") + " " + s.Get("season.good.mandi");
            }
            goods.Sort((a, b) => { int c = byGood[b].Amount.CompareTo(byGood[a].Amount); return c != 0 ? c : string.CompareOrdinal(a, b); });
            foreach (string g in goods) vm.Earned.Add(byGood[g]);
            if (orders != null) vm.Earned.Add(orders);
            if (homeUse != 0)
                vm.Earned.Add(new SeasonLineVm
                {
                    Key = "homeUse", Label = s.Get("season.homeUse"), Icon = "basket", Amount = homeUse,
                    WhyText = s.Get("report.homeUse.why", "homeUse", ctx.Fmt.Coins(homeUse), "garden", ctx.Fmt.Coins(garden)),
                });
            // Sales the record doesn't name (a map migration's crops): kept, so the block still adds up to the books.
            long rest = of(Account.CropSales) + of(Account.FruitSales) - recorded - homeUse;
            if (rest != 0) vm.Earned.Add(AccountLine(ctx, "otherSales", s.Get("season.otherSales"), rest, null));
            foreach (Account a in Accounts.All)
            {
                if (Accounts.GroupOf(a) != AccountGroup.Income || a == Account.CropSales || a == Account.FruitSales || of(a) == 0) continue;
                vm.Earned.Add(AccountLine(ctx, a.ToString(), s.Get("season.account." + a), of(a), Icon(a)));
            }
            foreach (SeasonLineVm l in vm.Earned) vm.EarnedTotal += l.Amount;
        }

        private static void Spent(ViewContext ctx, SeasonVm vm, System.Func<Account, long> of, SortedDictionary<long, long> wages, long labourers)
        {
            var s = ctx.Strings;
            foreach (Account a in Accounts.All)
            {
                if (Accounts.GroupOf(a) != AccountGroup.Expenses || of(a) == 0) continue;
                if (a != Account.HiredLabour) { vm.Spent.Add(AccountLine(ctx, a.ToString(), s.Get("season.account." + a), -of(a), Icon(a))); continue; }
                // Each hand's wage by name (spec 019 §3.9), then the labourers together.
                foreach (KeyValuePair<long, long> w in wages)
                {
                    if (w.Value == 0) continue;
                    vm.Spent.Add(new SeasonLineVm
                    {
                        Key = "hand:" + w.Key, Label = s.Get("season.wage", "name", HandName(ctx, w.Key)), Icon = "person_hand_m", Amount = w.Value,
                        WhyText = s.Get("season.wage.why"),
                    });
                }
                if (labourers != 0) vm.Spent.Add(AccountLine(ctx, "labourers", s.Get("season.labourers"), labourers, "person_labourer_m"));
            }
            foreach (SeasonLineVm l in vm.Spent) vm.SpentTotal += l.Amount;
        }

        private static SeasonLineVm AccountLine(ViewContext ctx, string key, string label, long amount, string icon)
        {
            return new SeasonLineVm { Key = key, Label = label, Icon = icon, Amount = amount, WhyText = ctx.Strings.Get("season." + key + ".why") };
        }

        private static string Icon(Account a)
        {
            switch (a)
            {
                case Account.MilkSales: return "milk";
                case Account.LivestockSales: case Account.AnimalFeed: case Account.VetAndBreeding: return "animal_buffalo";
                case Account.RentReceived: return "family_house";
                case Account.BusinessDrawingsIncome: return "flour_mill";
                case Account.DayLabourEarnings: case Account.LivingCosts: case Account.HealthCosts: return "person_founder";
                case Account.SeedsAndPlanting: return "field";
                case Account.FertiliserAndSpraying: return "compost";
                case Account.TransportAndSelling: return "cart";
                case Account.Interest: case Account.PenaltyInterest: return "coin";
                default: return null;
            }
        }

        private static SeasonLineVm MovedLine(ViewContext ctx, string key, long amount)
        {
            var s = ctx.Strings;
            string icon = key == "boughtBuildings" ? "family_house" : key == "boughtLivestock" ? "animal_buffalo" : key == "boughtTrees" ? "tree"
                : key == "boughtLand" ? "land" : key == "boughtInvestmentInBusinesses" || key == "fromBusiness" ? "flour_mill" : "coin";
            return new SeasonLineVm { Key = key, Label = s.Get("season.moved." + key), Icon = icon, Amount = amount, WhyText = s.Get("season.moved." + key + ".why") };
        }

        /// <summary>
        /// What the next close takes if nothing changes (spec 019 §3.9, ADR 0083 §10), in the close's order: loan payments, the family's
        /// living, then each hand's renewal (less the house, and less an advance he repays). All at today's prices.
        /// </summary>
        private static void Due(ViewContext ctx, SeasonVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long cash = sim.State.Ledger.Cash, left = cash;
            void Owe(SeasonLineVm line, string who)
            {
                vm.Due.Add(line);
                vm.DueTotal += line.Amount;
                if (vm.ShortWho == null && line.Amount > left) vm.ShortWho = who;
                left -= System.Math.Min(left, line.Amount);
            }
            if (sim.State.Credit != null)
                foreach (Loan loan in sim.State.Credit.Loans)
                {
                    if (!loan.IsOpen || loan.BusinessId != 0) continue; // a business pays its own loan (spec 016 §3.8)
                    long total = Repay.NextPayment(sim, loan, out long interest, out long principal);
                    if (total == 0) continue;
                    string lender = s.Get(sim.Config.Game.Credit.Lenders[loan.Lender].NameKey);
                    // A bullet loan pays interest only until its last close: then the row says so, not "and principal".
                    var line = new SeasonLineVm
                    {
                        Key = "loan:" + loan.Id, Label = s.Get(principal > 0 ? "season.due.loan" : "season.due.loanInterest", "lender", lender), Icon = "coin", Amount = total,
                        WhyText = s.Get("season.due.loan.why"),
                    };
                    line.Why.Add(new WhyRowVm(s.Get("season.due.interest"), ctx.Fmt.Coins(interest)));
                    if (principal > 0) line.Why.Add(new WhyRowVm(s.Get("season.due.principal"), ctx.Fmt.Coins(principal)));
                    Owe(line, s.Get("season.short.loan", "lender", lender));
                }
            FounderState founder = sim.State.Farm.Founder;
            int plan = founder.AutoSubsistence ? founder.PreviousPlan : founder.DietPlan;
            long living = Sim.Core.Market.MarketRules.Indexed(sim, sim.Config.Game.Farm.DietPlans[plan].CostPerSeason);
            if (living > 0)
                Owe(new SeasonLineVm { Key = "living", Label = s.Get("season.due.living"), Icon = "person_founder", Amount = living, WhyText = s.Get("season.due.living.why") },
                    s.Get("season.short.living"));
            if (sim.State.People != null)
                foreach (Hand h in sim.State.People.Hands)
                {
                    if (h.Contract == ContractState.Lapsed || HrRules.Leaves(sim, h) || CitizenRules.Retiring(sim, h)) continue; // not renewed at the close
                    long wage = PeopleSystem.RenewalWage(sim, h, out long housing), need = System.Math.Max(0, wage - h.Hr.Advance);
                    string name = PeopleWords.Name(ctx, h.Name);
                    var line = new SeasonLineVm { Key = "hand:" + h.Id, Label = s.Get("season.due.renewal", "name", name), Icon = "person_hand_m", Amount = need, WhyText = s.Get("season.due.renewal.why") };
                    line.Why.Add(new WhyRowVm(s.Get("season.due.wage"), ctx.Fmt.Coins(wage + housing)));
                    if (housing > 0) line.Why.Add(new WhyRowVm(s.Get("season.due.house"), "−" + ctx.Fmt.Coins(housing)));
                    if (h.Hr.Advance > 0) line.Why.Add(new WhyRowVm(s.Get("season.due.advance"), "−" + ctx.Fmt.Coins(System.Math.Min(wage, h.Hr.Advance))));
                    Owe(line, s.Get("season.short.renewal", "name", name));
                }
            vm.Short = System.Math.Max(0, vm.DueTotal - cash);
        }

        public static string GoodName(ViewContext ctx, string good)
        {
            int g = ctx.Sim.Config.Game.Market.IndexOf(good);
            return g >= 0 ? ctx.Strings.Get(ctx.Sim.Config.Game.Market.Goods[g].NameKey) : ctx.Strings.Get("season.good." + good);
        }

        private static string HandName(ViewContext ctx, long handId)
        {
            Hand h = ctx.Sim.State.People?.Hand(handId);
            if (h != null) return PeopleWords.Name(ctx, h.Name);
            Citizen c = ctx.Sim.State.Citizens?.Citizens.Find(x => x.HandId == handId); // a former hand too
            return c != null ? PeopleWords.Name(ctx, c.Name) : ctx.Strings.Get("away.formerHand");
        }
    }
}
