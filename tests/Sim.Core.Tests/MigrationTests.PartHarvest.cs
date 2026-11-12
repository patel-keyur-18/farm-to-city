using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // ADR 0105 (schema 17): a schema-16 town opens with no crop part-harvested; a schema-16 write of a town with none is the same town.
        [Test]
        public void Schema16_PartHarvest()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(16)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Farm.Crops.All(c => c.TakenGrams == 0), Is.True);
            LoadResult old = SaveCodec.Read(SaveCodec.Write(r.State, config, TestSim.Wall0, schema: 16), config, MinSchema);
            Assert.That(old.Error, Is.EqualTo(SaveError.None), old.Detail);
            Assert.That(old.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "a schema-16 write is the same town");
        }
    }
}
