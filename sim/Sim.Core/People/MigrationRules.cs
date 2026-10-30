using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>
    /// Migration (spec 014 §3.10, §3.13): a free family house is offered first to a hand living alone; otherwise a family moves in
    /// every 18 ± 6 h, live or away; households that stay unhappy or fall behind on rent leave. Seeded (stream Migration).
    /// </summary>
    public static class MigrationRules
    {
        // Keys: (id × 16 + kind), as PopulationRules. Counters keep them apart from its name and trait draws (counter 0): a
        // household's draws 1, a new citizen's 2, a hand's answer 3; the wait is keyed by time, leaving by season (+ 4).
        private const ulong WaitKey = 1, SizeKey = 2, ElderKey = 3, AgeKey = 4, ChildAgeKey = 5, VillageKey = 6, SkillKey = 7, EducationKey = 8,
            LeaveRoll = 9, SpouseAgeKey = 10, ChildrenKey = 11, GenderKey = 12;

        private const long Hour = 3600;

        /// <summary>
        /// The house's rent taken off a hand's wage while his household lives in a family house (§3.6), else 0. Once a house (ADR 0083
        /// §2): only the household's first hand (by id) has it in his pay.
        /// </summary>
        public static long HousingInKind(Simulation sim, Hand h)
        {
            Citizen c = sim.State.Citizens?.OfHand(h.Id);
            Household x = c == null ? null : sim.State.Citizens.Household(c.HouseholdId);
            return x != null && HousedBy(sim, x) == h ? MarketRules.Indexed(sim, sim.Config.Game.Population.RentPerSeason) : 0;
        }

        /// <summary>The hand whose pay the household's family house is part of: its first hand by id; null if none or not in a house.</summary>
        public static Hand HousedBy(Simulation sim, Household x)
        {
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home == null || !HouseRules.IsHouse(sim, home)) return null;
            Hand first = null;
            foreach (long id in x.Members)
            {
                Citizen m = sim.State.Citizens.Citizen(id);
                if (m.Job != CitizenJob.Hand) continue;
                Hand h = sim.State.People.Hand(m.HandId);
                if (h != null && (first == null || h.Id < first.Id)) first = h;
            }
            return first;
        }

        /// <summary>A hand under contract whose household is just him in his hut (§3.13), first by id; null if none.</summary>
        public static Hand HandAlone(Simulation sim)
        {
            foreach (Hand h in sim.State.People.Hands)
                if (Alone(sim, h)) return h;
            return null;
        }

        private static bool Alone(Simulation sim, Hand h)
        {
            if (h.Contract != ContractState.Active || h.HutId == 0) return false;
            Citizen c = sim.State.Citizens.OfHand(h.Id);
            Household x = c == null ? null : sim.State.Citizens.Household(c.HouseholdId);
            return x != null && x.Members.Count == 1 && x.HomeId == h.HutId;
        }

        private static bool Free(Simulation sim, long houseId)
        {
            return sim.State.Land.Buildings.Exists(b => b.Id == houseId) && !sim.State.Citizens.Households.Exists(x => x.HomeId == houseId);
        }

        /// <summary>
        /// The hourly pass (§3.12): an open offer lapses after <c>familyEveryHours</c>; a free house is offered to a hand living alone
        /// first, else a family is due 18 ± 6 h after it fell free and moves in.
        /// </summary>
        internal static void Hourly(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            long t = sim.State.Clock.SimTime;
            if (s.OfferHandId != 0)
            {
                Hand h = sim.State.People.Hand(s.OfferHandId);
                bool open = h != null && Alone(sim, h) && Free(sim, s.OfferHouseId);
                if (open && t < s.OfferAt + sim.Config.Game.Population.FamilyEveryHours * Hour) return;
                CloseOffer(s);
                if (open) s.NextFamilyAt = t + Wait(sim, t); // unanswered: strangers may come
            }
            Building free = PopulationRules.FreeHouse(sim);
            if (free == null)
            {
                s.NextFamilyAt = 0;
                return;
            }
            if (s.NextFamilyAt == 0)
            {
                Hand alone = HandAlone(sim);
                if (alone != null)
                {
                    s.OfferHandId = alone.Id;
                    s.OfferHouseId = free.Id;
                    s.OfferAt = t;
                    sim.Emit(new FamilyRequest(t, alone.Id, free.Id));
                }
                else s.NextFamilyAt = t + Wait(sim, t);
                return;
            }
            if (t < s.NextFamilyAt) return;
            Arrive(sim, free);
            s.NextFamilyAt = 0;
        }

        private static void CloseOffer(CitizensState s)
        {
            s.OfferHandId = 0;
            s.OfferHouseId = 0;
            s.OfferAt = 0;
        }

        /// <summary>18 ± 6 h in seconds, seeded.</summary>
        private static long Wait(Simulation sim, long t)
        {
            PopulationDefs p = sim.Config.Game.Population;
            int jitter = p.FamilyJitterHours;
            long hours = p.FamilyEveryHours - jitter + (long)Rng.Below(PopulationRules.Roll(sim, RngStream.Migration, 0, WaitKey, t), (ulong)(2 * jitter + 1));
            return hours * Hour;
        }

        private const long ForHousehold = 1, ForCitizen = 2, ForAnswer = 3;

        private static int Between(Simulation sim, long id, ulong key, long counter, int min, int max)
        {
            return min + (int)Rng.Below(PopulationRules.Roll(sim, RngStream.Migration, id, key, counter), (ulong)(max - min + 1));
        }

        /// <summary>A new citizen with a seeded name, traits, skill and schooling (an adult's drawn as an applicant's; a child has none).</summary>
        private static Citizen Person(Simulation sim, Household x, bool female, int age, int village)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            PeopleDefs people = sim.Config.Game.People;
            long id = s.NextCitizenId++;
            bool adult = age >= p.AdultFrom;
            var c = new Citizen
            {
                Id = id, Name = PopulationRules.Name(sim, RngStream.Migration, id, female), Female = female, BornSeason = CitizenRules.BornFor(sim, age),
                HouseholdId = x.Id, Health = Fixed.One, Village = village,
                Skill = adult ? Fixed.FromInt(Between(sim, id, SkillKey, ForCitizen, people.Hand.SkillMin, people.Hand.SkillMax)) : CitizenRules.DrawFarming(sim, id), Crafting = CitizenRules.DrawCrafting(sim, id),
                Education = adult && Rng.Chance(PopulationRules.Roll(sim, RngStream.Migration, id, EducationKey, ForCitizen), p.PrimaryEducationShare) ? 1 : 0,
            };
            PopulationRules.Traits(sim, RngStream.Migration, c);
            c.Story.Add(new StoryLine(StoryKind.Arrived, sim.State.Clock.SimTime, 0));
            s.Citizens.Add(c);
            PopulationRules.InsertMember(x, id);
            return c;
        }

        private static void Marry(Citizen a, Citizen b)
        {
            a.Spouse = b.Id;
            b.Spouse = a.Id;
        }

        /// <summary>0–<paramref name="max"/> children of the couple, aged up to the mother's age less 20 (and under 18).</summary>
        private static void Children(Simulation sim, Household x, Citizen father, Citizen mother, int count, int village)
        {
            int oldest = System.Math.Min(sim.Config.Game.Population.AdultFrom - 1, System.Math.Max(0, CitizenRules.Age(sim, mother) - 20));
            for (int i = 0; i < count; i++)
            {
                long next = sim.State.Citizens.NextCitizenId;
                bool female = Rng.Chance(PopulationRules.Roll(sim, RngStream.Migration, next, GenderKey, ForCitizen), Fixed.FromRatio(1, 2));
                Citizen child = Person(sim, x, female, Between(sim, next, ChildAgeKey, ForCitizen, 0, oldest), village);
                child.Father = father.Id;
                child.Mother = mother.Id;
            }
        }

        /// <summary>
        /// A family moves into a free house (§3.10): 2–5 people, a couple, sometimes a widowed elder (his mother), the rest children;
        /// from one home village. They pay rent from the next close.
        /// </summary>
        private static void Arrive(Simulation sim, Building house)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            long t = sim.State.Clock.SimTime, key = s.NextHouseholdId;
            var x = new Household { Id = s.NextHouseholdId++, HomeId = house.Id };
            s.Households.Add(x);
            int size = Between(sim, key, SizeKey, ForHousehold, p.FamilySizeMin, p.FamilySizeMax);
            int village = Between(sim, key, VillageKey, ForHousehold, 0, p.Villages.Count - 1);
            int age = Between(sim, key, AgeKey, ForHousehold, p.MarriageAgeMin, p.ApplicantAgeMax);
            Citizen husband = Person(sim, x, false, age, village);
            Citizen wife = Person(sim, x, true, System.Math.Max(p.AdultFrom, age - Between(sim, key, SpouseAgeKey, ForHousehold, 0, 4)), village);
            Marry(husband, wife);
            int children = size - 2;
            if (children > 0 && Rng.Chance(PopulationRules.Roll(sim, RngStream.Migration, key, ElderKey, ForHousehold), p.FamilyElderChance))
            {
                Citizen elder = Person(sim, x, true, System.Math.Max(p.ElderFrom, age + 20 + Between(sim, key, ChildrenKey, ForHousehold, 0, 8)), village);
                husband.Mother = elder.Id;
                children--;
            }
            Children(sim, x, husband, wife, children, village);
            HouseholdRules.SetBudget(sim, x);
            sim.Emit(new FamilyMovedIn(t, x.Id, house.Id, x.Members.Count, husband.Name, 0));
        }

        /// <summary>
        /// The hand's answer (§3.13). Yes: his household moves into the house with a seeded spouse and 0–2 children from his home
        /// village, and his hut is free; his wage drops by the rent from his next renewal (§3.6). Not now: strangers may come.
        /// </summary>
        internal static Commands.CommandResult Answer(Simulation sim, long handId, bool yes)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null || s.OfferHandId == 0 || s.OfferHandId != handId) return Commands.CommandResult.Reject(Commands.RejectReason.NoOffer);
            Hand h = sim.State.People.Hand(handId);
            if (h == null || !Alone(sim, h)) return Commands.CommandResult.Reject(Commands.RejectReason.NoOffer);
            if (!Free(sim, s.OfferHouseId)) return Commands.CommandResult.Reject(Commands.RejectReason.NoEmptyHouse);
            long t = sim.State.Clock.SimTime, houseId = s.OfferHouseId;
            CloseOffer(s);
            if (!yes)
            {
                s.NextFamilyAt = t + Wait(sim, t);
                return Commands.CommandResult.Ok;
            }
            Citizen me = s.OfHand(handId);
            Household x = s.Household(me.HouseholdId);
            x.HomeId = houseId;
            h.HutId = 0;
            int age = CitizenRules.Age(sim, me), village = me.Village < 0 ? 0 : me.Village;
            int spouseAge = me.Female ? age + Between(sim, me.Id, SpouseAgeKey, ForAnswer, 0, 4) : System.Math.Max(sim.Config.Game.Population.AdultFrom, age - Between(sim, me.Id, SpouseAgeKey, ForAnswer, 0, 4));
            Citizen spouse = Person(sim, x, !me.Female, spouseAge, village);
            Marry(me, spouse);
            Children(sim, x, me.Female ? spouse : me, me.Female ? me : spouse, Between(sim, me.Id, ChildrenKey, ForAnswer, 0, 2), village);
            HouseholdRules.SetBudget(sim, x); // nothing sent home now: his family is here (§3.13)
            s.NextFamilyAt = 0;
            sim.Emit(new FamilyMovedIn(t, x.Id, houseId, x.Members.Count, me.Name, handId));
            return Commands.CommandResult.Ok;
        }

        /// <summary>
        /// Moving out at the close (§3.10, after the budgets): a household below <c>leaveBelow</c> for <c>leaveSeasons</c> closes in a
        /// row, or <c>rentBehindSeasons</c> behind, leaves with chance <c>leaveChance</c>; an elder alone behind on rent goes to family.
        /// </summary>
        internal static void MoveOut(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            PopulationDefs p = sim.Config.Game.Population;
            long season = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime);
            foreach (Household x in new List<Household>(s.Households))
            {
                x.LowSeasons = x.Happiness < Fixed.FromInt(p.LeaveBelow) ? x.LowSeasons + 1 : 0;
                bool rent = x.RentBehind >= p.RentBehindSeasons;
                if (x.LowSeasons < p.LeaveSeasons && !rent) continue;
                if (!Rng.Chance(PopulationRules.Roll(sim, RngStream.Migration, x.Id, LeaveRoll, season + 4), p.LeaveChance)) continue;
                bool elderAlone = x.Members.Count == 1 && HouseholdRules.IsElder(sim, s.Citizen(x.Members[0]));
                HouseRules.Evict(sim, x, !rent ? LeaveReason.Unhappy : elderAlone ? LeaveReason.ToFamily : LeaveReason.RentBehind);
            }
        }
    }
}
