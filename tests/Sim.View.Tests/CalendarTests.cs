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
            if (moisture >= 0) sim.State.Land.Tiles[86].Moisture = Fixed.FromInt(moisture);
            return sim;
        }

        [Test]
        public void CropCalendarCard()
        {
            Simulation sim = Spring(45);
            Tile20(sim);
            SowSheetVm sheet = SowSheet.Build(ViewKit2.Context(sim), 86);
            CropRowVm potato = sheet.Rows.Single(r => r.CropId == "potato"), radish = sheet.Rows.Single(r => r.CropId == "radish");
            Assert.That(sheet.Rows.Any(r => r.CropId == "wheat" || r.CropId == "rice"), Is.False, "other seasons' crops are on the crop calendar (spec 020 G22)");

            // One sowing (owner, Sprint 6): spend, sells for, profit. One potato sowing (15 h) is 15/96 of the season's; a
            // 2-hour radish is 2/96 of 16,850.
            Assert.That(potato.HarvestText + " | " + potato.SpendText + " | " + potato.GetText + " | " + potato.ProfitText,
                Is.EqualTo("Harvest about 957 kg | Spend 2,406 coins | Sells for about 5,070 coins | Profit about 2,660 coins"));
            Assert.That(radish.ProfitText, Is.EqualTo("Profit about 350 coins"));
            Assert.That(potato.Profit, Is.EqualTo(SowSheet.Round10Coins(potato.Get - potato.Spend)));
            Assert.That(radish.InputsText, Is.EqualTo("seed 50"));
            Assert.That(radish.CostText, Is.EqualTo("pay 20 coins now"), "part of the radish seed is in the store");
            Assert.That(sheet.Fertilisers.Select(f => f.Name + ": " + f.EffectText), Is.EqualTo(new[]
            {
                "Normal: No fertiliser · the harvest the soil gives",
                "Compost: +5% harvest · holds water",
                "Chemical: +25% harvest",
            }));
            // Fertility after this crop (ADR 0051): a potato takes 5 a season, 15/96 of a season here; compost puts 6 back.
            Assert.That(potato.SoilText, Is.EqualTo("Soil 45.0 → 44.2"));
            Assert.That(potato.SoilFalls, Is.True);
            Assert.That(radish.SoilText + " " + radish.SoilFalls, Is.EqualTo("Soil 45.0 → 45.0 False"), "radish takes 0.04: no fall shown");
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 86, "compost").Rows.Single(r => r.CropId == "potato").SoilText, Is.EqualTo("Soil 45.0 → 45.2"));
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 86, "chemical").Rows.Single(r => r.CropId == "potato").SoilText, Is.EqualTo("Soil 45.0 → 43.8"));
            Assert.That(potato.Suits, Is.EqualTo(Suits.Good));
            Assert.That(radish.Suits, Is.EqualTo(Suits.Good));
            Assert.That(potato.TimeText, Is.EqualTo("15 h"));
            Assert.That(radish.TimeText, Is.EqualTo("2 h"));

            // At moisture 38 both are inside neither band and rate "fair" (water 0.97).
            Simulation dry = Spring(38);
            SowSheetVm drySheet = SowSheet.Build(ViewKit2.Context(dry), 86);
            Assert.That(drySheet.Rows.Single(r => r.CropId == "potato").Suits, Is.EqualTo(Suits.Fair));
            Assert.That(drySheet.Rows.Single(r => r.CropId == "radish").Suits, Is.EqualTo(Suits.Fair));
            Assert.That(drySheet.Rows.Single(r => r.CropId == "potato").Get, Is.LessThan(potato.Get));
        }

        // Spec 020 AC-25 (moved from the sow sheet's out-of-season rows): every crop under each of its seasons and on its rung,
        // fodder only with cattle; a crop's page has a Why? for each number; Sow is still refused out of season.
        [Test]
        public void CropCalendar_EveryCrop()
        {
            Simulation sim = Spring(45);
            ViewContext ctx = ViewKit2.Context(sim);
            var farm = sim.Config.Game.Farm;
            CropCalendarVm cal = CropCalendar.Build(ctx);
            Assert.That(cal.Seasons, Is.EqualTo(new[] { "Spring", "Summer", "Monsoon", "Winter" }));
            Assert.That(cal.Now, Is.EqualTo(0));
            Assert.That(cal.NowText, Is.EqualTo("now"));
            Assert.That(cal.Rows.Select(r => r.Label), Is.EqualTo(new[] { "About 1 h", "About 2 h", "About 4 h", "6–8 h", "About 15 h" }), "no fodder row before cattle");
            Assert.That(cal.Rows.All(r => r.Cells.Count == 4), Is.True);

            // Every crop in content, under each season it's sown in and nowhere else, on the rung its growing time puts it.
            foreach (var c in farm.Crops.Where(c => !c.Fodder))
            {
                int rung = CropCalendar.Rung(ctx.Ui.CalendarRungMinutes, c.GrowthMinutes);
                for (int season = 0; season < 4; season++)
                    for (int row = 0; row < cal.Rows.Count; row++)
                        Assert.That(cal.Rows[row].Cells[season].Any(x => x.CropId == c.Id), Is.EqualTo(row == rung && c.SowSeasons[season]), c.Id + " season " + season + " row " + row);
            }
            Assert.That(cal.Rows[3].Cells[3].Select(x => x.CropId), Is.EqualTo(new[] { "wheat", "lentils", "mustard" }), "Winter's day rung, quickest first");
            Assert.That(cal.Rows[2].Cells[3].Select(x => x.CropId), Is.EqualTo(new[] { "peas" }), "peas under Winter only");
            Assert.That(cal.Rows.SelectMany(r => r.Cells.SelectMany(x => x)).Select(x => x.Open.Opens.Value.Kind).Distinct(), Is.EqualTo(new[] { CropCalendar.PageSheet }));

            // A crop's page: the same facts as content, each number with its Why? in words.
            int wheat = farm.IndexOfCrop("wheat");
            SheetVm page = CropCalendar.Page(ctx, wheat);
            Assert.That(page.Title, Is.EqualTo("Wheat"));
            Assert.That(page.Lines.Select(l => l.Label + ": " + l.Value), Is.EqualTo(new[]
            {
                "Sow in: Winter", "Grows in: 6 h", "Water it likes: Moisture 35 to 65", "Work it needs: 3.0 h a day an acre",
                "The soil: Takes 4.0 points a season", "In the store: Loses 0.5% a day", "Seed: Can be saved: loses 2.0% a season",
            }));
            for (int i = 0; i < farm.Crops.Count; i++)
                foreach (LineVm l in CropCalendar.Page(ctx, i).Lines)
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(l.Value, @"\d")) continue;
                    Assert.That(WhyView.Has(l.WhyKey), Is.True, farm.Crops[i].Id + " / " + l.Label);
                    WhyTests.AssertWords(farm.Crops[i].Id + " / " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
                }
            Assert.That(CropCalendar.Page(ctx, farm.IndexOfCrop("okra")).Lines.Single(l => l.Label == "Seed").Value, Is.EqualTo("Can't be saved: buy it each time"));
            Assert.That(CropCalendar.Page(ctx, farm.IndexOfCrop("peas")).Lines.Single(l => l.Label == "The soil").Value, Does.StartWith("Gives back"), "a legume");

            // Out of season, sowing is still refused (the rule the faded rows showed): nothing is sown from the calendar.
            Assert.That(sim.Preview(new Sim.Core.Commands.Sow(sim.State.Clock.SimTime, 86, "wheat", Sim.Core.Farm.SeedSource.Certified, "none")).Accepted, Is.False);
        }

        // Spec 020 AC-25: fodder gets its row once cattle unlock.
        [Test]
        public void CropCalendar_FodderWithCattle()
        {
            Simulation sim = CattleViewTests.Hamlet(out ViewContext ctx);
            CropCalendarVm cal = CropCalendar.Build(ctx);
            CalendarRowVm fodder = cal.Rows.Last();
            Assert.That(fodder.Fodder, Is.True);
            Assert.That(fodder.Label, Is.EqualTo("Fodder"));
            Assert.That(fodder.Cells.Select(c => string.Join(",", c.Select(x => x.CropId))), Is.EqualTo(new[] { "", "sorghum", "", "berseem" }));
            Assert.That(cal.Rows.Take(cal.Rows.Count - 1).All(r => r.Cells.All(c => c.All(x => x.CropId != "berseem" && x.CropId != "sorghum"))), Is.True);
            Assert.That(CropCalendar.Page(ctx, sim.Config.Game.Farm.IndexOfCrop("berseem")).Lines.Any(l => l.Label == "In the store"), Is.False, "fodder isn't stored");
        }

        private static void Tile20(Simulation sim)
        {
            // The example of spec 008 §3.7: a loam acre at fertility 45.
            Assert.That(sim.State.Land.Tiles[86].Fertility, Is.EqualTo(Fixed.FromInt(45)));
            Assert.That(sim.Config.Game.Soils[sim.State.Land.Tiles[86].Soil].Id, Is.EqualTo("loam"));
        }

        [Test]
        public void SowButton_SendsTheSow_AndShowsTheCost()
        {
            Simulation sim = Spring(45);
            SowSheetVm sheet = SowSheet.Build(ViewKit2.Context(sim), 86, "compost");
            CropRowVm potato = sheet.Rows.Single(r => r.CropId == "potato");
            Assert.That(potato.Sow.Enabled, Is.True);
            Assert.That(sheet.Fertilisers.Single(f => f.Selected).Id, Is.EqualTo("compost"));
            Assert.That(potato.CostText, Does.StartWith("pay").Or.EqualTo("from your store"));
            CropRowVm plain = SowSheet.Build(ViewKit2.Context(sim), 86).Rows.Single(r => r.CropId == "potato");
            Assert.That(potato.HarvestGrams, Is.GreaterThan(plain.HarvestGrams), "compost adds 5% to the harvest");
            Assert.That(potato.Spend, Is.GreaterThan(plain.Spend), "and costs its compost");
            Assert.That(potato.InputsText, Does.Contain("Compost"));
            Command c = potato.Sow.Create(sim.State.Clock.SimTime);
            Assert.That(c, Is.TypeOf<Sow>());
            Assert.That(((Sow)c).FertiliserId, Is.EqualTo("compost"));
            Assert.That(sim.Execute(c).Accepted, Is.True);
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 86).Rows.All(r => !r.Sow.Enabled), Is.True, "occupied: every sow is greyed");
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 86).Rows[0].Sow.DisabledReason, Is.EqualTo("Something is already growing here."));
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
