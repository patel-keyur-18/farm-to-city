using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.Core.Business
{
    /// <summary>
    /// A hand growing for your vegetable stall (spec 030 §3.6, US-269): the stall's preorder, what is already growing for it, and the pick at each
    /// sowing. Nothing here is saved: the order keeps only the stall's id (<see cref="StandingOrder.ForBusiness"/>).
    /// </summary>
    public static class StallOrderRules
    {
        /// <summary>The stall an order grows for, or null when it has none or the stall is sold or closed (the hand then chooses, ADR 0050).</summary>
        public static Business Stall(Simulation sim, StandingOrder o)
        {
            if (o == null || o.ForBusiness == 0) return null;
            return OpenStall(sim, sim.State.Businesses?.Find(o.ForBusiness));
        }

        /// <summary>The business when it is an open vegetable stall of yours, else null.</summary>
        public static Business OpenStall(Simulation sim, Business b)
        {
            return b != null && b.Status == BusinessStatus.Open && BusinessRules.TypeOf(sim, b).Kind == ShopKind.Veg ? b : null;
        }

        /// <summary>Your open vegetable stalls, oldest first.</summary>
        public static List<Business> OpenStalls(Simulation sim)
        {
            var list = new List<Business>();
            if (sim.State.Businesses == null) return list;
            foreach (Business b in sim.State.Businesses.Businesses) if (OpenStall(sim, b) != null) list.Add(b);
            return list;
        }

        /// <summary>The grams of a good that fields ordered for this stall will bring in: what stands growing or ready on them.</summary>
        public static long GrowingFor(Simulation sim, Business b, int good)
        {
            long sum = 0;
            PeopleState p = sim.State.People;
            if (p == null) return 0;
            foreach (StandingOrder o in p.Orders)
            {
                if (o.ForBusiness != b.Id || o.Kind != OrderTargetKind.Field) continue;
                Crop c = sim.State.Farm.CropOn((int)o.Target);
                if (c == null) continue;
                CropDef def = sim.Config.Game.Farm.Crops[c.Def];
                if (def.OutputGood != good) continue;
                // Ready: what a harvest would bring; still growing: what sowing it was expected to give (the yield is frozen only when growth ends).
                sum += System.Math.Max(0, c.Breakdown != null ? c.HarvestGrams
                    : FarmRules.Estimate(sim, sim.State.Land.Tiles[c.TileId], def, sim.Config.Game.Farm.Fertilisers[c.Fertiliser]).HarvestGrams);
            }
            return sum;
        }

        /// <summary>
        /// What the stall will still need tomorrow of a good, grams: what it puts out, less its own stock, less what your store holds free and what is
        /// growing for it, never over what its cap lets it take from you, never below 0.
        /// </summary>
        public static long Preorder(Simulation sim, Business b, int good)
        {
            long need = b.PutOut[good] - b.Stock[good];
            long cap = SupplyRules.CapOf(sim, b, good);
            if (cap < need) need = cap;
            need -= System.Math.Max(0, sim.State.Market.Stock[good] - sim.State.Market.Offered[good]);
            need -= GrowingFor(sim, b, good);
            return need > 0 ? need : 0;
        }

        /// <summary>
        /// What the stall will still need of each vegetable a hand can sow now (in season, not fodder, a good it sells), biggest first, the earlier crop first among equals:
        /// crop index and grams, those with a preorder only.
        /// </summary>
        public static List<KeyValuePair<int, long>> Wanted(Simulation sim, Business b)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            var list = new List<KeyValuePair<int, long>>();
            for (int i = 0; i < farm.Crops.Count; i++)
            {
                CropDef c = farm.Crops[i];
                if (c.Fodder || !c.SowSeasons[season] || !System.Linq.Enumerable.Contains(t.Goods, c.OutputGood) || ShopRules.VegShare(sim, c.OutputGood) <= Fixed.Zero) continue;
                long need = Preorder(sim, b, c.OutputGood);
                if (need > 0) list.Add(new KeyValuePair<int, long>(i, need));
            }
            for (int i = 1; i < list.Count; i++) // a stable insertion sort, biggest first (no order taken from a hash or an unstable sort)
            {
                KeyValuePair<int, long> x = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Value < x.Value) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
            return list;
        }

        /// <summary>
        /// The crop he sows for the stall: of the in-season vegetables it sells that he may sow on this field and can pay the seed for, the one with the
        /// biggest preorder, then the most bought at the market lately, then the biggest day's amount, then the earlier crop. −1 when none fits
        /// (he then sows his best pick, as Let him choose). <paramref name="needless"/> is true when some vegetable fits but none has a preorder: the stall
        /// needs nothing more, so he waits (and never grows what it takes none of: a good capped at none has no preorder).
        /// </summary>
        public static int Pick(Simulation sim, int tile, int fertiliser, Business b, out bool needless)
        {
            needless = false;
            FarmDefs farm = sim.Config.Game.Farm;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            FertiliserDef fert = farm.Fertilisers[fertiliser];
            int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            int best = -1;
            bool fits = false;
            long bestPre = -1, bestBought = -1, bestDay = -1;
            for (int i = 0; i < farm.Crops.Count; i++)
            {
                CropDef c = farm.Crops[i];
                int g = c.OutputGood;
                if (c.Fodder || !c.SowSeasons[season] || !System.Linq.Enumerable.Contains(t.Goods, g) || ShopRules.VegShare(sim, g) <= Fixed.Zero) continue;
                if (OrchardRules.MaySow(sim, tile, i) != Commands.RejectReason.None) continue;
                if (!sim.State.Ledger.CanAfford(FarmRules.SowingCost(sim, c, SeedSource.Certified, fert))) continue;
                long pre = Preorder(sim, b, g), bought = SupplyRules.BoughtOver(sim, b, g), day = b.PutOut[g];
                if (pre <= 0) { fits = true; continue; }
                bool better = best < 0 || pre > bestPre || (pre == bestPre && (bought > bestBought || (bought == bestBought && day > bestDay)));
                if (!better) continue;
                best = i; bestPre = pre; bestBought = bought; bestDay = day;
            }
            needless = best < 0 && fits;
            return best;
        }
    }
}
