using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class ParcelTests
    {
        /// <summary>The default content with more starting cash (in coins).</summary>
        public static GameDefs Rich(long coins = 50_00_000, System.Func<string, string> mapEdit = null)
        {
            return TestContent.Game((f, j) =>
                f == "start.json" ? j.Replace("\"startingCash\": 60000", "\"startingCash\": " + coins)
                : f == "map.json" && mapEdit != null ? mapEdit(j) : j);
        }

        private static Parcel P(Simulation sim, string id) { return sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)]; }

        private static CommandResult Buy(Simulation sim, string id) { return sim.Execute(new BuyParcel(sim.State.Clock.SimTime, id)); }

        private static CommandResult Sell(Simulation sim, string id) { return sim.Execute(new SellParcel(sim.State.Clock.SimTime, id)); }

        // Spec 002 AC-11.
        [Test]
        public void Price_Formula()
        {
            // Split n1 so tile 8 (loam, moderate, fertility 50) is a 1-acre parcel at location 1.0.
            Simulation sim = NewFarm(game: Rich(mapEdit: j => j.Replace(
                "{ \"id\": \"n1\",   \"tiles\": [8, 9],        \"locationFactor\": 1.05 }",
                "{ \"id\": \"n1\",   \"tiles\": [8],  \"locationFactor\": 1.0 }, { \"id\": \"n1b\", \"tiles\": [9], \"locationFactor\": 1.05 }")));
            Assert.That(LandRules.ParcelPrice(sim, P(sim, "n1")), Is.EqualTo(1_20_000_00));

            // s1: tile 20 clay, poor, fertility 65 and tile 21 loam, moderate, fertility 55, location 0.95.
            GameDefs d = sim.Config.Game;
            Parcel s1 = P(sim, "s1");
            long t20 = LandRules.TilePrice(d, sim.State.Land.Tiles[20]);
            long t21 = LandRules.TilePrice(d, sim.State.Land.Tiles[21]);
            Assert.That(t20, Is.EqualTo(92_443_00)); // 1,20,000 × 0.90 × 0.85 × 1.06 × 0.95 = 92,442.60, rounded to the coin
            Assert.That(t21, Is.EqualTo(1_16_280_00)); // 1,20,000 × 1.00 × 1.00 × 1.02 × 0.95
            Assert.That(LandRules.ParcelPrice(sim, s1), Is.EqualTo(t20 + t21));
        }

        // Spec 002 AC-12.
        [Test]
        public void Buy_RulesAndPayment()
        {
            Simulation poor = NewFarm();
            ulong before = poor.State.ComputeHash();
            Assert.That(Buy(poor, "w1").Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(Buy(poor, "n3").Reason, Is.EqualTo(RejectReason.NotAdjacent));
            Assert.That(Buy(poor, "home").Reason, Is.EqualTo(RejectReason.AlreadyOwned));
            Assert.That(Buy(poor, "nowhere").Reason, Is.EqualTo(RejectReason.UnknownParcel));
            Assert.That(poor.State.ComputeHash(), Is.EqualTo(before), "rejections change nothing");
            Assert.That(poor.DrainEvents().OfType<ParcelBought>(), Is.Empty);

            Simulation sim = NewFarm(game: Rich());
            sim.AdvanceTo(5 * Hour);
            long cash = sim.State.Ledger.Cash;
            long price = LandRules.ParcelPrice(sim, P(sim, "w1"));
            int slots = sim.State.Land.Slots.Count;
            sim.DrainEvents();

            Assert.That(Buy(sim, "w1").Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - price));
            Assert.That(sim.State.Ledger.Balance(Account.Land), Is.EqualTo(P(sim, "home").CostBasis + price));
            Parcel w1 = P(sim, "w1");
            Assert.That((w1.Owned, w1.Protected, w1.CostBasis, w1.AcquiredAt), Is.EqualTo((true, false, price, 5 * Hour)));
            Assert.That(sim.State.Land.Slots.Count, Is.EqualTo(slots + 4));
            foreach (int tile in new[] { 12, 13 })
                Assert.That(sim.State.Land.Slots.Count(s => s.TileId == tile && s.Kind == SlotKind.FieldEdge), Is.EqualTo(2));
            ParcelBought e = sim.DrainEvents().OfType<ParcelBought>().Single();
            Assert.That((e.ParcelId, e.Price, e.Acres), Is.EqualTo(("w1", price, 2)));
        }

        // Spec 002 AC-13.
        [Test]
        public void Adjacency_Grows()
        {
            Simulation sim = NewFarm(game: Rich());
            Assert.That(Buy(sim, "s3").Reason, Is.EqualTo(RejectReason.NotAdjacent), "s3 touches home only at a corner");
            Assert.That(Buy(sim, "nw").Reason, Is.EqualTo(RejectReason.NotAdjacent));
            Assert.That(Buy(sim, "w1").Accepted, Is.True);
            Assert.That(Buy(sim, "s3").Accepted, Is.True);
            Assert.That(Buy(sim, "nw").Accepted, Is.True);
            Assert.That(Buy(sim, "n3").Reason, Is.EqualTo(RejectReason.NotAdjacent));
        }

        // Spec 002 AC-14.
        [Test]
        public void Sell_QuickSale_AndGuards()
        {
            Simulation sim = NewFarm(game: Rich());
            Assert.That(Sell(sim, "home").Reason, Is.EqualTo(RejectReason.Protected));
            Assert.That(Sell(sim, "w1").Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(Sell(sim, "nowhere").Reason, Is.EqualTo(RejectReason.UnknownParcel));

            Buy(sim, "w1");
            Buy(sim, "s3");
            Buy(sim, "e1");
            Assert.That(Sell(sim, "w1").Reason, Is.EqualTo(RejectReason.WouldStrandLand), "s3 connects to home only through w1");

            LandSystem.SetOccupied(sim, 10, true);
            Assert.That(Sell(sim, "e1").Reason, Is.EqualTo(RejectReason.ParcelInUse), "a crop");
            LandSystem.SetOccupied(sim, 10, false);
            TreeSlot slot = sim.State.Land.Slots.First(s => s.TileId == 11);
            slot.TreeId = 7;
            Assert.That(Sell(sim, "e1").Reason, Is.EqualTo(RejectReason.ParcelInUse), "a tree");
            slot.TreeId = 0;
            sim.State.Land.Buildings.Add(new Building { Id = 99, Def = 0, TileId = 17 });
            Assert.That(Sell(sim, "e1").Reason, Is.EqualTo(RejectReason.ParcelInUse), "a building");
            sim.State.Land.Buildings.RemoveAt(sim.State.Land.Buildings.Count - 1);

            // Fertility has changed since purchase, so price ≠ cost basis: a loss on the sale.
            sim.State.Land.Tiles[18].Fertility -= Maths.Fixed.FromInt(10);
            Parcel s3 = P(sim, "s3");
            long basis = s3.CostBasis;
            long expected = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, s3), sim.Config.Game.Land.QuickSaleFactor));
            long cash = sim.State.Ledger.Cash;
            sim.DrainEvents();

            Assert.That(Sell(sim, "s3").Accepted, Is.True);
            Assert.That(expected % 100, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash + expected));
            Assert.That(sim.State.Ledger.Balance(Account.LossOnAssetSales), Is.EqualTo(basis - expected));
            Assert.That(s3.Owned, Is.False);
            Assert.That(sim.State.Land.Slots.Any(s => s.TileId == 18 || s.TileId == 19), Is.False);
            ParcelSold e = sim.DrainEvents().OfType<ParcelSold>().Single();
            Assert.That((e.ParcelId, e.Price, e.CostBasis), Is.EqualTo(("s3", expected, basis)));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            Assert.That(Sell(sim, "w1").Accepted, Is.True, "nothing left to strand");
        }
    }
}
