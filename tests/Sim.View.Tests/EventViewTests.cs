using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-154 (spec 027 §7, Preview 12): the card, its choices and costs, the default named, the Events sheet, the bell, the countdown and the away lines.</summary>
    public class EventViewTests
    {
        private static Simulation Town()
        {
            Simulation sim = Situations.TownshipBuilt();
            EventRules.State(sim).StartedAt = sim.State.Clock.SimTime - 10 * 86400;
            EventRules.State(sim).Waiting.Clear();
            return sim;
        }

        private static PendingEvent Force(Simulation sim, string id, long t1 = 0, long t2 = 0)
        {
            EventsDef d = EventRules.Def(sim);
            int def = d.IndexOf(id);
            EventRules.Raise(sim, EventRules.State(sim), def, t1, t2, 1, sim.State.Clock.SimTime, d.Events[def].Major);
            return EventRules.State(sim).Waiting.Last();
        }

        [Test]
        public void Card_Choices_Default()
        {
            Simulation sim = Town();
            var ctx = ViewKit2.Context(sim);
            PendingEvent p = Force(sim, "wedding", sim.State.Citizens.Households[0].Id);
            SheetVm card = EventSheet.Card(ctx, p.Id);
            Assert.That(card.Title, Is.EqualTo("Wedding"));
            var choices = card.Lines.Where(l => l.Action != null).ToList();
            Assert.That(choices.Count, Is.EqualTo(2));
            Assert.That(choices[0].Tag, Is.EqualTo("If you wait"), "the default is named");
            Assert.That(choices[0].Action.Kind, Is.EqualTo(ActionKind.Secondary));
            Assert.That(choices[1].Action.Label, Does.Contain("coins").Or.Contain("500"), "the paid choice shows its cost on its button");
            Assert.That(card.Lines.Last().Label, Does.StartWith("If you do not answer: Send regards in "));
            Assert.That(card.Lines[0].WhyKey, Is.EqualTo("event"), "the ? says why it came");

            // The default costs nothing and asks nothing; the paid one asks on the usual card; both are the command.
            Assert.That(ConfirmCards.For(ctx, new AnswerEvent(ctx.Now, p.Id, 0)), Is.Null, "a free choice just answers");
            ConfirmCardVm pay = ConfirmCards.For(ctx, new AnswerEvent(ctx.Now, p.Id, 1));
            Assert.That(pay, Is.Not.Null);
            Assert.That(pay.CashChange, Is.LessThan(0));
        }

        [Test]
        public void Gone_Card()
        {
            Simulation sim = Town();
            var ctx = ViewKit2.Context(sim);
            Assert.That(EventSheet.Card(ctx, 77).Title, Does.Contain("gone"));
        }

        [Test]
        public void Events_Sheet_Bell_Banner()
        {
            Simulation sim = Town();
            var ctx = ViewKit2.Context(sim);
            Assert.That(EventSheet.List(ctx).Subtitle, Is.EqualTo("Nothing is waiting"));
            Assert.That(EventSheet.DisasterNote(ctx), Is.Null);
            PendingEvent p = Force(sim, "wedding", sim.State.Citizens.Households[0].Id);
            PendingEvent fire = Force(sim, "fire", sim.State.Land.Buildings.First(b => b.Owner == 0 && b.TileId >= 0).Id);
            ctx = ViewKit2.Context(sim);
            SheetVm list = EventSheet.List(ctx);
            Assert.That(list.Lines.Count(l => l.Action != null && l.Action.Opens.HasValue && l.Action.Opens.Value.Kind == EventSheet.CardKind), Is.EqualTo(2), "each waiting card opens");
            var alerts = Alerts.Build(ctx);
            Assert.That(alerts.Count(a => a.Text.Contains("warning")), Is.EqualTo(1), "a disaster's countdown on the bell");
            Assert.That(alerts.First(a => a.Text.Contains("warning")).Level, Is.EqualTo(AlertLevel.Urgent));
            Assert.That(EventSheet.DisasterNote(ctx), Is.EqualTo("A fire risk in 2 d"));
            SheetVm card = EventSheet.Card(ctx, fire.Id);
            Assert.That(card.Subtitle, Is.EqualTo("Disaster"));
            Assert.That(card.Lines.Last().Label, Does.Contain("never strikes while you are away"));
        }

        [Test]
        public void Answered_Goes_To_History_And_Away_Line()
        {
            Simulation sim = Town();
            long hh = sim.State.Citizens.Households[0].Id;
            PendingEvent p = Force(sim, "wedding", hh);
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, p.Id, 1)).Accepted, Is.True);
            var ctx = ViewKit2.Context(sim);
            SheetVm list = EventSheet.List(ctx);
            LineVm row = list.Lines.First(l => l.WhyKey == "eventHist");
            Assert.That(row.Label, Is.EqualTo("Wedding"));
            Assert.That(row.Value, Does.StartWith("−"), "what it cost");
            Assert.That(row.Note, Does.Contain("Attend and give a gift"));

            // A default taken while away is a line on the away card, with the note that nothing big starts while you are away.
            var events = new System.Collections.Generic.List<SimEvent> { new EventDefaulted(sim.State.Clock.SimTime, 5, sim.Config.Game.Events.IndexOf("wedding"), 0, 0) };
            var rows = TownAway.Rows(ctx, events);
            Assert.That(rows.First().Label, Is.EqualTo("Wedding"));
            Assert.That(rows.First().Note, Does.Contain("the safe choice was taken: Send regards"));
            Assert.That(rows.Last().Label, Is.EqualTo("No big event or disaster can start while you are away."));
        }
    }
}
