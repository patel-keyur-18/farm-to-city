using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    /// <summary>The game's time of day matches the device's (a town made elsewhere can start at another time of day).</summary>
    public class ClockAlignTests
    {
        private const long Hour = 3600, Day = 86400;

        [Test]
        public void Behind_Moves_Forward_Ahead_Waits()
        {
            long sim = 5 * Day + 16 * Hour + 48 * 60; // 16:48 on day 6
            Assert.That(ClockAlign.Offset(sim, 17 * Hour + 53 * 60), Is.EqualTo(65 * 60), "the owner's case: the game 65 minutes behind the phone");
            Assert.That(ClockAlign.Offset(sim, 16 * Hour + 40 * 60), Is.EqualTo(-8 * 60), "ahead by 8 minutes: wait");
            Assert.That(ClockAlign.Offset(sim, 16 * Hour + 48 * 60 + 20), Is.EqualTo(0), "20 seconds is rounding");
            Assert.That(ClockAlign.Offset(sim, 2 * Hour), Is.EqualTo(9 * Hour + 12 * 60), "the nearer way past midnight, 9 h 12 min forward");
            Assert.That(ClockAlign.Offset(sim, 6 * Hour), Is.EqualTo(-(10 * Hour + 48 * 60)), "10 h 48 min ahead: wait, not 13 h 12 min forward");
            Assert.That(ClockAlign.Forward(sim, 16 * Hour + 48 * 60), Is.EqualTo(0));
            Assert.That(ClockAlign.Forward(sim, 16 * Hour + 47 * 60), Is.EqualTo(Day - 60));
        }

        [Test]
        public void Away_Advance_Matches_And_Loses_Nothing()
        {
            Simulation sim = Simulation.NewGame(new SimConfig(TestContent.Clock(), TestContent.Weather()), new NewGame(7, 1_800_000_000, 16 * Hour + 48 * 60));
            long local = 17 * Hour + 53 * 60;
            long off = ClockAlign.Offset(sim.State.Clock.SimTime, local);
            Assert.That(sim.Execute(new AdvanceTo(sim.State.Clock.SimTime + off, AdvanceMode.Away)).Accepted, Is.True);
            Assert.That(sim.State.Clock.SimTime % Day, Is.EqualTo(local));
            Assert.That(ClockAlign.Offset(sim.State.Clock.SimTime, local), Is.EqualTo(0));
        }
    }
}
