using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>Zoning and builders as the player reads them (US-142, spec 024 §3.3, Preview 7).</summary>
    public static class ZoningViews
    {
        public const string WaitingWhy = "familiesWaiting";

        /// <summary>The rows an empty field of yours gets in the Township: its land value and what the parcel is zoned for, with the families waiting once zoned.</summary>
        public static void AddFieldRows(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            if (!ZoningRules.On(sim)) return;
            var s = ctx.Strings;
            Tile tile = sim.State.Land.Tiles[tileId];
            Parcel parcel = sim.State.Land.Parcels[tile.ParcelId];
            LineVm value = LandValueViews.AcreRow(ctx, tileId);
            if (value != null) vm.Lines.Add(value);
            if (parcel.Protected) return; // the inherited land is never sold, so never zoned
            int acres = ZoningRules.HomeTiles(sim, parcel.Id).Count;
            bool zoned = ZoningRules.ZoneOf(parcel) == Zone.Residential;
            vm.Lines.Add(new LineVm(s.Get("zone.row"), s.Get(zoned ? "zone.houses" : "zone.farming", "acres", s.Plural("land.acres", acres)), WaitingWhy, parcel.Id));
            vm.Lines.Add(new LineVm(s.Get(zoned ? "zone.housesNote" : "zone.farmingNote"), null));
            if (zoned)
            {
                vm.Lines.Add(new LineVm(s.Get("zone.waiting"), s.Get("zone.waitingValue", "n", ctx.Fmt.Integer(TownshipRules.State(sim).TurnedAway), "of", ctx.Fmt.Integer(ZoningRules.Need(sim, parcel.Id))), WaitingWhy, parcel.Id));
                vm.Lines.Add(new LineVm(s.Get("zone.hint"), null));
            }
        }

        /// <summary>Offer to builders, or take it back to farming; greyed with the sim's reason when it can't be done.</summary>
        public static void AddFieldActions(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            if (!ZoningRules.On(sim)) return;
            var s = ctx.Strings;
            Parcel parcel = sim.State.Land.Parcels[sim.State.Land.Tiles[tileId].ParcelId];
            if (parcel.Protected) return;
            string id = sim.Config.Game.Map.Parcels[parcel.Id].Id;
            if (ZoningRules.ZoneOf(parcel) == Zone.Residential)
                vm.Actions.Add(ActionVm.Checked(ctx, "unzone", s.Get("action.unzone"), s.Get("action.unzoneDetail"), ActionKind.Secondary, at => new UnzoneParcel(at, id)));
            else
                vm.Actions.Add(ActionVm.Checked(ctx, "zone", s.Get("action.zone"), s.Get("action.zoneDetail", "price", ctx.Fmt.Coins(LandRules.ParcelPrice(sim, parcel))), ActionKind.Secondary, at => new ZoneParcel(at, id)));
        }

        /// <summary>The Builders section of the Town sheet, once there is something to say.</summary>
        public static void TownRows(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!ZoningRules.On(sim)) return;
            TownshipState t = TownshipRules.State(sim);
            if (t.BuilderHomes == 0 && t.TurnedAway == 0) return;
            var s = ctx.Strings;
            int homes = 0, families = 0;
            foreach (Building b in sim.State.Land.Buildings) if (b.Builders) homes++;
            foreach (Household x in sim.State.Citizens.Households) if (HouseholdRules.InBuildersHome(sim, x)) families++;
            vm.Lines.Add(LineVm.Header(s.Get("builders.header")));
            vm.Lines.Add(new LineVm(s.Get("builders.homes"), ctx.Fmt.Integer(homes), WaitingWhy, -1) { Icon = "family_house" });
            vm.Lines.Add(new LineVm(s.Get("builders.families"), ctx.Fmt.Integer(families), WaitingWhy, -1));
            vm.Lines.Add(new LineVm(s.Get("builders.familiesNote"), null));
            vm.Lines.Add(new LineVm(s.Get("builders.waiting"), ctx.Fmt.Integer(t.TurnedAway), WaitingWhy, -1));
        }

        /// <summary>A parcel builders bought: whose homes, and how many families live there.</summary>
        public static SheetVm BuildersAcre(ViewContext ctx, Tile tile, Parcel parcel)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ParcelDef def = sim.Config.Game.Map.Parcels[parcel.Id];
            int homes = 0, families = 0;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!b.Builders || sim.State.Land.Tiles[b.TileId].ParcelId != parcel.Id) continue;
                homes++;
                if (sim.State.Citizens.Households.Exists(x => x.HomeId == b.Id)) families++;
            }
            var vm = new SheetVm { Icon = "family_house", Title = s.Get("builders.acreTitle"), Subtitle = s.Get("builders.acreSubtitle", "acres", s.Plural("land.acres", def.Tiles.Count)) };
            Sim.Core.Town.BuildProject making = sim.State.Township.Projects.Find(x => x.Owner == Sim.Core.Town.BuildProject.Builders && x.Parcel == parcel.Id); // spec 025 §3.2
            if (making != null)
                vm.Lines.Add(new LineVm(s.Get("build.buildersStatus"), making.Working ? s.Get("build.buildingLeft", "time", ctx.Fmt.Duration(making.ReadyAt - ctx.Now)) : s.Get("build.waitingFor", "good", BuildingSheet.Good(ctx, Sim.Core.Town.ConstructionRules.Missing(sim, making))), "build", making.Id * 100 + BuildingSheet.Status));
            vm.Lines.Add(new LineVm(s.Get("builders.acreHomes"), ctx.Fmt.Integer(homes), WaitingWhy, -1));
            vm.Lines.Add(new LineVm(s.Get("builders.acreFamilies"), ctx.Fmt.Integer(families), WaitingWhy, -1));
            vm.Lines.Add(new LineVm(s.Get("builders.familiesNote"), null));
            return vm;
        }

        private static string Whole(ViewContext ctx, Fixed share) { return ctx.Fmt.Integer((share * Fixed.FromInt(100)).RoundToLong()) + "%"; }

        /// <summary>The Why? of the families waiting for a home: where they come from, what a builder needs and who gets what (id: a parcel, or −1 for the town).</summary>
        internal static WhyVm Why(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ZoningDef d = ZoningRules.Def(sim);
            TownshipState t = TownshipRules.State(sim);
            var vm = new WhyVm { Title = s.Get("why.waiting.title"), Value = ctx.Fmt.Integer(t.TurnedAway) };
            vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.so"), ctx.Fmt.Integer(t.TurnedAway)));
            vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.each"), s.Plural("why.waiting.families", d.FamiliesPerAcre)));
            if (id >= 0)
            {
                int acres = ZoningRules.HomeTiles(sim, (int)id).Count;
                vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.parcel"), s.Get("why.waiting.parcelValue", "acres", s.Plural("land.acres", acres), "n", ctx.Fmt.Integer(acres * d.FamiliesPerAcre))));
                vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.road"), s.Get(ZoningRules.Serviced(sim, (int)id) ? "why.waiting.yes" : "why.waiting.no")));
                long price = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[(int)id]);
                vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.fee"), s.Get("why.waiting.feeValue", "pct", Whole(ctx, d.PermitFee), "coins", ctx.Fmt.Coins(Money.MulRate(price, d.PermitFee)))));
            }
            else vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.fee"), s.Get("why.waiting.feeTown", "pct", Whole(ctx, d.PermitFee))));
            vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.pace"), s.Plural("why.waiting.paceValue", d.ParcelsPerSeason)));
            vm.Rows.Add(new WhyRowVm(s.Get("why.waiting.text"), null));
            vm.Footnote = s.Get("why.waiting.lesson");
            return vm;
        }
    }
}
