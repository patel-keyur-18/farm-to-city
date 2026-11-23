using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Business
{
    /// <summary>
    /// The night shift (spec 028 §3.8, ADR 0160): a second worker for the mill, bakery, dairy and sweet shop works 20:00 to 04:00 on the town's grid, at the role's wage plus a quarter, and is offered
    /// only after the day shift could not keep up with the day's plan on 3 of the last 8 days. It adds hours, not demand: a plan the day shift already meets gains nothing.
    /// </summary>
    public static class NightRules
    {
        public static BenefitsDef Defs(Simulation sim) { return sim.Config.Game.Benefits; }

        /// <summary>The mill and the businesses that make by recipe (bakery, dairy, sweet shop).</summary>
        public static bool Eligible(BusinessTypeDef t) { return !t.IsKiln && (t.ByRecipe || t.Input >= 0); }

        public static bool Grid(Simulation sim) { return Town.UtilityRules.Built(sim, UtilityDef.Grid); }

        public static Worker Of(Simulation sim, Business b) { return sim.State.People?.NightAt(b.Id); }

        /// <summary>Whether the night shift works: its worker is hired and the grid is on.</summary>
        public static bool Active(Simulation sim, Business b) { return Defs(sim) != null && Of(sim, b) != null && Grid(sim); }

        /// <summary>Whether the hour of day is one of the night's (20:00 to 04:00).</summary>
        public static bool NightHour(Simulation sim, int hour) { BenefitsDef d = Defs(sim); return hour >= d.NightStart || hour < d.NightEnd; }

        // ---- when it is offered ----

        /// <summary>The days (of the last <c>nightLookBack</c>) the day shift could not keep up with the day's plan.</summary>
        public static int ShortCount(Simulation sim, Business b)
        {
            BenefitsDef d = Defs(sim);
            int mask = (1 << d.NightLookBack) - 1, n = 0;
            for (int bits = b.ShortDays & mask; bits != 0; bits &= bits - 1) n++;
            return n;
        }

        public static bool Offered(Simulation sim, Business b) { return ShortCount(sim, b) >= Defs(sim).NightNeedDays; }

        /// <summary>At the end of the day shift: did the plan go unmet (a recipe's mix not made, the mill's own wheat not ground)? Kept for the last days.</summary>
        internal static void EndOfDay(Simulation sim, Business b, BusinessTypeDef t, long day)
        {
            if (Defs(sim) == null || !Eligible(t) || b.ShortDay == day + 1) return;
            bool unmet = false;
            if (t.ByRecipe) { foreach (int p in t.Products) if (b.Mix[p] > 0 && b.MadeToday[p] < b.Mix[p]) unmet = true; }
            else unmet = b.OwnPerDay > b.OwnToday && sim.State.Market.Stock[t.Input] > 0;
            b.ShortDay = day + 1;
            b.ShortDays = ((b.ShortDays << 1) | (unmet ? 1 : 0)) & 0xFFFF;
        }

        // ---- starting and stopping ----

        /// <summary>The wage the night worker asks: the role's wage at skill 50 plus the night premium, never under the minimum wage.</summary>
        public static long Wage(Simulation sim, Business b)
        {
            long market = HiringRules.MarketWage(sim, b.Id);
            long asked = PeopleRules.AskedWage(sim, market, Fixed.FromInt(50), false);
            return Town.PolicyRules.WageFloor(sim, Money.RoundToCoin(Money.MulRate(asked, Fixed.One + Defs(sim).NightWageUp)), market);
        }

        public static RejectReason CanStart(Simulation sim, Business b)
        {
            if (Defs(sim) == null || sim.State.Township == null || !Town.TownshipRules.Reached(sim)) return RejectReason.NoTownship;
            if (b == null || b.Status == BusinessStatus.Closed) return RejectReason.UnknownBusiness;
            if (b.Status != BusinessStatus.Open) return RejectReason.NotOpen;
            if (!Eligible(BusinessRules.TypeOf(sim, b))) return RejectReason.NoNightShift;
            if (Of(sim, b) != null) return RejectReason.NightState;
            if (sim.State.People?.StaffAt(b.Id) == null) return RejectReason.NoWorker;
            if (!Grid(sim)) return RejectReason.NoGrid;
            if (!Offered(sim, b)) return RejectReason.NightNotOffered;
            return RejectReason.None;
        }

        /// <summary>Hires the night worker: a commuter of the role at skill 50, the rest of the season paid now from the business's cash.</summary>
        internal static CommandResult Start(Simulation sim, long businessId)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            RejectReason r = CanStart(sim, b);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            PeopleState s = sim.State.People;
            long now = sim.State.Clock.SimTime, wage = Wage(sim, b), prepay = PeopleRules.RestOfSeason(sim, wage);
            if (BusinessBooks.CanSpend(sim, b) < prepay) return CommandResult.Reject(RejectReason.InsufficientFunds);
            int steady = sim.Config.Game.People.IndexOfTrait("steady");
            int names = sim.Config.Game.People.Names.Count;
            int name = (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Exit, (ulong)b.Id | (1UL << 41), (ulong)(now / ClockDef.SecondsPerDay)), (ulong)names);
            var w = new Worker { Id = s.NextWorkerId++, Place = b.Id, Name = name, Skill = Fixed.FromInt(50), Trait1 = steady, Trait2 = steady, Wage = wage, HiredAt = now, Night = true };
            s.Workers.Add(w);
            BusinessBooks.Draw(sim, b, prepay);
            BusinessRules.PayWage(sim, b, prepay);
            BenefitRules.ChargeNew(sim, b.Id);
            sim.Emit(new NightShiftChanged(now, b.Id, true));
            return CommandResult.Ok;
        }

        internal static CommandResult Stop(Simulation sim, long businessId)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (Of(sim, b) == null) return CommandResult.Reject(RejectReason.NightState);
            Remove(sim, b, true);
            return CommandResult.Ok;
        }

        /// <summary>The night worker goes: severance (10% of the wage after a first season) from the business's cash. No emit when the business is closing.</summary>
        internal static long Remove(Simulation sim, Business b, bool announce)
        {
            Worker w = Of(sim, b);
            if (w == null) return 0;
            long paid = 0;
            if (w.SeasonsWorked > 0)
            {
                long sev = System.Math.Min(Money.MulRate(w.Wage, sim.Config.Game.People.Hand.SeveranceShare), BusinessBooks.CanSpend(sim, b));
                if (sev > 0 && BusinessBooks.Draw(sim, b, sev))
                {
                    b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.severance", "business:" + b.Id, new Posting(Account.HiredLabour, sev), new Posting(Account.Cash, -sev));
                    paid = sev;
                }
            }
            HrRules.Gone(sim, w);
            sim.State.People.Workers.Remove(w);
            if (announce) sim.Emit(new NightShiftChanged(sim.State.Clock.SimTime, b.Id, false));
            return paid;
        }

        /// <summary>At a renewal the night worker's new season is paid upfront like the day worker's; if the business can't pay, the shift ends.</summary>
        internal static void Renew(Simulation sim, Business b)
        {
            Worker w = Of(sim, b);
            if (w == null) return;
            long wage = System.Math.Max(Wage(sim, b), 0);
            w.Wage = wage;
            if (BusinessBooks.CanSpend(sim, b) >= wage)
            {
                BusinessBooks.Draw(sim, b, wage);
                BusinessRules.PayWage(sim, b, wage);
                w.SeasonsWorked++;
            }
            else Remove(sim, b, true);
        }
    }
}
