using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Market;

namespace Sim.Core.People
{
    /// <summary>The four benefits an employer may give all its workers (spec 028 §3.8); the bits of <c>Business.Benefits</c> and the farm's.</summary>
    [System.Flags]
    public enum Benefit
    {
        None = 0,
        Meal = 1,
        Health = 2,
        Festival = 4,
        Leave = 8,
    }

    /// <summary>
    /// Benefits (spec 028 §3.8, ADR 0160): per employer (a business, or your farm for its hands), a cost a worker a season paid upfront with the wage (the rest of the season when switched on
    /// part-way; the festival bonus on the festival's day), switched off at once with no refund. They raise motivation, make a worker harder to poach, and two or more kept through a close raise
    /// your name as an employer. They never pay in output alone.
    /// </summary>
    public static class BenefitRules
    {
        public static BenefitsDef Defs(Simulation sim) { return sim.Config.Game.Benefits; }

        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.Township != null && sim.State.Businesses != null && sim.State.People != null && Town.TownshipRules.Reached(sim); }

        public static readonly Benefit[] All = { Benefit.Meal, Benefit.Health, Benefit.Festival, Benefit.Leave };

        /// <summary>The benefits the staff member's employer gives.</summary>
        public static int FlagsOf(Simulation sim, IStaff w)
        {
            if (Defs(sim) == null) return 0;
            if (w is Worker k) return sim.State.Businesses?.Find(k.Place)?.Benefits ?? 0;
            return sim.State.Township?.Exit.FarmBenefits ?? 0;
        }

        public static bool Has(Simulation sim, IStaff w, Benefit b) { return (FlagsOf(sim, w) & (int)b) != 0; }

        public static int Count(int flags)
        {
            int n = 0;
            foreach (Benefit b in All) if ((flags & (int)b) != 0) n++;
            return n;
        }

        public static int FlagsOfEmployer(Simulation sim, long employer)
        {
            if (employer == 0) return sim.State.Township?.Exit.FarmBenefits ?? 0;
            return sim.State.Businesses?.Find(employer)?.Benefits ?? 0;
        }

        /// <summary>The coins a worker a season a benefit costs, at today's prices (the festival bonus is paid on the day, not with the wage).</summary>
        public static long Cost(Simulation sim, Benefit b)
        {
            BenefitsDef d = Defs(sim);
            long c = b == Benefit.Meal ? d.MealCost : b == Benefit.Health ? d.HealthCost : b == Benefit.Festival ? d.FestivalCost : d.LeaveCost;
            return MarketRules.Indexed(sim, c);
        }

        /// <summary>Motivation the benefits add: each one's, the festival bonus's only in the season it was paid. Never over the work rules' cap.</summary>
        public static Fixed Mood(Simulation sim, IStaff w)
        {
            BenefitsDef d = Defs(sim);
            if (d == null) return Fixed.Zero;
            int f = FlagsOf(sim, w);
            if (f == 0) return Fixed.Zero;
            Fixed m = Fixed.Zero;
            if ((f & (int)Benefit.Meal) != 0) m += d.MealMotivation;
            if ((f & (int)Benefit.Health) != 0) m += d.HealthMotivation;
            if ((f & (int)Benefit.Leave) != 0) m += d.LeaveMotivation;
            if ((f & (int)Benefit.Festival) != 0 && PaidSeason(sim, w) == sim.Period + 1) m += d.FestivalMotivation;
            return m;
        }

        private static long PaidSeason(Simulation sim, IStaff w)
        {
            if (w is Worker k) return sim.State.Businesses?.Find(k.Place)?.FestivalPaid ?? 0;
            return sim.State.Township?.Exit.FarmFestivalPaid ?? 0;
        }

        /// <summary>Each benefit multiplies the chance of being poached, or of leaving unhappy, by the loyalty.</summary>
        public static Fixed Loyalty(Simulation sim, IStaff w) { return LoyaltyOf(sim, FlagsOf(sim, w)); }

