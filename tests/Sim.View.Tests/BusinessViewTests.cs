using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 016 §3.3, §7 (US-115): the Businesses sheet, research, the plan, placement and the business card.</summary>
    public class BusinessViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        // AC-14 (the plan shown; compared after 4 seasons in US-116): from the Menu to an open mill, every step in words.
        [Test]
        public void Founding_Flow()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(MenuSheet.Build(ctx).Lines.Select(l => l.Label), Does.Contain("Businesses"));
            SheetVm list = BusinessesSheet.Build(ctx);
            ActionVm start = list.Lines.First(l => l.Buttons != null).Buttons.Single();
            Assert.That((start.Label, start.Enabled), Is.EqualTo(("Start a flour mill", true)));
            Assert.That(list.Lines.Any(l => l.Label == "None yet"), Is.True);

            SheetVm research = BusinessResearchSheet.Build(ctx, 0);
            Assert.That(research.Lines.Single(l => l.Label == "Grain the area brings a day").Value, Does.StartWith("about 2,"));
            Assert.That(research.Lines.Single(l => l.Label == "Atta at the trader today").Value, Does.Contain("/kg"));
            SheetVm plan = BusinessPlanSheet.Build(ctx, 0, false);
            Assert.That(plan.Lines.Single(l => l.Label == "Who runs it").Value, Is.EqualTo("A hired miller"));
            BusinessRules.Projection p = BusinessRules.Projected(sim, sim.Config.Game.Businesses.Types[0], false, 200_000, Sim.Core.Maths.Fixed.FromInt(4));
            Assert.That(plan.Lines.Single(l => l.Label == "Profit").Value, Is.EqualTo(ctx.Fmt.Coins(p.Profit)));
            Assert.That(plan.Lines.Single(l => l.Label == "Cash to put in").Value, Is.EqualTo(ctx.Fmt.Coins(p.CashNeeded)));
            Assert.That(BusinessPlanSheet.Build(ctx, 0, true).Lines.Any(l => l.Label.StartsWith("The miller")), Is.False, "no wage when you run it");
            ActionVm place = plan.Actions.Single();
            Assert.That(place.Opens.Value.Kind, Is.EqualTo("BusinessPlacement"));

            int tile = BusinessPlacement.Places(ctx).First(x => x.Allowed).TileId;
            Assert.That(BusinessPlacement.ReasonFor(ctx, 86), Is.EqualTo("Something is growing here"));
            Ok(sim, BusinessPlacement.Confirm(ctx, tile, 0, false).Create(sim.State.Clock.SimTime));
            Business b = sim.State.Businesses.Businesses.Single();
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), tile).Title, Is.EqualTo("Flour mill"), "the acre opens its card");
            Assert.That(BusinessesSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Flour mill").Value, Does.StartWith("Being built"));
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400 + 2) * 86_400 + 12 * 3600);
            SheetVm card = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(card.Subtitle, Is.EqualTo("Open now · until 18:00"));
            Assert.That(card.Tabs.Select(t => t.Label), Is.EqualTo(new[] { "Today", "Settings" }));
            Assert.That(card.Lines.Single(l => l.Label == "Ground for the area today").Value, Does.Contain("in fees"));
            Assert.That(card.Lines.Single(l => l.Label == "Who works here").Value, Is.EqualTo("Manoj (miller)").Or.EqualTo("Usha (miller)"), "the town's best-skilled adult");

            SheetVm settings = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings);
            LineVm hours = settings.Lines.Single(l => l.Label == "Open");
            Assert.That(hours.Value, Is.EqualTo("8:00–18:00"));
            Ok(sim, hours.Action.Create(sim.State.Clock.SimTime));
            Assert.That((b.OpenHour, b.CloseHour), Is.EqualTo((8, 14)));
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings).Lines.Single(l => l.Label == "Open").Value, Is.EqualTo("8:00–14:00"));
        }
    }
}
