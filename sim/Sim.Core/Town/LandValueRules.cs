using System;
using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;

namespace Sim.Core.Town
{
    /// <summary>One part of an acre's land value: its key (a string table row), what it adds, and the number it counts (businesses, households, or 0).</summary>
    public readonly struct LandPart
    {
        public string Key { get; }
        public Fixed Value { get; }
        public int Count { get; }

        public LandPart(string key, Fixed value, int count) { Key = key; Value = value; Count = count; }
    }

    /// <summary>
    /// Land value (US-143, spec 024 §3.1): <c>LV</c> starts at 1.00 and adds what is near an acre (the road, the school, the clinic, the square, water,
    /// businesses with workers, the town's size), held between the floor and the ceiling and read once a season at the close, so it never moves
    /// while the player looks. It exists from the Township on; before that every acre is 1.00 and price, rent and tax are as Phases 1 and 2 built them.
    /// </summary>
    public static class LandValueRules
    {
        public static LandValueDef Def(Simulation sim) { return sim.Config.Game?.Township?.LandValue; }

        /// <summary>From the Township's unlock (the old village joining), when the content has it.</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Township != null && sim.State.Township.Cohort.Joined; }

        /// <summary>The acre's value multiplier as last read (1.00 before the Township).</summary>
        public static Fixed Of(Simulation sim, int tileId)
        {
            if (!On(sim)) return Fixed.One;
            Fixed[] v = sim.State.Township.LandValues;
            return tileId >= 0 && tileId < v.Length ? v[tileId] : Fixed.One;
        }

        /// <summary>Rent's place for an acre from the Township: <c>base + slope × (LV − 1)</c> between min and max (spec 024 §3.2).</summary>
        public static Fixed RentPlace(Simulation sim, int tileId)
        {
            LandValueDef d = Def(sim);
            Fixed place = d.RentBase + d.RentSlope * (Of(sim, tileId) - Fixed.One);
            return place < d.RentMin ? d.RentMin : place > d.RentMax ? d.RentMax : place;
        }

        /// <summary>The parts that make an acre's value now, in order, each with what it adds; zero parts are left out. Pure: the same inputs give the same list.</summary>
        public static List<LandPart> Parts(Simulation sim, int tileId)
        {
            LandValueDef d = Def(sim);
            var list = new List<LandPart>();
            int road = LeaseRules.RoadAccess(sim, tileId);
            if (road == 2 && d.RoadDistrict > Fixed.Zero) list.Add(new LandPart("district", d.RoadDistrict, 0));
            else if (road == 1 && d.RoadLane > Fixed.Zero) list.Add(new LandPart("lane", d.RoadLane, 0));
            SchoolDef school = SchoolRules.Def(sim);
            if (school != null && SchoolRules.IsOpen(sim) && CoverageRules.Within(sim, tileId, school.Tile, school.Reach)) list.Add(new LandPart("school", d.School, 0));
            ClinicDef clinic = ClinicRules.Def(sim);
            if (clinic != null && ClinicRules.IsOpen(sim) && CoverageRules.Within(sim, tileId, clinic.Tile, clinic.Reach)) list.Add(new LandPart("clinic", d.Clinic, 0));
            if (CoverageRules.Within(sim, tileId, d.SquareTile, d.SquareReach)) list.Add(new LandPart("square", d.Square, 0));
            if (HasWater(sim, tileId)) list.Add(new LandPart("water", d.Water, 0));
            int jobs = Jobs(sim, d, tileId);
            if (jobs > 0) list.Add(new LandPart("jobs", d.JobEach * Fixed.FromInt(jobs), jobs));
            int hh = EraRules.Households(sim);
            int steps = hh > d.SizeOver ? (hh - d.SizeOver) / 10 : 0;
            Fixed size = d.SizePer10 * Fixed.FromInt(steps);
            if (size > d.SizeMax) size = d.SizeMax;
            if (size > Fixed.Zero) list.Add(new LandPart("size", size, hh));
            Fixed smoke = SmokeRules.LandValue(sim, tileId, out int smokers);
            if (smoke > Fixed.Zero) list.Add(new LandPart("smoke", Fixed.Zero - smoke, smokers)); // a kiln or a generator's smoke (spec 025)
            if (UtilityRules.DrainsMissing(sim, tileId)) list.Add(new LandPart("drains", Fixed.Zero - UtilityRules.Defs(sim).DrainsLandValue, 0)); // a home with no drains (spec 025 §3.1)
            Fixed crime = TownLifeRules.LandValueTerm(sim, tileId);
            if (crime != Fixed.Zero) list.Add(new LandPart("crime", crime, 0)); // spec 026 §3.5: safe acres are worth more, unsafe ones less
            return list;
        }

        /// <summary>The acre's value now: 1.00 plus its parts, held between the floor and the ceiling.</summary>
        public static Fixed Compute(Simulation sim, int tileId)
        {
            LandValueDef d = Def(sim);
            Fixed lv = Fixed.One;
            foreach (LandPart p in Parts(sim, tileId)) lv += p.Value;
            return lv < d.Floor ? d.Floor : lv > d.Ceiling ? d.Ceiling : lv;
        }

        /// <summary>The pond, a well or a working water tower in reach (the tower's own 400 m; spec 025 §3.1).</summary>
        public static bool HasWater(Simulation sim, int tileId) { return UtilityRules.HasWater(sim, tileId); }

        /// <summary>The pond, or any well, within the water reach (a pump or a borewell waters fields and does not count; spec 024 §3.1).</summary>
        public static bool PondOrWell(Simulation sim, int tileId)
        {
            LandValueDef d = Def(sim);
            if (CoverageRules.Within(sim, tileId, d.WaterTile, d.WaterReach)) return true;
            foreach (Building b in sim.State.Land.Buildings)
                if (b.TileId >= 0 && sim.Config.Game.Buildings[b.Def].Id == BuildingDef.WellId && CoverageRules.Within(sim, tileId, b.TileId, d.WaterReach)) return true;
            return false;
        }

        /// <summary>Open businesses with a hired worker within the job reach, at most the cap.</summary>
        private static int Jobs(Simulation sim, LandValueDef d, int tileId)
        {
            BusinessesState bs = sim.State.Businesses;
            if (bs == null || sim.State.People == null) return 0;
            int n = 0;
            foreach (Business.Business b in bs.Businesses)
            {
                if (b.Status != BusinessStatus.Open || b.TileId < 0 || sim.State.People.StaffAt(b.Id) == null) continue;
                if (CoverageRules.Within(sim, tileId, b.TileId, d.JobReach) && ++n >= d.JobMax) break;
            }
            return n;
        }

        /// <summary>Reads every acre's value once (spec 024 §3.1): at the Township's unlock and at each close, before the season's leases renew.</summary>
        internal static void Read(Simulation sim)
        {
            if (!On(sim)) return;
            LandState land = sim.State.Land;
            var v = new Fixed[land.Tiles.Count];
            for (int i = 0; i < v.Length; i++) v[i] = Compute(sim, i);
            sim.State.Township.LandValues = v;
        }
    }
}
