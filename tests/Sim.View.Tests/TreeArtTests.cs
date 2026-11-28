using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Sim.View.Tests
{
    /// <summary>The realistic trees (US-275, ADR 0186): every picture the game can ask for a tree is an 8x render.</summary>
    public class TreeArtTests
    {
        private static readonly string[] Stages = { "sapling", "young", "mature", "ripe" };

        // Every fruit tree in content at all four stages (orchards repeat them), plus the trees nobody plants and the banyan's panel
        // picture. A new tree added to content without a render fails here.
        [Test]
        public void EveryTreePictureIsRenderedAt8x()
        {
            Dictionary<string, double> ppu = CropArtTests.PpuByName();
            var wanted = new List<string> { "tree_neem", "tree_babul", "village_tree", "village_banyan", "place_banyan", "village_peepal" };
            using (JsonDocument doc = JsonDocument.Parse(ViewKit.ReadContent("trees.json")))
                foreach (string id in doc.RootElement.GetProperty("trees").EnumerateArray().Select(e => e.GetProperty("id").GetString()))
                    wanted.AddRange(Stages.Select(s => "tree_" + id + "_" + s));
            Assert.That(wanted.Count, Is.GreaterThanOrEqualTo(26));
            Assert.That(wanted.Where(n => !ppu.ContainsKey(n)), Is.Empty, "rendered");
            Assert.That(wanted.Where(n => ppu.ContainsKey(n) && ppu[n] < 1056), Is.Empty, "at 8x (1,056 px a unit)");
        }
    }
}
