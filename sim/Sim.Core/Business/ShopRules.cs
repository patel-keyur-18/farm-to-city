using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Business
{
    /// <summary>Who buys at the shops (spec 017 §3.2): your town's households and the village's old families.</summary>
    public enum ShopGroup
    {
        Town = 0,
        Village = 1,
    }

    /// <summary>A shop's attraction to a customer group (spec 017 §3.3), part by part so the *Why?* can show each.</summary>
    public struct Attraction
    {
        public Fixed Price, Quality, Stars, Distance, Wait, Total;
        public int Metres;
        public Fixed WaitMinutes;
    }

    /// <summary>
    /// Shops and customer choice (spec 017): demand by group and kind, each shop's attraction, the hour's shares and sales (stock-outs
    /// passing customers on), the stalls' morning stock, the pitch rent, the mill's area customers against the chakki, and a shop's plan.
    /// </summary>
    public static class ShopRules
    {
        private const long Hour = 3600, Day = 86_400;
        private static readonly Fixed NeutralStars = Fixed.FromRatio(5, 2);

        public static ShopDefs Defs(Simulation sim) { return sim.Config.Game.Shops; }

        // ---- demand (§3.2) ----

        /// <summary>People in a group: your town's, or the village's old families (their products: the area the village shops serve).</summary>
        private static int PeopleIn(Simulation sim, ShopGroup g, bool product)
        {
            if (g == ShopGroup.Town) return sim.State.Citizens?.Population ?? 0;
            return product ? BusinessRules.Defs(sim).CatchmentPeople : Defs(sim).VillagePeople;
        }

        /// <summary>A vegetable's or fruit's share of a person's vegetables, or zero.</summary>
        public static Fixed VegShare(Simulation sim, int good)
        {
            foreach (KeyValuePair<int, Fixed> kv in Defs(sim).VegShares) if (kv.Key == good) return kv.Value;
            return Fixed.Zero;
        }

        /// <summary>What the village shops buy of one of your products a person a day of life (spec 016 §3.9c), or zero.</summary>
        public static Fixed ProductKg(Simulation sim, int good)
        {
            foreach (BusinessTypeDef t in BusinessRules.Defs(sim).Types)
                foreach (KeyValuePair<int, Fixed> kv in t.Shops) if (kv.Key == good) return kv.Value;
            return Fixed.Zero;
        }

        /// <summary>Whether a vegetable stall's good is one of your products (pav, paneer, curd: ADR 0091 §2), not produce from your store.</summary>
        public static bool IsProduct(Simulation sim, int good) { return VegShare(sim, good) == Fixed.Zero && ProductKg(sim, good) > Fixed.Zero; }

        /// <summary>The goods customers come for at a kind of shop, in good order: chai is one (−1), cups.</summary>
        public static List<int> KindGoods(Simulation sim, ShopKind kind)
        {
            var list = new List<int>();
            if (kind == ShopKind.Chai) { list.Add(-1); return list; }
            if (kind == ShopKind.Sweets)
            {
                foreach (KeyValuePair<int, Fixed> kv in Defs(sim).Kind(kind).References) list.Add(kv.Key);
            }
            else
            {
                foreach (KeyValuePair<int, Fixed> kv in Defs(sim).VegShares) list.Add(kv.Key);
                foreach (BusinessTypeDef t in BusinessRules.Defs(sim).Types)
                    if (t.Kind == ShopKind.Veg)
                        foreach (int g in t.Goods) if (!list.Contains(g) && IsProduct(sim, g)) list.Add(g);
            }
            list.Sort();
            return list;
        }

        /// <summary>
        /// A group's demand a real day (§3.2), grams (cups for chai): people × the kind's amount a person a day of life × days of life a
        /// real day; vegetables by each good's share, your products by what the village shops buy of them (ADR 0091 §2), sweets shared
        /// evenly between the sweets.
        /// </summary>
        public static long DemandPerDay(Simulation sim, ShopKind kind, int good, ShopGroup g)
        {
            ShopKindDef k = Defs(sim).Kind(kind);
            bool product = kind == ShopKind.Veg && IsProduct(sim, good);
            Fixed people = Fixed.FromInt(PeopleIn(sim, g, product)) * k.AdultShare * sim.Config.Game.Population.RealDaysPerGameDay;
            switch (kind)
            {
                case ShopKind.Chai: return (k.PerPerson * people).RoundToLong();
                case ShopKind.Sweets: return (k.PerPerson * people).MulRatio(1000, k.References.Count).RoundToLong();
                default: return ((product ? ProductKg(sim, good) : k.PerPerson * VegShare(sim, good)) * people).MulRatio(1000, 1).RoundToLong();
            }
        }

        /// <summary>The hour's share of a day's demand: customers come over the usual hours (8:00–20:00); none at other hours.</summary>
        public static long DemandThisHour(Simulation sim, ShopKind kind, int good, ShopGroup g, int hour)
        {
            ShopDefs d = Defs(sim);
            if (hour < d.OpenHour || hour >= d.CloseHour) return 0;
            return DemandPerDay(sim, kind, good, g) / (d.CloseHour - d.OpenHour);
        }

        // ---- prices (§3.3) ----

        /// <summary>
        /// The reference price a kg (a cup for chai, good −1) at today's prices: vegetables the local market's × the retail markup, your
        /// products and snacks the trader's × the markup (ADR 0091 §2), chai and sweets their reference × the index.
        /// </summary>
        public static Fixed Reference(Simulation sim, ShopKind kind, int good)
        {
            ShopDefs d = Defs(sim);
            Fixed index = sim.State.Market.PriceIndex;
            if (kind == ShopKind.Chai && good < 0) return d.Kind(kind).Reference * index;
            if (kind == ShopKind.Sweets)
                foreach (KeyValuePair<int, Fixed> kv in d.Kind(kind).References) if (kv.Key == good) return kv.Value * index;
            if (kind == ShopKind.Veg && VegShare(sim, good) > Fixed.Zero) return sim.State.Market.Price[good] * d.RetailMarkup;
            return MarketRules.Floor(sim, good) * d.RetailMarkup;
        }

        /// <summary>A shop's price for a good: the reference × its price setting.</summary>
        public static Fixed Price(Simulation sim, Business b, int good) { return Reference(sim, BusinessRules.TypeOf(sim, b).Kind, good) * b.PriceFactor; }

        // ---- where (§3.3, §3.4) ----

        /// <summary>The middle of your town's houses (§3.3): the square when you have no households yet.</summary>
        public static int TownCentre(Simulation sim) { return HousesMiddle(sim) ?? Defs(sim).SquareTile; }

        /// <summary>
        /// The middle of your land for the mill's area (ADR 0091 §3, owner 2026-11-05): the area's farm families live round your land, so
        /// the middle of your houses, or your yard before you have any.
        /// </summary>
        public static int LandCentre(Simulation sim)
        {
            int? houses = HousesMiddle(sim);
            if (houses.HasValue) return houses.Value;
            foreach (Land.Tile t in sim.State.Land.Tiles) if (t.Terrain == Terrain.Yard) return t.Id;
            return Defs(sim).SquareTile;
        }

        private static int? HousesMiddle(Simulation sim)
        {
            // Kept while the time and every household's home are the same (a cheap key: the homes' ids summed with their count).
            ShopsState s = sim.State.Shops;
            long homes = 0;
            if (sim.State.Citizens != null)
                foreach (People.Household h in sim.State.Citizens.Households) homes = homes * 31 + h.HomeId + 1;
            if (s != null && s.CentreAt == sim.State.Clock.SimTime && s.CentreHomes == homes) return s.CentreTile < 0 ? (int?)null : s.CentreTile;
            int? middle = WorkOutMiddle(sim);
            if (s != null)
            {
                s.CentreAt = sim.State.Clock.SimTime;
                s.CentreHomes = homes;
                s.CentreTile = middle ?? -1;
            }
            return middle;
        }

        private static int? WorkOutMiddle(Simulation sim)
        {
            int width = sim.Config.Game.Map.Width;
            long x = 0, y = 0, n = 0;
            if (sim.State.Citizens != null)
                foreach (People.Household h in sim.State.Citizens.Households)
                {
                    Land.Building home = sim.State.Land.Buildings.Find(b => b.Id == h.HomeId);
                    if (home == null) continue;
                    x += home.TileId % width;
                    y += home.TileId / width;
                    n++;
                }
            return n == 0 ? (int?)null : (int)(y / n) * width + (int)(x / n);
        }

        /// <summary>Metres between two tiles: an acre is 100 m, Manhattan.</summary>
        public static int Between(Simulation sim, int from, int to)
        {
            int width = sim.Config.Game.Map.Width;
            return (System.Math.Abs(from % width - to % width) + System.Math.Abs(from / width - to / width)) * Defs(sim).MetresPerAcre;
        }

        public static int Centre(Simulation sim, ShopGroup g) { return g == ShopGroup.Town ? TownCentre(sim) : Defs(sim).SquareTile; }

        /// <summary>Metres from a group's centre to a tile (an acre is 100 m, Manhattan), or to a place off the map past the square.</summary>
        public static int Metres(Simulation sim, ShopGroup g, int tile, int offMapMetres = 0)
        {
            return Between(sim, Centre(sim, g), tile < 0 ? Defs(sim).SquareTile : tile) + (tile < 0 ? offMapMetres : 0);
        }

        /// <summary>Minutes in the queue (§3.3) from last hour's customers over what it could serve: 10 × max(0, load − 0.8) ÷ 0.2.</summary>
        public static Fixed WaitMinutes(long arrivals, long capacity)
        {
            if (capacity <= 0 || arrivals <= 0) return Fixed.Zero;
            Fixed over = Fixed.FromRatio(arrivals, capacity) - Fixed.FromRatio(4, 5);
            return over <= Fixed.Zero ? Fixed.Zero : over * Fixed.FromInt(50);
        }

        /// <summary>
        /// The attraction (§3.3): max(floor, 1 + wPrice × (1 − price ÷ reference) + wQuality × (quality − 1) + wStars × (stars − 2.5) ÷ 2.5
        /// − wDistance × metres ÷ 100 − wWait × wait ÷ 10 minutes).
        /// </summary>
        public static Attraction Attract(Simulation sim, Fixed priceFactor, int quality, Fixed stars, int metres, Fixed waitMinutes)
        {
            ShopDefs d = Defs(sim);
            var a = new Attraction
            {
                Price = d.WPrice * (Fixed.One - priceFactor),
                Quality = d.WQuality * Fixed.FromInt(quality - 1),
                Stars = d.WStars * (stars - NeutralStars) / NeutralStars,
                Distance = -d.WDistance.MulRatio(metres, 100),
                Wait = -d.WWait.MulRatio(1, 10) * waitMinutes,
                Metres = metres, WaitMinutes = waitMinutes,
            };
            Fixed total = Fixed.One + a.Price + a.Quality + a.Stars + a.Distance + a.Wait;
            a.Total = total < d.AttractionFloor ? d.AttractionFloor : total;
            return a;
        }

        /// <summary>Your shop's attraction to a group now.</summary>
        public static Attraction Attract(Simulation sim, Business b, ShopGroup g)
        {
            return Attract(sim, b.PriceFactor, b.Quality, b.Stars, Metres(sim, g, b.TileId), WaitMinutes(b.LastArrivals, b.LastCapacity));
        }

        /// <summary>A rival's attraction to a group now.</summary>
        public static Attraction Attract(Simulation sim, Rival r, ShopGroup g)
        {
            RivalDef d = Defs(sim).Rivals[r.Def];
            return Attract(sim, r.PriceFactor, d.Quality, r.Stars, Metres(sim, g, d.Tile, d.Metres), WaitMinutes(r.LastArrivals, d.CapacityPerHour));
        }

        /// <summary>A shop's most this hour: its rate × its worker's share (spec 015 §3.4), cups or grams.</summary>
        public static long Capacity(Simulation sim, Business b)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            People.Worker w = BusinessRules.HiredWorker(sim, b);
            return w == null ? t.CapacityPerHour : People.WorkRules.RelativeOutput(sim, w).MulRatio(t.CapacityPerHour, 1).RoundToLong();
        }

        public static bool RivalOpen(RivalDef d, int hour) { return hour >= d.OpenHour && hour < d.CloseHour; }

        // ---- the mill and the chakki (§3.6) ----

        /// <summary>
        /// The area's share that grinds at your mill (§3.6): your attraction at your fee (against the chakki's as the reference), quality,
        /// stars and distance from the middle of your land (ADR 0091 §3), over yours and the chakki's (1.5 km away). Without shops content,
        /// spec 016's cliff.
        /// </summary>
        public static Fixed MillShare(Simulation sim, Fixed fee, int quality, Fixed stars, int tile)
        {
            BusinessDefs bd = BusinessRules.Defs(sim);
            ShopDefs d = Defs(sim);
            if (d == null) return fee <= bd.NextVillageFee + bd.FeeTolerance ? Fixed.One : Fixed.Zero;
            Fixed factor = bd.NextVillageFee == Fixed.Zero ? Fixed.One : fee / bd.NextVillageFee;
            int metres = tile < 0 ? 0 : Between(sim, LandCentre(sim), tile);
            Fixed yours = Attract(sim, factor, quality, stars, metres, Fixed.Zero).Total;
            return yours / (yours + Chakki(sim).Total);
        }

        public static Fixed MillShare(Simulation sim, Business b) { return MillShare(sim, b.Fee, b.Quality, b.Stars, b.TileId); }

        /// <summary>The chakki's attraction (§3.6): its fee is the reference, its quality and stars, 1.5 km from the middle of your land.</summary>
        public static Attraction Chakki(Simulation sim)
        {
            ShopDefs d = Defs(sim);
            return Attract(sim, Fixed.One, d.ChakkiQuality, d.ChakkiStars, d.ChakkiMetres, Fixed.Zero);
        }

        /// <summary>Your mill's attraction to the area, part by part (its *Why?*).</summary>
        public static Attraction MillAttraction(Simulation sim, Business b)
        {
            BusinessDefs bd = BusinessRules.Defs(sim);
            Fixed factor = bd.NextVillageFee == Fixed.Zero ? Fixed.One : b.Fee / bd.NextVillageFee;
            return Attract(sim, factor, b.Quality, b.Stars, Between(sim, LandCentre(sim), b.TileId), Fixed.Zero);
        }

        // ---- the hour (§3.3, §3.8) ----

        /// <summary>A shop open this hour: one of yours or a rival, with what's left of its rate.</summary>
        private sealed class Outlet
        {
            public Business B;
            public Rival R;
            public long CapLeft;
            public long Capacity;
            public long Arrivals, Served, Amount;
        }

        /// <summary>
        /// The hour's customers (§3.3, §3.8), after the market and the businesses' hour: for each kind of shop, each good and each group,
        /// the hour's demand at the shops' mean price is shared by attraction among the shops open with it in stock; a shop out of stock or
        /// full passes the rest of its share on; what nobody serves is lost. Your shops first by id, then the rivals.
        /// </summary>
        internal static void Hourly(Simulation sim)
        {
            ShopsState s = sim.State.Shops;
            if (s == null || sim.State.Businesses == null) return;
            long start = sim.State.Clock.SimTime - Hour;
            NewDay(sim, s, start / Day);
            int hour = (int)(start % Day / Hour);
            foreach (ShopKind kind in new[] { ShopKind.Veg, ShopKind.Chai, ShopKind.Sweets })
            {
                var outlets = new List<Outlet>();
                foreach (Business b in sim.State.Businesses.Businesses)
                    if (b.Status == BusinessStatus.Open && BusinessRules.TypeOf(sim, b).Kind == kind && BusinessRules.OpenAt(sim, b, hour))
                    {
                        long cap = Capacity(sim, b);
                        outlets.Add(new Outlet { B = b, CapLeft = cap, Capacity = cap });
                    }
                foreach (Rival r in s.Rivals)
                {
                    RivalDef d = Defs(sim).Rivals[r.Def];
                    if (d.Kind == kind && RivalOpen(d, hour)) outlets.Add(new Outlet { R = r, CapLeft = d.CapacityPerHour > 0 ? d.CapacityPerHour : long.MaxValue, Capacity = d.CapacityPerHour });
                }
                if (outlets.Count > 0)
                    foreach (int good in KindGoods(sim, kind))
                        foreach (ShopGroup g in new[] { ShopGroup.Town, ShopGroup.Village })
                            Share(sim, s, kind, good, g, DemandThisHour(sim, kind, good, g, hour), outlets);
                foreach (Outlet o in outlets) Close(sim, o);
                foreach (Business b in sim.State.Businesses.Businesses) // a shop shut this hour has no queue next hour
                    if (b.Status == BusinessStatus.Open && BusinessRules.TypeOf(sim, b).Kind == kind && !outlets.Exists(o => o.B == b)) b.LastArrivals = b.LastCapacity = 0;
                foreach (Rival r in s.Rivals) if (Defs(sim).Rivals[r.Def].Kind == kind && !outlets.Exists(o => o.R == r)) r.LastArrivals = 0;
            }
        }

        /// <summary>The day's figures start again at a new day; your town's buying at your stalls moves to yesterday's.</summary>
        private static void NewDay(Simulation sim, ShopsState s, long day)
        {
            if (s.Day == day) return;
            s.Day = day;
            System.Array.Copy(s.TownToday, s.TownYesterday, s.TownToday.Length);
            System.Array.Clear(s.TownToday, 0, s.TownToday.Length);
            System.Array.Clear(s.LostToday, 0, s.LostToday.Length);
            foreach (Rival r in s.Rivals) r.CustomersToday = 0;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                b.CustomersToday = 0;
                b.ServedToday = 0;
            }
        }

        /// <summary>What a shop can serve of a good now: its rate left, its stock (cups: its milk).</summary>
        private static long Limit(Simulation sim, Outlet o, ShopKind kind, int good)
        {
            if (o.R != null) return o.CapLeft;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, o.B);
            long stock = kind == ShopKind.Chai ? Milk(sim, o.B) / t.CupMilkMl : o.B.Stock[good];
            return System.Math.Min(o.CapLeft, stock);
        }

        /// <summary>A tea stall's milk of every kind, ml.</summary>
        public static long Milk(Simulation sim, Business b)
        {
            long ml = 0;
            var kinds = sim.Config.Game.Livestock?.Animals;
            if (kinds == null) return 0;
            foreach (Definitions.AnimalDef k in kinds)
            {
                int g = sim.Config.Game.Market.IndexOf("milk_" + k.Id);
                if (g >= 0) ml += b.Stock[g];
            }
            return ml;
        }

        private static Fixed PriceFactor(Outlet o) { return o.B != null ? o.B.PriceFactor : o.R.PriceFactor; }

        /// <summary>One good's hour for one group: the price response, the shares, the stock-outs, the sales.</summary>
        private static void Share(Simulation sim, ShopsState s, ShopKind kind, int good, ShopGroup g, long demand, List<Outlet> all)
        {
            if (demand <= 0) return;
            var open = new List<Outlet>();
            var attraction = new List<Fixed>();
            foreach (Outlet o in all)
            {
                if (Limit(sim, o, kind, good) <= 0) continue;
                open.Add(o);
                attraction.Add(o.B != null ? Attract(sim, o.B, g).Total : Attract(sim, o.R, g).Total);
            }
            if (open.Count == 0)
            {
                s.LostToday[(int)kind] += demand;
                return;
            }
            // The price response (§3.2): fewer buy when the shops' mean price (by attraction) is above the reference.
            Fixed sum = Fixed.Zero, weighted = Fixed.Zero;
            for (int i = 0; i < open.Count; i++)
            {
                sum += attraction[i];
                weighted += attraction[i] * PriceFactor(open[i]);
            }
            Fixed mean = weighted / sum;
            long total = mean == Fixed.One ? demand : Fixed.Pow(Fixed.One / mean, Defs(sim).Kind(kind).Elasticity).MulRatio(demand, 1).RoundToLong();

            // Shares by attraction, the remainder to the first; a shop that can't take its share passes the rest to those still serving.
            var got = new long[open.Count];
            var full = new bool[open.Count];
            long left = total;
            for (int round = 0; round < open.Count && left > 0; round++)
            {
                Fixed active = Fixed.Zero;
                int first = -1;
                for (int i = 0; i < open.Count; i++)
                    if (!full[i])
                    {
                        active += attraction[i];
                        if (first < 0) first = i;
                    }
                if (first < 0) break;
                var share = new long[open.Count];
                long given = 0;
                for (int i = 0; i < open.Count; i++)
                    if (!full[i])
                    {
                        share[i] = (attraction[i] / active).MulRatio(left, 1).FloorToLong();
                        given += share[i];
                    }
                share[first] += left - given;
                if (round == 0)
                    for (int i = 0; i < open.Count; i++) open[i].Arrivals += share[i]; // the customers who chose it, for its queue and its figures
                bool passed = false;
                long served = 0;
                for (int i = 0; i < open.Count; i++)
                {
                    if (full[i]) continue;
                    long can = Limit(sim, open[i], kind, good) - got[i];
                    long take = System.Math.Min(share[i], can);
                    got[i] += take;
                    served += take;
                    if (take < share[i]) { full[i] = true; passed = true; }
                }
                left -= served;
                if (!passed) break;
            }
            s.LostToday[(int)kind] += left;
            for (int i = 0; i < open.Count; i++)
            {
                Outlet o = open[i];
                if (got[i] <= 0) continue;
                o.CapLeft -= got[i];
                o.Served += got[i];
                if (o.B != null)
                {
                    o.Amount += Sell(sim, o.B, kind, good, got[i]);
                    if (g == ShopGroup.Town && kind == ShopKind.Veg) s.TownToday[good] += got[i];
                }
            }
        }

        /// <summary>The hour's figures kept for the next hour's queue and today's and this season's counts.</summary>
        private static void Close(Simulation sim, Outlet o)
        {
            if (o.R != null)
            {
                o.R.LastArrivals = o.Arrivals;
                o.R.CustomersToday += o.Arrivals;
                o.R.SeasonServed += o.Served;
                return;
            }
            Business b = o.B;
            b.LastArrivals = o.Arrivals;
            b.LastCapacity = o.Capacity;
            b.CustomersToday += o.Arrivals;
            b.ServedToday += o.Served;
            b.SeasonCustomers += o.Arrivals;
            b.SeasonServed += o.Served;
            BusinessBooks.Sweep(sim, b);
            if (o.Arrivals > 0) sim.Emit(new ShopSales(sim.State.Clock.SimTime, b.Id, o.Arrivals, o.Served, o.Amount));
        }

        /// <summary>
        /// Sells <paramref name="q"/> grams of a good (cups of chai) at the shop's price: its sales and cost of goods. A cup takes its milk
        /// (the dearest kind first) and its tea and sugar; a share of cups take a snack from its stock at the snack's price.
        /// </summary>
        private static long Sell(Simulation sim, Business b, ShopKind kind, int good, long q)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            if (kind != ShopKind.Chai)
            {
                long amount = BusinessRules.SalePrice(sim, b, MarketRules.Value(sim, good, q, Price(sim, b, good)));
                BusinessRules.Sold(sim, b, good, q, amount);
                return amount;
            }
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            long cups = BusinessRules.SalePrice(sim, b, Money.Value(q, Price(sim, b, -1), 1));
            if (cups > 0) b.Ledger.Post(now, sim.Period, "ledger.sales", reference, new Posting(Account.Cash, cups), new Posting(Account.Sales, -cups));
            long milk = q * t.CupMilkMl;
            foreach (int g in MilkGoods(sim))
            {
                long use = System.Math.Min(milk, b.Stock[g]);
                if (use <= 0) continue;
                Use(sim, b, g, use);
                milk -= use;
            }
            long supplies = Money.Value(q, t.CupSupplies * sim.State.Market.PriceIndex * BusinessRules.CostFactor(sim, b), 1);
            BusinessBooks.Draw(sim, b, supplies);
            if (supplies > 0) b.Ledger.Post(now, sim.Period, "ledger.supplies", reference, new Posting(Account.CostOfGoodsSold, supplies), new Posting(Account.Cash, -supplies));
            long snacks = Defs(sim).Kind(kind).SnackShare.MulRatio(q, 1).FloorToLong() * t.SnackGrams, total = cups;
            foreach (int g in t.Goods)
            {
                long take = System.Math.Min(snacks, b.Stock[g]);
                if (take <= 0) continue;
                long amount = BusinessRules.SalePrice(sim, b, MarketRules.Value(sim, g, take, Reference(sim, ShopKind.Veg, g) * b.PriceFactor));
                BusinessRules.Sold(sim, b, g, take, amount);
                total += amount;
                snacks -= take;
            }
            return total;
        }

        /// <summary>The milk goods, the dearest first (buffalo's).</summary>
        private static List<int> MilkGoods(Simulation sim)
        {
            var list = new List<int>();
            var kinds = sim.Config.Game.Livestock?.Animals;
            if (kinds == null) return list;
            var order = new List<int>();
            for (int k = 0; k < kinds.Count; k++) if (!kinds[k].Draught) order.Add(k);
            order.Sort((x, y) => { int c = Livestock.LivestockRules.MilkPrice(sim, y).CompareTo(Livestock.LivestockRules.MilkPrice(sim, x)); return c != 0 ? c : x.CompareTo(y); });
            foreach (int k in order) list.Add(sim.Config.Game.Market.IndexOf("milk_" + kinds[k].Id));
            return list;
        }

        /// <summary>Stock used up in what's sold, at its cost.</summary>
        private static void Use(Simulation sim, Business b, int good, long grams)
        {
            long cost = (long)((System.Numerics.BigInteger)b.StockCost[good] * grams / b.Stock[good]);
            b.Stock[good] -= grams;
            b.StockCost[good] -= cost;
            if (cost > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.costOfGoods", "business:" + b.Id, new Posting(Account.CostOfGoodsSold, cost), new Posting(Account.BusinessStock, -cost));
        }

        // ---- the morning (§5 SetStallGoods) ----

        /// <summary>Whether a business is a stall that's stocked each morning (the vegetable stall, the tea stall's snacks), not one that makes its goods.</summary>
        public static bool Stocked(BusinessTypeDef t) { return t.IsShop && !t.ByRecipe; }

        /// <summary>
        /// The stalls' 6:00 round, before your bakery and dairy sell to the village shops (§5, ADR 0091 §2): what's left from yesterday
        /// spoils by its rate, then each good is filled up to its amount a day, produce from your store at today's market price (an
        /// internal sale: the farm's sales), your products from your businesses at the trader's price, else (if allowed) the market's at
        /// its import price. Returns the grams of each good taken from your businesses (the village shops buy that much less).
        /// </summary>
        internal static long[] Stock(Simulation sim)
        {
            var took = new long[sim.Config.Game.Market.Goods.Count];
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                if (b.Status != BusinessStatus.Open || !Stocked(t)) continue;
                foreach (int g in t.Goods) if (b.Stock[g] > 0) BusinessRules.Spoil(sim, b, g, sim.Config.Game.Market.Goods[g].SpoilagePerDay.MulRatio(b.Stock[g], 1).RoundToLong());
                if (!BusinessRules.Staffed(sim, b)) continue; // nobody to take it out today
                foreach (int g in t.Goods)
                {
                    long need = b.PutOut[g] - b.Stock[g];
                    if (need <= 0) continue;
                    if (VegShare(sim, g) > Fixed.Zero) need -= FromStore(sim, b, g, need);
                    else need -= FromYours(sim, b, g, need, took);
                    if (need > 0 && b.BuyFromMarket && VegShare(sim, g) == Fixed.Zero) FromMarket(sim, b, g, need);
                }
                BusinessBooks.Sweep(sim, b);
            }
            return took;
        }

        /// <summary>Produce from your store at today's market price (the trader's, as spec 016's own wheat): Cr the farm's sales, Dr the stall's stock.</summary>
        private static long FromStore(Simulation sim, Business b, int g, long need)
        {
            long q = System.Math.Min(need, sim.State.Market.Stock[g] - sim.State.Market.Offered[g]);
            Fixed price = MarketRules.Floor(sim, g);
            q = System.Math.Min(q, Money.QuantityFor(System.Math.Max(0, BusinessBooks.CanSpend(sim, b)), price, 1000));
            if (q <= 0) return 0;
            long value = MarketRules.Value(sim, g, q, price), now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            BusinessBooks.Draw(sim, b, value);
            Store.Take(sim, g, q);
            if (value > 0)
            {
                Account sales = sim.Config.Game.Market.Goods[g].Sales == SalesKind.Fruit ? Account.FruitSales : Account.CropSales;
                sim.State.Ledger.Post(now, sim.Period, "ledger.soldToBusiness", reference, new Posting(Account.Cash, value), new Posting(sales, -value));
                sim.State.Finance.Sales.Add(sim, g, SoldTo.Business, value);
                b.Ledger.Post(now, sim.Period, "ledger.boughtFromFarm", reference, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
            }
            b.Stock[g] += q;
            b.StockCost[g] += value;
            return q;
        }

        /// <summary>Your product from your businesses that make it, in id order, at the trader's price (an internal sale, as the mill's atta).</summary>
        private static long FromYours(Simulation sim, Business to, int g, long need, long[] took)
        {
            long got = 0;
            foreach (Business from in sim.State.Businesses.Businesses)
            {
                if (from == to || from.Status != BusinessStatus.Open || from.Stock[g] <= 0 || !BusinessRules.TypeOf(sim, from).Makes(g)) continue;
                Fixed price = MarketRules.Floor(sim, g);
                long q = System.Math.Min(System.Math.Min(need - got, from.Stock[g]), Money.QuantityFor(System.Math.Max(0, BusinessBooks.CanSpend(sim, to)), price, 1000));
                if (q <= 0) continue;
                long value = MarketRules.Value(sim, g, q, price);
                BusinessBooks.Draw(sim, to, value);
                to.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtFromBusiness", "business:" + to.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                to.Stock[g] += q;
                to.StockCost[g] += value;
                BusinessRules.Sold(sim, from, g, q, value);
                BusinessBooks.Sweep(sim, from);
                took[g] += q;
                got += q;
            }
            return got;
        }

        /// <summary>A snack or product bought at the market's import price, as far as its cash goes.</summary>
        private static void FromMarket(Simulation sim, Business b, int g, long need)
        {
            Fixed price = MarketRules.Ceiling(sim, g);
            long q = System.Math.Min(need, Money.QuantityFor(System.Math.Max(0, BusinessBooks.CanSpend(sim, b)), price, 1000));
            long value = MarketRules.Value(sim, g, q, price);
            if (q <= 0 || value <= 0) return;
            BusinessBooks.Draw(sim, b, value);
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtAtMarket", "business:" + b.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
            b.Stock[g] += q;
            b.StockCost[g] += value;
        }

        // ---- the season (§3.4) ----

        /// <summary>A pitch's rent for the season ahead (§3.4), paid upfront to the council: Dr Rent, Cr Cash, on its overdraft if need be.</summary>
        internal static void PayRent(Simulation sim, Business b, long period)
        {
            if (b.Pitch < 0 || Defs(sim) == null) return;
            long rent = MarketRules.Indexed(sim, Defs(sim).PitchRent);
            BusinessBooks.Draw(sim, b, rent, period);
            if (rent > 0) b.Ledger.Post(sim.State.Clock.SimTime, period, "ledger.pitchRent", "business:" + b.Id, new Posting(Account.Rent, rent), new Posting(Account.Cash, -rent));
        }

        /// <summary>The season's counts start again at the close.</summary>
        internal static void CloseSeason(Simulation sim)
        {
            if (sim.State.Shops == null) return;
            foreach (Rival r in sim.State.Shops.Rivals) r.SeasonServed = 0;
        }

        // ---- the plan (§3.1, spec 016 §3.3) ----

        /// <summary>
        /// A new shop's expected share of a group (the plan's): its attraction at the reference, standard, the starting stars and no queue,
        /// against the rivals of its kind (your other shops aren't counted).
        /// </summary>
        public static Fixed ExpectedShare(Simulation sim, ShopKind kind, ShopGroup g, int tile)
        {
            Fixed yours = Attract(sim, Fixed.One, 1, Defs(sim).StartStars, Metres(sim, g, tile), Fixed.Zero).Total, all = yours;
            foreach (Rival r in sim.State.Shops.Rivals) if (Defs(sim).Rivals[r.Def].Kind == kind) all += Attract(sim, r, g).Total;
            return yours / all;
        }

        /// <summary>What a shop at <paramref name="tile"/> would sell a day of a good (cups of chai): each group's demand × its expected share.</summary>
        public static long ExpectedPerDay(Simulation sim, ShopKind kind, int good, int tile)
        {
            long n = 0;
            foreach (ShopGroup g in new[] { ShopGroup.Town, ShopGroup.Village }) n += ExpectedShare(sim, kind, g, tile).MulRatio(DemandPerDay(sim, kind, good, g), 1).FloorToLong();
            return n;
        }

        /// <summary>The square's tile for a pitch, or the acre.</summary>
        public static int SiteTile(Simulation sim, int pitch, int tile) { return pitch >= 0 ? Defs(sim).SquareTile : tile; }

        /// <summary>A stall's goods a day to start with (the plan's): what it would sell of each (the tea stall's snack: the first one, for its cups).</summary>
        public static long[] DefaultPutOut(Simulation sim, BusinessTypeDef t, int tile)
        {
            var put = new long[sim.Config.Game.Market.Goods.Count];
            if (t.Kind == ShopKind.Veg)
                foreach (int g in t.Goods) put[g] = ExpectedPerDay(sim, ShopKind.Veg, g, tile);
            if (t.Kind == ShopKind.Chai && t.Goods.Count > 0)
                put[t.Goods[0]] = Defs(sim).Kind(ShopKind.Chai).SnackShare.MulRatio(ExpectedPerDay(sim, ShopKind.Chai, -1, tile), 1).FloorToLong() * t.SnackGrams;
            return put;
        }

        /// <summary>The sweet shop's day's batches to start with: what it would sell of each sweet.</summary>
        public static long[] DefaultMix(Simulation sim, BusinessTypeDef t, int tile)
        {
            var mix = new long[sim.Config.Game.Market.Goods.Count];
            foreach (int p in t.Products) mix[p] = ExpectedPerDay(sim, ShopKind.Sweets, p, tile);
            return mix;
        }

        /// <summary>The tea stall's milk a day to start with: its cups' milk.</summary>
        public static long DefaultMilk(Simulation sim, BusinessTypeDef t, int tile) { return ExpectedPerDay(sim, ShopKind.Chai, -1, tile) * t.CupMilkMl; }

        /// <summary>The building on its site: on a pitch, the type's pitch building when it has one (the vegetable stall's handcart).</summary>
        public static long BuildingValue(BusinessTypeDef t, int pitch) { return pitch >= 0 && t.PitchBuildingValue >= 0 ? t.PitchBuildingValue : t.BuildingValue; }

        public static int BuildingLife(BusinessTypeDef t, int pitch) { return pitch >= 0 && t.PitchBuildingValue >= 0 ? t.PitchBuildingLifeSeasons : t.BuildingLifeSeasons; }

        /// <summary>
        /// A shop's season ahead (the plan, §3.1): what it would sell at the reference (your expected share of each group's demand), its
        /// goods at what they'd cost it (produce and products at the trader's price, your milk at the co-op's, a cup's tea and sugar,
        /// the sweets by their recipes), against the wage, the pitch's rent, repairs and wear; the cash a day's stock and the rent need.
        /// </summary>
        internal static BusinessRules.Projection Projected(Simulation sim, BusinessTypeDef t, bool runYourself, long loan, int pitch, int tile)
        {
            int days = sim.Clock.DaysPerSeason;
            int at = SiteTile(sim, pitch, tile);
            long sales = 0, inputs = 0, fuel = 0;
            foreach (int good in KindGoods(sim, t.Kind))
            {
                if (t.Kind == ShopKind.Veg && !t.Sells(good)) continue;
                if (t.Kind == ShopKind.Sweets && !t.Makes(good)) continue;
                long q = ExpectedPerDay(sim, t.Kind, good, at) * days;
                if (t.Kind == ShopKind.Chai)
                {
                    sales += Money.Value(q, Reference(sim, t.Kind, -1), 1);
                    inputs += Money.Value(q * t.CupMilkMl, MilkCost(sim, t), 1000) + Money.Value(q, t.CupSupplies * sim.State.Market.PriceIndex, 1);
                    long snack = Defs(sim).Kind(t.Kind).SnackShare.MulRatio(q, 1).FloorToLong() * t.SnackGrams;
                    if (snack > 0 && t.Goods.Count > 0)
                    {
                        int g = t.Goods[0];
                        sales += MarketRules.Value(sim, g, snack, Reference(sim, ShopKind.Veg, g));
                        inputs += MarketRules.Value(sim, g, snack, BusinessRules.InputPrice(sim, g));
                    }
                    continue;
                }
                sales += MarketRules.Value(sim, good, q, Reference(sim, t.Kind, good));
                if (t.Kind == ShopKind.Sweets)
                {
                    RecipeDef r = BusinessRules.First(t, good);
                    Fixed unit = BusinessRules.UnitCost(sim, t, good), fuelUnit = r.Fuel * sim.State.Market.PriceIndex;
                    inputs += MarketRules.Value(sim, good, q, unit - fuelUnit.MulRatio(1000, r.OutputGrams));
                    fuel += Money.Value(q, fuelUnit, r.OutputGrams);
                }
                else inputs += MarketRules.Value(sim, good, q, MarketRules.Floor(sim, good));
            }
            long building = MarketRules.Indexed(sim, BuildingValue(t, pitch)), equipment = MarketRules.Indexed(sim, t.EquipmentValue);
            long repairs = Money.MulRate(equipment, t.RepairsShare);
            long depreciation = building / BuildingLife(t, pitch) + equipment / t.EquipmentLifeSeasons;
            long wage = runYourself ? 0 : BusinessRules.Wage(sim, t);
            long rent = pitch >= 0 ? MarketRules.Indexed(sim, Defs(sim).PitchRent) : 0;
            long interest = loan <= 0 || sim.State.Credit == null ? 0
                : Money.MulRate(loan, CreditRules.Rate(sim.Config.Game.Credit.Lenders[sim.Config.Game.Credit.IndexOfLender(BusinessRules.Defs(sim).LenderId)], sim.State.Credit.Score));
            long costs = inputs + fuel + repairs + depreciation + wage + rent + interest;
            long aDay = (inputs + fuel + days - 1) / days;
            return new BusinessRules.Projection
            {
                Sales = sales, Wheat = inputs, Power = fuel, Repairs = repairs, Depreciation = depreciation, Wage = wage, Rent = rent, Interest = interest, Loan = loan,
                Revenue = sales, Costs = costs, Profit = sales - costs, Cost = building + equipment,
                CashNeeded = building + equipment + (runYourself ? repairs : wage) + rent + aDay,
            };
        }

        /// <summary>A litre of the tea stall's milk as the plan sees it: your herd's at the co-op's price while it lasts, else neighbours'.</summary>
        private static Fixed MilkCost(Simulation sim, BusinessTypeDef t)
        {
            long[] herd = BusinessRules.HerdMilk(sim);
            var kinds = sim.Config.Game.Livestock.Animals;
            for (int k = 0; k < kinds.Count; k++) if (!kinds[k].Draught && herd[k] > 0) return Livestock.LivestockRules.MilkPrice(sim, k);
            return BusinessRules.NeighbourMilkPrice(sim, t);
        }
    }
}
