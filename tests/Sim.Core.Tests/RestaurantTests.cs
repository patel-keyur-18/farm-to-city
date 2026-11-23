using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    public class RestaurantTests
    {
        /// <summary>The free acre nearest the square, where the dhaba's trade is.</summary>
        internal static int NearSquare(Simulation sim)
        {
            int best = -1, metres = int.MaxValue;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (BusinessRules.Place(sim, t.Id) != RejectReason.None || Sim.Core.People.HouseRules.Place(sim, t.Id) != RejectReason.None) continue;
                int m = ShopRules.Between(sim, ShopRules.Defs(sim).SquareTile, t.Id);
                if (m < metres) { metres = m; best = t.Id; }
            }
            return best;
        }

        internal static Business.Business Open(Simulation sim, bool pitch = true)
        {
            long give = 3_000_000 * Coin - sim.State.Ledger.Cash;
            if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, give), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -give));
            Ok(sim, new Sim.Core.Commands.FoundBusiness(sim.State.Clock.SimTime, "restaurant", pitch ? 0 : NearSquare(sim), 400_000 * Coin, false, null, pitch ? 0 : -1));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            return b;
        }

        // Measures the diners a Township bot's town really has (ADR 0163): run with --filter Diners.
        [Test, Explicit("measure")]
        public void Diners()
        {
            TestContent.WithCohort = true;
            SimConfig config;
            try { config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game(null)); }
            finally { TestContent.WithCohort = false; }
            foreach (int seasons in new[] { 30, 40 })
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    Tools.Balancer.Economy.Run run = Tools.Balancer.Economy.RunBot(config, new Tools.Balancer.TownshipBot(false), seasons, seed, null);
                    Simulation sim = run.Sim;
                    var hh = sim.State.Citizens.Households;
                    long min = Sim.Core.Market.MarketRules.Indexed(sim, 20000 * 100);
                    var rich = hh.Where(x => x.Income >= min).ToList();
                    TestContext.Out.WriteLine("DINERS seasons " + seasons + " seed " + seed + ": households " + hh.Count + ", income >= min " + rich.Count + ", mean leisure of those " + (rich.Count == 0 ? 0 : rich.Average(x => x.Leisure) / 100) + ", town/day " + MealsRules.PerDay(sim, ShopGroup.Town) + ", village/day " + MealsRules.PerDay(sim, ShopGroup.Village) + ", index " + sim.State.Market.PriceIndex);
                }
        }

        /// <summary>A Township with <paramref name="n"/> households of yours that can eat out: income 40,000 a season, leisure 5,700 (about what the households' budgets give).</summary>
        private static Simulation Town(int n, System.Func<string, string, string> edit = null)
        {
            Simulation sim = edit == null ? InvoiceTests.Township() : TownshipFor(edit);
            for (int i = 0; i < n; i++) { Household x = AddFamily(sim, 0, 30, 28, 6); x.Income = 40_000 * Coin; x.Leisure = 5_700 * Coin; }
            return sim;
        }

        private static Simulation TownshipFor(System.Func<string, string, string> edit)
        {
            Simulation sim = TownshipTests.Start(cohort: true, edit);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            return sim;
        }

        /// <summary>Runs <paramref name="hours"/> hours and returns the portions the restaurant and the dhaba served (a season's close in between starts the counts again).</summary>
        private static (long Yours, long Dhaba) Run(Simulation sim, Business.Business b, int hours)
        {
            long yours = 0, dhaba = 0, y0 = b.SeasonServed, d0 = sim.State.Shops.Rivals.Where(r => r.Kind == ShopKind.Meals).Sum(r => r.SeasonServed);
            for (int h = 0; h < hours; h++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
                long y1 = b.SeasonServed, d1 = sim.State.Shops.Rivals.Where(r => r.Kind == ShopKind.Meals).Sum(r => r.SeasonServed);
                yours += y1 >= y0 ? y1 - y0 : y1;
                dhaba += d1 >= d0 ? d1 - d0 : d1;
                y0 = y1; d0 = d1;
            }
            return (yours, dhaba);
        }

        private static MealsDef D(Simulation sim) { return MealsRules.Type(sim).Meals; }

        private static bool Near(Fixed a, Fixed b) { Fixed e = Fixed.FromRatio(1, 1000); return a - b < e && b - a < e; }

        private static void ToHour(Simulation sim, int hour)
        {
            long t = sim.State.Clock.SimTime / 86400 * 86400 + hour * 3600;
            if (t <= sim.State.Clock.SimTime) t += 86400;
            sim.AdvanceTo(t);
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // The type loads with its numbers: a shop of the kind meals with two sittings and two dishes; a bad menu is refused.
        [Test]
        public void Content_Is_Checked()
        {
            Simulation sim = InvoiceTests.Township();
            BusinessTypeDef t = MealsRules.Type(sim);
            Assert.That((t.Kind, t.IsShop, t.ByRecipe, t.OpenHour, t.CloseHour, t.CapacityPerHour, t.Wage), Is.EqualTo((ShopKind.Meals, true, false, 11, 22, 30L, 20_000L * 100)));
            Assert.That((D(sim).BreakFrom, D(sim).BreakTo, D(sim).Dishes.Count, D(sim).ShareTotal, D(sim).MinIncome), Is.EqualTo((15, 18, 2, 10, 20_000L * 100)));
            Assert.That(t.OpenSpan(t.OpenHour, t.CloseHour), Is.EqualTo(8), "four hours at lunch and four at dinner");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "businesses.json" ? j.Replace("\"break\": [15, 18]", "\"break\": [9, 18]") : j), "the break lies between the sittings");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "businesses.json" ? j.Replace("\"lentils\": 0.05", "\"unobtainium\": 0.05") : j), "an unknown ingredient");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "shops.json" ? j.Replace("\"kind\": \"meals\",  \"metres\"", "\"kind\": \"cheese\",  \"metres\"") : j), "an unknown kind");
        }

        // AC-17 Demand: a household earning the minimum sets aside its share of leisure and buys floor(that / the portion's price) portions a season, over the days; below the minimum it eats at home.
        [Test]
        public void Demand()
        {
            Simulation sim = Town(0);
            long none = MealsRules.PerDay(sim, ShopGroup.Town);
            Assert.That(none, Is.Zero, "no household of yours has money to eat out");
            Household poor = AddFamily(sim, 0, 30, 28);
            poor.Income = 15_000 * Coin; poor.Leisure = 3_000 * Coin;
            Assert.That(MealsRules.PerDay(sim, ShopGroup.Town), Is.Zero, "under 20,000 a season: it eats at home");
            Household x = AddFamily(sim, 0, 30, 28);
            x.Income = 40_000 * Coin; x.Leisure = 5_700 * Coin;
            long price = Sim.Core.Finance.Money.Value(1, MealsRules.Average(sim, D(sim)), 1);
            long each = Sim.Core.Finance.Money.MulRate(x.Leisure, D(sim).LeisureShare) / price;
            Assert.That(MealsRules.PerDay(sim, ShopGroup.Town), Is.EqualTo(each / sim.Clock.DaysPerSeason), "30% of leisure, in whole portions, spread over the days");
            Assert.That(MealsRules.Diners(sim), Is.EqualTo(1));
            for (int i = 0; i < 9; i++) { Household y = AddFamily(sim, 0, 30, 28); y.Income = 40_000 * Coin; y.Leisure = 5_700 * Coin; }
            Assert.That(MealsRules.PerDay(sim, ShopGroup.Town), Is.EqualTo(10 * each / sim.Clock.DaysPerSeason));
            Assert.That(MealsRules.PerDay(sim, ShopGroup.Village), Is.GreaterThan(0), "the old village's households eat out too");
        }

        // The doors: open at the two sittings only; nobody is served in the break or at night; the cook's rate caps an hour.
        [Test]
        public void Sittings()
        {
            Simulation sim = Town(30);
            Business.Business b = Open(sim);
            long start = sim.State.Clock.SimTime / 86400 * 86400 + 86400;
            sim.AdvanceTo(start);
            long served = b.SeasonServed;
            for (int hour = 0; hour < 24; hour++)
            {
                sim.AdvanceTo(start + (hour + 1) * 3600);
                bool sitting = hour >= 11 && hour < 15 || hour >= 18 && hour < 22;
                Assert.That(b.SeasonServed > served, Is.EqualTo(sitting), "hour " + hour);
                Assert.That(BusinessRules.OpenAt(sim, b, hour), Is.EqualTo(sitting), "open at " + hour);
                Assert.That(b.SeasonServed - served, Is.LessThanOrEqualTo(30), "30 portions an hour at most, hour " + hour);
                served = b.SeasonServed;
            }
            Reconciles(sim);
        }

        // AC-17 Menu_Buying: a portion is cooked to order: the ingredients come from your store at the trader's price first (a cash sale to the farm), then the market at the retail price.
        [Test]
        public void Menu_Buying()
        {
            Simulation sim = Town(5);
            Business.Business b = Open(sim);
            Ok(sim, new SetDish(sim.State.Clock.SimTime, b.Id, 1, false)); // thali only, so the ingredients are easy to count
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            DishDef thali = D(sim).Dishes[0];
            int rice = thali.Inputs[0].Key;
            long grams = thali.Inputs[0].Value * 10;
            foreach (var kv in thali.Inputs) sim.State.Market.Stock[kv.Key] = 1_000_000;
            long farm = sim.State.Ledger.Cash, cash = b.Ledger.Cash;
            long sales = MealsRules.Sell(sim, b, t, 10);
            long store = 0;
            foreach (var kv in thali.Inputs) store += Sim.Core.Market.MarketRules.Value(sim, kv.Key, kv.Value * 10, Sim.Core.Market.MarketRules.Floor(sim, kv.Key));
            Assert.That(sales, Is.EqualTo(Sim.Core.Finance.Money.Value(10, thali.Price * sim.State.Market.PriceIndex, 1)), "ten thalis at the thali's price");
            Assert.That(sim.State.Market.Stock[rice], Is.EqualTo(1_000_000 - grams), "the rice came from your store");
            Assert.That(sim.State.Ledger.Cash - farm, Is.EqualTo(store), "the farm was paid the trader's price for it: a cash sale");
            long fuel = Sim.Core.Finance.Money.Value(10, thali.Fuel * sim.State.Market.PriceIndex, 1), supplies = Sim.Core.Finance.Money.Value(10, thali.Supplies * sim.State.Market.PriceIndex, 1);
            Assert.That(b.Ledger.Cash - cash, Is.EqualTo(sales - store - fuel - supplies), "the kitchen paid for the ingredients, the gas and the supplies");
            Assert.That(b.Stock[rice], Is.Zero, "nothing is left over: it is cooked to order");

            foreach (var kv in thali.Inputs) sim.State.Market.Stock[kv.Key] = 0; // an empty store: the market's retail price
            long cash2 = b.Ledger.Cash;
            MealsRules.Sell(sim, b, t, 10);
            long market = 0;
            foreach (var kv in thali.Inputs) market += Sim.Core.Market.MarketRules.Value(sim, kv.Key, kv.Value * 10, MealsRules.MarketPrice(sim, kv.Key));
            Assert.That(b.Ledger.Cash - cash2, Is.EqualTo(sales - market - fuel - supplies), "from the market at its retail price");
            Assert.That(market, Is.GreaterThan(store), "dearer than the store's");
            Reconciles(sim);
        }

        // Both dishes at 6 to 4; a dish off hands its diners to the other; the last cannot go; only a restaurant has dishes.
        [Test]
        public void Dishes()
        {
            Simulation sim = Town(5);
            Business.Business b = Open(sim);
            Assert.That(MealsRules.Split(D(sim), 0, 100), Is.EqualTo(new long[] { 60, 40 }));
            Assert.That(MealsRules.Split(D(sim), 0, 7), Is.EqualTo(new long[] { 5, 2 }), "the remainder goes to the first dish");
            Ok(sim, new SetDish(sim.State.Clock.SimTime, b.Id, 0, false));
            Assert.That(MealsRules.Split(D(sim), b.DishesOff, 100), Is.EqualTo(new long[] { 0, 100 }), "the paratha takes the thali's diners");
            Assert.That(sim.Execute(new SetDish(sim.State.Clock.SimTime, b.Id, 1, false)).Accepted, Is.False, "the last dish stays");
            Assert.That(sim.Execute(new SetDish(sim.State.Clock.SimTime, b.Id, 5, true)).Accepted, Is.False, "no such dish");
            Ok(sim, new SetDish(sim.State.Clock.SimTime, b.Id, 0, true));
            Assert.That(b.DishesOff, Is.Zero);
            Business.Business tea = InvoiceTests.Found(sim, "tuition");
            Assert.That(sim.Execute(new SetDish(sim.State.Clock.SimTime, tea.Id, 0, false)).Accepted, Is.False, "not a restaurant");
        }

        // The dhaba takes its share by the shops' score: a good, cheap restaurant wins more; a dear one loses them; both are served.
        [Test]
        public void Rival_Shares()
        {
            long Served(Fixed price, int quality)
            {
                Simulation sim = Town(30);
                Business.Business b = Open(sim);
                Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, price, quality));
                (long yours, long dhaba) = Run(sim, b, 48);
                Assert.That(dhaba, Is.GreaterThan(0), "the dhaba serves some");
                return yours;
            }
            long usual = Served(Fixed.One, 1), cheap = Served(Fixed.FromRatio(9, 10), 1), dear = Served(Fixed.FromRatio(12, 10), 1);
            Assert.That(dear, Is.LessThan(usual), "a price above the usual sends diners to the dhaba");
            Assert.That(cheap, Is.GreaterThanOrEqualTo(usual));
        }

        // The kitchen cooks only what its cash can pay for: portions an hour = what it can spend over the dearest cost of a portion of the dishes on the menu.
        [Test]
        public void No_Cash_No_Cooking()
        {
            Simulation sim = Town(30);
            Business.Business b = Open(sim);
            long spend = BusinessBooks.CanSpend(sim, b);
            DishDef thali = D(sim).Dishes[0], paratha = D(sim).Dishes[1];
            long both = (MealsRules.PortionCost(sim, b, thali) * 6 + MealsRules.PortionCost(sim, b, paratha) * 4 + 9) / 10;
            Assert.That(MealsRules.Afford(sim, b), Is.EqualTo(spend / both));
            Ok(sim, new SetDish(sim.State.Clock.SimTime, b.Id, 0, false));
            Assert.That(MealsRules.Afford(sim, b), Is.EqualTo(spend / MealsRules.PortionCost(sim, b, paratha)), "the paratha alone");
            long drain = b.Ledger.Cash + BusinessBooks.CanSpend(sim, b) - b.Ledger.Cash;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, b.Ledger.Cash), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, -b.Ledger.Cash));
            Assert.That(MealsRules.Afford(sim, b), Is.LessThan(spend / MealsRules.PortionCost(sim, b, paratha)), "with the cash gone it can cook fewer");
        }

        // Good quality cooks with finer ingredients: they cost a tenth more.
        [Test]
        public void Good_Quality()
        {
            Simulation sim = Town(5);
            Business.Business b = Open(sim);
            DishDef thali = D(sim).Dishes[0];
            long standard = MealsRules.PortionCost(sim, b, thali);
            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, Fixed.One, 2));
            long good = MealsRules.PortionCost(sim, b, thali);
            Assert.That(good, Is.GreaterThan(standard));
            long extra = Sim.Core.Finance.Money.MulRate(standard - Sim.Core.Finance.Money.Value(1, thali.Fuel * sim.State.Market.PriceIndex, 1), MealsRules.Type(sim).GoodRecipeExtra);
            Assert.That(System.Math.Abs(good - standard - extra), Is.LessThanOrEqualTo(2), "a tenth of the ingredients and supplies");
        }

        // The money: in a town that can fill it, the plan reads in profit hired (3,000 to 8,000 a season) and 18,000 to 28,000 run by you; the actual days after the plan agree and the books reconcile.
        [Test]
        public void Money()
        {
            Simulation sim = Town(25);
            BusinessTypeDef t = MealsRules.Type(sim);
            BusinessRules.Projection hired = BusinessRules.Projected(sim, t, false, 0, Fixed.Zero), mine = BusinessRules.Projected(sim, t, true, 0, Fixed.Zero);
            Assert.That(hired.Profit / Coin, Is.InRange(3_000, 8_000), "hired: " + hired.Profit / Coin);
            Assert.That(mine.Profit / Coin, Is.InRange(18_000, 28_000), "run by you: " + mine.Profit / Coin);
            Business.Business b = Open(sim);
            (long yours, long dhaba) = Run(sim, b, 72);
            Assert.That(yours, Is.GreaterThan(300), "a town of 25 households fills the kitchen's days");
            Assert.That(dhaba, Is.GreaterThan(0));
            Assert.That(b.Stars, Is.GreaterThan(Fixed.FromRatio(5, 2)), "served diners lift the stars");
            Reconciles(sim);
        }

        // Without a restaurant of yours nothing runs: the dhaba's figures stay at their start (a town without one keeps its state hash).
        [Test]
        public void Dhaba_Waits()
        {
            Simulation sim = Town(30);
            ulong before = sim.State.ComputeHash();
            sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            Assert.That(sim.State.Shops.Rivals.Where(r => r.Kind == ShopKind.Meals).All(r => r.SeasonServed == 0 && r.CustomersToday == 0 && r.LastArrivals == 0), Is.True);
            Assert.That(sim.State.Shops.LostSeason[(int)ShopKind.Meals] + sim.State.Shops.LostToday[(int)ShopKind.Meals], Is.Zero);
            Assert.That(before, Is.Not.Zero);
        }

        // A price above the usual can draw a newcomer's food cart at the close (the shops' rule).
        [Test]
        public void Newcomer()
        {
            Simulation sim = Town(30, (f, j) => f == "shops.json" ? j.Replace("\"chance\": 0.5", "\"chance\": 1") : j);
            Business.Business b = Open(sim);
            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, Fixed.FromRatio(12, 10), 1));
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason - 3600);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 3600);
            Assert.That(sim.State.Shops.Newcomers.Any(n => n.Kind == ShopKind.Meals), Is.True, "a food cart opened beside it");
        }
    }
}
