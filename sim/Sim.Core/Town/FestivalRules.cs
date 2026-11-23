using System;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Town
{
    /// <summary>
    /// Festivals (US-149, spec 026 §3.1): one a season, at the community hall (caretaker in post) or the banyan square, paid by the treasury or as your gift, on one of the
    /// next three days (a whole real day from 0:00, so a long absence never skips it). On the day households are happier, most of all within the venue's reach, and the shops
    /// sell more. The day is a plain fact of the clock: nothing runs to start it, and the next day it is gone.
    /// </summary>
    public static class FestivalRules
    {
        public const string HeldJob = "festival.held";

        public static FestivalDef Def(Simulation sim) { return TownLifeRules.Def(sim)?.Festival; }

        /// <summary>From the Township, when the content has festivals.</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && TownLifeRules.On(sim); }

        public static TownLifeState State(Simulation sim) { return TownLifeRules.State(sim); }

        /// <summary>The kind this season's festival is: the one for the season it is now.</summary>
        public static int KindNow(Simulation sim) { return (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock); }

        public static long SeasonNow(Simulation sim) { return sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason; }

        public static long Today(Simulation sim) { return sim.State.Clock.SimTime / ClockDef.SecondsPerDay; }

        /// <summary>One festival is scheduled or held a season, counted from the season the day was chosen in.</summary>
        public static bool DoneThisSeason(Simulation sim) { return State(sim).FestivalSeason == SeasonNow(sim); }

        /// <summary>Your town's people: your households' members and the old village's.</summary>
        public static int People(Simulation sim)
        {
            int n = 0;
            foreach (Household x in TownLifeRules.Yours(sim)) n += x.Members.Count;
            return n + CohortRules.People(sim);
        }

        /// <summary><c>perPerson</c> × your town's people × the price index (§3.1).</summary>
        public static long Cost(Simulation sim, int kind)
        {
            return MarketRules.Indexed(sim, Def(sim).Kinds[kind].PerPerson * Money.Coin * People(sim));
        }

        public static int Tile(Simulation sim, int venue)
        {
            return venue == FestivalDef.Hall ? CivicRules.Def(sim, CivicDef.Hall).Tile : sim.Config.Game.Township.LandValue.SquareTile;
        }

        public static int Reach(Simulation sim, int venue)
        {
            return venue == FestivalDef.Hall ? CivicRules.Def(sim, CivicDef.Hall).Reach : Def(sim).SquareReach;
        }

        /// <summary>The hall needs its caretaker; the square is always there.</summary>
        public static bool VenueReady(Simulation sim, int venue) { return venue == FestivalDef.Square || (CivicRules.On(sim) && CivicRules.Staffed(sim, CivicDef.Hall)); }

        /// <summary>Why a festival can't be scheduled now, or None.</summary>
        public static RejectReason CanSchedule(Simulation sim, int venue, int payer, int daysAhead)
        {
            if (!On(sim)) return RejectReason.NoTownship;
            if (venue != FestivalDef.Hall && venue != FestivalDef.Square || payer != FestivalDef.Town && payer != FestivalDef.You) return RejectReason.InvalidArgument;
            if (daysAhead < 1 || daysAhead > Def(sim).DaysAhead) return RejectReason.InvalidArgument;
            if (DoneThisSeason(sim)) return RejectReason.FestivalThisSeason;
            if (!VenueReady(sim, venue)) return RejectReason.VenueNotReady;
            long cost = Cost(sim, KindNow(sim));
            bool afford = payer == FestivalDef.Town ? TreasuryRules.IsOpen(sim) && sim.State.Treasury.Ledger.CanAfford(cost) : sim.State.Ledger.CanAfford(cost);
            return afford ? RejectReason.None : RejectReason.InsufficientFunds;
        }

        internal static RejectReason Schedule(Simulation sim, int venue, int payer, int daysAhead)
        {
            RejectReason r = CanSchedule(sim, venue, payer, daysAhead);
            if (r != RejectReason.None) return r;
            TownLifeState s = State(sim);
            long now = sim.State.Clock.SimTime;
            int kind = KindNow(sim);
            long cost = Cost(sim, kind);
            if (payer == FestivalDef.Town)
                sim.State.Treasury.Ledger.Post(now, sim.Period, "ledger.townFestival", "festival", new Posting(Account.Festivals, cost), new Posting(Account.Cash, -cost));
            else
                sim.State.Ledger.Post(now, sim.Period, "ledger.festivalGift", "festival", new Posting(Account.GiftsToVillage, cost), new Posting(Account.Cash, -cost));
            s.FestivalSeason = SeasonNow(sim);
            s.FestivalOn = true;
            s.FestivalKind = kind;
            s.FestivalVenue = venue;
            s.FestivalPayer = payer;
            s.FestivalDay = Today(sim) + daysAhead;
            s.FestivalCost = cost;
            s.FestivalPeople = People(sim);
            s.FestivalJobId = sim.ScheduleJob(HeldJob, 0, (s.FestivalDay + 1) * ClockDef.SecondsPerDay).Id;
            sim.Emit(new FestivalScheduled(now, kind, venue, payer, s.FestivalDay, cost));
            return RejectReason.None;
        }

        /// <summary>Today is the festival's day.</summary>
        public static bool IsDay(Simulation sim)
        {
            if (!On(sim)) return false;
            TownLifeState s = State(sim);
            return s.FestivalOn && s.FestivalDay == Today(sim);
        }

        /// <summary>
        /// A festival's day is a day of rest (owner, 2026-11-20): nobody works. Today is the festival day: hands, labourers and your own day labour stop, nobody earns outside
        /// wages, and businesses make nothing, except the stalls (the vegetable stall, the tea stall, the sweet shop), whose keepers sell to the crowd.
        /// </summary>
        public static bool Resting(Simulation sim) { return IsDay(sim); }

        /// <summary>Whether the day with this index (a day count) was a festival's, whether or not its record has been written yet.</summary>
        public static bool WasFestival(Simulation sim, long day)
        {
            if (!On(sim)) return false;
            TownLifeState s = State(sim);
            if (s.FestivalOn && s.FestivalDay == day) return true;
            foreach (FestivalRecord f in s.Festivals) if (f.Day == day) return true;
            return false;
        }

        /// <summary>The business doesn't work in the hour that has just ended because it was a festival's day and it is not a stall.</summary>
        public static bool ClosedForFestival(Simulation sim, BusinessTypeDef type, long hourStart)
        {
            return type.Kind == ShopKind.None && WasFestival(sim, hourStart / ClockDef.SecondsPerDay);
        }

        /// <summary>Happiness points a household gets today: inside the venue's reach, or elsewhere in the town (§3.1).</summary>
        public static int Lift(Simulation sim, Household x)
        {
            if (!IsDay(sim)) return 0;
            TownLifeState s = State(sim);
            FestivalDef d = Def(sim);
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            return b != null && b.TileId >= 0 && CoverageRules.Within(sim, b.TileId, Tile(sim, s.FestivalVenue), Reach(sim, s.FestivalVenue)) ? d.InReach : d.Outside;
        }

        /// <summary>The shops' demand rise on the day for a kind of shop (§3.1), zero otherwise.</summary>
        public static Fixed ShopRise(Simulation sim, ShopKind kind)
        {
            if (!IsDay(sim)) return Fixed.Zero;
            FestivalDef d = Def(sim);
            return kind == ShopKind.Chai ? d.ShopChai : kind == ShopKind.Sweets || kind == ShopKind.Meals ? d.ShopSweets : d.ShopVeg; // a feast day fills the restaurant as it fills the sweet shop
        }

        /// <summary>The festival's day is over: kept in the record (the last few) and announced.</summary>
        internal sealed class Held : IJobHandler
        {
            public string JobType { get { return HeldJob; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Job job)
            {
                TownLifeState s = State(sim);
                sim.CompleteJob(job);
                if (s == null || !s.FestivalOn) return;
                s.FestivalOn = false;
                s.FestivalJobId = 0;
                s.Festivals.Add(new FestivalRecord(s.FestivalKind, s.FestivalVenue, s.FestivalPayer, s.FestivalDay, s.FestivalPeople, s.FestivalCost));
                while (s.Festivals.Count > Def(sim).Keep) s.Festivals.RemoveAt(0);
                sim.Emit(new FestivalHeld(sim.State.Clock.SimTime, s.FestivalKind, s.FestivalVenue, s.FestivalPayer, s.FestivalDay, s.FestivalCost));
                Sim.Core.People.BenefitRules.Festival(sim); // spec 028 §3.8: the festival bonus, where it is given
            }
        }
    }
}
