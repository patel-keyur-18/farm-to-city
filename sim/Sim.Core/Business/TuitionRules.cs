using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.Core.Business
{
    /// <summary>
    /// The tuition centre (spec 028 §3.5, US-244, ADR 0161): evening classes for children of 7 to 17. Families are enrolled when the centre opens and at each season's close:
    /// a household's schooling budget (5% of its income) buys as many places as it covers, and of those a share (its stars: 40% at none, all at five) are sent, your own
    /// children first and the old village's as a head-count after them. A pupil keeps the place all season and pays a fee a day for each batch that runs; a season of
    /// tuition adds a point of farming and of crafting to a child of yours, up to ten in all. The fee a pupil pays is the one at enrolment, so a price raised mid-season
    /// waits for the next close.
    /// </summary>
    public static class TuitionRules
    {
        private const ulong TakeUpKey = 7;

        public static bool Is(Simulation sim, Business b) { return BusinessRules.TypeOf(sim, b).IsTuition; }

        public static TuitionDef Def(Simulation sim, Business b) { return BusinessRules.TypeOf(sim, b).Tuition; }

        /// <summary>Your tuition centres that stand open, in id order.</summary>
        public static List<Business> Open(Simulation sim)
        {
            var list = new List<Business>();
            if (sim.State.Businesses == null) return list;
            foreach (Business b in sim.State.Businesses.Businesses)
                if (b.Status == BusinessStatus.Open && Is(sim, b)) list.Add(b);
            return list;
        }

        /// <summary>The fee for a pupil a season at today's prices (1/100 coin) at a preset's factor.</summary>
        public static long FeeAt(Simulation sim, TuitionDef d, Fixed factor) { return Money.MulRate(MarketRules.Indexed(sim, d.Fee), factor); }

        /// <summary>The fee a pupil enrolled now pays a season: the preset chosen, at today's prices.</summary>
        public static long FeeNow(Simulation sim, Business b) { return FeeAt(sim, Def(sim, b), b.PriceFactor); }

        /// <summary>The fee a day a pupil pays at the price they enrolled at: the season's fee over its days, at the factor fixed at enrolment.</summary>
        public static long DayFee(Simulation sim, Business b) { return FeeAt(sim, Def(sim, b), b.Fee) / sim.Clock.DaysPerSeason; }

        /// <summary>Books and chalk a pupil uses a day, at today's prices.</summary>
        public static long DaySupplies(Simulation sim, Business b) { return MarketRules.Indexed(sim, Def(sim, b).SuppliesPerPupil) / sim.Clock.DaysPerSeason; }

        /// <summary>The share of the children a family can pay for who are sent, from the stars: the low end at none, the high end at five.</summary>
        public static Fixed TakeUp(TuitionDef d, Fixed stars)
        {
            Fixed s = stars < Fixed.Zero ? Fixed.Zero : stars > Fixed.FromInt(5) ? Fixed.FromInt(5) : stars;
            return d.TakeUpLow + (d.TakeUpHigh - d.TakeUpLow) * s / Fixed.FromInt(5);
        }

        public static int Pupils(Business b) { return b.Pupils.Count + b.CohortPupils; }

        /// <summary>Pupils in a batch (0-based): the batches fill in turn, yours first.</summary>
        public static int InBatch(Business b, TuitionDef d, int batch)
        {
            int n = Pupils(b) - batch * d.BatchSize;
            return n < 0 ? 0 : n > d.BatchSize ? d.BatchSize : n;
        }

        /// <summary>The hours a batch takes: the open hours split evenly.</summary>
        public static int BatchHours(Business b, TuitionDef d) { return (b.CloseHour - b.OpenHour) / d.Batches; }

        // ---- who can come ----

        /// <summary>A child of yours who may be enrolled: living, of the centre's ages and not yet at the cap. Their household's budget is checked by the caller.</summary>
        private static bool Eligible(Simulation sim, TuitionDef d, Citizen c)
        {
            if (!c.Living || c.TuitionSeasons >= d.GainMax) return false;
            int age = CitizenRules.Age(sim, c);
            return age >= d.AgeMin && age <= d.AgeMax;
        }

        /// <summary>The old village's children of the centre's ages, as a head-count: its people times the share who are children, for the years between the centre's ages out of all a child's.</summary>
        public static int CohortChildren(Simulation sim, TuitionDef d)
        {
            if (!CohortRules.Joined(sim)) return 0;
            int adult = sim.Config.Game.Population.AdultFrom, years = System.Math.Max(0, System.Math.Min(d.AgeMax, adult - 1) - d.AgeMin + 1);
            return (int)(Fixed.FromInt(CohortRules.People(sim)) * CohortRules.Def(sim).ChildShare * Fixed.FromRatio(years, adult)).FloorToLong();
        }

        /// <summary>The places the old village's households can pay for at this fee: each household's schooling budget over the fee, for every household, never more than its children.</summary>
        public static int CohortAfford(Simulation sim, TuitionDef d, long fee)
        {
            if (!CohortRules.Joined(sim) || fee <= 0) return 0;
            long places = CohortRules.BudgetOf(sim).Education / fee * CohortRules.Households(sim);
            return (int)System.Math.Min(places, CohortChildren(sim, d));
        }

        /// <summary>Every enrolled pupil of yours in any centre, so a child is in one centre only.</summary>
        private static HashSet<long> Enrolled(Simulation sim, Business except)
        {
            var set = new HashSet<long>();
            foreach (Business b in Open(sim)) if (b != except) foreach (long id in b.Pupils) set.Add(id);
            return set;
        }

        /// <summary>The old village's places already taken in other centres.</summary>
        private static int CohortTaken(Simulation sim, Business except)
        {
            int n = 0;
            foreach (Business b in Open(sim)) if (b != except) n += b.CohortPupils;
            return n;
        }

        /// <summary>
        /// Enrolment for the season at one centre (ADR 0161): children of yours in household order whose budget covers the fee (a household's budget goes down by each fee it
        /// commits, to this centre or another), each sent by a seeded draw at the stars' take-up; then the old village's, a share of what it can pay for. The fee fixes now.
        /// </summary>
        internal static void Enrol(Simulation sim, Business b)
        {
            TuitionDef d = Def(sim, b);
            b.Pupils.Clear();
            b.CohortPupils = 0;
            b.TuitionFees = 0;
            b.Fee = b.PriceFactor;
            long fee = FeeAt(sim, d, b.Fee);
            if (fee <= 0 || sim.State.Citizens == null) return;
            Fixed takeUp = TakeUp(d, b.Stars);
            long season = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime);
            HashSet<long> taken = Enrolled(sim, b);
            var budget = new Dictionary<long, long>();
            foreach (Business other in Open(sim)) // what other centres already took from a household's budget
                if (other != b) foreach (long id in other.Pupils) { Citizen c = sim.State.Citizens.Citizen(id); if (c != null) budget[c.HouseholdId] = (budget.TryGetValue(c.HouseholdId, out long v) ? v : 0) + FeeAt(sim, Def(sim, other), other.Fee); }
            foreach (Household x in sim.State.Citizens.Households)
            {
                long left = x.Schooling - (budget.TryGetValue(x.Id, out long spent) ? spent : 0);
                foreach (long m in x.Members) // ascending ids
                {
                    if (b.Pupils.Count >= d.Capacity) break;
                    if (left < fee) break;
                    Citizen c = sim.State.Citizens.Citizen(m);
                    if (c == null || !Eligible(sim, d, c) || taken.Contains(m)) continue;
                    if (!Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Citizens, TakeUpKey, (ulong)m ^ ((ulong)season << 32) ^ ((ulong)b.Id << 56)), takeUp)) continue;
                    b.Pupils.Add(m);
                    left -= fee;
                }
            }
            int room = d.Capacity - b.Pupils.Count;
            int cohort = (int)(Fixed.FromInt(System.Math.Max(0, CohortAfford(sim, d, fee) - CohortTaken(sim, b))) * takeUp).FloorToLong();
            b.CohortPupils = System.Math.Min(room, cohort);
        }

        /// <summary>The season's end for each centre: each child of yours who sat the season gains a point of farming and crafting (up to the cap), then the new season's pupils are enrolled.</summary>
        internal static void AtClose(Simulation sim)
        {
            foreach (Business b in Open(sim))
            {
                TuitionDef d = Def(sim, b);
                if (b.TuitionFees > 0)
                    foreach (long id in b.Pupils)
                    {
                        Citizen c = sim.State.Citizens?.Citizen(id);
                        if (c == null || !c.Living || c.TuitionSeasons >= d.GainMax) continue;
                        Fixed gain = Fixed.FromInt(d.GainPerSeason), max = Fixed.FromInt(100);
                        c.Skill = c.Skill + gain > max ? max : c.Skill + gain;
                        c.Crafting = c.Crafting + gain > max ? max : c.Crafting + gain;
                        c.TuitionSeasons++;
                    }
            }
            foreach (Business b in Open(sim)) Enrol(sim, b);
        }

        /// <summary>A centre has just opened: its first pupils are enrolled at once (not at the next close).</summary>
        internal static void Opened(Simulation sim, Business b)
        {
            if (Is(sim, b)) Enrol(sim, b);
        }

        /// <summary>A centre closes or is sold: its pupils go (no refund, their fees were paid by the day).</summary>
        internal static void Release(Business b)
        {
            b.Pupils.Clear();
            b.CohortPupils = 0;
            b.TuitionFees = 0;
        }

        /// <summary>Picks a fee preset for the next enrolment: the pupils in keep the fee they came at.</summary>
        internal static Commands.CommandResult SetFee(Simulation sim, long businessId, int preset)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return Commands.CommandResult.Reject(Commands.RejectReason.UnknownBusiness);
            if (!Is(sim, b)) return Commands.CommandResult.Reject(Commands.RejectReason.InvalidArgument);
            TuitionDef d = Def(sim, b);
            if (preset < 0 || preset >= d.Presets.Count) return Commands.CommandResult.Reject(Commands.RejectReason.InvalidArgument);
            b.PriceFactor = d.Presets[preset];
            return Commands.CommandResult.Ok;
        }

        /// <summary>A manager's style sets the preset nearest to its price factor (spec 028 §3.2, gap 5): what the style says about prices, in a centre's own presets.</summary>
        internal static void SetNearest(Simulation sim, Business b, Fixed factor)
        {
            TuitionDef d = Def(sim, b);
            Fixed best = d.Presets[0], gap = Fixed.FromInt(100);
            foreach (Fixed p in d.Presets)
            {
                Fixed g = p > factor ? p - factor : factor - p;
                if (g < gap) { gap = g; best = p; }
            }
            b.PriceFactor = best;
        }

        // ---- the evening ----

        /// <summary>The last hour of a batch: its pupils pay the day's fee and use the day's books. Called in each open hour; nothing on a day the centre is shut.</summary>
        internal static void Teach(Simulation sim, Business b, int hour)
        {
            TuitionDef d = Def(sim, b);
            int each = BatchHours(b, d), at = hour - b.OpenHour;
            if (each <= 0 || at < 0 || at % each != each - 1) return;
            int batch = at / each;
            b.Pupils.RemoveAll(id => sim.State.Citizens?.Citizen(id)?.Living != true); // one who has gone leaves the roll
            int n = InBatch(b, d, batch);
            if (n <= 0) return;
            long now = sim.State.Clock.SimTime, fees = DayFee(sim, b) * n, supplies = DaySupplies(sim, b) * n;
            string reference = "business:" + b.Id;
            if (fees > 0) b.Ledger.Post(now, sim.Period, "ledger.tuitionFees", reference, new Posting(Account.Cash, fees), new Posting(Account.Sales, -fees));
            b.TuitionFees += fees;
            long spend = BusinessBooks.CanSpend(sim, b);
            if (supplies > spend) supplies = System.Math.Max(0, spend);
            if (supplies <= 0) return;
            BusinessBooks.Draw(sim, b, supplies);
            b.Ledger.Post(now, sim.Period, "ledger.tuitionSupplies", reference, new Posting(Account.CostOfGoodsSold, supplies), new Posting(Account.Cash, -supplies));
        }

        /// <summary>A day's stars: they move a tenth of the way to the target, which is full batches and pupils who stayed (three fifths to two fifths).</summary>
        internal static void Morning(Simulation sim)
        {
            foreach (Business b in Open(sim))
            {
                TuitionDef d = Def(sim, b);
                int pupils = Pupils(b);
                if (pupils <= 0) continue;
                b.Stars = b.Stars + (Target(sim, b) - b.Stars).MulRatio(1, 10);
            }
        }

        /// <summary>The stars the centre is heading for: 5 × (full × 0.6 + stayed × 0.4); full is pupils over places, stayed the share of your pupils who have sat a season already.</summary>
        public static Fixed Target(Simulation sim, Business b)
        {
            TuitionDef d = Def(sim, b);
            Fixed full = Fixed.FromRatio(Pupils(b), d.Capacity);
            return Fixed.FromInt(5) * (full * Fixed.FromRatio(3, 5) + Stayed(sim, b) * Fixed.FromRatio(2, 5));
        }

        /// <summary>The share of the pupils of yours who have sat a season already (0 with no pupils of yours).</summary>
        public static Fixed Stayed(Simulation sim, Business b)
        {
            if (b.Pupils.Count == 0) return Fixed.Zero;
            int n = 0;
            foreach (long id in b.Pupils) if ((sim.State.Citizens?.Citizen(id)?.TuitionSeasons ?? 0) > 0) n++;
            return Fixed.FromRatio(n, b.Pupils.Count);
        }

        // ---- the plan ----

        /// <summary>The pupils a new centre could expect now: your children whose family can pay, and the old village's, at the starting stars' take-up, up to its places.</summary>
        public static int Expected(Simulation sim, TuitionDef d, Fixed factor)
        {
            long fee = FeeAt(sim, d, factor);
            if (fee <= 0 || sim.State.Citizens == null) return 0;
            int yours = 0;
            HashSet<long> taken = Enrolled(sim, null);
            foreach (Household x in sim.State.Citizens.Households)
            {
                long left = x.Schooling;
                foreach (long m in x.Members)
                {
                    if (left < fee) break;
                    Citizen c = sim.State.Citizens.Citizen(m);
                    if (c == null || !Eligible(sim, d, c) || taken.Contains(m)) continue;
                    yours++;
                    left -= fee;
                }
            }
            int cohort = System.Math.Max(0, CohortAfford(sim, d, fee) - CohortTaken(sim, null));
            return System.Math.Min(d.Capacity, (int)(Fixed.FromInt(yours + cohort) * TakeUp(d, d.StartStars)).FloorToLong());
        }

        /// <summary>A season of tuition at the pupils the town would send today (at the reference fee), against the wage, the books, repairs, wear and interest.</summary>
        internal static BusinessRules.Projection Projected(Simulation sim, BusinessTypeDef t, bool runYourself, long loan)
        {
            TuitionDef d = t.Tuition;
            int pupils = Expected(sim, d, Fixed.One);
            long fee = FeeAt(sim, d, Fixed.One), sales = fee * pupils, supplies = MarketRules.Indexed(sim, d.SuppliesPerPupil) * pupils;
            long repairs = Money.MulRate(MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare);
            long depreciation = MarketRules.Indexed(sim, t.BuildingValue) / t.BuildingLifeSeasons + MarketRules.Indexed(sim, t.EquipmentValue) / t.EquipmentLifeSeasons;
            long wage = runYourself ? 0 : BusinessRules.Wage(sim, t);
            long interest = loan <= 0 || sim.State.Credit == null ? 0
                : Money.MulRate(loan, Finance.CreditRules.Rate(sim.Config.Game.Credit.Lenders[sim.Config.Game.Credit.IndexOfLender(BusinessRules.Defs(sim).LenderId)], sim.State.Credit.Score));
            long costs = supplies + repairs + depreciation + wage + interest;
            long cost = BusinessRules.Cost(sim, t);
            return new BusinessRules.Projection
            {
                OwnGrams = pupils, Sales = sales, Power = supplies, Repairs = repairs, Depreciation = depreciation, Wage = wage, Interest = interest, Loan = loan, Revenue = sales, Costs = costs,
                Profit = sales - costs, Cost = cost, CashNeeded = cost + (runYourself ? supplies + repairs : wage),
            };
        }
    }
}
