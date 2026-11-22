using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-258 (spec 028 §3.8, Preview 18 screen 7): the Benefits section, health cover greyed without the clinic, the night shift's row and card.</summary>
    public class BenefitViewTests
    {
        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // Four benefits with their cost, effect and a switch, each with a ?; the kept line; health cover greyed with the sim's own reason before the clinic.
        [Test]
        public void Benefits_Section()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Business bakery = Of(sim, "bakery");
            SheetVm set = BusinessSheet.Build(ctx, bakery.Id, BusinessSheet.Settings);
            var rows = set.Lines.Where(l => l.WhyKey == "benefit").ToList();
            Assert.That(rows.Count, Is.EqualTo(4));
            Assert.That(rows.Single(l => l.Label == "A meal each working day").Value, Is.EqualTo("On"));
            Assert.That(rows.Single(l => l.Label == "Festival bonus").Value, Is.EqualTo("Off"));
            Assert.That(rows.All(l => l.Note.Contains("a worker")), Is.True);
            LineVm health = rows.Single(l => l.Label == "Health cover");
            Assert.That(health.Action.Enabled, Is.False, "no clinic yet");
            Assert.That(health.Action.DisabledReason, Does.Contain("clinic"));
            LineVm kept = set.Lines.Single(l => l.WhyKey == "loyalty");
            Assert.That(kept.Label, Is.EqualTo("Kept: 2 of 4"));
            Assert.That(kept.Note, Does.Contain("raise your name"));
            LineVm meal = rows.Single(l => l.Label == "A meal each working day");
            Assert.That(meal.Action.Create(0), Is.TypeOf<SetBenefit>());
            Assert.That(ConfirmRules.Of(meal.Action.Create(0)), Is.EqualTo(ConfirmKind.Everyday), "a switch, with its cost on the row");
        }

        // Your hands' benefits sit on People, once you have a hand.
        [Test]
        public void Farm_Benefits_On_People()
        {
            Simulation sim = Situations.Owned();
            SheetVm people = PeopleSheet.Build(ViewKit2.Context(sim));
            Assert.That(people.Lines.Count(l => l.WhyKey == "benefit"), Is.EqualTo(sim.State.People.Hands.Count > 0 ? 4 : 0));
        }

        // The night shift: on for the bakery (a Stop button), offered for the mill (Start, a tap with the wage and what the business pays now), refused without the grid.
        [Test]
        public void Night_Shift_Row_And_Card()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm bakery = BusinessSheet.Build(ctx, Of(sim, "bakery").Id, BusinessSheet.Settings);
            LineVm on = bakery.Lines.Single(l => l.WhyKey == "night");
            Assert.That(on.Label, Is.EqualTo("Night shift on"));
            Assert.That(on.Action.Id, Is.EqualTo("night:stop"));
            Assert.That(ConfirmCards.For(ctx, on.Action.Create(sim.State.Clock.SimTime)), Is.Null, "stopping spends nothing");

            Business mill = Of(sim, "flour_mill");
            LineVm offered = BusinessSheet.Build(ctx, mill.Id, BusinessSheet.Settings).Lines.Single(l => l.WhyKey == "night");
            Assert.That(offered.Label, Is.EqualTo("A night shift is offered"));
            Assert.That(offered.Value, Does.Contain("3 of the last 8"));
            Assert.That(ConfirmRules.Of(offered.Action.Create(0)), Is.EqualTo(ConfirmKind.Tap));
            ConfirmCardVm card = ConfirmCards.For(ctx, offered.Action.Create(sim.State.Clock.SimTime));
            Assert.That(card.Rows.Count, Is.EqualTo(3));
            Assert.That(card.Rows.Any(r => r.Label == "Hours" && r.Value == "20:00 to 4:00"), Is.True);

            sim.State.Township.UtilitiesBuilt = 0;
            LineVm noGrid = BusinessSheet.Build(ViewKit2.Context(sim), mill.Id, BusinessSheet.Settings).Lines.Single(l => l.WhyKey == "night");
            Assert.That(noGrid.Action.Enabled, Is.False);
            Assert.That(noGrid.Action.DisabledReason, Does.Contain("grid"));
        }

        // A stall has no night shift section; the Why? of a benefit says what it pays in.
        [Test]
        public void Why_Does_It_Pay()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Business bakery = Of(sim, "bakery");
            WhyVm why = WhyView.Build(ctx, "benefit", bakery.Id * 16 + (int)Benefit.Meal);
            Assert.That(why.Rows.Any(r => r.Label.StartsWith("In output")), Is.True, "your own numbers");
            Assert.That(why.Footnote, Does.Contain("do not pay in output alone"));
        }
    }
}
