using NUnit.Framework;
using Sim.Content;
using Sim.Core.Definitions;

namespace Sim.View.Tests
{
    /// <summary>Spec 009 AC-1: onboarding.json and eras.json load and are validated.</summary>
    public class OnboardingTests
    {
        private static string Onboarding { get { return ViewKit.ReadContent("onboarding.json"); } }
        private static string Eras { get { return ViewKit.ReadContent("eras.json"); } }

        private static ContentException Error(string onboarding, string eras = null)
        {
            return Assert.Throws<ContentException>(() => ContentLoader.LoadOnboarding(onboarding, eras ?? Eras));
        }

        [Test]
        public void Content_Validation()
        {
            OnboardingDefs d = ContentLoader.LoadOnboarding(Onboarding, Eras);
            Assert.That(d.Steps.Count, Is.EqualTo(7));
            Assert.That(d.Steps[5].Goal.Goal, Is.EqualTo(GoalKind.CropSown));
            Assert.That(d.Steps[5].Goal.Number, Is.EqualTo(12));
            Assert.That(d.Chapters[d.IndexOfChapter("borrowing")].Triggers[1].AfterChapter, Is.EqualTo(d.IndexOfChapter("labour")));
            Assert.That(d.Eras[1].Id, Is.EqualTo("hamlet"));
            Assert.That(d.Eras[1].AcresHarvestedInSeason, Is.EqualTo(6));

            ContentException e = Error(Onboarding.Replace("\"kind\": \"bedGrowing\"", "\"kind\": \"bedSown\""));
            Assert.That(e.FieldPath, Is.EqualTo("onboarding.steps[1].goal.kind"));
            e = Error(Onboarding.Replace("\"kind\": \"traderFull\"", "\"kind\": \"traderBusy\""));
            Assert.That(e.FieldPath, Is.EqualTo("onboarding.chapters[10].triggers[1].kind"));
            e = Error(Onboarding.Replace("\"chapter\": \"household\"", "\"chapter\": \"house\""));
            Assert.That(e.FieldPath, Is.EqualTo("onboarding.steps[2].chapter"));
            e = Error(Onboarding.Replace("\"events\": [\"PestWarning\"]", "\"events\": [\"PestSeen\"]"));
            Assert.That(e.FieldPath, Is.EqualTo("onboarding.chapters[9].triggers[0].events[0]"));
            e = Error(Onboarding.Replace("\"kind\": \"cropSown\", \"minGrowthHours\": 12", "\"kind\": \"cropSown\", \"hours\": 12"));
            Assert.That(e.FieldPath, Is.EqualTo("onboarding.steps[5].goal.hours"));
            e = Error(Onboarding, Eras.Replace("{ \"id\": \"homestead\" }", "{ \"id\": \"homestead\", \"conditions\": {} }"));
            Assert.That(e.FieldPath, Is.EqualTo("eras.eras[0].conditions"));
            e = Error(Onboarding, Eras.Replace("\"meanCareAtLeast\": 0.9", "\"meanCareAtLeast\": 3"));
            Assert.That(e.FieldPath, Is.EqualTo("eras.eras[1].conditions.meanCareAtLeast"));

            // A chapter without its string, or a step without its nudge, names the field (checked against en.json).
            Sim.View.Text.StringTable t = Sim.View.Text.StringTable.Load(ViewKit.ReadContent("strings/en.json").Replace("\"letterTitle.pest\"", "\"letterTitle.pests\"").Replace("\"nudge.firstSale\"", "\"nudge.sale\""));
            Assert.That(Sim.View.Views.Letter.MissingStrings(d, t), Is.EqualTo(new[] { "onboarding.chapters[9]: letterTitle.pest", "onboarding.steps[4]: nudge.firstSale" }));
            Assert.That(Sim.View.Views.Letter.MissingStrings(d, ViewKit.Strings()), Is.Empty);
        }
    }
}
