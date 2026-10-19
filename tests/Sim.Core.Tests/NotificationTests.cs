using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 007 §3.5 (ADR 0041): notifications from a simulated absence (AC-10, AC-13).</summary>
    public class NotificationTests
    {
        private static NotifyDef Def() { return ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")).Notify; }

        // Wall0 (1,800,000,000) is 08:00 UTC; a device at UTC+5:30 (India) sees 13:30.
        private const int India = 330;

        private static AbsenceItem At(long wall, NotifyKind kind, string crop = null) { return new AbsenceItem(wall, kind, crop); }

        [Test]
        public void Plan_GroupQuietRank()
        {
            NotifyDef def = Def();
            long w = Wall0; // 13:30 in India
            var items = new List<AbsenceItem>
            {
                At(w + 600, NotifyKind.CropReady, "wheat"),
                At(w + 1200, NotifyKind.CropReady, "wheat"),       // within 30 minutes: grouped
                At(w + 3000, NotifyKind.CropReady, "potato"),      // 40 minutes after the group's first: a new one
                At(w + 600, NotifyKind.HandsWork, "rice"),
                At(w + 3 * 3600, NotifyKind.HandsWork, "rice"),    // within 4 hours: grouped with the first hands' note
                At(w + 5 * 3600, NotifyKind.HandsWork, "lentils"), // after 4 hours: a new note
                At(w + 10 * 3600, NotifyKind.ContractLapsed),      // 23:30 in India: quiet, moved to 07:00
                At(w + 11 * 3600, NotifyKind.BedReady, "garden_radish"), // 00:30: moved to 07:00 too
            };
            List<PlannedNotification> plan = NotificationPlanner.Group(items, def, India);
            string Show(PlannedNotification p) { return p.Kind + "×" + p.Count + (p.CropId == null ? "" : " " + p.CropId) + " @" + (p.WallUtc - w); }
            long seven = 7 * 3600 + 24 * 3600 - (13 * 3600 + 1800); // 07:00 the next day, seconds after 13:30
            Assert.That(plan.Select(Show), Is.EqualTo(new[]
            {
                "CropReady×2 wheat @600", "HandsWork×2 rice @600", "CropReady×1 potato @3000", "HandsWork×1 lentils @18000",
                "ContractLapsed×1 @" + seven, "BedReady×1 garden_radish @" + seven,
            }));
            foreach (PlannedNotification p in plan)
            {
                long minute = ((p.WallUtc + India * 60) % 86400) / 60;
                Assert.That(minute >= def.QuietToMinute && minute < def.QuietFromMinute, Is.True, "nothing in quiet hours: " + Show(p));
            }

            // Over the pending limit the most important come first, then the earliest.
            var many = Enumerable.Range(0, 120).Select(i => At(w + i * 3600, NotifyKind.CropReady, "wheat")).ToList();
            many.Add(At(w + 119 * 3600, NotifyKind.LoanMissed));
            List<PlannedNotification> capped = NotificationPlanner.Group(many, def, 0);
            Assert.That(capped.Count, Is.EqualTo(def.MaxPending));
            Assert.That(capped.Any(p => p.Kind == NotifyKind.LoanMissed), Is.True, "urgent is never dropped for a ready crop");
            Assert.That(capped.Select(p => p.WallUtc), Is.Ordered);
        }

        [Test]
        public void Plan_OffAndOrderedFields()
        {
            Simulation sim = PeopleKit.Start();
            sim.Execute(new Sow(sim.State.Clock.SimTime, PeopleKit.HomeTile, "radish", SeedSource.Certified, "none"));
            Assert.That(NotificationPlanner.Plan(sim, Def(), enabled: false, India), Is.Empty);
            Assert.That(NotificationPlanner.Plan(sim, Def(), enabled: true, India).Select(p => p.Kind), Does.Contain(NotifyKind.CropReady));

            // With a hand ordered to keep radish on that field, the hand harvests it: no "ready", a hands' note instead.
            Hand hand = PeopleKit.HireHand(sim);
            PeopleKit.Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, hand.Id,
                OrderSpec.KeepCrop(PeopleKit.HomeTile, "radish", SeedSource.Certified, "none", SellRule.Store)));
            List<AbsenceItem> items = NotificationPlanner.Absence(sim);
            Assert.That(items.Where(i => i.Kind == NotifyKind.CropReady), Is.Empty);
            Assert.That(items.Select(i => i.Kind), Does.Contain(NotifyKind.HandsWork));
        }

        [Test]
        public void Plan_MatchesRealAbsence()
        {
            SimConfig config = SaveTests.Config();
            int checkedTowns = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                Simulation sim = SaveTests.Town(seed, 2);
                if (sim.State.People.Hands.Count == 0) continue;
                checkedTowns++;
                ulong before = sim.State.ComputeHash();
                List<SimEvent> planned = NotificationPlanner.SimulateAbsence(sim);
                Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), "planning never touches the town");
                Assert.That(sim.PendingEvents, Is.Empty);

                long t0 = sim.State.Clock.SimTime, away = 30 * 3600 + 17;
                sim.Execute(new Sync(sim.State.Clock.LastWallUtc + away));
                List<string> real = sim.DrainEvents().Where(e => e.SimTime < t0 + away).Select(e => e.ToString()).ToList();
                Assert.That(planned.Where(e => e.SimTime < t0 + away).Select(e => e.ToString()), Is.EqualTo(real), "seed " + seed);
                Assert.That(real.Count, Is.GreaterThan(10));
            }
            Assert.That(checkedTowns, Is.GreaterThan(2));
        }
    }
}
