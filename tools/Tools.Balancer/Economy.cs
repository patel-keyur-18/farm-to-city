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
    /// The seasonal farm cash-flow model (US-013): a bot farming the inherited farm on the real simulation, with every
    /// number from <c>content/</c> and only player commands. It prints each season's P&amp;L, cash and net worth, where
    /// the produce was sold, then the anchor checks of spec 003 §9 and ADR 0028, 0033. Money in the anchor checks is at
    /// year-0 prices (divided by the price index), so inflation doesn't move the anchors. The hired hand (spec 006) is
    /// still estimated.
    /// </summary>
    public static class Economy
    {
        private static string C(double coins) { return coins.ToString("N0", CultureInfo.InvariantCulture); }

        private static double Real(long money, Fixed index) { return money / 100.0 / ToDouble(index); }

        /// <summary>Runs a bot for <paramref name="years"/> from a new game at 08:00 and collects each season.</summary>
        public static Run RunBot(SimConfig config, Bot bot, int years, ulong seed) { return RunBot(config, bot, years * 4, seed, null); }

        /// <summary>Runs a bot for <paramref name="seasons"/> seasons, calling <paramref name="onHour"/> after each round.</summary>
        public static Run RunBot(SimConfig config, Bot bot, int seasons, ulong seed, Action<Simulation> onHour)
        {
            Simulation sim = Simulation.NewGame(config, new NewGame(seed, 0, 8 * 3600));
            bot.Start(sim);
            var run = new Run { Sim = sim, Bot = bot };
            var season = new SeasonRow();
            long end = seasons * sim.Clock.SecondsPerSeason;
            int reports = 0;
            for (long t = sim.State.Clock.SimTime + 600; t < end; t += 3600)
            {
                sim.AdvanceTo(t);
                bot.Play(sim, t);
                onHour?.Invoke(sim);
                foreach (SimEvent e in sim.DrainEvents()) season.Add(sim, e);
                if (sim.State.Finance.Reports.Count > reports)
                {
                    reports = sim.State.Finance.Reports.Count;
                    season.Close(sim.State.Finance.Reports[reports - 1], sim.State.Market.PriceIndex);
                    run.Seasons.Add(season);
                    season = new SeasonRow();
                }
            }
            return run;
        }

        public static int Print(SimConfig config, int years, ulong seed)
        {
            var watch = Stopwatch.StartNew();
            Run run = RunBot(config, new FarmerBot(), years, seed);
            watch.Stop();
            Simulation sim = run.Sim;

            Console.WriteLine("Farm to City economy model (US-013), sim " + SimInfo.Version + ". " + years + " game years, seed " + seed
                + ", " + watch.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s.");
            Console.WriteLine("A competent founder alone on the inherited farm, playing through commands only: the best crop in season on as many");
            Console.WriteLine("acres as Care ≥ 1.0 allows, the garden, 2 mangoes and 2 guavas in the yard, spare hours as day labour; sells by mandi");
            Console.WriteLine("when a cart beats the trader after its labour, a small minimum-price offer locally, the rest to the trader.");
            Console.WriteLine("Money in coins as booked (the price index rises 2–6% a year); the last column is net worth at year-0 prices.");
            Console.WriteLine();
            Console.WriteLine("  Season     Harv  Care  Water  Harvest kg  Farm income  Farm costs  Farm profit  Day labour  Household  Net result   Cash end   Net worth  (year-0)");
            foreach (SeasonRow r in run.Seasons) Console.WriteLine(r.Line);
            Summary(run);
            return 0;
        }

        private static void Summary(Run run)
        {
            Simulation sim = run.Sim;
            GameDefs defs = sim.Config.Game;
            List<SeasonRow> steady = run.Seasons.Skip(4).ToList(); // after the first year
            double Avg(Func<SeasonRow, double> f) { return steady.Count == 0 ? 0 : steady.Average(f); }
            double farmProfit = Avg(r => r.RealFarmProfit);
            double perAcreSeason = steady.Sum(r => r.RealFarmProfit) / Math.Max(1, steady.Sum(r => r.AcreSeasons));

            Console.WriteLine();
            Console.WriteLine("Where the produce went (all seasons, booked coins): trader " + C(run.Seasons.Sum(r => r.Trader / 100.0))
                + ", local market " + C(run.Seasons.Sum(r => r.Local / 100.0)) + ", mandi (net) " + C(run.Seasons.Sum(r => r.Mandi / 100.0))
                + " in " + run.Seasons.Sum(r => r.MandiTrips) + " trips, eaten at home " + C(run.Seasons.Sum(r => r.HomeUse / 100.0))
                + "; spoiled " + C(run.Seasons.Sum(r => r.SpoiledKg)) + " kg; pest outbreaks " + run.Seasons.Sum(r => r.Outbreaks) + ".");
            Console.WriteLine();
            Console.WriteLine("Anchor checks (seasons after the first year, at year-0 prices):");
            Console.WriteLine("  Founder alone: " + Avg(r => r.AcreSeasons).ToString("0.0", CultureInfo.InvariantCulture)
                + " acres kept busy a season, farm profit " + C(farmProfit) + " a season (spec 003 §9: about 30,000–38,000 on about 2 acres)");
            Console.WriteLine("  Net result a season: " + C(Avg(r => r.RealNet)) + "; day labour " + C(Avg(r => r.RealDayLabour))
                + "; living costs " + C(-Avg(r => r.RealHousehold)) + " (home use paid " + C(Avg(r => r.RealHomeUse)) + " of it)");
            Console.WriteLine("  Kitchen garden: " + Avg(r => r.GardenKg).ToString("0", CultureInfo.InvariantCulture) + " kg picked a season, "
                + C(Avg(r => r.RealGardenHomeUse)) + " of groceries saved (spec 003 §9: about 200 kg, about 1,500)");

            const double hand = 36_000; // spec 006 (Sprint 4): a farm hand's season wage, ADR 0008
            double acresPerHand = 2.5;
            double netWithHand = perAcreSeason - hand / acresPerHand;
            double acrePrice = Real(LandRules.TilePrice(sim, sim.State.Land.Tiles[18]), sim.State.Market.PriceIndex);
            Console.WriteLine("  Farm profit per busy acre a season (founder's own labour unpaid): " + C(perAcreSeason));
            Console.WriteLine("  With a hired hand (36,000 a season ÷ " + acresPerHand + " acres = 14,400 an acre): about "
                + C(netWithHand) + " an acre a season (spec 003 §9, ADR 0033: about 2,000–5,000)");
            Console.WriteLine("  Land: an inherited loam acre is priced " + C(acrePrice) + " = "
                + (acrePrice / perAcreSeason).ToString("0.0", CultureInfo.InvariantCulture) + " seasons of an owner-operated acre's net (ADR 0028, 0033: 8–15), "
                + (netWithHand <= 0 ? "never" : (acrePrice / netWithHand).ToString("0", CultureInfo.InvariantCulture)) + " with a hired hand");
            Console.WriteLine("  Trees (cost, returns to date at the trader's price when picked, payback):");
            foreach (Tree tree in sim.State.Farm.Trees)
            {
                TreeDef d = defs.Farm.Trees[tree.Def];
                Console.WriteLine("    " + d.Id.PadRight(8) + "slot " + tree.SlotId + ": cost " + C(tree.Cost / 100.0) + ", returns " + C(tree.Returns / 100.0)
                    + ", " + tree.Batches + " batches");
            }
            Console.WriteLine("  Price index after " + (run.Seasons.Count / 4) + " years: " + ToDouble(sim.State.Market.PriceIndex).ToString("0.000", CultureInfo.InvariantCulture)
                + " (spec 004 §9: 1.22–1.79 after 10)");
            Console.WriteLine();
            Console.WriteLine("Not modelled yet: the hired hand and the hand's hut (spec 006, Sprint 4), loans (Sprint 4).");
        }

        internal static double ToDouble(Fixed f) { return f.Raw / 4294967296.0; }

        /// <summary>A bot's run: the sim at the end and one row per closed season.</summary>
        public sealed class Run
        {
            public Simulation Sim;
            public Bot Bot;
            public readonly List<SeasonRow> Seasons = new List<SeasonRow>();
        }

        /// <summary>One closed season: the report plus what the events showed.</summary>
        public sealed class SeasonRow
        {
            public int Harvests, MandiTrips, Outbreaks, UnpaidLiving, OverCap;
            private readonly Dictionary<long, long> _bedGrams = new Dictionary<long, long>(); // keyed lookups only
            private readonly Dictionary<long, int> _treeBatches = new Dictionary<long, int>();
            public double Kg, CareSum, WaterSum, GardenKg, AcreSeasons, SpoiledKg;
            public long Trader, Local, Mandi, HomeUse;
            public SeasonReport Report;
            public Fixed Index;
            public string Line;

            public long FarmProfit { get { return Report.FarmProfit; } }
            public double RealFarmProfit { get { return Real(Report.FarmProfit, Index); } }
            public double RealNet { get { return Real(Report.NetResult, Index); } }
            public double RealDayLabour { get { return Real(Report.Line("otherIncome").Amount, Index); } }
            public double RealHousehold { get { return Real(Report.Line("household").Amount, Index); } }
            public double RealHomeUse { get { return Real(Report.HomeUse, Index); } }
            public double RealGardenHomeUse { get { return Real(Report.GardenHomeUse, Index); } }
            public double RealNetWorth { get { return Real(Report.NetWorth.Total, Index); } }

            internal void Add(Simulation sim, SimEvent e)
            {
                switch (e)
                {
                    case CropHarvested h:
                        Harvests++;
                        if (h.Grams > (sim.Config.Game.Farm.Farm.YieldCap * h.Breakdown.Base).MulRatio(1000, 1).RoundToLong()) OverCap++;
                        Kg += h.Grams / 1000.0;
                        CareSum += ToDouble(h.Breakdown.Care);
                        WaterSum += ToDouble(h.Breakdown.Water);
                        FarmDefs f = sim.Config.Game.Farm;
                        AcreSeasons += ToDouble(FarmRules.CycleShare(sim.Clock, f.Crops[f.IndexOfCrop(h.CropId)]));
                        break;
                    case GardenPicked g:
                        GardenKg += g.Grams / 1000.0;
                        _bedGrams.TryGetValue(g.BedId, out long bed);
                        _bedGrams[g.BedId] = bed + g.Grams;
                        if (bed + g.Grams > sim.Config.Game.Farm.Farm.GardenSeasonOutputGrams) OverCap++;
                        break;
                    case FruitPicked p:
                        _treeBatches.TryGetValue(p.SlotId, out int n);
                        _treeBatches[p.SlotId] = n + 1;
                        if (n + 1 > 1) OverCap++; // one batch per fruiting season
                        break;
                    case ObligationUnpaid u: if (u.Kind == "Living") UnpaidLiving++; break;
                    case SoldToTrader s: Trader += s.Amount; break;
                    case MarketSales m: Local += m.Amount; break;
                    case MandiTripReturned r: Mandi += r.Net; MandiTrips++; break;
                    case HomeUseEaten u: HomeUse += u.Value; break;
                    case PestWarning _: Outbreaks++; break;
                    case GoodsSpoiled s:
                        if (sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(s.GoodId)].Kind == GoodKind.Produce) SpoiledKg += s.Quantity / 1000.0;
                        break;
                }
            }

            internal void Close(SeasonReport r, Fixed index)
            {
                Report = r;
                Index = index;
                string name = "Y" + r.Period / 4 + " " + ((Sim.Core.Time.Season)(r.Period % 4));
                Line = "  " + name.PadRight(11) + Harvests.ToString().PadLeft(5)
                    + (Harvests == 0 ? "-" : (CareSum / Harvests).ToString("0.00", CultureInfo.InvariantCulture)).PadLeft(6)
                    + (Harvests == 0 ? "-" : (WaterSum / Harvests).ToString("0.00", CultureInfo.InvariantCulture)).PadLeft(7)
                    + C(Kg).PadLeft(12) + C(r.Line("farmIncome").Amount / 100.0).PadLeft(13) + C(r.Line("farmCosts").Amount / 100.0).PadLeft(12)
                    + C(r.FarmProfit / 100.0).PadLeft(13) + C(r.Line("otherIncome").Amount / 100.0).PadLeft(12) + C(r.Line("household").Amount / 100.0).PadLeft(11)
                    + C(r.NetResult / 100.0).PadLeft(12) + C(r.CashFlow.Closing / 100.0).PadLeft(11) + C(RealNetWorth).PadLeft(12);
            }
        }
    }
}
