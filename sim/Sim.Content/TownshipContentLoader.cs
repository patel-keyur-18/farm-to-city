using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>township.json</c> (spec 023 §4): the paved link road's petition, share, build days and the tier it brings.</summary>
        public static TownshipDef LoadTownship(string json, RegionDef region)
        {
            const string root = "township";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "road");
            string rp = Join(root, "road");
            JObject r = Object(Required(o, root, "road"), rp);
            OnlyFields(r, rp, "petitionHouseholds", "share", "days", "tier");
            int households = BoundedInt(r, rp, "petitionHouseholds", 1, 100);
            long share = Money(Required(r, rp, "share"), Join(rp, "share"));
            int days = BoundedInt(r, rp, "days", 1, 60);
            string tier = String(Required(r, rp, "tier"), Join(rp, "tier"));
            if (region.CostOfTier(tier) == null) throw new ContentException(Join(rp, "tier"), "unknown road tier '" + tier + "'");
            return new TownshipDef(new RoadDef(households, share, days, tier));
        }
    }
}
