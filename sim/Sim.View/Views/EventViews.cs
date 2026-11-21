using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Events (US-154, spec 027 §7, Preview 12): the card with its choices and what each costs, the Events sheet (what waits, the last answers), the bell and the disaster's
    /// countdown. A view: it reads the state; an answer is the <see cref="AnswerEvent"/> command, paid choices asking on the usual card.
    /// </summary>
    public static class EventSheet
    {
        public const string CardKind = "EventCard", ListKind = "Events";

        private static EventsDef Defs(ViewContext ctx) { return ctx.Sim.Config.Game.Events; }

        public static string IdOf(ViewContext ctx, int def) { return Defs(ctx).Events[def].Id; }

        public static string Title(ViewContext ctx, int def) { return ctx.Strings.Get("event." + IdOf(ctx, def) + ".title"); }

        /// <summary>The picture beside the card: a family's or an event's own (a drought is the sun, hail a storm).</summary>
        public static string Icon(ViewContext ctx, int def)
        {
            switch (IdOf(ctx, def))
            {
                case "drought": return "weather_sun";
                case "heatwave": return "weather_heatwave";
                case "hailstorm": return "weather_storm";
                case "waterlogging": return "weather_heavy_rain";
                case "fire": case "fire_repair": return "flame";
                case "epidemic": return "health";
            }
            switch (Defs(ctx).Events[def].Family)
            {
                case EventDef.Economic: return "coin";
                case EventDef.Business: return "orders";
                case EventDef.Workforce: return "person_hand_f";
                case EventDef.Social: return "happiness";
                case EventDef.Opportunity: return "deed";
                default: return "bell";
            }
        }

        public static string Tag(ViewContext ctx, int def)
        {
            EventDef ev = Defs(ctx).Events[def];
            return ev.Disaster || ev.Id == EventsDef.FireRepair ? ctx.Strings.Get("event.tagDisaster") : ev.Major ? ctx.Strings.Get("event.tagMajor") : null;
        }

        private static PendingEvent Find(ViewContext ctx, long id)
        {
            EventsState st = EventRules.State(ctx.Sim);
            return st == null ? null : st.Waiting.Find(x => x.Id == id);
        }

        // ---- words: what each number in a card is ----

        private static string GoodName(ViewContext ctx, long good) { return ctx.Strings.Get(ctx.Sim.Config.Game.Market.Goods[(int)good].NameKey); }

        private static string BusinessName(ViewContext ctx, long id)
        {
            Sim.Core.Business.Business b = ctx.Sim.State.Businesses?.Find(id);
            return b == null ? ctx.Strings.Get("event.aBusiness") : BusinessWords.Name(ctx, Sim.Core.Business.BusinessRules.TypeOf(ctx.Sim, b));
        }

        private static string Building(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Building b = sim.State.Land.Buildings.Find(x => x.Id == id);
            return b == null ? ctx.Strings.Get("event.aBuilding") : ctx.Strings.Get(sim.Config.Game.Buildings[b.Def].NameKey);
        }

        private static string Family(ViewContext ctx, long id)
        {
            Household x = ctx.Sim.State.Citizens?.Household(id);
            return x == null ? ctx.Strings.Get("event.aFamily") : TownWords.Household(ctx, x);
        }

        private static int Buildings(Simulation sim, out int tower)
        {
            int n = 0;
            tower = 0;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (b.Owner != 0 || b.TileId < 0 || b.Cost <= 0) continue;
                n++;
                if (UtilityRules.Covers(sim, UtilityDef.Tower, b.TileId)) tower++;
            }
            return n;
        }

        /// <summary>Name/value pairs for the event's strings: what it is about and the numbers its text states.</summary>
        private static string[] About(ViewContext ctx, PendingEvent p, int choice)
        {
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            EventDef ev = Defs(ctx).Events[p.Def];
            var a = new List<string>();
            void Add(string k, string v) { a.Add(k); a.Add(v); }
            long now = sim.State.Clock.SimTime;
            Add("dry", ctx.Fmt.Integer(System.Math.Max(1, (now - st.LastRainAt) / Sim.Core.Definitions.ClockDef.SecondsPerDay)));
            Add("epiDays", ctx.Fmt.Integer(Defs(ctx).EpidemicDef.Days));
            Add("closeDays", ctx.Fmt.Integer(Defs(ctx).EpidemicDef.CloseDays));
            Add("fireDays", ctx.Fmt.Integer(Defs(ctx).FireDef.DowntimeDays));
            Add("slowDays", ctx.Fmt.Integer(Defs(ctx).FireDef.SlowRepairDays));
            Add("acres", ctx.Strings.Plural("land.acres", EventRules.RainFedAcres(sim)));
            Add("fields", ctx.Fmt.Integer(sim.State.Farm == null ? 0 : sim.State.Farm.Crops.Count));
            Add("hit", ctx.Fmt.Integer(p.Param));
            switch (ev.Id)
            {
                case "price_boom": case "price_slump": Add("crop", GoodName(ctx, p.Target)); break;
                case "supplier_strike": case "health_inspection": case "viral_rush": Add("business", BusinessName(ctx, p.Target)); break;
                case "breakdown": Add("what", p.Target2 == 1 ? BusinessName(ctx, p.Target) : ctx.Strings.Get("event.breakdown.machine")); break;
                case "wedding": Add("family", Family(ctx, p.Target)); break;
                case "dispute": Add("a", Family(ctx, p.Target)); Add("b", Family(ctx, p.Target2)); break;
                case "regional_grant": Add("grant", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, 6000 * Sim.Core.Finance.Money.Coin))); break;
                case "land_auction":
                    Add("price", ctx.Fmt.Coins(EventRules.BidPrice(sim, (int)p.Target)));
                    Add("size", ctx.Strings.Plural("land.acres", Defs(ctx) == null ? 1 : sim.Config.Game.Map.Parcels[(int)p.Target].Tiles.Count));
                    break;
                case "fire":
                {
                    int total = Buildings(sim, out int tower);
                    Add("n", ctx.Fmt.Integer(total));
                    Add("tower", ctx.Fmt.Integer(tower));
                    Add("building", Building(ctx, p.Target));
                    break;
                }
                case EventsDef.FireRepair: Add("building", Building(ctx, p.Target)); Add("bill", ctx.Fmt.Coins(p.Param)); break;
            }
            {
                foreach (EffectDef e in ev.Choices[choice >= 0 ? choice : 0].Effects)
                {
                    if (e.Kind == EffectKind.CropHealth) Add("pct", ctx.Fmt.Integer(System.Math.Max(1, (int)((Fixed.One - e.Factor) * Fixed.FromInt(100)).RoundToLong())));
                    if (e.Kind == EffectKind.PriceShift || e.Kind == EffectKind.CostShift) Add("pct", ctx.Fmt.Integer(System.Math.Abs(e.Percent)));
                    if (e.Days > 0) Add("days", ctx.Fmt.Integer(e.Days));
                    if (e.Hours > 0) Add("hours", ctx.Fmt.Integer(e.Hours));
                    if (e.Kind == EffectKind.Happiness) Add("points", ctx.Fmt.Integer(System.Math.Abs(e.Points)));
                    if (e.Kind == EffectKind.Cash && e.Unit == "rainfedAcre") Add("unit", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, e.Amount * Sim.Core.Finance.Money.Coin)));
                    if (e.Kind == EffectKind.Cash && e.Unit == "field") Add("unit", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, e.Amount * Sim.Core.Finance.Money.Coin)));
                    if (e.Kind == EffectKind.Cash && e.Unit == "person") Add("unit", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, e.Amount * Sim.Core.Finance.Money.Coin)));
                }
                Add("people", ctx.Fmt.Integer(FestivalRules.People(sim)));
            }
            return a.ToArray();
        }

        /// <summary>A choice's name by event and choice index (the away card and the history).</summary>
        public static string ChoiceName(ViewContext ctx, int def, int choice) { return ctx.Strings.Get("event." + IdOf(ctx, def) + ".choice." + Defs(ctx).Events[def].Choices[choice].Id); }

        /// <summary>What a choice does, in words, with no card behind it (the away card's line).</summary>
        public static string Outcome(ViewContext ctx, int def, int choice)
        {
            var p = new PendingEvent { Def = def };
            return ctx.Strings.Get("event." + IdOf(ctx, def) + ".hint." + Defs(ctx).Events[def].Choices[choice].Id, About(ctx, p, choice));
        }

        public static string ChoiceLabel(ViewContext ctx, PendingEvent p, int choice) { return ctx.Strings.Get("event." + IdOf(ctx, p.Def) + ".choice." + Defs(ctx).Events[p.Def].Choices[choice].Id); }

        /// <summary>The preparation's note under the choices ("Your pump keeps 6 acres watered"), or null.</summary>
        private static string Prep(ViewContext ctx, PendingEvent p)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            switch (IdOf(ctx, p.Def))
            {
                case "drought":
                {
                    int watered = 0;
                    foreach (Sim.Core.Farm.Crop c in sim.State.Farm.Crops) if (!sim.Config.Game.Farm.Crops[c.Def].Fodder && !EventRules.RainFed(sim, c.TileId)) watered++;
                    return watered == 0 ? null : s.Get("event.drought.prep", "n", ctx.Fmt.Integer(watered));
                }
                case "fire":
                {
                    int total = Buildings(sim, out int tower);
                    return s.Get(tower > 0 ? "event.fire.prep" : "event.fire.prepNone", "tower", ctx.Fmt.Integer(tower), "n", ctx.Fmt.Integer(total));
                }
                case "epidemic":
                {
                    int covered = 0, all = 0;
                    foreach (Household x in TownLifeRules.Yours(sim))
                    {
                        all++;
                        Building b = sim.State.Land.Buildings.Find(q => q.Id == x.HomeId);
                        if (b != null && b.TileId >= 0 && ClinicRules.InPost(sim) && ClinicRules.Covered(sim, b.TileId)) covered++;
                    }
                    return s.Get(covered > 0 ? "event.epidemic.prep" : "event.epidemic.prepNone", "n", ctx.Fmt.Integer(covered), "all", ctx.Fmt.Integer(all));
                }
            }
            return null;
        }

        /// <summary>One card: the event, its choices with their costs and a default named, what happens if you wait (Preview 12).</summary>
        public static SheetVm Card(ViewContext ctx, long id)
        {
            var s = ctx.Strings;
            Simulation sim = ctx.Sim;
            PendingEvent p = Find(ctx, id);
            if (p == null) return new SheetVm { Icon = "bell", Title = ctx.Reject(RejectReason.EventClosed) };
            EventsDef d = Defs(ctx);
            EventDef ev = d.Events[p.Def];
            string name = ev.Id;
            var vm = new SheetVm { Icon = Icon(ctx, p.Def), Title = Title(ctx, p.Def), Subtitle = Tag(ctx, p.Def), Amber = ev.Disaster || ev.Id == EventsDef.FireRepair };
            vm.Lines.Add(new LineVm(s.Get("event." + name + ".about", About(ctx, p, -1)), null, "event", p.Id));
            for (int c = 0; c < ev.Choices.Count; c++)
            {
                int choice = c;
                string[] a = About(ctx, p, choice);
                long cost = EventRules.ChoiceCost(sim, p.Def, choice, p);
                bool def = choice == ev.Default;
                string label = ChoiceLabel(ctx, p, choice);
                string hint = s.Get("event." + name + ".hint." + ev.Choices[choice].Id, a);
                if (cost > 0) hint = s.Get("event.hintCost", "cost", ctx.Fmt.Coins(cost), "hint", hint);
                var line = new LineVm(label, null, null, 0) { Note = hint, Tag = def ? s.Get("event.ifYouWait") : null };
                string button = cost > 0 ? s.Get("event.pay", "what", s.Get("event." + name + ".button." + ev.Choices[choice].Id), "cost", ctx.Fmt.Coins(cost)) : s.Get("event." + name + ".button." + ev.Choices[choice].Id);
                line.Action = ActionVm.Checked(ctx, "event:" + p.Id + ":" + choice, button, null, def ? ActionKind.Secondary : ActionKind.Primary, at => new AnswerEvent(at, p.Id, choice));
                vm.Lines.Add(line);
            }
            string prep = Prep(ctx, p);
            if (prep != null) vm.Lines.Add(new LineVm(prep, null) { Dim = true });
            long left = System.Math.Max(0, p.ExpiresAt - sim.State.Clock.SimTime);
            vm.Lines.Add(new LineVm(ev.Disaster ? s.Get("event.timerStrike", "time", ctx.Fmt.Duration(left)) : s.Get("event.timer", "choice", ChoiceLabel(ctx, p, ev.Default), "time", ctx.Fmt.Duration(left)), null) { Dim = true });
            return vm;
        }

        // ---- the Events sheet ----

        /// <summary>Menu → Events: the cards waiting with their clocks, and the last answers with what each cost or paid.</summary>
        public static SheetVm List(ViewContext ctx)
        {
            var s = ctx.Strings;
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            var vm = new SheetVm { Icon = "bell", Title = s.Get("events.title"), Subtitle = st != null && st.Waiting.Count > 0 ? s.Get("events.waitingN", "n", ctx.Fmt.Integer(st.Waiting.Count)) : s.Get("events.quiet") };
            if (st == null) return vm;
            if (st.Waiting.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("events.waiting", "n", ctx.Fmt.Integer(st.Waiting.Count))));
                foreach (PendingEvent p in st.Waiting)
                {
                    EventDef ev = Defs(ctx).Events[p.Def];
                    long left = System.Math.Max(0, p.ExpiresAt - sim.State.Clock.SimTime);
                    string sub = ev.Disaster ? s.Get("event.timerStrike", "time", ctx.Fmt.Duration(left)) : s.Get("event.timer", "choice", ChoiceLabel(ctx, p, ev.Default), "time", ctx.Fmt.Duration(left));
                    vm.Lines.Add(new LineVm(Title(ctx, p.Def), null, "event", p.Id) { Icon = Icon(ctx, p.Def), Note = sub, Tag = Tag(ctx, p.Def), Action = ActionVm.Open("eventOpen:" + p.Id, s.Get("action.open"), new SheetRequest(CardKind, p.Id)) });
                }
            }
            if (st.History.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("events.last")));
                for (int i = st.History.Count - 1; i >= 0; i--)
                {
                    EventRecord r = st.History[i];
                    string ago = ctx.Fmt.Duration(System.Math.Max(0, sim.State.Clock.SimTime - r.At));
                    string choice = s.Get("event." + IdOf(ctx, r.Def) + ".choice." + Defs(ctx).Events[r.Def].Choices[r.Choice].Id);
                    string how = r.Defaulted ? s.Get("events.taken", "choice", choice) : choice;
                    vm.Lines.Add(new LineVm(Title(ctx, r.Def), r.Cash < 0 ? "−" + ctx.Fmt.Coins(-r.Cash) : s.Get("events.nothing"), "eventHist", i) { Note = s.Get("events.rowNote", "how", how, "ago", ctx.Fmt.Duration(System.Math.Max(0, sim.State.Clock.SimTime - r.At))) });
                }
            }
            return vm;
        }

        // ---- the bell, the clock's banner and the away card ----

        /// <summary>One alert for each card waiting: a disaster's amber with its countdown, an ordinary event's with the time to answer.</summary>
        public static void Alerts(ViewContext ctx, List<AlertVm> list)
        {
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            if (st == null) return;
            var s = ctx.Strings;
            foreach (PendingEvent p in st.Waiting)
            {
                EventDef ev = Defs(ctx).Events[p.Def];
                long left = System.Math.Max(0, p.ExpiresAt - sim.State.Clock.SimTime);
                bool disaster = ev.Disaster || ev.Id == EventsDef.FireRepair;
                list.Add(new AlertVm
                {
                    Level = disaster ? AlertLevel.Urgent : AlertLevel.Decision,
                    Text = s.Get(ev.Id == EventsDef.FireRepair ? "alert.eventFire" : disaster ? "alert.eventDisaster" : "alert.event", "name", Title(ctx, p.Def), "time", ctx.Fmt.Duration(left)),
                    Note = null,
                    Action = ActionVm.Open("event:" + p.Id, s.Get("action.see"), new SheetRequest(CardKind, p.Id)),
                });
            }
        }

        /// <summary>The note beside the clock while a disaster is signalled: "A fire risk in 47 h", or null.</summary>
        public static string DisasterNote(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            if (st == null || st.DisasterDef < 0) return null;
            long left = System.Math.Max(0, st.DisasterStrikeAt - sim.State.Clock.SimTime);
            return ctx.Strings.Get("event.banner", "name", ctx.Strings.Get("event." + IdOf(ctx, st.DisasterDef) + ".risk"), "time", ctx.Fmt.Duration(left));
        }
    }
}
