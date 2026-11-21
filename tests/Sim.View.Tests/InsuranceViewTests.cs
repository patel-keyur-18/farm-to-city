using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-155 (spec 027 §7, Preview 13): the Money page's section, a business's row, the sow sheet's line, the dues and the away lines.</summary>
    public class InsuranceViewTests
    {
        private static SheetVm Money(ViewContext ctx) { return MoneySheet.Build(ctx); }

        private static LineVm Row(SheetVm vm, string label) { return vm.Lines.Single(l => !l.IsHeader && l.Label == label); }

        // The Money page: the section appears at the Township, the switches cost nothing (no card), and flip the policy.
        [Test]
        public void Money_Section_Switches()
        {
            Simulation sim = Situations.TownshipBuilt();
            var ctx = ViewKit2.Context(sim);
            SheetVm vm = Money(ctx);
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label == "Insurance"), Is.True);
            LineVm crop = Row(vm, "Crop insurance"), prop = Row(vm, "Property insurance");
            Assert.That(crop.Action.Label, Is.EqualTo("Off"));
            Assert.That(crop.Note, Does.Contain("not insured"));
            Assert.That(ConfirmCards.For(ctx, crop.Action.Create(ctx.Now)), Is.Null, "no confirm card: it costs nothing");
            Assert.That(sim.Execute(crop.Action.Create(ctx.Now)).Accepted, Is.True);
            Assert.That(InsuranceRules.State(sim).CropOn, Is.True);
            Assert.That(Row(Money(ViewKit2.Context(sim)), "Crop insurance").Action.Label, Is.EqualTo("On"));

            Assert.That(sim.Execute(prop.Action.Create(ctx.Now)).Accepted, Is.True);
            LineVm after = Row(Money(ViewKit2.Context(sim)), "Property insurance");
            Assert.That(after.Note, Is.EqualTo("Starts at the next close."), "switched on mid-season: it starts at the next close");
            Assert.That(after.Action.Label, Is.EqualTo("On"));
        }

        // The last claims, newest first, each with a ?.
        [Test]
        public void Claims_List()
        {
            Simulation sim = Situations.Insured();
            var ctx = ViewKit2.Context(sim);
            SheetVm vm = Money(ctx);
            int at = vm.Lines.FindIndex(l => l.IsHeader && l.Label.StartsWith("Claims"));
            Assert.That(at, Is.GreaterThan(0));
            LineVm first = vm.Lines[at + 1];
            Assert.That(first.WhyKey, Is.EqualTo(InsuranceSheet.ClaimWhy));
            Assert.That(first.Label, Is.EqualTo("A paused line, tea stall").Or.Contains("paused line"));
            Assert.That(WhyView.Build(ctx, first.WhyKey, first.WhyId).Rows.Count, Is.GreaterThan(1));
            Assert.That(vm.Lines.Any(l => l.Label != null && l.Label.StartsWith("Hail, wheat")), Is.True, "what hit and what was hit");
        }

        // A business's row: its own premium and what it pays.
        [Test]
        public void Business_Row()
        {
            Simulation sim = Situations.Insured();
            var ctx = ViewKit2.Context(sim);
            Business b = sim.State.Businesses.Businesses.Last();
            SheetVm vm = BusinessSheet.Build(ctx, b.Id);
            LineVm row = Row(vm, "Business insurance");
            Assert.That(row.Action.Label, Is.EqualTo("On"));
            Assert.That(row.Note, Does.Contain("fire"));
            Assert.That(row.WhyKey, Is.EqualTo(InsuranceSheet.BusinessWhy));
            Assert.That(sim.Execute(row.Action.Create(ctx.Now)).Accepted, Is.True);
            Assert.That(InsuranceRules.State(sim).BusinessPolicy(b.Id).On, Is.False);
        }

        // The sow sheet: a line under each crop with its premium, or that the cash cannot pay it.
        [Test]
        public void Sow_Line()
        {
            Simulation sim = Situations.Insured();
            var ctx = ViewKit2.Context(sim);
            int tile = sim.State.Land.Tiles.First(t => t.Terrain == Sim.Core.Definitions.Terrain.Farm && sim.State.Land.Parcels[t.ParcelId].Owned && sim.State.Farm.CropOn(t.Id) == null).Id;
            CropRowVm row = SowSheet.Build(ctx, tile).Rows.First(r => r.InsuranceText != null);
            Assert.That(row.InsuranceText, Does.StartWith("Insured: premium"));
            Assert.That(row.InsuranceWarn, Is.False);
            Assert.That(WhyView.Build(ctx, row.InsuranceWhyKey, row.InsuranceWhyId).Rows.Count, Is.GreaterThan(3));

            Situations.SetCash(sim, 0);
            CropRowVm poor = SowSheet.Build(ViewKit2.Context(sim), tile).Rows.First(r => r.InsuranceText != null);
            Assert.That(poor.InsuranceWarn, Is.True);
            Assert.That(poor.InsuranceText, Does.StartWith("Not insured"));

            InsuranceRules.State(sim).CropOn = false;
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), tile).Rows.All(r => r.InsuranceText == null), Is.True, "off: no line");
        }

        // The next close's dues list the property premium, and a business's own premium stands beside.
        [Test]
        public void Dues_Premiums()
        {
            Simulation sim = Situations.Insured();
            var ctx = ViewKit2.Context(sim);
            SeasonVm season = SeasonBook.Build(ctx, false);
            Assert.That(season.Due.Any(l => l.Key == "insurance" && l.Amount == InsuranceRules.PropertyPremium(sim)), Is.True);
            Assert.That(season.Own.Any(l => l.Why.Any(w => w.Label == "Its insurance premium")), Is.True);
        }

        // An insured fire's repair card says what the policy pays back, and the confirm card shows the bill, the refund and what you pay.
        [Test]
        public void Repair_Card_Insured()
        {
            Simulation sim = Situations.Insured();
            EventsState st = EventRules.State(sim);
            int repairDef = sim.Config.Game.Events.IndexOf(Sim.Core.Definitions.EventsDef.FireRepair);
            long house = InsuranceRules.Insurable(sim).First().Id;
            long bill = 12_000_00;
            var p = new PendingEvent { Id = st.NextId++, Def = repairDef, RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 24 * 3600, Target = house, Param = bill };
            st.Waiting.Add(p);
            var ctx = ViewKit2.Context(sim);
            Assert.That(EventSheet.Card(ctx, p.Id).Lines.Where(l => l.Action != null).Any(l => l.Note != null && l.Note.Contains("insurance pays back")), Is.False, "uninsured: no line");

            st.FireInsured = true;
            long back = Sim.Core.Finance.Money.MulRate(bill, Sim.Core.Maths.Fixed.FromRatio(90, 100));
            Assert.That(InsuranceRules.RepairPays(sim, p, 0), Is.EqualTo(back));
            SheetVm card = EventSheet.Card(ctx, p.Id);
            Assert.That(card.Lines.Where(l => l.Action != null).All(l => l.Note != null && l.Note.Contains(" · insurance pays back ")), Is.True);
            ConfirmCardVm pay = ConfirmCards.For(ctx, new AnswerEvent(ctx.Now, p.Id, 0));
            Assert.That(pay.Rows.Select(r => r.Label), Is.EqualTo(new[] { "The repair bill", "Your insurance pays back", "You pay", "Cash after" }));
            Assert.That(pay.CashChange, Is.EqualTo(-(bill - back)));
        }

        // The away card names a claim, a lapse and an uninsured sowing.
        [Test]
        public void Away_Lines()
        {
            Simulation sim = Situations.Insured();
            var ctx = ViewKit2.Context(sim);
            var events = new SimEvent[]
            {
                new ClaimPaid(sim.State.Clock.SimTime, Claim.PropertyPolicy, sim.Config.Game.Events.IndexOf("fire") + 1, 1, 12_600_00, 0),
                new InsuranceLapsed(sim.State.Clock.SimTime, Claim.PropertyPolicy, 0, 2_100_00),
                new CropNotInsured(sim.State.Clock.SimTime, 40, sim.Config.Game.Farm.IndexOfCrop("wheat"), 1_100_00),
            };
            var rows = TownAway.Rows(ctx, events);
            Assert.That(rows.Any(r => r.Note != null ? r.Note.Contains("Insurance paid") : r.Value != null && r.Value.Contains("Insurance paid")), Is.True);
            Assert.That(rows.Any(r => (r.Value ?? r.Note ?? "").Contains("ended")), Is.True);
            Assert.That(rows.Any(r => (r.Value ?? r.Note ?? "").Contains("uninsured")), Is.True);
        }
    }
}
