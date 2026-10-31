using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Money and loans' *Your season* (spec 019 §3.9, AC-11 to AC-15, ADR 0083 §7–12).</summary>
    public class MoneyViewTests
    {
        private const long Hour = 3600;

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static long NextClose(Simulation sim) { return (T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        /// <summary>Some of the store's radish to the trader and some to the mandi, a moneylender loan, then the hand hut's town a few hours on.</summary>
        private static Simulation Busy()
        {
            Simulation sim = Situations.Town();
            Ok(sim, new TakeLoan(T(sim), "moneylender", 20_000 * 100, 4));
            int radish = Good(sim, "radish");
            sim.State.Market.Stock[radish] += 10_000; // a harvest in the store (the store's own rules are in the market tests)
            Ok(sim, new SellToTrader(T(sim), "radish", 1000));
            Ok(sim, new GoToMandi(T(sim), new[] { new MandiLoad("radish", 2000) }));
            sim.AdvanceTo(T(sim) + 6 * Hour);
            return sim;
        }

        private static Simulation Closed()
        {
            Simulation sim = Busy();
            int reports = sim.State.Finance.Reports.Count;
            sim.AdvanceTo(NextClose(sim) + 3 * Hour, AdvanceMode.Away);
            Assert.That(sim.State.Finance.Reports.Count, Is.EqualTo(reports + 1));
            Loan loan = sim.State.Credit.Loans.Single();
            Ok(sim, new RepayLoan(T(sim), loan.Id, 5_000 * 100)); // some principal back this season
            return sim;
        }

        private static long Moved(SeasonVm s) { return s.Moved.Sum(l => l.Amount); }

        // AC-11: Money and loans opens on Your season; Your wealth holds net worth, land, businesses, owed, the loans and borrowing.
        [Test]
        public void OpensOnSeason()
        {
            Simulation sim = Busy();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm money = MoneySheet.Build(ctx);
            Assert.That(money.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "tab:season", "tab:wealth" }));
            Assert.That(money.Tab, Is.EqualTo("tab:season"));
            Assert.That(money.Tabs.Select(t => t.Label), Is.EqualTo(new[] { "Your season", "Your wealth" }));
            Assert.That(money.Switch.Select(a => a.Id), Is.EqualTo(new[] { "switch:this", "switch:last" }), "a season has closed");
            Assert.That(money.Lines.Where(l => l.IsHeader).Select(l => l.Label.Split(" · ")[0]),
                Is.EqualTo(new[] { "Earned", "Spent", "Profit this season", "Why your cash moved differently", "Next season, already due" }));
            Assert.That(money.Lines.Any(l => l.WhyKey == "netWorth" || l.WhyKey == "creditLimit"), Is.False, "land values out of the way");
            Assert.That(money.Lines.Where(l => !l.IsHeader && l.Value != null && l.Value != "").All(l => WhyView.Has(l.WhyKey)), Is.True, "every number has its Why?");

            SheetVm wealth = MoneySheet.Build(ctx, MoneySheet.Wealth);
            Assert.That(wealth.Tab, Is.EqualTo("tab:wealth"));
            Assert.That(wealth.Lines.Select(l => l.Label), Is.SupersetOf(new[] { "Cash", "What you own minus what you owe", "Of that, your land", "Your loans", "Borrow" }));
            Loan loan = sim.State.Credit.Loans.Single();
            Assert.That(wealth.Lines.Any(l => l.Action != null && l.Action.Id == "repay:" + loan.Id), Is.True);
            Assert.That(wealth.Lines.Count(l => l.WhyKey == "creditLimit"), Is.EqualTo(sim.Config.Game.Credit.Lenders.Count));
        }

        // AC-12: earned − spent is the report's what's left at the close; cash at the start + profit + every why-your-cash-moved line = cash now,
        // to the coin, every hour through a close; bought things and loan principal are never under Spent.
        [Test]
        public void Season_Reconciles()
        {
            Simulation sim = Busy();
            long close = NextClose(sim);
            for (long t = close - 4 * Hour; t <= close + 4 * Hour; t += Hour)
            {
                sim.AdvanceTo(t);
                ViewContext ctx = ViewKit2.Context(sim);
                SeasonVm now = SeasonBook.Build(ctx, false);
                Assert.That(now.CashStart + now.Profit + Moved(now), Is.EqualTo(sim.State.Ledger.Cash), "this season at " + t);
                Assert.That(now.Profit, Is.EqualTo(sim.State.Ledger.NetIncome), "as the books count it at " + t);
                if (!SeasonBook.HasLast(sim)) continue;
                SeasonVm last = SeasonBook.Build(ctx, true);
                SeasonReport r = sim.State.Finance.Reports.Last();
                Assert.That(last.Profit, Is.EqualTo(r.NetResult), "the report's what's left");
                Assert.That(last.FarmProfit, Is.EqualTo(r.FarmProfit), "the farm's own profit inside it");
                Assert.That((last.CashStart, last.CashEnd), Is.EqualTo((r.CashFlow.Opening, r.CashFlow.Closing)));
                Assert.That(last.CashStart + last.Profit + Moved(last), Is.EqualTo(last.CashEnd), "last season at " + t);
            }

            // Last season borrowed; this one paid some back and built a house: neither is a cost.
            Ok(sim, new RepayLoan(T(sim), sim.State.Credit.Loans.Single().Id, 5_000 * 100));
            Ok(sim, new BuildFamilyHouse(T(sim), Situations.HouseTile(sim)));
            ViewContext c = ViewKit2.Context(sim);
            SeasonVm s = SeasonBook.Build(c, false), before = SeasonBook.Build(c, true);
            Assert.That(before.Moved.Single(l => l.Key == "borrowed").Amount, Is.EqualTo(20_000 * 100));
            Assert.That(s.Moved.Single(l => l.Key == "repaid").Amount, Is.LessThan(0));
            Assert.That(s.Moved.Single(l => l.Key == "boughtBuildings").Amount, Is.LessThan(0));
            Assert.That(s.Spent.Select(l => l.Key), Has.None.AnyOf("Buildings", "BankLoans", "MoneylenderLoans", "repaid", "boughtBuildings"));
            Assert.That(s.CashStart + s.Profit + Moved(s), Is.EqualTo(sim.State.Ledger.Cash));
            Assert.That(WhyView.Build(c, "season", MoneySheet.WhyId(false, MoneySheet.MovedBlock, s.Moved.FindIndex(l => l.Key == "boughtBuildings"))).Rows.Last().Label,
                Does.StartWith("You still own it: it's in Your wealth, not a cost"));
            // Land entering the books with no cash and no profit (the cattle acre a migrated town inherits) adds no cancelling lines.
            int lines = s.Moved.Count;
            sim.State.Ledger.Post(T(sim), sim.Period, "ledger.cattleAcreInherited", "parcel:home_d",
                new Posting(Account.Land, 85_932 * 100), new Posting(Account.OpeningEquity, -85_932 * 100));
            Assert.That(SeasonBook.Build(ViewKit2.Context(sim), false).Moved, Has.Count.EqualTo(lines), "nothing to explain");
            WhyVm cash = WhyView.Build(c, "season", MoneySheet.WhyId(false, MoneySheet.Totals, MoneySheet.CashLine));
            Assert.That(cash.Rows.Select(r => r.Label), Is.SupersetOf(new[] { "At the season's start", "Profit this season", "Paid back on loans: not a cost", "Cash now" }));
        }

        // AC-13: each good sold has its own line, summing the sales record, with where it sold in its Why?; orders their own line; every
        // crop and fruit sale on the books is in the record (no "Other sales").
        [Test]
        public void Season_ByGood()
        {
            Simulation sim = Busy();
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonVm s = SeasonBook.Build(ctx, false);
            List<Sale> record = sim.State.Finance.Sales.This;
            SeasonLineVm radish = s.Earned.Single(l => l.Key == "radish");
            Assert.That(radish.Label, Is.EqualTo("Radish"));
            Assert.That(radish.Amount, Is.EqualTo(record.Where(x => x.Good == "radish" && x.To != SoldTo.Orders).Sum(x => x.Amount)));
            Assert.That(radish.Why.Select(r => r.Label), Is.SupersetOf(new[] { "To the trader", "At the mandi" }));
            Assert.That(record.Single(x => x.Good == "radish" && x.To == SoldTo.Mandi).Amount, Is.GreaterThan(0), "the mandi's gross, before its commission");
            Assert.That(s.Spent.Single(l => l.Key == "TransportAndSelling").Amount, Is.GreaterThan(0), "the commission is a selling cost");
            Assert.That(s.Earned.Any(l => l.Key == "otherSales"), Is.False, "every sale on the books is in the record");
            Ledger books = sim.State.Ledger;
            Assert.That(record.Sum(x => x.Amount) + sim.State.Farm.Founder.HomeUseSeason, Is.EqualTo(-(books.Balance(Account.CropSales) + books.Balance(Account.FruitSales))));
            WhyVm why = WhyView.Build(ctx, "season", MoneySheet.WhyId(false, MoneySheet.EarnedBlock, s.Earned.IndexOf(radish)));
            Assert.That(why.Rows.First(r => r.Label == "To the trader").Value, Is.EqualTo(ctx.Fmt.Coins(record.Single(x => x.Good == "radish" && x.To == SoldTo.Trader).Amount)));

            // A supermarket delivery is its own line, never the good's (the hand fills the orders first, else we deliver one).
            sim.State.Guidance.Skipped = true; // orders start after the first session
            bool Delivered() { return sim.State.Finance.Sales.This.Any(x => x.To == SoldTo.Orders); }
            for (int i = 0; i < 30 && !Delivered(); i++)
            {
                SupermarketOrder o = sim.State.Market.SupermarketOrders.FirstOrDefault(x => x.Left > 0);
                if (o != null)
                {
                    sim.State.Market.Stock[o.Good] += 5_000; // a fresh harvest (the store's own rule is in OrderTests)
                    sim.State.Market.FreshToday[o.Good] += 5_000;
                    Ok(sim, new DeliverOrder(T(sim), o.Id, System.Math.Min(5_000, o.Left)));
                }
                else sim.AdvanceTo(T(sim) + Hour);
            }
            Assert.That(Delivered(), Is.True, "a supermarket order delivered");
            s = SeasonBook.Build(ViewKit2.Context(sim), false);
            SeasonLineVm orders = s.Earned.Single(l => l.Key == "orders");
            Assert.That(orders.Amount, Is.EqualTo(sim.State.Finance.Sales.This.Where(x => x.To == SoldTo.Orders).Sum(x => x.Amount)));
            Assert.That(orders.Why.Select(r => r.Label), Does.Contain(SeasonBook.GoodName(ViewKit2.Context(sim), sim.State.Finance.Sales.This.First(x => x.To == SoldTo.Orders).Good)));
            var goods = sim.State.Finance.Sales.This.Select(x => x.Good).ToList();
            Assert.That(s.Earned.Where(l => goods.Contains(l.Key)).Sum(l => l.Amount) + orders.Amount, Is.EqualTo(sim.State.Finance.Sales.This.Sum(x => x.Amount)),
                "the goods' lines and orders sum the record");
        }

        // AC-14: next season, already due, is what the close takes when nothing else changes (each hand's renewal less the house, loan
        // payments as interest and principal, living); the cover line warns when cash is short and names who'd go unpaid.
        [Test]
        public void NextSeasonDue()
        {
            Simulation sim = Busy();
            long close = NextClose(sim);
            sim.AdvanceTo(close - 1);
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonVm s = SeasonBook.Build(ctx, false);
            Hand hand = sim.State.People.Hands.Single();
            Loan loan = sim.State.Credit.Loans.Single();
            sim.DrainEvents();
            sim.AdvanceTo(close);
            List<SimEvent> events = sim.DrainEvents().ToList();
            LoanRepayment paid = events.OfType<LoanRepayment>().Single(e => e.LoanId == loan.Id);
            SeasonLineVm due = s.Due.Single(l => l.Key == "loan:" + loan.Id);
            Assert.That(due.Amount, Is.EqualTo(paid.Interest + paid.Arrears + paid.Principal).Within(1), "the loan's payment");
            // A part season first: interest only, and the row says so (a bullet loan's principal comes at its last close).
            Assert.That(paid.Principal, Is.EqualTo(0));
            Assert.That((due.Label, due.Why.Single().Label), Is.EqualTo(("Village moneylender: interest", "Interest: a cost")));
            Assert.That(s.Due.Single(l => l.Key == "hand:" + hand.Id).Amount,
                Is.EqualTo(events.OfType<ObligationPaid>().Single(e => e.Kind == "Wages" && e.OwnerRef == "hand:" + hand.Id).Amount));
            long living = events.OfType<ObligationPaid>().Single(e => e.Kind == "Living").Amount;
            Assert.That(s.Due.Single(l => l.Key == "living").Amount, Is.EqualTo(living).Within(living / 50), "at today's prices: the close's index may add a little");
            Assert.That(s.DueTotal, Is.EqualTo(s.Due.Sum(l => l.Amount)));

            // Cash just short of the hand's renewal: the cover line is amber, names him, and opens the Store.
            Simulation poor = Busy();
            ViewContext pc = ViewKit2.Context(poor);
            SeasonVm p = SeasonBook.Build(pc, false);
            Hand h = poor.State.People.Hands.Single();
            long keep = p.DueTotal - 8_400 * 100;
            poor.State.Ledger.Post(T(poor), poor.Period, "test", "test", new Posting(Account.Cash, keep - poor.State.Ledger.Cash), new Posting(Account.OpeningEquity, poor.State.Ledger.Cash - keep));
            p = SeasonBook.Build(pc, false);
            Assert.That(p.Short, Is.EqualTo(8_400 * 100));
            Assert.That(p.ShortWho, Is.EqualTo(PeopleWords.Name(pc, h.Name) + "'s renewal would lapse"));
            LineVm cover = MoneySheet.Build(pc).Lines.Single(l => l.Label == "Due at the season's end");
            Assert.That(cover.NoteTone, Is.EqualTo(Tone.Warn));
            Assert.That(cover.Note, Is.EqualTo("Short by " + pc.Fmt.Coins(8_400 * 100) + ": " + p.ShortWho + ". Sell from the store, or borrow on Your wealth"));
            Assert.That(cover.Action.Opens.Value.Kind, Is.EqualTo("Store"));
            Assert.That(MoneySheet.Build(ctx).Lines.Single(l => l.Label == "Due at the season's end").NoteTone, Is.EqualTo(Tone.Good));
        }

        // Spec 014 §3.6 in AC-14: a townsperson's renewal is the wage less the house, from the first renewal.
        [Test]
        public void NextSeasonDue_LessTheHouse()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen manoj = sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == "Manoj");
            Ok(sim, new OfferTownsperson(T(sim), manoj.Id, OfferLevel.Asked));
            Hand h = sim.State.People.Hands.Single(x => x.Id == manoj.HandId);
            SeasonLineVm renewal = SeasonBook.Build(ViewKit2.Context(sim), false).Due.Single(l => l.Key == "hand:" + h.Id);
            long rent = MigrationRules.HousingInKind(sim, h);
            Assert.That(rent, Is.GreaterThan(0));
            Assert.That(renewal.Amount, Is.EqualTo(h.Wage - rent));
            Assert.That(renewal.Why.Select(r => r.Label), Is.EqualTo(new[] { "Wage", "The house, part of the pay" }));
        }

        // AC-15: Last season shows the finished season (its report's figures) without next season due, ending with the season report;
        // before the first close only This season shows.
        [Test]
        public void LastSeason()
        {
            Simulation fresh = Situations.Rich();
            ViewContext fc = ViewKit2.Context(fresh);
            Assert.That(SeasonBook.HasLast(fresh), Is.False);
            Assert.That(MoneySheet.Build(fc, MoneySheet.LastSeason).Switch, Is.Null);
            Assert.That(SeasonBook.Build(fc, true).Last, Is.False, "only This season before the first close");

            Simulation sim = Closed();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm now = MoneySheet.Build(ctx);
            Assert.That(now.Switch.Select(a => a.Id), Is.EqualTo(new[] { "switch:this", "switch:last" }));
            Assert.That(now.SwitchOn, Is.EqualTo("switch:this"));
            SheetVm last = MoneySheet.Build(ctx, MoneySheet.LastSeason);
            SeasonReport r = sim.State.Finance.Reports.Last();
            Assert.That(last.SwitchOn, Is.EqualTo("switch:last"));
            Assert.That(last.Subtitle, Is.EqualTo(ReportSheet.PeriodName(ctx, r.Period)));
            Assert.That(last.Lines.Any(l => l.IsHeader && l.Label.StartsWith("Next season")), Is.False);
            Assert.That(last.Lines.Last().Action.Opens.Value, Is.EqualTo(new SheetRequest("Report", r.Period)));
            SeasonVm s = SeasonBook.Build(ctx, true);
            Assert.That(s.Due, Is.Empty);
            Assert.That(s.Profit, Is.EqualTo(r.NetResult));
            // Its earned and spent lines are the report's accounts: crops and fruit (by good, orders, home use) and the rest one by one.
            long income = r.ByAccount.Where(kv => Accounts.GroupOf(kv.Key) == AccountGroup.Income).Sum(kv => kv.Value);
            long costs = -r.ByAccount.Where(kv => Accounts.GroupOf(kv.Key) == AccountGroup.Expenses).Sum(kv => kv.Value);
            Assert.That((s.EarnedTotal, s.SpentTotal), Is.EqualTo((income, costs)));
            Assert.That(s.Earned.Single(l => l.Key == "radish").Amount, Is.EqualTo(sim.State.Finance.Sales.Last.Where(x => x.Good == "radish" && x.To != SoldTo.Orders).Sum(x => x.Amount)));
            Assert.That(last.Lines.Single(l => l.WhyKey == "season" && l.WhyId == MoneySheet.WhyId(true, MoneySheet.Totals, MoneySheet.ProfitLine)).Value, Is.EqualTo(MoneySheet.Signed(ctx, r.NetResult)));
        }
    }
}
