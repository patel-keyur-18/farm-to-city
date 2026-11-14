using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>You and your family (spec 003 §7, ADR 0043 §6): today, food, hours, day labour, eating from the store, medicine.</summary>
    public static class FounderSheet
    {
        public static readonly int[] Shares = { 0, 25, 50, 75 };

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            FarmDefs farm = sim.Config.Game.Farm;
            FounderState f = sim.State.Farm.Founder;
            var vm = new SheetVm { Icon = "person_founder", Title = s.Get("founder.title"), Subtitle = s.Get("founder.subtitle") };

            vm.Lines.Add(LineVm.Header(s.Get("founder.today")));
            vm.Lines.Add(new LineVm(s.Get("founder.work"), f.SickToday ? s.Get("founder.sick") : s.Get("founder.workValue", "n", ctx.Fmt.Decimal1(FounderRules.LabourRate(sim))), "labour"));
            vm.Lines.Add(new LineVm(s.Get("founder.health"), ctx.Rating(f.Health), "health"));
            vm.Lines.Add(new LineVm(s.Get("founder.tiredness"), ctx.Rating(Fixed.One - f.Fatigue), "fatigue"));

            vm.Lines.Add(LineVm.Header(s.Get("founder.food")));
            bool winter = Sim.Core.Time.Calendar.SeasonAt(ctx.Now, sim.Clock) == Season.Winter;
            for (int i = 0; i < farm.DietPlans.Count; i++)
            {
                DietPlanDef p = farm.DietPlans[i];
                Fixed n = p.Nutrition;
                string work = n == Fixed.One ? s.Get("founder.fullWork") : s.Get("founder.work.effect", "effect", WhyView.Effect(ctx, n));
                string id = p.Id;
                bool current = f.DietPlan == i;
                if (p.Id == DietPlanDef.SubsistenceId)
                {
                    // Never chosen by the player (ADR 0034): the household falls back to it when the season's food can't be paid.
                    string note = s.Get(current ? "founder.subsistenceNow" : "founder.subsistence", "work", work);
                    if (winter) note += "\n" + s.Get("founder.noWarmClothes");
                    vm.Lines.Add(new LineVm(s.Get(p.NameKey), note, "diet", i));
                    continue;
                }
                string value = s.Get("founder.planValue", "cost", ctx.Fmt.Coins(MarketRules.Indexed(sim, p.CostPerSeason)), "work", work);
                vm.Lines.Add(new LineVm(s.Get(p.NameKey), value, "diet", i)
                {
                    Action = current ? Chosen(ctx, "plan:" + id) : ActionVm.Checked(ctx, "plan:" + id, s.Get("action.choose"), null, ActionKind.Primary, at => new SetDietPlan(at, id)),
                });
            }

            vm.Lines.Add(LineVm.Header(s.Get("founder.hours")));
            for (int i = 0; i < farm.WorkHours.Count; i++)
            {
                WorkHoursDef w = farm.WorkHours[i];
                string id = w.Id;
                string tired = w.FatigueChange > Fixed.Zero ? s.Get("founder.tires") : w.FatigueChange < Fixed.Zero ? s.Get("founder.rests") : s.Get("founder.steady");
                vm.Lines.Add(new LineVm(s.Get("hours." + w.Id), s.Get("founder.hoursValue", "hours", s.Get("why.hoursADay", "n", ctx.Fmt.Integer(w.Hours.RoundToLong())), "tired", tired), "hours", i)
                {
                    Action = f.WorkHours == i ? Chosen(ctx, "hours:" + id) : ActionVm.Checked(ctx, "hours:" + id, s.Get("action.choose"), null, ActionKind.Primary, at => new SetWorkHours(at, id)),
                });
            }

            vm.Lines.Add(LineVm.Header(s.Get("founder.dayLabour")));
            long wage = MarketRules.Indexed(sim, farm.Founder.DayLabourWagePerHour);
            foreach (int share in Shares)
            {
                int sh = share;
                Fixed hours = FounderRules.LabourRate(sim).MulRatio(share, 100);
                string value = share == 0 ? s.Get("founder.shareNone")
                    : s.Get("founder.shareValue", "pay", ctx.Fmt.Coins(Sim.Core.Finance.Money.MulRate(wage, hours)), "hours", s.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(hours)));
                vm.Lines.Add(new LineVm(s.Get("founder.share." + share), value, share == 0 ? null : "dayLabour", share)
                {
                    Action = f.DayLabourShare == share ? Chosen(ctx, "share:" + share) : ActionVm.Checked(ctx, "share:" + share, s.Get("action.choose"), null, ActionKind.Primary, at => new SetLabourSplit(at, sh)),
                });
            }

            vm.Lines.Add(LineVm.Header(s.Get("founder.more")));
            bool on = f.EatFromStore;
            vm.Lines.Add(new LineVm(s.Get("founder.eatFromStore"), s.Get(on ? "founder.eatOn" : "founder.eatOff"))
            {
                Action = new ActionVm("eat", s.Get(on ? "settings.on" : "settings.off"), null, on ? ActionKind.Primary : ActionKind.Danger, true, null, at => new SetEatFromStore(at, !on)),
            });
            FounderDef d = farm.Founder;
            vm.Lines.Add(new LineVm(s.Get("founder.medicine"), s.Get("founder.medicineValue", "gain", ctx.Fmt.Percent(d.MedicineHealthGain)), "medicine")
            {
                Action = ActionVm.Checked(ctx, "medicine", s.Get("founder.buyMedicine"), ctx.Fmt.Coins(MarketRules.Indexed(sim, d.MedicineCost)), ActionKind.Primary, at => new BuyMedicine(at)),
            });
            vm.Actions.Add(ActionVm.Open("store", s.Get("action.openStore"), new SheetRequest("Store", 0)));
            return vm;
        }

        /// <summary>The current choice: a disabled "Chosen" button with no reason.</summary>
        internal static ActionVm Chosen(ViewContext ctx, string id) { return new ActionVm(id, ctx.Strings.Get("action.chosen"), null, ActionKind.Secondary, false, null, null); }
    }

    /// <summary>A good's sheet and offering it at the market (spec 004 §7, ADR 0043 §7).</summary>
    public static class GoodSheet
    {
        public static SheetVm Build(ViewContext ctx, int g)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MarketState m = sim.State.Market;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            string id = good.Id;
            long free = m.InStore(g) - m.OnOffer(g), offered = m.OnOffer(g);
            long room = MarketRules.TraderCapacityLeft(sim, g), sell = System.Math.Min(free, room);
            Fixed floor = MarketRules.Floor(sim, g);
            var vm = new SheetVm { Icon = good.Id, Title = s.Get(good.NameKey) };
            vm.Lines.Add(new LineVm(s.Get("good.inStore"), ctx.Fmt.Kg(m.InStore(g)), "store"));
            vm.Lines.Add(new LineVm(s.Get("good.onOffer"), ctx.Fmt.Kg(offered), "marketPrice", g));
            vm.Lines.Add(new LineVm(s.Get("good.spoils"), ctx.Fmt.Kg((Fixed.FromInt(free) * good.SpoilagePerDay).RoundToLong()), "spoilage", g));
            vm.Lines.Add(new LineVm(s.Get("good.market"), ctx.Fmt.PricePerKg(m.MarketPrice(g)), "marketPrice", g));
            vm.Lines.Add(new LineVm(s.Get("good.trader"), sim.Config.Game.Market.Truck != null && good.Kind == GoodKind.Produce
                ? OrdersSheet.TodayVsYesterday(ctx, g) : ctx.Fmt.PricePerKg(floor), "traderPrice", g));
            vm.Lines.Add(new LineVm(s.Get("good.traderRoom"), ctx.Fmt.Kg(room), "traderRoom", g));
            if (MarketRules.StallClosed(sim)) vm.Lines.Add(new LineVm(s.Get("store.stallClosed"), null));
            vm.Actions.Add(new ActionVm("offer", s.Get("good.offer"), null, ActionKind.Open, free > 0, free > 0 ? null : s.Get("good.nothingFree"), null, new SheetRequest("Offer", g)));
            if (offered > 0)
                vm.Actions.Add(ActionVm.Checked(ctx, "withdraw", s.Get("action.withdrawRow"), ctx.Fmt.Kg(offered), ActionKind.Secondary, at => new Withdraw(at, id, offered)));
            vm.Actions.Add(ActionVm.Checked(ctx, "sell", s.Get("good.sellTrader"), sell > 0 ? s.Get("store.sellDetail", "qty", ctx.Fmt.Kg(sell), "amount", ctx.Fmt.Coins(MarketRules.Value(sim, g, sell, floor))) : null,
                ActionKind.Primary, at => new SellToTrader(at, id, sell > 0 ? sell : System.Math.Max(1, free))));
            return vm;
        }

        /// <summary>Offer: first how much (all, a half, a quarter of what's free), then the lowest price.</summary>
        public static SheetVm Offer(ViewContext ctx, int g, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MarketState m = sim.State.Market;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            string id = good.Id;
            long free = m.InStore(g) - m.OnOffer(g);
            var vm = new SheetVm { Icon = good.Id, Title = s.Get("offer.title", "good", s.Get(good.NameKey)) };
            if (string.IsNullOrEmpty(arg))
            {
                vm.Subtitle = s.Get("offer.howMuch");
                foreach (int part in new[] { 4, 2, 1 })
                {
                    long q = free * part / 4;
                    if (q <= 0) continue;
                    vm.Lines.Add(new LineVm(s.Get("offer.part." + part), ctx.Fmt.Kg(q), "store")
                    { Action = ActionVm.Open("qty:" + part, s.Get("borrow.choose"), new SheetRequest("Offer", g, q.ToString(CultureInfo.InvariantCulture))) });
                }
                return vm;
            }
            long qty = long.Parse(arg, CultureInfo.InvariantCulture);
            vm.Subtitle = s.Get("offer.lowest", "qty", ctx.Fmt.Kg(qty));
            Fixed floor = MarketRules.Floor(sim, g), today = m.MarketPrice(g);
            vm.Lines.Add(new LineVm(s.Get("offer.any"), s.Get("offer.anyNote"))
            { Action = ActionVm.Checked(ctx, "offerAny", s.Get("offer.button"), null, ActionKind.Primary, at => new Offer(at, id, qty)) });
            vm.Lines.Add(new LineVm(s.Get("offer.trader"), ctx.Fmt.PricePerKg(floor), "traderPrice", g)
            { Action = ActionVm.Checked(ctx, "offerTrader", s.Get("offer.button"), null, ActionKind.Primary, at => new Offer(at, id, qty, floor)) });
            vm.Lines.Add(new LineVm(s.Get("offer.today"), ctx.Fmt.PricePerKg(today), "marketPrice", g)
            { Action = ActionVm.Checked(ctx, "offerToday", s.Get("offer.button"), null, ActionKind.Primary, at => new Offer(at, id, qty, today)) });
            return vm;
        }
    }

    /// <summary>Buying inputs ahead (spec 004 §3, ADR 0031, ADR 0043 §7): one row per input, amounts in acres or sowings.</summary>
    public static class BuyInputsSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var goods = sim.Config.Game.Market.Goods;
            var vm = new SheetVm { Icon = "sack", Title = s.Get("inputs.title"), Subtitle = s.Get("inputs.subtitle") };
            for (int g = 0; g < goods.Count; g++)
            {
                if (goods[g].Kind == GoodKind.Produce || goods[g].Unit == GoodUnit.Each) continue;
                // Straw and cattle feed need the shed's stack and feed room (spec 012 §3.3).
                if ((goods[g].Place == GoodPlace.StrawStack || goods[g].Place == GoodPlace.FeedRoom) && Sim.Core.Livestock.LivestockRules.Shed(sim) == null) continue;
                int seedOf = g;
                if (!Sim.Core.Livestock.LivestockRules.Unlocked(sim) && FodderSeed(sim, seedOf)) continue; // fodder seed comes with cattle
                int good = g;
                vm.Lines.Add(new LineVm(s.Get(goods[g].NameKey), s.Get("inputs.priceValue", "price", Price(ctx, g), "cheapest", Cheapest(ctx, g)), "inputPrice", g)
                {
                    Icon = goods[g].Id, Action = ActionVm.Open("input:" + goods[g].Id, s.Get("inputs.buy"), new SheetRequest("BuyInput", good)),
                    Note = goods[g].Place == GoodPlace.Store ? null : s.Get("inputs.goesTo." + goods[g].Place),
                });
            }
            return vm;
        }

        private static bool FodderSeed(Simulation sim, int g)
        {
            foreach (CropDef c in sim.Config.Game.Farm.Crops) if (c.Fodder && c.SeedGood == g) return true;
            return false;
        }

        private static string Price(ViewContext ctx, int g)
        {
            GoodDef good = ctx.Sim.Config.Game.Market.Goods[g];
            Fixed p = MarketRules.InputPrice(ctx.Sim, g);
            return good.Unit == GoodUnit.Litre ? ctx.Strings.Get("unit.perLitre", "amount", ctx.Fmt.PricePerKg(p).Replace(" /kg", "")) : ctx.Fmt.PricePerKg(p);
        }

        /// <summary>The season this input is cheapest in, or "the same all year".</summary>
        private static string Cheapest(ViewContext ctx, int g)
        {
            var region = ctx.Sim.Config.Game.Market.Region;
            int best = 0;
            bool same = true;
            for (int season = 1; season < 4; season++)
            {
                if (region.SeasonalFactor(g, season) != region.SeasonalFactor(g, 0)) same = false;
                if (region.SeasonalFactor(g, season) < region.SeasonalFactor(g, best)) best = season;
            }
            if (same || ctx.Sim.Config.Game.Market.Goods[g].PriceControlled) return ctx.Strings.Get("inputs.sameAllYear");
            return ctx.Strings.Get("inputs.cheapest", "season", ctx.Strings.Get("season." + ((Season)best).ToString().ToLowerInvariant()));
        }

        /// <summary>The amounts offered for one input: seed to sow 1, 2 or 6 acres once; fertiliser and spray for a season on 1, 2 or 6 acres;
        /// garden seed for 5, 10 or 20 sowings of a bed.</summary>
        public static List<KeyValuePair<string, long>> Amounts(ViewContext ctx, int g)
        {
            Simulation sim = ctx.Sim;
            FarmDefs farm = sim.Config.Game.Farm;
            var list = new List<KeyValuePair<string, long>>();
            GoodDef gd = sim.Config.Game.Market.Goods[g];
            if (gd.Place == GoodPlace.StrawStack || gd.Place == GoodPlace.FeedRoom)
            {
                // The shed's feeds (spec 012 §7): fill the place, or enough for 2 or 4 days at today's need.
                long room = Store.Fits(sim, g);
                list.Add(new KeyValuePair<string, long>(ctx.Strings.Get("inputs.fillUp"), room));
                long need = 0;
                foreach (Sim.Core.Livestock.Animal a in sim.State.Livestock.Animals)
                {
                    var n = Sim.Core.Livestock.LivestockSystem.NeedOf(sim, a);
                    need += gd.Place == GoodPlace.StrawStack ? n.Straw : n.Feed;
                }
                if (need > 0)
                    foreach (int days in new[] { 2, 4 })
                    {
                        long q = System.Math.Min(room, need * days);
                        if (q > 0 && q < room) list.Add(new KeyValuePair<string, long>(ctx.Strings.Plural("inputs.forDays", days), q));
                    }
                return list;
            }
            foreach (CropDef c in farm.Crops)
                if (c.SeedGood == g)
                {
                    long one = FarmRules.SeedGrams(sim.Clock, c); // a fodder acre is sown once (spec 012 §3.8b)
                    foreach (int acres in new[] { 1, 2, 6 }) list.Add(new KeyValuePair<string, long>(ctx.Strings.Plural("inputs.sowAcres", acres), one * acres));
                    return list;
                }
            foreach (GardenCropDef c in farm.GardenCrops)
                if (c.SeedGood == g)
                {
                    foreach (int n in new[] { 5, 10, 20 }) list.Add(new KeyValuePair<string, long>(ctx.Strings.Plural("inputs.bedSowings", n), c.SeedGramsPerSowing * n));
                    return list;
                }
            Fixed perAcre = Fixed.Zero;
            foreach (FertiliserDef f in farm.Fertilisers) if (f.Good == g) perAcre = f.KgPerAcrePerSeason * Fixed.FromInt(1000);
            if (farm.Pests.SprayGood == g) perAcre = farm.Pests.SprayPerAcrePerSeason;
            foreach (int acres in new[] { 1, 2, 6 })
                list.Add(new KeyValuePair<string, long>(ctx.Strings.Plural("inputs.seasonAcres", acres), (perAcre * Fixed.FromInt(acres)).RoundToLong()));
            return list;
        }

        public static SheetVm One(ViewContext ctx, int g)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            string id = good.Id;
            var vm = new SheetVm { Icon = good.Id, Title = s.Get(good.NameKey), Subtitle = s.Get(good.Place == GoodPlace.Store ? "inputs.inStore" : "inputs.inPlace." + good.Place, "qty", ctx.Fmt.Quantity(good, sim.State.Market.InStore(g))) };
            foreach (KeyValuePair<string, long> a in Amounts(ctx, g))
            {
                long q = a.Value;
                if (q <= 0) continue;
                long cost = MarketRules.Value(sim, g, q, MarketRules.InputPrice(sim, g));
                vm.Lines.Add(new LineVm(a.Key, ctx.Fmt.Quantity(good, q), "inputPrice", g)
                { Action = ActionVm.Checked(ctx, "buy:" + q, s.Get("inputs.buy"), ctx.Fmt.Coins(cost), ActionKind.Primary, at => new Buy(at, id, q)) });
            }
            return vm;
        }
    }

    /// <summary>
    /// The mandi trip (spec 004 §3.7, ADR 0037, ADR 0043 §7, US-252 / ADR 0107 §14): a slider for each crop in the store, best gain over the
    /// trader first, each stopping at the room the other crops leave in the trolley; Fill loads the best-paying crops; the sum written out
    /// against the trader. The loads travel in the request's Arg ("goodIndex:grams;…"), so a move re-draws the sheet in place.
    /// </summary>
    public static class MandiSheet
    {
        /// <summary>The load per good from the sheet's Arg ("goodIndex:grams;…").</summary>
        public static Dictionary<int, long> Parse(string arg)
        {
            var d = new Dictionary<int, long>(); // looked up by key only, never iterated for order
            if (string.IsNullOrEmpty(arg)) return d;
            foreach (string part in arg.Split(';'))
            {
                string[] kv = part.Split(':');
                d[int.Parse(kv[0], CultureInfo.InvariantCulture)] = long.Parse(kv[1], CultureInfo.InvariantCulture);
            }
            return d;
        }

        public static string With(Simulation sim, Dictionary<int, long> loads, int g, long grams)
        {
            var parts = new List<string>();
            for (int i = 0; i < sim.Config.Game.Market.Goods.Count; i++)
            {
                long q = i == g ? grams : loads.TryGetValue(i, out long v) ? v : 0;
                if (q > 0) parts.Add(i.ToString(CultureInfo.InvariantCulture) + ":" + q.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(";", parts);
        }

        /// <summary>
        /// "Mandi trip pays you about 4,310 coins at 15:00" (or "… tomorrow"), with its Why? (what the cart carries); shown in the Store
        /// under the supermarket's line only while a cart is away (owner, 2026-11-12, ADR 0104).
        /// </summary>
        internal static LineVm TripLine(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MandiTrip trip = sim.State.Market.Trip;
            long day = Sim.Core.Definitions.ClockDef.SecondsPerDay;
            bool today = trip.ReturnAt / day == sim.State.Clock.SimTime / day;
            string time = Text.Formatter.Clock(Sim.Core.Time.Calendar.From(trip.ReturnAt, sim.Clock));
            string value = s.Get(today ? "mandi.paysToday" : "mandi.paysTomorrow", "amount", ctx.Fmt.Coins(MarketRules.MandiTripPays(sim)), "time", time);
            return new LineVm(s.Get("mandi.pays"), value, "mandi", 5); // one line like the supermarket's; what the cart carries is in its Why?
        }

        /// <summary>What the mandi pays a kg after its commission, less what the trader pays: the order of the rows and of Fill.</summary>
        public static Fixed Gain(Simulation sim, int g)
        {
            Fixed mandi = MarketRules.RegionalPrice(sim, g) * (Fixed.One - sim.Config.Game.Market.Mandi.CommissionRate);
            return mandi - MarketRules.Floor(sim, g);
        }

        /// <summary>The most of a good that fits <paramref name="roomGrams"/> of the trolley's weight.</summary>
        private static long FitsWeight(GoodDef good, long roomGrams)
        {
            long perThousand = System.Math.Max(1, Store.WeightGrams(good, 1000));
            return System.Math.Max(0, roomGrams * 1000 / perThousand);
        }

        private sealed class Row
        {
            public int G;
            public long Free, Load;
            public Fixed Gain;
        }

        public static SheetVm Build(ViewContext ctx, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;
            MandiDef md = defs.Mandi;
            Dictionary<int, long> given = Parse(arg);
            var vm = new SheetVm { Icon = "cart", Title = s.Get("mandi.title"), Subtitle = s.Get("mandi.subtitle", "kg", ctx.Fmt.Kg(md.CartCapacityGrams), "hours", ctx.Fmt.Integer(md.TripHours)) };

            // The crops in the store, best gain first (ties by good), each load kept within what's free and the room the earlier rows leave.
            var rows = new List<Row>();
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                if (defs.Goods[g].Kind != GoodKind.Produce) continue;
                long free = m.InStore(g) - m.OnOffer(g);
                if (free > 0) rows.Add(new Row { G = g, Free = free, Gain = Gain(sim, g) });
            }
            rows = rows.OrderByDescending(r => r.Gain.Raw).ThenBy(r => r.G).ToList();
            long weight = 0;
            foreach (Row r in rows)
            {
                given.TryGetValue(r.G, out long want);
                long room = md.CartCapacityGrams - weight;
                r.Load = System.Math.Max(0, System.Math.Min(System.Math.Min(want, r.Free), FitsWeight(defs.Goods[r.G], room)));
                weight += Store.WeightGrams(defs.Goods[r.G], r.Load);
            }
            var loads = new Dictionary<int, long>();
            foreach (Row r in rows) if (r.Load > 0) loads[r.G] = r.Load;

            // The trolley, and Fill (what pays best, until the trolley is full; a crop the trader pays as much for stays out).
            vm.Lines.Add(new LineVm(s.Get("mandi.trolley"), s.Get("mandi.trolleyValue", "load", ctx.Fmt.Kg(weight), "cap", ctx.Fmt.Kg(md.CartCapacityGrams), "room", ctx.Fmt.Kg(md.CartCapacityGrams - weight)), "mandi", 2)
            { Meter = (int)(weight * 100 / md.CartCapacityGrams) });
            var fill = new Dictionary<int, long>();
            long fillWeight = 0;
            foreach (Row r in rows)
            {
                if (r.Gain.Raw <= 0) continue;
                long q = System.Math.Min(r.Free, FitsWeight(defs.Goods[r.G], md.CartCapacityGrams - fillWeight));
                if (q <= 0) continue;
                fill[r.G] = q;
                fillWeight += Store.WeightGrams(defs.Goods[r.G], q);
            }
            string fillArg = With(sim, fill, -1, 0);
            string label = s.Get("mandi.fill");
            ActionVm fillAction = fill.Count == 0
                ? new ActionVm("replace:fill", label, null, ActionKind.Secondary, false, s.Get("mandi.fillNone"), null)
                : new ActionVm("replace:fill", label, null, ActionKind.Secondary, true, null, null, new SheetRequest("Mandi", 0, fillArg));
            vm.Lines.Add(new LineVm(s.Get("mandi.fillNote"), null) { Action = fillAction });

            long gross = 0, trader = 0;
            var commands = new List<MandiLoad>();
            var sums = new List<LineVm>();
            foreach (Row r in rows)
            {
                int g = r.G;
                GoodDef good = defs.Goods[g];
                Fixed mandiPrice = MarketRules.RegionalPrice(sim, g), floor = MarketRules.Floor(sim, g);
                long roomLeft = md.CartCapacityGrams - (weight - Store.WeightGrams(good, r.Load)); // the cart's room if this crop were the only one moved
                long max = System.Math.Min(r.Free, FitsWeight(good, roomLeft));
                string caption = r.Gain.Raw > 0
                    ? s.Get("mandi.rowCaption", "mandi", ctx.Fmt.Decimal2(mandiPrice * (Fixed.One - md.CommissionRate)), "trader", ctx.Fmt.Decimal2(floor), "gain", ctx.Fmt.Decimal2(r.Gain))
                    : s.Get("mandi.rowCaptionLoss", "mandi", ctx.Fmt.Decimal2(mandiPrice * (Fixed.One - md.CommissionRate)), "trader", ctx.Fmt.Decimal2(floor));
                int gi = g;
                var amount = new AmountVm("load:" + g, 0, max, md.LoadStepGrams, r.Load, a => RowPick(ctx, gi, a), ctx.Fmt.Kg)
                {
                    Scale = 1000,
                    Span = r.Free,
                    Then = a => new SheetRequest("Mandi", 0, With(sim, loads, gi, a)),
                    MinCaption = "0",
                    MaxCaption = max < r.Free ? s.Get("mandi.mostRoom", "kg", ctx.Fmt.Kg(max)) : s.Get("mandi.allInStore", "kg", ctx.Fmt.Kg(r.Free)),
                };
                vm.Lines.Add(new LineVm(s.Get(good.NameKey), null) { Icon = good.Id, Note = caption, Amount = amount });
                if (r.Load <= 0) continue;
                long value = MarketRules.Value(sim, g, r.Load, mandiPrice);
                gross += value;
                trader += MarketRules.Value(sim, g, System.Math.Min(r.Load, MarketRules.TraderCapacityLeft(sim, g)), floor);
                commands.Add(new MandiLoad(good.Id, r.Load));
                sums.Add(new LineVm(s.Get("mandi.cropLine", "good", s.Get(good.NameKey), "kg", ctx.Fmt.Kg(r.Load), "price", ctx.Fmt.PricePerKg(mandiPrice)), ctx.Fmt.Coins(value), "mandi", 0));
            }

            long commission = Sim.Core.Finance.Money.MulRate(gross, md.CommissionRate);
            // Your own cart or tractor-trolley takes the load if it fits: no trolley to hire (spec 013 §3.2), as GoToMandi charges it.
            long hire = weight <= Sim.Core.Equipment.EquipmentRules.CartGrams(sim) ? 0 : MarketRules.Indexed(sim, md.CartHirePerTrip);
            long net = gross - commission - hire;
            vm.Lines.Add(LineVm.Header(s.Get("mandi.sum")));
            vm.Lines.AddRange(sums);
            vm.Lines.Add(new LineVm(s.Get("mandi.atMandi"), ctx.Fmt.Coins(gross), "mandi", 0));
            vm.Lines.Add(new LineVm(s.Get("mandi.commission"), (commission > 0 ? "−" : "") + ctx.Fmt.Coins(commission), "mandi", 1));
            vm.Lines.Add(new LineVm(s.Get("mandi.hire"), "−" + ctx.Fmt.Coins(hire), "mandi", 2));
            vm.Lines.Add(new LineVm(s.Get("mandi.time"), s.Get("mandi.timeValue"), "mandi", 3));
            vm.Lines.Add(new LineVm(s.Get("mandi.youGet"), ctx.Fmt.Coins(commands.Count == 0 ? 0 : net), "mandi", 0));
            if (MarketRules.TraderBuys(sim))
            {
                vm.Lines.Add(new LineVm(s.Get("mandi.trader"), ctx.Fmt.Coins(trader), "mandi", 4));
                vm.Lines.Add(new LineVm(s.Get(net >= trader ? "mandi.better" : "mandi.worse"), ctx.Fmt.Coins(commands.Count == 0 ? 0 : System.Math.Abs(net - trader)), "mandi", 4));
            }
            else vm.Lines.Add(new LineVm(s.Get("mandi.trader"), s.Get("mandi.traderAway", "hour", ctx.Fmt.Integer(defs.Truck == null ? 6 : defs.Truck.ArriveHour)), "mandi", 4)); // no trader's figure to compare with
            vm.Actions.Add(commands.Count == 0
                ? new ActionVm("go", s.Get("mandi.go"), null, ActionKind.Primary, false, s.Get("mandi.empty"), null)
                : ActionVm.Checked(ctx, "go", s.Get("mandi.go"), ctx.Fmt.Kg(weight), ActionKind.Primary, at => new GoToMandi(at, commands)));
            return vm;
        }

        /// <summary>What a load of this crop would pay at the mandi, under its slider.</summary>
        private static AmountPick RowPick(ViewContext ctx, int g, long grams)
        {
            Simulation sim = ctx.Sim;
            if (grams <= 0) return new AmountPick { Note = ctx.Strings.Get("mandi.rowNone") };
            long mandi = MarketRules.Value(sim, g, grams, MarketRules.RegionalPrice(sim, g));
            if (!MarketRules.TraderBuys(sim)) return new AmountPick { Note = ctx.Strings.Get("mandi.rowSumAway", "mandi", ctx.Fmt.Coins(mandi)) }; // the truck is away: no figure to set beside it
            long trader = MarketRules.Value(sim, g, System.Math.Min(grams, MarketRules.TraderCapacityLeft(sim, g)), MarketRules.Floor(sim, g));
            return new AmountPick { Note = ctx.Strings.Get("mandi.rowSum", "mandi", ctx.Fmt.Coins(mandi), "trader", ctx.Fmt.Coins(trader)) };
        }
    }
}
