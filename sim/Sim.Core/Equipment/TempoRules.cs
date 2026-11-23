using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Equipment
{
    /// <summary>What carries a mandi trip (US-245, spec 028 §3.6, ADR 0162): your own cart or tractor-trolley, the tempo, or the hired trolley.</summary>
    public enum Ride { Own = 0, Tempo = 1, Hired = 2 }

    /// <summary>The ride chosen for a load: who carries it, what it costs now, how long it takes, and the legs a tempo makes.</summary>
    public readonly struct MandiRide
    {
        public Ride Ride { get; }
        public long Cost { get; }
        public int Hours { get; }
        public int Legs { get; }

        public MandiRide(Ride ride, long cost, int hours, int legs)
        {
            Ride = ride;
            Cost = cost;
            Hours = hours;
            Legs = legs;
        }
    }

    /// <summary>
    /// The tempo (spec 028 §3.6, US-245, ADR 0162): a vehicle for the mandi. A send is one or two legs of 1,000 kg (4 hours, 300 a leg), the tempo has two legs a day, and the
    /// cheapest ride carries a load: your own cart (free), then the tempo, then the hired trolley. Let out, the legs you did not use go to neighbours at the end of the day.
    /// </summary>
    public static class TempoRules
    {
        /// <summary>Your tempo, bought or leased, or null.</summary>
        public static Machine Of(Simulation sim)
        {
            if (!EquipmentRules.On(sim)) return null;
            foreach (Machine m in sim.State.Equipment.Machines) if (EquipmentRules.Defs(sim).Machines[m.Kind].IsTempo) return m;
            return null;
        }

        public static TripDef Trip(Simulation sim, Machine m) { return EquipmentRules.Defs(sim).Machines[m.Kind].Trip; }

        private static long Day(Simulation sim) { return sim.State.Clock.SimTime / ClockDef.SecondsPerDay; }

        /// <summary>Legs it has made today for you.</summary>
        public static int UsedToday(Simulation sim, Machine m) { return m.LegsDay == Day(sim) + 1 ? m.LegsUsed : 0; }

        /// <summary>Legs it can still make today: none while broken or unpaid.</summary>
        public static int LegsLeft(Simulation sim, Machine m)
        {
            if (m == null || !EquipmentRules.Running(sim, m)) return 0;
            return System.Math.Max(0, Trip(sim, m).LegsPerDay - UsedToday(sim, m));
        }

        /// <summary>A leg's cost now: diesel (moved by a fuel spike) and wear.</summary>
        public static long Diesel(Simulation sim, TripDef t) { return Money.MulRate(MarketRules.Indexed(sim, t.Diesel), Town.EventRules.CostFactor(sim, Town.EventEffect.Fuel, 0)); }

        public static long Wear(Simulation sim, TripDef t) { return MarketRules.Indexed(sim, t.Wear); }

        public static long LegCost(Simulation sim, TripDef t) { return Diesel(sim, t) + Wear(sim, t); }

        /// <summary>What neighbours pay for a leg of the tempo, at today's prices.</summary>
        public static long NeighbourPay(Simulation sim, TripDef t) { return MarketRules.Indexed(sim, t.NeighbourPay); }

        /// <summary>The hired trolley's price for a trip against which the tempo is compared.</summary>
        public static long Trolley(Simulation sim) { return MarketRules.Indexed(sim, sim.Config.Game.Market.Mandi.CartHirePerTrip); }

        /// <summary>
        /// The cheapest ride for a load of <paramref name="grams"/> now (spec §3.6): your own cart or tractor-trolley if it fits, else the tempo if it has the legs for the load
        /// (one leg up to its load, two up to twice), else the hired trolley.
        /// </summary>
        public static MandiRide Choose(Simulation sim, long grams)
        {
            MandiDef def = sim.Config.Game.Market.Mandi;
            if (grams <= EquipmentRules.CartGrams(sim)) return new MandiRide(Ride.Own, 0, def.TripHours, 0);
            Machine m = Of(sim);
            if (m != null)
            {
                TripDef t = Trip(sim, m);
                long per = t.LoadKg * 1000;
                int legs = (int)((grams + per - 1) / per);
                if (legs >= 1 && legs <= LegsLeft(sim, m)) return new MandiRide(Ride.Tempo, legs * LegCost(sim, t), legs * t.Hours, legs);
            }
            return new MandiRide(Ride.Hired, Trolley(sim), def.TripHours, 0);
        }

        /// <summary>A tempo ride starts: its legs counted for today and this season, its diesel and wear paid now.</summary>
        internal static void Depart(Simulation sim, Machine m, MandiRide ride)
        {
            long t = sim.State.Clock.SimTime;
            TripDef trip = Trip(sim, m);
            m.LegsDay = Day(sim) + 1;
            m.LegsUsed = UsedToday(sim, m) + ride.Legs;
            m.OwnLegs += ride.Legs;
            long diesel = Diesel(sim, trip) * ride.Legs, wear = Wear(sim, trip) * ride.Legs;
            if (diesel + wear > 0)
                sim.State.Ledger.Post(t, sim.Period, "ledger.tempoTrip", "machine:" + m.Id, new Posting(Account.Power, diesel), new Posting(Account.Repairs, wear), new Posting(Account.Cash, -(diesel + wear)));
        }

        /// <summary>At midnight (the hourly pass): each tempo let out settles the day just ended.</summary>
        internal static void Midnight(Simulation sim)
        {
            if (!EquipmentRules.On(sim) || Market.MarketRules.HourOfDay(sim) != 0) return;
            foreach (Machine m in sim.State.Equipment.Machines)
                if (EquipmentRules.Defs(sim).Machines[m.Kind].IsTempo && LetOut(sim, m) > 0) FarmSystem.Refresh(sim);
        }

        /// <summary>
        /// With the tempo let out and a hand to drive it, the legs you did not use in the day just ended went to neighbours, 600 a leg, its diesel and wear yours;
        /// the day's pass then doubles its chance of breaking as the tractor's is. Returns the legs run.
        /// </summary>
        internal static int LetOut(Simulation sim, Machine m)
        {
            TripDef trip = Trip(sim, m);
            long day = Day(sim);
            int used = m.LegsDay == day ? m.LegsUsed : 0; // LegsDay is the day plus one: yesterday's legs, the new day not yet counted
            int spare = trip.LegsPerDay - used;
            if (!(m.HireOut && !m.Leased && EquipmentRules.Running(sim, m) && EquipmentRules.HasDriver(sim)) || spare <= 0) return 0;
            long pay = NeighbourPay(sim, trip) * spare, cost = LegCost(sim, trip) * spare;
            if (sim.State.Ledger.Cash + pay - cost < 0) return 0; // no cash for the diesel: not driven
            long t = sim.State.Clock.SimTime;
            FarmSystem.Accrue(sim);
            sim.State.Ledger.Post(t, sim.Period, "ledger.tempoLetOut", "machine:" + m.Id, new Posting(Account.Cash, pay - cost), new Posting(Account.MachineHire, -pay),
                new Posting(Account.Power, Diesel(sim, trip) * spare), new Posting(Account.Repairs, Wear(sim, trip) * spare));
            m.LetOutLegs += spare;
            m.LetDay = day;
            sim.Emit(new TempoLetOut(t, m.Id, spare, pay, cost));
            return spare;
        }

        // ---- is it worth it ----

        /// <summary>What a leg you use saves against the hired trolley.</summary>
        public static long SavesPerLeg(Simulation sim, TripDef t) { return Trolley(sim) - LegCost(sim, t); }

        /// <summary>What a leg let out nets after its diesel and wear.</summary>
        public static long NetPerLetLeg(Simulation sim, TripDef t) { return NeighbourPay(sim, t) - LegCost(sim, t); }

        /// <summary>The tempo's own cost of standing a season: depreciation and upkeep, or its lease.</summary>
        public static long StandingPerSeason(Simulation sim, Machine m)
        {
            return m.Leased ? EquipmentRules.Lease(sim, EquipmentRules.Defs(sim).Machines[m.Kind]) : EquipmentRules.StandingCostPerSeason(sim, m);
        }

        /// <summary>The most a season can leave: every leg of it (days × legs) used for you, saving the trolley, less its standing cost.</summary>
        public static long BestPerSeason(Simulation sim, Machine m)
        {
            TripDef t = Trip(sim, m);
            return SavesPerLeg(sim, t) * t.LegsPerDay * sim.Clock.DaysPerSeason - StandingPerSeason(sim, m);
        }

        /// <summary>What let out to neighbours alone leaves a season, every leg run.</summary>
        public static long LetOutPerSeason(Simulation sim, Machine m)
        {
            TripDef t = Trip(sim, m);
            return NetPerLetLeg(sim, t) * t.LegsPerDay * sim.Clock.DaysPerSeason - StandingPerSeason(sim, m);
        }

        /// <summary>The season's legs counted at the close start again.</summary>
        internal static void CloseSeason(Machine m)
        {
            m.OwnLegs = 0;
            m.LetOutLegs = 0;
        }
    }
}
