using NUnit.Framework;
using Sim.Core.Town;

namespace Sim.Core.Tests
{
    /// <summary>Spec 027 AC-18: a town that answers nothing (every default) still works.</summary>
    public class RiskTests
    {
        [Test]
        public void LeftAlone()
        {
            Simulation sim = EventEngineTests.Town(minor: "1", major: "0.5");
            long t = sim.State.Clock.SimTime;
            int events = 0;
            for (int i = 1; i <= 8; i++) // two game years
            {
                sim.AdvanceTo(t + i * sim.Clock.SecondsPerSeason);
                events += sim.DrainEvents().FindAll(e => e is Sim.Core.Events.EventRaised).Count;
                EventsState s = EventRules.State(sim);
                Assert.That(s.Waiting.Count, Is.LessThanOrEqualTo(3), "nothing piles up (a fire's repair card may join the two)");
                Assert.That(s.History.Count, Is.LessThanOrEqualTo(EventRules.Def(sim).Engine.KeepHistory));
                Assert.That(s.Effects.Count, Is.LessThan(40), "what ran its course is dropped");
                Assert.That(sim.State.Ledger.Cash, Is.GreaterThanOrEqualTo(0), "never negative");
            }
            Assert.That(events, Is.GreaterThan(8), "events came");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
        }
    }
}
