using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Finance;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 020 §3.6: the sow sheet as a ladder (order, ready time, an hour's earnings, notes).</summary>
    public class SowSheetTests
    {
        /// <summary>A new game in Spring, day 1, at <paramref name="hour"/>:00 (the sim's time of day is the device's, spec 001).</summary>
        private static Simulation SpringAt(int hour) { return ViewKit.NewGame(localSeconds: hour * 3600L); }

        // Spec 020 AC-15 (US-228's part: the order and the ready times; US-232 adds "and no other crop").
        [Test]
        public void CropLadder_OrderAndReadyAt()
        {
            Simulation sim = SpringAt(15);
            ViewContext ctx = ViewKit2.Context(sim);
            SowSheetVm sheet = SowSheet.Build(ctx, 86);
            var now = sheet.Rows.ToList();
            Assert.That(now.Select(r => r.CropId), Is.EqualTo(new[] { "methi", "radish", "okra", "maize", "potato" }), "quickest first, and no other crop (US-232)");
            Assert.That(now.Select(r => r.ReadyText), Is.EqualTo(new[] { "Ready at 16:00", "Ready at 17:00", "Ready at 19:00", "Ready at 21:00", "Ready tomorrow 06:00" }));
            Assert.That(sheet.AllCrops.Opens.Value.Kind, Is.EqualTo(CropCalendar.Sheet), "All crops › opens the crop calendar");
            Assert.That(sheet.AllCrops.Label, Is.EqualTo("All crops ›"));

            // Each row has its note, old crops and new (spec 020 §3.6).
            Assert.That(sheet.Rows.All(r => !string.IsNullOrEmpty(r.NoteText)), Is.True);
            Assert.That(now[2].NoteText, Is.EqualTo("Bhindi: picked every other day, so it needs plenty of hands."));

            // Late in the evening the quick crop is still today, the next one tomorrow; a crop longer than a day says in how many days.
            Simulation late = SpringAt(22);
            SowSheetVm night = SowSheet.Build(ViewKit2.Context(late), 86);
            Assert.That(night.Rows.Single(r => r.CropId == "methi").ReadyText, Is.EqualTo("Ready at 23:00"));
            Assert.That(night.Rows.Single(r => r.CropId == "radish").ReadyText, Is.EqualTo("Ready tomorrow 00:00"));
            Assert.That(SowSheet.ReadyText(ctx, sim.State.Clock.SimTime, 40 * 3600L), Is.EqualTo("Ready in 2 days"), "15:00 + 40 h is 07:00 two days on");

            // A hand's order sheet is the same sheet (ADR 0059), with the same order and lines.
            Simulation rich = PeopleViewTests.Rich();
            PeopleViewTests.Hire(rich);
            ViewContext rctx = ViewKit2.Context(rich);
            SowSheetVm order = OrderSheet.Crops(rctx, 85, null), own = SowSheet.Build(rctx, 85);
            Assert.That(order.Rows.Select(r => r.CropId + r.ReadyText + r.PerHourText + r.NoteText),
                Is.EqualTo(own.Rows.Select(r => r.CropId + r.ReadyText + r.PerHourText + r.NoteText)));
            Assert.That(order.AllCrops, Is.Null, "the order sheet has its Other seasons row instead");
        }

        // Spec 020 AC-16: "about N an hour" is the row's profit ÷ growing hours, rounded to 10 coins, ties away from zero, with a Why?.
        [Test]
        public void CropLadder_PerHour()
        {
            Simulation sim = SpringAt(15);
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (string fert in new[] { "none", "compost", "chemical" })
            {
                SowSheetVm sheet = SowSheet.Build(ctx, 86, fert);
                foreach (CropRowVm r in sheet.Rows)
                {
                    decimal perHour = (decimal)r.Profit / Money.Coin * 3600m / r.GrowthSeconds;
                    long expected = (long)Math.Round(perHour / 10m, MidpointRounding.AwayFromZero) * 10 * Money.Coin;
                    Assert.That(r.PerHour, Is.EqualTo(expected), r.CropId + " " + fert);
                    Assert.That(r.PerHourText, Is.EqualTo((r.PerHour >= 0 ? "about " : "losing about ") + ctx.Fmt.Amount(Math.Abs(r.PerHour)) + " an hour"));
                    Assert.That(WhyView.Has(r.PerHourWhyKey), Is.True, r.CropId);
                    WhyVm why = WhyView.Build(ctx, r.PerHourWhyKey, r.PerHourWhyId);
                    WhyTests.AssertWords(r.CropId + " per hour", why);
                    Assert.That(why.Value, Is.EqualTo("About " + ctx.Fmt.Amount(r.PerHour) + " an hour"), "the Why? shows the row's figure");
                    Assert.That(why.Rows.Select(x => x.Value), Does.Contain(ctx.Fmt.Coins(Math.Abs(r.Profit))).And.Contain(ctx.Fmt.Duration(r.GrowthSeconds)));
                    Assert.That(why.Footnote, Is.EqualTo("A shorter crop earns about as much an hour if you come back when it's ready."));
                }
            }

            // The spec's example: methi 170 coins over 1 h is about 170 an hour.
            Assert.That(SowSheet.PerHour(170 * Money.Coin, 3600), Is.EqualTo(170 * Money.Coin));
            // Rounded to 10 coins, ties away from zero, with nothing rounded before the last step.
            Assert.That(SowSheet.PerHour(150 * Money.Coin, 10 * 3600), Is.EqualTo(20 * Money.Coin), "15 an hour: a tie, up");
            Assert.That(SowSheet.PerHour(-150 * Money.Coin, 10 * 3600), Is.EqualTo(-20 * Money.Coin), "a loss: a tie, away from zero");
            Assert.That(SowSheet.PerHour(149 * Money.Coin, 10 * 3600), Is.EqualTo(10 * Money.Coin));
            Assert.That(SowSheet.PerHour(2660 * Money.Coin, 15 * 3600), Is.EqualTo(180 * Money.Coin), "potato: 177.3 an hour");
            Assert.That(SowSheet.PerHour(0, 3600), Is.EqualTo(0));

            // The id carries the field, the crop and the fertiliser.
            SowSheet.PerHourParts(SowSheet.PerHourId(191, 12, 2), out int tile, out int crop, out int f);
            Assert.That(new[] { tile, crop, f }, Is.EqualTo(new[] { 191, 12, 2 }));
        }
    }
}
