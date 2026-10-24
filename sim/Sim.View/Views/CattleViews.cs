using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>
    /// The cattle acre's sheet (spec 012 §7, ADR 0064): before the Hamlet, when it opens; then Build the shed; while it's built,
    /// when it's ready; then the shed sheet: milk today, a row per animal (her card behind it), buy buttons, every feed with its
    /// stock and Fill, the green split and buying from neighbours, the compost pit and the work. A Why? on every number.
    /// </summary>
    public static class ShedSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LivestockState st = sim.State.Livestock;
            LivestockDefs defs = LivestockRules.Defs(sim);
            if (st == null || defs == null || !LivestockRules.Unlocked(sim))
            {
                var locked = new SheetVm { Icon = "hut", Title = s.Get("cattleAcre.title"), Subtitle = s.Get("cattleAcre.locked") };
                if (defs != null) locked.Lines.Add(CompostRow(ctx, defs));
                return locked;
            }
            BuildingDef shedDef = sim.Config.Game.Buildings[defs.Shed];
            if (LivestockRules.Shed(sim) == null)
            {
                var plan = new SheetVm { Icon = "cattle_shed", Title = s.Get("cattleAcre.title") };
                if (st.ShedReadyAt > 0)
                {
                    plan.Subtitle = s.Get("shed.building");
                    plan.Lines.Add(new LineVm(s.Get("shed.readyIn"), ctx.Fmt.Duration(System.Math.Max(0, st.ShedReadyAt - ctx.Now)), "shedCost", 0));
                    plan.Lines.Add(CompostRow(ctx, defs));
                    return plan;
                }
                plan.Subtitle = s.Get("cattleAcre.open");
                long cost = MarketRules.Indexed(sim, shedDef.Value);
                plan.Lines.Add(new LineVm(s.Get("shed.cost"), ctx.Fmt.Coins(cost), "shedCost", 0));
                plan.Lines.Add(new LineVm(s.Get("shed.room"), s.Plural("shed.roomValue", shedDef.Animals), "shedCost", 1));
                plan.Lines.Add(new LineVm(s.Get("shed.stores"), s.Get("shed.storesValue", "feed", ctx.Fmt.Kg(shedDef.FeedRoomKg * 1000), "straw", ctx.Fmt.Kg(shedDef.StrawStackKg * 1000)), "shedCost", 1));
                plan.Lines.Add(new LineVm(s.Get("shed.time"), ctx.Fmt.Duration(shedDef.BuildHours * 3600L), "shedCost", 0));
                plan.Lines.Add(CompostRow(ctx, defs));
                plan.Actions.Add(ActionVm.Checked(ctx, "buildShed", s.Get("shed.build"), ctx.Fmt.Coins(cost), ActionKind.Primary, at => new BuildShed(at)));
                return plan;
            }

            var vm = new SheetVm { Icon = "cattle_shed", Title = s.Get("shed.title"), Subtitle = s.Get("shed.stalls", "n", ctx.Fmt.Integer(st.Animals.Count), "of", ctx.Fmt.Integer(shedDef.Animals)) };
            vm.Lines.Add(new LineVm(s.Get("shed.milkToday"), st.LastMilkMl > 0
                ? s.Get("shed.milkTodayValue", "milk", ctx.Fmt.Litres(st.LastMilkMl), "amount", ctx.Fmt.Coins(st.LastMilkAmount))
                : s.Get("dairy.nothingYet"), st.LastMilkMl > 0 ? "milkToday" : null, 0));

            vm.Lines.Add(LineVm.Header(s.Get("shed.animals")));
            foreach (Animal a in st.Animals) vm.Lines.Add(AnimalRow(ctx, a));
            if (st.Animals.Count == 0) vm.Lines.Add(new LineVm(s.Get("shed.noAnimals"), null));
            var buy = new List<ActionVm>();
            for (int k = 0; k < defs.Animals.Count; k++)
            {
                AnimalDef d = defs.Animals[k];
                ActionVm b = ActionVm.Open("buyAnimal:" + d.Id, s.Get("shed.buy", "animal", s.Get(d.NameKey).ToLowerInvariant()), new SheetRequest("BuyAnimal", k),
                    st.Animals.Count >= shedDef.Animals ? s.Get("reject.ShedFull") : ctx.Fmt.Coins(MarketRules.Indexed(sim, d.Price)));
                b.Icon = "animal_" + d.Id;
                buy.Add(b);
            }
            vm.Lines.Add(new LineVm(null, null) { Buttons = buy });

            vm.Lines.Add(LineVm.Header(s.Get("shed.feed")));
            vm.Lines.Add(GreenRow(ctx));
            vm.Lines.Add(PlaceRow(ctx, defs.Straw, "shed.straw", shedDef.StrawStackKg * 1000, st.StrawLow));
            vm.Lines.Add(PlaceRow(ctx, defs.Feed, "shed.feedRoom", shedDef.FeedRoomKg * 1000, st.FeedLow));
            vm.Lines.Add(CompostRow(ctx, defs));
            vm.Lines.Add(new LineVm(s.Get("shed.work"), s.Get("shed.workValue", "hours", ctx.Fmt.Decimal1(LivestockRules.LabourDemand(sim))), "animalWork", 0));
            vm.Lines.Add(LookAfter(ctx));
            return vm;
        }

        /// <summary>The compost pit in the acre's far corner, the farm's from the start (§3.3).</summary>
        private static LineVm CompostRow(ViewContext ctx, LivestockDefs defs)
        {
            Simulation sim = ctx.Sim;
            return new LineVm(ctx.Strings.Get("shed.compost"), ctx.Strings.Get("shed.ofValue", "have", ctx.Fmt.Kg(sim.State.Market.Stock[defs.Compost]),
                "cap", ctx.Fmt.Kg(Store.CapacityGrams(sim, GoodPlace.CompostPit))), "feedStock", defs.Compost) { Icon = "compost" };
        }

        /// <summary>Her row: name, what she's doing and her milk this morning, her health under it; the row opens her card.</summary>
        private static LineVm AnimalRow(ViewContext ctx, Animal a)
        {
            var s = ctx.Strings;
            var line = new LineVm(a.Name, Stage(ctx, a), "animalMilk", a.Id)
            {
                Icon = "animal_" + LivestockRules.Defs(ctx.Sim).Animals[a.Kind].Id, Opens = new SheetRequest("Animal", a.Id),
            };
            string health = s.Get("animal.health", "pct", ctx.Fmt.Percent(a.Health));
            line.Note = a.SickDays > 0 ? health + " · " + s.Get("animal.sick")
                : a.FedScore < LivestockRules.Defs(ctx.Sim).Livestock.HungryBelow ? health + " · " + s.Get("animal.hungry") : health;
            line.NoteTone = a.SickDays > 0 || a.Health < Fixed.Parse("0.9") || a.FedScore < LivestockRules.Defs(ctx.Sim).Livestock.HungryBelow ? Tone.Warn : Tone.Good;
            return line;
        }

        /// <summary>"In milk, day 4 of 13 · 219.5 L this morning" or "Dry, a calf in 2 days".</summary>
        internal static string Stage(ViewContext ctx, Animal a)
        {
            AnimalDef d = LivestockRules.Defs(ctx.Sim).Animals[a.Kind];
            if (a.Stage == AnimalStage.Dry) return ctx.Strings.Plural("animal.dry", System.Math.Max(1, d.DryDays - a.StageDay));
            string day = ctx.Strings.Get("animal.inMilk", "day", ctx.Fmt.Integer(a.StageDay + 1), "of", ctx.Fmt.Integer(d.MilkDays));
            return a.LastMilkMl > 0 ? day + " · " + ctx.Strings.Get("animal.milkToday", "milk", ctx.Fmt.Litres(a.LastMilkMl)) : day;
        }

        /// <summary>The green fodder this morning: the need, and where it came from; buying from neighbours on or off on the row.</summary>
        private static LineVm GreenRow(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LivestockState st = sim.State.Livestock;
            FeedingReport f = st.LastFeeding;
            long needNow = 0;
            foreach (Animal a in st.Animals) needNow += LivestockSystem.NeedOf(sim, a).Green;
            // A first animal hasn't had a morning here yet: what she'll need from 6:00.
            string value = f.GreenNeed == 0 ? needNow > 0 ? s.Get("shed.greenFrom", "need", ctx.Fmt.Kg(needNow)) : s.Get("shed.greenNone")
                : s.Get("shed.greenValue", "need", ctx.Fmt.Kg(f.GreenNeed), "commons", ctx.Fmt.Kg(f.GreenCommons), "own", ctx.Fmt.Kg(f.GreenOwn), "bought", ctx.Fmt.Kg(f.GreenBought));
            bool on = st.BuyGreenFodder;
            var line = new LineVm(s.Get("shed.green"), value, "greenFodder", 0) { Icon = "green_fodder" };
            line.Note = s.Get(on ? "shed.buyingOn" : "shed.buyingOff");
            line.NoteTone = on ? Tone.Plain : Tone.Warn;
            line.Action = ActionVm.Checked(ctx, "buyGreen", s.Get(on ? "shed.stopBuying" : "shed.startBuying"), null, ActionKind.Secondary, at => new SetBuyGreenFodder(at, !on));
            return line;
        }

        /// <summary>The straw stack or the feed room: how much of its room, how many days at today's need, Fill.</summary>
        private static LineVm PlaceRow(ViewContext ctx, int good, string key, long capacity, bool low)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long have = sim.State.Market.Stock[good];
            long days = LivestockRules.DaysLeft(sim, good);
            string value = s.Get("shed.ofValue", "have", ctx.Fmt.Kg(have), "cap", ctx.Fmt.Kg(capacity));
            if (days != long.MaxValue) value += " · " + s.Plural("why.days", days);
            var line = new LineVm(s.Get(key), value, "feedStock", good) { Icon = sim.Config.Game.Market.Goods[good].Id };
            if (low) { line.Note = s.Get("shed.low"); line.NoteTone = Tone.Warn; }
            line.Action = ActionVm.Open("fill:" + good, s.Get("shed.fill"), new SheetRequest("BuyInput", good));
            return line;
        }

        /// <summary>Who keeps the straw and feed stocked: a hand's animals order, or "You", with the way to give the order.</summary>
        private static LineVm LookAfter(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var people = sim.State.People;
            Sim.Core.People.StandingOrder o = people?.OrderFor(Sim.Core.People.OrderTargetKind.Animals, 0);
            if (o != null)
                return new LineVm(s.Get("shed.keeper"), s.Get("shed.keeperHand", "name", PeopleWords.Name(ctx, people.Hand(o.HandId).Name), "days", s.Plural("why.days", o.Days)), "animalWork", 1);
            var line = new LineVm(s.Get("shed.keeper"), s.Get("shed.keeperYou"));
            Sim.Core.People.Hand h = people == null ? null : people.Hands.Find(x => x.Contract == Sim.Core.People.ContractState.Active);
            if (h != null)
            {
                long id = h.Id;
                line.Action = ActionVm.Checked(ctx, "animalsOrder", s.Get("shed.giveOrder", "name", PeopleWords.Name(ctx, h.Name)), null, ActionKind.Secondary,
                    at => new SetStandingOrder(at, id, OrderSpec.LookAfterAnimals(3)));
            }
            return line;
        }
    }

    /// <summary>
    /// The buy card for an animal (spec 012 §7): her price, milk, fat and price a litre, ration and its cost bought or home-grown,
    /// labour, payback; the straw and feed in the shed with Fill; Buy, or Buy with a dairy loan.
    /// </summary>
    public static class AnimalBuySheet
    {
        public static SheetVm Build(ViewContext ctx, int kind)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LivestockDefs defs = LivestockRules.Defs(sim);
            AnimalDef d = defs.Animals[kind];
            string name = s.Get(d.NameKey);
            long price = MarketRules.Indexed(sim, d.Price);
            Economics e = Economics.Of(sim, kind);
            var vm = new SheetVm { Icon = "animal_" + d.Id, Title = s.Get("buyAnimal.title", "animal", name.ToLowerInvariant()), Subtitle = s.Get("buyAnimal.subtitle") };
            long id = kind * 10;
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.price"), ctx.Fmt.Coins(price), "animalBuy", id));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.milk"), s.Get("buyAnimal.milkValue", "milk", ctx.Fmt.Litres(e.MilkMlInMilk)), "animalBuy", id + 1));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.fat"), s.Get("dairy.priceValue", "fat", ctx.Fmt.Decimal1(d.FatPercent),
                "amount", ctx.Fmt.Paise((LivestockRules.MilkPrice(sim, kind) * Fixed.FromInt(100)).RoundToLong())), "milkPrice", kind));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.ration"), s.Get("buyAnimal.rationValue", "green", ctx.Fmt.Kg(e.Green), "straw", ctx.Fmt.Kg(e.Straw), "feed", ctx.Fmt.Kg(e.Feed)), "animalBuy", id + 2));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.feedCost"), s.Get("buyAnimal.feedCostValue", "bought", ctx.Fmt.Coins(e.FeedBought), "home", ctx.Fmt.Coins(e.FeedHome)), "animalBuy", id + 3));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.labour"), s.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(d.LabourHoursPerDay)), "animalBuy", id + 4));
            vm.Lines.Add(new LineVm(s.Get("buyAnimal.payback"), Payback(ctx, price, e), "animalBuy", id + 5));
            vm.Lines.Add(LineVm.Header(s.Get("buyAnimal.inShed")));
            BuildingDef shed = sim.Config.Game.Buildings[defs.Shed];
            foreach (int good in new[] { defs.Straw, defs.Feed })
            {
                long have = sim.State.Market.Stock[good];
                vm.Lines.Add(new LineVm(s.Get(good == defs.Straw ? "shed.straw" : "shed.feedRoom"),
                    s.Get("shed.ofValue", "have", ctx.Fmt.Kg(have), "cap", ctx.Fmt.Kg(good == defs.Straw ? shed.StrawStackKg * 1000 : shed.FeedRoomKg * 1000)), "feedStock", good)
                {
                    Icon = sim.Config.Game.Market.Goods[good].Id,
                    Action = ActionVm.Open("fill:" + good, s.Get("shed.fill"), new SheetRequest("BuyInput", good)),
                });
            }
            string kindId = d.Id;
            vm.Actions.Add(ActionVm.Checked(ctx, "buy", s.Get("buyAnimal.buy"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new BuyAnimal(at, kindId)));
            int bank = LandLoanSheet.Bank(sim);
            if (bank >= 0)
            {
                long loan = Loan(sim, bank, price);
                bool can = loan >= sim.Config.Game.Credit.Lenders[bank].MinAmount && LivestockRules.Shed(sim) != null
                    && sim.State.Livestock.Animals.Count < LivestockRules.Stalls(sim);
                vm.Actions.Add(new ActionVm("buyLoan", s.Get("buyAnimal.loan"), can ? s.Get("land.buyLoanDetail", "down", ctx.Fmt.Coins(price - loan)) : null,
                    ActionKind.Open, can, can ? null : s.Get("buyAnimal.noLoan"), null, new SheetRequest("AnimalLoan", kind)));
            }
            return vm;
        }

        /// <summary>"About 40 days with bought feed · 25 days on your own fodder" (whole game days, over a full cycle with her dry days).</summary>
        private static string Payback(ViewContext ctx, long price, Economics e)
        {
            long bought = e.MilkValue - e.FeedBought, home = e.MilkValue - e.FeedHome;
            if (home <= 0) return ctx.Strings.Get("buyAnimal.never");
            string h = ctx.Strings.Plural("why.days", (price + home - 1) / home);
            if (bought <= 0) return ctx.Strings.Get("buyAnimal.paybackHomeOnly", "home", h);
            return ctx.Strings.Get("buyAnimal.paybackValue", "bought", ctx.Strings.Plural("why.days", (price + bought - 1) / bought), "home", h);
        }

        /// <summary>The most the bank lends towards her (its limit with her as collateral at livestockLtv), in whole thousands, at most her price.</summary>
        internal static long Loan(Simulation sim, int bank, long price)
        {
            long limit = CreditRules.Limit(sim, bank, 0, price).Limit;
            return System.Math.Min(price, limit) / (1000 * Money.Coin) * (1000 * Money.Coin);
        }
    }

    /// <summary>
    /// One animal's money over a full cycle, a game day on average (her dry days included): milk at the co-op's price, feed bought
    /// (green from neighbours, straw and feed at today's input prices) or home-grown (green and straw your own: only the feed bought).
    /// </summary>
    public readonly struct Economics
    {
        public long MilkMlInMilk { get; }
        public long Green { get; }
        public long Straw { get; }
        public long Feed { get; }
        public long MilkValue { get; }
        public long FeedBought { get; }
        public long FeedHome { get; }

        private Economics(long milkInMilk, long green, long straw, long feed, long milkValue, long bought, long home)
        {
            MilkMlInMilk = milkInMilk;
            Green = green;
            Straw = straw;
            Feed = feed;
            MilkValue = milkValue;
            FeedBought = bought;
            FeedHome = home;
        }

        public static Economics Of(Simulation sim, int kind)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            AnimalDef d = defs.Animals[kind];
            int milkDays = d.MilkDays, cycle = d.MilkDays + d.DryDays;
            Fixed milkL = LivestockRules.PerGameDay(sim, d.MilkPerDay); // the lactation's mean (§3.2)
            long milkMl = (milkL * Fixed.FromInt(1000)).RoundToLong();
            long green = Grams(LivestockRules.PerGameDay(sim, d.Green)), straw = Grams(LivestockRules.PerGameDay(sim, d.Straw));
            long feed = Grams(LivestockRules.PerGameDay(sim, d.Feed));
            long feedAvg = Grams(LivestockRules.PerGameDay(sim, d.Feed * (Fixed.FromInt(milkDays) + d.DryFeedFactor * Fixed.FromInt(d.DryDays)) / Fixed.FromInt(cycle)));
            long milkValue = Money.Value(milkMl * milkDays / cycle, LivestockRules.MilkPrice(sim, kind), 1000);
            long greenCost = Money.Value(green, defs.Livestock.GreenFodderBuyPrice * sim.State.Market.PriceIndex, 1000);
            long strawCost = Money.Value(straw, MarketRules.InputPrice(sim, defs.Straw), sim.Config.Game.Market.Goods[defs.Straw].UnitSize);
            long feedCost = Money.Value(feedAvg, MarketRules.InputPrice(sim, defs.Feed), sim.Config.Game.Market.Goods[defs.Feed].UnitSize);
            return new Economics(milkMl, green, straw, feed, milkValue, greenCost + strawCost + feedCost, feedCost);
        }

        private static long Grams(Fixed kg) { return (kg * Fixed.FromInt(1000)).RoundToLong(); }
    }

    /// <summary>Buying an animal with a dairy loan (spec 012 §3.12, as the land loan, ADR 0028): one row per term.</summary>
    public static class AnimalLoanSheet
    {
        public static SheetVm Build(ViewContext ctx, int kind)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            AnimalDef d = LivestockRules.Defs(sim).Animals[kind];
            long price = MarketRules.Indexed(sim, d.Price);
            int bank = LandLoanSheet.Bank(sim);
            LenderDef l = sim.Config.Game.Credit.Lenders[bank];
            long loan = AnimalBuySheet.Loan(sim, bank, price);
            var vm = new SheetVm
            {
                Icon = "animal_" + d.Id, Title = s.Get("animalLoan.title"),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            string kindId = d.Id;
            foreach (int term in BorrowSheet.Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = BorrowSheet.Plan(sim, bank, loan, term);
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]),
                    "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]), "interest", ctx.Fmt.Coins(plan.TotalInterest)), "borrow", bank * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, "buyLoan:" + term, s.Get("buyAnimal.buy"), ctx.Fmt.Coins(price - loan), ActionKind.Primary,
                        at => new BuyAnimal(at, kindId, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }
    }

    /// <summary>One animal's card (spec 012 §7): her milk, fed score, health, vaccination, cost, earnings, sell price, and Sell at the fair.</summary>
    public static class AnimalSheet
    {
        public static SheetVm Build(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return new SheetVm { Icon = "hut", Title = s.Get("reject.UnknownAnimal") };
            AnimalDef d = LivestockRules.Defs(sim).Animals[a.Kind];
            var vm = new SheetVm { Icon = "animal_" + d.Id, Title = a.Name, Subtitle = s.Get(d.NameKey) + " · " + ShedSheet.Stage(ctx, a) };
            vm.Lines.Add(new LineVm(s.Get("animal.milk"), a.Stage == AnimalStage.Dry ? s.Get("animal.milkDry") : ctx.Fmt.Litres(a.LastMilkMl), "animalMilk", a.Id));
            vm.Lines.Add(new LineVm(s.Get("animal.fed"), ctx.Fmt.Percent(a.FedScore), "animalFed", a.Id));
            vm.Lines.Add(new LineVm(s.Get("animal.healthRow"), ctx.Fmt.Percent(a.Health), "animalHealth", a.Id));
            vm.Lines.Add(new LineVm(s.Get("animal.vaccinated"), s.Get(a.VaccinatedPeriod == sim.Period ? "animal.vaccinatedYes" : "animal.vaccinatedNo")));
            vm.Lines.Add(new LineVm(s.Get("animal.cost"), ctx.Fmt.Coins(a.Cost), "animalMoney", a.Id));
            vm.Lines.Add(new LineVm(s.Get("animal.earned"), ctx.Fmt.Coins(a.Earned), "animalMoney", a.Id));
            long sell = LivestockRules.SellPrice(sim, a);
            vm.Lines.Add(new LineVm(s.Get("animal.sellsFor"), ctx.Fmt.Coins(sell), "animalSell", a.Id));
            ActionVm sellAction = ActionVm.Checked(ctx, "sellAnimal", s.Get("animal.sell"), ctx.Fmt.Coins(sell), ActionKind.Danger, at => new SellAnimal(at, id));
            sellAction.Confirm = new ConfirmVm(s.Get("animal.sellQ", "name", a.Name, "amount", ctx.Fmt.Coins(sell)), s.Get("animal.sell"), s.Get("animal.keep"),
                at => new Command[] { new SellAnimal(at, id) }, noSends: false) { YesIsDanger = true };
            vm.Actions.Add(sellAction);
            return vm;
        }
    }
}

