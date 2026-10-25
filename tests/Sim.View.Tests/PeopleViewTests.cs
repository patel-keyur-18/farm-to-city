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
            Assert.That(PeopleSheet.Hire(ViewKit2.Context(sim)).Lines.Count(l => l.Action != null && l.Action.Id.StartsWith("hire:")), Is.EqualTo(0), "nothing growing to hire for (ADR 0049)");
            // US-215: the Store's layout, picture buttons for hiring, a hut and sending word.
            List<ActionVm> buttons = s.Lines.Single(l => l.Buttons != null).Buttons;
            Assert.That(buttons.Select(b => b.Id), Is.EqualTo(new[] { "hire", "buildHut", "word" }));
            Assert.That(buttons.Select(b => b.Opens.Value.Kind), Is.EqualTo(new[] { "Hire", "HutPlacement", "Word" }));
            Assert.That(buttons.All(b => b.Icon != null && b.Detail != null), Is.True);
            Assert.That(PeopleSheet.Word(ViewKit2.Context(sim)).Lines.Single(l => l.Action?.Id == "postMarket").Action.Enabled, Is.False, "no hut yet");

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
            Assert.That(s.Lines.Single(l => l.Action?.Id == "hand:" + h.Id).Icon, Does.StartWith("person_hand_"));
            Assert.That(s.Lines.Single(l => l.Action?.Id == "hand:" + h.Id).Note, Is.Not.Null, "the hand's orders");
            Assert.That(s.Lines.Any(l => l.Icon == "hand_hut" && l.Action?.Id?.StartsWith("hut:") == true), Is.True, "the hut he lives in");
            Assert.That(FieldSheets.ForTile(ctx, 119).Lines.Any(l => l.Label.StartsWith("Hut place")), Is.True);

            // A field sheet offers the order; the order sheet steps crop, then sell rule.
            LineVm give = FieldSheets.ForTile(ctx, 85).Lines.Single(l => l.Action?.Id == "order");
            Assert.That(give.Action.Opens.Value.Kind, Is.EqualTo("Order"));
            // ADR 0059 §3: the crop step is the player's sow sheet, with the fertiliser choice and every figure, and Choose.
            Assert.That(OrderSheet.IsCropStep(OrderTargetKind.Field, null), Is.True);
            SowSheetVm crops = OrderSheet.Crops(ctx, 85, null);
            Assert.That(crops.Rows.Count, Is.EqualTo(sim.Config.Game.Farm.Crops.Count(c => !c.Fodder)), "each crop (fodder has its own card, US-205)");
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
            SheetVm people = PeopleSheet.Hire(ViewKit2.Context(sim));
            Assert.That(people.Lines.Any(l => l.Action?.Id?.StartsWith("hire:") == true), Is.False, "nothing growing");
            Assert.That(people.Lines.Any(l => l.Value == "Sow something first: they stay till its harvest"), Is.True);
            Ok(sim, new Sow(t, 86, "potato", SeedSource.Certified, "none"));
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), 86).Lines.Any(l => l.Action?.Id?.StartsWith("hire:") == true), Is.False, "not on the field");
            int free = PeopleRules.FreeNow(sim);
            people = PeopleSheet.Hire(ViewKit2.Context(sim));
            List<LineVm> hires = people.Lines.Where(l => l.Action?.Id?.StartsWith("hire:") == true).ToList();
            Assert.That(hires.Count, Is.EqualTo(free));
            Assert.That(hires[0].Label, Is.EqualTo("1 labourer till harvest"));
            Assert.That(sim.Execute(hires[0].Action.Create(t)).Accepted, Is.True);
            Assert.That(PeopleSheet.Hire(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Working today").Value, Is.EqualTo("1, till the crops are in"));
            Assert.That(PeopleSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Icon == "person_labourer_m" && l.Note != null).Label, Is.EqualTo("1 labourer working"));
            Assert.That(MapView.Build(ViewKit2.Context(sim)).People.Single(p => p.Kind == PersonKind.Labourer).Patrol, Is.EqualTo(new[] { 86 }));
        }

        // Spec 014 §3.13 (AC-14): every needs row says what would help, or that it can't be changed yet; never a bare number.
        [Test]
        public void NeedsLevers()
        {
            Simulation sim = Rich();
            Hand h = Hire(sim);
            Household x = sim.State.Citizens.Household(sim.State.Citizens.OfHand(h.Id).HouseholdId);
            List<LineVm> rows = NeedsView.Rows(ViewKit2.Context(sim), x);
            Assert.That(rows.Count(r => r.IsHeader), Is.EqualTo(4), "the four layers");
            Assert.That(rows.Where(r => !r.IsHeader).All(r => !string.IsNullOrEmpty(r.Note)), Is.True, "every need names its lever");
            Assert.That(rows.Single(r => r.Label == "Steady work").Note, Is.EqualTo("1 of 1 adults work for you"));
            Assert.That(rows.Single(r => r.Label == "School").Note, Does.Contain("the Village builds one"));
            x.FoodMet = Sim.Core.Maths.Fixed.Parse("0.83");
            sim.State.Citizens.OfHand(h.Id).Job = CitizenJob.Outside;
            rows = NeedsView.Rows(ViewKit2.Context(sim), x);
            Assert.That(rows.Single(r => r.Label == "Food").Note, Is.EqualTo("Their money buys 83% of the food they need: a better wage or a job here helps"));
            Assert.That(rows.Single(r => r.Label == "Food").NoteTone, Is.EqualTo(Tone.Good), "0.83 is above the 0.8 gate");
            Assert.That(rows.Single(r => r.Label == "Steady work").Note, Does.EndWith("give someone here a job (your hand)"));
            Assert.That(rows.Single(r => r.Label == "Steady work").NoteTone, Is.EqualTo(Tone.Warn));
        }

        // Spec 014 §7, §3.13 (US-210): the Town tab, the household and citizen cards, placing a house, the HUD and the away lines.
        [Test]
        public void TownScreens()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();

            SheetVm workers = PeopleSheet.Build(ctx), town = TownSheet.Build(ctx);
            Assert.That(workers.Tabs.Select(t => t.Label), Is.EqualTo(new[] { "Workers", "Town" }));
            Assert.That(workers.Tab, Is.EqualTo("tab:workers"));
            Assert.That(town.Tab, Is.EqualTo("tab:town"));
            Assert.That(town.Subtitle, Is.EqualTo("6 people in 2 households"), "the hand alone in his hut counts as a household (§3.13)");
            Assert.That(town.Lines.Single(l => l.Label == "Work in town").Value, Is.EqualTo("1 of 3 adults"), "the hand; Manoj and Usha work outside");
            Household family = sim.State.Citizens.Households.Single(x => x.Members.Count == 5);
            LineVm row = town.Lines.Single(l => l.Label == "Manoj's family (5)");
            Assert.That(row.Value, Does.StartWith("Family house 1 · income "));
            Assert.That(row.Opens.Value.Kind, Is.EqualTo("Household"));
            List<string> bySize = TownSheet.Build(ctx, TownSheet.BySize).Lines.Where(l => l.Opens.HasValue && l.Opens.Value.Kind == "Household").Select(l => l.Label).ToList();
            Assert.That(bySize.First(), Is.EqualTo("Manoj's family (5)"), "the biggest first");
            Assert.That(town.Lines.Single(l => l.Buttons != null).Buttons.Single().Label, Is.EqualTo("Build a family house"));

            // The household card: the small tree, members, the budget pie, rent, needs.
            SheetVm card = HouseholdSheet.Build(ctx, family.Id);
            FamilyTreeVm tree = card.Lines.First(l => l.Tree != null).Tree;
            Assert.That(tree.Generations.Select(g => g.Select(p => p.Text)), Is.EqualTo(new[]
                { new[] { "Kamala (66)" }, new[] { "Manoj (38)", "Usha (35)" }, new[] { "Asha (9)", "Ravi (12)" }.Reverse().ToArray() }));
            Assert.That(tree.Generations[1][1].SpouseOfPrevious, Is.True);
            LineVm budget = card.Lines.Single(l => l.Label == "Budget");
            Assert.That(budget.Pie.First().Label, Is.EqualTo("Food"));
            Assert.That(budget.Pie.Sum(p => p.Percent), Is.InRange(97, 103), "rounded shares");
            Assert.That(card.Lines.First(l => l.Label == "Rent").Value, Does.Match(@"^6,0\d0 coins a season · paid$"), "6,000 at today's prices");
            Assert.That(card.Lines.Single(l => l.Label == "Happiness").WhyKey, Is.EqualTo("household.happiness"));
            AssertWords(card);

            // The citizen card: close family with links, the departed folded.
            Citizen manoj = sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == "Manoj");
            SheetVm me = CitizenSheet.Build(ctx, manoj.Id);
            Assert.That(me.Lines.Single(l => l.Label == "Age").Value, Is.EqualTo("38 · adult"));
            Assert.That(me.Lines.Where(l => l.Opens.HasValue && l.Opens.Value.Kind == "Citizen").Select(l => l.Value),
                Is.EqualTo(new[] { "mother, 66", "wife, 35", "son, 12", "daughter, 9" }));
            Assert.That(me.Lines.Single(l => l.Label == "1 who has passed away or moved").Opens.Value.Kind, Is.EqualTo("Departed"));
            Assert.That(CitizenSheet.Departed(ctx, manoj.Id).Lines.Single().Value, Is.EqualTo("Passed away at 74"));
            Assert.That(WhyView.Build(ctx, "citizen.age", manoj.Id).Rows[0].Label, Does.StartWith("People age a year each season"));
            AssertWords(me);

            // Placing a house: lit where it may go, refusals in words; an empty field offers it, the house acre is a homestead.
            int acre = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b)).TileId;
            List<HutPlaceVm> places = HousePlacement.Places(ctx);
            Assert.That(places.Single(p => p.TileId == acre).Allowed, Is.True, "room for a second house");
            Assert.That(places.Single(p => p.TileId == 86).Reason, Is.EqualTo("Something is growing here"));
            Assert.That(FieldSheets.ForTile(ctx, Situations.HouseTile(sim)).Actions.Select(a => a.Label), Does.Contain("Build a family house"));
            SheetVm homestead = FieldSheets.ForTile(ctx, acre);
            Assert.That(homestead.Title, Is.EqualTo("Family houses"));
            Assert.That(homestead.Actions.Select(a => a.Label), Does.Not.Contain("Sow"));
            Assert.That(homestead.Lines.First().Value, Is.EqualTo("Manoj's family"));

            HudVm hud = HudView.Build(ctx);
            Assert.That(hud.HasTown && hud.Population == 6, Is.True);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "views never change the state");

            // A second house fills the acre: "This acre has 2 houses".
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, acre));
            Assert.That(HousePlacement.ReasonFor(ViewKit2.Context(sim), acre), Is.EqualTo("This acre has 2 houses"));

            // Away lines: the season's rent, a hand gone home, a family that left, gently and in words.
            int raju = sim.Config.Game.People.Names.ToList().FindIndex(d => d.Id == "raju");
            var events = new List<SimEvent>
            {
                new RentPaid(1, family.Id, 6_000_00), new CitizenRetired(2, 1, 1, raju, 60, true),
                new HouseholdLeft(3, family.Id, 5, LeaveReason.LandTaken, manoj.Name),
            };
            List<LineVm> away = TownAway.Rows(ctx, events);
            Assert.That(away.Select(l => l.Label + ": " + l.Value), Is.EqualTo(new[]
                { "Your people: Raju (60) retired and went home", "Your people: Manoj's family left: the bank took the land" }));
            Assert.That(TownAway.Rent(events, out int payers), Is.EqualTo(6_000_00));
            Assert.That(payers, Is.EqualTo(1), "a money row on the card: \"Rent from 1 family\"");
            Assert.That(card.Lines.Single(l => l.Label == "Budget").Value, Does.EndWith(budget.Pie.First().Percent + "%"), "the pie's food share as the row says");
        }

        // Spec 014 §3.9, ADR 0070: births, marriages and deaths in gentle words on the away card and the Town tab's "This season".
        [Test]
        public void Flows_InWords()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            CitizensState town = sim.State.Citizens;
            Citizen Named(string n) => town.Citizens.Single(c => PeopleWords.Name(ctx, c.Name) == n);
            Citizen mohan = Named("Mohan"), manoj = Named("Manoj"), asha = Named("Asha"), kamala = Named("Kamala");
            Household family = town.Household(manoj.HouseholdId);
            int village = sim.Config.Game.Population.Villages.ToList().IndexOf("rampur");
            var events = new List<SimEvent>
            {
                new CitizenBorn(1, asha.Id, asha.Name, true, family.Id, asha.Mother),
                new CitizenMarried(2, manoj.Id, manoj.Name, false, kamala.Id, village, family.Id, family.Id),
                new CitizenMarried(3, asha.Id, asha.Name, true, 0, village, family.Id, family.Id),
                new CitizenDied(4, mohan.Id, mohan.Name, false, 74, family.Id, false),
            };
            Assert.That(TownAway.Rows(ctx, events).Select(l => l.Value), Is.EqualTo(new[]
            {
                "A baby girl, Asha, was born to Manoj's family",
                "Manoj married Kamala from Rampur",
                "Asha married and moved to her husband's village, Rampur",
                "Mohan (74), Manoj's father, passed away peacefully. His family mourns this season.",
            }));

            // The Town tab tells this season's story from the record.
            asha.Story.Add(new StoryLine(StoryKind.Born, ctx.Now, 0));
            mohan.Story.Add(new StoryLine(StoryKind.Died, ctx.Now, 0));
            SheetVm tab = TownSheet.Build(ctx);
            int at = tab.Lines.FindIndex(l => l.IsHeader && l.Label == "This season");
            Assert.That(tab.Lines.Skip(at + 1).Take(2).Select(l => l.Label), Is.EqualTo(new[]
                { "Mohan (74), Manoj's father, passed away peacefully. His family mourns this season.", "A baby girl, Asha, was born to Manoj's family" }));
            Assert.That(CitizenSheet.Build(ctx, asha.Id).Lines.Any(l => l.Label == "Born here"), Is.True);
        }

        // Spec 014 §3.6, §3.13 (US-110): a free house is offered to the hand living alone: the card says what changes, an alert
        // answers Yes, the away card tells who moved in, and from the next renewal the wage's Why? shows the house as part of the pay.
        [Test]
        public void HandFamily_Card()
        {
            Simulation sim = Situations.Town();
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, Situations.HouseTile(sim)));
            sim.DrainEvents();
            sim.AdvanceTo(t + 49 * Hour);
            ViewContext ctx = ViewKit2.Context(sim);
            FamilyRequest ask = sim.DrainEvents().OfType<FamilyRequest>().Single();
            Citizen me = sim.State.Citizens.OfHand(ask.HandId);
            string name = PeopleWords.Name(ctx, me.Name), her = me.Female ? "her" : "his";

            CardVm card = Cards.FamilyRequest(ctx, ask.HandId);
            Assert.That(card.Title, Is.EqualTo(name + " asks to bring " + her + " family here"));
            Assert.That(card.Text, Does.StartWith("Family house 2 is free. " + name + "'s family would come from "));
            Assert.That(card.Text, Does.Contain("the wage is 6,"));
            Assert.That(card.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Yes, bring them", "Not now" }));
            AlertVm alert = Alerts.Build(ctx).Single(a => a.Text == card.Title);
            Assert.That(alert.Action.Label, Is.EqualTo("Yes, bring them"));

            Ok(sim, alert.Action.Create(sim.State.Clock.SimTime));
            Assert.That(Cards.FamilyRequest(ViewKit2.Context(sim), ask.HandId), Is.Null, "answered: no card");
            List<LineVm> away = TownAway.Rows(ViewKit2.Context(sim), sim.DrainEvents().ToList());
            Assert.That(away.Single().Value, Is.EqualTo(name + "'s family came to live in Family house 2"));
            Assert.That(WhyView.Build(ViewKit2.Context(sim), "hand.wage", ask.HandId).Rows.Any(r => r.Label.StartsWith("The family's house becomes part of the pay at the next renewal: 6,")), Is.True, "before the renewal");

            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
            WhyRowVm cut = WhyView.Build(ViewKit2.Context(sim), "hand.wage", ask.HandId).Rows.Single(r => r.Label.EndsWith("for the family house"));
            Hand h = sim.State.People.Hand(ask.HandId);
            Assert.That(cut.Value, Is.EqualTo(ViewKit2.Context(sim).Fmt.Coins(h.Wage)), "in cash");
            Assert.That(WhyView.Build(ViewKit2.Context(sim), "hand.home", ask.HandId).Rows.Any(r => r.Label.StartsWith("The family house, part of the pay")), Is.True);
        }

        // Spec 014 §3.3, §7 (US-111): a bride who married in shows her home village; a hand living alone has family in theirs.
        [Test]
        public void FamilyTree_Words()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            CitizensState town = sim.State.Citizens;
            Citizen manoj = town.Citizens.Single(c => PeopleWords.Name(ctx, c.Name) == "Manoj"), usha = town.Citizen(manoj.Spouse);
            usha.Story.Add(new StoryLine(StoryKind.Married, ctx.Now, manoj.Id));
            FamilyTreeVm tree = HouseholdSheet.Build(ctx, manoj.HouseholdId).Lines.First(l => l.Tree != null).Tree;
            Assert.That(tree.Generations[1].Select(p => p.Text), Is.EqualTo(new[] { "Manoj (38)", "Usha (35, from Rampur)" }));
            Assert.That(CitizenSheet.Build(ctx, manoj.Id).Lines.Single(l => l.Label == "Usha").Value, Is.EqualTo("wife, 35, from Rampur"));
            Assert.That(CitizenSheet.Build(ctx, manoj.Id).Lines.Single(l => l.Label == "Asha").Value, Is.EqualTo("daughter, 9"), "born here");

            Citizen hand = town.Citizens.Single(c => c.Living && c.Job == CitizenJob.Hand);
            LineVm home = CitizenSheet.Build(ctx, hand.Id).Lines.Single(l => l.Label == "Their family lives in");
            Assert.That(home.Value, Is.EqualTo(TownWords.Village(ctx, hand)));
        }

        // ADR 0071: a family house with the bank's home loan: the Build button stays open when the loan covers the shortfall; the
        // placement offers "With a home loan"; its sheet has a row a term, each building with the loan.
        [Test]
        public void HomeLoan_Sheet()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = Situations.HouseTile(sim);
            long cost = TownSheet.HouseCost(sim), loan = HouseLoanSheet.Loan(sim, cost);
            Assert.That(loan, Is.GreaterThan(0), "the bank lends against the houses");
            Assert.That(HousePlacement.WithLoan(ctx, tile).Enabled, Is.True);
            SheetVm sheet = HouseLoanSheet.Build(ctx, tile);
            Assert.That(sheet.Title, Is.EqualTo("Build with a home loan"));
            LineVm row = sheet.Lines.First(l => l.Action != null);
            Assert.That(row.Action.Label, Is.EqualTo("Build here"));
            Assert.That(row.Action.Detail, Is.EqualTo(ctx.Fmt.Coins(cost - loan)), "the down payment");
            AssertWords(sheet);
            int loans = sim.State.Credit.Loans.Count;
            Ok(sim, row.Action.Create(sim.State.Clock.SimTime));
            Assert.That(sim.State.Credit.Loans.Count, Is.EqualTo(loans + 1));
            Assert.That(sim.State.Citizens.Constructions.Count, Is.EqualTo(1));
        }

        [Test]
        public void LivesAlone()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen hand = sim.State.Citizens.Citizens.Single(c => c.Living && c.Job == CitizenJob.Hand);
            Citizen manoj = sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == "Manoj");
            string LivesIn(SheetVm s) => s.Lines.Single(l => l.Label == "Lives in").Value;

            Assert.That(LivesIn(CitizenSheet.Build(ctx, hand.Id)), Is.EqualTo("Hut 1 · alone"), "not their own name (owner, 2026-10-25)");
            Assert.That(LivesIn(HandSheet.Build(ctx, hand.HandId)), Is.EqualTo("Hut 1 · alone"));
            Assert.That(LivesIn(CitizenSheet.Build(ctx, manoj.Id)), Is.EqualTo("Family house 1 · Manoj's family"));
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
