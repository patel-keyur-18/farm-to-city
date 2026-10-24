using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>Citizens and households (spec 014 §3.1–3.4, §3.11): ages, a hand becoming a citizen, leaving.</summary>
    public static class CitizenRules
    {
        // Keys of the Citizens stream: one per kind of draw, the counter is the citizen, applicant or hand id.
        private const ulong ApplicantAgeKey = 1, EducationKey = 2, VillageKey = 3, MigratedAgeKey = 4;

        /// <summary>The season index of a time (t ÷ S).</summary>
        public static long SeasonOf(Simulation sim, long t) { return t / sim.Clock.SecondsPerSeason; }

        /// <summary>Whole years of age now (§3.2): one a season by default.</summary>
        public static int Age(Simulation sim, Citizen c)
        {
            long at = c.Living ? sim.State.Clock.SimTime : c.GoneAt;
            return (int)((SeasonOf(sim, at) - c.BornSeason) * sim.Config.Game.Population.LifeYearsPerSeason);
        }

        /// <summary>The born season for someone <paramref name="age"/> now.</summary>
        internal static long BornFor(Simulation sim, int age)
        {
            int perSeason = sim.Config.Game.Population.LifeYearsPerSeason;
            return SeasonOf(sim, sim.State.Clock.SimTime) - (age + perSeason - 1) / perSeason;
        }

        /// <summary>An applicant's age (§3.11): a seeded draw in the applicant range, the same for the applicant and the hand he becomes.</summary>
        public static int ApplicantAge(Simulation sim, Applicant a)
        {
            PopulationDefs p = sim.Config.Game.Population;
            return Draw(sim, ApplicantAgeKey, (ulong)a.Id, p.ApplicantAgeMin, p.ApplicantAgeMax);
        }

        private static int Draw(Simulation sim, ulong key, ulong counter, int min, int max)
        {
            return min + (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Citizens, key, counter), (ulong)(max - min + 1));
        }

        /// <summary>A new adult (§3.3): education drawn (primary or none), health 1, a home village; not yet in a household.</summary>
        private static Citizen NewAdult(Simulation sim, int name, int age, int trait1, int trait2, Fixed skill)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            long id = s.NextCitizenId++;
            var c = new Citizen
            {
                Id = id, Name = name, Female = sim.Config.Game.People.Names[name].Female, BornSeason = BornFor(sim, age),
                Education = Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Citizens, EducationKey, (ulong)id), p.PrimaryEducationShare) ? 1 : 0,
                Skill = skill, Trait1 = trait1, Trait2 = trait2, Health = Fixed.One,
                Village = Draw(sim, VillageKey, (ulong)id, 0, p.Villages.Count - 1),
            };
            c.Story.Add(new StoryLine(StoryKind.Arrived, sim.State.Clock.SimTime, 0));
            s.Citizens.Add(c);
            return c;
        }

        /// <summary>A household of one in a hand's hut (§3.4): his family stays in his home village for now.</summary>
        private static void HouseAlone(Simulation sim, Citizen c, long homeId)
        {
            CitizensState s = sim.State.Citizens;
            var x = new Household { Id = s.NextHouseholdId++, HomeId = homeId };
            x.Members.Add(c.Id);
            c.HouseholdId = x.Id;
            s.Households.Add(x);
        }

        /// <summary>A hired applicant becomes a citizen with a household of one in his hut (spec 014 §3.4, §3.11).</summary>
        internal static void HandHired(Simulation sim, Hand h, Applicant a)
        {
            if (sim.State.Citizens == null) return;
            Citizen c = NewAdult(sim, h.Name, ApplicantAge(sim, a), h.Trait1, h.Trait2, h.Skill);
            Hire(sim, c, h);
            HouseAlone(sim, c, h.HutId);
        }

        private static void Hire(Simulation sim, Citizen c, Hand h)
        {
            c.Job = CitizenJob.Hand;
            c.HandId = h.Id;
            c.Story.Add(new StoryLine(StoryKind.Hired, sim.State.Clock.SimTime, 0));
        }

        /// <summary>
        /// A hand leaves the job (spec 006). Living alone in his hut, he goes home to his village: his household leaves (§3.13). A hand whose
        /// family lives in a family house stays on as a tenant (US-208 onward).
        /// </summary>
        internal static void HandLeft(Simulation sim, Hand h)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return;
            Citizen c = s.OfHand(h.Id);
            if (c == null) return;
            c.Job = CitizenJob.None;
            c.HandId = 0;
            Household x = s.Household(c.HouseholdId);
            if (x != null && x.HomeId == h.HutId) Leave(sim, x);
        }

        /// <summary>A household moves away: its members leave the town, kept only while a living citizen links to them (§3.3).</summary>
        internal static void Leave(Simulation sim, Household x)
        {
            CitizensState s = sim.State.Citizens;
            long t = sim.State.Clock.SimTime;
            foreach (long id in x.Members)
            {
                Citizen c = s.Citizen(id);
                c.Gone = CitizenGone.Left;
                c.GoneAt = t;
                c.Job = CitizenJob.None;
                c.HandId = 0;
                c.Story.Add(new StoryLine(StoryKind.Left, t, 0));
            }
            s.Households.Remove(x);
            Prune(sim);
        }

        /// <summary>
        /// Drops the departed no living citizen links to as a spouse, child or grandchild (§3.3): never a count cap, so the tree
        /// stays about two generations deep.
        /// </summary>
        internal static void Prune(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            s.Citizens.RemoveAll(c => !c.Living && !Linked(s, c));
        }

        private static bool Linked(CitizensState s, Citizen gone)
        {
            foreach (Citizen c in s.Citizens)
            {
                if (!c.Living) continue;
                if (c.Spouse == gone.Id || c.Father == gone.Id || c.Mother == gone.Id) return true;
                Citizen parent = c.Father != 0 ? s.Citizen(c.Father) : null, mother = c.Mother != 0 ? s.Citizen(c.Mother) : null;
                if (parent != null && (parent.Father == gone.Id || parent.Mother == gone.Id)) return true;
                if (mother != null && (mother.Father == gone.Id || mother.Mother == gone.Id)) return true;
            }
            return false;
        }

        /// <summary>
        /// Save schema 9 (§3.11): each hand of an older town becomes a citizen, age drawn 22–45, the gender his name has, a household of
        /// one in his hut, education drawn, health 1. True if the town gained the citizens section.
        /// </summary>
        internal static bool Migrate(Simulation sim)
        {
            if (sim.Config.Game?.Population == null || sim.State.People == null) return false;
            PopulationDefs p = sim.Config.Game.Population;
            sim.State.Citizens = new CitizensState();
            foreach (Hand h in sim.State.People.Hands)
            {
                Citizen c = NewAdult(sim, h.Name, Draw(sim, MigratedAgeKey, (ulong)h.Id, p.MigratedHandAgeMin, p.MigratedHandAgeMax), h.Trait1, h.Trait2, h.Skill);
                Hire(sim, c, h);
                HouseAlone(sim, c, h.HutId);
            }
            return true;
        }
    }
}
