using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// Owner's finding, 2026-11-14: with a second hand (a tenant hired from their card) no order could reach them, a hand's stars did not
    /// move on the citizen card, and Care read as ambiguous. Orders go to the hand the player picks, from the field or from the hand.
    /// </summary>
    public class HandOrdersViewTests
    {
        private static Simulation TwoHands(out Hand first, out Hand second)
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen Named(string n) => sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == n);
            Citizen manoj = Named("Manoj"), usha = Named("Usha");
            Assert.That(sim.Execute(new OfferTownsperson(sim.State.Clock.SimTime, manoj.Id)).Accepted, Is.True);
            Assert.That(sim.Execute(new OfferTownsperson(sim.State.Clock.SimTime, usha.Id)).Accepted, Is.True);
            first = sim.State.People.Hand(manoj.HandId);
            second = sim.State.People.Hand(usha.HandId);
            return sim;
        }

        // From a field: with two hands the field asks who; each hand is listed with stars and orders used; choosing the second hand
        // carries that hand through the crop step to the order, which is theirs (it used to go to the first hand hired, always).
        [Test]
        public void Field_AsksWhoLooksAfterIt()
        {
            Simulation sim = TwoHands(out Hand first, out Hand second);
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm give = FieldSheets.ForTile(ctx, 85).Lines.Single(l => l.Action?.Id == "order");
            Assert.That(give.Action.Opens.Value.Kind, Is.EqualTo(OrderSheet.WhoSheet), "two hands: ask who first");

            SheetVm who = OrderSheet.Who(ctx, OrderTargetKind.Field, 85);
            Assert.That(who.Lines.Select(l => l.Label), Is.EqualTo(sim.State.People.Hands.Select(h => PeopleWords.Name(ctx, h.Name))), "every hand, the Town's own too");
            Assert.That(who.Lines.All(l => l.Stars.HasValue && l.Action.Label == "Choose"), Is.True);
            LineVm usha = who.Lines.Single(l => l.Label == PeopleWords.Name(ctx, second.Name));
            Assert.That(usha.Note, Does.Contain("0 of 6 orders"));

            SheetRequest next = usha.Action.Opens.Value;
            Assert.That(next.Kind, Is.EqualTo("Order"));
            SowSheetVm crops = OrderSheet.Crops(ctx, 85, next.Arg);
            Assert.That(crops.Title, Does.Contain(PeopleWords.Name(ctx, second.Name)), "the crop step is for the chosen hand");
            ActionVm set = OrderSheet.Build(ctx, OrderTargetKind.Field, 85, OrderSheet.Tag(second.Id, "radish+none|every")).Lines.Single(l => l.Action?.Id == "set:trader").Action;
            Assert.That(sim.Execute(set.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.People.OrderFor(OrderTargetKind.Field, 85).HandId, Is.EqualTo(second.Id));
        }

        // A hand with six orders cannot be chosen, and the list says why; with one hand there is no question.
        [Test]
        public void Who_FullHandIsOff_OneHandIsNotAsked()
        {
            Simulation sim = TwoHands(out Hand first, out Hand second);
            int max = sim.Config.Game.People.Hand.MaxOrders;
            for (int i = 0; i < max; i++)
                sim.State.People.Orders.Add(new StandingOrder { HandId = second.Id, Kind = OrderTargetKind.Field, Target = 1000 + i });
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm full = OrderSheet.Who(ctx, OrderTargetKind.Field, 85).Lines.Single(l => l.Label == PeopleWords.Name(ctx, second.Name));
            Assert.That((full.Action.Enabled, full.Action.DisabledReason), Is.EqualTo((false, "Has 6 orders already. Remove one first.")));

            Simulation one = Situations.Town(); // the farm's own hand only
            ViewContext c1 = ViewKit2.Context(one);
            Assert.That(one.State.People.Hands.Count, Is.EqualTo(1));
            Assert.That(FieldSheets.ForTile(c1, 85).Lines.Single(l => l.Action?.Id == "order").Action.Opens.Value.Kind, Is.EqualTo("Order"), "one hand: straight to the crop");
        }

        // From the hand: Give an order lists fields; the unattended ones first, then another hand's, which Give to moves.
        [Test]
        public void Hand_GivesAnOrder()
        {
            Simulation sim = TwoHands(out Hand first, out Hand second);
            Assert.That(sim.Execute(new SetStandingOrder(sim.State.Clock.SimTime, first.Id, OrderSpec.KeepCrop(85, "wheat", SeedSource.Certified, "none", SellRule.Store, Fixed.Zero))).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sheet = HandSheet.Build(ctx, second.Id);
            LineVm row = sheet.Lines.Single(l => l.Action?.Id == "giveOrder");
            Assert.That(row.Action.Opens.Value.Kind, Is.EqualTo(OrderSheet.FieldsSheet));
            Assert.That(sheet.Lines.Any(l => l.Label == "Orders · 0 of 6"), Is.True, "how many orders he has");

            SheetVm fields = OrderSheet.Fields(ctx, second.Id);
            LineVm taken = fields.Lines.Single(l => l.Action?.Label == "Give to " + PeopleWords.Name(ctx, second.Name));
            Assert.That(taken.Label, Is.EqualTo(PeopleWords.Target(ctx, OrderTargetKind.Field, 85)));
            Assert.That(taken.Value, Does.StartWith(PeopleWords.Name(ctx, first.Name)));
            int free = fields.Lines.Count(l => l.Action?.Label == "Choose");
            Assert.That(free, Is.GreaterThan(0));
            Assert.That(fields.Lines.Where(l => l.Action?.Label == "Choose").All(l => l.Action.Opens.Value.Arg.StartsWith("@" + second.Id + ";")), Is.True);

            // Give to: the same steps, the order moves to him.
            SheetRequest go = taken.Action.Opens.Value;
            ActionVm set = OrderSheet.Build(ctx, OrderTargetKind.Field, go.Id, go.Arg + "radish+none|every").Lines.Single(l => l.Action?.Id == "set:trader").Action;
            Assert.That(sim.Execute(set.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.People.OrderFor(OrderTargetKind.Field, 85).HandId, Is.EqualTo(second.Id), "moved");
        }

        // The citizen card read the skill the person had when hired, never what the hand had grown. Both cards now agree, and the hand's
        // sheet says where the skill is, how fast it grows and when the next star comes.
        [Test]
        public void Skill_CitizenCardFollowsTheHand()
        {
            Simulation sim = TwoHands(out Hand first, out Hand second);
            first.Skill = Fixed.FromInt(55);
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen me = sim.State.Citizens.OfHand(first.Id);
            LineVm card = CitizenSheet.Build(ctx, me.Id).Lines.Single(l => l.Label == "Skill");
            LineVm hand = HandSheet.Build(ctx, first.Id).Lines.Single(l => l.Label == "Skill");
            Assert.That(card.Value, Is.EqualTo(hand.Value));
            Assert.That((card.Stars.Value.Solid, card.Stars.Value.Faded), Is.EqualTo((hand.Stars.Value.Solid, hand.Stars.Value.Faded)));
            Assert.That(hand.Note, Does.StartWith("55 of 100 · grows "));
            Assert.That(hand.Note, Does.Contain("the next star at 70"));
        }

        // Care is one pool for the farm: a line on the People sheet and on the hand's sheet, and a row for each hand in its Why?.
        [Test]
        public void Care_ExplainedAndPerHand()
        {
            Simulation sim = TwoHands(out Hand first, out Hand second);
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm people = PeopleSheet.Build(ctx).Lines.Single(l => l.Label == "Care of your fields today");
            Assert.That(people.Note, Does.StartWith("Your crops need "));
            Assert.That(people.WhyKey, Is.EqualTo("care"));
            Assert.That(HandSheet.Build(ctx, second.Id).Lines.Single(l => l.Label == "Care of your fields").WhyKey, Is.EqualTo("care"));
            WhyVm why = WhyView.Build(ctx, "care", 0);
            foreach (Hand h in new[] { first, second })
                Assert.That(why.Rows.Any(r => r.Label == PeopleWords.Name(ctx, h.Name) + " gives"), Is.True, PeopleWords.Name(ctx, h.Name));
            WhyTests.AssertWords("care", why);
        }
    }
}
