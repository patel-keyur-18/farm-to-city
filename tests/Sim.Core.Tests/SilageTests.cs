using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-255, spec 025 §3.8, ADR 0136: the silage pit and the chaff cutter.</summary>
    public class SilageTests
    {
        private static long Cost(Simulation sim, long baseCost) { return Market.MarketRules.Indexed(sim, baseCost); }

        [Test]
        public void Pit_NeedsTheShed_IsBuiltOnce_AndCosts()
        {
            Simulation none = Start();
            Assert.That(none.Execute(new BuildSilagePit(none.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.NoShed));
            Simulation sim = LivestockKit.WithShed();
            long buildings = sim.State.Ledger.Balance(Account.Buildings), cash = sim.State.Ledger.Cash;
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(Cost(sim, 10_000 * Coin)));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(sim.State.Ledger.Balance(Account.Buildings) - buildings));
            Assert.That(LivestockKit.L(sim).SilagePit, Is.True);
            Assert.That(sim.Execute(new BuildSilagePit(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void Cutter_NeedsTheShed_IsBoughtOnce_AndCosts()
        {
            Simulation none = Start();
            Assert.That(none.Execute(new BuyChaffCutter(none.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.NoShed));
            Simulation sim = LivestockKit.WithShed();
            long equipment = sim.State.Ledger.Balance(Account.Equipment);
            Ok(sim, new BuyChaffCutter(sim.State.Clock.SimTime));
            Assert.That(sim.State.Ledger.Balance(Account.Equipment) - equipment, Is.EqualTo(Cost(sim, 30_000 * Coin)));
            Assert.That(sim.Execute(new BuyChaffCutter(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void Cutter_SavesAFifth_OfGreenAndStraw_NotOfFeed()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal a = LivestockKit.Buy(sim);
            var before = LivestockSystem.NeedOf(sim, a);
            Ok(sim, new BuyChaffCutter(sim.State.Clock.SimTime));
            var after = LivestockSystem.NeedOf(sim, a);
            Assert.That(after.Green, Is.EqualTo((Fixed.FromInt(before.Green) * Fixed.Parse("0.8")).RoundToLong()).Within(1));
            Assert.That(after.Straw, Is.EqualTo((Fixed.FromInt(before.Straw) * Fixed.Parse("0.8")).RoundToLong()).Within(1));
            Assert.That(after.Feed, Is.EqualTo(before.Feed), "the ground feed is not chopped");
            Assert.That(LivestockSystem.NeedOf(sim, a, false).Green, Is.EqualTo(before.Green), "the need without the cutter is the old one");
        }

        [Test]
        public void Spare_GoesInThePit_At85Percent_InsteadOfBeingSold()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            LivestockKit.Buy(sim);
            long sales = sim.State.Ledger.Balance(Account.CropSales);
            LivestockSystem.Feed(sim, 2_000_000); // 2,000 kg cut; the buffalo needs 616 kg, the commons give 10%
            FeedingReport f = LivestockKit.L(sim).LastFeeding;
            long spare = 2_000_000 - f.GreenOwn;
            Assert.That(f.GreenSold, Is.EqualTo(0), "all of it went in the pit");
            Assert.That(f.SilagePut, Is.EqualTo((Fixed.Parse("0.85") * Fixed.FromInt(1)).MulRatio(spare, 1).RoundToLong()).Within(1));
            Assert.That(f.SilagePut + f.SilageLost, Is.EqualTo(spare));
            Assert.That(LivestockKit.L(sim).SilageGrams, Is.EqualTo(f.SilagePut));
            Assert.That(sim.State.Ledger.Balance(Account.CropSales), Is.EqualTo(sales), "nothing sold standing");
        }

        [Test]
        public void WithoutAPit_TheSpareIsSoldStanding_AsBefore()
        {
            Simulation sim = LivestockKit.WithShed();
            LivestockKit.Buy(sim);
            LivestockSystem.Feed(sim, 2_000_000);
            FeedingReport f = LivestockKit.L(sim).LastFeeding;
            Assert.That(f.SilagePut, Is.EqualTo(0));
            Assert.That(f.GreenSold, Is.EqualTo(2_000_000 - f.GreenOwn));
        }

        [Test]
        public void ThePitFeeds_BeforeAnythingIsBought()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            LivestockKit.Buy(sim);
            LivestockSystem.Feed(sim, 2_000_000);
            long silage = LivestockKit.L(sim).SilageGrams;
            LivestockSystem.Feed(sim, 0); // a morning with no cut at all
            FeedingReport f = LivestockKit.L(sim).LastFeeding;
            Assert.That(f.SilageFed, Is.EqualTo(f.GreenNeed - f.GreenCommons), "the pit covered all that the commons did not");
            Assert.That(f.GreenBought, Is.EqualTo(0));
            Assert.That(LivestockKit.L(sim).SilageGrams, Is.EqualTo(silage - f.SilageFed));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void AFullPit_SellsWhatItCannotHold()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            LivestockKit.Buy(sim);
            LivestockKit.L(sim).SetSilageForTest(19_900_000);
            LivestockSystem.Feed(sim, 2_000_000);
            FeedingReport f = LivestockKit.L(sim).LastFeeding;
            Assert.That(LivestockKit.L(sim).SilageGrams, Is.EqualTo(20_000_000), "full: 20 t");
            Assert.That(f.GreenSold, Is.GreaterThan(0), "the rest is sold standing");
            Assert.That(f.SilagePut, Is.EqualTo(100_000));
        }

        [Test]
        public void ChaffSaving_IsTheBoughtFeedItSaves()
        {
            Simulation sim = LivestockKit.WithShed();
            for (int i = 0; i < 4; i++) LivestockKit.Buy(sim);
            long perDay = LivestockRules.ChaffSavingPerDay(sim);
            Assert.That(perDay, Is.GreaterThan(0));
            long one = 0;
            Simulation single = LivestockKit.WithShed();
            LivestockKit.Buy(single);
            one = LivestockRules.ChaffSavingPerDay(single);
            Assert.That(perDay, Is.EqualTo(4 * one).Within(8), "four animals save four times one");
        }

        [Test]
        public void Both_Survive_ASave()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            Ok(sim, new BuyChaffCutter(sim.State.Clock.SimTime));
            LivestockKit.Buy(sim);
            LivestockSystem.Feed(sim, 2_000_000);
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That((r.State.Livestock.SilagePit, r.State.Livestock.ChaffCutter, r.State.Livestock.SilageGrams),
                Is.EqualTo((true, true, LivestockKit.L(sim).SilageGrams)));
            Assert.That(r.State.Livestock.LastFeeding.SilagePut, Is.EqualTo(LivestockKit.L(sim).LastFeeding.SilagePut));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
