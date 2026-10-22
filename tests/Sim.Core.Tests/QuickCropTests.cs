using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class CropTests
    {
        private static string DayLong(string file, string json)
        {
            return file == "crops.json" ? json.Replace("\"growthMinutes\": 900", "\"growthMinutes\": 1440") : json;
        }

        // Spec 011 AC-12 (ADR 0055): rice and potato take 15 h; per sowing harvest and seed scale by cycleShare, a season's totals don't.
        [Test]
        public void RiceAndPotato_15h()
        {
            Simulation now = NewFarm(game: RichGame()), before = NewFarm(game: RichGame(DayLong));
            foreach (string id in new[] { "rice", "potato" })
            {
                CropDef c15 = now.Config.Game.Farm.Crops.Single(c => c.Id == id), c24 = before.Config.Game.Farm.Crops.Single(c => c.Id == id);
                Assert.That(c15.GrowthMinutes, Is.EqualTo(900), id);
                Assert.That(c24.GrowthMinutes, Is.EqualTo(1440), id);
                long season = now.Clock.SecondsPerSeason;
                // One acre's season: base harvest and seed per sowing × sowings a season, the same either way (to rounding).
                Fixed Total(Simulation sim, CropDef c, Fixed perSeason) { return Fixed.FromRatio(FarmRules.CycleGrams(sim.Clock, c, perSeason) * season, c.GrowthSeconds * 1000); }
                Assert.That((Total(now, c15, c15.SeasonYieldPerAcre) - Total(before, c24, c24.SeasonYieldPerAcre)).Raw, Is.InRange(-Fixed.One.Raw, Fixed.One.Raw), id + " harvest a season");
                Assert.That((Total(now, c15, c15.SeedPerAcrePerSeason) - Total(before, c24, c24.SeedPerAcrePerSeason)).Raw, Is.InRange(-Fixed.One.Raw, Fixed.One.Raw), id + " seed a season");
                Assert.That(FarmRules.CycleGrams(now.Clock, c15, c15.SeasonYieldPerAcre) * 1440, Is.EqualTo(FarmRules.CycleGrams(before.Clock, c24, c24.SeasonYieldPerAcre) * 900).Within(1440), id + " per sowing scales by 15/24");
            }
            Assert.That(FarmRules.CycleGrams(now.Clock, now.Config.Game.Farm.Crops.Single(c => c.Id == "potato"), Fixed.FromInt(7000)), Is.EqualTo(1_093_750), "potato: 1,094 kg a sowing");

            // A crop sown before the change keeps its ready time.
            Simulation old = NewFarm(game: RichGame(DayLong));
            Assert.That(old.Execute(new Sow(0, 84, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            long readyAt = old.State.Farm.CropOn(84).ReadyAt;
            Assert.That(readyAt, Is.EqualTo(24 * Hour));
            Simulation moved = Simulation.FromState(now.Config, old.State.Clone());
            Assert.That(moved.State.Farm.CropOn(84).ReadyAt, Is.EqualTo(readyAt));
            moved.AdvanceTo(15 * Hour);
            Assert.That(moved.State.Farm.CropOn(84).State, Is.EqualTo(CropState.Growing), "not ready at 15 h");
            moved.AdvanceTo(readyAt);
            Assert.That(moved.State.Farm.CropOn(84).State, Is.EqualTo(CropState.Ready));
        }
    }
}
