using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Tools.Balancer
{
    /// <summary>
    /// The seasonal farm cash-flow model (US-013): a competent founder farming alone on the inherited farm, run on the
    /// real simulation with every number from <c>content/</c>. It prints each season's P&amp;L, cash and net worth, then
    /// the anchor checks of spec 003 §9 and ADR 0028 (land priced at 8–15 seasons of an acre's net income).
    /// Phase 1 parts that don't exist yet are noted: the hired hand (spec 006) is estimated from the measured acre
    /// results, and produce is sold to the trader at the floor (the market and the mandi arrive in Sprint 3).
    /// </summary>
    public static class Economy
    {
        private static readonly int[] Acres = { 20, 28, 18, 19, 26, 27 }; // the well acres first
        private static readonly string[] BestCrop = { "potato", "lentils", "rice", "potato" }; // Spring, Summer, Monsoon, Winter

        private static string C(long money) { return (money / 100).ToString("N0", CultureInfo.InvariantCulture); }

        public static int Run(SimConfig config, int years, ulong seed)
        {
            var watch = Stopwatch.StartNew();
            Simulation sim = Simulation.NewGame(config, new NewGame(seed, 0, 8 * 3600));
            var rows = new List<string>();
            var stats = new SeasonStats();
            long end = years * sim.Clock.SecondsPerYear;
            int reports = 0;
            for (long t = sim.State.Clock.SimTime + 600; t < end; t += 3600)
            {
                Play(sim, t, stats);
                foreach (SimEvent e in sim.DrainEvents())
                {
                    if (e is CropHarvested h)
                    {
                        stats.Acres++;
                        stats.Kg += h.Grams / 1000.0;
                        stats.CareSum += ToDouble(h.Breakdown.Care);
                        stats.WaterSum += ToDouble(h.Breakdown.Water);
                        FarmDefs f = config.Game.Farm;
                        stats.AcreSeasons += ToDouble(FarmRules.CycleShare(sim.Clock, f.Crops[f.IndexOfCrop(h.CropId)]));
                    }
                    if (e is GardenPicked g) stats.GardenKg += g.Grams / 1000.0;
                }
                if (sim.State.Finance.Reports.Count > reports)
                {
                    reports = sim.State.Finance.Reports.Count;
                    rows.Add(Row(sim, sim.State.Finance.Reports[reports - 1], stats));
                    stats.Seasons.Add(stats.Snapshot(sim.State.Finance.Reports[reports - 1]));
                    stats.Reset();
                }
            }
            watch.Stop();

            Console.WriteLine("Farm to City economy model (US-013), sim " + SimInfo.Version + ". " + years + " game years, seed " + seed
                + ", " + watch.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s.");
            Console.WriteLine("A competent founder alone on the inherited farm: the best crop in season on as many acres as Care ≥ 1.0 allows");
            Console.WriteLine("(the well acres first), basic diet, normal hours, spare hours sold as day labour, produce sold to the trader at the floor.");
            Console.WriteLine();
            Console.WriteLine("  Season    Harvests  Care  Water   Harvest kg   Farm income   Farm costs   Farm profit   Day labour   Household   Net result   Cash end    Net worth");
            foreach (string r in rows) Console.WriteLine(r);
            Summary(sim, stats.Seasons);
            return 0;
        }

        /// <summary>One bot round: harvest, sell surplus, sow to the labour limit, tend the garden, set the day-labour split.</summary>
        private static void Play(Simulation sim, long t, SeasonStats stats)
        {
            sim.AdvanceTo(t);
            FarmDefs farm = sim.Config.Game.Farm;
            int season = (int)Sim.Core.Time.Calendar.SeasonAt(t, sim.Clock);
            foreach (int tile in Acres)
            {
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) sim.Execute(new Harvest(t, tile, Overflow.SellRestToTrader));
            }
            SellSurplus(sim);

            // Sow while the founder's hours cover the demand (Care ≥ 1.0), the well acres first.
            Fixed supply = FounderRules.LabourRate(sim);
            foreach (int tile in Acres)
            {
                if (sim.State.Farm.CropOn(tile) != null) continue;
                CropDef crop = farm.Crops[farm.IndexOfCrop(BestCrop[season])];
                Fixed demand = FarmSystem.LabourDemand(sim) + crop.LabourPerAcrePerDay;
                if (demand > supply) break; // Care would fall below 1.0
                sim.Execute(new Sow(t, tile, crop.Id, SeedSource.Certified, "none"));
            }

            foreach (GardenBed b in sim.State.Farm.Beds)
            {
                if (b.State == BedState.Ready) sim.Execute(new PickGarden(t, b.Id, Overflow.SellRestToTrader));
                if (b.State == BedState.Empty) sim.Execute(new SowGarden(t, b.Id, b.Id == 1 ? "garden_spinach" : "garden_radish"));
            }

            // Spare hours go to day labour, in steps of 25%.
            Fixed rate = FounderRules.LabourRate(sim), need = FarmSystem.LabourDemand(sim);
            int share = 0;
            while (share < 100 && rate.MulRatio(100 - share - 25, 100) >= need) share += 25;
            if (share != sim.State.Farm.Founder.DayLabourShare) sim.Execute(new SetLabourSplit(t, share));
        }

        /// <summary>Sells produce above a small household reserve to the trader at the floor (the Sprint 3 market stands in).</summary>
        private static void SellSurplus(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
            {
                if (goods[g].Kind != GoodKind.Produce) continue;
                long surplus = sim.State.Market.InStore(g) - 50_000;
                long q = Math.Min(surplus, MarketRules.TraderCapacityLeft(sim, g));
                if (q <= 0) continue;
                Store.Take(sim, g, q);
                Store.SellToTrader(sim, g, q, "model");
            }
        }

        private static string Row(Simulation sim, SeasonReport r, SeasonStats s)
        {
            long year = r.Period / 4;
            string name = "Y" + year + " " + ((Sim.Core.Time.Season)(r.Period % 4)).ToString();
            return "  " + name.PadRight(12) + s.Acres.ToString().PadLeft(6)
                + (s.Acres == 0 ? "-" : (s.CareSum / s.Acres).ToString("0.00", CultureInfo.InvariantCulture)).PadLeft(6)
                + (s.Acres == 0 ? "-" : (s.WaterSum / s.Acres).ToString("0.00", CultureInfo.InvariantCulture)).PadLeft(7)
                + s.Kg.ToString("N0", CultureInfo.InvariantCulture).PadLeft(13)
                + C(r.Line("farmIncome").Amount).PadLeft(14) + C(r.Line("farmCosts").Amount).PadLeft(13) + C(r.FarmProfit).PadLeft(14)
                + C(r.Line("otherIncome").Amount).PadLeft(13) + C(r.Line("household").Amount).PadLeft(12) + C(r.NetResult).PadLeft(13)
                + C(r.CashFlow.Closing).PadLeft(11) + C(r.NetWorth.Total).PadLeft(13);
        }

        private static void Summary(Simulation sim, List<SeasonStats.Row> seasons)
        {
            GameDefs defs = sim.Config.Game;
            List<SeasonStats.Row> steady = seasons.Skip(4).ToList(); // after the first year
            double Avg(Func<SeasonStats.Row, double> f) { return steady.Count == 0 ? 0 : steady.Average(f); }
            double acres = Avg(r => r.Acres);
            double farmProfit = Avg(r => r.FarmProfit / 100.0);
            double perAcreSeason = steady.Sum(r => r.FarmProfit / 100.0) / Math.Max(1, steady.Sum(r => r.AcreSeasons));

            Console.WriteLine();
            Console.WriteLine("Anchor checks (seasons after the first year):");
            Console.WriteLine("  Founder alone: " + steady.Average(r => r.AcreSeasons).ToString("0.0", CultureInfo.InvariantCulture)
                + " acres kept busy a season, farm profit " + farmProfit.ToString("N0", CultureInfo.InvariantCulture)
                + " a season (spec 003 §9: about 30,000–38,000 on about 2 acres)");
            Console.WriteLine("  Net result a season: " + Avg(r => r.Net / 100.0).ToString("N0", CultureInfo.InvariantCulture)
                + "; day labour " + Avg(r => r.DayLabour / 100.0).ToString("N0", CultureInfo.InvariantCulture)
                + "; living costs " + Avg(r => -r.Household / 100.0).ToString("N0", CultureInfo.InvariantCulture)
                + " (home use paid " + Avg(r => r.HomeUse / 100.0).ToString("N0", CultureInfo.InvariantCulture) + " of it)");
            Console.WriteLine("  Kitchen garden: " + Avg(r => r.GardenKg).ToString("0", CultureInfo.InvariantCulture)
                + " kg a season (spec 003 §9: about 200 kg)");

            const double hand = 36_000; // spec 006 (Sprint 4): a farm hand's season wage, ADR 0008
            double acresPerHand = 2.5;
            double netWithHand = perAcreSeason - hand / acresPerHand;
            long acrePrice = LandRules.TilePrice(defs, sim.State.Land.Tiles[18]) / 100;
            Console.WriteLine("  Farm profit per busy acre a season (founder's own labour unpaid): " + perAcreSeason.ToString("N0", CultureInfo.InvariantCulture));
            Console.WriteLine("  With a hired hand (36,000 a season ÷ " + acresPerHand + " acres = 14,400 an acre): about "
                + netWithHand.ToString("N0", CultureInfo.InvariantCulture) + " an acre a season (spec 003 §9: about 8,000–12,000)");
            Console.WriteLine("  Land: an inherited loam acre is priced " + acrePrice.ToString("N0", CultureInfo.InvariantCulture) + " = "
                + (netWithHand <= 0 ? "∞" : (acrePrice / netWithHand).ToString("0.0", CultureInfo.InvariantCulture))
                + " seasons of that net income (ADR 0028: 8–15)");
            Console.WriteLine();
            Console.WriteLine("Not modelled yet: the hired hand and the hand's hut (spec 006, Sprint 4), the local market, minimum prices and the");
            Console.WriteLine("mandi trip (Sprint 3), spoilage (Sprint 3), pests (Sprint 3), loans (Sprint 4).");
        }

        private static double ToDouble(Fixed f) { return f.Raw / 4294967296.0; }

        private sealed class SeasonStats
        {
            public int Acres;
            public double Kg, CareSum, WaterSum, GardenKg, AcreSeasons;
            public readonly List<Row> Seasons = new List<Row>();

            public void Reset() { Acres = 0; Kg = CareSum = WaterSum = GardenKg = AcreSeasons = 0; }

            public Row Snapshot(SeasonReport r)
            {
                return new Row
                {
                    Acres = Acres, FarmProfit = r.FarmProfit, Net = r.NetResult, DayLabour = r.Line("otherIncome").Amount,
                    Household = r.Line("household").Amount, HomeUse = r.HomeUse, GardenKg = GardenKg,
                    AcreSeasons = AcreSeasons,
                };
            }

            public sealed class Row
            {
                public int Acres;
                public long FarmProfit, Net, DayLabour, Household, HomeUse;
                public double GardenKg, AcreSeasons;
            }
        }
    }
}
