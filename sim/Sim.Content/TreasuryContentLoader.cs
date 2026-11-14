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
                "healthLeaveBelow", "educationMinCondition", "budgetMaxOfNeeds", "giftMinimum", "retainSeasons", "lines", "works");
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

            var departments = new List<DepartmentDef>();
            for (int i = 0; i < DepartmentIds.Length; i++) departments.Add(new DepartmentDef(DepartmentIds[i], DepartmentAccounts[i]));

            var lines = new List<LineDef>();
            var ids = new List<string>();
            List<JToken> ls = NonEmptyArray(o, root, "lines");
            for (int i = 0; i < ls.Count; i++)
            {
                string p = Index(Join(root, "lines"), i);
                JObject l = Object(ls[i], p);
                OnlyFields(l, p, "id", "department", "kind", "amount", "requires");
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
                else throw new ContentException(Join(p, "requires"), "must be treasury, school or clinic");
                lines.Add(new LineDef(id, d, kind == "wage", source, amount, requires));
            }

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
            return new TreasuryDef(era, startUp, untied, tied, clerk, doctor, departments.AsReadOnly(), lines.AsReadOnly(), works.AsReadOnly(), step, officeBase, officeWeight, leave, minEdu, maxBudget, gift, retain);
        }
    }
}
