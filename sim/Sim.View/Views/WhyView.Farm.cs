using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The land, crop, garden and tree sources (specs 002, 003).</summary>
    public static partial class WhyView
    {
        private static WhyVm Fertility(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Tile t = TileOf(ctx, id);
            LandDef land = sim.Config.Game.Land;
            WhyVm vm = New(ctx, "why.fertility.title", Num(ctx, t.Fertility) + " / 100");
            Row(vm, ctx, "why.fertility.effect", Effect(ctx, LandRules.FertilityFactor(land, t.Fertility)));
            foreach (FertilityReason r in new[] { FertilityReason.CropUse, FertilityReason.Fertiliser, FertilityReason.Compost, FertilityReason.Lentils, FertilityReason.Fallow })
            {
                Fixed change = t.FertilityChange(r);
                if (change == Fixed.Zero) continue;
                Row(vm, ctx, "why.fertility." + r.ToString().ToLowerInvariant(), Signed(ctx, change));
            }
            if (vm.Rows.Count == 1) Text(vm, ctx, "why.fertility.noChange");
            vm.Footnote = ctx.Strings.Get("why.fertility.lesson");
            return vm;
        }

        private static string Signed(ViewContext ctx, Fixed v)
        {
            long n = v.RoundToLong();
            return n == 0 ? ctx.Strings.Get("why.same") : (n > 0 ? "+" : "−") + ctx.Fmt.Integer(System.Math.Abs(n));
        }

        private static WhyVm Moisture(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Tile t = TileOf(ctx, id);
            WhyVm vm = New(ctx, "why.moisture.title", Num(ctx, t.Moisture) + " / 100");
            string weather = ctx.Strings.Get(sim.CurrentWeather.NameKey).ToLowerInvariant();
            Fixed perHour = sim.Config.Game.Land.MoistureByWeather[sim.Weather.IndexOf(sim.CurrentWeather.Id)];
            Row(vm, ctx, perHour >= Fixed.Zero ? "why.moisture.wetter" : "why.moisture.drier", ctx.Fmt.Integer(System.Math.Abs(perHour.RoundToLong() == 0 ? 1 : perHour.RoundToLong())), "weather", weather);
            Text(vm, ctx, "why.moisture.drainage", "soil", ctx.Strings.Get(sim.Config.Game.Soils[t.Soil].NameKey).ToLowerInvariant());
            if (LandSystem.IsWellTile(sim.Config.Game, t.Id)) Row(vm, ctx, "why.moisture.well", Num(ctx, sim.Config.Game.Land.WellMoistureFloor) + " / 100");
            Crop c = CropOn(ctx, id);
            if (c != null)
            {
                CropDef def = sim.Config.Game.Farm.Crops[c.Def];
                Row(vm, ctx, "why.moisture.band", Num(ctx, def.MoistureMin) + "–" + Num(ctx, def.MoistureMax), "crop", CropName(ctx, def));
            }
            vm.Footnote = ctx.Strings.Get("why.moisture.lesson");
            return vm;
        }

        private static WhyVm Soil(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Tile t = TileOf(ctx, id);
            GameDefs defs = sim.Config.Game;
            WhyVm vm = New(ctx, "why.soil.title", ctx.Rating(LandRules.SoilFactor(sim, t)));
            Row(vm, ctx, "why.soil.type", Effect(ctx, defs.Soils[t.Soil].YieldFactor), "soil", ctx.Strings.Get(defs.Soils[t.Soil].NameKey));
            Row(vm, ctx, "why.soil.fertility", Effect(ctx, LandRules.FertilityFactor(defs.Land, t.Fertility)), "n", Num(ctx, t.Fertility));
            Row(vm, ctx, "why.soil.shade", Effect(ctx, LandRules.ShadeFactor(sim, t)));
            Row(vm, ctx, "why.soil.all", Effect(ctx, LandRules.SoilFactor(sim, t)));
            vm.Footnote = ctx.Strings.Get("why.soil.lesson");
            return vm;
        }

        private static WhyVm TimeLeft(ViewContext ctx, long id)
        {
            Crop c = CropOn(ctx, id);
            CropDef def = ctx.Sim.Config.Game.Farm.Crops[c.Def];
            WhyVm vm = New(ctx, "why.timeLeft.title", ctx.Fmt.Duration(System.Math.Max(0, c.ReadyAt - ctx.Now)), "crop", CropName(ctx, def));
            Row(vm, ctx, "why.timeLeft.grows", ctx.Fmt.Duration(def.GrowthMinutes * 60L), "crop", CropName(ctx, def));
            Row(vm, ctx, "why.timeLeft.sown", ForecastView.AtTime(ctx, c.SownAt));
            Row(vm, ctx, "why.timeLeft.ready", ForecastView.AtTime(ctx, c.ReadyAt));
            vm.Footnote = ctx.Strings.Get("why.timeLeft.lesson");
            return vm;
        }

        private static WhyVm Care(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Fixed supply = Sim.Core.People.PeopleRules.CareSupply(sim), demand = FarmRules.LabourDemand(sim);
            WhyVm vm = New(ctx, "why.care.title", ctx.Rating(sim.State.Farm.CareRatio));
            Row(vm, ctx, "why.care.need", Hours(ctx, demand));
            Row(vm, ctx, "why.care.founder", Hours(ctx, FounderRules.FieldHours(sim)));
            Fixed hired = sim.State.People == null ? Fixed.Zero : Sim.Core.People.PeopleRules.HiredHours(sim);
            if (hired > Fixed.Zero) Row(vm, ctx, "why.care.hired", Hours(ctx, hired));
            Row(vm, ctx, "why.care.effect", Effect(ctx, sim.State.Farm.CareRatio));
            if (supply > demand) Text(vm, ctx, "why.care.cap");
            vm.Footnote = ctx.Strings.Get("why.care.lesson");
            return vm;
        }

        private static string Hours(ViewContext ctx, Fixed h) { return ctx.Strings.Get("why.hoursADay", "n", ctx.Fmt.Decimal1(h)); }

        private static WhyVm Water(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Crop c = CropOn(ctx, id);
            CropDef def = sim.Config.Game.Farm.Crops[c.Def];
            Tile t = TileOf(ctx, id);
            WhyVm vm = New(ctx, "why.water.title", ctx.Rating(c.WaterRate));
            Row(vm, ctx, "why.moisture.band", Num(ctx, def.MoistureMin) + "–" + Num(ctx, def.MoistureMax), "crop", CropName(ctx, def));
            Row(vm, ctx, "why.water.now", Num(ctx, t.Moisture) + " / 100");
            Row(vm, ctx, "why.water.effect", Effect(ctx, c.WaterRate));
            vm.Footnote = ctx.Strings.Get("why.water.lesson");
            return vm;
        }

        private static WhyVm Weather(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Crop c = CropOn(ctx, id);
            WhyVm vm = New(ctx, "why.weather.title", ctx.Rating(c.WeatherRate));
            FarmDef farm = sim.Config.Game.Farm.Farm;
            for (int w = 0; w < sim.Weather.Types.Count; w++)
            {
                Fixed f = farm.WeatherYieldFactor[w];
                if (f >= Fixed.One) continue;
                Row(vm, ctx, "why.weather.hurts", Effect(ctx, f), "weather", ctx.Strings.Get(sim.Weather.Types[w].NameKey));
            }
            Row(vm, ctx, "why.weather.sofar", Effect(ctx, c.WeatherRate));
            vm.Footnote = ctx.Strings.Get("why.weather.lesson");
            return vm;
        }

        private static WhyVm Yield(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Crop c = CropOn(ctx, id);
            CropDef def = sim.Config.Game.Farm.Crops[c.Def];
            YieldBreakdown b = c.Breakdown;
            WhyVm vm = New(ctx, "why.yield.title", ctx.Fmt.Kg(c.HarvestGrams), "crop", CropName(ctx, def));
            Row(vm, ctx, "why.yield.base", Kg(ctx, b.Base), "crop", CropName(ctx, def), "season", ctx.Fmt.Integer(def.SeasonYieldPerAcre.RoundToLong()));
            Row(vm, ctx, "why.yield.soil", Effect(ctx, b.Soil));
            Row(vm, ctx, "why.yield.water", Effect(ctx, b.Water));
            Row(vm, ctx, "why.yield.care", Effect(ctx, b.Care));
            Row(vm, ctx, c.Seed == SeedSource.Certified ? "why.yield.seedCertified" : "why.yield.seedSaved", Effect(ctx, b.Seed));
            Row(vm, ctx, "why.yield.weather", Effect(ctx, b.Weather));
            Row(vm, ctx, "why.yield.tools", Effect(ctx, b.Tools));
            Row(vm, ctx, "why.yield.fertiliser", Effect(ctx, b.Fertiliser), "fert", ctx.Strings.Get("fertiliser." + sim.Config.Game.Farm.Fertilisers[c.Fertiliser].Id));
            Row(vm, ctx, "why.yield.health", Effect(ctx, b.CropHealth));
            if (b.Capped) Row(vm, ctx, "why.yield.cap", Kg(ctx, b.Base * sim.Config.Game.Farm.Farm.YieldCap));
            if (c.SpoiledGrams > 0) Row(vm, ctx, "why.yield.spoiled", "−" + ctx.Fmt.Kg(c.SpoiledGrams));
            vm.Footnote = ctx.Strings.Get("why.yield.lesson");
            return vm;
        }

        private static WhyVm Spoils(ViewContext ctx, long id)
        {
            Crop c = CropOn(ctx, id);
            long grace = ctx.Sim.Config.Game.Farm.Farm.ReadyGraceSeconds;
            long left = c.ReadyAt + grace - ctx.Now;
            WhyVm vm = New(ctx, "why.spoils.title", left > 0 ? ctx.Fmt.Duration(left) : ctx.Strings.Get("field.spoiling"));
            Row(vm, ctx, "why.spoils.grace", ctx.Fmt.Duration(grace));
            Row(vm, ctx, "why.spoils.rate", Pct(ctx, ctx.Sim.Config.Game.Market.Goods[ctx.Sim.Config.Game.Farm.Crops[c.Def].OutputGood].SpoilagePerDay));
            vm.Footnote = ctx.Strings.Get("why.spoils.lesson");
            return vm;
        }

        private static WhyVm Pests(ViewContext ctx, long id)
        {
            Crop c = CropOn(ctx, id);
            WhyVm vm = New(ctx, "why.pests.title", ctx.Strings.Get(c.Spray == SprayState.None ? "field.pestsFound" : "field.pestsSprayed"));
            Row(vm, ctx, "why.pests.repeats", ctx.Fmt.Integer(c.Repeats));
            Text(vm, ctx, "why.pests.wet");
            Text(vm, ctx, c.Spray == SprayState.None ? "why.pests.spray" : "why.pests.sprayed");
            vm.Footnote = ctx.Strings.Get("why.pests.lesson");
            return vm;
        }

        private static WhyVm LandPrice(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            Tile t = TileOf(ctx, id);
            Parcel parcel = sim.State.Land.Parcels[t.ParcelId];
            ParcelDef pd = defs.Map.Parcels[parcel.Id];
            WhyVm vm = New(ctx, "why.landPrice.title", Money(ctx, LandRules.ParcelPrice(sim, parcel)));
            Row(vm, ctx, "why.landPrice.acre", Money(ctx, MarketRules.Indexed(sim, defs.Land.AcreBasePrice)));
            Row(vm, ctx, "why.landPrice.acres", ctx.Fmt.Integer(pd.Tiles.Count));
            Row(vm, ctx, "why.landPrice.soil", Effect(ctx, defs.Soils[t.Soil].PriceFactor), "soil", ctx.Strings.Get(defs.Soils[t.Soil].NameKey));
            Row(vm, ctx, "why.landPrice.drainage", Effect(ctx, defs.Drainages[t.Drainage].PriceFactor));
            Row(vm, ctx, "why.landPrice.fertility", Effect(ctx, LandRules.FertilityPriceFactor(defs.Land, t.Fertility)), "n", Num(ctx, t.Fertility));
            Row(vm, ctx, "why.landPrice.location", Effect(ctx, pd.LocationFactor));
            vm.Footnote = ctx.Strings.Get("why.landPrice.lesson");
            return vm;
        }

        private static WhyVm Resale(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Parcel parcel = sim.State.Land.Parcels[TileOf(ctx, id).ParcelId];
            long price = LandRules.ParcelPrice(sim, parcel);
            WhyVm vm = New(ctx, "why.resale.title", Money(ctx, Sim.Core.Finance.Money.RoundToCoin(Sim.Core.Finance.Money.MulRate(price, sim.Config.Game.Land.QuickSaleFactor))));
            Row(vm, ctx, "why.resale.price", Money(ctx, price));
            Row(vm, ctx, "why.resale.quick", Effect(ctx, sim.Config.Game.Land.QuickSaleFactor));
            vm.Footnote = ctx.Strings.Get("why.resale.lesson");
            return vm;
        }

        // ---- The garden and trees ---------------------------------------------------------------------------------------------

        private static WhyVm BedTime(ViewContext ctx, long id)
        {
            GardenBed bed = ctx.Sim.State.Farm.Beds.Find(b => b.Id == id);
            GardenCropDef def = ctx.Sim.Config.Game.Farm.GardenCrops[bed.Crop];
            WhyVm vm = New(ctx, "why.timeLeft.title", ctx.Fmt.Duration(System.Math.Max(0, bed.ReadyAt - ctx.Now)), "crop", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.timeLeft.grows", ctx.Fmt.Duration(def.GrowthMinutes * 60L), "crop", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.timeLeft.ready", ForecastView.AtTime(ctx, bed.ReadyAt));
            vm.Footnote = ctx.Strings.Get("why.garden.lesson");
            return vm;
        }

        private static WhyVm GardenSeason(ViewContext ctx, long id)
        {
            GardenBed bed = ctx.Sim.State.Farm.Beds.Find(b => b.Id == id);
            long season = ctx.Sim.Config.Game.Farm.Farm.GardenSeasonOutputGrams;
            WhyVm vm = New(ctx, "why.gardenSeason.title", ctx.Fmt.Kg(season - bed.RemainingGrams));
            Row(vm, ctx, "why.gardenSeason.total", ctx.Fmt.Kg(season));
            Text(vm, ctx, "why.gardenSeason.rest");
            vm.Footnote = ctx.Strings.Get("why.garden.lesson");
            return vm;
        }

        /// <summary>A garden crop's row on the bed sheet; id = the garden crop's index.</summary>
        private static WhyVm GardenCrop(ViewContext ctx, long id)
        {
            GardenCropDef def = ctx.Sim.Config.Game.Farm.GardenCrops[(int)id];
            WhyVm vm = New(ctx, "why.gardenCrop.title", ctx.Fmt.Duration(def.GrowthMinutes * 60L), "crop", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.gardenCrop.gives", ctx.Fmt.Kg(def.HarvestGrams));
            Row(vm, ctx, "why.gardenCrop.seed", ctx.Fmt.Kg(def.SeedGramsPerSowing));
            vm.Footnote = ctx.Strings.Get("why.garden.lesson");
            return vm;
        }

        private static WhyVm TreeMatures(ViewContext ctx, long id)
        {
            Tree tree = ctx.Sim.State.Farm.Trees.Find(t => t.SlotId == id);
            TreeDef def = ctx.Sim.Config.Game.Farm.Trees[tree.Def];
            WhyVm vm = New(ctx, "why.treeMatures.title", ctx.Fmt.Duration(System.Math.Max(0, tree.PlantedAt + def.GrowthSeconds - ctx.Now)), "tree", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.treeMatures.grows", ctx.Fmt.Duration(def.GrowthSeconds), "tree", ctx.Strings.Get(def.NameKey));
            Text(vm, ctx, "why.treeMatures.fruits");
            return vm;
        }

        private static WhyVm Payback(ViewContext ctx, long id)
        {
            Tree tree = ctx.Sim.State.Farm.Trees.Find(t => t.SlotId == id);
            TreePayback p = TreeRules.Payback(ctx.Sim, tree);
            WhyVm vm = New(ctx, "why.payback.title", ctx.Strings.Get(p.PaidBack ? "why.payback.done" : "why.payback.notYet"));
            Row(vm, ctx, "why.payback.cost", Money(ctx, p.Cost));
            Row(vm, ctx, "why.payback.returns", Money(ctx, p.Returns));
            Row(vm, ctx, "why.payback.left", Money(ctx, System.Math.Max(0, p.Cost - p.Returns)));
            if (p.Season.HasValue) Row(vm, ctx, "why.payback.season", ctx.Fmt.Integer(p.Season.Value));
            vm.Footnote = ctx.Strings.Get("why.payback.lesson");
            return vm;
        }

        /// <summary>A sapling's planting row; id = the tree def's index.</summary>
        private static WhyVm Sapling(ViewContext ctx, long id)
        {
            TreeDef def = ctx.Sim.Config.Game.Farm.Trees[(int)id];
            int g = def.SaplingGood;
            WhyVm vm = New(ctx, "why.sapling.title", Money(ctx, MarketRules.Value(ctx.Sim, g, 1, MarketRules.InputPrice(ctx.Sim, g))), "tree", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.treeMatures.grows", ctx.Fmt.Duration(def.GrowthSeconds), "tree", ctx.Strings.Get(def.NameKey));
            Text(vm, ctx, "why.sapling.planting");
            vm.Footnote = ctx.Strings.Get("why.payback.lesson");
            return vm;
        }
    }
}
