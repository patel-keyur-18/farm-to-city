using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.Core.Town;

namespace Sim.Core.Business
{
    /// <summary>
    /// The brick kiln (spec 025 §3.3, US-243, ADR 0129): a hired kiln hand or you fire bricks each open hour outside the Monsoon, from clay (free) and
    /// fuel bought as it burns (a cost in the brick, not a good). Bricks go first to your own builds at what they cost, then to the trader each
    /// morning at his price less a tenth (up to what he takes a day, and what he buys adds to his stock for every build in town); the rest keeps
    /// in the yard.
    /// </summary>
    public static class KilnRules
    {
        public static bool IsKiln(Simulation sim, Business b) { return BusinessRules.TypeOf(sim, b).IsKiln; }

        public static KilnDef Def(Simulation sim, Business b) { return BusinessRules.TypeOf(sim, b).Kiln; }

        /// <summary>Your kilns that stand open, in id order.</summary>
        public static List<Business> Open(Simulation sim)
        {
            var list = new List<Business>();
            if (sim.State.Businesses == null) return list;
            foreach (Business b in sim.State.Businesses.Businesses)
                if (b.Status == BusinessStatus.Open && IsKiln(sim, b)) list.Add(b);
            return list;
        }

        /// <summary>Wet clay can't be fired: nothing is made in the Monsoon.</summary>
        public static bool Fires(Simulation sim) { return Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock) != Season.Monsoon; }

        /// <summary>The trader's price for a brick today (1/100 coin): the base price moved by today's prices.</summary>
        public static long TraderPrice(Simulation sim) { return MarketRules.Indexed(sim, sim.Config.Game.Township.Construction.BrickPrice); }

        /// <summary>What the trader pays a kiln for a brick: his price less a tenth.</summary>
        public static long PaysFor(Simulation sim, KilnDef k, long bricks) { return Money.MulRate(MarketRules.Indexed(sim, sim.Config.Game.Township.Construction.BrickPrice * bricks), k.TraderShare); }

        /// <summary>The fuel in a brick at today's prices (1/100 coin), fuel kg × its price a kg; frugal hands burn less.</summary>
        public static long FuelCost(Simulation sim, Business b, KilnDef k, long bricks)
        {
            return Money.Value(bricks, k.FuelKg * k.FuelPrice * sim.State.Market.PriceIndex * BusinessRules.CostFactor(sim, b), 1);
        }

        /// <summary>The fuel a brick takes at base prices, for the plan (a hand's thrift is not counted).</summary>
        public static long FuelCost(Simulation sim, KilnDef k, long bricks) { return Money.Value(bricks, k.FuelKg * k.FuelPrice * sim.State.Market.PriceIndex, 1); }

        /// <summary>Bricks an hour: the type's rate × its worker's share (a skill-50 worker's is 1); yours as rated.</summary>
        public static long PerHour(Simulation sim, Business b)
        {
            KilnDef k = Def(sim, b);
            Worker w = BusinessRules.HiredWorker(sim, b);
            return w == null ? k.BricksPerHour : WorkRules.RelativeOutput(sim, w).MulRatio(k.BricksPerHour, 1).RoundToLong();
        }

        // ---- firing ----