        public static Fixed LoyaltyOf(Simulation sim, int flags)
        {
            BenefitsDef d = Defs(sim);
            int n = d == null ? 0 : Count(flags);
            Fixed f = Fixed.One;
            for (int i = 0; i < n; i++) f = f * d.Loyalty;
            return f;
        }

        /// <summary>Health cover halves the chance of a sick day (2% to 1% a season).</summary>
        public static Fixed SickFactor(Simulation sim, IStaff w) { return Has(sim, w, Benefit.Health) ? Defs(sim).HealthSick : Fixed.One; }

        /// <summary>Paid leave: its days are not worked, so output is that much less (4 of a season's 90 days).</summary>
        public static Fixed LeaveOutput(Simulation sim, IStaff w)
        {
            if (!Has(sim, w, Benefit.Leave)) return Fixed.One;
            BenefitsDef d = Defs(sim);
            return Fixed.One - Fixed.FromRatio(d.LeaveDays, d.LeaveOf);
        }

        // ---- who works for an employer ----

        /// <summary>Everyone the employer pays: a business's day worker, night worker and manager; your active hands.</summary>
        public static List<IStaff> Staff(Simulation sim, long employer)
        {
            var list = new List<IStaff>();
            PeopleState s = sim.State.People;
            if (s == null) return list;
            if (employer == 0) { foreach (Hand h in s.Hands) if (h.Contract == ContractState.Active) list.Add(h); }
            else foreach (Worker w in s.Workers) if (w.Place == employer) list.Add(w);
            return list;
        }

        /// <summary>What a season's benefits (festival bonus apart) cost the employer for each of its workers, from the flags.</summary>
        public static long SeasonCost(Simulation sim, int flags)
        {
            long c = 0;
            foreach (Benefit b in All) if (b != Benefit.Festival && (flags & (int)b) != 0) c += Cost(sim, b);
            return c;
        }

        // ---- paying ----

        /// <summary>Pays <paramref name="amount"/> from the employer's cash (a business draws on its overdraft; the farm uses yours); false when it can't.</summary>
        private static bool Pay(Simulation sim, long employer, long amount, string key)
        {
            if (amount <= 0) return true;
            long now = sim.State.Clock.SimTime;
            if (employer == 0)
            {
                if (!sim.State.Ledger.CanAfford(amount)) return false;
                sim.State.Ledger.Post(now, sim.Period, key, "benefits", new Posting(Account.HiredLabour, amount), new Posting(Account.Cash, -amount));
                return true;
            }
            Business.Business b = sim.State.Businesses.Find(employer);
            if (b == null || BusinessBooks.CanSpend(sim, b) < amount) return false;
            BusinessBooks.Draw(sim, b, amount);
            b.Ledger.Post(now, sim.Period, key, "business:" + b.Id, new Posting(Account.HiredLabour, amount), new Posting(Account.Cash, -amount));
            return true;
        }

        private static void SetFlags(Simulation sim, long employer, int flags)
        {
            if (employer == 0) sim.State.Township.Exit.FarmBenefits = flags;
            else sim.State.Businesses.Find(employer).Benefits = flags;
        }

