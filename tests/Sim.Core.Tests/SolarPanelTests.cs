using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-257, spec 025 §3.8, ADR 0138: a solar panel for the pump and one for the borewell, each taking 80% off its power.</summary>
    public class SolarPanelTests
    {
        private const int WellA = 86, WellB = 102;

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static Simulation Village()
        {
            Simulation sim = Start();
            sim.State.Era.Current = sim.Config.Game.Irrigation.Borewell.UnlockEra;
            EquipmentTests.Give(sim, 5_00_000);
            return sim;
        }

        private static long Panel(Simulation sim) { return Market.MarketRules.Indexed(sim, 60_000 * Coin); }

        [Test]
        public void Refusals_AndTheCost()
        {
            Simulation none = Village();
            Assert.That(none.Execute(new FitSolarPanel(T(none), 1)).Reason, Is.EqualTo(RejectReason.NoWaterSource), "no pump yet");
            Assert.That(none.Execute(new FitSolarPanel(T(none), 2)).Reason, Is.EqualTo(RejectReason.NoWaterSource), "no borewell yet");
            Assert.That(none.Execute(new FitSolarPanel(T(none), 0)).Reason, Is.EqualTo(RejectReason.InvalidArgument));

            Simulation sim = Village();
            Ok(sim, new InstallPump(T(sim)));
            long cash = sim.State.Ledger.Cash, buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new FitSolarPanel(T(sim), 1));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Panel(sim)));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(Panel(sim)), "capitalised, at cost");
            Assert.That(sim.State.Irrigation.PumpSolar, Is.EqualTo(Panel(sim)));
            Assert.That(sim.Execute(new FitSolarPanel(T(sim), 1)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.DrainEvents().OfType<SolarPanelFitted>().Single().Source, Is.EqualTo(1));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            Simulation poor = Village();
            Ok(poor, new InstallPump(T(poor)));
            Ok(poor, new SetCashLow());
            Assert.That(poor.Execute(new FitSolarPanel(T(poor), 1)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
        }

        private sealed class SetCashLow : TimedCommand
        {
            public SetCashLow() : base(0) { }
            protected override CommandResult ApplyAt(Simulation sim)
            {
                long d = 1000 * Coin - sim.State.Ledger.Cash;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.cash", "test", new Posting(Account.Cash, d), new Posting(Account.OpeningEquity, -d));
                return CommandResult.Ok;
            }
        }

        [Test]
        public void ThePanel_TakesEightyPercentOff_ThatSourcesPower_AndTheCloseChargesTheLowerSum()
        {
            Simulation sim = Village();
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB, 84, 85 })); // it starts on the 2 well acres; 4 is all it waters
            long plain = IrrigationRules.PumpPower(sim, 1);
            Assert.That(plain, Is.EqualTo(Market.MarketRules.Indexed(sim, 4 * 250 * Coin)));
            Ok(sim, new FitSolarPanel(T(sim), 1));
            long solar = IrrigationRules.PumpPower(sim, 1);
            Assert.That(solar, Is.EqualTo(plain - Money.RoundToCoin(Money.MulRate(plain, Maths.Fixed.Parse("0.8")))).Within(Coin));
            Assert.That(IrrigationRules.PowerWithout(sim, 1), Is.EqualTo(plain));
            Assert.That(IrrigationRules.Saving(sim, 1), Is.EqualTo(plain - solar));
            Assert.That(CloseDues.Next(sim, true).Any(d => d.Amount == solar), Is.True, "the next close's due line is the lower sum");

            sim.DrainEvents();
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
            ObligationPaid paid = sim.DrainEvents().OfType<ObligationPaid>().Single(e => e.Kind == "Irrigation" && e.OwnerRef == "pump");
            Assert.That(paid.Amount, Is.EqualTo(solar), "paid at the close: the lower power");
            Assert.That(sim.State.Irrigation.PumpUnpaid, Is.False);
        }

        [Test]
        public void EachSource_HasItsOwnPanel()
        {
            Simulation sim = Village();
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new DrillBorewell(T(sim), 100));
            Ok(sim, new SetBorewellAcres(T(sim), new[] { 100, 84 }));
            long borePlain = IrrigationRules.PumpPower(sim, 2);
            Ok(sim, new FitSolarPanel(T(sim), 1));
            Assert.That(IrrigationRules.Solar(sim, 1), Is.True);
            Assert.That(IrrigationRules.Solar(sim, 2), Is.False, "the borewell's own");
            Assert.That(IrrigationRules.PumpPower(sim, 2), Is.EqualTo(borePlain));
            Ok(sim, new FitSolarPanel(T(sim), 2));
            Assert.That(IrrigationRules.PumpPower(sim, 2), Is.LessThan(borePlain / 4).Within(Coin));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // The balance finding the owner accepted for later: at 60,000 the panel never pays back within the pump's life.
        [Test]
        public void ThePayback_IsLongerThanThePumpsLife_AndTheSumIsExact()
        {
            Simulation sim = Village();
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new SetPumpAcres(T(sim), new[] { WellA, WellB, 84, 85 }));
            long saving = IrrigationRules.PanelSaving(sim, 1), cost = IrrigationRules.PanelCost(sim, 1);
            Assert.That(saving, Is.EqualTo(Money.RoundToCoin(Money.MulRate(Market.MarketRules.Indexed(sim, 4 * 250 * Coin), Maths.Fixed.Parse("0.8")))));
            int pays = IrrigationRules.PaybackSeasons(sim, 1);
            Assert.That(pays, Is.EqualTo((int)((cost + saving - 1) / saving)), "rounded up: never quicker than it is");
            Assert.That(pays, Is.InRange(70, 80), "about 75 seasons");
            Assert.That(IrrigationRules.LifeSeasons(sim, 1), Is.EqualTo(40));
            Assert.That(pays, Is.GreaterThan(IrrigationRules.LifeSeasons(sim, 1)), "it would not pay back before the pump wears out");
            Ok(sim, new SetPumpAcres(T(sim), new int[0]));
            Assert.That(IrrigationRules.PanelSaving(sim, 1), Is.EqualTo(0), "no acre on the list: nothing to save yet");
            Assert.That(IrrigationRules.PaybackSeasons(sim, 1), Is.EqualTo(0));
        }

        [Test]
        public void TheBoreWellsPanel_GoesWithTheBorewell_AtCost()
        {
            Simulation sim = Village();
            Ok(sim, new BuyParcel(T(sim), "n4"));
            Ok(sim, new DrillBorewell(T(sim), 71));
            Ok(sim, new SetBorewellAcres(T(sim), new[] { 71 }));
            Ok(sim, new FitSolarPanel(T(sim), 2));
            long panel = sim.State.Irrigation.BorewellSolar, cash = sim.State.Ledger.Cash;
            Assert.That(panel, Is.GreaterThan(0));
            Ok(sim, new SellParcel(T(sim), "n4"));
            Assert.That(IrrigationRules.Borewell(sim), Is.Null);
            Assert.That(sim.State.Irrigation.BorewellSolar, Is.EqualTo(0), "the panel went with it");
            Assert.That(sim.State.Ledger.Cash - cash, Is.GreaterThanOrEqualTo(panel), "its cost came back with the land");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void TheSave_KeepsThePanels_AndOldSavesReadWithNone()
        {
            Simulation sim = Village();
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new FitSolarPanel(T(sim), 1));
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That((r.State.Irrigation.PumpSolar, r.State.Irrigation.BorewellSolar), Is.EqualTo((sim.State.Irrigation.PumpSolar, 0L)));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));

            Simulation plain = Village();
            Ok(plain, new InstallPump(T(plain)));
            Assert.That(plain.State.Irrigation.PumpSolar, Is.EqualTo(0), "none until you fit one");
        }
    }
}
