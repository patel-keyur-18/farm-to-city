using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;

namespace Sim.Core.People
{
    /// <summary>
    /// Needs and happiness (spec 014 §3.8): survival (the least of food, shelter, water), security (town jobs, health care, rent paid),
    /// belonging, growth; each layer gated by the one below at 0.8. Needs the game doesn't have yet (the school) aren't counted.
    /// </summary>
    public static class HappinessRules
    {
        /// <summary>Room in the home: members up to its capacity, scaled down above it (§3.4).</summary>
        public static Fixed Shelter(Simulation sim, Household x)
        {
            int capacity = Capacity(sim, x);
            return x.Members.Count <= capacity ? Fixed.One : Fixed.FromRatio(capacity, x.Members.Count);
        }

        /// <summary>People the home holds: a hand's hut its hands, a family house its capacity.</summary>
        public static int Capacity(Simulation sim, Household x)
        {
            Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
            if (b == null) return x.Members.Count;
            if (b.Def == sim.Config.Game.Population.House) return sim.Config.Game.Population.HouseCapacity;
            return System.Math.Max(1, sim.Config.Game.Buildings[b.Def].HousesHands);
        }

        /// <summary>Working-age adults with a job in town, and all working-age adults (§3.6).</summary>
        public static void Jobs(Simulation sim, Household x, out int inTown, out int adults)
        {
            inTown = adults = 0;
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (HouseholdRules.IsChild(sim, c) || HouseholdRules.IsElder(sim, c)) continue;
                adults++;
                if (c.Job == CitizenJob.Hand || c.Job == CitizenJob.Business) inTown++; // a job at your business is a town job (spec 016 §3.6)
            }
        }

        /// <summary>A stable job: the share of working-age adults with a town job; met when nobody here is of working age.</summary>
        public static Fixed Job(Simulation sim, Household x)
        {
            Jobs(sim, x, out int inTown, out int adults);
            return adults == 0 ? Fixed.One : Fixed.FromRatio(inTown, adults);
        }

        /// <summary>Updates a household's needs met and happiness (0–100), less the season's mourning (§3.9).</summary>
        public static void Update(Simulation sim, Household x)
        {
            PopulationDefs p = sim.Config.Game.Population;
            Fixed survival = Min(Min(x.FoodMet, Shelter(sim, x)), Fixed.One); // water: the well and the pond, always met
            Fixed security = (Job(sim, x) + p.Chemist + (x.RentBehind == 0 ? Fixed.One : Fixed.Zero)).MulRatio(1, 3);
            Fixed belonging = p.Belonging; // the temple and the banyan square
            Fixed growth = x.Leisure > 0 ? p.Leisure : Fixed.Zero; // the school isn't counted until spec 018 builds one
            x.Survival = survival;
            x.Security = security;
            x.Belonging = belonging;
            x.Growth = growth;
            Fixed score = p.SurvivalWeight * survival
                + p.SecurityWeight * security * Gate(p, survival)
                + p.BelongingWeight * belonging * Gate(p, security)
                + p.GrowthWeight * growth * Gate(p, belonging);
            Fixed happiness = score.MulRatio(100, 1);
            if (x.MourningSeason >= CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime)) happiness -= Fixed.FromInt(p.Mourning);
            x.Happiness = happiness < Fixed.Zero ? Fixed.Zero : happiness;
        }

        /// <summary>gate(layer) = min(1, met(previous layer) ÷ gate).</summary>
        private static Fixed Gate(PopulationDefs p, Fixed previous) { return Min(Fixed.One, previous / p.Gate); }

        private static Fixed Min(Fixed a, Fixed b) { return a < b ? a : b; }

        /// <summary>The town's happiness: the mean over citizens, each taking their household's (0 with nobody).</summary>
        public static Fixed Town(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return Fixed.Zero;
            Fixed sum = Fixed.Zero;
            int n = 0;
            foreach (Household x in s.Households)
            {
                sum += x.Happiness.MulRatio(x.Members.Count, 1);
                n += x.Members.Count;
            }
            return n == 0 ? Fixed.Zero : sum.MulRatio(1, n);
        }

        internal static void UpdateAll(Simulation sim)
        {
            foreach (Household x in sim.State.Citizens.Households) Update(sim, x);
        }
    }

    /// <summary>Population (spec 014 §3.12). Hourly: offers and families moving in. Daily: outside work paid, then needs and happiness. The season close runs inside settlement (HouseholdRules.Close).</summary>
    internal sealed class PopulationSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Population; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (sim.State.Citizens == null) return;
            if (pass == ItemClass.Hourly) MigrationRules.Hourly(sim);
            if (pass != ItemClass.Daily) return;
            HouseholdRules.PayOutsideWork(sim);
            HappinessRules.UpdateAll(sim);
        }
    }
}
