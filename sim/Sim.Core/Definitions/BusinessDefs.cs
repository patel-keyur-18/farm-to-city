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

    /// <summary>
    /// The brick kiln's own numbers (spec 025 §3.3, US-243, Preview 3): bricks fired an hour, the fuel a brick takes and its price a kg
    /// (year-0 coins), the yard's size, the most the trader buys a day and the share of his price he pays. Its smoke is in <c>township.json</c>.
    /// </summary>
    public sealed class KilnDef
    {
        public long BricksPerHour { get; internal set; }
        public Fixed FuelKg { get; internal set; }
        public Fixed FuelPrice { get; internal set; }
        public long YardMax { get; internal set; }
        public long TraderPerDay { get; internal set; }
        public Fixed TraderShare { get; internal set; }
    }

    /// <summary>
    /// The tuition centre's own numbers (spec 028 §3.5, US-244, ADR 0161): batches of pupils in the evening, the fee a pupil a season (year-0 coins) and its
    /// presets, the stars it starts with and the share of affordable children sent at 0 and at 5 stars, the books and chalk a pupil uses a season, and
    /// the skill a season of tuition adds (up to a cap in all).
    /// </summary>
    public sealed class TuitionDef
    {
        public int Batches { get; internal set; }
        public int BatchSize { get; internal set; }
        public int AgeMin { get; internal set; }
        public int AgeMax { get; internal set; }
        public long Fee { get; internal set; }
        public System.Collections.Generic.IReadOnlyList<Fixed> Presets { get; internal set; }
        public Fixed StartStars { get; internal set; }
        public Fixed TakeUpLow { get; internal set; }
        public Fixed TakeUpHigh { get; internal set; }
        public long SuppliesPerPupil { get; internal set; }
        public int GainPerSeason { get; internal set; }
        public int GainMax { get; internal set; }

        /// <summary>Pupils a season: batches × batch size.</summary>
        public int Capacity { get { return Batches * BatchSize; } }
    }

    /// <summary>One dish on the restaurant's menu (US-246): its price a portion (year-0 coins), the diners' share of it, what a portion takes and its fuel and supplies.</summary>
    public sealed class DishDef
    {
        public string Id { get; internal set; }
        public string NameKey { get; internal set; }
        public Fixed Price { get; internal set; }

        /// <summary>Parts of ten diners that order it (a thali 6, a paratha plate 4).</summary>
        public int Share { get; internal set; }
        public IReadOnlyList<KeyValuePair<int, long>> Inputs { get; internal set; }
        public Fixed Fuel { get; internal set; }
        public Fixed Supplies { get; internal set; }
    }

    /// <summary>
    /// The restaurant's own numbers (spec 028 §3.7, US-246, ADR 0163): the two sittings (open 11:00 to 15:00 and 18:00 to 22:00, so the break is the hours between),
    /// the dishes, the part of a household's leisure that goes on eating out and the income below which a household eats at home, and how much dearer
    /// good quality's finer ingredients are.
    /// </summary>
    public sealed class MealsDef
    {
        public int BreakFrom { get; internal set; }
        public int BreakTo { get; internal set; }
        public IReadOnlyList<DishDef> Dishes { get; internal set; }
        public Fixed LeisureShare { get; internal set; }
        public long MinIncome { get; internal set; }

        /// <summary>Hours the doors are shut between the sittings.</summary>
        public int BreakHours { get { return BreakTo - BreakFrom; } }

        /// <summary>The ten diners' parts, added up.</summary>
        public int ShareTotal { get { int n = 0; foreach (DishDef d in Dishes) n += d.Share; return n; } }
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

        /// <summary>The brick kiln's numbers (US-243), or null: it fires bricks, not grams of goods.</summary>
        public KilnDef Kiln { get; internal set; }

        public bool IsKiln { get { return Kiln != null; } }

        /// <summary>The tuition centre's numbers (US-244), or null: it teaches pupils for a fee, not goods.</summary>
        public TuitionDef Tuition { get; internal set; }

        public bool IsTuition { get { return Tuition != null; } }

        /// <summary>The restaurant's numbers (US-246), or null: it cooks dishes to order, from your store and the market.</summary>
        public MealsDef Meals { get; internal set; }

        public bool IsMeals { get { return Meals != null; } }

        /// <summary>Hours a day its doors are open: the span less the restaurant's break between sittings.</summary>
        public int OpenSpan(int openHour, int closeHour) { return closeHour - openHour - (Meals != null ? Meals.BreakHours : 0); }

        /// <summary>What kind of shop it is (spec 017 §3.1), or None for the mill, bakery and dairy.</summary>
        public ShopKind Kind { get; internal set; }

        public bool IsShop { get { return Kind != ShopKind.None; } }

        /// <summary>The building on a rented pitch in the square when it differs (the vegetable stall's handcart and shade, §3.1); −1: the same.</summary>
        public long PitchBuildingValue { get; internal set; } = -1;
        public int PitchBuildingLifeSeasons { get; internal set; }

        /// <summary>A shop's most a real hour (§3.1): grams for the vegetable stall and the sweet shop, cups for the tea stall.</summary>
        public long CapacityPerHour { get; internal set; }

        /// <summary>The goods a stall may put out (§3.1): the vegetable stall's produce and products, the tea stall's snacks.</summary>
        public IReadOnlyList<int> Goods { get; internal set; } = new int[0];

        /// <summary>A cup of chai (§3.1): its milk (ml), its tea and sugar (coins, year-0) and the snack that goes with some cups (grams).</summary>
        public long CupMilkMl { get; internal set; }
        public Fixed CupSupplies { get; internal set; }
        public long SnackGrams { get; internal set; }

        /// <summary>
        /// Good quality's costs (spec 017 §3.5, ADR 0092): a good cup's milk (ml: full-cream); the extra share on a recipe's cost (pure
        /// ghee, fresh paneer); and whether nothing is kept overnight (leftovers cleared at 6:00 as spoilage).
        /// </summary>
        public long GoodCupMilkMl { get; internal set; }
        public Fixed GoodRecipeExtra { get; internal set; }
        public bool GoodFresh { get; internal set; }

        /// <summary>A cup's milk at <paramref name="quality"/> (1 standard, 2 good).</summary>
        public long CupMl(int quality) { return quality == 2 ? GoodCupMilkMl : CupMilkMl; }

        /// <summary>Whether a stall may put <paramref name="good"/> out.</summary>
        public bool Sells(int good)
        {
            foreach (int g in Goods) if (g == good) return true;
            return false;
        }

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

        /// <summary>A mill buys wheat at the market only while its unsold atta and bran stay under this many days of what the trader takes (ADR 0140).</summary>
        public int MarketStockDays { get; internal set; }

        /// <summary>The goods a mill's unsold output is also offered to the village market (US-264, spec 016 §3.5c): atta.</summary>
        public System.Collections.Generic.List<string> VillageGoods { get; internal set; } = new System.Collections.Generic.List<string>();

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
