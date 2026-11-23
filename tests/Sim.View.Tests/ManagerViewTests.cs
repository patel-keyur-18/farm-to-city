using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-124 (spec 028 §3.2, §7, Preview 16): the board's Manager tab and offer, the business's manager line and Settings section, the report.</summary>
    public class ManagerViewTests
    {
        private static Sim.Core.Business.Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // Preview 16, screen 3: a Manager tab from the Township; the post, and each applicant with stars and a style.
        [Test]
        public void Board_Manager_Tab()
        {
            Simulation sim = Situations.Managed();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(HiringBoard.Places(sim), Does.Contain(HiringRules.ManagerPlace));
            SheetVm vm = HiringBoard.Build(ctx, HiringRules.ManagerPlace);
            Assert.That(vm.Tabs.Any(t => t.Label == "Manager"), Is.True);
            Assert.That(vm.Lines[0].Label, Is.EqualTo("Manager"));
            Assert.That(vm.Lines[0].Value, Does.Contain("a tenth of the profit"));
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, HiringRules.ManagerPlace, false)).Accepted, Is.True);
            Situations.ManagerApplicant(sim);
            vm = HiringBoard.Build(ViewKit2.Context(sim), HiringRules.ManagerPlace);
            LineVm row = vm.Lines.First(l => l.WhyKey == "managerApplicant");
            Assert.That(row.Value, Does.Contain("cutter").Or.Contain("chaser").Or.Contain("first"), "the style shows");
            Assert.That(row.Action.Opens.Value.Kind, Is.EqualTo(ManagerSheet.OfferKind));
        }

        // Preview 16, screen 4: the offer names the business first (one with a manager is taken), then three terms.
        [Test]
        public void Offer_Names_The_Business()
        {
            Simulation sim = Situations.Managed();
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, HiringRules.ManagerPlace, false)).Accepted, Is.True);
            Applicant a = Situations.ManagerApplicant(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = ManagerSheet.Offer(ctx, a.Id, null);
            Assert.That(vm.Lines.Count(l => l.Badge == "Taken"), Is.EqualTo(1), "the bakery has one already");
            Assert.That(vm.Lines.Count(l => l.Badge == "Chosen"), Is.EqualTo(1), "the mill is chosen");
            Assert.That(vm.Subtitle, Does.Contain("flour mill"));
            Assert.That(vm.Lines.Any(l => l.Value != null && l.Value.Contains("A worker works there")), Is.True, "the mill's worker must go first: he works it himself");
            var offers = vm.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("offerManager")).ToList();
            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(offers.All(o => !o.Action.Enabled), Is.True, "so the offers wait");
            Assert.That(sim.Execute(new LetWorkerGo(sim.State.Clock.SimTime, Of(sim, "flour_mill").Id)).Accepted, Is.True);
            vm = ManagerSheet.Offer(ViewKit2.Context(sim), a.Id, null);
            offers = vm.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("offerManager")).ToList();
            Assert.That(sim.Execute(offers[0].Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.People.ManagerAt(Of(sim, "flour_mill").Id), Is.Not.Null);
        }

        // Preview 16, screen 5: Today has the Manager line with his style and pay; Settings has his section; what he sets has no button.
        [Test]
        public void Business_Shows_Him()
        {
            Simulation sim = Situations.Managed();
            ViewContext ctx = ViewKit2.Context(sim);
            Sim.Core.Business.Business bakery = Of(sim, "bakery");
            SheetVm today = BusinessSheet.Build(ctx, bakery.Id, BusinessSheet.Today);
            LineVm line = today.Lines.Single(l => l.Label == "Manager");
            Assert.That(line.Note, Does.Contain("and a tenth of the profit"));
            Assert.That(line.Action.Opens.Value.Kind, Is.EqualTo(ManagerSheet.ReportKind), "a report waits");
            SheetVm set = BusinessSheet.Build(ctx, bakery.Id, BusinessSheet.Settings);
            Assert.That(set.Lines.Any(l => l.Label == "Target" && l.Action != null && l.Action.Enabled), Is.True);
            Assert.That(set.Lines.Any(l => l.Action != null && l.Action.Id == "letGoManager"), Is.True);
            LineVm target = set.Lines.Single(l => l.Label == "Target");
            Assert.That(sim.Execute(target.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.People.ManagerAt(bakery.Id).Mgr.Target, Is.EqualTo(ManagerTarget.Growth));
            // The mill has none: its Settings say how to get one.
            SheetVm mill = BusinessSheet.Build(ctx, Of(sim, "flour_mill").Id, BusinessSheet.Settings);
            Assert.That(mill.Lines.Any(l => l.Action != null && l.Action.Id == "hireManager"), Is.True);
        }

        // Preview 16, screen 6: the report has the target against the result, what he changed and why, the days he mis-set and his pay, a Why? on each.
        [Test]
        public void Report_Sheet()
        {
            Simulation sim = Situations.Managed();
            ViewContext ctx = ViewKit2.Context(sim);
            Sim.Core.Business.Business bakery = Of(sim, "bakery");
            SheetVm vm = ManagerSheet.Report(ctx, bakery.Id);
            Assert.That(vm.Title, Does.EndWith("report"));
            Assert.That(vm.Lines.Any(l => l.Badge == "Met" || l.Badge == "Missed"), Is.True);
            Assert.That(vm.Lines.Any(l => l.WhyKey == "managerMiss"), Is.True);
            Assert.That(vm.Lines.Any(l => l.WhyKey == "managerWage"), Is.True);
            Assert.That(vm.Lines.Any(l => l.WhyKey == "managerStyle"), Is.True);
            Assert.That(ManagerSheet.Report(ctx, Of(sim, "flour_mill").Id).Title, Is.EqualTo("No report yet"));
        }
    }
}
