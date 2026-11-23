using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-124 (spec 028 §3.2, ADR 0157): a manager's post and wage, one a business, what each style sets, the mis-set day, the close report and passive income.</summary>
    public class ManagerTests
    {
        private const long Day = 86_400, Hour = 3600;

        private static Simulation Township() { return InvoiceTests.Township(); }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        /// <summary>Posts the manager's job and returns the townspeople who applied, most skilled first.</summary>
        private static Applicant Post(Simulation sim)
        {
            Ok(sim, new PostJob(sim.State.Clock.SimTime, HiringRules.ManagerPlace, false));
            Applicant a = P(sim).ApplicantsFor(HiringRules.ManagerPlace).FirstOrDefault();
            Assert.That(a, Is.Not.Null, "your townspeople apply at once");
            return a;
        }

        /// <summary>Hires the first applicant for a business at 10% over the asked wage (a townsperson always takes it) in the given style (0 cost-cutter, 1 growth-chaser, 2 quality-first).</summary>
        internal static Worker Manage(Simulation sim, Business.Business b, int style = 0)
        {
            if (b.Worker == WorkerKind.Citizen || b.Worker == WorkerKind.Neighbour) Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id)); // he works it himself: its worker goes first
            if (P(sim).PostAt(HiringRules.ManagerPlace) == null) Post(sim);
            Applicant a = P(sim).ApplicantsFor(HiringRules.ManagerPlace).First();
            Ok(sim, new HireManager(sim.State.Clock.SimTime, a.Id, b.Id, OfferLevel.High));
            Worker m = P(sim).ManagerAt(b.Id);
            Assert.That(m, Is.Not.Null);
            m.Mgr.Style = style;
            return m;
        }

        // AC-5: the post is one post on the board for the town, the business chosen at the offer; a townsperson applies, the offer takes the base wage for the rest of the
        // season from the business's cash; one manager a business; nothing before the Township.
        [Test]
        public void Post_Wage()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Applicant a = Post(sim);
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, HiringRules.ManagerPlace, false)).Reason, Is.EqualTo(RejectReason.AlreadyPosted));
            long wage = HiringRules.OfferWage(sim, a, OfferLevel.Asked, b.Id);
            Assert.That(wage, Is.GreaterThan(0));
            Assert.That(sim.Execute(new HireManager(sim.State.Clock.SimTime, a.Id, 999, OfferLevel.Asked)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new HireManager(sim.State.Clock.SimTime, a.Id, b.Id, OfferLevel.Asked)).Reason, Is.EqualTo(RejectReason.AlreadyStaffed), "he works it himself: its worker goes first");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id));
            long cash = b.Ledger.Cash;
            Ok(sim, new HireManager(sim.State.Clock.SimTime, a.Id, b.Id, OfferLevel.Asked));
            Worker m = P(sim).ManagerAt(b.Id);
            Assert.That(m.Wage, Is.EqualTo(wage), "the asked wage");
            Assert.That(m.Mgr, Is.Not.Null);
            Assert.That(cash - b.Ledger.Cash, Is.EqualTo(PeopleRules.RestOfSeason(sim, wage)), "the rest of the season from the business's cash");
            Assert.That(P(sim).WorkerAt(b.Id), Is.Null, "no other worker");
            Assert.That(P(sim).StaffAt(b.Id), Is.SameAs(m), "he is the one who works it");
            Assert.That(b.Worker, Is.Not.EqualTo(WorkerKind.None));
            Assert.That(m.Skill.RoundToLong(), Is.EqualTo(a.Skill));
            Assert.That(sim.Execute(new HireManager(sim.State.Clock.SimTime, a.Id, b.Id, OfferLevel.Asked)).Accepted, Is.False, "one manager a business");
            Reconciles(sim);
        }

        [Test]
        public void One_Per_Business()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Business.Business c = InvoiceTests.Found(sim, "flour_mill", b.TileId);
            Worker first = Manage(sim, b);
            Applicant a = P(sim).ApplicantsFor(HiringRules.ManagerPlace).First();
            Assert.That(sim.Execute(new HireManager(sim.State.Clock.SimTime, a.Id, b.Id, OfferLevel.High)).Reason, Is.EqualTo(RejectReason.HasManager));
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, c.Id)); // he works it himself
            Ok(sim, new HireManager(sim.State.Clock.SimTime, a.Id, c.Id, OfferLevel.High));
            Assert.That(P(sim).ManagerAt(c.Id).Id, Is.Not.EqualTo(first.Id), "another business, another manager");
            Assert.That(sim.Execute(new RunBusinessYourself(sim.State.Clock.SimTime, b.Id)).Accepted, Is.False, "you do not run what he runs");
            Ok(sim, new DismissManager(sim.State.Clock.SimTime, b.Id));
            Assert.That(P(sim).ManagerAt(b.Id), Is.Null);
            Assert.That(sim.Execute(new DismissManager(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.NoManager));
            Reconciles(sim);
        }

        [Test]
        public void Before_The_Township_Nothing()
        {
            Simulation sim = Start();
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, HiringRules.ManagerPlace, false)).Reason, Is.EqualTo(RejectReason.NoTownship));
        }

        // AC-5: taking over a business you ran yourself frees your hours at once: he works it himself.
        [Test]
        public void Takes_Your_Place()
        {
            Simulation sim = Township();
            InvoiceTests.Found(sim, "bakery");
            Business.Business b = InvoiceTests.Found(sim, "flour_mill", sim.State.Businesses.Businesses.Last().TileId);
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, b.Id));
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.Founder));
            Manage(sim, b);
            Assert.That(b.Worker, Is.Not.EqualTo(WorkerKind.Founder), "your hours are free: he works it");
            Assert.That(b.Worker, Is.Not.EqualTo(WorkerKind.None));
            Assert.That(BusinessRules.FounderHours(sim) == Fixed.Zero || sim.State.Businesses.Businesses.All(x => x.Worker != WorkerKind.Founder), Is.True);
        }

        // AC-5: his wage at the close is a tenth of the profit before his share when it is positive, and nothing when the business lost; the share never turns a profit into a loss.
        [Test]
        public void Share_Of_Profit()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Worker m = Manage(sim, b);
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 100_000 * Coin), new Posting(Account.Sales, -100_000 * Coin));
            long before = BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period));
            Assert.That(before, Is.GreaterThan(0));
            long cash = b.Ledger.Cash;
            ManagerRules.PayShare(sim, b, sim.Period);
            long share = m.Mgr.ReportShare;
            Assert.That(share, Is.EqualTo(Finance.Money.MulRate(before, sim.Config.Game.Managers.WageProfitShare)));
            Assert.That(cash - b.Ledger.Cash, Is.EqualTo(share));
            Assert.That(BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period)), Is.EqualTo(before - share).And.GreaterThan(0), "a profit stays a profit");
            // A losing season pays no share.
            Business.Business c = InvoiceTests.Found(sim, "flour_mill", b.TileId);
            Worker m2 = Manage(sim, c);
            c.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Repairs, 50_000 * Coin), new Posting(Account.Cash, -50_000 * Coin));
            ManagerRules.PayShare(sim, c, sim.Period);
            Assert.That(m2.Mgr.ReportShare, Is.EqualTo(0));
            Reconciles(sim);
        }

        private static Business.Business Stall(Simulation sim, string type)
        {
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, type, 0, 200_000 * Coin, false, null, 0));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            return b;
        }

        private static Fixed F(double d) { return Fixed.FromRatio((long)(d * 1000), 1000); }

        // AC-6: each style sets what its kind of business has (gap 5): a shop its price, quality and the morning's stock; the mill its fee; a maker days of inputs ahead.
        [Test]
        public void Styles()
        {
            Simulation sim = Township();
            Business.Business stall = Stall(sim, "veg_stall");
            Business.Business mill = InvoiceTests.Found(sim, "flour_mill");
            Business.Business bakery = InvoiceTests.Found(sim, "bakery", mill.TileId);
            long[] one = ShopRules.DefaultPutOut(sim, BusinessRules.TypeOf(sim, stall), stall.TileId);
            Assert.That(one.Sum(), Is.GreaterThan(0));
            Worker ms = Manage(sim, stall, 1), mm = Manage(sim, mill, 1), mb = Manage(sim, bakery, 1);
            foreach (Worker w in new[] { ms, mm, mb }) w.Fairness = 0;
            InvoiceTests.ToHour(sim, 7, 1);
            Assert.That(stall.PriceFactor, Is.EqualTo(F(0.95)), "the growth-chaser undercuts the reference by 5%");
            Assert.That(stall.Quality, Is.EqualTo(1));
            for (int g = 0; g < one.Length; g++) Assert.That(stall.PutOut[g], Is.EqualTo(one[g] * 2), "two days' stock");
            Assert.That(mill.Fee, Is.EqualTo(BusinessRules.TypeOf(sim, mill).Fee * F(0.95)));
            int atta = sim.Config.Game.Market.IndexOf("atta");
            Assert.That(bakery.Stock[atta], Is.GreaterThan(0), "he bought atta ahead");

            ms.Mgr.Style = 2; mm.Mgr.Style = 2;
            InvoiceTests.ToHour(sim, 7, 1);
            Assert.That(stall.PriceFactor, Is.EqualTo(F(1.05)), "quality-first charges 5% over the reference");
            Assert.That(stall.Quality, Is.EqualTo(2), "good quality");
            for (int g = 0; g < one.Length; g++) Assert.That(stall.PutOut[g], Is.EqualTo(one[g]), "one day's stock");
            Assert.That(mill.Fee, Is.EqualTo(BusinessRules.TypeOf(sim, mill).Fee * F(1.05)));

            ms.Mgr.Style = 0; mm.Mgr.Style = 0;
            InvoiceTests.ToHour(sim, 7, 1);
            Assert.That(stall.PriceFactor, Is.EqualTo(Fixed.One));
            Assert.That(stall.Quality, Is.EqualTo(1));
            Assert.That(mill.Fee, Is.EqualTo(BusinessRules.TypeOf(sim, mill).Fee));
            Reconciles(sim);
        }

        // AC-6: the chance of a mis-set day follows the stars: 20%, 15.5, 11, 6.5, 2; a mis-set day cuts what the business makes or sells by half.
        [Test]
        public void MisSet_By_Stars()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "flour_mill");
            Worker m = Manage(sim, b);
            m.Fairness = 0;
            double[] chance = { 0.20, 0.155, 0.11, 0.065, 0.02 };
            for (int stars = 1; stars <= 5; stars++)
            {
                m.Skill = Fixed.FromInt(stars * 20);
                Assert.That(ManagerRules.Stars(sim, m), Is.EqualTo(stars));
                Assert.That(ManagerRules.MissChance(sim, m), Is.EqualTo(F(chance[stars - 1])), stars + " stars");
            }
            Assert.That(sim.Config.Game.Managers.Stars(Fixed.FromInt(5)), Is.EqualTo(1), "never below one star");
            long rate = BusinessRules.Capacity(sim, b);
            m.Mgr.MisToday = true;
            Assert.That(BusinessRules.Capacity(sim, b), Is.EqualTo(rate / 2), "half made on a mis-set day");
            m.Mgr.MisToday = false;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 50_000_000 * Coin), new Posting(Account.OwnersCapital, -50_000_000 * Coin));
            int days = 0, missed = 0;
            for (int d = 0; d < 40; d++)
            {
                m.Skill = Fixed.FromInt(20); // one star each morning, whatever the seasons did
                InvoiceTests.ToHour(sim, 7, 1);
                if (P(sim).ManagerAt(b.Id) != m) break;
                days++;
                if (m.Mgr.MisToday) missed++;
            }
            Assert.That(days, Is.GreaterThan(30));
            Assert.That(missed, Is.InRange(days / 20, days * 2 / 5), "about one day in five at one star");
        }

        // Sprint 25 balance (ADR 0164): he works it himself. He is the business's one worker, so a staffed business must let its worker go first, nobody else is hired, a manager's
        // base follows what a worker of the trade earns, and when he goes the seat is empty.
        [Test]
        public void Works_It_Himself()
        {
            Simulation sim = Township();
            Business.Business mill = InvoiceTests.Found(sim, "flour_mill");
            Business.Business bakery = InvoiceTests.Found(sim, "bakery", mill.TileId);
            Worker m = Manage(sim, bakery, 2);
            Assert.That(P(sim).StaffAt(bakery.Id), Is.SameAs(m));
            Assert.That(BusinessRules.HiredWorker(sim, bakery), Is.SameAs(m), "the business is worked by him");
            Assert.That(sim.Execute(new LetWorkerGo(sim.State.Clock.SimTime, bakery.Id)).Reason, Is.EqualTo(RejectReason.HasManager), "he goes by Dismiss, not Let go");
            for (int d = 0; d < 4; d++) InvoiceTests.ToHour(sim, 7, 1);
            Assert.That(P(sim).Workers.Count(w => w.Place == bakery.Id), Is.EqualTo(1), "nobody else is hired, whatever his style");
            Assert.That(P(sim).PostAt(bakery.Id), Is.Null);
            Assert.That(bakery.MadeToday.Sum() + bakery.Reports.Count, Is.GreaterThan(0), "the bakery works");
            // His base follows the trade: the same applicant asks more for a mill (27,000) or a bakery (30,000) than for a stall (12,000), by the share of a worker's wage.
            Applicant a = P(sim).ApplicantsFor(HiringRules.ManagerPlace).FirstOrDefault() ?? Post(sim);
            long forMill = HiringRules.OfferWage(sim, a, OfferLevel.Asked, mill.Id), forBakery = HiringRules.OfferWage(sim, a, OfferLevel.Asked, bakery.Id);
            Assert.That(forBakery, Is.GreaterThan(forMill), "a baker earns more than a miller here (30,000 against 27,000)");
            Assert.That(ManagerRules.MarketWage(sim, mill.Id), Is.EqualTo(Finance.Money.RoundToCoin(Finance.Money.MulRate(BusinessRules.TypeOf(sim, mill).Wage, sim.Config.Game.Managers.WageOfRole))));
            // He goes: the seat is empty and you can run it yourself again.
            Ok(sim, new DismissManager(sim.State.Clock.SimTime, bakery.Id));
            Assert.That((bakery.Worker, P(sim).StaffAt(bakery.Id)), Is.EqualTo((WorkerKind.None, (Worker)null)));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, bakery.Id));
            Reconciles(sim);
        }

        // AC-7: the target is profit or growth; at the close he reports against it (the goal from last season, raised) and two misses in a row sour his mood.
        [Test]
        public void Report_Target()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Worker m = Manage(sim, b);
            Assert.That(m.Mgr.Target, Is.EqualTo(ManagerTarget.Profit));
            Ok(sim, new SetManagerTarget(sim.State.Clock.SimTime, b.Id, ManagerTarget.Growth));
            Assert.That(m.Mgr.Target, Is.EqualTo(ManagerTarget.Growth));
            Ok(sim, new SetManagerTarget(sim.State.Clock.SimTime, b.Id, ManagerTarget.Profit));
            Assert.That(sim.Execute(new SetManagerTarget(sim.State.Clock.SimTime, 999, ManagerTarget.Profit)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            Assert.That(m.Mgr.ReportPeriod, Is.GreaterThan(0), "he reported");
            Assert.That(m.Mgr.ReportResult, Is.EqualTo(BusinessBooks.NetProfit(b.Reports.Last())), "on the profit the books show");
            Assert.That(m.Mgr.ReportGoal, Is.EqualTo(System.Math.Max(0, b.Plan.Profit - m.Wage)), "a first season: the plan's profit less his pay, so he is judged on what he adds");
            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            BusinessReport prev = b.Reports[b.Reports.Count - 2];
            long profit = BusinessBooks.NetProfit(prev);
            Assert.That(m.Mgr.ReportGoal, Is.EqualTo(profit > 0 ? Finance.Money.MulRate(profit, sim.Config.Game.Managers.ProfitGoalRise) : 0), "last season's profit plus 5%");
            Assert.That(m.Mgr.ReportMet, Is.EqualTo(m.Mgr.ReportResult >= m.Mgr.ReportGoal));
            Assert.That(sim.PendingEvents.OfType<ManagerReported>().Count(), Is.GreaterThanOrEqualTo(2));

            // Growth: last season's sales plus 10%; two misses in a row sour his mood for the season.
            m.Mgr.Target = ManagerTarget.Growth;
            m.Mgr.Misses = 0;
            m.Hr.Mood = Fixed.Zero;
            b.Plan.Revenue = 1_000_000_000 * Coin; // a goal he cannot meet
            b.Reports.Clear();
            ManagerRules.Report(sim, b, sim.Period);
            Assert.That(m.Mgr.ReportMet, Is.False);
            Assert.That(m.Mgr.Misses, Is.EqualTo(1));
            Fixed mood = m.Hr.Mood;
            ManagerRules.Report(sim, b, sim.Period);
            Assert.That(m.Mgr.Misses, Is.EqualTo(2));
            Assert.That(m.Hr.Mood, Is.EqualTo(sim.Config.Game.Managers.MissedMood), "two misses in a row sour his mood");
            Assert.That(m.Hr.Mood, Is.Not.EqualTo(mood));
        }

        // AC-7: his contract renews at the new season's wage and his skill grows; one who took an offer from elsewhere goes at the close; one the business cannot pay goes too.
        [Test]
        public void Renewal_And_Leaving()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Worker m = Manage(sim, b);
            Fixed skill = m.Skill;
            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            Assert.That(P(sim).ManagerAt(b.Id), Is.SameAs(m), "renewed");
            Assert.That(m.SeasonsWorked, Is.EqualTo(1));
            Assert.That(m.Skill, Is.GreaterThanOrEqualTo(skill), "skill grows with seasons managed");
            m.Hr.Leaving = true;
            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            Assert.That(P(sim).ManagerAt(b.Id), Is.Null, "he took an offer from elsewhere");
            Assert.That(sim.PendingEvents.OfType<ManagerLeft>().Any(), Is.True);
            Reconciles(sim);
        }

        // AC-7: a managed business counts as passive income, as a hired one does; one you run yourself does not.
        [Test]
        public void Passive_Income()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Manage(sim, b);
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 500_000 * Coin), new Posting(Account.Sales, -500_000 * Coin));
            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            Hub.Passive(sim, out _, out _, out long business);
            Assert.That(business, Is.EqualTo(BusinessBooks.NetProfit(b.Reports.Last())).And.GreaterThan(0));
            Ok(sim, new DismissManager(sim.State.Clock.SimTime, b.Id));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, b.Id));
            Hub.Passive(sim, out _, out _, out business);
            Assert.That(business, Is.EqualTo(0), "yours to run: your labour");
        }

        [Test]
        public void Closing_Lets_Him_Go()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Worker m = Manage(sim, b);
            Citizen c = sim.State.Citizens.Citizen(m.CitizenId);
            Assert.That(c.Job, Is.EqualTo(CitizenJob.Business));
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That(P(sim).ManagerAt(b.Id), Is.Null);
            Assert.That(c.Job, Is.EqualTo(CitizenJob.None), "back to outside work");
            Reconciles(sim);
        }

        // Content: the file loads as shipped; a mis-set chance that rises with stars, a missing style or an unknown field is refused with its path.
        [Test]
        public void Content_Is_Checked()
        {
            string json = TestContent.ReadContent("managers.json");
            Sim.Core.Definitions.ManagersDef d = Sim.Content.ContentLoader.LoadManagers(json);
            Assert.That((d.WageOfRole, d.Styles.Count, d.MissDay.Count), Is.EqualTo((Fixed.FromRatio(60, 100), 3, 5)));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadManagers(json.Replace("0.155", "0.5")), "more stars never mis-set more days");
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadManagers(json.Replace("\"stockDays\": 2", "\"stockDays\": 2, \"pick\": \"best\"")), "the old hiring fields are gone");
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadManagers(json.Replace("\"wageOfRole\"", "\"wage\"")));
        }
    }
}
