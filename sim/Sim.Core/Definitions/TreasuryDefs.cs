using System.Collections.Generic;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>Where a department line's amount comes from (spec 021 §3.4).</summary>
    public enum LineAmount
    {
        /// <summary>The line's own <c>amount</c>.</summary>
        Fixed = 0,
        ClerkWage = 1,
        DoctorAllowance = 2,

        /// <summary>The school's upkeep in <c>population.json</c> (the founder's gift becomes the treasury's line, spec 021 §3.1).</summary>
        SchoolUpkeep = 3,
    }

    /// <summary>When a line exists (spec 021 §3.4's "Opens with").</summary>
    public enum LineRequires
    {
        /// <summary>From the treasury's opening.</summary>
        Treasury = 0,
        School = 1,

        /// <summary>A staffed clinic (spec 023); until it is built the line never opens.</summary>
        Clinic = 2,
    }

    /// <summary>One of the seven departments, in the order they are paid (spec 021 §3.4, spec 022 §3.5).</summary>
    public sealed class DepartmentDef
    {
        public string Id { get; }
        public Account Account { get; }

        public DepartmentDef(string id, Account account)
        {
            Id = id;
            Account = account;
        }
    }

    /// <summary>One line a department pays: a wage or an upkeep (spec 021 §3.4). Wages are paid before upkeep inside a department.</summary>
    public sealed class LineDef
    {
        public string Id { get; }
        public int Department { get; }
        public bool Wage { get; }
        public LineAmount Source { get; }

        /// <summary>Year-0 coins for a <see cref="LineAmount.Fixed"/> line.</summary>
        public long Amount { get; }
        public LineRequires Requires { get; }

        public LineDef(string id, int department, bool wage, LineAmount source, long amount, LineRequires requires)
        {
            Id = id;
            Department = department;
            Wage = wage;
            Source = source;
            Amount = amount;
            Requires = requires;
        }
    }

    /// <summary>A capital work the treasury can commission (spec 021 §3.7): paid from its cash to a builder, kept at cost and depreciated.</summary>
    public sealed class TownWorkDef
    {
        public string Id { get; }

        /// <summary>Year-0 coins, indexed when paid.</summary>
        public long Cost { get; }
        public int LifeSeasons { get; }

        public TownWorkDef(string id, long cost, int lifeSeasons)
        {
            Id = id;
            Cost = cost;
            LifeSeasons = lifeSeasons;
        }
    }

    /// <summary>
    /// <c>treasury.json</c> (spec 021 §4, ADR 0108): the town's second wallet. Optional content: without it there is no treasury, so
    /// the frozen golden content is unchanged. Every coin amount is year-0 coins, indexed when charged (ADR 0034).
    /// </summary>
    public sealed class TreasuryDef
    {
        /// <summary>Index of the era it opens in (the Township).</summary>
        public int Era { get; }
        public long StartUpGrant { get; }

        /// <summary>A resident a season, untied and tied to the clinic.</summary>
        public long GrantUntied { get; }
        public long GrantTied { get; }
        public long ClerkWage { get; }
        public long DoctorAllowance { get; }
        public IReadOnlyList<DepartmentDef> Departments { get; }
        public IReadOnlyList<LineDef> Lines { get; }
        public IReadOnlyList<TownWorkDef> Works { get; }
        public Fixed ConditionStep { get; }
        public Fixed OfficeBase { get; }
        public Fixed OfficeWeight { get; }
        public Fixed HealthLeaveBelow { get; }
        public Fixed EducationMinCondition { get; }
        public Fixed BudgetMaxOfNeeds { get; }
        public long GiftMinimum { get; }
        public int RetainSeasons { get; }

        public TreasuryDef(int era, long startUpGrant, long grantUntied, long grantTied, long clerkWage, long doctorAllowance, IReadOnlyList<DepartmentDef> departments,
            IReadOnlyList<LineDef> lines, IReadOnlyList<TownWorkDef> works, Fixed conditionStep, Fixed officeBase, Fixed officeWeight, Fixed healthLeaveBelow,
            Fixed educationMinCondition, Fixed budgetMaxOfNeeds, long giftMinimum, int retainSeasons)
        {
            Era = era;
            StartUpGrant = startUpGrant;
            GrantUntied = grantUntied;
            GrantTied = grantTied;
            ClerkWage = clerkWage;
            DoctorAllowance = doctorAllowance;
            Departments = departments;
            Lines = lines;
            Works = works;
            ConditionStep = conditionStep;
            OfficeBase = officeBase;
            OfficeWeight = officeWeight;
            HealthLeaveBelow = healthLeaveBelow;
            EducationMinCondition = educationMinCondition;
            BudgetMaxOfNeeds = budgetMaxOfNeeds;
            GiftMinimum = giftMinimum;
            RetainSeasons = retainSeasons;
        }

        /// <summary>The index of a department by id, or -1.</summary>
        public int DepartmentIndex(string id)
        {
            for (int i = 0; i < Departments.Count; i++) if (Departments[i].Id == id) return i;
            return -1;
        }
    }
}
