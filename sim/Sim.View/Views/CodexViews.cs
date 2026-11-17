using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The Concept Codex's moments (spec 018 §3.3, ADR 0095 §7): after each batch of events (and a rejection), the cards whose moment
    /// came, each with the player's numbers then. The host sends them in one <see cref="FindCodexCards"/>. Nothing during the first
    /// session; a card already found never again; nothing back-filled: a card is found the next time its moment comes.
    /// </summary>
    public static class CodexWatch
    {
        /// <summary>The cards whose moment is in <paramref name="events"/>, in the content's order.</summary>
        public static List<CodexFind> Observe(Simulation sim, IReadOnlyList<SimEvent> events)
        {
            var found = new List<CodexFind>();
            CodexDefs d = sim.Config.Game?.Codex;
            if (d == null || sim.State.Guidance == null || Guide.FirstSessionActive(sim)) return found;
            foreach (CodexCardDef c in d.Cards)
            {
                if (sim.State.Guidance.Found(c.Id) != null) continue;
                long[] n = null;
                foreach (SimEvent e in events)
                    if ((n = Moment(sim, c, e)) != null) break;
                if (n != null) found.Add(new CodexFind(c.Id, n));
            }
            return found;
        }

        /// <summary>A refused command's moment: <i>liquidity</i>, short of cash with assets in hand.</summary>
        public static List<CodexFind> Rejected(Simulation sim, RejectReason reason)
        {
            var found = new List<CodexFind>();
            CodexDefs d = sim.Config.Game?.Codex;
            if (d == null || sim.State.Guidance == null || Guide.FirstSessionActive(sim) || reason != RejectReason.InsufficientFunds) return found;
            foreach (CodexCardDef c in d.Cards)
            {
                if (c.Trigger != "liquidity" || sim.State.Guidance.Found(c.Id) != null) continue;
                NetWorthSnapshot w = Reporting.NetWorth(sim);
                long assets = w.LandAtMarket + w.BuildingsAtBook + w.StoredGoods + w.LivestockAtBook + w.EquipmentAtBook + w.TreesAtBook + w.Businesses;
                if (assets > w.Cash) found.Add(new CodexFind(c.Id, new[] { w.Cash, assets }));
            }
            return found;
        }

        /// <summary>The command that records them, or null when there's none.</summary>
        public static Command Command(long at, List<CodexFind> found) { return found.Count == 0 ? null : new FindCodexCards(at, found); }

        private static long Coins(Fixed perKg) { return perKg.MulRatio(Money.Coin, 1).RoundToLong(); }

        /// <summary>The card's numbers if <paramref name="e"/> is its moment, else null (spec 018 §3.3's table, after the result).</summary>
        private static long[] Moment(Simulation sim, CodexCardDef c, SimEvent e)
        {
            switch (c.Trigger)
            {
                case "diminishingReturns": // chemical fertiliser on a field at the yield cap: its extra was wasted
                    if (e is CropHarvested h && h.Breakdown.Capped && h.Breakdown.Fertiliser > Fixed.One)
                        return new[] { h.Breakdown.Uncapped.Raw, sim.Config.Game.Farm.Farm.YieldCap.Raw };
                    return null;
                case "soilFertility": // a crop that uses the soil left its acre below the threshold
                    if (e is CropHarvested s)
                    {
                        CropDef crop = sim.Config.Game.Farm.Crops[sim.Config.Game.Farm.IndexOfCrop(s.CropId)];
                        Fixed fertility = sim.State.Land.Tiles[s.TileId].Fertility;
                        if (crop.FertilityPerSeason > Fixed.Zero && fertility < c.Threshold) return new[] { fertility.Raw, crop.FertilityPerSeason.Raw };
                    }
                    return null;
                case "opportunityCost": // day labour paid while acres of your own stood empty
                    if (e is DayLabourPaid p && p.Amount > 0)
                    {
                        int empty = EmptyAcres(sim);
                        if (empty > 0) return new[] { p.Amount, (long)empty };
                    }
                    return null;
                case "profitNotCash": // a season that made a profit and left less cash
                    if (e is SeasonClosed pc && pc.Report.NetResult > 0 && pc.Report.CashFlow.Closing < pc.Report.CashFlow.Opening)
                        return new[] { pc.Report.NetResult, pc.Report.CashFlow.Closing - pc.Report.CashFlow.Opening };
                    return null;
                case "interest": // the first season with interest charged
                    if (e is SeasonClosed ic)
                    {
                        long interest = Cost(ic.Report, Account.Interest);
                        if (interest > 0) return new[] { interest, ic.Report.NetWorth.LoanPrincipal };
                    }
                    return null;
                case "creditScore":
                    return e is CreditScoreChanged cs && cs.New != cs.Old ? new[] { (long)cs.Old, cs.New } : null;
                case "leverage": // a loan bigger than a season's profit
                    if (e is LoanTaken l)
                    {
                        var reports = sim.State.Finance.Reports;
                        long profit = reports.Count == 0 ? 0 : reports[reports.Count - 1].FarmProfit;
                        if (l.Amount > profit) return new[] { l.Amount, profit };
                    }
                    return null;
                case "inflation": // the price index past the threshold
                    return e is PriceIndexChanged pi && pi.New >= c.Threshold ? new[] { (pi.New - Fixed.One).Raw } : null;
                case "workingCapital": // a business working on its overdraft
                    if (e is HourStarted && sim.State.Businesses != null)
                        foreach (Business b in sim.State.Businesses.Businesses)
                            if (b.Status != BusinessStatus.Closed && BusinessBooks.Overdrawn(b) > 0)
                                return new[] { BusinessBooks.Overdrawn(b), BusinessBooks.OverdraftLimit(sim, b) };
                    return null;
                case "grossMargin": // a business's first season with sales
                    if (e is SeasonClosed gm && sim.State.Businesses != null)
                        foreach (Business b in sim.State.Businesses.Businesses)
                        {
                            BusinessReport r = Report(b, gm.Period);
                            long sales = r == null ? 0 : Sales(r), cogs = r == null ? 0 : r.Balance(Account.CostOfGoodsSold);
                            if (sales > 0) return new[] { sales, cogs, Fixed.FromRatio(sales - cogs, sales).Raw };
                        }
                    return null;
                case "fixedVariable": // sales fell from the last season while the wage and rent didn't
                    if (e is SeasonClosed fv && sim.State.Businesses != null)
                        foreach (Business b in sim.State.Businesses.Businesses)
                        {
                            BusinessReport now = Report(b, fv.Period), before = Report(b, fv.Period - 1);
                            if (now == null || before == null) continue;
                            long fixedNow = now.Balance(Account.HiredLabour) + now.Balance(Account.Rent), fixedBefore = before.Balance(Account.HiredLabour) + before.Balance(Account.Rent);
                            if (Sales(now) < Sales(before) && fixedNow > 0 && fixedNow >= fixedBefore && now.Balance(Account.HiredLabour) >= before.Balance(Account.HiredLabour))
                                return new[] { Sales(before), Sales(now), fixedNow };
                        }
                    return null;
                case "breakEven": // a business founded on its plan
                    if (e is BusinessFounded bf)
                    {
                        Business b = sim.State.Businesses.Businesses.Find(x => x.Id == bf.BusinessId);
                        if (b?.Plan != null) return new[] { b.Plan.Revenue, b.Plan.Costs };
                    }
                    return null;
                case "forecastActual": // the plan against the seasons since
                    if (e is SeasonClosed fa && sim.State.Businesses != null)
                        foreach (Business b in sim.State.Businesses.Businesses)
                        {
                            if (b.Plan == null || b.Reports.Count < c.Threshold.RoundToLong()) continue;
                            long sum = 0;
                            foreach (BusinessReport r in b.Reports) sum += BusinessBooks.NetProfit(r);
                            return new[] { b.Plan.Profit, sum / b.Reports.Count };
                        }
                    return null;
                case "unitEconomics": // a batch made: what a kg costs to make and what it sells for
                    if (e is BusinessWorked w)
                    {
                        Business b = sim.State.Businesses.Businesses.Find(x => x.Id == w.BusinessId);
                        BusinessTypeDef t = b == null ? null : BusinessRules.TypeOf(sim, b);
                        if (t != null && t.ByRecipe)
                            foreach (int good in t.Products)
                                if (b.MadeToday[good] > 0) return new[] { Coins(BusinessRules.UnitCost(sim, t, good)), Coins(MarketRules.Floor(sim, good)) };
                    }
                    return null;
                case "depreciation": // the first season a building or machine wore down
                    if (e is SeasonClosed dp)
                    {
                        long wear = Cost(dp.Report, Account.Depreciation);
                        if (wear > 0) return new[] { wear, dp.Report.NetWorth.BuildingsAtBook + dp.Report.NetWorth.EquipmentAtBook };
                    }
                    return null;
                case "appreciation": // land you bought now worth more than you paid
                    if (e is SeasonClosed)
                        foreach (Parcel parcel in sim.State.Land.Parcels)
                        {
                            if (!parcel.Owned || parcel.Protected || parcel.CostBasis <= 0) continue;
                            long value = LandRules.ParcelPrice(sim, parcel);
                            if (value > parcel.CostBasis) return new[] { parcel.CostBasis, value };
                        }
                    return null;
                case "yield": // the first rent: what a season's rent is of the house's cost
                    if (e is RentPaid rp && rp.Amount > 0)
                    {
                        Household x = sim.State.Citizens?.Household(rp.HouseholdId);
                        Building home = x == null ? null : sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
                        if (home != null && home.Cost > 0) return new[] { rp.Amount, Fixed.FromRatio(rp.Amount, home.Cost).Raw };
                    }
                    return null;
                case "hiringUncertainty":
                    return e is ReferenceChecked ? new[] { MarketRules.Indexed(sim, sim.Config.Game.People.Hiring.ReferenceCost) } : null;
                case "marketWage": // an offer under the asking wage, countered
                    if (e is CounterOffer co)
                    {
                        Applicant a = sim.State.People.Applicant(co.ApplicantId);
                        if (a != null) return new[] { HiringRules.OfferWage(sim, a, OfferLevel.Low), co.Wage };
                    }
                    return null;
                case "retention": // someone else offered your worker more
                    if (e is HrEventRaised hr && hr.Kind == HrEventKind.Poaching)
                    {
                        HrEvent x = sim.State.People.HrEvents.Find(v => v.Id == hr.EventId);
                        Worker worker = sim.State.People.Workers.Find(v => v.Id == hr.WorkerId);
                        if (x != null && worker != null) return new[] { worker.Wage, x.Amount };
                    }
                    return null;
                case "trainingRoi":
                    return e is CourseFinished cf ? new[] { cf.Skill.RoundToLong() } : null;
                case "employerReputation":
                    return e is ReputationChanged rc && rc.Delta != 0 ? new[] { (long)rc.Score } : null;
                case "publicGoods":
                    return e is SchoolOpened && SchoolRules.On(sim) ? new[] { SchoolRules.State(sim).Cost, (long)SchoolRules.Pupils(sim).Count } : null;
                case "longTermInvestment":
                    return e is CitizenLiterate cl && cl.How == LiterateHow.School && SchoolRules.On(sim)
                        ? new[] { (long)sim.Config.Game.School.PrimaryYears, SchoolRules.State(sim).Cost } : null;
                case "publicMoney": // the treasury opened
                    return e is TreasuryOpened to ? new[] { to.Grant } : null;
                case "budgetPriority": // a department paid after the others' budgets, or left unpaid: the budget is the order
                {
                    TreasuryState t = sim.State.Treasury;
                    if (t == null || t.Reports.Count == 0) return null;
                    TreasuryReport last = t.Reports[t.Reports.Count - 1];
                    if (e is DepartmentPaid dpd && dpd.InPass2) return new[] { last.Rows[dpd.Department].PaidInPass2, dpd.Needs };
                    if (e is DepartmentUnpaid du) return new[] { du.Amount, last.Rows[du.Department].Needs };
                    return null;
                }
                case "tiedGrants": // the tied part of the district's grant withheld
                    return e is GrantPaid g && g.TiedWithheld > 0 ? new[] { g.TiedWithheld, g.Untied } : null;
                default:
                    return null;
            }
        }

        /// <summary>An expense in the season report: its by-account figures count costs as negative (income less costs), so a cost is turned back.</summary>
        private static long Cost(SeasonReport r, Account a)
        {
            foreach (KeyValuePair<Account, long> kv in r.ByAccount) if (kv.Key == a) return -kv.Value;
            return 0;
        }

        private static BusinessReport Report(Business b, long period) { return b.Reports.Find(r => r.Period == period); }

        private static long Sales(BusinessReport r) { return -(r.Balance(Account.Sales) + r.Balance(Account.ServiceIncome)); }

        /// <summary>Farm acres you own with nothing growing and nothing built.</summary>
        private static int EmptyAcres(Simulation sim)
        {
            LandState land = sim.State.Land;
            int n = 0;
            for (int t = 0; t < land.Tiles.Count; t++)
            {
                if (sim.Config.Game.Map.Tiles[t].Terrain != Terrain.Farm || !land.Parcels[land.Tiles[t].ParcelId].Owned) continue;
                if (sim.State.Farm.CropOn(t) != null || sim.State.Orchards?.On(t) != null || land.Buildings.Exists(b => b.TileId == t)) continue;
                n++;
            }
            return n;
        }
    }

    /// <summary>The Codex's words: a card's title, and its numbers shown in its formats.</summary>
    public static class CodexWords
    {
        public static string Title(ViewContext ctx, string id) { return ctx.Strings.Get("codex." + id + ".title"); }

        public static string Number(ViewContext ctx, CodexNumber f, long v)
        {
            switch (f)
            {
                case CodexNumber.Coins: return ctx.Fmt.Coins(v);
                case CodexNumber.Count: return ctx.Fmt.Integer(v);
                case CodexNumber.Percent: return ctx.Fmt.Percent(Fixed.FromRaw(v));
                case CodexNumber.Factor: return ctx.Fmt.Decimal2(Fixed.FromRaw(v)) + "×";
                default: return ctx.Fmt.Decimal1(Fixed.FromRaw(v));
            }
        }

        /// <summary>"Your numbers": the card's sentence with the player's numbers as {n0}, {n1}…</summary>
        public static string Numbers(ViewContext ctx, CodexCardDef d, CodexFind f)
        {
            var args = new List<string>();
            for (int i = 0; i < f.Numbers.Length && i < d.Numbers.Count; i++) { args.Add("n" + i); args.Add(Number(ctx, d.Numbers[i], f.Numbers[i])); }
            return ctx.Strings.Get("codex." + d.Id + ".numbers", args.ToArray());
        }
    }

    /// <summary>The toast after a pass (ADR 0095 §7): one card names itself and opens it; two or more say how many and open Menu → Codex.</summary>
    public sealed class CodexToastVm
    {
        public string Kicker { get; internal set; }
        public string Text { get; internal set; }
        public SheetRequest Opens { get; internal set; }

        public static CodexToastVm Build(ViewContext ctx, IReadOnlyList<CodexFind> found)
        {
            if (found == null || found.Count == 0) return null;
            var s = ctx.Strings;
            if (found.Count == 1)
                return new CodexToastVm { Kicker = s.Get("codex.toast.found"), Text = CodexWords.Title(ctx, found[0].Id), Opens = new SheetRequest(CodexSheet.CardKind, ctx.Sim.Config.Game.Codex.IndexOf(found[0].Id)) };
            return new CodexToastVm { Kicker = s.Get("codex.title"), Text = s.Get("codex.toast.burst", "n", ctx.Fmt.Integer(found.Count)), Opens = new SheetRequest(CodexSheet.Kind, 0) };
        }
    }

    /// <summary>Menu → Codex (spec 018 §3.3, §7): the cards found by group, unfound ones hidden, a New tag until read; and one card.</summary>
    public static class CodexSheet
    {
        public const string Kind = "Codex", CardKind = "CodexCard";

        public static SheetVm Build(ViewContext ctx)
        {
            var s = ctx.Strings;
            CodexDefs d = ctx.Sim.Config.Game.Codex;
            GuidanceState g = ctx.Sim.State.Guidance;
            var vm = new SheetVm { Icon = "codex", Title = s.Get("codex.title"),
                Subtitle = s.Get("codex.subtitle", "n", ctx.Fmt.Integer(Found(ctx.Sim)), "of", ctx.Fmt.Integer(d.Cards.Count)) };
            for (int gi = 0; gi < d.Groups.Count; gi++)
            {
                bool header = false;
                for (int i = 0; i < d.Cards.Count; i++)
                {
                    CodexCardDef c = d.Cards[i];
                    CodexFind f = c.Group == gi ? g.Found(c.Id) : null;
                    if (f == null) continue;
                    if (!header) { LineVm h = LineVm.Header(s.Get("codex.group." + d.Groups[gi])); h.Icon = d.GroupIcons[gi]; vm.Lines.Add(h); header = true; }
                    vm.Lines.Add(new LineVm(CodexWords.Title(ctx, c.Id), null)
                        { Tag = f.Read ? null : s.Get("codex.new"), Action = ActionVm.Open("codex:" + c.Id, s.Get("codex.read"), new SheetRequest(CardKind, i)) });
                }
            }
            if (Found(ctx.Sim) == 0) vm.Lines.Add(new LineVm(s.Get("codex.empty"), null));
            return vm;
        }

        public static int Found(Simulation sim)
        {
            int n = 0;
            CodexDefs d = sim.Config.Game?.Codex;
            if (d != null && sim.State.Guidance != null) foreach (CodexFind f in sim.State.Guidance.Codex) if (d.IndexOf(f.Id) >= 0) n++;
            return n;
        }

        /// <summary>One card, as the Why? popup shows it: its group and title, the plain idea, the player's numbers, a real-world line.</summary>
        public static WhyVm Card(ViewContext ctx, int index)
        {
            var s = ctx.Strings;
            CodexDefs d = ctx.Sim.Config.Game.Codex;
            CodexCardDef c = d.Cards[index];
            CodexFind f = ctx.Sim.State.Guidance.Found(c.Id);
            var vm = new WhyVm { Icon = d.GroupIcons[c.Group], Kicker = s.Get("codex.group." + d.Groups[c.Group]), Title = CodexWords.Title(ctx, c.Id) };
            if (f == null) return vm; // not found: never listed, so never opened
            vm.Rows.Add(new WhyRowVm(s.Get("codex." + c.Id + ".text"), null));
            vm.Rows.Add(new WhyRowVm(s.Get("codex.yourNumbers"), null) { Heading = true });
            vm.Rows.Add(new WhyRowVm(CodexWords.Numbers(ctx, c, f), null));
            vm.Rows.Add(new WhyRowVm(s.Get("codex.realWorld"), null) { Heading = true });
            vm.Rows.Add(new WhyRowVm(s.Get("codex." + c.Id + ".world"), null));
            return vm;
        }

        /// <summary>The command that takes a card's New tag off when it's opened, or null when it's read already.</summary>
        public static Command Read(Simulation sim, int index, long at)
        {
            CodexCardDef c = sim.Config.Game.Codex.Cards[index];
            CodexFind f = sim.State.Guidance.Found(c.Id);
            return f == null || f.Read ? null : new ReadCodexCard(at, c.Id);
        }
    }

    public static partial class WhyView
    {
        /// <summary>The Codex's count on the Town tab: how cards are found, and that the unfound stay hidden (spec 018 §3.3).</summary>
        private static WhyVm Codex(ViewContext ctx, long id)
        {
            int found = CodexSheet.Found(ctx.Sim), of = ctx.Sim.Config.Game.Codex.Cards.Count;
            WhyVm vm = New(ctx, "why.codex.title", ctx.Strings.Get("codex.subtitle", "n", ctx.Fmt.Integer(found), "of", ctx.Fmt.Integer(of)));
            Text(vm, ctx, "why.codex.how");
            Text(vm, ctx, "why.codex.hidden");
            vm.Footnote = ctx.Strings.Get("why.codex.lesson");
            return vm;
        }
    }
}
