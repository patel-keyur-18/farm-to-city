using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>How well a crop suits the field's moisture now: the water factor is 1.00 (good), at least 0.85 (fair), or less (poor).</summary>
    public enum Suits
    {
        Good = 0,
        Fair = 1,
        Poor = 2,
    }

    /// <summary>One crop on the sow sheet and the crop-calendar card (spec 008 §3.7).</summary>
    public sealed class CropRowVm
    {
        public string CropId { get; internal set; }
        public string Name { get; internal set; }
        public string TimeText { get; internal set; }
        public bool SowableNow { get; internal set; }

        /// <summary>"Winter only" when it can't be sown this season, else null.</summary>
        public string SeasonNote { get; internal set; }
        public Suits Suits { get; internal set; }
        public string SuitsText { get; internal set; }

        /// <summary>Money per acre per season at the trader's floor, minus certified seed, rounded to 10 coins.</summary>
        public long Earns { get; internal set; }
        public string EarnsText { get; internal set; }
        public string LabourText { get; internal set; }

        /// <summary>What sowing would buy now (seed and fertiliser not in the store).</summary>
        public string CostText { get; internal set; }
        public ActionVm Sow { get; internal set; }
    }

    public sealed class FertiliserVm
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public bool Selected { get; internal set; }
    }

    public sealed class SowSheetVm
    {
        public int TileId { get; internal set; }
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public string FreeHoursText { get; internal set; }
        public List<FertiliserVm> Fertilisers { get; } = new List<FertiliserVm>();
        public List<CropRowVm> Rows { get; } = new List<CropRowVm>();
    }

    /// <summary>The sow sheet with the crop-calendar card (spec 008 §3.5, §3.7).</summary>
    public static class SowSheet
    {
        public static SowSheetVm Build(ViewContext ctx, int tileId, string fertiliserId = "none", SeedSource seed = SeedSource.Certified)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            Tile tile = sim.State.Land.Tiles[tileId];
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            var vm = new SowSheetVm
            {
                TileId = tileId,
                Title = ctx.Strings.Get("sow.title", "soil", ctx.Strings.Get(defs.Soils[tile.Soil].NameKey)),
                Subtitle = ctx.Strings.Get("sow.subtitle", "season", ctx.Strings.Get("season." + season.ToString().ToLowerInvariant()),
                    "fertility", ctx.Fmt.Integer(tile.Fertility.RoundToLong()), "moisture", ctx.Fmt.Integer(tile.Moisture.RoundToLong())),
            };
            Fixed rate = FounderRules.LabourRate(sim);
            Fixed free = rate - FarmRules.LabourDemand(sim);
            vm.FreeHoursText = ctx.Strings.Get("sow.freeHours", "free", Hours(free < Fixed.Zero ? Fixed.Zero : free), "total", Hours(rate));
            foreach (FertiliserDef f in defs.Farm.Fertilisers)
                vm.Fertilisers.Add(new FertiliserVm { Id = f.Id, Name = ctx.Strings.Get("fertiliser." + f.Id), Selected = f.Id == fertiliserId });

            Fixed soilFactor = LandRules.SoilFactor(sim, tile);
            FertiliserDef fert = defs.Farm.Fertilisers[defs.Farm.IndexOfFertiliser(fertiliserId)];
            foreach (CropDef crop in defs.Farm.Crops)
            {
                CropDef c = crop;
                bool sowable = c.SowSeasons[(int)season];
                string name = ctx.Strings.Get(c.NameKey);
                Fixed water = FarmRules.WaterFactor(defs.Farm.Farm, c, tile.Moisture);
                Suits suits = water >= Fixed.One ? Suits.Good : water >= Fixed.Parse("0.85") ? Suits.Fair : Suits.Poor;
                Fixed floor = MarketRules.Floor(sim, c.OutputGood);
                long gross = MarketRules.Value(sim, c.OutputGood, (c.SeasonYieldPerAcre * soilFactor * water).MulRatio(1000, 1).RoundToLong(), floor);
                long seedCost = MarketRules.Value(sim, c.SeedGood, c.SeedPerAcrePerSeason.MulRatio(1000, 1).RoundToLong(), MarketRules.InputPrice(sim, c.SeedGood));
                long earns = Round10Coins(gross - seedCost);
                long seedGrams = FarmRules.CycleGrams(sim.Clock, c, c.SeedPerAcrePerSeason);
                long fertGrams = fert.Good < 0 ? 0 : FarmRules.CycleGrams(sim.Clock, c, fert.KgPerAcrePerSeason);
                long buy = (seed == SeedSource.Certified ? Store.ShortfallCost(sim, c.SeedGood, seedGrams) : 0)
                    + (fert.Good < 0 ? 0 : Store.ShortfallCost(sim, fert.Good, fertGrams));
                var row = new CropRowVm
                {
                    CropId = c.Id, Name = name, TimeText = ctx.Fmt.Duration(c.GrowthSeconds), SowableNow = sowable,
                    SeasonNote = sowable ? null : ctx.Strings.Get("sow.seasonsOnly", "crop", name, "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons)),
                    Suits = suits, SuitsText = ctx.Strings.Get("suits." + suits.ToString().ToLowerInvariant()),
                    Earns = earns, EarnsText = ctx.Strings.Get("sow.earns", "amount", ctx.Fmt.Amount(earns)),
                    LabourText = ctx.Strings.Get("sow.labour", "hours", Hours(c.LabourPerAcrePerDay + BucketHours(ctx, tile))),
                    CostText = buy == 0 ? ctx.Strings.Get("sow.fromStore") : ctx.Strings.Get("sow.buys", "cost", ctx.Fmt.Coins(buy)),
                };
                row.Sow = ActionVm.Checked(ctx, "sow:" + c.Id, ctx.Strings.Get("action.sowCrop", "crop", name), row.CostText, ActionKind.Primary,
                    at => new Sow(at, tileId, c.Id, seed, fertiliserId), "crop", name, "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons));
                // An ordered field sown with another crop: the player wins, and may change the order too (ADR 0039 §6).
                Sim.Core.People.StandingOrder order = sim.State.People?.OrderFor(Sim.Core.People.OrderTargetKind.Field, tileId);
                if (order != null && order.Crop != defs.Farm.IndexOfCrop(c.Id))
                {
                    Sim.Core.People.StandingOrder o = order;
                    string hand = PeopleWords.Name(ctx, sim.State.People.Hand(o.HandId).Name);
                    row.Sow.Confirm = new ConfirmVm(ctx.Strings.Get("order.changeQ", "name", hand, "crop", name), ctx.Strings.Get("order.changeYes"), ctx.Strings.Get("order.changeNo"),
                        at => new Command[]
                        {
                            new SetStandingOrder(at, o.HandId, o.Once ? OrderSpec.KeepCrop(tileId, c.Id, c.CanSaveSeed ? o.Seed : SeedSource.Certified, defs.Farm.Fertilisers[o.Fertiliser].Id, o.Sell, o.MinPrice).ThisSeasonOnly()
                                : OrderSpec.KeepCrop(tileId, c.Id, c.CanSaveSeed ? o.Seed : SeedSource.Certified, defs.Farm.Fertilisers[o.Fertiliser].Id, o.Sell, o.MinPrice)),
                            new Sow(at, tileId, c.Id, seed, fertiliserId),
                        });
                }
                vm.Rows.Add(row);
            }
            return vm;
        }

        /// <summary>Bucket-carrying hours on a well acre on a dry day (spec 003 §3.6): 24 × the well's lift × bucketHoursPerMoisturePoint.</summary>
        private static Fixed BucketHours(ViewContext ctx, Tile tile)
        {
            return Fixed.FromInt(24) * tile.WellLift * ctx.Sim.Config.Game.Farm.Farm.BucketHoursPerMoisturePoint;
        }


        /// <summary>One decimal: 3.5.</summary>
        private static string Hours(Fixed h)
        {
            long tenths = (h * Fixed.FromInt(10)).RoundToLong();
            return (tenths / 10) + "." + (System.Math.Abs(tenths) % 10);
        }

        /// <summary>Rounded to 10 coins, ties away from zero.</summary>
        public static long Round10Coins(long money)
        {
            long unit = 10 * Money.Coin;
            long abs = System.Math.Abs(money);
            long rounded = (abs + unit / 2) / unit * unit;
            return money < 0 ? -rounded : rounded;
        }
    }
}
