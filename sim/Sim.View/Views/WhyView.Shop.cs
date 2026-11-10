using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Shops and customer choice (spec 017 §7): every share, figure and price explained in words.</summary>
    public static partial class WhyView
    {
        /// <summary>A part of the attraction in points: "+0.15 points" or "−0.20 points" (spec 017 §7).</summary>
        private static string Part(ViewContext ctx, Fixed f) { return ctx.Strings.Get("why.shopShare.points", "n", Signed(f)); }

        /// <summary>A pull in points: "1.15 points".</summary>
        private static string Pull(ViewContext ctx, Fixed f) { return ctx.Strings.Get("why.shopShare.points", "n", ctx.Fmt.Decimal2(f)); }

        /// <summary>A cup's milk in millilitres ("60 ml"): litres to one decimal can't tell a standard cup from a good one.</summary>
        private static string Ml(ViewContext ctx, long ml) { return ctx.Strings.Get("why.shopQuality.ml", "n", ctx.Fmt.Integer(ml)); }

        private static string Signed(Fixed f)
        {
            long hundredths = (f * Fixed.FromInt(100)).RoundToLong();
            string sign = hundredths < 0 ? "−" : "+";
            long a = System.Math.Abs(hundredths);
            return sign + (a / 100) + "." + (a % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The attraction's parts for a group: price, quality, stars, distance, queue, and the total.</summary>
        private static void Parts(WhyVm vm, ViewContext ctx, Attraction a, ShopGroup g, ShopKind kind, Fixed factor, Fixed stars, int quality)
        {
            Text(vm, ctx, g == ShopGroup.Town ? "why.shopShare.town" : "why.shopShare.village");
            Row(vm, ctx, "why.shopShare.price", Part(ctx, a.Price), "price", ShopWords.PriceSetting(ctx, factor));
            Row(vm, ctx, "why.shopShare.quality", Part(ctx, a.Quality), "quality", ShopWords.Quality(ctx, quality));
            Row(vm, ctx, "why.shopShare.stars", Part(ctx, a.Stars), "stars", ShopWords.Stars(ctx, stars));
            Row(vm, ctx, "why.shopShare.distance", Part(ctx, a.Distance), "m", ctx.Fmt.Integer(a.Metres));
            Row(vm, ctx, "why.shopShare.wait", Part(ctx, a.Wait), "min", ctx.Fmt.Integer(a.WaitMinutes.RoundToLong()));
            Row(vm, ctx, "why.shopShare.total", Pull(ctx, a.Total));
        }

        /// <summary>A shop's share of today's customers (§3.3): its attraction to each group, part by part, over all the shops open.</summary>
        private static WhyVm ShopShare(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm;
            if (id >= ShopWords.RivalBase)
            {
                int i = (int)(id - ShopWords.RivalBase);
                var all = sim.State.Shops.All();
                if (i >= all.Count) return New(ctx, "why.shopShare.gone", null); // a newcomer packed up since the sheet was drawn
                Rival r = all[i];
                vm = New(ctx, "why.shopShare.title", ctx.Fmt.Percent(ShopSheet.Share(r.CustomersToday, ShopSheet.Everyone(sim, r.Kind))), "name", ShopWords.Rival(ctx, r));
                foreach (ShopGroup g in new[] { ShopGroup.Village, ShopGroup.Town }) Parts(vm, ctx, ShopRules.Attract(sim, r, g), g, r.Kind, r.PriceFactor, r.Stars, ShopRules.RivalQuality(sim, r));
                Text(vm, ctx, "why.shopShare.rival." + (r.Newcomer ? (r.Pitch >= 0 ? "newcomer" : "newcomerCart") : r.Id));
                if (!r.Newcomer && ShopRules.Defs(sim).Rivals[r.Def].Reacts) Text(vm, ctx, "why.shopShare.reacts", "pct", ctx.Fmt.Percent(ShopRules.Defs(sim).RivalStep),
                    "floor", ctx.Fmt.Percent(Fixed.One - ShopRules.Defs(sim).RivalFloor));
            }
            else
            {
                Business b = BusinessOf(ctx, id);
                BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                vm = New(ctx, "why.shopShare.title", ctx.Fmt.Percent(ShopSheet.Share(b.CustomersToday, ShopSheet.Everyone(sim, t.Kind))), "name", BusinessWords.Name(ctx, t));
                foreach (ShopGroup g in new[] { ShopGroup.Village, ShopGroup.Town }) Parts(vm, ctx, ShopRules.Attract(sim, b, g), g, t.Kind, b.PriceFactor, b.Stars, b.Quality);
            }
            Text(vm, ctx, "why.shopShare.how");
            vm.Footnote = ctx.Strings.Get("why.shopShare.lesson");
            return vm;
        }

        /// <summary>Today's customers of a kind (§3.2): the groups' demand at the usual hours, and how much of it you served.</summary>
        private static WhyVm ShopToday(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            WhyVm vm = New(ctx, "why.shopToday.title", ShopWords.Amount(ctx, t.Kind, b.ServedToday), "name", BusinessWords.Name(ctx, t));
            Row(vm, ctx, "why.shopToday.chose", ShopWords.Amount(ctx, t.Kind, b.CustomersToday));
            Row(vm, ctx, "why.shopToday.served", ShopWords.Amount(ctx, t.Kind, b.ServedToday));
            ShopDefs d = ShopRules.Defs(sim);
            Text(vm, ctx, "why.shopToday.hours", "open", d.OpenHour + ":00", "close", d.CloseHour + ":00");
            Text(vm, ctx, b.CustomersToday > b.ServedToday ? "why.shopToday.short" : "why.shopToday.all");
            vm.Footnote = ctx.Strings.Get("why.shopToday.lesson");
            return vm;
        }

        /// <summary>Customers no shop served today (§3.2): every shop of the kind closed, full or out of stock.</summary>
        private static WhyVm ShopLost(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, BusinessOf(ctx, id));
            WhyVm vm = New(ctx, "why.shopLost.title", ShopWords.Amount(ctx, t.Kind, sim.State.Shops.LostToday[(int)t.Kind]));
            Text(vm, ctx, "why.shopLost.how");
            vm.Footnote = ctx.Strings.Get("why.shopLost.lesson");
            return vm;
        }

        /// <summary>The people who buy at shops (§3.2): the village's old families and your town.</summary>
        private static WhyVm ShopPeople(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            int town = sim.State.Citizens?.Population ?? 0;
            WhyVm vm = New(ctx, "why.shopPeople.title", ctx.Fmt.Integer(ShopRules.Defs(sim).VillagePeople + town));
            Row(vm, ctx, "why.shopPeople.village", ctx.Fmt.Integer(ShopRules.Defs(sim).VillagePeople));
            Row(vm, ctx, "why.shopPeople.town", ctx.Fmt.Integer(town));
            Text(vm, ctx, "why.shopPeople.centres");
            return vm;
        }

        /// <summary>What people buy of a kind a day (§3.2): a person's amount × the days of life in a real day × the people who buy it.</summary>
        private static WhyVm ShopDemand(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[(int)type];
            ShopKindDef k = ShopRules.Defs(sim).Kind(t.Kind);
            long total = 0;
            foreach (int g in ShopRules.KindGoods(sim, t.Kind))
                if (t.Kind != ShopKind.Veg || t.Sells(g))
                    foreach (ShopGroup grp in new[] { ShopGroup.Town, ShopGroup.Village }) total += ShopRules.DemandPerDay(sim, t.Kind, g, grp);
            string kind = t.Kind.ToString().ToLowerInvariant();
            WhyVm vm = New(ctx, "why.shopDemand.title." + kind, ShopWords.Amount(ctx, t.Kind, total));
            Fixed perDay = k.PerPerson * sim.Config.Game.Population.RealDaysPerGameDay;
            Row(vm, ctx, "why.shopDemand.each." + kind, t.Kind == ShopKind.Chai ? ShopWords.Cups(ctx, perDay.RoundToLong()) : ctx.Fmt.Kg(perDay.MulRatio(1000, 1).RoundToLong()));
            if (k.AdultShare < Fixed.One) Row(vm, ctx, "why.shopDemand.adults", ctx.Fmt.Percent(k.AdultShare));
            Row(vm, ctx, "why.shopDemand.price", ctx.Fmt.Percent(k.Elasticity.MulRatio(1, 10))); // 10% dearer: about this much less bought
            Text(vm, ctx, "why.shopDemand.how." + kind);
            return vm;
        }

        /// <summary>The reference price (§3.3): what customers measure a shop's price against.</summary>
        private static WhyVm ShopReference(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[(int)type];
            string kind = t.Kind.ToString().ToLowerInvariant();
            WhyVm vm = New(ctx, "why.shopReference.title", ShopWords.Price(ctx, t.Kind, Fixed.One));
            Text(vm, ctx, "why.shopReference." + kind, "pct", ctx.Fmt.Percent(ShopRules.Defs(sim).RetailMarkup - Fixed.One));
            vm.Footnote = ctx.Strings.Get("why.shopReference.lesson");
            return vm;
        }

        /// <summary>A shop's price setting (§3.1): the reference × its setting, and what it does to customers.</summary>
        private static WhyVm ShopPrice(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            WhyVm vm = New(ctx, "why.shopPrice.title", ShopWords.Price(ctx, t.Kind, b.PriceFactor));
            Row(vm, ctx, "why.shopPrice.setting", ShopWords.PriceSetting(ctx, b.PriceFactor));
            Row(vm, ctx, "why.shopPrice.part", Part(ctx, ShopRules.Defs(sim).WPrice * (Fixed.One - b.PriceFactor)));
            Text(vm, ctx, "why.shopPrice.fewer");
            // ADR 0095 §1: above the reference a newcomer may come; the village's own cut when they lose customers.
            ShopDefs sd = ShopRules.Defs(sim);
            if (b.PriceFactor > Fixed.One)
                Text(vm, ctx, sim.State.Shops.Newcomers.Exists(x => x.Kind == t.Kind) ? "why.shopPrice.newcomerHere" : "why.shopPrice.newcomer", "pct", ctx.Fmt.Percent(sd.NewcomerChance));
            vm.Footnote = ctx.Strings.Get("why.shopPrice.lesson");
            return vm;
        }

        /// <summary>The share a new shop on a pitch would get of the village (the plan's, §3.3): its attraction against the rivals'.</summary>
        private static WhyVm ShopExpected(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[(int)type];
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.shopExpected.title", ctx.Fmt.Percent(ShopRules.ExpectedShare(sim, t.Kind, ShopGroup.Village, d.SquareTile)));
            Row(vm, ctx, "why.shopExpected.yours", Pull(ctx, ShopRules.Attract(sim, Fixed.One, 1, d.StartStars, 0, Fixed.Zero).Total));
            foreach (Rival r in sim.State.Shops.All())
                if (r.Kind == t.Kind) Row(vm, ctx, "why.shopExpected.rival", Pull(ctx, ShopRules.Attract(sim, r, ShopGroup.Village).Total), "name", ShopWords.Rival(ctx, r));
            Text(vm, ctx, "why.shopExpected.how");
            vm.Footnote = ctx.Strings.Get("why.shopShare.lesson");
            return vm;
        }

        /// <summary>A pitch's rent (§3.4): to the council each season, upfront.</summary>
        private static WhyVm PitchRent(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.pitchRent.title", ctx.Fmt.Coins(MarketRules.Indexed(sim, ShopRules.Defs(sim).PitchRent)));
            Text(vm, ctx, "why.pitchRent.how");
            vm.Footnote = ctx.Strings.Get("why.pitchRent.lesson");
            return vm;
        }

        /// <summary>A stall's good (§5): what it puts out a day, where it comes from, what's in stock now.</summary>
        private static WhyVm ShopPutOut(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            int g = (int)(id % 1000);
            string name = ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey);
            WhyVm vm = New(ctx, "why.shopPutOut.title", ctx.Fmt.Kg(b.PutOut[g]), "good", name);
            Row(vm, ctx, "why.shopPutOut.stock", ctx.Fmt.Kg(b.Stock[g]));
            Row(vm, ctx, "why.shopPutOut.price", ctx.Fmt.PricePerKg(ShopRules.Price(sim, b, g)));
            Text(vm, ctx, ShopRules.VegShare(sim, g) > Fixed.Zero ? "why.shopPutOut.store" : "why.shopPutOut.yours");
            Text(vm, ctx, "why.shopPutOut.spoils", "pct", ctx.Fmt.Percent(sim.Config.Game.Market.Goods[g].SpoilagePerDay));
            return vm;
        }

        /// <summary>The tea stall's milk (§3.1, ADR 0091 §1): your herd's first at the 6:00 milking, then neighbours'.</summary>
        /// <summary>
        /// A shop's or the mill's stars (§3.5, ADR 0092): today's score so far, part by part, and how a night moves the stars a tenth of
        /// the way to it; shut days in a row (the first forgiven).
        /// </summary>
        private static WhyVm ShopStars(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.shopStars.title", ShopWords.Stars(ctx, b.Stars), "name", BusinessWords.Name(ctx, t));
            Fixed served = ShopRules.ServedShare(b);
            Text(vm, ctx, "why.shopStars.today");
            Row(vm, ctx, "why.shopStars.served", Part(ctx, served * d.ScoreServed), "pct", ctx.Fmt.Percent(served));
            Row(vm, ctx, "why.shopStars.quality", Part(ctx, b.Quality == 2 ? d.ScoreGood : d.ScoreStandard), "quality", ShopWords.Quality(ctx, b.Quality));
            Row(vm, ctx, "why.shopStars.value", Part(ctx, ShopRules.ValuePart(sim, b)), "price", t.IsShop ? ShopWords.PriceSetting(ctx, b.PriceFactor) : ctx.Strings.Get("why.shopStars.fee"));
            Row(vm, ctx, "why.shopStars.score", ShopWords.Stars(ctx, ShopRules.DayScore(sim, b, served)));
            if (b.ShutDays > 0) Row(vm, ctx, "why.shopStars.shut", ctx.Fmt.Integer(b.ShutDays));
            Text(vm, ctx, "why.shopStars.how");
            vm.Footnote = ctx.Strings.Get("why.shopStars.lesson");
            return vm;
        }

        /// <summary>Quality (§3.5, ADR 0092): what good quality costs this kind of shop, and what it earns.</summary>
        private static WhyVm ShopQuality(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.shopQuality.title", ShopWords.Quality(ctx, b.Quality, true));
            Text(vm, ctx, "why.shopQuality." + t.Kind.ToString().ToLowerInvariant());
            if (t.Kind == ShopKind.Chai) Row(vm, ctx, "why.shopQuality.cupMilk", Ml(ctx, t.GoodCupMilkMl), "standard", Ml(ctx, t.CupMilkMl));
            if (t.GoodRecipeExtra > Fixed.Zero) Row(vm, ctx, "why.shopQuality.recipe", ctx.Fmt.Percent(t.GoodRecipeExtra));
            Row(vm, ctx, "why.shopQuality.pull", Part(ctx, d.WQuality));
            Row(vm, ctx, "why.shopQuality.stars", Part(ctx, d.ScoreGood - d.ScoreStandard));
            vm.Footnote = ctx.Strings.Get("why.shopQuality.lesson");
            return vm;
        }

        private static WhyVm ShopMilk(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            WhyVm vm = New(ctx, "why.shopMilk.title", ctx.Fmt.Litres(ShopRules.Milk(sim, b)));
            Row(vm, ctx, "why.shopMilk.day", ctx.Fmt.Litres(b.OwnPerDay));
            Row(vm, ctx, "why.shopMilk.cup", Ml(ctx, t.CupMl(b.Quality)));
            Row(vm, ctx, "why.shopMilk.neighbours", ctx.Fmt.PricePerLitre(BusinessRules.NeighbourMilkPrice(sim, t)));
            Text(vm, ctx, "why.shopMilk.how");
            return vm;
        }
    }
}
