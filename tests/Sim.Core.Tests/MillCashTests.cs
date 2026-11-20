using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Finance;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>
    /// A mill that buys wheat at the market with no cash and its overdraft used up (the owner's town, 2026-11-20): the hour's purchase is sized to
    /// the cent, so paying for the power can never push the cash below 0 (it used to throw "Entry 'ledger.power' would make Cash negative", which
    /// dropped the hourly pass, ADR 0139).
    /// </summary>
    public class MillCashTests
    {
        [Test]
        public void ABuyingMill_AtItsOverdraftLimit_NeverMakesCashNegative()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestContent.Game());
                Business.Business b = BusinessTests.Mill(sim, runYourself: true, capital: 135_000_00);
                sim.AdvanceTo(b.ReadyAt + 60);
                Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, Maths.Fixed.Zero, 0, true));
                for (int s = 0; s < 8; s++)
                {
                    Assert.That(() => sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason), Throws.Nothing, "seed " + seed + " season " + s);
                    Assert.That(b.Ledger.Cash, Is.GreaterThanOrEqualTo(0), "seed " + seed);
                    Assert.That(b.Ledger.Reconcile(), Is.Null);
                }
            }
        }

        // ADR 0140: the trader takes 400 kg of atta and 100 kg of bran a day, so a mill that buys wheat at the market keeps its unsold atta and
        // bran under 2 days of that (the owner's mill had 184,475 coins of stock and no cash).
        [Test]
        public void ABuyingMill_KeepsItsUnsoldStockUnderTwoDaysOfWhatTheTraderTakes()
        {
            Simulation sim = TestSim.NewFarm(3, game: TestContent.Game());
            Business.Business b = BusinessTests.Mill(sim, runYourself: true, capital: 400_000_00);
            sim.AdvanceTo(b.ReadyAt + 60);
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, 0, true));
            int atta = sim.Config.Game.Market.IndexOf("atta"), bran = sim.Config.Game.Market.IndexOf("bran");
            var region = sim.Config.Game.Market.Region;
            Assert.That(sim.Config.Game.Businesses.MarketStockDays, Is.EqualTo(2));
            long worstAtta = 0, worstBran = 0;
            for (int h = 0; h < 24 * 20; h++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
                worstAtta = System.Math.Max(worstAtta, b.Stock[atta]);
                worstBran = System.Math.Max(worstBran, b.Stock[bran]);
            }
            Assert.That(worstAtta, Is.GreaterThan(0), "it did mill");
            Assert.That(worstAtta, Is.LessThanOrEqualTo(2 * region.TraderBuyPerDay(atta) + 400_000), "an hour's grinding past the line at most");
            Assert.That(b.Ledger.Cash, Is.GreaterThan(0), "its cash is not buried in stock");
            Assert.That(BusinessRules.MarketRoom(sim, b, BusinessRules.TypeOf(sim, b)), Is.GreaterThanOrEqualTo(0));
            b.Stock[atta] = 3 * region.TraderBuyPerDay(atta);
            Assert.That(BusinessRules.MarketRoom(sim, b, BusinessRules.TypeOf(sim, b)), Is.EqualTo(0), "over the line: no wheat bought");
        }

        // The same starving for every other kind of business: cash taken out every day so each sits at its overdraft limit, buying at the
        // market: no entry may push its cash below 0 (the owner asked, 2026-11-20).
        [TestCase("bakery", -1)]
        [TestCase("dairy", -1)]
        [TestCase("veg_stall", 0)]
        [TestCase("tea_stall", 1)]
        [TestCase("sweet_shop", 2)]
        public void EveryOtherBusiness_AtItsOverdraftLimit_NeverThrowsOrGoesNegative(string type, int pitch)
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestContent.Game());
                Business.Business b = ShopTests.Shop(sim, type, pitch);
                Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, b.OwnPerDay, true));
                for (int day = 0; day < 4 * 8; day++)
                {
                    Assert.That(() => sim.AdvanceTo(sim.State.Clock.SimTime + 86_400), Throws.Nothing, type + " seed " + seed + " day " + day);
                    Assert.That(b.Ledger.Cash, Is.GreaterThanOrEqualTo(0), type);
                    long take = b.Ledger.Cash;
                    if (take > 0 && b.Status != BusinessStatus.Closed) Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, take));
                }
                Assert.That(b.Ledger.Reconcile(), Is.Null, type);
            }
        }

        [Test]
        public void TheKiln_AtItsOverdraftLimit_NeverThrowsOrGoesNegative()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            long give = 2_000_000 * Coin - sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            int tile = sim.State.Land.Tiles.First(t => BusinessRules.Place(sim, t.Id) == RejectReason.None && People.HouseRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "brick_kiln", tile, 400_000 * Coin, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            for (int day = 0; day < 4 * 8; day++)
            {
                Assert.That(() => sim.AdvanceTo(sim.State.Clock.SimTime + 86_400), Throws.Nothing, "day " + day);
                Assert.That(b.Ledger.Cash, Is.GreaterThanOrEqualTo(0));
                long take = b.Ledger.Cash;
                if (take > 0) Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, take));
            }
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }
    }
}
