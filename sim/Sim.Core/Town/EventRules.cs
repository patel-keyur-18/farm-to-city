using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Town
{
    /// <summary>
    /// The event engine (US-154, spec 027 §3.1–§3.4): one roll a day at 6:00 draws what happens to you, weighted by the town's state; a card gives two or three choices with
    /// what each costs, and a default that is taken when its clock runs out or you are away. Disasters are signalled two days ahead. Everything is a command or the clock,
    /// seeded by the day, so the same seed and commands give the same events.
    /// </summary>
    public static class EventRules
    {
        /// <summary>The flags an event can leave running, by code.</summary>
        public const int TractorParked = 1, SecondSupplier = 2, KitchenFixed = 3, Rush = 4, RushStocked = 5, CloseShops = 6, GrantDue = 7;

        public static EventsDef Def(Simulation sim) { return sim.Config.Game?.Events; }

        public static EventsState State(Simulation sim) { return sim.State.Township?.Events; }

        /// <summary>Events start at the Township (§3.1).</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && State(sim) != null && TownshipRules.Reached(sim); }

        private const long Day = ClockDef.SecondsPerDay;
        private const long Hour = 3600;

        public static long SeasonNow(Simulation sim) { return sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason; }

        private static long SeasonEnd(Simulation sim) { return (SeasonNow(sim) + 1) * sim.Clock.SecondsPerSeason; }

        private static Fixed Index(Simulation sim, long coins) { return Fixed.FromInt((int)coins); }

        // ---- the clock ----

        /// <summary>The engine's hourly pass: the weather log, the clocks, what has run its course, and at 6:00 a disaster's strike and the day's roll.</summary>
        internal static void Hourly(Simulation sim)
        {
            if (!On(sim)) return;
            EventsState s = State(sim);
            long now = sim.State.Clock.SimTime;
            Track(sim, s, now);
            ExpireCards(sim, s, now);
            ExpireEffects(sim, s, now);
            if (MarketRules.HourOfDay(sim) != 6) return;
            ContractRules.Sample(sim);
            Strike(sim, s, now);
            Roll(sim, s, now);
        }

        private static bool Weather(Simulation sim, string id) { return sim.CurrentWeather.Id == id; }

        /// <summary>The rain hours' log (§3.3): the last rain, the last day's heavy-rain hours, the last heat, and the index at each season's start.</summary>
        private static void Track(Simulation sim, EventsState s, long now)
        {
            if (s.LastHourAt == now) return;
            if (s.LastRainAt < 0) s.LastRainAt = now;
            s.HeavyMask = ((s.HeavyMask << 1) | (Weather(sim, "heavy_rain") ? 1L : 0L)) & 0xFFFFFFL;
            if (Weather(sim, "rain") || Weather(sim, "heavy_rain") || Weather(sim, "storm")) s.LastRainAt = now;
            if (Weather(sim, "heatwave")) s.LastHeatAt = now;
            if (now % sim.Clock.SecondsPerSeason == 0 || s.IndexThen == 0)
            {
                s.IndexBefore = s.IndexThen == 0 ? sim.State.Market.PriceIndex.Raw : s.IndexThen;
                s.IndexThen = sim.State.Market.PriceIndex.Raw;
            }
            s.LastHourAt = now;
        }

        private static void ExpireCards(Simulation sim, EventsState s, long now)
        {
            for (int i = 0; i < s.Waiting.Count; i++)
            {
                PendingEvent p = s.Waiting[i];
                if (p.ExpiresAt > now) continue;
                Answer(sim, p.Id, Def(sim).Events[p.Def].Default, true);
                i = -1; // the list changed: look again from the top
            }
        }

        private static void ExpireEffects(Simulation sim, EventsState s, long now)
        {
            for (int i = s.Effects.Count - 1; i >= 0; i--)
            {
                EventEffect e = s.Effects[i];
                if (e.Until > now) continue;
                s.Effects.RemoveAt(i);
                if (e.Kind == EventEffect.Epidemic) sim.Emit(new EpidemicEnded(now));
            }
        }

        // ---- the day's roll (§3.1) ----

        private static void Roll(Simulation sim, EventsState s, long now)
        {
            EventsDef d = Def(sim);
            if (s.StartedAt < 0) { s.StartedAt = now; return; }
            if (now < s.StartedAt + d.Engine.GraceDays * Day) return;
            bool away = sim.State.Clock.IsAway;
            if (!away && s.Waiting.Count >= d.Engine.MaxWaiting) return; // a third day's roll is skipped: nothing piles up
            if (PromiseDay(sim)) return;
            long day = now / Day;
            ulong seed = sim.State.Seed;
            if (!away && Rng.Chance(Rng.Next(seed, RngStream.Events, (ulong)day, 1), d.Engine.MajorChance))
                if (Draw(sim, s, true, day, now)) return; // a major came: no minor today
            if (Rng.Chance(Rng.Next(seed, RngStream.Events, (ulong)day, 2), d.Engine.MinorChance)) Draw(sim, s, false, day, now);
        }

        /// <summary>The close's petition promises fall due on a season's first day: no event on it (§3.1 mercy rule 3).</summary>
        private static bool PromiseDay(Simulation sim)
        {
            long now = sim.State.Clock.SimTime;
            if (now % sim.Clock.SecondsPerSeason >= Day) return false;
            TownLifeState life = TownLifeRules.State(sim);
            if (life == null) return false;
            foreach (TownPetition p in life.Petitions) if (p.Status == PetitionStatus.Promised) return true;
            return false;
        }

        private struct Candidate
        {
            public int Def;
            public int Weight;
            public long Target, Target2;
            public int Why;
        }

        private static bool Draw(Simulation sim, EventsState s, bool major, long day, long now)
        {
            EventsDef d = Def(sim);
            long season = SeasonNow(sim);
            if (major && s.MajorSeason == season) return false; // at most one major a season
            var list = new List<Candidate>();
            int total = 0;
            for (int i = 0; i < d.Events.Count; i++)
            {
                EventDef ev = d.Events[i];
                if (!ev.Rolled || ev.Major != major || s.LastSeasonOf(i) == season) continue;
                if (major && IsCropMajor(ev) && season <= s.CropQuietUntil) continue;
                if (ev.Disaster && s.DisasterDef >= 0) continue;
                if (!Weigh(sim, s, i, out Fixed w, out long t1, out long t2, out int why)) continue;
                if (major && DearestChoice(sim, ev, t1) > Money.MulRate(sim.State.Ledger.Cash, d.Engine.CashShareCap)) continue; // you can always afford the safe choice (§3.1 rule 4)
                int weight = (int)(w * Fixed.FromInt(ev.Disaster ? d.Engine.DisasterWeight : d.Engine.BaseWeight) * Fixed.FromInt(100)).RoundToLong();
                if (weight <= 0) continue;
                list.Add(new Candidate { Def = i, Weight = weight, Target = t1, Target2 = t2, Why = why });
                total += weight;
            }
            if (list.Count == 0) return false;
            var weights = new int[list.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = list[i].Weight;
            Candidate c = list[Rng.WeightedPick(Rng.Next(sim.State.Seed, RngStream.Events, (ulong)day, major ? 3UL : 4UL), weights)];
            Raise(sim, s, c.Def, c.Target, c.Target2, c.Why, now, major);
            return true;
        }

        public static bool IsCropMajor(EventDef ev) { return ev.Id == "drought" || ev.Id == "hailstorm" || ev.Id == "waterlogging"; }

        internal static void Raise(Simulation sim, EventsState s, int def, long t1, long t2, int why, long now, bool major)
        {
            EventsDef d = Def(sim);
            EventDef ev = d.Events[def];
            long season = SeasonNow(sim);
            s.SetLastSeason(def, season);
            if (major) s.MajorSeason = season;
            if (IsCropMajor(ev)) s.CropQuietUntil = season + 1;
            long hours = ev.Disaster ? d.Engine.WarningHours : major ? d.Engine.MajorHours : d.Engine.MinorHours;
            var p = new PendingEvent { Id = s.NextId++, Def = def, RaisedAt = now, ExpiresAt = now + hours * Hour, Target = t1, Target2 = t2, Why = why };
            if (ev.Id == "hailstorm") p.Param = Hail(sim, s, p);
            if (ev.Disaster)
            {
                s.DisasterDef = def;
                s.DisasterTarget = t1;
                s.DisasterTarget2 = t2;
                s.DisasterWarnedAt = now;
                s.DisasterStrikeAt = p.ExpiresAt;
                s.DisasterSlides = 0;
                s.DisasterChoice = -1;
                sim.Emit(new DisasterWarned(now, def, t1, p.ExpiresAt));
            }
            s.Waiting.Add(p);
            sim.Emit(new EventRaised(now, p.Id, def, major));
            if (sim.State.Clock.IsAway) Answer(sim, p.Id, ev.Default, true); // a minor while you are away takes its default at once, and goes on the away card
        }

        // ---- gates and weights (§3.3) ----

        private static List<Crop> Standing(Simulation sim)
        {
            var r = new List<Crop>();
            if (sim.State.Farm == null) return r;
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Growing && !sim.Config.Game.Farm.Crops[c.Def].Fodder) r.Add(c);
            return r;
        }

        /// <summary>A field no pump, borewell, flowing canal or drip waters, and that is not a well acre (§3.2).</summary>
        public static bool RainFed(Simulation sim, int tile)
        {
            if (LandSystem.IsWellTile(sim.Config.Game, tile)) return false;
            Fixed[] floors = IrrigationRules.Floors(sim);
            return floors[tile] == Fixed.Zero;
        }

        public static int RainFedAcres(Simulation sim)
        {
            int n = 0;
            Fixed[] floors = IrrigationRules.Floors(sim);
            foreach (Crop c in Standing(sim)) if (floors[c.TileId] == Fixed.Zero && !LandSystem.IsWellTile(sim.Config.Game, c.TileId)) n++;
            return n;
        }

        private static List<Household> Yours(Simulation sim) { return TownLifeRules.On(sim) ? TownLifeRules.Yours(sim) : new List<Household>(); }

        private static List<Business.Business> OpenBusinesses(Simulation sim)
        {
            var r = new List<Business.Business>();
            if (sim.State.Businesses == null) return r;
            foreach (Business.Business b in sim.State.Businesses.Businesses) if (b.Status == BusinessStatus.Open && !BusinessRules.TypeOf(sim, b).IsKiln) r.Add(b);
            return r;
        }

        private static bool Food(Simulation sim, Business.Business b) { BusinessTypeDef t = BusinessRules.TypeOf(sim, b); return !t.IsKiln && t.Id != "flour_mill"; }

        private static Fixed Mean(Simulation sim)
        {
            List<Household> hs = Yours(sim);
            if (hs.Count == 0) return Fixed.FromInt(100);
            long sum = 0;
            foreach (Household x in hs) sum += x.Happiness.RoundToLong();
            return Fixed.FromInt((int)(sum / hs.Count));
        }

        private static ulong Roll(Simulation sim, long salt, long key) { return Rng.Next(sim.State.Seed, RngStream.Events, (ulong)(sim.State.Clock.SimTime / Day), (ulong)(100 + salt * 1000 + key)); }

        private static int Pick(Simulation sim, int count, long salt) { return (int)Rng.Below(Roll(sim, salt, 0), (ulong)count); }

        /// <summary>
        /// Whether the event is open to you now and its weight's modifiers' product (before the clamp), what it falls on and which modifier made it likely (§3.3).
        /// </summary>
        public static bool Weigh(Simulation sim, EventsState s, int def, out Fixed weight, out long target, out long target2, out int why)
        {
            EventsDef d = Def(sim);
            EventDef ev = d.Events[def];
            Fixed prod = Fixed.One;
            target = 0; target2 = 0; why = 0;
            long now = sim.State.Clock.SimTime;
            Season season = Calendar.SeasonAt(now, sim.Clock);
            bool summer = season == Season.Summer;
            weight = Fixed.One;
            int whyCode = 0;
            void Mul(Fixed f, int code) { if (f != Fixed.One) { prod = prod * f; if (f > Fixed.One && whyCode == 0) whyCode = code; } }
            switch (ev.Id)
            {
                case "drought":
                    if (season != Season.Spring && !summer) return false;
                    if (now - s.LastRainAt < 3 * Day || RainFedAcres(sim) == 0) return false;
                    Mul(ev.Mod("dry3"), 1);
                    if (now - s.LastRainAt >= 6 * Day) Mul(ev.Mod("dry6"), 2);
                    break;
                case "heatwave":
                    if (!Weather(sim, "heatwave") || Standing(sim).Count == 0) return false;
                    if (summer) Mul(ev.Mod("summer"), 1);
                    break;
                case "hailstorm":
                    if (!Weather(sim, "storm") || season != Season.Spring && season != Season.Monsoon || Standing(sim).Count == 0) return false;
                    break;
                case "waterlogging":
                    if (season != Season.Monsoon || PopCount(s.HeavyMask) < (int)ev.Mod("heavyHours").RoundToLong() || Standing(sim).Count == 0) return false;
                    break;
                case "price_boom":
                case "price_slump":
                {
                    List<int> goods = CropGoods(sim);
                    if (goods.Count == 0) return false;
                    int good = goods[Pick(sim, goods.Count, def)];
                    target = good;
                    Fixed daily = sim.State.Market.DailyFactor[good];
                    if (ev.Id == "price_boom" ? daily < Fixed.One : daily > Fixed.One) Mul(ev.Mod(ev.Id == "price_boom" ? "belowNorm" : "aboveNorm"), 1);
                    break;
                }
                case "fuel_spike":
                    if (!PaysFuel(sim)) return false;
                    if (s.IndexBefore > 0 && Fixed.FromRaw(s.IndexThen) >= Fixed.FromRaw(s.IndexBefore) * (Fixed.One + ev.Mod("riseShare"))) Mul(ev.Mod("afterRise"), 1);
                    break;
                case "supplier_strike":
                {
                    List<Business.Business> bs = OpenBusinesses(sim);
                    if (bs.Count == 0) return false;
                    Business.Business b = bs[Pick(sim, bs.Count, def)];
                    target = b.Id;
                    if (Mean(sim) < ev.Mod("unhappyBelow")) Mul(ev.Mod("unhappy"), 1);
                    if (HasFlag(s, SecondSupplier, b.Id, now)) Mul(ev.Mod("afterSecond"), 2);
                    break;
                }
                case "health_inspection":
                {
                    List<Business.Business> bs = OpenBusinesses(sim).FindAll(x => Food(sim, x) && !HasFlag(s, KitchenFixed, x.Id, now));
                    if (bs.Count == 0) return false;
                    Business.Business b = bs[Pick(sim, bs.Count, def)];
                    target = b.Id;
                    if (b.Stars < ev.Mod("lowBelow")) Mul(ev.Mod("lowStars"), 1);
                    if (b.Quality >= 2) Mul(ev.Mod("goodQuality"), 2);
                    break;
                }
                case "viral_rush":
                {
                    List<Business.Business> bs = OpenBusinesses(sim).FindAll(x => BusinessRules.TypeOf(sim, x).IsShop);
                    if (bs.Count == 0) return false;
                    Business.Business b = bs[Pick(sim, bs.Count, def)];
                    target = b.Id;
                    Mul(Fixed.One + b.Stars / ev.Mod("starsDivisor"), 1);
                    break;
                }
                case "breakdown":
                {
                    var machines = new List<Machine>();
                    if (sim.State.Equipment != null) foreach (Machine m in sim.State.Equipment.Machines) if (!m.Leased && m.BrokenUntil <= now) machines.Add(m);
                    List<Business.Business> bs = OpenBusinesses(sim);
                    int n = machines.Count + bs.Count;
                    if (n == 0) return false;
                    int at = Pick(sim, n, def);
                    if (at < machines.Count)
                    {
                        target = machines[at].Id; target2 = 0;
                        if (!machines[at].Maintained) Mul(ev.Mod("skipped"), 1);
                    }
                    else { target = bs[at - machines.Count].Id; target2 = 1; }
                    break;
                }
                case "skilled_migrant":
                    if (sim.State.People == null || sim.State.People.Post == null) return false;
                    Mul(Fixed.One + Fixed.FromInt(sim.State.People.Reputation) / ev.Mod("reputationDivisor"), 1);
                    break;
                case "wedding":
                {
                    List<Household> hs = Yours(sim);
                    if (hs.Count == 0) return false;
                    target = hs[Pick(sim, hs.Count, def)].Id;
                    Fixed f = Fixed.FromInt(hs.Count) / ev.Mod("householdsDivisor");
                    Mul(f > ev.Mod("max") ? ev.Mod("max") : f, 1);
                    break;
                }
                case "dispute":
                {
                    if (!Pair(sim, def, out Household a, out Household b)) return false;
                    target = a.Id; target2 = b.Id;
                    TownLifeState life = TownLifeRules.State(sim);
                    if (life != null && life.RatioKnown && life.Ratio > Fixed.FromInt(TownLifeRules.Def(sim).HappinessAbove)) Mul(ev.Mod("inequality"), 1);
                    if (Mean(sim) < ev.Mod("unhappyBelow")) Mul(ev.Mod("unhappy"), 2);
                    break;
                }
                case "new_family":
                {
                    Building free = PopulationRules.FreeHouse(sim);
                    if (free == null || free.Builders) return false;
                    TownLifeState life = TownLifeRules.State(sim);
                    if (life != null && life.Read) Mul(life.Reputation[TownLifeDef.Livability] / ev.Mod("livabilityDivisor"), 1);
                    break;
                }
                case "regional_grant":
                {
                    IReadOnlyList<SeasonReport> reports = sim.State.Finance.Reports;
                    if (reports.Count == 0 || reports[reports.Count - 1].FarmProfit <= 0) return false;
                    TownLifeState life = TownLifeRules.State(sim);
                    if (life != null && life.Read) Mul(life.Reputation[TownLifeDef.Business] / ev.Mod("climateDivisor"), 1);
                    break;
                }
                case "land_auction":
                {
                    int parcel = AuctionParcel(sim);
                    if (parcel < 0) return false;
                    target = parcel;
                    break;
                }
                case "fire":
                {
                    var homes = new List<Building>();
                    foreach (Building b in sim.State.Land.Buildings) if (b.Owner == 0 && b.TileId >= 0 && b.Cost > 0) homes.Add(b);
                    List<Business.Business> shops = OpenBusinesses(sim).FindAll(x => InsuranceRules.BusinessBook(x) > 0); // a shop or mill burns like a house (§3.4)
                    if ((homes.Count == 0 && shops.Count == 0) || !summer || now - s.LastHeatAt > Day) return false;
                    long sum = 0;
                    foreach (Building b in homes) sum += b.BookValue;
                    foreach (Business.Business b in shops) sum += InsuranceRules.BusinessBook(b);
                    long r = (long)Rng.Below(Roll(sim, def, 1), (ulong)System.Math.Max(1, sum));
                    bool picked = false;
                    foreach (Building b in homes) { if (r < b.BookValue) { target = b.Id; picked = true; break; } r -= b.BookValue; }
                    if (!picked)
                        foreach (Business.Business b in shops) { long v = InsuranceRules.BusinessBook(b); if (r < v) { target = b.Id; target2 = 1; picked = true; break; } r -= v; }
                    if (!picked)
                    {
                        if (homes.Count > 0) target = homes[0].Id;
                        else { target = shops[0].Id; target2 = 1; }
                    }
                    Mul(ev.Mod("summer"), 1);
                    break;
                }
                case "epidemic":
                {
                    List<Household> hs = Yours(sim);
                    if (hs.Count == 0) return false;
                    bool noWater = false;
                    foreach (Household x in hs)
                    {
                        Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
                        if (home != null && home.TileId >= 0 && !UtilityRules.HasWater(sim, home.TileId)) noWater = true;
                    }
                    if (noWater) Mul(ev.Mod("noWater"), 1);
                    if (!ClinicRules.InPost(sim)) Mul(ev.Mod("noClinic"), 2);
                    break;
                }
                default:
                    return false;
            }
            Fixed clamped = prod < d.Engine.WeightMin ? d.Engine.WeightMin : prod > d.Engine.WeightMax ? d.Engine.WeightMax : prod;
            weight = clamped;
            why = whyCode;
            return true;
        }

        private static int PopCount(long mask) { int n = 0; while (mask != 0) { n += (int)(mask & 1); mask >>= 1; } return n; }

        /// <summary>The crops you hold in the store or grow now, by their output good, in good order.</summary>
        private static List<int> CropGoods(Simulation sim)
        {
            var goods = new List<int>();
            foreach (CropDef c in sim.Config.Game.Farm.Crops)
            {
                if (c.Fodder || goods.Contains(c.OutputGood)) continue;
                bool grows = Standing(sim).Exists(x => sim.Config.Game.Farm.Crops[x.Def].OutputGood == c.OutputGood);
                if (grows || sim.State.Market.InStore(c.OutputGood) > 0) goods.Add(c.OutputGood);
            }
            goods.Sort();
            return goods;
        }

        private static bool PaysFuel(Simulation sim)
        {
            if (sim.State.Equipment != null) foreach (Machine m in sim.State.Equipment.Machines) if (!m.Leased) return true;
            return IrrigationRules.On(sim) && (IrrigationRules.Pump(sim) != null || IrrigationRules.Borewell(sim) != null);
        }

        /// <summary>Two of your households whose homes are within a square's reach of each other, seeded.</summary>
        private static bool Pair(Simulation sim, int def, out Household a, out Household b)
        {
            a = b = null;
            List<Household> hs = Yours(sim);
            if (hs.Count < 2) return false;
            int start = Pick(sim, hs.Count, def);
            int reach = TownLifeRules.Def(sim).Festival.SquareReach;
            for (int i = 0; i < hs.Count; i++)
            {
                Household x = hs[(start + i) % hs.Count];
                Building bx = sim.State.Land.Buildings.Find(q => q.Id == x.HomeId);
                if (bx == null || bx.TileId < 0) continue;
                foreach (Household y in hs)
                {
                    if (y.Id == x.Id) continue;
                    Building by = sim.State.Land.Buildings.Find(q => q.Id == y.HomeId);
                    if (by == null || by.TileId < 0 || !CoverageRules.Within(sim, bx.TileId, by.TileId, reach)) continue;
                    a = x; b = y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The first parcel (by id) for sale beside your land that no builder has and that isn't locked, or −1.</summary>
        public static int AuctionParcel(Simulation sim)
        {
            for (int i = 0; i < sim.State.Land.Parcels.Count; i++)
            {
                Parcel p = sim.State.Land.Parcels[i];
                if (p.Owned || p.BuilderOwned || LandRules.Locked(sim, i) || !sim.Config.Game.Map.Parcels[i].ForSale || !LandSystem.IsAdjacentToOwned(sim, i)) continue;
                return i;
            }
            return -1;
        }

        public static long BidPrice(Simulation sim, int parcel)
        {
            Fixed share = Def(sim).Events[Def(sim).IndexOf("land_auction")].Mod("bidShare");
            return Money.MulRate(LandRules.ParcelPrice(sim, sim.State.Land.Parcels[parcel]), share);
        }

        // ---- costs (§3.2) ----

        /// <summary>What a choice costs now, in 1/100 coin: its cash effects at today's prices (a fire's bill is the bill).</summary>
        public static long ChoiceCost(Simulation sim, int def, int choice, PendingEvent p)
        {
            long total = 0;
            foreach (EffectDef e in Def(sim).Events[def].Choices[choice].Effects)
            {
                if (e.Kind == EffectKind.Cash) total += CashOf(sim, e, p);
                else if (e.Kind == EffectKind.Offer && e.What == "parcel" && p != null) total += BidPrice(sim, (int)p.Target);
                else if (e.Kind == EffectKind.Resow) total += ResowCost(sim);
            }
            return total;
        }

        private static long CashOf(Simulation sim, EffectDef e, PendingEvent p)
        {
            switch (e.Unit)
            {
                case "bill": return p == null ? 0 : p.Param;
                case "halfBill": return p == null ? 0 : Money.MulRate(p.Param, Def(sim).FireDef.SlowRepairShare);
                case "rainfedAcre": return MarketRules.Indexed(sim, e.Amount * Money.Coin * RainFedAcres(sim));
                case "field": return MarketRules.Indexed(sim, e.Amount * Money.Coin * Standing(sim).Count);
                case "person": return MarketRules.Indexed(sim, e.Amount * Money.Coin * FestivalRules.People(sim));
                default: return MarketRules.Indexed(sim, e.Amount * Money.Coin);
            }
        }

        private static long DearestChoice(Simulation sim, EventDef ev, long target)
        {
            long max = 0;
            var probe = new PendingEvent { Target = target };
            for (int c = 0; c < ev.Choices.Count; c++) max = System.Math.Max(max, ChoiceCost(sim, Def(sim).IndexOf(ev.Id), c, probe));
            return max;
        }

        /// <summary>The new seed for the damaged fields (hail's resow): each damaged crop's sowing seed at today's price.</summary>
        public static long ResowCost(Simulation sim)
        {
            long total = 0;
            foreach (Crop c in Damaged(sim)) total += FarmRules.SeedValue(sim, sim.Config.Game.Farm.Crops[c.Def]);
            return total;
        }

        /// <summary>Standing crops that hail has hit hardest (their event health is at or under the hail's factor).</summary>
        public static List<Crop> Damaged(Simulation sim)
        {
            Fixed hail = Def(sim).Events[Def(sim).IndexOf("hailstorm")].Mod("damage");
            return Standing(sim).FindAll(c => c.EventHealth <= hail);
        }

        /// <summary>Whether the choice can be taken now: its cash is there (a refusal in words, never a silent skip).</summary>
        public static bool Affordable(Simulation sim, int def, int choice, PendingEvent p) { return sim.State.Ledger.CanAfford(ChoiceCost(sim, def, choice, p)); }

        // ---- answering (§3.1) ----

        internal static RejectReason Answer(Simulation sim, int id, int choice, bool defaulted)
        {
            EventsState s = State(sim);
            if (s == null) return RejectReason.NoTownship;
            PendingEvent p = s.Waiting.Find(x => x.Id == id);
            if (p == null) return RejectReason.EventClosed;
            EventsDef d = Def(sim);
            EventDef ev = d.Events[p.Def];
            if (choice < 0 || choice >= ev.Choices.Count) return RejectReason.InvalidArgument;
            long now = sim.State.Clock.SimTime;
            long cost = ChoiceCost(sim, p.Def, choice, p);
            if (!defaulted && cost > 0 && !sim.State.Ledger.CanAfford(cost)) return RejectReason.InsufficientFunds;
            if (defaulted && cost > sim.State.Ledger.Cash) cost = System.Math.Max(0, sim.State.Ledger.Cash); // a repair that can't be paid in full is paid as far as it goes
            s.Waiting.Remove(p);
            if (cost > 0)
            {
                Account account = ev.Id == EventsDef.FireRepair ? Account.DisasterLoss : Account.EventCosts;
                sim.State.Ledger.Post(now, sim.Period, ev.Id == EventsDef.FireRepair ? "ledger.fireRepair" : "ledger.eventCost", "event:" + ev.Id, new Posting(account, cost), new Posting(Account.Cash, -cost));
                s.LossTotal += cost;
                if (ev.Id == EventsDef.FireRepair) InsuranceRules.OnRepair(sim, s, p, choice, cost, now); // an insured fire pays back its share of what was paid (spec 027 §3.6)
            }
            Apply(sim, s, p, ev, choice, now);
            long cash = -cost;
            s.History.Add(new EventRecord(p.Def, choice, defaulted, now, cash, p.Target));
            while (s.History.Count > d.Engine.KeepHistory) s.History.RemoveAt(0);
            if (ev.Disaster) s.DisasterChoice = choice;
            if (defaulted) sim.Emit(new EventDefaulted(now, p.Id, p.Def, choice, cash));
            else sim.Emit(new EventAnswered(now, p.Id, p.Def, choice, cash));
            return RejectReason.None;
        }

        private static void Apply(Simulation sim, EventsState s, PendingEvent p, EventDef ev, int choice, long now)
        {
            foreach (EffectDef e in ev.Choices[choice].Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.CropHealth:
                        foreach (Crop c in InFilter(sim, e.Filter, p)) { c.EventHealth = c.EventHealth * e.Factor; c.HitBy = p.Def + 1; }
                        break;
                    case EffectKind.PriceShift:
                        s.Effects.Add(new EventEffect { Kind = EventEffect.Price, Code = (int)p.Target, Percent = e.Percent, Until = now + e.Days * Day, Def = p.Def });
                        break;
                    case EffectKind.CostShift:
                    {
                        int percent = e.Percent;
                        if (e.Target == "inputs" && HasFlag(s, SecondSupplier, p.Target, now)) percent = (percent + 1) / 2; // a second supplier halves a strike
                        s.Effects.Add(new EventEffect { Kind = EventEffect.Cost, Code = e.Target == "fuel" ? EventEffect.Fuel : EventEffect.Inputs, Target = e.Target == "inputs" ? p.Target : 0, Percent = percent, Until = e.Days == 0 ? SeasonEnd(sim) + Hour : now + e.Days * Day, Def = p.Def });
                        break;
                    }
                    case EffectKind.Happiness:
                    {
                        long until = SeasonEnd(sim);
                        if (e.Who == "both" || e.Who == "one") s.Effects.Add(new EventEffect { Kind = EventEffect.Lift, Target = p.Target, Percent = e.Points, Until = until, Def = p.Def });
                        if (e.Who == "both" || e.Who == "other") s.Effects.Add(new EventEffect { Kind = EventEffect.Lift, Target = p.Target2, Percent = e.Points, Until = until, Def = p.Def });
                        break;
                    }
                    case EffectKind.Approval:
                    {
                        TownLifeState life = TownLifeRules.State(sim);
                        if (life != null && life.Read) life.Approval = Fixed.FromInt(System.Math.Max(0, System.Math.Min(100, (int)life.Approval.RoundToLong() + e.Points)));
                        break;
                    }
                    case EffectKind.Flag:
                    {
                        int code = FlagCode(e.Name);
                        long until = e.Days == 0 ? SeasonEnd(sim) : now + e.Days * Day;
                        s.Effects.Add(new EventEffect { Kind = EventEffect.Flag, Code = code, Target = code == KitchenFixed || code == SecondSupplier || code == Rush || code == RushStocked ? p.Target : 0, Until = until, Def = p.Def });
                        break;
                    }
                    case EffectKind.Downtime:
                    {
                        long until = now + e.Hours * Hour;
                        if (p.Target2 == 0 && e.Target == "machine" && sim.State.Equipment != null)
                        {
                            Machine m = sim.State.Equipment.Machines.Find(x => x.Id == p.Target);
                            if (m != null) m.BrokenUntil = System.Math.Max(m.BrokenUntil, until);
                        }
                        else
                        {
                            s.Effects.Add(new EventEffect { Kind = EventEffect.Shut, Code = EventEffect.ShutBusiness, Target = p.Target, Until = until, Def = p.Def });
                            if (ev.Id == "supplier_strike") InsuranceRules.OnPause(sim, p, e.Hours, now); // business insurance pays the lost days of a pause
                        }
                        break;
                    }
                    case EffectKind.Offer:
                        Offer(sim, e, p);
                        break;
                    case EffectKind.Grant:
                        s.Effects.Add(new EventEffect { Kind = EventEffect.Flag, Code = GrantDue, Target = MarketRules.Indexed(sim, e.Amount * Money.Coin), Until = SeasonEnd(sim) + 1, Def = p.Def });
                        break;
                    case EffectKind.Resow:
                        foreach (Crop c in Damaged(sim)) FarmRules.Resow(sim, c);
                        break;
                    case EffectKind.SellNow:
                        SellHeld(sim, (int)p.Target);
                        break;
                    case EffectKind.Mediate:
                        if (sim.State.Farm != null) // an hour of your day: the founder's working hours today are one less
                        {
                            FounderState f = sim.State.Farm.Founder;
                            f.TodayHours = f.TodayHours > Fixed.FromInt(e.Hours) ? f.TodayHours - Fixed.FromInt(e.Hours) : Fixed.Zero;
                        }
                        break;
                }
            }
            if (ev.Id == EventsDef.FireRepair) Repaired(sim, s, p, choice, now);
        }

        private static int FlagCode(string name)
        {
            switch (name)
            {
                case "tractorParked": return TractorParked;
                case "secondSupplier": return SecondSupplier;
                case "kitchenFixed": return KitchenFixed;
                case "rush": return Rush;
                case "rushStocked": return RushStocked;
                default: return 0;
            }
        }

        private static List<Crop> InFilter(Simulation sim, string filter, PendingEvent p)
        {
            List<Crop> all = Standing(sim);
            var r = new List<Crop>();
            if (filter == "all") return all;
            Fixed[] floors = filter == "rainfed" ? IrrigationRules.Floors(sim) : null;
            foreach (Crop c in all)
            {
                if (filter == "rainfed" && floors[c.TileId] == Fixed.Zero && !LandSystem.IsWellTile(sim.Config.Game, c.TileId)) r.Add(c);
                else if (filter == "wet" && sim.State.Land.Tiles[c.TileId].Moisture >= Fixed.FromInt(80)) r.Add(c);
            }
            return r;
        }

        /// <summary>Hail (no warning): a third of the standing fields, chosen by the day's roll, at the event's factor; returns how many.</summary>
        private static long Hail(Simulation sim, EventsState s, PendingEvent p)
        {
            EventDef ev = Def(sim).Events[p.Def];
            List<Crop> all = Standing(sim);
            int hit = (all.Count + (int)ev.Mod("share").RoundToLong() - 1) / (int)ev.Mod("share").RoundToLong();
            var order = new List<KeyValuePair<ulong, Crop>>();
            foreach (Crop c in all) order.Add(new KeyValuePair<ulong, Crop>(Rng.Next(sim.State.Seed, RngStream.Events, (ulong)(p.RaisedAt / Day), (ulong)(5000 + c.Id)), c));
            order.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.Id.CompareTo(b.Value.Id));
            for (int i = 0; i < hit && i < order.Count; i++) { order[i].Value.EventHealth = order[i].Value.EventHealth * ev.Mod("damage"); order[i].Value.HitBy = p.Def + 1; }
            return hit;
        }

        private static void Offer(Simulation sim, EffectDef e, PendingEvent p)
        {
            switch (e.What)
            {
                case "family":
                    MigrationRules.ArriveNow(sim);
                    break;
                case "worker":
                    PeopleSystem.ArriveSkilled(sim);
                    break;
                case "parcel":
                {
                    int id = (int)p.Target;
                    Parcel parcel = sim.State.Land.Parcels[id];
                    long price = BidPrice(sim, id);
                    long now = sim.State.Clock.SimTime;
                    if (parcel.Owned || !sim.State.Ledger.CanAfford(price)) break;
                    sim.State.Ledger.Post(now, sim.Period, "ledger.buyParcel", "parcel:" + sim.Config.Game.Map.Parcels[id].Id, new Posting(Account.Land, price), new Posting(Account.Cash, -price));
                    parcel.CostBasis = price;
                    LandSystem.Acquire(sim, parcel, now);
                    sim.Emit(new ParcelBought(now, sim.Config.Game.Map.Parcels[id].Id, price, sim.Config.Game.Map.Parcels[id].Tiles.Count));
                    break;
                }
            }
        }

        /// <summary>Sell what you hold of the good to the trader now, as far as he buys today (the price boom's second choice).</summary>
        private static void SellHeld(Simulation sim, int good)
        {
            long held = sim.State.Market.InStore(good);
            if (held <= 0) return;
            long room = MarketRules.TraderCapacityLeft(sim, good);
            long q = System.Math.Min(held, room);
            if (q > 0) Store.SellToTrader(sim, good, q, "event:held");
        }

        // ---- the effects' readers (the rest of the sim asks these) ----

        public static bool HasFlag(EventsState s, int code, long target, long now)
        {
            foreach (EventEffect e in s.Effects) if (e.Kind == EventEffect.Flag && e.Code == code && e.Until > now && (e.Target == target || target == 0 && e.Target == 0)) return true;
            return false;
        }

        /// <summary>Any flag of the code runs now (for a flag about the town, not a business).</summary>
        public static bool AnyFlag(Simulation sim, int code)
        {
            EventsState s = State(sim);
            if (s == null) return false;
            long now = sim.State.Clock.SimTime;
            foreach (EventEffect e in s.Effects) if (e.Kind == EventEffect.Flag && e.Code == code && e.Until > now) return true;
            return false;
        }

        /// <summary>The trader's price factor for a good: 1 + the percent of a boom or a slump (§3.2).</summary>
        public static Fixed PriceFactor(Simulation sim, int good)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return Fixed.One;
            long now = sim.State.Clock.SimTime;
            Fixed f = Fixed.One;
            foreach (EventEffect e in s.Effects) if (e.Kind == EventEffect.Price && e.Code == good && e.Until > now) f = f * (Fixed.One + Fixed.FromRatio(e.Percent, 100));
            return f;
        }

        /// <summary>Diesel and power's factor (a fuel spike), or a business's inputs' (a strike), 1 when nothing runs.</summary>
        public static Fixed CostFactor(Simulation sim, int target, long business)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return Fixed.One;
            long now = sim.State.Clock.SimTime;
            Fixed f = Fixed.One;
            foreach (EventEffect e in s.Effects)
                if (e.Kind == EventEffect.Cost && e.Code == target && e.Until > now && (target == EventEffect.Fuel || e.Target == business)) f = f * (Fixed.One + Fixed.FromRatio(e.Percent, 100));
            return f;
        }

        /// <summary>Points a household's happiness has from an event (a wedding's gift, a dispute) now.</summary>
        public static int Lift(Simulation sim, Household x)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return 0;
            long now = sim.State.Clock.SimTime;
            int n = 0;
            foreach (EventEffect e in s.Effects) if (e.Kind == EventEffect.Lift && e.Target == x.Id && e.Until > now) n += e.Percent;
            return n;
        }

        /// <summary>The business is shut by an event now: a supplier strike's pause or a fire's repair.</summary>
        public static bool Shut(Simulation sim, Business.Business b)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return false;
            long now = sim.State.Clock.SimTime;
            foreach (EventEffect e in s.Effects) if (e.Kind == EventEffect.Shut && e.Until > now && (e.Code == EventEffect.ShutBusiness && e.Target == b.Id || e.Code == EventEffect.ShutBuilding && BusinessOn(sim, b, e.Target))) return true;
            return false;
        }

        private static bool BusinessOn(Simulation sim, Business.Business b, long building)
        {
            Building x = sim.State.Land.Buildings.Find(q => q.Id == building);
            return x != null && x.TileId == b.TileId;
        }

        /// <summary>The shops' rise (+) or fall (−) from events: a viral rush for a day, a closure in an epidemic (§3.2, §3.4).</summary>
        public static Fixed ShopRise(Simulation sim)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return Fixed.Zero;
            Fixed r = Fixed.Zero;
            if (AnyFlag(sim, RushStocked)) r += Fixed.FromRatio(40, 100); // stocked up: everyone who comes is served
            else if (AnyFlag(sim, Rush)) r += Fixed.FromRatio(20, 100); // carrying on: the stock you have serves about half of them
            if (AnyFlag(sim, CloseShops)) r -= Def(sim).EpidemicDef.CloseShopsDemand;
            return r;
        }

        /// <summary>The epidemic's multiplier on a sick-day chance, 1 when none runs; where a staffed clinic covers the home it is also cut (§3.4).</summary>
        public static Fixed EpidemicFactor(Simulation sim, bool clinicCovers)
        {
            EventsState s = State(sim);
            if (s == null || s.Effects.Count == 0) return Fixed.One;
            long now = sim.State.Clock.SimTime;
            foreach (EventEffect e in s.Effects)
                if (e.Kind == EventEffect.Epidemic && e.Until > now) return Fixed.FromRatio(e.Percent, 100) * (clinicCovers ? Def(sim).EpidemicDef.ClinicFactor : Fixed.One);
            return Fixed.One;
        }

        public static bool EpidemicOn(Simulation sim)
        {
            EventsState s = State(sim);
            if (s == null) return false;
            long now = sim.State.Clock.SimTime;
            return s.Effects.Exists(e => e.Kind == EventEffect.Epidemic && e.Until > now);
        }

        // ---- the disasters (§3.4) ----

        /// <summary>At 6:00: a signalled disaster whose time has come strikes; if you were away it slides to your next live 6:00, at most <c>slideDays</c> days, then passes.</summary>
        private static void Strike(Simulation sim, EventsState s, long now)
        {
            if (s.DisasterDef < 0 || now < s.DisasterStrikeAt) return;
            EventsDef d = Def(sim);
            if (sim.State.Clock.IsAway)
            {
                s.DisasterSlides++;
                if (s.DisasterSlides > d.Engine.SlideDays)
                {
                    sim.Emit(new DisasterPassed(now, s.DisasterDef));
                    s.DisasterDef = -1;
                }
                return;
            }
            EventDef ev = d.Events[s.DisasterDef];
            if (ev.Id == EventsDef.Fire) FireStrikes(sim, s, now);
            else EpidemicStrikes(sim, s, now);
            s.DisasterDef = -1;
        }

        private static void FireStrikes(Simulation sim, EventsState s, long now)
        {
            EventsDef d = Def(sim);
            FireDef f = d.FireDef;
            bool business = s.DisasterTarget2 == 1;
            long id, book;
            int tile;
            if (business)
            {
                Business.Business shop = sim.State.Businesses?.Find(s.DisasterTarget);
                if (shop == null || shop.Status != BusinessStatus.Open) return; // closed or gone: nothing to burn
                id = shop.Id; tile = shop.TileId; book = InsuranceRules.BusinessBook(shop);
            }
            else
            {
                Building b = sim.State.Land.Buildings.Find(x => x.Id == s.DisasterTarget);
                if (b == null || b.TileId < 0) return; // sold or gone: nothing to burn
                id = b.Id; tile = b.TileId; book = b.BookValue;
            }
            bool tower = UtilityRules.Covers(sim, UtilityDef.Tower, tile);
            bool prepared = s.DisasterChoice == 1;
            Fixed share = f.DamageShare * (tower ? f.TowerFactor : Fixed.One) * (prepared ? f.PrepareFactor : Fixed.One);
            long bill = Money.MulRate(book, share);
            s.FireTarget = id;
            s.FireBill = bill;
            s.FireAt = now;
            s.FireInsured = InsuranceRules.CoversFire(sim, business, id, s.DisasterWarnedAt);
            s.Effects.Add(new EventEffect { Kind = EventEffect.Shut, Code = business ? EventEffect.ShutBusiness : EventEffect.ShutBuilding, Target = id, Until = now + f.DowntimeDays * Day, Def = d.IndexOf(EventsDef.Fire) });
            sim.Emit(new FireStruck(now, id, bill, tower, prepared, business));
            int repair = d.IndexOf(EventsDef.FireRepair);
            s.Waiting.Add(new PendingEvent { Id = s.NextId++, Def = repair, RaisedAt = now, ExpiresAt = now + d.Engine.RepairHours * Hour, Target = id, Target2 = business ? 1 : 0, Param = bill });
            sim.Emit(new EventRaised(now, s.Waiting[s.Waiting.Count - 1].Id, repair, false));
        }

        /// <summary>The repair card's answer: at once, the building is back after the usual days; slowly, six days out.</summary>
        private static void Repaired(Simulation sim, EventsState s, PendingEvent p, int choice, long now)
        {
            FireDef f = Def(sim).FireDef;
            if (choice != 1) return;
            int fire = Def(sim).IndexOf(EventsDef.Fire);
            int code = p.Target2 == 1 ? EventEffect.ShutBusiness : EventEffect.ShutBuilding;
            foreach (EventEffect e in s.Effects)
                if (e.Kind == EventEffect.Shut && e.Code == code && e.Target == p.Target && e.Def == fire) e.Until = System.Math.Max(e.Until, s.FireAt + f.SlowRepairDays * Day);
        }

        private static void EpidemicStrikes(Simulation sim, EventsState s, long now)
        {
            EpidemicDef e = Def(sim).EpidemicDef;
            Fixed factor = s.DisasterChoice == 1 ? e.CampFactor : s.DisasterChoice == 2 ? e.CloseFactor : e.SickFactor;
            long until = now + e.Days * Day;
            int def = Def(sim).IndexOf(EventsDef.Epidemic);
            s.Effects.Add(new EventEffect { Kind = EventEffect.Epidemic, Percent = (int)(factor * Fixed.FromInt(100)).RoundToLong(), Until = until, Def = def });
            if (s.DisasterChoice == 2) s.Effects.Add(new EventEffect { Kind = EventEffect.Flag, Code = CloseShops, Until = now + e.CloseDays * Day, Def = def });
            sim.Emit(new EpidemicBegan(now, (int)(factor * Fixed.FromInt(100)).RoundToLong(), until));
        }

        // ---- the grant (§3.2) ----

        /// <summary>At each close: a regional grant you applied for is paid if the books and credit are sound, else lapses with a word.</summary>
        internal static void AtClose(Simulation sim)
        {
            EventsState s = State(sim);
            if (s == null) return;
            long now = sim.State.Clock.SimTime;
            for (int i = s.Effects.Count - 1; i >= 0; i--)
            {
                EventEffect e = s.Effects[i];
                if (e.Kind != EventEffect.Flag || e.Code != GrantDue) continue;
                s.Effects.RemoveAt(i);
                IReadOnlyList<SeasonReport> reports = sim.State.Finance.Reports;
                bool sound = reports.Count > 0 && reports[reports.Count - 1].FarmProfit > 0 && !(sim.State.Credit != null && sim.State.Credit.Loans.Exists(l => l.State == LoanState.Defaulted));
                if (sound)
                    sim.State.Ledger.Post(now, sim.Period, "ledger.regionalGrant", "event:grant", new Posting(Account.Cash, e.Target), new Posting(Account.RegionalGrants, -e.Target));
                sim.Emit(new RegionalGrantPaid(now, sound ? e.Target : 0));
            }
        }
    }

    /// <summary>The engine's place in the processing order (spec 027 §3.1): last, after the treasury, so it reads the day's final state.</summary>
    internal sealed class EventsSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Events; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Hourly) EventRules.Hourly(sim);
            if (pass == ItemClass.Seasonal) EventRules.AtClose(sim);
        }
    }
}
