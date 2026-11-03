using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.View.Views;

namespace Sim.View.Tests
{
    public class FarmViewTests
    {
        // Spec 020 AC-6: fertility's Why? says legumes, whichever legume it was.
        [Test]
        public void Fertility_LegumesLine()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(sim.Execute(new Sow(sim.State.Clock.SimTime, 86, "methi", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3700);
            WhyVm why = WhyView.Build(ctx, "fertility", 86);
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("Legumes fed the soil this season"));
        }
    }
}
