using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 013 §3.4 as amended by ADR 0087: your godown in the mandi town, rented warehouse space, and letting your godown.</summary>
    public class GranaryTests
    {
        private const long Tonne = 1_000_000; // grams

        /// <summary>Rich content with year-0 prices, on subsistence and not eating from the store, so the store's grams are exact.</summary>
        private static GameDefs Game()
        {
            return PeopleKit.Game((f, j) => f != "founder.json" ? j
                : j.Replace("\"startDietPlan\": \"basic\"", "\"startDietPlan\": \"subsistence\"").Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false"));
        }

        private static Simulation Hamlet(ulong seed = 42)
        {
            Simulation sim = Start(Game(), seed);
            sim.State.Era.Current = sim.Config.Game.Godown.UnlockEra;
            return sim;
        }

        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        private static long Season(Simulation sim) { return sim.Clock.SecondsPerSeason; }

        // AC-7: the godown takes dry goods first and spoils them at a fifth of the rate; it is bought, not built, and stands off the map.
        [Test]
        public void Capacity_Spoilage()
        {
            Simulation sim = Start(Game());
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuyGodown(t)).Reason, Is.EqualTo(RejectReason.EraLocked), "from the Hamlet");
            sim.State.Era.Current = sim.Config.Game.Godown.UnlockEra;

            int wheat = Good(sim, "wheat"), potato = Good(sim, "potato");
            Assert.That(GodownRules.Holds(sim, wheat), Is.True);
            Assert.That(GodownRules.Holds(sim, potato), Is.False, "potato needs cold storage");
            Assert.That(GodownRules.Holds(sim, Good(sim, "seed_potato")), Is.False, "and so does seed potato (ADR 0087)");
            Assert.That(GodownRules.Holds(sim, Good(sim, "seed_wheat")), Is.True);

            long hutBefore = Store.UsedGrams(sim), fitsBefore = Store.Fits(sim, wheat), potatoFits = Store.Fits(sim, potato);
            long buildings = sim.State.Ledger.Balance(Account.Buildings), cash = sim.State.Ledger.Cash;
            Ok(sim, new BuyGodown(t));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(45000 * Coin), "capitalised to 1600");
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(45000 * Coin));
            Assert.That(GodownRules.Owned(sim).TileId, Is.EqualTo(-1), "in the mandi town, off the map");
            Assert.That(sim.State.Land.Buildings.Count(b => b.Def == sim.Config.Game.Godown.Building), Is.EqualTo(1));
            Assert.That(sim.DrainEvents().OfType<GodownBought>().Single().Cost, Is.EqualTo(45000 * Coin));
            Assert.That(sim.Execute(new BuyGodown(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));

            // Room: 10,000 kg more for a dry good, none for potato; the dry goods already in store moved out of the hut.
            long dryBefore = GodownRules.UsedGrams(sim);
            Assert.That(Store.UsedGrams(sim), Is.EqualTo(hutBefore - dryBefore), "the godown's goods aren't in the hut store");
            Assert.That(Store.Fits(sim, wheat) - fitsBefore, Is.EqualTo(10 * Tonne), "10,000 kg more");
            Assert.That(Store.Fits(sim, potato) - potatoFits, Is.EqualTo(dryBefore), "potato only gains the hut room the dry goods left");

            // 12 t of wheat: the godown holds what fits after the faster-spoiling dry goods (none here: wheat spoils fastest of those
            // in store), the rest waits in the hut store.
            sim.State.Market.Stock[wheat] = 12 * Tonne;
            long[] dry = GodownRules.Dry(sim);
            Assert.That(dry[wheat], Is.EqualTo(10 * Tonne), "wheat first: the dry good that spoils fastest in store");
            Assert.That(dry.Sum(), Is.EqualTo(dry[wheat]), "the godown is full");

            // A day: 10 t × 0.005 × 0.2 = 10 kg, 2 t × 0.005 = 10 kg; 60 kg without the godown.
            sim.DrainEvents();
            sim.AdvanceTo(Day);
            Assert.That(sim.DrainEvents().OfType<GoodsSpoiled>().Single(e => e.GoodId == "wheat").Quantity, Is.EqualTo(20_000));
            Assert.That(sim.State.Market.InStore(wheat), Is.EqualTo(12 * Tonne - 20_000));

            // Taken out, the hut store empties first: the godown stays full.
            Store.Take(sim, wheat, 1 * Tonne);
            Assert.That(GodownRules.Dry(sim)[wheat], Is.EqualTo(10 * Tonne));
        }

        // AC-11: renting whole tonnes, the days left now and a season at each close, after your godown and before the hut store.
        [Test]
        public void Rent()
        {
            Simulation sim = Hamlet();
            int wheat = Good(sim, "wheat");
            long season = Season(sim);
            Assert.That(sim.Execute(new RentWarehouse(0, 21)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "1–20 t");
            Assert.That(sim.Execute(new RentWarehouse(0, -1)).Reason, Is.EqualTo(RejectReason.InvalidArgument));

            // Booked half way through the season: 5 t × 1,000 kg × 0.30 = 1,500 a season, 750 for the half left.
            sim.AdvanceTo(season / 2);
            long t = sim.State.Clock.SimTime, cash = sim.State.Ledger.Cash;
            Ok(sim, new RentWarehouse(t, 5));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(750 * Coin));
            Assert.That(sim.State.Godown.RentedTonnes, Is.EqualTo(5));
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidRent), Is.EqualTo(750 * Coin), "prepaid, expensed to 5300 through the season");

            // 3 t of wheat goes to the rented space (no godown of your own); less space can't strand it.
            sim.State.Market.Stock[wheat] = 3 * Tonne;
            Assert.That(GodownRules.InRentedGrams(sim), Is.GreaterThanOrEqualTo(3 * Tonne));
            Assert.That(sim.Execute(new RentWarehouse(t, 2)).Reason, Is.EqualTo(RejectReason.StockTooHigh));
            int needed = (int)((GodownRules.InRentedGrams(sim) + Tonne - 1) / Tonne);
            Ok(sim, new RentWarehouse(t, needed));
            Assert.That(sim.State.Godown.RentedTonnes, Is.EqualTo(5), "less space from the next close");
            Assert.That(sim.State.Godown.RentNextTonnes, Is.EqualTo(needed));

            // At the close: a full season of the tonnes booked, paid upfront.
            sim.DrainEvents();
            sim.AdvanceTo(season);
            ObligationPaid paid = sim.DrainEvents().OfType<ObligationPaid>().Single(e => e.Kind == "Warehouse");
            Assert.That(paid.Amount, Is.EqualTo(needed * 300 * Coin));
            Assert.That(sim.State.Godown.RentedTonnes, Is.EqualTo(needed));
            Prepayment p = sim.State.Finance.Prepayments.Single(x => x.OwnerRef == "warehouse" && x.Start == season);
            Assert.That((p.Total, p.End), Is.EqualTo((needed * 300 * Coin, 2 * season)));

            // Goods that grew past the space booked keep their space at the close: never below what is stored.
            sim.State.Market.Stock[wheat] += 3 * Tonne;
            Assert.That(sim.Execute(new RentWarehouse(sim.State.Clock.SimTime, 0)).Reason, Is.EqualTo(RejectReason.StockTooHigh));
            sim.State.Godown.RentNextTonnes = 0; // as if booked down before the wheat came in
            sim.AdvanceTo(2 * season);
            Assert.That(sim.State.Godown.RentedTonnes, Is.EqualTo(needed), "kept for the wheat stored there (the rest waits in the hut store), not dropped to 0");
            Assert.That(sim.State.Godown.RentedTonnes * Tonne, Is.GreaterThanOrEqualTo(GodownRules.InRentedGrams(sim)), "never below what is stored");

            // With your own godown too: it fills first, then the rented space, then the hut store.
            Ok(sim, new BuyGodown(sim.State.Clock.SimTime));
            sim.State.Market.Stock[wheat] = 12 * Tonne;
            long rented = GodownRules.RentedCapacityGrams(sim);
            Assert.That(GodownRules.InRentedGrams(sim), Is.EqualTo(System.Math.Min(rented, GodownRules.UsedGrams(sim) - 10 * Tonne)));
            Assert.That(GodownRules.UsedGrams(sim), Is.LessThanOrEqualTo(10 * Tonne + rented));
        }

        // AC-12: a godown set to let and empty is let with the seeded chance for 2,400 upfront, takes none of your goods while
        // let, isn't let while your goods need it, and renews until switched off.
        [Test]
        public void LetOut()
        {
            // A seed whose first 12 closes have both a tenant and none (the chance is 0.8, so most seeds do).
            ulong seed = 1;
            while (true)
            {
                int hits = Enumerable.Range(1, 12).Count(k => Rng.Chance(Rng.Next(seed, RngStream.Godown, 0, (ulong)k), Fixed.Parse("0.8")));
                if (hits > 0 && hits < 12) break;
                seed++;
            }
            Simulation sim = Hamlet(seed);
            long season = Season(sim);
            Assert.That(sim.Execute(new LetGodown(0, true)).Reason, Is.EqualTo(RejectReason.NoGodown));
            Ok(sim, new BuyGodown(0));
            // The start's grain and seed fit 10 t of rented space, so the godown can be let: they move there.
            Ok(sim, new RentWarehouse(0, 10));
            Ok(sim, new LetGodown(0, true));

            int lets = 0, empties = 0;
            for (int k = 1; k <= 12; k++)
            {
                sim.DrainEvents();
                long rentReceived = sim.State.Ledger.Balance(Account.RentReceived);
                sim.AdvanceTo(k * season);
                bool expected = Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Godown, 0, (ulong)sim.Period), sim.Config.Game.Godown.TenantChance);
                var events = sim.DrainEvents();
                Assert.That(sim.State.Godown.LetNow, Is.EqualTo(expected), "season " + k + ": the seeded chance");
                Assert.That(sim.State.Godown.LetNext, Is.True, "renewed each close");
                if (expected)
                {
                    lets++;
                    Assert.That(events.OfType<GodownLet>().Single().Rent, Is.EqualTo(2400 * Coin), "10,000 kg × 0.24");
                    Assert.That(GodownRules.OwnCapacityGrams(sim), Is.EqualTo(0), "none of your goods while let");
                    Assert.That(GodownRules.InRentedGrams(sim), Is.EqualTo(GodownRules.UsedGrams(sim)), "they are in the rented space");
                }
                else
                {
                    empties++;
                    Assert.That(events.OfType<GodownNotLet>().Single().Reason, Is.EqualTo(LetOutcome.NoTenant));
                    Assert.That(GodownRules.OwnCapacityGrams(sim), Is.EqualTo(10 * Tonne), "no tenant: yours to use");
                }
            }
            Assert.That((lets, empties), Is.Not.EqualTo((0, 12)).And.Not.EqualTo((12, 0)), "both outcomes happen");

            // Your goods need it at the close (more than the 10 t rented space takes): not let.
            int wheat = Good(sim, "wheat");
            sim.State.Market.Stock[wheat] = 11 * Tonne;
            sim.DrainEvents();
            sim.AdvanceTo(13 * season);
            Assert.That(sim.State.Godown.LetNow, Is.False);
            Assert.That(sim.State.Godown.LastOutcome, Is.EqualTo(LetOutcome.YourGoods));
            Assert.That(sim.DrainEvents().OfType<GodownNotLet>().Single().Reason, Is.EqualTo(LetOutcome.YourGoods));

            // Switched off: never let again.
            Ok(sim, new LetGodown(sim.State.Clock.SimTime, false));
            sim.State.Market.Stock[wheat] = 0;
            for (int k = 14; k <= 16; k++)
            {
                sim.AdvanceTo(k * season);
                Assert.That(sim.State.Godown.LetNow, Is.False);
            }
        }

        // Spec 013 §9 (ADR 0093 §9): 5 t of rice from the Monsoon harvest, sold to the trader at once, or kept in your godown and sold
        // in Spring: waiting earns more than its spoilage and the godown's cost for the two seasons (its wear, or renting 5 t instead).
        [Test]
        public void Rice_Waits_Pays()
        {
            long Sold(bool wait)
            {
                Simulation sim = Hamlet();
                int rice = Good(sim, "rice");
                sim.AdvanceTo(3 * Season(sim) - 20 * 3600); // the Monsoon's last day, 4:00
                if (wait) Ok(sim, new BuyGodown(sim.State.Clock.SimTime));
                sim.State.Market.Stock[rice] += 5 * Tonne;
                long from = wait ? 4 * Season(sim) : sim.State.Clock.SimTime, revenue = 0;
                sim.AdvanceTo(from);
                for (long t = from + 600; t < from + 6 * 86_400; t += 3600)
                {
                    sim.AdvanceTo(t);
                    long q = System.Math.Min(sim.State.Market.InStore(rice), MarketRules.TraderCapacityLeft(sim, rice));
                    if (q > 0) Ok(sim, new SellToTrader(t, "rice", q));
                    revenue += sim.DrainEvents().OfType<SoldToTrader>().Sum(e => e.Amount);
                }
                Assert.That(sim.State.Market.InStore(rice), Is.Zero, "all of it sold");
                return revenue;
            }

            long now = Sold(false), later = Sold(true);
            long wear = 2 * 45000 * Coin / 80, rent = 2 * 5 * 1000 * Fixed.Parse("0.30").MulRatio(Coin, 1).RoundToLong();
            TestContext.Out.WriteLine("sold at once " + now / Coin + ", in Spring " + later / Coin + ", the godown's wear " + wear / Coin + ", rent " + rent / Coin);
            Assert.That(later - now, Is.GreaterThan(wear), "waiting pays for your godown's two seasons");
            Assert.That(later - now, Is.GreaterThan(rent), "and for renting 5 t instead");
        }
    }
}