        /// <summary>An open hour of the kiln: bricks into the yard as far as the rate, the room and the money go; fuel paid as it burns.</summary>
        internal static void Fire(Simulation sim, Business b)
        {
            KilnDef k = Def(sim, b);
            if (!Fires(sim)) return;
            long q = System.Math.Min(PerHour(sim, b), k.YardMax - b.Yard);
            if (q <= 0) return;
            long cost = FuelCost(sim, b, k, q), spend = BusinessBooks.CanSpend(sim, b);
            if (cost > spend) q = cost <= 0 ? 0 : (long)((System.Numerics.BigInteger)q * System.Math.Max(0, spend) / cost);
            while (q > 0 && FuelCost(sim, b, k, q) > BusinessBooks.CanSpend(sim, b)) q = q * 99 / 100 - 1; // rounding can leave it a few coins over
            if (q <= 0) return;
            cost = FuelCost(sim, b, k, q);
            BusinessBooks.Draw(sim, b, cost);
            if (cost > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.fuel", "business:" + b.Id, new Posting(Account.BusinessStock, cost), new Posting(Account.Cash, -cost));
            b.Yard += q;
            b.YardCost += cost;
            b.KilnMade += q;
            b.KilnSeason += q;
        }

        // ---- selling ----

        /// <summary>Bricks leave the yard at <paramref name="amount"/>: the sale and what they cost, in its books.</summary>
        private static void Sell(Simulation sim, Business b, long bricks, long amount)
        {
            long cost = b.Yard <= 0 ? 0 : (long)((System.Numerics.BigInteger)b.YardCost * bricks / b.Yard);
            b.Yard -= bricks;
            b.YardCost -= cost;
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            if (amount > 0) b.Ledger.Post(now, sim.Period, "ledger.sales", reference, new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
            if (cost > 0) b.Ledger.Post(now, sim.Period, "ledger.costOfGoods", reference, new Posting(Account.CostOfGoodsSold, cost), new Posting(Account.BusinessStock, -cost));
        }

        /// <summary>A closing kiln sells what is left in its yard to the trader at his price less a tenth.</summary>
        internal static void Clear(Simulation sim, Business b)
        {
            if (!IsKiln(sim, b) || b.Yard <= 0) return;
            Sell(sim, b, b.Yard, PaysFor(sim, Def(sim, b), b.Yard));
        }

        /// <summary>The trader's morning: each kiln sells him what he takes (up to a day's), his stock for the whole town grows by it.</summary>
        internal static void Morning(Simulation sim)
        {
            TownshipState town = sim.State.Township;
            long total = 0, left = -1; // the trader's day for the whole town, shared by the kilns in id order (ADR 0143)
            foreach (Business b in Open(sim))
            {
                KilnDef k = Def(sim, b);
                if (left < 0) left = k.TraderPerDay;
                long q = System.Math.Min(b.Yard, left);
                if (q <= 0) continue;
                left -= q;
                Sell(sim, b, q, BusinessRules.SalePrice(sim, b, PaysFor(sim, k, q)));
                b.KilnSold += q;
                total += q;
            }
            if (town != null && total > 0) ConstructionRules.KilnDelivered(sim, total);
        }

        /// <summary>Bricks in the yards in all.</summary>
        public static long Yards(Simulation sim)
        {
            long n = 0;
            foreach (Business b in Open(sim)) n += b.Yard;
            return n;
        }

        /// <summary>What <paramref name="bricks"/> of the yards would cost your build (what they cost to fire), the yards in id order.</summary>
        public static long CostOf(Simulation sim, long bricks)
        {
            long cost = 0;
            foreach (Business b in Open(sim))
            {
                long q = System.Math.Min(bricks, b.Yard);
                if (q <= 0) continue;
                cost += (long)((System.Numerics.BigInteger)b.YardCost * q / b.Yard);
                bricks -= q;
            }
            return cost;
        }

        /// <summary>Your own build takes bricks from the yards at what they cost: the kilns sell them to you at cost (no profit, no loss).</summary>
        internal static void TakeForBuild(Simulation sim, long bricks)
        {
            foreach (Business b in Open(sim))
            {
                long q = System.Math.Min(bricks, b.Yard);
                if (q <= 0) continue;
                long cost = (long)((System.Numerics.BigInteger)b.YardCost * q / b.Yard);
                Sell(sim, b, q, cost);
                b.KilnToBuilds += q;
                bricks -= q;
            }
        }

        // ---- the plan ----

        /// <summary>A season that fires at today's prices (every day fired and sold to the trader up to what he takes), and the Monsoon that does not.</summary>
        internal static BusinessRules.Projection Projected(Simulation sim, BusinessTypeDef t, bool runYourself, long loan)
        {
            KilnDef k = t.Kiln;
            int days = sim.Clock.DaysPerSeason;
            long made = System.Math.Min(k.BricksPerHour * (t.CloseHour - t.OpenHour), k.TraderPerDay) * days;
            long sales = PaysFor(sim, k, made), fuel = FuelCost(sim, k, made);
            long repairs = Money.MulRate(MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare);
            long depreciation = MarketRules.Indexed(sim, t.BuildingValue) / t.BuildingLifeSeasons + MarketRules.Indexed(sim, t.EquipmentValue) / t.EquipmentLifeSeasons;
            long wage = runYourself ? 0 : BusinessRules.Wage(sim, t);
            long interest = loan <= 0 || sim.State.Credit == null ? 0
                : Money.MulRate(loan, Finance.CreditRules.Rate(sim.Config.Game.Credit.Lenders[sim.Config.Game.Credit.IndexOfLender(BusinessRules.Defs(sim).LenderId)], sim.State.Credit.Score));
            long costs = fuel + repairs + depreciation + wage + interest;
            long cost = BusinessRules.Cost(sim, t);
            return new BusinessRules.Projection
            {
                OwnGrams = made, Sales = sales, Power = fuel, Repairs = repairs, Depreciation = depreciation, Wage = wage, Interest = interest, Loan = loan, Revenue = sales, Costs = costs,
                Profit = sales - costs, Cost = cost, Off = wage + repairs + depreciation + interest, CashNeeded = cost + (runYourself ? fuel + repairs : wage) + (fuel + days - 1) / days,
            };
        }
    }
}
