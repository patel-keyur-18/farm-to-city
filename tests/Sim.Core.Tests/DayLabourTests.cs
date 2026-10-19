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
    }
}
