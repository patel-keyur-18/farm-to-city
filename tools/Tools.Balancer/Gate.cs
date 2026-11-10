using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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
            int[] acres = Home.FarmTiles(defs);
            int tile = acres[(int)Rng.Below(Rng.Next(r, (RngStream)954, 0, 0), (ulong)acres.Length)];
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

        /// <summary>Spec 011 §9: the competent bot's days the truck filled, and the supermarket's share of its sales, over a run.</summary>
        public sealed class TradeResult
        {
            public int TruckDays, TruckFullDays;
            public long Supermarket, AllSales;
            public double FullShare { get { return TruckDays == 0 ? 0 : (double)TruckFullDays / TruckDays; } }
            public double OrderShare { get { return AllSales == 0 ? 0 : (double)Supermarket / AllSales; } }
        }

        public static TradeResult Trade(SimConfig config, int seeds, int seasons = 8)
        {
            var runs = new Economy.Run[seeds];
            Parallel.For(0, seeds, i => runs[i] = Economy.RunBot(config, new FarmerBot(), seasons, (ulong)(i + 1), null));
            var r = new TradeResult();
            foreach (Economy.Run run in runs)
                foreach (Economy.SeasonRow s in run.Seasons)
                {
                    r.TruckDays += s.TruckDays;
                    r.TruckFullDays += s.TruckFullDays;
                    r.Supermarket += s.Supermarket;
                    r.AllSales += s.Trader + s.Local + s.Mandi + s.Supermarket;
                }
            return r;
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

        // ---- Spec 014 §9 (US-211): the town every bot builds ----

        public sealed class TownResult
        {
            public string Bot;
            public int Seeds, HamletRuns;

            /// <summary>Per seed that reached the Hamlet: households at day 14 and 21, people at day 21.</summary>
            public List<int> Households14 = new List<int>(), Households21 = new List<int>(), People21 = new List<int>();

            /// <summary>Town happiness (mean of hourly samples from day 14), households leaving a household-year, rent ÷ the founder's income.</summary>
            public List<double> Happiness = new List<double>(), LeavingPerYear = new List<double>(), RentShare = new List<double>();

            /// <summary>The day 5 households first live on the founder's land, per seed that reached the Hamlet (NaN if never).</summary>
            public List<double> Day5 = new List<double>();
        }

        /// <summary>Plays a bot for <paramref name="seasons"/> (8: two years, the Village by about day 21, ADR 0028) and measures its town.</summary>
        public static TownResult Town(SimConfig config, Func<Bot> make, int seeds, int seasons = 8)
        {
            var rows = new TownResult[seeds];
            string name = make().Name;
            Parallel.For(0, seeds, i =>
            {
                var r = new TownResult();
                bool hamlet = false;
                int h14 = -1, h21 = -1, p21 = -1, left = 0;
                double day5 = double.NaN;
                double happy = 0, householdHours = 0;
                int samples = 0;
                long rent = 0;
                Economy.Run run = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), sim =>
                {
                    long t = sim.State.Clock.SimTime;
                    foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                    {
                        if (e is Sim.Core.Events.EraUnlocked u && u.EraId == "hamlet") hamlet = true;
                        if (e is Sim.Core.Events.HouseholdLeft) left++;
                        if (e is Sim.Core.Events.RentPaid rp) rent += rp.Amount;
                    }
                    var town = sim.State.Citizens;
                    householdHours += town.Households.Count;
                    if (double.IsNaN(day5) && town.Households.Count >= 5) day5 = t / 86400.0;
                    if (h14 < 0 && t >= 14 * 86400) h14 = town.Households.Count;
                    if (h21 < 0 && t >= 21 * 86400) { h21 = town.Households.Count; p21 = town.Population; }
                    if (t >= 14 * 86400 && town.Population > 0) { happy += (double)Sim.Core.People.HappinessRules.Town(sim).Raw / 4294967296.0; samples++; }
                });
                long income = 0;
                foreach (Sim.Core.Finance.SeasonReport rep in run.Sim.State.Finance.Reports)
                    income += rep.Line("farmIncome").Amount + rep.Line("otherIncome").Amount;
                lock (rows)
                {
                    rows[i] = r;
                    if (!hamlet) return;
                    r.HamletRuns = 1;
                    r.Households14.Add(h14);
                    r.Day5.Add(day5);
                    r.Households21.Add(h21);
                    r.People21.Add(p21);
                    if (samples > 0) r.Happiness.Add(happy / samples);
                    double years = householdHours / (16 * 24.0);
                    r.LeavingPerYear.Add(years > 0 ? left / years : 0);
                    r.RentShare.Add(income > 0 ? rent / (double)income : 0);
                }
            });
            var all = new TownResult { Bot = name, Seeds = seeds };
            foreach (TownResult r in rows)
            {
                all.HamletRuns += r.HamletRuns;
                all.Households14.AddRange(r.Households14);
                all.Households21.AddRange(r.Households21);
                all.People21.AddRange(r.People21);
                all.Happiness.AddRange(r.Happiness);
                all.LeavingPerYear.AddRange(r.LeavingPerYear);
                all.RentShare.AddRange(r.RentShare);
                all.Day5.AddRange(r.Day5);
            }
            return all;
        }

        // ---- Spec 018 §9 (US-102): the Village a bot reaches ----

        public sealed class VillageResult
        {
            public string Bot;
            public int Seeds;

            /// <summary>Per seed, in days from the new game (NaN if never): the Village, the Hamlet, and each of the Village's three conditions first met.</summary>
            public List<double> Village = new List<double>(), Hamlet = new List<double>(), Households = new List<double>(), Business = new List<double>(), School = new List<double>();

            /// <summary>Per seed that reached the Village: the literacy rate then (%; spec 018 §9, 50–70).</summary>
            public List<int> Literacy = new List<int>();
        }

        /// <summary>Plays a bot for <paramref name="seasons"/> (12: 48 days, past the saver's 42) and records when the Village and each condition come.</summary>
        public static VillageResult Village(SimConfig config, Func<Bot> make, int seeds, int seasons = 12)
        {
            var rows = new VillageResult[seeds];
            string name = make().Name;
            Parallel.For(0, seeds, i =>
            {
                double village = double.NaN, hamlet = double.NaN, homes = double.NaN, business = double.NaN, school = double.NaN;
                int literacy = -1;
                Economy.RunBot(config, make(), seasons, (ulong)(i + 1), sim =>
                {
                    double day = sim.State.Clock.SimTime / 86400.0;
                    foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                        if (e is Sim.Core.Events.EraUnlocked u)
                        {
                            if (u.EraId == "hamlet") hamlet = e.SimTime / 86400.0;
                            if (u.EraId == "village") { village = e.SimTime / 86400.0; literacy = Sim.Core.People.SchoolRules.LiteracyRate(sim); }
                        }
                    if (double.IsNaN(homes) && Sim.Core.Progress.EraRules.Households(sim) >= 5) homes = day;
                    if (double.IsNaN(business) && Sim.Core.Progress.EraRules.BusinessSeasons(sim) >= 2) business = day;
                    if (double.IsNaN(school) && Sim.Core.People.SchoolRules.IsOpen(sim)) school = day;
                });
                var r = new VillageResult();
                r.Village.Add(village);
                r.Hamlet.Add(hamlet);
                r.Households.Add(homes);
                r.Business.Add(business);
                r.School.Add(school);
                if (literacy >= 0) r.Literacy.Add(literacy);
                rows[i] = r;
            });
            var all = new VillageResult { Bot = name, Seeds = seeds };
            foreach (VillageResult r in rows)
            {
                all.Village.AddRange(r.Village);
                all.Hamlet.AddRange(r.Hamlet);
                all.Households.AddRange(r.Households);
                all.Business.AddRange(r.Business);
                all.School.AddRange(r.School);
                all.Literacy.AddRange(r.Literacy);
            }
            return all;
        }

        // ---- Spec 016 §9 (US-117, ADR 0072): the flour mill a bot founds ----

        public sealed class MillResult
        {
            public string Bot;
            public int Seeds, Founded, Closed, Foreclosed, WorkersLeft, HandsLeft;
            public Dictionary<string, int> Answers = new Dictionary<string, int>();

            /// <summary>Per seed that founded one: the mill's mean net profit a full season, its payback in seasons (its cost ÷ that
            /// profit; NaN when never), and the most closes in a row it ended overdrawn.</summary>
            public List<double> Profit = new List<double>(), Payback = new List<double>();
            public List<int> OverdrawnRun = new List<int>();

            /// <summary>Your cash at the end of every run: never stuck (ADR 0009) means playing on, a foreclosure included.</summary>
            public List<long> EndCash = new List<long>();

            /// <summary>Your town's people at the end of each run (the shops' customers include them).</summary>
            public List<int> EndPeople = new List<int>();

            /// <summary>Per seed that founded one: full seasons measured.</summary>
            public List<int> FullSeasons = new List<int>();
        }

        /// <summary>Plays a mill bot for <paramref name="seasons"/> and measures its mill's own books.</summary>
        public static MillResult Mill(SimConfig config, Func<Bot> make, int seeds, int seasons = 12) { return Business(config, make, seeds, "flour_mill", seasons); }

        /// <summary>
        /// Spec 016 §9 (US-119): the bakery measured from the Hamlet, because the Village's own gate comes with spec 018 (Sprint 17);
        /// the content is edited only in its era.
        /// </summary>
        public static string BakeryInHamlet(string file, string json) { return InHamlet("bakery", file, json); }

        public static string DairyInHamlet(string file, string json) { return InHamlet("dairy", file, json); }

        private static string InHamlet(string type, string file, string json)
        {
            return file == "businesses.json" ? json.Replace("\"id\": \"" + type + "\", \"nameKey\": \"business." + type + "\", \"era\": \"village\"",
                "\"id\": \"" + type + "\", \"nameKey\": \"business." + type + "\", \"era\": \"hamlet\"") : json;
        }

        /// <summary>Plays a business bot for <paramref name="seasons"/> and measures the own books of its business of <paramref name="type"/>.</summary>
        public static MillResult Business(SimConfig config, Func<Bot> make, int seeds, string type, int seasons = 12)
        {
            var all = new MillResult { Bot = make().Name, Seeds = seeds };
            Parallel.For(0, seeds, i =>
            {
                int run = 0, worst = 0, foreclosed = 0;
                Economy.Run r = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), sim =>
                {
                    foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                    {
                        if (e is Sim.Core.Events.ForeclosureCompleted) foreclosed++;
                        if (e is Sim.Core.Events.WorkerLeft || e is Sim.Core.Events.HandLeft || e is Sim.Core.Events.HrEventAnswered)
                            lock (all)
                            {
                                if (e is Sim.Core.Events.WorkerLeft) all.WorkersLeft++;
                                if (e is Sim.Core.Events.HandLeft) all.HandsLeft++;
                                if (e is Sim.Core.Events.HrEventAnswered a) { string k = a.Kind + ":" + a.Choice; all.Answers[k] = (all.Answers.TryGetValue(k, out int n) ? n : 0) + 1; }
                            }
                        if (!(e is Sim.Core.Events.SeasonClosed)) continue;
                        var b = sim.State.Businesses?.Businesses.Find(x => x.Status != Sim.Core.Business.BusinessStatus.Closed && Sim.Core.Business.BusinessRules.TypeOf(sim, x).Id == type);
                        if (b == null) continue;
                        run = Sim.Core.Business.BusinessBooks.Overdrawn(b) > 0 ? run + 1 : 0;
                        worst = Math.Max(worst, run);
                    }
                });
                Sim.Core.Business.Business mill = r.Sim.State.Businesses?.Businesses.Find(x => Sim.Core.Business.BusinessRules.TypeOf(r.Sim, x).Id == type);
                lock (all)
                {
                    // Cash you can reach: your own and the open businesses' (Take out moves it to you), not inputs or stock bought.
                    long reach = r.Sim.State.Ledger.Cash;
                    foreach (Sim.Core.Business.Business x in r.Sim.State.Businesses?.Businesses ?? new System.Collections.Generic.List<Sim.Core.Business.Business>())
                        if (x.Status != Sim.Core.Business.BusinessStatus.Closed) reach += Math.Max(0, x.Ledger.Cash - Sim.Core.Business.BusinessBooks.Overdrawn(x));
                    all.EndCash.Add(reach);
                    all.EndPeople.Add(r.Sim.State.Citizens?.Population ?? 0);
                    all.Foreclosed += foreclosed > 0 ? 1 : 0;
                    if (mill == null) return;
                    all.Founded++;
                    if (mill.Status == Sim.Core.Business.BusinessStatus.Closed) all.Closed++;
                    long founded = mill.FoundedAt / r.Sim.Clock.SecondsPerSeason;
                    var full = mill.Reports.Where(x => x.Period > founded).ToList();
                    if (full.Count == 0) return;
                    all.FullSeasons.Add(full.Count);
                    double profit = full.Average(x => (double)Sim.Core.Business.BusinessBooks.NetProfit(x)) / Money.Coin;
                    all.Profit.Add(profit);
                    double cost = Sim.Core.Business.BusinessRules.Cost(r.Sim, Sim.Core.Business.BusinessRules.TypeOf(r.Sim, mill)) / (double)Money.Coin;
                    all.Payback.Add(profit > 0 ? cost / profit : double.NaN);
                    all.OverdrawnRun.Add(worst);
                }
            });
            return all;
        }

        /// <summary>The bakery's bots (US-119): hired with no mill, hired beside your mill, run by you; the shops' pav amount optionally tried.</summary>
        /// <summary>The dairy's bots (US-216): hired, run by you, and hired on your own milk only (no neighbours').</summary>
        public static int PrintDairy(ConfigFor configFor, int seeds, string paneerPrice = null, string paneerTrader = null, string gheeTrader = null)
        {
            SimConfig config = configFor((f, j) =>
            {
                j = DairyInHamlet(f, j);
                if (paneerPrice != null && f == "goods.json") j = System.Text.RegularExpressions.Regex.Replace(j, "(\"id\": \"paneer\",[^}]*\"basePrice\": )320", "${1}" + paneerPrice);
                if (f == "region.json")
                {
                    if (paneerTrader != null) j = System.Text.RegularExpressions.Regex.Replace(j, "(\"traderBuyPerDay\"[^}]*\"paneer\": )0", "${1}" + paneerTrader);
                    if (gheeTrader != null) j = System.Text.RegularExpressions.Regex.Replace(j, "(\"traderBuyPerDay\"[^}]*\"ghee\": )20", "${1}" + gheeTrader);
                }
                return j;
            });
            if (paneerPrice != null) Console.WriteLine("  paneer " + paneerPrice + ", the trader's paneer " + paneerTrader + " kg and ghee " + gheeTrader + " kg a day");
            Console.WriteLine("Dairy (spec 016 §9, US-216), " + seeds + " seeds, 16 seasons");
            foreach (Func<Bot> make in new Func<Bot>[] { () => new BusinessBot(false, false, "dairy"), () => new BusinessBot(true, false, "dairy"),
                () => new BusinessBot(false, false, "dairy") { OwnMilkOnly = true }, () => new BusinessBot(true, false, "dairy") { OwnMilkOnly = true } })
            {
                MillResult m = Business(config, make, seeds, "dairy", 16);
                List<double> pr = m.Profit.OrderBy(x => x).ToList(), pb = m.Payback.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                Console.WriteLine("  " + m.Bot.PadRight(26) + "founded " + m.Founded + " of " + m.Seeds + ", profit a season "
                    + (pr.Count == 0 ? "-" : K(pr[0]) + " / " + K(pr[pr.Count / 2]) + " / " + K(pr[pr.Count - 1]))
                    + " (min / median / max), payback " + (pb.Count == 0 ? "never" : pb[pb.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " seasons")
                    + ", overdrawn run " + (m.OverdrawnRun.Count == 0 ? 0 : m.OverdrawnRun.Max()) + ", closed " + m.Closed + ", foreclosed " + m.Foreclosed
                    + ", end cash min " + K(m.EndCash.Min() / (double)Money.Coin));
            }
            return 0;
        }

        /// <summary>Spec 013 §9: drip, the borewell and the tractor in the Hamlet, so the competent bot meets them (the Village gate is spec 018's).</summary>
        public static string FarmInHamlet(string file, string json) { return file == "farming.json" ? json.Replace("\"unlockEra\": \"village\"", "\"unlockEra\": \"hamlet\"") : json; }

        /// <summary>
        /// Spec 013 §9 (ADR 0093): the competent bot with and without each farm investment on the same seeds; the paired difference in
        /// farm profit a season (year-0 prices) from the season after it was made, what it cost, and its payback; the orchard's
        /// running difference season by season.
        /// </summary>
        public static int PrintFarm(ConfigFor configFor, int seeds, int seasons)
        {
            string[] tryEdit = (Environment.GetEnvironmentVariable("FTC_FARM_EDIT") ?? "").Split("=>"); // a variant of farming.json to try: "old=>new"
            SimConfig config = configFor((file, json) => tryEdit.Length == 2 && file == "farming.json" ? FarmInHamlet(file, json).Replace(tryEdit[0], tryEdit[1]) : FarmInHamlet(file, json));
            Console.WriteLine("Farm investments (spec 013 §9), " + seeds + " seeds, " + seasons + " seasons; farm profit at year-0 prices against the same bot without it");
            var bots = new (string name, Func<Bot> make, Func<Bot> against)[]
            {
                ("canal", () => new FarmToolBot("canal"), () => new FarmerBot()),
                ("pump", () => new FarmToolBot("pump"), () => new FarmerBot()),
                ("drip (against the pump)", () => new FarmToolBot("pump", "drip"), () => new FarmToolBot("pump")),
                ("tractor bought", () => new FarmToolBot("tractor"), () => new FarmerBot()),
                ("tractor hired", () => new FarmToolBot("tractor-hire"), () => new FarmerBot()),
                ("mango orchard", () => new FarmToolBot("mango"), () => new FarmerBot()),
                ("granary (rice to Spring)", () => new FarmToolBot("granary"), () => new FarmerBot()),
            };
            foreach (var (name, make, against) in bots)
            {
                var gains = new List<double>();
                var paybacks = new List<double>();
                var running = new List<double[]>();
                double spent = 0, perAcre = 0;
                int made = 0, channels = 0;
                var held = new List<double>();
                for (int seed = 1; seed <= seeds; seed++)
                {
                    var bot = (FarmToolBot)make();
                    Economy.Run with = Economy.RunBot(config, bot, seasons, (ulong)seed, null), without = Economy.RunBot(config, against(), seasons, (ulong)seed, null);
                    if (bot.Period < 0) continue;
                    made++;
                    spent += bot.Spent / (double)Money.Coin;
                    channels += Sim.Core.Land.IrrigationRules.ChannelCount(with.Sim);
                    if (bot.RiceHeld > 0) held.Add(bot.RiceHeld / 1000.0);
                    var after = new List<double>();
                    var run = new double[seasons];
                    double sum = 0;
                    for (int i = 0; i < Math.Min(with.Seasons.Count, without.Seasons.Count); i++)
                    {
                        double d = with.Seasons[i].RealFarmProfit - without.Seasons[i].RealFarmProfit;
                        if (with.Seasons[i].Report.Period < bot.Period) continue;
                        sum += d;
                        run[with.Seasons[i].Report.Period - bot.Period] = sum - bot.Spent / (double)Money.Coin;
                        if (with.Seasons[i].Report.Period > bot.Period) after.Add(d);
                    }
                    double gain = after.Count == 0 ? 0 : after.Average();
                    gains.Add(gain);
                    paybacks.Add(gain <= 0 ? double.PositiveInfinity : bot.Spent / (double)Money.Coin / gain);
                    running.Add(run);
                    var steady = without.Seasons.Where(r => r.Report.Period > bot.Period).ToList();
                    perAcre += steady.Sum(r => r.RealFarmProfit) / Math.Max(1, steady.Sum(r => r.AcreSeasons));
                }
                if (made == 0) { Console.WriteLine("  " + name.PadRight(26) + "never made"); continue; }
                gains.Sort();
                paybacks.Sort();
                Console.WriteLine("  " + name.PadRight(26) + "made in " + made + " of " + seeds + ", cost " + K(spent / made) + ", farm profit a season "
                    + K(gains[0]) + " / " + K(gains[gains.Count / 2]) + " / " + K(gains[gains.Count - 1]) + " (min / median / max), payback "
                    + (double.IsInfinity(paybacks[paybacks.Count / 2]) ? "never" : paybacks[paybacks.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " seasons")
                    + (channels > 0 ? "; " + (channels / (double)made).ToString("0.0", CultureInfo.InvariantCulture) + " channels, a canal acre "
                        + (gains[gains.Count / 2] / (channels / (double)made) / (perAcre / made)).ToString("+0%;-0%", CultureInfo.InvariantCulture) + " against a rain-fed acre's "
                        + K(perAcre / made) : ""));
                if (held.Count > 0) Console.WriteLine("    rice held at most (median) " + K(held.OrderBy(x => x).ElementAt(held.Count / 2)) + " kg");
                if (name.StartsWith("mango"))
                    Console.WriteLine("    net of its cost, by seasons since planting (median): "
                        + string.Join(" ", Enumerable.Range(0, seasons).Select(k => K(running.Select(r => r[k]).OrderBy(x => x).ElementAt(running.Count / 2)))));
            }
            return 0;
        }

        /// <summary>The shops in the Hamlet (spec 017 §9): the Village gate is spec 018's, so the bots meet them sooner.</summary>
        public static string ShopsInHamlet(string file, string json)
        {
            foreach (string type in new[] { "veg_stall", "tea_stall", "sweet_shop", "dairy" }) json = InHamlet(type, file, json);
            return json;
        }

        /// <summary>Spec 017 §9: the shops' bots, each type on a pitch hired, good, by you, and on your acre; the sweet shop beside your dairy.</summary>
        public static int PrintShops(ConfigFor configFor, int seeds, int seasons, string only = null)
        {
            SimConfig config = configFor(ShopsInHamlet);
            Console.WriteLine("Shops (spec 017 §9), " + seeds + " seeds, " + seasons + " seasons");
            var bots = new List<(string type, Func<Bot> make)>
            {
                ("tea_stall", () => new BusinessBot(false, false, "tea_stall")),
                ("tea_stall", () => new BusinessBot(false, false, "tea_stall") { Quality = 2 }),
                ("tea_stall", () => new BusinessBot(false, false, "tea_stall") { Quality = 2, Price = Fixed.Parse("1.1") }), // spec 017 §9's target (ADR 0093)
                ("tea_stall", () => new BusinessBot(false, false, "tea_stall") { Price = Fixed.Parse("1.1") }),
                ("tea_stall", () => new BusinessBot(true, false, "tea_stall")),
                ("tea_stall", () => new BusinessBot(false, false, "tea_stall") { OnAcre = true }),
                ("sweet_shop", () => new BusinessBot(false, false, "sweet_shop")),
                ("sweet_shop", () => new BusinessBot(false, false, "sweet_shop") { Quality = 2 }),
                ("sweet_shop", () => new BusinessBot(false, false, "sweet_shop") { Quality = 2, Price = Fixed.Parse("1.1") }),
                ("sweet_shop", () => new BusinessBot(false, false, "dairy", "sweet_shop")),
                ("veg_stall", () => new BusinessBot(false, false, "veg_stall")),
                ("veg_stall", () => new BusinessBot(true, false, "veg_stall")),
            };
            foreach (var (type, make) in bots)
            {
                if (only != null && type != only) continue;
                MillResult m = Business(config, make, seeds, type, seasons);
                List<double> pr = m.Profit.OrderBy(x => x).ToList(), pb = m.Payback.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                Console.WriteLine("  " + m.Bot.PadRight(32) + "founded " + m.Founded + " of " + m.Seeds + ", profit a season "
                    + (pr.Count == 0 ? "-" : K(pr[0]) + " / " + K(pr[pr.Count / 2]) + " / " + K(pr[pr.Count - 1]))
                    + " (min / median / max), payback " + (pb.Count == 0 ? "never" : pb[pb.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " seasons")
                    + ", overdrawn run " + (m.OverdrawnRun.Count == 0 ? 0 : m.OverdrawnRun.Max()) + ", closed " + m.Closed + ", foreclosed " + m.Foreclosed
                    + ", end cash min " + K(m.EndCash.Min() / (double)Money.Coin));
            }
            return 0;
        }

        /// <summary>One run of a shop bot (seed 1): each season's books by account, its customers and served, and its stars.</summary>
        public static int PrintShopBooks(ConfigFor configFor, string bot, int seasons)
        {
            SimConfig config = configFor(ShopsInHamlet);
            string[] parts = bot.Split('-');
            var make = new BusinessBot(parts.Contains("yours"), false, parts[0].Split('+')) { OnAcre = parts.Contains("acre"), Quality = parts.Contains("good") ? 2 : 1 };
            string type = parts[0].Split('+').Last();
            Economy.Run r = Economy.RunBot(config, make, seasons, 1, sim =>
            {
                if (Environment.GetEnvironmentVariable("FTC_SHOP_DEBUG") == "1" && sim.State.Clock.SimTime % 86_400 / 3600 == 23)
                {
                    var s = sim.State.Businesses?.Businesses.Find(x => Sim.Core.Business.BusinessRules.TypeOf(sim, x).Id == type);
                    if (s != null)
                        Console.WriteLine("  day " + sim.State.Clock.SimTime / 86_400 + " 7:00 yesterday chose " + s.CustomersToday / 1000 + " served " + s.ServedToday / 1000 + " worker " + s.Worker + " staffed " + Sim.Core.Business.BusinessRules.Staffed(sim, s) + " hours " + s.OpenHour + "-" + s.CloseHour + " cash " + s.Ledger.Cash / Money.Coin + " can spend " + Sim.Core.Business.BusinessBooks.CanSpend(sim, s) / Money.Coin + ": "
                            + string.Join(", ", Enumerable.Range(0, s.PutOut.Length).Where(g => s.PutOut[g] > 0 || s.Stock[g] > 0)
                                .Select(g => sim.Config.Game.Market.Goods[g].Id + " out " + s.PutOut[g] / 1000 + " stall " + s.Stock[g] / 1000 + " store " + (sim.State.Market.InStore(g) - sim.State.Market.OnOffer(g)) / 1000)));
                }
                foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                {
                    if (!(e is Sim.Core.Events.SeasonClosed)) continue;
                    var b = sim.State.Businesses?.Businesses.Find(x => Sim.Core.Business.BusinessRules.TypeOf(sim, x).Id == type);
                    if (b == null || b.Reports.Count == 0) continue;
                    var rep = b.Reports[b.Reports.Count - 1];
                    Console.WriteLine("season " + rep.Period + " " + b.Status + ", stars " + b.Stars.ToString() + ", customers " + b.SeasonCustomers + " served " + b.SeasonServed + ": "
                        + string.Join(", ", rep.Balances.Where(kv => kv.Value != 0).Select(kv => kv.Key + " " + K(-kv.Value / (double)Money.Coin))));
                }
            });
            return 0;
        }

        public static int PrintBakery(ConfigFor configFor, int seeds, string pav)
        {
            SimConfig config = configFor((f, j) =>
            {
                j = BakeryInHamlet(f, j);
                return pav != null && f == "businesses.json" ? j.Replace("\"shops\": { \"pav\": 0.03,", "\"shops\": { \"pav\": " + pav + ",") : j;
            });
            Console.WriteLine("Bakery (spec 016 §9, US-119), " + seeds + " seeds, 16 seasons" + (pav == null ? "" : ", the shops' pav " + pav + " kg a person a day"));
            foreach (Func<Bot> make in new Func<Bot>[] { () => new BusinessBot(false, false, "bakery"), () => new BusinessBot(false, false, "flour_mill", "bakery"),
                () => new BusinessBot(true, false, "bakery"), () => new BusinessBot(false, true, "bakery"), () => new MillBot(true) })
            {
                string type = make() is MillBot ? "flour_mill" : "bakery";
                MillResult m = Business(config, make, seeds, type, 16);
                List<double> pr = m.Profit.OrderBy(x => x).ToList(), pb = m.Payback.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                Console.WriteLine("  " + m.Bot.PadRight(26) + "founded " + m.Founded + " of " + m.Seeds + ", profit a season "
                    + (pr.Count == 0 ? "-" : K(pr[0]) + " / " + K(pr[pr.Count / 2]) + " / " + K(pr[pr.Count - 1]))
                    + " (min / median / max), payback " + (pb.Count == 0 ? "never" : pb[pb.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " seasons")
                    + ", overdrawn run " + (m.OverdrawnRun.Count == 0 ? 0 : m.OverdrawnRun.Max()) + ", closed " + m.Closed + ", foreclosed " + m.Foreclosed
                    + ", end cash min " + K(m.EndCash.Min() / (double)Money.Coin) + ", town " + m.EndPeople.OrderBy(x => x).ElementAt(m.EndPeople.Count / 2)
                    + " people, " + (m.FullSeasons.Count == 0 ? 0 : m.FullSeasons.OrderBy(x => x).ElementAt(m.FullSeasons.Count / 2)) + " full seasons (medians)");
            }
            return 0;
        }

        private static string K(double coins) { return coins.ToString("N0", CultureInfo.InvariantCulture); }

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

        // ---- Spec 020: the crop ladder ----

        /// <summary>The crops spec 020 §3.4 measures the new ones against.</summary>
        private static readonly string[] ExistingCrops = { "radish", "wheat", "rice", "potato", "lentils" };

        public sealed class LadderCrop
        {
            public string Id;
            public double Gm, Net;
            public double[] PerHour = new double[4]; // by season; the floor's profit per growing hour, at soil 0.875
            public double[] Sowing = new double[4];  // by season; one sowing's profit at the same prices
        }

        /// <summary>Every field crop on spec 020 §3.4's two price bases.</summary>
        public static List<LadderCrop> LadderCrops(SimConfig config)
        {
            GameDefs game = config.Game;
            Simulation sim = Simulation.NewGame(config, new NewGame(1, 0, 8 * 3600));
            double wage = game.Farm.Founder.DayLabourWagePerHour / 100.0, soil = 0.875;
            var list = game.Farm.Crops.Where(c => !c.Fodder).Select(c =>
            {
                double yield = Economy.ToDouble(c.SeasonYieldPerAcre), seed = Economy.ToDouble(c.SeedPerAcrePerSeason);
                double gm = yield * Economy.ToDouble(game.Market.Goods[c.OutputGood].BasePrice) - seed * Economy.ToDouble(game.Market.Goods[c.SeedGood].BasePrice);
                return new LadderCrop { Id = c.Id, Gm = gm, Net = gm - Economy.ToDouble(c.LabourPerAcrePerDay) * sim.Clock.DaysPerSeason * wage };
            }).ToList();
            for (int s = 0; s < 4; s++)
            {
                sim.AdvanceTo(s * sim.Clock.SecondsPerSeason + 9 * 3600);
                foreach (LadderCrop l in list)
                {
                    CropDef c = game.Farm.Crops[game.Farm.IndexOfCrop(l.Id)];
                    double share = c.GrowthSeconds / (double)sim.Clock.SecondsPerSeason;
                    l.Sowing[s] = share * (Economy.ToDouble(c.SeasonYieldPerAcre) * soil * Economy.ToDouble(MarketRules.Floor(sim, c.OutputGood))
                        - Economy.ToDouble(c.SeedPerAcrePerSeason) * Economy.ToDouble(MarketRules.InputPrice(sim, c.SeedGood)));
                    l.PerHour[s] = l.Sowing[s] / (c.GrowthSeconds / 3600.0);
                }
            }
            return list;
        }

        /// <summary>Spec 020 §3.4's checks from the loaded content: what breaks them, empty when none does.</summary>
        public static List<string> CropLadderRule(SimConfig config)
        {
            var crops = config.Game.Farm.Crops;
            bool Sown(LadderCrop l, int s) { return crops[config.Game.Farm.IndexOfCrop(l.Id)].SowSeasons[s]; }
            List<LadderCrop> all = LadderCrops(config);
            List<LadderCrop> old = all.Where(l => ExistingCrops.Contains(l.Id)).ToList();
            var failures = new List<string>();
            double floor = 0.9 * old.Min(l => l.Gm);
            foreach (LadderCrop n in all.Except(old))
            {
                if (n.Gm < floor) failures.Add(n.Id + ": GM " + n.Gm.ToString("0") + " under the floor " + floor.ToString("0"));
                for (int s = 0; s < 4; s++)
                {
                    if (!Sown(n, s)) continue;
                    List<LadderCrop> here = old.Where(l => Sown(l, s)).ToList();
                    if (n.Gm > here.Max(l => l.Gm)) failures.Add(n.Id + ": GM above the leader in season " + s);
                    if (n.Net > here.Max(l => l.Net)) failures.Add(n.Id + ": Net above the leader in season " + s);
                    if (n.PerHour[s] > here.Max(l => l.PerHour[s])) failures.Add(n.Id + ": " + n.PerHour[s].ToString("0.0") + " an hour above the leader's " + here.Max(l => l.PerHour[s]).ToString("0.0") + " in season " + s);
                }
            }
            return failures;
        }

        /// <summary>Spec 020 §3.4's niche: the realistic cases where each new crop is the best pick, by crop.</summary>
        public static Dictionary<string, List<string>> CropLadderNiches(SimConfig config)
        {
            FarmDefs farm = config.Game.Farm;
            string[] seasons = { "Spring", "Summer", "Monsoon", "Winter" };
            List<LadderCrop> all = LadderCrops(config);
            CropDef Def(LadderCrop l) { return farm.Crops[farm.IndexOfCrop(l.Id)]; }
            var niches = all.Where(l => !ExistingCrops.Contains(l.Id)).ToDictionary(l => l.Id, l => new List<string>());
            void Win(string id, string why) { if (niches.ContainsKey(id)) niches[id].Add(why); }
            for (int s = 0; s < 4; s++)
            {
                int season = s;
                List<LadderCrop> here = all.Where(l => Def(l).SowSeasons[season]).ToList();
                foreach (long back in here.Select(l => Def(l).GrowthSeconds).Distinct())
                    Win(here.Where(l => Def(l).GrowthSeconds <= back).OrderByDescending(l => l.Sowing[season]).First().Id, seasons[s] + ": back in " + back / 3600.0 + " h");
                Fixed dry = Fixed.FromInt(25);
                Win(here.OrderByDescending(l => l.PerHour[season] * Economy.ToDouble(FarmRules.WaterFactor(farm.Farm, Def(l), dry))).First().Id, seasons[s] + ": a dry acre");
                Win(here.OrderByDescending(l => l.Gm / Economy.ToDouble(Def(l).LabourPerAcrePerDay)).First().Id, seasons[s] + ": light labour");
                Win(here.OrderByDescending(l => Economy.ToDouble(Def(l).FertilityPerSeason)).ThenByDescending(l => l.Gm).First().Id, seasons[s] + ": tired soil");
            }
            return niches;
        }

        /// <summary>The hand's pick each morning on the inherited acres, no fertiliser (spec 020 §9): by season, crop → share.</summary>
        public static Dictionary<string, double>[] HandPicks(SimConfig config, int seeds, int seasons = 8)
        {
            var counts = new Dictionary<string, long>[4];
            for (int s = 0; s < 4; s++) counts[s] = new Dictionary<string, long>();
            Parallel.For(0, seeds, i => Economy.RunBot(config, new FarmerBot(), seasons, (ulong)(i + 1), sim =>
            {
                long t = sim.State.Clock.SimTime;
                if (t % ClockDef.SecondsPerDay / 3600 != 7) return;
                var mine = counts[(int)Sim.Core.Time.Calendar.SeasonAt(t, sim.Clock)];
                foreach (int tile in Home.Acres(sim.Config.Game))
                {
                    int pick = FarmRules.BestCrop(sim, sim.State.Land.Tiles[tile], sim.Config.Game.Farm.Fertilisers[0]);
                    if (pick < 0) continue;
                    string id = sim.Config.Game.Farm.Crops[pick].Id;
                    lock (mine) { mine.TryGetValue(id, out long n); mine[id] = n + 1; }
                }
            }));
            return counts.Select(m => m.ToDictionary(kv => kv.Key, kv => kv.Value / (double)Math.Max(1, m.Values.Sum()))).ToArray();
        }

        /// <summary>Spec 020 AC-9: the ladder and diversifier bots against the competent bot.</summary>
        public static List<StressResult> CropLadderBots(SimConfig config, int seeds, int seasons = 8)
        {
            return Stress(config, seeds, seasons, new Func<Bot>[] { () => new LadderBot(), () => new DiversifierBot() });
        }

        /// <summary>Spec 020 AC-20: every season's best mix against the competent bot.</summary>
        public static List<StressResult> CropLadderBestMix(SimConfig config, int seeds, int seasons = 8)
        {
            return Stress(config, seeds, seasons, new Func<Bot>[]
            {
                () => new ForcedCropBot("maize in Spring", new[] { "maize", null, null, null }),
                () => new ForcedCropBot("mustard in Winter", new string[] { null, null, null, "mustard" }),
                () => new ForcedCropBot("maize and mustard", new[] { "maize", null, null, "mustard" }),
            });
        }

        /// <summary>Spec 020 AC-10: seeds of the ladder bot with a harvest over its cap.</summary>
        public static int CropLadderOverCap(SimConfig config, int seeds, int seasons = 8)
        {
            int over = 0;
            Parallel.For(0, seeds, i =>
            {
                if (Economy.RunBot(config, new LadderBot(), seasons, (ulong)(i + 1), null).Seasons.Any(s => s.OverCap > 0)) Interlocked.Increment(ref over);
            });
            return over;
        }

        /// <summary>
        /// Spec 020 AC-21: each new crop on every acre the bot can tend through a whole game. Every close reconciles (strict checks), 1200 equals
        /// the inputs at cost, the farm's income is its record of sales and home use line by line, a save round-trips, nothing goes over its cap.
        /// </summary>
        public static List<string> CropLadderLedger(SimConfig config, int seeds, int seasons = 8)
        {
            if (!config.Options.StrictChecks) throw new ArgumentException("the ledger check needs strict checks");
            FarmDefs farm = config.Game.Farm;
            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            foreach (CropDef crop in farm.Crops.Where(c => !c.Fodder && !ExistingCrops.Contains(c.Id)))
                Parallel.For(0, seeds, i =>
                {
                    string[] bySeason = Enumerable.Range(0, 4).Select(s => crop.SowSeasons[s] ? crop.Id : null).ToArray();
                    string who = crop.Id + " seed " + (i + 1) + ": ";
                    int seen = 0;
                    var sold = new List<long>();
                    try
                    {
                        Economy.Run run = Economy.RunBot(config, new ForcedCropBot(crop.Id, bySeason), seasons, (ulong)(i + 1), sim =>
                        {
                            FinanceState f = sim.State.Finance;
                            if (f.Reports.Count == seen) return;
                            seen = f.Reports.Count;
                            SeasonReport r = f.Reports[seen - 1];
                            long stock = 0;
                            for (int g = 0; g < config.Game.Market.Goods.Count; g++) stock += sim.State.Market.StockCostOf(g);
                            if (stock != sim.State.Ledger.Balance(Account.InputsInStore)) failures.Add(who + "1200 is " + sim.State.Ledger.Balance(Account.InputsInStore) + ", inputs at cost " + stock);
                            sold.Add(f.Sales.Last.Sum(x => x.Amount));
                            var again = Sim.Core.Saves.SaveCodec.Read(Sim.Core.Saves.SaveCodec.Write(sim.State, config, 0), config, Sim.Core.Saves.SaveCodec.SchemaVersion);
                            if (again.State.ComputeHash() != sim.State.ComputeHash()) failures.Add(who + "the save didn't round-trip");
                        });
                        if (run.Seasons.Any(s => s.OverCap > 0)) failures.Add(who + "over its cap");
                        // Co-op milk has its own line, not a sale in the record, and the milk the house drinks is in both the milk account and home use.
                        for (int k = 0; k < run.Seasons.Count; k++)
                        {
                            SeasonReport r = run.Seasons[k].Report;
                            long crops = r.ByAccount.Where(a => a.Key == Account.CropSales || a.Key == Account.FruitSales).Sum(a => a.Value);
                            long recorded = sold[k] + r.HomeUse - run.Seasons[k].HomeMilk;
                            if (Math.Abs(crops) != recorded) failures.Add(who + "season " + (k + 1) + ": crops and fruit " + Math.Abs(crops) + ", sales and home use " + recorded);
                        }
                    }
                    catch (InvalidOperationException e) { failures.Add(who + e.Message); }
                });
            return failures.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        /// <summary>The balancer's report for spec 020 §9.</summary>
        public static int PrintCropLadder(SimConfig config, int seeds)
        {
            string[] seasons = { "Spring", "Summer", "Monsoon", "Winter" };
            Console.WriteLine("Crop ladder (spec 020), " + seeds + " seeds × 8 seasons");
            Console.WriteLine("  Rule: " + (CropLadderRule(config) is var rule && rule.Count == 0 ? "pass" : string.Join("; ", rule)));
            foreach (LadderCrop l in LadderCrops(config))
                Console.WriteLine("    " + l.Id.PadRight(9) + " GM " + l.Gm.ToString("0").PadLeft(6) + "  Net " + l.Net.ToString("0").PadLeft(6) + "  an hour " + string.Join(" / ", l.PerHour.Select(p => p.ToString("0"))));
            foreach (var n in CropLadderNiches(config)) Console.WriteLine("  Niche, " + n.Key + ": " + string.Join(", ", n.Value));
            var picks = HandPicks(config, Math.Min(seeds, 16));
            for (int s = 0; s < 4; s++) Console.WriteLine("  Hand's picks, " + seasons[s] + ": " + string.Join(", ", picks[s].OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + (kv.Value * 100).ToString("0") + "%")));
            foreach (StressResult s in CropLadderBots(config, seeds).Concat(CropLadderBestMix(config, seeds).Skip(1))) Console.WriteLine("  " + s.Bot.PadRight(20) + s.MedianNetWorth.ToString("N0", CultureInfo.InvariantCulture).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            return 0;
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
            public int Seeds, LoansTaken, BankLoans, Foreclosures, SeedsForeclosed, SeedsDefaulted, DefaultedNotForeclosed;
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
                bool defaulted = c.History.Exists(h => h.Reason == "score.default");
                if (defaulted) a.SeedsDefaulted++;
                if (defaulted && c.Foreclosures == 0) a.DefaultedNotForeclosed++;
            }
            a.MedianScoreAtEnd = Median(runs.Select(r => (double)r.Sim.State.Credit.Score));
            return a;
        }

        // ---- Spec 015 §9: HR ----

        public sealed class HrResult
        {
            public string Bot;
            public List<double> Motivation = new List<double>(), Condition = new List<double>(), FarmProfit = new List<double>(), Wages = new List<double>(),
                EventsPerWorkerSeason = new List<double>(), SeasonsToQuit = new List<double>(),
                Fair = new List<double>(), Home = new List<double>(), Mood = new List<double>(), Fatigue = new List<double>();
            public int Hands, Quits;
            public Dictionary<Sim.Core.People.HrEventKind, int> Kinds = new Dictionary<Sim.Core.People.HrEventKind, int>();
        }

        /// <summary>
        /// A farm bot's hands for <paramref name="seasons"/>: motivation and motivation × (1 − fatigue) per hand-hour under contract, HR
        /// events per worker-season, the farm's profit (year-0 prices) and wages over the run, and how long each hand who left had stayed.
        /// </summary>
        public static HrResult Hr(SimConfig config, Func<Bot> make, int seeds, int seasons = 12)
        {
            var all = new HrResult { Bot = make().Name };
            Parallel.For(0, seeds, i =>
            {
                double m = 0, c = 0, hours = 0, fair = 0, home = 0, mood = 0, tired = 0;
                int events = 0, hands = 0;
                var hired = new Dictionary<long, long>();
                var stayed = new List<double>();
                var kinds = new Dictionary<Sim.Core.People.HrEventKind, int>();
                Economy.Run r = Economy.RunBot(config, make(), seasons, (ulong)(i + 1), sim =>
                {
                    foreach (Sim.Core.People.Hand h in sim.State.People?.Hands ?? new List<Sim.Core.People.Hand>())
                    {
                        if (h.Contract != Sim.Core.People.ContractState.Active || Sim.Core.People.WorkRules.Away(h)) continue;
                        m += Economy.ToDouble(Sim.Core.People.WorkRules.Motivation(sim, h));
                        c += Economy.ToDouble(Sim.Core.People.WorkRules.Condition(sim, h));
                        fair += Economy.ToDouble(Sim.Core.People.WorkRules.Fairness(sim, h));
                        home += Economy.ToDouble(Sim.Core.People.WorkRules.Home(sim, h));
                        mood += Economy.ToDouble(Sim.Core.People.HrRules.Mood(sim, h));
                        tired += Economy.ToDouble(h.Fatigue);
                        hours++;
                    }
                    foreach (Sim.Core.Events.SimEvent e in sim.PendingEvents)
                    {
                        if (e is Sim.Core.Events.HandHired hh) { hired[hh.HandId] = e.SimTime; hands++; }
                        if (e is Sim.Core.Events.HandLeft hl && hired.TryGetValue(hl.HandId, out long at)) stayed.Add((e.SimTime - at) / (double)sim.Clock.SecondsPerSeason);
                        if (e is Sim.Core.Events.HrEventRaised x && x.Place == 0)
                        {
                            events++;
                            kinds[x.Kind] = kinds.TryGetValue(x.Kind, out int n) ? n + 1 : 1;
                        }
                    }
                });
                lock (all)
                {
                    double workerSeasons = hours * 3600 / r.Sim.Clock.SecondsPerSeason;
                    if (hours > 0)
                    {
                        all.Motivation.Add(m / hours);
                        all.Condition.Add(c / hours);
                        all.Fair.Add(fair / hours);
                        all.Home.Add(home / hours);
                        all.Mood.Add(mood / hours);
                        all.Fatigue.Add(tired / hours);
                        all.EventsPerWorkerSeason.Add(events / workerSeasons);
                    }
                    all.FarmProfit.Add(r.Seasons.Sum(x => x.RealFarmProfit));
                    all.Wages.Add(r.Seasons.Sum(x => Economy.Real(x.Report.ByAccount.Where(k => k.Key == Account.HiredLabour).Sum(k => Math.Abs(k.Value)), x.Index)));
                    all.Hands += hands;
                    all.Quits += stayed.Count;
                    all.SeasonsToQuit.AddRange(stayed);
                    foreach (var k in kinds) all.Kinds[k.Key] = (all.Kinds.TryGetValue(k.Key, out int n) ? n : 0) + k.Value;
                }
            });
            return all;
        }

        /// <summary>Phase 1's output for spec 015 §9's baseline: motivation fixed at 1, no fatigue, and of the HR events only raise requests (Phase 1's wages followed skill).</summary>
        public static string Phase1Output(string file, string json)
        {
            if (file != "people.json") return json;
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"(motivationMin|motivationMax)\": [0-9.]+", "\"$1\": 1.00");
            return System.Text.RegularExpressions.Regex.Replace(json, "\"(poachChance|sickChance|emergencyChance|badBatchChance|fatigueUp)\": [0-9.]+", "\"$1\": 0");
        }

        /// <summary>Spec 015 §9 printed: the fair bot with output neutral and only raise requests (Phase 1, where wages followed skill), the fair bot, the 10%-low bot.</summary>
        public static int PrintHr(ConfigFor configFor, int seeds, int seasons)
        {
            SimConfig live = configFor(null);
            SimConfig neutral = configFor(Phase1Output);
            string C(double v) { return v.ToString("N0", CultureInfo.InvariantCulture); }
            string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
            foreach (var (name, config, make) in new (string, SimConfig, Func<Bot>)[] {
                ("phase 1", neutral, () => new FarmerBot()), ("fair", live, () => new FarmerBot()), ("low-pay", live, () => new LowPayBot()) })
            {
                HrResult r = Hr(config, make, seeds, seasons);
                Console.WriteLine(name + ": motivation " + F(Median(r.Motivation)) + " (" + F(r.Motivation.DefaultIfEmpty().Min()) + "–" + F(r.Motivation.DefaultIfEmpty().Max())
                    + ") [fair " + F(Median(r.Fair)) + ", home " + F(Median(r.Home)) + ", mood " + F(Median(r.Mood)) + ", fatigue " + F(Median(r.Fatigue)) + "], × (1 − fatigue) " + F(Median(r.Condition)) + "; farm profit over the run " + C(Median(r.FarmProfit)) + "; wages " + C(Median(r.Wages))
                    + "; HR events a worker-season " + F(Median(r.EventsPerWorkerSeason)) + " (max " + F(r.EventsPerWorkerSeason.DefaultIfEmpty().Max()) + ")"
                    + "; hands hired " + r.Hands + ", left " + r.Quits + " (seasons stayed median " + F(Median(r.SeasonsToQuit)) + ")"
                    + "; events " + string.Join(", ", r.Kinds.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            }
            return 0;
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
                    + " foreclosures in " + d.SeedsForeclosed + " of " + d.Seeds + " runs (" + d.SeedsDefaulted + " defaulted), median score " + d.MedianScoreAtEnd.ToString("0", CultureInfo.InvariantCulture));
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
            TownResult town = Town(config, () => new FarmerBot(), Math.Min(seeds, 20));
            List<double> d5 = town.Day5.Where(d => !double.IsNaN(d)).OrderBy(d => d).ToList();
            Console.WriteLine("  Town (spec 014 §9): 5 households by day " + (d5.Count == 0 ? "-" : d5[d5.Count / 2].ToString("0.0", CultureInfo.InvariantCulture))
                + " (median of " + d5.Count + "; target 21), " + (town.People21.Count == 0 ? "-" : town.People21.OrderBy(x => x).ElementAt(town.People21.Count / 2).ToString())
                + " people at day 21 (12–25)");
            foreach (Func<Bot> make in new Func<Bot>[] { () => new MillBot(false), () => new MillBot(true), () => new MillBot(false, true) })
            {
                MillResult m = Mill(config, make, Math.Min(seeds, 12));
                List<double> pr = m.Profit.OrderBy(x => x).ToList(), pb = m.Payback.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                Console.WriteLine("  Mill (spec 016 §9, ADR 0072) " + m.Bot + ": founded " + m.Founded + " of " + m.Seeds + ", profit a season "
                    + (pr.Count == 0 ? "-" : C((long)pr[0] * Money.Coin) + " / " + C((long)pr[pr.Count / 2] * Money.Coin) + " / " + C((long)pr[pr.Count - 1] * Money.Coin))
                    + " (min / median / max), payback " + (pb.Count == 0 ? "never" : pb[pb.Count / 2].ToString("0.0", CultureInfo.InvariantCulture) + " seasons")
                    + ", most closes overdrawn in a row " + (m.OverdrawnRun.Count == 0 ? 0 : m.OverdrawnRun.Max()) + ", closed " + m.Closed + ", foreclosed " + m.Foreclosed);
            }
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
            TradeResult trade = Trade(config, n);
            Console.WriteLine("  The truck and orders (spec 011 §9): the truck filled on " + (100 * trade.FullShare).ToString("0.0", CultureInfo.InvariantCulture)
                + "% of days (under 10%); supermarket orders " + (100 * trade.OrderShare).ToString("0.0", CultureInfo.InvariantCulture) + "% of sales (under 20%)");
            Console.WriteLine("  Bounded prices: the rail in " + (100 * RailShare(config, n)).ToString("0.000", CultureInfo.InvariantCulture) + "% of good-hours (under 0.1%)");
            Console.WriteLine("  Land-flipper over 4 years (it can first afford a parcel in year 3):");
            foreach (StressResult s in Stress(config, Math.Min(seeds, 100), 16, new Func<Bot>[] { () => new LandFlipperBot() }))
                Console.WriteLine("    " + s.Bot.PadRight(20) + C(s.MedianNetWorth).PadLeft(12) + "  " + s.Ratio.ToString("0.000", CultureInfo.InvariantCulture));
            Console.WriteLine("  (" + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s)");
            return 0;
        }
    }
}
