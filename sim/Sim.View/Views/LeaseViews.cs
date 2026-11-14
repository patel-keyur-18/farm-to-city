using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>The ids of the renewal card's buttons (US-240, spec 014 §3.14). The platform maps them to commands.</summary>
    public static class LeaseIds
    {
        public const string Market = "lease:market", Smaller = "lease:smaller", Keep = "lease:keep", Lower = "lease:lower", Later = "lease:later", Rest = "lease:rest";

        public static LeaseChoice ChoiceOf(string id)
        {
            switch (id)
            {
                case Market: return LeaseChoice.Market;
                case Smaller: return LeaseChoice.Smaller;
                case Keep: return LeaseChoice.Keep;
                case Lower: return LeaseChoice.Lower;
                default: return LeaseChoice.None;
            }
        }
    }

    /// <summary>
    /// Rent as the player reads it (US-240): the renewal card, and the lease rows on the household sheet. A rent's parts are always shown
    /// (the kind's base × its place × the prices), so nothing about it is hidden (spec 014 §3.14).
    /// </summary>
    public static class LeaseViews
    {
        /// <summary>"2,500 base × 1.20 district road × 1.04 prices": the parts of a house's market rent.</summary>
        public static string Parts(ViewContext ctx, Household x)
        {
            Simulation sim = ctx.Sim;
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            Fixed place = LeaseRules.Place(sim, home.TileId);
            var d = sim.Config.Game.Population;
            string where = place == d.RentDistrict ? "lease.placeDistrict" : place == d.RentLane ? "lease.placeLane" : "lease.placeInner";
            Fixed shift = place > Fixed.One ? place - Fixed.One : Fixed.One - place; // words, not factors: "20% more", "15% less"
            return ctx.Strings.Get("lease.parts", "base", ctx.Fmt.Coins(d.BaseRent), "where", ctx.Strings.Get(where, "pct", ctx.Fmt.Percent(shift)),
                "prices", ctx.Fmt.Percent(sim.State.Market.PriceIndex - Fixed.One));
        }

        private static string Pct(ViewContext ctx, long now, long next)
        {
            long diff = next - now;
            return ctx.Fmt.Integer(now <= 0 ? 0 : System.Math.Abs(diff) * 100 / now);
        }

        /// <summary>The sentence under the title: what the rent is, what the market asks, and what limits a raise.</summary>
        private static string Body(ViewContext ctx, Household x, LeaseOptions o)
        {
            var s = ctx.Strings;
            string name = TownWords.Household(ctx, x), home = TownWords.Home(ctx, x.HomeId);
            string head = s.Get("lease.head", "family", name, "home", home, "rent", ctx.Fmt.Coins(o.Current));
            if (o.Allowed <= o.Current)
                return head + " " + s.Get("lease.cantBear", "market", ctx.Fmt.Coins(o.Market), "income", ctx.Fmt.Coins(o.Income), "pct", ctx.Fmt.Integer(ctx.Sim.Config.Game.Population.BurdenLimit.MulRatio(100, 1).RoundToLong()),
                    "share", ctx.Fmt.Integer(o.Income <= 0 ? 0 : o.Current * 100 / o.Income));
            string limit = o.Allowed == o.Market ? "lease.limitMarket" : o.Allowed == o.Bearable ? "lease.limitBear" : "lease.limitCap";
            return head + " " + s.Get(limit, "market", ctx.Fmt.Coins(o.Market), "most", ctx.Fmt.Coins(o.Allowed), "income", ctx.Fmt.Coins(o.Income),
                "cap", ctx.Fmt.Integer(ctx.Sim.Config.Game.Population.MaxRise.MulRatio(100, 1).RoundToLong()),
                "pct", ctx.Fmt.Integer(ctx.Sim.Config.Game.Population.BurdenLimit.MulRatio(100, 1).RoundToLong()))
                + " " + s.Get("lease.burden", "most", ctx.Fmt.Coins(o.Allowed), "share", ctx.Fmt.Integer(o.Income <= 0 ? 0 : o.Allowed * 100 / o.Income));
        }

        /// <summary>The choices a lease offers, as buttons: a label and a second line, in the order of the card.</summary>
        internal static List<CardButton> Buttons(ViewContext ctx, LeaseOptions o)
        {
            var s = ctx.Strings;
            var list = new List<CardButton>();
            bool raise = o.Allowed > o.Current;
            if (raise)
            {
                list.Add(new CardButton(LeaseIds.Market, s.Get("lease.raiseTo", "rent", ctx.Fmt.Amount(o.Allowed), "pct", Pct(ctx, o.Current, o.Allowed)), ActionKind.Primary, s.Get("lease.marketDetail")));
                if (o.Offers(LeaseChoice.Smaller))
                    list.Add(new CardButton(LeaseIds.Smaller, s.Get("lease.raiseTo", "rent", ctx.Fmt.Amount(o.Smaller), "pct", Pct(ctx, o.Current, o.Smaller)), ActionKind.Secondary, s.Get("lease.smallerDetail")));
                list.Add(new CardButton(LeaseIds.Keep, s.Get("lease.keepAt", "rent", ctx.Fmt.Amount(o.Current)), ActionKind.Secondary, s.Get("lease.keepDetail", "less", ctx.Fmt.Amount(o.Allowed - o.Current))));
            }
            else
                list.Add(new CardButton(LeaseIds.Keep, s.Get("lease.keepAt", "rent", ctx.Fmt.Amount(o.Current)), ActionKind.Primary, s.Get("lease.keepBear")));
            if (o.Lowered > 0)
                list.Add(new CardButton(LeaseIds.Lower, s.Get("lease.lowerTo", "rent", ctx.Fmt.Amount(o.Lowered), "pct", Pct(ctx, o.Current, o.Lowered)), ActionKind.Secondary, s.Get("lease.lowerDetail", "less", ctx.Fmt.Amount(o.Current - o.Lowered))));
            return list;
        }

        /// <summary>The renewal card for one lease (the preview the owner approved): null when it is no longer due. <paramref name="index"/> of <paramref name="total"/> are those asked at this close.</summary>
        public static CardVm Card(ViewContext ctx, long householdId, int index, int total)
        {
            Simulation sim = ctx.Sim;
            Household x = sim.State.Citizens?.Household(householdId);
            if (x == null || !LeaseRules.Applies(sim, x) || !LeaseRules.InLastSeason(sim, x)) return null;
            LeaseOptions o = LeaseRules.Options(sim, x);
            if (!o.CardDue) return null;
            var s = ctx.Strings;
            var c = new CardVm { Title = s.Get(total > 1 ? "lease.titleOf" : "lease.title", "family", TownWords.Household(ctx, x), "n", ctx.Fmt.Integer(index + 1), "of", ctx.Fmt.Integer(total)), Text = Body(ctx, x, o) };
            c.Buttons.AddRange(Buttons(ctx, o));
            if (total - index > 1) c.Buttons.Add(new CardButton(LeaseIds.Rest, s.Get("lease.restDefault", "n", ctx.Fmt.Integer(total - index)), ActionKind.Secondary, null));
            c.Buttons.Add(new CardButton(LeaseIds.Later, s.Get("lease.later"), ActionKind.Secondary, s.Get("lease.laterDetail")));
            return c;
        }

        /// <summary>The lease rows of the household sheet: the rent and when it ends, the parts, and, in its last season, your answer.</summary>
        public static void SheetRows(ViewContext ctx, Household x, List<LineVm> lines)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!LeaseRules.Applies(sim, x)) return;
            long left = LeaseRules.SeasonsLeft(sim, x);
            lines.Add(new LineVm(s.Get("household.lease"), s.Get(left <= 1 ? "household.leaseLast" : "household.leaseLeft", "rent", ctx.Fmt.Coins(x.LeaseRent), "n", ctx.Fmt.Integer(left)), "household.lease", x.Id));
            if (!LeaseRules.InLastSeason(sim, x)) return;
            LeaseOptions o = LeaseRules.Options(sim, x);
            if (!o.CardDue)
            {
                lines.Add(new LineVm(s.Get("household.leaseRenews"), s.Get(o.AboveMarket ? "household.leaseToMarket" : "household.leaseSame", "rent", ctx.Fmt.Coins(o.AboveMarket ? o.Market : o.Current)), "household.lease", x.Id));
                return;
            }
            LeaseChoice now = x.Renewal != LeaseChoice.None ? x.Renewal : o.Default;
            lines.Add(new LineVm(s.Get("household.leaseRenews"), s.Get(x.Renewal != LeaseChoice.None ? "household.leaseAnswered" : "household.leaseDefault", "rent", ctx.Fmt.Coins(o.Rent(now))), "household.lease", x.Id));
            foreach (CardButton b in Buttons(ctx, o))
            {
                LeaseChoice choice = LeaseIds.ChoiceOf(b.Id);
                long id = x.Id;
                var line = new LineVm(null, null) { Action = ActionVm.Checked(ctx, b.Id, b.Label, b.Detail, b.Kind == ActionKind.Primary ? ActionKind.Primary : ActionKind.Secondary, at => new RenewLease(at, id, choice)) };
                lines.Add(line);
            }
        }
    }
}
