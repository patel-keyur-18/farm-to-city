using System;
using System.Linq;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Tools.Balancer
{
    /// <summary>
    /// Spec 012 §9: one animal's economics over a whole lactation cycle (in milk, dry, a calf), played through player commands on
    /// the live rules: milk and the calf against everything she eats and the vet. "Home-grown" swaps the bought green fodder and
    /// straw for their own cost (an acre's fodder seed shared by the animals it feeds; straw from your own wheat costs nothing).
    /// Family labour isn't costed, as farmers reckon dairy.
    /// </summary>
    public static class Dairy
    {
        public sealed class Result
        {
            public string Kind;
            public long Price, Days, Milk, Calf, Feed, GreenBought, Straw, Vet;

            /// <summary>Net a game day, all feed bought.</summary>
            public double NetPerDay { get { return (Milk + Calf - Feed - Vet) / (double)Days; } }

            /// <summary>Net a game day with home-grown green fodder and straw (the fodder seed shared by about 6 animals).</summary>
            public double HomeNetPerDay { get { return NetPerDay + (GreenBought + Straw - FodderSeedShare) / (double)Days; } }

            public long FodderSeedShare;

            public double PaybackYears { get { return Price / NetPerDay / 16; } }
            public double HomePaybackYears { get { return Price / HomeNetPerDay / 16; } }
        }

        /// <summary>Runs one cycle for the animal kind in a fresh town (Summer start, so sorghum is in season for the fodder cost).</summary>
        public static Result Cycle(Gate.ConfigFor configFor, string kind, ulong seed = 1)
        {
            SimConfig config = configFor((f, j) =>
            {
                if (f == "livestock.json") j = j.Replace("\"unlockEra\": \"hamlet\"", "\"unlockEra\": \"homestead\"");
                if (f == "start.json") j = j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000");
                if (f == "market.json") j = j.Replace("\"inflationMinPerYear\": 0.02, \"inflationMaxPerYear\": 0.06", "\"inflationMinPerYear\": 0, \"inflationMaxPerYear\": 0");
                return j;
            });
            Simulation sim = Simulation.NewGame(config, new NewGame(seed, 1_800_000_000, 0));
            long day = 86_400;
            sim.AdvanceTo(sim.Clock.SecondsPerSeason + 6 * 3600); // Summer, 6:00
            long t = sim.State.Clock.SimTime;
            Must(sim, new BuildShed(t));
            sim.AdvanceTo(t + day);
            t = sim.State.Clock.SimTime;
            var defs = sim.Config.Game.Livestock;
            var a = defs.Animals[defs.IndexOfAnimal(kind)];
            int cycle = a.MilkDays + a.DryDays;
            Must(sim, new BuyAnimal(t, kind));
            Must(sim, new Buy(t, "straw", (a.Straw * defs.Livestock.RealDaysPerGameDay * Fixed.FromInt(1000 * (cycle + 1))).RoundToLong()));
            Must(sim, new Buy(t, "cattle_feed", (a.Feed * defs.Livestock.RealDaysPerGameDay * Fixed.FromInt(1000 * (cycle + 1))).RoundToLong()));
            Ledger l = sim.State.Ledger;
            sim.DrainEvents();
            long green = 0, strawGrams = 0, strawCost = 0, milk = 0, calf = 0, feed = 0, vet = 0;
            for (int d = 0; d < cycle; d++)
            {
                long before = sim.State.Clock.SimTime;
                sim.AdvanceTo(t + (d + 1) * day);
                // Summed a day at a time: the journal keeps only recent seasons, and income balances close at every season close.
                milk -= Posted(l, before, Account.MilkSales);
                calf -= Posted(l, before, Account.LivestockSales);
                feed += Posted(l, before, Account.AnimalFeed);
                vet += Posted(l, before, Account.VetAndBreeding);
                foreach (AnimalsFed f in sim.DrainEvents().OfType<AnimalsFed>())
                {
                    green += f.GreenBoughtAmount;
                    strawGrams += f.StrawGiven;
                }
            }
            int straw = defs.Straw;
            strawCost = MarketRules.Value(sim, straw, strawGrams, MarketRules.InputPrice(sim, straw));
            var crop = sim.Config.Game.Farm.Crops[sim.Config.Game.Farm.IndexOfCrop("sorghum")];
            long seedPerStand = MarketRules.Value(sim, crop.SeedGood, crop.SeedPerAcrePerSeason.MulRatio(1000, 1).RoundToLong(), MarketRules.InputPrice(sim, crop.SeedGood));
            return new Result
            {
                Kind = kind, Price = MarketRules.Indexed(sim, a.Price), Days = cycle,
                Milk = milk, Calf = calf, Feed = feed, Vet = vet, GreenBought = green, Straw = strawCost,
                FodderSeedShare = seedPerStand * cycle / 8 / 6, // a stand is about 8 game days; an acre feeds about 6 animals
            };
        }

        private static long Posted(Ledger l, long from, Account account)
        {
            return l.Entries.Where(e => e.SimTime > from && e.DescriptionKey != "ledger.closeSeason").SelectMany(e => e.Postings).Where(p => p.Account == account).Sum(p => p.Amount);
        }

        private static void Must(Simulation sim, Command c)
        {
            CommandResult r = sim.Execute(c);
            if (!r.Accepted) throw new InvalidOperationException(c.GetType().Name + ": " + r);
        }

        public static void Print(Gate.ConfigFor configFor)
        {
            foreach (string kind in new[] { "buffalo", "cow" })
            {
                Result r = Cycle(configFor, kind);
                Console.WriteLine("  " + kind + ": a " + r.Days + "-day cycle: milk " + r.Milk / 100 + ", calf " + r.Calf / 100 + ", feed " + r.Feed / 100 + " (green bought "
                    + r.GreenBought / 100 + ", straw " + r.Straw / 100 + "), vet " + r.Vet / 100 + "; net " + (r.NetPerDay / 100).ToString("0") + " a day (home-grown "
                    + (r.HomeNetPerDay / 100).ToString("0") + "); payback " + r.PaybackYears.ToString("0.00") + " years (home-grown " + r.HomePaybackYears.ToString("0.00") + ")");
            }
        }
    }
}
