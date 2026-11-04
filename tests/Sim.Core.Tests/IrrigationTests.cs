using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 013 §3.1 as amended by ADR 0086 and ADR 0087: the pump, the canal's channels, drip and the borewell.</summary>
    public class IrrigationTests
    {
        private const int WellA = 86, WellB = 102; // the well acres (ADR 0032)

        private static IrrigationDef D(Simulation sim) { return sim.Config.Game.Irrigation; }

        private static Simulation At(int era)
        {
            Simulation sim = Start();
            sim.State.Era.Current = era;
            return sim;
        }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>One hour of sun on a dry tile; returns its moisture after.</summary>
        private static Fixed DryHour(Simulation sim, int tile)
        {
            Tile t = sim.State.Land.Tiles[tile];
            t.Moisture = Fixed.FromInt(5);
            LandSystem.UpdateMoisture(sim, t, LandTest.Weather(sim, "sun"));
            return t.Moisture;
        }

        private static ObligationPaid Paid(Simulation sim, string what)
        {
            return sim.DrainEvents().OfType<ObligationPaid>().SingleOrDefault(e => e.Kind == "Irrigation" && e.OwnerRef == what);
        }

        private static void ToNextClose(Simulation sim)
        {
            sim.DrainEvents();
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
        }

        // AC-1 (the pump): from the Hamlet, 4 acres you choose at 40 or more with no buckets; its power paid at the close; unpaid, off.
        [Test]
        public void Pump()
        {
            Simulation sim = At(0);
            Assert.That(sim.Execute(new InstallPump(T(sim))).Reason, Is.EqualTo(RejectReason.EraLocked));
            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA })).Reason, Is.EqualTo(RejectReason.NoWaterSource));
            Assert.That(DryHour(sim, WellA), Is.EqualTo(sim.Config.Game.Land.WellMoistureFloor), "buckets hold the well acre at 30");
            Assert.That(sim.State.Land.Tiles[WellA].WellLift, Is.GreaterThan(Fixed.Zero), "the founder's buckets");

            sim.State.Era.Current = D(sim).Pump.UnlockEra;
            long cash = sim.State.Ledger.Cash, buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new InstallPump(T(sim)));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(15000 * Coin));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(15000 * Coin), "capitalised");
            Assert.That(sim.State.Irrigation.PumpAcres, Is.EqualTo(new[] { WellA, WellB }), "it starts on the well's acres");
            Assert.That(sim.Execute(new InstallPump(T(sim))).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA, WellB, 84, 85, 100 })).Reason, Is.EqualTo(RejectReason.TooManyAcres));
            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA, 70 })).Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA, YardTile })).Reason, Is.EqualTo(RejectReason.NotFarmTile));
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB, 84, 85 }));
            foreach (int tile in new[] { WellA, 84 })
            {
                Assert.That(DryHour(sim, tile), Is.EqualTo(D(sim).Pump.MoistureFloor), "held at 40");
                Assert.That(sim.State.Land.Tiles[tile].WellLift, Is.EqualTo(Fixed.Zero), "no buckets");
            }
            Assert.That(DryHour(sim, 100), Is.LessThan(Fixed.FromInt(10)), "an acre off the list stays rain-fed");

            ToNextClose(sim);
            Assert.That(Paid(sim, "pump").Amount, Is.EqualTo(4 * 250 * Coin), "250 a season an acre");
            Assert.That(sim.State.Irrigation.PumpUnpaid, Is.False);

            CashTo(sim, 0);
            ToNextClose(sim);
            Assert.That(sim.State.Irrigation.PumpUnpaid, Is.True, "no cash for the power");
            Assert.That(DryHour(sim, 84), Is.LessThan(Fixed.FromInt(10)), "the pump is off this season");
            Assert.That(DryHour(sim, WellA), Is.EqualTo(sim.Config.Game.Land.WellMoistureFloor), "back to the buckets on the well acre");
        }

        // AC-1 (the canal): a channel on an acre on the canal or next to a channelled one; at 45 or more except in Summer; its fee at the close.
        [Test]
        public void Canal()
        {
            Simulation sim = At(0);
            Assert.That(sim.Execute(new BuildChannel(T(sim), 85)).Reason, Is.EqualTo(RejectReason.EraLocked));
            sim.State.Era.Current = D(sim).Canal.UnlockEra;
            Assert.That(IrrigationRules.BordersCanal(sim, 85), Is.True, "row 5 borders the canal (ADR 0087 §2)");
            Assert.That(IrrigationRules.BordersCanal(sim, 101), Is.False);
            Assert.That(sim.Execute(new BuildChannel(T(sim), 101)).Reason, Is.EqualTo(RejectReason.NotAdjacent), "not on the canal or a channel yet");
            Assert.That(sim.Execute(new BuildChannel(T(sim), 70)).Reason, Is.EqualTo(RejectReason.NotOwned));

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new BuildChannel(T(sim), 85));
            Ok(sim, new BuildChannel(T(sim), 101)); // home_b through home_a
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(2 * 6000 * Coin));
            Assert.That(sim.Execute(new BuildChannel(T(sim), 85)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(Calendar.SeasonAt(T(sim), sim.Clock), Is.EqualTo(Season.Spring));
            Assert.That(DryHour(sim, 101), Is.EqualTo(D(sim).Canal.MoistureFloor), "45 in Spring");
            Assert.That(sim.State.Land.Tiles[101].WellLift, Is.EqualTo(Fixed.Zero));
            Assert.That(DryHour(sim, 100), Is.LessThan(Fixed.FromInt(10)));

            ToNextClose(sim); // into Summer: the canal is closed for repairs
            Assert.That(Paid(sim, "canal").Amount, Is.EqualTo(2 * 400 * Coin), "400 a season an acre");
            Assert.That(Calendar.SeasonAt(T(sim), sim.Clock), Is.EqualTo(Season.Summer));
            Assert.That(DryHour(sim, 85), Is.LessThan(Fixed.FromInt(10)), "dry in Summer");
            Assert.That(DryHour(sim, WellB), Is.EqualTo(sim.Config.Game.Land.WellMoistureFloor), "the well's buckets still work");

            // A channelled acre may also be pumped: the higher floor, and the pump covers the Summer closure.
            sim.State.Era.Current = D(sim).Pump.UnlockEra;
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB, 85 }));
            Assert.That(DryHour(sim, 85), Is.EqualTo(D(sim).Pump.MoistureFloor));
        }

        // AC-2: drip holds a pumped acre's crop at its lowest good moisture all year; beyond the pump's acres it needs the borewell.
        [Test]
        public void Drip_Source()
        {
            Simulation sim = At(0);
            sim.State.Era.Current = D(sim).Drip.UnlockEra; // the Village
            Assert.That(sim.Execute(new LayDrip(T(sim), WellA)).Reason, Is.EqualTo(RejectReason.NoWaterSource), "drip runs on pumped water");
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB, 84, 85 }));

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new LayDrip(T(sim), WellA));
            Ok(sim, new LayDrip(T(sim), WellB));
            Ok(sim, new LayDrip(T(sim), 84));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo((20000 + 20000 + 40000) * Coin), "half subsidised on the first 2 acres");
            Assert.That(sim.DrainEvents().OfType<DripLaid>().Select(e => e.Subsidy), Is.EqualTo(new[] { 20000 * Coin, 20000 * Coin, 0L }));
            Assert.That(sim.Execute(new LayDrip(T(sim), 84)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA, WellB, 85 })).Reason, Is.EqualTo(RejectReason.DripNeedsWater));

            // Rice (moisture 60–95) on the drip acre is held at 60; the pump alone would give 40.
            Ok(sim, new Sow(T(sim), WellA, "rice", SeedSource.Certified, "chemical"));
            Ok(sim, new Sow(T(sim), 85, "rice", SeedSource.Certified, "chemical"));
            Assert.That(DryHour(sim, WellA), Is.EqualTo(Fixed.FromInt(60)));
            Assert.That(DryHour(sim, 85), Is.EqualTo(D(sim).Pump.MoistureFloor));
            CropDef rice = sim.Config.Game.Farm.Crops.First(c => c.Id == "rice");
            Assert.That(FarmSystem.CareDemand(sim, rice, sim.State.Land.Tiles[85]) - FarmSystem.CareDemand(sim, rice, sim.State.Land.Tiles[WellA]),
                Is.EqualTo(D(sim).Drip.CareCutHoursPerDay), "no weeding between the lines");
            FertiliserDef chemical = sim.Config.Game.Farm.Fertilisers.First(f => f.Id == "chemical"), none = sim.Config.Game.Farm.Fertilisers.First(f => f.Id == "none");
            Assert.That(IrrigationRules.FertiliserFactor(sim, WellA, chemical), Is.EqualTo(chemical.YieldFactor * (Fixed.One + D(sim).Drip.FertigationBonus)), "fertigation");
            Assert.That(IrrigationRules.FertiliserFactor(sim, 85, chemical), Is.EqualTo(chemical.YieldFactor));
            Assert.That(IrrigationRules.FertiliserFactor(sim, WellA, none), Is.EqualTo(Fixed.One));

            // A fifth acre: not on the pump's list, so drip needs the borewell.
            Assert.That(sim.Execute(new LayDrip(T(sim), 100)).Reason, Is.EqualTo(RejectReason.NoWaterSource));
            Ok(sim, new DrillBorewell(T(sim), 100));
            Ok(sim, new LayDrip(T(sim), 100));

            ToNextClose(sim);
            ObligationPaid drip = Paid(sim, "drip");
            Assert.That(drip.Amount, Is.EqualTo(4 * 300 * Coin + (20000 + 20000 + 40000 + 40000) * Coin / 100), "power 300 an acre and 1% upkeep");
        }

        // AC-10 (ADR 0086): from the Village, a borewell on an acre you own waters the 4 acres you choose at 40 with no buckets, its
        // power paid at the close; a fifth acre is refused; before the Village it's refused; one source an acre (ADR 0087 §3).
        [Test]
        public void Borewell()
        {
            Simulation sim = At(0);
            sim.State.Era.Current = D(sim).Pump.UnlockEra;
            Assert.That(sim.Execute(new DrillBorewell(T(sim), 100)).Reason, Is.EqualTo(RejectReason.EraLocked), "the Hamlet is too early");
            sim.State.Era.Current = D(sim).Borewell.UnlockEra;
            Assert.That(sim.Execute(new DrillBorewell(T(sim), 70)).Reason, Is.EqualTo(RejectReason.NotOwned));
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new DrillBorewell(T(sim), 100));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(30000 * Coin));
            Assert.That(sim.State.Irrigation.BorewellAcres, Is.EqualTo(new[] { 100 }), "it starts on its own acre");
            Assert.That(sim.Execute(new DrillBorewell(T(sim), 101)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            Assert.That(sim.Execute(new SetBorewellAcres(T(sim), new[] { 100, 101, 84, 85, WellA })).Reason, Is.EqualTo(RejectReason.TooManyAcres));
            Ok(sim, new SetBorewellAcres(T(sim), new[] { 100, 101, 84, 85 }));
            foreach (int tile in new[] { 100, 85 })
            {
                Assert.That(DryHour(sim, tile), Is.EqualTo(D(sim).Borewell.MoistureFloor));
                Assert.That(sim.State.Land.Tiles[tile].WellLift, Is.EqualTo(Fixed.Zero));
            }

            Ok(sim, new InstallPump(T(sim)));
            Assert.That(sim.Execute(new SetPumpAcres(T(sim), new[] { WellA, 84 })).Reason, Is.EqualTo(RejectReason.AlreadyWatered), "one pumped source an acre");
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB }));

            ToNextClose(sim);
            Assert.That(Paid(sim, "borewell").Amount, Is.EqualTo(4 * 250 * Coin));
        }

        // A field's channel, drip and a borewell go with the land when it's sold, at book value (spec 013 §3.1).
        [Test]
        public void Sold_With_The_Land()
        {
            Simulation sim = At(0);
            sim.State.Era.Current = D(sim).Borewell.UnlockEra;
            Ok(sim, new BuyParcel(T(sim), "n4")); // 70, 71: on the canal (row 4)
            Ok(sim, new BuildChannel(T(sim), 70));
            Ok(sim, new DrillBorewell(T(sim), 71));
            Ok(sim, new LayDrip(T(sim), 71));
            long buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new SellParcel(T(sim), "n4"));
            Assert.That(buildings - sim.State.Ledger.Balance(Account.Buildings), Is.EqualTo((6000 + 30000 + 20000) * Coin), "out of the books");
            Assert.That(IrrigationRules.Borewell(sim), Is.Null);
            Assert.That(sim.State.Irrigation.BorewellAcres, Is.Empty);
        }
    }
}
