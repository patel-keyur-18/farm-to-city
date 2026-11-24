using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.People;
using Sim.View.Views;
using static Sim.View.Tests.Situations;

namespace Sim.View.Tests
{
    /// <summary>US-259 (spec 028 §3.9, §7, Preview 20): the Supervisor post, the Workplace section, the three cards, the away line and every Why?.</summary>
    public class WorkplaceViewTests
    {
        /// <summary>A Township whose farm has <paramref name="hands"/> hands (the first hired, the rest copied from it).</summary>
        private static Simulation Farm(int hands)
        {
            Simulation sim = Township();
            SetCash(sim, 9_000_000_00);
            Hand first = PeopleViewTests.Hire(sim);
            while (sim.State.People.Hands.Count > hands) sim.State.People.Hands.RemoveAt(sim.State.People.Hands.Count - 1);
            while (sim.State.People.Hands.Count < hands)
            {
                Hand h = first.Clone();
                h.Id = sim.State.People.NextHandId++;
                h.PrepaymentId = 0;
                sim.State.People.Hands.Add(h);
            }
            return sim;
        }

        private static HrEvent Card(Simulation sim, HrEventKind kind, long worker, long other = 0)
        {
            long now = sim.State.Clock.SimTime;
            var e = new HrEvent { Id = sim.State.People.NextHrEventId++, Kind = kind, Place = 0, WorkerId = worker, OtherId = other, At = now, ExpiresAt = now + 24 * 3600, Amount = 166_700, Cost = 200_000, Hours = 8 };
            sim.State.People.HrEvents.Add(e);
            return e;
        }

