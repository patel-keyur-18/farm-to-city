using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>The seven departments in the order they are paid (spec 021 §3.4; spec 022 §3.5's seventh, Policies).</summary>
        internal static readonly string[] CivicIds = { "watchman", "hall", "playground" };
        internal static readonly string[] UtilityIds = { "tower", "generator", "grid", "drains", "collection" };
        private static readonly string[] DepartmentIds = { "office", "health", "education", "roads", "safety", "utilities", "policies" };

        private static readonly Account[] DepartmentAccounts =
            { Account.DeptOffice, Account.DeptHealth, Account.DeptEducation, Account.DeptRoads, Account.DeptSafety, Account.DeptUtilities, Account.DeptPolicies };

        /// <summary>
        /// <c>treasury.json</c> (spec 021 §4, ADR 0108): the grants, the clerk's and doctor's pay, the departments' lines, the condition and the
        /// budget's bounds. The departments are fixed (their order is the order they are paid), so the file names only the lines.
        /// </summary>
        public static TreasuryDef LoadTreasury(string json, OnboardingDefs onboarding)
        {
            const string root = "treasury";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "era", "startUpGrant", "grantUntied", "grantTied", "clerkWage", "doctorAllowance", "conditionStep", "officeBase", "officeWeight",
                "healthLeaveBelow", "educationMinCondition", "budgetMaxOfNeeds", "giftMinimum", "retainSeasons", "taxes", "policies", "lines", "works");
            string eraId = String(Required(o, root, "era"), Join(root, "era"));
            int era = onboarding.Eras.Count;
            for (int i = 0; i < onboarding.Eras.Count; i++) if (onboarding.Eras[i].Id == eraId) era = i;
            if (era == onboarding.Eras.Count) throw new ContentException(Join(root, "era"), "unknown era '" + eraId + "'");
            if (era == 0) throw new ContentException(Join(root, "era"), "the treasury opens in a later era than the start");

            long Coins(string name) { return Money(Required(o, root, name), Join(root, name)); }
            long startUp = Coins("startUpGrant"), untied = Coins("grantUntied"), tied = Coins("grantTied"), clerk = Coins("clerkWage"), doctor = Coins("doctorAllowance");
            Fixed step = Bounded(o, root, "conditionStep", "0", "1");
            Fixed officeBase = Bounded(o, root, "officeBase", "0", "1"), officeWeight = Bounded(o, root, "officeWeight", "0", "1");
            if (officeBase + officeWeight != Fixed.One) throw new ContentException(Join(root, "officeWeight"), "officeBase and officeWeight must add up to 1");
            Fixed leave = Bounded(o, root, "healthLeaveBelow", "0", "1"), minEdu = Bounded(o, root, "educationMinCondition", "0", "1");
            Fixed maxBudget = Bounded(o, root, "budgetMaxOfNeeds", "1", "2");
            long gift = Coins("giftMinimum");
            if (gift < 1) throw new ContentException(Join(root, "giftMinimum"), "must be at least 1 coin hundredth");
            int retain = BoundedInt(o, root, "retainSeasons", 4, 16);

            TaxDef taxes = LoadTaxes(Object(Required(o, root, "taxes"), Join(root, "taxes")), Join(root, "taxes"));

            PolicyDefs policies = LoadPolicies(Object(Required(o, root, "policies"), Join(root, "policies")), Join(root, "policies"));

            var departments = new List<DepartmentDef>();
            for (int i = 0; i < DepartmentIds.Length; i++) departments.Add(new DepartmentDef(DepartmentIds[i], DepartmentAccounts[i]));

            var lines = new List<LineDef>();
            var ids = new List<string>();
            List<JToken> ls = NonEmptyArray(o, root, "lines");
            for (int i = 0; i < ls.Count; i++)
            {
                string p = Index(Join(root, "lines"), i);
                JObject l = Object(ls[i], p);
                OnlyFields(l, p, "id", "department", "kind", "amount", "requires", "civic", "utility");
                string id = UniqueId(l, p, ids);
                ids.Add(id);
                string dep = String(Required(l, p, "department"), Join(p, "department"));
                int d = System.Array.IndexOf(DepartmentIds, dep);
                if (d < 0) throw new ContentException(Join(p, "department"), "unknown department '" + dep + "'");
                string kind = String(Required(l, p, "kind"), Join(p, "kind"));
                if (kind != "wage" && kind != "upkeep") throw new ContentException(Join(p, "kind"), "must be wage or upkeep");
                LineAmount source;
                long amount = 0;
                JToken a = Required(l, p, "amount");
                if (a.Type == JTokenType.String)
                {
                    string name = a.Value<string>();
                    if (name == "clerkWage") source = LineAmount.ClerkWage;
                    else if (name == "doctorAllowance") source = LineAmount.DoctorAllowance;
                    else if (name == "schoolUpkeep") source = LineAmount.SchoolUpkeep;
                    else throw new ContentException(Join(p, "amount"), "unknown amount '" + name + "'");
                }
                else
                {
                    source = LineAmount.Fixed;
                    amount = Money(a, Join(p, "amount"));
                    if (amount <= 0) throw new ContentException(Join(p, "amount"), "must be above 0");
                }
                string req = String(Required(l, p, "requires"), Join(p, "requires"));
                LineRequires requires;
                if (req == "treasury") requires = LineRequires.Treasury;
                else if (req == "school") requires = LineRequires.School;
                else if (req == "clinic") requires = LineRequires.Clinic;
                else if (req == "clinicOpen") requires = LineRequires.ClinicOpen;
                else if (req == "road") requires = LineRequires.Road;
                else if (req == "civic") requires = LineRequires.CivicBuilt;
                else if (req == "civicStaffed") requires = LineRequires.CivicStaffed;
                else if (req == "utility") requires = LineRequires.UtilityBuilt;
                else throw new ContentException(Join(p, "requires"), "must be treasury, school, clinic, clinicOpen, road, civic, civicStaffed or utility");
                int civic = -1;
                if (requires == LineRequires.CivicBuilt || requires == LineRequires.CivicStaffed)
                {
                    string cid = String(Required(l, p, "civic"), Join(p, "civic"));
                    civic = System.Array.IndexOf(CivicIds, cid);
                    if (civic < 0) throw new ContentException(Join(p, "civic"), "must be watchman, hall or playground");
                }
                else if (l["civic"] != null) throw new ContentException(Join(p, "civic"), "only civic lines name a civic building");
                int utility = -1;
                if (requires == LineRequires.UtilityBuilt)
                {
                    utility = System.Array.IndexOf(UtilityIds, String(Required(l, p, "utility"), Join(p, "utility")));
                    if (utility < 0) throw new ContentException(Join(p, "utility"), "must be tower, generator, grid, drains or collection");
                }
                else if (l["utility"] != null) throw new ContentException(Join(p, "utility"), "only utility lines name a utility");
                lines.Add(new LineDef(id, d, kind == "wage", source, amount, requires, -1, civic, utility));
            }

            for (int i = 0; i < PolicyIds.Length; i++) // each policy's cost is a line in its department (spec 022 §3.5)
                lines.Add(new LineDef("policy:" + PolicyIds[i], policies.Department[i], false, LineAmount.Policy, 0, LineRequires.Policy, i));

            var works = new List<TownWorkDef>();
            var workIds = new List<string>();
            if (o["works"] != null)
            {
                List<JToken> ws = Array(o["works"], Join(root, "works"));
                for (int i = 0; i < ws.Count; i++)
                {
                    string p = Index(Join(root, "works"), i);
                    JObject w = Object(ws[i], p);
                    OnlyFields(w, p, "id", "cost", "lifeSeasons");
                    string id = UniqueId(w, p, workIds);
                    workIds.Add(id);
                    long cost = Money(Required(w, p, "cost"), Join(p, "cost"));
                    if (cost <= 0) throw new ContentException(Join(p, "cost"), "must be above 0");
                    works.Add(new TownWorkDef(id, cost, BoundedInt(w, p, "lifeSeasons", 1, 400)));
                }
            }
            return new TreasuryDef(era, startUp, untied, tied, clerk, doctor, departments.AsReadOnly(), lines.AsReadOnly(), works.AsReadOnly(), step, officeBase, officeWeight, leave, minEdu, maxBudget, gift, retain, taxes, policies);
        }

        private static readonly string[] PolicyIds = { "meals", "subsidy", "grant", "minWage", "rentControl" };

        private static PolicyDefs LoadPolicies(JObject t, string p)
        {
            OnlyFields(t, p, "maxActive", "reenactAfterSeasons", "meals", "subsidy", "grant", "minWage", "rentControl");
            var lag = new int[PolicyIds.Length];
            var dept = new int[PolicyIds.Length];
            var blocks = new JObject[PolicyIds.Length];
            for (int i = 0; i < PolicyIds.Length; i++)
            {
                string q = Join(p, PolicyIds[i]);
                blocks[i] = Object(Required(t, p, PolicyIds[i]), q);
                lag[i] = BoundedInt(blocks[i], q, "lag", 0, 4);
                string dep = String(Required(blocks[i], q, "department"), Join(q, "department"));
                dept[i] = System.Array.IndexOf(DepartmentIds, dep);
                if (dept[i] < 0) throw new ContentException(Join(q, "department"), "unknown department '" + dep + "'");
            }
            string m = Join(p, "meals"), s = Join(p, "subsidy"), g = Join(p, "grant"), w = Join(p, "minWage"), r = Join(p, "rentControl");
            OnlyFields(blocks[0], m, "lag", "department", "perPupil", "share", "health");
            OnlyFields(blocks[1], s, "lag", "department", "perAcre", "supply", "hangover", "hangoverSeasons");
            OnlyFields(blocks[2], g, "lag", "department", "amount", "wasted", "newcomerChance");
            OnlyFields(blocks[3], w, "lag", "department", "above", "newcomerChance", "inspector");
            OnlyFields(blocks[4], r, "lag", "department", "cap", "officer");
            long Coins(JObject b, string q, string name) { long v = Money(Required(b, q, name), Join(q, name)); if (v <= 0) throw new ContentException(Join(q, name), "must be above 0"); return v; }
            return new PolicyDefs(BoundedInt(t, p, "maxActive", 1, 5), BoundedInt(t, p, "reenactAfterSeasons", 0, 8), lag, dept,
                Coins(blocks[0], m, "perPupil"), Bounded(blocks[0], m, "share", "0", "1"), Bounded(blocks[0], m, "health", "0", "1"),
                Coins(blocks[1], s, "perAcre"), Bounded(blocks[1], s, "supply", "1", "2"), Bounded(blocks[1], s, "hangover", "0.5", "1"), BoundedInt(blocks[1], s, "hangoverSeasons", 0, 8),
                Coins(blocks[2], g, "amount"), Bounded(blocks[2], g, "wasted", "0", "1"), Bounded(blocks[2], g, "newcomerChance", "1", "3"),
                Bounded(blocks[3], w, "above", "0", "1"), Bounded(blocks[3], w, "newcomerChance", "0", "1"), Coins(blocks[3], w, "inspector"),
                Bounded(blocks[4], r, "cap", "0", "0.1"), Coins(blocks[4], r, "officer"));
        }

        private static TaxDef LoadTaxes(JObject t, string p)
        {
            OnlyFields(t, p, "house", "business", "homeAssessedAs", "referenceProfit");
            JObject h = Object(Required(t, p, "house"), Join(p, "house"));
            OnlyFields(h, Join(p, "house"), "rate", "max", "maxShareOfRent");
            JObject b = Object(Required(t, p, "business"), Join(p, "business"));
            OnlyFields(b, Join(p, "business"), "rate", "max");
            Fixed hMax = Bounded(h, Join(p, "house"), "max", "0", "0.1"), hRate = Bounded(h, Join(p, "house"), "rate", "0", "0.1");
            if (hRate > hMax) throw new ContentException(Join(Join(p, "house"), "rate"), "the default rate is above the bound");
            Fixed bMax = Bounded(b, Join(p, "business"), "max", "0", "0.5"), bRate = Bounded(b, Join(p, "business"), "rate", "0", "0.5");
            if (bRate > bMax) throw new ContentException(Join(Join(p, "business"), "rate"), "the default rate is above the bound");
            string home = String(Required(t, p, "homeAssessedAs"), Join(p, "homeAssessedAs"));
            JObject rp = Object(Required(t, p, "referenceProfit"), Join(p, "referenceProfit"));
            var profits = new SortedDictionary<string, long>(System.StringComparer.Ordinal);
            foreach (JProperty prop in rp.Properties()) profits[prop.Name] = Money(prop.Value, Join(Join(p, "referenceProfit"), prop.Name));
            if (!profits.ContainsKey("newcomer")) throw new ContentException(Join(p, "referenceProfit"), "needs a newcomer's reference profit");
            return new TaxDef(hRate, hMax, Bounded(h, Join(p, "house"), "maxShareOfRent", "0", "0.5"), bRate, bMax, home, profits);
        }
    }
}
