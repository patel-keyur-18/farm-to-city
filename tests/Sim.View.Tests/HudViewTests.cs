using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The HUD's own rules (spec 030 §3.2, US-265).</summary>
    public class HudViewTests
    {
        private const long Hour = 3600;

        // AC-1: Collect all is on screen only when something is ready.
        [Test]
        public void CollectHidden()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long t = sim.State.Clock.SimTime;
            HudVm none = HudView.Build(ViewKit2.Context(sim));
            Assert.That(none.ReadyCount, Is.EqualTo(0));
            Assert.That(none.ShowCollect, Is.False, "nothing ready: no button");

            Assert.That(sim.Execute(new Sow(t, 86, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(t + 3 * Hour);
            HudVm ready = HudView.Build(ViewKit2.Context(sim));
            Assert.That(ready.ReadyCount, Is.GreaterThan(0));
            Assert.That(ready.ShowCollect, Is.True, "something ready: the button shows");
            Assert.That(ready.CollectText, Does.StartWith("Collect"));
        }

        // AC-3: a button's badge reads its whole number, and "99+" from 100.
        [Test]
        public void BadgeText()
        {
            Assert.That(HudVm.BadgeText(1), Is.EqualTo("1"));
            Assert.That(HudVm.BadgeText(11), Is.EqualTo("11"));
            Assert.That(HudVm.BadgeText(99), Is.EqualTo("99"));
            Assert.That(HudVm.BadgeText(100), Is.EqualTo("99+"));
            Assert.That(HudVm.BadgeText(250), Is.EqualTo("99+"));
        }

        // AC-6: with two towns the time card's top line starts with the open town's name; with one it is as it was.
        [Test]
        public void TownName()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            ViewContext ctx = ViewKit2.Context(sim);
            string plain = HudView.Build(ctx).DateText;
            Assert.That(plain, Does.Not.Contain("Town"));
            ctx.TownName = "Town 2";
            string named = HudView.Build(ctx).DateText;
            Assert.That(named, Is.EqualTo("Town 2 · " + plain));
        }
    }
}
