using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
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

        /// <summary>The finished cattle shed on the cattle acre, or null (§3.3).</summary>
        public static Building Shed(Simulation sim)
        {
            if (sim.State.Livestock == null) return null;
            int def = Defs(sim).Shed, acre = Defs(sim).Livestock.ShedTile;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def && b.TileId == acre) return b;
            return null;
        }

        /// <summary>The finished second shed on a field of yours (ADR 0088), or null.</summary>
        public static Building SecondShed(Simulation sim)
        {
            if (sim.State.Livestock == null) return null;
            int def = Defs(sim).Shed, acre = Defs(sim).Livestock.ShedTile;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == def && b.TileId != acre) return b;
            return null;
        }

        /// <summary>The field a second shed stands on or is being built on, or −1.</summary>
        public static int SecondShedTile(Simulation sim)
        {
            if (sim.State.Livestock == null) return -1;
            Building b = SecondShed(sim);
            return b != null ? b.TileId : sim.State.Livestock.ShedReadyAt > 0 ? sim.State.Livestock.ShedTile : -1;
        }

        /// <summary>The shed can be built from the unlock era (§3.13).</summary>
        public static bool Unlocked(Simulation sim)
        {
            return sim.State.Livestock != null && (sim.State.Era == null || sim.State.Era.Current >= Defs(sim).UnlockEra);
        }

        /// <summary>Places in one shed (0 the cattle acre's, 1 the second): 0 while it isn't finished.</summary>
        public static int Stalls(Simulation sim, int shed)
        {
            Building b = shed == 0 ? Shed(sim) : SecondShed(sim);
            return b == null ? 0 : sim.Config.Game.Buildings[b.Def].Animals;
        }

        /// <summary>Places over both sheds (ADR 0088).</summary>
        public static int Stalls(Simulation sim) { return Stalls(sim, 0) + Stalls(sim, 1); }

        public static int InShed(Simulation sim, int shed)
        {
            int n = 0;
            foreach (Animal a in sim.State.Livestock.Animals) if (a.Shed == shed) n++;
            return n;
        }

        /// <summary>Where a cow or a buffalo goes: the first shed, then the second (ADR 0088); −1 when both are full.</summary>
        public static int PlaceFor(Simulation sim)
        {
            for (int shed = 0; shed < 2; shed++) if (InShed(sim, shed) < Stalls(sim, shed)) return shed;
            return -1;
        }

        /// <summary>
        /// Whether the second shed may go on this field (ADR 0088): from the Hamlet, the first shed standing, one second shed; a farm acre
        /// you own with nothing growing or standing on it.
        /// </summary>
        public static RejectReason PlaceSecond(Simulation sim, int tileId)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null || tileId < 0 || tileId >= sim.State.Land.Tiles.Count) return RejectReason.UnknownTile;
            if (!Unlocked(sim)) return RejectReason.EraLocked;
            if (Shed(sim) == null) return RejectReason.NoShed;
            if (SecondShedTile(sim) >= 0) return RejectReason.AlreadyBuilt;
            Tile tile = sim.State.Land.Tiles[tileId];
            if (!sim.State.Land.Parcels[tile.ParcelId].Owned) return RejectReason.NotOwned;
            if (tile.Terrain != Terrain.Farm) return RejectReason.TerrainNotAllowed;
            if (tile.Occupied || sim.State.Farm.CropOn(tileId) != null || People.HouseRules.HousesOn(sim, tileId) > 0 || Business.BusinessRules.On(sim, tileId) != null)
                return RejectReason.TileOccupied;
            foreach (Building b in sim.State.Land.Buildings) if (b.TileId == tileId) return RejectReason.TileOccupied;
            return RejectReason.None;
        }

        /// <summary>
        /// Foreclosure takes bought land (ADR 0088): a second shed on it goes with the field at its book value (one being built at its
        /// cost). Its animals move to free places in the first shed; those that don't fit are seized at their fair price. Returns the
        /// value raised.
        /// </summary>
        internal static long Seize(Simulation sim, int parcelId, long period)
        {
            int tile = SecondShedTile(sim);
            if (tile < 0 || sim.State.Land.Tiles[tile].ParcelId != parcelId) return 0;
            LivestockState s = sim.State.Livestock;
            var ledger = sim.State.Ledger;
            long t = sim.State.Clock.SimTime, raised;
            Building b = SecondShed(sim);
            if (b != null)
            {
                raised = b.Cost - b.Depreciated;
                ledger.Post(t, period, "ledger.shedSeized", "building:" + b.Id, new Posting(Account.Cash, raised),
                    new Posting(Account.AccumulatedDepreciation, b.Depreciated), new Posting(Account.Buildings, -b.Cost));
                sim.State.Land.Buildings.Remove(b);
            }
            else
            {
                raised = s.ShedCost;
                ledger.Post(t, period, "ledger.shedSeized", "shed", new Posting(Account.Cash, raised), new Posting(Account.Buildings, -raised));
                foreach (Scheduling.Job j in sim.State.Jobs.All)
                    if (j.Type == LivestockSystem.ShedJob && j.State == Scheduling.JobState.Running) { sim.CompleteJob(j); break; }
                s.ShedReadyAt = 0;
                s.ShedCost = 0;
                s.ShedTile = -1;
            }
            LandSystem.SetOccupied(sim, tile, false);
            Farm.FarmSystem.Accrue(sim);
            foreach (Animal a in s.Animals.ToArray())
            {
                if (a.Shed != 1) continue;
                if (InShed(sim, 0) < Stalls(sim, 0)) { a.Shed = 0; continue; }
                long value = SellPrice(sim, a), gain = value - a.Cost;
                ledger.Post(t, period, "ledger.animalSeized", "animal:" + a.Id, new Posting(Account.Cash, value), new Posting(Account.Livestock, -a.Cost),
                    gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
                s.Animals.Remove(a);
                raised += value;
                sim.Emit(new Events.AnimalSeized(t, a.Id, a.Name, value));
            }
            return raised;
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

        /// <summary>Her chance of falling ill this morning (§3.10): diseaseChance × (vaccinated this season ? 1 : unvaccinatedFactor) × (1 + sickHealthFactor × (1 − health)).</summary>
        public static Fixed DiseaseChance(Simulation sim, Animal a)
        {
            LivestockDef ld = Defs(sim).Livestock;
            return ld.DiseaseChance * (a.VaccinatedPeriod == sim.Period ? Fixed.One : ld.UnvaccinatedFactor) * (Fixed.One + ld.SickHealthFactor * (Fixed.One - a.Health));
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
        /// <summary>Game days the straw or the feed in stock lasts at today's need (long.MaxValue with no need).</summary>
        public static long DaysLeft(Simulation sim, int good)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            long need = 0;
            foreach (Animal a in sim.State.Livestock.Animals)
            {
                var n = LivestockSystem.NeedOf(sim, a);
                need += good == defs.Straw ? n.Straw : n.Feed;
            }
            return need <= 0 ? long.MaxValue : sim.State.Market.Stock[good] / need;
        }

    }
}
