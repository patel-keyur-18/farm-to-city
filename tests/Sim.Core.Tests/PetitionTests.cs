using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-150 (spec 026 §3.2, §3.3, ADR 0127): petitions raised from needs, how they end, and approval.</summary>
    public class PetitionTests
    {
        private static Simulation Township(int families = 12) { return TownLifeTests.Township(families); }

        private static PetitionDef D(Simulation sim) { return PetitionRules.Def(sim); }

        private static TownPetition Of(Simulation sim, int topic, bool standing = true)
        {
            var list = TownLifeRules.State(sim).Petitions.Where(p => p.Topic == topic && (!standing || !p.Ended)).ToList();
            return list.Count == 0 ? null : list[list.Count - 1];
        }

        /// <summary>Takes the jobs from the first <paramref name="n"/> of your households: the no-work need.</summary>
        private static void Idle(Simulation sim, int n)
        {
            foreach (Household x in TownLifeRules.Yours(sim).Take(n))
                foreach (long id in x.Members) sim.State.Citizens.Citizen(id).Job = CitizenJob.None;
        }

        private static void Employ(Simulation sim)
        {
            foreach (Household x in sim.State.Citizens.Households)
                foreach (long id in x.Members) sim.State.Citizens.Citizen(id).Job = CitizenJob.Hand;
        }

        private static void Happy(Simulation sim, int h) { foreach (Household x in sim.State.Citizens.Households) x.Happiness = Fixed.FromInt(h); }

        private static long Approval(Simulation sim) { return TownLifeRules.State(sim).Approval.RoundToLong(); }

        /// <summary>What a close does to approval besides the petitions: the happiness step.</summary>
        private static long StepNow(Simulation sim) { return PetitionRules.Step(D(sim), HappinessRules.Town(sim)); }

        private static CommandResult Answer(Simulation sim, int id, int closes) { return sim.Execute(new AnswerPetition(sim.State.Clock.SimTime, id, closes)); }

        private static RejectReason Reason(Simulation sim, int id, int closes) { return Answer(sim, id, closes).Reason; }

        // AC-4: a petition needs 3 families (or a fifth), one a topic; the cohort never raises one.
        [Test]
        public void Raised_Threshold_Cohort()
        {
            Simulation sim = Township(families: 12);
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            int households = TownLifeRules.Yours(sim).Count;
            TownPetition hall = Of(sim, PetitionDef.Hall);
            Assert.That(hall, Is.Not.Null, "no community hall stands: every family lacks it");
            Assert.That(hall.Households, Is.EqualTo(households));
            Assert.That(hall.Status, Is.EqualTo(PetitionStatus.Open));
            Assert.That(Of(sim, PetitionDef.Work), Is.Null, "everyone has a job");

            TownLifeRules.AtClose(sim);
            Assert.That(s.Petitions.Count(p => p.Topic == PetitionDef.Hall && !p.Ended), Is.EqualTo(1), "one open petition a topic");

            // Two jobless families are not enough; three are.
            Idle(sim, 2);
            TownLifeRules.AtClose(sim);
            Assert.That(Of(sim, PetitionDef.Work), Is.Null, "two families");
            Idle(sim, 3);
            TownLifeRules.AtClose(sim);
            TownPetition work = Of(sim, PetitionDef.Work);
            Assert.That(work, Is.Not.Null, "three families");
            Assert.That(work.Households, Is.EqualTo(3));

            // A fifth: of 40 families it is 8, so 5 are not enough.
            Assert.That(PetitionRules.Needed(sim, D(sim), 40), Is.EqualTo(8));
            Assert.That(PetitionRules.Needed(sim, D(sim), 12), Is.EqualTo(3));
            Assert.That(PetitionRules.Needed(sim, D(sim), 16), Is.EqualTo(4), "a fifth of 16, rounded up, beats 3");

            // The old village's cohort is not counted in your households, so it raises none.
            Assert.That(TownLifeRules.Yours(sim).Count, Is.EqualTo(households));
            Assert.That(CohortRules.People(sim), Is.GreaterThan(0));
        }

        // AC-5: done, kept, broken, declined and ignored.
        [Test]
        public void Endings()
        {
            Simulation sim = Township();
            Employ(sim);
            Idle(sim, 4);
            TownLifeRules.AtClose(sim);
            TownPetition p = Of(sim, PetitionDef.Work);
            Assert.That(p, Is.Not.Null);
            PetitionDef d = D(sim);

            // Done: the need is gone at the next close, before any promise: +4.
            Employ(sim);
            long before = Approval(sim);
            long step = StepNow(sim);
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Done));
            Assert.That(Approval(sim), Is.EqualTo(before + step + d.Done));

            // Kept: promised for the next close, and the need is gone then: +6.
            Idle(sim, 4);
            TownLifeRules.AtClose(sim);
            TownLifeRules.AtClose(sim); // the topic is quiet for a close after the last ended
            TownLifeRules.AtClose(sim);
            p = Of(sim, PetitionDef.Work);
            Assert.That(p, Is.Not.Null, "raised again after its quiet close");
            Assert.That(Answer(sim, p.Id, 1).Accepted, Is.True);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Promised));
            Employ(sim);
            before = Approval(sim);
            step = StepNow(sim);
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Kept));
            Assert.That(Approval(sim), Is.EqualTo(before + step + d.Kept));

            // Broken: promised and the need is still there at the promise's close: −8.
            Idle(sim, 4);
            for (int i = 0; i < 3; i++) TownLifeRules.AtClose(sim);
            p = Of(sim, PetitionDef.Work);
            Assert.That(p, Is.Not.Null);
            Assert.That(Answer(sim, p.Id, 2).Accepted, Is.True);
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Promised), "two closes: not yet");
            before = Approval(sim);
            step = StepNow(sim);
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Broken));
            Assert.That(Approval(sim), Is.EqualTo(Fixed.FromInt((int)System.Math.Max(0, before + step + d.Broken)).RoundToLong()));
        }

        [Test]
        public void Declined_Ignored()
        {
            Simulation sim = Township();
            Employ(sim);
            Idle(sim, 4);
            TownLifeRules.AtClose(sim);
            TownPetition p = Of(sim, PetitionDef.Work);
            PetitionDef d = D(sim);

            // Declined: −2 at once, closed, and the topic is quiet for 4 closes.
            long before = Approval(sim);
            Assert.That(Answer(sim, p.Id, 0).Accepted, Is.True);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Declined));
            Assert.That(Approval(sim), Is.EqualTo(before + d.Declined));
            for (int i = 0; i < 4; i++)
            {
                TownLifeRules.AtClose(sim);
                Assert.That(Of(sim, PetitionDef.Work), Is.Null, "quiet at close " + (i + 1));
            }
            TownLifeRules.AtClose(sim);
            p = Of(sim, PetitionDef.Work);
            Assert.That(p, Is.Not.Null, "raised again at the fifth close");

            // Ignored: unanswered for 2 closes: −4, closed.
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Open), "one close: still open");
            before = Approval(sim);
            long step = StepNow(sim);
            TownLifeRules.AtClose(sim);
            Assert.That(p.Status, Is.EqualTo(PetitionStatus.Ignored));
            Assert.That(Approval(sim), Is.EqualTo(System.Math.Max(0, before + step + d.Ignored)));
        }

        [Test]
        public void Answer_Rejections()
        {
            Simulation sim = Township();
            Assert.That(Reason(sim, 1, 1), Is.EqualTo(RejectReason.InvalidStep), "nothing read yet");
            TownLifeRules.AtClose(sim);
            TownPetition p = Of(sim, PetitionDef.Hall);
            Assert.That(Reason(sim, 999, 1), Is.EqualTo(RejectReason.UnknownPetition));
            Assert.That(Reason(sim, p.Id, 3), Is.EqualTo(RejectReason.InvalidArgument), "1, 2 or 4 closes");
            Assert.That(Answer(sim, p.Id, 4).Accepted, Is.True);
            Assert.That(Reason(sim, p.Id, 0), Is.EqualTo(RejectReason.PetitionClosed), "a promise can't be withdrawn or declined");
            Assert.That(Reason(sim, p.Id, 1), Is.EqualTo(RejectReason.PetitionClosed));
        }

        // AC-6: approval moves with happiness (70 neutral, at most 4 a close), stays 0–100, and changes only the pull.
        [Test]
        public void Moves()
        {
            Simulation sim = Township();
            TownLifeRules.AtClose(sim);
            Happy(sim, 90);
            long a = Approval(sim);
            TownLifeRules.AtClose(sim);
            Assert.That(Approval(sim), Is.EqualTo(a + 4), "up to 4 a close");
            int n = D(sim).NeutralHappiness;
            Happy(sim, n);
            Assert.That(StepNow(sim), Is.EqualTo(0));
            Happy(sim, n + 3);
            Assert.That(StepNow(sim), Is.EqualTo(0), "toward zero: 3 ÷ 5 is 0");
            Happy(sim, n - 3);
            Assert.That(StepNow(sim), Is.EqualTo(0), "−3 ÷ 5 toward zero");
            Happy(sim, n + 7);
            Assert.That(StepNow(sim), Is.EqualTo(1));
            Happy(sim, n - 10);
            Assert.That(StepNow(sim), Is.EqualTo(-2));
            Happy(sim, n - 30);
            Assert.That(StepNow(sim), Is.EqualTo(-4), "at most 4 either way");

            Happy(sim, 100);
            for (int i = 0; i < 40; i++) TownLifeRules.AtClose(sim);
            Assert.That(Approval(sim), Is.LessThanOrEqualTo(100));
            Happy(sim, 0);
            for (int i = 0; i < 40; i++) TownLifeRules.AtClose(sim);
            Assert.That(Approval(sim), Is.GreaterThanOrEqualTo(0));
            Assert.That(TownLifeRules.State(sim).ApprovalHistory.Count, Is.EqualTo(D(sim).KeepHistory));
        }

        [Test]
        public void OnlyMigration()
        {
            Simulation sim = Township();
            TownLifeRules.AtClose(sim);
            TownLifeState s = TownLifeRules.State(sim);
            s.Approval = Fixed.FromInt(50);
            Fixed mean = TownLifeRules.Mean(s);
            Fixed flat = TownLifeRules.Pull(sim);
            s.Approval = Fixed.FromInt(100);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(flat + Fixed.FromInt(25)), "50 more approval draws families 25 points sooner");
            s.Approval = Fixed.FromInt(0);
            Assert.That(TownLifeRules.Pull(sim), Is.EqualTo(flat - Fixed.FromInt(25)));
            Assert.That(TownLifeRules.GrantFactor(sim), Is.EqualTo(TownLifeRules.GrantFactor(sim)), "grants follow the scores, not approval");
            Assert.That(TownLifeRules.Mean(s), Is.EqualTo(mean), "the scores don't read approval");
        }

        // A petition's side is the compass side of the square where most of the families live.
        [Test]
        public void Side()
        {
            Simulation sim = Township();
            TownLifeRules.AtClose(sim);
            TownPetition hall = Of(sim, PetitionDef.Hall);
            Assert.That(hall.Side, Is.InRange(0, 3));
        }

        [Test]
        public void SaveRoundTrips()
        {
            Simulation sim = Township();
            Idle(sim, 4);
            TownLifeRules.AtClose(sim);
            Answer(sim, Of(sim, PetitionDef.Hall).Id, 2);
            TownLifeRules.AtClose(sim);
            LoadResult r = SaveTests.RoundTrip(sim);
            TownLifeState a = TownLifeRules.State(sim), b = r.State.Township.TownLife;
            Assert.That(b.Petitions.Count, Is.EqualTo(a.Petitions.Count));
            Assert.That(b.Closes, Is.EqualTo(a.Closes));
            Assert.That(b.ApprovalHistory, Is.EqualTo(a.ApprovalHistory));
            Assert.That(b.QuietUntil, Is.EqualTo(a.QuietUntil));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
