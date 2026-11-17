using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>
        /// <c>township.json</c> (spec 023 §4): the paved link road's petition, share, build days and tier; the clinic, the doctor's quarters and the
        /// doctor (US-148), on the map's <c>clinic_plot</c> and <c>quarters_plot</c>.
        /// </summary>
        public static TownshipDef LoadTownship(string json, RegionDef region, MapDef map, System.Collections.Generic.IReadOnlyList<BuildingDef> buildings, OnboardingDefs onboarding)
        {
            const string root = "township";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "road", "clinic", "quarters", "doctor");
            string rp = Join(root, "road");
            JObject r = Object(Required(o, root, "road"), rp);
            OnlyFields(r, rp, "petitionHouseholds", "share", "days", "tier");
            int households = BoundedInt(r, rp, "petitionHouseholds", 1, 100);
            long share = Money(Required(r, rp, "share"), Join(rp, "share"));
            int days = BoundedInt(r, rp, "days", 1, 60);
            string tier = String(Required(r, rp, "tier"), Join(rp, "tier"));
            if (region.CostOfTier(tier) == null) throw new ContentException(Join(rp, "tier"), "unknown road tier '" + tier + "'");

            string cp = Join(root, "clinic");
            JObject c = Object(Required(o, root, "clinic"), cp);
            OnlyFields(c, cp, "unlockEra", "place", "share", "buildHours", "doctorArrivesHours", "sickCut", "lossCut", "gainBoost", "deathCut", "reach", "capacity");
            string eraId = String(Required(c, cp, "unlockEra"), Join(cp, "unlockEra"));
            int era = 0;
            if (onboarding != null)
            {
                era = -1;
                for (int i = 0; i < onboarding.Eras.Count; i++) if (onboarding.Eras[i].Id == eraId) era = i;
                if (era < 0) throw new ContentException(Join(cp, "unlockEra"), "unknown era '" + eraId + "'");
            }
            var clinic = new ClinicDef
            {
                UnlockEra = era, Tile = PlotTile(c, cp, "clinic_plot", map),
                Share = Money(Required(c, cp, "share"), Join(cp, "share")),
                BuildHours = BoundedInt(c, cp, "buildHours", 1, 24 * 30),
                DoctorArrivesHours = BoundedInt(c, cp, "doctorArrivesHours", 0, 24 * 30),
                SickCut = Bounded(c, cp, "sickCut", "0", "1"), LossCut = Bounded(c, cp, "lossCut", "0", "1"),
                GainBoost = Bounded(c, cp, "gainBoost", "0", "5"), DeathCut = Bounded(c, cp, "deathCut", "0", "1"),
                Reach = BoundedInt(c, cp, "reach", 100, 100000), Capacity = BoundedInt(c, cp, "capacity", 1, 100000),
            };
            if (clinic.Share <= 0) throw new ContentException(Join(cp, "share"), "must be above 0");

            string qp = Join(root, "quarters");
            JObject q = Object(Required(o, root, "quarters"), qp);
            OnlyFields(q, qp, "place", "building", "rent", "buildHours");
            string bid = String(Required(q, qp, "building"), Join(qp, "building"));
            int bi = -1;
            for (int i = 0; i < buildings.Count; i++) if (buildings[i].Id == bid) bi = i;
            if (bi < 0) throw new ContentException(Join(qp, "building"), "unknown building '" + bid + "'");
            var quarters = new QuartersDef
            {
                Tile = PlotTile(q, qp, "quarters_plot", map), Building = bi,
                Rent = Money(Required(q, qp, "rent"), Join(qp, "rent")),
                BuildHours = BoundedInt(q, qp, "buildHours", 1, 24 * 30),
            };
            if (clinic.Tile == quarters.Tile) throw new ContentException(Join(qp, "place"), "the quarters need their own plot");

            string dp = Join(root, "doctor");
            JObject d = Object(Required(o, root, "doctor"), dp);
            OnlyFields(d, dp, "salary", "leaveCloses", "returnCondition");
            var doctor = new DoctorDef
            {
                Salary = Money(Required(d, dp, "salary"), Join(dp, "salary")),
                LeaveCloses = BoundedInt(d, dp, "leaveCloses", 1, 20),
                ReturnCondition = Bounded(d, dp, "returnCondition", "0", "1"),
            };
            return new TownshipDef(new RoadDef(households, share, days, tier), clinic, quarters, doctor);
        }

        private static int PlotTile(JObject o, string path, string kind, MapDef map)
        {
            string placeId = String(Required(o, path, "place"), Join(path, "place"));
            foreach (PlaceDef x in map.Places) if (x.Id == placeId && x.Kind == kind) return x.Tile;
            throw new ContentException(Join(path, "place"), "must name the map's " + kind.Replace('_', ' '));
        }
    }
}
