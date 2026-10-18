using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Farm
{
    public enum SprayState
    {
        None = 0,
        BeforeCheck = 1,
        AfterWarning = 2,
    }

    public enum Outbreak
    {
        None = 0,
        Normal = 1,
        Severe = 2,
    }

    /// <summary>Spec 003 §3.8's pest and disease rules (ADR 0014, 0034), pure functions.</summary>
    public static class PestRules
    {
        /// <summary>
        /// risk = baseRisk × (1 + monocultureStep × min(repeats, max)) × (1 + wetStep × wetShare) × (spray before ? factor : 1),
        /// at most 1.
        /// </summary>
        public static Fixed Risk(PestRiskDef p, int crop, int repeats, Fixed wetShare, bool sprayedBefore)
        {
            int n = repeats < p.MonocultureMaxRepeats ? repeats : p.MonocultureMaxRepeats;
            Fixed risk = p.BaseRisk[crop] * (Fixed.One + p.MonocultureStep * Fixed.FromInt(n)) * (Fixed.One + p.WetStep * wetShare);
            if (sprayedBefore) risk = risk * p.SprayRiskFactor;
            return risk > Fixed.One ? Fixed.One : risk;
        }

        /// <summary>
        /// The seeded roll for one crop (stream Pest, counter = crop id; keys 0, 1, 2 for the outbreak, the severity and
        /// the health): the rolled CropHealth, 1 with no outbreak. A severe result rolled while away becomes a normal
        /// outbreak at the band's minimum (ADR 0002, 0014).
        /// </summary>
        public static Fixed Roll(PestRiskDef p, ulong seed, long cropId, Fixed risk, bool away, out Outbreak outbreak)
        {
            outbreak = Outbreak.None;
            if (!Rng.Chance(Rng.Next(seed, RngStream.Pest, 0, (ulong)cropId), risk)) return Fixed.One;
            if (Rng.Chance(Rng.Next(seed, RngStream.Pest, 1, (ulong)cropId), p.SevereShare))
            {
                if (!away) { outbreak = Outbreak.Severe; return p.SevereHealth; }
                outbreak = Outbreak.Normal;
                return p.OutbreakHealthMin;
            }
            outbreak = Outbreak.Normal;
            Fixed w = Fixed.FromRaw((long)(Rng.Next(seed, RngStream.Pest, 2, (ulong)cropId) >> 32));
            return p.OutbreakHealthMin + (p.OutbreakHealthMax - p.OutbreakHealthMin) * w;
        }

        /// <summary>The CropHealth applied at readiness: 1, the rolled health, or 1 − (1 − rolled) × damageFactor if sprayed after the warning.</summary>
        public static Fixed FinalHealth(PestRiskDef p, Crop c)
        {
            if (c.Outbreak == Outbreak.None) return Fixed.One;
            if (c.Spray == SprayState.AfterWarning) return Fixed.One - (Fixed.One - c.RolledHealth) * p.SprayDamageFactor;
            return c.RolledHealth;
        }

        /// <summary>
        /// Consecutive harvests of this crop on the tile before a new sowing (ADR 0034): 0 after a different crop, or if
        /// the tile lay fallow for a whole day (a daily pass at least a day after it became vacant; spec 002).
        /// </summary>
        internal static int RepeatsAt(Simulation sim, int tileId, int crop)
        {
            FarmState farm = sim.State.Farm;
            if (farm.TileRunCrop[tileId] != crop) return 0;
            long day = ClockDef.SecondsPerDay;
            long firstFallowPass = ((sim.State.Land.Tiles[tileId].VacantSince + day + day - 1) / day) * day;
            return firstFallowPass <= sim.State.Clock.SimTime ? 0 : farm.TileRun[tileId];
        }

        /// <summary>The spray dose for one cycle of the crop on an acre, in the spray good's units (ml).</summary>
        public static long Dose(Simulation sim, CropDef crop)
        {
            return (sim.Config.Game.Farm.Pests.SprayPerAcrePerSeason * FarmRules.CycleShare(sim.Clock, crop)).RoundToLong();
        }
    }
}
