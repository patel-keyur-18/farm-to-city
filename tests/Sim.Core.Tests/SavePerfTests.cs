using System.Diagnostics;
using NUnit.Framework;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 007 AC-12: writing and reading the 10-year save.</summary>
    public partial class PerfTests
    {
        [Test, Category("Perf")]
        public void Save_WriteRead()
        {
            SimConfig config = SaveTests.Config();
            Simulation sim = SaveTests.TenYears(config);
            SaveCodec.Read(SaveCodec.Write(sim.State.Clone(), config, Wall0), config); // warm-up (JIT)
            var w = Stopwatch.StartNew();
            byte[] bytes = SaveCodec.Write(sim.State.Clone(), config, Wall0);
            double write = w.Elapsed.TotalMilliseconds;
            w.Restart();
            LoadResult r = SaveCodec.Read(bytes, config);
            double read = w.Elapsed.TotalMilliseconds;
            TestContext.Out.WriteLine("write " + write.ToString("0.0") + " ms, read " + read.ToString("0.0") + " ms, " + bytes.Length + " bytes");
            Assert.That(r.Error, Is.EqualTo(SaveError.None));
            Assert.That(write, Is.LessThan(50));
            Assert.That(read, Is.LessThan(50));
        }
    }
}
