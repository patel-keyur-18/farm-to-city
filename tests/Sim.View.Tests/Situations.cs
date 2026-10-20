using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// Seeded Phase 1 situations for the views' sweeping tests (spec 008 AC-1, AC-15): a new game, a growing farm with people and
    /// trees, the same farm ready to harvest with goods on offer, and a later game with loans and a closed season.
    /// </summary>
    public static class Situations
    {
        private const long Hour = 3600, Day = 86400;

        public static Simulation Rich(ulong seed = 42)
        {
            return ViewKit.NewGame(seed: seed, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
        }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>Potato on the well acres, a garden bed, a mango, day labourers, a hut and a hand with an order.</summary>
        public static Simulation Growing()
        {
            Simulation sim = Rich();
            Ok(sim, new Sow(T(sim), 20, "potato", SeedSource.Certified, "compost"));
            Ok(sim, new Sow(T(sim), 28, "radish", SeedSource.Certified, "none"));
            GardenBed bed = sim.State.Farm.Beds[0];
            Ok(sim, new SowGarden(T(sim), bed.Id, "garden_spinach"));
            TreeSlot slot = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard && s.TreeId == 0);
            Ok(sim, new PlantTree(T(sim), slot.Id, "mango"));
            Ok(sim, new HireDayLabourers(T(sim), 2));
            Ok(sim, new BuildHandHut(T(sim), 37));
            for (int i = 0; i < 24 * 10 && sim.State.People.Hands.Count == 0; i++)
            {
                sim.AdvanceTo(T(sim) + Hour);
                if (sim.State.People.Applicants.Count > 0) Ok(sim, new OfferContract(T(sim), sim.State.People.Applicants[0].Id));
            }
            Assert.That(sim.State.People.Hands.Count, Is.EqualTo(1));
            Ok(sim, new SetLabourSplit(T(sim), 25));
            sim.AdvanceTo(T(sim) + 2 * Hour);
            return sim;
        }

        /// <summary>The growing farm a day and a half later: ready crops and goods on offer.</summary>
        public static Simulation Ready()
        {
            Simulation sim = Growing();
            sim.AdvanceTo(T(sim) + 30 * Hour);
            foreach (Crop c in sim.State.Farm.Crops.Where(c => c.State == CropState.Ready && c.TileId == 28).ToList())
                Ok(sim, new Harvest(T(sim), c.TileId));
            int radish = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "radish");
            if (sim.State.Market.InStore(radish) > 0) Ok(sim, new Offer(T(sim), "radish", sim.State.Market.InStore(radish) / 2));
            return sim;
        }

        /// <summary>A moneylender loan and a closed season (a report), with the events of the last away advance.</summary>
        public static Simulation Later(out IReadOnlyList<SimEvent> away)
        {
            Simulation sim = Ready();
            Ok(sim, new TakeLoan(T(sim), "moneylender", 20_000 * 100, 4));
            sim.DrainEvents();
            long next = (T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(next + 2 * Hour, AdvanceMode.Away);
            away = sim.DrainEvents();
            Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThan(0));
            return sim;
        }

        public static IEnumerable<Simulation> All()
        {
            yield return Rich();
            yield return Growing();
            yield return Ready();
            yield return Later(out _);
        }
    }
}

namespace Sim.View.Tests
{
    /// <summary>The Sprint 7 panels (ADR 0043) for the sweeping tests; each new sheet joins here.</summary>
    public static class Panels
    {
        public static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, Sim.View.Views.SheetVm>> All(Sim.View.Views.ViewContext ctx)
        {
            yield break;
        }
    }
}
