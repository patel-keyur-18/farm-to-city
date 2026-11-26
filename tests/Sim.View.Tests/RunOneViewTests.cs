using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 030 §3.1, §7.3 (US-266, Preview 22): one business run by you in the plan, the business card and the card at load.</summary>
    public class RunOneViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        private static ActionVm RunAction(SheetVm card) { return card.Lines.Select(l => l.Action).First(a => a != null && a.Id == "runYourself"); }

        // The plan's "Who runs it": with nothing run by you "You" can be chosen; with the tea stall run by you the row is a hire and Change is closed with the reason.
        [Test]
        public void PlanBlocked()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm open = BusinessPlanSheet.Build(ctx, 0, true);
            LineVm who = open.Lines.Single(l => l.Label == "Who runs it");
            Assert.That(who.Value, Is.EqualTo("You, off your farm hours"));
            Assert.That(who.Action.Enabled, Is.True);

            Business tea = Of(sim, "tea_stall");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, tea.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, tea.Id));
            SheetVm closed = BusinessPlanSheet.Build(ctx, 0, true);
            LineVm hired = closed.Lines.Single(l => l.Label == "Who runs it");
            Assert.That(hired.Value, Is.EqualTo("A hired miller"), "asked to run it yourself, the plan shows a hire");
            Assert.That(hired.Action.Enabled, Is.False);
            Assert.That(hired.Action.DisabledReason, Is.EqualTo("You already run the tea stall: your day has room for one. Hire someone there first."));
            Assert.That(closed.Lines.Any(l => l.Label == "Your hours there"), Is.False, "no hours of yours in a hired plan");
        }

        // The business card: Run it is there when you run nothing, closed with the reason when you run another, and the hint no longer says "run it yourself".
        [Test]
        public void CardBlocked()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = Of(sim, "tea_stall"), veg = Of(sim, "veg_stall");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, veg.Id));
            SheetVm free = BusinessSheet.Build(ctx, veg.Id);
            Assert.That(RunAction(free).Enabled, Is.True);
            Assert.That(free.Lines.Single(l => l.Label == "Who works here").Note, Does.Contain("or run it yourself"));

            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, tea.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, tea.Id));
            SheetVm blocked = BusinessSheet.Build(ctx, veg.Id);
            Assert.That(RunAction(blocked).Enabled, Is.False);
            Assert.That(RunAction(blocked).DisabledReason, Is.EqualTo("You already run the tea stall. Hire someone there first, then run this one."));
            Assert.That(blocked.Lines.Single(l => l.Label == "Who works here").Note, Is.EqualTo("Nobody works here: hire someone on the board."));
        }

        // AC-6: an older town running two gets the card: a row each with its profit and hours, a button each, nothing else; a button answers with the command.
        [Test]
        public void RunTwoCard()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = Of(sim, "tea_stall"), veg = Of(sim, "veg_stall");
            Assert.That(RunOne.MustChoose(sim), Is.False, "a town running nothing needs no card");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, tea.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, tea.Id));
            Assert.That(RunOne.MustChoose(sim), Is.False, "one run by you is the rule");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, veg.Id));
            veg.Worker = WorkerKind.Founder; // a town saved before the rule

            Assert.That(RunOne.MustChoose(sim), Is.True);
            CardVm card = RunOne.Card(ctx);
            Assert.That(card.Title, Is.EqualTo("You can run only one business"));
            Assert.That(card.Buttons, Is.Empty, "no close and no other button: the day has room for one");
            Assert.That(card.Choices.Select(c => c.Id), Is.EqualTo(new[] { "keep:" + tea.Id, "keep:" + veg.Id }));
            Assert.That(card.Choices.Select(c => c.Title), Is.EqualTo(new[] { "Tea stall", "Vegetable stall" }));
            Assert.That(card.Choices.All(c => c.Detail.StartsWith("Profit this season ") && c.Detail.Contains("\n") && c.Detail.EndsWith(" hours a day")), Is.True, card.Choices[0].Detail);
            Assert.That(card.Choices.All(c => c.ButtonLabel == "Keep running"), Is.True);

            Command answer = RunOne.Answer(sim, card.Choices[1].Id);
            Assert.That(answer, Is.TypeOf<KeepRunningYourself>());
            Assert.That(RunOne.Answer(sim, "ok"), Is.Null);
            Ok(sim, answer);
            Assert.That(RunOne.MustChoose(sim), Is.False);
            Assert.That(RunOne.Kept(ctx, veg.Id, new[] { tea.Id }), Is.EqualTo("You run the vegetable stall. The tea stall waits for a hire: use Hire on its card."));
        }
    }
}
