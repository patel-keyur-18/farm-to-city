using System.Collections.Generic;
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
    /// <summary>US-254, spec 025 §3.8, ADR 0135: heifer calves kept, grown on feed, carried at their worth for their age.</summary>
    public class HeiferTests
    {
        /// <summary>A shed with one cow ready to calve in 16 mornings; every calf a heifer (share 1) unless told otherwise.</summary>
        private static Simulation Dairy(bool keep, string share = "1")
        {
            Simulation sim = LivestockKit.WithShed(Game((f, j) => f == "livestock.json" ? j.Replace("\"share\": 0.5", "\"share\": " + share) : j));
            if (keep) Ok(sim, new SetKeepHeifers(sim.State.Clock.SimTime, true));
            return sim;
        }

        /// <summary>One morning with the feed room and straw stack topped up, so every animal is fully fed.</summary>
        private static void FedMorning(Simulation sim)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            foreach (int g in new[] { defs.Straw, defs.Feed })
            {
                long room = Market.Store.Fits(sim, g);
                if (room > 0) sim.Execute(new Buy(sim.State.Clock.SimTime, sim.Config.Game.Market.Goods[g].Id, room));
            }
            LivestockKit.ToMorning(sim);
        }

        private static Animal Calve(Simulation sim, out CalfKept kept)
        {
            Animal cow = LivestockKit.Buy(sim, "cow");
            kept = null;
            for (int d = 0; d < 16 && kept == null; d++)
            {
                sim.DrainEvents();
                FedMorning(sim);
                kept = sim.PendingEvents.OfType<CalfKept>().FirstOrDefault();
            }
            Assert.That(kept, Is.Not.Null, "the cow calved a heifer");
            return cow;
        }

        [Test]
        public void Switch_NeedsAShed_AndIsOffByDefault()
        {
            Simulation noShed = Start();
            Assert.That(noShed.Execute(new SetKeepHeifers(noShed.State.Clock.SimTime, true)).Reason, Is.EqualTo(RejectReason.NoShed));
            Simulation sim = LivestockKit.WithShed();
            Assert.That(LivestockKit.L(sim).KeepHeifers, Is.False);
            Ok(sim, new SetKeepHeifers(sim.State.Clock.SimTime, true));
            Assert.That(LivestockKit.L(sim).KeepHeifers, Is.True);
        }

        [Test]
        public void SwitchOff_EveryCalfIsSold_AsBefore()
        {
            Simulation sim = Dairy(false);
            LivestockKit.Buy(sim, "cow");
            for (int d = 0; d < 16; d++) { sim.DrainEvents(); FedMorning(sim); }
            Assert.That(LivestockKit.L(sim).Animals.Count, Is.EqualTo(1));
            Assert.That(sim.State.Ledger.Balance(Account.HerdGrowth), Is.EqualTo(0));
        }

        [Test]
        public void Kept_AHeifer_OnTheBooks_NotInFarmProfit()
        {
            Simulation sim = Dairy(true);
            long sales = sim.State.Ledger.Balance(Account.LivestockSales);
            Animal cow = Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            long calf = Market.MarketRules.Indexed(sim, sim.Config.Game.Livestock.Animals[cow.Kind].CalfPrice);
            Assert.That(h.Stage, Is.EqualTo(AnimalStage.Heifer));
            Assert.That(h.MotherName, Is.EqualTo(cow.Name));
            Assert.That(h.Cost, Is.EqualTo(calf), "a kept calf goes on the books at the calf's fair price");
            Assert.That(sim.State.Ledger.Balance(Account.HerdGrowth), Is.EqualTo(-calf), "credit: not cash");
            Assert.That(sim.State.Ledger.Balance(Account.LivestockSales), Is.EqualTo(sales), "no calf was sold");
            Assert.That(h.Shed, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            // Herd growth is Other income: outside farm profit.
            var lines = Reporting.LinesFrom(new List<KeyValuePair<Account, long>> { new KeyValuePair<Account, long>(Account.HerdGrowth, 500) });
            Assert.That(lines.Single(l => l.Key == "farmProfit").Amount, Is.EqualTo(0));
            Assert.That(lines.Single(l => l.Key == "otherIncome").Amount, Is.EqualTo(500));
        }

        [Test]
        public void Grows_AFedDay_AtATime_AndHerWorthRisesEvenly()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            AnimalDef d = sim.Config.Game.Livestock.Animals[h.Kind];
            Assert.That(LivestockRules.GrowDays(sim, d), Is.EqualTo(32), "8 seasons of 4 days");
            for (int day = 1; day <= 10; day++)
            {
                FedMorning(sim);
                Assert.That(h.StageDay, Is.EqualTo(day));
                Assert.That(h.Cost, Is.EqualTo(LivestockRules.HeiferValue(sim, h)), "her book value is her worth for her age");
            }
            Assert.That(LivestockRules.HeiferValue(sim, h), Is.GreaterThan(Market.MarketRules.Indexed(sim, d.CalfPrice)));
            Assert.That(LivestockRules.SellPrice(sim, h), Is.EqualTo(Money.RoundToCoin(Money.MulRate(LivestockRules.HeiferValue(sim, h), d.SellShare * h.Health))), "the fair's share of her worth");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void ADayWithoutFeed_IsNotAGrownDay()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            FedMorning(sim);
            Assert.That(h.StageDay, Is.EqualTo(1));
            Ok(sim, new SetBuyGreenFodder(sim.State.Clock.SimTime, false));
            sim.State.Market.Stock[LivestockRules.Defs(sim).Straw] = 0;
            sim.State.Market.Stock[LivestockRules.Defs(sim).Feed] = 0;
            LivestockKit.ToMorning(sim); // she is not fed this morning...
            Assert.That(h.FedScore, Is.LessThan(LivestockRules.Defs(sim).Livestock.HungryBelow));
            LivestockKit.ToMorning(sim); // ...so tomorrow is not a day grown
            Assert.That(h.StageDay, Is.EqualTo(2), "the morning after the fed one counted, the next did not");
            LivestockKit.ToMorning(sim);
            Assert.That(h.StageDay, Is.EqualTo(2), "still waiting");
        }

        [Test]
        public void EatsAShare_OfAGrownRation_AndWorksLess()
        {
            Simulation sim = Dairy(true);
            Animal cow = LivestockKit.Buy(sim, "cow");
            var grown = LivestockSystem.NeedOf(sim, cow);
            cow.Stage = AnimalStage.Heifer;
            var heifer = LivestockSystem.NeedOf(sim, cow);
            Assert.That(heifer.Green, Is.EqualTo((Fixed.FromInt(grown.Green) * Fixed.Parse("0.4")).RoundToLong()).Within(1));
            Assert.That(heifer.Feed, Is.EqualTo((Fixed.FromInt(grown.Feed) * Fixed.Parse("0.4")).RoundToLong()).Within(1));
            Assert.That(LivestockRules.LabourDemand(sim), Is.EqualTo(Fixed.Parse("0.5")), "0.5 h a game day, not 1.5");
        }

        [Test]
        public void Grown_AMilkingCow_AtDayZero_WithHerFirstCalf()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            AnimalDef d = sim.Config.Game.Livestock.Animals[h.Kind];
            sim.DrainEvents();
            for (int day = 0; day < 40 && h.Stage == AnimalStage.Heifer; day++) FedMorning(sim);
            Assert.That(h.Stage, Is.EqualTo(AnimalStage.Milking));
            Assert.That(h.StageDay, Is.EqualTo(0));
            Assert.That(h.Cost, Is.EqualTo(Market.MarketRules.Indexed(sim, d.Price)), "carried at a grown animal's price");
            Assert.That(h.MotherName, Is.Null);
            Assert.That(sim.PendingEvents.OfType<HeiferGrown>().Single().AnimalId, Is.EqualTo(h.Id));
            Assert.That(sim.PendingEvents.OfType<CalfKept>().Count() + sim.PendingEvents.OfType<CalfBorn>().Count() + sim.PendingEvents.OfType<CalfNoRoom>().Count(),
                Is.GreaterThanOrEqualTo(1), "her first calf was kept or sold");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void ShedFull_TheHeiferIsSold_AtTheFair()
        {
            Simulation sim = Dairy(true);
            Animal cow = LivestockKit.Buy(sim, "cow");
            for (int i = 0; i < 3; i++) LivestockKit.Buy(sim, "buffalo"); // four places, four animals
            long cash = sim.State.Ledger.Cash;
            CalfNoRoom noRoom = null;
            for (int d = 0; d < 18 && noRoom == null; d++)
            {
                sim.DrainEvents();
                FedMorning(sim);
                noRoom = sim.PendingEvents.OfType<CalfNoRoom>().FirstOrDefault();
            }
            Assert.That(noRoom, Is.Not.Null, "no place, so the heifer was sold");
            Assert.That(LivestockKit.L(sim).Animals.Count(a => a.Stage == AnimalStage.Heifer), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Balance(Account.HerdGrowth), Is.EqualTo(0));
            Assert.That(noRoom.Amount, Is.GreaterThan(0));
            Assert.That(cow.Earned, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void BullCalves_AreSold_WhileKeeping()
        {
            Simulation sim = Dairy(true, "0");
            LivestockKit.Buy(sim, "cow");
            CalfBorn born = null;
            for (int d = 0; d < 16 && born == null; d++)
            {
                sim.DrainEvents();
                FedMorning(sim);
                born = sim.PendingEvents.OfType<CalfBorn>().FirstOrDefault();
            }
            Assert.That(born, Is.Not.Null);
            Assert.That(born.Bull, Is.True);
            Assert.That(LivestockKit.L(sim).Animals.Count, Is.EqualTo(1), "no heifer");
        }

        [Test]
        public void AGrowingHeifer_SecuresNothing()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            long with = CreditRules.LivestockCollateral(sim);
            long without = CreditRules.LivestockCollateral(sim, 0, h.Id);
            Assert.That(with, Is.EqualTo(without), "a heifer is not collateral until she is grown");
        }

        [Test]
        public void Seized_AtHerWorth()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            Animal h = LivestockRules.Find(sim, kept.HeiferId);
            long price = LivestockRules.SellPrice(sim, h);
            Ok(sim, new SellAnimal(sim.State.Clock.SimTime, h.Id));
            Assert.That(sim.PendingEvents.OfType<AnimalSold>().Last().Amount, Is.EqualTo(price));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void Kept_Survives_ASave()
        {
            Simulation sim = Dairy(true);
            Calve(sim, out CalfKept kept);
            FedMorning(sim);
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Animal was = LivestockRules.Find(sim, kept.HeiferId);
            Animal h = r.State.Livestock.Animals.Find(a => a.Id == kept.HeiferId);
            Assert.That(r.State.Livestock.KeepHeifers, Is.True);
            Assert.That((h.Stage, h.StageDay, h.Cost, h.MotherName), Is.EqualTo((AnimalStage.Heifer, 1, was.Cost, was.MotherName)));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
