using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-6 and §3.7: the crop-calendar card on the sow sheet.</summary>
    public class CalendarTests
    {
        private static Simulation Spring(long moisture = -1)
        {
            // No inflation, so the year-0 prices of the spec's example hold at any time in the season.
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "market.json" ? j.Replace("\"inflationMinPerYear\": 0.02, \"inflationMaxPerYear\": 0.06", "\"inflationMinPerYear\": 0, \"inflationMaxPerYear\": 0") : j);
            if (moisture >= 0) sim.State.Land.Tiles[20].Moisture = Fixed.FromInt(moisture);
            return sim;
        }

        [Test]
        public void CropCalendarCard()
        {
            Simulation sim = Spring(45);
            Tile20(sim);
            SowSheetVm sheet = SowSheet.Build(ViewKit2.Context(sim), 20);
            CropRowVm potato = sheet.Rows.Single(r => r.CropId == "potato"), radish = sheet.Rows.Single(r => r.CropId == "radish"), wheat = sheet.Rows.Single(r => r.CropId == "wheat");

            Assert.That(potato.SowableNow, Is.True);
            // One sowing (owner, Sprint 6): spend, sells for, profit. One potato sowing is a quarter of the season's 17,060; a
            // 2-hour radish is 2/96 of 16,850.
            Assert.That(potato.HarvestText + " | " + potato.SpendText + " | " + potato.GetText + " | " + potato.ProfitText,
                Is.EqualTo("Harvest about 1,531 kg | Spend 3,850 coins | Sells for about 8,120 coins | Profit about 4,270 coins"));
            Assert.That(radish.ProfitText, Is.EqualTo("Profit about 350 coins"));
            Assert.That(potato.Profit, Is.EqualTo(SowSheet.Round10Coins(potato.Get - potato.Spend)));
            Assert.That(radish.InputsText, Is.EqualTo("seed 50"));
            Assert.That(radish.CostText, Is.EqualTo("pay 20 coins now"), "part of the radish seed is in the store");
            Assert.That(sheet.Fertilisers.Select(f => f.Name + ": " + f.EffectText), Is.EqualTo(new[]
            {
                "Normal: No fertiliser · the harvest the soil gives",
                "Compost: +5% harvest · builds up the soil",
                "Chemical: +25% harvest · wears out the soil",
            }));
            Assert.That(potato.Suits, Is.EqualTo(Suits.Good));
            Assert.That(radish.Suits, Is.EqualTo(Suits.Good));
            Assert.That(potato.TimeText, Is.EqualTo("1 d"));
            Assert.That(radish.TimeText, Is.EqualTo("2 h"));

            Assert.That(wheat.SowableNow, Is.False);
            Assert.That(wheat.SeasonNote, Is.EqualTo("Wheat: Winter only"));
            Assert.That(wheat.Sow.Enabled, Is.False);
            Assert.That(wheat.Sow.DisabledReason, Is.EqualTo("Wheat can be sown in Winter only."));
            Assert.That(sheet.Rows.Single(r => r.CropId == "rice").SeasonNote, Is.EqualTo("Rice: Summer and Monsoon only"));

            // At moisture 38 both are inside neither band and rate "fair" (water 0.97).
            Simulation dry = Spring(38);
            SowSheetVm drySheet = SowSheet.Build(ViewKit2.Context(dry), 20);
            Assert.That(drySheet.Rows.Single(r => r.CropId == "potato").Suits, Is.EqualTo(Suits.Fair));
            Assert.That(drySheet.Rows.Single(r => r.CropId == "radish").Suits, Is.EqualTo(Suits.Fair));
            Assert.That(drySheet.Rows.Single(r => r.CropId == "potato").Get, Is.LessThan(potato.Get));
        }

        private static void Tile20(Simulation sim)
        {
            // The example of spec 008 §3.7: a loam acre at fertility 45.
            Assert.That(sim.State.Land.Tiles[20].Fertility, Is.EqualTo(Fixed.FromInt(45)));
            Assert.That(sim.Config.Game.Soils[sim.State.Land.Tiles[20].Soil].Id, Is.EqualTo("loam"));
        }

        [Test]
        public void SowButton_SendsTheSow_AndShowsTheCost()
        {
            Simulation sim = Spring(45);
            SowSheetVm sheet = SowSheet.Build(ViewKit2.Context(sim), 20, "compost");
            CropRowVm potato = sheet.Rows.Single(r => r.CropId == "potato");
            Assert.That(potato.Sow.Enabled, Is.True);
            Assert.That(sheet.Fertilisers.Single(f => f.Selected).Id, Is.EqualTo("compost"));
            Assert.That(potato.CostText, Does.StartWith("pay").Or.EqualTo("from your store"));
            CropRowVm plain = SowSheet.Build(ViewKit2.Context(sim), 20).Rows.Single(r => r.CropId == "potato");
            Assert.That(potato.HarvestGrams, Is.GreaterThan(plain.HarvestGrams), "compost adds 5% to the harvest");
            Assert.That(potato.Spend, Is.GreaterThan(plain.Spend), "and costs its compost");
            Assert.That(potato.InputsText, Does.Contain("Compost"));
            Command c = potato.Sow.Create(sim.State.Clock.SimTime);
            Assert.That(c, Is.TypeOf<Sow>());
            Assert.That(((Sow)c).FertiliserId, Is.EqualTo("compost"));
            Assert.That(sim.Execute(c).Accepted, Is.True);
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 20).Rows.All(r => !r.Sow.Enabled), Is.True, "occupied: every sow is greyed");
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 20).Rows[0].Sow.DisabledReason, Is.EqualTo("Something is already growing here."));
        }

        [Test]
        public void Round10Coins()
        {
            Assert.That(SowSheet.Round10Coins(1_706_250), Is.EqualTo(1_706_000), "17,062.50 → 17,060");
            Assert.That(SowSheet.Round10Coins(1_706_500), Is.EqualTo(1_707_000), "ties away from zero");
            Assert.That(SowSheet.Round10Coins(-1_706_500), Is.EqualTo(-1_707_000));
            Assert.That(SowSheet.Round10Coins(499), Is.EqualTo(0));
        }
    }
}
