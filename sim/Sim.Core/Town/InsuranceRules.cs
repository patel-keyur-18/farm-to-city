using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Town
{
    /// <summary>
    /// Insurance (spec 027 §3.5–§3.6, US-155): crop insurance fixed at each sowing, property and business insurance as season policies bought at the close. Insurance covers luck, not
    /// neglect: a crop's claim reads only weather, pests and events; a fire's claim is a share of the repair bill actually paid. Every premium and claim goes through the books.
    /// </summary>
    public static class InsuranceRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        public static InsuranceDef Def(Simulation sim) { return sim.Config.Game?.Insurance; }

        public static InsuranceState State(Simulation sim) { return sim.State.Township?.Insurance; }

        /// <summary>Insurance comes with the events, at the Township (§3.5).</summary>
        public static bool Available(Simulation sim) { return Def(sim) != null && State(sim) != null && TownshipRules.Reached(sim); }

        // ---- crop insurance (§3.5) ----

        /// <summary>The expected pest loss as a share of the sum insured: the crop's pest risk at sowing (its repeats, the season's usual wet share, unsprayed) × the average shortfall of an outbreak.</summary>
        public static Fixed PestPart(Simulation sim, int crop, int repeats)
        {
            CropInsuranceDef d = Def(sim).Crop;
            Fixed wet = d.WetShare[(int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock)];
            return PestRules.Risk(sim.Config.Game.Farm.Pests, crop, repeats, wet, false) * d.PestShortfall;
        }

        /// <summary>The premium share of the sum insured: (the pest part + the event part) × the loading.</summary>
        public static Fixed CropRate(Simulation sim, int crop, int repeats)
        {
            CropInsuranceDef d = Def(sim).Crop;
            return (PestPart(sim, crop, repeats) + d.EventPart) * d.Loading;
        }

        public static long CropPremium(Simulation sim, long sum, int crop, int repeats) { return Money.MulRate(sum, CropRate(sim, crop, repeats)); }

        /// <summary>At a sowing with crop insurance on: the sum insured is the sow sheet's harvest value at the trader's floor; the premium is charged from cash at once. Cash short, it goes uninsured.</summary>
        internal static void OnSown(Simulation sim, Crop c, Tile tile, CropDef crop, FertiliserDef fert)
        {
            if (!Available(sim) || !State(sim).CropOn || crop.Fodder) return;
            long sum = FarmRules.Estimate(sim, tile, crop, fert).Get;
            if (sum <= 0) return;
            long premium = CropPremium(sim, sum, c.Def, c.Repeats), now = sim.State.Clock.SimTime;
            if (premium <= 0) return;
            if (!sim.State.Ledger.CanAfford(premium))
            {
                sim.Emit(new CropNotInsured(now, c.TileId, c.Def, premium));
                return;
            }
            sim.State.Ledger.Post(now, sim.Period, "ledger.cropPremium", "crop:" + c.Id, new Posting(Account.CropInsurancePremiums, premium), new Posting(Account.Cash, -premium));
            c.Insured = true;
            c.SumInsured = sum;
            c.Premium = premium;
            State(sim).PremiumsPaid += premium;
            sim.Emit(new PremiumCharged(now, Claim.CropPolicy, 0, premium));
        }

        /// <summary>The insured yield index: weather × crop health (pests and events), the luck in the yield; soil, water, care, seed and fertiliser are never insured (§3.5).</summary>
        public static Fixed Index(Crop c) { return c.Breakdown == null ? Fixed.One : c.Breakdown.Weather * c.Breakdown.CropHealth; }

        /// <summary>At readiness: an insured crop whose index fell under the threshold pays the sum insured × the shortfall, in cash.</summary>
        internal static void CropReady(Simulation sim, Crop c)
        {
            if (!c.Insured || !Available(sim)) return;
            CropInsuranceDef d = Def(sim).Crop;
            Fixed index = Index(c);
            if (index >= d.Threshold) return;
            long pay = Money.MulRate(c.SumInsured, d.Threshold - index), now = sim.State.Clock.SimTime;
            if (pay <= 0) return;
            sim.State.Ledger.Post(now, sim.Period, "ledger.cropClaim", "crop:" + c.Id, new Posting(Account.Cash, pay), new Posting(Account.InsuranceClaims, -pay));
            int cause = Cause(sim, c);
            Paid(sim, new Claim(Claim.CropPolicy, cause, c.Def, pay, now, 0, c.SumInsured, (int)(index * Fixed.FromInt(100)).RoundToLong()));
        }

        /// <summary>What hurt the crop most: an event (its definition + 1), the pests (−1) or the season's weather (0).</summary>
        public static int Cause(Simulation sim, Crop c)
        {
            Fixed pests = PestRules.FinalHealth(sim.Config.Game.Farm.Pests, c), events = c.EventHealth, weather = c.Breakdown.Weather;
            if (events < Fixed.One && c.HitBy > 0 && events <= pests && events <= weather) return c.HitBy;
            if (pests < Fixed.One && pests <= weather) return -1;
            return 0;
        }

        // ---- property and business insurance (§3.6) ----

        /// <summary>The buildings a property policy covers: those on your own books (a builder's homes and civic buildings are not yours).</summary>
        public static List<Building> Insurable(Simulation sim)
        {
            var r = new List<Building>();
            foreach (Building b in sim.State.Land.Buildings) if (b.Owner == 0 && b.TileId >= 0 && b.Cost > 0) r.Add(b);
            return r;
        }

        public static long PropertyValue(Simulation sim)
        {
            long sum = 0;
            foreach (Building b in Insurable(sim)) sum += b.BookValue;
            return sum;
        }

        public static long PropertyPremium(Simulation sim) { return Money.MulRate(PropertyValue(sim), Def(sim).Property.Rate); }

        /// <summary>A business's building and equipment at book, from its own books.</summary>
        public static long BusinessBook(Business.Business b)
        {
            return System.Math.Max(0, b.Ledger.Balance(Account.Buildings) + b.Ledger.Balance(Account.Equipment) + b.Ledger.Balance(Account.AccumulatedDepreciation));
        }

        /// <summary>The business's net profit in its last closed season (0 before it has one).</summary>
        public static long LastProfit(Business.Business b) { return b.Reports.Count == 0 ? 0 : BusinessBooks.NetProfit(b.Reports[b.Reports.Count - 1]); }

        public static long BusinessPremium(Simulation sim, Business.Business b)
        {
            BusinessInsuranceDef d = Def(sim).Business;
            return Money.MulRate(BusinessBook(b), d.Rate) + Money.MulRate(System.Math.Max(0, LastProfit(b)), d.ProfitShare);
        }

        /// <summary>What a day shut costs a business in lost profit, and what the policy pays of it: the share of last season's profit over the season's length.</summary>
        public static long LostPaid(Simulation sim, Business.Business b, long seconds)
        {
            long profit = System.Math.Max(0, LastProfit(b));
            if (profit <= 0 || seconds <= 0) return 0;
            long perSeason = Money.MulRate(profit, Def(sim).Business.DailyProfitShare);
            return Money.MulRate(perSeason, Fixed.FromRatio((int)seconds, (int)sim.Clock.SecondsPerSeason));
        }

        internal static void SetProperty(Simulation sim, bool on) { State(sim).Property.On = on; }

        internal static void SetBusiness(Simulation sim, Business.Business b, bool on)
        {
            Policy p = on ? State(sim).BusinessPolicyOrNew(b.Id) : State(sim).BusinessPolicy(b.Id);
            if (p != null) p.On = on;
        }

        /// <summary>The next close's property premium (0 when the policy is off or nothing stands): the close's dues list shows it.</summary>
        public static long NextProperty(Simulation sim)
        {
            if (!Available(sim) || !State(sim).Property.On) return 0;
            return PropertyPremium(sim);
        }

        public static long NextBusiness(Simulation sim, Business.Business b)
        {
            if (!Available(sim) || b.Status != BusinessStatus.Open) return 0;
            Policy p = State(sim).BusinessPolicy(b.Id);
            return p == null || !p.On ? 0 : BusinessPremium(sim, b);
        }

        /// <summary>At the close: the property premium for the season that begins, from your cash; unpaid, the policy ends (never a debt).</summary>
        internal static void Close(Simulation sim)
        {
            if (!Available(sim)) return;
            InsuranceState st = State(sim);
            Policy p = st.Property;
            if (!p.On) return;
            long premium = PropertyPremium(sim), now = sim.State.Clock.SimTime;
            if (premium <= 0) { p.On = false; return; } // nothing of yours stands to insure
            if (!sim.State.Ledger.CanAfford(premium))
            {
                p.On = false;
                sim.Emit(new InsuranceLapsed(now, Claim.PropertyPolicy, 0, premium));
                return;
            }
            sim.State.Ledger.Post(now, sim.Period, "ledger.insurancePremium", "property", new Posting(Account.InsurancePremiums, premium), new Posting(Account.Cash, -premium));
            Renew(sim, p, Claim.PropertyPolicy, 0, premium, now);
        }

        /// <summary>A business sold or closed drops its policy (ADR 0154's flagged item, fixed in ADR 0158): nothing of it is kept.</summary>
        internal static void Drop(Simulation sim, long businessId) { State(sim)?.Businesses.RemoveAll(x => x.BusinessId == businessId); }

        /// <summary>At the close, after a business's wage: its premium from its own cash, or the policy ends.</summary>
        internal static void CloseBusiness(Simulation sim, Business.Business b)
        {
            if (!Available(sim)) return;
            Policy p = State(sim).BusinessPolicy(b.Id);
            if (p == null || !p.On) return;
            long now = sim.State.Clock.SimTime;
            if (b.Status != BusinessStatus.Open)
            {
                if (b.Status == BusinessStatus.Closed) p.On = false;
                return;
            }
            long premium = BusinessPremium(sim, b);
            if (premium > 0 && BusinessBooks.CanSpend(sim, b) < premium)
            {
                p.On = false;
                sim.Emit(new InsuranceLapsed(now, Claim.BusinessPolicy, b.Id, premium));
                return;
            }
            if (premium > 0)
            {
                BusinessBooks.Draw(sim, b, premium);
                b.Ledger.Post(now, sim.Period, "ledger.insurancePremium", "business:" + b.Id, new Posting(Account.InsurancePremiums, premium), new Posting(Account.Cash, -premium));
            }
            Renew(sim, p, Claim.BusinessPolicy, b.Id, premium, now);
        }

        private static void Renew(Simulation sim, Policy p, int policy, long business, long premium, long now)
        {
            bool continued = p.PaidSeason == sim.Period - 1; // covered last season too: the cover has run unbroken
            p.PaidSeason = sim.Period;
            if (!continued)
            {
                p.Since = now;
                sim.Emit(new InsuranceStarted(now, policy, business));
            }
            State(sim).PremiumsPaid += premium;
            sim.Emit(new PremiumCharged(now, policy, business, premium));
        }

        // ---- claims on a fire and a pause ----

        /// <summary>Whether a fire on this building or business is insured: the policy covers the season now and its cover began no later than the warning (a risk already signalled is not insurable).</summary>
        internal static bool CoversFire(Simulation sim, bool business, long target, long warnedAt)
        {
            if (!Available(sim)) return false;
            Policy p = business ? State(sim).BusinessPolicy(target) : State(sim).Property;
            return p != null && p.Covers(sim.Period) && p.Since <= warnedAt;
        }

        /// <summary>
        /// A fire's repair was paid (the repair card's answer, at once or slowly): an insured fire pays back the share of what was actually paid, and a business also its lost days
        /// (a share of last season's profit for each day shut) into its own books.
        /// </summary>
        internal static void OnRepair(Simulation sim, EventsState s, PendingEvent p, int choice, long paid, long now)
        {
            if (!s.FireInsured || !Available(sim)) return;
            InsuranceDef d = Def(sim);
            bool business = p.Target2 == 1;
            Split(sim, p, choice, paid, out long repair, out long lost, out int days);
            Business.Business b = business ? sim.State.Businesses?.Find(p.Target) : null;
            if (repair > 0) sim.State.Ledger.Post(now, sim.Period, "ledger.fireClaim", "event:fire", new Posting(Account.Cash, repair), new Posting(Account.InsuranceClaimsReceived, -repair));
            if (lost > 0 && b != null) b.Ledger.Post(now, sim.Period, "ledger.lostDaysClaim", "business:" + b.Id, new Posting(Account.Cash, lost), new Posting(Account.InsuranceClaimsReceived, -lost));
            else lost = 0;
            if (repair + lost <= 0) return;
            int cause = EventRules.Def(sim).IndexOf(EventsDef.Fire) + 1;
            Paid(sim, new Claim(business ? Claim.BusinessPolicy : Claim.PropertyPolicy, cause, p.Target, repair + lost, now, days, paid, (int)((Fixed.One - (business ? d.Business.Excess : d.Property.Excess)) * Fixed.FromInt(100)).RoundToLong()));
        }

        /// <summary>What a fire's repair cost you in the end: what was paid, less the repair share an insured fire pays back (the lost days of a business are its own books' and not in it).</summary>
        internal static long NetOfClaim(Simulation sim, EventsState s, PendingEvent p, int choice, long paid)
        {
            if (paid <= 0 || !s.FireInsured || !Available(sim)) return paid;
            Split(sim, p, choice, paid, out long repair, out _, out _);
            return System.Math.Max(0, paid - repair);
        }

        /// <summary>What a repair that cost <paramref name="paid"/> pays back: the share of the bill, and for a business its lost days (a share of last season's profit for each day shut).</summary>
        private static void Split(Simulation sim, PendingEvent p, int choice, long paid, out long repair, out long lost, out int days)
        {
            InsuranceDef d = Def(sim);
            bool business = p.Target2 == 1;
            repair = Money.MulRate(paid, Fixed.One - (business ? d.Business.Excess : d.Property.Excess));
            lost = 0;
            days = 0;
            Business.Business b = business ? sim.State.Businesses?.Find(p.Target) : null;
            if (b == null) return;
            FireDef f = EventRules.Def(sim).FireDef;
            days = choice == 1 ? f.SlowRepairDays : f.DowntimeDays;
            lost = LostPaid(sim, b, days * Day);
        }

        /// <summary>What the insurance would pay back if this fire-repair choice were taken now (0 when the fire is not insured): the repair card says so.</summary>
        public static long RepairPays(Simulation sim, PendingEvent p, int choice)
        {
            EventsState s = EventRules.State(sim);
            if (s == null || !s.FireInsured || !Available(sim)) return 0;
            Split(sim, p, choice, EventRules.ChoiceCost(sim, p.Def, choice, p), out long repair, out long lost, out _);
            return repair + lost;
        }

        /// <summary>A supplier strike's <em>Pause the line</em>: the policy pays its share of the lost profit for the hours shut (not a risk signalled before the cover began).</summary>
        internal static void OnPause(Simulation sim, PendingEvent p, int hours, long now)
        {
            if (!Available(sim) || p.Target2 != 0) return;
            Business.Business b = sim.State.Businesses?.Find(p.Target);
            Policy pol = State(sim).BusinessPolicy(p.Target);
            if (b == null || pol == null || !pol.Covers(sim.Period) || pol.Since > p.RaisedAt) return;
            long lost = LostPaid(sim, b, hours * 3600L);
            if (lost <= 0) return;
            b.Ledger.Post(now, sim.Period, "ledger.lostDaysClaim", "business:" + b.Id, new Posting(Account.Cash, lost), new Posting(Account.InsuranceClaimsReceived, -lost));
            Paid(sim, new Claim(Claim.BusinessPolicy, EventRules.Def(sim).IndexOf("supplier_strike") + 1, b.Id, lost, now, (hours + 23) / 24, System.Math.Max(0, LastProfit(b)), (int)(Def(sim).Business.DailyProfitShare * Fixed.FromInt(100)).RoundToLong()));
        }

        private static void Paid(Simulation sim, Claim c)
        {
            InsuranceState st = State(sim);
            st.Claims.Add(c);
            while (st.Claims.Count > Def(sim).KeepClaims) st.Claims.RemoveAt(0);
            st.ClaimsReceived += c.Amount;
            sim.Emit(new ClaimPaid(c.At, c.Policy, c.Cause, c.Target, c.Amount, c.Days));
        }
    }
}
