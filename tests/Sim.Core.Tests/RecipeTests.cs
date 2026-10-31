using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 016 §3.9c (US-118): recipes, the day's mix, the morning round, your mill's atta first.</summary>
    public class RecipeTests
    {
        internal static void Village(Simulation sim)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
        }

        /// <summary>A rich Village town with a business of <paramref name="type"/> founded on <paramref name="tile"/>, built and a day on.</summary>
        internal static Business.Business Found(Simulation sim, string type, int tile = HomeTile, long capital = 200_000_00)
        {
            Village(sim);
            CashTo(sim, 1_000_000_00);
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, type, tile, capital, false));
            BusinessTests.Staff(sim, sim.State.Businesses.Businesses.Last());
            Business.Business b = sim.State.Businesses.Businesses.Last();
            sim.AdvanceTo(b.ReadyAt + 1);
            return b;
        }

        private static long NextDayAt(Simulation sim, int hour) { return (sim.State.Clock.SimTime / 86_400 + 1) * 86_400 + hour * 3600L; }

        // US-118.T3: each hour makes toward the mix within the rate (kg of inputs a real hour), by the recipe's yields, buying atta
        // at the import price with no mill of yours; pav made into rusk is made again for the shops; at the morning round the shops buy
        // each product up to their amount (the area's and your town's people × 22.8) at the trader's price, the trader takes rusk, and
        // pav left over spoils; the day's counts start again.
        [Test]
        public void Rate_Yields_MorningRound()
        {
            Simulation sim = Start();
            Business.Business b = Found(sim, "bakery");
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            int pav = sim.Config.Game.Market.IndexOf("pav"), rusk = sim.Config.Game.Market.IndexOf("rusk"), atta = sim.Config.Game.Market.IndexOf("atta");
            long shopPav = BusinessRules.ShopGramsPerDay(sim, t, pav), shopRusk = BusinessRules.ShopGramsPerDay(sim, t, rusk);
            Assert.That(shopPav, Is.EqualTo(Round(0.01 * 22.8 * BusinessRules.AreaPeople(sim) * 1000)));
            Assert.That(b.Mix[pav], Is.EqualTo(shopPav), "the default mix is what the shops buy");
            Assert.That(b.Mix[rusk], Is.EqualTo(shopRusk + sim.Config.Game.Market.Region.TraderBuyPerDay(rusk)), "and for what keeps, what the trader takes");
            Ok(sim, new SetMix(sim.State.Clock.SimTime, b.Id, "rusk", shopRusk)); // the shops' rusk only, for the round below
            Ok(sim, new SetMix(sim.State.Clock.SimTime, b.Id, "pav", shopPav + 50_000)); // 50 kg more than the shops take

            sim.AdvanceTo(NextDayAt(sim, 0));
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * 3600);
            var worked = sim.DrainEvents().OfType<BusinessWorked>().ToList();
            Assert.That(worked.All(w => w.OwnGrams <= 95_000), Is.True, "95 kg of inputs a real hour at most");
            Assert.That(worked.Select(w => (int)((w.SimTime - 1) % 86_400 / 3600)).All(h => h >= 8 && h < 18), Is.True, "open 8:00–18:00 only");
            Assert.That(b.MadeToday[pav], Is.EqualTo(shopPav + 50_000), "pav for sale, besides what went into rusk");
            Assert.That(b.MadeToday[rusk], Is.EqualTo(shopRusk));
            Assert.That(b.Stock[pav], Is.EqualTo(shopPav + 50_000));
            Assert.That(b.Stock[rusk], Is.EqualTo(shopRusk));
            Assert.That(b.Ledger.Balance(Account.Power), Is.GreaterThan(0), "fuel");
            Assert.That(b.Stock[atta], Is.LessThan(10_000), "atta bought as needed, not hoarded");

            // The round at 6:00.
            long salesBefore = -b.Ledger.Balance(Account.Sales);
            sim.AdvanceTo(NextDayAt(sim, 6) - 86_400 + 1);
            long expected = Finance.Money.Value(shopPav, Market.MarketRules.Floor(sim, pav), 1000) + Finance.Money.Value(shopRusk, Market.MarketRules.Floor(sim, rusk), 1000);
            Assert.That(-b.Ledger.Balance(Account.Sales) - salesBefore, Is.EqualTo(expected).Within(2), "the shops at the trader's price");
            Assert.That(b.Stock[pav], Is.Zero, "the 50 kg left spoiled");
            Assert.That(sim.DrainEvents().OfType<GoodsSpoiled>().Single(e => e.GoodId == "pav").Quantity, Is.EqualTo(50_000));
            Assert.That(b.MadeToday.Sum(), Is.Zero, "a new day's counts");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // §3.9c: your mill's atta goes to your bakery first (an internal sale at the trader's price) before the trader.
        [Test]
        public void YourMillsAttaFirst()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 2_000_000);
            Business.Business bakery = Found(sim, "bakery", HomeTile + 1);
            sim.AdvanceTo(System.Math.Max(mill.ReadyAt, bakery.ReadyAt) + 1);
            sim.AdvanceTo(NextDayAt(sim, 18));
            int atta = sim.Config.Game.Market.IndexOf("atta");
            long fromMill = bakery.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.boughtFromBusiness").Sum(e => e.Postings.Where(p => p.Account == Account.BusinessStock).Sum(p => p.Amount));
            Assert.That(fromMill, Is.GreaterThan(0), "atta from your mill");
            Assert.That(mill.Ledger.Balance(Account.Sales), Is.LessThan(0));
            Assert.That(mill.Ledger.Reconcile(), Is.Null);
            Assert.That(bakery.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Market.TraderBoughtToday[atta], Is.GreaterThanOrEqualTo(0));
        }

        // Determinism: the same days in one step, in odd chunks, or away give the same town; a recipe business round-trips (schema 11).
        [Test]
        public void ChunkingInvariance_RoundTrip()
        {
            ulong Run(System.Action<Simulation, long> advance)
            {
                Simulation sim = Start(seed: 9);
                Business.Business b = Found(sim, "bakery");
                long end = sim.State.Clock.SimTime + 5 * 86_400;
                advance(sim, end);
                Assert.That(b.Ledger.Balance(Account.Sales), Is.LessThan(0), "it sold");
                return sim.State.ComputeHash();
            }
            ulong one = Run((sim, end) => sim.AdvanceTo(end));
            ulong many = Run((sim, end) => { while (sim.State.Clock.SimTime < end) sim.AdvanceTo(System.Math.Min(end, sim.State.Clock.SimTime + 5_555)); });
            ulong away = Run((sim, end) => { long d = end - sim.State.Clock.SimTime; sim.AdvanceAway(d, d, false); });
            Assert.That(many, Is.EqualTo(one));
            Assert.That(away, Is.EqualTo(one));

            Simulation s = CreditKit.NewSim(cash: 1_000_000, seed: 9); // a save holds only real commands (no test cash)
            Village(s);
            Ok(s, new FoundBusiness(s.State.Clock.SimTime, "bakery", HomeTile, 200_000_00, false));
            BusinessTests.Staff(s, s.State.Businesses.Businesses.Last());
            Business.Business bk = s.State.Businesses.Businesses.Last();
            s.AdvanceTo(bk.ReadyAt + 1);
            Ok(s, new SetMix(s.State.Clock.SimTime, bk.Id, "rusk", 12_000));
            Assert.That(s.Execute(new SetMix(s.State.Clock.SimTime, bk.Id, "atta", 1_000)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "not a product");
            s.AdvanceTo(s.State.Clock.SimTime + 2 * 86_400);
            LoadResult r = SaveTests.RoundTrip(s);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(s.State.ComputeHash()), "the mix and the day's counts round-trip");
        }

        private static long Round(double x) { return (long)System.Math.Round(x); }
    }
}
