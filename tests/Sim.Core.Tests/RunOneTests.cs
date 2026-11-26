using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 030 §3.1 (US-266, ADR 0172): you can run only one business yourself; an older town that runs several keeps one.</summary>
    public class RunOneTests
    {
        private const long Coin = 100;

        /// <summary>A Village town with a staffed, open tea stall and veg stall on pitches 0 and 1.</summary>
        private static Simulation Town(out Business.Business tea, out Business.Business veg)
        {
            Simulation sim = Start();
            tea = ShopTests.Shop(sim, "tea_stall", 0);
            veg = ShopTests.Shop(sim, "veg_stall", 1, rich: false);
            return sim;
        }

        private static void YouRun(Simulation sim, Business.Business b)
        {
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, b.Id));
        }

        // AC-4, AC-5: with one business run by you, a second can't be founded or taken on by you, a hire for it is fine, and handing the first
        // to a hire frees you to run another.
        [Test]
        public void OnlyOneRunByYou()
        {
            Simulation sim = Town(out Business.Business tea, out Business.Business veg);
            Assert.That(BusinessRules.RunByYou(sim), Is.Empty, "a new town runs nothing yourself");

            YouRun(sim, tea);
            Assert.That(BusinessRules.RunByYou(sim), Is.EqualTo(new[] { tea }));

            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, veg.Id));
            Assert.That(sim.Execute(new RunBusinessYourself(sim.State.Clock.SimTime, veg.Id)).Reason, Is.EqualTo(RejectReason.AlreadyRunOne), "a second run by you");
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "sweet_shop", 0, 100_000 * Coin, true, null, 2)).Reason, Is.EqualTo(RejectReason.AlreadyRunOne), "founding a second to run yourself");
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "sweet_shop", 0, 100_000 * Coin, false, null, 2)).Accepted, Is.True, "a hire for it is fine");
            Assert.That(BusinessRules.RunByYou(sim), Is.EqualTo(new[] { tea }), "refusals changed nothing");

            BusinessTests.Staff(sim, tea); // a hire for the first: you stop running it
            Assert.That(BusinessRules.RunByYou(sim), Is.Empty);
            Assert.That(sim.Execute(new RunBusinessYourself(sim.State.Clock.SimTime, veg.Id)).Accepted, Is.True, "free to run the other");
            Assert.That(BusinessRules.RunByYou(sim), Is.EqualTo(new[] { veg }));
        }

        // A closed business you ran doesn't count.
        [Test]
        public void ClosedDoesNotCount()
        {
            Simulation sim = Town(out Business.Business tea, out Business.Business veg);
            YouRun(sim, tea);
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, tea.Id));
            Assert.That(BusinessRules.RunByYou(sim), Is.Empty);
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, veg.Id));
            Assert.That(sim.Execute(new RunBusinessYourself(sim.State.Clock.SimTime, veg.Id)).Accepted, Is.True);
        }

        // AC-6: a town saved running two keeps both until it answers; keeping one sends the others to nobody, which then make nothing until hired.
        [Test]
        public void KeepRunningYourself()
        {
            Simulation sim = Town(out Business.Business tea, out Business.Business veg);
            YouRun(sim, tea);
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, veg.Id));
            veg.Worker = WorkerKind.Founder; // a town saved before the rule: two run by you
            Assert.That(BusinessRules.RunByYou(sim), Is.EqualTo(new[] { tea, veg }));
            sim.DrainEvents();

            Assert.That(sim.Execute(new KeepRunningYourself(sim.State.Clock.SimTime, 9999)).Accepted, Is.False, "an unknown business");
            long vegCash = veg.Ledger.Cash;
            Ok(sim, new KeepRunningYourself(sim.State.Clock.SimTime, veg.Id));
            Assert.That(veg.Worker, Is.EqualTo(WorkerKind.Founder), "the one kept");
            Assert.That(tea.Worker, Is.EqualTo(WorkerKind.None), "the other waits for a hire");
            Assert.That(tea.Status, Is.EqualTo(BusinessStatus.Open), "it is not closed");
            Assert.That(veg.Ledger.Cash, Is.EqualTo(vegCash), "cash untouched");
            RunYourselfChosen e = sim.DrainEvents().OfType<RunYourselfChosen>().Single();
            Assert.That(e.KeptId, Is.EqualTo(veg.Id));
            Assert.That(e.StoppedIds, Is.EqualTo(new[] { tea.Id }));

            Assert.That(sim.Execute(new KeepRunningYourself(sim.State.Clock.SimTime, veg.Id)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "only when you run two or more");
            long served = tea.SeasonServed;
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86_400);
            Assert.That(tea.SeasonServed, Is.EqualTo(served), "with nobody to work it, it serves no one");
            BusinessTests.Staff(sim, tea);
            Assert.That(tea.Worker, Is.Not.EqualTo(WorkerKind.None), "a hire gets it going again");
        }

        // The command survives the save's command codec (the command log is part of a save).
        [Test]
        public void KeepRoundTrips()
        {
            var stream = new System.IO.MemoryStream();
            var writer = new SaveWriter(stream);
            CommandCodec.Write(writer, new KeepRunningYourself(1234, 77));
            writer.Flush();
            stream.Position = 0;
            var back = (KeepRunningYourself)CommandCodec.Read(new SaveReader(stream, 25, null, null));
            Assert.That(back.BusinessId, Is.EqualTo(77));
            Assert.That(back.At, Is.EqualTo(1234));
        }
    }
}
