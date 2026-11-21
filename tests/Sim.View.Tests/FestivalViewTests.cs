using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-149 (spec 026 §3.1, §7, Preview 14): the festival on the hall's and the square's sheets, the planning sheet, its card, the bell and the day's banner.</summary>
    public class FestivalViewTests
    {
        private static Simulation Town()
        {
            Simulation sim = Situations.TownshipBuilt();
            if (!TownLifeRules.Known(sim)) TownLifeRules.AtClose(sim);
            return sim;
        }

        // The square's sheet and the hall's carry this season's festival and a way in; the planning sheet has the choices, the cost and the button.
        [Test]
        public void Sheets()
        {
            Simulation sim = Town();
            ViewContext ctx = ViewKit2.Context(sim);
            int square = sim.Config.Game.Shops.SquareTile;
            SheetVm tile = FieldSheets.ForTile(ctx, square);
            Assert.That(tile.Lines.Any(l => l.IsHeader && l.Label == ctx.Strings.Get("festival.header")), Is.True, "the square holds festivals");
            ActionVm hold = tile.Actions.Single(a => a.Id == "festival");
            Assert.That(hold.Opens.Value.Kind, Is.EqualTo(FestivalSheet.Kind));

            SheetVm plan = FestivalSheet.Build(ctx, null);
            Assert.That(plan.Title, Is.EqualTo(ctx.Strings.Get("festival." + FestivalRules.Def(sim).Kinds[FestivalRules.KindNow(sim)].Id)));
            Assert.That(plan.Lines.Count(l => l.Tabs != null), Is.EqualTo(3), "where, who pays, which day");
            LineVm cost = plan.Lines.First(l => l.WhyKey == "festival");
            Assert.That(cost.Value, Is.EqualTo(ctx.Fmt.Coins(FestivalRules.Cost(sim, FestivalRules.KindNow(sim)))));
            WhyVm why = WhyView.Build(ctx, "festival", FestivalRules.KindNow(sim));
            Assert.That(why.Rows.Count, Is.EqualTo(5), "each person, people, the day, the shops, and the words");
            ActionVm go = plan.Actions.Single(a => a.Id == "holdFestival");
            Assert.That(go.Enabled, Is.True);

            // A choice is a tab that reopens the sheet with it chosen.
            LineVm payer = plan.Lines.Where(l => l.Tabs != null).ElementAt(1);
            ActionVm you = payer.Tabs.Single(t => t.Id == "fest:p" + FestivalDef.You);
            Assert.That(you.Opens.Value.Arg, Does.Contain(":1:"));
            Assert.That(payer.TabOn, Is.EqualTo("fest:p" + FestivalDef.Town));

            // The hall needs its caretaker: chosen, the button says so.
            SheetVm hall = FestivalSheet.Build(ctx, FestivalDef.Hall + ":0:1");
            if (!FestivalRules.VenueReady(sim, FestivalDef.Hall)) Assert.That(hall.Actions.Single(a => a.Id == "holdFestival").Enabled, Is.False);
        }

        // The card: where, day, cost, payer, what is left, whether the town would still pay its bills; a promise-free, can't-undo line.
        [Test]
        public void Card()
        {
            Simulation sim = Town();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new ScheduleFestival(ctx.Now, FestivalDef.Square, FestivalDef.Town, 2));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Rows.Count, Is.EqualTo(6));
            Assert.That(card.Rows[0].Value, Is.EqualTo(ctx.Strings.Get("festival.venue.square")));
            Assert.That(card.Rows[1].Value, Is.EqualTo(ctx.Strings.Get("festival.dayN", "n", "2")));
            Assert.That(card.Rows[5].Total, Is.True);
            Assert.That(card.Warning, Does.Contain(ctx.Strings.Get("confirm.ScheduleFestival.warn")));
            ConfirmCardVm gift = ConfirmCards.For(ctx, new ScheduleFestival(ctx.Now, FestivalDef.Square, FestivalDef.You, 1));
            Assert.That(gift.Warning, Does.Contain("outside your farm profit"));
        }

        // The bell from the day before, the planned line, the banner on the day, the away line.
        [Test]
        public void Bell_Banner_Record()
        {
            Simulation sim = Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(FestivalSheet.TodayNote(ctx), Is.Null);
            Assert.That(Sim.Core.Commands.CommandResult.Ok.Accepted, Is.True);
            Assert.That(sim.Execute(new ScheduleFestival(ctx.Now, FestivalDef.Square, FestivalDef.Town, 1)).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);
            AlertVm bell = Alerts.Build(ctx).Single(a => a.Action != null && a.Action.Id == "festival");
            Assert.That(bell.Text, Does.Contain("tomorrow"));
            Assert.That(FestivalSheet.Build(ctx, null).Lines.Any(l => l.Tabs != null), Is.False, "this season's is planned: no more choices");

            sim.AdvanceTo((FestivalRules.Today(sim) + 1) * 86400 + 3600);
            ctx = ViewKit2.Context(sim);
            Assert.That(FestivalSheet.TodayNote(ctx), Does.Contain("today"));
            Assert.That(Alerts.Build(ctx).Single(a => a.Action != null && a.Action.Id == "festival").Text, Does.Contain("today"));

            sim.AdvanceTo((FestivalRules.Today(sim) + 1) * 86400 + 3600);
            ctx = ViewKit2.Context(sim);
            Assert.That(FestivalSheet.TodayNote(ctx), Is.Null, "gone the next day");
            SheetVm square = FieldSheets.ForTile(ctx, sim.Config.Game.Shops.SquareTile);
            Assert.That(square.Lines.Any(l => l.IsHeader && l.Label == ctx.Strings.Get("festival.last")), Is.True, "the record");
        }

        // Owner, 2026-11-20: on a festival's day everyone rests at the venue, except the stalls' keepers and customers.
        [Test]
        public void Crowd_AtVenue()
        {
            Simulation sim = Town();
            ViewContext ctx = ViewKit2.Context(sim);
            // Give the founder a family or two at home so there is a crowd.
            Assert.That(sim.Execute(new ScheduleFestival(ctx.Now, FestivalDef.Square, FestivalDef.Town, 1)).Accepted, Is.True);
            long day = FestivalRules.Today(sim) + 1;
            int venue = FestivalRules.Tile(sim, FestivalDef.Square);

            sim.AdvanceTo((day - 1) * 86400 + 10 * 3600); // the day before, mid-morning: people about their work
            MapVm before = MapView.Build(ViewKit2.Context(sim));
            Assert.That(before.People.Any(p => p.Festival), Is.False);

            sim.AdvanceTo(day * 86400 + 10 * 3600); // the festival, mid-morning
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            var gone = map.People.Where(p => p.Kind != PersonKind.Customer && p.Kind != PersonKind.Worker).ToList();
            Assert.That(gone, Is.Not.Empty, "the founder, at least");
            Assert.That(gone.All(p => p.Festival && p.TileId == venue && p.Patrol.Count == 0 && !p.Working), Is.True, "everyone is at the venue, resting");
            Assert.That(gone.Count, Is.LessThanOrEqualTo(MapView.FestivalDrawn));
            Assert.That(gone.Select(p => p.Spot).Distinct().Count(), Is.EqualTo(gone.Count), "each has a place in the crowd");
            Assert.That(map.People.Any(p => p.Kind == PersonKind.Labourer), Is.False, "neighbours hired for the day rest too");

            sim.AdvanceTo(day * 86400 + 23 * 3600); // late evening: home again
            Assert.That(MapView.Build(ViewKit2.Context(sim)).People.Any(p => p.Festival), Is.False);
            sim.AdvanceTo((day + 1) * 86400 + 10 * 3600);
            Assert.That(MapView.Build(ViewKit2.Context(sim)).People.Any(p => p.Festival), Is.False, "the next day: back to work");
        }
    }
}
