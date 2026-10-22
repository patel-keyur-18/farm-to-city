using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Progress;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>The Hamlet gate (spec 009 §3.6, AC-8; ADR 0030, 0032).</summary>
    public class EraTests
    {
        private static readonly int[] SixAcres = { 84, 85, 86, 100, 101, 102 };
        private static readonly Fixed Good = Fixed.Parse("0.95");

        private static int Unlocks(Simulation sim) { return sim.PendingEvents.OfType<EraUnlocked>().Count(); }

        /// <summary>A town with a finished hand's hut and one profitable season on the books.</summary>
        private static Simulation Ready(bool hut = true, int profitable = 1)
        {
            Simulation sim = Start();
            if (hut) BuildHut(sim);
            for (int i = 0; i < profitable; i++) EraRules.OnSeasonClosed(sim, 100, sim.Period - 1);
            sim.DrainEvents();
            return sim;
        }

        [Test]
        public void HamletGate_RealHarvestCounts()
        {
            Simulation sim = Start();
            Ok(sim, new Sow(sim.State.Clock.SimTime, 86, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Hour);
            sim.DrainEvents();
            Ok(sim, new Harvest(sim.State.Clock.SimTime, 86));
            CropHarvested h = Last<CropHarvested>(sim);
            EraProgress p = Last<EraProgress>(sim);
            Assert.That(sim.State.Era.SeasonAcres, Is.EqualTo(new[] { 86 }));
            Assert.That(sim.State.Era.SeasonCareSum, Is.EqualTo(h.Breakdown.Care));
            Assert.That(p.Acres, Is.EqualTo(1));
            Assert.That(p.MeanCare, Is.EqualTo(h.Breakdown.Care));
        }

        [Test]
        public void HamletGate_UnlocksOnceWhenAllHold()
        {
            Simulation sim = Ready();
            foreach (int t in SixAcres.Take(5)) EraRules.OnHarvest(sim, t, Good);
            Assert.That(Unlocks(sim), Is.EqualTo(0), "5 acres");
            EraRules.OnHarvest(sim, SixAcres[5], Good);
            Assert.That(Unlocks(sim), Is.EqualTo(1));
            Assert.That(sim.PendingEvents.OfType<EraUnlocked>().Single().EraId, Is.EqualTo("hamlet"));
            Assert.That(sim.State.Era.Current, Is.EqualTo(1));
            Assert.That(sim.State.Era.UnlockedAt, Is.EqualTo(sim.State.Clock.SimTime));
            EraRules.OnHarvest(sim, 84, Good);
            EraRules.OnSeasonClosed(sim, 100, sim.Period);
            Assert.That(Unlocks(sim), Is.EqualTo(1), "once");
        }

        [Test]
        public void HamletGate_SixHarvestsOnFewerAcresStayLocked()
        {
            Simulation sim = Ready();
            foreach (int t in new[] { 84, 85, 86, 100, 101, 101, 101 }) EraRules.OnHarvest(sim, t, Good);
            Assert.That(Unlocks(sim), Is.EqualTo(0));
            Assert.That(EraRules.Acres(sim, sim.Period), Is.EqualTo(5));
        }

        [Test]
        public void HamletGate_MeanCareBelowBoundStaysLocked()
        {
            Simulation exact = Ready();
            foreach (int t in SixAcres) EraRules.OnHarvest(exact, t, Fixed.Parse("0.9"));
            Assert.That(Unlocks(exact), Is.EqualTo(1), "a mean of exactly 0.9 passes");

            Simulation sim = Ready();
            foreach (int t in SixAcres.Take(5)) EraRules.OnHarvest(sim, t, Fixed.Parse("0.9"));
            EraRules.OnHarvest(sim, SixAcres[5], Fixed.Parse("0.88"));
            Assert.That(Unlocks(sim), Is.EqualTo(0), "mean 0.897");
            EraRules.OnHarvest(sim, 84, Fixed.Parse("0.95")); // every harvest counts in the mean: 0.904
            Assert.That(Unlocks(sim), Is.EqualTo(1));
        }

        [Test]
        public void HamletGate_HutUnderConstructionStaysLocked()
        {
            Simulation sim = Ready(hut: false);
            Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, QuartersTile));
            foreach (int t in SixAcres) EraRules.OnHarvest(sim, t, Good);
            Assert.That(Unlocks(sim), Is.EqualTo(0), "building");
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour); // the hut is done, but the check waits for a harvest or a close
            Assert.That(Unlocks(sim), Is.EqualTo(0));
            EraRules.OnHarvest(sim, 84, Good);
            Assert.That(Unlocks(sim), Is.EqualTo(1));
        }

        [Test]
        public void HamletGate_NoProfitableSeasonStaysLocked()
        {
            Simulation sim = Ready(profitable: 0);
            foreach (int t in SixAcres) EraRules.OnHarvest(sim, t, Good);
            EraRules.OnSeasonClosed(sim, 0, sim.Period);
            Assert.That(Unlocks(sim), Is.EqualTo(0), "farm profit 0 isn't profitable");
            // A profitable close of the same season unlocks it at the close.
            EraRules.OnSeasonClosed(sim, 1, sim.Period);
            Assert.That(Unlocks(sim), Is.EqualTo(1));
        }

        [Test]
        public void HamletGate_AcresCountWithinOneSeason()
        {
            Simulation sim = Ready();
            foreach (int t in SixAcres.Take(3)) EraRules.OnHarvest(sim, t, Good);
            sim.AdvanceTo((sim.Period + 1) * sim.Clock.SecondsPerSeason + Hour);
            sim.DrainEvents();
            foreach (int t in SixAcres.Skip(3)) EraRules.OnHarvest(sim, t, Good);
            Assert.That(Unlocks(sim), Is.EqualTo(0));
            Assert.That(EraRules.Acres(sim, sim.Period), Is.EqualTo(3));
        }

        [Test]
        public void HamletGate_SeasonCloseCountsProfitableSeasons()
        {
            Simulation sim = Start();
            sim.AdvanceTo(sim.Clock.SecondsPerSeason + Hour);
            SeasonReport r = sim.State.Finance.Reports.Single();
            Assert.That(sim.State.Era.ProfitableSeasons, Is.EqualTo(r.FarmProfit > 0 ? 1 : 0));
        }

        [Test]
        public void HamletGate_SurvivesSaveAndReplay()
        {
            Simulation sim = Ready();
            foreach (int t in SixAcres.Take(4)) EraRules.OnHarvest(sim, t, Good);
            LoadResult load = SaveTests.RoundTrip(sim, sim.Config);
            Assert.That(load.Ok, Is.True, load.Detail);
            GameState copy = load.State;
            Assert.That(copy.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(copy.Era.SeasonAcres, Is.EqualTo(new[] { 84, 85, 86, 100 }));
        }
    }
}
