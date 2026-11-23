using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Equipment;
using Sim.Core.Market;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-245 (spec 028 §3.6, ADR 0162, Preview 18): the tempo on the Equipment sheet, the mandi sheet's "carried by" line, the let-out block and every number's Why?.</summary>
    public class TempoViewTests
    {
        private static string Arg(Simulation sim, long grams) { return sim.Config.Game.Market.IndexOf("potato") + ":" + grams; }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey == "tempo"))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                WhyTests.AssertWords(l.Label, why);
            }
        }

        // Before it is bought: the Tempo block with its Buy and Lease buttons and no hire a day (a tempo is never hired for a day); once yours, its legs, the let-out block and the honest "does it pay?".
        [Test]
        public void Equipment_Sheet()
        {
            Simulation sim = Situations.Township();
            SheetVm before = EquipmentSheet.Build(ViewKit2.Context(sim));
            Assert.That(before.Lines.Where(l => l.IsHeader).Select(l => l.Label), Does.Contain("Tempo"));
            Assert.That(before.Lines.First(l => l.Value != null && l.Value.StartsWith("Carries")).Value, Is.EqualTo("Carries 1,000 kg a leg · 4 h a leg · 2 legs a day"));
            var buttons = before.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).ToList();
            Assert.That(buttons.Any(b => b.Id == "buy:tempo"), Is.True);
            Assert.That(buttons.Any(b => b.Id == "lease:tempo"), Is.True);
            Assert.That(buttons.Any(b => b.Id == "hire:tempo"), Is.False, "never hired for a day");

            sim = Situations.Owned();
            SheetVm mine = EquipmentSheet.Build(ViewKit2.Context(sim));
            Assert.That(mine.Lines.Any(l => l.Value == "Ready · 2 of 2 legs left today"), Is.True);
            LineVm letOut = mine.Lines.Single(l => l.Label == "Let its idle legs out");
            Assert.That(letOut.Value, Is.EqualTo("Off"));
            Assert.That(letOut.Note, Does.Contain("Your own trips always come first"));
            Assert.That(letOut.Action.Create(0), Is.TypeOf<SetHireOut>());
            LineVm pays = mine.Lines.Single(l => l.Label == "Does it pay?");
            Assert.That(pays.Value, Does.StartWith("Pays back in about"));
            Assert.That(pays.Note, Does.Contain("saves").And.Contain("hired trolley"));
            Assert.That(mine.Lines.Single(l => l.Label == "Legs this season").Value, Is.EqualTo("0 for you · 0 for neighbours"));
            AssertWhys(sim, mine);
        }

        // The mandi sheet says who carries the load: the tempo, the tempo's two legs, then the hired trolley when more than two legs or none left.
        [Test]
        public void Mandi_Says_Who_Carries()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(TempoSheet.RideText(ctx, TempoRules.Choose(sim, 800_000)), Is.EqualTo("Your tempo · 1 leg"));
            Assert.That(TempoSheet.RideText(ctx, TempoRules.Choose(sim, 1_500_000)), Is.EqualTo("Your tempo · 2 legs"));
            Assert.That(TempoSheet.RideText(ctx, TempoRules.Choose(sim, 2_500_000)), Is.EqualTo("The hired trolley"));
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 5_000_000;
            SheetVm mandi = MandiSheet.Build(ctx, Arg(sim, 800_000));
            Assert.That(mandi.Lines.Single(l => l.Label == "Carried by").Value, Is.EqualTo("Your tempo · 1 leg"));
            Assert.That(mandi.Lines.Any(l => l.Label == "The tempo's diesel and wear" && l.Value == "−" + ctx.Fmt.Coins(TempoRules.LegCost(sim, TempoRules.Trip(sim, TempoRules.Of(sim))))), Is.True, string.Join(" | ", mandi.Lines.Select(l => l.Label + "=" + l.Value)));
            AssertWhys(sim, mandi);
            SheetVm two = MandiSheet.Build(ctx, Arg(sim, 1_500_000));
            Assert.That(two.Lines.Single(l => l.Label == "Your time").Value, Does.StartWith("1.0 h of your day"));
        }

        // Without a tempo the sheet is as it was: no "Carried by" line, the trolley's hire.
        [Test]
        public void Without_A_Tempo()
        {
            Simulation sim = Situations.Township();
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 5_000_000;
            SheetVm mandi = MandiSheet.Build(ViewKit2.Context(sim), Arg(sim, 1_500_000));
            Assert.That(mandi.Lines.Any(l => l.Label == "Carried by"), Is.False);
            Assert.That(mandi.Lines.Any(l => l.Label == "Trolley hire"), Is.True);
        }

        // The icon is the tempo's own sprite name and the map draws it parked while it is home.
        [Test]
        public void Map_Parks_It()
        {
            Simulation sim = Situations.Owned();
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.Machines.Any(m => m.Sprite == "tempo" && m.Parked), Is.True);
        }
    }
}
