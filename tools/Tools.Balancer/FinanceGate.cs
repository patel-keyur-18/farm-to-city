using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sim.Core;
using Sim.Core.Events;
using Sim.Core.Finance;

namespace Tools.Balancer
{
    /// <summary>Spec 019 §9 (Sprint 18): what the finance hub shows a bot, and every bot's books.</summary>
    public static class FinanceGate
    {
        public sealed class FinanceResult
        {
            public string Bot;
            public int Seeds;

            /// <summary>Per seed that reached the Village: debt to assets then (%), and passive income a season later (% of living).</summary>
            public List<int> DebtAtVillage = new List<int>(), PassiveAtVillage = new List<int>();

            /// <summary>Per seed: the emergency fund on day 30, in tenths of a season (14 is 1.4). −1 when nothing was due.</summary>
            public List<long> Emergency30 = new List<long>();

            /// <summary>Per milestone id, the day each seed reached it (seeds that never did are left out).</summary>
            public Dictionary<string, List<double>> MilestoneDays = new Dictionary<string, List<double>>();
        }

        /// <summary>Plays a bot for <paramref name="seasons"/> and reads the hub's numbers at the Village, on day 30, and when each milestone comes.</summary>
        public static FinanceResult Measure(SimConfig config, Func<Bot> make, int seeds, int seasons = 24)
        {
            var rows = new FinanceResult[seeds];
            string name = make().Name;
            Parallel.For(0, seeds, i =>
            {
                double village = double.NaN;
                int debt = -1, passive = -1;
                long emergency = long.MinValue;
                var days = new Dictionary<string, double>();
                Economy.RunBot(config, make(), seasons, (ulong)(i + 1), sim =>
                {
                    double day = sim.State.Clock.SimTime / 86400.0;
                    foreach (SimEvent e in sim.PendingEvents)
                    {
                        if (e is EraUnlocked u && u.EraId == "village") village = e.SimTime / 86400.0;
                        if (e is MilestoneReached m && !days.ContainsKey(m.Id)) days[m.Id] = e.SimTime / 86400.0;
                    }
                    if (!double.IsNaN(village) && debt < 0) debt = Hub.DebtPercent(Reporting.NetWorth(sim));
                    if (!double.IsNaN(village) && passive < 0 && day >= village + 4) passive = Hub.PassivePercent(sim); // a season on: the close has read it
                    if (emergency == long.MinValue && day >= 30) emergency = Hub.EmergencyTenths(sim);
                });
                var r = new FinanceResult();
                if (debt >= 0) r.DebtAtVillage.Add(debt);
                if (passive >= 0) r.PassiveAtVillage.Add(passive);
                if (emergency != long.MinValue) r.Emergency30.Add(emergency);
                foreach (KeyValuePair<string, double> kv in days) r.MilestoneDays[kv.Key] = new List<double> { kv.Value };
                rows[i] = r;
            });
            var all = new FinanceResult { Bot = name, Seeds = seeds };
            foreach (FinanceResult r in rows)
            {
                all.DebtAtVillage.AddRange(r.DebtAtVillage);
                all.PassiveAtVillage.AddRange(r.PassiveAtVillage);
                all.Emergency30.AddRange(r.Emergency30);
                foreach (KeyValuePair<string, List<double>> kv in r.MilestoneDays)
                {
                    if (!all.MilestoneDays.TryGetValue(kv.Key, out List<double> list)) all.MilestoneDays[kv.Key] = list = new List<double>();
                    list.AddRange(kv.Value);
                }
            }
            return all;
        }

        /// <summary>Every bot the balancer has, each in the way a player might play it (the books are checked for all of them).</summary>
        public static Func<Bot>[] EveryBot()
        {
            return new Func<Bot>[]
            {
                () => new FarmerBot(), () => new FieldHolderBot(), () => new SubsistenceDodgerBot(), () => new LandFlipperBot(), () => new OfferChurnerBot(),
                () => new MandiEveryDayBot(), () => new LeverageBot(), () => new StrategicDefaulterBot(), () => new CautiousSaverBot(), () => new LowPayBot(),
                () => new RandomBot(), () => new LadderBot(), () => new DiversifierBot(), () => new RecoveringBot(), () => new SoloBot(),
                () => new DayLabourSpammerBot(), () => new HandChurnerBot(), () => new LapseGamerBot(), () => new AbsenteeBot(), () => new DairyBot(),
                () => new FarmToolBot(), () => new MillBot(false, true), () => new MillBot(true), () => new VillageBot(false), () => new VillageBot(true),
                () => new BusinessBot(false, false, "bakery"), () => new DepositLoopBot(),
            };
        }

        /// <summary>Plays <paramref name="make"/> for <paramref name="seasons"/> and returns the town (the tests read its books).</summary>
        public static Simulation Play(SimConfig config, Func<Bot> make, ulong seed, int seasons)
        {
            return Economy.RunBot(config, make(), seasons, seed, null).Sim;
        }
    }
}
