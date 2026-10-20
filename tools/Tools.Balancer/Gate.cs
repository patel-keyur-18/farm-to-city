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

        /// <summary>No cash and an empty store: day labour first, then farming (the competent bot may borrow; <see cref="NeverStuck"/> refuses credit).</summary>
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

        /// <summary>A seeded random player command (the zero-time check and the random bot).</summary>
        public static Command RandomCommand(Simulation sim, long t, ulong r)
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

        /// <summary>When the sim unlocks the Hamlet (<c>EraUnlocked</c>, spec 009 §3.6), in days from the new game, per seed (NaN if never).</summary>
        public static List<double> HamletDays(SimConfig config, int seeds, int seasons = 6)
        {
            var days = new double[seeds];
            Parallel.For(0, seeds, i =>
            {
                double day = double.NaN;
                Economy.RunBot(config, new FarmerBot(), seasons, (ulong)(i + 1), sim =>
                {
                    if (!double.IsNaN(day)) return;
                    foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                        if (e is Sim.Core.Events.EraUnlocked u && u.EraId == "hamlet") { day = e.SimTime / 86400.0; break; }
                });
                days[i] = day;
            });
            return days.ToList();
        }

        // ---- US-098 (ADR 0045 §4): the Phase 1 health checks ----

        /// <summary>No cash, an empty store and no credit: the cautious saver (it never borrows) plays on; per seed, the first
        /// season (1-based) whose farm profit is above 0, or 0 if none within <paramref name="seasons"/>.</summary>
        public static List<int> NeverStuck(ConfigFor configFor, int seeds, int seasons = 4)
        {
            SimConfig config = configFor((f, j) =>
                f == "start.json" ? System.Text.RegularExpressions.Regex.Replace(
                    j.Replace("\"startingCash\": 20000", "\"startingCash\": 0"), "\"store\": \\[[^\\]]*\\]", "\"store\": []") : j);
            var first = new int[seeds];
            Parallel.For(0, seeds, i =>
            {
                Economy.Run run = Economy.RunBot(config, new CautiousSaverBot(), seasons, (ulong)(i + 1), null);
                if (run.Sim.State.Credit.Loans.Count > 0) throw new InvalidOperationException("the never-stuck bot borrowed");
                int k = run.Sim.State.Finance.Reports.FindIndex(r => r.FarmProfit > 0);
                first[i] = k < 0 ? 0 : k + 1;
            });
            return first.ToList();
        }

        public sealed class RecoveryResult
        {
            public int Seeds, Foreclosed;
            /// <summary>Per foreclosed seed: seasons from the foreclosure to the first close with farm profit above 0 (−1 never).</summary>
            public List<int> SeasonsToProfit = new List<int>();
            /// <summary>Per foreclosed seed: game days from the foreclosure until the Hamlet's conditions all hold again (NaN never).</summary>
            public List<double> DaysToHamlet = new List<double>();
        }

        /// <summary>The recovering bot: defaults until foreclosed, then plays competently (ADR 0045 §4).</summary>
        public static RecoveryResult Recovery(SimConfig config, int seeds, int seasons = 20)
        {
            var profit = new int[seeds];
            var hamlet = new double[seeds];
            var foreclosed = new bool[seeds];
            Parallel.For(0, seeds, i =>
            {
                var bot = new RecoveringBot();
                double met = double.NaN;
                int reportsAtForeclosure = -1;
                Economy.Run run = Economy.RunBot(config, bot, seasons, (ulong)(i + 1), sim =>
                {
                    if (bot.ForeclosedAt < 0 || !double.IsNaN(met)) return;
                    if (reportsAtForeclosure < 0) reportsAtForeclosure = sim.State.Finance.Reports.Count;
                    // Every condition again, from the foreclosure on: one season's acres and Care, a hut, a profitable season.
                    long foreclosedPeriod = bot.ForeclosedAt / sim.Clock.SecondsPerSeason;
                    bool profitable = sim.State.Finance.Reports.Skip(reportsAtForeclosure).Any(r => r.FarmProfit > 0);
                    Sim.Core.Definitions.EraDef h = sim.Config.Game.Onboarding.Eras[1];
                    foreach (long p in new[] { sim.Period, sim.Period - 1 })
                    {
                        if (p <= foreclosedPeriod) continue;
                        if (profitable && Sim.Core.Progress.EraRules.Acres(sim, p) >= h.AcresHarvestedInSeason
                            && Sim.Core.Progress.EraRules.MeanCare(sim, p) >= h.MeanCareAtLeast && Sim.Core.Progress.EraRules.Huts(sim) >= h.HandHutsAtLeast)
                            met = (sim.State.Clock.SimTime - bot.ForeclosedAt) / 86400.0;
                    }
                });
                if (bot.ForeclosedAt < 0) return;
                foreclosed[i] = true;
                int k = run.Sim.State.Finance.Reports.FindIndex(Math.Max(0, reportsAtForeclosure), r => r.FarmProfit > 0);
                profit[i] = k < 0 ? -1 : k - reportsAtForeclosure + 1;
                hamlet[i] = met;
            });
            var result = new RecoveryResult { Seeds = seeds };
            for (int i = 0; i < seeds; i++)
            {
                if (!foreclosed[i]) continue;
                result.Foreclosed++;
                result.SeasonsToProfit.Add(profit[i]);
                result.DaysToHamlet.Add(hamlet[i]);
            }
            return result;
        }

        /// <summary>
        /// Every Phase 1 bot closes every season with a reconciled ledger (strict checks throw on a failure, spec 005 §3.1).
        /// Returns the failures: a run that threw or closed fewer seasons than it played.
        /// </summary>
        public static List<string> ExactLedger(SimConfig config, int seeds, int seasons = 8)
        {
            if (!config.Options.StrictChecks) throw new ArgumentException("the ledger check needs strict checks");
            var makers = new Func<Bot>[] { () => new FarmerBot(), () => new CautiousSaverBot(), () => new LeverageBot(), () => new RandomBot(), () => new RecoveringBot() };
            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            foreach (Func<Bot> make in makers)
                Parallel.For(0, seeds, i =>
                {
                    Bot bot = make();
                    try
                    {
                        Economy.Run run = Economy.RunBot(config, bot, seasons, (ulong)(i + 1), null);
                        if (run.Sim.State.Finance.Reports.Count < seasons - 1) failures.Add(bot.Name + " seed " + (i + 1) + ": " + run.Sim.State.Finance.Reports.Count + " closes");
                    }
                    catch (InvalidOperationException e) { failures.Add(bot.Name + " seed " + (i + 1) + ": " + e.Message); }
                });
            return failures.OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        /// <summary>The share of produce good-hours where the 0.5–2× rail clamped the price, over the competent runs.</summary>
        public static double RailShare(SimConfig config, int seeds, int seasons = 8)
        {
            var hits = new long[seeds];
            var hours = new long[seeds];
            Parallel.For(0, seeds, i =>
            {
                Simulation sim = Economy.RunBot(config, new FarmerBot(), seasons, (ulong)(i + 1), null).Sim;
                var goods = sim.Config.Game.Market.Goods;
                for (int g = 0; g < goods.Count; g++)
                {
                    if (goods[g].Kind != GoodKind.Produce) continue;
                    hits[i] += sim.State.Market.RailHitsOf(g);
                    hours[i] += sim.State.Market.MarketHours;
                }
            });
            return (double)hits.Sum() / Math.Max(1, hours.Sum());
        }

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
            Console.WriteLine("  People (spec 006, ADR 0039): stress bots at most 1.20 × competent, the absentee at least 0.80 ×; solo is the founder never hiring:");
            foreach (StressResult s in Stress(config, Math.Min(seeds, 100), 8, new Func<Bot>[] { () => new SoloBot(), () => new DayLabourSpammerBot(),
                () => new HandChurnerBot(), () => new LapseGamerBot(), () => new AbsenteeBot() }))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            List<double> hamlet = HamletDays(config, Math.Min(seeds, 100)).Where(d => !double.IsNaN(d)).OrderBy(d => d).ToList();
            Console.WriteLine("  Hamlet unlocked (EraUnlocked: 6 acres at Care ≥ 0.9 in a season, a hand's hut, a profitable season): " + hamlet.Count + " of "
                + Math.Min(seeds, 100) + " seeds; day " + (hamlet.Count == 0 ? "-" : hamlet[hamlet.Count / 10].ToString("0.0", CultureInfo.InvariantCulture) + " / "
                + hamlet[hamlet.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " / " + hamlet[hamlet.Count * 9 / 10].ToString("0.0", CultureInfo.InvariantCulture))
                + " (10th / median / 90th percentile; target 7 ± 2)");
            int n = Math.Min(seeds, 100);
            Console.WriteLine("  Phase 1 stress bots (ADR 0045 §4), 2 years, " + n + " seeds (cautious at most 1.20 ×; random below competent):");
            foreach (StressResult s in Stress(config, n, 8, new Func<Bot>[] { () => new CautiousSaverBot(), () => new LeverageBot(), () => new RandomBot() }))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            List<int> stuck = NeverStuck(configFor, seeds);
            Console.WriteLine("  Never stuck (no cash, empty store, no credit): first profitable season 1 / 2 / 3 / 4 / never: "
                + string.Join(" / ", new[] { 1, 2, 3, 4, 0 }.Select(k => stuck.Count(x => x == k))));
            RecoveryResult rec = Recovery(config, n);
            List<double> ham = rec.DaysToHamlet.Where(d => !double.IsNaN(d)).OrderBy(d => d).ToList();
            Console.WriteLine("  Recovery after foreclosure: " + rec.Foreclosed + " of " + n + " foreclosed; profitable season after 1 / 2 / 3 / later / never: "
                + rec.SeasonsToProfit.Count(x => x == 1) + " / " + rec.SeasonsToProfit.Count(x => x == 2) + " / " + rec.SeasonsToProfit.Count(x => x == 3) + " / "
                + rec.SeasonsToProfit.Count(x => x > 3) + " / " + rec.SeasonsToProfit.Count(x => x < 0) + "; the Hamlet's conditions again in " + ham.Count
                + (ham.Count == 0 ? "" : ", days median " + ham[ham.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + ", max " + ham.Last().ToString("0.0", CultureInfo.InvariantCulture)));
            Console.WriteLine("  Exact ledger (5 bots × " + n + " seeds × 8 seasons): " + (ExactLedger(config, n) is var led && led.Count == 0 ? "pass" : "FAIL " + string.Join("; ", led.Take(5))));
            Console.WriteLine("  Bounded prices: the rail in " + (100 * RailShare(config, n)).ToString("0.000", CultureInfo.InvariantCulture) + "% of good-hours (under 0.1%)");
            Console.WriteLine("  Land-flipper over 4 years (it can first afford a parcel in year 3):");
            foreach (StressResult s in Stress(config, Math.Min(seeds, 100), 16, new Func<Bot>[] { () => new LandFlipperBot() }))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            Console.WriteLine("  (" + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s)");
            return 0;
        }
    }
}