        private static void WhyWorks(ViewContext ctx, SheetVm vm)
        {
            foreach (LineVm l in vm.Lines.Where(x => x.WhyKey != null && (x.WhyKey == "supervisor" || x.WhyKey == "supervisorPays" || x.WhyKey == "accident" || x.WhyKey == "conflict" || x.WhyKey == "association")))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why.Rows, Is.Not.Empty, l.WhyKey);
                foreach (WhyRowVm r in why.Rows) Assert.That((r.Label + r.Value).Contains("{"), Is.False, l.WhyKey + ": " + r.Label);
            }
        }

        // Preview 20, screen 1: a Supervisor tab; with 2 hands the post is greyed with the reason; with 4 it shows what it pays and the applicants.
        [Test]
        public void Board_Supervisor_Tab()
        {
            Simulation sim = Farm(2);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(HiringBoard.Places(sim), Does.Contain(HiringRules.SupervisorPlace));
            SheetVm vm = HiringBoard.Build(ctx, HiringRules.SupervisorPlace);
            Assert.That(vm.Tabs.Any(t => t.Label == "Supervisor"), Is.True);
            Assert.That(vm.Lines[0].Label, Is.EqualTo("Supervisor"));
            Assert.That(vm.Lines[0].Note, Does.Contain("Needs 3 hands").And.Contain("you have 2"));
            ActionVm post = vm.Lines.First(l => l.Action != null && l.Action.Id == "postSupervisor").Action;
            Assert.That(post.Enabled, Is.False, "greyed");
            Assert.That(post.DisabledReason, Does.Contain("3 or more hands"));
            sim = Farm(4);
            ctx = ViewKit2.Context(sim);
            vm = HiringBoard.Build(ctx, HiringRules.SupervisorPlace);
            Assert.That(vm.Lines[0].Note, Does.Contain("Covers up to 5"));
            LineVm pays = vm.Lines.First(l => l.WhyKey == "supervisorPays");
            Assert.That(pays.Value, Does.StartWith("Not in coins"), "honest about 30,000");
            WhyWorks(ctx, vm);
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, HiringRules.SupervisorPlace, false)).Accepted, Is.True);
            for (int i = 0; i < 24 * 8 && sim.State.People.ApplicantsFor(HiringRules.SupervisorPlace).Count == 0; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            vm = HiringBoard.Build(ViewKit2.Context(sim), HiringRules.SupervisorPlace);
            LineVm row = vm.Lines.First(l => l.WhyKey == "applicant");
            Assert.That(row.Action.Opens.Value.Kind, Is.EqualTo("JobOffer"));
            SheetVm offer = HiringBoard.Offer(ViewKit2.Context(sim), row.WhyId);
            Assert.That(offer.Lines.All(l => l.Action == null || l.Action.Enabled), Is.True, "he can be offered work");
            Assert.That(offer.Subtitle, Does.Contain("supervisor"));
        }

        // Preview 20, screen 2: the Workplace section on the farm's People sheet and a business's, each line with a Why?.
        [Test]
        public void Workplace_Section()
        {
            Simulation sim = Farm(4);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = PeopleSheet.Build(ctx);
            int at = vm.Lines.FindIndex(l => l.IsHeader && l.Label == "Workplace");
            Assert.That(at, Is.GreaterThan(0));
            string text = string.Join(" | ", vm.Lines.Skip(at).Take(5).Select(l => l.Label + "=" + l.Value));
            Assert.That(text, Does.Contain("Supervisor=None: hire one from the board").And.Contain("Accidents=").And.Contain("Quarrels=").And.Contain("Association="), text);
            WhyWorks(ctx, vm);
            Hand h = sim.State.People.Hands[0];
            sim.State.People.Workplace.Supervisor = new Supervisor { Skill = Sim.Core.Maths.Fixed.FromInt(60), Wage = 30_000_00, HiredAt = sim.State.Clock.SimTime };
            vm = PeopleSheet.Build(ViewKit2.Context(sim));
            LineVm sv = vm.Lines.First(l => l.WhyKey == "supervisor");
            Assert.That(sv.Note, Does.Contain("Covers 4 of 4"));
            Assert.That(sv.Action.Confirm, Is.Not.Null, "letting him go asks first");
            Assert.That(vm.Lines.First(l => l.Label == "Quarrels").Value, Is.EqualTo("Settled for you"));
            WhyWorks(ViewKit2.Context(sim), vm);
            // a business's section says what each rule needs: one worker is too few for a supervisor or a quarrel
            Simulation b = Situations.Managed();
            Sim.Core.Business.Business open = b.State.Businesses.Businesses.First(x => x.Status == BusinessStatus.Open);
            SheetVm lines = WorkplaceSheet.AddSectionLines(ViewKit2.Context(b), open.Id);
            Assert.That(lines.Lines.First(l => l.WhyKey == "supervisor").Value, Does.StartWith("Needs 3 workers"));
            Assert.That(lines.Lines.First(l => l.WhyKey == "conflict").Value, Does.StartWith("Needs 2 workers"));
            WhyWorks(ViewKit2.Context(b), lines);
        }

        // Preview 20, screens 3 to 5: the three cards, their answers (two or three) and a default that is named.
        [Test]
        public void Cards()
        {
            Simulation sim = Farm(4);
            var hands = sim.State.People.Hands;
            ViewContext ctx = ViewKit2.Context(sim);
            HrEvent acc = Card(sim, HrEventKind.Accident, hands[0].Id);
            SheetVm vm = HrEventSheet.Build(ctx, acc.Id);
            Assert.That(vm.Actions.Count, Is.EqualTo(2));
            Assert.That(vm.Subtitle, Does.Contain("left to answer"));
            Assert.That(vm.Lines[0].Label, Does.Contain("out for 8 h").And.Not.Contain("{"));
            Assert.That(vm.Title, Does.Contain("hurt"));
            HrEvent con = Card(sim, HrEventKind.Conflict, hands[0].Id, hands[1].Id);
            vm = HrEventSheet.Build(ctx, con.Id);
            Assert.That(vm.Actions.Select(a => a.Label), Is.EqualTo(new[] { "Talk to both", "Move one apart", "Let it be" }));
            Assert.That(vm.Actions.All(a => a.Enabled), Is.True);
            Assert.That(vm.Title, Does.Contain("are not talking"));
            HrEvent ass = Card(sim, HrEventKind.Association, hands[0].Id);
            vm = HrEventSheet.Build(ctx, ass.Id);
            Assert.That(vm.Actions.Select(a => a.Label), Is.EqualTo(new[] { "Accept", "Negotiate", "Refuse" }));
            Assert.That(vm.Actions[0].Detail, Does.Contain("now"), "accepting says what it costs now");
            Assert.That(vm.Lines.Any(l => l.Label == "If you don't answer" && l.Value == "Refuse"), Is.True, "the default is named");
            foreach (HrEvent e in new[] { acc, con, ass })
            {
                CardVm card = HrEventSheet.Card(ctx, e.Id);
                Assert.That(card.Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.Decide, SettingsIds.NotNow }), "a card with 3 answers opens the sheet");
                Assert.That(card.Text, Does.Contain("left to answer").And.Not.Contain("{"));
            }
            // answering from the sheet works and the card is gone
            ActionVm talk = HrEventSheet.Build(ctx, con.Id).Actions[0];
            Assert.That(sim.Execute(talk.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(HrEventSheet.Card(ViewKit2.Context(sim), con.Id), Is.Null);
            // the older kinds keep their two buttons
            HrEvent sick = new HrEvent { Id = sim.State.People.NextHrEventId++, Kind = HrEventKind.SickLeave, WorkerId = hands[2].Id, Amount = 100_00 };
            sim.State.People.HrEvents.Add(sick);
            Assert.That(HrEventSheet.Card(ViewKit2.Context(sim), sick.Id).Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.Yes, SettingsIds.NotNow }));
        }

        // Preview 20, screen 6: a card nobody answered says so on the away card.
        [Test]
        public void Away_Line()
        {
            Simulation sim = Farm(4);
            long now = sim.State.Clock.SimTime;
            HrEvent con = Card(sim, HrEventKind.Conflict, sim.State.People.Hands[0].Id, sim.State.People.Hands[1].Id);
            sim.AdvanceTo(now + 26 * 3600);
            Assert.That(sim.State.People.HrEvents.Any(e => e.Id == con.Id), Is.False);
            var d = new HrEventDefaulted(now, con.Id, HrEventKind.Conflict, 2, 0, sim.State.People.Hands[0].Id);
            Assert.That(WorkplaceSheet.Defaulted(ViewKit2.Context(sim), d), Does.Contain("let be").And.Not.Contain("{"));
            d = new HrEventDefaulted(now, 1, HrEventKind.Association, 2, 0, sim.State.People.Hands[0].Id);
            Assert.That(WorkplaceSheet.Defaulted(ViewKit2.Context(sim), d), Does.Contain("refused"));
            d = new HrEventDefaulted(now, 1, HrEventKind.Accident, 0, 0, sim.State.People.Hands[0].Id);
            Assert.That(WorkplaceSheet.Defaulted(ViewKit2.Context(sim), d), Does.Contain("hurt"));
        }

        // A talk or a negotiation says what it came to, on a card with one button.
        [Test]
        public void Outcome_Card()
        {
            Simulation sim = Farm(4);
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (HrEventKind kind in new[] { HrEventKind.Conflict, HrEventKind.Association })
                foreach (bool yes in new[] { true, false })
                {
                    CardVm card = HrEventSheet.Outcome(ctx, new HrOutcome(sim.State.Clock.SimTime, kind, kind == HrEventKind.Conflict ? 0 : 1, yes));
                    Assert.That(card.Title + card.Text, Does.Not.Contain("{").And.Not.Contain("hr.outcome"));
                    Assert.That(card.Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.Ok }));
                }
            HrEvent con = Card(sim, HrEventKind.Conflict, sim.State.People.Hands[0].Id, sim.State.People.Hands[1].Id);
            Assert.That(sim.Execute(new AnswerHrEvent(sim.State.Clock.SimTime, con.Id, 0)).Accepted, Is.True);
            Assert.That(sim.PendingEvents.OfType<HrOutcome>().Any(o => o.Kind == HrEventKind.Conflict), Is.True, "talking says how it went");
        }
    }
}
