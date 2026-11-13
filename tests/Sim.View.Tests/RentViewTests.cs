using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-240 (spec 014 §3.14): the renewal card the owner approved, the household sheet's lease rows and the rent's Why?.</summary>
    public class RentViewTests
    {
        private static Household InLastSeason(out Simulation sim, long rent, long income)
        {
            sim = Situations.Town();
            Household x = sim.State.Citizens.Households.Single(h => h.Members.Count == 5);
            x.LeaseRent = x.Rent = rent;
            x.LeaseStart = LeaseRules.Season(sim) - (sim.Config.Game.Population.LeaseSeasons - 1);
            x.Income = income;
            return x;
        }

        // AC-24: the card names the family, the rent and the market, what limits a raise and the choices with their effects; the market rise first.
        [Test]
        public void RenewalCard()
        {
            Household x = InLastSeason(out Simulation sim, 1_900_00, 38_000_00);
            ViewContext ctx = ViewKit2.Context(sim);
            CardVm card = LeaseViews.Card(ctx, x.Id, 0, 1);
            Assert.That(card.Title, Is.EqualTo("A lease is ending: Manoj's family"));
            Assert.That(card.Text, Does.Contain("rented Family house 1 for a year at 1,900 coins a season").And.Contain("a rise is capped at 10% a year, so the most you can ask is 2,090"));
            Assert.That(card.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LeaseIds.Market, LeaseIds.Smaller, LeaseIds.Keep, LeaseIds.Later }));
            Assert.That(card.Buttons[0].Label, Is.EqualTo("Raise to 2,090 (+10%)"));
            Assert.That(card.Buttons[1].Label, Is.EqualTo("Raise to 1,995 (+5%)"));
            Assert.That(card.Buttons[2].Label, Is.EqualTo("Keep it at 1,900"));
            Assert.That(card.Buttons[0].Detail, Does.Contain("Applies if you don't answer"));
            Assert.That(card.Text, Does.Contain("5% of what they earn").Or.Contain("%"), "the burden at the most they'd pay");

            // Several at once: a counter and one button for all the rest.
            CardVm of3 = LeaseViews.Card(ctx, x.Id, 0, 3);
            Assert.That(of3.Title, Is.EqualTo("A lease is ending: Manoj's family (1 of 3)"));
            Assert.That(of3.Buttons.Any(b => b.Id == LeaseIds.Rest && b.Label == "Market rise for all 3 left"), Is.True);
            Assert.That(LeaseViews.Card(ctx, x.Id, 2, 3).Buttons.Any(b => b.Id == LeaseIds.Rest), Is.False, "the last card has nobody left to share with");

            // A family that can't bear a raise: the card says so and offers keep or lower.
            x.Income = 5_000_00;
            CardVm tight = LeaseViews.Card(ctx, x.Id, 0, 1);
            Assert.That(tight.Text, Does.Contain("already pay 38% of it").And.Contain("a raise would not be fair now"));
            Assert.That(tight.Buttons.Select(b => b.Id), Is.EqualTo(new[] { LeaseIds.Keep, LeaseIds.Lower, LeaseIds.Later }));
            Assert.That(tight.Buttons[1].Label, Is.EqualTo("Lower to 1,805 (−5%)"));

            // Not in its last season, or nothing to ask: no card.
            x.LeaseStart = LeaseRules.Season(sim);
            Assert.That(LeaseViews.Card(ctx, x.Id, 0, 1), Is.Null);
        }

        // AC-24: the household sheet shows the lease and, in its last season, the same choices as buttons that run the command; every row has a Why? that builds.
        [Test]
        public void HouseholdSheetLease()
        {
            Household x = InLastSeason(out Simulation sim, 1_900_00, 38_000_00);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sheet = HouseholdSheet.Build(ctx, x.Id);
            LineVm lease = sheet.Lines.Single(l => l.Label == "Lease");
            Assert.That(lease.Value, Is.EqualTo("1,900 coins a season · ends this season"));
            Assert.That(sheet.Lines.Single(l => l.Label == "At the close").Value, Does.StartWith("Renews at 2,090 coins, unless you answer"));
            ActionVm smaller = sheet.Lines.Where(l => l.Action != null).Select(l => l.Action).Single(a => a.Id == LeaseIds.Smaller);
            Assert.That(smaller.Enabled, Is.True);
            Assert.That(sim.Execute(smaller.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(HouseholdSheet.Build(ctx, x.Id).Lines.Single(l => l.Label == "At the close").Value, Does.StartWith("Renews at 1,995 coins, as you chose"));
            foreach (LineVm l in sheet.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
            }
            WhyVm rent = WhyView.Build(ctx, "household.rent", x.Id);
            Assert.That(string.Join(" ", rent.Rows.Select(r => r.Label + " " + r.Value)), Does.Contain("2,500 coins base rent, an inner acre (15% less)"));
        }

        // The alert: an unanswered lease in its last season is a decision in the alerts list that opens the household sheet.
        [Test]
        public void LeaseAlert()
        {
            Household x = InLastSeason(out Simulation sim, 1_900_00, 38_000_00);
            ViewContext ctx = ViewKit2.Context(sim);
            var alerts = Alerts.Build(ctx);
            AlertVm a = alerts.Single(r => r.Text == "A lease is ending: Manoj's family");
            Assert.That(a.Action.Opens.Value.Kind, Is.EqualTo("Household"));
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, x.Id, LeaseChoice.Keep)).Accepted, Is.True);
            Assert.That(Alerts.Build(ctx).Any(r => r.Text == "A lease is ending: Manoj's family"), Is.False, "answered: no longer waiting");
        }
    }
}
