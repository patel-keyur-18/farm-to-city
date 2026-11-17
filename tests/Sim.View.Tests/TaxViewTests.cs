using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-140 (spec 022 §7): the Taxes sheet, its preview, a pending rate and your side's rows.</summary>
    public class TaxViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name); }

        // The sheet: the town's money line, a slider for each tax set from the next season, a note of what a rate would do, who pays and whether the close is covered.
        [Test]
        public void Sheet()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = TaxSheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Taxes and policies"));
            Assert.That(vm.Lines.First().Banner, Is.True);
            LineVm house = vm.Lines.Single(l => l.Label == "House tax"), biz = vm.Lines.Single(l => l.Label == "Business tax");
            Assert.That((house.Amount.Min, house.Amount.Max, house.Amount.Start), Is.EqualTo((0L, 24L, 16L)), "tenths of a percent: 0 to 2.4%, now 1.6%");
            Assert.That((biz.Amount.Min, biz.Amount.Max, biz.Amount.Start), Is.EqualTo((0L, 20L, 10L)));
            Assert.That(house.Amount.Words(16), Is.EqualTo("1.6% a year"));
            Assert.That(house.Amount.Set(20).Create(ctx.Now), Is.TypeOf<SetTaxRate>());
            Assert.That(house.WhyKey, Is.EqualTo("taxHouse"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // AC-7: the preview's revenue change is exactly what the next close does with nothing else changed.
        [Test]
        public void Preview()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm house = TaxSheet.Build(ctx).Lines.Single(l => l.Label == "House tax");
            AmountPick same = house.Amount.Pick(16), up = house.Amount.Pick(24);
            Assert.That(same.Note, Does.Contain("a season · you"));
            long was = TaxRules.Expected(sim, Fixed.Parse("0.016"), Fixed.Parse("0.10"), out _, out _, out long youNow, out _);
            long will = TaxRules.Expected(sim, Fixed.Parse("0.024"), Fixed.Parse("0.10"), out _, out _, out long youUp, out _);
            Assert.That(up.Note, Does.StartWith("+" + ctx.Fmt.Coins(will - was)).And.Contain("you " + ctx.Fmt.Coins(youUp - youNow)));

            // The close then bills what the preview said, from the rate set now.
            Ok(sim, new SetTaxRate(ctx.Now, TaxKind.House, Fixed.Parse("0.024")));
            long billed = CloseDues.Next(sim).Single(d => d.Kind == DueKind.HouseTax).Amount; // priced at the index the close will have, at the rate in force
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 3600);
            Assert.That(sim.State.Ledger.Entries.Last(e => e.DescriptionKey == "ledger.houseTax").Postings.Single(p => p.Account == Account.HouseTax).Amount, Is.EqualTo(billed),
                "the season that ended was billed at the rate in force, the new rate starts with the next");
        }

        // A rate set now is pending: the sheet says from when, and Undo takes it back.
        [Test]
        public void Pending()
        {
            Simulation sim = Situations.Township();
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.Business, Fixed.Parse("0.15")));
            SheetVm vm = TaxSheet.Build(ViewKit2.Context(sim));
            LineVm pending = vm.Lines.Single(l => l.Label != null && l.Label.StartsWith("From "));
            Assert.That(pending.Label, Does.Contain("15%").And.Contain("now 10%"));
            Assert.That(vm.Lines.Single(l => l.Label == "Business tax").Amount.Start, Is.EqualTo(15L), "the slider stands on the pending rate");
            Ok(sim, pending.Action.Create(sim.State.Clock.SimTime));
            Assert.That(TaxSheet.Build(ViewKit2.Context(sim)).Lines.Any(l => l.Label != null && l.Label.StartsWith("From ")), Is.False);
        }

        // Your side: the house tax is on Next season, already due, with the number of houses, and on the season's Spent once paid.
        [Test]
        public void YourSide()
        {
            Simulation sim = Situations.Township();
            SeasonVm due = SeasonBook.Build(ViewKit2.Context(sim), false);
            SeasonLineVm tax = due.Due.Single(l => l.Key == "houseTax");
            Assert.That(tax.Label, Does.StartWith("House tax,").And.Contain("houses"));
            Assert.That(tax.Amount, Is.GreaterThan(0));
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 3600);
            SeasonVm spent = SeasonBook.Build(ViewKit2.Context(sim), false);
            Assert.That(spent.Spent.Any(l => l.Label == "House tax"), Is.True);
        }
    }
}
