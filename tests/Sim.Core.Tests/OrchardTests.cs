using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 013 §3.3 as settled by ADR 0090 (US-129): whole-acre orchards, the intercrop, shade beside them, the books.</summary>
    public class OrchardTests
    {
        private const int Field = 84, Beside = 85; // 85 shares 84's east side

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static int Crop(Simulation sim, string id) { return sim.Config.Game.Farm.IndexOfCrop(id); }

        /// <summary>10:00 on the next day: the trader's truck is in (ADR 0054).</summary>
        private static void Morning(Simulation sim) { sim.AdvanceTo((T(sim) / 86400 + 1) * 86400 + 10 * Hour); }

        private static Simulation Hamlet()
        {
            Simulation sim = Start();
            EquipmentTests.Era(sim, "hamlet");
            EquipmentTests.Give(sim, 5_00_000);
            return sim;
        }

        // AC-6: an orchard bears by spec 003's rules on every tree; intercrops grow at half until the first fruit (ADR 0090 §3).
        [Test]
        public void Bearing_Intercrop()
        {
            Simulation sim = Start();
            Assert.That(sim.Execute(new PlantOrchard(T(sim), Field, "banana")).Reason, Is.EqualTo(RejectReason.EraLocked), "orchards come with the Hamlet (ADR 0090 §2)");
            sim = Hamlet();
            int banana = sim.Config.Game.Farm.IndexOfTree("banana");
            Definitions.TreeDef def = sim.Config.Game.Farm.Trees[banana];
            long trees = sim.State.Ledger.Balance(Account.Trees), cost = OrchardRules.Price(sim, banana);
            Assert.That(cost, Is.EqualTo(400 * MarketRules.Value(sim, def.SaplingGood, 1, MarketRules.InputPrice(sim, def.SaplingGood))), "400 saplings with planting");
            Fixed before = FarmSystem.LabourDemand(sim);
            Ok(sim, new PlantOrchard(T(sim), Field, "banana"));
            long planted = T(sim);
            Assert.That(sim.State.Ledger.Balance(Account.Trees) - trees, Is.EqualTo(cost), "capitalised");
            Assert.That(FarmSystem.LabourDemand(sim) - before, Is.EqualTo(Fixed.FromInt(2)), "a young banana acre: 2 h a day");
            Assert.That(sim.Execute(new PlantOrchard(T(sim), Field, "mango")).Reason, Is.EqualTo(RejectReason.TileOccupied));

            // Between the young rows: a short crop only, at half its labour and half the acre's yield.
            int season = (int)Time.Calendar.SeasonAt(T(sim), sim.Clock);
            Definitions.CropDef other = sim.Config.Game.Farm.Crops.First(c => !c.Fodder && c.SowSeasons[season] && !sim.Config.Game.Orchard.Intercrops[Crop(sim, c.Id)]);
            Assert.That(sim.Execute(new Sow(T(sim), Field, other.Id, SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.NotAnIntercrop));
            Definitions.CropDef radish = sim.Config.Game.Farm.Crops[Crop(sim, "radish")];
            Fixed young = FarmSystem.LabourDemand(sim);
            Ok(sim, new Sow(T(sim), Field, "radish", SeedSource.Certified, "none"));
            Assert.That(FarmSystem.LabourDemand(sim) - young, Is.EqualTo(radish.LabourPerAcrePerDay * Fixed.Parse("0.5")), "half the labour");
            Assert.That(FarmRules.BestCrop(sim, sim.State.Land.Tiles[Field], sim.Config.Game.Farm.Fertilisers[0]), Is.Not.EqualTo(Crop(sim, other.Id)));
            sim.AdvanceTo(sim.State.Farm.CropOn(Field).ReadyAt);
            Crop crop = sim.State.Farm.CropOn(Field);
            Assert.That(crop.Breakdown.Base, Is.EqualTo(radish.SeasonYieldPerAcre * FarmRules.CycleShare(sim.Clock, radish) * Fixed.Parse("0.5")), "half the acre's yield");
            Ok(sim, new Harvest(T(sim), Field, Overflow.SellRestToTrader));
            Fixed shade = LandRules.ShadeFactor(sim, sim.State.Land.Tiles[Beside]);

            // The trees bear: a batch is every tree's fruit × the acre's Water, Care and Weather since the planting.
            sim.AdvanceTo(planted + def.GrowthSeconds + 1);
            Orchard o = OrchardRules.Of(sim, Field);
            Assert.That(o.State, Is.EqualTo(TreeState.Mature));
            Assert.That(o.Last.Base, Is.EqualTo(Fixed.FromInt(400 * 8)), "400 trees × 8 kg");
            Assert.That(o.RipeGrams, Is.EqualTo(o.Last.Grams).And.GreaterThan(0));
            Assert.That(o.Last.Water * o.Last.Care * o.Last.Weather, Is.GreaterThan(Fixed.Zero).And.LessThanOrEqualTo(sim.Config.Game.Farm.Farm.YieldCap));
            Assert.That(sim.Execute(new Sow(T(sim), Field, "radish", SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TreesBearing));
            Assert.That(FarmSystem.LabourDemand(sim) - before, Is.EqualTo(Fixed.FromInt(3)), "a bearing banana acre: 3 h a day");

            // ADR 0090 §1: the field beside it is shaded like one mature banana at its edge.
            Assert.That(shade - LandRules.ShadeFactor(sim, sim.State.Land.Tiles[Beside]), Is.EqualTo(def.Shade));

            // Batch 1 picked in parts: what the store and the trader can take today; the rest waits on the trees, then goes to the contractor
            // (ADR 0090 §4). Batches 2–4 sold whole on the trees. Spent after 4 harvests, written off.
            long lossBefore = sim.State.Ledger.Balance(Account.LossOnAssetSales);
            for (int batch = 1; batch <= def.HarvestLimit; batch++)
            {
                if (batch > 1) sim.AdvanceTo(sim.Clock.SecondsPerSeason * (sim.Period + 1) + 1);
                Morning(sim);
                o = OrchardRules.Of(sim, Field);
                long ripe = o.RipeGrams, stock = sim.State.Market.Stock[def.FruitGood];
                Assert.That(ripe, Is.GreaterThan(0), "batch " + batch);
                if (batch == 1)
                {
                    long room = OrchardRules.Pickable(sim, o, Overflow.SellRestToTrader);
                    Assert.That(room, Is.GreaterThan(0).And.LessThan(ripe), "a batch is more than a day's store and trader");
                    Ok(sim, new PickOrchard(T(sim), Field, Overflow.SellRestToTrader));
                    Assert.That(sim.State.Market.Stock[def.FruitGood], Is.GreaterThan(stock), "into the store");
                    Assert.That(o.RipeGrams, Is.EqualTo(ripe - room), "the rest waits on the trees");
                    Assert.That(o.Batches, Is.Zero, "not a harvest till the batch is gone");
                    Assert.That(sim.Execute(new PickOrchard(T(sim), Field, Overflow.SellRestToTrader)).Reason, Is.EqualTo(RejectReason.TraderFull));
                }
                long cash = sim.State.Ledger.Cash, price = OrchardRules.ContractorPrice(sim, o);
                Assert.That(price, Is.EqualTo(Money.MulRate(MarketRules.Value(sim, def.FruitGood, o.RipeGrams, MarketRules.Floor(sim, def.FruitGood)), Fixed.Parse("0.7"))));
                Ok(sim, new SellOrchardOnTrees(T(sim), Field));
                Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(price).And.GreaterThan(0), "70% of the trader's price");
                Assert.That(sim.State.Finance.Sales.This.Any(x => x.To == SoldTo.Contractor && x.Good == "banana"), Is.True);
            }
            Assert.That(OrchardRules.Of(sim, Field), Is.Null, "spent after 4 harvests: replant");
            Assert.That(sim.State.Ledger.Balance(Account.LossOnAssetSales) - lossBefore, Is.EqualTo(cost));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(trees));
            Assert.That(LandRules.ShadeFactor(sim, sim.State.Land.Tiles[Beside]), Is.EqualTo(shade));
        }

        // An orchard takes an empty field you own and keeps houses, a business and the second shed off it; machines don't plough it.
        [Test]
        public void Place_Clear()
        {
            Simulation sim = Hamlet();
            Ok(sim, new Sow(T(sim), Beside, "radish", SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new PlantOrchard(T(sim), Beside, "mango")).Reason, Is.EqualTo(RejectReason.TileOccupied), "something growing");
            Assert.That(sim.Execute(new PlantOrchard(T(sim), 104, "mango")).Reason, Is.EqualTo(RejectReason.NotFarmTile), "the cattle acre");
            Assert.That(sim.Execute(new PlantOrchard(T(sim), Field, "apple")).Reason, Is.EqualTo(RejectReason.UnknownTree));
            Ok(sim, new PlantOrchard(T(sim), Field, "mango"));
            Assert.That(sim.State.Orchards.Orchards.Single().Trees, Is.EqualTo(40));
            Assert.That(People.HouseRules.Place(sim, Field), Is.EqualTo(RejectReason.TileOccupied));
            Assert.That(Business.BusinessRules.Place(sim, Field), Is.EqualTo(RejectReason.TileOccupied));
            Assert.That(Equipment.EquipmentRules.Fields(sim), Does.Not.Contain(Field));

            long loss = sim.State.Ledger.Balance(Account.LossOnAssetSales), cost = sim.State.Orchards.Orchards[0].Cost;
            Ok(sim, new ClearOrchard(T(sim), Field));
            Assert.That(sim.State.Ledger.Balance(Account.LossOnAssetSales) - loss, Is.EqualTo(cost), "written off");
            Assert.That(sim.State.Jobs.All.Any(j => j.Type == FarmSystem.OrchardJob && j.State != Scheduling.JobState.Done), Is.False);
            Assert.That(sim.Execute(new ClearOrchard(T(sim), Field)).Reason, Is.EqualTo(RejectReason.NoOrchard));
            Assert.That(sim.Execute(new PickOrchard(T(sim), Field)).Reason, Is.EqualTo(RejectReason.NoOrchard));
            Assert.That(sim.Execute(new SellOrchardOnTrees(T(sim), Field)).Reason, Is.EqualTo(RejectReason.NoOrchard));
        }

        // ADR 0090: an orchard goes with its land at book value when the parcel is sold, like its irrigation.
        [Test]
        public void Sold_With_The_Land()
        {
            Simulation sim = Hamlet();
            Ok(sim, new BuyParcel(T(sim), "n4"));
            Ok(sim, new PlantOrchard(T(sim), 70, "guava"));
            long trees = sim.State.Ledger.Balance(Account.Trees), cash = sim.State.Ledger.Cash;
            Parcel parcel = sim.State.Land.Parcels[sim.State.Land.Tiles[70].ParcelId];
            long land = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), sim.Config.Game.Land.QuickSaleFactor));
            Ok(sim, new SellParcel(T(sim), "n4"));
            Assert.That(trees - sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(OrchardRules.Price(sim, sim.Config.Game.Farm.IndexOfTree("guava"))));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(land + trees - sim.State.Ledger.Balance(Account.Trees)), "the land and the orchard at book");
            Assert.That(sim.State.Orchards.Orchards, Is.Empty);
        }

        // Same seed and commands give the same orchard, in one chunk or many.
        [Test]
        public void ChunkingInvariance()
        {
            Simulation a = Hamlet(), b = Hamlet();
            foreach (Simulation s in new[] { a, b }) Ok(s, new PlantOrchard(T(s), Field, "papaya"));
            long end = T(a) + 9 * 86400;
            a.AdvanceTo(end);
            for (long t = T(b); t < end; t += 3 * Hour + 17) b.AdvanceTo(System.Math.Min(end, t));
            b.AdvanceTo(end);
            Assert.That(b.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()));
            Assert.That(a.State.Orchards.Orchards[0].RipeGrams, Is.GreaterThan(0));
        }
    }
}
