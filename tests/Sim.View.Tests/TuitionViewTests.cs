using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-244 (spec 028 §3.5, ADR 0161, Preview 18): the tuition centre's research, plan, evening and fee; a child's seasons on the citizen card; every number's Why?.</summary>
    public class TuitionViewTests
    {
        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
                if (l.WhyKey == "tuition" || l.WhyKey.StartsWith("plan") || l.WhyKey == "citizen.tuition") WhyTests.AssertWords(l.Label, why);
            }
        }

        private static int TypeOf(Simulation sim) { return sim.Config.Game.Businesses.IndexOfType("tuition"); }

        private static Business Centre(Simulation sim) { return sim.State.Businesses.Businesses.Single(b => BusinessRules.TypeOf(sim, b).IsTuition); }

        // The Businesses sheet has its picture button from the Township, with its cost; greyed before.
        [Test]
        public void Menu_Button()
        {
            Simulation sim = Situations.Township();
            ActionVm start = BusinessesSheet.Build(ViewKit2.Context(sim)).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(a => a.Id == "start:tuition");
            Assert.That(start.Label, Is.EqualTo("Start a tuition centre"));
            Assert.That(start.Detail, Does.StartWith("Classroom and benches "));
            Assert.That(start.Enabled, Is.True);
        }

        // Step 1: the market, every row with a Why?, and no mill or shop rows.
        [Test]
        public void Research()
        {
            Simulation sim = Situations.Owned();
            SheetVm vm = BusinessResearchSheet.Build(ViewKit2.Context(sim), TypeOf(sim));
            Assert.That(vm.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Children whose families would pay", "The fee a pupil pays a season", "Places", "A tutor's wage", "Classes run", "Classroom and benches" }));
            Assert.That(vm.Lines[0].Value, Does.EndWith("aged 7–17"));
            Assert.That(vm.Lines[2].Value, Does.StartWith("40 · 2 batches of 20"));
            AssertWhys(sim, vm);
        }

        // Step 2: the plan names fees and books, not grinding or fuel; run by you it has no wage.
        [Test]
        public void Plan()
        {
            Simulation sim = Situations.Owned();
            SheetVm hired = BusinessPlanSheet.Build(ViewKit2.Context(sim), TypeOf(sim), false), mine = BusinessPlanSheet.Build(ViewKit2.Context(sim), TypeOf(sim), true);
            Assert.That(hired.Lines.Select(l => l.Label), Does.Contain("A season of classes").And.Contain("Fees from pupils").And.Contain("Books and chalk"));
            Assert.That(hired.Lines.Select(l => l.Label), Does.Not.Contain("Grinding fees").And.Not.Contain("Wheat"));
            Assert.That(hired.Lines.Any(l => l.Label.StartsWith("The tutor") || l.Label.Contains("tutor")), Is.True, "a wage line");
            Assert.That(mine.Lines.Any(l => l.Label.Contains("tutor")), Is.False, "run by you there is none");
            Assert.That(hired.Lines.Select(l => l.Label), Does.Not.Contain("The Monsoon, nothing fired"), "that is the kiln's");
            AssertWhys(sim, hired);
            AssertWhys(sim, mine);
        }

        // Step 4: its evening: pupils (yours and the old village's), a line a batch, fees, stars with their take-up, the seasons its pupils have sat.
        [Test]
        public void Today()
        {
            Simulation sim = Situations.Owned();
            Business b = Centre(sim);
            SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(vm.Title, Is.EqualTo("Tuition centre"));
            Assert.That(vm.Tabs.Select(x => x.Label), Does.Not.Contain("Orders"), "it sells to families, not to businesses");
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Pupils this season").And.Contain("Batch 1, 15:00 to 17:00").And.Contain("Batch 2, 17:00 to 19:00").And.Contain("Fees taken this season").And.Contain("Stars").And.Contain("Seasons your pupils have sat"));
            LineVm pupils = vm.Lines.First(l => l.Label == "Pupils this season");
            Assert.That(pupils.Value, Is.EqualTo(TuitionRules.Pupils(b) + " of 40"));
            Assert.That(pupils.Note, Does.Contain(b.Pupils.Count + " of yours"));
            Assert.That(vm.Lines.First(l => l.Label == "Stars").Note, Does.Contain("Families send"));
            AssertWhys(sim, vm);
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Books).Lines.Count, Is.GreaterThan(0));
            AssertWhys(sim, BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Books));
        }

        // The fee setting cycles the presets by the command, says it takes effect at the next enrolment, and is not the hours of a shop.
        [Test]
        public void Fee_Setting()
        {
            Simulation sim = Situations.Owned();
            Business b = Centre(sim);
            SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings);
            LineVm fee = vm.Lines.First(l => l.Label == "Fee a pupil a season");
            Assert.That(fee.Value, Is.EqualTo("The usual fee"));
            Assert.That(fee.Note, Does.Contain("keep the fee they came at"));
            Assert.That(vm.Lines.Any(l => l.Label == "Open hours"), Is.False, "the evening is fixed");
            Assert.That(fee.Action.Create(sim.State.Clock.SimTime), Is.TypeOf<SetTuitionFee>());
            Assert.That(sim.Execute(fee.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings).Lines.First(l => l.Label == "Fee a pupil a season").Value, Is.EqualTo("10% cheaper"));
            AssertWhys(sim, vm);
        }

        // A child of yours who sat seasons shows it, with its Why?.
        [Test]
        public void Citizen_Card()
        {
            Simulation sim = Situations.Owned();
            Sim.Core.People.Citizen kid = sim.State.Citizens.Citizens.First(c => c.TuitionSeasons > 0);
            SheetVm vm = CitizenSheet.Build(ViewKit2.Context(sim), kid.Id);
            LineVm line = vm.Lines.Single(l => l.Label == "Tuition");
            Assert.That(line.Value, Is.EqualTo("3 seasons at a tuition centre"));
            AssertWhys(sim, vm);
        }
    }
}
