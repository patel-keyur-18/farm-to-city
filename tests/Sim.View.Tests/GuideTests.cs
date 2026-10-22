using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Shared driving for the spec 009 view tests: the host's loop of observe, step commands and pages.</summary>
    internal static class GuideKit
    {
        public const long Wall0 = 1_800_000_000;

        public static SettingsInfo Info(Simulation sim, bool notificationsOn = false)
        {
            return new SettingsInfo { NowWallUtc = sim.State.Clock.LastWallUtc, NotificationsOn = notificationsOn };
        }

        public static void Ok(Simulation sim, Command c)
        {
            CommandResult r = sim.Execute(c);
            Assert.That(r.Accepted, Is.True, c.GetType().Name + ": " + r);
        }

        /// <summary>What the host does after every command or Sync: observe the events, then send the steps that are done.</summary>
        public static void Settle(Simulation sim, Guide guide)
        {
            guide.Observe(sim, sim.DrainEvents());
            foreach (Command c in guide.StepCommands(sim, sim.State.Clock.SimTime)) Ok(sim, c);
            sim.DrainEvents();
        }

        /// <summary>Reads the page the guide opens now, if any (Continue, or Not now on an ask); returns its id.</summary>
        public static string ReadNext(Simulation sim, Guide guide)
        {
            string id = guide.Next(sim);
            if (id == null) return null;
            Ok(sim, guide.Close(id, sim.State.Clock.SimTime));
            Settle(sim, guide);
            return id;
        }

        public static void Live(Simulation sim, Guide guide, long seconds)
        {
            Ok(sim, new AdvanceTo(sim.State.Clock.SimTime + seconds));
            Settle(sim, guide);
        }

        public static Simulation Skipped(out Guide guide)
        {
            Simulation sim = ViewKit.NewGame();
            guide = new Guide();
            Ok(sim, new SkipFirstSession(sim.State.Clock.SimTime));
            Settle(sim, guide);
            return sim;
        }
    }

    public class FirstSessionTests
    {
        // Spec 009 AC-2, AC-16: a bot that only does what each step highlights finishes steps 1–7 within 20 minutes of sim time,
        // with the first sale within 15, using only player commands.
        [Test]
        public void HighlightsComplete()
        {
            Simulation sim = ViewKit.NewGame();
            var guide = new Guide();
            ViewContext ctx = ViewKit2.Context(sim);
            long start = sim.State.Clock.SimTime, firstSale = -1;
            GuideKit.Settle(sim, guide);
            var pages = new List<string>();
            while (Guide.FirstSessionActive(sim) && sim.State.Clock.SimTime - start <= 30 * 60)
            {
                string page = GuideKit.ReadNext(sim, guide);
                if (page != null) { pages.Add(page); continue; }
                NudgeVm n = guide.Nudge(ctx);
                Assert.That(n, Is.Not.Null, "a step with nothing to do at " + sim.State.Guidance.Step);
                long t = sim.State.Clock.SimTime;
                string h = n.Highlight;
                if (h == "bed:1" && sim.State.Guidance.Step == 1) GuideKit.Ok(sim, new SowGarden(t, 1, "garden_radish"));
                else if (h == "hut") guide.SheetVisited("Founder");
                else if (h != null && h.StartsWith("bed:"))
                {
                    GardenBed b = sim.State.Farm.Beds.First(x => x.Id == long.Parse(h.Substring(4)));
                    if (b.State == BedState.Ready) GuideKit.Ok(sim, new PickGarden(t, b.Id));
                }
                else if (h == "store")
                {
                    GuideKit.Ok(sim, new SellToTrader(t, "radish", sim.State.Market.InStore(sim.Config.Game.Market.IndexOf("radish"))));
                    if (firstSale < 0) firstSale = t - start;
                }
                else if (h == "tile:86") GuideKit.Ok(sim, new Sow(t, 86, "potato", SeedSource.Certified, "none"));
                GuideKit.Settle(sim, guide);
                if (Guide.FirstSessionActive(sim) && guide.Next(sim) == null) GuideKit.Live(sim, guide, 60);
            }
            TestContext.Out.WriteLine("first session " + (sim.State.Clock.SimTime - start) / 60 + " min, first sale at " + firstSale / 60 + " min");
            Assert.That(Guide.FirstSessionActive(sim), Is.False, "the first session finished");
            Assert.That(sim.State.Clock.SimTime - start, Is.LessThanOrEqualTo(20 * 60));
            Assert.That(firstSale, Is.InRange(0, 15 * 60));
            Assert.That(pages, Is.EqualTo(new[] { "welcome", "garden", "household", "firstPick", "firstSale", "longCrop", "comeBack" }));
        }

        // Spec 009 AC-3: a goal reached early skips its step (and its page); skipping keeps every chapter; no step blocks a command.
        [Test]
        public void SkipAndNeverBlock()
        {
            Simulation sim = ViewKit.NewGame();
            var guide = new Guide();
            GuideKit.Settle(sim, guide);
            long t = sim.State.Clock.SimTime;
            // Ahead of the guide: a bed sown and the household sheet seen while the welcome page is still open.
            GuideKit.Ok(sim, new SowGarden(t, 1, "garden_radish"));
            guide.SheetVisited("Founder");
            GuideKit.Settle(sim, guide);
            Assert.That(sim.State.Guidance.Step, Is.EqualTo(0), "the welcome page isn't read yet");
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("welcome"));
            Assert.That(sim.State.Guidance.Step, Is.EqualTo(3), "garden and household skipped: their goals already hold");
            Assert.That(guide.Next(sim), Is.Null, "no page until the pick");
            Assert.That(guide.Nudge(ViewKit2.Context(sim)).Lines[0], Is.EqualTo("Pick the bed when it's ready"));

            // The same commands are accepted with or without the first session (the guide never rejects or delays).
            Simulation other = ViewKit.NewGame();
            GuideKit.Ok(other, new SkipFirstSession(other.State.Clock.SimTime));
            foreach (Simulation s in new[] { sim, other })
            {
                long at = s.State.Clock.SimTime;
                GuideKit.Ok(s, new Sow(at, 86, "potato", SeedSource.Certified, "none"));
                GuideKit.Ok(s, new SetEatFromStore(at, false));
            }

            // Skipping: the steps end, the week's chapters still come.
            GuideKit.Ok(sim, guide.Skip("firstPick", sim.State.Clock.SimTime)[0]);
            GuideKit.Ok(sim, guide.Skip("firstPick", sim.State.Clock.SimTime)[1]);
            GuideKit.Settle(sim, guide);
            Assert.That(Guide.FirstSessionActive(sim), Is.False);
            Assert.That(guide.Nudge(ViewKit2.Context(sim)), Is.Null);
            Assert.That(sim.Execute(new CompleteStep(sim.State.Clock.SimTime, "firstPick")).Reason, Is.EqualTo(RejectReason.NotCurrentStep));
            GuideKit.Live(sim, guide, 2 * 86400); // the potato is ready and the hours pass
            GuideKit.Ok(sim, new Harvest(sim.State.Clock.SimTime, 86, Overflow.SellRestToTrader));
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Next(sim), Is.EqualTo("firstFieldHarvest"));
        }

        // ADR 0045 §1, AC-13: the nudge pill while a step waits; the tour only during the garden wait; nothing after.
        [Test]
        public void Nudges()
        {
            Simulation sim = ViewKit.NewGame();
            var guide = new Guide();
            ViewContext ctx = ViewKit2.Context(sim);
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Nudge(ctx), Is.Null, "the welcome page waits on itself");
            GuideKit.ReadNext(sim, guide);
            Assert.That(guide.Nudge(ctx), Is.Null, "the garden page isn't read yet");
            GuideKit.ReadNext(sim, guide);
            NudgeVm n = guide.Nudge(ctx);
            Assert.That(n.Lines, Is.EqualTo(new[] { "Tap a garden bed by the hut and sow radish" }));
            Assert.That(n.Highlight, Is.EqualTo("bed:1"));
            GuideKit.Ok(sim, new SowGarden(sim.State.Clock.SimTime, 1, "garden_radish"));
            GuideKit.Settle(sim, guide);
            GuideKit.ReadNext(sim, guide); // household
            Assert.That(guide.Nudge(ctx).Lines.Count, Is.EqualTo(1), "no tour on the household step");
            guide.SheetVisited("Founder");
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Next(sim), Is.Null, "the first-pick page waits for the pick (it reacts to it)");
            n = guide.Nudge(ctx);
            Assert.That(n.Lines.Count, Is.EqualTo(4), "the pick nudge and three tour lines while the bed grows");
            Assert.That(n.Opens.Skip(1).Select(o => o.Value.Kind), Is.EqualTo(new[] { "Store", "Tile", "Founder" }));
            GuideKit.Live(sim, guide, 11 * 60);
            n = guide.Nudge(ctx);
            Assert.That(n.Lines.Count, Is.EqualTo(1), "the bed is ready: no tour");
            Assert.That(n.Highlight, Is.EqualTo("bed:1"));
            GuideKit.Ok(sim, new PickGarden(sim.State.Clock.SimTime, 1));
            GuideKit.Settle(sim, guide);
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("firstPick"));
            n = guide.Nudge(ctx);
            Assert.That(n.Lines, Is.EqualTo(new[] { "Open the Store and sell to the trader" }));
            Assert.That(n.Highlight, Is.EqualTo("store"));
            // Nothing the trader would buy (all of it on offer at the market): no sale nudge, no bouncing store (ADR 0046 §3).
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == GoodKind.Produce && sim.State.Market.InStore(g) > 0)
                    GuideKit.Ok(sim, new Offer(sim.State.Clock.SimTime, goods[g].Id, sim.State.Market.InStore(g)));
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Nudge(ctx), Is.Null);
            GuideKit.Ok(sim, new SkipFirstSession(sim.State.Clock.SimTime));
            Assert.That(guide.Nudge(ctx), Is.Null);
        }

        // ADR 0045 §2, AC-14: comeBack asks; Continue when notifications are on; the card is the fallback after a skip; 2 asks at most.
        [Test]
        public void ComeBackAsks()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            GuideKit.Ok(sim, new Sow(sim.State.Clock.SimTime, 86, "potato", SeedSource.Certified, "none"));
            LetterVm page = Letter.Page(ctx, "comeBack", false, GuideKit.Info(sim), 0);
            Assert.That(page.Asks, Is.True);
            Assert.That(page.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LetterIds.Yes, LetterIds.NotNow }));
            Assert.That(page.Text, Does.Match("Your potato will be ready (today|tonight|tomorrow) at \\d\\d:\\d\\d\\.").And.EndWith("Never at night.")); // spec 011 §3.6b
            Assert.That(page.Text, Does.Not.Contain("twice"));

            LetterVm on = Letter.Page(ctx, "comeBack", false, GuideKit.Info(sim, notificationsOn: true), 0);
            Assert.That(on.Asks, Is.False);
            Assert.That(on.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LetterIds.Continue }));
            Assert.That(on.Text, Does.Not.Contain("nudge"));
            Assert.That(Letter.Page(ctx, "comeBack", true, GuideKit.Info(sim), 0).Asks, Is.False, "reading it again never asks");
            Assert.That(Letter.Page(ctx, "comeBack", false, GuideKit.Info(sim), 1).Asks, Is.False, "already asked");

            // The card: quiet while the letter will ask, the first ask after a skip, never a third.
            IReadOnlyList<SimEvent> sown = sim.PendingEvents.ToList();
            long w = sim.State.Clock.LastWallUtc, t = sim.State.Clock.SimTime;
            Assert.That(NotifyAsk.Moment(sown, 0, false, w, t, letterWillAsk: Guide.FirstSessionActive(sim)), Is.Null);
            GuideKit.Ok(sim, new SkipFirstSession(t));
            Assert.That(NotifyAsk.Moment(sown, 0, false, w, t, letterWillAsk: Guide.FirstSessionActive(sim)).Value.CropId, Is.EqualTo("potato"));
            Assert.That(NotifyAsk.Moment(sown, 2, false, w, t), Is.Null);
        }
    }

    public class ChapterTests
    {
        // Spec 009 AC-4: each chapter opens once, at the first time its condition holds, in priority order; away triggers open on return.
        [Test]
        public void GuidedOnce()
        {
            Simulation sim = GuideKit.Skipped(out Guide guide);
            long t = sim.State.Clock.SimTime;
            // Two at once: a field harvest and a pest warning queue in priority order (firstFieldHarvest before pest).
            GuideKit.Ok(sim, new Sow(t, 84, "radish", SeedSource.Certified, "none"));
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Next(sim), Is.Null);
            GuideKit.Live(sim, guide, 3 * 3600);
            GuideKit.Ok(sim, new Harvest(sim.State.Clock.SimTime, 84));
            guide.Observe(sim, sim.DrainEvents().Concat(new SimEvent[] { new PestWarning(sim.State.Clock.SimTime, 19, Outbreak.Normal, sim.State.Clock.SimTime + 3600) }).ToList());
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("firstFieldHarvest"));
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("pest"));
            Assert.That(guide.Next(sim), Is.Null);
            // A second harvest opens nothing: seen once.
            GuideKit.Ok(sim, new Sow(sim.State.Clock.SimTime, 84, "radish", SeedSource.Certified, "none"));
            GuideKit.Live(sim, guide, 3 * 3600);
            GuideKit.Ok(sim, new Harvest(sim.State.Clock.SimTime, 84));
            GuideKit.Settle(sim, guide);
            Assert.That(guide.Next(sim), Is.Null);

            // Away: a short absence with no news shows no away card and no page; the first return with a card queues its page.
            GuideKit.Ok(sim, new Sync(sim.State.Clock.LastWallUtc + 20 * 60));
            GuideKit.Settle(sim, guide);
            Assert.That(AwayCard.Build(ViewKit2.Context(sim), sim.LastAwayReport), Is.Null);
            Assert.That(guide.Next(sim), Is.Null);
            GuideKit.Ok(sim, new Sync(sim.State.Clock.LastWallUtc + 30 * 3600));
            GuideKit.Settle(sim, guide);
            Assert.That(AwayCard.Build(ViewKit2.Context(sim), sim.LastAwayReport), Is.Not.Null);
            guide.AwayCardShown(sim);
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("firstReturn"));

            // A season start, the first close, care short for an hour, money short for a sowing, cash below the hut's cost.
            GuideKit.Live(sim, guide, sim.Clock.SecondsPerSeason - sim.State.Clock.SimTime % sim.Clock.SecondsPerSeason + 60);
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("seasonReport"));
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("season.summer"));
            foreach (int tile in new[] { 84, 85, 86, 100, 101, 102 }) sim.Execute(new Sow(sim.State.Clock.SimTime, tile, "lentils", SeedSource.Certified, "none"));
            GuideKit.Settle(sim, guide);
            Assert.That(sim.State.Farm.CareRatio, Is.LessThan(Fixed.One), "six acres are more than the founder tends");
            GuideKit.Live(sim, guide, 3600);
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("labour"));
            guide.Rejected(sim, new Sow(sim.State.Clock.SimTime, 84, "lentils", SeedSource.Certified, "none"), RejectReason.InsufficientFunds);
            Assert.That(GuideKit.ReadNext(sim, guide), Is.EqualTo("borrowing"));
            Assert.That(guide.Next(sim), Is.Null);
        }

        // Spec 009 AC-5: seen pages, the step and skipping survive a save and load and a replay of the command log.
        [Test]
        public void SeenSurvivesSaveAndReplay()
        {
            Simulation sim = ViewKit.NewGame();
            var guide = new Guide();
            GuideKit.Settle(sim, guide);
            GuideKit.ReadNext(sim, guide);
            GuideKit.ReadNext(sim, guide);
            GuideKit.Ok(sim, new SeeChapter(sim.State.Clock.SimTime, "garden")); // seeing it again changes nothing
            Assert.That(sim.State.Guidance.Seen.Count, Is.EqualTo(2));
            Assert.That(sim.Execute(new SeeChapter(sim.State.Clock.SimTime, "nope")).Reason, Is.EqualTo(RejectReason.UnknownChapter));
            GuideKit.Ok(sim, new SkipFirstSession(sim.State.Clock.SimTime));

            LoadResult load = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, GuideKit.Wall0), sim.Config);
            Assert.That(load.Ok, Is.True, load.Detail);
            Assert.That(load.State.Guidance.Seen, Is.EqualTo(sim.State.Guidance.Seen));
            Assert.That(load.State.Guidance.Step, Is.EqualTo(1));
            Assert.That(load.State.Guidance.Skipped, Is.True);
            Assert.That(load.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));

            Simulation replay = Simulation.Replay(sim.Config, sim.State.CommandLog.Entries.Select(e => e.Command).ToList());
            Assert.That(replay.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // ADR 0045 §3, AC-12: Menu → Letter lists exactly the pages seen, in the order seen, then the Hamlet's progress.
        [Test]
        public void ReopenList()
        {
            Simulation sim = GuideKit.Skipped(out Guide guide);
            GuideKit.Ok(sim, new SeeChapter(sim.State.Clock.SimTime, "pest"));
            GuideKit.Ok(sim, new SeeChapter(sim.State.Clock.SimTime, "welcome"));
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm list = Letter.List(ctx);
            Assert.That(list.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Pests", "The farm is yours", "Becoming a Hamlet" }));
            Assert.That(list.Lines.Select(l => l.Action.Opens.Value.Kind), Is.EqualTo(new[] { "LetterPage", "LetterPage", "Progress" }));
            LetterVm again = Letter.Page(ctx, "pest", true, GuideKit.Info(sim), 0);
            Assert.That(again.Footer, Is.EqualTo("Page 1 of 2 · read it again from the Menu"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "reading sends no command");
        }
    }

    public class LetterTests
    {
        // ADR 0045 §3, AC-11: the page's text, the signature, "n of m", Show me only with an action; Continue sends SeeChapter.
        [Test]
        public void Sheet()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            var guide = new Guide();
            LetterVm welcome = Letter.Page(ctx, "welcome", false, GuideKit.Info(sim), 0);
            Assert.That(welcome.Title, Is.EqualTo("The farm is yours"));
            Assert.That(welcome.Text, Does.StartWith("My dear beta, the farm is yours now."));
            Assert.That(welcome.Signature, Is.EqualTo("Your Dadi, Kamla"));
            Assert.That(welcome.Footer, Is.EqualTo("Page 1 of 1 · read it again from the Menu"));
            Assert.That(welcome.ShowMe, Is.Null);
            Assert.That(welcome.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LetterIds.Continue }), "no skip before the first step");

            GuideKit.Ok(sim, guide.Close("welcome", sim.State.Clock.SimTime));
            GuideKit.Settle(sim, guide);
            Assert.That(sim.State.Guidance.HasSeen(0), Is.True);
            LetterVm garden = Letter.Page(ctx, "garden", false, GuideKit.Info(sim), 0);
            Assert.That(garden.ShowMe.Value.Kind, Is.EqualTo("Bed"));
            Assert.That(garden.Footer, Is.EqualTo("Page 2 of 2 · read it again from the Menu"));
            Assert.That(garden.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LetterIds.ShowMe, LetterIds.Continue, LetterIds.Skip }));
            foreach (var ch in sim.Config.Game.Onboarding.Chapters)
                Assert.That(Letter.Page(ctx, ch.Id, true, GuideKit.Info(sim), 0).Text, Does.Not.Contain("⟦"), ch.Id);
        }
    }

    public class HintTests
    {
        // Spec 009 AC-6: the season pages name the crops sown in it, the field good at its best price, the seeds dearer next season.
        [Test]
        public void FromContent()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            Assert.That(Hints.Season(ctx, "winter"), Does.StartWith("Rabi. You can sow radish, wheat, potato and lentils now. Wheat fetches its best price this season."));
            Assert.That(Hints.Season(ctx, "spring"), Is.EqualTo("Late rabi. You can sow radish and potato now. Rice fetches its best price this season. Buy rice and lentils seed now: it costs more next season."));
            Assert.That(Hints.Season(ctx, "monsoon"), Does.Contain("Potato fetches its best price"));
            Assert.That(Hints.Season(ctx, "summer"), Does.StartWith("Zaid season. You can sow radish, rice and lentils now. Radish fetches its best price"));

            // Changing the content changes the hint.
            ViewContext edited = ViewKit2.Context(ViewKit.NewGame(edit: (f, j) => f == "crops.json" ? j.Replace("\"sowSeasons\": [\"winter\"]", "\"sowSeasons\": [\"spring\", \"winter\"]") : j));
            Assert.That(Hints.Season(edited, "spring"), Does.StartWith("Late rabi. You can sow radish, wheat and potato now."));
        }

        // Spec 009 AC-7: the seed-keeping hint in the household page, and again at the first Winter only with grain in store and eating on.
        [Test]
        public void SeedKeeping()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(Letter.Page(ctx, "household", false, GuideKit.Info(sim), 0).Text, Does.Contain("switch 'eat from the store' off"));
            Assert.That(Hints.Season(ctx, "winter"), Does.Contain("Your grandfather's wheat and lentils are still in the store"));
            GuideKit.Ok(sim, new SetEatFromStore(sim.State.Clock.SimTime, false));
            Assert.That(Hints.Season(ctx, "winter"), Does.Not.Contain("grandfather"), "eating from the store is off");
            GuideKit.Ok(sim, new SetEatFromStore(sim.State.Clock.SimTime, true));
            foreach (string g in new[] { "wheat", "lentils" })
            {
                int i = sim.Config.Game.Market.IndexOf(g);
                GuideKit.Ok(sim, new SellToTrader(sim.State.Clock.SimTime, g, sim.State.Market.InStore(i)));
            }
            Assert.That(Hints.Season(ctx, "winter"), Does.Not.Contain("grandfather"), "no grain left in store");
        }
    }

    public class EraTests
    {
        // Spec 009 AC-9: the progress view shows this season's acres and their Care, the hut and the profitable seasons, equal to the state.
        [Test]
        public void Progress()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 200000") : j);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = ProgressSheet.Build(ctx);
            Assert.That(vm.Lines.Select(l => l.Label + " = " + l.Value), Is.EqualTo(new[]
                { "This season's harvested acres = 0 of 6", "A house for a hand = Not yet", "A season with farm profit = Not yet" }));
            Assert.That(vm.Lines.All(l => l.WhyKey == "era"), Is.True);

            EraRules.OnHarvest(sim, 84, Fixed.Parse("0.9"));
            EraRules.OnHarvest(sim, 85, Fixed.Parse("1.1"));
            EraRules.OnSeasonClosed(sim, 0, sim.Period);
            vm = ProgressSheet.Build(ctx);
            Assert.That(vm.Lines[0].Value, Is.EqualTo("2 of 6"));
            Assert.That(vm.Lines[1].Value, Is.EqualTo("100% (needs 90%)"));
            Assert.That(EraRules.Acres(sim, sim.Period), Is.EqualTo(2));

            GuideKit.Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, 119));
            Assert.That(ProgressSheet.Build(ctx).Lines[2].Value, Does.StartWith("Building, ready in"));
            GuideKit.Ok(sim, new AdvanceTo(sim.State.Clock.SimTime + 86400));
            Assert.That(ProgressSheet.Build(ctx).Lines.First(l => l.Label == "A house for a hand").Value, Is.EqualTo("Built"));
        }
    }
}
