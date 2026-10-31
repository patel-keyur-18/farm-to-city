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

    /// <summary>
    /// A recipe (spec 016 §3.9, §3.9c): a batch's inputs (goods, grams) into one product, with fuel (expensed as power and fuel)
    /// and supplies (part of the product's cost), coins a batch at year-0 prices. Made in any amount, pro rata to the batch.
    /// </summary>
    public sealed class RecipeDef
    {
        public string Id { get; internal set; }
        public IReadOnlyList<KeyValuePair<int, long>> Inputs { get; internal set; }
        public int Output { get; internal set; }
        public long OutputGrams { get; internal set; }
        public Fixed Fuel { get; internal set; }
        public Fixed Supplies { get; internal set; }

        /// <summary>Grams of inputs a batch, all together (what counts against the rate).</summary>
        public long InputGrams
        {
            get { long n = 0; foreach (KeyValuePair<int, long> kv in Inputs) n += kv.Value; return n; }
        }
    }

    /// <summary>A business type from <c>content/businesses.json</c> (spec 016 §3.1): the flour mill (Hamlet), the bakery and the dairy (Village).</summary>
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

        /// <summary>The skill the role reads (spec 015 §3.4): crafting (miller, baker) or farming (dairyman).</summary>
        public bool WorkerFarms { get; internal set; }

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

        /// <summary>The mill's input and its yields (spec 016 §3.5a); −1 and empty for a type that works by recipes.</summary>
        public int Input { get; internal set; } = -1;
        public IReadOnlyList<BusinessOutput> Outputs { get; internal set; } = new BusinessOutput[0];

        /// <summary>The recipes, in the order the worker makes them (§3.9c): a product's recipes in order of preference (buffalo milk first for paneer).</summary>
        public IReadOnlyList<RecipeDef> Recipes { get; internal set; } = new RecipeDef[0];

        /// <summary>The goods it makes by recipe, each once, in recipe order.</summary>
        public IReadOnlyList<int> Products { get; internal set; } = new int[0];

        /// <summary>What the village shops buy at the morning round (§3.9c), kg a person a day of life, by good.</summary>
        public IReadOnlyList<KeyValuePair<int, Fixed>> Shops { get; internal set; } = new KeyValuePair<int, Fixed>[0];

        /// <summary>Whether it keeps your animals' milk at the 6:00 milking, up to its milk a day (the dairy, §3.9b).</summary>
        public bool TakesMilk { get; internal set; }

        /// <summary>What the dairy pays neighbours above the co-op's price a litre for their milk, when it buys it (§3.9b; coins, year-0).</summary>
        public Fixed NeighbourMilkPremium { get; internal set; }

        public bool ByRecipe { get { return Recipes.Count > 0; } }

        /// <summary>Whether it makes <paramref name="good"/> by a recipe.</summary>
        public bool Makes(int good)
        {
            foreach (int p in Products) if (p == good) return true;
            return false;
        }
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
