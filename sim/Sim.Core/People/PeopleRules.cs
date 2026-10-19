using System.Numerics;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>Hired labour, wages and huts (spec 006 §3.1–3.6).</summary>
    public static class PeopleRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        /// <summary>
        /// HandHours = hoursPerDay × (productivityBase + productivityPerSkill × skill ÷ 100) × Π traitHoursFactor (spec 006 §3.1).
        /// </summary>
        public static Fixed HandHours(Simulation sim, Hand h)
        {
            PeopleDefs p = sim.Config.Game.People;
            Fixed hours = p.Hand.HoursPerDay * (p.Hand.ProductivityBase + p.Hand.ProductivityPerSkill * h.Skill.MulRatio(1, 100));
            return hours * p.Traits[h.Trait1].HoursFactor * p.Traits[h.Trait2].HoursFactor;
        }

        /// <summary>Day labourers' hours a day while they're hired today.</summary>
        public static Fixed DayLabourHours(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s == null) return Fixed.Zero;
            int n = 0;
            foreach (Neighbour x in s.Roster) if (x.HiredToday) n++;
            return sim.Config.Game.People.Pool.HoursPerDay.MulRatio(n, 1);
        }

        /// <summary>Hired hours a day into the Care pool: day labourers plus hands under an active contract.</summary>
        public static Fixed HiredHours(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s == null) return Fixed.Zero;
            Fixed hours = DayLabourHours(sim);
            foreach (Hand h in s.Hands) if (h.Contract == ContractState.Active) hours += HandHours(sim, h);
            return hours;
        }

        /// <summary>supplyPerDay of the Care ratio (spec 003 §3.6, spec 006 §3.1): the founder's field hours plus hired hours.</summary>
        public static Fixed CareSupply(Simulation sim)
        {
            return FounderRules.FieldHours(sim) + HiredHours(sim);
        }

        /// <summary>
        /// The asked season wage: marketWage × (wageBase + wagePerSkill × skill ÷ 100) × the price index, in whole coins
        /// (spec 006 §3.5); 1.10 × that through an above-market post.
        /// </summary>
        public static long AskedWage(Simulation sim, Fixed skill, bool aboveMarket)
        {
            HandDef h = sim.Config.Game.People.Hand;
            Fixed factor = h.WageBase + h.WagePerSkill * skill.MulRatio(1, 100);
            if (aboveMarket) factor = factor * sim.Config.Game.People.JobPost.AboveMarketFactor;
            return Money.RoundToCoin(MarketRules.Indexed(sim, Money.MulRate(h.MarketWage, factor)));
        }

        /// <summary>The shown skill range: skill ± skillRangeHalfWidth, clamped to 0–100 (a regular's is exact).</summary>
        public static void SkillRange(Simulation sim, Applicant a, out int low, out int high)
        {
            int w = a.SkillKnown ? 0 : sim.Config.Game.People.Hand.SkillRangeHalfWidth;
            low = System.Math.Max(0, a.Skill - w);
            high = System.Math.Min(100, a.Skill + w);
        }

        /// <summary>The next daily pass after t.</summary>
        public static long NextDailyPass(long t) { return (t / Day + 1) * Day; }

        /// <summary>Cost of <paramref name="count"/> day labourers for the rest of today: dailyWage × (nextDailyPass − t) ÷ 86,400 each.</summary>
        public static long DayLabourCost(Simulation sim, int count)
        {
            long t = sim.State.Clock.SimTime;
            long wage = MarketRules.Indexed(sim, sim.Config.Game.People.Pool.DailyWage);
            return (long)((BigInteger)wage * count * (NextDailyPass(t) - t) / Day);
        }

        /// <summary>Labourers free today and not yet hired.</summary>
        public static int FreeNow(Simulation sim)
        {
            int n = 0;
            foreach (Neighbour x in sim.State.People.Roster) if (x.FreeToday && !x.HiredToday) n++;
            return n;
        }

        /// <summary>The rest of the season's wage: wage × (seasonEnd − t) ÷ S, rounded down.</summary>
        public static long RestOfSeason(Simulation sim, long wage)
        {
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            return (long)((BigInteger)wage * ((t / S + 1) * S - t) / S);
        }

        public static bool IsHandHut(Simulation sim, Building b) { return b.Def == sim.Config.Game.People.HandHut; }

        /// <summary>The first finished hand's hut (by id) with no hand in it, or null.</summary>
        public static Building EmptyHut(Simulation sim)
        {
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!IsHandHut(sim, b)) continue;
                if (sim.State.People.Hands.Find(h => h.HutId == b.Id) == null) return b;
            }
            return null;
        }

        /// <summary>Hands' huts on a tile, finished or being built.</summary>
        public static int HutsOn(Simulation sim, int tileId)
        {
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings) if (IsHandHut(sim, b) && b.TileId == tileId) n++;
            foreach (Construction c in sim.State.People.Constructions) if (c.TileId == tileId) n++;
            return n;
        }

        /// <summary>All hands' huts, finished or being built.</summary>
        public static int HutCount(Simulation sim)
        {
            int n = sim.State.People.Constructions.Count;
            foreach (Building b in sim.State.Land.Buildings) if (IsHandHut(sim, b)) n++;
            return n;
        }

        /// <summary>
        /// Why a hut can't go on this tile now (ADR 0040), or None: the inherited labour quarters with a free hut place, under the cap.
        /// Cash isn't checked here (the command does it).
        /// </summary>
        public static Commands.RejectReason HutPlace(Simulation sim, int tileId)
        {
            if (tileId < 0 || tileId >= sim.State.Land.Tiles.Count) return Commands.RejectReason.UnknownTile;
            Tile tile = sim.State.Land.Tiles[tileId];
            if (tile.Terrain != Terrain.Quarters || !sim.State.Land.Parcels[tile.ParcelId].Protected) return Commands.RejectReason.NotQuarters;
            if (HutsOn(sim, tileId) >= sim.Config.Game.People.HutPlacesPerAcre) return Commands.RejectReason.NoRoom;
            if (HutCount(sim) >= sim.Config.Game.People.Hand.MaxHandHuts) return Commands.RejectReason.TooManyHuts;
            return Commands.RejectReason.None;
        }

        internal static bool HasTrait(Simulation sim, Hand h, System.Func<TraitDef, bool> test)
        {
            var traits = sim.Config.Game.People.Traits;
            return test(traits[h.Trait1]) || test(traits[h.Trait2]);
        }

        /// <summary>
        /// Seconds until the next applicant (spec 006 §3.5): applicantEveryHours ± up to applicantJitterHours, both × rateFactor
        /// while a job is posted. Seeded by the applicant's id (stream Applicants, draw 0).
        /// </summary>
        internal static long ApplicantInterval(Simulation sim, long applicantId, bool posted)
        {
            PeopleDefs p = sim.Config.Game.People;
            Fixed factor = posted ? p.JobPost.RateFactor : Fixed.One;
            long baseSec = factor.MulRatio(p.Hand.ApplicantEveryHours * 3600L, 1).RoundToLong();
            long jitter = factor.MulRatio(p.Hand.ApplicantJitterHours * 3600L, 1).RoundToLong();
            ulong r = Rng.Next(sim.State.Seed, RngStream.Applicants, (ulong)applicantId, 0);
            return baseSec - jitter + (long)Rng.Below(r, (ulong)(2 * jitter + 1));
        }
    }
}
