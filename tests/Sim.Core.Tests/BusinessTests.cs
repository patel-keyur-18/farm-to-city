using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 016: founding a business and its books, the mill's hour, running it yourself.</summary>
    public class BusinessTests
    {
        internal static void Hamlet(Simulation sim)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
        }

        internal static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        /// <summary>A rich Hamlet town with a flour mill founded on <see cref="PeopleKit.HomeTile"/> (capital 1,50,000), not yet built.</summary>
        internal static Business.Business Mill(Simulation sim, bool runYourself = false, long capital = 150_000_00)
        {
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "flour_mill", HomeTile, capital, runYourself));
            return sim.State.Businesses.Businesses.Last();
        }

        internal static void GiveWheat(Simulation sim, long grams)
        {
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            sim.State.Market.Stock[wheat] += grams;
        }

        // AC-1: founding moves the founder's cash into the business (1800 / Owner's capital); the business buys and capitalises its
        // building and equipment; refusals in order; both ledgers reconcile.
        [Test]
        public void Founding_Books()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.EraLocked), "the Hamlet's");
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            Assert.That(sim.Execute(new FoundBusiness(t, "bakery", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.EraLocked), "the Village's");
            Assert.That(sim.Execute(new FoundBusiness(t, "sweet_shop", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", QuartersTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.TerrainNotAllowed));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 50_000_00, false)).Reason, Is.EqualTo(RejectReason.CapitalTooLow), "the building, equipment and a season's wage");
            Ok(sim, new Sow(t, HomeTile + 1, "radish", Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile + 1, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.TileOccupied));

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            long cost = MarketRules.Indexed(sim, 100_000_00);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(150_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(150_000_00));
            Assert.That(b.Ledger.Balance(Account.OwnersCapital), Is.EqualTo(-150_000_00));
            Assert.That(b.Ledger.Balance(Account.Buildings) + b.Ledger.Balance(Account.Equipment), Is.EqualTo(cost), "capitalised");
            Assert.That(b.Ledger.Cash, Is.EqualTo(150_000_00 - cost));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
            Assert.That(sim.Execute(new Sow(t, HomeTile, "radish", Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TileOccupied), "the acre stops farming");
            Assert.That(sim.Execute(new BuildFamilyHouse(t, HomeTile)).Reason, Is.EqualTo(RejectReason.TileOccupied), "no house on a business's acre");
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.NoRoom), "one business an acre");
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Building));
            sim.AdvanceTo(b.ReadyAt);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.Neighbour), "nobody in town: a daily worker from a neighbouring village");
            Assert.That(b.Ledger.Balance(Account.HiredLabour), Is.EqualTo(PeopleRules.RestOfSeason(sim, MarketRules.Indexed(sim, 27_000_00))).Within(100), "the rest of the season's wage, upfront");
        }

        // AC-3: the founder's wheat goes to the mill at today's market price: Crop sales on the farm, Stock on the mill.
        [Test]
        public void InternalSale()
        {
            Simulation sim = Start();
            Business.Business b = Mill(sim);
            GiveWheat(sim, 3_000_000); // the household eats from the store too
            sim.AdvanceTo(b.ReadyAt);
            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400 + 8 * 3600);
            long sales = sim.State.Ledger.Balance(Account.CropSales), cash = sim.State.Ledger.Cash;
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            Fixed price = MarketRules.Floor(sim, wheat);
            sim.AdvanceTo(day * 86_400 + 18 * 3600);
            long value = MarketRules.Value(sim, wheat, 200_000, price);
            Assert.That(b.OwnToday, Is.EqualTo(200_000), "200 kg a day by default");
            Assert.That(sales - sim.State.Ledger.Balance(Account.CropSales), Is.EqualTo(value).Within(value / 50), "Crop sales on the farm (prices move a little in a day)");
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(value).Within(value / 50));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }

        // AC-11: run by you, the mill works only in your hours there, which come off your farm work; no wage is paid.
        [Test]
        public void RunItYourself()
        {
            Simulation sim = Start();
            Fixed before = Farm.FounderRules.LabourRate(sim);
            Business.Business b = Mill(sim, runYourself: true, capital: 120_000_00);
            sim.AdvanceTo(b.ReadyAt);
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.Founder));
            Fixed hours = sim.State.Farm.Founder.TodayHours;
            Fixed cut = (hours < Fixed.FromInt(10) ? hours : Fixed.FromInt(10)) * Farm.FounderRules.Nutrition(sim);
            Assert.That(before - Farm.FounderRules.LabourRate(sim), Is.EqualTo(cut), "a 10-hour day at the mill takes the founder's day");
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 12, b.Fee, b.OwnPerDay, false));
            Fixed four = Fixed.FromInt(4) * Farm.FounderRules.Nutrition(sim);
            Assert.That(before - Farm.FounderRules.LabourRate(sim), Is.EqualTo(four < cut ? four : cut), "4 hours open, 4 hours off the farm");
            sim.AdvanceTo(NextClose(sim) + 3600);
            Assert.That(b.Ledger.Balance(Account.HiredLabour) + b.Reports.Last().Balance(Account.HiredLabour), Is.EqualTo(0), "no wage");
            Assert.That(b.Reports.Last().Balance(Account.ServiceIncome), Is.LessThan(0), "it ground for the area");
        }

        // AC-4: each business closes its season with the founder's: repairs, depreciation, its own P&L, cash flow and balance sheet, profit to
        // its retained earnings; the next season's wage paid upfront at the close. Run by you, a full season makes a profit.
        [Test]
        public void SeasonClose()
        {
            Simulation sim = Start();
            Business.Business b = Mill(sim, runYourself: true, capital: 120_000_00);
            GiveWheat(sim, 12_000_000);
            sim.AdvanceTo(NextClose(sim));
            Assert.That(b.Reports.Count, Is.EqualTo(1), "the founding season closes too");
            sim.AdvanceTo(NextClose(sim));
            BusinessReport r = b.Reports.Last();
            Assert.That(r.Period, Is.EqualTo(sim.Period - 1));
            Assert.That(BusinessBooks.NetProfit(r), Is.GreaterThan(0), "a profitable season, run by you");
            Assert.That(r.Balance(Account.Depreciation), Is.EqualTo(MarketRules.Indexed(sim, 40_000_00) / 80 + MarketRules.Indexed(sim, 60_000_00) / 40).Within(2));
            Assert.That(r.Balance(Account.Repairs), Is.GreaterThan(0));
            Assert.That(r.OwnGrams, Is.EqualTo(200_000L * sim.Clock.DaysPerSeason), "200 kg of your wheat a day");
            Assert.That(r.Closing, Is.EqualTo(r.Balance(Account.Cash)), "start + in − out = the cash at the close");
            Assert.That(r.Opening, Is.EqualTo(b.Reports[0].Closing), "a season starts with the last one's cash");
            Assert.That(b.Ledger.NetIncome, Is.EqualTo(0), "income and costs closed to retained earnings");
            Assert.That(-b.Ledger.Balance(Account.RetainedEarnings), Is.EqualTo(b.Reports.Sum(x => BusinessBooks.NetProfit(x))));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));

            Simulation hired = Start();
            Business.Business h = Mill(hired);
            hired.AdvanceTo(NextClose(hired));
            Assert.That(h.Ledger.Balance(Account.HiredLabour), Is.EqualTo(MarketRules.Indexed(hired, 27_000_00)), "the new season's wage, paid upfront at the close");
            Assert.That(h.Reports.Last().Balance(Account.HiredLabour), Is.GreaterThan(0), "the first season's wage in its own P&L");
        }

        // AC-5 and §3.2: net worth shows a business at its equity (never below 0) in place of 1800 at cost; cash in and out moves it between
        // your books and the business's without changing your net worth; drawings past your capital are income (4300).
        [Test]
        public void NetWorth()
        {
            Simulation sim = Start();
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            long t = sim.State.Clock.SimTime, before = Reporting.NetWorth(sim).Total;
            Ok(sim, new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            Assert.That(Reporting.NetWorth(sim).Businesses, Is.EqualTo(150_000_00), "its equity: cash and the building at cost");
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(before), "founding moves your money, it doesn't make or lose any");

            Assert.That(sim.Execute(new PutCash(t, 99, 10_000_00)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new PutCash(t, b.Id, 0)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            Assert.That(sim.Execute(new TakeCash(t, b.Id, b.Ledger.Cash + 1)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Ok(sim, new PutCash(t, b.Id, 10_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(160_000_00));
            Ok(sim, new TakeCash(t, b.Id, 30_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(130_000_00), "your capital back first");
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(before));

            // A business with more cash than your capital in it (profits): drawings past the capital are your income.
            b.Ledger.Post(t, sim.Period, "ledger.sales", null, new Posting(Account.Cash, 200_000_00), new Posting(Account.Sales, -200_000_00));
            Ok(sim, new TakeCash(t, b.Id, 140_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Balance(Account.BusinessDrawingsIncome), Is.EqualTo(-10_000_00));

            // Owing more than it owns: counted at 0, never below.
            b.Ledger.Post(t, sim.Period, "ledger.repairs", null, new Posting(Account.Repairs, 400_000_00), new Posting(Account.Overdraft, -400_000_00));
            Assert.That(BusinessBooks.Equity(b), Is.LessThan(0));
            Assert.That(Reporting.NetWorth(sim).Businesses, Is.EqualTo(0));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }

        // §3.8: a business loan is the business's: up to 60% of its building and equipment, its entries on the business's books, paid
        // from its cash at each close; your guarantee shows on your credit (the bank's room), not in your own books.
        [Test]
        public void BusinessLoan()
        {
            Simulation sim = Start();
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            long t = sim.State.Clock.SimTime;
            var mill = sim.Config.Game.Businesses.Types[0];
            long max = BusinessBooks.MaxLoan(sim, mill), need = BusinessRules.Projected(sim, mill, false, mill.OwnPerDay, mill.Fee).CashNeeded;
            Assert.That(max, Is.EqualTo(MarketRules.Indexed(sim, 100_000_00) * 6 / 10 / 1000_00 * 1000_00), "60% of the building and equipment");
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, need - max, false, new Financing("coop_bank", max + 1000_00, 12))).Reason, Is.EqualTo(RejectReason.OverLimit));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, need - max, false, new Financing("coop_bank", max, 13))).Reason, Is.EqualTo(RejectReason.InvalidTerm));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, need - max, false, new Financing("moneylender", max, 4))).Reason, Is.EqualTo(RejectReason.UnknownLender));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, need - max - 100_00, false, new Financing("coop_bank", max, 12))).Reason, Is.EqualTo(RejectReason.CapitalTooLow));
            long bankBefore = sim.State.Ledger.Balance(Account.BankLoans), worth = Reporting.NetWorth(sim).Total;
            Ok(sim, new FoundBusiness(t, "flour_mill", HomeTile, need - max, false, new Financing("coop_bank", max, 12)));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            Loan loan = BusinessBooks.LoanOf(sim, b);
            Assert.That((loan.BusinessId, loan.Principal, loan.Term), Is.EqualTo((b.Id, max, 12)));
            Assert.That(b.Ledger.Balance(Account.BankLoans), Is.EqualTo(-max), "on the business's books");
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(bankBefore), "not on yours");
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(worth), "your net worth: the business at its equity (the loan inside it)");
            Assert.That(CreditRules.Owed(sim, loan.Lender), Is.EqualTo(max), "your guarantee uses the bank's room");

            sim.AdvanceTo(NextClose(sim) + 1);
            sim.AdvanceTo(NextClose(sim) + 1); // a full season's instalment
            Assert.That(loan.Principal, Is.EqualTo(max - (12 - loan.Remaining) * (max / 12)), "equal-principal instalments, paid by the business");
            Assert.That(loan.Remaining, Is.LessThan(12));
            Assert.That(b.Reports.Last().Balance(Account.Interest), Is.GreaterThan(0), "its interest in its own P&L");
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }

        // AC-6: a short business uses its overdraft to the limit (half a season's running costs, 4% a season on what's drawn); past it,
        // inputs stop, and at the close the unpaid worker leaves; cash put in pays the overdraft off and a worker is taken on again.
        [Test]
        public void Overdraft_Trouble()
        {
            Simulation sim = Start();
            Business.Business b = Mill(sim);
            sim.AdvanceTo(b.ReadyAt);
            var mill = BusinessRules.TypeOf(sim, b);
            BusinessRules.Projection p = BusinessRules.Projected(sim, mill, false, 0, Fixed.Zero);
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 18, Fixed.Zero, 0, false)); // grinding for nothing: only power goes out
            Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, b.Ledger.Cash));
            long limit = BusinessBooks.OverdraftLimit(sim, b);
            Assert.That(limit, Is.EqualTo((p.Costs - p.Depreciation) / 2 / 100_00 * 100_00).Within(100_00), "half a season's running costs");
            Assert.That(BusinessBooks.Short(sim, b), Is.False);

            sim.AdvanceTo(System.Math.Min(NextClose(sim) - 3600, sim.State.Clock.SimTime + 3 * 86_400));
            Assert.That(BusinessBooks.Overdrawn(b), Is.GreaterThan(0).And.LessThanOrEqualTo(limit), "drawn, never past the limit");

            // Drive it to the limit, then the close.
            while (!BusinessBooks.Short(sim, b) && sim.State.Clock.SimTime < NextClose(sim) - 3600) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            long drawn = BusinessBooks.Overdrawn(b);
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim) + 1);
            var events = sim.DrainEvents();
            Assert.That(b.Reports.Last().Balance(Account.Interest), Is.EqualTo(Money.MulRate(drawn, Fixed.Parse("0.04"))).Within(Money.MulRate(limit, Fixed.Parse("0.04"))), "4% a season on what's drawn");
            Assert.That(events.OfType<WagesMissed>().Count(), Is.EqualTo(1), "the wage couldn't be paid");
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.None), "the worker left");
            Assert.That(BusinessBooks.Short(sim, b), Is.True);

            CashTo(sim, 500_000_00);
            Ok(sim, new PutCash(sim.State.Clock.SimTime, b.Id, 100_000_00));
            Assert.That(BusinessBooks.Overdrawn(b), Is.EqualTo(0), "cash put in pays the overdraft off first");
            Assert.That(b.Worker, Is.Not.EqualTo(WorkerKind.None), "a worker again, paid upfront");
            Assert.That(BusinessBooks.Short(sim, b), Is.False);
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-7: closing sells stock and equipment (60% of book value), repays the business's debts, and moves what's still owed to you
        // as your own bank loan over 4 seasons; what's left comes to you, your capital not returned is a loss; foreclosure closes a
        // business whose own loan has defaulted, and one on bought land.
        [Test]
        public void Closing_Guarantee()
        {
            Simulation sim = Start();
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            var mill = sim.Config.Game.Businesses.Types[0];
            long max = BusinessBooks.MaxLoan(sim, mill), need = BusinessRules.Projected(sim, mill, false, mill.OwnPerDay, mill.Fee).CashNeeded;
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "flour_mill", HomeTile, need - max, false, new Financing("coop_bank", max, 12)));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            sim.AdvanceTo(b.ReadyAt + 86_400);
            Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, b.Ledger.Cash));
            long book = b.Ledger.Balance(Account.Buildings) + b.Ledger.Balance(Account.Equipment) + b.Ledger.Balance(Account.AccumulatedDepreciation);
            sim.DrainEvents();
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            BusinessClosed closed = sim.DrainEvents().OfType<BusinessClosed>().Single();
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed));
            Assert.That(sim.State.Land.Tiles[HomeTile].Occupied, Is.False, "the acre is free");
            Assert.That(BusinessBooks.LoanOf(sim, b).State, Is.EqualTo(LoanState.Repaid), "its loan repaid");
            Assert.That(b.Ledger.Balance(Account.LossOnAssetSales), Is.EqualTo(book - Money.RoundToCoin(Money.MulRate(book, Fixed.Parse("0.6")))).Within(100), "40% of book lost");
            Assert.That(closed.Guaranteed, Is.GreaterThan(0), "60% of the equipment didn't cover the loan");
            Loan yours = sim.State.Credit.Loans.Last();
            Assert.That((yours.BusinessId, yours.Principal, yours.Term), Is.EqualTo((0L, closed.Guaranteed, 4)), "what's still owed is your loan, over 4 seasons");
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(0));
            Assert.That(sim.Execute(new CloseBusiness(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.BusinessClosed));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));

            // A mill without debts: its cash and 60% of its book come back to you.
            Simulation clean = Start();
            Business.Business m = Mill(clean);
            clean.AdvanceTo(m.ReadyAt);
            long cash = clean.State.Ledger.Cash, inMill = m.Ledger.Cash;
            long mBook = m.Ledger.Balance(Account.Buildings) + m.Ledger.Balance(Account.Equipment);
            Ok(clean, new CloseBusiness(clean.State.Clock.SimTime, m.Id));
            Assert.That(clean.State.Ledger.Cash - cash, Is.EqualTo(inMill + Money.RoundToCoin(Money.MulRate(mBook, Fixed.Parse("0.6")))));
            Assert.That(clean.State.Ledger.Balance(Account.LossOnAssetSales), Is.GreaterThan(0), "your capital not returned");

            // Foreclosure closes a business whose loan has defaulted, on inherited land too; your guarantee joins your debts.
            Simulation fore = Start();
            Hamlet(fore);
            CashTo(fore, 500_000_00);
            Ok(fore, new FoundBusiness(fore.State.Clock.SimTime, "flour_mill", HomeTile, need - max, false, new Financing("coop_bank", max, 12)));
            Business.Business f = fore.State.Businesses.Businesses.Last();
            BusinessBooks.LoanOf(fore, f).State = LoanState.Defaulted;
            CreditSystem.Foreclose(fore);
            Assert.That(f.Status, Is.EqualTo(BusinessStatus.Closed));
            Assert.That(fore.State.Credit.Loans.All(l => !l.IsOpen), Is.True, "every loan settled or written off");
            Assert.That((fore.State.Ledger.Reconcile(), f.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }

        // AC-9: the same seed and commands give the same town with a business, in one chunk or many, live or away.
        [Test]
        public void ChunkingInvariance()
        {
            ulong Run(System.Action<Simulation, long> advance)
            {
                Simulation sim = Start(seed: 5);
                Business.Business b = Mill(sim);
                GiveWheat(sim, 2_000_000);
                long end = sim.State.Clock.SimTime + 6 * 86_400;
                advance(sim, end);
                Assert.That(b.Ledger.Balance(Account.Sales), Is.LessThan(0), "it sold atta");
                return sim.State.ComputeHash();
            }
            ulong one = Run((sim, end) => sim.AdvanceTo(end));
            ulong many = Run((sim, end) => { while (sim.State.Clock.SimTime < end) sim.AdvanceTo(System.Math.Min(end, sim.State.Clock.SimTime + 7_777)); });
            ulong away = Run((sim, end) => { long d = end - sim.State.Clock.SimTime; sim.AdvanceAway(d, d, false); });
            Assert.That(many, Is.EqualTo(one));
            Assert.That(away, Is.EqualTo(one));

            Simulation s = CreditKit.NewSim(cash: 500_000);
            Hamlet(s);
            Ok(s, new FoundBusiness(s.State.Clock.SimTime, "flour_mill", HomeTile, 150_000_00, false));
            s.AdvanceTo(s.State.Clock.SimTime + 3 * 86_400);
            LoadResult r = SaveTests.RoundTrip(s);
            Assert.That(r.Error, Is.EqualTo(SaveError.None));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(s.State.ComputeHash()), "a business round-trips");
        }
    }

    /// <summary>Spec 016 §3.4, §3.5: the mill's rate, order of work and yields.</summary>
    public class ProductionTests
    {
        // AC-2: open 8–18 it grinds at most 380 kg a real hour, the area's grain first (catchment + town × the grain a person), then your
        // wheat up to the daily amount; 0.97 atta and 0.03 bran; grain not ground in the day is lost; bran goes to the feed room first.
        [Test]
        public void Mill_Rate_Order_Yield()
        {
            Simulation sim = Start();
            Business.Business b = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 5_000_000);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(System.Math.Max(b.ReadyAt, sim.State.Clock.SimTime + 25 * 3600));
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, "buffalo"));
            int feed = sim.Config.Game.Livestock.Feed, atta = sim.Config.Game.Market.IndexOf("atta"), bran = sim.Config.Game.Market.IndexOf("bran");
            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400);
            sim.DrainEvents();
            sim.AdvanceTo(day * 86_400 + 7 * 3600); // after the animals' 6:00 feed
            long feedBefore = sim.State.Market.Stock[feed];
            sim.AdvanceTo(day * 86_400 + 19 * 3600);
            long attaSold = sim.State.Market.TraderBoughtToday[atta], branSold = sim.State.Market.TraderBoughtToday[bran], toFeed = sim.State.Market.Stock[feed] - feedBefore;
            sim.AdvanceTo((day + 1) * 86_400);
            var worked = sim.DrainEvents().OfType<BusinessWorked>().ToList();
            long area = BusinessRules.AreaGramsPerDay(sim);
            Assert.That(worked.Select(w => (w.SimTime % 86_400) / 3600 - 1), Is.EqualTo(Enumerable.Range(8, 10)), "the hours 8:00–18:00 only");
            Assert.That(worked.All(w => w.AreaGrams + w.OwnGrams <= 380_000), Is.True, "380 kg a real hour at most");
            Assert.That(worked.Sum(w => w.AreaGrams), Is.EqualTo(area / 10 * 10), "the area's grain first");
            Assert.That(worked.Sum(w => w.OwnGrams), Is.EqualTo(200_000), "then 200 kg of your wheat");
            Assert.That(worked.First().OwnGrams, Is.EqualTo(System.Math.Min(200_000, 380_000 - area / 10)), "your wheat fills what the area leaves");
            Assert.That(b.Stock[atta] + attaSold, Is.EqualTo(194_000).Within(10), "0.97 atta (rounded an hour at a time)");
            Assert.That(toFeed, Is.GreaterThan(0), "bran to the feed room first");
            Assert.That(b.Stock[bran] + branSold + toFeed, Is.EqualTo(6_000).Within(10), "0.03 bran");

            // Open only 8–12: the area's afternoon grain goes to the next village.
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 12, b.Fee, b.OwnPerDay, false));
            sim.AdvanceTo((day + 2) * 86_400);
            Assert.That(b.AreaToday, Is.EqualTo(area / 10 * 4), "4 hours of it");

            // A fee more than 1 coin above the next village's: the area goes there.
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 18, Fixed.FromInt(6), b.OwnPerDay, false));
            sim.AdvanceTo((day + 3) * 86_400);
            Assert.That(b.AreaToday, Is.EqualTo(0));
            Assert.That(b.OwnToday, Is.EqualTo(200_000), "your own wheat still");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-12: the bakery turns 10 kg atta (+ 1.5 kg of supplies, bought as 150 coins) into 13 kg of pav, and 12 kg of pav into 10 kg
        // of rusk, at most 95 kg of inputs (about 124 kg of bread) a real hour; at 6:00 the village shops buy each up to their amount a
        // person for the area's people and your town's; then the trader takes rusk; with no mill of yours the plan prices atta at the
        // market's import price, with one at the trader's.
        [Test]
        public void Bakery_Recipe_Buyers()
        {
            Simulation sim = Start();
            int atta = sim.Config.Game.Market.IndexOf("atta"), pav = sim.Config.Game.Market.IndexOf("pav"), rusk = sim.Config.Game.Market.IndexOf("rusk");
            Assert.That(BusinessRules.InputPrice(sim, atta), Is.EqualTo(MarketRules.Ceiling(sim, atta)), "no mill: atta at the market's import price");
            Business.Business b = RecipeTests.Found(sim, "bakery");
            var t = BusinessRules.TypeOf(sim, b);
            Assert.That(BusinessRules.AreaPeople(sim), Is.EqualTo(BusinessRules.Defs(sim).CatchmentPeople + sim.State.Citizens.Population), "the area's people and your town's");
            long shopPav = BusinessRules.ShopGramsPerDay(sim, t, pav), shopRusk = BusinessRules.ShopGramsPerDay(sim, t, rusk);
            long trader = sim.Config.Game.Market.Region.TraderBuyPerDay(rusk);
            Assert.That(trader, Is.GreaterThan(0), "rusk keeps, so the trader takes it");
            Ok(sim, new SetMix(sim.State.Clock.SimTime, b.Id, "rusk", shopRusk + trader));

            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400 + 7 * 3600);
            sim.DrainEvents();
            long supplies0 = SuppliesPaid(b);
            sim.AdvanceTo(day * 86_400 + 19 * 3600);
            var worked = sim.DrainEvents().OfType<BusinessWorked>().Where(w => w.BusinessId == b.Id).ToList();
            Assert.That(worked.All(w => w.OwnGrams <= 95_000), Is.True, "95 kg of inputs a real hour at most");
            long ruskMade = b.MadeToday[rusk], pavIntoRusk = ruskMade * 12 / 10, pavMade = b.MadeToday[pav] + pavIntoRusk;
            Assert.That(ruskMade, Is.EqualTo(shopRusk + trader));
            Assert.That(b.MadeToday[pav], Is.EqualTo(shopPav));
            long attaUsed = worked.Sum(w => w.OwnGrams) - pavIntoRusk;
            Assert.That(attaUsed, Is.EqualTo(pavMade * 10 / 13).Within(worked.Count * 2), "10 kg of atta to 13 kg of pav (rounded up a batch at a time)");
            Assert.That(SuppliesPaid(b) - supplies0, Is.EqualTo(Finance.Money.Value(pavMade, Fixed.FromInt(150) * sim.State.Market.PriceIndex, 13_000)).Within(worked.Count * 2),
                "1.5 kg of supplies (150 coins) a 13 kg batch");
            Assert.That(worked.Max(w => w.OwnGrams) * 13 / 10, Is.LessThanOrEqualTo(124_000), "about 124 kg of bread a real hour at most");

            // The round at 6:00: the shops first, then the trader's truck takes the rusk left.
            sim.AdvanceTo((day + 1) * 86_400 + 6 * 3600 + 1);
            Assert.That(sim.State.Market.TraderBoughtToday[rusk], Is.EqualTo(trader), "the trader takes the rusk the shops don't");
            Assert.That(sim.State.Market.TraderBoughtToday[pav], Is.Zero, "a crop trader doesn't buy fresh bread");
            Assert.That(b.Stock[pav] + b.Stock[rusk], Is.Zero, "all of it sold");
            Assert.That(b.Ledger.Reconcile(), Is.Null);

            // With your mill open, its atta comes at the trader's price, and the plan counts it so.
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "flour_mill", HomeTile + 1, 150_000_00, false));
            Assert.That(BusinessRules.InputPrice(sim, atta), Is.EqualTo(MarketRules.Floor(sim, atta)), "your mill's atta at the trader's price");
        }

        // AC-13: the dairy keeps your milk at 6:00 up to its milk a day at the co-op's price (your farm's milk sales either way; the rest
        // to the co-op), buffalo milk to paneer first and cow milk to curd first, by the yields, at most 190 L a real hour; with
        // neighbours' milk allowed it fills the day's amount at the co-op's price and the premium.
        [Test]
        public void Dairy_MilkIn_Recipes()
        {
            Simulation sim = Start();
            RecipeTests.Village(sim);
            CashTo(sim, 1_000_000_00);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 25 * 3600);
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, "buffalo"));
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, "cow"));
            Business.Business b = RecipeTests.Found(sim, "dairy", HomeTile + 1);
            var t = BusinessRules.TypeOf(sim, b);
            int buf = sim.Config.Game.Market.IndexOf("milk_buffalo"), cow = sim.Config.Game.Market.IndexOf("milk_cow");
            int paneer = sim.Config.Game.Market.IndexOf("paneer"), curd = sim.Config.Game.Market.IndexOf("curd"), ghee = sim.Config.Game.Market.IndexOf("ghee");
            Assert.That(b.OwnPerDay, Is.EqualTo(BusinessRules.MilkFor(t, b.Mix)), "it starts keeping what its mix needs");
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, 150_000, false)); // your milk only, 150 L

            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400 + 5 * 3600);
            long fromFarm0 = Paid(b, "ledger.boughtFromFarm");
            sim.AdvanceTo(day * 86_400 + 6 * 3600 + 1);
            long keptBuf = b.Stock[buf], keptCow = b.Stock[cow], herd = sim.State.Livestock.LastMilkMl - sim.State.Livestock.LastHomeMl;
            Assert.That(keptBuf + keptCow, Is.EqualTo(System.Math.Min(150_000, herd)), "your milk up to its milk a day");
            Assert.That(keptBuf, Is.GreaterThan(0), "buffalo milk first");
            long price = Finance.Money.Value(keptBuf, Livestock.LivestockRules.MilkPrice(sim, 0), 1000) + Finance.Money.Value(keptCow, Livestock.LivestockRules.MilkPrice(sim, 1), 1000);
            Assert.That(Paid(b, "ledger.boughtFromFarm") - fromFarm0, Is.EqualTo(price).Within(1), "at the co-op's price");

            sim.DrainEvents();
            sim.AdvanceTo(day * 86_400 + 19 * 3600);
            var worked = sim.DrainEvents().OfType<BusinessWorked>().Where(w => w.BusinessId == b.Id).ToList();
            Assert.That(worked.All(w => w.OwnGrams <= 190_000), Is.True, "190 L a real hour at most");
            // Buffalo milk to paneer, cow milk to curd, buffalo milk left to ghee (15 L a kilo).
            long paneerKg = System.Math.Min(b.Mix[paneer], keptBuf * 2 / 10);
            Assert.That(b.MadeToday[paneer], Is.EqualTo(paneerKg).Within(100), "5 L of buffalo milk a kilo of paneer, first");
            Assert.That(b.MadeToday[curd], Is.EqualTo(System.Math.Min(b.Mix[curd], keptCow)).Within(100), "a litre of cow milk a kilo of curd");
            Assert.That(b.MadeToday[ghee], Is.EqualTo(System.Math.Min(b.Mix[ghee], (keptBuf - paneerKg * 5) / 15)).Within(100), "the buffalo milk left to ghee");

            // Neighbours' milk fills the day's amount after yours, at the co-op's price and the premium.
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, b.OpenHour, b.CloseHour, b.Fee, 600_000, true));
            long fromNeighbours0 = Paid(b, "ledger.boughtFromNeighbours");
            sim.AdvanceTo((day + 1) * 86_400 + 6 * 3600 + 1);
            long own = b.Stock[buf] + b.Stock[cow];
            Assert.That(own, Is.EqualTo(600_000), "filled to its milk a day");
            long bought = Paid(b, "ledger.boughtFromNeighbours") - fromNeighbours0;
            Assert.That(bought, Is.GreaterThan(0));
            Assert.That(BusinessRules.NeighbourMilkPrice(sim, t), Is.EqualTo(Livestock.LivestockRules.MilkPrice(sim, 0) + Fixed.FromInt(2) * sim.State.Market.PriceIndex));
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // §3.9b: with no herd at all (no shed), the dairy still buys its milk a day from neighbours at 6:00 and makes paneer and curd.
        [Test]
        public void Dairy_NoHerd_NeighboursMilk()
        {
            Simulation sim = Start();
            Business.Business b = RecipeTests.Found(sim, "dairy", HomeTile + 1);
            Assert.That(sim.State.Livestock.Animals, Is.Empty);
            int buf = sim.Config.Game.Market.IndexOf("milk_buffalo"), paneer = sim.Config.Game.Market.IndexOf("paneer");
            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400 + 6 * 3600 + 1);
            Assert.That(b.Stock[buf], Is.EqualTo(b.OwnPerDay), "neighbours' buffalo milk, the whole day's amount");
            sim.AdvanceTo(day * 86_400 + 19 * 3600);
            Assert.That(b.MadeToday[paneer], Is.GreaterThan(0));
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        private static long Paid(Business.Business b, string key)
        {
            return b.Ledger.Entries.Where(e => e.DescriptionKey == key).Sum(e => e.Postings.Where(p => p.Account == Account.BusinessStock).Sum(p => p.Amount));
        }

        private static long SuppliesPaid(Business.Business b)
        {
            return b.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.supplies").Sum(e => e.Postings.Where(p => p.Account == Account.BusinessStock).Sum(p => p.Amount));
        }
    }
}
