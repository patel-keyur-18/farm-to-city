using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Land;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>The kirana (spec 028 §3.10, ADR 0167): a shop of the grocery kind against the village grocer's staples.</summary>
    public class KiranaTests
    {
        /// <summary>A Township with <paramref name="n"/> more households of yours (income 20,000 a season) and a kirana on pitch 0, staffed and open.</summary>
        internal static Simulation Town(int n, out Business.Business shop, bool staff = true, int pitch = 0)
        {
            Simulation sim = InvoiceTests.Township();
            for (int i = 0; i < n; i++) { Household x = AddFamily(sim, 0, 30, 28, 6); x.Income = 20_000 * Coin; }
            shop = Open(sim, staff, pitch);
            return sim;
        }

        /// <summary>A family of yours with a house on a tile (so it has a home to shop from), income given.</summary>
        internal static Household Family(Simulation sim, int tile, long income, params int[] ages)
        {
            var house = new Building { Id = sim.State.Land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = tile };
            sim.State.Land.Buildings.Add(house);
            Household x = AddFamily(sim, house.Id, ages.Length == 0 ? new[] { 30, 28, 6 } : ages);
            x.Income = income * Coin;
            return x;
        }

        private static GameDefs Game(System.Func<string, string, string> edit) { return TestContent.Game(edit); }

        internal static Business.Business Open(Simulation sim, bool staff = true, int pitch = 0)
        {
            long give = 3_000_000 * Coin - sim.State.Ledger.Cash;
            if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "kirana", pitch >= 0 ? 0 : RestaurantTests.NearSquare(sim), 200_000 * Coin, false, null, pitch));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            if (staff) BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            return b;
        }

        // The type loads with its numbers; the shares must add up to 1 and the kind be known.
        [Test]
        public void Content_Is_Checked()
        {
            Simulation sim = Town(0, out Business.Business shop, staff: false);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, shop);
            Assert.That((t.Kind, t.IsShop, t.BuildingValue, t.EquipmentValue, t.Wage, t.OpenHour, t.CloseHour, t.Goods.Count), Is.EqualTo((ShopKind.Grocery, true, 20_000L * Coin, 8_000L * Coin, 12_000L * Coin, 7, 21, 8)));
            Fixed total = Fixed.Zero;
            foreach (var kv in ShopRules.Defs(sim).GroceryShares) total += kv.Value;
            Assert.That(total > Fixed.FromRatio(999, 1000) && total < Fixed.FromRatio(1001, 1000), Is.True, "the staples' shares add up to 1");
            Assert.That(ShopRules.Defs(sim).Rivals.Any(r => r.Id == "grocer_staples" && r.Kind == ShopKind.Grocery && r.Reacts), Is.True, "the village grocer's staples");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "shops.json" ? j.Replace("\"rice\": 0.25", "\"rice\": 0.30") : j), "shares must add up to 1");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "shops.json" ? j.Replace("\"kind\": \"grocery\", \"place\"", "\"kind\": \"cheese\", \"place\"") : j), "an unknown kind");
            Assert.Throws<Sim.Content.ContentException>(() => Game((f, j) => f == "shops.json" ? j.Replace("\"limits\": [500, 1000, 2000]", "\"limits\": [1000, 500]") : j), "limits go up");
        }

        // AC-24: a day's demand is the staples' shares of 0.25 kg a person; stock is filled to the amount a day; before a kirana opens the village grocer's staples serve nobody.
        [Test]
        public void Stock_Demand()
        {
            Simulation sim = InvoiceTests.Township();
            int atta = sim.Config.Game.Market.IndexOf("atta"), ghee = sim.Config.Game.Market.IndexOf("ghee");
            long all = 0;
            foreach (int g in ShopRules.KindGoods(sim, ShopKind.Grocery)) all += ShopRules.DemandPerDay(sim, ShopKind.Grocery, g, ShopGroup.Village);
            Assert.That(ShopRules.DemandPerDay(sim, ShopKind.Grocery, atta, ShopGroup.Village) * 100 / all, Is.EqualTo(35).Within(1), "atta is 35% of the groceries");
            Assert.That(ShopRules.DemandPerDay(sim, ShopKind.Grocery, ghee, ShopGroup.Village) * 100 / all, Is.EqualTo(5).Within(1), "ghee 5%");
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86400);
            Assert.That(sim.State.Shops.Rivals.Single(r => r.Kind == ShopKind.Grocery).SeasonServed, Is.Zero, "no kirana: the staples serve nobody yet");
            Business.Business b = Open(sim);
            int rice = sim.Config.Game.Market.IndexOf("rice");
            Assert.That(b.PutOut[rice], Is.GreaterThan(0), "the plan puts out each staple");
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86400);
            Assert.That(b.SeasonServed > 0 || b.Reports.Count > 0, Is.True, "it sells");
            Assert.That(sim.State.Shops.Rivals.Single(r => r.Kind == ShopKind.Grocery).SeasonServed + sim.State.Shops.Rivals.Single(r => r.Kind == ShopKind.Grocery).CustomersToday, Is.GreaterThan(0), "now the staples compete");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-24: your store's goods first: rice in your store goes to the kirana at the trader's price (the farm's sale) before anything is bought.
        [Test]
        public void Store_First()
        {
            Simulation sim = InvoiceTests.Township();
            int rice = sim.Config.Game.Market.IndexOf("rice");
            sim.State.Market.Stock[rice] += 500_000;
            long before = sim.State.Market.Stock[rice];
            Business.Business b = Open(sim);
            sim.AdvanceTo(sim.State.Clock.SimTime / 86400 * 86400 + 86400 + 7 * 3600); // the next morning's round
            Assert.That(sim.State.Market.Stock[rice], Is.LessThan(before), "it took the farm's rice first");
            Assert.That(b.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.boughtFromFarm"), Is.True);
        }

        // AC-24: the village grocer's staples cut 5% when you take a third of its share (ADR 0095 §3), and say so.
        [Test]
        public void Rival()
        {
            Simulation sim = Town(10, out Business.Business b);
            Rival staples = sim.State.Shops.Rivals.Single(r => r.Kind == ShopKind.Grocery);
            staples.LastShare = Fixed.FromRatio(9, 10); // it had nearly everything
            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, ShopRules.Defs(sim).PricePresets[1], 1)); // 10% under the reference: it wins customers
            long season = sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason;
            while (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason == season) sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            Assert.That(staples.PriceFactor, Is.LessThan(Fixed.One), "it cut its price");
            Assert.That(sim.State.Shops.Newcomers.Any(x => x.Kind == ShopKind.Grocery), Is.False, "no newcomer kirana");
        }

        // Measures a season's takings at the usual setting (ADR 0027, gap 8): run with --filter Takings.
        [Test, Explicit("measure")]
        public void Takings()
        {
            foreach (int families in new[] { 10, 20, 40 })
            {
                Simulation sim = Town(families, out Business.Business b);
                for (int d = 0; d < 12; d++) sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
                foreach (BusinessReport r in b.Reports) TestContext.Out.WriteLine("TAKINGS families " + families + " people " + sim.State.Citizens.Population + ": sales " + -r.Balance(Account.Sales) / 100 + " cogs " + r.Balance(Account.CostOfGoodsSold) / 100 + " hired profit " + BusinessBooks.NetProfit(r) / 100 + " run by you " + (BusinessBooks.NetProfit(r) + r.Balance(Account.HiredLabour)) / 100 + " stars " + b.Stars);
            }
        }
    }
}
