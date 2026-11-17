using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// <em>Taxes and policies</em> (spec 022 §7, US-140): a slider for each tax with its preview, who pays and whether the close is covered;
    /// the policies' tab arrives with US-141. The rates apply from the next season, so a change shows as pending, with <em>Undo</em>.
    /// </summary>
    public static class TaxSheet
    {
        public const string Kind = "Taxes";

        /// <summary>A house rate is set in tenths of a percent a year (16 = 1.6%), a business rate in whole percents (10 = 10%).</summary>
        public static Fixed HouseRate(long tenths) { return Fixed.FromRatio(tenths, 1000); }
        public static Fixed BusinessRate(long percent) { return Fixed.FromRatio(percent, 100); }
        public static long Units(TaxKind kind, Fixed rate) { return kind == TaxKind.House ? (rate * Fixed.FromInt(1000)).RoundToLong() : (rate * Fixed.FromInt(100)).RoundToLong(); }
        public static Fixed RateOf(TaxKind kind, long units) { return kind == TaxKind.House ? HouseRate(units) : BusinessRate(units); }

        public static string Percent(ViewContext ctx, TaxKind kind, long units)
        {
            return kind == TaxKind.House ? ctx.Strings.Get("tax.houseRate", "rate", ctx.Fmt.Decimal1(Fixed.FromRatio(units, 10))) : ctx.Strings.Get("tax.businessRate", "rate", ctx.Fmt.Integer(units));
        }

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("tax.title"), Subtitle = ReportSheet.PeriodName(ctx, sim.Period) };
            if (!TreasuryRules.IsOpen(sim)) return vm;
            TreasuryState st = TreasuryRules.State(sim);
            vm.Lines.Add(new LineVm(s.Get("tax.notYours"), null) { Banner = true });
            if (!st.TaxTourSeen) // the first-visit note (spec 022 §3.7): once
                vm.Lines.Add(new LineVm(s.Get("tax.tourTitle"), null) { Banner = true, Note = s.Get("tax.tour"), Action = ActionVm.Checked(ctx, "tour", s.Get("tax.tourGotIt"), null, ActionKind.Primary, at => new SeeTaxTour(at)) });

            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(sim);
            long income = TreasuryRules.ExpectedIncome(sim, TaxRules.Next(sim, TaxKind.House), TaxRules.Next(sim, TaxKind.Business));
            vm.Lines.Add(new LineVm(s.Get("tax.cash"), ctx.Fmt.Coins(st.Cash), "treasuryBalance", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.atClose"), TreasurySheet.CloseStatus(ctx, income, f.Due, st.Cash, out Tone tone), "treasuryDue", 0) { NoteTone = tone });

            Row(ctx, vm, TaxKind.House, f);
            Row(ctx, vm, TaxKind.Business, f);

            TreasuryReport last = st.Reports.Count == 0 ? null : st.Reports[st.Reports.Count - 1];
            if (last != null)
            {
                long billedHouse = -last.Balance(Sim.Core.Finance.Account.HouseTaxIncome), billedBiz = -last.Balance(Sim.Core.Finance.Account.BusinessTaxIncome);
                vm.Lines.Add(new LineVm(s.Get("tax.collectedLast", "amount", ctx.Fmt.Coins(billedHouse + billedBiz)), null, "taxCollected", 0));
            }
            return vm;
        }

        /// <summary>One tax: its slider (set when let go, from the next season), the pending line with <em>Undo</em>, and what a rate would do.</summary>
        private static void Row(ViewContext ctx, SheetVm vm, TaxKind kind, TreasuryRules.CloseForecast f)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            bool house = kind == TaxKind.House;
            Fixed now = TaxRules.Rate(sim, kind), next = TaxRules.Next(sim, kind), bound = TaxRules.Bound(sim, kind);
            long max = Units(kind, bound), start = Units(kind, next);
            Fixed other = TaxRules.Next(sim, house ? TaxKind.Business : TaxKind.House);
            var amount = new AmountVm(house ? "tax:house" : "tax:business", 0, max, 1, start, units => Pick(ctx, kind, units, f), units => Percent(ctx, kind, units))
            {
                Scale = 1, // typed in tenths of a percent (house) or whole percents (business), not coins
                MinCaption = ctx.Fmt.Percent(Fixed.Zero),
                MaxCaption = s.Get(house ? "tax.houseMax" : "tax.businessMax", "max", Percent(ctx, kind, max)),
                Set = units => ActionVm.Checked(ctx, "tax:" + kind, s.Get("tax.set"), null, ActionKind.Primary, at => new SetTaxRate(at, kind, RateOf(kind, units))),
            };
            string caption = house ? s.Plural("tax.houseCaption", HousesYou(sim), "each", ctx.Fmt.Coins(TaxRules.StandardHouseTax(sim, now)))
                : s.Get("tax.businessCaption");
            vm.Lines.Add(new LineVm(s.Get(house ? "tax.house" : "tax.business"), null, house ? "taxHouse" : "taxBusiness", 0) { Amount = amount, Icon = house ? "place_house_1" : "place_grocery", Note = caption });
            if (TaxRules.Pending(sim, kind) >= Fixed.Zero)
                vm.Lines.Add(new LineVm(s.Get("tax.pending", "from", NextSeason(ctx), "to", Percent(ctx, kind, Units(kind, TaxRules.Pending(sim, kind))), "now", Percent(ctx, kind, Units(kind, now))), null)
                {
                    NoteTone = Tone.Warn,
                    Action = ActionVm.Checked(ctx, "undo:" + kind, s.Get("tax.undo"), null, ActionKind.Secondary, at => new UndoTaxRate(at, kind)),
                });
        }

        private static string NextSeason(ViewContext ctx) { return FieldSheets.SeasonName(ctx, ctx.Sim.Period + 1); }

        private static int HousesYou(Simulation sim) { TaxRules.YourHouseBill(sim, TaxRules.Rate(sim, TaxKind.House), out int n); return n; }

        /// <summary>What a rate would do (spec 022 §7): the change in a season's revenue, who pays it, and whether the close is covered; each from the sim's own sums.</summary>
        private static AmountPick Pick(ViewContext ctx, TaxKind kind, long units, TreasuryRules.CloseForecast f)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TreasuryState st = TreasuryRules.State(sim);
            bool house = kind == TaxKind.House;
            Fixed rate = RateOf(kind, units);
            Fixed hr = house ? rate : TaxRules.Next(sim, TaxKind.House), br = house ? TaxRules.Next(sim, TaxKind.Business) : rate;
            Fixed hrNow = TaxRules.Rate(sim, TaxKind.House), brNow = TaxRules.Rate(sim, TaxKind.Business);
            long total = TaxRules.Expected(sim, hr, br, out _, out long shops, out long houseYou, out long bizYou);
            long totalNow = TaxRules.Expected(sim, hrNow, brNow, out _, out long shopsNow, out long houseNow, out long bizNow);
            long delta = total - totalNow, you = house ? houseYou - houseNow : bizYou - bizNow, other = house ? 0 : shops - shopsNow;
            string line1, line2;
            if (units == Units(kind, TaxRules.Rate(sim, kind)))
            {
                line1 = house ? s.Get("tax.nowHouse", "all", ctx.Fmt.Coins(houseNow), "you", ctx.Fmt.Coins(houseNow))
                    : s.Get("tax.nowBusiness", "all", ctx.Fmt.Coins(bizNow + shopsNow), "you", ctx.Fmt.Coins(bizNow), "shops", ctx.Fmt.Coins(shopsNow));
            }
            else
            {
                string sign = delta >= 0 ? "+" : "−";
                line1 = house ? s.Get("tax.changeHouse", "amount", sign + ctx.Fmt.Coins(System.Math.Abs(delta)), "you", ctx.Fmt.Coins(System.Math.Abs(you)))
                    : s.Get("tax.changeBusiness", "amount", sign + ctx.Fmt.Coins(System.Math.Abs(delta)), "you", ctx.Fmt.Coins(System.Math.Abs(you)), "shops", ctx.Fmt.Coins(System.Math.Abs(other)));
            }
            if (house) line2 = s.Get("tax.leftHouse", "amount", ctx.Fmt.Coins(LeftAtCap(sim)));
            else
            {
                long profit = 0;
                if (sim.State.Businesses != null)
                    foreach (Sim.Core.Business.Business b in sim.State.Businesses.Businesses)
                        if (b.Status != Sim.Core.Business.BusinessStatus.Closed && b.Reports.Count > 0)
                        {
                            Sim.Core.Business.BusinessReport last = b.Reports[b.Reports.Count - 1];
                            profit += System.Math.Max(0, Sim.Core.Business.BusinessBooks.NetProfit(last) + last.Balance(Sim.Core.Finance.Account.BusinessTax));
                        }
                line2 = profit > 0 ? s.Get("tax.keepBusiness", "kept", ctx.Fmt.Coins(profit - Sim.Core.Finance.Money.MulRate(profit, rate)), "profit", ctx.Fmt.Coins(profit)) : null;
            }
            long income = TreasuryRules.ExpectedIncome(sim, hr, br);
            string status = TreasurySheet.CloseStatus(ctx, income, f.Due, st.Cash, out Tone tone);
            bool shortOfCash = income + st.Cash < f.Due;
            return new AmountPick { Note = line1 + "\n" + (line2 == null ? "" : line2 + "\n") + s.Get("tax.atClose", "in", ctx.Fmt.Coins(income), "out", ctx.Fmt.Coins(f.Due), "status", status), Tone = shortOfCash ? Tone.Warn : Tone.Plain };
        }

        /// <summary>What a house leaves you at the top rate, a season: the rent less its tax and its wear (spec 022 §3.1's example: 2,500 − 360 − 500).</summary>
        private static long LeftAtCap(Simulation sim)
        {
            Sim.Core.Definitions.BuildingDef house = sim.Config.Game.Buildings[sim.Config.Game.Population.House];
            long rent = Sim.Core.Market.MarketRules.Indexed(sim, sim.Config.Game.Population.BaseRent);
            long value = Sim.Core.Market.MarketRules.Indexed(sim, house.Value);
            long tax = System.Math.Min(Sim.Core.Finance.Money.MulRate(value, sim.Config.Game.Treasury.Taxes.HouseMax) / 4, Sim.Core.Finance.Money.MulRate(rent, sim.Config.Game.Treasury.Taxes.MaxShareOfRent));
            return rent - tax - value / house.UsefulLifeSeasons;
        }
    }
}
