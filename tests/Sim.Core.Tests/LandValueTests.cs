using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-143 (spec 024 §3.1–§3.5, ADR 0119): land value from what is near an acre, and what follows it: rent's place, the house tax's base, the price of land, the slow sale.</summary>
    public class LandValueTests
    {
        private static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        private static LandValueDef Def(Simulation sim) { return sim.Config.Game.Township.LandValue; }

        private static Parcel P(Simulation sim, string id) { return sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)]; }

        private static int FirstTile(Simulation sim, string parcel) { return sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel(parcel)].Tiles[0]; }

        private static Fixed Total(Simulation sim, int tile) { return Fixed.One + LandValueRules.Parts(sim, tile).Aggregate(Fixed.Zero, (a, p) => a + p.Value); }

        // Before the Township every acre is 1.00 and price, rent and tax are as Phase 2 built them.
        [Test]
        public void NotBeforeTheTownship()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            Assert.That(LandValueRules.On(sim), Is.False);
            Assert.That(LandValueRules.Of(sim, 60), Is.EqualTo(Fixed.One));
            Assert.That(sim.State.Township.LandValues, Is.Empty);
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, "n1")).Reason, Is.EqualTo(RejectReason.NoTownship));
        }

        // AC-1: LV is 1.00 plus each part, held between the floor and the ceiling, read at the unlock.
        [Test]
        public void Parts()
        {
            Simulation sim = Township();
            Assert.That(LandValueRules.On(sim), Is.True);
            LandValueDef d = Def(sim);
            Assert.That(sim.State.Township.LandValues.Length, Is.EqualTo(sim.State.Land.Tiles.Count));
            int checkedTiles = 0;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                Fixed expect = Total(sim, t.Id);
                expect = expect < d.Floor ? d.Floor : expect > d.Ceiling ? d.Ceiling : expect;
                Assert.That(LandValueRules.Of(sim, t.Id), Is.EqualTo(expect), "tile " + t.Id);
                Assert.That(LandValueRules.Of(sim, t.Id), Is.InRange(d.Floor, d.Ceiling));
                checkedTiles++;
            }
            Assert.That(checkedTiles, Is.EqualTo(432), "the ring is read too: it is open from the Township");

            // The square's acre is within 500 m of the square and 300 m of the pond; the pond's neighbour touches the lane, so it is worth more than a far field.
            int square = d.SquareTile;
            Assert.That(LandValueRules.Parts(sim, square).Select(p => p.Key), Does.Contain("square").And.Contain("water"));
            Assert.That(LandValueRules.Of(sim, square), Is.GreaterThan(LandValueRules.Of(sim, 0)));
            // Water counts the pond or any well, never a pump (it irrigates).
            Assert.That(LandValueRules.Parts(sim, 0).Any(p => p.Key == "water"), Is.False, "the far corner has no water near");
        }

        // The school and the clinic raise the acres within their reach, but only once they stand, and only at the next close (it never jumps while you look).
        [Test]
        public void ServicesAndTheClose()
        {
            Simulation sim = Township();
            SchoolDef school = sim.Config.Game.School;
            int tile = school.Tile;
            Fixed before = LandValueRules.Of(sim, tile);
            Assert.That(LandValueRules.Parts(sim, tile).Any(p => p.Key == "school"), Is.False, "no school yet");
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 49 * Hour);
            Assert.That(LandValueRules.Parts(sim, tile).Any(p => p.Key == "school"), Is.True, "it stands: the part exists now");
            Assert.That(LandValueRules.Of(sim, tile), Is.EqualTo(before), "but the value is read at the close, not while you look");
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(LandValueRules.Of(sim, tile), Is.EqualTo(before + Def(sim).School));
        }

        // AC-1: the floor and the ceiling hold.
        [Test]
        public void Bounds()
        {
            Simulation low = TownshipTests.Start(cohort: true, (f, j) => f == "township.json" ? j.Replace("\"floor\": 0.80, \"ceiling\": 1.80", "\"floor\": 0.80, \"ceiling\": 1.05") : j);
            TownshipTests.Families(low, 2);
            TreasuryTests.Township(low);
            Assert.That(low.State.Township.LandValues.Max(), Is.EqualTo(Fixed.Parse("1.05")), "the ceiling caps the best acre");
            Assert.That(low.State.Township.LandValues.Min(), Is.GreaterThanOrEqualTo(Fixed.Parse("0.80")));
        }

        // AC-1: the town's size: 0.02 for each 10 households of yours over 20.
        [Test]
        public void TownSize()
        {
            Simulation sim = Township();
            Assert.That(LandValueRules.Parts(sim, 60).Any(p => p.Key == "size"), Is.False, "a small town adds nothing");
            TownshipTests.Families(sim, 30);
            LandPart size = LandValueRules.Parts(sim, 60).Single(p => p.Key == "size");
            Assert.That(size.Value, Is.EqualTo(Fixed.Parse("0.02") * Fixed.FromInt((EraHouseholds(sim) - 20) / 10)));
            TownshipTests.Families(sim, 200);
            Assert.That(LandValueRules.Parts(sim, 60).Single(p => p.Key == "size").Value, Is.EqualTo(Def(sim).SizeMax), "capped");
        }

        private static int EraHouseholds(Simulation sim) { return sim.State.Citizens.Households.Count; }

        // AC-2: rent's place follows LV between 0.85 and 1.50; before the Township it is ADR 0110's constants.
        [Test]
        public void RentPlace()
        {
            Simulation sim = Township();
            LandValueDef d = Def(sim);
            foreach (Tile t in sim.State.Land.Tiles)
            {
                Fixed place = LeaseRules.Place(sim, t.Id);
                Fixed expect = d.RentBase + d.RentSlope * (LandValueRules.Of(sim, t.Id) - Fixed.One);
                expect = expect < d.RentMin ? d.RentMin : expect > d.RentMax ? d.RentMax : expect;
                Assert.That(place, Is.EqualTo(expect));
                Assert.That(place, Is.InRange(Fixed.Parse("0.85"), Fixed.Parse("1.50")));
            }
            Simulation before = TownshipTests.Start(cohort: true);
            Assert.That(LeaseRules.Place(before, 60), Is.EqualTo(before.Config.Game.Population.RentInner).Or.EqualTo(before.Config.Game.Population.RentLane).Or.EqualTo(before.Config.Game.Population.RentDistrict));
        }

        // AC-3: the house tax's base is the house's value × land value; the 15%-of-rent guard still holds at every rate.
        [Test]
        public void TaxBase()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            Household x = TreasuryTests.Family(sim);
            TreasuryTests.Township(sim);
            Building home = sim.State.Land.Buildings.First(b => b.Id == x.HomeId);
            Fixed lv = LandValueRules.Of(sim, home.TileId);
            Fixed rate = Fixed.Parse("0.016");
            long value = Money.MulRate(Sim.Core.Market.MarketRules.Indexed(sim, sim.Config.Game.Buildings[home.Def].Value), lv);
            long rent = x.LeaseRent > 0 ? x.LeaseRent : Sim.Core.Market.MarketRules.Indexed(sim, sim.Config.Game.Population.BaseRent);
            long cap = Money.MulRate(rent, TaxRules.Def(sim).MaxShareOfRent);
            Assert.That(TaxRules.HouseTax(sim, home, rate), Is.EqualTo(System.Math.Min(Money.MulRate(value, rate) / 4, cap)));
            Assert.That(TaxRules.HouseTax(sim, home, Fixed.Parse("0.024")), Is.LessThanOrEqualTo(cap), "never over 15% of the rent");
        }

        // AC-1 / §3.5: a parcel's price is spec 002's price × land value, whole coins once.
        [Test]
        public void PriceFollowsLandValue()
        {
            Simulation sim = Township();
            GameDefs defs = sim.Config.Game;
            Tile t = sim.State.Land.Tiles.First(x => x.Terrain == Terrain.Farm && LandValueRules.Of(sim, x.Id) != Fixed.One);
            Fixed lv = LandValueRules.Of(sim, t.Id);
            Fixed factor = defs.Soils[t.Soil].PriceFactor * defs.Drainages[t.Drainage].PriceFactor * LandRules.FertilityPriceFactor(defs.Land, t.Fertility) * defs.Map.Parcels[t.ParcelId].LocationFactor * lv;
            Assert.That(LandRules.TilePrice(sim, t), Is.EqualTo(Money.RoundToCoin(Money.MulRate(Sim.Core.Market.MarketRules.Indexed(sim, defs.Land.AcreBasePrice), factor))));
            Assert.That(lv, Is.Not.EqualTo(Fixed.One));
        }

        private static string FarParcelNotOwned(Simulation sim)
        {
            // a bought parcel the player can sell: any neighbour that is for sale and adjacent
            foreach (ParcelDef p in sim.Config.Game.Map.Parcels)
                if (p.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(p.Id)].Owned && LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(p.Id))) return p.Id;
            return null;
        }

        private static string Bought(Simulation sim)
        {
            string id = FarParcelNotOwned(sim);
            Assert.That(id, Is.Not.Null);
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, id));
            sim.DrainEvents();
            return id;
        }

        // AC-8: a slow sale pays 100% of the land value fixed at listing after 2 days, the acre is locked meanwhile, and it can be taken off.
        [Test]
        public void SlowSale()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            Parcel p = P(sim, id);
            long price = LandRules.ParcelPrice(sim, p);
            Ok(sim, new ListParcel(sim.State.Clock.SimTime, id));
            ParcelListed e = sim.DrainEvents().OfType<ParcelListed>().Single();
            Assert.That((e.Price, e.EndsAt), Is.EqualTo((price, sim.State.Clock.SimTime + Def(sim).SlowSaleSeconds)));
            Assert.That(p.Listed, Is.True);

            // locked: no second listing, no quick sale, no sowing
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, id)).Reason, Is.EqualTo(RejectReason.Listed));
            Assert.That(sim.Execute(new SellParcel(sim.State.Clock.SimTime, id)).Reason, Is.EqualTo(RejectReason.Listed));
            int tile = sim.Config.Game.Map.Parcels[p.Id].Tiles.First(x => sim.State.Land.Tiles[x].Terrain == Terrain.Farm);
            Assert.That(sim.Execute(new Sow(sim.State.Clock.SimTime, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.Listed));

            sim.AdvanceTo(sim.State.Clock.SimTime + Def(sim).SlowSaleSeconds - 60);
            Assert.That(p.Owned, Is.True, "not before its days are up");
            sim.AdvanceTo(sim.State.Clock.SimTime + 120);
            Assert.That(p.Owned, Is.False);
            Assert.That(sim.DrainEvents().OfType<ParcelSold>().Single().Price, Is.EqualTo(price), "100% of the land value, the price fixed at listing");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null.Or.Empty);
        }

        // AC-8: a quick sale pays 80% of the land value now; the listing can be cancelled and the acre is free again.
        [Test]
        public void QuickAndCancel()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            Parcel p = P(sim, id);
            long quick = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, p), sim.Config.Game.Land.QuickSaleFactor));
            long slow = LandRules.ParcelPrice(sim, p);
            Assert.That(slow - quick, Is.GreaterThan(0), "the slow sale pays more");

            Assert.That(sim.Execute(new CancelSale(sim.State.Clock.SimTime, id)).Reason, Is.EqualTo(RejectReason.NotListed));
            Ok(sim, new ListParcel(sim.State.Clock.SimTime, id));
            Ok(sim, new CancelSale(sim.State.Clock.SimTime, id));
            Assert.That(p.Listed, Is.False);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day);
            Assert.That(p.Owned, Is.True, "a cancelled listing never sells");
            quick = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, p), sim.Config.Game.Land.QuickSaleFactor)); // today's price
            sim.DrainEvents();
            Ok(sim, new SellParcel(sim.State.Clock.SimTime, id));
            Assert.That(sim.DrainEvents().OfType<ParcelSold>().Single().Price, Is.EqualTo(quick));
        }

        // The refusals a quick sale has, a slow sale has: protected land, an acre in use.
        [Test]
        public void SlowSaleRefusals()
        {
            Simulation sim = Township();
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, sim.Config.Game.Map.Parcels[sim.Config.Game.Start.InheritedParcels[0]].Id)).Reason, Is.EqualTo(RejectReason.Protected));
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, "nowhere")).Reason, Is.EqualTo(RejectReason.UnknownParcel));
            string id = Bought(sim);
            int tile = sim.Config.Game.Map.Parcels[P(sim, id).Id].Tiles.First(x => sim.State.Land.Tiles[x].Terrain == Terrain.Farm);
            Ok(sim, new Sow(sim.State.Clock.SimTime, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, id)).Reason, Is.EqualTo(RejectReason.ParcelInUse));
        }

        // AC-12: a listing and the land values survive a save and load, and the hash matches.
        [Test]
        public void SavesRoundTrip()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            Ok(sim, new ListParcel(sim.State.Clock.SimTime, id));
            Saves.LoadResult r = Saves.SaveCodec.Read(Saves.SaveCodec.Write(sim.State, sim.Config, 0), sim.Config);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Township.LandValues, Is.EqualTo(sim.State.Township.LandValues));
            Assert.That(r.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)].Listed, Is.True);
        }
    }
}
