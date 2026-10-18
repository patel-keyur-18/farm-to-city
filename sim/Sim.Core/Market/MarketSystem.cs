using Sim.Core.Scheduling;

namespace Sim.Core.Market
{
    /// <summary>Market and store (spec 004). So far: the trader's daily capacity reset (spec 004 §3.3, §3.8).</summary>
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
        }
    }
}
