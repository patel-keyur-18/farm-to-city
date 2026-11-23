using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.History;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // Sprint 26 (spec 029 §3.1, AC-5): a schema 23 town loads with an empty Chronicle, "The Chronicle begins" when it is loaded into a simulation,
        // and only the firsts state can still date (the eras it has reached, the school, clinic and road), all dated at the load.
        [Test]
        public void Schema24_Chronicle()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Growth23Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Chronicle.Entries, Is.Empty, "an older save holds no Chronicle");
            Simulation sim = Simulation.FromState(config, r.State);
            long now = sim.State.Clock.SimTime;
            ChronicleState c = sim.State.Chronicle;
            Assert.That(c.Entries[0].Kind, Is.EqualTo(ChronicleKind.Begins), "it begins at the load");
            Assert.That(c.Entries.All(e => e.At == now), Is.True, "nothing is dated before the load");
            Assert.That(c.Entries.Select(e => e.Kind), Is.EquivalentTo(new[] { ChronicleKind.Begins, ChronicleKind.Hamlet, ChronicleKind.Village, ChronicleKind.Township }.Concat(
                new[] { ChronicleKind.SchoolOpened, ChronicleKind.ClinicBuilt, ChronicleKind.RoadBuilt }.Where(k => c.Entries.Any(e => e.Kind == k)))), "the eras it reached and what it built, no more");
            Assert.That(c.Entries.Count(e => e.Kind == ChronicleKind.Founding), Is.Zero, "the founding is not invented");
            int n = c.Entries.Count;
            sim.AdvanceTo(now + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(c.Entries.Count(e => e.Kind == ChronicleKind.Begins), Is.EqualTo(1));
            Assert.That(c.Entries.Count, Is.GreaterThanOrEqualTo(n), "it carries on");
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "round-trips");
        }
    }
}
