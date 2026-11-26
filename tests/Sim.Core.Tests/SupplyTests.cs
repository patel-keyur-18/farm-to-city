using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 030 §3.4, §3.5 (US-268): today's supply counted at every path, Automatic and Manual, the caps, the commands and schema 25.</summary>
    public class SupplyTests
    {
        private static long NextDayAt(Simulation sim, int hour) { return (sim.State.Clock.SimTime / 86_400 + 1) * 86_400 + hour * 3600L; }

        /// <summary>A stall that puts out 3 kg of potato a day and nothing else, with plenty of potato in your store.</summary>
        private static Business.Business Stall(Simulation sim, long storeGrams = 2_000_000, bool saveable = false)
        {
            if (saveable) // a balanced entry, not the test-only SetCash command: the file must stay saveable
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Finance.Posting(Finance.Account.Cash, 1_000_000_00), new Finance.Posting(Finance.Account.Sales, -1_000_000_00));
            Business.Business b = ShopTests.Shop(sim, "veg_stall", 0, HomeTile, !saveable);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            foreach (int g in BusinessRules.TypeOf(sim, b).Goods) Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, "potato", 3_000));
            sim.State.Market.Stock[potato] = sim.State.Market.Offered[potato] + storeGrams;
            return b;
        }

        // AC-9: every gram from you and every gram bought is counted for the day from 6:00, and the day turns at 6:00.
        [Test]
        public void Counts()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(3_000), "from your store at 6:00");
            Assert.That(SupplyRules.BoughtTodayOf(sim, b, potato), Is.Zero);

            sim.State.Market.Stock[potato] = sim.State.Market.Offered[potato]; // nothing of yours left
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.Zero, "a new day's count");
            Assert.That(SupplyRules.BoughtTodayOf(sim, b, potato), Is.EqualTo(3_000), "bought at the mandi");
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.BoughtTodayOf(sim, b, potato), Is.EqualTo(3_000));
            Assert.That(b.BoughtDays[0][potato], Is.EqualTo(3_000), "yesterday's buying is kept for the stall order");
            Assert.That(SupplyRules.BoughtOver(sim, b, potato), Is.EqualTo(6_000).Or.EqualTo(3_000), "today and the days the lookback keeps");
        }

        // AC-10: Automatic is today's rule: a business never set to Manual takes all it needs from you.
        [Test]
        public void AutomaticIsToday()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Assert.That(b.SupplyManual, Is.False);
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(b.Stock[potato], Is.EqualTo(3_000));
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(3_000));
        }

        // AC-11: Manual at half of the stall's potato takes at most half its day's amount from your store; the rest at the market if allowed, none if not.
        [Test]
        public void ManualCap()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, b.Id, true));
            Ok(sim, new SetSupplyCap(sim.State.Clock.SimTime, b.Id, "potato", 50, 0));
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(1_500), "half of 3 kg from you");
            Assert.That(SupplyRules.BoughtTodayOf(sim, b, potato), Is.EqualTo(1_500), "the rest bought");
            Assert.That(b.Stock[potato], Is.EqualTo(3_000));

            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, b.OwnPerDay, false)); // buying off
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(1_500));
            Assert.That(SupplyRules.BoughtTodayOf(sim, b, potato), Is.Zero);
            Assert.That(b.Stock[potato], Is.EqualTo(1_500), "with buying off it puts out only what it may take");
            Assert.That(b.Ledger.Reconcile(), Is.Null);

            Ok(sim, new SetSupplyCap(sim.State.Clock.SimTime, b.Id, "potato", 0, 0)); // none
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.Zero);
            Assert.That(b.Stock[potato], Is.Zero);
        }

        // AC-12: Manual never takes more than it needs, and never takes what your store doesn't hold.
        [Test]
        public void CapNeverForces()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, b.Id, true));
            Ok(sim, new SetSupplyCap(sim.State.Clock.SimTime, b.Id, "potato", 100, 50_000)); // 50 kg a day: far above the 3 kg it needs
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(3_000), "no more than it needs");

            long store = sim.State.Market.Stock[potato];
            sim.AdvanceTo(NextDayAt(sim, 5));
            sim.State.Market.Stock[potato] = sim.State.Market.Offered[potato] + 1_000; // 1 kg only, set just before the morning round
            b.Stock[potato] = 0; b.StockCost[potato] = 0; // nothing left on the stall from yesterday
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600 + 1); // the morning round
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(1_000), "no more than the store holds");
            Assert.That(store, Is.GreaterThan(0));
        }

        // The commands: who may be set, the shares, the steps, Manual first.
        [Test]
        public void Commands()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetSupplyCap(t, b.Id, "potato", 50, 0)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "Manual first");
            Assert.That(sim.Execute(new SetSupplyMode(t, 999, true)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Ok(sim, new SetSupplyMode(t, b.Id, true));
            Assert.That(sim.Execute(new SetSupplyCap(t, b.Id, "potato", 30, 0)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "a share of 0, 25, 50 or 100");
            Assert.That(sim.Execute(new SetSupplyCap(t, b.Id, "potato", 100, 7_000)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "grams are one of the steps");
            Assert.That(sim.Execute(new SetSupplyCap(t, b.Id, "wheat", 50, 0)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "a good it doesn't take from you");
            Assert.That(sim.Execute(new SetSupplyCap(t, b.Id, "nothing", 50, 0)).Reason, Is.EqualTo(RejectReason.UnknownGood));
            Ok(sim, new SetSupplyCap(t, b.Id, "potato", 25, 0));
            Ok(sim, new SetSupplyCap(t, b.Id, "potato", 100, 10_000));
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Assert.That(SupplyRules.CapOf(sim, b, potato), Is.EqualTo(10_000));
            Ok(sim, new SetSupplyMode(t, b.Id, true)); // Manual again while it is Manual: nothing changes
            Assert.That(SupplyRules.CapOf(sim, b, potato), Is.EqualTo(10_000));
            Ok(sim, new SetSupplyMode(t, b.Id, false));
            Assert.That(SupplyRules.CapOf(sim, b, potato), Is.EqualTo(long.MaxValue), "Automatic has no cap");
            Ok(sim, new SetSupplyMode(t, b.Id, true));
            Assert.That(SupplyRules.CapOf(sim, b, potato), Is.EqualTo(long.MaxValue), "coming back to Manual starts again at all it needs");

            Business.Business tea = ShopTests.Shop(sim, "tea_stall", 1);
            Assert.That(sim.Execute(new SetSupplyMode(sim.State.Clock.SimTime, tea.Id, true)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "the tea stall has its milk a day instead");
        }

        // Switching Automatic to Manual starts every good at "all it needs", so nothing changes until a cap is set.
        [Test]
        public void ManualStartsAtAll()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, b.Id, true));
            Assert.That(SupplyRules.CapOf(sim, b, potato), Is.EqualTo(long.MaxValue));
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(SupplyRules.FromYouTodayOf(sim, b, potato), Is.EqualTo(3_000));
        }

        // The mill's wheat counts as from you (its own wheat a day) and bought (the market's).
        [Test]
        public void MillCounts()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 2_000_000);
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            sim.AdvanceTo(mill.ReadyAt + 1);
            sim.AdvanceTo(NextDayAt(sim, 18));
            Assert.That(SupplyRules.FromYouTodayOf(sim, mill, wheat), Is.GreaterThan(0), "your wheat");
            Assert.That(SupplyRules.FromYouTodayOf(sim, mill, wheat), Is.LessThanOrEqualTo(mill.OwnPerDay));
        }

        // The bakery's atta from your mill counts as from you, and Manual's cap limits it; the rest is bought as before.
        [Test]
        public void BakeryAttaCap()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 2_000_000);
            Business.Business bakery = RecipeTests.Found(sim, "bakery", HomeTile + 1);
            sim.AdvanceTo(System.Math.Max(mill.ReadyAt, bakery.ReadyAt) + 1);
            int atta = sim.Config.Game.Market.IndexOf("atta");
            Assert.That(SupplyRules.HasMode(sim, BusinessRules.TypeOf(sim, bakery)), Is.True);
            Assert.That(SupplyRules.CappedInputs(sim, BusinessRules.TypeOf(sim, bakery)), Does.Contain(atta));
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, bakery.Id, true));
            Ok(sim, new SetSupplyCap(sim.State.Clock.SimTime, bakery.Id, "atta", 0, 0)); // none from your mill
            sim.AdvanceTo(NextDayAt(sim, 18));
            Assert.That(SupplyRules.FromYouTodayOf(sim, bakery, atta), Is.Zero, "none from your mill");
            Assert.That(bakery.Ledger.Reconcile(), Is.Null);
            Assert.That(mill.Ledger.Reconcile(), Is.Null);
        }

        // Schema 25: the supply state round-trips through a save with its hash, and a business never set to Manual leaves the state's hash as it was.
        [Test]
        public void RoundTripAndHash()
        {
            Simulation sim = Start();
            Business.Business b = Stall(sim, 2_000_000, true);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            ulong before = sim.State.ComputeHash();
            System.Array.Clear(b.FromYouToday, 0, b.FromYouToday.Length);
            System.Array.Clear(b.BoughtToday, 0, b.BoughtToday.Length);
            b.BoughtDays.Clear();
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), "unused supply is not hashed: a town that never uses it keeps its hash");

            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, b.Id, true));
            Ok(sim, new SetSupplyCap(sim.State.Clock.SimTime, b.Id, "potato", 50, 0));
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Business.Business back = r.State.Businesses.Find(b.Id);
            Assert.That((back.SupplyManual, back.SupplyUsed, back.SupplyDay), Is.EqualTo((true, true, b.SupplyDay)));
            Assert.That(back.CapShare[potato], Is.EqualTo(50));
            Assert.That(back.FromYouToday[potato], Is.EqualTo(b.FromYouToday[potato]));
            Assert.That(back.BoughtDays.Count, Is.EqualTo(b.BoughtDays.Count));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // The commands survive the command log.
        [Test]
        public void CommandsRoundTrip()
        {
            var stream = new System.IO.MemoryStream();
            var w = new SaveWriter(stream);
            CommandCodec.Write(w, new SetSupplyMode(5, 3, true));
            CommandCodec.Write(w, new SetSupplyCap(6, 3, "potato", 25, 10_000));
            w.Flush();
            stream.Position = 0;
            var r = new SaveReader(stream, SaveCodec.SchemaVersion, null, null);
            var mode = (SetSupplyMode)CommandCodec.Read(r);
            var cap = (SetSupplyCap)CommandCodec.Read(r);
            Assert.That((mode.At, mode.BusinessId, mode.Manual), Is.EqualTo((5L, 3L, true)));
            Assert.That((cap.BusinessId, cap.Good, cap.Share, cap.Grams), Is.EqualTo((3L, "potato", 25, 10_000L)));
        }
    }
}
