using NUnit.Framework;

namespace Sim.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void SimCoreAssembly_Loads()
        {
            Assert.That(typeof(SimInfo).Assembly.GetName().Name, Is.EqualTo("Sim.Core"));
        }
    }
}
