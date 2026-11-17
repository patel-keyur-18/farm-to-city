using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-148 part 1 (spec 023 §3.1, §3.2, §3.9, Preview 3): the clinic plot's and the quarters plot's sheets in every state, their cards and loans, the household's Health care row, and every number's Why?.</summary>
    public class ClinicViewTests
    {
        private const long Hour = 3600;

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        private static void Era(Simulation sim, string id)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == id) sim.State.Era.Current = i;
        }

        /// <summary>A Village town with a family and cash.</summary>
        private static Simulation Village(bool rich = true)
        {
            Simulation sim = Situations.Town();
            Era(sim, "village");
            if (rich) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 300_000_00), new Posting(Account.Sales, -300_000_00));
            return sim;
        }

        private static SheetVm Plot(Simulation sim, string kind)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            return FieldSheets.ForTile(ctx, sim.Config.Game.Map.Places.First(p => p.Kind == kind).Tile);
        }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
            }
        }

        // Before the Village the clinic plot is locked with the sim's reason; in the Village it shows the share, the days, the quarters and what it will do.
        [Test]
        public void Locked_And_Ready()
        {
            Simulation hamlet = Situations.Town();
            Era(hamlet, "hamlet");
            SheetVm locked = Plot(hamlet, "clinic_plot");
            Assert.That(locked.Lines.Select(l => l.Value), Does.Contain("Not yet"));
            ActionVm build = locked.Actions.Single(a => a.Id == "buildClinic");
            Assert.That(build.Enabled, Is.False);
            AssertWhys(hamlet, locked);

            Simulation sim = Village();
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = Plot(sim, "clinic_plot");
            Assert.That(vm.Lines.Where(l => !l.IsHeader && l.WhyKey != null).Select(l => l.Label), Is.EqualTo(new[]
            {
                "The community share, a gift to the village", "The district builds it in", "The doctor's quarters (yours)", "The doctor arrives",
                "Sick days", "Health lost when food is short", "Recovery after a good season", "Elders' chance of passing away a season", "Health care need",
            }));
            Assert.That(vm.Lines.First(l => l.Label.StartsWith("The district builds")).Value, Is.EqualTo("2 d"));
            Assert.That(vm.Actions.Single(a => a.Id == "buildClinic").Enabled, Is.True);
            Assert.That(vm.Actions.Single(a => a.Id == "clinicLoan").Opens.Value.Kind, Is.EqualTo(ClinicSheet.ClinicLoanKind));
            Assert.That(vm.Actions.Single(a => a.Id == "quarters").Opens.Value, Is.EqualTo(new SheetRequest("Tile", ClinicRules.QuartersDef(sim).Tile)));
            AssertWhys(sim, vm);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        [Test]
        public void Card()
        {
            Simulation sim = Village();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new BuildClinic(ctx.Now));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Hold, Is.False);
            Assert.That(card.Title, Is.EqualTo("The village clinic?"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Your cash now", "Your community share", "Built in", "Cash after" }));
            Assert.That(card.Rows[2].Value, Is.EqualTo("2 days"));
            ConfirmCardVm q = ConfirmCards.For(ctx, new BuildDoctorQuarters(ctx.Now));
            Assert.That(q.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Your cash now", "Cost", "Built in", "Cash after" }));
            SheetVm loan = ClinicSheet.LoanSheet(ctx, false);
            Assert.That(loan.Lines.Count, Is.GreaterThan(0));
        }

        // Building, then built with no doctor, then the doctor in post, then at the Township unpaid and left.
        [Test]
        public void States()
        {
            Simulation sim = Village();
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildClinic(t));
            SheetVm building = Plot(sim, "clinic_plot");
            Assert.That(building.Title, Is.EqualTo("The clinic is being built"));
            Assert.That(building.Icon, Is.EqualTo("clinic_building"));
            AssertWhys(sim, building);

            sim.AdvanceTo(t + 48 * Hour);
            SheetVm waiting = Plot(sim, "clinic_plot");
            Assert.That(waiting.Title, Is.EqualTo("The village clinic"));
            Assert.That(waiting.Lines.Select(l => l.Value), Does.Contain("Waiting for his house"));
            Assert.That(waiting.Lines.Where(l => l.Label == "Sick days").Single().Value, Is.EqualTo("—"));
            AssertWhys(sim, waiting);

            Ok(sim, new BuildDoctorQuarters(sim.State.Clock.SimTime));
            SheetVm qb = Plot(sim, "quarters_plot");
            Assert.That(qb.Icon, Is.EqualTo("doctor_quarters_building"));
            AssertWhys(sim, qb);
            sim.AdvanceTo(sim.State.Clock.SimTime + (48 + 24) * Hour);

            SheetVm open = Plot(sim, "clinic_plot");
            Assert.That(open.Subtitle, Does.StartWith("Dr. "));
            Assert.That(open.Lines.Single(l => l.Label == "Who pays").Value, Does.Contain("the district"));
            Assert.That(open.Lines.Single(l => l.Label == "Health service").Value, Is.EqualTo("100%"));
            Assert.That(open.Lines.Single(l => l.Label == "Sick days").Value, Is.EqualTo("50% fewer"));
            Assert.That(open.Lines.Single(l => l.Label == "Elders' chance of passing away a season").Value, Is.EqualTo("25% less"));
            Assert.That(open.Lines.Single(l => l.Label == "Health care need").Value, Is.EqualTo("100% met"));
            Assert.That(open.Lines.Any(l => l.Label == "Sick days avoided"), Is.True);
            AssertWhys(sim, open);
            SheetVm quarters = Plot(sim, "quarters_plot");
            Assert.That(quarters.Title, Is.EqualTo("The doctor's quarters"));
            Assert.That(quarters.Lines.Single(l => l.Label == "Rent from the doctor").Value, Does.EndWith("a season"));
            AssertWhys(sim, quarters);

            // The household's Health care row reads the clinic.
            Household fam = sim.State.Citizens.Households.FirstOrDefault(x => !ClinicRules.IsDoctorHome(sim, x));
            if (fam != null)
            {
                LineVm care = NeedsRows(sim, fam).Single(l => l.Label == "Health care");
                Assert.That(care.Note, Is.EqualTo("The chemist and the village clinic"));
                Assert.That(care.Value, Is.EqualTo("100%"));
            }

            // At the Township, unpaid: the warning and the way to fix it.
            sim = Situations.Township();
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 300_000_00), new Posting(Account.Sales, -300_000_00));
            Ok(sim, new BuildClinic(sim.State.Clock.SimTime));
            Ok(sim, new BuildDoctorQuarters(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 72 * Hour);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            Assert.That(Plot(sim, "clinic_plot").Lines.Single(l => l.Label == "Who pays").Value, Does.Contain("Health budget").Or.Contain("the district"));
            sim.State.Treasury.Unpaid[1] = 1;
            SheetVm warn = Plot(sim, "clinic_plot");
            Assert.That(warn.Lines.Any(l => l.Banner), Is.True);
            Assert.That(warn.Actions.Select(a => a.Id), Is.EquivalentTo(new[] { "budget", "give" }));
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Any(al => al.Text.Contains("doctor's allowance")), Is.True);
            sim.State.Treasury.Unpaid[1] = 0;
            ClinicRules.State(sim).Doctor = DoctorStatus.Left; // as if he had left
            SheetVm left = Plot(sim, "clinic_plot");
            Assert.That(left.Subtitle, Is.EqualTo("The clinic covers nobody until a new doctor comes."));
            Assert.That(left.Lines.Single(l => l.Label == "Sick days").Value, Is.EqualTo("—"));
            AssertWhys(sim, left);
        }

        private static List<LineVm> NeedsRows(Simulation sim, Household x) { return NeedsView.Rows(ViewKit2.Context(sim), x); }
    }
}
