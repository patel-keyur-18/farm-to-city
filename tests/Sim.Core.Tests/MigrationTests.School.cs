using System.IO;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // Spec 018 AC-10 (US-131): a schema-14 save loads into 15 with no school and nothing given to the village, builds one, and
        // round-trips; before one, a schema-14 write is the same town.
        [Test]
        public void Schema14_School()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(14)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            People.SchoolState school = r.State.Citizens.School;
            Assert.That((school.Status, school.Cost), Is.EqualTo((People.SchoolStatus.None, 0L)), "no school yet");
            Assert.That(r.State.Ledger.Balance(Finance.Account.GiftsToVillage), Is.EqualTo(0));

            Simulation sim = Simulation.FromState(config, r.State);
            sim.State.Era.Current = config.Game.School.UnlockEra;
            long gift = 1_000_000 * 100;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.gift", "test", new Finance.Posting(Finance.Account.Cash, gift), new Finance.Posting(Finance.Account.OpeningEquity, -gift));
            LoadResult old = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0, schema: 14), config, MinSchema);
            Assert.That(old.Error, Is.EqualTo(SaveError.None), old.Detail);
            Assert.That(old.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "before a school, schema 14 writes the same town");

            CommandResult built = sim.Execute(new BuildSchool(sim.State.Clock.SimTime));
            Assert.That(built.Accepted, Is.True, built.ToString());
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "the school round-trips");
        }
    }
}
