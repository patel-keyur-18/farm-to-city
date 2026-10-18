using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class PestTests
    {
        private static Fixed F(string s) { return Fixed.Parse(s); }

        private static void AssertNear(Fixed actual, string expected) { FarmTest.AssertNear(actual, expected, 16); }

        /// <summary>Content where every crop sows in every season, with a chosen wheat risk and severe share.</summary>
        private static GameDefs Pests(string wheatRisk, string severeShare)
        {
            return FarmTest.AnySeason((f, j) => f == "farming.json"
                ? j.Replace("\"wheat\": 0.06", "\"wheat\": " + wheatRisk).Replace("\"severeShare\": 0.05", "\"severeShare\": " + severeShare) : j);
        }

        // Spec 003 AC-14: the risk product, the repeat cap, spraying, and the seeded outbreak rate.
        [Test]
        public void Risk_Formula_AndRate()
        {
            PestRiskDef p = TestContent.Game().Farm.Pests;
            int wheat = TestContent.Game().Farm.IndexOfCrop("wheat"), rice = TestContent.Game().Farm.IndexOfCrop("rice");
            AssertNear(PestRules.Risk(p, wheat, 0, Fixed.Zero, false), "0.06");
            AssertNear(PestRules.Risk(p, wheat, 1, Fixed.Zero, false), "0.09");  // wheat after wheat, in sun: 9%
            AssertNear(PestRules.Risk(p, wheat, 2, Fixed.Zero, false), "0.12");
            AssertNear(PestRules.Risk(p, wheat, 3, Fixed.Zero, false), "0.12");  // capped at 2 repeats
            AssertNear(PestRules.Risk(p, rice, 2, F("0.7"), false), "0.34");     // rice ×3 in a wet Monsoon
            AssertNear(PestRules.Risk(p, rice, 2, F("0.7"), true), "0.17");      // sprayed before the check: halved

            foreach (string r in new[] { "0.06", "0.34" })
            {
                int outbreaks = 0;
                for (long id = 1; id <= 10_000; id++)
                {
                    PestRules.Roll(p, 7, id, F(r), false, out Outbreak o);
                    if (o != Outbreak.None) outbreaks++;
                }
                Assert.That(outbreaks / 10_000.0, Is.EqualTo(double.Parse(r, System.Globalization.CultureInfo.InvariantCulture)).Within(0.01), "risk " + r);
            }
        }

        // Spec 003 §3.8 (ADR 0034): repeats count consecutive harvests; a fallow day or another crop resets; a clear doesn't.
        [Test]
        public void Repeats_CountHarvests()
        {
            Simulation sim = FarmTest.Farm();
            int Sow(long t, string crop)
            {
                Assert.That(sim.Execute(new Sow(t, 19, crop, SeedSource.Certified, "none")).Accepted, Is.True);
                return FarmTest.Crop(sim, 19).Repeats;
            }
            void Harvest(long t) { sim.AdvanceTo(t); Assert.That(sim.Execute(new Harvest(t, 19, Market.Overflow.SellRestToTrader)).Accepted, Is.True); }
            Assert.That(Sow(0, "radish"), Is.EqualTo(0));
            Harvest(2 * Hour);
            Assert.That(Sow(2 * Hour, "radish"), Is.EqualTo(1));
            Harvest(4 * Hour);
            Assert.That(Sow(4 * Hour, "radish"), Is.EqualTo(2));
            sim.Execute(new ClearField(5 * Hour, 19));
            Assert.That(Sow(5 * Hour, "radish"), Is.EqualTo(2), "a clear doesn't change the count");
            Harvest(7 * Hour);
            Assert.That(Sow(7 * Hour, "wheat"), Is.EqualTo(0), "another crop");
            Harvest(13 * Hour);
            Assert.That(Sow(13 * Hour, "wheat"), Is.EqualTo(1));
            Harvest(19 * Hour);
            sim.AdvanceTo(2 * Day); // vacant from 19:00 through the pass at 48 h: a fallow day
            Assert.That(Sow(2 * Day, "wheat"), Is.EqualTo(0));
        }

        // Spec 003 AC-15: a warning at the check; a curative spray cuts the damage by 70%; severe gives 0.
        [Test]
        public void Warning_AndCurativeSpray()
        {
            Fixed HealthAt(string severe, bool spray, out PestWarning warning)
            {
                Simulation sim = FarmTest.Farm(Pests("1", severe));
                Assert.That(sim.Execute(new Sow(0, 19, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
                sim.DrainEvents();
                sim.AdvanceTo(3 * Hour); // the check, at half of 6 h
                warning = sim.DrainEvents().OfType<PestWarning>().Single();
                Assert.That(warning.SimTime, Is.EqualTo(3 * Hour));
                Assert.That(warning.ReadyAt, Is.EqualTo(6 * Hour));
                Crop c = FarmTest.Crop(sim, 19);
                Fixed rolled = c.RolledHealth;
                long cash = sim.State.Ledger.Cash;
                if (spray)
                {
                    Assert.That(sim.Execute(new Spray(3 * Hour, 19)).Accepted, Is.True);
                    Assert.That(sim.Execute(new Spray(3 * Hour, 19)).Reason, Is.EqualTo(RejectReason.AlreadySprayed));
                    // 2,000 ml × 0.0625 = 125 ml at 525 a litre (Spring rush) = 65.63, to 5010.
                    Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(65_63));
                }
                sim.AdvanceTo(6 * Hour);
                Fixed health = FarmTest.Crop(sim, 19).Breakdown.CropHealth;
                if (spray) Assert.That(health, Is.EqualTo(Fixed.One - (Fixed.One - rolled) * F("0.3")));
                else Assert.That(health, Is.EqualTo(rolled));
                return health;
            }
            Fixed h = HealthAt("0", false, out PestWarning w);
            Assert.That(w.Severity, Is.EqualTo(Outbreak.Normal));
            Assert.That(h, Is.InRange(F("0.4"), F("0.8")));
            Assert.That(HealthAt("0", true, out _), Is.InRange(F("0.82"), F("0.94")));
            Assert.That(HealthAt("1", false, out w), Is.EqualTo(Fixed.Zero));
            Assert.That(w.Severity, Is.EqualTo(Outbreak.Severe));
            AssertNear(HealthAt("1", true, out _), "0.7");

            // With no outbreak there's nothing to spray after the check.
            Simulation clean = FarmTest.Farm(Pests("0", "0"));
            Assert.That(clean.Execute(new Sow(0, 19, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            clean.AdvanceTo(3 * Hour);
            Assert.That(clean.Execute(new Spray(3 * Hour, 19)).Reason, Is.EqualTo(RejectReason.NoPestThreat));
            clean.AdvanceTo(6 * Hour);
            Assert.That(clean.Execute(new Spray(6 * Hour, 19)).Reason, Is.EqualTo(RejectReason.NotGrowing));
            Assert.That(FarmTest.Crop(clean, 19).Breakdown.CropHealth, Is.EqualTo(Fixed.One));
        }

        // Spec 003 AC-16: a check during an away advance never applies a severe outbreak.
        [Test]
        public void NoSevereWhileAway()
        {
            Simulation sim = FarmTest.Farm(Pests("1", "1"));
            Assert.That(sim.Execute(new Sow(0, 19, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(6 * Hour, AdvanceMode.Away);
            Crop c = FarmTest.Crop(sim, 19);
            Assert.That(c.Outbreak, Is.EqualTo(Outbreak.Normal));
            Assert.That(c.Breakdown.CropHealth, Is.EqualTo(F("0.4")));
            Assert.That(sim.LastAwayReport.OfType<PestWarning>().Single().Severity, Is.EqualTo(Outbreak.Normal));
        }
    }
}
