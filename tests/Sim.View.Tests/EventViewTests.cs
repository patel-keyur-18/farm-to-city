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

        // The *Why?* of a card never shows a raw key: every event raised with every "why it came" code its weighing can produce (a review found six sentences missing).
        [Test]
        public void Why_Came_Every_Code()
        {
            Simulation sim = Situations.Events();
            EventsState st = EventRules.State(sim);
            EventsDef defs = sim.Config.Game.Events;
            long hh = sim.State.Citizens.Households[0].Id;
            for (int i = 0; i < defs.Events.Count; i++)
            {
                if (defs.Events[i].Disaster && i != defs.IndexOf("fire")) continue;
                for (int why = 0; why < 4; why++)
                {
                    st.Waiting.Clear();
                    EventRules.Raise(sim, st, i, defs.Events[i].Id == "wedding" || defs.Events[i].Id == "dispute" ? hh : 0, hh, why, sim.State.Clock.SimTime, defs.Events[i].Major);
                    var ctx = ViewKit2.Context(sim);
                    WhyVm vm = WhyView.Build(ctx, "event", st.Waiting.Last().Id);
                    foreach (WhyRowVm row in vm.Rows) Assert.That((row.Label ?? "") + (row.Value ?? "") + (row.Note ?? ""), Does.Not.Contain("\u27E6"), defs.Events[i].Id + " why " + why);
                }
            }
        }

        // A business fire: the card and the *Why?* name the business and its book value, not a building that happens to share its id (a review found they did).
        [Test]
        public void Fire_On_A_Business()
        {
            Simulation sim = Situations.Insured();
            EventsState st = EventRules.State(sim);
            st.Waiting.Clear();
            Sim.Core.Business.Business shop = sim.State.Businesses.Businesses.Last();
            EventsDef d = sim.Config.Game.Events;
            EventRules.Raise(sim, st, d.IndexOf(EventsDef.FireRepair), shop.Id, 1, 0, sim.State.Clock.SimTime, false);
            PendingEvent p = st.Waiting.Last();
            p.Param = 500_000;
            var ctx = ViewKit2.Context(sim);
            string name = ctx.Strings.Get(sim.Config.Game.Businesses.Types[shop.Type].NameKey);
            SheetVm card = EventSheet.Card(ctx, p.Id);
            Assert.That(string.Join(" ", card.Lines.Select(l => (l.Label ?? "") + (l.Note ?? ""))) + card.Subtitle + card.Title, Does.Contain(name).IgnoreCase.Or.Contain(name.ToLowerInvariant()));
            WhyVm why = WhyView.Build(ctx, "event", p.Id);
            Assert.That(why.Rows.Any(r => r.Value != null && r.Value.Contains(ctx.Fmt.Coins(Sim.Core.Town.InsuranceRules.BusinessBook(shop)))), Is.True, "its own book value is the worth");
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
            Assert.That(EventSheet.DisasterNote(ctx), Does.StartWith("A fire risk in 2 d"), "a strike always falls on a 6:00, so the time left is the 48 hours less the hours since 6:00");
            EventRules.State(sim).DisasterStrikeAt = sim.State.Clock.SimTime - 3600; // slid past its hour while you were away
            Assert.That(EventSheet.DisasterNote(ViewKit2.Context(sim)), Is.EqualTo("A fire risk at your next 6:00 here"), "never \"in 0 min\"");
            EventRules.State(sim).DisasterStrikeAt = sim.State.Clock.SimTime + 2 * 86400;
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
