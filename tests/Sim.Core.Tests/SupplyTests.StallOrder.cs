using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 030 §3.6 (US-269): a hand growing for your vegetable stall.</summary>
    public partial class SupplyTests
    {
        /// <summary>The in-season vegetables the stall sells, in crop order.</summary>
        private static List<CropDef> InSeasonVeg(Simulation sim)
        {
            int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            return sim.Config.Game.Farm.Crops.Where(c => !c.Fodder && c.SowSeasons[season] && ShopRules.VegShare(sim, c.OutputGood) > Maths.Fixed.Zero).ToList();
        }

        /// <summary>An open stall that puts out <paramref name="small"/> g of the first in-season vegetable and <paramref name="big"/> g of the second, none else; your store holds none of either.</summary>
        private static Business.Business TwoVegStall(Simulation sim, long small, long big, out CropDef a, out CropDef b)
        {
            List<CropDef> veg = InSeasonVeg(sim);
            Assert.That(veg.Count, Is.GreaterThanOrEqualTo(2), "two vegetables are in season at the start");
            a = veg[0];
            b = veg[1];
            Business.Business stall = ShopTests.Shop(sim, "veg_stall", 0, HomeTile);
            foreach (int g in BusinessRules.TypeOf(sim, stall).Goods) Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, stall.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, stall.Id, sim.Config.Game.Market.Goods[a.OutputGood].Id, small));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, stall.Id, sim.Config.Game.Market.Goods[b.OutputGood].Id, big));
            foreach (int g in BusinessRules.TypeOf(sim, stall).Goods) { sim.State.Market.Stock[g] = sim.State.Market.Offered[g]; stall.Stock[g] = 0; }
            return stall;
        }

        private static List<int> FreeFields(Simulation sim)
        {
            return sim.State.Land.Tiles.Where(t => Land.LandRules.Tillable(t.Terrain) && sim.State.Land.Parcels[t.ParcelId].Owned && sim.State.Farm.CropOn(t.Id) == null
                && sim.State.Orchards?.Orchards.Exists(o => o.TileId == t.Id) != true).Select(t => t.Id).ToList();
        }

        private static Hand StallHand(Simulation sim)
        {
            Hand h = HireHand(sim);
            while (P(sim).Applicants.Count > 0) sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            return h;
        }

        // AC-13: the hand sows the in-season vegetable with the biggest unmet preorder, the next field takes the other (what is growing counts against the preorder),
        // and the harvest is kept in store.
        [Test]
        public void StallOrderPick()
        {
            Simulation sim = Start();
            CashTo(sim, 2_000_000_00);
            Business.Business stall = TwoVegStall(sim, 10_000, 30_000, out CropDef small, out CropDef big);
            Hand h = StallHand(sim);
            List<int> fields = FreeFields(sim);
            Assert.That(fields.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(StallOrderRules.Preorder(sim, stall, big.OutputGood), Is.EqualTo(30_000), "nothing in stock, nothing in store, nothing growing");
            var wanted = StallOrderRules.Wanted(sim, stall);
            Assert.That(wanted.Select(w => (w.Key, w.Value)), Is.EqualTo(new[] { (sim.Config.Game.Farm.IndexOfCrop(big.Id), 30_000L), (sim.Config.Game.Farm.IndexOfCrop(small.Id), 10_000L) }), "only what a hand can sow now, the biggest first");

            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.ForStall(fields[0], stall.Id, "none")));
            Assert.That(sim.State.Farm.CropOn(fields[0]).Def, Is.EqualTo(sim.Config.Game.Farm.IndexOfCrop(big.Id)), "the biggest preorder first");
            Assert.That(StallOrderRules.GrowingFor(sim, stall, big.OutputGood), Is.GreaterThan(0));
            Assert.That(StallOrderRules.Preorder(sim, stall, big.OutputGood), Is.EqualTo(System.Math.Max(0, 30_000 - StallOrderRules.GrowingFor(sim, stall, big.OutputGood))), "what grows counts against it");
            Assert.That(P(sim).OrderFor(OrderTargetKind.Field, fields[0]).Sell, Is.EqualTo(SellRule.Store), "kept in store");

            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.ForStall(fields[1], stall.Id, "none")));
            Crop second = sim.State.Farm.CropOn(fields[1]);
            Assert.That(second, Is.Not.Null);
            Assert.That(StallOrderRules.GrowingFor(sim, stall, big.OutputGood), Is.GreaterThanOrEqualTo(30_000), "a field of it covers the whole day");
            Assert.That(second.Def, Is.EqualTo(sim.Config.Game.Farm.IndexOfCrop(small.Id)), "so the next field is the other vegetable");
        }

        // AC-13: ties go to the one the stall bought most at the market, and nothing in season that fits falls back to his best pick.
        [Test]
        public void StallOrderTieAndFallback()
        {
            Simulation sim = Start();
            CashTo(sim, 2_000_000_00);
            Business.Business stall = TwoVegStall(sim, 10_000, 10_000, out CropDef a, out CropDef b);
            Hand h = StallHand(sim);
            int field = FreeFields(sim)[0];
            stall.BoughtToday[b.OutputGood] = 5_000; // the stall bought the second at the market today
            SupplyRules.Touch(sim, stall);
            Assert.That(StallOrderRules.Preorder(sim, stall, a.OutputGood), Is.EqualTo(StallOrderRules.Preorder(sim, stall, b.OutputGood)));
            var order = new StandingOrder { HandId = h.Id, Kind = OrderTargetKind.Field, Target = field, Crop = -1, ForBusiness = stall.Id, Fertiliser = 0 };
            Assert.That(StallOrderRules.Pick(sim, field, 0, stall), Is.EqualTo(sim.Config.Game.Farm.IndexOfCrop(b.Id)), "the one bought most at the market");

            foreach (CropDef c in sim.Config.Game.Farm.Crops) sim.State.Market.Stock[c.SeedGood] = 0;
            CashTo(sim, 0);
            Assert.That(StallOrderRules.Pick(sim, field, 0, stall), Is.EqualTo(-1), "no seed money: nothing fits");
            Assert.That(Orders.CropToSow(sim, order), Is.EqualTo(FarmRules.BestCrop(sim, sim.State.Land.Tiles[field], sim.Config.Game.Farm.Fertilisers[0])), "he picks as Let him choose");
        }

        // AC-14: a stall sold or closed: the order is Let him choose from then on.
        [Test]
        public void StallGone()
        {
            Simulation sim = Start();
            CashTo(sim, 2_000_000_00);
            Business.Business stall = TwoVegStall(sim, 10_000, 30_000, out CropDef small, out CropDef big);
            Hand h = StallHand(sim);
            int field = FreeFields(sim)[0];
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.ForStall(field, stall.Id, "none")));
            StandingOrder o = P(sim).OrderFor(OrderTargetKind.Field, field);
            Assert.That(StallOrderRules.Stall(sim, o), Is.Not.Null);
            stall.Status = BusinessStatus.Closed;
            Assert.That(StallOrderRules.Stall(sim, o), Is.Null, "closed: no stall");
            Assert.That(Orders.CropToSow(sim, o), Is.EqualTo(FarmRules.BestCrop(sim, sim.State.Land.Tiles[field], sim.Config.Game.Farm.Fertilisers[0])), "his own choice");
            sim.State.Businesses.Businesses.Remove(stall);
            Assert.That(StallOrderRules.Stall(sim, o), Is.Null, "sold");
            Assert.That(Orders.CropToSow(sim, o), Is.GreaterThanOrEqualTo(-1));
        }

        // The command: an open stall of yours and Keep in store only.
        [Test]
        public void StallOrderCommand()
        {
            Simulation sim = Start();
            CashTo(sim, 2_000_000_00);
            Business.Business stall = TwoVegStall(sim, 10_000, 30_000, out _, out _);
            Hand h = StallHand(sim);
            int field = FreeFields(sim)[0];
            long t = sim.State.Clock.SimTime;
            Assert.That(new SetStandingOrder(t, h.Id, OrderSpec.ForStall(field, stall.Id + 99, "none")).ApplyNow(sim).Reason, Is.EqualTo(RejectReason.InvalidArgument), "no such stall");
            Assert.That(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(field, OrderSpec.StallPrefix + stall.Id, SeedSource.Certified, "none", SellRule.Trader)).ApplyNow(sim).Reason, Is.EqualTo(RejectReason.InvalidArgument), "not to the trader");
            Assert.That(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(field, OrderSpec.StallPrefix + "x", SeedSource.Certified, "none", SellRule.Store)).ApplyNow(sim).Reason, Is.EqualTo(RejectReason.UnknownCrop));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.ForStall(field, stall.Id, "none")));
            Assert.That(P(sim).OrderFor(OrderTargetKind.Field, field).ForBusiness, Is.EqualTo(stall.Id));
            // The same command through the log's encoding.
            var stream = new System.IO.MemoryStream();
            var w = new SaveWriter(stream);
            CommandCodec.Write(w, new SetStandingOrder(t, h.Id, OrderSpec.ForStall(field, stall.Id, "compost")));
            w.Flush();
            stream.Position = 0;
            var again = (SetStandingOrder)CommandCodec.Read(new SaveReader(stream, SaveCodec.SchemaVersion, null, null));
            Assert.That((again.Order.CropId, again.Order.Sell, again.Order.FertiliserId), Is.EqualTo((OrderSpec.StallPrefix + stall.Id, SellRule.Store, "compost")));
        }

        // Schema 25: the stall's id is saved and hashed, and a town with no stall order hashes as before.
        [Test]
        public void StallOrderSaves()
        {
            Simulation sim = Start();
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Finance.Posting(Finance.Account.Cash, 2_000_000_00), new Finance.Posting(Finance.Account.Sales, -2_000_000_00));
            Business.Business stall = TwoVegStall(sim, 10_000, 30_000, out _, out _);
            Hand h = StallHand(sim);
            ulong before = sim.State.ComputeHash();
            int field = FreeFields(sim)[0];
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.ForStall(field, stall.Id, "none")));
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0, 0, 0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.People.OrderFor(OrderTargetKind.Field, field).ForBusiness, Is.EqualTo(stall.Id));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "round-trips");
            Assert.That(before, Is.Not.EqualTo(sim.State.ComputeHash()));
        }
    }
}
