using Sim.Core.Scheduling;

namespace Sim.Core.Market
{
    /// <summary>Market and store (spec 004): the hourly market, daily spoilage and the trader's reset, the seasonal price index.</summary>
    internal sealed class MarketSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Market; } }

        public static void Initialise(Simulation sim)
        {
            sim.State.Market = new MarketState(sim.Config.Game.Market.Goods.Count) { PriceIndex = sim.Config.Game.Market.Economy.StartIndex };
            foreach (Definitions.StartStock s in sim.Config.Game.Start.Store) sim.State.Market.Stock[s.Good] += s.Quantity;
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Daily) System.Array.Clear(sim.State.Market.TraderBoughtToday, 0, sim.State.Market.TraderBoughtToday.Length);
            if (pass == ItemClass.Seasonal) StepIndex(sim);
        }

        /// <summary>
        /// The seasonal inflation step (spec 004 §3.1): i = (min + (max − min) × u) ÷ 4, u a seeded draw for the new
        /// season; PriceIndex ← PriceIndex × (1 + i).
        /// </summary>
        private static void StepIndex(Simulation sim)
        {
            Definitions.EconomyDef e = sim.Config.Game.Market.Economy;
            ulong r = Maths.Rng.Next(sim.State.Seed, Maths.RngStream.Inflation, 0, (ulong)sim.Period);
            Maths.Fixed u = Maths.Fixed.FromRaw((long)(r >> 32));
            Maths.Fixed i = (e.InflationMinPerYear + (e.InflationMaxPerYear - e.InflationMinPerYear) * u).MulRatio(1, 4);
            Maths.Fixed old = sim.State.Market.PriceIndex;
            sim.State.Market.PriceIndex = old * (Maths.Fixed.One + i);
            sim.Emit(new Events.PriceIndexChanged(sim.State.Clock.SimTime, old, sim.State.Market.PriceIndex));
        }
    }
}
