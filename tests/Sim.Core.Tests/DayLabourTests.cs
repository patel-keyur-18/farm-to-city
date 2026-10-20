using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class DayLabourTests
    {
        // Spec 006 AC-2.
        [Test]
        public void Pool_Seeded()
        {
            Simulation a = Start(seed: 7), b = Start(seed: 7), c = Start(seed: 8);
            Assert.That(P(a).Roster.Select(n => n.Name), Is.EqualTo(P(b).Roster.Select(n => n.Name)));
            Assert.That(P(a).Roster.Select(n => n.Name).Distinct().Count(), Is.EqualTo(8), "distinct names");
            Assert.That(P(a).Roster.Select(n => n.Name), Is.Not.EqualTo(P(c).Roster.Select(n => n.Name)));

            int[] expected = { 2, 4, 3, 2 };
            string firstDay = string.Join(",", P(a).Roster.Where(n => n.FreeToday).Select(n => n.Id));
            bool differs = false;
            for (long day = 0; day < 16; day++)
            {
                a.AdvanceTo(day * Day + 1);
                b.AdvanceTo(day * Day + 1);
                int season = (int)Calendar.SeasonAt(a.State.Clock.SimTime, a.Clock);
                Assert.That(P(a).Roster.Count(n => n.FreeToday), Is.EqualTo(expected[season]), "day " + day);
                string free = string.Join(",", P(a).Roster.Where(n => n.FreeToday).Select(n => n.Id));
                Assert.That(free, Is.EqualTo(string.Join(",", P(b).Roster.Where(n => n.FreeToday).Select(n => n.Id))));
                if (day > 0 && free != firstDay) differs = true;
            }
            Assert.That(differs, Is.True, "the pool changes from day to day");
        }

        // Spec 006 AC-3.
        [Test]
        public void Hire_CostAndCare()
        {
            Simulation sim = Start(localSeconds: 0);
            Ok(sim, new Sow(18 * Hour, HomeTile, "wheat", Farm.SeedSource.Certified, "none"));
            Fixed before = PeopleRules.CareSupply(sim);
            long cash = sim.State.Ledger.Cash, labour = sim.State.Ledger.Balance(Account.HiredLabour);
            Ok(sim, new HireDayLabourers(18 * Hour, 2));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(4_687_50));
            Assert.That(sim.State.Ledger.Balance(Account.HiredLabour) - labour, Is.EqualTo(4_687_50));
            Assert.That(PeopleRules.CareSupply(sim) - before, Is.EqualTo(Fixed.FromInt(16)));
            Assert.That(P(sim).Roster.Where(n => n.HiredToday).All(n => n.SecondsWorked == 6 * Hour), Is.True);

            // Time-weighted: the crop's Care sum gains the higher ratio only from 18:00.
            var crop = sim.State.Farm.Crops[0];
            Fixed sum18 = crop.CareSum, ratio = sim.State.Farm.CareRatio;
            sim.AdvanceTo(19 * Hour);
            Assert.That(crop.CareSum - sum18, Is.EqualTo(ratio.MulRatio(Hour, 1)));

            // Hiring more than are free is rejected and changes nothing.
            int free = PeopleRules.FreeNow(sim);
            ulong hash = sim.State.ComputeHash();
            Assert.That(sim.Execute(new HireDayLabourers(19 * Hour, free + 1)).Reason, Is.EqualTo(RejectReason.NoneAvailable));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash));

            // They go home at the daily pass.
            sim.AdvanceTo(Day);
            Assert.That(PeopleRules.DayLabourHours(sim), Is.EqualTo(Fixed.Zero));
            Assert.That(PeopleRules.CareSupply(sim), Is.EqualTo(Farm.FounderRules.FieldHours(sim)));
        }

        // Spec 006 AC-4 (ADR 0039 §3, ADR 0041 §7).
        [Test]
        public void Regular_AsksToMoveIn()
        {
            // Six 1-hour hires at 23:00 are six part days, not six days: nobody asks, even with a hut standing empty.
            Simulation part = Start(seed: 5);
            BuildHut(part);
            for (int d = 1; d <= 6; d++)
            {
                part.AdvanceTo(d * Day - Hour);
                Ok(part, new HireDayLabourers(part.State.Clock.SimTime, 1));
            }
            part.AdvanceTo(part.State.Clock.SimTime + 2 * Day);
            Assert.That(P(part).Applicants.Where(a => a.FromRosterId != 0), Is.Empty);
            Assert.That(P(part).Roster.Max(n => n.SecondsWorked), Is.LessThan(6 * Day));

            // Hire every free labourer at the start of each day: someone reaches six full days. No hut, no ask.
            Simulation sim = Start(seed: 5, game: Game((f, j) => f == "people.json" ? j.Replace("\"acceptChance\": 0.85", "\"acceptChance\": 0.0") : j));
            for (int d = 0; P(sim).Roster.Max(n => n.SecondsWorked) < 6 * Day; d++)
            {
                sim.AdvanceTo(d * Day + 1);
                int free = P(sim).Roster.Count(n => n.FreeToday);
                Ok(sim, new HireDayLabourers(sim.State.Clock.SimTime, free));
            }
            sim.AdvanceTo(sim.State.Clock.SimTime + Day);
            Assert.That(P(sim).Applicants, Is.Empty);
            Neighbour regular = P(sim).Roster.Where(n => n.SecondsWorked >= 6 * Day).OrderBy(n => n.Id).First();

            // Not while away: the ask waits for the player (ADR 0002).
            Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, QuartersTile));
            sim.DrainEvents();
            Ok(sim, new AdvanceTo(sim.State.Clock.SimTime + 2 * Day, AdvanceMode.Away));
            Assert.That(sim.DrainEvents().OfType<Events.RegularAsksToMoveIn>(), Is.Empty);

            // Live again: they ask within the hour, with skill and both traits known, the first their day-labour trait.
            sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Events.RegularAsksToMoveIn ask = sim.PendingEvents.OfType<Events.RegularAsksToMoveIn>().Single();
            Applicant a = P(sim).Applicant(ask.ApplicantId);
            Assert.That((ask.NeighbourId, a.FromRosterId, a.Name, a.Trait1), Is.EqualTo((regular.Id, regular.Id, regular.Name, regular.Trait)));
            Assert.That((a.SkillKnown, a.SecondTraitKnown), Is.EqualTo((true, true)));
            Assert.That(a.Trait2, Is.Not.EqualTo(a.Trait1));
            Assert.That(a.Skill, Is.InRange(sim.Config.Game.People.Hand.SkillMin, sim.Config.Game.People.Hand.SkillMax));

            // They always accept (walk-ins here never do: acceptChance 0); a new neighbour keeps the roster at 8.
            Ok(sim, new OfferContract(sim.State.Clock.SimTime, a.Id));
            Assert.That(P(sim).Hands.Single().Name, Is.EqualTo(regular.Name));
            Assert.That(P(sim).Roster.Count, Is.EqualTo(8));
            Assert.That(P(sim).Roster.Select(n => n.Id), Does.Not.Contain(regular.Id));
            Assert.That(P(sim).Roster.Select(n => n.Name).Append(regular.Name).Distinct().Count(), Is.EqualTo(9), "the newcomer has a name of their own");
            Assert.That(P(sim).Roster.Last().SecondsWorked, Is.EqualTo(0));
        }

        // ADR 0041 §7: a regular joins a full queue, and one not hired goes back to day labour and never asks again.
        [Test]
        public void Regular_FullQueue_AsksOnce()
        {
            // One full day makes a regular here, and applicants wait 5 days, so the walk-in queue stays full.
            Simulation sim = Start(seed: 9, game: Game((f, j) => f != "people.json" ? j
                : j.Replace("\"regularThresholdDays\": 6", "\"regularThresholdDays\": 1").Replace("\"applicantWaitDays\": 2", "\"applicantWaitDays\": 5")));
            BuildHut(sim);
            for (int i = 0; i < 24 * 8 && P(sim).Applicants.Count < sim.Config.Game.People.Hand.MaxApplicants; i++)
                sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Assert.That(P(sim).Applicants.Count, Is.EqualTo(3));
            sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day); // a hire at the daily pass is a full day
            Ok(sim, new HireDayLabourers(sim.State.Clock.SimTime, 1));
            sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Assert.That(P(sim).Applicants.Count(a => a.FromRosterId != 0), Is.EqualTo(1), "joins a full queue");
            Assert.That(P(sim).Applicants.Count, Is.EqualTo(4));

            sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Day);
            Assert.That(P(sim).Applicants.Where(a => a.FromRosterId != 0), Is.Empty, "left after the wait");
            Assert.That(P(sim).Roster.Count(n => n.AskedToMoveIn), Is.EqualTo(1), "back to day labour, asked once");
        }
    }
}
