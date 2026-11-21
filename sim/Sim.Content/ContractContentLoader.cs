using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>contracts.json</c> (spec 027 §3.7 and §4): which staples, how much, and the discount. Every crop must be a produce good.</summary>
        public static ContractsDef LoadContracts(string json, MarketDefs market)
        {
            const string root = "contracts";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "crops", "topCrops", "shareOfHarvest", "minKg", "roundKg", "discount", "maxOpen", "keepHistory");
            var arr = Array(Required(o, root, "crops"), Join(root, "crops"));
            if (arr.Count == 0) throw new ContentException(Join(root, "crops"), "at least one crop");
            var crops = new List<string>();
            for (int i = 0; i < arr.Count; i++)
            {
                string id = String(arr[i], Index(Join(root, "crops"), i));
                int g = market.IndexOf(id);
                if (g < 0 || market.Goods[g].Kind != GoodKind.Produce) throw new ContentException(Index(Join(root, "crops"), i), "'" + id + "' is not a produce good");
                if (crops.Contains(id)) throw new ContentException(Index(Join(root, "crops"), i), "'" + id + "' is listed twice");
                crops.Add(id);
            }
            return new ContractsDef
            {
                Crops = crops, TopCrops = BoundedInt(o, root, "topCrops", 1, 6), ShareOfHarvest = Bounded(o, root, "shareOfHarvest", "0.05", "1"),
                MinKg = BoundedInt(o, root, "minKg", 1, 100000), RoundKg = BoundedInt(o, root, "roundKg", 1, 10000), Discount = Bounded(o, root, "discount", "0", "0.5"),
                MaxOpen = BoundedInt(o, root, "maxOpen", 1, 6), KeepHistory = BoundedInt(o, root, "keepHistory", 1, 20),
            };
        }
    }
}
