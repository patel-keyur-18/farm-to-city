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
    /// <summary>
    /// The sheet a tap opens (spec 008 §3.5): a field, unowned land or the yard. Buttons carry the command they send and, when
    /// the sim would refuse it, the reason as a sentence.
    /// </summary>
    public static class FieldSheets
    {
        public static SheetVm ForTile(ViewContext ctx, int tileId)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            Tile tile = sim.State.Land.Tiles[tileId];
            Parcel parcel = sim.State.Land.Parcels[tile.ParcelId];
            string soil = ctx.Strings.Get(defs.Soils[tile.Soil].NameKey);
            if (!parcel.Owned) return Unowned(ctx, tile, parcel);
            if (tile.Terrain == Terrain.Yard) return Yard(ctx);
            if (tile.Terrain == Terrain.Quarters) return Quarters(ctx, tile);

            var vm = new SheetVm { Icon = "field" };
            Crop crop = sim.State.Farm.CropOn(tileId);
            vm.Title = ctx.Strings.Get("field.title", "soil", soil);
            vm.Subtitle = ctx.Strings.Get(parcel.Protected ? "field.inherited" : "field.bought");
            vm.Lines.Add(new LineVm(ctx.Strings.Get("field.fertility"), ctx.Fmt.Integer(tile.Fertility.RoundToLong()) + " / 100", "fertility", tileId));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("field.moisture"), ctx.Fmt.Integer(tile.Moisture.RoundToLong()) + " / 100", "moisture", tileId));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("field.soilFactor"), ctx.Rating(LandRules.SoilFactor(sim, tile)), "soil", tileId)); // a word, not a factor (spec 008 §10)
            if (LandSystem.IsWellTile(defs, tileId)) vm.Lines.Add(new LineVm(ctx.Strings.Get("field.well"), ctx.Strings.Get("field.wellNote")));

            if (crop == null)
            {
                AddPeopleRows(ctx, vm, Sim.Core.People.OrderTargetKind.Field, tileId);
                vm.Actions.Add(ActionVm.Open("sow", ctx.Strings.Get("action.sow"), new SheetRequest("Sow", tileId)));
                if (!parcel.Protected) vm.Actions.Add(SellParcelAction(ctx, parcel));
                return vm;
            }

            CropDef def = defs.Farm.Crops[crop.Def];
            string cropName = ctx.Strings.Get(def.NameKey);
            long grace = defs.Farm.Farm.ReadyGraceSeconds;
            vm.Icon = def.Id;
            if (crop.State == CropState.Growing)
            {
                vm.Title = ctx.Strings.Get("field.growing", "crop", cropName);
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.timeLeft"), ctx.Fmt.Duration(System.Math.Max(0, crop.ReadyAt - ctx.Now)), "timeLeft", tileId));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.care"), ctx.Rating(sim.State.Farm.CareRatio), "care", tileId));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.water"), ctx.Rating(crop.WaterRate), "water", tileId));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.weather"), ctx.Rating(crop.WeatherRate), "weather", tileId));
                if (crop.Outbreak != Outbreak.None)
                    vm.Lines.Add(new LineVm(ctx.Strings.Get("field.pests"), ctx.Strings.Get(crop.Spray == SprayState.None ? "field.pestsFound" : "field.pestsSprayed")));
                else if (crop.Spray != SprayState.None) vm.Lines.Add(new LineVm(ctx.Strings.Get("field.pests"), ctx.Strings.Get("field.pestsSprayed")));
                vm.Actions.Add(ActionVm.Checked(ctx, "spray", ctx.Strings.Get("action.spray"), null, ActionKind.Primary, at => new Spray(at, tileId)));
                AddLabourRows(ctx, vm, tileId);
            }
            else
            {
                long left = crop.ReadyAt + grace - ctx.Now;
                vm.Title = ctx.Strings.Get("field.ready", "crop", cropName);
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.yield"), ctx.Fmt.Kg(crop.HarvestGrams), "yield", tileId));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.waits"),
                    left > 0 ? ctx.Strings.Get("field.spoilsIn", "time", ctx.Fmt.Duration(left)) : ctx.Strings.Get("field.spoiling"), "spoils", tileId));
                ActionVm store = ActionVm.Checked(ctx, "harvest", ctx.Strings.Get("action.harvest"), ctx.Fmt.Kg(crop.HarvestGrams), ActionKind.Primary,
                    at => new Harvest(at, tileId, Overflow.StoreOnly));
                vm.Actions.Add(store);
                if (!store.Enabled)
                    vm.Actions.Add(ActionVm.Checked(ctx, "harvestSell", ctx.Strings.Get("action.harvestSell"), null, ActionKind.Secondary,
                        at => new Harvest(at, tileId, Overflow.SellRestToTrader)));
            }
            AddPeopleRows(ctx, vm, Sim.Core.People.OrderTargetKind.Field, tileId);
            vm.Actions.Add(ActionVm.Checked(ctx, "clear", ctx.Strings.Get("action.clear"), null, ActionKind.Danger, at => new ClearField(at, tileId)));
            return vm;
        }

        private static ActionVm SellParcelAction(ViewContext ctx, Parcel parcel)
        {
            string key = ctx.Sim.Config.Game.Map.Parcels[parcel.Id].Id;
            long value = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(ctx.Sim, parcel), ctx.Sim.Config.Game.Land.QuickSaleFactor));
            return ActionVm.Checked(ctx, "sellParcel", ctx.Strings.Get("action.sellParcel"), ctx.Fmt.Coins(value), ActionKind.Danger, at => new SellParcel(at, key));
        }

        private static SheetVm Unowned(ViewContext ctx, Tile tile, Parcel parcel)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            ParcelDef def = defs.Map.Parcels[parcel.Id];
            long price = LandRules.ParcelPrice(sim, parcel);
            long resale = Money.RoundToCoin(Money.MulRate(price, defs.Land.QuickSaleFactor));
            var vm = new SheetVm { Icon = "land", Title = ctx.Strings.Get("land.title"), Subtitle = ctx.Strings.Plural("land.acres", def.Tiles.Count) };
            vm.Lines.Add(new LineVm(ctx.Strings.Get("land.price"), ctx.Fmt.Coins(price), "landPrice", tile.Id));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("land.cash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash"));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("land.resale"), ctx.Fmt.Coins(resale), "resale", tile.Id));
            vm.Actions.Add(ActionVm.Checked(ctx, "buy", ctx.Strings.Get("action.buy"), ctx.Fmt.Coins(price), ActionKind.Primary, at => new BuyParcel(at, def.Id)));
            // With a bank loan secured by the land it buys (ADR 0028, ADR 0043 §5).
            int bank = sim.State.Credit == null ? -1 : LandLoanSheet.Bank(sim);
            if (bank >= 0)
            {
                long loan = LandLoanSheet.Loan(sim, bank, price);
                // The land itself must be buyable (next to yours, …); only the cash is what the loan solves.
                RejectReason why = sim.Preview(new BuyParcel(ctx.Now, def.Id)).Reason;
                bool landOk = why == RejectReason.None || why == RejectReason.InsufficientFunds;
                bool can = landOk && loan >= defs.Credit.Lenders[bank].MinAmount;
                string reason = !landOk ? ctx.Reject(why) : can ? null : ctx.Strings.Get("land.noBankLoan");
                vm.Actions.Add(new ActionVm("buyLoan", ctx.Strings.Get("land.buyLoan"), can ? ctx.Strings.Get("land.buyLoanDetail", "down", ctx.Fmt.Coins(price - loan)) : null,
                    ActionKind.Open, can, reason, null, new SheetRequest("LandLoan", tile.Id)));
            }
            return vm;
        }

        private static SheetVm Yard(ViewContext ctx)
        {
            var vm = new SheetVm { Icon = "hut", Title = ctx.Strings.Get("yard.title"), Subtitle = ctx.Strings.Get("yard.subtitle") };
            vm.Actions.Add(ActionVm.Open("store", ctx.Strings.Get("action.openStore"), new SheetRequest("Store", 0)));
            return vm;
        }

        /// <summary>The kitchen-garden bed sheet (spec 003 §3.13).</summary>
        public static SheetVm ForBed(ViewContext ctx, long bedId)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            GardenBed bed = sim.State.Farm.Beds.Find(b => b.Id == bedId);
            var vm = new SheetVm { Icon = "garden", Title = ctx.Strings.Get("garden.title", "n", bedId.ToString()) };
            long season = defs.Farm.Farm.GardenSeasonOutputGrams;
            vm.Lines.Add(new LineVm(ctx.Strings.Get("garden.season"),
                ctx.Strings.Get("garden.seasonValue", "picked", ctx.Fmt.Kg(season - bed.RemainingGrams), "total", ctx.Fmt.Kg(season)), "gardenSeason", bedId));
            if (bed.State == BedState.Empty)
            {
                vm.Subtitle = ctx.Strings.Get(bed.RemainingGrams <= 0 ? "garden.rested" : "garden.empty");
                // One row per crop with its own Sow button (owner feedback, device run 2: the full-height sheet reads as a list).
                foreach (GardenCropDef crop in defs.Farm.GardenCrops)
                {
                    GardenCropDef c = crop;
                    var line = new LineVm(ctx.Strings.Get(c.NameKey), ctx.Strings.Get("garden.readyIn", "time", ctx.Fmt.Duration(c.GrowthMinutes * 60L)), "gardenCrop", IndexOf(defs.Farm.GardenCrops, c))
                    { Icon = ctx.Sim.Config.Game.Market.Goods[c.Good].Id };
                    line.Action = ActionVm.Checked(ctx, "sow:" + c.Id, ctx.Strings.Get("action.sow"), null, ActionKind.Primary, at => new SowGarden(at, bedId, c.Id));
                    vm.Lines.Add(line);
                }
                return vm;
            }
            GardenCropDef growing = defs.Farm.GardenCrops[bed.Crop];
            string cropName = ctx.Strings.Get(growing.NameKey);
            vm.Icon = growing.Id;
            if (bed.State == BedState.Growing)
            {
                vm.Subtitle = ctx.Strings.Get("garden.growing", "crop", cropName);
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.timeLeft"), ctx.Fmt.Duration(System.Math.Max(0, bed.ReadyAt - ctx.Now)), "bedTime", bedId));
                return vm;
            }
            vm.Subtitle = ctx.Strings.Get("garden.ready", "crop", cropName);
            long grams = System.Math.Min(growing.HarvestGrams, bed.RemainingGrams);
            vm.Actions.Add(ActionVm.Checked(ctx, "pick", ctx.Strings.Get("action.pick"), ctx.Fmt.Kg(grams), ActionKind.Primary, at => new PickGarden(at, bedId, Overflow.StoreOnly)));
            return vm;
        }

        /// <summary>The tree sheet for a slot (spec 003 §3.12): plant a sapling, or pick and remove.</summary>
        public static SheetVm ForSlot(ViewContext ctx, long slotId)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            TreeSlot slot = sim.State.Land.Slots.Find(s => s.Id == slotId);
            Tree tree = sim.State.Farm.TreeIn(slotId);
            var vm = new SheetVm { Icon = "tree", Title = ctx.Strings.Get(slot.Kind == SlotKind.Yard ? "slot.yard" : "slot.edge") };
            if (tree == null)
            {
                vm.Subtitle = ctx.Strings.Get("slot.empty");
                if (slot.Kind == SlotKind.FieldEdge) vm.Lines.Add(new LineVm(ctx.Strings.Get("slot.shade"), ctx.Strings.Get("slot.shadeNote")));
                // One row per tree: when it fruits and how long it grows, with its own Plant button and price.
                foreach (TreeDef def in defs.Farm.Trees)
                {
                    TreeDef d = def;
                    long cost = MarketRules.Value(sim, d.SaplingGood, 1, MarketRules.InputPrice(sim, d.SaplingGood));
                    var line = new LineVm(ctx.Strings.Get(d.NameKey),
                        ctx.Strings.Get("slot.treeInfo", "seasons", SeasonsText(ctx, d.FruitingSeasons), "time", ctx.Fmt.Duration(d.GrowthSeconds)), "sapling", IndexOf(defs.Farm.Trees, d))
                    { Icon = defs.Market.Goods[d.FruitGood].Id };
                    line.Action = ActionVm.Checked(ctx, "plant:" + d.Id, ctx.Strings.Get("action.plant"), ctx.Fmt.Coins(cost), ActionKind.Primary,
                        at => new PlantTree(at, slotId, d.Id));
                    vm.Lines.Add(line);
                }
                return vm;
            }

            TreeDef treeDef = defs.Farm.Trees[tree.Def];
            vm.Icon = treeDef.Id;
            vm.Title = ctx.Strings.Get(treeDef.NameKey);
            if (tree.State == TreeState.Growing)
            {
                vm.Subtitle = ctx.Strings.Get("tree.growing");
                vm.Lines.Add(new LineVm(ctx.Strings.Get("tree.matures"), ctx.Fmt.Duration(System.Math.Max(0, tree.PlantedAt + treeDef.GrowthSeconds - ctx.Now)), "treeMatures", slotId));
            }
            else
            {
                vm.Subtitle = ctx.Strings.Get(tree.RipeGrams > 0 ? "tree.ripe" : "tree.mature");
                vm.Lines.Add(new LineVm(ctx.Strings.Get("tree.fruits"), SeasonsText(ctx, treeDef.FruitingSeasons)));
            }
            AddPeopleRows(ctx, vm, Sim.Core.People.OrderTargetKind.Tree, slotId);
            vm.Lines.Add(new LineVm(ctx.Strings.Get("tree.cost"), ctx.Fmt.Coins(tree.Cost), "payback", slotId));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("tree.returns"), ctx.Fmt.Coins(tree.Returns), "payback", slotId));
            TreePayback pay = TreeRules.Payback(sim, tree);
            string payback = pay.PaidBack ? ctx.Strings.Get("tree.paidBack", "season", SeasonName(ctx, pay.Season.Value))
                : pay.Season.HasValue ? ctx.Strings.Get("tree.paysBack", "season", SeasonName(ctx, pay.Season.Value)) : ctx.Strings.Get("tree.neverPaysBack");
            vm.Lines.Add(new LineVm(ctx.Strings.Get("tree.payback"), payback, "payback", slotId));
            if (tree.RipeGrams > 0)
                vm.Actions.Add(ActionVm.Checked(ctx, "pickFruit", ctx.Strings.Get("action.pick"), ctx.Fmt.Kg(tree.RipeGrams), ActionKind.Primary,
                    at => new PickFruit(at, slotId, Overflow.StoreOnly)));
            vm.Actions.Add(ActionVm.Checked(ctx, "removeTree", ctx.Strings.Get("action.removeTree"), null, ActionKind.Danger, at => new RemoveTree(at, slotId)));
            return vm;
        }

        /// <summary>The labour quarters (ADR 0040): its hut places, who lives in each, and the button to build one.</summary>
        private static SheetVm Quarters(ViewContext ctx, Tile tile)
        {
            Simulation sim = ctx.Sim;
            Sim.Core.People.PeopleState p = sim.State.People;
            int places = sim.Config.Game.People == null ? 0 : sim.Config.Game.People.HutPlacesPerAcre;
            var vm = new SheetVm { Icon = "hand_hut", Title = ctx.Strings.Get("quarters.title"), Subtitle = ctx.Strings.Get("quarters.subtitle", "n", ctx.Fmt.Integer(places)) };
            if (p == null) return vm;
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!Sim.Core.People.PeopleRules.IsHandHut(sim, b) || b.TileId != tile.Id) continue;
                Sim.Core.People.Hand who = p.Hands.Find(h => h.HutId == b.Id);
                var line = new LineVm(ctx.Strings.Get("quarters.place", "n", ctx.Fmt.Integer(++n)),
                    who == null ? ctx.Strings.Get("people.hutEmpty") : ctx.Strings.Get("people.hutLives", "name", PeopleWords.Name(ctx, who.Name)));
                if (who != null) line.Action = ActionVm.Open("hand:" + who.Id, ctx.Strings.Get("action.see"), new SheetRequest("Hand", who.Id));
                vm.Lines.Add(line);
            }
            foreach (Sim.Core.People.Construction c in p.Constructions)
                if (c.TileId == tile.Id)
                    vm.Lines.Add(new LineVm(ctx.Strings.Get("quarters.place", "n", ctx.Fmt.Integer(++n)), ctx.Strings.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now)), "hut"));
            while (n < places) vm.Lines.Add(new LineVm(ctx.Strings.Get("quarters.place", "n", ctx.Fmt.Integer(++n)), ctx.Strings.Get("quarters.free")));
            vm.Actions.Add(ActionVm.Open("buildHut", ctx.Strings.Get("people.buildHut"), new SheetRequest("HutPlacement", 0)));
            return vm;
        }

        /// <summary>
        /// On a growing field (ADR 0048 §3): the labourers already on its crop, and a hire row for each number free today. They stay
        /// until the crop is ready, harvest it and go home; paid upfront to then.
        /// </summary>
        private static void AddLabourRows(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            Sim.Core.People.PeopleState p = sim.State.People;
            if (p == null) return;
            long until = Sim.Core.People.PeopleRules.LabourUntil(sim, tileId);
            int here = 0;
            foreach (Sim.Core.People.Neighbour n in p.Roster) if (n.Hired && n.Tile == tileId) here++;
            if (here > 0)
                vm.Lines.Add(new LineVm(ctx.Strings.Get("field.labourers"), ctx.Strings.Plural("field.labourersValue", here, "time", ctx.Fmt.Duration(System.Math.Max(0, until - ctx.Now)))));
            int free = Sim.Core.People.PeopleRules.FreeNow(sim);
            string now = PeopleWords.CareWith(ctx, Fixed.Zero);
            for (int k = 1; k <= free; k++)
            {
                int count = k;
                var line = new LineVm(ctx.Strings.Plural("field.hireN", count),
                    ctx.Strings.Get("people.carePreview", "from", now, "to", PeopleWords.CareWith(ctx, sim.Config.Game.People.Pool.HoursPerDay.MulRatio(count, 1))), "hire", tileId * 100L + count);
                line.Action = ActionVm.Checked(ctx, "hire:" + count, ctx.Strings.Get("action.hire"),
                    ctx.Fmt.Coins(Sim.Core.People.PeopleRules.LabourCost(sim, count, until)), ActionKind.Primary, at => new HireLabourers(at, tileId, count));
                vm.Lines.Add(line);
            }
        }

        /// <summary>On a field or tree: "Ramesh's orders" with the order or a button to give one (ADR 0039 §8).</summary>
        private static void AddPeopleRows(ViewContext ctx, SheetVm vm, Sim.Core.People.OrderTargetKind kind, long target)
        {
            Simulation sim = ctx.Sim;
            Sim.Core.People.PeopleState p = sim.State.People;
            if (p == null) return;
            Sim.Core.People.Hand hand = PeopleWords.OrderHand(sim, kind, target);
            if (hand == null) return;
            Sim.Core.People.StandingOrder o = p.OrderFor(kind, target);
            string name = PeopleWords.Name(ctx, hand.Name);
            var line = new LineVm(ctx.Strings.Get("field.handLooksAfter", "name", name),
                o == null ? ctx.Strings.Get("field.noOrder") : PeopleWords.Order(ctx, o) + (PeopleWords.Last(ctx, o) is string last ? "\n" + last : ""), "order", hand.Id);
            line.Action = ActionVm.Open("order", ctx.Strings.Get(o == null ? "action.giveOrder" : "action.changeOrder"),
                new SheetRequest("Order", target, kind == Sim.Core.People.OrderTargetKind.Tree ? "tree" : null));
            vm.Lines.Add(line);
        }

        internal static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], item)) return i;
            return -1;
        }

        internal static string SeasonName(ViewContext ctx, long period)
        {
            return ctx.Strings.Get("season." + ((Season)(int)(period % 4)).ToString().ToLowerInvariant());
        }

        /// <summary>"Winter", "Spring and Summer", "all seasons".</summary>
        internal static string SeasonsText(ViewContext ctx, IReadOnlyList<bool> seasons)
        {
            var names = new List<string>();
            for (int s = 0; s < 4; s++)
                if (seasons[s]) names.Add(ctx.Strings.Get("season." + ((Season)s).ToString().ToLowerInvariant()));
            if (names.Count == 4) return ctx.Strings.Get("seasons.all");
            string text = names[0];
            for (int i = 1; i < names.Count; i++)
                text = i == names.Count - 1 ? ctx.Strings.Get("seasons.list", "first", text, "second", names[i]) : text + ", " + names[i];
            return text;
        }
    }
}