        /// <summary>A switch (spec 028 §3.8): on charges the rest of the season for each worker now (the festival bonus is paid on the day); off stops it at once, no refund.</summary>
        internal static CommandResult Set(Simulation sim, long employer, Benefit benefit, bool on)
        {
            if (!On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            if (benefit == Benefit.None || !System.Array.Exists(All, x => x == benefit)) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (employer != 0)
            {
                Business.Business b = sim.State.Businesses.Find(employer);
                if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            }
            int flags = FlagsOfEmployer(sim, employer);
            bool now = (flags & (int)benefit) != 0;
            if (on == now) return CommandResult.Ok;
            if (on && benefit == Benefit.Health && !Town.ClinicRules.InPost(sim)) return CommandResult.Reject(RejectReason.NoClinic);
            if (on && benefit != Benefit.Festival)
            {
                long each = PeopleRules.RestOfSeason(sim, Cost(sim, benefit));
                if (!Pay(sim, employer, each * Staff(sim, employer).Count, "ledger.benefits")) return CommandResult.Reject(RejectReason.InsufficientFunds);
            }
            SetFlags(sim, employer, on ? flags | (int)benefit : flags & ~(int)benefit);
            sim.Emit(new BenefitChanged(sim.State.Clock.SimTime, employer, (int)benefit, on));
            return CommandResult.Ok;
        }

        /// <summary>A worker hired part-way through a season is charged the rest of it for the benefits already given.</summary>
        internal static void ChargeNew(Simulation sim, long employer)
        {
            if (Defs(sim) == null) return;
            int flags = FlagsOfEmployer(sim, employer);
            long each = PeopleRules.RestOfSeason(sim, SeasonCost(sim, flags));
            if (each > 0 && !Pay(sim, employer, each, "ledger.benefits")) SetFlags(sim, employer, flags & (int)Benefit.Festival);
        }

        /// <summary>At a renewal the season's benefits for every worker are paid with the wage; if the employer can't pay, they lapse (the festival bonus is paid only on the day).</summary>
        internal static void Renew(Simulation sim, long employer)
        {
            if (Defs(sim) == null) return;
            int flags = FlagsOfEmployer(sim, employer);
            if (flags == 0) return;
            long all = SeasonCost(sim, flags) * Staff(sim, employer).Count;
            if (!Pay(sim, employer, all, "ledger.benefits"))
            {
                SetFlags(sim, employer, flags & (int)Benefit.Festival);
                foreach (Benefit b in All) if (b != Benefit.Festival && (flags & (int)b) != 0) sim.Emit(new BenefitChanged(sim.State.Clock.SimTime, employer, (int)b, false));
            }
            if ((FlagsOfEmployer(sim, employer) & (int)Benefit.Leave) != 0)
                foreach (IStaff w in Staff(sim, employer)) Rest(w, Defs(sim).LeaveFatigue);
        }

        private static void Rest(IStaff w, Fixed by)
        {
            Fixed f = w.Fatigue - by;
            if (f < Fixed.Zero) f = Fixed.Zero;
            if (w is Hand h) h.Fatigue = f; else ((Worker)w).Fatigue = f;
        }

        /// <summary>A festival's day is over: each employer with the bonus pays each worker, and your name as an employer rises once (spec 028 §3.8).</summary>
        internal static void Festival(Simulation sim)
        {
            BenefitsDef d = Defs(sim);
            if (d == null || sim.State.People == null || sim.State.Township == null) return;
            bool paid = false;
            if ((sim.State.Township.Exit.FarmBenefits & (int)Benefit.Festival) != 0 && Pay(sim, 0, Cost(sim, Benefit.Festival) * Staff(sim, 0).Count, "ledger.festivalBonus"))
            {
                sim.State.Township.Exit.FarmFestivalPaid = sim.Period + 1;
                paid = true;
            }
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                    if (b.Status != BusinessStatus.Closed && (b.Benefits & (int)Benefit.Festival) != 0 && Pay(sim, b.Id, Cost(sim, Benefit.Festival) * Staff(sim, b.Id).Count, "ledger.festivalBonus"))
                    {
                        b.FestivalPaid = sim.Period + 1;
                        paid = true;
                    }
            if (paid) HrRules.Repute(sim, d.FestivalReputation);
        }

        /// <summary>At a season close: two or more benefits kept through it raise your name as an employer by 1 (once, however many employers).</summary>
        internal static void AtClose(Simulation sim)
        {
            BenefitsDef d = Defs(sim);
            if (d == null || sim.State.Township == null) return;
            bool kept = Count(sim.State.Township.Exit.FarmBenefits) >= d.KeptForReputation;
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed && Count(b.Benefits) >= d.KeptForReputation) kept = true;
            if (kept) HrRules.Repute(sim, 1);
        }
    }
}
