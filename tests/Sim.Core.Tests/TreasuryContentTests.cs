using NUnit.Framework;
using Sim.Content;
using Sim.Core.Definitions;

namespace Sim.Core.Tests
{
    /// <summary>spec 021 §4: <c>treasury.json</c> loads, and the loader refuses what would break the treasury.</summary>
    public class TreasuryContentTests
    {
        private static OnboardingDefs Onboarding() { return TestContent.Game().Onboarding; }

        private static TreasuryDef Load(string edit = null, string find = null)
        {
            string json = TestContent.ReadContent("treasury.json");
            if (find != null) { Assert.That(json, Does.Contain(find)); json = json.Replace(find, edit); }
            return ContentLoader.LoadTreasury(json, Onboarding());
        }

        [Test]
        public void Loads()
        {
            TreasuryDef d = Load();
            Assert.That(d.Departments.Count, Is.EqualTo(7));
            Assert.That(d.Departments[0].Id, Is.EqualTo("office"));
            Assert.That(d.Departments[6].Id, Is.EqualTo("policies"));
            Assert.That(d.Lines.Count, Is.EqualTo(10 + 5), "the clerk, the doctor, the clinic, the school and the road, the watchman and caretaker, the three civic upkeeps, and one line for each policy (spec 022 §3.5, spec 023 §3.8)");
            Assert.That((d.Policies.MaxActive, d.Policies.ReenactAfterSeasons, d.Policies.Lag[(int)Sim.Core.Town.PolicyId.Subsidy]), Is.EqualTo((3, 1, 1)));
            Assert.That(d.StartUpGrant, Is.EqualTo(100_000_00L));
            Assert.That(d.ClerkWage, Is.EqualTo(30_000_00L));
            Assert.That(Onboarding().Eras[d.Era].Id, Is.EqualTo("township"));
        }

        [TestCase("\"era\": \"township\"", "\"era\": \"nowhere\"", "unknown era")]
        [TestCase("\"era\": \"township\"", "\"era\": \"homestead\"", "later era")]
        [TestCase("\"officeWeight\": 0.30", "\"officeWeight\": 0.40", "add up to 1")]
        [TestCase("\"department\": \"office\"", "\"department\": \"mayor\"", "unknown department")]
        [TestCase("\"amount\": \"clerkWage\"", "\"amount\": \"mayorWage\"", "unknown amount")]
        [TestCase("\"requires\": \"treasury\"", "\"requires\": \"never\"", "treasury, school, clinic, clinicOpen, road, civic or civicStaffed")]
        [TestCase("\"retainSeasons\": 8", "\"retainSeasons\": 2", "between 4 and 16")]
        [TestCase("\"conditionStep\": 0.5", "\"conditionStep\": 1.5", "between")]
        [TestCase("\"maxActive\": 3", "\"maxActive\": 9", "between")]
        [TestCase("\"supply\": 1.05", "\"supply\": 0.9", "between")]
        [TestCase("\"rentControl\": { \"lag\": 0, \"department\": \"office\"", "\"rentControl\": { \"lag\": 0, \"department\": \"mayor\"", "unknown department")]
        public void Refuses(string find, string replace, string message)
        {
            ContentException e = Assert.Throws<ContentException>(() => Load(replace, find));
            Assert.That(e.Message, Does.Contain(message));
        }
    }
}
