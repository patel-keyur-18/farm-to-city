using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.View.Text;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-2, AC-3, AC-13: the string tables load, cover every key, and say every rejection in a sentence.</summary>
    public class StringTests
    {
        // AC-2.
        [Test]
        public void Load_Validation()
        {
            Assert.That(StringTable.Load("{\"a.b\": \"Hello {name}\", \"c.one\": \"{n} thing\", \"c.other\": \"{n} things\"}").Count, Is.EqualTo(3));

            ContentException dup = Assert.Throws<ContentException>(() => StringTable.Load("{\"a\": \"x\", \"a\": \"y\"}"));
            Assert.That(dup.FieldPath, Is.EqualTo("a"));
            Assert.That(dup.Message, Does.Contain("duplicate"));

            Assert.That(Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": \"Hello {}\"}")).FieldPath, Is.EqualTo("k"));
            Assert.That(Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": \"Hello {na me}\"}")).FieldPath, Is.EqualTo("k"));
            Assert.That(Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": \"Hello {name\"}")).FieldPath, Is.EqualTo("k"));
            Assert.That(Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": \"Hello name}\"}")).FieldPath, Is.EqualTo("k"));
            Assert.That(Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": \"Hello {1st}\"}")).FieldPath, Is.EqualTo("k"));
            ContentException plural = Assert.Throws<ContentException>(() => StringTable.Load("{\"items.one\": \"1 item\"}"));
            Assert.That(plural.FieldPath, Is.EqualTo("items.one"));
            Assert.That(plural.Message, Does.Contain(".other"));
            Assert.Throws<ContentException>(() => StringTable.Load("{\"k\": 5}"));
            Assert.Throws<ContentException>(() => StringTable.Load("[1]"));
        }

        [Test]
        public void Get_FillsArguments_AndHandlesMissing()
        {
            StringTable t = StringTable.Load("{\"w\": \"{crop} can be sown in {seasons} only\", \"plain\": \"Hi\", \"n.one\": \"1 ready\", \"n.other\": \"{n} ready\"}");
            Assert.That(t.Get("w", "crop", "Wheat", "seasons", "Winter"), Is.EqualTo("Wheat can be sown in Winter only"));
            Assert.That(t.Get("plain"), Is.EqualTo("Hi"));
            Assert.That(t.Plural("n", 1), Is.EqualTo("1 ready"));
            Assert.That(t.Plural("n", 5), Is.EqualTo("5 ready"));
            Assert.That(t.Plural("n", 0), Is.EqualTo("0 ready"));

            t.ThrowOnMissing = false;
            Assert.That(t.Get("nope"), Is.EqualTo("⟦nope⟧"), "a visible gap for players");
            Assert.That(t.Get("w", "crop", "Wheat"), Is.EqualTo("Wheat can be sown in {seasons} only"));
            t.ThrowOnMissing = true;
            Assert.Throws<KeyNotFoundException>(() => t.Get("nope"));
            Assert.Throws<ArgumentException>(() => t.Get("w", "crop", "Wheat"));
        }

        private static IEnumerable<string> ContentKeys(GameDefs g, WeatherSet weather)
        {
            foreach (GoodDef x in g.Market.Goods) yield return x.NameKey;
            foreach (CropDef x in g.Farm.Crops) yield return x.NameKey;
            foreach (GardenCropDef x in g.Farm.GardenCrops) yield return x.NameKey;
            foreach (TreeDef x in g.Farm.Trees) yield return x.NameKey;
            foreach (DietPlanDef x in g.Farm.DietPlans) yield return x.NameKey;
            foreach (SoilDef x in g.Soils) yield return x.NameKey;
            foreach (BuildingDef x in g.Buildings) yield return x.NameKey;
            foreach (WeatherTypeDef x in weather.Types) yield return x.NameKey;
            foreach (LenderDef x in g.Credit.Lenders) yield return x.NameKey;
            foreach (WorkHoursDef x in g.Farm.WorkHours) yield return "hours." + x.Id;
            foreach (FertiliserDef x in g.Farm.Fertilisers) yield return "fertiliser." + x.Id;
        }

        private static IEnumerable<string> SourceKeys(string pattern)
        {
            string sim = Path.Combine(ViewKit.RepoRoot, "sim");
            var seen = new HashSet<string>();
            foreach (string file in Directory.GetFiles(sim, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(" + pattern + ")\"")) seen.Add(m.Groups[1].Value);
            }
            return seen.OrderBy(k => k, StringComparer.Ordinal);
        }

        // AC-3: every key the sim and Sim.View use exists in en.json.
        [Test]
        public void Coverage()
        {
            StringTable t = ViewKit.Strings();
            var missing = new List<string>();
            void Need(string key) { if (!t.Has(key)) missing.Add(key); }
            void NeedPlural(string key) { Need(key + ".one"); Need(key + ".other"); }

            Simulation sim = ViewKit.NewGame();
            WeatherSet weather = sim.Config.Weather;
            foreach (string k in ContentKeys(sim.Config.Game, weather)) Need(k);
            foreach (string k in SourceKeys(@"ledger\.[A-Za-z]+")) Need(k);
            foreach (string k in SourceKeys(@"score\.[A-Za-z]+")) Need(k);
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)).Cast<RejectReason>().Where(r => r != RejectReason.None)) Need("reject." + r);
            foreach (Type e in typeof(SimEvent).Assembly.GetTypes().Where(x => typeof(SimEvent).IsAssignableFrom(x) && !x.IsAbstract)) Need("event." + e.Name);
            foreach (string key in new[] { "report.cashFlow", "report.cashFlow.why", "report.netWorth", "report.netWorth.why", "report.homeUse.why" }) Need(key);
            foreach (string line in new[] { "farmIncome", "farmCosts", "farmProfit", "otherIncome", "household", "finance", "other", "netResult" })
            {
                Need("report." + line);
                Need("report." + line + ".why");
            }
            foreach (string s in new[] { "spring", "summer", "monsoon", "winter" }) Need("season." + s);
            NeedPlural("unit.coins");

            Assert.That(missing, Is.Empty, "missing from content/strings/en.json");
        }

        // AC-13: every rejection a Phase 1 command can return reads as a sentence (no braces left, a full stop at the end).
        [Test]
        public void RejectionSentences()
        {
            StringTable t = ViewKit.Strings(false);
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)).Cast<RejectReason>().Where(r => r != RejectReason.None))
            {
                string s = t.Get("reject." + r, "crop", "Wheat", "seasons", "Winter");
                Assert.That(s, Does.Not.Contain("⟦").And.Not.Contain("{"), r.ToString());
                Assert.That(s, Does.EndWith("."), r.ToString());
            }
            Assert.That(t.Get("reject.OutOfSeason", "crop", "Wheat", "seasons", "Winter"), Is.EqualTo("Wheat can be sown in Winter only."));
        }
    }
}
