using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>
        /// <c>township.json</c> (spec 023 §4): the paved link road's petition, share, build days and tier; the clinic, the doctor's quarters and the
        /// doctor (US-148), on the map's <c>clinic_plot</c> and <c>quarters_plot</c>.
        /// </summary>
        public static TownshipDef LoadTownship(string json, RegionDef region, MapDef map, System.Collections.Generic.IReadOnlyList<BuildingDef> buildings, OnboardingDefs onboarding, TreasuryDef treasury, MarketDefs market = null)
        {
            const string root = "township";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "road", "clinic", "quarters", "doctor", "gate", "cohort", "safetyBase", "civic", "landValue", "zoning", "construction", "smoke", "utilities", "upgrade", "coldStore", "townLife");
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
            return new TownshipDef(new RoadDef(households, share, days, tier), clinic, quarters, doctor, gate, cohort, civic, safetyBase, LoadLandValue(o, root, map), LoadZoning(o, root)) { Construction = LoadConstruction(o, root, treasury), Smoke = LoadSmoke(o, root), Utilities = LoadUtilities(o, root, treasury, map), Upgrade = LoadUpgrade(o, root), ColdStore = LoadColdStore(o, root, market), TownLife = o["townLife"] == null ? null : LoadTownLife(o, root) };
        }

        /// <summary>The utilities (spec 025 §3.1): each step's plot, work, lines, reach and capacity, and what going without costs.</summary>
        private static UtilitiesDef LoadUtilities(JObject o, string root, TreasuryDef treasury, MapDef map)
        {
            string p = Join(root, "utilities");
            JObject j = Object(Required(o, root, "utilities"), p);
            var fields = new System.Collections.Generic.List<string>(UtilityIds) { "minCondition", "townSize", "giftRound", "waterHealth", "drainsHealth", "drainsHealthCap", "drainsLandValue", "wasteBelonging", "powerLeisure", "eveningHours" };
            OnlyFields(j, p, fields.ToArray());
            var steps = new System.Collections.Generic.List<UtilityDef>();
            for (int i = 0; i < UtilityIds.Length; i++)
            {
                string ip = Join(p, UtilityIds[i]);
                JObject b = Object(Required(j, p, UtilityIds[i]), ip);
                OnlyFields(b, ip, "place", "work", "upkeepLine", "extraLine", "reach", "capacity");
                string work = String(Required(b, ip, "work"), Join(ip, "work"));
                bool known = false;
                foreach (TownWorkDef w in treasury.Works) if (w.Id == work) known = true;
                if (!known) throw new ContentException(Join(ip, "work"), "unknown work '" + work + "' (treasury.json works)");
                steps.Add(new UtilityDef
                {
                    Index = i, Id = UtilityIds[i], Tile = PlotTile(b, ip, UtilityIds[i] + "_plot", map), Work = work,
                    Reach = BoundedInt(b, ip, "reach", 100, 1000000), Capacity = BoundedInt(b, ip, "capacity", 1, 1000000),
                    UpkeepLine = UtilityLine(treasury, b, ip, "upkeepLine", i, true), ExtraLine = UtilityLine(treasury, b, ip, "extraLine", i, false),
                });
            }
            return new UtilitiesDef
            {
                Steps = steps.AsReadOnly(), MinCondition = Bounded(j, p, "minCondition", "0", "1"), TownSize = BoundedInt(j, p, "townSize", 0, 100000), GiftRound = Money(Required(j, p, "giftRound"), Join(p, "giftRound")),
                WaterHealth = Bounded(j, p, "waterHealth", "0", "1"), DrainsHealth = Bounded(j, p, "drainsHealth", "0", "1"), DrainsHealthCap = Bounded(j, p, "drainsHealthCap", "0", "1"),
                DrainsLandValue = Bounded(j, p, "drainsLandValue", "0", "1"), WasteBelonging = Bounded(j, p, "wasteBelonging", "0", "1"), PowerLeisure = Bounded(j, p, "powerLeisure", "0", "1"),
                EveningHours = BoundedInt(j, p, "eveningHours", 0, 6),
            };
        }

        private static int UtilityLine(TreasuryDef treasury, JObject b, string path, string field, int utility, bool required)
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
                if (l.Requires != LineRequires.UtilityBuilt || l.Utility != utility) throw new ContentException(Join(path, field), "line '" + id + "' doesn't open with this utility");
                return i;
            }
            throw new ContentException(Join(path, field), "unknown line '" + id + "'");
        }

        /// <summary>Smoke (spec 025 §3.3, §3.4): each source's reach, land value and health, and the cap on an acre's total.</summary>
        private static SmokeDef LoadSmoke(JObject o, string root)
        {
            string p = Join(root, "smoke");
            JObject j = Object(Required(o, root, "smoke"), p);
            OnlyFields(j, p, "landValueCap", "healthCap", "sources");
            JObject src = Object(Required(j, p, "sources"), Join(p, "sources"));
            var list = new System.Collections.Generic.List<SmokeSource>();
            foreach (JProperty prop in src.Properties())
            {
                string sp = Join(Join(p, "sources"), prop.Name);
                JObject x = Object(prop.Value, sp);
                OnlyFields(x, sp, "reach", "landValue", "health");
                list.Add(new SmokeSource { Id = prop.Name, Reach = BoundedInt(x, sp, "reach", 0, 5000), LandValue = Bounded(x, sp, "landValue", "0", "1"), Health = Bounded(x, sp, "health", "0", "1") });
            }
            return new SmokeDef { Sources = list.AsReadOnly(), LandValueCap = Bounded(j, p, "landValueCap", "0", "1"), HealthCap = Bounded(j, p, "healthCap", "0", "1") };
        }

        /// <summary>Construction from the Township (spec 025 §3.2): daily limits, prices, masons and the buildings' materials, mason-days and hours.</summary>
        private static ConstructionDef LoadConstruction(JObject o, string root, TreasuryDef treasury)
        {
            string p = Join(root, "construction");
            JObject j = Object(Required(o, root, "construction"), p);
            OnlyFields(j, p, "daily", "price", "masonDay", "gangPrice", "gangHours", "maxMasons", "restockHour", "masonShare", "kinds");
            string dp = Join(p, "daily");
            JObject d = Object(Required(j, p, "daily"), dp);
            OnlyFields(d, dp, "bricks", "cement", "timber");
            string pp = Join(p, "price");
            JObject pr = Object(Required(j, p, "price"), pp);
            OnlyFields(pr, pp, "brick", "cement", "timber");
            var kinds = new System.Collections.Generic.List<BuildKindDef>();
            string kp = Join(p, "kinds");
            var ka = Array(Required(j, p, "kinds"), kp);
            for (int i = 0; i < ka.Count; i++)
            {
                string ip = Join(kp, i.ToString());
                JObject k = Object(ka[i], ip);
                OnlyFields(k, ip, "id", "work", "bricks", "cement", "timber", "masonDays", "hours");
                string work = k["work"] == null ? null : String(k["work"], Join(ip, "work"));
                if (work != null)
                {
                    bool known = false;
                    foreach (TownWorkDef w in treasury.Works) if (w.Id == work) known = true;
                    if (!known) throw new ContentException(Join(ip, "work"), "unknown work '" + work + "' (treasury.json works)");
                }
                kinds.Add(new BuildKindDef
                {
                    Id = String(Required(k, ip, "id"), Join(ip, "id")), Work = work, Bricks = BoundedInt(k, ip, "bricks", 0, 100000), Cement = BoundedInt(k, ip, "cement", 0, 10000),
                    Timber = BoundedInt(k, ip, "timber", 0, 10000), MasonDays = BoundedInt(k, ip, "masonDays", 1, 1000), Hours = BoundedInt(k, ip, "hours", 1, 24 * 30),
                });
            }
            return new ConstructionDef
            {
                DailyBricks = BoundedInt(d, dp, "bricks", 1, 1000000), DailyCement = BoundedInt(d, dp, "cement", 1, 100000), DailyTimber = BoundedInt(d, dp, "timber", 1, 100000),
                BrickPrice = Money(Required(pr, pp, "brick"), Join(pp, "brick")), CementPrice = Money(Required(pr, pp, "cement"), Join(pp, "cement")), TimberPrice = Money(Required(pr, pp, "timber"), Join(pp, "timber")),
                MasonDay = Money(Required(j, p, "masonDay"), Join(p, "masonDay")), GangPrice = Bounded(j, p, "gangPrice", "1", "3"), GangHours = Bounded(j, p, "gangHours", "1", "3"),
                MaxMasons = BoundedInt(j, p, "maxMasons", 1, 10), RestockHour = BoundedInt(j, p, "restockHour", 0, 23), MasonShare = BoundedInt(j, p, "masonShare", 0, 100), Kinds = kinds,
            };
        }

        /// <summary>Town life (spec 026 §4): the five scores' weights, the pull, the grants' bonus, approval, crime and inequality.</summary>
        private static TownLifeDef LoadTownLife(JObject o, string root)
        {
            string p = Join(root, "townLife");
            JObject j = Object(Required(o, root, "townLife"), p);
            OnlyFields(j, p, "weights", "pullDivisor", "pullMin", "pullMax", "grantBonusDivisor", "roadTrack", "approvalStart", "crime", "inequality", "petitions", "festivals");
            string wp = Join(p, "weights");
            JObject w = Object(Required(j, p, "weights"), wp);
            string[] names = { "livability", "business", "education", "health", "environment" };
            OnlyFields(w, wp, names);
            var weights = new System.Collections.Generic.List<int[]>();
            for (int i = 0; i < names.Length; i++)
            {
                string np = Join(wp, names[i]);
                var a = Array(Required(w, wp, names[i]), np);
                if (a.Count != TownLifeDef.PartsEach) throw new ContentException(np, "three weights");
                var row = new int[TownLifeDef.PartsEach];
                int sum = 0;
                for (int k = 0; k < row.Length; k++) { row[k] = Int(a[k], Index(np, k)); Range(row[k], 0, 100, Index(np, k)); sum += row[k]; }
                if (sum != 100) throw new ContentException(np, "the weights must add up to 100");
                weights.Add(row);
            }
            string cp = Join(p, "crime"), ip = Join(p, "inequality");
            JObject c = Object(Required(j, p, "crime"), cp), q = Object(Required(j, p, "inequality"), ip);
            OnlyFields(c, cp, "base", "perUnemployed", "inequalityShare", "landValueStep", "landValueMax");
            OnlyFields(q, ip, "needsHouseholds", "happinessAbove", "maxPoints", "pushOutAt", "ratioCap");
            var d = new TownLifeDef
            {
                Weights = weights, PullDivisor = BoundedInt(j, p, "pullDivisor", 1, 10), PullMin = BoundedInt(j, p, "pullMin", 10, 100), PullMax = BoundedInt(j, p, "pullMax", 100, 300),
                GrantBonusDivisor = BoundedInt(j, p, "grantBonusDivisor", 1, 20), RoadTrack = BoundedInt(j, p, "roadTrack", 0, 100), ApprovalStart = BoundedInt(j, p, "approvalStart", 0, 100),
                CrimeBase = BoundedInt(c, cp, "base", 0, 100), CrimePerUnemployed = Bounded(c, cp, "perUnemployed", "0", "5"), CrimeInequalityShare = Bounded(c, cp, "inequalityShare", "0", "1"),
                CrimeLandValueStep = Bounded(c, cp, "landValueStep", "0", "0.1"), CrimeLandValueMax = Bounded(c, cp, "landValueMax", "0", "0.5"),
                NeedsHouseholds = BoundedInt(q, ip, "needsHouseholds", 2, 100), HappinessAbove = BoundedInt(q, ip, "happinessAbove", 1, 10), MaxPoints = BoundedInt(q, ip, "maxPoints", 0, 20),
                PushOutAt = BoundedInt(q, ip, "pushOutAt", 2, 10), RatioCap = BoundedInt(q, ip, "ratioCap", 5, 100),
                Petition = LoadPetitions(j, p), Festival = LoadFestivals(j, p),
            };
            return d;
        }

        /// <summary>Festivals (spec 026 §3.1): one for each season, the venues' reach, the lift and the shops' rise.</summary>
        private static FestivalDef LoadFestivals(JObject o, string root)
        {
            string p = Join(root, "festivals");
            JObject j = Object(Required(o, root, "festivals"), p);
            OnlyFields(j, p, "inReach", "outside", "squareReach", "daysAhead", "keep", "shops", "list");
            string sp = Join(p, "shops");
            JObject shops = Object(Required(j, p, "shops"), sp);
            OnlyFields(shops, sp, "chai", "sweets", "veg");
            string lp = Join(p, "list");
            var arr = Array(Required(j, p, "list"), lp);
            if (arr.Count != 4) throw new ContentException(lp, "four festivals, one for each season");
            var kinds = new FestivalKindDef[4];
            string[] seasons = { "spring", "summer", "monsoon", "winter" };
            for (int i = 0; i < arr.Count; i++)
            {
                string ip = Index(lp, i);
                JObject k = Object(arr[i], ip);
                OnlyFields(k, ip, "id", "season", "perPerson");
                string season = String(Required(k, ip, "season"), Join(ip, "season"));
                int at = System.Array.IndexOf(seasons, season);
                if (at < 0) throw new ContentException(Join(ip, "season"), "spring, summer, monsoon or winter");
                if (kinds[at] != null) throw new ContentException(Join(ip, "season"), "two festivals in " + season);
                kinds[at] = new FestivalKindDef { Id = String(Required(k, ip, "id"), Join(ip, "id")), Season = at, PerPerson = BoundedInt(k, ip, "perPerson", 20, 300) };
            }
            return new FestivalDef
            {
                Kinds = System.Array.AsReadOnly(kinds), InReach = BoundedInt(j, p, "inReach", 0, 20), Outside = BoundedInt(j, p, "outside", 0, 20), SquareReach = BoundedInt(j, p, "squareReach", 100, 5000),
                DaysAhead = BoundedInt(j, p, "daysAhead", 1, 3), Keep = BoundedInt(j, p, "keep", 1, 50),
                ShopChai = Bounded(shops, sp, "chai", "0", "0.5"), ShopSweets = Bounded(shops, sp, "sweets", "0", "0.5"), ShopVeg = Bounded(shops, sp, "veg", "0", "0.5"),
            };
        }

        /// <summary>Petitions and approval (spec 026 §3.2, §3.3).</summary>
        private static PetitionDef LoadPetitions(JObject o, string root)
        {
            string p = Join(root, "petitions");
            JObject j = Object(Required(o, root, "petitions"), p);
            OnlyFields(j, p, "minHouseholds", "minShare", "rentShare", "promiseCloses", "done", "kept", "declined", "ignored", "broken", "quietCloses", "declinedQuietCloses", "ignoredAfter", "neutralHappiness", "step", "stepMax", "keepEnded", "keepHistory");
            string cp = Join(p, "promiseCloses");
            var arr = Array(Required(j, p, "promiseCloses"), cp);
            var closes = new System.Collections.Generic.List<int>();
            for (int i = 0; i < arr.Count; i++) { int n = Int(arr[i], Index(cp, i)); Range(n, 1, 12, Index(cp, i)); closes.Add(n); }
            if (closes.Count == 0) throw new ContentException(cp, "at least one choice");
            return new PetitionDef
            {
                MinHouseholds = BoundedInt(j, p, "minHouseholds", 1, 50), MinShare = Bounded(j, p, "minShare", "0", "1"), RentShare = Bounded(j, p, "rentShare", "0.1", "0.3"), PromiseCloses = closes.AsReadOnly(),
                Done = BoundedInt(j, p, "done", 0, 20), Kept = BoundedInt(j, p, "kept", 0, 20), Declined = BoundedInt(j, p, "declined", -20, 0), Ignored = BoundedInt(j, p, "ignored", -20, 0), Broken = BoundedInt(j, p, "broken", -20, 0),
                QuietCloses = BoundedInt(j, p, "quietCloses", 0, 12), DeclinedQuietCloses = BoundedInt(j, p, "declinedQuietCloses", 0, 12), IgnoredAfter = BoundedInt(j, p, "ignoredAfter", 1, 12),
                NeutralHappiness = BoundedInt(j, p, "neutralHappiness", 0, 100), Step = BoundedInt(j, p, "step", 1, 50), StepMax = BoundedInt(j, p, "stepMax", 0, 20),
                KeepEnded = BoundedInt(j, p, "keepEnded", 1, 50), KeepHistory = BoundedInt(j, p, "keepHistory", 2, 24),
            };
        }

        /// <summary>Pucca upgrades (spec 024 §3.7): the land value that offers one and the pucca rent; its materials are the <c>pucca_upgrade</c> construction kind.</summary>
        private static UpgradeDef LoadUpgrade(JObject o, string root)
        {
            string p = Join(root, "upgrade");
            JObject j = Object(Required(o, root, "upgrade"), p);
            OnlyFields(j, p, "puccaMinLandValue", "puccaRent");
            return new UpgradeDef { PuccaMinLandValue = Bounded(j, p, "puccaMinLandValue", "1", "3"), PuccaRent = Money(Required(j, p, "puccaRent"), Join(p, "puccaRent")) };
        }

        /// <summary>The cold store (spec 025 §3.6): the rent, the cap and each kept good's share of its spoilage.</summary>
        private static ColdStoreDef LoadColdStore(JObject o, string root, MarketDefs market)
        {
            string p = Join(root, "coldStore");
            JObject j = Object(Required(o, root, "coldStore"), p);
            OnlyFields(j, p, "rentPerKgSeason", "maxRentTonnes", "goods");
            JObject goods = Object(Required(j, p, "goods"), Join(p, "goods"));
            var factor = new Fixed[market == null ? 0 : market.Goods.Count];
            foreach (JProperty g in goods.Properties())
            {
                string gp = Join(Join(p, "goods"), g.Name);
                int i = market == null ? -1 : market.IndexOf(g.Name);
                if (i < 0) throw new ContentException(gp, "unknown good '" + g.Name + "'");
                if (market.Goods[i].Place != GoodPlace.Store) throw new ContentException(gp, "only goods kept in the hut store go to the cold store");
                Fixed f = Bounded(goods, Join(p, "goods"), g.Name, "0.01", "1");
                factor[i] = f;
            }
            if (goods.Count == 0) throw new ContentException(Join(p, "goods"), "at least one good");
            return new ColdStoreDef { RentPerKgSeason = Money(Required(j, p, "rentPerKgSeason"), Join(p, "rentPerKgSeason")), MaxRentTonnes = BoundedInt(j, p, "maxRentTonnes", 1, 1000), Factor = factor };
        }

        private static ZoningDef LoadZoning(JObject o, string root)
        {
            string p = Join(root, "zoning");
            JObject j = Object(Required(o, root, "zoning"), p);
            OnlyFields(j, p, "familiesPerAcre", "homesPerAcre", "parcelsPerSeason", "permitFee", "turnedAwayMax");
            return new ZoningDef
            {
                FamiliesPerAcre = BoundedInt(j, p, "familiesPerAcre", 1, 4), HomesPerAcre = BoundedInt(j, p, "homesPerAcre", 1, 4),
                ParcelsPerSeason = BoundedInt(j, p, "parcelsPerSeason", 1, 4), PermitFee = Bounded(j, p, "permitFee", "0", "0.1"),
                TurnedAwayMax = BoundedInt(j, p, "turnedAwayMax", 1, 100),
            };
        }

        private static LandValueDef LoadLandValue(JObject o, string root, MapDef map)
        {
            string p = Join(root, "landValue");
            JObject j = Object(Required(o, root, "landValue"), p);
            OnlyFields(j, p, "square", "water", "squareReach", "waterReach", "road", "service", "jobs", "size", "floor", "ceiling", "rentPlace", "slowSaleSeconds", "leaseYield");
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
                SlowSaleSeconds = BoundedInt(j, p, "slowSaleSeconds", 3600, 86400 * 30), LeaseYield = Bounded(j, p, "leaseYield", "0", "0.1"),
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
