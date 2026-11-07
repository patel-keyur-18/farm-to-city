using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Equipment
{
    /// <summary>
    /// The equipment ladder's rules (spec 013 §3.2, ADR 0088, ADR 0089): which fields each machine covers, its labour cut and Tools
    /// factor there, what it costs to run, the close, breakdowns, and selling or losing it.
    /// </summary>
    public static class EquipmentRules
    {
        public static EquipmentDefs Defs(Simulation sim) { return sim.Config.Game.Equipment; }

        public static bool On(Simulation sim) { return sim.State.Equipment != null && sim.Config.Game.Equipment != null; }

        public static bool Unlocked(Simulation sim, MachineDef d) { return sim.State.Era == null || sim.State.Era.Current >= d.UnlockEra; }

        public static Machine Find(Simulation sim, long id) { return On(sim) ? sim.State.Equipment.Machines.Find(m => m.Id == id) : null; }

        /// <summary>Your machine of a kind, bought or leased, or null.</summary>
        public static Machine OfKind(Simulation sim, int kind) { return On(sim) ? sim.State.Equipment.Machines.Find(m => m.Kind == kind) : null; }

        public static bool Hired(Simulation sim, int kind) { return On(sim) && sim.State.Equipment.HiredUntil[kind] > sim.State.Clock.SimTime; }

        /// <summary>Your draught animals (the bullocks), in the second shed.</summary>
        public static int Bullocks(Simulation sim)
        {
            if (sim.State.Livestock == null) return 0;
            int n = 0;
            foreach (Livestock.Animal a in sim.State.Livestock.Animals) if (a.Kind == Defs(sim).Bullock) n++;
            return n;
        }

        public static bool Broken(Simulation sim, Machine m) { return m.RepairDue > 0 || m.BrokenUntil > sim.State.Clock.SimTime; }

        /// <summary>Working now: not broken, its diesel or lease paid, and its bullocks there to pull it.</summary>
        public static bool Running(Simulation sim, Machine m)
        {
            MachineDef d = Defs(sim).Machines[m.Kind];
            return !Broken(sim, m) && !m.Unpaid && (d.Bullocks == 0 || Bullocks(sim) >= d.Bullocks);
        }

        /// <summary>
        /// Your fields a machine can work, in the order machines take them (spec 013 §3.2): the well acres first, then by id. Owned farm
        /// acres that aren't a homestead, a business's yard or a second cattle acre.
        /// </summary>
        public static List<int> Fields(Simulation sim)
        {
            var list = new List<int>();
            int shed = Livestock.LivestockRules.SecondShedTile(sim);
            bool Field(int t)
            {
                Tile tile = sim.State.Land.Tiles[t];
                return tile.Terrain == Terrain.Farm && sim.State.Land.Parcels[tile.ParcelId].Owned && t != shed && People.HouseRules.HousesOn(sim, t) == 0
                    && Business.BusinessRules.On(sim, t) == null && Farm.OrchardRules.Of(sim, t) == null; // an orchard isn't ploughed (ADR 0090)
            }
            foreach (int t in sim.Config.Game.Land.WellTiles) if (Field(t)) list.Add(t);
            for (int t = 0; t < sim.State.Land.Tiles.Count; t++) if (!list.Contains(t) && Field(t)) list.Add(t);
            return list;
        }

        /// <summary>Machine kinds, best first (the biggest labour cut; by content order on a tie).</summary>
        private static List<int> BestFirst(EquipmentDefs d)
        {
            var kinds = new List<int>();
            for (int k = 0; k < d.Machines.Count; k++) kinds.Add(k);
            kinds.Sort((a, b) => { int c = d.Machines[b].LabourCut.CompareTo(d.Machines[a].LabourCut); return c != 0 ? c : a.CompareTo(b); });
            return kinds;
        }

        /// <summary>
        /// Which machine kind covers each tile, or −1 (spec 013 §3.2): best first, a machine hired for the day covers all your fields,
        /// yours (bought or leased) the next of its acres in field order. <paramref name="running"/> leaves out broken, unpaid and
        /// unpulled machines; <paramref name="hires"/> counts the day's hires.
        /// </summary>
        public static int[] Cover(Simulation sim, bool running = true, bool hires = true)
        {
            var cover = new int[sim.State.Land.Tiles.Count];
            for (int i = 0; i < cover.Length; i++) cover[i] = -1;
            if (!On(sim)) return cover;
            List<int> fields = Fields(sim);
            foreach (int k in BestFirst(Defs(sim)))
            {
                if (hires && Hired(sim, k)) { foreach (int t in fields) if (cover[t] < 0) cover[t] = k; continue; }
                Machine m = OfKind(sim, k);
                if (m == null || running && !Running(sim, m)) continue;
                int left = Defs(sim).Machines[k].Acres;
                foreach (int t in fields)
                {
                    if (left == 0) break;
                    if (cover[t] >= 0) continue;
                    cover[t] = k;
                    left--;
                }
            }
            return cover;
        }

        /// <summary>The fields your own machine of this kind works (for its diesel), with every machine of yours counted as working.</summary>
        public static int Acres(Simulation sim, int kind)
        {
            int n = 0;
            foreach (int k in Cover(sim, false, false)) if (k == kind) n++;
            return n;
        }

        /// <summary>The share of a covered field's labour saved (0 on hand tools).</summary>
        public static Fixed LabourCut(Simulation sim, int[] cover, int tile) { return cover[tile] < 0 ? Fixed.Zero : Defs(sim).Machines[cover[tile]].LabourCut; }

        /// <summary>The Tools factor a crop sown on this tile now gets: the machine's, or hand tools' (spec 003's <c>toolsFactor</c>).</summary>
        public static Fixed Tools(Simulation sim, int[] cover, int tile) { return cover[tile] < 0 ? sim.Config.Game.Farm.Farm.ToolsFactor : Defs(sim).Machines[cover[tile]].Tools; }

        /// <summary>A crop is sown: under a machine, its Tools factor is kept for its harvest (timely sowing, ADR 0089).</summary>
        internal static void Sown(Simulation sim, Crop c)
        {
            if (!On(sim)) return;
            Fixed tools = Tools(sim, Cover(sim), c.TileId);
            if (tools != sim.Config.Game.Farm.Farm.ToolsFactor) sim.State.Equipment.SownTools.Add(new KeyValuePair<long, Fixed>(c.Id, tools));
        }

        /// <summary>The Tools factor a crop was sown under; hand tools' when it wasn't.</summary>
        public static Fixed ToolsOf(Simulation sim, Crop c)
        {
            if (On(sim)) foreach (KeyValuePair<long, Fixed> k in sim.State.Equipment.SownTools) if (k.Key == c.Id) return k.Value;
            return sim.Config.Game.Farm.Farm.ToolsFactor;
        }

        /// <summary>A crop's yield is settled (or the crop is gone): its sowing record goes.</summary>
        internal static void Forget(Simulation sim, long cropId)
        {
            if (On(sim)) sim.State.Equipment.SownTools.RemoveAll(k => k.Key == cropId);
        }

        // ---- money ----

        public static long Price(Simulation sim, MachineDef d) { return MarketRules.Indexed(sim, d.Price); }

        /// <summary>The bullocks that come with it, at the fair's price.</summary>
        public static long BullocksPrice(Simulation sim, MachineDef d)
        {
            return d.Bullocks == 0 ? 0 : d.Bullocks * MarketRules.Indexed(sim, sim.Config.Game.Livestock.Animals[Defs(sim).Bullock].Price);
        }

        public static long Lease(Simulation sim, MachineDef d) { return MarketRules.Indexed(sim, d.LeasePerSeason); }

        public static long Hire(Simulation sim, MachineDef d) { return MarketRules.Indexed(sim, d.HirePerDay); }

        /// <summary>A season's diesel for <paramref name="acres"/> fields.</summary>
        public static long Diesel(Simulation sim, MachineDef d, int acres) { return MarketRules.Indexed(sim, d.DieselPerAcreSeason) * acres; }

        /// <summary>A season's maintenance: a share of what it cost.</summary>
        public static long Maintenance(Simulation sim, Machine m) { return Money.RoundToCoin(Money.MulRate(m.Cost, Defs(sim).Machines[m.Kind].MaintenanceShare)); }

        public static long Repair(Simulation sim, Machine m) { return Money.RoundToCoin(Money.MulRate(m.Cost, Defs(sim).RepairShare)); }

        public static long Book(Machine m) { return m.Cost - m.Depreciated; }

        /// <summary>What it sells for: its book value × <c>resaleShare</c> (the market value shown beside the book value).</summary>
        public static long Resale(Simulation sim, Machine m) { return Money.RoundToCoin(Money.MulRate(Book(m), Defs(sim).ResaleShare)); }

        /// <summary>What selling it raises: its resale and, for the ox plough, its bullocks at their fair price.</summary>
        public static long SaleValue(Simulation sim, Machine m)
        {
            long value = Resale(sim, m);
            foreach (Livestock.Animal a in Pair(sim, m)) value += Livestock.LivestockRules.SellPrice(sim, a);
            return value;
        }

        /// <summary>The bullocks that go with a machine when it's sold (the first of them in the herd; none for the tractor).</summary>
        private static List<Livestock.Animal> Pair(Simulation sim, Machine m)
        {
            var pair = new List<Livestock.Animal>();
            int left = Defs(sim).Machines[m.Kind].Bullocks;
            foreach (Livestock.Animal a in sim.State.Livestock.Animals)
            {
                if (pair.Count == left) break;
                if (a.Kind == Defs(sim).Bullock) pair.Add(a);
            }
            return pair;
        }

        /// <summary>A season's charge for what's left of this season (rounded down), for a machine bought or leased mid-season.</summary>
        public static long RestOfSeason(Simulation sim, long seasonCharge)
        {
            long season = sim.Clock.SecondsPerSeason, left = Market.GodownRules.SeasonEnd(sim) - sim.State.Clock.SimTime;
            return (long)(new System.Numerics.BigInteger(seasonCharge) * left / season);
        }

        /// <summary>The most a mandi trip takes without hiring the trolley: your working machines' carts (spec 013 §3.2).</summary>
        public static long CartGrams(Simulation sim)
        {
            long kg = 0;
            if (On(sim))
                foreach (Machine m in sim.State.Equipment.Machines)
                    if (Running(sim, m) && Defs(sim).Machines[m.Kind].CartKg > kg) kg = Defs(sim).Machines[m.Kind].CartKg;
            return kg * 1000;
        }

        /// <summary>Book value of your bought machines the bank lends against, less one being sold.</summary>
        public static long Collateral(Simulation sim, long extra = 0, long excluding = -1)
        {
            long v = extra;
            if (On(sim))
                foreach (Machine m in sim.State.Equipment.Machines)
                    if (!m.Leased && m.Id != excluding && Defs(sim).Machines[m.Kind].Loan) v += Book(m);
            return v;
        }

        // ---- the close, the day ----

        /// <summary>
        /// At the season close, with the payments that belong to the new season (spec 013 §3.5): a lease's rent and diesel upfront
        /// (unpaid, the machine goes back); a bought machine's maintenance (when on and cash allows; skipped, it breaks more often)
        /// and diesel (unpaid, it stands idle until the next close).
        /// </summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            FarmSystem.Accrue(sim);
            long t = sim.State.Clock.SimTime;
            foreach (Machine m in sim.State.Equipment.Machines.ToArray())
            {
                MachineDef d = Defs(sim).Machines[m.Kind];
                long diesel = Diesel(sim, d, Acres(sim, m.Kind));
                if (m.Leased)
                {
                    long lease = Lease(sim, d);
                    if (!sim.State.Ledger.CanAfford(lease + diesel))
                    {
                        sim.Emit(new ObligationUnpaid(t, "Equipment", d.Id, lease + diesel));
                        sim.State.Equipment.Machines.Remove(m);
                        sim.Emit(new MachineReturned(t, m.Id, d.Id));
                        continue;
                    }
                    Post(sim, "ledger.machineLease", m, new Posting(Account.Rent, lease), new Posting(Account.Power, diesel));
                    m.Unpaid = false;
                    continue;
                }
                long upkeep = m.Maintain ? Maintenance(sim, m) : 0;
                m.Maintained = m.Maintain && sim.State.Ledger.CanAfford(upkeep);
                if (m.Maintained && upkeep > 0) Post(sim, "ledger.machineUpkeep", m, new Posting(Account.Repairs, upkeep));
                if (m.Maintain && !m.Maintained) sim.Emit(new ObligationUnpaid(t, "Equipment", d.Id + ":upkeep", upkeep));
                m.Unpaid = !sim.State.Ledger.CanAfford(diesel);
                if (m.Unpaid) sim.Emit(new ObligationUnpaid(t, "Equipment", d.Id, diesel));
                else if (diesel > 0) Post(sim, "ledger.machineDiesel", m, new Posting(Account.Power, diesel));
            }
            FarmSystem.Refresh(sim);
        }

        private static void Post(Simulation sim, string key, Machine m, params Posting[] costs)
        {
            long total = 0;
            var postings = new List<Posting>();
            foreach (Posting p in costs) if (p.Amount != 0) { postings.Add(p); total += p.Amount; }
            if (total == 0) return;
            postings.Add(new Posting(Account.Cash, -total));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, key, "machine:" + m.Id, postings.ToArray());
        }

        /// <summary>
        /// The daily pass (spec 013 §3.2, §3.5), bought machines in id order: a repair waiting for cash is paid when it can be (then
        /// 24 h to fix); a broken machine past its 24 h works again; a working one breaks with a seeded chance (stream Equipment, key =
        /// machine, counter = day) of <c>breakdownMaintained</c>, or <c>breakdownSkipped</c> with this season's maintenance skipped.
        /// </summary>
        internal static void Daily(Simulation sim)
        {
            if (!On(sim)) return;
            long t = sim.State.Clock.SimTime;
            ulong day = (ulong)(t / ClockDef.SecondsPerDay);
            bool changed = false;
            foreach (Machine m in sim.State.Equipment.Machines)
            {
                if (m.Leased) continue; // leased machines never break on you (spec 013 §10 G5)
                MachineDef d = Defs(sim).Machines[m.Kind];
                if (m.RepairDue > 0)
                {
                    if (!sim.State.Ledger.CanAfford(m.RepairDue)) continue;
                    if (!changed) FarmSystem.Accrue(sim);
                    Post(sim, "ledger.machineRepair", m, new Posting(Account.Repairs, m.RepairDue));
                    m.RepairDue = 0;
                    m.BrokenUntil = t + ClockDef.SecondsPerDay;
                    continue;
                }
                if (m.BrokenUntil > 0)
                {
                    if (m.BrokenUntil > t) continue;
                    if (!changed) FarmSystem.Accrue(sim);
                    changed = true;
                    m.BrokenUntil = 0;
                    sim.Emit(new MachineRepaired(t, m.Id, d.Id));
                    continue;
                }
                Fixed chance = m.Maintained ? Defs(sim).BreakdownMaintained : Defs(sim).BreakdownSkipped;
                Fixed u = Fixed.FromRaw((long)(Rng.Next(sim.State.Seed, RngStream.Equipment, (ulong)m.Id, day) >> 32));
                if (u >= chance) continue;
                if (!changed) FarmSystem.Accrue(sim);
                changed = true;
                long cost = Repair(sim, m);
                bool paid = sim.State.Ledger.CanAfford(cost);
                if (paid && cost > 0) Post(sim, "ledger.machineRepair", m, new Posting(Account.Repairs, cost));
                if (!paid) m.RepairDue = cost;
                m.BrokenUntil = t + ClockDef.SecondsPerDay;
                sim.Emit(new MachineBroke(t, m.Id, d.Id, cost, paid));
            }
            if (changed) FarmSystem.Refresh(sim);
        }

        /// <summary>Straight-line depreciation of bought machines at the close (spec 005 §3.3): Dr 5500, Cr 1610 (their book value).</summary>
        internal static void Depreciate(Simulation sim, long closing)
        {
            if (!On(sim)) return;
            foreach (Machine m in sim.State.Equipment.Machines)
            {
                if (m.Leased) continue;
                int life = Defs(sim).Machines[m.Kind].LifeSeasons;
                if (m.SeasonsDepreciated >= life) continue;
                long amount = m.SeasonsDepreciated + 1 == life ? m.Cost - m.Depreciated : m.Cost / life;
                m.SeasonsDepreciated++;
                if (amount == 0) continue;
                m.Depreciated += amount;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, closing, "ledger.depreciation", "machine:" + m.Id,
                    new Posting(Account.Depreciation, amount), new Posting(Account.Equipment, -amount));
            }
        }

        /// <summary>
        /// Sells a bought machine at its resale value (Dr Cash, Cr 1610 at book, the difference a gain or loss), or seizes it in
        /// foreclosure; its bullocks go with it at their fair price. Returns what it raised.
        /// </summary>
        internal static long Sell(Simulation sim, Machine m, string key, long period)
        {
            long t = sim.State.Clock.SimTime, proceeds = Resale(sim, m), book = Book(m), raised = proceeds;
            var postings = new List<Posting> { new Posting(Account.Cash, proceeds), new Posting(Account.Equipment, -book) };
            long diff = proceeds - book;
            if (diff > 0) postings.Add(new Posting(Account.GainOnAssetSales, -diff));
            if (diff < 0) postings.Add(new Posting(Account.LossOnAssetSales, -diff));
            FarmSystem.Accrue(sim);
            sim.State.Ledger.Post(t, period, key, "machine:" + m.Id, postings.ToArray());
            foreach (Livestock.Animal a in Pair(sim, m))
            {
                long value = Livestock.LivestockRules.SellPrice(sim, a), gain = value - a.Cost;
                sim.State.Ledger.Post(t, period, key, "animal:" + a.Id, new Posting(Account.Cash, value), new Posting(Account.Livestock, -a.Cost),
                    gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
                sim.State.Livestock.Animals.Remove(a);
                raised += value;
            }
            sim.State.Equipment.Machines.Remove(m);
            FarmSystem.Refresh(sim);
            return raised;
        }
    }
}
