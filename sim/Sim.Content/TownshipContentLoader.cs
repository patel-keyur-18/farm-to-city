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
        public static TownshipDef LoadTownship(string json, RegionDef region, MapDef map, System.Collections.Generic.IReadOnlyList<BuildingDef> buildings, OnboardingDefs onboarding, TreasuryDef treasury)
        {
            const string root = "township";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "road", "clinic", "quarters", "doctor", "gate", "cohort", "safetyBase", "civic", "landValue");
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

            string gp = Join(root, "gate");
            JObject gj = Object(Required(o, root, "gate"), gp);
            OnlyFields(gj, gp, "households", "businesses", "literacy");
            var gate = new GateDef { Households = BoundedInt(gj, gp, "households", 1, 1000), Businesses = BoundedInt(gj, gp, "businesses", 0, 20), Literacy = Bounded(gj, gp, "literacy", "0", "1") };

            string hp = Join(root, "cohort");
            JObject hj = Object(Required(o, root, "cohort"), hp);
            OnlyFields(hj, hp, "households", "peoplePerHousehold", "literacy", "income", "leaveBelow", "leaveCloses", "childShare", "elderShare", "pupilShare");
            var cohort = new CohortDef
            {
                Households = BoundedInt(hj, hp, "households", 0, 1000), PeoplePerHousehold = BoundedInt(hj, hp, "peoplePerHousehold", 1, 20),
                Literacy = Bounded(hj, hp, "literacy", "0", "1"), Income = Money(Required(hj, hp, "income"), Join(hp, "income")),
                LeaveBelow = BoundedInt(hj, hp, "leaveBelow", 0, 100), LeaveCloses = BoundedInt(hj, hp, "leaveCloses", 1, 20),
                ChildShare = Bounded(hj, hp, "childShare", "0", "1"), ElderShare = Bounded(hj, hp, "elderShare", "0", "1"), PupilShare = Bounded(hj, hp, "pupilShare", "0", "1"),
            };
            if (cohort.ChildShare + cohort.ElderShare > Sim.Core.Maths.Fixed.One) throw new ContentException(Join(hp, "childShare"), "children and elders can't be more than everyone");

            Sim.Core.Maths.Fixed safetyBase = Bounded(o, root, "safetyBase", "0", "1");
            var civic = new System.Collections.Generic.List<CivicDef>();
            string vp = Join(root, "civic");
            JObject cj = Object(Required(o, root, "civic"), vp);
            OnlyFields(cj, vp, CivicIds);
            for (int i = 0; i < CivicIds.Length; i++)
            {
                string ip = Join(vp, CivicIds[i]);
                JObject b = Object(Required(cj, vp, CivicIds[i]), ip);
                OnlyFields(b, ip, "place", "work", "upkeepLine", "wageLine", "reach", "capacity", "gain", "staffHours", "leaveCloses");
                string work = String(Required(b, ip, "work"), Join(ip, "work"));
                bool known = false;
                foreach (TownWorkDef w in treasury.Works) if (w.Id == work) known = true;
                if (!known) throw new ContentException(Join(ip, "work"), "unknown work '" + work + "' (treasury.json works)");
                var def = new CivicDef
                {
                    Index = i, Id = CivicIds[i], Tile = PlotTile(b, ip, CivicIds[i] + "_plot", map), Work = work,
                    Reach = BoundedInt(b, ip, "reach", 100, 100000), Capacity = BoundedInt(b, ip, "capacity", 1, 100000),
                    Gain = Bounded(b, ip, "gain", "0", "1"), StaffHours = BoundedInt(b, ip, "staffHours", 0, 24 * 30), LeaveCloses = BoundedInt(b, ip, "leaveCloses", 1, 20),
                    UpkeepLine = LineOf(treasury, b, ip, "upkeepLine", i, LineRequires.CivicBuilt, true),
                    WageLine = LineOf(treasury, b, ip, "wageLine", i, LineRequires.CivicStaffed, false),
                };
                civic.Add(def);
            }
            return new TownshipDef(new RoadDef(households, share, days, tier), clinic, quarters, doctor, gate, cohort, civic, safetyBase, LoadLandValue(o, root, map));
        }

        private static LandValueDef LoadLandValue(JObject o, string root, MapDef map)
        {
            string p = Join(root, "landValue");
            JObject j = Object(Required(o, root, "landValue"), p);
            OnlyFields(j, p, "square", "water", "squareReach", "waterReach", "road", "service", "jobs", "size", "floor", "ceiling", "rentPlace", "slowSaleSeconds");
            JObject road = Sub(j, p, "road", out string rp, "district", "lane");
            JObject svc = Sub(j, p, "service", out string sp, "school", "clinic", "square", "water");
            JObject jobs = Sub(j, p, "jobs", out string jp, "each", "max", "reach");
            JObject size = Sub(j, p, "size", out string zp, "over", "per10", "max");
            JObject rent = Sub(j, p, "rentPlace", out string tp, "base", "slope", "min", "max");
            var d = new LandValueDef
            {
                SquareTile = NamedPlace(j, p, "square", map), WaterTile = NamedPlace(j, p, "water", map),
                SquareReach = BoundedInt(j, p, "squareReach", 100, 5000), WaterReach = BoundedInt(j, p, "waterReach", 100, 500),
                RoadDistrict = Bounded(road, rp, "district", "0", "0.5"), RoadLane = Bounded(road, rp, "lane", "0", "0.5"),
                School = Bounded(svc, sp, "school", "0", "0.3"), Clinic = Bounded(svc, sp, "clinic", "0", "0.3"),
                Square = Bounded(svc, sp, "square", "0", "0.3"), Water = Bounded(svc, sp, "water", "0", "0.3"),
                JobEach = Bounded(jobs, jp, "each", "0", "0.1"), JobMax = BoundedInt(jobs, jp, "max", 0, 20), JobReach = BoundedInt(jobs, jp, "reach", 100, 5000),
                SizeOver = BoundedInt(size, zp, "over", 0, 1000), SizePer10 = Bounded(size, zp, "per10", "0", "0.2"), SizeMax = Bounded(size, zp, "max", "0", "1"),
                Floor = Bounded(j, p, "floor", "0.5", "1"), Ceiling = Bounded(j, p, "ceiling", "1", "3"),
                RentBase = Bounded(rent, tp, "base", "0.1", "3"), RentSlope = Bounded(rent, tp, "slope", "0", "5"),
                RentMin = Bounded(rent, tp, "min", "0.1", "3"), RentMax = Bounded(rent, tp, "max", "0.1", "3"),
                SlowSaleSeconds = BoundedInt(j, p, "slowSaleSeconds", 3600, 86400 * 30),
            };
            if (d.RentMin > d.RentMax) throw new ContentException(Join(tp, "min"), "can't be above max");
            return d;
        }

        private static JObject Sub(JObject o, string path, string name, out string subPath, params string[] fields)
        {
            subPath = Join(path, name);
            JObject s = Object(Required(o, path, name), subPath);
            OnlyFields(s, subPath, fields);
            return s;
        }

        private static int NamedPlace(JObject o, string path, string field, MapDef map)
        {
            string id = String(Required(o, path, field), Join(path, field));
            foreach (PlaceDef x in map.Places) if (x.Id == id) return x.Tile;
            throw new ContentException(Join(path, field), "no map place '" + id + "'");
        }

        /// <summary>The treasury line a civic building names for its upkeep or its post: it must be a Safety line that opens with this very building.</summary>
        private static int LineOf(TreasuryDef treasury, JObject b, string path, string field, int civic, LineRequires requires, bool required)
        {
            if (b[field] == null)
            {
                if (required) throw new ContentException(Join(path, field), "required");
                return -1;
            }
            string id = String(b[field], Join(path, field));
            for (int i = 0; i < treasury.Lines.Count; i++)
            {
                LineDef l = treasury.Lines[i];
                if (l.Id != id) continue;
                if (l.Requires != requires || l.Civic != civic) throw new ContentException(Join(path, field), "line '" + id + "' doesn't open with this building");
                return i;
            }
            throw new ContentException(Join(path, field), "unknown line '" + id + "'");
        }

        private static int PlotTile(JObject o, string path, string kind, MapDef map)
        {
            string placeId = String(Required(o, path, "place"), Join(path, "place"));
            foreach (PlaceDef x in map.Places) if (x.Id == placeId && x.Kind == kind) return x.Tile;
            throw new ContentException(Join(path, "place"), "must name the map's " + kind.Replace('_', ' '));
        }
    }
}
