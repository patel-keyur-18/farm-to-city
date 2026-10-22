using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The people views (ADR 0039 §7–§9, spec 006 AC-21, AC-22).</summary>
    public class PeopleViewTests
    {
        private const long Hour = 3600, Day = 86400;

        private static Simulation Rich(ulong seed = 42)
        {
            return ViewKit.NewGame(seed: seed, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
        }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name); }

        /// <summary>A hut on field 18, then a hand (the first applicant who accepts).</summary>
        private static Hand Hire(Simulation sim)
        {
            Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, 119));
            for (int i = 0; i < 24 * 10; i++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
                if (sim.State.People.Applicants.Count == 0) continue;
                Ok(sim, new OfferContract(sim.State.Clock.SimTime, sim.State.People.Applicants[0].Id));
                if (sim.State.People.Hands.Count > 0) return sim.State.People.Hands[0];
            }
            Assert.Fail("nobody accepted");
            return null;
        }

        private static IEnumerable<string> Texts(SheetVm s)
        {
            yield return s.Title;
            yield return s.Subtitle;
            foreach (LineVm l in s.Lines)
            {
                yield return l.Label;
                yield return l.Value;
                if (l.Action != null) { yield return l.Action.Label; yield return l.Action.Detail; yield return l.Action.DisabledReason; }
            }
            foreach (ActionVm a in s.Actions) { yield return a.Label; yield return a.Detail; yield return a.DisabledReason; }
        }

        /// <summary>Words, not raw factors: no decimal numbers like 7.6 or 0.85 anywhere a player reads.</summary>
        private static void AssertWords(SheetVm s)
        {
            foreach (string t in Texts(s).Where(x => x != null))
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(t, @"\d\.\d"), Is.False, "a raw factor in: " + t);
        }

        // Spec 006 AC-22.
        [Test]
        public void Words()
        {
            Simulation sim = Rich();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(PeopleWords.Stars(ctx, 39, 69), Is.EqualTo(new StarsVm(2, 1)));
            Assert.That(PeopleWords.Stars(ctx, 54, 54), Is.EqualTo(new StarsVm(3, 0)));
            Assert.That(PeopleWords.Stars(ctx, 100, 100), Is.EqualTo(new StarsVm(5, 0)));
            Assert.That(PeopleWords.Band(ctx, 34), Is.EqualTo("Beginner"));
            Assert.That(PeopleWords.Band(ctx, 35), Is.EqualTo("Capable"));
            Assert.That(PeopleWords.Band(ctx, 55), Is.EqualTo("Skilled"));
            Assert.That(PeopleWords.Band(ctx, 75), Is.EqualTo("Expert"));
            Assert.That(PeopleWords.Range(ctx, 39, 69), Is.EqualTo("Capable to Skilled"));
            Assert.That(PeopleWords.Tends(ctx, Fixed.Parse("7.6")), Is.EqualTo("Tends about 2½ acres"));
            Assert.That(PeopleWords.Tends(ctx, Fixed.Parse("6.0")), Is.EqualTo("Tends about 2 acres"));

            Hand h = Hire(sim);
            ctx = ViewKit2.Context(sim);
            SheetVm hand = HandSheet.Build(ctx, h.Id);
            Assert.That(hand.Lines.Single(l => l.Label == "Skill").Stars, Is.Not.Null);
            AssertWords(hand);
            AssertWords(PeopleSheet.Build(ctx));
        }

        [Test]
        public void PeopleSheet_ThroughHiring()
        {
            Simulation sim = Rich();
            SheetVm s = PeopleSheet.Build(ViewKit2.Context(sim));
            Assert.That(s.Lines.Count(l => l.Action != null && l.Action.Id.StartsWith("hire:")), Is.EqualTo(0), "nothing growing to hire for (ADR 0049)");
            Assert.That(s.Lines.Single(l => l.Action?.Id == "buildHut").Action.Opens.Value.Kind, Is.EqualTo("HutPlacement"));
            Assert.That(s.Lines.Single(l => l.Action?.Id == "postMarket").Action.Enabled, Is.False, "no hut yet");

            // Hut placement: the six inherited acres, the yard is not among them.
            List<HutPlaceVm> places = HutPlacement.Places(ViewKit2.Context(sim));
            Assert.That(places.Select(p => p.TileId), Is.EquivalentTo(new[] { 119 }), "the labour quarters (ADR 0040)");
            Assert.That(places.All(p => p.Allowed), Is.True);
            Assert.That(HutPlacement.ReasonFor(ViewKit2.Context(sim), 87), Is.EqualTo("A hand's hut goes in the labour quarters beside your yard."));
            Assert.That(HutPlacement.ReasonFor(ViewKit2.Context(sim), 84), Is.EqualTo("A hand's hut goes in the labour quarters beside your yard."));

            Hand h = Hire(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(HutPlacement.Places(ctx).Single(p => p.TileId == 119).Allowed, Is.True, "room for a second hut");
            s = PeopleSheet.Build(ctx);
            Assert.That(s.Lines.Any(l => l.Action?.Id == "hand:" + h.Id), Is.True);
            Assert.That(FieldSheets.ForTile(ctx, 119).Lines.Any(l => l.Label.StartsWith("Hut place")), Is.True);

            // A field sheet offers the order; the order sheet steps crop, then sell rule.
            LineVm give = FieldSheets.ForTile(ctx, 85).Lines.Single(l => l.Action?.Id == "order");
            Assert.That(give.Action.Opens.Value.Kind, Is.EqualTo("Order"));
            // ADR 0059 §3: the crop step is the player's sow sheet, with the fertiliser choice and every figure, and Choose.
            Assert.That(OrderSheet.IsCropStep(OrderTargetKind.Field, null), Is.True);
            SowSheetVm crops = OrderSheet.Crops(ctx, 85, null);
            Assert.That(crops.Rows.Count, Is.EqualTo(sim.Config.Game.Farm.Crops.Count), "each crop");
            Assert.That(crops.Fertilisers.Select(f => f.Name), Is.EqualTo(new[] { "Normal", "Compost", "Chemical" }));
            Assert.That(crops.Lines.Single().Action.Opens.Value.Arg, Is.EqualTo(OrderSpec.HandChooses + "+none"), "the hand's choice first");
            Assert.That(crops.Rows.All(r => r.Sow.Label == "Choose" && r.Sow.Opens.HasValue), Is.True, "in season or not");
            Assert.That(crops.Rows.Where(r => !r.SowableNow).Select(r => r.SeasonNote), Has.All.Contain("picks the best crop in between"));
            SowSheetVm compost = OrderSheet.Crops(ctx, 85, "+compost");
            Assert.That(OrderSheet.IsCropStep(OrderTargetKind.Field, "+compost"), Is.True);
            Assert.That(compost.Fertilisers.Single(f => f.Selected).Id, Is.EqualTo("compost"));
            SowSheetVm own = SowSheet.Build(ctx, 85, "compost");
            Assert.That(compost.Rows.Select(r => r.HarvestText + r.SpendText + r.GetText + r.ProfitText + r.InputsText + r.LabourText + r.SoilText),
                Is.EqualTo(own.Rows.Select(r => r.HarvestText + r.SpendText + r.GetText + r.ProfitText + r.InputsText + r.LabourText + r.SoilText)), "the same figures as sowing yourself");
            Assert.That(compost.Rows.Single(r => r.CropId == "radish").Sow.Opens.Value.Arg, Is.EqualTo("radish+compost"));
            SheetVm withCompost = OrderSheet.Build(ctx, OrderTargetKind.Field, 85, "radish+compost|once");
            Assert.That(withCompost.Subtitle, Does.StartWith("Keep Radish with compost"));
            Simulation c2 = Rich();
            Hand h2 = Hire(c2);
            ViewContext ctx2 = ViewKit2.Context(c2);
            ActionVm trader = OrderSheet.Build(ctx2, OrderTargetKind.Field, 85, "radish+compost|every").Lines.Single(l => l.Action?.Id == "set:trader").Action;
            Assert.That(c2.Execute(trader.Create(c2.State.Clock.SimTime)).Accepted, Is.True);
            StandingOrder kept = c2.State.People.OrderFor(OrderTargetKind.Field, 85);
            Assert.That(c2.Config.Game.Farm.Fertilisers[kept.Fertiliser].Id, Is.EqualTo("compost"), "the order keeps the fertiliser");
            Assert.That(PeopleWords.Order(ctx2, kept), Does.StartWith("Keep Radish with compost"));
            Assert.That(OrderSheet.Crops(ctx2, 85, null).Lines[0].Label, Is.EqualTo("The order now"));
            Assert.That(OrderSheet.Crops(ctx2, 85, null).Actions.Single().Id, Is.EqualTo("clearOrder"));
            Assert.That(OrderSheet.Crops(ctx2, 85, null).Fertilisers.Single(f => f.Selected).Id, Is.EqualTo("compost"), "a changed order starts from its fertiliser");
            Assert.That(OrderSheet.Crops(ctx2, 85, "+none").Fertilisers.Single(f => f.Selected).Id, Is.EqualTo("none"), "until another is tapped");
            SheetVm auto = OrderSheet.Build(ctx, OrderTargetKind.Field, 100, OrderSpec.HandChooses + "|every");
            Assert.That(auto.Lines.Any(l => l.Action?.Id == "set:offer"), Is.False, "no minimum price: his crop changes with the season");
            Assert.That(sim.Execute(auto.Lines.Single(l => l.Action?.Id == "set:store").Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.People.OrderFor(OrderTargetKind.Field, 100).Crop, Is.EqualTo(-1));
            Assert.That(sim.State.Farm.CropOn(100), Is.Not.Null, "he sows at once");
            string pick = ctx.Strings.Get(sim.Config.Game.Farm.Crops[sim.State.Farm.CropOn(100).Def].NameKey);
            Assert.That(PeopleWords.Order(ctx, sim.State.People.OrderFor(OrderTargetKind.Field, 100)), Does.StartWith("Let ").And.Contain("(" + pick + " now)"));
            SheetVm how = OrderSheet.Build(ctx, OrderTargetKind.Field, 85, "radish");
            Assert.That(how.Lines.Select(l => l.Action?.Opens?.Arg), Is.EqualTo(new[] { "radish|once", "radish|every" }), "this season only, or every season");
            SheetVm sell = OrderSheet.Build(ctx, OrderTargetKind.Field, 85, "radish|every");
            ActionVm offer = sell.Lines.Single(l => l.Action?.Id == "set:offer").Action;
            Assert.That(offer.Enabled, Is.True);
            Assert.That(sim.Execute(offer.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(PeopleWords.Order(ViewKit2.Context(sim), sim.State.People.OrderFor(OrderTargetKind.Field, 85)), Does.StartWith("Keep Radish · Sell at the market, not below").And.EndWith("Every season"));
            SheetVm up = OrderSheet.Build(ViewKit2.Context(sim), OrderTargetKind.Field, 85, "radish|once|30");
            Assert.That(up.Lines.Single(l => l.Action?.Id == "min:up").Action.Opens.Value.Arg, Is.EqualTo("radish|once|31"));
            AssertWords(HandSheet.Build(ViewKit2.Context(sim), h.Id));
        }

        // ADR 0039 §6: sowing another crop on an ordered field asks whether to change the order.
        [Test]
        public void SowOnOrderedField_AsksToChangeTheOrder()
        {
            Simulation sim = Rich();
            Hand h = Hire(sim);
            // The order sows at once (ADR 0049); clearing the field leaves it empty with its order until the next hour.
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.KeepCrop(85, "radish", SeedSource.Certified, "none", SellRule.Store)));
            Ok(sim, new ClearField(sim.State.Clock.SimTime, 85));
            Assert.That(sim.State.Farm.CropOn(85), Is.Null);
            SowSheetVm sow = SowSheet.Build(ViewKit2.Context(sim), 85);
            Assert.That(sow.Rows.Single(r => r.CropId == "radish").Sow.Confirm, Is.Null, "same crop: no question");
            CropRowVm potato = sow.Rows.Single(r => r.CropId == "potato");
            Assert.That(potato.Sow.Confirm.Question, Does.Contain("Potato"));
            long t = sim.State.Clock.SimTime;
            foreach (Command c in potato.Sow.Confirm.Yes(t)) Ok(sim, c);
            Assert.That(sim.Config.Game.Farm.Crops[sim.State.People.OrderFor(OrderTargetKind.Field, 85).Crop].Id, Is.EqualTo("potato"));
            Assert.That(sim.State.Farm.CropOn(85), Is.Not.Null);
        }

        // Spec 006 AC-21.
        [Test]
        public void AwayCard_People()
        {
            Simulation sim = Rich();
            Hand h = Hire(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(85, "radish", SeedSource.Certified, "none", SellRule.Trader)));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(86, "wheat", SeedSource.Certified, "none", SellRule.Store)));
            Ok(sim, new AdvanceTo(t + 30 * Hour, AdvanceMode.Away));
            IReadOnlyList<SimEvent> events = sim.LastAwayReport;
            AwayVm card = AwayView.People(ViewKit2.Context(sim), events);
            Assert.That(card, Is.Not.Null);
            var done = events.OfType<OrderExecuted>().ToList();
            Assert.That(card.OrderSales, Is.EqualTo(done.Where(e => e.Action != OrderAction.Sowed).Sum(e => e.Amount)));
            Assert.That(card.OrderInputs, Is.EqualTo(-done.Where(e => e.Action == OrderAction.Sowed).Sum(e => e.Amount)));
            Assert.That(card.OrderSales, Is.GreaterThan(0));
            Assert.That(card.Lines.Count, Is.LessThanOrEqualTo(8));
            Assert.That(card.Lines.Any(l => l.Value.StartsWith("Brought in")), Is.True);
            Assert.That(card.Subtitle, Is.EqualTo("30 h away").Or.EqualTo("1 d 6 h away"));
        }

        // ADR 0040: people move between the fields with work for them, and go home when there's none.
        [Test]
        public void Map_Patrols_And_Homes()
        {
            Simulation sim = Rich();
            Hand h = Hire(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new Sow(t, 85, "radish", SeedSource.Certified, "none"));
            Ok(sim, new Sow(t, 86, "radish", SeedSource.Certified, "none"));
            PersonVm idle = MapView.Build(ViewKit2.Context(sim)).People.Single(p => p.Kind == PersonKind.Hand);
            Assert.That(idle.Patrol, Is.EqualTo(new[] { 85, 86 }), "no orders: his hours still tend the growing fields (ADR 0048 §2)");
            Assert.That(idle.HomeTile, Is.EqualTo(119));
            Assert.That(idle.HomeHut, Is.EqualTo(h.HutId));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(85, "radish", SeedSource.Certified, "none", SellRule.Store)));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(86, "radish", SeedSource.Certified, "none", SellRule.Store)));
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.People.Single(p => p.Kind == PersonKind.Hand).Patrol, Is.EquivalentTo(new[] { 85, 86 }));
            Assert.That(map.People.Single(p => p.Kind == PersonKind.Founder).Patrol, Is.Empty, "the founder keeps off the hand's fields (owner, 2026-10-19)");
            Ok(sim, new Sow(t, 100, "radish", SeedSource.Certified, "none"));
            map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.People.Single(p => p.Kind == PersonKind.Founder).Patrol, Is.EqualTo(new[] { 100 }), "and works the field nobody else has");
            Assert.That(map.People.Single(p => p.Kind == PersonKind.Hand).Patrol, Is.EqualTo(new[] { 85, 86, 100 }), "his own fields first, then the rest");

            // Nothing growing and no orders: home.
            foreach (int tile in new[] { 85, 86 }) Ok(sim, new ClearStandingOrder(t, h.Id, OrderTargetKind.Field, tile));
            foreach (int tile in new[] { 85, 86, 100 }) Ok(sim, new ClearField(t, tile));
            Assert.That(MapView.Build(ViewKit2.Context(sim)).People.Single(p => p.Kind == PersonKind.Hand).Patrol, Is.Empty);
        }

        // Spec 006 AC-25 (ADR 0049 §3): labourers are hired from People, till the crops growing now are in; never from a field.
        [Test]
        public void PeopleSheet_HiresTillCropsIn()
        {
            Simulation sim = Rich();
            long t = sim.State.Clock.SimTime;
            SheetVm people = PeopleSheet.Build(ViewKit2.Context(sim));
            Assert.That(people.Lines.Any(l => l.Action?.Id?.StartsWith("hire:") == true), Is.False, "nothing growing");
            Assert.That(people.Lines.Any(l => l.Value == "Sow something first: they stay till its harvest"), Is.True);
            Ok(sim, new Sow(t, 86, "potato", SeedSource.Certified, "none"));
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), 86).Lines.Any(l => l.Action?.Id?.StartsWith("hire:") == true), Is.False, "not on the field");
            int free = PeopleRules.FreeNow(sim);
            people = PeopleSheet.Build(ViewKit2.Context(sim));
            List<LineVm> hires = people.Lines.Where(l => l.Action?.Id?.StartsWith("hire:") == true).ToList();
            Assert.That(hires.Count, Is.EqualTo(free));
            Assert.That(hires[0].Label, Is.EqualTo("1 labourer till harvest"));
            Assert.That(sim.Execute(hires[0].Action.Create(t)).Accepted, Is.True);
            Assert.That(PeopleSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Working today").Value, Is.EqualTo("1, till the crops are in"));
            Assert.That(MapView.Build(ViewKit2.Context(sim)).People.Single(p => p.Kind == PersonKind.Labourer).Patrol, Is.EqualTo(new[] { 86 }));
        }

        [Test]
        public void Map_People_And_Construction()
        {
            Simulation sim = Rich();
            Ok(sim, new Sow(sim.State.Clock.SimTime, 86, "potato", SeedSource.Certified, "none"));
            Ok(sim, new HireLabourers(sim.State.Clock.SimTime, 2));
            Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, 119));
            sim.AdvanceTo(sim.State.Clock.SimTime + 12 * Hour);
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.People.Count(p => p.Kind == PersonKind.Founder), Is.EqualTo(1));
            Assert.That(map.People.Count(p => p.Kind == PersonKind.Labourer), Is.EqualTo(PeopleRules.DayLabourHours(sim) > Fixed.Zero ? 2 : 0));
            Assert.That(map.Constructions.Single().Progress, Is.EqualTo(0.5f).Within(0.05f));
            ulong hash = sim.State.ComputeHash();
            ViewContext ctx = ViewKit2.Context(sim);
            PeopleSheet.Build(ctx);
            HutPlacement.Places(ctx);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "views never change the state");
        }
    }
}
