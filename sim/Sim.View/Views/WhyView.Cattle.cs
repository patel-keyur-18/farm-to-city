using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Livestock;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>The dairy co-op's sources (spec 012 §3.5, ADR 0061).</summary>
    public static partial class WhyView
    {
        /// <summary>The co-op's price a litre for an animal kind (id): a base, plus so much a point of fat, at today's prices.</summary>
        private static WhyVm MilkPrice(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockDefs defs = sim.Config.Game.Livestock;
            AnimalDef a = defs.Animals[(int)id];
            LivestockDef l = defs.Livestock;
            WhyVm vm = New(ctx, "why.milkPrice.title", PerLitre(ctx, LivestockRules.MilkPrice(sim, (int)id)), "animal", ctx.Strings.Get(a.NameKey).ToLowerInvariant());
            Row(vm, ctx, "why.milkPrice.base", PerLitre(ctx, l.MilkBasePerLitre));
            Row(vm, ctx, "why.milkPrice.fat", PerLitre(ctx, l.MilkPerFatPoint * a.FatPercent), "fat", ctx.Fmt.Decimal1(a.FatPercent), "per", ctx.Fmt.Paise((l.MilkPerFatPoint * Fixed.FromInt(100)).RoundToLong()));
            Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
            vm.Footnote = ctx.Strings.Get("why.milkPrice.lesson");
            return vm;
        }

        /// <summary>This morning's milk: what went to the co-op and what it paid, and what the household kept.</summary>
        private static WhyVm MilkToday(ViewContext ctx, long id)
        {
            LivestockState s = ctx.Sim.State.Livestock;
            WhyVm vm = New(ctx, "why.milkToday.title", ctx.Fmt.Coins(s.LastMilkAmount));
            Row(vm, ctx, "why.milkToday.sold", ctx.Fmt.Litres(s.LastMilkMl - s.LastHomeMl));
            Row(vm, ctx, "why.milkToday.home", ctx.Fmt.Litres(s.LastHomeMl));
            vm.Footnote = ctx.Strings.Get("why.milkToday.lesson");
            return vm;
        }

        /// <summary>The shed (spec 012 §3.3): what it costs, what it holds, how long it takes. id 0 cost and time, 1 room.</summary>
        private static WhyVm ShedCost(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BuildingDef d = sim.Config.Game.Buildings[LivestockRules.Defs(sim).Shed];
            WhyVm vm = New(ctx, "why.shed.title", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.Value)));
            Row(vm, ctx, "why.shed.base", Money(ctx, d.Value));
            Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
            Row(vm, ctx, "why.shed.room", ctx.Strings.Plural("shed.roomValue", d.Animals));
            Row(vm, ctx, "why.shed.feedRoom", ctx.Fmt.Kg(d.FeedRoomKg * 1000));
            Row(vm, ctx, "why.shed.strawStack", ctx.Fmt.Kg(d.StrawStackKg * 1000));
            Row(vm, ctx, "why.shed.time", ctx.Fmt.Duration(d.BuildHours * 3600L));
            vm.Footnote = ctx.Strings.Get("why.shed.lesson");
            return vm;
        }

        /// <summary>Her milk this morning (spec 012 §3.5): a day's mean × the lactation curve × the season × fed × health × Care.</summary>
        private static WhyVm AnimalMilk(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            LivestockDefs defs = LivestockRules.Defs(sim);
            AnimalDef d = defs.Animals[a.Kind];
            WhyVm vm = New(ctx, "why.animalMilk.title", ctx.Fmt.Litres(a.LastMilkMl), "name", a.Name);
            if (a.Stage == AnimalStage.Dry)
            {
                Text(vm, ctx, "why.animalMilk.dry", "days", ctx.Strings.Plural("why.days", System.Math.Max(1, d.DryDays - a.StageDay)));
                vm.Footnote = ctx.Strings.Get("why.animalMilk.lesson");
                return vm;
            }
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            Row(vm, ctx, "why.animalMilk.mean", ctx.Fmt.Litres((LivestockRules.PerGameDay(sim, d.MilkPerDay) * Fixed.FromInt(1000)).RoundToLong()),
                "perDay", ctx.Fmt.Decimal1(d.MilkPerDay), "days", ctx.Fmt.Decimal1(defs.Livestock.RealDaysPerGameDay));
            Row(vm, ctx, "why.animalMilk.curve", Effect(ctx, LivestockRules.Lactation(defs.Livestock, a.StageDay)), "day", ctx.Fmt.Integer(a.StageDay + 1));
            if (d.SeasonMilk[(int)season] != Fixed.One) Row(vm, ctx, "why.animalMilk.season", Effect(ctx, d.SeasonMilk[(int)season]));
            Row(vm, ctx, "why.animalMilk.fed", Pct(ctx, LivestockRules.FedFactor(sim, a)));
            Row(vm, ctx, "why.animalMilk.health", Pct(ctx, a.Health));
            Row(vm, ctx, "why.animalMilk.care", Pct(ctx, LivestockRules.CareFactor(sim)));
            // Bought since yesterday's collection: only her hours with you are paid this morning (§3.5).
            long sixAm = ctx.Now - ((ctx.Now - defs.Livestock.FeedHour * 3600L) % ClockDef.SecondsPerDay + ClockDef.SecondsPerDay) % ClockDef.SecondsPerDay;
            long owned = sixAm - a.BoughtAt;
            if (owned > 0 && owned < ClockDef.SecondsPerDay) Row(vm, ctx, "why.animalMilk.owned", ctx.Fmt.Duration(owned));
            if (a.SickDays > 0) Text(vm, ctx, "why.animalMilk.sick");
            vm.Footnote = ctx.Strings.Get("why.animalMilk.lesson");
            return vm;
        }

        /// <summary>Her fed score this morning (§3.7): each feed's share of her need, weighted green 40%, straw 20%, feed 40%.</summary>
        private static WhyVm AnimalFed(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            LivestockDef l = LivestockRules.Defs(sim).Livestock;
            FeedingReport f = sim.State.Livestock.LastFeeding;
            WhyVm vm = New(ctx, "why.animalFed.title", Pct(ctx, a.FedScore), "name", a.Name);
            Row(vm, ctx, "why.animalFed.green", Share(ctx, f.GreenCommons + f.GreenOwn + f.GreenBought, f.GreenNeed), "weight", Pct(ctx, l.GreenWeight));
            Row(vm, ctx, "why.animalFed.straw", Share(ctx, f.StrawGiven, f.StrawNeed), "weight", Pct(ctx, l.StrawWeight));
            Row(vm, ctx, "why.animalFed.feed", Share(ctx, f.FeedGiven, f.FeedNeed), "weight", Pct(ctx, l.FeedWeight));
            Text(vm, ctx, "why.animalFed.hungry", "pct", Pct(ctx, l.HungryBelow));
            vm.Footnote = ctx.Strings.Get("why.animalFed.lesson");
            return vm;
        }

        private static string Share(ViewContext ctx, long given, long need)
        {
            return Pct(ctx, need <= 0 || given >= need ? Fixed.One : Fixed.FromRatio(given, need));
        }

        /// <summary>Her health (§3.7, §3.10): hunger takes it down after a grace, a fed day brings it back, illness takes some.</summary>
        private static WhyVm AnimalHealth(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            LivestockDef l = LivestockRules.Defs(sim).Livestock;
            WhyVm vm = New(ctx, "why.animalHealth.title", Pct(ctx, a.Health), "name", a.Name);
            Row(vm, ctx, "why.animalHealth.hungry", "−" + Pct(ctx, l.HungerHealthLoss), "days", ctx.Strings.Plural("why.days", l.HungerGraceDays));
            Row(vm, ctx, "why.animalHealth.fed", "+" + Pct(ctx, l.HealthRecoveryPerDay));
            Row(vm, ctx, "why.animalHealth.sick", "−" + Pct(ctx, l.SickHealthLoss));
            Row(vm, ctx, "why.animalHealth.floor", Pct(ctx, l.MinHealth));
            Row(vm, ctx, "why.animalHealth.risk", Pct(ctx, LivestockRules.DiseaseChance(sim, a)));
            vm.Footnote = ctx.Strings.Get("why.animalHealth.lesson");
            return vm;
        }

        /// <summary>What she cost and what she has earned (milk paid by the co-op and her calves).</summary>
        private static WhyVm AnimalMoney(ViewContext ctx, long id)
        {
            Animal a = LivestockRules.Find(ctx.Sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            WhyVm vm = New(ctx, "why.animalMoney.title", Money(ctx, a.Earned - a.Cost), "name", a.Name);
            Row(vm, ctx, "why.animalMoney.cost", "−" + Money(ctx, a.Cost));
            Row(vm, ctx, "why.animalMoney.earned", Money(ctx, a.Earned));
            vm.Footnote = ctx.Strings.Get("why.animalMoney.lesson");
            return vm;
        }

        /// <summary>Her price at the fair (§3.4): price × index × the fair's share × her health.</summary>
        private static WhyVm AnimalSell(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            AnimalDef d = LivestockRules.Defs(sim).Animals[a.Kind];
            WhyVm vm = New(ctx, "why.animalSell.title", Money(ctx, LivestockRules.SellPrice(sim, a)), "name", a.Name);
            Row(vm, ctx, "why.animalSell.price", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.Price)));
            Row(vm, ctx, "why.animalSell.share", Pct(ctx, d.SellShare));
            Row(vm, ctx, "why.animalSell.health", Pct(ctx, a.Health));
            vm.Footnote = ctx.Strings.Get("why.animalSell.lesson");
            return vm;
        }

        /// <summary>The buy card's numbers (id = kind × 10 + row): price, milk, ration, feed cost, labour, payback.</summary>
        private static WhyVm AnimalBuy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int kind = (int)(id / 10), row = (int)(id % 10);
            LivestockDefs defs = LivestockRules.Defs(sim);
            AnimalDef d = defs.Animals[kind];
            Economics e = Economics.Of(sim, kind);
            string animal = ctx.Strings.Get(d.NameKey).ToLowerInvariant();
            WhyVm vm;
            switch (row)
            {
                case 0:
                    vm = New(ctx, "why.animalBuy.price", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.Price)), "animal", animal);
                    Row(vm, ctx, "why.animalSell.price", Money(ctx, d.Price));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.animalBuy.calved");
                    break;
                case 1:
                    vm = New(ctx, "why.animalBuy.milk", ctx.Fmt.Litres(e.MilkMlInMilk), "animal", animal);
                    Row(vm, ctx, "why.animalMilk.mean", ctx.Fmt.Litres(e.MilkMlInMilk), "perDay", ctx.Fmt.Decimal1(d.MilkPerDay), "days", ctx.Fmt.Decimal1(defs.Livestock.RealDaysPerGameDay));
                    Row(vm, ctx, "why.animalBuy.inMilk", ctx.Strings.Plural("why.days", d.MilkDays));
                    Row(vm, ctx, "why.animalBuy.dry", ctx.Strings.Plural("why.days", d.DryDays));
                    Text(vm, ctx, "why.animalBuy.curve");
                    break;
                case 2:
                    vm = New(ctx, "why.animalBuy.ration", null, "animal", animal);
                    Row(vm, ctx, "why.animalBuy.green", ctx.Fmt.Kg(e.Green));
                    Row(vm, ctx, "why.animalBuy.straw", ctx.Fmt.Kg(e.Straw));
                    Row(vm, ctx, "why.animalBuy.feed", ctx.Fmt.Kg(e.Feed));
                    Row(vm, ctx, "why.animalBuy.dryFeed", Pct(ctx, d.DryFeedFactor));
                    Text(vm, ctx, "why.animalBuy.commons");
                    break;
                case 3:
                    vm = New(ctx, "why.animalBuy.feedCost", Money(ctx, e.FeedBought), "animal", animal);
                    Row(vm, ctx, "why.animalBuy.greenCost", Money(ctx, e.FeedBought - e.FeedHome - StrawCost(sim, e)));
                    Row(vm, ctx, "why.animalBuy.strawCost", Money(ctx, StrawCost(sim, e)));
                    Row(vm, ctx, "why.animalBuy.feedRoomCost", Money(ctx, e.FeedHome));
                    Text(vm, ctx, "why.animalBuy.home");
                    break;
                case 4:
                    vm = New(ctx, "why.animalBuy.labour", ctx.Strings.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(d.LabourHoursPerDay)), "animal", animal);
                    Text(vm, ctx, "why.animalBuy.labourText");
                    break;
                default:
                    vm = New(ctx, "why.animalBuy.payback", null, "animal", animal);
                    Row(vm, ctx, "why.animalBuy.milkValue", Money(ctx, e.MilkValue));
                    Row(vm, ctx, "why.animalBuy.feedBought", "−" + Money(ctx, e.FeedBought));
                    Row(vm, ctx, "why.animalBuy.feedHome", "−" + Money(ctx, e.FeedHome));
                    Text(vm, ctx, "why.animalBuy.paybackText");
                    break;
            }
            vm.Footnote = ctx.Strings.Get("why.animalBuy.lesson");
            return vm;
        }

        private static long StrawCost(Simulation sim, Economics e)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            return Sim.Core.Finance.Money.Value(e.Straw, Sim.Core.Market.MarketRules.InputPrice(sim, defs.Straw), sim.Config.Game.Market.Goods[defs.Straw].UnitSize);
        }

        /// <summary>This morning's green fodder (§3.7): the need, the commons' share, your fields' cut, what was bought and sold.</summary>
        private static WhyVm GreenFodder(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            FeedingReport f = sim.State.Livestock.LastFeeding;
            LivestockDef l = LivestockRules.Defs(sim).Livestock;
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            WhyVm vm = New(ctx, "why.green.title", ctx.Fmt.Kg(f.GreenNeed));
            Row(vm, ctx, "why.green.commons", ctx.Fmt.Kg(f.GreenCommons), "pct", Pct(ctx, l.CommonsShare[(int)season]));
            Row(vm, ctx, "why.green.own", ctx.Fmt.Kg(f.GreenOwn));
            Row(vm, ctx, "why.green.bought", ctx.Fmt.Kg(f.GreenBought) + " · " + Money(ctx, f.GreenBoughtAmount));
            if (f.GreenSold > 0) Row(vm, ctx, "why.green.sold", ctx.Fmt.Kg(f.GreenSold) + " · " + Money(ctx, f.GreenSoldAmount));
            Row(vm, ctx, "why.green.price", ctx.Strings.Get("unit.perKg", "amount", ctx.Fmt.Decimal1(l.GreenFodderBuyPrice * sim.State.Market.PriceIndex)));
            vm.Footnote = ctx.Strings.Get("why.green.lesson");
            return vm;
        }

        /// <summary>Straw, feed or compost in its place (§3.3, §3.7): how much of the room, the day's need, the days it lasts.</summary>
        private static WhyVm FeedStock(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int good = (int)id;
            LivestockDefs defs = LivestockRules.Defs(sim);
            GoodDef g = sim.Config.Game.Market.Goods[good];
            long have = sim.State.Market.Stock[good];
            WhyVm vm = New(ctx, "why.stock.title", ctx.Fmt.Kg(have), "good", ctx.Strings.Get(g.NameKey).ToLowerInvariant());
            Row(vm, ctx, "why.stock.room", ctx.Fmt.Kg(Store.CapacityGrams(sim, g.Place)));
            if (good == defs.Compost)
            {
                Text(vm, ctx, "why.stock.compost");
                vm.Footnote = ctx.Strings.Get("why.stock.compostLesson");
                return vm;
            }
            long need = 0;
            foreach (Animal a in sim.State.Livestock.Animals) { var n = LivestockSystem.NeedOf(sim, a); need += good == defs.Straw ? n.Straw : n.Feed; }
            Row(vm, ctx, "why.stock.need", ctx.Fmt.Kg(need));
            long days = LivestockRules.DaysLeft(sim, good);
            if (days != long.MaxValue) Row(vm, ctx, "why.stock.days", ctx.Strings.Plural("why.days", days));
            Row(vm, ctx, "why.stock.price", ctx.Fmt.PricePerKg(Sim.Core.Market.MarketRules.InputPrice(sim, good)));
            Text(vm, ctx, good == defs.Straw ? "why.stock.strawText" : "why.stock.feedText", "low", ctx.Strings.Plural("why.days", defs.Livestock.LowDays));
            vm.Footnote = ctx.Strings.Get("why.stock.lesson");
            return vm;
        }

        /// <summary>The animals' work (§3.9): 1.5 h a day each from the Care pool; the hand's animals order (id 1).</summary>
        private static WhyVm AnimalWork(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.animalWork.title", ctx.Strings.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(LivestockRules.LabourDemand(sim))));
            foreach (Animal a in sim.State.Livestock.Animals)
                Row(vm, ctx, "why.animalWork.animal", ctx.Strings.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(LivestockRules.Defs(sim).Animals[a.Kind].LabourHoursPerDay)), "name", a.Name);
            Row(vm, ctx, "why.care.need", ctx.Strings.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(FarmRules.LabourDemand(sim))));
            Text(vm, ctx, id == 1 ? "why.animalWork.order" : "why.animalWork.text");
            vm.Footnote = ctx.Strings.Get("why.animalWork.lesson");
            return vm;
        }

        /// <summary>A fodder field's daily cut (§3.8b): its season's yield over the season's days × the soil, water, Care, weather and fertiliser.</summary>
        private static WhyVm FodderCut(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Crop c = sim.State.Farm.CropOn((int)id);
            if (c == null) return New(ctx, "why.animal.gone", null);
            CropDef def = sim.Config.Game.Farm.Crops[c.Def];
            WhyVm vm = New(ctx, "why.fodder.title", ctx.Fmt.Kg(FodderView.CutPerDay(sim, c)), "crop", ctx.Strings.Get(def.NameKey).ToLowerInvariant());
            Row(vm, ctx, "why.fodder.base", ctx.Fmt.Kg((def.SeasonYieldPerAcre * Fixed.FromInt(1000)).RoundToLong()));
            Row(vm, ctx, "why.fodder.days", ctx.Fmt.Integer(sim.Clock.SecondsPerSeason / ClockDef.SecondsPerDay));
            Row(vm, ctx, "why.yield.soil", Effect(ctx, c.SoilFactor));
            Row(vm, ctx, "why.fodder.water", Effect(ctx, c.WaterRate));
            Row(vm, ctx, "why.fodder.care", Effect(ctx, sim.State.Farm.CareRatio > Fixed.One ? Fixed.One : sim.State.Farm.CareRatio));
            Row(vm, ctx, "why.fodder.fertiliser", Effect(ctx, sim.Config.Game.Farm.Fertilisers[c.Fertiliser].YieldFactor));
            FeedingReport f = sim.State.Livestock.LastFeeding;
            Row(vm, ctx, "why.fodder.eaten", ctx.Fmt.Kg(f.GreenOwn));
            Row(vm, ctx, "why.fodder.sold", ctx.Fmt.Kg(f.GreenSold) + " · " + Money(ctx, f.GreenSoldAmount));
            vm.Footnote = ctx.Strings.Get("why.fodder.lesson");
            return vm;
        }

        private static string PerLitre(ViewContext ctx, Fixed coins)
        {
            return ctx.Strings.Get("dairy.perLitre", "amount", ctx.Fmt.Paise((coins * Fixed.FromInt(100)).RoundToLong()));
        }
    }
}
