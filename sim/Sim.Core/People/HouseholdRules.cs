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
                else if (WorksOutside(sim, c)) outside += OutsideSeasonPay(sim);
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
        /// </summary>
        public static void SetBudget(Simulation sim, Household x)
        {
            PopulationDefs p = sim.Config.Game.Population;
            long income = Income(sim, x, out long outside);
            x.OutsideExpected = outside;
            Fixed ae = AdultEquivalents(sim, x);
            long perPerson = ae == Fixed.Zero ? income : Money.MulRate(income, Fixed.One / ae);
            x.Income = income;
            x.Food = Money.MulRate(income, FoodShare(sim, perPerson));
            x.FoodNeed = Money.MulRate(MarketRules.Indexed(sim, p.FoodBasketPerPerson), ae);
            x.Rent = InFamilyHouse(sim, x) && !HasHand(sim, x) ? MarketRules.Indexed(sim, p.RentPerSeason) : 0;
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

        /// <summary>A hand's household in a family house pays no rent: the house is part of his pay (§3.6).</summary>
        private static bool HasHand(Simulation sim, Household x)
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
            foreach (Household x in s.Households)
            {
                foreach (long id in x.Members)
                {
                    Citizen c = s.Citizen(id);
                    Fixed health = x.FoodMet < Fixed.One ? c.Health - p.HealthLossPerSeason * (Fixed.One - x.FoodMet) : c.Health + p.HealthGainPerSeason;
                    c.Health = health > Fixed.One ? Fixed.One : health < p.MinHealth ? p.MinHealth : health;
                }
                // The season's saving, plus what outside work and labour paid beyond what the budget expected (or less).
                x.Savings = System.Math.Max(0, x.Savings + x.Saved + x.Earned - x.OutsideExpected);
                x.Earned = 0;
            }
            PopulationRules.Close(sim);
            foreach (Household x in s.Households) SetBudget(sim, x);
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
                    x.Earned += pay;
                }
        }

        /// <summary>
        /// The town's food buying a game day in grams (§3.7): each household's basket by adult equivalents × food met (a household short
        /// of money buys less), × real days a game day. Added to the local market's demand.
        /// </summary>
        public static long TownDemandPerDay(Simulation sim, int good)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return 0;
            PopulationDefs p = sim.Config.Game.Population;
            Fixed kg = Fixed.Zero;
            foreach (BasketItem b in p.Basket) if (b.Good == good) kg = b.KgPerRealDay;
            if (kg == Fixed.Zero) return 0;
            Fixed eaters = Fixed.Zero;
            foreach (Household x in s.Households) eaters += AdultEquivalents(sim, x) * x.FoodMet;
            return (kg * p.RealDaysPerGameDay * eaters).MulRatio(1000, 1).RoundToLong();
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
