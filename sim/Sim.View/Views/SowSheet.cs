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

        /// <summary>This one sowing (owner, Sprint 6 device run): the expected harvest in grams, with the chosen fertiliser.</summary>
        public long HarvestGrams { get; internal set; }
        public string HarvestText { get; internal set; }

        /// <summary>What this sowing uses: its seed and fertiliser at today's input prices (from the store or bought), in money.</summary>
        public long Spend { get; internal set; }
        public string SpendText { get; internal set; }

        /// <summary>The whole harvest at the trader's price today (the floor, what a harvest reliably fetches), rounded to 10 coins.</summary>
        public long Get { get; internal set; }
        public string GetText { get; internal set; }

        /// <summary>Sells for − spend, rounded to 10 coins ("Profit about …", or "Loss about …").</summary>
        public long Profit { get; internal set; }
        public string ProfitText { get; internal set; }

        /// <summary>What the spend is made of, and the work: "seed 20 + compost 300 · 3.0 h a day".</summary>
        public string InputsText { get; internal set; }
        public string LabourText { get; internal set; }

        /// <summary>The field's fertility before and after this crop with the chosen fertiliser (ADR 0051): "Soil 45.0 → 45.1".</summary>
        public string SoilText { get; internal set; }
        public bool SoilFalls { get; internal set; }

        /// <summary>What sowing would buy now (seed and fertiliser not in the store): "pay 20 coins now" or "from your store".</summary>
        public string CostText { get; internal set; }
        public ActionVm Sow { get; internal set; }
    }

    public sealed class FertiliserVm
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }

        /// <summary>What it does, from its data: "+25% harvest · wears out the soil".</summary>
        public string EffectText { get; internal set; }
        public bool Selected { get; internal set; }
    }

    public sealed class SowSheetVm
    {
        public int TileId { get; internal set; }
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public string FreeHoursText { get; internal set; }

        /// <summary>One line on what the numbers mean: this one sowing, sold to the trader at today's price.</summary>
        public string NoteText { get; internal set; }
        public string FertiliserHeading { get; internal set; }
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
                    "fertility", ctx.Fmt.Decimal1(tile.Fertility), "moisture", ctx.Fmt.Integer(tile.Moisture.RoundToLong())),
            };
            Fixed rate = FounderRules.LabourRate(sim);
            Fixed free = rate - FarmRules.LabourDemand(sim);
            vm.FreeHoursText = ctx.Strings.Get("sow.freeHours", "free", Hours(free < Fixed.Zero ? Fixed.Zero : free), "total", Hours(rate));
            vm.NoteText = ctx.Strings.Get("sow.note");
            vm.FertiliserHeading = ctx.Strings.Get("sow.fertiliserHeading");
            foreach (FertiliserDef f in defs.Farm.Fertilisers)
                vm.Fertilisers.Add(new FertiliserVm { Id = f.Id, Name = ctx.Strings.Get("fertiliser." + f.Id), EffectText = Effect(ctx, f), Selected = f.Id == fertiliserId });

            FertiliserDef fert = defs.Farm.Fertilisers[defs.Farm.IndexOfFertiliser(fertiliserId)];
            foreach (CropDef crop in defs.Farm.Crops)
            {
                CropDef c = crop;
                bool sowable = c.SowSeasons[(int)season];
                string name = ctx.Strings.Get(c.NameKey);
                Fixed water = FarmRules.WaterFactor(defs.Farm.Farm, c, tile.Moisture);
                Suits suits = water >= Fixed.One ? Suits.Good : water >= Fixed.Parse("0.85") ? Suits.Fair : Suits.Poor;
                // This one sowing (FarmRules.Estimate, the same numbers a hand picks his crop by, ADR 0048).
                SowingEstimate e = FarmRules.Estimate(sim, tile, c, fert);
                long harvest = e.HarvestGrams;
                long get = Round10Coins(e.Get);
                long seedGrams = FarmRules.CycleGrams(sim.Clock, c, c.SeedPerAcrePerSeason);
                long fertGrams = e.FertiliserGrams;
                long seedValue = e.SeedValue;
                long fertValue = e.FertiliserValue;
                long spend = e.Spend;
                long profit = Round10Coins(get - spend);
                // Fertility after the cycle (spec 003 §3.10): the crop's use and the fertiliser's change, each × cycleShare.
                Fixed share = FarmRules.CycleShare(sim.Clock, c);
                Fixed after = tile.Fertility + (fert.FertilityPerSeason - c.FertilityPerSeason) * share;
                after = after < Fixed.Zero ? Fixed.Zero : after > Fixed.FromInt(100) ? Fixed.FromInt(100) : after;
                long buy = (seed == SeedSource.Certified ? Store.ShortfallCost(sim, c.SeedGood, seedGrams) : 0)
                    + (fert.Good < 0 ? 0 : Store.ShortfallCost(sim, fert.Good, fertGrams));
                string inputs = fert.Good < 0
                    ? ctx.Strings.Get("sow.inputsSeed", "seed", ctx.Fmt.Amount(seedValue))
                    : ctx.Strings.Get("sow.inputsBoth", "seed", ctx.Fmt.Amount(seedValue), "fertiliser", ctx.Strings.Get("fertiliser." + fert.Id), "kg", ctx.Fmt.Kg(fertGrams), "cost", ctx.Fmt.Amount(fertValue));
                var row = new CropRowVm
                {
                    CropId = c.Id, Name = name, TimeText = ctx.Fmt.Duration(c.GrowthSeconds), SowableNow = sowable,
                    SeasonNote = sowable ? null : ctx.Strings.Get("sow.seasonsOnly", "crop", name, "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons)),
                    Suits = suits, SuitsText = ctx.Strings.Get("suits." + suits.ToString().ToLowerInvariant()),
                    HarvestGrams = harvest, HarvestText = ctx.Strings.Get("sow.harvest", "kg", ctx.Fmt.Kg(harvest)),
                    Spend = spend, SpendText = ctx.Strings.Get("sow.spend", "amount", ctx.Fmt.Coins(spend)),
                    Get = get, GetText = ctx.Strings.Get("sow.get", "amount", ctx.Fmt.Coins(get)),
                    Profit = profit, ProfitText = ctx.Strings.Get(profit >= 0 ? "sow.profit" : "sow.loss", "amount", ctx.Fmt.Coins(System.Math.Abs(profit))),
                    InputsText = inputs,
                    LabourText = ctx.Strings.Get("sow.labour", "hours", Hours(c.LabourPerAcrePerDay + BucketHours(ctx, tile))),
                    SoilText = ctx.Strings.Get("sow.soil", "before", ctx.Fmt.Decimal1(tile.Fertility), "after", ctx.Fmt.Decimal1(after)),
                    SoilFalls = after < tile.Fertility && ctx.Fmt.Decimal1(after) != ctx.Fmt.Decimal1(tile.Fertility), // amber only for a fall the player can see
                    CostText = buy == 0 ? ctx.Strings.Get("sow.fromStore") : ctx.Strings.Get("sow.buys", "cost", ctx.Fmt.Coins(buy)),
                };
                row.Sow = ActionVm.Checked(ctx, "sow:" + c.Id, ctx.Strings.Get("action.sowCrop", "crop", name), row.CostText, ActionKind.Primary,
                    at => new Sow(at, tileId, c.Id, seed, fertiliserId), "crop", name, "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons));
                // An ordered field sown with another crop: the player wins, and may change the order too (ADR 0039 §6).
                Sim.Core.People.StandingOrder order = sim.State.People?.OrderFor(Sim.Core.People.OrderTargetKind.Field, tileId);
                if (order != null && order.Crop >= 0 && order.Crop != defs.Farm.IndexOfCrop(c.Id)) // the hand's choice stays his
                {
                    Sim.Core.People.StandingOrder o = order;
                    string hand = PeopleWords.Name(ctx, sim.State.People.Hand(o.HandId).Name);
                    row.Sow.Confirm = new ConfirmVm(ctx.Strings.Get("order.changeQ", "name", hand, "crop", name), ctx.Strings.Get("order.changeYes"), ctx.Strings.Get("order.changeNo"),
                        at => new Command[]
                        {
                            // The player's sowing first: the new order acts at once (ADR 0049) and would otherwise sow the field itself.
                            new Sow(at, tileId, c.Id, seed, fertiliserId),
                            new SetStandingOrder(at, o.HandId, o.Once ? OrderSpec.KeepCrop(tileId, c.Id, c.CanSaveSeed ? o.Seed : SeedSource.Certified, defs.Farm.Fertilisers[o.Fertiliser].Id, o.Sell, o.MinPrice).ThisSeasonOnly()
                                : OrderSpec.KeepCrop(tileId, c.Id, c.CanSaveSeed ? o.Seed : SeedSource.Certified, defs.Farm.Fertilisers[o.Fertiliser].Id, o.Sell, o.MinPrice)),
                        });
                }
                vm.Rows.Add(row);
            }
            return vm;
        }

        /// <summary>A fertiliser's effect from its data: how much more harvest, and whether it holds water. Its soil effect is on each crop (ADR 0051).</summary>
        private static string Effect(ViewContext ctx, FertiliserDef f)
        {
            if (f.Good < 0) return ctx.Strings.Get("fert.effectNone");
            string pct = ctx.Fmt.Integer(((f.YieldFactor - Fixed.One) * Fixed.FromInt(100)).RoundToLong());
            return ctx.Strings.Get(f.DryingFactor < Fixed.One ? "fert.effectHoldsWater" : "fert.effect", "pct", pct);
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
