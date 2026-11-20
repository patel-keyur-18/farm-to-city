using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>ADR 0140: the mill's *Buy wheat at the market* Why? says what the trader takes, that the village does not buy from the mill, and the cap.</summary>
    public class MillMarketWhyTests
    {
        [Test]
        public void TheMarketWhy_SaysWhoBuysTheAtta_AndTheTwoDayCap()
        {
            Simulation sim = Situations.Mill();
            long id = sim.State.Businesses.Businesses.Last().Id;
            ViewContext ctx = ViewKit2.Context(sim);
            WhyVm why = WhyView.Build(ctx, "businessMarket", id);
            string all = string.Join(" ", why.Rows.Select(r => r.Label));
            Assert.That(all, Does.Contain("400 kg of atta").And.Contain("100 kg of bran").And.Contain("2 days").And.Contain("village does not buy"));
        }
    }
}
