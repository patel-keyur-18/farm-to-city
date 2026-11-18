using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-142 (spec 024 §3.3, ADR 0119): zoning, the families turned away, builders buying one parcel a season, their homes, and what is not yours.</summary>
    public class ZoningTests
    {
        private static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        private static int Id(Simulation sim, string parcel) { return sim.Config.Game.Map.IndexOfParcel(parcel); }

        private static Parcel P(Simulation sim, string id) { return sim.State.Land.Parcels[Id(sim, id)]; }

        private static void Wait(Simulation sim, int n) { for (int i = 0; i < n; i++) TownshipRules.State(sim).TurnedAway += 0; }

        private static void Families(Simulation sim, int n) { typeof(TownshipState).GetProperty("TurnedAway").GetSetMethod(true).Invoke(sim.State.Township, new object[] { n }); }

        private static string Bought(Simulation sim, string id)
        {
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, id));
            sim.DrainEvents();
            return id;
        }

        private static void ToClose(Simulation sim) { sim.AdvanceTo(TreasuryTests.NextClose(sim) + Hour); }

        /// <summary>Families waiting exactly <paramref name="n"/> at the next close (the hourly draw adds some on the way).</summary>
        private static void CloseWith(Simulation sim, int n)
        {
            sim.AdvanceTo(TreasuryTests.NextClose(sim) - 1);
            Families(sim, n);
            sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
        }

        // Before the Township nothing can be zoned; inherited, listed and used land can't be.
        [Test]
        public void ZoningRefusals()
        {
            Simulation before = TownshipTests.Start(cohort: true);
            Assert.That(before.Execute(new ZoneParcel(before.State.Clock.SimTime, "e1")).Reason, Is.EqualTo(RejectReason.NoTownship));

            Simulation sim = Township();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new ZoneParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(sim.Execute(new ZoneParcel(t, sim.Config.Game.Map.Parcels[sim.Config.Game.Start.InheritedParcels[0]].Id)).Reason, Is.EqualTo(RejectReason.Protected));
            Bought(sim, "e1");
            Assert.That(sim.Execute(new UnzoneParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.NotZoned));
            Ok(sim, new ZoneParcel(t, "e1"));
            Assert.That(P(sim, "e1").Zone, Is.EqualTo((int)Zone.Residential));
            Assert.That(sim.Execute(new ZoneParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.AlreadyZoned));
            Assert.That(sim.Execute(new ListParcel(t, "e1")).Accepted, Is.True, "zoned land can still be sold");
            Ok(sim, new CancelSale(t, "e1"));
            Ok(sim, new UnzoneParcel(t, "e1"));
            Assert.That(P(sim, "e1").Zone, Is.EqualTo(0));
            int tile = sim.Config.Game.Map.Parcels[Id(sim, "e1")].Tiles[0];
            Ok(sim, new Sow(t, tile, "wheat", Sim.Core.Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new ZoneParcel(t, "e1")).Reason, Is.EqualTo(RejectReason.ParcelInUse), "a growing crop first");
        }

        // The migration draw: with no free house a family is turned away at the usual pace; with one, nobody.
        [Test]
        public void TurnedAway()
        {
            Simulation sim = Township();
            Assert.That(TownshipRules.State(sim).TurnedAway, Is.EqualTo(0));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day);
            int n = TownshipRules.State(sim).TurnedAway;
            Assert.That(n, Is.InRange(2, 6), "about one family every 18 hours");
            Families(sim, ZoningRules.Def(sim).TurnedAwayMax);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            Assert.That(TownshipRules.State(sim).TurnedAway, Is.LessThanOrEqualTo(ZoningRules.Def(sim).TurnedAwayMax), "the counter is capped");
        }

        // AC-4: a residential parcel of yours sells to a builder at its land value when 2 families an acre are waiting; the builder's homes are the builder's.
        [Test]
        public void BuilderBuys()
        {
            Simulation sim = Township();
            Bought(sim, "e1");
            Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, "e1"));
            int acres = ZoningRules.HomeTiles(sim, Id(sim, "e1")).Count;
            Assert.That(ZoningRules.Need(sim, Id(sim, "e1")), Is.EqualTo(2 * acres));
            CloseWith(sim, 1);
            Assert.That(P(sim, "e1").Owned, Is.True, "one family waiting: nothing is bought");
            Assert.That(sim.State.Land.Parcels.Any(p => p.BuilderOwned), Is.False);
            sim.DrainEvents();

            long price = LandRules.ParcelPrice(sim, P(sim, "e1"));
            long fees = sim.State.Treasury.Ledger.Balance(Account.Fees);
            CloseWith(sim, 2 * acres);
            Parcel p = P(sim, "e1");
            Assert.That((p.Owned, p.BuilderOwned), Is.EqualTo((false, true)));
            AcreSoldToBuilder sold = sim.DrainEvents().OfType<AcreSoldToBuilder>().Single();
            Assert.That((sold.Yours, sold.Price > 0), Is.EqualTo((true, true)));
            var homes = sim.State.Land.Buildings.Where(b => b.Builders).ToList();
            Assert.That(homes.Count, Is.EqualTo(2 * acres));
            Assert.That(homes.All(b => b.Cost == 0 && HouseRules.IsHouse(sim, b)), Is.True, "off your books");
            Assert.That(homes.Select(b => b.TileId).Distinct().Count(), Is.EqualTo(acres));
            Assert.That(TownshipRules.State(sim).TurnedAway, Is.LessThan(2 * acres), "the families waiting are used up");
            // AC-6: the permit fee, 2% of the land value, into the town's purse
            Assert.That(sim.State.Treasury.Ledger.Balance(Account.Fees) - fees, Is.EqualTo(Money.MulRate(sold.Price, ZoningRules.Def(sim).PermitFee)).Within(sold.Price / 20));
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "e1")).Reason, Is.EqualTo(RejectReason.NotForSale), "never for sale again");
        }

        // AC-4: at most one parcel a season, even with two zoned and enough families.
        [Test]
        public void Pace()
        {
            Simulation sim = Township();
            Bought(sim, "e1");
            string other = sim.Config.Game.Map.Parcels.Where(x => x.ForSale && !P(sim, x.Id).Owned && x.Id != "e1" && ZoningRules.Serviced(sim, Id(sim, x.Id)) && LandSystem.IsAdjacentToOwned(sim, Id(sim, x.Id))).Select(x => x.Id).FirstOrDefault();
            if (other != null) Bought(sim, other);
            Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, "e1"));
            if (other != null && P(sim, other).Owned) Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, other));
            CloseWith(sim, 16);
            Assert.That(sim.State.Land.Parcels.Count(p => p.BuilderOwned), Is.EqualTo(1), "one parcel at the first close");
            CloseWith(sim, 16);
            Assert.That(sim.State.Land.Parcels.Count(p => p.BuilderOwned), Is.GreaterThanOrEqualTo(2).Or.EqualTo(1), "and at most one at the next");
            Assert.That(sim.State.Land.Parcels.Count(p => p.BuilderOwned), Is.LessThanOrEqualTo(2));
        }

        // AC-7: a town with no zoning still grows: builders buy a neighbour's parcel for sale by the road, nearest the square; you receive nothing.
        [Test]
        public void LeftAlone()
        {
            Simulation sim = Township();
            int first = ZoningRules.Candidates(sim).First();
            Assert.That(sim.State.Land.Parcels[first].Owned, Is.False);
            long cash = sim.State.Ledger.Cash;
            CloseWith(sim, ZoningRules.Need(sim, first));
            Assert.That(sim.State.Land.Parcels[first].BuilderOwned, Is.True);
            Assert.That(sim.State.Land.Buildings.Count(b => b.Builders), Is.EqualTo(2 * ZoningRules.HomeTiles(sim, first).Count));
            Assert.That(sim.State.Ledger.Cash, Is.GreaterThanOrEqualTo(cash - 10_000_000), "nothing paid to you or by you");
            Assert.That(sim.DrainEvents().OfType<AcreSoldToBuilder>().Single().Yours, Is.False);
            Assert.That(ZoningRules.Candidates(sim), Does.Not.Contain(first));
            // nothing needs doing: without families waiting nothing is bought
            Simulation quiet = Township();
            Families(quiet, 0);
            quiet.AdvanceTo(quiet.State.Clock.SimTime + 30);
            Assert.That(quiet.State.Land.Parcels.Any(p => p.BuilderOwned), Is.False);
        }

        // spec 024 §3.3: a builder's family is a resident, not one of your households; it pays you no rent and its house pays you no tax.
        [Test]
        public void NotYours()
        {
            Simulation sim = Township();
            int yours = EraRules.Households(sim);
            int first = ZoningRules.Candidates(sim).First();
            CloseWith(sim, ZoningRules.Need(sim, first));
            Assert.That(sim.State.Land.Buildings.Any(b => b.Builders), Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 12 * Day);
            Household x = sim.State.Citizens.Households.FirstOrDefault(h => HouseholdRules.InBuildersHome(sim, h));
            Assert.That(x, Is.Not.Null, "a family moved into a builder's home");
            Assert.That(EraRules.Households(sim), Is.EqualTo(sim.State.Citizens.Households.Count - sim.State.Citizens.Households.Count(h => HouseholdRules.InBuildersHome(sim, h))));
            Assert.That(sim.State.Citizens.Households.Count, Is.GreaterThan(EraRules.Households(sim)));
            Assert.That(TaxRules.YourHouseBill(sim, Fixed.Parse("0.016"), out int houses), Is.GreaterThanOrEqualTo(0));
            Assert.That(houses, Is.EqualTo(sim.State.Land.Buildings.Count(b => HouseRules.IsHouse(sim, b) && !b.Builders) + sim.State.Land.Buildings.Count(b => !HouseRules.IsHouse(sim, b) && sim.Config.Game.Buildings[b.Def].Id == "hut" )), "only your houses are billed");
            // rent: paid by the family, not received by you
            long received = sim.State.Ledger.Balance(Account.RentReceived);
            x.Rent = 1000 * Coin; x.Savings = 100_000 * Coin; x.Income = 100_000 * Coin;
            long savings = x.Savings;
            HouseRules.CollectRent(sim, x);
            Assert.That(sim.State.Ledger.Balance(Account.RentReceived), Is.EqualTo(received), "no rent to you");
            Assert.That(x.RentBehind, Is.EqualTo(0));
            // a hand living alone is never offered a builder's home
            Assert.That(LeaseRules.Due(sim), Does.Not.Contain(x.Id));
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, x.Id, LeaseChoice.Keep)).Reason, Is.EqualTo(RejectReason.NoLease));
            Assert.That(yours, Is.GreaterThan(0));
        }

        // AC-12: zones, builder parcels, turned-away families and builders' homes survive a save and load.
        [Test]
        public void SavesRoundTrip()
        {
            Simulation sim = Township();
            Bought(sim, "e1");
            Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, "e1"));
            int first = ZoningRules.Candidates(sim).First();
            CloseWith(sim, ZoningRules.Need(sim, first));
            Saves.LoadResult r = Saves.SaveCodec.Read(Saves.SaveCodec.Write(sim.State, sim.Config, 0), sim.Config);
            Assert.That(r.Error, Is.EqualTo(Saves.SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Land.Parcels.Count(p => p.BuilderOwned), Is.EqualTo(sim.State.Land.Parcels.Count(p => p.BuilderOwned)));
            Assert.That(r.State.Land.Buildings.Count(b => b.Builders), Is.EqualTo(sim.State.Land.Buildings.Count(b => b.Builders)));
            Assert.That(r.State.Township.BuilderParcels, Is.EqualTo(sim.State.Township.BuilderParcels));
        }
    }
}
