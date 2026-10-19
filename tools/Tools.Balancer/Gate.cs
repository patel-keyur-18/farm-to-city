using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Tools.Balancer
{
    /// <summary>
    /// The Phase 0 gate checks (US-015, US-016), as ADR 0034 §7 defines them. <c>dotnet test</c> asserts them
    /// (GateTests, category Balance) and <c>dotnet run --project tools/Tools.Balancer -- gate</c> prints them. Every bot
    /// plays through player commands only. Seeds run in parallel: each run is its own deterministic sim.
    /// </summary>
    public static class Gate
    {
        /// <summary>Builds a config with an optional content edit (file, JSON) → JSON.</summary>
        public delegate SimConfig ConfigFor(Func<string, string, string> edit);

        // ---- US-016: fresh start and bounded creation ----

        public sealed class FreshStartResult
        {
            public int Seeds, SpringProfitable, LivingAlwaysPaid, OverCapSeeds;
            public List<double> SeasonFarmProfit = new List<double>(); // year-0 prices, all seasons of all seeds
            public double MedianNetWorth;
        }

        /// <summary>The competent founder from a new game for <paramref name="seasons"/> seasons, on each seed.</summary>
        public static FreshStartResult FreshStart(SimConfig config, int seeds, int seasons = 4)
        {
            var runs = new Economy.Run[seeds];
            Parallel.For(0, seeds, i => runs[i] = Economy.RunBot(config, new FarmerBot(), seasons, (ulong)(i + 1), null));
            var r = new FreshStartResult { Seeds = seeds };
            foreach (Economy.Run run in runs)
            {
                if (run.Seasons[0].FarmProfit > 0) r.SpringProfitable++;
                if (run.Seasons.All(s => s.UnpaidLiving == 0)) r.LivingAlwaysPaid++;
                if (run.Seasons.Any(s => s.OverCap > 0)) r.OverCapSeeds++;
                r.SeasonFarmProfit.AddRange(run.Seasons.Select(s => s.RealFarmProfit));
            }
            r.MedianNetWorth = Median(runs.Select(x => x.Seasons.Last().RealNetWorth));
            return r;
        }

        // ---- US-016: the broke founder ----

        public sealed class BrokeResult
        {
            public int Seeds, BasicWithinOneSeason, SeasonOfCostsWithinTwo;
        }

        /// <summary>No cash, an empty store, no credit (loans arrive in Sprint 4): day labour first, then farming.</summary>
        public static BrokeResult Broke(ConfigFor configFor, int seeds)
        {
            SimConfig config = configFor((f, j) =>
                f == "start.json" ? System.Text.RegularExpressions.Regex.Replace(
                    j.Replace("\"startingCash\": 20000", "\"startingCash\": 0"), "\"store\": \\[[^\\]]*\\]", "\"store\": []") : j);
            var basic = new bool[seeds];
            var held = new bool[seeds];
            Parallel.For(0, seeds, i =>
            {
                long S = config.Clock.SecondsPerSeason;
                Economy.RunBot(config, new FarmerBot(), 2, (ulong)(i + 1), sim =>
                {
                    long t = sim.State.Clock.SimTime;
                    FounderState f = sim.State.Farm.Founder;
                    if (t <= S + 3600 && !f.AutoSubsistence && sim.Config.Game.Farm.DietPlans[f.DietPlan].Id != DietPlanDef.SubsistenceId) basic[i] = true;
                    long season = MarketRules.Indexed(sim, sim.Config.Game.Farm.DietPlans[sim.Config.Game.Farm.IndexOfDietPlan("basic")].CostPerSeason);
                    if (sim.State.Ledger.Cash >= season) held[i] = true;
                });
            });
            return new BrokeResult { Seeds = seeds, BasicWithinOneSeason = basic.Count(x => x), SeasonOfCostsWithinTwo = held.Count(x => x) };
        }

        // ---- US-015: structural and zero-time checks ----

        /// <summary>Every way to buy produce or sell inputs is rejected. Returns the failures (empty = pass).</summary>
        public static List<string> Structural(SimConfig config)
        {
            var failures = new List<string>();
            Simulation sim = Simulation.NewGame(config, new NewGame(1, 0, 8 * 3600));
            var goods = config.Game.Market.Goods;
            long t = sim.State.Clock.SimTime;
            foreach (GoodDef g in goods)
            {
                int i = config.Game.Market.IndexOf(g.Id);
                if (g.Kind == GoodKind.Produce)
                {
                    if (sim.Execute(new Buy(t, g.Id, 1_000)).Reason != RejectReason.NotAnInput) failures.Add("Buy accepted produce " + g.Id);
                }
                else
                {
                    if (sim.Execute(new SellToTrader(t, g.Id, 1)).Reason != RejectReason.NotProduce) failures.Add("SellToTrader accepted input " + g.Id);
                    if (sim.Execute(new Offer(t, g.Id, 1)).Reason != RejectReason.NotProduce) failures.Add("Offer accepted input " + g.Id);
                    if (sim.Execute(new GoToMandi(t, new[] { new MandiLoad(g.Id, 1) })).Reason != RejectReason.NotProduce) failures.Add("GoToMandi accepted input " + g.Id);
                    if (config.Game.Market.Region.TraderBuyPerDay(i) != 0) failures.Add("the trader buys input " + g.Id);
                }
            }
            return failures;
        }

        /// <summary>
        /// From <paramref name="states"/> seeded mid-game states, random command sequences applied at one instant never
        /// raise net worth beyond rounding (1/100 coin per accepted command). Harvests and picks are left out: they turn
        /// growth into stored goods, which is value made over time. Returns the worst gain seen and the failures.
        /// </summary>
        public static List<string> ZeroTimeLoops(SimConfig config, int states, int sequencesPerState, out long worstGain)
        {
            var failures = new List<string>();
            long worst = long.MinValue;
            object gate = new object();
            Parallel.For(0, states, i =>
            {
                ulong seed = (ulong)(i + 1);
                int seasons = 1 + i % 6;
                Economy.Run run = Economy.RunBot(config, new FarmerBot(), seasons, seed, null);
                Simulation sim = run.Sim;
                long t = sim.State.Clock.SimTime;
                for (int q = 0; q < sequencesPerState; q++)
                {
                    long before = Reporting.NetWorth(sim).Total;
                    int accepted = 0;
                    for (int c = 0; c < 8; c++)
                        if (sim.Execute(RandomCommand(sim, t, Rng.Next(seed, (RngStream)950, (ulong)q, (ulong)c))).Accepted) accepted++;
                    long gain = Reporting.NetWorth(sim).Total - before;
                    lock (gate)
                    {
                        worst = Math.Max(worst, gain - accepted);
                        if (gain > accepted) failures.Add("state " + i + " sequence " + q + ": net worth +" + gain + " with " + accepted + " commands");
                    }
                }
            });
            worstGain = worst;
            return failures;
        }

        private static Command RandomCommand(Simulation sim, long t, ulong r)
        {
            GameDefs defs = sim.Config.Game;
            var goods = defs.Market.Goods;
            int g = (int)Rng.Below(r, (ulong)goods.Count);
            string good = goods[g].Id;
            ulong r2 = Rng.Next(r, (RngStream)951, 0, 0);
            long q = (long)Rng.Below(r2, 500_000) + 1;
            Parcel parcel = sim.State.Land.Parcels[(int)Rng.Below(Rng.Next(r, (RngStream)952, 0, 0), (ulong)sim.State.Land.Parcels.Count)];
            string parcelId = defs.Map.Parcels[parcel.Id].Id;
            TreeSlot slot = sim.State.Land.Slots.Count == 0 ? null : sim.State.Land.Slots[(int)Rng.Below(Rng.Next(r, (RngStream)953, 0, 0), (ulong)sim.State.Land.Slots.Count)];
            int[] acres = { 18, 19, 20, 26, 27, 28 };
            int tile = acres[(int)Rng.Below(Rng.Next(r, (RngStream)954, 0, 0), 6)];
            switch (Rng.Below(Rng.Next(r, (RngStream)955, 0, 0), 16))
            {
                case 0: return new Offer(t, good, q, (r & 1) == 0 ? (Fixed?)null : MarketRules.RegionalPrice(sim, g));
                case 1: return new Withdraw(t, good, q);
                case 2: return new SellToTrader(t, good, q);
                case 3: return new Buy(t, good, q);
                case 4: return new GoToMandi(t, new[] { new MandiLoad(good, q) });
                case 5: return new BuyParcel(t, parcelId);
                case 6: return new SellParcel(t, parcelId);
                case 7: return new SetDietPlan(t, defs.Farm.DietPlans[(int)(r % (ulong)defs.Farm.DietPlans.Count)].Id);
                case 8: return slot == null ? (Command)new BuyMedicine(t) : new PlantTree(t, slot.Id, defs.Farm.Trees[(int)(r % (ulong)defs.Farm.Trees.Count)].Id);
                case 9: return slot == null ? (Command)new BuyMedicine(t) : new RemoveTree(t, slot.Id);
                case 10: return new Sow(t, tile, defs.Farm.Crops[(int)(r % (ulong)defs.Farm.Crops.Count)].Id, (r & 2) == 0 ? SeedSource.Certified : SeedSource.Saved, "none");
                case 11: return new ClearField(t, tile);
                case 12: return new Spray(t, tile);
                case 13: return new BuyMedicine(t);
                case 14: return new SetLabourSplit(t, 25 * (int)(r % 5));
                default: return new SetEatFromStore(t, (r & 1) == 0);
            }
        }

        // ---- US-015: stress bots ----

        public sealed class StressResult
        {
            public string Bot;
            public double MedianNetWorth, Ratio;
        }

        /// <summary>Each stress bot's median net worth (year-0 prices) after <paramref name="seasons"/> against the competent bot's.</summary>
        public static List<StressResult> Stress(SimConfig config, int seeds, int seasons = 8, Func<Bot>[] stress = null)
        {
            var makers = new List<Func<Bot>> { () => new FarmerBot() };
            makers.AddRange(stress ?? new Func<Bot>[] { () => new FieldHolderBot(), () => new SubsistenceDodgerBot(),
                () => new LandFlipperBot(), () => new OfferChurnerBot(), () => new MandiEveryDayBot(),
                () => new LeverageBot(), () => new StrategicDefaulterBot() });
            var medians = new double[makers.Count];
            var names = new string[makers.Count];
            for (int b = 0; b < makers.Count; b++)
            {
                var worth = new double[seeds];
                names[b] = makers[b]().Name;
                Func<Bot> make = makers[b];
                Parallel.For(0, seeds, i => worth[i] = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), null).Seasons.Last().RealNetWorth);
                medians[b] = Median(worth);
            }
            var list = new List<StressResult>();
            for (int b = 0; b < makers.Count; b++)
                list.Add(new StressResult { Bot = names[b], MedianNetWorth = medians[b], Ratio = medians[b] / medians[0] });
            return list;
        }

        // ---- ADR 0038: the debt bots must actually borrow, miss, default and be foreclosed (every mechanic is used) ----

        public sealed class DebtActivity
        {
            public string Bot;
            public int Seeds, LoansTaken, BankLoans, Foreclosures, SeedsForeclosed;
            public double MedianScoreAtEnd;
        }

        /// <summary>What a debt stress bot did over <paramref name="seasons"/> seasons on each seed.</summary>
        public static DebtActivity DebtBotActivity(SimConfig config, Func<Bot> make, int seeds, int seasons = 8)
        {
            var runs = new Economy.Run[seeds];
            Parallel.For(0, seeds, i => runs[i] = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), null));
            var a = new DebtActivity { Bot = make().Name, Seeds = seeds };
            foreach (Economy.Run run in runs)
            {
                CreditState c = run.Sim.State.Credit;
                a.LoansTaken += c.Loans.Count;
                a.BankLoans += c.Loans.Count(l => l.Lender == 1);
                a.Foreclosures += c.Foreclosures;
                if (c.Foreclosures > 0) a.SeedsForeclosed++;
            }
            a.MedianScoreAtEnd = Median(runs.Select(r => (double)r.Sim.State.Credit.Score));
            return a;
        }

        private static double Median(IEnumerable<double> values)
        {
            double[] v = values.OrderBy(x => x).ToArray();
            return v.Length == 0 ? 0 : v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
        }

        private static double Percentile(List<double> values, double p)
        {
            double[] v = values.OrderBy(x => x).ToArray();
            return v.Length == 0 ? 0 : v[(int)Math.Min(v.Length - 1, Math.Floor(p * v.Length))];
        }

        // ---- The report (US-016.T3) ----

        public static int Print(ConfigFor configFor, int seeds)
        {
            SimConfig config = configFor(null);
            string P(int part, int whole) { return (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%"; }
            string C(double coins) { return coins.ToString("N0", CultureInfo.InvariantCulture); }
            var watch = System.Diagnostics.Stopwatch.StartNew();

            Console.WriteLine("Farm to City Phase 0 gate checks (US-015, US-016, ADR 0034 §7), " + seeds + " seeds.");
            List<string> structural = Structural(config);
            Console.WriteLine("  Structural: " + (structural.Count == 0 ? "pass" : "FAIL " + string.Join("; ", structural)));
            List<string> loops = ZeroTimeLoops(config, 200, 5, out long worst);
            Console.WriteLine("  Zero-time loops (200 states × 5 sequences): " + (loops.Count == 0 ? "pass" : "FAIL " + loops.Count) + ", worst net-worth change "
                + (worst / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " beyond rounding");

            FreshStartResult fresh = FreshStart(config, seeds);
            Console.WriteLine("  Fresh start, 1 year: Spring farm profit > 0 in " + P(fresh.SpringProfitable, seeds) + " (≥ 99%), living costs paid at every close in "
                + P(fresh.LivingAlwaysPaid, seeds) + " (100%), seeds with output over its cap: " + fresh.OverCapSeeds + " (0)");
            Console.WriteLine("    Farm profit a season (year-0 prices): median " + C(Median(fresh.SeasonFarmProfit)) + ", 5th percentile "
                + C(Percentile(fresh.SeasonFarmProfit, 0.05)) + ", 95th " + C(Percentile(fresh.SeasonFarmProfit, 0.95)) + "; median net worth after a year " + C(fresh.MedianNetWorth));

            BrokeResult broke = Broke(configFor, seeds);
            Console.WriteLine("  Broke founder: back on basic within 1 season in " + P(broke.BasicWithinOneSeason, seeds) + " (100%), a season of living costs in hand within 2 in "
                + P(broke.SeasonOfCostsWithinTwo, seeds) + " (≥ 99%)");

            Console.WriteLine("  Stress bots, 2 years, " + Math.Min(seeds, 100) + " seeds (median net worth at year-0 prices; at most 1.20 × competent):");
            foreach (StressResult s in Stress(config, Math.Min(seeds, 100)))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            foreach (Func<Bot> make in new Func<Bot>[] { () => new LeverageBot(), () => new StrategicDefaulterBot() })
            {
                DebtActivity d = DebtBotActivity(config, make, Math.Min(seeds, 100));
                Console.WriteLine("  " + d.Bot + " used credit: " + d.LoansTaken + " loans (" + d.BankLoans + " from the bank), " + d.Foreclosures
                    + " foreclosures in " + d.SeedsForeclosed + " of " + d.Seeds + " runs, median score " + d.MedianScoreAtEnd.ToString("0", CultureInfo.InvariantCulture));
            }
            Console.WriteLine("  Land-flipper over 4 years (it can first afford a parcel in year 3):");
            foreach (StressResult s in Stress(config, Math.Min(seeds, 100), 16, new Func<Bot>[] { () => new LandFlipperBot() }))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            Console.WriteLine("  (" + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s)");
            return 0;
        }
    }
}
