using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 017 (US-120, ADR 0091): customer choice between the shops, stock-outs, the mill against the chakki, the pitches.</summary>
    public class ShopTests
    {
        /// <summary>A rich Village town with a shop of <paramref name="type"/> on <paramref name="pitch"/> (or your acre), staffed, built and a day on.</summary>
        internal static Business.Business Shop(Simulation sim, string type, int pitch = 0, int tile = HomeTile, bool rich = true)
        {
            RecipeTests.Village(sim);
            if (rich) CashTo(sim, 1_000_000_00);
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, type, tile, 200_000_00, false, null, pitch));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 1);
            return b;
        }

        private static long NextDayAt(Simulation sim, int hour) { return (sim.State.Clock.SimTime / 86_400 + 1) * 86_400 + hour * 3600L; }

        private static double D(Fixed f) { return (double)f.Raw / (1L << Fixed.FractionBits); }

        private static Rival RivalOf(Simulation sim, string id) { return sim.State.Shops.Rivals.Single(r => r.Id == id); }

        // AC-1: each part of the attraction as §3.3 gives it; two equal shops share a group's demand equally; 10% below the reference
        // takes the share the formula gives; a shop shut that hour gets none.
        [Test]
        public void Choice_Shares()
        {
            Simulation sim = Start();
            Attraction a = ShopRules.Attract(sim, Fixed.FromRatio(9, 10), 2, Fixed.FromInt(5), 200, Fixed.FromInt(10));
            Assert.That(D(a.Price), Is.EqualTo(0.15).Within(0.001), "10% below: +0.15");
            Assert.That(D(a.Quality), Is.EqualTo(0.25).Within(0.001), "good: +0.25");
            Assert.That(D(a.Stars), Is.EqualTo(0.30).Within(0.001), "5 stars: +0.30");
            Assert.That(D(a.Distance), Is.EqualTo(-0.20).Within(0.001), "200 m: −0.20");
            Assert.That(D(a.Wait), Is.EqualTo(-0.20).Within(0.001), "a 10-minute queue: −0.20");
            Assert.That(D(ShopRules.Attract(sim, Fixed.FromInt(2), 1, Fixed.Zero, 3000, Fixed.Zero).Total), Is.EqualTo(0.05).Within(0.001), "never below the floor");
            Assert.That(D(ShopRules.WaitMinutes(100, 100)), Is.EqualTo(10).Within(0.001), "full: 10 minutes");
            Assert.That(ShopRules.WaitMinutes(80, 100), Is.EqualTo(Fixed.Zero), "80% full: no queue");

            Business.Business one = Shop(sim, "tea_stall", 0);
            Business.Business two = Shop(sim, "tea_stall", 1);
            sim.AdvanceTo(NextDayAt(sim, 9) + 1);
            two.Stars = one.Stars; // the first opened a day sooner and has had a night's stars (ShopTests.Stars): make them equal
            Assert.That(ShopRules.Milk(sim, one), Is.GreaterThan(0), "neighbours' milk at the 6:00 milking");
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            var hour = sim.DrainEvents().OfType<ShopSales>().ToList();
            long c1 = hour.Single(e => e.BusinessId == one.Id).Customers, c2 = hour.Single(e => e.BusinessId == two.Id).Customers;
            Assert.That(c1, Is.GreaterThan(0));
            Assert.That(System.Math.Abs(c1 - c2), Is.LessThanOrEqualTo(2), "two equal shops on the square: an equal share (the remainder to the first)");

            Ok(sim, new SetShop(sim.State.Clock.SimTime, one.Id, Fixed.FromRatio(9, 10)));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            hour = sim.DrainEvents().OfType<ShopSales>().ToList();
            c1 = hour.Single(e => e.BusinessId == one.Id).Customers;
            c2 = hour.Single(e => e.BusinessId == two.Id).Customers;
            Fixed expected = ShopRules.Attract(sim, one, ShopGroup.Village).Total / ShopRules.Attract(sim, two, ShopGroup.Village).Total;
            Assert.That((double)c1 / c2, Is.EqualTo(D(expected)).Within(0.05), "the cheaper one's share by the formula");
            Assert.That(sim.Execute(new SetShop(sim.State.Clock.SimTime, one.Id, Fixed.FromRatio(3, 4))).Reason, Is.EqualTo(RejectReason.InvalidArgument), "the presets only");

            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, two.Id, 8, 14, two.Fee, two.OwnPerDay, two.BuyFromMarket));
            sim.AdvanceTo(NextDayAt(sim, 15) - 86_400 + 1 + (sim.State.Clock.SimTime % 86_400 >= 15 * 3600 ? 86_400 : 0));
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            hour = sim.DrainEvents().OfType<ShopSales>().ToList();
            Assert.That(hour.Any(e => e.BusinessId == two.Id), Is.False, "shut from 14:00: no customers");
            Assert.That(hour.Single(e => e.BusinessId == one.Id).Customers, Is.GreaterThan(c1), "they go to the shops open");
            Assert.That(one.Ledger.Reconcile(), Is.Null);
        }

        // ADR 0091 §4 (owner, 2026-11-05): a stall that opens after the 6:00 round takes its milk and snacks that hour, once, instead of
        // standing empty till the next morning.
        [Test]
        public void FirstDay()
        {
            Simulation sim = Start();
            sim.AdvanceTo(NextDayAt(sim, 1));
            Business.Business b = Shop(sim, "tea_stall", 0);
            Assert.That(b.ReadyAt % 86_400, Is.GreaterThan(6 * 3600L), "it opens after the morning round");
            int rusk = sim.Config.Game.Market.IndexOf("rusk");
            sim.AdvanceTo((b.ReadyAt / 3600 + 1) * 3600 + 1);
            Assert.That(ShopRules.Milk(sim, b), Is.GreaterThan(0), "neighbours' milk the hour it opens");
            Assert.That(b.Stock[rusk], Is.GreaterThan(0), "and its snacks");
            Assert.That(b.StockedDay, Is.EqualTo(sim.State.Clock.SimTime / 86_400));
            long milk = ShopRules.Milk(sim, b);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(b.ServedToday, Is.GreaterThan(0), "it served the same day");
            Assert.That(ShopRules.Milk(sim, b), Is.LessThan(milk), "once a day: no more bought that afternoon");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-3 (ADR 0092): each day the stars move a tenth of the way to the day's score (served share × 2 + quality + value, on a 0–5
        // scale); value falls above the reference; a shut day is forgiven once, then scores as serving nobody; the mill earns stars too.
        [Test]
        public void Stars()
        {
            Simulation sim = Start();
            Business.Business b = Shop(sim, "tea_stall", 0);
            sim.AdvanceTo(NextDayAt(sim, 22) + 1);
            Assert.That(b.OpenToday, Is.True);
            Assert.That(D(ShopRules.ServedShare(b)), Is.EqualTo((double)b.ServedToday / b.CustomersToday).Within(0.0001));
            Fixed before = b.Stars, score = ShopRules.DayScore(sim, b, ShopRules.ServedShare(b));
            Assert.That(D(ShopRules.DayScore(sim, b, Fixed.One)), Is.EqualTo(4.0 / 4.5 * 5).Within(0.001), "all served, standard, the usual price: 4 of 4.5");
            sim.AdvanceTo(NextDayAt(sim, 1) + 1);
            Assert.That(D(b.Stars), Is.EqualTo(D(before) + 0.1 * (D(score) - D(before))).Within(0.0001), "a tenth of the way");

            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, Fixed.FromRatio(12, 10)));
            Assert.That(D(ShopRules.ValuePart(sim, b)), Is.EqualTo(1.0 / 3).Within(0.001), "20% above: a third of value's point");
            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, Fixed.One, 2));
            Assert.That(D(ShopRules.DayScore(sim, b, Fixed.One)), Is.EqualTo(5).Within(0.001), "good quality, all served, the usual price: the best day");

            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id));
            before = b.Stars;
            sim.AdvanceTo(NextDayAt(sim, 1) + 1);
            Assert.That(b.ShutDays, Is.EqualTo(1));
            Assert.That(b.Stars, Is.EqualTo(before), "the first shut day is forgiven");
            Fixed shut = ShopRules.DayScore(sim, b, Fixed.Zero);
            sim.AdvanceTo(NextDayAt(sim, 1) + 1);
            Assert.That(b.ShutDays, Is.EqualTo(2));
            Assert.That(D(b.Stars), Is.EqualTo(D(before) + 0.1 * (D(shut) - D(before))).Within(0.0001), "the second scores as serving nobody");

            Business.Business mill = BusinessTests.Mill(sim);
            sim.AdvanceTo(NextDayAt(sim, 20) + 1);
            Assert.That(mill.ServedToday, Is.GreaterThan(0).And.EqualTo(mill.AreaToday), "what it ground of the area's grain");
            before = mill.Stars;
            sim.AdvanceTo(NextDayAt(sim, 1) + 1);
            Assert.That(mill.Stars, Is.GreaterThan(before), "a mill that keeps up at the chakki's fee earns stars");
        }

        // §3.5 (ADR 0092): good chai takes 0.08 L a cup (full-cream milk); the milk a day keeps the same cups; a good vegetable
        // stall keeps nothing overnight.
        [Test]
        public void GoodQuality()
        {
            Simulation sim = Start();
            Business.Business b = Shop(sim, "tea_stall", 0);
            long standardMilk = b.OwnPerDay;
            Ok(sim, new SetShop(sim.State.Clock.SimTime, b.Id, Fixed.One, 2));
            Assert.That(b.OwnPerDay, Is.EqualTo(standardMilk * 80 / 60), "the milk a day keeps the same cups");
            sim.AdvanceTo(NextDayAt(sim, 9) + 1);
            long milk = ShopRules.Milk(sim, b), cups = b.ServedToday;
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(b.ServedToday - cups, Is.GreaterThan(0));
            Assert.That(milk - ShopRules.Milk(sim, b), Is.EqualTo((b.ServedToday - cups) * 80), "0.08 L a good cup");

            Business.Business v = Shop(sim, "veg_stall", 1);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            foreach (int g in BusinessRules.TypeOf(sim, v).Goods) Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, v.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, v.Id, "potato", 1_000_000)); // more than a day's customers
            Ok(sim, new SetShop(sim.State.Clock.SimTime, v.Id, Fixed.One, 2));
            sim.State.Market.Stock[potato] += 10_000_000;
            sim.AdvanceTo(NextDayAt(sim, 21) + 1);
            Assert.That(v.Stock[potato], Is.GreaterThan(0), "left over at closing");
            sim.DrainEvents();
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            var spoiled = sim.DrainEvents().OfType<GoodsSpoiled>().Where(e => e.Where == v.Id && e.GoodId == "potato").ToList();
            Assert.That(spoiled, Is.Not.Empty, "yesterday's potatoes cleared at 6:00");
            Assert.That(v.Stock[potato], Is.EqualTo(1_000_000), "and the day's put out fresh");
            Assert.That(v.Ledger.Reconcile(), Is.Null);
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // ADR 0093 (owner, 2026-11-05): what your store can't give, the vegetable stall buys at the local market's price each morning, as a
        // sabziwala does at the mandi (on by default); with buying off it puts out only what your store has.
        [Test]
        public void Mandi()
        {
            Simulation sim = Start();
            Business.Business b = Shop(sim, "veg_stall", 0);
            Assert.That(b.BuyFromMarket, Is.True, "on by default");
            int potato = sim.Config.Game.Market.IndexOf("potato");
            foreach (int g in BusinessRules.TypeOf(sim, b).Goods) Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, "potato", 3_000));
            sim.State.Market.Stock[potato] = sim.State.Market.Offered[potato]; // nothing of yours to take
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(b.Stock[potato], Is.EqualTo(3_000), "bought at the market");
            Assert.That((double)b.StockCost[potato] / 3 / 100, Is.EqualTo(D(sim.State.Market.Price[potato])).Within(D(sim.State.Market.Price[potato]) * 0.05), "at the local market's price a kg");

            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, b.OwnPerDay, false));
            int bought = b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.boughtAtMarket");
            sim.State.Market.Stock[potato] = sim.State.Market.Offered[potato];
            sim.AdvanceTo(NextDayAt(sim, 6) + 1);
            Assert.That(b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.boughtAtMarket"), Is.EqualTo(bought), "buying off: nothing bought");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-2: a stall that runs out of a good serves no more of it that hour; the rest of its share goes to the grocer, none lost.
        [Test]
        public void StockOut()
        {
            Simulation sim = Start();
            Business.Business b = Shop(sim, "veg_stall", 0);
            int potato = sim.Config.Game.Market.IndexOf("potato");
            foreach (int g in BusinessRules.TypeOf(sim, b).Goods) Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, b.Id, "potato", 3_000));
            sim.State.Market.Stock[potato] += 2_000_000; // more than the household eats
            sim.AdvanceTo(NextDayAt(sim, 8) + 1);
            Assert.That(b.Stock[potato], Is.EqualTo(3_000), "3 kg taken from your store at 6:00");
            Assert.That(-sim.State.Ledger.Balance(Account.CropSales), Is.GreaterThan(0), "an internal sale: your farm's sales");
            long cash = b.Ledger.Cash;
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(b.Stock[potato], Is.Zero, "sold out in the first hour");
            Assert.That(b.ServedToday, Is.EqualTo(3_000));
            Assert.That(b.CustomersToday, Is.GreaterThan(b.ServedToday), "more chose it than it could serve");
            Assert.That(sim.State.Shops.LostToday[(int)ShopKind.Veg], Is.Zero, "the rest went to the grocer, who never runs out");
            Assert.That(b.Ledger.Cash, Is.GreaterThan(cash), "it was paid");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-5: the area's grain leaves your mill smoothly as its fee rises above the chakki's, not all at once.
        [Test]
        public void Mill_Choice()
        {
            Simulation sim = Start();
            Fixed Share(int fee) { return ShopRules.MillShare(sim, Fixed.FromInt(fee), 1, Fixed.FromRatio(5, 2), HomeTile); }
            Assert.That(Share(4), Is.GreaterThan(Fixed.FromRatio(85, 100)), "at the chakki's fee almost all the area grinds with you");
            Assert.That(Share(3), Is.GreaterThan(Share(4)));
            Assert.That(Share(5), Is.LessThan(Share(4)).And.GreaterThan(Fixed.FromRatio(1, 2)), "a coin above takes a share away, not all of it");
            Assert.That(Share(6), Is.LessThan(Share(5)).And.GreaterThan(Fixed.Zero));

            Business.Business mill = BusinessTests.Mill(sim);
            sim.AdvanceTo(mill.ReadyAt + 1);
            Assert.That(BusinessRules.AreaGramsThisHour(sim, mill), Is.EqualTo(ShopRules.MillShare(sim, mill).MulRatio(BusinessRules.AreaGramsPerDay(sim) / 10, 1).FloorToLong()));
            sim.AdvanceTo(NextDayAt(sim, 10) + 1);
            Assert.That(mill.AreaToday, Is.EqualTo(2 * BusinessRules.AreaGramsThisHour(sim, mill)), "8:00 and 9:00 at its share");
        }

        // AC-6: a pitch's rent is paid upfront when founded and at each close; a taken pitch is refused, a mill can't stand on one, and
        // a closed shop frees its pitch.
        [Test]
        public void Pitch()
        {
            Simulation sim = Start();
            Business.Business b = Shop(sim, "tea_stall", 2);
            long rent = Market.MarketRules.Indexed(sim, sim.Config.Game.Shops.PitchRent);
            Assert.That(b.Ledger.Balance(Account.Rent), Is.EqualTo(rent), "the first season's rent, upfront");
            Assert.That(b.TileId, Is.EqualTo(sim.Config.Game.Shops.SquareTile));
            Assert.That(sim.State.Businesses.On(b.TileId), Is.Null, "a pitch isn't an acre");
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "veg_stall", 0, 100_000_00, false, null, 2)).Reason, Is.EqualTo(RejectReason.PitchTaken));
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "flour_mill", 0, 200_000_00, false, null, 1)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 1);
            Assert.That(b.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.pitchRent" && e.Period == sim.Period), Is.EqualTo(1), "the new season's, at the close");
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "veg_stall", 0, 100_000_00, false, null, 2)).Accepted, Is.True, "free again");
        }

        // AC-7: the same seed and commands give the same shares and books in one chunk, many, or away; a shop round-trips a save.
        [Test]
        public void ChunkingInvariance()
        {
            ulong Run(System.Action<Simulation, long> advance)
            {
                Simulation sim = Start(seed: 7);
                Business.Business tea = Shop(sim, "tea_stall", 0);
                Business.Business veg = Shop(sim, "veg_stall", 1);
                sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 200_000;
                long end = sim.State.Clock.SimTime + 3 * 86_400;
                advance(sim, end);
                Assert.That(tea.SeasonServed + veg.SeasonServed, Is.GreaterThan(0), "they sold");
                return sim.State.ComputeHash();
            }
            ulong one = Run((sim, end) => sim.AdvanceTo(end));
            ulong many = Run((sim, end) => { while (sim.State.Clock.SimTime < end) sim.AdvanceTo(System.Math.Min(end, sim.State.Clock.SimTime + 7_777)); });
            ulong away = Run((sim, end) => { long d = end - sim.State.Clock.SimTime; sim.AdvanceAway(d, d, false); });
            Assert.That(many, Is.EqualTo(one));
            Assert.That(away, Is.EqualTo(one));

            Simulation s = CreditKit.NewSim(cash: 1_000_000);
            Shop(s, "sweet_shop", 0, rich: false);
            s.AdvanceTo(s.State.Clock.SimTime + 86_400);
            LoadResult r = SaveTests.RoundTrip(s);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(s.State.ComputeHash()), "a shop round-trips");
        }
    }
}
