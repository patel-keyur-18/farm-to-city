using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Sim.View.Tests
{
    /// <summary>The realistic animals, vehicles and machines (US-277, spec 032 §3.9).</summary>
    public class AnimalArtTests
    {
        // AC-20: the cattle in three poses, the neighbours' tied cow and buffalo, the machines and the two road vehicles, all at 8x.
        [Test]
        public void EveryAnimalAndVehiclePictureIsRendered()
        {
            Dictionary<string, double> ppu = CropArtTests.PpuByName();
            var wanted = new List<string> { "village_cow", "village_buffalo", "plough_team", "tempo", "tractor", "tractor_cultivator", "tractor_trolley",
                "truck_front", "truck_back", "van_front", "van_back" };
            foreach (string kind in new[] { "buffalo", "cow", "bullock" })
                wanted.AddRange(new[] { "stand", "eat", "lie" }.Select(pose => "animal_" + kind + "_" + pose));
            Assert.That(wanted.Count, Is.EqualTo(20));
            Assert.That(wanted.Where(n => !ppu.ContainsKey(n)), Is.Empty, "rendered");
            Assert.That(wanted.Where(n => ppu.ContainsKey(n) && ppu[n] < 1056), Is.Empty, "at 8x (1,056 px a unit)");
        }
    }
}