namespace Sim.View.Views
{
    /// <summary>Green fodder on screen (spec 012 §3.8b): a field's cut a day, the sow sheet's fodder card, the fodder field's rows.</summary>
    public static class FodderView
    {
        /// <summary>
        /// About how much a fodder field cuts a game day now, grams: the season's yield an acre over the season's days × the soil, the
        /// water and weather now, Care (capped at 1) and the fertiliser.
        /// </summary>
        public static long CutPerDay(Simulation sim, Sim.Core.Farm.Crop c)
        {
            CropDef def = sim.Config.Game.Farm.Crops[c.Def];
            Fixed care = sim.State.Farm.CareRatio > Fixed.One ? Fixed.One : sim.State.Farm.CareRatio;
            Fixed kg = def.SeasonYieldPerAcre.MulRatio(ClockDef.SecondsPerDay, sim.Clock.SecondsPerSeason) * c.SoilFactor * c.WaterRate * c.WeatherRate * care
                * sim.Config.Game.Farm.Fertilisers[c.Fertiliser].YieldFactor;
            return (kg * Fixed.FromInt(1000)).RoundToLong();
        }

        /// <summary>The same for a field not sown yet (the sow sheet): its soil, the crop's water factor at today's moisture, fertiliser.</summary>
        public static long CutPerDay(Simulation sim, Sim.Core.Land.Tile tile, CropDef def, FertiliserDef fert)
        {
            Fixed kg = def.SeasonYieldPerAcre.MulRatio(ClockDef.SecondsPerDay, sim.Clock.SecondsPerSeason) * Sim.Core.Land.LandRules.SoilFactor(sim, tile)
                * Sim.Core.Farm.FarmRules.WaterFactor(sim.Config.Game.Farm.Farm, def, tile.Moisture) * fert.YieldFactor;
            return (kg * Fixed.FromInt(1000)).RoundToLong();
        }

