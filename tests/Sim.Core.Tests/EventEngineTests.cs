using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-154 (spec 027 §3.1, ADR 0141): the 6:00 roll, the waiting list and its clocks, the away rules and the four mercy rules.</summary>
    public class EventEngineTests
    {
        private const long Day = ClockDef.SecondsPerDay, Hour = 3600;

        /// <summary>A town at the Township; with <paramref name="minor"/> a minor event every day that has one, and no majors unless asked.</summary>
        internal static Simulation Town(string minor = "0.9", string major = "0.0625", System.Func<string, string, string> edit = null)
        {
            return TownLifeTests.Township(12, (f, j) =>
            {
                if (f == "events.json") j = j.Replace("\"minorChance\": 0.9", "\"minorChance\": " + minor).Replace("\"majorChance\": 0.0625", "\"majorChance\": " + major);
                return edit == null ? j : edit(f, j);
            });
        }

        internal static void Pass(Simulation sim, long days) { sim.AdvanceTo(sim.State.Clock.SimTime + days * Day); }

        /// <summary>Runs to just after the next 6:00 (the first roll sees the Township; the days of grace follow).</summary>
        internal static void UntilSix(Simulation sim, int days = 1)
        {
            long t = sim.State.Clock.SimTime;
            long six = (t / Day + days) * Day + 6 * Hour + 1;
            sim.AdvanceTo(six);
        }

        internal static PendingEvent Force(Simulation sim, string id, long t1 = 0, long t2 = 0)
        {
            EventsDef d = EventRules.Def(sim);
            int def = d.IndexOf(id);
            EventRules.Raise(sim, EventRules.State(sim), def, t1, t2, 0, sim.State.Clock.SimTime, d.Events[def].Major);
            return EventRules.State(sim).Waiting.Last();
        }

        // AC-1: nothing before the Township; none in the first two days after the engine sees it; then the same seed gives the same events.
        [Test]
        public void NotBeforeTownship()
        {
            Simulation sim = TownshipTests.Start();
            Pass(sim, 6);
            Assert.That(sim.PendingEvents.OfType<EventRaised>(), Is.Empty);
            Assert.That(EventRules.On(sim), Is.False);
        }

        [Test]
        public void Grace_Days()
        {
            Simulation sim = Town(minor: "1");
            UntilSix(sim, 1);
            long started = EventRules.State(sim).StartedAt;
            Assert.That(started, Is.GreaterThan(0), "the first 6:00 sees the Township");
            sim.AdvanceTo(started + 2 * Day - 1);
            Assert.That(sim.PendingEvents.OfType<EventRaised>(), Is.Empty, "no event in the first 2 days");
            sim.AdvanceTo(started + 3 * Day);
            Assert.That(sim.PendingEvents.OfType<EventRaised>().Any(), Is.True, "then they come");
        }

        [Test]
        public void Roll_Deterministic_Rates()
        {
            Simulation a = Town(minor: "1"), b = Town(minor: "1");
            Pass(a, 30);
            Pass(b, 30);
            var ea = Describe(a.PendingEvents.Where(e => e is EventRaised || e is EventDefaulted));
            var eb = Describe(b.PendingEvents.Where(e => e is EventRaised || e is EventDefaulted));
            Assert.That(ea, Is.Not.Empty);
            Assert.That(ea, Is.EqualTo(eb), "the same seed and commands give the same events");
            Assert.That(EventRules.State(a).History.Count + EventRules.State(a).Waiting.Count, Is.GreaterThan(0));
        }

        // AC-2: a minor card waits 24 h and takes its default; a major 48 h; at most 2 wait and a third day's roll is skipped.
        [Test]
        public void Waiting_Defaults()
        {
            Simulation sim = Town(minor: "0", major: "0");
            UntilSix(sim);
            PendingEvent minor = Force(sim, "wedding", sim.State.Citizens.Households[0].Id);
            Assert.That(minor.ExpiresAt - minor.RaisedAt, Is.EqualTo(24 * Hour));
            PendingEvent major = Force(sim, "regional_grant");
            Assert.That(major.ExpiresAt - major.RaisedAt, Is.EqualTo(48 * Hour));
            Assert.That(EventRules.State(sim).Waiting.Count, Is.EqualTo(2));

            sim.AdvanceTo(minor.RaisedAt + 23 * Hour);
            Assert.That(EventRules.State(sim).Waiting.Count, Is.EqualTo(2), "not yet");
            sim.DrainEvents();
            sim.AdvanceTo(minor.RaisedAt + 25 * Hour);
            EventDefaulted d = sim.PendingEvents.OfType<EventDefaulted>().Single();
            Assert.That(d.Choice, Is.EqualTo(EventRules.Def(sim).Events[minor.Def].Default), "the default is the first choice");
            Assert.That(d.Cash, Is.EqualTo(0), "a default costs nothing");
            sim.AdvanceTo(major.RaisedAt + 49 * Hour);
            Assert.That(EventRules.State(sim).Waiting, Is.Empty);
        }

        [Test]
        public void Waiting_CapSkipsTheRoll()
        {
            Simulation sim = Town(minor: "1", major: "0");
            UntilSix(sim);
            Pass(sim, 2);
            Force(sim, "wedding", sim.State.Citizens.Households[0].Id);
            Force(sim, "new_family");
            int before = sim.PendingEvents.OfType<EventRaised>().Count();
            UntilSix(sim);
            Assert.That(sim.PendingEvents.OfType<EventRaised>().Count(), Is.EqualTo(before), "a third day's roll is skipped");
        }

        // AC-2 (answer): a choice's cost is paid and the card goes; a card that is gone is refused in words.
        [Test]
        public void Answer_Pays_AndCloses()
        {
            Simulation sim = Town(minor: "0", major: "0");
            UntilSix(sim);
            long hh = sim.State.Citizens.Households[0].Id;
            PendingEvent p = Force(sim, "wedding", hh);
            long cash = sim.State.Ledger.Cash;
            CommandResult r = sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, p.Id, 1));
            Assert.That(r.Accepted, Is.True);
            long cost = MarketRulesIndexed(sim, 500);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - cost));
            Assert.That(sim.State.Ledger.Balance(Account.EventCosts), Is.EqualTo(cost));
            Assert.That(EventRules.State(sim).Waiting, Is.Empty);
            Assert.That(EventRules.State(sim).History.Single().Cash, Is.EqualTo(-cost));
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, p.Id, 0)).Reason, Is.EqualTo(RejectReason.EventClosed));
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, 999, 0)).Reason, Is.EqualTo(RejectReason.EventClosed));
            PendingEvent q = Force(sim, "wedding", hh);
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, q.Id, 5)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.EventCosts, sim.State.Ledger.Cash - 100), new Posting(Account.Cash, -(sim.State.Ledger.Cash - 100)));
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, q.Id, 1)).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "a gift you can't pay for is refused");
            Assert.That(sim.Execute(new AnswerEvent(sim.State.Clock.SimTime, q.Id, 0)).Accepted, Is.True, "the safe choice is always there");
        }

        private static long MarketRulesIndexed(Simulation sim, long coins) { return Sim.Core.Market.MarketRules.Indexed(sim, coins * Coin); }

        // AC-3: away, a minor takes its default at once and is reported; no major rolls while you are away and the day is not saved up.
        [Test]
        public void AwayRules()
        {
            Simulation sim = Town(minor: "1", major: "1");
            UntilSix(sim);
            Pass(sim, 2);
            sim.DrainEvents();
            long now = sim.State.Clock.SimTime;
            sim.Execute(new AdvanceTo(now + 60 * Hour, AdvanceMode.Away));
            var report = sim.LastAwayReport;
            Assert.That(report.OfType<EventRaised>().All(e => !EventRules.Def(sim).Events[e.Def].Major), Is.True, "no major or disaster rolls while away");
            var defaulted = report.OfType<EventDefaulted>().Select(e => e.Id).ToList();
            foreach (EventRaised e in report.OfType<EventRaised>()) Assert.That(defaulted, Does.Contain(e.Id), "every minor took its default and is on the away card");
            Assert.That(EventRules.State(sim).Waiting.All(p => !report.OfType<EventRaised>().Any(e => e.Id == p.Id)), Is.True, "what waits now was raised on your return, not while away");
        }

        // ADR 0154: a day rolls once, at your first live hour from 6:00. Away over the 6:00 and back at 9:00, the minor took its default at 6:00 and the day's live roll
        // (majors) runs at 9:00; no second roll that day; and a player in the app at 6:00 gets it at 6:00 as before.
        [Test]
        public void RollsAtYourFirstLiveHour()
        {
            Simulation sim = Town(minor: "1", major: "1");
            UntilSix(sim);
            Pass(sim, 3); // live days: the grace is over
            EventsState s = EventRules.State(sim);
            s.Waiting.Clear();
            s.LastSeason.Clear(); s.MajorSeason = -1; s.CropQuietUntil = -1; // the mercy rules' marks from the three live days: start the season's day fresh
            sim.DrainEvents();
            long day = sim.State.Clock.SimTime / Day + 1;
            sim.Execute(new AdvanceTo(day * Day + 9 * Hour, AdvanceMode.Away));
            Assert.That(s.AwayRollDay, Is.EqualTo(day), "the 6:00 ran while you were away");
            Assert.That(s.RollDay, Is.EqualTo(day), "and your return at 9:00 ran the day's live roll");
            Assert.That(s.Waiting.Count, Is.GreaterThan(0), "a card waits for you");
            Assert.That(s.Waiting.All(p => p.RaisedAt == day * Day + 9 * Hour), Is.True, "raised on your return");
            s.Waiting.Clear();
            sim.DrainEvents();
            sim.AdvanceTo(day * Day + 14 * Hour);
            Assert.That(sim.PendingEvents.OfType<EventRaised>(), Is.Empty, "a day rolls once");
            sim.AdvanceTo((day + 1) * Day + 6 * Hour + 1);
            Assert.That(s.RollDay, Is.EqualTo(day + 1), "in the app at 6:00 the next day: rolled at 6:00 as before");
        }

        // AC-4: a kind doesn't repeat in a season; one major a season; the crop majors rest the next season; a major over 40% of cash isn't drawn.
        [Test]
        public void MercyRules()
        {
            Simulation sim = Town(minor: "1", major: "1");
            UntilSix(sim);
            Pass(sim, 2);
            sim.DrainEvents();
            // Run a season: each kind once at most, and at most one major.
            long start = sim.State.Clock.SimTime;
            sim.AdvanceTo((start / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason - 1);
            var raised = sim.PendingEvents.OfType<EventRaised>().ToList();
            Assert.That(raised.Select(e => e.Def).Distinct().Count(), Is.EqualTo(raised.Count), "a kind doesn't repeat within a season");
            Assert.That(raised.Count(e => e.Major), Is.LessThanOrEqualTo(1), "at most one major a season");
        }

        // AC-4 rule 4: a major whose dearest choice is over 40% of your cash is not drawn (the review found the old check compared with cash plus a million coins and could not fail).
        // Run for a pauper (100 coins) and for a rich town: the rich one meets majors, so the check is known to run.
        [TestCase(100L, false)]
        [TestCase(-1L, true)]
        public void MercyRule_CashCap(long keepCoins, bool expectMajors)
        {
            Simulation sim = Town(minor: "0", major: "1");
            UntilSix(sim);
            Pass(sim, 3);
            if (keepCoins > 0)
            {
                long cash = sim.State.Ledger.Cash;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.EventCosts, cash - keepCoins * Coin), new Posting(Account.Cash, -(cash - keepCoins * Coin)));
            }
            sim.DrainEvents();
            int majors = 0;
            for (int day = 0; day < 60; day++)
            {
                long before = sim.State.Ledger.Cash;
                EventsState s = EventRules.State(sim);
                s.Waiting.Clear();
                UntilSix(sim);
                foreach (PendingEvent p in s.Waiting.ToList())
                {
                    EventDef ev = EventRules.Def(sim).Events[p.Def];
                    if (!ev.Major) continue;
                    majors++;
                    long dearest = 0;
                    for (int c = 0; c < ev.Choices.Count; c++) dearest = System.Math.Max(dearest, EventRules.ChoiceCost(sim, p.Def, c, p));
                    Assert.That(dearest, Is.LessThanOrEqualTo(Money.MulRate(before, Fixed.FromRatio(2, 5))), ev.Id + ": never dearer than 40% of the cash you had when it was drawn");
                }
            }
            if (expectMajors) Assert.That(majors, Is.GreaterThan(0), "a rich town meets majors, so the check ran");
        }

        // AC-4 rule 2: after a crop major (drought, hail, waterlogging) the next season has none.
        [Test]
        public void MercyRule_CropMajorsRest()
        {
            Simulation sim = Town(minor: "0", major: "1");
            UntilSix(sim);
            Pass(sim, 3);
            EventsState s = EventRules.State(sim);
            long season = (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason);
            s.Waiting.Clear();
            sim.DrainEvents();
            EventRules.Raise(sim, s, EventRules.Def(sim).IndexOf("drought"), 0, 0, 0, sim.State.Clock.SimTime, true);
            Assert.That(s.CropQuietUntil, Is.EqualTo(season + 1), "the next season is quiet for the crop majors");
            sim.DrainEvents();
            long end = (season + 2) * sim.Clock.SecondsPerSeason - 1;
            while (sim.State.Clock.SimTime < end)
            {
                s.Waiting.Clear();
                Pass(sim, 1);
                foreach (EventRaised e in sim.PendingEvents.OfType<EventRaised>())
                    Assert.That(EventRules.IsCropMajor(EventRules.Def(sim).Events[e.Def]), Is.False, "no drought, hail or waterlogging this season or the next");
                sim.DrainEvents();
            }
        }

        // AC-4 rule 3: no event on the day a promise was judged (kept or broken at the close); the day after, they come again.
        [Test]
        public void MercyRule_PromiseDay()
        {
            Simulation sim = Town(minor: "1", major: "0");
            UntilSix(sim);
            Pass(sim, 3);
            EventsState s = EventRules.State(sim);
            s.Waiting.Clear();
            sim.DrainEvents();
            TownLifeRules.State(sim).Petitions.Add(new TownPetition { Id = 99, Topic = 0, Status = PetitionStatus.Broken, ClosedAt = (sim.State.Clock.SimTime / Day + 1) * Day });
            UntilSix(sim);
            Assert.That(sim.PendingEvents.OfType<EventRaised>(), Is.Empty, "nothing on the day the promise was judged");
            s.Waiting.Clear();
            sim.DrainEvents();
            for (int d = 0; d < 3; d++) { UntilSix(sim); s.Waiting.Clear(); } // live days, one at a time (a long jump is "away", which has its own rules)
            Assert.That(sim.PendingEvents.OfType<EventRaised>().Any(), Is.True, "and the days after, they come again");
        }

        // The events file is checked at load: a misspelt word or a zero divisor is refused with its path, not silently ignored (a review found a typo became "no flag").
        [Test]
        public void Content_Refuses_Typos()
        {
            var typo = Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) => f == "events.json" ? j.Replace("\"name\": \"rushStocked\"", "\"name\": \"rushStokced\"") : j));
            Assert.That(typo.Message, Does.Contain("unknown name 'rushStokced'"));
            var filter = Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) => f == "events.json" ? j.Replace("\"filter\": \"rainfed\"", "\"filter\": \"rainfd\"") : j));
            Assert.That(filter.Message, Does.Contain("unknown filter"));
            var zero = Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) => f == "events.json" ? j.Replace("\"starsDivisor\": 5", "\"starsDivisor\": 0") : j));
            Assert.That(zero.Message, Does.Contain("starsDivisor"));
        }
    }
}
