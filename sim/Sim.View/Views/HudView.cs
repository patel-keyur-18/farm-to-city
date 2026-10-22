using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>The always-visible HUD (spec 008 §3.3): time strip, cash, store fill and the ready count.</summary>
    public sealed class HudVm
    {
        public string DateText { get; internal set; }
        public string ClockText { get; internal set; }
        public string WeatherId { get; internal set; }
        public string WeatherText { get; internal set; }
        public int Season { get; internal set; }
        public long SecondOfDay { get; internal set; }

        /// <summary>Whole coins, short form from a lakh (1.2 L).</summary>
        public string CashText { get; internal set; }

        /// <summary>The cash in money (1/100 coin), exactly the state's.</summary>
        public long Cash { get; internal set; }
        public string CashFullText { get; internal set; }

        /// <summary>The store's fill, 0–100 (rounded down so a full-looking store is full).</summary>
        public int StorePercent { get; internal set; }
        public string StoreText { get; internal set; }
        public bool StoreNearlyFull { get; internal set; }

        /// <summary>Ready crops, ready garden beds and ripe fruit trees.</summary>
        public int ReadyCount { get; internal set; }
        public string CollectText { get; internal set; }

        /// <summary>The current weather run and the next: "Sun until 14:00 · then heavy rain about 1 h" (ADR 0043 §2).</summary>
        public string ForecastText { get; internal set; }

        /// <summary>Open alerts (spec 008 §3.10): the ! button's count (0 hides it), red when one is urgent.</summary>
        public int AlertCount { get; internal set; }
        public bool AlertUrgent { get; internal set; }

        /// <summary>Applicants waiting for an answer: the People button's badge (0 hides it).</summary>
        public int PeopleBadge { get; internal set; }
        public bool HasPeople { get; internal set; }

        /// <summary>The supermarket's orders (spec 011 §7): shown once orders have begun; the badge counts open orders.</summary>
        public bool HasOrders { get; internal set; }
        public int OrdersBadge { get; internal set; }
    }

    public static class HudView
    {
        public static HudVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            CalendarDate d = Calendar.From(ctx.Now, sim.Clock);
            long cash = sim.State.Ledger.Cash;
            long used = Store.UsedGrams(sim), capacity = Store.CapacityGrams(sim);
            int pct = capacity <= 0 ? 0 : (int)System.Math.Min(100, used * 100 / capacity);
            int ready = CollectAll.ReadyCount(sim);
            List<AlertVm> alerts = Alerts.Build(ctx);
            return new HudVm
            {
                ForecastText = ForecastView.HudLine(ctx),
                AlertCount = alerts.Count,
                AlertUrgent = alerts.Count > 0 && alerts[0].Level == AlertLevel.Urgent,
                DateText = ctx.Fmt.Date(d, sim.Clock.DaysPerSeason),
                ClockText = Text.Formatter.Clock(d),
                WeatherId = sim.CurrentWeather.Id,
                WeatherText = ctx.Strings.Get(sim.CurrentWeather.NameKey),
                Season = (int)d.Season,
                SecondOfDay = ctx.Now % 86400,
                Cash = cash,
                CashText = ctx.Fmt.ShortCoins(cash),
                CashFullText = ctx.Fmt.Coins(cash),
                StorePercent = pct,
                StoreText = ctx.Fmt.Percent(Sim.Core.Maths.Fixed.FromInt(pct) / Sim.Core.Maths.Fixed.FromInt(100)),
                StoreNearlyFull = pct >= ctx.Ui.StoreNearlyFullPercent,
                ReadyCount = ready,
                CollectText = ctx.Strings.Plural("hud.collect", ready),
                HasPeople = sim.State.People != null,
                PeopleBadge = sim.State.People == null ? 0 : sim.State.People.Applicants.Count,
                HasOrders = Supermarket.Started(sim) || sim.State.Market.SupermarketOrders.Count > 0,
                OrdersBadge = OrdersSheet.OpenCount(sim),
            };
        }
    }

    /// <summary>
    /// Collect all (spec 008 §3.9): a harvest or pick for everything ready, the one nearest to spoiling first, each with
    /// <see cref="Overflow.StoreOnly"/>: what doesn't fit stays where it is. The presentation executes the commands in order.
    /// </summary>
    public static class CollectAll
    {
        public static int ReadyCount(Simulation sim)
        {
            int n = 0;
            foreach (Crop c in sim.State.Farm.Crops) if (c.State == CropState.Ready) n++;
            foreach (GardenBed b in sim.State.Farm.Beds) if (b.State == BedState.Ready) n++;
            foreach (Tree t in sim.State.Farm.Trees) if (t.RipeGrams > 0) n++;
            return n;
        }

        /// <summary>The commands, soonest-to-spoil first (ready time + the grace), ties by kind then id.</summary>
        public static List<Command> Commands(Simulation sim, long at)
        {
            var items = new List<KeyValuePair<long, Command>>();
            long grace = sim.Config.Game.Farm.Farm.ReadyGraceSeconds;
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Ready) items.Add(new KeyValuePair<long, Command>(c.ReadyAt + grace, new Harvest(at, c.TileId, Overflow.StoreOnly)));
            foreach (GardenBed b in sim.State.Farm.Beds)
                if (b.State == BedState.Ready) items.Add(new KeyValuePair<long, Command>(b.ReadyAt + grace, new PickGarden(at, b.Id, Overflow.StoreOnly)));
            foreach (Tree t in sim.State.Farm.Trees)
                if (t.RipeGrams > 0) items.Add(new KeyValuePair<long, Command>(t.RipeAt + grace, new PickFruit(at, t.SlotId, Overflow.StoreOnly)));
            // A stable sort: List.Sort isn't stable, so order by the deadline and the position in the list.
            var order = new List<int>();
            for (int i = 0; i < items.Count; i++) order.Add(i);
            order.Sort((a, b) => items[a].Key != items[b].Key ? items[a].Key.CompareTo(items[b].Key) : a.CompareTo(b));
            var result = new List<Command>();
            foreach (int i in order) result.Add(items[i].Value);
            return result;
        }
    }
}
