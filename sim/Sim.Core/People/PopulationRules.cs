using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>
    /// Births, marriages and deaths at the season close (spec 014 §3.9, ADR 0070): seeded (streams Births, Deaths), citizens in id
    /// order. Only elders die, gently; a son's bride moves in, a daughter marries into her husband's village.
    /// </summary>
    public static class PopulationRules
    {
        // Keys: (citizen id × 16 + kind); the counter is the season, or 0 for a new citizen's own draws.
        private const ulong DeathRoll = 1, BirthRoll = 2, MarriageRoll = 3, GenderKey = 4, NameKey = 5, Trait1Key = 6, Trait2Key = 7,
            SpouseAgeKey = 8, VillageKey = 9, SkillKey = 10, EducationKey = 11;

        private static ulong Roll(Simulation sim, RngStream stream, long id, ulong kind, long counter)
        {
            return Rng.Next(sim.State.Seed, stream, (ulong)id * 16 + kind, (ulong)counter);
        }

        /// <summary>An elder's chance of passing away this season (§3.9): base × 2^((age − 60) ÷ doubling), capped, × 1.5 in poor health.</summary>
        public static Fixed DeathChance(Simulation sim, Citizen c)
        {
            PopulationDefs p = sim.Config.Game.Population;
            int age = CitizenRules.Age(sim, c);
            if (age < p.ElderFrom || c.Job == CitizenJob.Hand) return Fixed.Zero; // a hand retires at 60, never dies under contract
            Fixed chance = p.DeathBase * Fixed.Exp2(Fixed.FromRatio(age - p.ElderFrom, p.DeathDoublingYears));
            if (chance > p.DeathCap) chance = p.DeathCap;
            return c.Health < p.PoorHealthBelow ? chance * p.PoorHealthDeathFactor : chance;
        }

        /// <summary>A couple's chance of a baby this season (§3.9): base × (room ? 1 : 0.3) × (0.6 + 0.4 × happiness ÷ 100), or 0.</summary>
        public static Fixed BirthChance(Simulation sim, Household x, Citizen woman)
        {
            PopulationDefs p = sim.Config.Game.Population;
            int age = CitizenRules.Age(sim, woman);
            if (!woman.Female || woman.Spouse == 0 || age < p.BirthAgeMin || age > p.BirthAgeMax || !x.Members.Contains(woman.Spouse)) return Fixed.Zero;
            if (Children(sim, woman.Id) >= p.MaxChildren) return Fixed.Zero;
            Fixed room = x.Members.Count < HappinessRules.Capacity(sim, x) ? Fixed.One : p.CrowdedBirthFactor;
            return p.BirthChance * room * (Fixed.FromRatio(6, 10) + x.Happiness.MulRatio(4, 1000));
        }

        /// <summary>Her children on record (living, or kept in the tree while she lives).</summary>
        public static int Children(Simulation sim, long motherId)
        {
            int n = 0;
            foreach (Citizen c in sim.State.Citizens.Citizens) if (c.Mother == motherId) n++;
            return n;
        }

        /// <summary>The season close's flows (§3.12): deaths, then births, then marriages. Budgets are set after, by the caller.</summary>
        internal static void Close(Simulation sim)
        {
            Deaths(sim);
            Births(sim);
            Marriages(sim);
        }

        private static void Deaths(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            long t = sim.State.Clock.SimTime, season = CitizenRules.SeasonOf(sim, t);
            foreach (Citizen c in new List<Citizen>(s.Citizens))
            {
                if (!c.Living) continue;
                Fixed chance = DeathChance(sim, c);
                if (chance == Fixed.Zero || !Rng.Chance(Roll(sim, RngStream.Deaths, c.Id, DeathRoll, season), chance)) continue;
                int age = CitizenRules.Age(sim, c);
                Household x = s.Household(c.HouseholdId);
                c.Gone = CitizenGone.Died;
                c.GoneAt = t;
                c.Job = CitizenJob.None;
                c.Story.Add(new StoryLine(StoryKind.Died, t, 0));
                x.Members.Remove(c.Id);
                x.MourningSeason = season; // the household mourns this season (§3.9)
                bool empty = x.Members.Count == 0;
                if (empty) s.Households.Remove(x); // an elder living alone: the house is free for a new family (§3.10)
                sim.Emit(new CitizenDied(t, c.Id, c.Name, c.Female, age, x.Id, empty));
            }
        }

        private static void Births(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            long t = sim.State.Clock.SimTime, season = CitizenRules.SeasonOf(sim, t);
            foreach (Household x in s.Households)
                foreach (long id in new List<long>(x.Members))
                {
                    Citizen woman = s.Citizen(id);
                    Fixed chance = BirthChance(sim, x, woman);
                    if (chance == Fixed.Zero || !Rng.Chance(Roll(sim, RngStream.Births, woman.Id, BirthRoll, season), chance)) continue;
                    Citizen father = s.Citizen(woman.Spouse);
                    long childId = s.NextCitizenId++;
                    bool female = Rng.Chance(Roll(sim, RngStream.Births, childId, GenderKey, 0), Fixed.FromRatio(1, 2));
                    var child = new Citizen
                    {
                        Id = childId, Name = Name(sim, RngStream.Births, childId, female), Female = female, BornSeason = season, HouseholdId = x.Id,
                        Health = Fixed.One, Father = father.Id, Mother = woman.Id, Village = -1,
                    };
                    Traits(sim, RngStream.Births, child);
                    child.Story.Add(new StoryLine(StoryKind.Born, t, 0));
                    s.Citizens.Add(child);
                    InsertMember(x, childId);
                    woman.Story.Add(new StoryLine(StoryKind.ChildBorn, t, childId));
                    father.Story.Add(new StoryLine(StoryKind.ChildBorn, t, childId));
                    sim.Emit(new CitizenBorn(t, childId, child.Name, female, x.Id, woman.Id));
                }
        }

        /// <summary>
        /// An unmarried adult child aged 22–30 living with a parent marries with chance 0.25 a season (ADR 0070): a son's bride moves in
        /// from another village (into a free family house as a new household if one stands empty, else with his parents); a daughter
        /// moves to her husband's village and is kept in the tree while a parent lives.
        /// </summary>
        private static void Marriages(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            long t = sim.State.Clock.SimTime, season = CitizenRules.SeasonOf(sim, t);
            foreach (Household x in new List<Household>(s.Households))
                foreach (long id in new List<long>(x.Members))
                {
                    Citizen c = s.Citizen(id);
                    int age = CitizenRules.Age(sim, c);
                    if (c.Spouse != 0 || c.Job == CitizenJob.Hand || age < p.MarriageAgeMin || age > p.MarriageAgeMax) continue;
                    if (!x.Members.Contains(c.Father) && !x.Members.Contains(c.Mother)) continue;
                    if (!Rng.Chance(Roll(sim, RngStream.Births, c.Id, MarriageRoll, season), p.MarriageChance)) continue;
                    int village = (int)Rng.Below(Roll(sim, RngStream.Births, c.Id, VillageKey, season), (ulong)p.Villages.Count);
                    if (c.Female) MarryOut(sim, x, c, village);
                    else BringBride(sim, x, c, age, village);
                }
        }

        private static void MarryOut(Simulation sim, Household x, Citizen daughter, int village)
        {
            long t = sim.State.Clock.SimTime;
            daughter.Gone = CitizenGone.MarriedAway;
            daughter.GoneAt = t;
            daughter.Job = CitizenJob.None;
            daughter.Village = village;
            daughter.Story.Add(new StoryLine(StoryKind.MarriedAway, t, 0));
            x.Members.Remove(daughter.Id);
            sim.Emit(new CitizenMarried(t, daughter.Id, daughter.Name, true, 0, village, x.Id, x.Id));
        }

        private static void BringBride(Simulation sim, Household x, Citizen son, int age, int village)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            long t = sim.State.Clock.SimTime, id = s.NextCitizenId++;
            int brideAge = System.Math.Max(p.AdultFrom, age - (int)Rng.Below(Roll(sim, RngStream.Births, id, SpouseAgeKey, 0), 5)); // 0–4 years younger
            var bride = new Citizen
            {
                Id = id, Name = Name(sim, RngStream.Births, id, true), Female = true, BornSeason = CitizenRules.BornFor(sim, brideAge), Health = Fixed.One,
                Spouse = son.Id, Village = village, Skill = Fixed.FromInt(sim.Config.Game.People.Hand.SkillMin + (int)Rng.Below(Roll(sim, RngStream.Births, id, SkillKey, 0),
                    (ulong)(sim.Config.Game.People.Hand.SkillMax - sim.Config.Game.People.Hand.SkillMin + 1))),
                Education = Rng.Chance(Roll(sim, RngStream.Births, id, EducationKey, 0), p.PrimaryEducationShare) ? 1 : 0,
            };
            Traits(sim, RngStream.Births, bride);
            son.Spouse = id;
            bride.Story.Add(new StoryLine(StoryKind.Married, t, son.Id));
            son.Story.Add(new StoryLine(StoryKind.Married, t, id));
            s.Citizens.Add(bride);

            Household home = x;
            Building free = FreeHouse(sim);
            if (free != null)
            {
                home = new Household { Id = s.NextHouseholdId++, HomeId = free.Id };
                x.Members.Remove(son.Id);
                home.Members.Add(son.Id);
                son.HouseholdId = home.Id;
                s.Households.Add(home);
            }
            bride.HouseholdId = home.Id;
            InsertMember(home, id);
            sim.Emit(new CitizenMarried(t, son.Id, son.Name, false, id, village, x.Id, home.Id));
        }

        /// <summary>A family house nobody lives in and not offered to a hand (§3.13), in building order; null if none.</summary>
        public static Building FreeHouse(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!HouseRules.IsHouse(sim, b) || b.Id == s.OfferHouseId) continue;
                if (s.Households.Find(x => x.HomeId == b.Id) == null) return b;
            }
            return null;
        }

        /// <summary>A seeded name of the gender, one no living citizen has if any is left.</summary>
        private static int Name(Simulation sim, RngStream stream, long id, bool female)
        {
            IReadOnlyList<NameDef> names = sim.Config.Game.People.Names;
            var free = new List<int>();
            var any = new List<int>();
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].Female != female) continue;
                any.Add(i);
                if (sim.State.Citizens.Citizens.Find(c => c.Living && c.Name == i) == null) free.Add(i);
            }
            List<int> from = free.Count > 0 ? free : any;
            return from[(int)Rng.Below(Roll(sim, stream, id, NameKey, 0), (ulong)from.Count)];
        }

        private static void Traits(Simulation sim, RngStream stream, Citizen c)
        {
            int n = sim.Config.Game.People.Traits.Count;
            c.Trait1 = (int)Rng.Below(Roll(sim, stream, c.Id, Trait1Key, 0), (ulong)n);
            c.Trait2 = (int)Rng.Below(Roll(sim, stream, c.Id, Trait2Key, 0), (ulong)(n - 1));
            if (c.Trait2 >= c.Trait1) c.Trait2++;
        }

        /// <summary>Members stay in ascending id order.</summary>
        private static void InsertMember(Household x, long id)
        {
            int at = x.Members.BinarySearch(id);
            if (at < 0) x.Members.Insert(~at, id);
        }
    }
}
