using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// The old village as one record (spec 023 §3.5, US-103): about 30 households and 150 people who join at the Township's unlock. They count
    /// as residents for the grants, pay the house tax from their leisure and savings before food, send their unlettered children to the school
    /// free, and are as happy as the town's real coverage makes them (food, the clinic, the school, belonging); a household under 35 for two
    /// closes running leaves. A town left alone keeps them.
    /// </summary>
    public static class CohortRules
    {
        public static CohortDef Def(Simulation sim) { return sim.Config.Game?.Township?.Cohort; }

        public static CohortState State(Simulation sim) { return sim.State.Township?.Cohort; }

        public static bool Joined(Simulation sim) { return State(sim) != null && State(sim).Joined; }

        public static int Households(Simulation sim) { return Joined(sim) ? State(sim).Households : 0; }

        /// <summary>People of the old village: households × people a household.</summary>
        public static int People(Simulation sim) { return Joined(sim) ? State(sim).Households * Def(sim).PeoplePerHousehold : 0; }

        /// <summary>School-age children who can't read yet and join the school free (about a fifteenth of the people), once the school is open.</summary>
        public static int Pupils(Simulation sim)
        {
            if (!Joined(sim) || !SchoolRules.IsOpen(sim)) return 0;
            return (int)(Fixed.FromInt(People(sim)) * Def(sim).PupilShare).FloorToLong();
        }

        /// <summary>The share of those aged 7 and over who can read (content), for the statistics.</summary>
        public static Fixed Literacy(Simulation sim) { return Def(sim).Literacy; }

        /// <summary>One household's season budget (spec 014 §3.5, the Engel curve) on the content's income, at today's prices.</summary>
        public sealed class Budget
        {
            public long Income, Food, FoodNeed, Clothing, Health, Education, Leisure, Saved;
            public Fixed FoodMet, AdultEquivalents;
        }

        /// <summary>Adult equivalents of one household: adults 1, children and elders by spec 014's weights.</summary>
        public static Fixed AdultEquivalents(Simulation sim)
        {
            CohortDef d = Def(sim);
            PopulationDefs p = sim.Config.Game.Population;
            Fixed adults = Fixed.One - d.ChildShare - d.ElderShare;
            return Fixed.FromInt(d.PeoplePerHousehold) * (adults + d.ChildShare * p.ChildEquivalent + d.ElderShare * p.ElderEquivalent);
        }

        public static Budget BudgetOf(Simulation sim)
        {
            CohortDef d = Def(sim);
            PopulationDefs p = sim.Config.Game.Population;
            var b = new Budget { Income = MarketRules.Indexed(sim, d.Income) };
            Fixed ae = AdultEquivalents(sim);
            b.AdultEquivalents = ae;
            long perPerson = Money.MulRate(b.Income, Fixed.One / ae);
            b.Food = Money.MulRate(b.Income, HouseholdRules.FoodShare(sim, perPerson));
            Fixed meals = Households(sim) == 0 ? Fixed.Zero : p.ChildEquivalent * PolicyRules.MealShare(sim) * Fixed.FromInt(Pupils(sim)) / Fixed.FromInt(Households(sim));
            b.FoodNeed = Money.MulRate(MarketRules.Indexed(sim, p.FoodBasketPerPerson), ae - meals);
            b.Clothing = Money.MulRate(b.Income, p.ClothingShare);
            b.Health = Money.MulRate(b.Income, p.HealthShare);
            b.Education = Money.MulRate(b.Income, p.EducationShare);
            long rest = System.Math.Max(0, b.Income - b.Food - b.Clothing - b.Health - b.Education);
            b.Leisure = Money.MulRate(rest, p.LeisureOfRest);
            b.Saved = rest - b.Leisure;
            b.FoodMet = b.FoodNeed <= 0 || b.Food >= b.FoodNeed ? Fixed.One : Fixed.FromRatio(b.Food, b.FoodNeed);
            return b;
        }

        /// <summary>The house tax of the old village at a rate: a standard kutcha house each (spec 022 §3.1), the office's share collected.</summary>
        public static long TaxBill(Simulation sim, Fixed rate)
        {
            return Joined(sim) ? State(sim).Households * TaxRules.StandardHouseTax(sim, rate) : 0;
        }

        public static long TaxCollected(Simulation sim, Fixed rate) { return TaxRules.Collected(sim, TaxBill(sim, rate)); }

        /// <summary>Leisure the old village has for tax each season (all households), before the tax.</summary>
        public static long LeisureTotal(Simulation sim) { return Joined(sim) ? BudgetOf(sim).Leisure * State(sim).Households : 0; }

        /// <summary>
        /// The house tax at the close (spec 022 §3.1, spec 023 §3.5): the office collects its share of the bill and the village pays it from its
        /// leisure first, then its savings, never from food; what it can't pay is not carried (it has no cash to lose). The treasury receives what is paid.
        /// </summary>
        internal static void PayTax(Simulation sim)
        {
            if (!Joined(sim) || !TreasuryRules.IsOpen(sim)) return;
            CohortState s = State(sim);
            Budget b = BudgetOf(sim);
            long collected = TaxCollected(sim, TaxRules.Rate(sim, TaxKind.House));
            long leisure = b.Leisure * s.Households;
            long paid = System.Math.Min(collected, leisure + s.Savings);
            long fromSavings = System.Math.Max(0, paid - leisure);
            s.TaxLast = s.TaxThis;
            s.TaxThis = paid;
            s.Savings += b.Saved * s.Households - fromSavings;
            if (paid > 0) TreasuryRules.ReceiveTax(sim, TaxKind.House, "cohort", paid);
        }

        /// <summary>The four layers (spec 014 §3.8) from the town's real coverage; the same weights and gates as a household's.</summary>
        internal static void Refresh(Simulation sim)
        {
            if (!Joined(sim)) return;
            CohortState s = State(sim);
            PopulationDefs p = sim.Config.Game.Population;
            Budget b = BudgetOf(sim);
            Fixed survival = b.FoodMet < Fixed.One ? b.FoodMet : Fixed.One;
            Fixed security = (Fixed.One + ClinicRules.HealthCareMetTown(sim, p.Chemist) + Fixed.One).MulRatio(1, 3);
            Fixed belonging = p.Belonging;
            long taxFromLeisure = System.Math.Min(s.TaxThis, b.Leisure * s.Households);
            Fixed kept = b.Leisure <= 0 ? Fixed.Zero : Fixed.FromRatio(b.Leisure * s.Households - taxFromLeisure, b.Leisure * s.Households);
            Fixed leisure = b.Leisure > 0 && kept > Fixed.Zero ? p.Leisure * kept : Fixed.Zero;
            Fixed growth = SchoolRules.IsOpen(sim) ? (Fixed.One + leisure).MulRatio(1, 2) : leisure;
            s.Survival = survival; s.Security = security; s.Belonging = belonging; s.Growth = growth;
            Fixed score = p.SurvivalWeight * survival + p.SecurityWeight * security * Gate(p, survival)
                + p.BelongingWeight * belonging * Gate(p, security) + p.GrowthWeight * growth * Gate(p, belonging);
            Fixed happiness = score.MulRatio(100, 1);
            if (happiness > Fixed.FromInt(100)) happiness = Fixed.FromInt(100);
            s.Happiness = happiness < Fixed.Zero ? Fixed.Zero : happiness;
        }

        private static Fixed Gate(PopulationDefs p, Fixed previous)
        {
            Fixed g = previous / p.Gate;
            return g > Fixed.One ? Fixed.One : g;
        }

        /// <summary>The old village joins (spec 023 §3.5): once, at the unlock.</summary>
        internal static void Join(Simulation sim)
        {
            CohortDef d = Def(sim);
            TownshipState t = sim.State.Township;
            if (d == null || t == null || t.Cohort.Joined || d.Households == 0) return; // content without an old village (tests) has none
            CohortState s = t.Cohort;
            s.Joined = true;
            s.JoinedAt = sim.State.Clock.SimTime;
            s.Households = d.Households;
            Refresh(sim);
            sim.Emit(new CohortJoined(s.JoinedAt, s.Households, People(sim)));
        }

        /// <summary>The close, after the treasury's (spec 023 §3.10): happiness, then a household under the line for two closes running leaves.</summary>
        internal static void AtClose(Simulation sim)
        {
            if (!Joined(sim)) return;
            CohortState s = State(sim);
            CohortDef d = Def(sim);
            Refresh(sim);
            s.LowCloses = s.Happiness < Fixed.FromInt(d.LeaveBelow) ? s.LowCloses + 1 : 0;
            if (s.LowCloses < d.LeaveCloses || s.Households == 0) return;
            s.Households--;
            s.Left++;
            s.LowCloses = 0;
            sim.Emit(new CohortHouseholdLeft(sim.State.Clock.SimTime, s.Households));
        }
    }
}
