using System.IO;
using System.Linq;
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
            Assert.That(r.State.Citizens.Citizens.Select(c => c.SchoolYears), Is.All.EqualTo(0), "nobody has been to the village school");
            Assert.That(r.State.Citizens.Citizens.Any(c => c.Literate), Is.True, "literacy from spec 014's education, as saved (AC-10)");
            Assert.That(school.Class, Is.Empty);
            Assert.That(r.State.Shops.Newcomers, Is.Empty, "no newcomers yet (spec 017 §3.7)");
            Assert.That(r.State.Shops.Rivals.Select(x => x.LastShare), Is.All.EqualTo(Maths.Fixed.Zero), "the grocer's and tea stall's last share starts at the first close");

            Simulation sim = Simulation.FromState(config, r.State);
            sim.State.Era.Current = config.Game.School.UnlockEra;
            long gift = 1_000_000 * 100;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.gift", "test", new Finance.Posting(Finance.Account.Cash, gift), new Finance.Posting(Finance.Account.OpeningEquity, -gift));
            LoadResult old = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0, schema: 14), config, MinSchema);
            Assert.That(old.Error, Is.EqualTo(SaveError.None), old.Detail);
            Assert.That(old.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "before a school, schema 14 writes the same town");

            CommandResult built = sim.Execute(new BuildSchool(sim.State.Clock.SimTime));
            Assert.That(built.Accepted, Is.True, built.ToString());
            sim.AdvanceTo(sim.State.Citizens.School.ReadyAt);
            if (People.SchoolRules.CanStartClass(sim) == RejectReason.None) Assert.That(sim.Execute(new StartAdultClass(sim.State.Clock.SimTime)).Accepted, Is.True);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "the school and its class round-trip");
        }
    }
}
