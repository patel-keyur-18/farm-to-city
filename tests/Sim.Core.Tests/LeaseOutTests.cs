using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-261 (spec 024 §3.6, AC-9): leasing land out pays upfront at the close, locks the acre, and ends at a close.</summary>
    public class LeaseOutTests
    {
        private static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, "e1"));
            sim.DrainEvents();
            return sim;
        }

        private static Parcel P(Simulation sim, string id) { return sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)]; }

        private static void Close(Simulation sim) { sim.AdvanceTo(TreasuryTests.NextClose(sim) + Hour); }

        private static long Cash(Simulation sim) { return sim.State.Ledger.Balance(Account.Cash); }

        private static long RentPaid(Simulation sim) { return sim.DrainEvents().OfType<LandRentPaid>().Sum(e => e.Amount); }

        // Before the Township nothing leases; what isn't yours, is used, zoned or listed doesn't either; the inherited acres can.
        [Test]
        public void Refusals()
        {
            Simulation before = TownshipTests.Start(cohort: true);
            Assert.That(before.Execute(new LeaseOutLand(before.State.Clock.SimTime, "e1")).Reason, Is.EqualTo(RejectReason.NoTownship));

            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new LeaseOutLand(t, "w1")).Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(sim.Execute(new EndLease(t, "e1")).Reason, Is.EqualTo(RejectReason.NotLeased));
            Ok(sim, new ZoneParcel(t, "e1"));
            Assert.That(sim.Execute(new LeaseOutLand(t, "e1")).Reason, Is.EqualTo(RejectReason.AlreadyZoned), "take it back to farming first");
            Ok(sim, new UnzoneParcel(t, "e1"));
            Ok(sim, new ListParcel(t, "e1"));
            Assert.That(sim.Execute(new LeaseOutLand(t, "e1")).Reason, Is.EqualTo(RejectReason.Listed));
            Ok(sim, new CancelSale(t, "e1"));
            int tile = sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel("e1")].Tiles[0];
            Ok(sim, new Sow(t, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new LeaseOutLand(t, "e1")).Reason, Is.EqualTo(RejectReason.ParcelInUse), "a crop is growing");
            int inherited = sim.Config.Game.Start.InheritedParcels.First(p => LandLeaseRules.CanLease(sim, p) == RejectReason.None);
            Assert.That(sim.Execute(new LeaseOutLand(t, sim.Config.Game.Map.Parcels[inherited].Id)).Accepted, Is.True, "inherited acres can be leased");
        }

        // The lease locks the acre at once, starts at the next close, and pays upfront then and at every close after.
        [Test]
        public void PaysUpfrontAtTheClose()
        {
            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            long before = Cash(sim);
            Ok(sim, new LeaseOutLand(t, "e1"));
            Assert.That(P(sim, "e1").Leased, Is.True);
            Assert.That(Cash(sim), Is.EqualTo(before), "nothing is paid at the tap");
            sim.DrainEvents();
            Close(sim);
            long rent = LandLeaseRules.Rent(sim, P(sim, "e1"));
            Assert.That(rent, Is.EqualTo(Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, P(sim, "e1")), Fixed.Parse("0.025")))), "2.5% of the land value, to the coin");
            Assert.That(rent, Is.GreaterThan(0));
            Assert.That(LandLeaseRules.StatusOf(P(sim, "e1")), Is.EqualTo(LeaseStatus.Active));
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.landLeased" && e.Postings.Any(p => p.Account == Account.LandRentReceived && p.Amount == -rent)), Is.True);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            sim.DrainEvents();
            Close(sim);
            Assert.That(RentPaid(sim), Is.EqualTo(LandLeaseRules.Rent(sim, P(sim, "e1"))), "and again the next close");
        }

        // While leased the acre can't be farmed, zoned, sold or listed; ending is at the next close, with no rent for the season after.
        [Test]
        public void LocksAndEndsAtAClose()
        {
            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            int tile = sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel("e1")].Tiles[0];
            Ok(sim, new LeaseOutLand(t, "e1"));
            Assert.That(sim.Execute(new LeaseOutLand(t, "e1")).Reason, Is.EqualTo(RejectReason.Leased));
            Assert.That(sim.Execute(new Sow(t, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.Leased));
            Assert.That(sim.Execute(new ZoneParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.Leased));
            Assert.That(sim.Execute(new SellParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.Leased));
            Assert.That(sim.Execute(new ListParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.Leased));
            Close(sim);
            Ok(sim, new EndLease(sim.State.Clock.SimTime, "e1"));
            Assert.That(LandLeaseRules.StatusOf(P(sim, "e1")), Is.EqualTo(LeaseStatus.Ending));
            Assert.That(P(sim, "e1").Leased, Is.True, "still locked till the close");
            sim.DrainEvents();
            Close(sim);
            var events = sim.DrainEvents();
            Assert.That(events.OfType<LandRentPaid>().Count(), Is.EqualTo(0), "no rent for the season after");
            Assert.That(events.OfType<LeaseEnded>().Count(), Is.EqualTo(1));
            Assert.That(P(sim, "e1").Leased, Is.False);
            Ok(sim, new Sow(sim.State.Clock.SimTime, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none"));
        }

        // A lease that hasn't started is cancelled at once, free.
        [Test]
        public void CancelBeforeItStarts()
        {
            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            long before = Cash(sim);
            Ok(sim, new LeaseOutLand(t, "e1"));
            Ok(sim, new EndLease(t, "e1"));
            Assert.That(P(sim, "e1").Leased, Is.False);
            sim.DrainEvents();
            Close(sim);
            Assert.That(RentPaid(sim), Is.EqualTo(0));
            Assert.That(Cash(sim), Is.LessThanOrEqualTo(before + 1), "nothing came in");
        }

        // The soil stays as it was: an empty acre recovers its fertility unleased, and not while leased.
        [Test]
        public void SoilStays()
        {
            long After(bool leased)
            {
                Simulation sim = Township();
                Tile tile = sim.State.Land.Tiles[sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel("e1")].Tiles[0]];
                tile.Fertility = Fixed.FromInt(10);
                tile.VacantSince = 0;
                if (leased) Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, "e1"));
                sim.AdvanceTo(sim.State.Clock.SimTime + 12 * Day);
                return tile.Fertility.Raw;
            }
            Assert.That(After(true), Is.EqualTo(Fixed.FromInt(10).Raw), "leased: neither used up nor built up");
            Assert.That(After(false), Is.GreaterThan(Fixed.FromInt(10).Raw), "unleased: fallow recovers");
        }

        // Review fixes: a seized or sold parcel loses its zone, its lease and a waiting slow sale; a listing is refused on an acre a hand has an order on.
        [Test]
        public void ReleaseClearsEverything()
        {
            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            Ok(sim, new ZoneParcel(t, "e1"));
            Ok(sim, new ListParcel(t, "e1"));
            Parcel p = P(sim, "e1");
            long job = p.SlowSaleJobId;
            Assert.That(job, Is.Not.EqualTo(0));
            p.Lease = (int)LeaseStatus.Active;
            LandSystem.Release(sim, p);
            Assert.That((p.Zone, p.Lease, p.Listed, p.SlowSaleJobId), Is.EqualTo((0, 0, false, 0L)));
            Assert.That(sim.State.Jobs.Find(job), Is.Null, "the waiting sale is cancelled");
        }

        [Test]
        public void ListRefusedWithAnOrder()
        {
            Simulation sim = Township();
            Assert.That(sim.State.People, Is.Not.Null);
            int tile = sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel("e1")].Tiles[0];
            sim.State.People.Orders.Add(new Sim.Core.People.StandingOrder { HandId = 1, Kind = Sim.Core.People.OrderTargetKind.Field, Target = tile });
            Assert.That(sim.Execute(new ListParcel(sim.State.Clock.SimTime, "e1")).Reason, Is.EqualTo(RejectReason.ParcelInUse));
            Assert.That(sim.Execute(new LeaseOutLand(sim.State.Clock.SimTime, "e1")).Reason, Is.EqualTo(RejectReason.ParcelInUse));
        }

        // AC-12: a running lease survives a save and load; the hash matches.
        [Test]
        public void SavesRoundTrip()
        {
            Simulation sim = Township();
            Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, "e1"));
            Close(sim);
            Saves.LoadResult r = Saves.SaveCodec.Read(Saves.SaveCodec.Write(sim.State, sim.Config, 0), sim.Config);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel("e1")].Lease, Is.EqualTo((int)LeaseStatus.Active));
        }
    }
}
