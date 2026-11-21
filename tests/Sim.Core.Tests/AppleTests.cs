using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-147 (spec 025 §3.5, ADR 0152): the hills open with the Township, apples grow on hill land only, 100 trees an acre, 3,000 kg a Monsoon, kept at a quarter of the hut's spoilage.</summary>
    public class AppleTests
    {
        private static Simulation Start()
        {
            Simulation sim = TownshipTests.Start();
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>The hill parcel <paramref name="id"/> made yours with its neighbours, the way a purchase would (the acres beside it yours first).</summary>
        private static int Buy(Simulation sim, string id)
        {
            MapDef map = sim.Config.Game.Map;
            int parcel = map.IndexOfParcel(id);
            foreach (int t in map.Parcels[parcel].Tiles)
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int n = map.TileAt(map.X(t) + dx, map.Y(t) + dy);
                    if (n >= 0 && map.Parcels[sim.State.Land.Tiles[n].ParcelId].Opens != "hills") sim.State.Land.Parcels[sim.State.Land.Tiles[n].ParcelId].Owned = true;
                }
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, id));
            return map.Parcels[parcel].Tiles[0];
        }

        // AC-10: the hills open with the Township and the ring, priced at 0.75 of the soil's, bought like other land.
        [Test]
        public void HillsOpenWithTheTownship()
        {
            Simulation early = TownshipTests.Start();
            Assert.That(early.Execute(new BuyParcel(early.State.Clock.SimTime, "rh1")).Reason, Is.EqualTo(RejectReason.LandClosed));
            Simulation sim = Start();
            MapDef map = sim.Config.Game.Map;
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rh1")), Is.False);
            Assert.That(LandRules.LockedParcels(sim).Count(x => x), Is.EqualTo(0), "the whole ring is open");
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "rh8")).Reason, Is.EqualTo(RejectReason.NotAdjacent), "open, but beside nothing of yours");
            Assert.That(map.Parcels.Where(p => p.Opens == "hills").All(p => p.LocationFactor == Fixed.Parse("0.75")), Is.True);
            long cash = sim.State.Ledger.Cash;
            int tile = Buy(sim, "rh8");
            Assert.That(sim.State.Land.Parcels[map.IndexOfParcel("rh8")].Owned, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.GreaterThan(0));
            Assert.That(sim.State.Land.Tiles[tile].Terrain, Is.EqualTo(Terrain.Hill));
        }

        // No houses, shops, zones or builders on hills; only farming and orchards (G4).
        [Test]
        public void HillsAreForFarmingOnly()
        {
            Simulation sim = Start();
            int tile = Buy(sim, "rh8");
            Assert.That(LandRules.Tillable(Terrain.Hill) && LandRules.Tillable(Terrain.Farm), Is.True);
            Assert.That(LandRules.Tillable(Terrain.Scrub), Is.False);
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, tile)).Accepted, Is.False, "no house on a hill");
            Assert.That(ZoningRules.HomeTiles(sim, sim.State.Land.Tiles[tile].ParcelId).Contains(tile), Is.False, "no zoning");
            // A short crop may be sown on a hill acre in its season.
            sim.AdvanceTo(sim.State.Clock.SimTime + 1);
            int season = (int)Time.Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            CropDef crop = sim.Config.Game.Farm.Crops.First(c => !c.Fodder && c.SowSeasons[season] && c.Id == "radish" || c.Id == "wheat" && c.SowSeasons[season]);
            Ok(sim, new Sow(sim.State.Clock.SimTime, tile, crop.Id, SeedSource.Certified, "none"));
            Assert.That(sim.State.Farm.CropOn(tile), Is.Not.Null);
        }

        // AC-10: apple is hill land only; the other trees grow on either.
        [Test]
        public void Apple_OnHillsOnly()
        {
            Simulation sim = Start();
            EquipmentTests.Era(sim, "township");
            int hill = Buy(sim, "rh8");
            int apple = sim.Config.Game.Farm.IndexOfTree("apple");
            Assert.That(sim.Config.Game.Farm.Trees[apple].HillOnly, Is.True);
            int valley = Enumerable.Range(0, sim.State.Land.Tiles.Count).First(t => sim.State.Land.Tiles[t].Terrain == Terrain.Farm && sim.State.Land.Parcels[sim.State.Land.Tiles[t].ParcelId].Owned
                && OrchardRules.Place(sim, t) == RejectReason.None);
            Assert.That(sim.Execute(new PlantOrchard(sim.State.Clock.SimTime, valley, "apple")).Reason, Is.EqualTo(RejectReason.HillOnly));
            Assert.That(sim.Execute(new PlantOrchard(sim.State.Clock.SimTime, hill, "guava")).Accepted, Is.True, "any tree grows on a hill");
            int other = sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel("rh8")].Tiles[1];
            long before = sim.State.Ledger.Cash;
            Ok(sim, new PlantOrchard(sim.State.Clock.SimTime, other, "apple"));
            Assert.That(before - sim.State.Ledger.Cash, Is.EqualTo(OrchardRules.Price(sim, apple)));
            Assert.That(OrchardRules.Price(sim, apple), Is.EqualTo(100 * Market.MarketRules.Value(sim, sim.Config.Game.Farm.Trees[apple].SaplingGood, 1, Market.MarketRules.InputPrice(sim, sim.Config.Game.Farm.Trees[apple].SaplingGood))), "100 saplings");
            Assert.That(sim.Execute(new PlantTree(sim.State.Clock.SimTime, sim.State.Land.Slots.First(s => s.TreeId == 0).Id, "apple")).Reason, Is.EqualTo(RejectReason.TerrainNotAllowed), "a yard slot is no hill");
        }

        // Bears after 8 seasons with 100 trees × 30 kg, in the Monsoon only; spoils 4% a day in the hut, a quarter of that in the cold store.
        [Test]
        public void Apple_Bears_3000Kg_InTheMonsoon()
        {
            Simulation sim = Start();
            int hill = Buy(sim, "rh8");
            int apple = sim.Config.Game.Farm.IndexOfTree("apple");
            TreeDef def = sim.Config.Game.Farm.Trees[apple];
            Assert.That(def.GrowthSeconds, Is.EqualTo(8 * sim.Clock.SecondsPerSeason), "8 seasons, 32 days");
            Assert.That(def.FruitingSeasons.Select((f, i) => f ? i : -1).Where(i => i >= 0), Is.EqualTo(new[] { 2 }), "Monsoon only");
            Assert.That(OrchardRules.TreesPerAcre(sim, apple), Is.EqualTo(100));
            Ok(sim, new PlantOrchard(sim.State.Clock.SimTime, hill, "apple"));
            long planted = sim.State.Clock.SimTime;
            sim.AdvanceTo(planted + def.GrowthSeconds + 1);
            Orchard o = OrchardRules.Of(sim, hill);
            Assert.That(o.State, Is.EqualTo(TreeState.Mature));
            Assert.That(o.Last, Is.Null.Or.Property("Base").EqualTo(Fixed.FromInt(3000)), "100 trees × 30 kg");
            // The first Monsoon after it bears has the batch: 3,000 kg at the base, capped as a crop's.
            while ((int)Time.Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock) != 2 || o.RipeGrams == 0) sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason / 4);
            Assert.That(o.Last.Base, Is.EqualTo(Fixed.FromInt(3000)));
            Assert.That(o.RipeGrams, Is.InRange(1_500_000L, 4_800_000L));
            Goods();
            void Goods()
            {
                var goods = sim.Config.Game.Market.Goods;
                Assert.That(goods[sim.Config.Game.Market.IndexOf("apple")].SpoilagePerDay, Is.EqualTo(Fixed.Parse("0.04")), "4% a day in the hut");
                Assert.That(sim.Config.Game.Township.ColdStore.Factor[sim.Config.Game.Market.IndexOf("apple")], Is.EqualTo(Fixed.Parse("0.25")), "a quarter of it in the cold store");
            }
        }
    }
}
