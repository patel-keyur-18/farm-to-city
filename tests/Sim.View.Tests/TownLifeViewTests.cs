using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-151 (spec 026 §7, Preview 14): the Town life tab, the five scores' Why?s and the crime overlay.</summary>
    public class TownLifeViewTests
    {
        private static Simulation Read()
        {
            Simulation sim = Situations.TownshipBuilt();
            if (!TownLifeRules.Known(sim)) TownLifeRules.AtClose(sim);
            Assert.That(TownLifeRules.Known(sim), Is.True);
            return sim;
        }

        private static LineVm Row(SheetVm vm, string key, long id = 0) { return vm.Lines.First(l => l.WhyKey == key && l.WhyId == id); }

        // The tab is there from the Township, opens in place, and a town not yet read says so.
        [Test]
        public void Tab()
        {
            Simulation village = Situations.Town();
            SheetVm v = TreasurySheet.Build(ViewKit2.Context(village));
            Assert.That(v.Lines.Any(l => l.Tabs != null && l.Tabs.Any(t => t.Id == "tab:" + TreasurySheet.LifeTab)), Is.False, "no town life before the Township");

            Simulation sim = Situations.TownshipBuilt();
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm tabs = TreasurySheet.Build(ctx).Lines.First(l => l.Tabs != null);
            ActionVm life = tabs.Tabs.Single(t => t.Id == "tab:" + TreasurySheet.LifeTab);
            Assert.That(life.Opens, Is.EqualTo(new SheetRequest(TreasurySheet.Kind, 0, TreasurySheet.LifeTab)));
            sim.State.Township.TownLife = new TownLifeState();
            SheetVm unread = TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            Assert.That(unread.Lines.Last().Label, Is.EqualTo(ctx.Strings.Get("life.notYet")));
        }

        // Approval, the five scores with their meters, what the name does, crime and inequality, each with a Why?.
        [Test]
        public void Rows_And_Whys()
        {
            Simulation sim = Read();
            ViewContext ctx = ViewKit2.Context(sim);
            TownLifeState st = TownLifeRules.State(sim);
            SheetVm vm = TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            Assert.That(Row(vm, "lifeApproval").Value, Is.EqualTo(ctx.Fmt.Integer(st.Approval.RoundToLong())));
            for (int i = 0; i < TownLifeDef.Scores; i++)
            {
                LineVm r = Row(vm, "lifeScore", i);
                Assert.That(r.Label, Is.EqualTo(ctx.Strings.Get("life.score." + i)));
                Assert.That(r.Meter, Is.EqualTo((int)st.Reputation[i].RoundToLong()), "the bar is the score");
                WhyVm why = WhyView.Build(ctx, "lifeScore", i);
                Assert.That(why.Value, Is.EqualTo(r.Value));
                Assert.That(why.Rows.Count, Is.EqualTo(TownLifeDef.PartsEach), "its three parts");
                Assert.That(why.Rows.All(x => x.Label.Contains("%")), Is.True, "each part names its weight");
                Assert.That(why.Footnote, Is.Not.Null.And.Not.Empty);
            }
            Assert.That(Row(vm, "lifePull").Label, Is.EqualTo(TownLifeSheet.PullLine(ctx)));
            Assert.That(Row(vm, "lifeCrime").Value, Is.EqualTo(ctx.Fmt.Integer(st.Crime.RoundToLong())));
            foreach (string key in new[] { "lifeApproval", "lifePull", "lifeCrime", "lifeRatio" })
            {
                WhyVm why = WhyView.Build(ctx, key, 0);
                Assert.That(why.Title, Is.Not.Empty, key);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), key);
            }
            ulong hash = sim.State.ComputeHash();
            TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view changes nothing");
        }

        // Inequality: too few families, then a ratio in words.
        [Test]
        public void Ratio()
        {
            Simulation sim = Read();
            ViewContext ctx = ViewKit2.Context(sim);
            TownLifeState st = TownLifeRules.State(sim);
            st.RatioKnown = false;
            SheetVm few = TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            Assert.That(Row(few, "lifeRatio").Label, Is.EqualTo(ctx.Strings.Get("life.ratioFew")));
            Assert.That(WhyView.Build(ctx, "lifeRatio", 0).Value, Is.EqualTo(ctx.Strings.Get("life.ratioFewValue")));
            st.RatioKnown = true; st.Ratio = Fixed.FromInt(3); st.RichestMean = 48_000 * 100; st.PoorestMean = 16_000 * 100;
            SheetVm known = TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            Assert.That(Row(known, "lifeRatio").Label, Does.Contain("3.0"));
            WhyVm why = WhyView.Build(ctx, "lifeRatio", 0);
            Assert.That(why.Rows.Count, Is.GreaterThanOrEqualTo(4));
        }

        // The crime overlay: lit once the town is read, one step for each band, the card's number and its Why?.
        [Test]
        public void Overlay()
        {
            Simulation sim = Situations.TownshipBuilt();
            ViewContext ctx = ViewKit2.Context(sim);
            sim.State.Township.TownLife = new TownLifeState();
            Assert.That(OverlayView.Choices(ctx).Single(c => c.Kind == OverlayKind.Crime).Muted, Is.True, "before the first reading: greyed");
            Assert.That(OverlayView.Card(ctx, OverlayKind.Crime, 0).Value, Is.EqualTo(ctx.Strings.Get("overlay.crime.value")));

            TownLifeRules.AtClose(sim);
            TownLifeRules.State(sim).Crime = Fixed.FromInt(58);
            Assert.That(OverlayView.Choices(ctx).Single(c => c.Kind == OverlayKind.Crime).Muted, Is.False);
            OverlayVm vm = OverlayView.Build(ctx, OverlayKind.Crime);
            Tile land = sim.State.Land.Tiles.First(t => OverlayView.IsLand(sim, t));
            Assert.That(vm.Steps[land.Id], Is.EqualTo(3), "50 to 64 is the fourth step");
            Assert.That(vm.Labels.Count, Is.EqualTo(5));
            OverlayCardVm card = OverlayView.Card(ctx, OverlayKind.Crime, land.Id);
            Assert.That(card.Value, Is.EqualTo(ctx.Fmt.Integer(TownLifeRules.CrimeAtTile(sim, land.Id).RoundToLong())), "the card is the sim's number");
            Assert.That(card.WhyKey, Is.EqualTo("lifeCrimeAcre"));
            Assert.That(WhyView.Build(ctx, card.WhyKey, card.WhyId).Rows.Count, Is.GreaterThanOrEqualTo(4));
        }

        // US-150: a petition's row, its Promise and Decline buttons, the choice sheet, the decline card, the bell and its Why?.
        [Test]
        public void Petitions()
        {
            Simulation sim = Read();
            ViewContext ctx = ViewKit2.Context(sim);
            TownLifeState st = TownLifeRules.State(sim);
            TownPetition p = st.Petitions.First(x => !x.Ended);
            SheetVm vm = TreasurySheet.Build(ctx, TreasurySheet.LifeTab);
            LineVm row = Row(vm, "lifePetition", p.Id);
            Assert.That(row.Label, Is.EqualTo(TownLifeSheet.Text(ctx, p)));
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label.Contains("waiting")), Is.True);
            Assert.That(row.Action.Opens, Is.EqualTo(new SheetRequest(TownLifeSheet.PromiseKind, p.Id)), "Promise opens the choice");
            Assert.That(row.Also.Enabled, Is.True);

            // The choice sheet: one Promise button for each of 1, 2 and 4 closes.
            SheetVm promise = TownLifeSheet.Promise(ctx, p.Id);
            var buttons = promise.Lines.Where(l => l.Action != null).Select(l => l.Action).ToList();
            Assert.That(buttons.Count, Is.EqualTo(3));
            Assert.That(ConfirmCards.For(ctx, buttons[0].Create(ctx.Now)), Is.Null, "a promise has no card: nothing is spent");

            // Decline has a card: approval and the quiet time.
            ConfirmCardVm card = ConfirmCards.For(ctx, new AnswerPetition(ctx.Now, p.Id, 0));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Rows[0].Value, Is.EqualTo("−2"));
            Assert.That(card.Rows[1].Value, Does.Contain("4"));

            // The bell carries it, to the Town life tab.
            AlertVm alert = Alerts.Build(ctx).First(a => a.Action != null && a.Action.Id == "petition:" + p.Id);
            Assert.That(alert.Action.Opens, Is.EqualTo(new SheetRequest(TreasurySheet.Kind, 0, TreasurySheet.LifeTab)));

            // Its Why? names the count and what answers it.
            WhyVm why = WhyView.Build(ctx, "lifePetition", p.Id);
            Assert.That(why.Rows.Count, Is.EqualTo(4), "the count, what it takes, who answers, and what answers it by itself");

            // Promised: the row says when, and drops its buttons.
            Assert.That(sim.Execute(new AnswerPetition(ctx.Now, p.Id, 2)).Accepted, Is.True);
            LineVm promised = Row(TreasurySheet.Build(ViewKit2.Context(sim), TreasurySheet.LifeTab), "lifePetition", p.Id);
            Assert.That(promised.Action, Is.Null);
            Assert.That(promised.Note, Does.Contain("Promised for the"));

            // Declined: it moves to "lately settled".
            TownPetition q = st.Petitions.First(x => x.Status == PetitionStatus.Open);
            Assert.That(sim.Execute(new AnswerPetition(ctx.Now, q.Id, 0)).Accepted, Is.True);
            SheetVm after = TreasurySheet.Build(ViewKit2.Context(sim), TreasurySheet.LifeTab);
            Assert.That(Row(after, "lifePetition", q.Id).Dim, Is.True);
        }
    }
}
