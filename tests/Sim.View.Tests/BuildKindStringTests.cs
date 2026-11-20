using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The independent review (2026-11-20): a held water tower, drains or collection post showed a raw string key on the queue, the bell and the away card.</summary>
    public class BuildKindStringTests
    {
        [Test]
        public void EveryTreasuryBuild_HasItsNameOnTheQueue_TheBell_AndTheAwayCard()
        {
            Simulation sim = Situations.TownshipBuilt();
            foreach (string work in new[] { "water_tower", "drains", "collection_post" }) sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, work));
            ViewContext ctx = ViewKit2.Context(sim); // missing strings throw here
            SheetVm queue = BuildingSheet.Build(ctx);
            Assert.That(queue.Lines.Any(l => l.Label.Contains("water tower")), Is.True, "the tower is on the queue by name");
            foreach (var l in queue.Lines.Where(l => l.Label.Contains("water tower") || l.Label.Contains("drains") || l.Label.Contains("collection post")))
                Assert.That(l.WhyKey, Is.Not.Null, "the queue row's time has a Why?: " + l.Label);

            var events = new List<SimEvent> { new AwayAdvanceCompleted(sim.State.Clock.SimTime, 0, sim.State.Clock.SimTime, 72 * 3600, 72 * 3600, false) };
            foreach (BuildProject p in sim.State.Township.Projects.Where(p => p.Owner != BuildProject.Builders))
                events.Insert(0, new BuildHeld(sim.State.Clock.SimTime, p.Id, p.Kind, p.Owner, 0, 0));
            Assert.That(() => AwayCard.Build(ctx, events), Throws.Nothing);
            Assert.That(() => Alerts.Build(ctx), Throws.Nothing);
            foreach (string kind in new[] { "water_tower", "drains", "collection_post" })
                Assert.That(ctx.Strings.Has("build.kind." + kind), Is.True, kind);
        }
    }
}