        /// <summary>
        /// The sow sheet's fodder card (spec 012 §3.8b): its cut a day and how many buffalo that feeds; what it saves your animals in
        /// bought fodder a day, or with no animals what neighbours pay for it standing.
        /// </summary>
        internal static void Card(ViewContext ctx, CropRowVm row, Sim.Core.Land.Tile tile, CropDef c, FertiliserDef fert)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LivestockDefs defs = LivestockRules.Defs(sim);
            LivestockDef l = defs.Livestock;
            long cut = CutPerDay(sim, tile, c, fert);
            long buffalo = (LivestockRules.PerGameDay(sim, defs.Animals[0].Green) * Fixed.FromInt(1000)).RoundToLong();
            Fixed index = sim.State.Market.PriceIndex;
            row.HarvestGrams = cut;
            row.HarvestText = s.Get("fodder.cut", "kg", ctx.Fmt.Kg(cut));
            row.GetText = s.Get("fodder.feeds", "n", ctx.Fmt.Integer(buffalo <= 0 ? 0 : cut / buffalo));
            LivestockState st = sim.State.Livestock;
            long need = 0;
            foreach (Animal a in st.Animals) need += Sim.Core.Livestock.LivestockSystem.NeedOf(sim, a).Green;
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            need -= l.CommonsShare[(int)season].MulRatio(need, 1).RoundToLong();
            long used = System.Math.Min(cut, need);
            long saves = Money.Value(used, l.GreenFodderBuyPrice * index, 1000) + Money.Value(cut - used, l.GreenFodderSellPrice * index, 1000);
            row.Get = saves;
            row.Profit = saves;
            row.ProfitText = st.Animals.Count == 0 ? s.Get("fodder.sellsStanding", "amount", ctx.Fmt.Coins(saves))
                : s.Plural("fodder.saves", st.Animals.Count, "amount", ctx.Fmt.Coins(saves));
            row.TimeText = s.Get("fodder.cutIn", "seasons", FieldSheets.SeasonsText(ctx, c.CutSeasons));
        }
    }
}
