using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Land
{
    /// <summary>
    /// The irrigation ladder (spec 013 §3.1, ADR 0086, ADR 0087). The pump on the well and the borewell each water the acres on
    /// their lists (one pumped source an acre) at their floor with no buckets; a canal channel waters its acre at the canal's
    /// floor except in the canal's dry seasons, if it reaches the canal through channelled acres you own; drip on a pumped acre
    /// holds its crop at the crop's lowest good moisture, saves care and adds to fertiliser's yield. Running costs are paid at
    /// each close; unpaid, that source is off for the season.
    /// </summary>
    public static class IrrigationRules
    {
        public static IrrigationDef Def(Simulation sim) { return sim.Config.Game?.Irrigation; }

        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Irrigation != null; }

        /// <summary>From the era the step comes with.</summary>
        public static bool Unlocked(Simulation sim, int era) { return sim.State.Era == null || sim.State.Era.Current >= era; }

        /// <summary>The first building of a kind (the pump, the borewell), or null.</summary>
        public static Building Find(Simulation sim, int def)
        {
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def) return b;
            return null;
        }

        /// <summary>A building of a kind on the tile (a channel, drip), or null.</summary>
        public static Building On(Simulation sim, int def, int tile)
        {
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def && b.TileId == tile) return b;
            return null;
        }

        public static Building Pump(Simulation sim) { return On(sim) ? Find(sim, Def(sim).Pump.Building) : null; }
        public static Building Borewell(Simulation sim) { return On(sim) ? Find(sim, Def(sim).Borewell.Building) : null; }
        public static bool Channelled(Simulation sim, int tile) { return On(sim) && On(sim, Def(sim).Canal.Building, tile) != null; }
        public static bool HasDrip(Simulation sim, int tile) { return On(sim) && On(sim, Def(sim).Drip.Building, tile) != null; }

        /// <summary>The acres a pumped source waters: 1 the pump, 2 the borewell.</summary>
        public static List<int> Acres(Simulation sim, int source) { return source == 1 ? sim.State.Irrigation.PumpAcres : sim.State.Irrigation.BorewellAcres; }

        /// <summary>Which pumped source has the tile on its list: 0 none, 1 the pump, 2 the borewell.</summary>
        public static int SourceOf(Simulation sim, int tile)
        {
            if (!On(sim)) return 0;
            if (sim.State.Irrigation.PumpAcres.Contains(tile)) return 1;
            if (sim.State.Irrigation.BorewellAcres.Contains(tile)) return 2;
            return 0;
        }

        /// <summary>The source is built and its power paid.</summary>
        public static bool Running(Simulation sim, int source)
        {
            if (!On(sim)) return false;
            return source == 1 ? Pump(sim) != null && !sim.State.Irrigation.PumpUnpaid
                : source == 2 && Borewell(sim) != null && !sim.State.Irrigation.BorewellUnpaid;
        }

        public static bool Pumped(Simulation sim, int tile)
        {
            int source = SourceOf(sim, tile);
            return source != 0 && Running(sim, source);
        }

        /// <summary>The canal's channels carry water: the fee paid and not a dry season (the canal closed for repairs).</summary>
        public static bool CanalFlowing(Simulation sim)
        {
            return On(sim) && !sim.State.Irrigation.CanalUnpaid && !Def(sim).Canal.DrySeasons[(int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock)];
        }

        public static bool DripRunning(Simulation sim, int tile) { return Pumped(sim, tile) && !sim.State.Irrigation.DripUnpaid && HasDrip(sim, tile); }

        /// <summary>The tile shares an edge with the minor canal (ADR 0087 §2: the edge below its row, between its columns).</summary>
        public static bool BordersCanal(Simulation sim, int tile)
        {
            if (!On(sim)) return false;
            CanalDef c = Def(sim).Canal;
            int w = sim.Config.Game.Map.Width, x = tile % w, y = tile / w;
            return x >= c.FromColumn && x <= c.ToColumn && (y == c.Row || y == c.Row + 1);
        }

        /// <summary>
        /// The channelled acres the canal's water reaches: those on the canal, then any channelled acre next to one reached
        /// (a channel cut off by an acre sold carries no water).
        /// </summary>
        public static bool[] Reached(Simulation sim)
        {
            int n = sim.State.Land.Tiles.Count;
            var reached = new bool[n];
            if (!On(sim)) return reached;
            int def = Def(sim).Canal.Building, w = sim.Config.Game.Map.Width;
            var channel = new bool[n];
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def && b.TileId >= 0) channel[b.TileId] = true;
            var queue = new Queue<int>();
            for (int t = 0; t < n; t++) if (channel[t] && BordersCanal(sim, t)) { reached[t] = true; queue.Enqueue(t); }
            while (queue.Count > 0)
            {
                int t = queue.Dequeue(), x = t % w;
                foreach (int m in new[] { t - w, x > 0 ? t - 1 : -1, x < w - 1 ? t + 1 : -1, t + w })
                    if (m >= 0 && m < n && channel[m] && !reached[m]) { reached[m] = true; queue.Enqueue(m); }
            }
            return reached;
        }

        /// <summary>A channel can be dug on the acre: it borders the canal or a channelled acre the water reaches.</summary>
        public static bool CanReach(Simulation sim, int tile)
        {
            if (BordersCanal(sim, tile)) return true;
            bool[] reached = Reached(sim);
            int w = sim.Config.Game.Map.Width, x = tile % w, n = reached.Length;
            foreach (int m in new[] { tile - w, x > 0 ? tile - 1 : -1, x < w - 1 ? tile + 1 : -1, tile + w })
                if (m >= 0 && m < n && reached[m]) return true;
            return false;
        }

        /// <summary>
        /// Per tile, the moisture irrigation holds it at with no labour (0 for none): the pumped source's floor, the canal's while it
        /// flows, and drip's, the growing crop's lowest good moisture; the highest applies (spec 013 §3.1).
        /// </summary>
        public static Fixed[] Floors(Simulation sim)
        {
            var floors = new Fixed[sim.State.Land.Tiles.Count];
            if (!On(sim)) return floors;
            IrrigationDef d = Def(sim);
            IrrigationState s = sim.State.Irrigation;
            bool channels = false;
            foreach (Building b in sim.State.Land.Buildings) channels |= b.Def == d.Canal.Building;
            if (s.PumpAcres.Count == 0 && s.BorewellAcres.Count == 0 && !channels) return floors; // rain-fed and buckets: most hours of most towns
            if (Running(sim, 1)) foreach (int t in s.PumpAcres) Raise(floors, t, d.Pump.MoistureFloor);
            if (Running(sim, 2)) foreach (int t in s.BorewellAcres) Raise(floors, t, d.Borewell.MoistureFloor);
            if (channels && CanalFlowing(sim))
            {
                bool[] reached = Reached(sim);
                for (int t = 0; t < reached.Length; t++) if (reached[t]) Raise(floors, t, d.Canal.MoistureFloor);
            }
            if (!s.DripUnpaid && sim.State.Farm != null)
                foreach (Building b in sim.State.Land.Buildings)
                {
                    if (b.Def != d.Drip.Building || !Pumped(sim, b.TileId)) continue;
                    Farm.Crop c = sim.State.Farm.CropOn(b.TileId);
                    if (c != null && c.State == Farm.CropState.Growing) Raise(floors, b.TileId, sim.Config.Game.Farm.Crops[c.Def].MoistureMin);
                }
            return floors;
        }

        private static void Raise(Fixed[] floors, int tile, Fixed floor)
        {
            if (tile >= 0 && tile < floors.Length && floor > floors[tile]) floors[tile] = floor;
        }

        /// <summary>Care hours a day drip saves on the tile's crop (no weeding between the lines), else 0.</summary>
        public static Fixed CareCut(Simulation sim, int tile) { return DripRunning(sim, tile) ? Def(sim).Drip.CareCutHoursPerDay : Fixed.Zero; }

        /// <summary>The fertiliser's yield factor on the tile: with drip running and a fertiliser given, raised by fertigation.</summary>
        public static Fixed FertiliserFactor(Simulation sim, int tile, FertiliserDef fert)
        {
            if (fert.YieldFactor == Fixed.One || fert.KgPerAcrePerSeason == Fixed.Zero || !DripRunning(sim, tile)) return fert.YieldFactor;
            return fert.YieldFactor * (Fixed.One + Def(sim).Drip.FertigationBonus);
        }

        /// <summary>What drip costs you on the next acre: the price at today's index, less the subsidy on the first acres.</summary>
        public static long DripPrice(Simulation sim)
        {
            DripDef d = Def(sim).Drip;
            long price = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (sim.State.Irrigation.SubsidisedDripAcres >= d.SubsidyAcres) return price;
            return price - Money.RoundToCoin(Money.MulRate(price, d.SubsidyShare));
        }

        /// <summary>The running costs due at the next close, at today's prices: each source's power, the canal's fee, drip's power and upkeep.</summary>
        public static long PumpPower(Simulation sim, int source)
        {
            if (!On(sim) || (source == 1 ? Pump(sim) : Borewell(sim)) == null) return 0;
            PumpedDef d = source == 1 ? Def(sim).Pump : Def(sim).Borewell;
            return MarketRules.Indexed(sim, d.PowerPerAcreSeason * Acres(sim, source).Count);
        }

        public static int ChannelCount(Simulation sim) { return Count(sim, Def(sim).Canal.Building); }

        public static int DripCount(Simulation sim) { return Count(sim, Def(sim).Drip.Building); }

        private static int Count(Simulation sim, int def)
        {
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def) n++;
            return n;
        }

        public static long CanalFee(Simulation sim) { return On(sim) ? MarketRules.Indexed(sim, Def(sim).Canal.FeePerAcreSeason * ChannelCount(sim)) : 0; }

        public static long DripPower(Simulation sim) { return On(sim) ? MarketRules.Indexed(sim, Def(sim).Drip.PowerPerAcreSeason * DripCount(sim)) : 0; }

        /// <summary>Drip's upkeep: a share of what each acre's drip cost.</summary>
        public static long DripUpkeep(Simulation sim)
        {
            if (!On(sim)) return 0;
            long cost = 0;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == Def(sim).Drip.Building) cost += b.Cost;
            return Money.RoundToCoin(Money.MulRate(cost, Def(sim).Drip.UpkeepShare));
        }

        /// <summary>
        /// At the season close, with the payments that belong to the new season (spec 013 §3.5): the pump's and the borewell's power
        /// for the acres on their lists, the canal's fee for each channelled acre, drip's power and upkeep. Each is paid if cash
        /// allows; unpaid, that source is off until the next close.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            IrrigationState s = sim.State.Irrigation;
            s.PumpUnpaid = !Pay(sim, PumpPower(sim, 1), 0, "pump", "ledger.pumpPower");
            s.BorewellUnpaid = !Pay(sim, PumpPower(sim, 2), 0, "borewell", "ledger.borewellPower");
            s.CanalUnpaid = !Pay(sim, CanalFee(sim), 0, "canal", "ledger.canalFee");
            s.DripUnpaid = !Pay(sim, DripPower(sim), DripUpkeep(sim), "drip", "ledger.dripRunning");
        }

        private static bool Pay(Simulation sim, long power, long upkeep, string what, string key)
        {
            long cost = power + upkeep, t = sim.State.Clock.SimTime;
            if (cost == 0) return true;
            if (!sim.State.Ledger.CanAfford(cost))
            {
                sim.Emit(new ObligationUnpaid(t, "Irrigation", what, cost));
                return false;
            }
            var postings = new List<Posting> { new Posting(Account.Cash, -cost) };
            if (power > 0) postings.Add(new Posting(Account.Power, power));
            if (upkeep > 0) postings.Add(new Posting(Account.Repairs, upkeep));
            sim.State.Ledger.Post(t, sim.Period, key, what, postings.ToArray());
            sim.Emit(new ObligationPaid(t, "Irrigation", what, cost));
            return true;
        }

        /// <summary>
        /// A parcel leaves you (sold, or seized by foreclosure): its channels, drip and a borewell on it go with the land at book
        /// value (spec 013 §3.1), and its acres leave the pump's and the borewell's lists. Returns the value raised.
        /// </summary>
        internal static long Release(Simulation sim, int parcelId, long period, string key)
        {
            if (!On(sim)) return 0;
            LandState land = sim.State.Land;
            IrrigationDef d = Def(sim);
            long t = sim.State.Clock.SimTime, raised = 0;
            foreach (Building b in land.Buildings.ToArray())
            {
                if (b.TileId < 0 || land.Tiles[b.TileId].ParcelId != parcelId) continue;
                if (b.Def != d.Canal.Building && b.Def != d.Drip.Building && b.Def != d.Borewell.Building) continue;
                long book = b.Cost - b.Depreciated;
                sim.State.Ledger.Post(t, period, key, "building:" + b.Id, new Posting(Account.Cash, book),
                    new Posting(Account.AccumulatedDepreciation, b.Depreciated), new Posting(Account.Buildings, -b.Cost));
                raised += book;
                land.Buildings.Remove(b);
                if (b.Def == d.Borewell.Building) sim.State.Irrigation.BorewellAcres.Clear();
            }
            IrrigationState s = sim.State.Irrigation;
            s.PumpAcres.RemoveAll(x => land.Tiles[x].ParcelId == parcelId);
            s.BorewellAcres.RemoveAll(x => land.Tiles[x].ParcelId == parcelId);
            return raised;
        }

        /// <summary>The irrigation buildings, which never keep a parcel from being sold (they go with it).</summary>
        public static bool IsIrrigation(Simulation sim, Building b)
        {
            if (!On(sim)) return false;
            IrrigationDef d = Def(sim);
            return b.Def == d.Canal.Building || b.Def == d.Drip.Building || b.Def == d.Borewell.Building || b.Def == d.Pump.Building;
        }
    }
}
