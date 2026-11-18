using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-145 (spec 025 §3.2, ADR 0125): from the Township a new building takes bricks, cement and timber from the trader's day, masons, and time.</summary>
    public class ConstructionTests
    {
        private static Simulation Township(int families = 0)
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            if (families > 0) TownshipTests.Families(sim, families);
            TreasuryTests.Township(sim);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 5_000_000 * Coin), new Posting(Account.GiftsFromFounder, -5_000_000 * Coin));
            sim.DrainEvents();
            return sim;
        }

        private static int FreeTile(Simulation sim)
        {
            foreach (Tile t in sim.State.Land.Tiles) if (HouseRules.Place(sim, t.Id) == RejectReason.None) return t.Id;
            Assert.Fail("no tile for a house");
            return -1;
        }

        private static void ToRestock(Simulation sim) { sim.AdvanceTo(sim.State.Clock.SimTime + ConstructionRules.SecondsToRestock(sim) + 60); }

        // AC-5: every kind's materials plus masons are today's cost within 2% at the base prices; the price index moves both.
        [Test]
        public void Cost()
        {
            Simulation sim = Township();
            ConstructionDef d = ConstructionRules.Def(sim);
            foreach (BuildKindDef k in d.Kinds)
            {
                long materials = k.Bricks * d.BrickPrice + k.Cement * d.CementPrice + k.Timber * d.TimberPrice, masons = k.MasonDays * d.MasonDay;
                long expected = k.Work == null ? sim.Config.Game.Buildings[sim.Config.Game.Population.House].Value : sim.Config.Game.Treasury.Works.First(w => w.Id == k.Work).Cost;
                Assert.That(materials + masons, Is.EqualTo(expected).Within(expected / 50), k.Id + " at the base prices");
            }
            BuildQuote q = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            Assert.That(q.Bricks, Is.EqualTo(2400));
            Assert.That(q.Materials, Is.EqualTo(Market.MarketRules.Indexed(sim, 2400 * d.BrickPrice + 16 * d.CementPrice + 12 * d.TimberPrice)));
        }

        // The district's gang when no mason is free: the masons' part and the hours at one and a half times.
        [Test]
        public void Gang()
        {
            Simulation sim = Township();
            Assert.That(ConstructionRules.FreeMasons(sim), Is.Empty, "no jobless grown-ups: nobody is a mason yet");
            ConstructionDef d = ConstructionRules.Def(sim);
            BuildQuote q = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            Assert.That(q.Gang, Is.True);
            Assert.That(q.Hours, Is.EqualTo(72));
            Assert.That(q.MasonPay, Is.EqualTo(Money.MulRate(Market.MarketRules.Indexed(sim, 20 * d.MasonDay), d.GangPrice)));
        }

        // Masons are the same people in every game of a seed, free ones only, up to three; their hours are the base hours divided among them.
        [Test]
        public void Masons()
        {
            Simulation sim = Township(20);
            var free = ConstructionRules.FreeMasons(sim);
            Assert.That(free.Count, Is.GreaterThan(0), "20 families of grown-ups: a quarter of the jobless are masons");
            Assert.That(ConstructionRules.FreeMasons(Township(20)), Is.EqualTo(free), "the same people for the same seed");
            BuildQuote q = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            int n = System.Math.Min(3, free.Count);
            Assert.That((q.Gang, q.Masons.Count, q.Hours), Is.EqualTo((false, n, (48 + n - 1) / n)));
            Assert.That(q.MasonPay, Is.EqualTo(Market.MarketRules.Indexed(sim, 20 * ConstructionRules.Def(sim).MasonDay)), "the masons' own price, no premium");
        }

        // The house is paid at the start, takes the trader's day (2,000 of its 2,400 bricks), waits for the rest, then builds; the masons are paid into their households.
        [Test]
        public void House_Waits_Then_Builds()
        {
            Simulation sim = Township(20);
            int tile = FreeTile(sim);
            BuildQuote q = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            long cash = sim.State.Ledger.Cash, earned = sim.State.Citizens.Households.Sum(x => x.Earned);
            int houses = sim.State.Land.Buildings.Count(b => HouseRules.IsHouse(sim, b));
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, tile));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(q.Total), "paid in full at the start");
            BuildProject p = ConstructionRules.Projects(sim).Single();
            Assert.That((p.Working, p.Bricks, p.Cement, p.Timber), Is.EqualTo((false, 2000L, 16L, 12L)), "the day's 2,000 bricks, all its cement and timber");
            Assert.That(ConstructionRules.Left(sim, 0), Is.EqualTo(0));
            Assert.That(sim.DrainEvents().OfType<BuildHeld>().Single().Good, Is.EqualTo(0), "waiting for bricks");
            Assert.That(ConstructionRules.EstimateReady(sim, p), Is.EqualTo(ConstructionRules.SecondsToRestock(sim) + q.Hours * 3600L));
            ToRestock(sim);
            Assert.That((p.Working, p.Bricks), Is.EqualTo((true, 2400L)), "the rest came at the next restock");
            Assert.That(sim.State.Land.Buildings.Count(b => HouseRules.IsHouse(sim, b)), Is.EqualTo(houses), "not yet standing");
            sim.AdvanceTo(p.ReadyAt + 60);
            Assert.That(sim.State.Land.Buildings.Count(b => HouseRules.IsHouse(sim, b)), Is.EqualTo(houses + 1));
            Assert.That(ConstructionRules.Projects(sim), Is.Empty);
            Assert.That(sim.State.Citizens.Constructions, Is.Empty);
            Assert.That(sim.PendingEvents.OfType<FamilyHouseBuilt>().Count(), Is.EqualTo(1));
            if (!q.Gang) Assert.That(sim.State.Citizens.Households.Sum(x => x.Earned) - earned, Is.GreaterThanOrEqualTo(q.MasonPay), "the masons' households were paid");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Yours and the treasury's draw from one day's limit in the order they began; the masons kept are not free for another build.
        [Test]
        public void One_Limit_For_The_Town()
        {
            Simulation sim = Township(20);
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "community_hall"));
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim)));
            BuildProject hall = ConstructionRules.Projects(sim)[0], house = ConstructionRules.Projects(sim)[1];
            Assert.That((hall.Owner, hall.Bricks, house.Bricks), Is.EqualTo((BuildProject.Town, 2000L, 0L)), "the hall began first and took the day");
            Assert.That(ConstructionRules.Pending(sim, "community_hall"), Is.True);
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "community_hall")).Reason, Is.EqualTo(RejectReason.PlotTaken), "its plot is taken while it is built");
            Assert.That(ConstructionRules.Kept(sim, hall.Masons.FirstOrDefault()), Is.EqualTo(hall.Masons.Count > 0));
            ToRestock(sim);
            Assert.That((hall.Bricks, house.Bricks), Is.EqualTo((4000L, 0L)), "the hall's last 2,000 take the whole day, none left for the house");
            Assert.That(CivicRules.Built(sim, CivicDef.Hall), Is.False, "not until it is built");
            sim.AdvanceTo(hall.ReadyAt + 60);
            Assert.That(CivicRules.Built(sim, CivicDef.Hall), Is.True);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
        }

        // A treasury short of the cost refuses, and your money stays when a house can't be paid.
        [Test]
        public void Refusals()
        {
            Simulation sim = Township();
            long poor = sim.State.Ledger.Cash - 10 * Coin;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.LivingCosts, poor), new Posting(Account.Cash, -poor));
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim))).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(ConstructionRules.Projects(sim), Is.Empty);
        }

        // Before the Township a house is the old rule: time only, no project.
        [Test]
        public void Before_Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            Assert.That(ConstructionRules.On(sim), Is.False);
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim)));
            Assert.That(sim.State.Township.Projects, Is.Empty);
            Assert.That(sim.State.Citizens.Constructions.Single().ReadyAt, Is.GreaterThan(sim.State.Clock.SimTime));
        }

        // The builders' homes take materials after yours and the treasury's, and the builders skip the acres beside yours (ADR 0126 §2).
        [Test]
        public void Builders_After_And_Away_From_Yours()
        {
            Simulation sim = Township(20);
            bool beside = false; // the acres touching your land come last, only when nothing else is left (ADR 0128)
            foreach (int id in ZoningRules.Candidates(sim))
            {
                bool touches = LandSystem.IsAdjacentToOwned(sim, id) && !sim.State.Land.Parcels[id].Owned;
                Assert.That(beside && !touches, Is.False, sim.Config.Game.Map.Parcels[id].Id + " comes after an acre that touches your land");
                beside |= touches;
            }
            var q = ConstructionRules.Quote(sim, "family_house", BuildProject.Builders, 4);
            Assert.That((q.Bricks, q.Gang, q.Hours), Is.EqualTo((9600L, true, 72)), "four homes: their bricks, the gang's hours");
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim)));
            sim.State.Township.Projects.Insert(0, new BuildProject { Id = 99, Kind = "family_house", Owner = BuildProject.Builders, Tile = 0, Parcel = 0, Units = 4 });
            Assert.That(ConstructionRules.Queue(sim).Last().Owner, Is.EqualTo(BuildProject.Builders), "builders last");
        }

        // The projects, the day's stock and a pending build survive a save: same hash, and the build carries on.
        [Test]
        public void Saved()
        {
            Simulation sim = Township(20);
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim)));
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Simulation again = Simulation.FromState(config, r.State);
            ToRestock(again);
            Assert.That(again.State.Township.Projects.Single().Working, Is.True);
            Assert.That(again.State.Township.Projects.Single().Bricks, Is.EqualTo(2400));
        }
    }
}
