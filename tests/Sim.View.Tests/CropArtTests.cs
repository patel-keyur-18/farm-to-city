using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Sim.View.Tests
{
    /// <summary>The realistic crops (US-274, ADR 0185): every picture the game can ask for a crop or a garden bed is an 8x render.</summary>
    public class CropArtTests
    {
        private static readonly string[] Stages = { "g1", "g2", "g3", "ready" };

        private static Dictionary<string, double> PpuByName()
        {
            string path = Path.Combine(ViewKit.RepoRoot, "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender", "sprites.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            double manifest = doc.RootElement.GetProperty("pixelsPerUnit").GetDouble();
            return doc.RootElement.GetProperty("sprites").EnumerateArray()
                .ToDictionary(e => e.GetProperty("name").GetString(), e => e.TryGetProperty("ppu", out JsonElement p) ? p.GetDouble() : manifest);
        }

        private static IEnumerable<string> Ids(string list)
        {
            using JsonDocument doc = JsonDocument.Parse(ViewKit.ReadContent("crops.json"));
            return doc.RootElement.GetProperty(list).EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToArray();
        }

        // Every field crop and every kitchen-garden crop in content, at all four stages, plus the empty bed, the planting pit and the
        // neighbours' sugarcane. A new crop added to content without a render fails here.
        [Test]
        public void EveryCropStageAndBedIsRenderedAt8x()
        {
            Dictionary<string, double> ppu = PpuByName();
            var wanted = new List<string> { "bed_empty", "tree_pit", "crop_sugarcane_g2" };
            foreach (string id in Ids("crops")) wanted.AddRange(Stages.Select(s => "crop_" + id + "_" + s));
            wanted.AddRange(new[] { "crop_berseem_cut", "crop_sorghum_cut" });
            foreach (string id in Ids("gardenCrops")) wanted.AddRange(Stages.Select(s => "bed_" + id + "_" + s));
            Assert.That(wanted.Count, Is.GreaterThan(60));
            Assert.That(wanted.Where(n => !ppu.ContainsKey(n)), Is.Empty, "rendered");
            Assert.That(wanted.Where(n => ppu.ContainsKey(n) && ppu[n] < 1056), Is.Empty, "at 8x (1,056 px a unit)");
        }
    }
}
