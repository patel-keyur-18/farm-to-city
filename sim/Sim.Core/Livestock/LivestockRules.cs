using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Livestock
{
    /// <summary>Spec 012's formulas, shared by the system, the commands and the view.</summary>
    public static class LivestockRules
    {
        private static readonly Fixed Thousand = Fixed.FromInt(1000);

        public static LivestockDefs Defs(Simulation sim) { return sim.Config.Game.Livestock; }

        /// <summary>The finished cattle shed, or null (§3.3).</summary>
        public static Building Shed(Simulation sim)
        {
            if (sim.State.Livestock == null) return null;
            int def = Defs(sim).Shed;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def) return b;
            return null;
        }

        /// <summary>The shed can be built from the unlock era (§3.13).</summary>
        public static bool Unlocked(Simulation sim)
        {
            return sim.State.Livestock != null && (sim.State.Era == null || sim.State.Era.Current >= Defs(sim).UnlockEra);
        }

        /// <summary>Stalls the shed has (0 without a finished shed).</summary>
        public static int Stalls(Simulation sim)
        {
            Building b = Shed(sim);
            return b == null ? 0 : sim.Config.Game.Buildings[b.Def].Animals;
        }

        /// <summary>x a real day × realDaysPerGameDay (§3.1).</summary>
        public static Fixed PerGameDay(Simulation sim, Fixed perRealDay) { return perRealDay * Defs(sim).Livestock.RealDaysPerGameDay; }

        /// <summary>The lactation curve at day d (§3.2), piecewise linear, flat past its last point.</summary>
        public static Fixed Lactation(LivestockDef def, int day)
        {
            var curve = def.LactationCurve;
            Fixed d = Fixed.FromInt(day);
            if (d >= curve[curve.Count - 1].X) return curve[curve.Count - 1].Y;
            int i = 1;
            while (i < curve.Count - 1 && d > curve[i].X) i++;
            CurvePoint a = curve[i - 1], b = curve[i];
            return a.Y + (b.Y - a.Y) * (d - a.X) / (b.X - a.X);
        }

        /// <summary>The co-op's price a litre (§3.5): (base + perFatPoint × fat%) × PriceIndex, in coins.</summary>
        public static Fixed MilkPrice(Simulation sim, int kind)
        {
            LivestockDefs d = Defs(sim);
            return (d.Livestock.MilkBasePerLitre + d.Livestock.MilkPerFatPoint * d.Animals[kind].FatPercent) * sim.State.Market.PriceIndex;
        }

        /// <summary>Her milk for a whole game day now, millilitres, before the day's fed, health and Care factors (§3.5).</summary>
        public static Fixed BaseMilkMl(Simulation sim, Animal a, Season season)
        {
            if (a.Stage != AnimalStage.Milking) return Fixed.Zero;
            AnimalDef def = Defs(sim).Animals[a.Kind];
            return PerGameDay(sim, def.MilkPerDay) * Thousand * Lactation(Defs(sim).Livestock, a.StageDay) * def.SeasonMilk[(int)season];
        }

        /// <summary>max(minMilkFactor, fedScore) (§3.5).</summary>
        public static Fixed FedFactor(Simulation sim, Animal a)
        {
            Fixed min = Defs(sim).Livestock.MinMilkFactor;
            return a.FedScore < min ? min : a.FedScore;
        }

        /// <summary>The time-weighted Care ratio since the last milking, capped at 1 (§3.9); the ratio now if no time has passed.</summary>
        public static Fixed CareFactor(Simulation sim)
        {
            LivestockState s = sim.State.Livestock;
            Fixed mean = s.CareSeconds > 0 ? s.CareSum.MulRatio(1, s.CareSeconds) : sim.State.Farm.CareRatio;
            return mean > Fixed.One ? Fixed.One : mean;
        }

        /// <summary>
        /// Her milk over <paramref name="seconds"/> (§3.5): base × fed × health × Care × seconds ÷ 86,400, rounded to millilitres; 0 while dry
        /// or ill.
        /// </summary>
        public static long MilkMl(Simulation sim, Animal a, Season season, Fixed care, long seconds)
        {
            if (a.Stage != AnimalStage.Milking || a.SickDays > 0 || seconds <= 0) return 0;
            Fixed ml = BaseMilkMl(sim, a, season) * FedFactor(sim, a) * a.Health * care;
            return ml.MulRatio(seconds, ClockDef.SecondsPerDay).RoundToLong();
        }

        /// <summary>Hours a game day the animals need from the Care pool (§3.9).</summary>
        public static Fixed LabourDemand(Simulation sim)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null) return Fixed.Zero;
            Fixed hours = Fixed.Zero;
            foreach (Animal a in s.Animals) hours += Defs(sim).Animals[a.Kind].LabourHoursPerDay;
            return hours;
        }

        /// <summary>Her price at the fair now (§3.4): price × PriceIndex × sellShare × health.</summary>
        public static long SellPrice(Simulation sim, Animal a)
        {
            AnimalDef def = Defs(sim).Animals[a.Kind];
            long price = Market.MarketRules.Indexed(sim, def.Price);
            return Finance.Money.RoundToCoin(Finance.Money.MulRate(price, def.SellShare * a.Health));
        }

        public static Animal Find(Simulation sim, long id)
        {
            LivestockState s = sim.State.Livestock;
            return s == null ? null : s.Animals.Find(a => a.Id == id);
        }
    }
}
