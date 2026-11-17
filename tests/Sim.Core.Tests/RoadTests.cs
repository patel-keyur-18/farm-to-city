using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-146 (spec 023 §3.3, ADR 0116): the petition for the paved link road, its share, the 4-day build and the road tier in every price.</summary>
    public class RoadTests
    {
        private const long Share = 25_000 * Coin;

        /// <summary>A Village town with one family of yours; <paramref name="households"/> is what the petition needs (10 in the content).</summary>
        private static Simulation Start(int households = 1)
        {
            Simulation sim = NewFarm(game: Game((f, j) => f == "township.json" ? j.Replace("\"petitionHouseholds\": 10", "\"petitionHouseholds\": " + households) : j),
                options: new SimOptions { DevCommandsEnabled = true });
            TreasuryTests.Era(sim, "village");
            Gift(sim, 2_000_000 * Coin);
            TreasuryTests.Family(sim);
            sim.DrainEvents();
            return sim;
        }

        private static void Gift(Simulation sim, long amount)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
        }

        // AC-3: the petition needs 10 households; before the Village it is locked; the share is a gift; one road only.
        [Test]
        public void Petition()
        {
            Simulation sim = Start(households: 10);
            Assert.That(RoadRules.Households(sim), Is.LessThan(10));
            Assert.That(sim.Execute(new PetitionRoad(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.TooFewHouseholds));

            Simulation early = NewFarm(game: Game(), options: new SimOptions { DevCommandsEnabled = true });
            Assert.That(early.Execute(new PetitionRoad(early.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.EraLocked), "the Village opens the petition");

            sim = Start();
            long cash = sim.State.Ledger.Cash, t = sim.State.Clock.SimTime;
            long price = RoadRules.Price(sim);
            Assert.That(price, Is.EqualTo(MarketRules.Indexed(sim, Share)));
            Ok(sim, new PetitionRoad(t));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - price), "the share is paid at once");
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage), Is.EqualTo(price), "a gift to the village, outside farm profit");
            Assert.That(RoadRules.State(sim).Status, Is.EqualTo(RoadStatus.Building));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.PendingEvents.OfType<RoadPetitioned>().Single().ReadyAt, Is.EqualTo(t + 4 * 86400));
            Assert.That(sim.Execute(new PetitionRoad(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
        }

        [Test]
        public void Petition_NeedsTheCash()
        {
            Simulation sim = Start();
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.LivingCosts, sim.State.Ledger.Cash - 100 * Coin), new Posting(Account.Cash, -(sim.State.Ledger.Cash - 100 * Coin)));
            Assert.That(sim.Execute(new PetitionRoad(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(RoadRules.State(sim).Status, Is.EqualTo(RoadStatus.None), "nothing started");
        }

        // AC-3: the district builds it in 4 days; the tier follows (1.50 to 1.00 a kg) and every price's cost moves by 0.50 a kg × weight × index.
        [Test]
        public void Prices()
        {
            Simulation sim = Start();
            var goods = sim.Config.Game.Market.Goods;
            int wheat = goods.ToList().FindIndex(g => g.Id == "wheat");
            Fixed before = MarketRules.TransportCost(sim, wheat), floorBefore = MarketRules.Floor(sim, wheat);
            Assert.That(before, Is.EqualTo(Fixed.Parse("1.50") * goods[wheat].WeightKg * sim.State.Market.PriceIndex));
            Ok(sim, new PetitionRoad(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400 - 1);
            Assert.That(RoadRules.IsBuilt(sim), Is.False, "not before the fourth day");
            Assert.That(MarketRules.TransportCost(sim, wheat), Is.EqualTo(before));
            sim.AdvanceTo(sim.State.Clock.SimTime + 1);
            Assert.That(RoadRules.IsBuilt(sim), Is.True);
            Assert.That(sim.PendingEvents.OfType<RoadBuilt>().Count(), Is.EqualTo(1));
            Assert.That(RoadRules.TransportPerKg(sim), Is.EqualTo(Fixed.Parse("1.00")));
            Assert.That(RoadRules.SavingPerKg(sim), Is.EqualTo(Fixed.Parse("0.50")));
            Fixed after = MarketRules.TransportCost(sim, wheat);
            Assert.That(before - after, Is.EqualTo(Fixed.Parse("0.50") * goods[wheat].WeightKg * sim.State.Market.PriceIndex), "0.50 a kg better at the farm gate");
            Assert.That(MarketRules.Floor(sim, wheat), Is.GreaterThan(floorBefore));
        }

        // AC-12: a town building or with the road built saves and loads to the same hash; the road holds through a save in its build.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start();
            SimConfig config = sim.Config;
            Ok(sim, new PetitionRoad(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86400);
            LoadResult mid = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(mid.Error, Is.EqualTo(SaveError.None), mid.Detail);
            Assert.That(mid.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Simulation loaded = Simulation.FromState(config, mid.State);
            loaded.AdvanceTo(loaded.State.Clock.SimTime + 2 * 86400);
            Assert.That(RoadRules.IsBuilt(loaded), Is.True, "the build job survived the save");
            LoadResult done = SaveCodec.Read(SaveCodec.Write(loaded.State, config, Wall0), config, 3);
            Assert.That(done.State.ComputeHash(), Is.EqualTo(loaded.State.ComputeHash()));
        }

        // A town that never petitions hashes as it did: the Township adds nothing until something starts.
        [Test]
        public void NothingStarted_AddsNothingToTheHash()
        {
            Simulation sim = Start();
            ulong with = sim.State.ComputeHash();
            sim.State.Township = null;
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(with));
        }
    }
}
