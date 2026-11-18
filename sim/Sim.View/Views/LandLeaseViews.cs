using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>Leasing land out as the player reads it (US-261, spec 024 §3.6, Preview 8): the rows, the buttons, the note on Your land and the rent's Why?.</summary>
    public static class LandLeaseViews
    {
        public const string RentWhy = "landRent";

        private static Parcel ParcelOf(Simulation sim, int tileId) { return sim.State.Land.Parcels[sim.State.Land.Tiles[tileId].ParcelId]; }

        private static string IdOf(Simulation sim, Parcel p) { return sim.Config.Game.Map.Parcels[p.Id].Id; }

        /// <summary>"in 2 days": the time to the close that starts the lease or pays the next rent.</summary>
        private static string Left(ViewContext ctx) { return ctx.Fmt.Duration(System.Math.Max(0, LandLeaseRules.NextClose(ctx.Sim) - ctx.Now)); }

        /// <summary>The rows of an acre that is leased out: the rent, when it comes next, and what the lease locks.</summary>
        public static void AddFieldRows(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            Parcel p = ParcelOf(sim, tileId);
            if (!LandLeaseRules.On(sim) || !p.Leased) return;
            var s = ctx.Strings;
            string rent = ctx.Fmt.Coins(LandLeaseRules.Rent(sim, p));
            LeaseStatus st = LandLeaseRules.StatusOf(p);
            vm.Lines.Add(new LineVm(s.Get("lease.row"), st == LeaseStatus.Starting ? s.Get("lease.fromClose") : s.Get("lease.rentValue", "rent", rent), RentWhy, p.Id));
            vm.Lines.Add(new LineVm(s.Get(st == LeaseStatus.Starting ? "lease.startNote" : st == LeaseStatus.Ending ? "lease.endingNote" : "lease.activeNote", "rent", rent), null));
            if (st != LeaseStatus.Ending) vm.Lines.Add(new LineVm(s.Get("lease.next"), s.Get("lease.nextValue", "left", Left(ctx)), RentWhy, p.Id));
            vm.Lines.Add(new LineVm(s.Get("lease.hint"), null));
        }

        /// <summary>Lease out (greyed with the reason), or end or cancel the lease.</summary>
        public static void AddActions(ViewContext ctx, SheetVm vm, int tileId)
        {
            Simulation sim = ctx.Sim;
            if (!LandLeaseRules.On(sim)) return;
            var s = ctx.Strings;
            Parcel p = ParcelOf(sim, tileId);
            string id = IdOf(sim, p);
            switch (LandLeaseRules.StatusOf(p))
            {
                case LeaseStatus.Starting:
                    vm.Actions.Add(ActionVm.Checked(ctx, "endLease", s.Get("action.cancelLease"), s.Get("action.cancelLeaseDetail"), ActionKind.Secondary, at => new EndLease(at, id)));
                    return;
                case LeaseStatus.Active:
                    vm.Actions.Add(ActionVm.Checked(ctx, "endLease", s.Get("action.endLease"), s.Get("action.endLeaseDetail"), ActionKind.Secondary, at => new EndLease(at, id)));
                    return;
                case LeaseStatus.Ending:
                    return; // ends at the close; nothing more to press
            }
            vm.Actions.Add(Lease(ctx, p, "lease"));
        }

        /// <summary>The Lease out button: checked by the sim, greyed with the reason it names.</summary>
        internal static ActionVm Lease(ViewContext ctx, Parcel p, string key)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            string id = IdOf(sim, p);
            string detail = s.Get("action.leaseDetail", "rent", ctx.Fmt.Coins(LandLeaseRules.Rent(sim, p)));
            RejectReason r = LandLeaseRules.CanLease(sim, p.Id);
            if (r == RejectReason.ParcelInUse)
            {
                string why = s.Get(LandLeaseRules.HasOrder(sim, p.Id) ? "lease.cant.order" : LandLeaseRules.HasIrrigation(sim, p.Id) ? "lease.cant.water" : "lease.cant.use");
                return new ActionVm(key, s.Get("action.lease"), detail, ActionKind.Secondary, false, why, at => new LeaseOutLand(at, id));
            }
            if (r == RejectReason.AlreadyZoned) return new ActionVm(key, s.Get("action.lease"), detail, ActionKind.Secondary, false, s.Get("lease.cant.zoned"), at => new LeaseOutLand(at, id));
            return ActionVm.Checked(ctx, key, s.Get("action.lease"), detail, ActionKind.Secondary, at => new LeaseOutLand(at, id));
        }

        /// <summary>"Loam · 3 acres": a parcel by its id.</summary>
        internal static string Label(ViewContext ctx, string parcelId)
        {
            GameDefs defs = ctx.Sim.Config.Game;
            ParcelDef d = defs.Map.Parcels[defs.Map.IndexOfParcel(parcelId)];
            string soil = ctx.Strings.Get(defs.Soils[ctx.Sim.State.Land.Tiles[d.Tiles[0]].Soil].NameKey);
            return ctx.Strings.Get("lands.parcel", "soil", soil, "acres", ctx.Strings.Plural("land.acres", d.Tiles.Count));
        }

        /// <summary>The note under a parcel on Your land while it is leased, or null.</summary>
        public static string Note(ViewContext ctx, Parcel p)
        {
            if (!p.Leased) return null;
            var s = ctx.Strings;
            string rent = ctx.Fmt.Coins(LandLeaseRules.Rent(ctx.Sim, p));
            switch (LandLeaseRules.StatusOf(p))
            {
                case LeaseStatus.Starting: return s.Get("lands.leaseStarting", "rent", rent);
                case LeaseStatus.Ending: return s.Get("lands.leaseEnding", "left", Left(ctx));
                default: return s.Get("lands.leased", "rent", rent, "left", Left(ctx));
            }
        }

        /// <summary>A parcel's buttons on Your land: Lease out, or End lease / Cancel (the sale buttons are the row's).</summary>
        public static ActionVm RowAction(ViewContext ctx, Parcel p)
        {
            Simulation sim = ctx.Sim;
            if (!LandLeaseRules.On(sim)) return null;
            var s = ctx.Strings;
            string id = IdOf(sim, p);
            switch (LandLeaseRules.StatusOf(p))
            {
                case LeaseStatus.Starting: return ActionVm.Checked(ctx, "endLease:" + id, s.Get("action.cancelLease"), null, ActionKind.Secondary, at => new EndLease(at, id));
                case LeaseStatus.Active: return ActionVm.Checked(ctx, "endLease:" + id, s.Get("action.endLease"), null, ActionKind.Secondary, at => new EndLease(at, id));
                case LeaseStatus.Ending: return null;
            }
            if (LandLeaseRules.CanLease(sim, p.Id) != RejectReason.None) return null; // the row stays quiet; the acre's own sheet greys the button with its reason
            return Lease(ctx, p, "lease:" + id);
        }

        /// <summary>The Why? of the rent, in words: what the land is worth, the part a tenant pays, that it comes upfront at the close, that the soil is left as it was.</summary>
        internal static WhyVm Why(ViewContext ctx, long parcelId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Parcel p = sim.State.Land.Parcels[(int)parcelId];
            long value = LandRules.ParcelPrice(sim, p);
            long rent = LandLeaseRules.Rent(sim, p);
            var vm = new WhyVm { Title = s.Get("why.landRent.title"), Value = ctx.Fmt.Coins(rent) };
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.worth"), ctx.Fmt.Coins(value)));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.part"), s.Get("why.landRent.partValue", "n", OneIn(ctx, LandLeaseRules.Yield(sim)))));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.so"), s.Get("why.landRent.soValue", "rent", ctx.Fmt.Coins(rent))));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.paid"), s.Get("why.landRent.paidValue")));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.soil"), s.Get("why.landRent.soilValue")));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landRent.text"), null));
            vm.Footnote = s.Get("why.landRent.lesson");
            return vm;
        }

        /// <summary>A yield as "1 coin in every 40": the whole number, so no decimals (ADR 0043).</summary>
        private static string OneIn(ViewContext ctx, Fixed yield)
        {
            return yield <= Fixed.Zero ? "0" : ctx.Fmt.Integer((Fixed.One / yield).RoundToLong());
        }
    }
}
