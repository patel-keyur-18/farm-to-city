using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>Households' budgets by Engel's law, food met, savings and health (spec 014 §3.4–3.6).</summary>
    public static class HouseholdRules
    {
        public static bool HasChildren(Simulation sim, Household x)
        {
            foreach (long id in x.Members) if (IsChild(sim, sim.State.Citizens.Citizen(id))) return true;
            return false;
        }

        public static bool IsChild(Simulation sim, Citizen c) { return CitizenRules.Age(sim, c) < sim.Config.Game.Population.AdultFrom; }

        public static bool IsElder(Simulation sim, Citizen c) { return CitizenRules.Age(sim, c) >= sim.Config.Game.Population.ElderFrom; }

        /// <summary>An adult of working age with no job in town: works outside as a day labourer (§3.6).</summary>
        public static bool WorksOutside(Simulation sim, Citizen c)
        {
            return c.Living && c.Job != CitizenJob.Hand && c.Job != CitizenJob.Business && c.Job != CitizenJob.Retired && !IsChild(sim, c) && !IsElder(sim, c);
        }

        /// <summary>Adult equivalents (§3.5): adult 1.0, child 0.6, elder 0.8.</summary>
        public static Fixed AdultEquivalents(Simulation sim, Household x)
        {
            PopulationDefs p = sim.Config.Game.Population;
            Fixed ae = Fixed.Zero;
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                ae += IsChild(sim, c) ? p.ChildEquivalent : IsElder(sim, c) ? p.ElderEquivalent : Fixed.One;
            }
            return ae;
        }

        /// <summary>Adult equivalents a household's pupils don't need fed at home: the free meals' share of each child's food (spec 022 §3.5).</summary>
        public static Fixed MealsEaten(Simulation sim, Household x)
        {
            Fixed share = Town.PolicyRules.MealShare(sim);
            if (share == Fixed.Zero) return Fixed.Zero;
            int pupils = 0;
            foreach (long id in x.Members) if (SchoolRules.IsPupil(sim, sim.State.Citizens.Citizen(id))) pupils++;
            return sim.Config.Game.Population.ChildEquivalent * share * Fixed.FromInt(pupils);
        }

        /// <summary>A season's expected outside earnings for one adult (§3.6): the founder's day-labour rate × 8 h × index × days × share.</summary>
        public static long OutsideSeasonPay(Simulation sim)
        {
            long day = MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour * 8);
            long days = sim.Clock.SecondsPerSeason / ClockDef.SecondsPerDay;
            return Money.MulRate(day * days, sim.Config.Game.Population.OutsideDaysShare);
        }

        /// <summary>A hand's hut, where his family isn't: he sends half his wage home (§3.4).</summary>
        public static bool InHut(Simulation sim, Household x)
        {
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            return b != null && PeopleRules.IsHandHut(sim, b);
        }

        /// <summary>The household lives in a builder's home (spec 024 §3.3): a resident of the town, not one of your households; its rent is the builder's.</summary>
        public static bool InBuildersHome(Simulation sim, Household x)
        {
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            return b != null && b.Builders;
        }

        /// <summary>A family house the founder rents out (§3.4).</summary>
        public static bool InFamilyHouse(Simulation sim, Household x)
        {
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            return b != null && b.Def == sim.Config.Game.Population.House;
        }

        /// <summary>
        /// The season's income (§3.5): each hand's season wage (paid upfront, spec 006), less half sent home from a hut, plus the expected
        /// outside earnings of every other working-age adult.
        /// </summary>
        public static long Income(Simulation sim, Household x) { return Income(sim, x, out _); }

        private static long Income(Simulation sim, Household x, out long outside)
        {
            PopulationDefs p = sim.Config.Game.Population;
            bool hut = InHut(sim, x);
            long income = 0;
            outside = 0;
            if (Town.ClinicRules.IsDoctorHome(sim, x)) return Town.ClinicRules.Salary(sim); // the district's pay (spec 023 §3.1); no outside work
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (c.Job == CitizenJob.Hand)
                {
                    Hand h = sim.State.People.Hand(c.HandId);
                    if (h == null || h.Contract != ContractState.Active) continue; // unpaid: nothing this season
                    income += hut ? h.Wage - Money.MulRate(h.Wage, p.RemitShare) : h.Wage;
                }
                else if (c.Job == CitizenJob.Business)
                {
                    Business.Business b = sim.State.Businesses?.Find(c.Employer);
                    if (b != null) income += Business.BusinessRules.Wage(sim, Business.BusinessRules.TypeOf(sim, b)); // spec 016 §3.6
                }
                else if (WorksOutside(sim, c)) outside += SchoolRules.WithPremium(sim, c, OutsideSeasonPay(sim)); // literate: 10% more (spec 018 §3.2)
            }
            return income + outside;
        }

        /// <summary>The food share for an income a person a season (§3.5): the Engel curve, piecewise linear, flat past its last point.</summary>
        public static Fixed FoodShare(Simulation sim, long perPerson)
        {
            var curve = sim.Config.Game.Population.EngelCurve;
            if (perPerson <= 0) return curve[0].FoodShare;
            for (int i = 1; i < curve.Count; i++)
            {
                EngelPoint a = curve[i - 1], b = curve[i];
                long ai = MarketRules.Indexed(sim, a.Income), bi = MarketRules.Indexed(sim, b.Income);
                if (perPerson >= bi) continue;
                return a.FoodShare - (a.FoodShare - b.FoodShare).MulRatio(perPerson - ai, bi - ai);
            }
            return curve[curve.Count - 1].FoodShare;
        }

        /// <summary>
        /// Sets the season's budget (§3.5): income ÷ adult equivalents gives the food share; food, rent, clothing 8%, health 5%,
        /// education 5%, the rest half leisure and half to be saved (banked at the close). Short of what eating enough costs, savings
        /// make up the food first.
        /// With <paramref name="keepRent"/> the season's rent already owed stands (a tenant hired as a hand mid-season, ADR 0083 §1).
        /// </summary>
        public static void SetBudget(Simulation sim, Household x, bool keepRent = false)
        {
            PopulationDefs p = sim.Config.Game.Population;
            long income = Income(sim, x, out long outside);
            x.OutsideExpected = outside;
            Fixed ae = AdultEquivalents(sim, x);
            long perPerson = ae == Fixed.Zero ? income : Money.MulRate(income, Fixed.One / ae);
            x.Income = income;
            x.Food = Money.MulRate(income, FoodShare(sim, perPerson));
            x.FoodNeed = Money.MulRate(MarketRules.Indexed(sim, p.FoodBasketPerPerson), ae - MealsEaten(sim, x)); // spec 022 §3.5: free school meals
            if (x.SinceSeason < 0) x.SinceSeason = LeaseRules.Season(sim);
            if (!keepRent)
            {
                if (InFamilyHouse(sim, x) && !HasHand(sim, x))
                {
                    LeaseRules.Begin(sim, x); // US-240: a fixed lease rent, set when the family begins to pay rent
                    x.Rent = x.LeaseRent;
                }
                else if (Town.ClinicRules.IsDoctorHome(sim, x))
                {
                    LeaseRules.End(x);
                    x.Rent = Town.ClinicRules.Rent(sim); // spec 023 §3.1: the quarters are let to him at a fixed rent, paid from the district's salary
                }
                else
                {
                    LeaseRules.End(x);
                    x.Rent = 0;
                }
            }
            x.Clothing = Money.MulRate(income, p.ClothingShare);
            x.HealthCare = Money.MulRate(income, p.HealthShare);
            x.Schooling = Money.MulRate(income, p.EducationShare);
            long rest = income - x.Food - x.Rent - x.Clothing - x.HealthCare - x.Schooling;
            if (rest < 0) rest = 0;
            x.Leisure = Money.MulRate(rest, p.LeisureOfRest);
            if (SavesLeisure(sim, x)) x.Leisure /= 2; // a frugal member (spec 015 §3.6): half the leisure saved
            x.Saved = rest - x.Leisure;
            if (x.Food < x.FoodNeed && x.Savings > 0)
            {
                long draw = System.Math.Min(x.Savings, x.FoodNeed - x.Food);
                x.Savings -= draw;
                x.Food += draw;
            }
            x.FoodMet = x.FoodNeed <= 0 || x.Food >= x.FoodNeed ? Fixed.One : Fixed.FromRatio(x.Food, x.FoodNeed);
            HappinessRules.Update(sim, x); // the new budget shows at once, not at the next daily pass
        }

        private static bool SavesLeisure(Simulation sim, Household x)
        {
            var traits = sim.Config.Game.People.Traits;
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (!IsChild(sim, c) && (traits[c.Trait1].SavesLeisure || traits[c.Trait2].SavesLeisure)) return true;
            }
            return false;
        }

        /// <summary>
        /// A citizen's health after the season's food (§3.12): down by the shortfall, up when fed; with the clinic's effect <paramref name="e"/> the
        /// loss is cut and the recovery boosted (spec 023 §3.2). Clamped.
        /// </summary>
        private static Fixed Next(Simulation sim, PopulationDefs p, Citizen c, Household x, Fixed e)
        {
            Definitions.ClinicDef clinic = e == Fixed.Zero ? null : Town.ClinicRules.Def(sim);
            Fixed loss = clinic == null ? p.HealthLossPerSeason : p.HealthLossPerSeason * (Fixed.One - clinic.LossCut * e);
            Fixed gain = clinic == null ? p.HealthGainPerSeason : p.HealthGainPerSeason * (Fixed.One + clinic.GainBoost * e);
            Fixed health = x.FoodMet < Fixed.One ? c.Health - loss * (Fixed.One - x.FoodMet) : c.Health + gain;
            if (SchoolRules.IsPupil(sim, c)) health += Town.PolicyRules.MealHealth(sim); // spec 022 §3.5: the meals help children's health
            health -= Town.SmokeRules.Health(sim, x); // spec 025: a kiln's or a generator's smoke
            health -= Town.UtilityRules.Strain(sim, x); // spec 025 §3.1: no clean water near, no drains
            return health > Fixed.One ? Fixed.One : health < p.MinHealth ? p.MinHealth : health;
        }

        /// <summary>A hand's household in a family house pays no rent: the house is part of his pay (§3.6).</summary>
        public static bool HasHand(Simulation sim, Household x)
        {
            foreach (long id in x.Members) if (sim.State.Citizens.Citizen(id).Job == CitizenJob.Hand) return true;
            return false;
        }

        /// <summary>
        /// The season close (§3.12, after spec 005's settlement): rent for the season that ended; health follows the season's food (down by the shortfall, up when fed),
        /// the season's saving is banked; deaths, births and marriages; then each household sets the new season's budget, those who
        /// stay unhappy or behind on rent may leave, and the
        /// departed nobody links to are dropped. Households in id order.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return;
            PopulationDefs p = sim.Config.Game.Population;
            foreach (Household x in s.Households) HouseRules.CollectRent(sim, x); // first, for the season that ended (§3.12)
            Town.CohortRules.EnsureJoined(sim); // a Township reached before the old village could join (an older save)
            Town.LandValueRules.Read(sim); // spec 024 §3.9: land value is read once, before this season's leases renew
            Town.LandLeaseRules.Close(sim); // leases end or pay their rent upfront (spec 024 §3.6)
            Town.ZoningRules.Close(sim); // then builders buy a parcel if families are waiting (spec 024 §3.3)
            Town.UpgradeRules.Close(sim); // and make one of their homes pucca, where it is offered (spec 024 §3.7)
            foreach (Household x in s.Households)
            {
                Fixed e = Town.ClinicRules.Effect(sim, x); // spec 023 §3.2: the clinic's reach, capacity and the Health service
                foreach (long id in x.Members)
                {
                    Citizen c = s.Citizen(id);
                    Fixed health = Next(sim, p, c, x, Fixed.Zero), withClinic = e == Fixed.Zero ? health : Next(sim, p, c, x, e);
                    c.Health = withClinic;
                    if (withClinic > health) Town.ClinicRules.CountRegained(sim); // an exact count: health that stands higher because of the clinic
                }
                // The season's saving, plus what outside work and labour paid beyond what the budget expected (or less).
                x.Savings = System.Math.Max(0, x.Savings + x.Saved + x.Earned - x.OutsideExpected);
                x.Earned = 0;
            }
            PopulationRules.Close(sim);
            foreach (Household x in s.Households) LeaseRules.AtClose(sim, x); // a lease that ends renews first (US-240)
            foreach (Household x in s.Households) SetBudget(sim, x);
            LeaseRules.Offer(sim); // the leases now in their last season (US-240)
            MigrationRules.MoveOut(sim);
            CitizenRules.Prune(sim);
        }
    
        /// <summary>A day's outside pay for one adult (§3.6): the founder's day-labour rate × 8 h × index × the share of days with work.</summary>
        public static long OutsideDayPay(Simulation sim)
        {
            return Money.MulRate(MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour * 8), sim.Config.Game.Population.OutsideDaysShare);
        }

        /// <summary>
        /// The daily pass (§3.6): each adult without a town job is paid the expected share of a day's outside work, unless the founder
        /// hired them as a labourer during the day (they were paid that wage instead).
        /// </summary>
        internal static void PayOutsideWork(Simulation sim)
        {
            long t = sim.State.Clock.SimTime, dayStart = t - ClockDef.SecondsPerDay, pay = OutsideDayPay(sim);
            foreach (Household x in sim.State.Citizens.Households)
                foreach (long id in x.Members)
                {
                    Citizen c = sim.State.Citizens.Citizen(id);
                    if (!WorksOutside(sim, c) || c.LabourUntil > dayStart || c.LabouredUntil > dayStart) continue;
                    x.Earned += SchoolRules.WithPremium(sim, c, pay);
                }
        }

        /// <summary>
        /// The town's food buying a game day in grams (§3.7): each household's basket by adult equivalents × food met (a household short
        /// of money buys less), × real days a game day. Added to the local market's demand.
        /// </summary>
        public static long TownDemandPerDay(Simulation sim, int good) { return TownDemandPerDay(sim, good, null); }

        /// <summary>As <see cref="TownDemandPerDay(Simulation, int)"/> with the town's eaters already counted (the hourly market counts them once for every good, not once for each).</summary>
        public static long TownDemandPerDay(Simulation sim, int good, Fixed? eatersKnown)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return 0;
            PopulationDefs p = sim.Config.Game.Population;
            Fixed kg = Fixed.Zero;
            foreach (BasketItem b in p.Basket) if (b.Good == good) kg = b.KgPerRealDay;
            if (kg == Fixed.Zero) return 0;
            Fixed eaters = eatersKnown ?? Eaters(sim);
            return (kg * p.RealDaysPerGameDay * eaters).MulRatio(1000, 1).RoundToLong();
        }

        /// <summary>The town's adult equivalents who are fed: each household's, by how much of its food is met.</summary>
        public static Fixed Eaters(Simulation sim)
        {
            Fixed eaters = Fixed.Zero;
            foreach (Household x in sim.State.Citizens.Households) eaters += AdultEquivalents(sim, x) * x.FoodMet;
            return eaters;
        }

        /// <summary>The employment rate (§3.6): working-age adults with a job in town ÷ all working-age adults (outside work doesn't count).</summary>
        public static Fixed EmploymentRate(Simulation sim)
        {
            int inTown = 0, adults = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                HappinessRules.Jobs(sim, x, out int i, out int a);
                inTown += i;
                adults += a;
            }
            return adults == 0 ? Fixed.Zero : Fixed.FromRatio(inTown, adults);
        }
    }
}
