using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Farm
{
    /// <summary>
    /// Whole-acre orchards (spec 013 §3.3, ADR 0090): planted from the Hamlet on an empty field you own; the trees grow, bear and spoil
    /// by spec 003's rules per tree, a batch being the trees' fruit × the acre's Water, Care and Weather since the last; a short crop
    /// between the young rows at half its yield and labour; a bearing orchard shades each field beside it like one edge tree of its kind.
    /// </summary>
    public static class OrchardRules
    {
        public static bool On(Simulation sim) { return sim.State.Orchards != null && sim.Config.Game.Orchard != null; }

        public static OrchardDefs Defs(Simulation sim) { return sim.Config.Game.Orchard; }

        public static Orchard Of(Simulation sim, int tileId) { return On(sim) ? sim.State.Orchards.On(tileId) : null; }

        public static bool Unlocked(Simulation sim) { return On(sim) && (sim.State.Era == null || sim.State.Era.Current >= Defs(sim).UnlockEra); }

        /// <summary>The trees an acre of this tree is planted with, or 0 when it isn't planted by the acre.</summary>
        public static int TreesPerAcre(Simulation sim, int tree) { return On(sim) && Defs(sim).Trees[tree] != null ? Defs(sim).Trees[tree].TreesPerAcre : 0; }

        /// <summary>Its saplings with planting at today's input price (spec 003 §3.12).</summary>
        public static long Price(Simulation sim, int tree)
        {
            TreeDef def = sim.Config.Game.Farm.Trees[tree];
            return MarketRules.Value(sim, def.SaplingGood, TreesPerAcre(sim, tree), MarketRules.InputPrice(sim, def.SaplingGood));
        }

        /// <summary>Where an orchard may go: an acre you own with nothing growing, built or kept on it (the field's own irrigation stays).</summary>
        public static RejectReason Place(Simulation sim, int tileId)
        {
            if (!On(sim) || tileId < 0 || tileId >= sim.State.Land.Tiles.Count) return RejectReason.UnknownTile;
            if (!Unlocked(sim)) return RejectReason.EraLocked;
            Tile tile = sim.State.Land.Tiles[tileId];
            if (!sim.State.Land.Parcels[tile.ParcelId].Owned) return RejectReason.NotOwned;
            if (tile.Terrain != Terrain.Farm) return RejectReason.NotFarmTile;
            if (tile.Occupied || sim.State.Farm.CropOn(tileId) != null || Of(sim, tileId) != null || People.HouseRules.HousesOn(sim, tileId) > 0
                || Business.BusinessRules.On(sim, tileId) != null || Livestock.LivestockRules.SecondShedTile(sim) == tileId)
                return RejectReason.TileOccupied;
            foreach (Building b in sim.State.Land.Buildings)
                if (b.TileId == tileId && !IrrigationRules.IsIrrigation(sim, b)) return RejectReason.TileOccupied;
            return RejectReason.None;
        }

        /// <summary>A crop on this field: refused once the orchard bears, and only a short crop between young rows (ADR 0090 §3).</summary>
        public static RejectReason MaySow(Simulation sim, int tileId, int crop)
        {
            Orchard o = Of(sim, tileId);
            if (o == null) return RejectReason.None;
            if (o.State != TreeState.Growing) return RejectReason.TreesBearing;
            return Defs(sim).Intercrops[crop] ? RejectReason.None : RejectReason.NotAnIntercrop;
        }

        /// <summary>A crop's share of the acre's yield and labour on this field: the intercrop share between an orchard's rows, else 1.</summary>
        public static Fixed CropShare(Simulation sim, int tileId) { return Of(sim, tileId) != null ? Defs(sim).IntercropShare : Fixed.One; }

        /// <summary>Hours a day the orchard needs: young or bearing.</summary>
        public static Fixed Labour(Simulation sim, Orchard o)
        {
            OrchardTreeDef d = Defs(sim).Trees[o.Def];
            return o.State == TreeState.Growing ? d.LabourYoung : d.LabourBearing;
        }

        /// <summary>The trees' water factor at this moisture: spec 003's crop curve on the tree's band.</summary>
        public static Fixed WaterFactor(Simulation sim, Orchard o, Fixed moisture)
        {
            OrchardTreeDef d = Defs(sim).Trees[o.Def];
            return FarmRules.WaterFactor(sim.Config.Game.Farm.Farm, d.MoistureMin, d.MoistureMax, moisture);
        }

        /// <summary>The shade on a field from the bearing orchards beside it (ADR 0090 §1): each as one mature edge tree of its kind.</summary>
        public static Fixed Shade(Simulation sim, int tileId)
        {
            if (!On(sim) || sim.State.Orchards.Orchards.Count == 0) return Fixed.Zero;
            MapDef map = sim.Config.Game.Map;
            int x = tileId % map.Width, y = tileId / map.Width;
            Fixed shade = Fixed.Zero;
            foreach (Orchard o in sim.State.Orchards.Orchards)
            {
                if (o.State == TreeState.Growing) continue;
                int ox = o.TileId % map.Width, oy = o.TileId / map.Width;
                if (System.Math.Abs(ox - x) + System.Math.Abs(oy - y) == 1) shade += sim.Config.Game.Farm.Trees[o.Def].Shade;
            }
            return shade;
        }

        /// <summary>
        /// A bearing orchard with no fruit waiting gets a batch if this season is one of its fruiting seasons (spec 003 §3.12): the trees'
        /// fruit × the average Water, Care and Weather since the last batch (or the planting), capped as a crop's. A batch left past its
        /// grace is lost first (ADR 0034).
        /// </summary>
        internal static void Ripen(Simulation sim, Orchard o)
        {
            TreeDef def = sim.Config.Game.Farm.Trees[o.Def];
            long now = sim.State.Clock.SimTime;
            if (o.State != TreeState.Mature || !def.FruitingSeasons[(int)Calendar.SeasonAt(now, sim.Clock)]) return;
            if (o.RipeGrams > 0)
            {
                if (now - o.RipeAt <= sim.Config.Game.Farm.Farm.ReadyGraceSeconds) return;
                sim.Emit(new GoodsSpoiled(now, sim.Config.Game.Market.Goods[def.FruitGood].Id, o.RipeGrams, SpoilSource.Field, o.TileId));
                o.RipeGrams = 0;
            }
            long span = now - o.SumsFrom;
            Fixed water = span > 0 ? o.WaterSum.MulRatio(1, span) : o.WaterRate, care = span > 0 ? o.CareSum.MulRatio(1, span) : sim.State.Farm.CareRatio,
                weather = span > 0 ? o.WeatherSum.MulRatio(1, span) : o.WeatherRate;
            Fixed baseKg = Fixed.FromRatio(def.BatchGrams * o.Trees, 1000);
            o.Last = FarmRules.Yield(sim.Config.Game.Farm.Farm, baseKg, Fixed.One, water, care, Fixed.One, weather, Fixed.One, Fixed.One, Fixed.One);
            o.RipeGrams = o.Last.Grams;
            o.RipeAt = now;
            o.SumsFrom = now;
            o.WaterSum = o.CareSum = o.WeatherSum = Fixed.Zero;
            if (o.RipeGrams > 0) sim.Emit(new OrchardRipe(now, o.TileId, def.Id, o.RipeGrams));
        }

        /// <summary>
        /// How much of the ripe batch can be picked now (ADR 0090 §4): what fits the store, and with SellRestToTrader what the trader
        /// can still take today. The rest waits on the trees.
        /// </summary>
        public static long Pickable(Simulation sim, Orchard o, Overflow overflow)
        {
            int good = sim.Config.Game.Farm.Trees[o.Def].FruitGood;
            long room = Store.Fits(sim, good);
            if (overflow == Overflow.SellRestToTrader && room < o.RipeGrams) room += MarketRules.TraderCapacityLeft(sim, good);
            return System.Math.Min(o.RipeGrams, room);
        }

        /// <summary>What a fruit contractor pays for the ripe batch on the trees: the contractor's share of the trader's price (ADR 0090 §4).</summary>
        public static long ContractorPrice(Simulation sim, Orchard o)
        {
            int good = sim.Config.Game.Farm.Trees[o.Def].FruitGood;
            return Money.MulRate(MarketRules.Value(sim, good, o.RipeGrams, MarketRules.Floor(sim, good)), Defs(sim).ContractorShare);
        }

        /// <summary>
        /// Money in from a batch (picked or sold on the trees): counted for its payback; once the batch is gone it counts as a harvest, and
        /// banana and papaya are spent after their last.
        /// </summary>
        internal static void Earned(Simulation sim, Orchard o, long value)
        {
            o.Returns += value;
            if (o.PaidBackPeriod < 0 && o.Returns >= o.Cost) o.PaidBackPeriod = sim.Period;
            if (o.RipeGrams > 0) return;
            o.Batches++;
            int limit = sim.Config.Game.Farm.Trees[o.Def].HarvestLimit;
            if (limit > 0 && o.Batches >= limit)
            {
                FarmSystem.Accrue(sim);
                Dispose(sim, o, OrchardEnd.Spent);
            }
        }

        /// <summary>Removes an orchard (spent or cleared): its book value goes to 5600 Loss (spec 005 §3.3) and unpicked fruit with it.</summary>
        internal static void Dispose(Simulation sim, Orchard o, OrchardEnd end)
        {
            if (o.JobId != 0) sim.CompleteJob(sim.State.Jobs.Find(o.JobId));
            long t = sim.State.Clock.SimTime;
            if (o.Cost > 0)
                sim.State.Ledger.Post(t, sim.Period, end == OrchardEnd.Spent ? "ledger.orchardSpent" : "ledger.orchardCleared", "orchard:" + o.Id,
                    new Posting(Account.LossOnAssetSales, o.Cost), new Posting(Account.Trees, -o.Cost));
            sim.State.Orchards.Orchards.Remove(o);
            FarmSystem.Refresh(sim);
            sim.Emit(new OrchardGone(t, o.TileId, sim.Config.Game.Farm.Trees[o.Def].Id, end, o.Cost));
        }

        /// <summary>The orchards on a parcel sold or seized go with it at book value (like its irrigation, spec 013 §3.1); the money raised.</summary>
        internal static long Release(Simulation sim, int parcelId, long period, string key)
        {
            if (!On(sim)) return 0;
            long t = sim.State.Clock.SimTime, raised = 0;
            foreach (Orchard o in sim.State.Orchards.Orchards.ToArray())
            {
                if (sim.State.Land.Tiles[o.TileId].ParcelId != parcelId) continue;
                if (o.JobId != 0) sim.CompleteJob(sim.State.Jobs.Find(o.JobId));
                if (o.Cost > 0) sim.State.Ledger.Post(t, period, key, "orchard:" + o.Id, new Posting(Account.Cash, o.Cost), new Posting(Account.Trees, -o.Cost));
                raised += o.Cost;
                sim.State.Orchards.Orchards.Remove(o);
                sim.Emit(new OrchardGone(t, o.TileId, sim.Config.Game.Farm.Trees[o.Def].Id, OrchardEnd.WithTheLand, o.Cost));
            }
            return raised;
        }
    }
}
