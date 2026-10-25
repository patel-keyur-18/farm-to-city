using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014 §3.4, §3.13: the family house.</summary>
    public class HouseTests
    {
        private static void Hamlet(Simulation sim)
        {
            if (sim.State.Era == null) return;
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
        }

        private static long Houses(Simulation sim) { return sim.State.Land.Buildings.Count(b => HouseRules.IsHouse(sim, b)); }

        // AC-2: from the Hamlet, on an owned acre with nothing growing: 60,000, 48 h, 2 an acre (ADR 0067); the acre can't be sown while one stands;
        // a free place in the labour quarters takes one too; refusals name what's wrong.
        [Test]
        public void Build_Limits()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildFamilyHouse(t, HomeTile)).Reason, Is.EqualTo(RejectReason.EraLocked), "Homestead");
            Hamlet(sim);
            int unowned = sim.State.Land.Tiles.First(x => x.Terrain == Definitions.Terrain.Farm && !sim.State.Land.Parcels[x.ParcelId].Owned).Id;
            Assert.That(sim.Execute(new BuildFamilyHouse(t, unowned)).Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(sim.Execute(new BuildFamilyHouse(t, YardTile)).Reason, Is.EqualTo(RejectReason.TerrainNotAllowed));
            Ok(sim, new Sow(t, HomeTile + 1, "radish", Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new BuildFamilyHouse(t, HomeTile + 1)).Reason, Is.EqualTo(RejectReason.TileOccupied), "something is growing here");

            long cash = sim.State.Ledger.Cash, buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new BuildFamilyHouse(t, HomeTile));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(60_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(60_000_00), "capitalised");
            Assert.That(sim.Execute(new Sow(t, HomeTile, "radish", Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TileOccupied), "no farming under a house");
            Ok(sim, new BuildFamilyHouse(t, HomeTile));
            Assert.That(sim.Execute(new BuildFamilyHouse(t, HomeTile)).Reason, Is.EqualTo(RejectReason.NoRoom), "2 an acre (ADR 0067)");

            sim.DrainEvents();
            sim.AdvanceTo(t + 48 * Hour - 1);
            Assert.That(Houses(sim), Is.EqualTo(0));
            sim.AdvanceTo(t + 48 * Hour);
            Assert.That(Houses(sim), Is.EqualTo(2));
            Assert.That(sim.PendingEvents.OfType<FamilyHouseBuilt>().Count(), Is.EqualTo(2));
            Assert.That(sim.State.Citizens.Constructions, Is.Empty);
            Assert.That(sim.Execute(new Sow(sim.State.Clock.SimTime, HomeTile, "radish", Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TileOccupied));

            // The labour quarters: a house takes one of its 2 places, so one hut fits beside it.
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, QuartersTile));
            Ok(sim, new BuildHandHut(sim.State.Clock.SimTime, QuartersTile));
            Assert.That(sim.Execute(new BuildHandHut(sim.State.Clock.SimTime, QuartersTile)).Reason, Is.EqualTo(RejectReason.NoRoom));
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, QuartersTile)).Reason, Is.EqualTo(RejectReason.NoRoom));

            CashTo(sim, 0);
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, HomeTile + 2)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        private static Building BuiltHouse(Simulation sim, int tile)
        {
            Hamlet(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, tile));
            sim.AdvanceTo(t + 48 * Hour);
            return sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b));
        }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        // AC-3 (rent): a household in a family house pays 6,000 at each close (Cr 4200); one that can't falls behind (RentMissed),
        // and its security need shows it.
        [Test]
        public void Rent()
        {
            Simulation sim = Start();
            Building house = BuiltHouse(sim, HomeTile);
            Household working = AddFamily(sim, house.Id, 32, 30);
            HouseholdRules.SetBudget(sim, working);
            Assert.That(working.Rent, Is.EqualTo(6_000_00));
            Household poor = AddFamily(sim, BuiltHouse(sim, HomeTile).Id, 70, 68); // elders: no income, no savings
            HouseholdRules.SetBudget(sim, poor);
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.PendingEvents.OfType<RentPaid>().Single().HouseholdId, Is.EqualTo(working.Id));
            Assert.That(sim.State.Ledger.Balance(Account.RentReceived), Is.EqualTo(-6_000_00), "4200 Rent received, in the new season");
            RentMissed missed = sim.PendingEvents.OfType<RentMissed>().Single();
            Assert.That((missed.HouseholdId, missed.Behind), Is.EqualTo((poor.Id, 1)));
            Assert.That(poor.RentBehind, Is.EqualTo(1));
            Assert.That(poor.Security, Is.EqualTo((Maths.Fixed.One + Maths.Fixed.Parse("0.5")).MulRatio(1, 3)), "rent behind: nothing for that need");
            Assert.That(working.RentBehind, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // §3.13: an acre with a house can't be sold; foreclosure takes a house on bought land at book value toward the debt and its
        // household leaves; inherited acres are never seized.
        [Test]
        public void NoSale_Foreclosure()
        {
            Simulation sim = CreditKit.NewSim(cash: 1_000_000, freeBank: false);
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", CreditKit.Coins(60_000), 8))).Accepted, Is.True);
            Assert.That(CreditKit.Take(sim, "moneylender", 20_000, 1).Accepted, Is.True);
            Hamlet(sim);
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, 82));
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, HomeTile));
            sim.AdvanceTo(sim.State.Clock.SimTime + 48 * Hour); // both at once: no family moves in before ours (US-110)
            Building bought = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b) && b.TileId == 82);
            Building inherited = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b) && b.TileId == HomeTile);
            Household tenants = AddFamily(sim, bought.Id, 35, 33, 8);
            Household safe = AddFamily(sim, inherited.Id, 40);
            Assert.That(sim.Execute(new SellParcel(sim.State.Clock.SimTime, "w1")).Reason, Is.EqualTo(RejectReason.ParcelInUse), "no family is made homeless by a tap");

            for (int i = 0; i < 4 && !sim.State.Credit.PendingForeclosure; i++)
            {
                CreditKit.Burn(sim);
                sim.AdvanceTo(NextClose(sim)); // unpaid closes: default
            }
            Assert.That(sim.State.Credit.PendingForeclosure, Is.True);
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            var events = sim.DrainEvents();
            Assert.That(events.OfType<ParcelSeized>().Single().ParcelId, Is.EqualTo("w1"));
            HouseholdLeft left = events.OfType<HouseholdLeft>().Single();
            Assert.That((left.HouseholdId, left.Reason, left.People), Is.EqualTo((tenants.Id, LeaveReason.LandTaken, 3)));
            Assert.That(sim.State.Land.Buildings.Any(b => b.Id == bought.Id), Is.False, "went with the land");
            Assert.That(sim.State.Land.Buildings.Any(b => b.Id == inherited.Id), Is.True, "inherited land is never seized");
            Assert.That(sim.State.Citizens.Households, Does.Contain(safe));
            Assert.That(sim.State.Citizens.Households, Does.Not.Contain(tenants));
            Assert.That(sim.State.Land.Tiles[82].Occupied, Is.False);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    
        // ADR 0071: the home loan. The bank counts family houses at houseLtv 0.75 of their book value (and the one being built), lends at
        // most the house's cost, the down payment is the rest; the loan is an ordinary bank loan; the command log keeps its financing (written under its own tag).
        [Test]
        public void HomeLoan()
        {
            Simulation sim = CreditKit.NewSim(cash: 30_000);
            Hamlet(sim);
            int bank = sim.Config.Game.Credit.IndexOfLender("coop_bank");
            long cost = Market.MarketRules.Indexed(sim, 60_000_00);
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, HomeTile)).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "30,000 isn't 60,000");
            CreditLimit with = CreditRules.Limit(sim, bank, 0, 0, cost), without = CreditRules.Limit(sim, bank);
            Assert.That(with.Houses, Is.EqualTo(cost));
            Assert.That(with.RawLimit - without.RawLimit, Is.EqualTo(Money.MulRate(cost, Maths.Fixed.Parse("0.75"))), "0.75 of the house");
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, HomeTile, new Financing("coop_bank", cost + Money.Coin, 8))).Reason, Is.EqualTo(RejectReason.InvalidAmount), "never more than it costs");

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, HomeTile, new Financing("coop_bank", 40_000_00, 8)));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(cost - 40_000_00), "the down payment");
            Loan loan = sim.State.Credit.Loans.Single(l => l.IsOpen && l.Lender == bank);
            Assert.That(loan.Principal, Is.EqualTo(40_000_00));
            Assert.That(CreditRules.Limit(sim, bank).Houses, Is.EqualTo(cost), "the house being built counts");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            Saves.LoadResult r = SaveTests.RoundTrip(sim);
            Assert.That(r.State.CommandLog.Entries.Select(e => e.Command).OfType<BuildFamilyHouse>().Single().Financing.Amount, Is.EqualTo(40_000_00));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
