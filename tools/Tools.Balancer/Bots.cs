using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Tools.Balancer
{
    /// <summary>
    /// A player bot: once an hour it looks at the farm and issues commands through <see cref="Simulation.Execute"/>, like
    /// a player would. Bots use only public commands and read-only state (ADR 0034 §7), so anything a bot does, a player
    /// can do in the game.
    /// </summary>
    public abstract class Bot
    {
        /// <summary>The well acres first, then the rain-fed inherited acres (ADR 0032).</summary>
        protected static readonly int[] Acres = { 20, 28, 18, 19, 26, 27 };

        public abstract string Name { get; }

        /// <summary>Called at a new game, before the first hour.</summary>
        public virtual void Start(Simulation sim) { }

        /// <summary>One round at sim time t (the sim has been advanced to t).</summary>
        public abstract void Play(Simulation sim, long t);

        protected static bool Ok(Simulation sim, Command c) { return sim.Execute(c).Accepted; }

        protected static int Season(Simulation sim, long t) { return (int)Sim.Core.Time.Calendar.SeasonAt(t, sim.Clock); }
    }

    /// <summary>
    /// The competent founder farming alone (US-013, F00.3): the best crop in season on as many acres as Care ≥ 1.0
    /// allows (the well acres first), the kitchen garden, fruit trees in the yard, spare hours sold as day labour.
    /// Selling: a cart to the mandi when it beats the trader after its labour, a small minimum-price offer on the local
    /// market, the rest to the trader.
    /// </summary>
    public class FarmerBot : Bot
    {
        private static readonly string[] BestCrop = { "potato", "lentils", "rice", "potato" }; // Spring, Summer, Monsoon, Winter

        public override string Name { get { return "competent"; } }

        public int MandiTrips { get; private set; }

        public override void Start(Simulation sim)
        {
            // Two mangoes and two guavas in the yard: cheap, long-lived, and in slots that shade no field.
            int n = 0;
            foreach (Sim.Core.Land.TreeSlot s in sim.State.Land.Slots)
            {
                if (s.Kind != Sim.Core.Land.SlotKind.Yard || s.TreeId != 0 || n >= 4) continue;
                Ok(sim, new PlantTree(sim.State.Clock.SimTime, s.Id, n < 2 ? "mango" : "guava"));
                n++;
            }
        }

        public override void Play(Simulation sim, long t)
        {
            SprayOutbreaks(sim, t);
            if (sim.State.Farm.Founder.Health < Fixed.Parse("0.9")) Ok(sim, new BuyMedicine(t));
            Harvest(sim, t);
            Sell(sim, t);
            Sow(sim, t, BestCrop[Season(sim, t)]);
            Garden(sim, t);
            SplitLabour(sim, t);
        }

        protected static void Harvest(Simulation sim, long t)
        {
            foreach (int tile in Acres)
            {
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) Ok(sim, new Harvest(t, tile, Overflow.SellRestToTrader));
            }
            foreach (Tree tree in sim.State.Farm.Trees)
                if (tree.RipeGrams > 0) Ok(sim, new PickFruit(t, tree.SlotId, Overflow.SellRestToTrader));
        }

        /// <summary>A curative spray on every warned crop: the dose costs far less than the damage it saves.</summary>
        protected static void SprayOutbreaks(Simulation sim, long t)
        {
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Growing && c.Outbreak != Outbreak.None && c.Spray == SprayState.None) Ok(sim, new Spray(t, c.TileId));
        }

        /// <summary>Sows the crop on vacant acres while the founder's hours cover the demand (Care ≥ 1.0).</summary>
        protected static void Sow(Simulation sim, long t, string cropId)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            CropDef crop = farm.Crops[farm.IndexOfCrop(cropId)];
            Fixed supply = FounderRules.LabourRate(sim) + FounderRules.MandiCut(sim);
            foreach (int tile in Acres)
            {
                if (sim.State.Farm.CropOn(tile) != null) continue;
                if (FarmRules.LabourDemand(sim) + crop.LabourPerAcrePerDay > supply) break;
                if (!Ok(sim, new Sow(t, tile, crop.Id, SeedSource.Certified, "none"))) break;
            }
        }

        protected static void Garden(Simulation sim, long t)
        {
            foreach (GardenBed b in sim.State.Farm.Beds)
            {
                if (b.State == BedState.Ready) Ok(sim, new PickGarden(t, b.Id, Overflow.SellRestToTrader));
                if (b.State == BedState.Empty) Ok(sim, new SowGarden(t, b.Id, b.Id == 1 ? "garden_spinach" : "garden_radish"));
            }
        }

        /// <summary>Spare hours go to day labour, in steps of 25%.</summary>
        protected static void SplitLabour(Simulation sim, long t)
        {
            Fixed rate = FounderRules.LabourRate(sim), need = FarmRules.LabourDemand(sim);
            int share = 0;
            while (share < 100 && rate.MulRatio(100 - share - 25, 100) >= need) share += 25;
            if (share != sim.State.Farm.Founder.DayLabourShare) Ok(sim, new SetLabourSplit(t, share));
        }

        /// <summary>The household's next day of food is kept back; the rest is surplus.</summary>
        protected static long Reserve(Simulation sim, int good)
        {
            GoodDef g = sim.Config.Game.Market.Goods[good];
            if (!sim.State.Farm.Founder.EatFromStore || !g.Edible) return 0;
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan];
            long allowance = MarketRules.Indexed(sim, Money.MulRate(plan.CostPerSeason, plan.FoodShare)) / sim.Clock.DaysPerSeason;
            Fixed floor = MarketRules.Floor(sim, good);
            return floor <= Fixed.Zero ? 0 : (Fixed.FromInt(allowance * 10) / floor).RoundToLong(); // 1/100 coin → grams
        }

        private void Sell(Simulation sim, long t)
        {
            MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;

            // The mandi: the good whose full cart beats the trader by most, after the trip's labour at the day-labour wage.
            if (m.Trip == null && m.LastTripDay != t / ClockDef.SecondsPerDay && !sim.State.Farm.Founder.SickToday)
            {
                MandiDef mandi = defs.Mandi;
                long labour = Money.MulRate(MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour), mandi.FounderHours);
                long hire = MarketRules.Indexed(sim, mandi.CartHirePerTrip);
                int best = -1;
                long bestGain = 0, bestQ = 0;
                for (int g = 0; g < defs.Goods.Count; g++)
                {
                    if (defs.Goods[g].Kind != GoodKind.Produce) continue;
                    long q = Math.Min(m.InStore(g) - m.OnOffer(g) - Reserve(sim, g), mandi.CartCapacityGrams);
                    if (q <= 0) continue;
                    long gross = MarketRules.Value(sim, g, q, MarketRules.RegionalPrice(sim, g));
                    long gain = gross - Money.MulRate(gross, mandi.CommissionRate) - hire - labour
                        - MarketRules.Value(sim, g, Math.Min(q, MarketRules.TraderCapacityLeft(sim, g)), MarketRules.Floor(sim, g));
                    if (gain > bestGain) { best = g; bestGain = gain; bestQ = q; }
                }
                if (best >= 0 && Ok(sim, new GoToMandi(t, new[] { new MandiLoad(defs.Goods[best].Id, bestQ) }))) MandiTrips++;
            }

            for (int g = 0; g < defs.Goods.Count; g++)
            {
                if (defs.Goods[g].Kind != GoodKind.Produce) continue;
                long surplus = m.InStore(g) - m.OnOffer(g) - Reserve(sim, g);
                if (surplus <= 0) continue;

                // A small offer at a minimum halfway between the floor and the regional price: what the valley buys near
                // the regional price in about a day (a tenth of its demand).
                Fixed floor = MarketRules.Floor(sim, g), regional = MarketRules.RegionalPrice(sim, g);
                long target = defs.Region.DemandPerDay(g) / 10;
                long add = Math.Min(surplus, target - m.OnOffer(g));
                if (add > 0 && Ok(sim, new Offer(t, defs.Goods[g].Id, add, floor + (regional - floor).MulRatio(1, 2)))) surplus -= add;

                long sell = Math.Min(surplus, MarketRules.TraderCapacityLeft(sim, g));
                if (sell > 0) Ok(sim, new SellToTrader(t, defs.Goods[g].Id, sell));
            }
        }
    }
}
