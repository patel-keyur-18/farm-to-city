using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>A product a business makes from its input, kg for kg (spec 016 §3.5a: 1 kg wheat → 0.97 atta + 0.03 bran).</summary>
    public readonly struct BusinessOutput
    {
        public int Good { get; }
        public Fixed Yield { get; }

        public BusinessOutput(int good, Fixed yield)
        {
            Good = good;
            Yield = yield;
        }
    }

    /// <summary>A business type from <c>content/businesses.json</c> (spec 016 §3.1). Phase 2 has the flour mill; the bakery and dairy join in F12.2.</summary>
    public sealed class BusinessTypeDef
    {
        public string Id { get; internal set; }
        public string NameKey { get; internal set; }

        /// <summary>The era it unlocks in (an index into the eras).</summary>
        public int Era { get; internal set; }
        public long BuildingValue { get; internal set; }
        public int BuildingLifeSeasons { get; internal set; }
        public long EquipmentValue { get; internal set; }
        public int EquipmentLifeSeasons { get; internal set; }
        public int BuildHours { get; internal set; }

        /// <summary>The worker's role ("miller"): strings <c>business.role.&lt;id&gt;</c>.</summary>
        public string Worker { get; internal set; }

        /// <summary>A season's wage at year-0 prices (spec 016 §3.6).</summary>
        public long Wage { get; internal set; }

        /// <summary>Grams a real hour while open: a real working day × 0.95 (§3 time compression).</summary>
        public long GramsPerHour { get; internal set; }

        /// <summary>Coins (year-0) a kg worked, and the share of the equipment's cost spent on repairs a season.</summary>
        public Fixed PowerPerKg { get; internal set; }
        public Fixed RepairsShare { get; internal set; }

        public int OpenHour { get; internal set; }
        public int CloseHour { get; internal set; }

        /// <summary>The service fee a kg at year-0 prices (the mill's grinding, §3.5b).</summary>
        public Fixed Fee { get; internal set; }

        /// <summary>The founder's own input a day by default, in grams.</summary>
        public long OwnPerDay { get; internal set; }

        public int Input { get; internal set; }
        public IReadOnlyList<BusinessOutput> Outputs { get; internal set; }
    }

    /// <summary>Businesses (spec 016 §4): the types, the area a mill serves, funding and closing.</summary>
    public sealed class BusinessDefs
    {
        public IReadOnlyList<BusinessTypeDef> Types { get; internal set; }

        /// <summary>People outside your town whose grain comes to a mill (§3.5b), and the grain a person brings a day of life, kg.</summary>
        public int CatchmentPeople { get; internal set; }
        public Fixed GrainPerPersonPerRealDay { get; internal set; }

        /// <summary>The next village's chakki's fee a kg (year-0), and how far above it yours can be before the area goes there (§3.7).</summary>
        public Fixed NextVillageFee { get; internal set; }
        public Fixed FeeTolerance { get; internal set; }

        public Fixed BusinessLtv { get; internal set; }
        public int TermSeasons { get; internal set; }
        public Fixed OverdraftLimit { get; internal set; }
        public Fixed OverdraftRate { get; internal set; }
        public Fixed ClosingSaleShare { get; internal set; }

        /// <summary>The lender of business loans and overdrafts (§3.8), and the term of the loan you take on when a closed business still owes (§3.10).</summary>
        public string LenderId { get; internal set; }
        public int GuaranteeTermSeasons { get; internal set; }

        public int IndexOfType(string id)
        {
            for (int i = 0; i < Types.Count; i++) if (Types[i].Id == id) return i;
            return -1;
        }
    }
}
