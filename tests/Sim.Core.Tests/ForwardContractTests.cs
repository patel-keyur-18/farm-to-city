using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>The kit the contract tests share: a quiet Township, a harvest on the tally, the close that makes the offers.</summary>
    internal static class ForwardKit
    {
        internal static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        internal static void Harvest(Simulation sim, string id, long kg) { ContractRules.Harvested(sim, Good(sim, id), kg * 1000); }

        /// <summary>A Township with last season's harvest on the tally (wheat 1,000 kg, maize 800, rice 500, potato 200) and the close that makes the offers run.</summary>
        internal static Simulation WithOffers()
        {
            Simulation sim = InsuranceKit.AtSix();
            Harvest(sim, "wheat", 1000);
            Harvest(sim, "maize", 800);
            Harvest(sim, "rice", 500);
            Harvest(sim, "potato", 200);
            InsuranceKit.Close(sim);
            return sim;
        }

        internal static Contract Offer(Simulation sim, string good) { return ContractRules.State(sim).Offers.Single(o => o.Good == good); }

        internal static Contract Accept(Simulation sim, string good)
        {
            Contract o = Offer(sim, good);
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), o.Id)).Accepted, Is.True);
            return ContractRules.State(sim).Open.Single(c => c.Id == o.Id);
        }

        /// <summary>Runs to the last hour of the season, so a close is the next thing that happens.</summary>
        internal static void ToLastHour(Simulation sim) { sim.AdvanceTo((InsuranceKit.Now(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason - 3600); }
    }

    /// <summary>US-156 forward contracts (spec 027 §3.7, AC-13): the trader's offer, delivery on the first kilos, the shortfall.</summary>
    public class ForwardContractTests
    {
        // The two biggest staples at 300 kg or more get an offer: 40% of last season's harvest rounded to 100 kg, for the season after this one.
        [Test]
        public void Offer()
        {
            Simulation sim = ForwardKit.WithOffers();
            ContractsState s = ContractRules.State(sim);
            Assert.That(s.Offers.Select(o => o.Good).ToArray(), Is.EqualTo(new[] { "wheat", "maize" }), "the biggest two; rice is third, potato is under 300 kg");
            Assert.That(ForwardKit.Offer(sim, "wheat").Quantity, Is.EqualTo(400 * 1000), "40% of 1,000 kg");
            Assert.That(ForwardKit.Offer(sim, "maize").Quantity, Is.EqualTo(300 * 1000), "40% of 800 kg is 320, rounded to 100");
            Assert.That(s.Offers.All(o => o.Delivery == sim.Period + 1), Is.True, "the season after the one you are in");
            Assert.That(sim.PendingEvents.OfType<ContractOffered>().Count(), Is.EqualTo(2));

            Simulation small = InsuranceKit.AtSix();
            ForwardKit.Harvest(small, "wheat", 250);
            InsuranceKit.Close(small);
            Assert.That(ContractRules.State(small).Offers, Is.Empty, "250 kg is under the 300 kg floor");

            Simulation none = InsuranceKit.AtSix();
            InsuranceKit.Close(none);
            Assert.That(ContractRules.State(none).Offers, Is.Empty, "no harvest, no offer");
        }

        // The price is the expected price less 3%, never above it.
        [Test]
        public void NeverBetterThanExpected()
        {
            Simulation sim = ForwardKit.WithOffers();
            foreach (Contract o in ContractRules.State(sim).Offers)
            {
                Assert.That(o.Price, Is.LessThan(o.Expected));
                Assert.That(o.Price, Is.EqualTo(ContractRules.OfferPrice(sim, o.Expected)));
                Assert.That(System.Math.Abs((o.Price / o.Expected - Fixed.FromRatio(97, 100)).Raw), Is.LessThanOrEqualTo(2), "3% under");
                int season = (int)(o.Delivery % 4);
                int g = ForwardKit.Good(sim, o.Good);
                Fixed index = sim.State.Market.PriceIndex;
                Assert.That(o.Expected, Is.EqualTo(sim.Config.Game.Market.Goods[g].BasePrice * index * sim.Config.Game.Market.Region.SeasonalFactor(g, season) - MarketRules.TransportCost(sim, g)), "the delivery season's floor, not today's");
            }
            sim.Config.Game.Contracts.Discount = Fixed.FromRatio(-1, 10); // a bad content edit can never lift a price above the expected one
            Assert.That(ContractRules.OfferPrice(sim, Fixed.FromInt(20)), Is.EqualTo(Fixed.FromInt(20)));
        }

        // Accept: the offer becomes a contract; the same offer again, an unknown one and a lapsed one are refused.
        [Test]
        public void Accept()
        {
            Simulation sim = ForwardKit.WithOffers();
            Contract wheat = ForwardKit.Offer(sim, "wheat");
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), wheat.Id)).Accepted, Is.True);
            ContractsState s = ContractRules.State(sim);
            Assert.That(s.Open.Single().Id, Is.EqualTo(wheat.Id));
            Assert.That(s.Offers.Count, Is.EqualTo(1));
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), wheat.Id)).Reason, Is.EqualTo(RejectReason.AlreadyContracted));
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), 999)).Reason, Is.EqualTo(RejectReason.UnknownOffer));
            long maize = ForwardKit.Offer(sim, "maize").Id;
            InsuranceKit.Close(sim); // the offer lapses with the close
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), maize)).Reason, Is.EqualTo(RejectReason.OfferLapsed));
            Assert.That(s.Open.Single().Id, Is.EqualTo(wheat.Id), "an accepted contract stays");
        }

        // The limit is per delivery season: a contract that is delivering does not block the next season's offer for the same crop.
        [Test]
        public void Limit_PerDeliverySeason()
        {
            Simulation sim = ForwardKit.WithOffers();
            sim.Config.Game.Contracts.MaxOpen = 1;
            ForwardKit.Accept(sim, "wheat");
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), ForwardKit.Offer(sim, "maize").Id)).Reason, Is.EqualTo(RejectReason.TooManyContracts));
            ForwardKit.Harvest(sim, "wheat", 1000);
            InsuranceKit.Close(sim); // wheat's contract is now delivering; the new wheat offer is for the season after
            Contract next = ForwardKit.Offer(sim, "wheat");
            Assert.That(next.Delivery, Is.EqualTo(sim.Period + 1));
            Assert.That(sim.Execute(new AcceptContract(InsuranceKit.Now(sim), next.Id)).Accepted, Is.True);
        }

        // Delivery: in its season the first kilos the trader buys are paid at the contract price; the rest at the floor.
        [Test]
        public void Delivery()
        {
            Simulation sim = ForwardKit.WithOffers();
            Contract c = ForwardKit.Accept(sim, "wheat");
            int g = ForwardKit.Good(sim, "wheat");
            Assert.That(Store.SellToTrader(sim, g, 50_000, "test"), Is.EqualTo(MarketRules.Value(sim, g, 50_000, MarketRules.Floor(sim, g))), "before its season, the floor");
            Assert.That(c.Filled, Is.EqualTo(0));

            InsuranceKit.Close(sim);
            Assert.That(sim.Period, Is.EqualTo(c.Delivery));
            long cash = sim.State.Ledger.Cash;
            Assert.That(ContractRules.SaleValue(sim, g, 300_000, MarketRules.Floor(sim, g)), Is.EqualTo(MarketRules.Value(sim, g, 300_000, c.Price)), "the Sell button shows what it will pay");
            long paid = Store.SellToTrader(sim, g, 300_000, "test");
            Assert.That(paid, Is.EqualTo(MarketRules.Value(sim, g, 300_000, c.Price)), "300 kg, all at the contract price");
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(paid));
            Assert.That(c.Filled, Is.EqualTo(300_000));

            Fixed floor = MarketRules.Floor(sim, g);
            paid = Store.SellToTrader(sim, g, 200_000, "test");
            Assert.That(paid, Is.EqualTo(MarketRules.Value(sim, g, 100_000, c.Price) + MarketRules.Value(sim, g, 100_000, floor)), "the last 100 kg at the contract price, the rest at the floor");
            Assert.That(c.Left, Is.EqualTo(0));
            Assert.That(c.Gain, Is.EqualTo(MarketRules.Value(sim, g, 400_000, c.Price) - MarketRules.Value(sim, g, 400_000, floor)), "what the contract paid over the floor, kept for its card");
            Assert.That(sim.PendingEvents.OfType<ContractFilled>().Count(), Is.EqualTo(2));

            long more = Store.SellToTrader(sim, g, 100_000, "test");
            Assert.That(more, Is.EqualTo(MarketRules.Value(sim, g, 100_000, floor)), "filled: the floor again");
        }

        // The shortfall: unfilled kilos × what the season's average price rose above the contract's; a price that fell costs nothing.
        [Test]
        public void Shortfall()
        {
            Simulation sim = ForwardKit.WithOffers();
            Contract c = ForwardKit.Accept(sim, "wheat");
            int g = ForwardKit.Good(sim, "wheat");
            InsuranceKit.Close(sim);
            Store.SellToTrader(sim, g, 100_000, "test");
            ForwardKit.ToLastHour(sim);
            c.Samples = 4;
            c.FloorSum = (c.Price + Fixed.FromInt(2)) * Fixed.FromInt(4); // the average rose 2 coins a kg above the contract
            long gain = c.Gain;
            long expected = Money.Value(300_000, Fixed.FromInt(2), 1000);
            Assert.That(expected, Is.EqualTo(60_000));
            Assert.That(ContractRules.Shortfall(sim, c), Is.EqualTo(expected));
            Assert.That(CloseDues.Next(sim).Any(d => d.Kind == DueKind.Contract && d.Amount == expected), Is.True, "the close's dues show it before it is taken");
            sim.DrainEvents();
            InsuranceKit.Close(sim);
            Assert.That(sim.State.Ledger.Balance(Account.ContractSettlement), Is.EqualTo(expected), "a farm cost");
            ContractsState s = ContractRules.State(sim);
            Assert.That(s.Open.Any(x => x.Id == c.Id), Is.False);
            ContractRecord rec = s.History.Single();
            Assert.That((rec.Filled, rec.Shortfall, rec.Against), Is.EqualTo((100_000L, expected, gain - expected)));
            Assert.That(sim.PendingEvents.OfType<ContractSettled>().Single().Shortfall, Is.EqualTo(expected));

            // A price that fell: nothing to pay, and the contract is a gain.
            Simulation fell = ForwardKit.WithOffers();
            Contract f = ForwardKit.Accept(fell, "wheat");
            InsuranceKit.Close(fell);
            ForwardKit.ToLastHour(fell);
            f.Samples = 4;
            f.FloorSum = (f.Price - Fixed.FromInt(2)) * Fixed.FromInt(4);
            Assert.That(ContractRules.Shortfall(fell, f), Is.EqualTo(0));
            InsuranceKit.Close(fell);
            Assert.That(fell.State.Ledger.Balance(Account.ContractSettlement), Is.EqualTo(0));
            Assert.That(ContractRules.State(fell).History.Single().Shortfall, Is.EqualTo(0));
        }

        // What cash cannot pay is carried and paid first at the next close; it is never lost and never grows.
        [Test]
        public void Shortfall_Carried()
        {
            Simulation sim = ForwardKit.WithOffers();
            Contract c = ForwardKit.Accept(sim, "wheat");
            InsuranceKit.Close(sim);
            ForwardKit.ToLastHour(sim);
            c.Samples = 4;
            c.FloorSum = (c.Price + Fixed.FromInt(2)) * Fixed.FromInt(4);
            long due = ContractRules.Shortfall(sim, c);
            InsuranceKit.SetCash(sim, 0);
            InsuranceKit.Close(sim);
            ContractsState s = ContractRules.State(sim);
            Assert.That(s.Owed, Is.EqualTo(due), "no cash: all of it carried");
            Assert.That(CloseDues.Next(sim).Any(d => d.Kind == DueKind.Contract && d.Amount == due), Is.True, "and shown as a due");
            sim.State.Ledger.Post(InsuranceKit.Now(sim), sim.Period, "test", "test", new Posting(Account.Cash, 5_000_000), new Posting(Account.RegionalGrants, -5_000_000));
            InsuranceKit.Close(sim);
            Assert.That(s.Owed, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Balance(Account.ContractSettlement), Is.EqualTo(due), "paid once");
        }

        // A harvest through the Harvest command is on the tally; a fodder crop or a good not on the list is not.
        [Test]
        public void Harvest_Tally()
        {
            Simulation sim = InsuranceKit.AtSix();
            int tile = InsuranceKit.SowWheat(sim);
            Crop crop = sim.State.Farm.CropOn(tile);
            sim.AdvanceTo(crop.ReadyAt + 1);
            long grams = sim.State.Farm.CropOn(tile).HarvestGrams;
            Assert.That(sim.Execute(new Harvest(InsuranceKit.Now(sim), tile)).Accepted, Is.True);
            Assert.That(ContractsState.Of(ContractRules.State(sim).HarvestThis, "wheat"), Is.EqualTo(grams));
            ForwardKit.Harvest(sim, "radish", 500);
            Assert.That(ContractsState.Of(ContractRules.State(sim).HarvestThis, "radish"), Is.EqualTo(0), "radish is not a contract staple");
        }

        [Test]
        public void SaveRoundTrips()
        {
            Simulation sim = ForwardKit.WithOffers();
            ForwardKit.Accept(sim, "wheat");
            InsuranceKit.Close(sim);
            Store.SellToTrader(sim, ForwardKit.Good(sim, "wheat"), 100_000, "test");
            ForwardKit.Harvest(sim, "rice", 300);
            var loaded = SaveTests.RoundTrip(sim);
            ContractsState a = ContractRules.State(sim), b = loaded.State.Township.Contracts;
            Assert.That(b.Open.Single().Filled, Is.EqualTo(a.Open.Single().Filled));
            Assert.That(b.Offers.Count, Is.EqualTo(a.Offers.Count));
            Assert.That(ContractsState.Of(b.HarvestThis, "rice"), Is.EqualTo(300_000));
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
