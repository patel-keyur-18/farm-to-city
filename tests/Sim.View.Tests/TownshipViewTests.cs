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
    /// <summary>US-103 (spec 023 §3.4, §3.5, Preview 4): the Becoming a Township sheet, the letter, the old village on the Town sheet, the tax you owe in the balance sheet, and every number's Why?.</summary>
    public class TownshipViewTests
    {
        private static void Era(Simulation sim, string id)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == id) sim.State.Era.Current = i;
        }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Title, Is.Not.Empty, l.Label);
            }
        }

        // The Village sees the five goals with what it has and needs; the Done tag shows once a goal is met for good.
        [Test]
        public void Goals()
        {
            Simulation sim = Situations.Town();
            Era(sim, "village");
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = ProgressSheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Becoming a Township"));
            string[] labels = { "Households of yours", "The village clinic", "The paved link road", "Businesses with a profitable season", "Literate: people aged 7 and over" };
            Assert.That(vm.Lines.Take(5).Select(l => l.Label), Is.EqualTo(labels));
            Assert.That(vm.Lines[0].Value, Does.EndWith(" of 20"));
            Assert.That(vm.Lines[1].Value, Is.EqualTo("Not yet"));
            Assert.That(vm.Lines[3].Value, Is.EqualTo("0 of 3"));
            Assert.That(vm.Lines[4].Value, Does.EndWith("% of 60%"));
            Assert.That(vm.Lines.Take(5).All(l => l.Badge == null), Is.True, "nothing done yet");
            Assert.That(vm.Lines.Any(l => l.Label.StartsWith("The usual order")), Is.True);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("House and business taxes, and five policies"));
            AssertWhys(sim, vm);
            sim.State.Township.GetType().GetProperty("GateMet").GetSetMethod(true).Invoke(sim.State.Township, new object[] { TownshipGoal.Road });
            Assert.That(ProgressSheet.Build(ctx).Lines[2].Badge, Is.EqualTo("Done"), "a goal met for good");
        }

        // Every era's celebration needs its banner (a missing string stalled the Township's letter in the Editor).
        [Test]
        public void EraBanners()
        {
            var strings = ViewKit.Strings();
            foreach (var era in Situations.Town().Config.Game.Onboarding.Eras.Skip(1))
            {
                Assert.That(strings.Has(era.Id + ".banner"), Is.True, era.Id);
                Assert.That(strings.Has(era.Id + ".bannerSub"), Is.True, era.Id);
            }
        }

        [Test]
        public void Letter()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(Sim.View.Views.Letter.Page(ctx, "township", false, GuideKit.Info(sim), 0).Text, Does.StartWith("Beta, a village feeds itself"));
            Assert.That(Sim.View.Views.Letter.List(ctx).Lines.Select(l => l.Label), Does.Contain("Your Township"), "the era's letter, titled as its sheet is");
        }

        // The old village: one section on the Town sheet, from the unlock, each row with a Why?.
        [Test]
        public void OldVillage()
        {
            Simulation sim = Situations.Town();
            Era(sim, "village");
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(NeedsOrTown(sim).Lines.Any(l => l.WhyKey == "cohort"), Is.False, "nobody before the unlock");
            Assert.That(sim.Execute(new DebugNextEra()).Accepted || Sim.Core.Progress.EraRules.Unlock(sim), Is.True);
            SheetVm vm = NeedsOrTown(sim);
            LineVm[] rows = vm.Lines.Where(l => l.WhyKey == "cohort").ToArray();
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "The old village", "Happiness", "Children at your school", "Can read: aged 7 and over", "House tax they paid last close" }).Or.EqualTo(new[] { "The old village", "Happiness", "Can read: aged 7 and over", "House tax they paid last close" }));
            Assert.That(rows[0].Value, Is.EqualTo("30 households · 150 people"));
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label == "The old village"), Is.True);
            AssertWhys(sim, vm);
        }

        private static SheetVm NeedsOrTown(Simulation sim) { return TownSheet.Build(ViewKit2.Context(sim)); }

        // What you owe the town is a row on the balance sheet, a liability.
        [Test]
        public void TaxOwed_OnTheBalanceSheet()
        {
            Simulation sim = Situations.Township();
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.HouseTax, 288_000), new Posting(Account.TaxOwed, -288_000));
            NetWorthSnapshot n = Reporting.NetWorth(sim);
            Assert.That(n.TaxOwed, Is.EqualTo(288_000));
            Assert.That(StatementSheet.Item(n, "taxOwed"), Is.EqualTo(288_000));
            Assert.That(StatementSheet.Item(n, "owe"), Is.EqualTo(n.LoanPrincipal + n.InterestPayable + 288_000));
            Assert.That(ViewKit.Strings().Get("stmt.bs.taxOwed"), Is.EqualTo("Tax you owe the town"));
        }
    }
}
