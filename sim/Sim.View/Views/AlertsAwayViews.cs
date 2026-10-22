using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    public enum AlertLevel
    {
        Urgent = 0,
        Decision = 1,
        Info = 2,
    }

    public sealed class AlertVm
    {
        public AlertLevel Level { get; internal set; }
        public string Text { get; internal set; }

        /// <summary>The alert's action (Spray, Collect all, Open the store…), or null.</summary>
        public ActionVm Action { get; internal set; }
    }

    /// <summary>Alerts (spec 008 §3.10), derived from the state at each refresh, urgent first.</summary>
    public static class Alerts
    {
        /// <summary>The id of the Collect all action: the presentation runs <see cref="CollectAll"/>.</summary>
        public const string CollectId = "collectAll";

        public static List<AlertVm> Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var list = new List<AlertVm>();
            long t = ctx.Now, S = sim.Clock.SecondsPerSeason;

            // Urgent: living costs at risk on the season's last day; a missed payment or a final notice.
            FounderState f = sim.State.Farm.Founder;
            long nextPlan = MarketRules.Indexed(sim, sim.Config.Game.Farm.DietPlans[f.AutoSubsistence ? f.PreviousPlan : f.DietPlan].CostPerSeason);
            if (S - t % S <= ClockDef.SecondsPerDay && sim.State.Ledger.Cash < nextPlan)
                list.Add(new AlertVm { Level = AlertLevel.Urgent, Text = s.Get("alert.living", "cost", ctx.Fmt.Coins(nextPlan)), Action = ActionVm.Open("money", s.Get("action.open"), new SheetRequest("Money", 0)) });
            if (sim.State.Credit != null)
            {
                if (sim.State.Credit.PendingForeclosure)
                    list.Add(new AlertVm { Level = AlertLevel.Urgent, Text = s.Get("alert.finalNotice"), Action = ActionVm.Open("money", s.Get("action.open"), new SheetRequest("Money", 0)) });
                foreach (Loan l in sim.State.Credit.Loans)
                    if (l.IsOpen && l.HasArrears)
                        list.Add(new AlertVm { Level = AlertLevel.Urgent, Text = s.Get("alert.missed", "lender", s.Get(sim.Config.Game.Credit.Lenders[l.Lender].NameKey)), Action = ActionVm.Open("repay:" + l.Id, s.Get("money.repay"), new SheetRequest("Repay", l.Id)) });
            }

            // Decisions: pests, spoiling soon, the store nearly full.
            long grace = sim.Config.Game.Farm.Farm.ReadyGraceSeconds, warn = ctx.Ui.SpoilWarningHours * 3600L;
            foreach (Crop c in sim.State.Farm.Crops)
            {
                string crop = s.Get(sim.Config.Game.Farm.Crops[c.Def].NameKey), field = PeopleWords.Field(ctx, c.TileId);
                int tile = c.TileId;
                if (c.State == CropState.Growing && c.Outbreak != Outbreak.None && c.Spray == SprayState.None)
                    list.Add(new AlertVm { Level = AlertLevel.Decision, Text = s.Get("alert.pests", "field", field), Action = ActionVm.Checked(ctx, "spray:" + tile, s.Get("action.spray"), null, ActionKind.Primary, at => new Spray(at, tile)) });
                long left = c.ReadyAt + grace - t;
                if (c.State == CropState.Ready && left > 0 && left <= warn)
                    list.Add(new AlertVm { Level = AlertLevel.Decision, Text = s.Get("alert.spoiling", "crop", crop, "field", field, "time", ctx.Fmt.Duration(left)), Action = ActionVm.Checked(ctx, "harvest:" + tile, s.Get("action.harvest"), null, ActionKind.Primary, at => new Harvest(at, tile, Overflow.StoreOnly)) });
            }
            foreach (Tree tree in sim.State.Farm.Trees)
            {
                long left = tree.RipeAt + grace - t;
                long slot = tree.SlotId;
                if (tree.RipeGrams > 0 && left > 0 && left <= warn)
                    list.Add(new AlertVm { Level = AlertLevel.Decision, Text = s.Get("alert.fruitSpoiling", "tree", s.Get(sim.Config.Game.Farm.Trees[tree.Def].NameKey), "time", ctx.Fmt.Duration(left)), Action = ActionVm.Checked(ctx, "pick:" + slot, s.Get("action.pick"), null, ActionKind.Primary, at => new PickFruit(at, slot, Overflow.StoreOnly)) });
            }
            long used = Store.UsedGrams(sim), cap = Store.CapacityGrams(sim);
            if (cap > 0 && used * 100 >= cap * ctx.Ui.StoreNearlyFullPercent)
                list.Add(new AlertVm { Level = AlertLevel.Decision, Text = s.Get("alert.storeFull"), Action = ActionVm.Open("store", s.Get("action.open"), new SheetRequest("Store", 0)) });

            // Information: ready to collect, a sick day.
            int ready = CollectAll.ReadyCount(sim);
            if (ready > 0) list.Add(new AlertVm { Level = AlertLevel.Info, Text = s.Plural("alert.ready", ready), Action = new ActionVm(CollectId, s.Get("alert.collect"), null, ActionKind.Primary, true, null, null) });
            if (f.SickToday) list.Add(new AlertVm { Level = AlertLevel.Info, Text = s.Get("alert.sick") });
            return list;
        }

        /// <summary>The alert list sheet: one row per alert with its action.</summary>
        public static SheetVm Sheet(ViewContext ctx)
        {
            var vm = new SheetVm { Icon = "alert", Title = ctx.Strings.Get("alert.title") };
            foreach (AlertVm a in Build(ctx))
                vm.Lines.Add(new LineVm(ctx.Strings.Get("alert.level." + a.Level) + " · " + a.Text, null) { Action = a.Action });
            if (vm.Lines.Count == 0) vm.Subtitle = ctx.Strings.Get("alert.none");
            return vm;
        }
    }

    /// <summary>What Collect all couldn't fit (spec 008 §3.9): the same commands with SellRestToTrader, priced on a copy of the town.</summary>
    public sealed class CollectRestVm
    {
        public List<Command> Commands { get; } = new List<Command>();
        public long Grams { get; internal set; }
        public long Coins { get; internal set; }
    }

    public static class CollectAllRest
    {
        /// <summary>Null when everything fitted. Run after the StoreOnly commands: what's still ready is offered to the trader.</summary>
        public static CollectRestVm Build(Simulation sim)
        {
            long at = sim.State.Clock.SimTime;
            List<Command> storeOnly = CollectAll.Commands(sim, at);
            if (storeOnly.Count == 0) return null;
            var rest = new CollectRestVm();
            foreach (Command c in storeOnly)
            {
                if (c is Harvest h) rest.Commands.Add(new Harvest(at, h.TileId, Overflow.SellRestToTrader));
                else if (c is PickGarden p) rest.Commands.Add(new PickGarden(at, p.BedId, Overflow.SellRestToTrader));
                else if (c is PickFruit f) rest.Commands.Add(new PickFruit(at, f.SlotId, Overflow.SellRestToTrader));
            }
            // Exact: run them on a copy of the town and read what the trader paid and how much was sold.
            Simulation copy = Simulation.FromState(sim.Config, sim.State.Clone(withCommandLog: false));
            long cash = copy.State.Ledger.Cash;
            foreach (Command c in rest.Commands) copy.Execute(c);
            foreach (SimEvent e in copy.DrainEvents())
                if (e is SoldToTrader s) rest.Grams += s.Quantity;
            rest.Coins = copy.State.Ledger.Cash - cash;
            return rest.Grams == 0 ? null : rest;
        }
    }

    /// <summary>
    /// The full While you were away card (spec 008 §3.9, ADR 0043 §8): farm and people rows, money first, at most
    /// <c>awayRowsMax</c> with "and N more"; null when nothing happened.
    /// </summary>
    public static class AwayCard
    {
        public sealed class Vm
        {
            public string Title { get; internal set; }
            public string Subtitle { get; internal set; }
            public List<LineVm> Lines { get; } = new List<LineVm>();
            public List<LineVm> More { get; } = new List<LineVm>();
            public int ReadyCount { get; internal set; }

            /// <summary>The period of the last season closed while away, or −1.</summary>
            public long ReportPeriod { get; internal set; } = -1;

            // Totals over the events (AC-8).
            public long MarketSales, TraderSales, MandiSales, DayLabour, HomeUse, SpoiledGrams;
            public int Pests, SickDays, Seasons;
        }

        public static Vm Build(ViewContext ctx, IReadOnlyList<SimEvent> events)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new Vm();
            long applied = 0;
            bool capped = false;
            Fixed indexFrom = Fixed.Zero, indexTo = Fixed.Zero;
            var pests = new List<string>();
            int becameReady = 0;
            // Spec 011 §7: what the truck took, the orders and the hands' held sales.
            var truckGrams = new List<KeyValuePair<string, long>>();
            void Truck(string good, long q)
            {
                if (q <= 0) return;
                int i = truckGrams.FindIndex(x => x.Key == good);
                if (i < 0) truckGrams.Add(new KeyValuePair<string, long>(good, q));
                else truckGrams[i] = new KeyValuePair<string, long>(good, truckGrams[i].Value + q);
            }
            var orders = new List<long>();
            var delivered = new Dictionary<long, long>(); // keyed lookups only
            var lapsed = new Dictionary<long, long>();
            var orderGood = new Dictionary<long, string>();
            long paid = 0, held = 0;
            foreach (SimEvent e in events)
            {
                switch (e)
                {
                    case CropReady _: case BedReady _: case FruitRipe _: becameReady++; break;
                    case AwayAdvanceCompleted a: applied = a.AppliedSeconds; capped = a.Capped; break;
                    case MarketSales m:
                        long local = MarketRules.Value(sim, sim.Config.Game.Market.IndexOf(m.GoodId), m.LocalQuantity, m.Price);
                        vm.MarketSales += local;
                        vm.TraderSales += m.Amount - local;
                        Truck(m.GoodId, m.TraderQuantity);
                        break;
                    case SoldToTrader t: vm.TraderSales += t.Amount; Truck(t.GoodId, t.Quantity); break;
                    case HandSoldHeld hs: held += hs.Quantity; break;
                    case OrderPaid op: paid += op.Amount; break;
                    case OrdersPosted posted:
                        foreach (SupermarketOrder o in posted.Orders) { orders.Add(o.Id); orderGood[o.Id] = sim.Config.Game.Market.Goods[o.Good].Id; }
                        break;
                    case OrderDelivered od:
                        if (!orders.Contains(od.OrderId)) { orders.Add(od.OrderId); orderGood[od.OrderId] = od.GoodId; }
                        delivered.TryGetValue(od.OrderId, out long sofar);
                        delivered[od.OrderId] = sofar + od.Quantity;
                        break;
                    case OrderLapsed ol:
                        if (!orders.Contains(ol.OrderId)) { orders.Add(ol.OrderId); orderGood[ol.OrderId] = ol.GoodId; }
                        lapsed[ol.OrderId] = ol.Left;
                        break;
                    case MandiTripReturned r: vm.MandiSales += r.Net; break;
                    case DayLabourPaid d: vm.DayLabour += d.Amount; break;
                    case HomeUseEaten h: vm.HomeUse += h.Value; break;
                    case GoodsSpoiled g: vm.SpoiledGrams += Store.WeightGrams(sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(g.GoodId)], g.Quantity); break;
                    case PestWarning p: vm.Pests++; string fld = PeopleWords.Field(ctx, p.TileId); if (!pests.Contains(fld)) pests.Add(fld); break;
                    case FounderSick _: vm.SickDays++; break;
                    case SeasonClosed c: vm.Seasons++; vm.ReportPeriod = c.Period; break;
                    case PriceIndexChanged i: if (indexFrom == Fixed.Zero) indexFrom = i.Old; indexTo = i.New; break;
                }
            }
            // A short absence is normal play: no card under the threshold (ADR 0046 §2); the HUD, the bell and the toasts cover it.
            if (!capped && applied < ctx.Ui.AwayCardMinHours * 3600L) return null;
            vm.Title = s.Get(capped ? "away.titleCapped" : "away.titleTime", "time", ctx.Fmt.Duration(applied));
            if (capped) vm.Subtitle = s.Get("away.capped", "hours", ctx.Fmt.Integer(sim.Clock.AwayCapHours));

            // Money rows, most valuable first.
            var money = new List<KeyValuePair<long, LineVm>>();
            void Money(long amount, string key, AwayRow row, string icon)
            {
                if (amount <= 0) return;
                money.Add(new KeyValuePair<long, LineVm>(amount, new LineVm(s.Get(key), ctx.Fmt.Coins(amount), "away", (long)row) { Icon = icon }));
            }
            Money(vm.MarketSales, "away.market", AwayRow.Sales, "coin");
            Money(vm.TraderSales, "away.trader", AwayRow.Sales, "coin");
            Money(vm.MandiSales, "away.mandi", AwayRow.Sales, "coin");
            Money(paid, "away.supermarketPaid", AwayRow.Orders, "coin");
            Money(vm.DayLabour, "away.dayLabour", AwayRow.DayLabour, "coin");
            Money(vm.HomeUse, "away.homeUse", AwayRow.HomeUse, "basket");
            money.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : 0);
            var rows = new List<LineVm>();
            vm.ReadyCount = CollectAll.ReadyCount(sim);
            // Only what ripened while away is news; things already waiting before don't bring the card back (ADR 0043 §8).
            if (becameReady > 0 && vm.ReadyCount > 0) rows.Add(new LineVm(s.Get("away.ready"), s.Plural("away.readyValue", vm.ReadyCount), "away", (long)AwayRow.Ready) { Icon = "basket" });
            foreach (KeyValuePair<long, LineVm> m in money) rows.Add(m.Value);

            // The truck, the orders and the hands' held sales (spec 011 §7).
            if (truckGrams.Count > 0)
            {
                truckGrams.Sort((a, b) => b.Value.CompareTo(a.Value));
                var parts = new List<string>();
                for (int i = 0; i < truckGrams.Count && i < 2; i++) parts.Add(s.Get("away.truckGood", "qty", ctx.Fmt.Kg(truckGrams[i].Value), "good", Names.Of(ctx, truckGrams[i].Key).ToLowerInvariant()));
                rows.Add(new LineVm(s.Get("away.truck"), string.Join(", ", parts) + (truckGrams.Count > 2 ? s.Get("away.andMore") : ""), "away", (long)AwayRow.Truck) { Icon = "coin" });
            }
            if (held > 0) rows.Add(new LineVm(s.Get("away.held"), ctx.Fmt.Kg(held), "away", (long)AwayRow.Held) { Icon = "person_hand_m" });
            foreach (long id in orders)
            {
                delivered.TryGetValue(id, out long d);
                bool gone = lapsed.TryGetValue(id, out long left);
                if (d == 0 && !gone) continue; // still open, nothing done yet: the Orders sheet shows it
                bool open = false;
                foreach (SupermarketOrder o in sim.State.Market.SupermarketOrders) open |= o.Id == id && o.Left > 0;
                string value = !gone ? s.Get(open ? "away.orderSoFar" : "away.orderDelivered", "qty", ctx.Fmt.Kg(d))
                    : d > 0 ? s.Get("away.orderPart", "qty", ctx.Fmt.Kg(d), "left", ctx.Fmt.Kg(left)) : s.Get("away.orderLapsed");
                rows.Add(new LineVm(s.Get("away.supermarketOrder", "good", Names.Of(ctx, orderGood[id])), value, "away", (long)AwayRow.Orders) { Icon = "basket" });
            }

            // The people rows (US-182) after the money.
            AwayVm people = AwayView.People(ctx, events);
            if (people != null)
                foreach (LineVm l in people.Lines)
                    if (l.Label != s.Get("away.dayLabour")) rows.Add(new LineVm(l.Label, l.Value, "away", (long)AwayRow.People) { Icon = "person_hand_m" });

            if (vm.SpoiledGrams > 0) rows.Add(new LineVm(s.Get("away.spoiled"), ctx.Fmt.Kg(vm.SpoiledGrams), "away", (long)AwayRow.Spoiled));
            foreach (string p in pests) rows.Add(new LineVm(s.Get("away.pests"), p, "away", (long)AwayRow.Pests));
            if (vm.SickDays > 0) rows.Add(new LineVm(s.Get("away.sick"), s.Plural("why.days", vm.SickDays), "away", (long)AwayRow.Sick));
            if (vm.Seasons > 0) rows.Add(new LineVm(s.Get("away.season"), ReportSheet.PeriodName(ctx, vm.ReportPeriod), "away", (long)AwayRow.Seasons));
            if (indexTo > indexFrom && indexFrom > Fixed.Zero)
                rows.Add(new LineVm(s.Get("away.prices"), s.Get("away.pricesValue", "pct", ctx.Fmt.Percent(indexTo / indexFrom - Fixed.One)), "away", (long)AwayRow.Index));

            int max = ctx.Ui.AwayRowsMax;
            for (int i = 0; i < rows.Count; i++) (i < max ? vm.Lines : vm.More).Add(rows[i]);
            return rows.Count == 0 ? null : vm;
        }
    }

    /// <summary>
    /// Every domain event's use in the presentation (spec 008 AC-9): a motion, an away row, an alert, or the state only.
    /// A new event type without an entry fails <c>MotionTests.EveryEventMapped</c>.
    /// </summary>
    public static class EventMap
    {
        public enum Use { Motion, Away, Alert, StateOnly }

        public static readonly Dictionary<string, Use> ByType = new Dictionary<string, Use>
        {
            // Spec 001.
            { "HourStarted", Use.StateOnly }, { "DayStarted", Use.StateOnly }, { "SeasonStarted", Use.StateOnly }, { "YearStarted", Use.StateOnly },
            { "WeatherSpellStarted", Use.StateOnly }, { "ForecastRevised", Use.StateOnly }, { "AwayAdvanceCompleted", Use.Away }, { "ClockAnomalyDetected", Use.StateOnly },
            // Specs 002, 003.
            { "ParcelBought", Use.Motion }, { "ParcelSold", Use.Motion }, { "CropSown", Use.Motion }, { "CropReady", Use.Motion }, { "CropHarvested", Use.Motion },
            { "FounderSick", Use.Alert }, { "PestWarning", Use.Alert }, { "FieldCleared", Use.StateOnly }, { "BedReady", Use.Motion }, { "GardenPicked", Use.Motion },
            { "DayLabourPaid", Use.Away }, { "DietPlanChanged", Use.StateOnly }, { "HomeUseEaten", Use.Away }, { "TreePlanted", Use.Motion }, { "TreeMatured", Use.Motion },
            { "FruitRipe", Use.Motion }, { "FruitPicked", Use.Motion }, { "TreeRemoved", Use.StateOnly }, { "TreeSpent", Use.StateOnly }, 
            // Spec 004.
            { "MarketSales", Use.Motion }, { "GoodsSpoiled", Use.Away }, { "MandiTripStarted", Use.Motion }, { "MandiTripReturned", Use.Motion },
            { "PriceIndexChanged", Use.Away }, { "SoldToTrader", Use.Motion },
            // Spec 011.
            { "TruckArrived", Use.Motion }, { "TruckLeft", Use.Away }, { "HandSoldHeld", Use.Away },
            { "OrdersPosted", Use.Alert }, { "OrderDelivered", Use.Motion }, { "OrderLapsed", Use.Away }, { "OrderPaid", Use.Motion }, { "InputsBought", Use.Motion },
            // Spec 005.
            { "LedgerReconciliationFailed", Use.StateOnly }, { "SeasonClosed", Use.Away }, { "LoanTaken", Use.Motion }, { "LoanRepayment", Use.Motion },
            { "LoanPaymentMissed", Use.Alert }, { "LoanRestructured", Use.StateOnly }, { "LoanDefaulted", Use.Alert }, { "FinalNotice", Use.Alert },
            { "ParcelSeized", Use.Alert }, { "ForeclosureCompleted", Use.Alert }, { "CreditScoreChanged", Use.StateOnly }, { "ObligationPaid", Use.StateOnly },
            { "ObligationUnpaid", Use.Alert },
            // Spec 006.
            { "LabourersHired", Use.Motion }, { "LabourersLeft", Use.Motion }, { "LabourersHarvested", Use.Motion }, { "ConstructionStarted", Use.Motion }, { "HandHutBuilt", Use.Motion },
            { "JobPosted", Use.StateOnly }, { "JobPostExpired", Use.StateOnly }, { "ApplicantArrived", Use.Motion }, { "RegularAsksToMoveIn", Use.Motion },
            { "ApplicantLeft", Use.StateOnly }, { "ApplicantInterviewed", Use.StateOnly }, { "OfferDeclined", Use.StateOnly }, { "HandHired", Use.Motion },
            { "ContractRenewed", Use.Away }, { "ContractLapsed", Use.Away }, { "HandLeft", Use.Away }, { "HandDismissed", Use.StateOnly },
            { "HandSkillGrew", Use.StateOnly }, { "StandingOrderSet", Use.StateOnly }, { "StandingOrderCleared", Use.StateOnly }, { "OrderExecuted", Use.Away },
            { "OrderWaiting", Use.Away }, { "OrderCompleted", Use.StateOnly },
            // Spec 009.
            { "EraProgress", Use.StateOnly }, { "EraUnlocked", Use.Away },
            // Spec 012.
            { "ShedStarted", Use.Motion }, { "ShedBuilt", Use.Motion }, { "AnimalBought", Use.Motion }, { "AnimalSold", Use.Motion },
            { "MilkCollected", Use.Motion }, { "AnimalDry", Use.Away }, { "CalfBorn", Use.Away },
        };
    }
}
