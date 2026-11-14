using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // AC-23 (US-240, schema 18): a schema-17 town's tenants keep their present rent as a lease that starts at the migration; every
        // household settles then; it round-trips at 18; the old history becomes the replay base.
        [Test]
        public void Schema17_Leases()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
            Ok(sim, new BuildFamilyHouse(t, HomeTile));
            sim.AdvanceTo(t + 48 * Hour);
            Building home = sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b));
            Household x = AddFamily(sim, home.Id, 32, 30);
            HouseholdRules.SetBudget(sim, x);
            x.Rent = 6_000_00; // an old town's flat rent
            long id = x.Id;

            byte[] old = SaveCodec.Write(sim.State, sim.Config, Wall0, schema: 17);
            LoadResult r = SaveCodec.Read(old, sim.Config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Simulation loaded = Simulation.FromState(sim.Config, r.State);
            Household y = r.State.Citizens.Household(id);
            long season = LeaseRules.Season(loaded);
            Assert.That((y.LeaseRent, y.LeaseStart, y.SinceSeason), Is.EqualTo((6_000_00L, season, season)), "a lease at the present rent, from the migration");
            Assert.That(r.State.Citizens.Households.All(h => h.SinceSeason == season), Is.True, "every household settles at the migration");

            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, sim.Config, Wall0), sim.Config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips at schema 18");
            Assert.That(again.State.Citizens.Household(id).LeaseRent, Is.EqualTo(6_000_00L));
        }
    }
}
