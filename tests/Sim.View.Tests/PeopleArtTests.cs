using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The realistic people and portraits (US-276): every picture the game can ask for a person is there.</summary>
    public class PeopleArtTests
    {
        private static readonly string[] Kinds = { "founder", "hand_m", "hand_f", "labourer_m", "labourer_f", "town_m", "town_f", "boy", "girl", "elder_m", "elder_f" };
        private static readonly string[] Working = { "founder", "hand_m", "hand_f", "labourer_m", "labourer_f", "town_m", "town_f" };
        private static readonly string[] Advisors = { "advisor_farm", "advisor_bank", "advisor_people", "advisor_planner" };

        // AC-17: 11 kinds on the map, front and back, standing and walking, work frames for the seven who hoe, all at 8x; 15 portraits.
        [Test]
        public void EveryPersonPictureIsRendered()
        {
            Dictionary<string, double> ppu = CropArtTests.PpuByName();
            var map = new List<string>();
            foreach (string kind in Kinds)
                foreach (string facing in new[] { "front", "back" })
                {
                    string stem = "person_" + kind + "_" + facing;
                    map.Add(stem);
                    map.AddRange(Enumerable.Range(0, 4).Select(i => stem + "_walk" + i));
                    if (Working.Contains(kind)) map.Add(stem + "_work");
                }
            Assert.That(map.Count, Is.EqualTo(124));
            Assert.That(map.Where(n => !ppu.ContainsKey(n)), Is.Empty, "rendered");
            Assert.That(map.Where(n => ppu.ContainsKey(n) && ppu[n] < 1056), Is.Empty, "at 8x (1,056 px a unit): none left at the old scale");

            string[] portraits = Kinds.Concat(Advisors).Select(k => "portrait_" + k).ToArray();
            Assert.That(portraits.Length, Is.EqualTo(15));
            Assert.That(portraits.Where(n => !ppu.ContainsKey(n)), Is.Empty, "portraits");
        }

        // AC-18: every person picture the UI asks for is a portrait that is there. The names are read from the views' and the UI's own code,
        // so a new one without a portrait fails here.
        [Test]
        public void EveryUiPersonPictureIsAPortrait()
        {
            Dictionary<string, double> art = CropArtTests.PpuByName();
            string[] folders =
            {
                Path.Combine(ViewKit.RepoRoot, "sim", "Sim.View", "Views"),
                Path.Combine(ViewKit.RepoRoot, "unity", "FarmToCity", "Assets", "Game", "Presentation", "Ui"),
            };
            var asked = folders.SelectMany(f => Directory.GetFiles(f, "*.cs"))
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(person_[a-z_]*[a-z])\"").Select(m => m.Groups[1].Value))
                .Concat(new[] { "people", "person_founder" }).Concat(Kinds.Select(k => "person_" + k)).Distinct().ToList();
            Assert.That(asked, Does.Contain("person_labourer_f").And.Contain("person_boy"));
            foreach (string name in asked)
            {
                string shown = PersonPictures.InUi(name);
                Assert.That(shown, Does.StartWith("portrait_"), name);
                Assert.That(art.Keys, Does.Contain(shown), name);
            }
            Assert.That(PersonPictures.InUi("people"), Is.EqualTo("portrait_founder"));
            foreach (string other in new[] { "coin", "family_house", "portrait_advisor_bank", "icon_population", "population", null })
                Assert.That(PersonPictures.InUi(other), Is.EqualTo(other), "any other picture is unchanged");
        }
    }
}
