using System;
using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// Town life (US-151, spec 026): at each close from the Township on, the town is read once: the five reputation scores from named parts, the inequality
    /// ratio and crime. Everything else reads that state, so a number never moves while the player looks. Reading changes no cash.
    /// </summary>
    public static class TownLifeRules
    {
        public static TownLifeDef Def(Simulation sim) { return sim.Config.Game?.Township?.TownLife; }

        /// <summary>From the Township's unlock (the old village joined), when the content has it.</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Citizens != null && LandValueRules.On(sim); }

        public static TownLifeState State(Simulation sim) { return sim.State.Township?.TownLife; }

        /// <summary>The town has been read at least once: before that every effect is off and plays as before.</summary>
        public static bool Known(Simulation sim) { return On(sim) && State(sim).Read; }

        /// <summary>Your households: a builder's family is a resident, not one of your people (spec 024 §3.3).</summary>
        public static List<Household> Yours(Simulation sim)
        {
            var list = new List<Household>();
            foreach (Household x in sim.State.Citizens.Households) if (!HouseholdRules.InBuildersHome(sim, x)) list.Add(x);
            return list;
        }

        // ---- the reading at each close ----

        /// <summary>Reads the town (system Treasury, after the treasury's close): inequality, crime, the five scores.</summary>
        internal static void AtClose(Simulation sim)
        {
            if (!On(sim)) return;
            TownLifeDef d = Def(sim);
            TownLifeState s = State(sim);
            if (!s.Read) { s.Read = true; s.Approval = Fixed.FromInt(d.ApprovalStart); }
            ReadInequality(sim, d, s);
            ReadCrime(sim, d, s);
            ReadReputation(sim, d, s);
            sim.Emit(new Events.ReputationRead(sim.State.Clock.SimTime, Mean(s).FloorToLong(), s.Crime.FloorToLong(), s.RatioKnown, s.Ratio.RoundToLong()));
        }

        /// <summary>
        /// What a household earns a person (the fairness measure): its season income over its members, or −1 for a household with nobody of working age
        /// (a retired couple isn't an earner, and a big family always earns more in all than a small one).
        /// </summary>
        public static long PerPerson(Simulation sim, Household x)
        {
            HappinessRules.Jobs(sim, x, out _, out int adults);
            return adults == 0 || x.Members.Count == 0 ? -1 : x.Income / x.Members.Count;
        }

        /// <summary>The ratio of the richest tenth's mean income a person to the poorest tenth's (§3.6), from your earning households only and from <c>needsHouseholds</c>.</summary>
        private static void ReadInequality(Simulation sim, TownLifeDef d, TownLifeState s)
        {
            s.RatioKnown = false;
            s.Ratio = Fixed.One;
            s.Score = Fixed.Zero;
            s.MedianIncome = 0;
            s.PoorestTop = -1;
            var incomes = new List<KeyValuePair<long, long>>(); // income a person, then household id: a total order, so ties never depend on the sort
            foreach (Household x in Yours(sim))
            {
                long v = PerPerson(sim, x);
                if (v >= 0) incomes.Add(new KeyValuePair<long, long>(v, x.Id));
            }
            if (incomes.Count < d.NeedsHouseholds) return;
            incomes.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            int tenth = Math.Max(1, incomes.Count / 10);
            long low = 0, high = 0;
            for (int i = 0; i < tenth; i++) { low += incomes[i].Key; high += incomes[incomes.Count - 1 - i].Key; }
            Fixed cap = Fixed.FromInt(d.RatioCap);
            Fixed ratio = low <= 0 ? cap : Fixed.FromRatio(high, low);
            if (ratio > cap) ratio = cap;
            if (ratio < Fixed.One) ratio = Fixed.One;
            s.RatioKnown = true;
            s.Ratio = ratio;
            s.Score = (ratio - Fixed.One).MulRatio(100, d.RatioCap - 1);
            s.MedianIncome = incomes[incomes.Count / 2].Key;
            s.PoorestTop = incomes[tenth - 1].Key;
        }

        /// <summary><c>crime = base + 2 × unemployed% + 0.3 × the inequality score</c>, 0–100 (§3.5): the old village's adults count as employed.</summary>
        private static void ReadCrime(Simulation sim, TownLifeDef d, TownLifeState s)
        {
            int adults = 0, inTown = 0;
            foreach (Household x in Yours(sim))
            {
                HappinessRules.Jobs(sim, x, out int a, out int b);
                inTown += a;
                adults += b;
            }
            CohortDef c = CohortRules.Def(sim);
            if (c != null && CohortRules.Joined(sim))
            {
                int working = (int)(Fixed.FromInt(CohortRules.People(sim)) * (Fixed.One - c.ChildShare - c.ElderShare)).FloorToLong();
                adults += working;
                inTown += working;
            }
            s.UnemployedPermille = adults == 0 ? 0 : (int)((long)(adults - inTown) * 1000 / adults);
            Fixed crime = Fixed.FromInt(d.CrimeBase) + d.CrimePerUnemployed * Fixed.FromRatio(s.UnemployedPermille, 10) + d.CrimeInequalityShare * s.Score;
            s.Crime = Clamp100(crime);
        }

        private static Fixed Clamp100(Fixed v) { return v < Fixed.Zero ? Fixed.Zero : v > Fixed.FromInt(100) ? Fixed.FromInt(100) : v; }

        /// <summary>The five scores, each the weighted sum of its three parts (a part is 0–100) (§3.4).</summary>
        private static void ReadReputation(Simulation sim, TownLifeDef d, TownLifeState s)
        {
            Fixed[] parts = ComputeParts(sim, d);
            for (int i = 0; i < TownLifeDef.Scores; i++)
            {
                Fixed score = Fixed.Zero;
                for (int k = 0; k < TownLifeDef.PartsEach; k++)
                {
                    s.Parts[i * TownLifeDef.PartsEach + k] = parts[i * TownLifeDef.PartsEach + k];
                    score += parts[i * TownLifeDef.PartsEach + k].MulRatio(d.Weights[i][k], 100);
                }
                s.Reputation[i] = Clamp100(score);
            }
        }

        private static Fixed Percent(int n, int of, int whenNone) { return of == 0 ? Fixed.FromInt(whenNone) : Fixed.FromRatio(n * 100L, of); }

        /// <summary>The fifteen parts, score by score (§3.4). Pure: the same town gives the same parts.</summary>
        public static Fixed[] ComputeParts(Simulation sim, TownLifeDef d)
        {
            var p = new Fixed[TownLifeDef.Scores * TownLifeDef.PartsEach];
            List<Household> hs = Yours(sim);
            PopulationDefs pop = sim.Config.Game.Population;

            // Homes: how many stand within a reach, and the counts the shares need.
            int homes = 0, water = 0, shelter = 0, clinic = 0, school = 0, noSmoke = 0, drains = 0, collection = 0;
            Fixed careSum = Fixed.Zero;
            foreach (Household x in hs)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                homes++;
                if (HappinessRules.Shelter(sim, x) >= Fixed.One) shelter++;
                if (ClinicRules.Effect(sim, x) > Fixed.Zero) clinic++;
                careSum += ClinicRules.HealthCareMet(sim, x, pop.Chemist);
                if (SmokeRules.Health(sim, x) == Fixed.Zero) noSmoke++;
                if (SchoolRules.IsOpen(sim) && SchoolRules.HomeCovered(sim, x)) school++;
                if (b == null || b.TileId < 0) continue;
                if (UtilityRules.HasWater(sim, b.TileId)) water++;
                if (UtilityRules.Covers(sim, UtilityDef.Drains, b.TileId)) drains++;
                if (UtilityRules.Covers(sim, UtilityDef.Collection, b.TileId)) collection++;
            }

            // Livability
            p[0] = Clamp100(HappinessRules.Town(sim));
            p[1] = Percent(water, homes, 0);
            p[2] = Percent(shelter, homes, 0);

            // Business climate
            Fixed cap = TaxRules.Bound(sim, TaxKind.Business), rate = TaxRules.Rate(sim, TaxKind.Business);
            p[3] = cap <= Fixed.Zero ? Fixed.FromInt(100) : Clamp100((Fixed.One - rate / cap).MulRatio(100, 1));
            int open = 0, profitable = 0;
            BusinessesState bs = sim.State.Businesses;
            if (bs != null)
                foreach (Business.Business b in bs.Businesses)
                {
                    if (b.Status != BusinessStatus.Open) continue;
                    open++;
                    if (b.Reports.Count > 0 && BusinessBooks.NetProfit(b.Reports[b.Reports.Count - 1]) > 0) profitable++;
                }
            p[4] = Percent(profitable, open, 50); // no business open: neither good nor bad
            p[5] = RoadRules.IsBuilt(sim) ? Fixed.FromInt(100) : Fixed.FromInt(d.RoadTrack);

            // Education
            p[6] = Fixed.FromInt(SchoolRules.LiteracyRate(sim));
            p[7] = Percent(school, homes, 0);
            int children = 0, enrolled = 0;
            SchoolDef sd = SchoolRules.Def(sim);
            foreach (Citizen c in sim.State.Citizens.Citizens)
            {
                int age = CitizenRules.Age(sim, c);
                if (!c.Living || age < sd.PupilAgeMin || age > sd.PupilAgeMax) continue;
                Household home = sim.State.Citizens.Household(c.HouseholdId);
                if (home == null || HouseholdRules.InBuildersHome(sim, home)) continue;
                children++;
                if (SchoolRules.IsOpen(sim) && (c.Literate || SchoolRules.Covered(sim, c))) enrolled++;
            }
            p[8] = Percent(enrolled, children, 100);

            // Health
            p[9] = Percent(clinic, homes, 0);
            p[10] = homes == 0 ? Fixed.Zero : Clamp100(careSum.MulRatio(100, homes));
            p[11] = Clamp100(ClinicRules.Condition(sim).MulRatio(100, 1));

            // Environment
            p[12] = Percent(noSmoke, homes, 100);
            p[13] = Percent(drains, homes, 0);
            p[14] = Percent(collection, homes, 0);
            return p;
        }

        /// <summary>The mean of the five scores (0–100); 50 before the town is read, so nothing changes until then.</summary>
        public static Fixed Mean(TownLifeState s)
        {
            Fixed sum = Fixed.Zero;
            foreach (Fixed f in s.Reputation) sum += f;
            return sum.MulRatio(1, TownLifeDef.Scores);
        }

        // ---- what the reading does ----

        /// <summary>The pull on a family's wait (§3.7): 100 at approval 50 and a mean of 50, between <c>pullMin</c> and <c>pullMax</c>; 100 before the town is read.</summary>
        public static Fixed Pull(Simulation sim)
        {
            if (!Known(sim)) return Fixed.FromInt(100);
            TownLifeDef d = Def(sim);
            TownLifeState s = State(sim);
            Fixed fifty = Fixed.FromInt(50);
            Fixed pull = Fixed.FromInt(100) + (s.Approval - fifty).MulRatio(1, d.PullDivisor) + (Mean(s) - fifty).MulRatio(1, d.PullDivisor);
            Fixed lo = Fixed.FromInt(d.PullMin), hi = Fixed.FromInt(d.PullMax);
            return pull < lo ? lo : pull > hi ? hi : pull;
        }

        /// <summary>The grants' multiplier (§3.4): 1 + (mean − 50) ÷ 5 percent above a mean of 50, never lowered.</summary>
        public static Fixed GrantFactor(Simulation sim)
        {
            if (!Known(sim)) return Fixed.One;
            Fixed over = Mean(State(sim)) - Fixed.FromInt(50);
            return over <= Fixed.Zero ? Fixed.One : Fixed.One + over.MulRatio(1, Def(sim).GrantBonusDivisor * 100);
        }

        /// <summary>Crime at an acre (§3.5): the town's, less the watchman's gain in his reach, never under 0.</summary>
        public static Fixed CrimeAtTile(Simulation sim, int tile)
        {
            TownLifeState s = State(sim);
            Fixed e = CivicRules.Staffed(sim, CivicDef.Watchman) && CivicRules.Covered(sim, CivicDef.Watchman, tile)
                ? CivicRules.Condition(sim) * CivicRules.Scale(sim, CivicDef.Watchman) : Fixed.Zero;
            return Floor0(s.Crime - CivicRules.Def(sim, CivicDef.Watchman).Gain * e * Fixed.FromInt(100));
        }

        /// <summary>Crime a household's home has (§3.5); the watchman's effect for the household.</summary>
        public static Fixed CrimeAt(Simulation sim, Household x)
        {
            TownLifeState s = State(sim);
            return Floor0(s.Crime - CivicRules.Def(sim, CivicDef.Watchman).Gain * CivicRules.Effect(sim, CivicDef.Watchman, x) * Fixed.FromInt(100));
        }

        /// <summary>Crime for the old village and the town's centre, where the watchman's reach always lies.</summary>
        public static Fixed CrimeTown(Simulation sim)
        {
            TownLifeState s = State(sim);
            return Floor0(s.Crime - CivicRules.Def(sim, CivicDef.Watchman).Gain * CivicRules.EffectTown(sim, CivicDef.Watchman) * Fixed.FromInt(100));
        }

        private static Fixed Floor0(Fixed v) { return v < Fixed.Zero ? Fixed.Zero : v; }

        /// <summary>The land value crime adds or takes (§3.5): ±step for every 10 points of crime under or over 50, at most ±max.</summary>
        public static Fixed LandValueTerm(Simulation sim, int tile)
        {
            if (!Known(sim)) return Fixed.Zero;
            TownLifeDef d = Def(sim);
            Fixed term = (Fixed.FromInt(50) - CrimeAtTile(sim, tile)).MulRatio(1, 10) * d.CrimeLandValueStep;
            return term > d.CrimeLandValueMax ? d.CrimeLandValueMax : term < Fixed.Zero - d.CrimeLandValueMax ? Fixed.Zero - d.CrimeLandValueMax : term;
        }

        /// <summary>Happiness a household loses to inequality (§3.6): one point for each whole point of the ratio above <c>happinessAbove</c>, at most <c>maxPoints</c>, below the median only.</summary>
        public static int InequalityPoints(Simulation sim, Household x)
        {
            if (!Known(sim)) return 0;
            TownLifeState s = State(sim);
            long mine = PerPerson(sim, x);
            if (!s.RatioKnown || mine < 0 || mine >= s.MedianIncome) return 0;
            TownLifeDef d = Def(sim);
            int over = (int)s.Ratio.FloorToLong() - d.HappinessAbove;
            return over <= 0 ? 0 : Math.Min(d.MaxPoints, over);
        }

        /// <summary>The poorest tenth leave a season sooner when the ratio reaches <c>pushOutAt</c> (§3.6).</summary>
        public static bool PushedOut(Simulation sim, Household x)
        {
            if (!Known(sim)) return false;
            TownLifeState s = State(sim);
            long mine = PerPerson(sim, x);
            return s.RatioKnown && mine >= 0 && s.Ratio >= Fixed.FromInt(Def(sim).PushOutAt) && mine <= s.PoorestTop;
        }
    }
}
