using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 018 §3.4, AC-7 (US-134, ADR 0095 §8): three advisors who warn and explain, capped, and never act.</summary>
    public class AdvisorTests
    {
        private const long Hour = 3600;

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static void Ok(Simulation sim, Command c) { CommandResult r = sim.Execute(c); Assert.That(r.Accepted, Is.True, c.GetType().Name + ": " + r); }

        /// <summary>A close's report: net result, cash at the start and end, and what you owe against what you own.</summary>
        private static SeasonClosed Closed(Simulation sim, long period, long net = 0, long opening = 0, long closing = 0, long cash = 0, long loans = 0)
        {
            var report = new SeasonReport
            {
                Period = period, ByAccount = new List<KeyValuePair<Account, long>>(),
                Lines = new List<ReportLine> { new ReportLine("farmProfit", 0, new Account[0]), new ReportLine("netResult", net, new Account[0]) },
                CashFlow = new CashFlowSummary { Opening = opening, Closing = closing }, NetWorth = new NetWorthSnapshot { Cash = cash, LoanPrincipal = loans },
            };
            return new SeasonClosed(T(sim), period, report);
        }

        /// <summary>The season report's note at this close, or null.</summary>
        private static AdvisorNote Report(ViewContext ctx, params SimEvent[] events)
        {
            return AdvisorWatch.Observe(ctx, events).SingleOrDefault(a => a.Report);
        }

        private static int OwnedField(Simulation sim, bool rainFed)
        {
            return sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && sim.State.Land.Parcels[t.ParcelId].Owned
                && sim.Config.Game.Land.WellTiles.Contains(t.Id) != rainFed).Id;
        }

        // AC-7: the season report shows the highest-priority note whose condition holds (the content's order); each report note holds
        // at its condition, with the player's numbers, and reads with no placeholder left.
        [Test]
        public void Priority()
        {
            Simulation sim = Situations.Town();
            Ok(sim, new SkipFirstSession(T(sim)));
            ViewContext ctx = ViewKit2.Context(sim);
            GameDefs g = sim.Config.Game;
            Assert.That(g.Advisors.Notes.Select(n => n.Id), Is.EqualTo(AdvisorDefs.NoteIds), "twelve notes in ADR 0095 §8's order");
            Assert.That(g.Advisors.Notes.Count(n => n.Report), Is.EqualTo(8));
            long p = sim.Period, c = Money.Coin;
            int field = OwnedField(sim, rainFed: true);
            foreach (Hand h in sim.State.People.Hands) h.Fatigue = Fixed.Zero;
            Assert.That(SchoolRules.State(sim).Status, Is.EqualTo(SchoolStatus.None));

            SeasonClosed close = Closed(sim, p, net: 5_000 * c, opening: 50_000 * c, closing: 40_000 * c, cash: 40_000 * c, loans: 60_000 * c);
            SimEvent soil = new FieldSeasonClosed(close.SimTime, field, Fixed.FromInt(-3), Fixed.FromInt(35), 30);
            AdvisorNote a = Report(ctx, close, soil);
            Assert.That((a.Id, a.Key, a.Numbers), Is.EqualTo(("debt", p, new[] { 60_000 * c, 40_000 * c })), "loans over half of what you own come first");
            Assert.That(AdvisorWords.Text(ctx, a), Is.EqualTo("You owe 60,000 coins against 40,000 coins you own: over 60%. One bad season could cost you land."));

            close = Closed(sim, p, net: 5_000 * c, opening: 50_000 * c, closing: 40_000 * c, cash: 40_000 * c, loans: 10_000 * c);
            a = Report(ctx, close, soil);
            Assert.That((a.Id, a.Numbers[0], a.Numbers[1]), Is.EqualTo(("profitNotCash", 5_000 * c, 10_000 * c)), "then a profit with cash falling");
            a = Report(ctx, Closed(sim, p, net: 5_000 * c, opening: 50_000 * c + 49, closing: 40_000 * c + 51, cash: 40_000 * c, loans: 10_000 * c), soil);
            Assert.That(a.Numbers[1], Is.EqualTo(9_999 * c), "the fall between the report's rounded start (50,000) and end (40,001)");

            close = Closed(sim, p, cash: 40_000 * c);
            a = Report(ctx, close, soil);
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("soil", new[] { (long)field, Fixed.FromInt(35).Raw })), "then the soil");
            Assert.That(AdvisorWords.Text(ctx, a), Does.EndWith("its soil is down to 35.0. Compost it, or rest it with lentils."));
            Assert.That(Report(ctx, close, new FieldSeasonClosed(close.SimTime, field, Fixed.FromInt(2), Fixed.FromInt(35), 0)), Is.Not.Null.And.Property("Id").Not.EqualTo("soil"),
                "soil that rose isn't worn");

            // The dry field: rain-fed only, once the pump can be had (the Hamlet).
            a = Report(ctx, close, new FieldSeasonClosed(close.SimTime, field, Fixed.Zero, Fixed.FromInt(60), 30));
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("dry", new[] { (long)field, 30L })));
            Assert.That(AdvisorWords.Text(ctx, a), Does.Contain("sat dry for 30 hours under its crop"));
            Assert.That(Report(ctx, close, new FieldSeasonClosed(close.SimTime, OwnedField(sim, rainFed: false), Fixed.Zero, Fixed.FromInt(60), 30))?.Id, Is.Not.EqualTo("dry"), "the well waters its acres");
            Assert.That(Report(ctx, close, new FieldSeasonClosed(close.SimTime, field, Fixed.Zero, Fixed.FromInt(60), 10))?.Id, Is.Not.EqualTo("dry"), "a few dry hours");

            // People: a worn-out hand, then children with no school, then grown-ups who can't read once it's open.
            Hand hand = sim.State.People.Hands[0];
            hand.Fatigue = Fixed.Parse("0.35");
            a = Report(ctx, close);
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("tired", new long[] { hand.Name })));
            hand.Fatigue = Fixed.Zero;
            a = Report(ctx, close);
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("children", new[] { 2L })), "Ravi (12) and Asha (9)");
            Assert.That(AdvisorWords.Text(ctx, a), Is.EqualTo("2 children aren't in school. The school plot is waiting."));
            SchoolRules.State(sim).Status = SchoolStatus.Open;
            a = Report(ctx, close);
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("adultClass", new[] { (long)SchoolRules.Learners(sim).Count, SchoolRules.ClassCost(sim) })));
            Assert.That(AdvisorWords.Text(ctx, a), Does.Contain("Readers earn 10% more at outside work"));
            SchoolRules.State(sim).Status = SchoolStatus.None;

            // Every note reads whole, alert notes too.
            foreach (AdvisorNoteDef n in g.Advisors.Notes)
            {
                long[] numbers = n.Id == "profitNotCash" ? new[] { 1L, 2L, 3L } : n.Id == "overdraft" ? new long[] { 0, 1, 2 } : n.Id == "soil" || n.Id == "dry" ? new long[] { field, 4 }
                    : new long[] { hand.Name, 2 };
                AdvisorVm line = AdvisorWords.Line(ctx, new AdvisorNote(n.Id, 0, n.Report, numbers), compact: !n.Report);
                Assert.That(line.Text + line.Who, Does.Not.Contain("{"), n.Id);
                Assert.That(line.Portrait, Does.StartWith("portrait_advisor_"));
            }
        }

        // AC-7: at most one advisor line a sheet; a topic at most once a game day; none in the first session; they never act.
        [Test]
        public void Caps()
        {
            Simulation sim = Situations.Growing();
            ViewContext ctx = ViewKit2.Context(sim);
            sim.State.Guidance.Step = 0;
            sim.State.Guidance.Skipped = false;
            long t = T(sim);
            int pests = 86, other = 102;
            var moments = new SimEvent[] { new PestWarning(t, pests, Outbreak.Normal, 0), new PestWarning(t, other, Outbreak.Normal, 0), new LoanPaymentMissed(t, 1, 100, 1), new FeedLow(t, "straw", 1) };
            Assert.That(Guide.FirstSessionActive(sim), Is.True);
            Assert.That(AdvisorWatch.Observe(ctx, moments), Is.Empty, "never during the first session");
            Ok(sim, new SkipFirstSession(t));

            List<AdvisorNote> said = AdvisorWatch.Observe(ctx, moments);
            Assert.That(said.Select(a => (a.Id, a.Key)), Is.EqualTo(new[] { ("missedPayment", 0L), ("pests", (long)pests), ("feed", (long)sim.Config.Game.Market.IndexOf("straw")) }),
                "each topic once a pass, in priority order");
            ulong before = Hash(sim, withoutGuidance: true);
            long cash = sim.State.Ledger.Cash;
            Ok(sim, AdvisorWatch.Command(t, said));
            Assert.That((Hash(sim, withoutGuidance: true), sim.State.Ledger.Cash), Is.EqualTo((before, cash)), "they never act");

            // Within a game day the topic stays quiet, then speaks again.
            sim.AdvanceTo(t + 23 * Hour);
            Assert.That(AdvisorWatch.Observe(ctx, new SimEvent[] { new PestWarning(T(sim), other, Outbreak.Normal, 0) }), Is.Empty, "pests spoke within the day");
            sim.AdvanceTo(t + 24 * Hour);
            Assert.That(AdvisorWatch.Observe(ctx, new SimEvent[] { new PestWarning(T(sim), other, Outbreak.Normal, 0) }).Single().Key, Is.EqualTo(other));

            // One line on the alert list: under the alert whose note ranks first among those spoken about.
            Crop crop = sim.State.Farm.Crops.First(x => x.TileId == pests);
            crop.State = CropState.Growing;
            crop.Outbreak = Outbreak.Normal;
            crop.Spray = SprayState.None;
            SheetVm alerts = Alerts.Sheet(ctx);
            List<LineVm> lines = alerts.Lines.Where(l => l.Advisor != null).ToList();
            Assert.That(lines.Count, Is.EqualTo(1), "one advisor line a sheet");
            int i = alerts.Lines.IndexOf(lines[0]);
            Assert.That(alerts.Lines[i - 1].Label, Does.Contain("Pests on"));
            Assert.That(lines[0].Advisor.Text, Does.StartWith("Ramdeen Kaka: “Spray now. Left alone, pests take 20% to 60% of the crop; sprayed, only 30% of that.”"));
            Assert.That(lines[0].Advisor.Compact, Is.True);

            // One line on the season report: the close's note, there each time the report opens.
            long p = sim.Period;
            SeasonClosed close = Closed(sim, p, cash: 1_000, loans: 100_000);
            List<AdvisorNote> atClose = AdvisorWatch.Observe(ctx, new SimEvent[] { close, new PestWarning(close.SimTime, other, Outbreak.Normal, 0) });
            Assert.That(atClose.Count(a => a.Report), Is.EqualTo(1));
            Ok(sim, AdvisorWatch.Command(T(sim), atClose));
            LineVm report = AdvisorWords.Report(ctx, p);
            Assert.That((report.Advisor.Who, report.Advisor.Compact), Is.EqualTo(("Sunita Verma, the co-op bank's secretary", false)));
            Assert.That(AdvisorWords.Report(ctx, p).Advisor.Text, Is.EqualTo(report.Advisor.Text), "the same line when the report opens again");
            Assert.That(AdvisorWords.Report(ctx, p - 1), Is.Null);
            Assert.That(AdvisorWatch.Observe(ctx, new SimEvent[] { Closed(sim, p, cash: 1_000, loans: 100_000) }).Where(a => a.Report).Select(a => a.Id), Has.None.EqualTo("debt"),
                "the same topic waits a day");

            // Refusals, saves and replays.
            Assert.That(sim.Execute(new HearAdvisors(T(sim), new[] { new AdvisorNote("nope", 0, false, null) })).Reason, Is.EqualTo(RejectReason.UnknownAdvisorNote));
            Assert.That(sim.Execute(new HearAdvisors(T(sim), new[] { new AdvisorNote("pests", 0, true, null) })).Reason, Is.EqualTo(RejectReason.UnknownAdvisorNote), "pests aren't a report note");
            LoadResult load = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 1_800_000_000), sim.Config);
            Assert.That(load.Ok, Is.True, load.Detail);
            Assert.That(load.State.Guidance.Advice.Select(a => (a.Id, a.At, a.Key, a.Report, string.Join(",", a.Numbers))),
                Is.EqualTo(sim.State.Guidance.Advice.Select(a => (a.Id, a.At, a.Key, a.Report, string.Join(",", a.Numbers)))));
            Assert.That(load.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // The farm advisor's dry field and worn soil come from the sim (spec 018 §3.4): a crop's hours below its moisture minimum and the
        // season's fertility change, given at the close for each of your fields, then cleared.
        [Test]
        public void FieldSeason()
        {
            Simulation sim = Situations.Growing();
            int field = OwnedField(sim, rainFed: true);
            Ok(sim, new Sow(T(sim), field, "potato", SeedSource.Certified, "none"));
            Tile tile = sim.State.Land.Tiles[field];
            long S = sim.Clock.SecondsPerSeason, close = (T(sim) / S + 1) * S;
            List<FieldSeasonClosed> seen = new List<FieldSeasonClosed>();
            sim.DrainEvents();
            while (T(sim) < close)
            {
                tile.Moisture = Fixed.Zero; // a dry spell all season
                sim.AdvanceTo(System.Math.Min(close, T(sim) + Hour));
                seen.AddRange(sim.DrainEvents().OfType<FieldSeasonClosed>());
            }
            FieldSeasonClosed f = seen.Single(x => x.TileId == field);
            Assert.That(f.DryHours, Is.GreaterThan(0), "hours under a growing crop below its minimum");
            Assert.That(f.SimTime, Is.EqualTo(close));
            Assert.That(tile.DrySeconds, Is.EqualTo(0), "cleared for the next season");
            Assert.That(seen.All(x => sim.State.Land.Parcels[sim.State.Land.Tiles[x.TileId].ParcelId].Owned), Is.True, "your fields only");
        }

        // Spec 018 §4: advisors.json loads and is checked.
        [Test]
        public void Content()
        {
            string json = ViewKit.ReadContent("advisors.json");
            AdvisorDefs d = ContentLoader.LoadAdvisors(json);
            Assert.That((d.Advisors.Count, d.Notes.Count, d.TopicHours), Is.EqualTo((3, 12, 24)));
            Assert.Throws<ContentException>(() => ContentLoader.LoadAdvisors(json.Replace("\"id\": \"raise\"", "\"id\": \"gossip\"")), "a note needs a condition");
            Assert.Throws<ContentException>(() => ContentLoader.LoadAdvisors(json.Replace("[\"PestWarning\"]", "[\"Rain\"]")), "known alerts only");
            Assert.Throws<ContentException>(() => ContentLoader.LoadAdvisors(json.Replace("\"report\": true, \"threshold\": 40", "\"report\": true")), "soil needs its threshold");
            Assert.Throws<ContentException>(() => ContentLoader.LoadAdvisors(json.Replace("\"advisor\": \"people\",     \"alerts\": [\"HrEventRaised\"]", "\"advisor\": \"people\"")), "a note speaks somewhere");
        }

        private static ulong Hash(Simulation sim, bool withoutGuidance)
        {
            GuidanceState g = sim.State.Guidance;
            var advice = new List<AdvisorNote>(g.Advice);
            if (withoutGuidance) g.Advice.Clear();
            ulong h = sim.State.ComputeHash();
            g.Advice.AddRange(advice.Where(a => !g.Advice.Contains(a)));
            return h;
        }
    }
}
