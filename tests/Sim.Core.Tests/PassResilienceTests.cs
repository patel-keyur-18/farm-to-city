using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    /// <summary>
    /// A pass that throws must never lose the pass after it: the clock keeps running, so a lost hourly pass leaves the weather buffer
    /// behind the clock and the forecast (the HUD) throws every refresh (device crash, 2026-11-20). A town already in that state is mended on load.
    /// </summary>
    public class PassResilienceTests
    {
        private sealed class ThrowsOnce : ISimSystem
        {
            public long At;
            public bool Thrown;
            public SystemOrder Order { get { return SystemOrder.Events; } }
            public void OnPass(Simulation sim, ItemClass pass)
            {
                if (pass != ItemClass.Hourly || Thrown || sim.State.Clock.SimTime < At) return;
                Thrown = true;
                throw new InvalidOperationException("a system failed in the hourly pass");
            }
        }

        private static long Spell(Simulation sim) { return Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock); }

        private static void Advance(Simulation sim, long seconds)
        {
            long target = sim.State.Clock.SimTime + seconds;
            while (sim.State.Clock.SimTime < target)
            {
                try { sim.AdvanceTo(target); }
                catch (InvalidOperationException) { /* the failing system's, as the app's Sync would see it */ }
            }
        }

        [Test]
        public void ASystemThatThrows_DoesNotStopTheHourlyPass_OrLeaveTheForecastBehind()
        {
            var bad = new ThrowsOnce { At = 5 * 3600 };
            Simulation sim = TestSim.New(systems: new ISimSystem[] { bad });
            Advance(sim, 40 * 3600);
            Assert.That(bad.Thrown, Is.True);
            Assert.That(() => sim.Forecast(), Throws.Nothing, "the forecast still has every spell it shows");
            Assert.That(sim.State.Weather.LastGeneratedSpell, Is.GreaterThanOrEqualTo(Spell(sim) + sim.Clock.ForecastSpells));
        }

        // A town saved while it was in that state (the device's): loads whole, with the hourly pass back and the same weather it would have had.
        [Test]
        public void ATownSavedWithoutItsHourlyPass_IsMendedOnLoad()
        {
            Simulation whole = TestSim.New();
            Simulation lost = TestSim.New();
            whole.AdvanceTo(60 * 3600);
            lost.AdvanceTo(10 * 3600);
            GameState broken = lost.State;
            var keep = broken.Scheduler.InOrder().Where(i => i.Class != ItemClass.Hourly).ToList();
            while (broken.Scheduler.Count > 0) broken.Scheduler.Pop();
            Assert.That(keep.Count, Is.GreaterThan(0));
            foreach (ScheduledItem i in keep) broken.Scheduler.Schedule(i.DueTime, i.Class, i.System, i.EntityId, i.JobId);
            broken.Clock.SimTime = 60 * 3600; // the clock went on without the hourly pass
            Assert.That(broken.Scheduler.Has(ItemClass.Hourly), Is.False);

            Simulation mended = Simulation.FromState(TestSim.Config(), broken);
            Assert.That(mended.Forecast().Count, Is.EqualTo(mended.Clock.ForecastSpells));
            Assert.That(mended.State.Scheduler.Has(ItemClass.Hourly), Is.True);
            Assert.That(mended.CurrentWeather.Id, Is.EqualTo(whole.CurrentWeather.Id), "the same spell's weather as a town that never lost a pass");
            Assert.That(() => mended.AdvanceTo(70 * 3600), Throws.Nothing);
            Assert.That(() => mended.Forecast(), Throws.Nothing);
        }

        [Test]
        public void AHealthyTown_IsNotChangedByLoading()
        {
            Simulation sim = TestSim.New();
            sim.AdvanceTo(37 * 3600 + 900);
            ulong before = sim.State.ComputeHash();
            Simulation loaded = Simulation.FromState(TestSim.Config(), sim.State.Clone());
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(before));
        }
    }
}
