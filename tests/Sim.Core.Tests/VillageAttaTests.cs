using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-264 (spec 016 §3.5c, ADR 0144 §3): the mill's unsold atta is offered to the village market, after your bakeries and the trader, never below the truck's price.</summary>
    public class VillageAttaTests
    {
        private const long Day = 86_400;

        private static Simulation RunningMill(out Business.Business b)
        {
            Simulation sim = Start();
            b = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 5_000_000);
            sim.AdvanceTo(b.ReadyAt + 1);
            Ok(sim, new Sim.Core.Commands.SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 18, b.Fee, 1_000_000, false)); // 1,000 kg of your wheat a day: more atta than the truck's 400 kg
            return sim;
        }

        // A working day: the trader takes its 400 kg first; the surplus is offered to the village and some of it sells, at the market's price, into the mill's books.
        [Test]
        public void Surplus_Sold_At_The_Village()
        {
            Simulation sim = RunningMill(out Business.Business b);
            int atta = sim.Config.Game.Market.IndexOf("atta");
            long day = sim.State.Clock.SimTime / Day + 1;
            sim.AdvanceTo(day * Day);
            sim.DrainEvents();
            long sales0 = -b.Ledger.Balance(Account.Sales);
            sim.AdvanceTo((day + 1) * Day);
            Assert.That(b.VillageToday, Is.GreaterThan(0), "some atta reached the village stall");
            Assert.That(b.VillageToday, Is.LessThan(sim.Config.Game.Market.Region.DemandPerDay(atta) * 2), "a thin market: about the village's own demand, not the surplus");
            Assert.That(sim.State.Market.TraderBoughtToday[atta], Is.LessThanOrEqualTo(sim.Config.Game.Market.Region.TraderBuyPerDay(atta)), "the trader still takes no more than 400 kg");
            Assert.That(-b.Ledger.Balance(Account.Sales) - sales0, Is.GreaterThan(0));
            Assert.That(b.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Never below the truck: while the market's price is under the trader's floor, nothing is offered.
        [Test]
        public void Never_Below_The_Truck()
        {
            Simulation sim = RunningMill(out Business.Business b);
            int atta = sim.Config.Game.Market.IndexOf("atta");
            b.Stock[atta] = 5_000_000; b.StockCost[atta] = 100_000_00;
            long t = (sim.State.Clock.SimTime / 3600 + 1) * 3600;
            sim.AdvanceTo(t - 1);
            Fixed floor = MarketRules.Floor(sim, atta);
            ((Fixed[])typeof(MarketState).GetField("Price", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sim.State.Market))[atta] = floor * Fixed.FromRatio(1, 2);
            long village = b.VillageToday;
            sim.AdvanceTo(t + 1);
            Assert.That(b.VillageToday, Is.EqualTo(village), "the market paid less than the truck: the mill kept its atta");
            sim.AdvanceTo(t + 20 * 3600); // the price climbs back to the floor over some hours: then it sells again
            Assert.That(b.VillageToday, Is.GreaterThan(village));
        }

        // What it offers: its stock less the trader's room, so a mill that has not yet filled the truck offers nothing; with more than the day's 400 kg, the rest.
        [Test]
        public void Offer_Is_What_The_Trader_Leaves()
        {
            Simulation sim = RunningMill(out Business.Business b);
            int atta = sim.Config.Game.Market.IndexOf("atta");
            long room = sim.Config.Game.Market.Region.TraderBuyPerDay(atta);
            b.Stock[atta] = room - 1000; b.StockCost[atta] = 1;
            Assert.That(BusinessRules.VillageOffer(sim, b, atta), Is.EqualTo(0), "the truck takes all of it");
            b.Stock[atta] = room + 50_000;
            long offer = BusinessRules.VillageOffer(sim, b, atta);
            Assert.That(offer, Is.GreaterThan(0).And.LessThanOrEqualTo(50_000));
            Assert.That(BusinessRules.VillageOffer(sim, b, sim.Config.Game.Market.IndexOf("bran")), Is.EqualTo(0), "only atta is offered");
            b.Stock[atta] = room + 50_000;
            b.Status = BusinessStatus.Closed;
            Assert.That(BusinessRules.VillageOffer(sim, b, atta), Is.EqualTo(0), "a closed mill offers nothing");
        }

        // Your bakery comes first: what it still needs today is kept back from the village.
        [Test]
        public void Bakery_First()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            sim.AdvanceTo(mill.ReadyAt + 1);
            Business.Business bakery = RecipeTests.Found(sim, "bakery", HomeTile + 1); // the Village's: after the Hamlet's mill
            int atta = sim.Config.Game.Market.IndexOf("atta");
            long room = sim.Config.Game.Market.Region.TraderBuyPerDay(atta);
            mill.Stock[atta] = room + 60_000; mill.StockCost[atta] = 1;
            long with = BusinessRules.VillageOffer(sim, mill, atta);
            bakery.Status = BusinessStatus.Closed;
            long without = BusinessRules.VillageOffer(sim, mill, atta);
            Assert.That(with, Is.LessThan(without), "the bakery's need is kept back");
        }
    }
}
