using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using Tools.Balancer;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The exploit checks of spec 028 §8 AC-31 and AC-30 (Sprint 25 balance, ADR 0164): a player who works the new rules for money finds none. Each check plays two
    /// towns that differ only by the trick and compares your net worth (cash, the businesses' equity, less what outside owners are owed). KhataWorst and
    /// MarketingSpiral wait for US-260 (Sprint 26).
    /// </summary>
    public partial class GateTests
    {
        private static long Worth(Simulation sim) { return Reporting.NetWorth(sim).Total; }

        private static void GrowthReconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        /// <summary>Plays days at a time to 7:00, calling <paramref name="each"/> before each.</summary>
        private static void PlayDays(Simulation sim, int days, System.Action<int> each = null)
        {
            for (int d = 0; d < days; d++)
            {
                each?.Invoke(d);
                InvoiceTests.ToHour(sim, 7, 1);
            }
        }

        // AC-31, terms and factoring: a longer term wins nothing (the seller waits for its cash), flipping the term day by day wins nothing, nothing turns a receivable into
        // cash early (there is no factoring), and no number of term changes at one instant moves a coin.
        [Test]
        public void InvoiceLoops()
        {
            long Play(string term, bool flip)
            {
                Simulation sim = InvoiceTests.MillAndBakery(out Business.Business mill, out Business.Business bakery);
                if (!flip) Ok(sim, new SetTerms(sim.State.Clock.SimTime, mill.Id, bakery.Id, null, term));
                PlayDays(sim, 16, d => { if (flip) Ok(sim, new SetTerms(sim.State.Clock.SimTime, mill.Id, bakery.Id, null, d % 2 == 0 ? "cash" : "season")); });
                GrowthReconciles(sim);
                return Worth(sim);
            }
            long cash = Play("cash", false), half = Play("half", false), season = Play("season", false), flip = Play(null, true);
            TestContext.Out.WriteLine("INVOICES net worth after 4 seasons: cash " + cash / Coin + ", half " + half / Coin + ", season " + season / Coin + ", flipping " + flip / Coin);
            long margin = cash / 100; // 1%
            Assert.That(half, Is.LessThanOrEqualTo(cash + margin), "half-season terms win nothing");
            Assert.That(season, Is.LessThanOrEqualTo(cash + margin), "season terms win nothing");
            Assert.That(flip, Is.LessThanOrEqualTo(cash + margin), "flipping the term wins nothing");

            Simulation spam = InvoiceTests.MillAndBakery(out Business.Business m, out Business.Business b);
            long before = Worth(spam), at = spam.State.Clock.SimTime;
            for (int i = 0; i < 500; i++) Ok(spam, new SetTerms(at, m.Id, b.Id, null, i % 3 == 0 ? "cash" : i % 3 == 1 ? "half" : "season"));
            Assert.That(Worth(spam), Is.EqualTo(before), "500 term changes at one instant move nothing");
            GrowthReconciles(spam);
        }

        // AC-31, sell-and-buy-back: found a business, sell it, found it again, over and over: you only ever lose (the sale is never under what it holds, but it never
        // pays a real premium on a business with no profit, and the sale costs its people's severance and the days between).
        [Test]
        public void SellBuyBack()
        {
            Simulation sim = InvoiceTests.Township();
            long give = 6_000_000 * Coin - sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            long start = Worth(sim), last = start;
            for (int cycle = 0; cycle < 4; cycle++)
            {
                Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "bakery", InvoiceTests.FreeTile(sim), 400_000 * Coin, false));
                Business.Business b = sim.State.Businesses.Businesses.Last();
                BusinessTests.Staff(sim, b);
                sim.AdvanceTo(b.ReadyAt + 60);
                Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, cycle % 2 == 1));
                for (int h = 0; h < 8 * 24 && b.Status != BusinessStatus.Closed; h++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
                Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed), "sold");
                long now = Worth(sim);
                TestContext.Out.WriteLine("SELLBUYBACK cycle " + cycle + ": net worth " + (now - last) / Coin + " on the cycle, " + (now - start) / Coin + " since the start");
                last = now;
            }
            SaleRecord[] sales = sim.State.Township.Exit.Sales.ToArray();
            Assert.That(sales.Length, Is.EqualTo(4));
            foreach (SaleRecord s in sales)
                Assert.That(s.Gain, Is.LessThanOrEqualTo(s.Held / 20 + 1), "a business with no profit sells for at most 5% over what it holds (" + s.Gain / Coin + " on " + s.Held / Coin + ")");
            long premium = sales.Sum(s => s.Gain - s.Tax - s.Severance), held = sales.Sum(s => s.Held);
            TestContext.Out.WriteLine("SELLBUYBACK premium after tax and severance " + premium / Coin + " on " + held / Coin + " held (" + (premium * 10_000 / held / 100.0).ToString("0.00") + "%) over about 6 seasons: under the deposit's 1.5% a season");
            Assert.That(premium, Is.LessThanOrEqualTo(held / 100), "the premium after tax and severance is under 1% of what the four businesses held, for about 6 seasons of waiting");
            GrowthReconciles(sim);
        }

        // AC-31, investor dilution: take an investor's cash and buy him back four seasons on. (a) At a steady value he is paid his share of the profit meanwhile, so the cash is
        // never free: you end no richer than the town that never took him, and the cash is no gain the moment it arrives (a share is owed on the books). (b) If the business's
        // profit then collapses, the buy-back must not cost less than he put in (he never gains by a fall he did not cause, and you never gain by one).
        [Test]
        public void InvestorDilution()
        {
            long Play(bool investor, bool collapse, out long cashIn, out long buyBack)
            {
                cashIn = buyBack = 0;
                Simulation sim = InvoiceTests.Township();
                Business.Business b = InvoiceTests.Found(sim, "bakery");
                ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000);
                if (investor)
                {
                    ExitState x = sim.State.Township.Exit;
                    long before = Worth(sim);
                    var o = new DealOffer { Id = x.NextOfferId++, Kind = DealKind.Investor, BusinessId = b.Id, Share = Fixed.FromRatio(30, 100), Cash = Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, Fixed.FromRatio(30, 100))), RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 3600 };
                    x.Offers.Add(o);
                    Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
                    cashIn = o.Cash;
                    Assert.That(Worth(sim) - before, Is.EqualTo(0).Within(Coin), "his cash is no gain the moment it arrives: the share is owed on the books");
                    Assert.That(sim.Execute(new TakeCash(sim.State.Clock.SimTime, b.Id, o.Cash)).Accepted, Is.False, "his money is not yours to draw: only what the business holds beyond it");
                    long free = BusinessBooks.Drawable(b);
                    if (free > 0) Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, free));
                    Assert.That(Worth(sim) - before, Is.EqualTo(0).Within(Coin), "and drawing what is yours changes nothing either");
                }
                // Twenty days (five seasons): the business keeps the profit it had (a steady value), or its profit falls to nothing from the first close.
                PlayDays(sim, 20, d => { if (collapse && d >= 4) ExitTests.Seasons(b, 0, 0, 0, 0); else if (!collapse) ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000); });
                if (investor && b.Stakes.Count > 0)
                {
                    b.Stakes[0].Period -= 5;
                    buyBack = OwnerRules.BuyBackPrice(sim, b, b.Stakes[0]);
                    sim.Execute(new BuyBackShare(sim.State.Clock.SimTime, b.Id, StakeKind.Investor));
                }
                GrowthReconciles(sim);
                return Worth(sim);
            }
            long plain = Play(false, false, out _, out _), steady = Play(true, false, out long cash, out long price);
            TestContext.Out.WriteLine("INVESTOR (a) steady value, net worth after 5 seasons: without " + plain / Coin + ", taken and bought back " + steady / Coin + " (his cash " + cash / Coin + ", bought back at " + price / Coin + ")");
            Assert.That(steady, Is.LessThanOrEqualTo(plain + plain / 100), "(a) an investor taken and bought back at a steady value leaves you no richer");
            Play(true, true, out long in2, out long back2);
            TestContext.Out.WriteLine("INVESTOR (b) profit collapses: his cash " + in2 / Coin + ", bought back at " + back2 / Coin);
            Assert.That(back2, Is.GreaterThanOrEqualTo(in2), "(b) after a collapse the buy-back is no cheaper than his cash");
        }

        // AC-31 (review): take an investor's cash, draw what you can and close. His money cannot be drawn, so the close pays him his share of what is left (a closing that
        // destroys value costs him part of his stake, as a real investor's); but it never beats simply selling the business.
        [Test]
        public void InvestorDrawAndClose()
        {
            long Play(bool investor)
            {
                Simulation sim = InvoiceTests.Township();
                Business.Business b = InvoiceTests.Found(sim, "bakery");
                ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000);
                long before = Worth(sim);
                if (investor)
                {
                    ExitState x = sim.State.Township.Exit;
                    var o = new DealOffer { Id = x.NextOfferId++, Kind = DealKind.Investor, BusinessId = b.Id, Share = Fixed.FromRatio(30, 100), Cash = Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, Fixed.FromRatio(30, 100))), RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 3600 };
                    x.Offers.Add(o);
                    Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
                    long free = BusinessBooks.Drawable(b);
                    if (free > 0) Ok(sim, new TakeCash(sim.State.Clock.SimTime, b.Id, free));
                }
                if (investor) Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
                else
                {
                    Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, true));
                    for (int h = 0; h < 8 * 24 && b.Status != BusinessStatus.Closed; h++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
                }
                GrowthReconciles(sim);
                return Worth(sim) - before;
            }
            long plain = Play(false), with = Play(true);
            TestContext.Out.WriteLine("INVESTORCLOSE net worth change: sold at once " + plain / Coin + ", investor taken, drawn and closed " + with / Coin);
            Assert.That(with, Is.LessThanOrEqualTo(plain), "taking an investor's cash and closing never beats selling the business");
        }

        // AC-31 (review): a business that has traded no season is worth what it holds, never a plan's profit: found it at the least capital, run it yourself, list it and sell it.
        [Test]
        public void SellNew()
        {
            foreach (bool yourself in new[] { true, false })
            {
                Simulation sim = InvoiceTests.Township();
                long give = 6_000_000 * Coin - sim.State.Ledger.Cash;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
                long start = Worth(sim);
                Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "bakery", InvoiceTests.FreeTile(sim), (yourself ? 145_000 : 200_000) * Coin, yourself));
                Business.Business b = sim.State.Businesses.Businesses.Last();
                if (!yourself) BusinessTests.Staff(sim, b);
                sim.AdvanceTo(b.ReadyAt + 60);
                var w = ExitRules.Valuation(sim, b);
                Assert.That(w.Value, Is.EqualTo(w.Book), "no season closed: worth is what it holds");
                Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, true));
                for (int h = 0; h < 8 * 24 && b.Status != BusinessStatus.Closed; h++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
                SaleRecord sale = sim.State.Township.Exit.Sales.Single();
                TestContext.Out.WriteLine("SELLNEW yourself=" + yourself + ": sold at a gain of " + sale.Gain / Coin + " on " + sale.Held / Coin + " held; net worth " + (Worth(sim) - start) / Coin + " with the days it ran");
                Assert.That(sale.Gain, Is.LessThanOrEqualTo(sale.Held / 100 + 1), "no season closed: it sells for what it holds, never for a plan's profit");
                GrowthReconciles(sim);
            }
        }

        // Sprint 25 balance (review): the tuition centre's and the restaurant's season profit, hired and run by you, in a Township bot's town at 30 seasons. Run with --filter Targets.
        [Test, Explicit("measure")]
        public void Targets()
        {
            TestContent.WithCohort = true;
            SimConfig config;
            try { config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game(null)); }
            finally { TestContent.WithCohort = false; }
            foreach (string type in new[] { "tuition", "restaurant" })
                foreach (bool yourself in new[] { false, true })
                    for (ulong seed = 1; seed <= 3; seed++)
                    {
                        Simulation sim = Tools.Balancer.Economy.RunBot(config, new Tools.Balancer.TownshipBot(false), 30, seed, null).Sim;
                        long give = 3_000_000 * Coin - sim.State.Ledger.Cash;
                        if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
                        bool found = false;
                        if (type == "tuition") // the bot's town has no free acre: buy one
                            foreach (var parcel in sim.Config.Game.Map.Parcels.ToList())
                            {
                                if (sim.Execute(new BuyParcel(sim.State.Clock.SimTime, parcel.Id)).Accepted)
                                {
                                    found = sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, type, InvoiceTests.FreeTile(sim), 400_000 * Coin, yourself)).Accepted;
                                    if (found) break;
                                }
                            }
                        else for (int pitch = 0; pitch < 12 && !found; pitch++) found = sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, type, 0, 400_000 * Coin, yourself, null, pitch)).Accepted;
                        if (!found) { TestContext.Out.WriteLine("TARGETS " + type + " seed " + seed + ": no free pitch"); continue; }
                        Business.Business b = sim.State.Businesses.Businesses.Last();
                        if (!yourself) BusinessTests.Staff(sim, b);
                        PlayDays(sim, 32);
                        var profits = b.Reports.Select(r => BusinessBooks.NetProfit(r) / Coin).ToList();
                        long mean = profits.Count == 0 ? 0 : profits.Skip(profits.Count > 3 ? 2 : 0).Sum() / System.Math.Max(1, profits.Count - (profits.Count > 3 ? 2 : 0));
                        TestContext.Out.WriteLine("TARGETS " + type + (yourself ? " run by you" : " hired") + " seed " + seed + ": seasons " + profits.Count + ", profits " + string.Join(",", profits) + ", mean after the first two " + mean);
                    }
        }

        // AC-31, all four benefits stacked: each costs more than the output it adds (spec 028 §3.8), so a bakery that gives all four earns the owner no more than one that gives none.
        [Test]
        public void StackBenefits()
        {
            long Play(bool all)
            {
                Simulation sim = InvoiceTests.Township();
                sim.State.Township.Clinic.Status = ClinicStatus.Open;
                sim.State.Township.Clinic.Doctor = DoctorStatus.InPost;
                Business.Business b = InvoiceTests.Found(sim, "bakery");
                if (all) foreach (Benefit k in new[] { Benefit.Meal, Benefit.Health, Benefit.Festival, Benefit.Leave }) Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, k, true));
                PlayDays(sim, 16);
                GrowthReconciles(sim);
                return Worth(sim);
            }
            long none = Play(false), all4 = Play(true);
            TestContext.Out.WriteLine("BENEFITS net worth after 4 seasons: none " + none / Coin + ", all four " + all4 / Coin + " (" + (all4 - none) / Coin + ")");
            Assert.That(all4, Is.LessThanOrEqualTo(none + none / 200), "four benefits cost more than the output they add");
        }

        // AC-31, night shifts without demand: a night worker adds hours, not customers (a second wage at a quarter more for nothing to sell), so a bakery in a town too small
        // to keep one shift busy earns no more with a night shift than without.
        [Test]
        public void NightNoDemand()
        {
            long Play(bool night)
            {
                Simulation sim = InvoiceTests.Township();
                sim.State.Township.UtilitiesBuilt |= 1 << UtilityDef.Grid;
                Business.Business b = InvoiceTests.Found(sim, "bakery");
                if (night)
                {
                    b.ShortDays = 0b111; // as if it had been offered
                    Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
                }
                PlayDays(sim, 16);
                GrowthReconciles(sim);
                return Worth(sim);
            }
            long day = Play(false), night = Play(true);
            TestContext.Out.WriteLine("NIGHT net worth after 4 seasons: day shift only " + day / Coin + ", with a night shift " + night / Coin + " (" + (night - day) / Coin + ")");
            Assert.That(night, Is.LessThanOrEqualTo(day + day / 200), "a night shift with no demand behind it earns nothing");
        }

        // AC-30: a town that answers nothing still works (invoices left to age, the treasury never below zero), and no command of the growth can be looped at one instant for
        // money: switching things on and off at the same second only ever costs.
        [Test]
        public void Growth()
        {
            Simulation sim = InvoiceTests.MillAndBakery(out Business.Business mill, out Business.Business bakery);
            Business.Business centre = InvoiceTests.Found(sim, "tuition", mill.TileId, bakery.TileId);
            PlayDays(sim, 24);
            Assert.That(sim.State.Treasury.Ledger.Cash, Is.GreaterThanOrEqualTo(0), "the treasury never goes below zero");
            GrowthReconciles(sim);

            sim.State.Township.UtilitiesBuilt |= 1 << UtilityDef.Grid;
            bakery.ShortDays = 0b111;
            long before = Worth(sim), at = sim.State.Clock.SimTime;
            for (int i = 0; i < 100; i++)
            {
                foreach (Benefit k in new[] { Benefit.Meal, Benefit.Festival, Benefit.Leave })
                {
                    sim.Execute(new SetBenefit(at, bakery.Id, k, true));
                    sim.Execute(new SetBenefit(at, bakery.Id, k, false));
                }
                sim.Execute(new SetNightShift(at, bakery.Id, true));
                sim.Execute(new SetNightShift(at, bakery.Id, false));
                sim.Execute(new ListBusiness(at, bakery.Id, false));
                sim.Execute(new CancelBusinessSale(at, bakery.Id));
                sim.Execute(new SetTuitionFee(at, centre.Id, i % 4));
            }
            long after = Worth(sim);
            TestContext.Out.WriteLine("GROWTH zero-time loops: net worth " + (after - before) / Coin + " (a benefit switched on is paid for and not refunded)");
            Assert.That(after, Is.LessThanOrEqualTo(before), "switching at one instant never earns");
            GrowthReconciles(sim);
        }

        // Spec 028 §9 (US-124, ADR 0157, 0164): managed against hired. The Township bot with a manager of each style working every business himself (its worker let go), against the same
        // bot with hired workers: the median net worth in today's coins after 60 seasons (12 seeds), and the businesses' last season profit, printed so a miss shows its size.
        // The owner's rule (2026-11-23): a manager who only costs money is no use, so every style ends at least level with hired; a manager is still paid, so none by over a quarter.
        [Test]
        public void Managed() { ManagedMeasure(LiveTownship()); }

        [Test, Explicit("measure")]
        public void ManagedLevers()
        {
            var variants = new (string Name, string From, string To)[]
            {
                ("base 60% of a worker's wage (as is)", "\"wageOfRole\": 0.60", "\"wageOfRole\": 0.60"),
                ("base 80%", "\"wageOfRole\": 0.60", "\"wageOfRole\": 0.80"),
                ("base 100%", "\"wageOfRole\": 0.60", "\"wageOfRole\": 1.00"),
            };
            foreach (var v in variants)
            {
                TestContext.Out.WriteLine("LEVER " + v.Name);
                ManagedMeasure(LiveTownship((f, j) => { if (f != "managers.json") return j; Assert.That(j, Does.Contain(v.From)); return j.Replace(v.From, v.To); }), styles: new[] { 0, 1, 2 });
            }
        }

        private static void ManagedMeasure(SimConfig config, int[] styles = null)
        {
            var types = new System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentBag<double>>();
            (double Worth, double Profit) Median(int? style)
            {
                types.Clear();
                var worth = new double[12]; var profit = new double[12];
                System.Threading.Tasks.Parallel.For(0, 12, i =>
                {
                    Economy.Run run = Economy.RunBot(config, new Tools.Balancer.TownshipBot(false) { ManagerStyle = style }, 60, (ulong)(i + 1), null);
                    worth[i] = (double)Reporting.NetWorth(run.Sim).Total / 100.0 / ((double)run.Sim.State.Market.PriceIndex.Raw / 4294967296.0);
                    long p = 0;
                    foreach (Business.Business b in run.Sim.State.Businesses.Businesses)
                        if (b.Reports.Count > 0)
                        {
                            p += BusinessBooks.NetProfit(b.Reports.Last());
                            {
                                var r = b.Reports.Last();
                                string id = run.Sim.Config.Game.Businesses.Types[b.Type].Id;
                                types.GetOrAdd(id, _ => new System.Collections.Concurrent.ConcurrentBag<double>()).Add(BusinessBooks.NetProfit(r) / 100.0);
                                long inc = 0, labour = 0;
                                foreach (var kv in r.Balances) { if (Accounts.GroupOf(kv.Key) == AccountGroup.Income) inc -= kv.Value; if (kv.Key == Account.HiredLabour) labour += kv.Value; }
                                types.GetOrAdd(id + " income", _ => new System.Collections.Concurrent.ConcurrentBag<double>()).Add(inc / 100.0);
                                types.GetOrAdd(id + " wages", _ => new System.Collections.Concurrent.ConcurrentBag<double>()).Add(labour / 100.0);
                            }
                        }
                    profit[i] = p / 100.0;
                });
                TestContext.Out.WriteLine("MANAGED   by type (median last season profit): " + string.Join(", ", types.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value.OrderBy(v => v).ElementAt(k.Value.Count / 2).ToString("0") + " (" + k.Value.Count + ")")));
                return (worth.OrderBy(w => w).ElementAt(6), profit.OrderBy(w => w).ElementAt(6));
            }
            (double Worth, double Profit) hired = Median(null);
            TestContext.Out.WriteLine("MANAGED hired: net worth median " + hired.Worth.ToString("0") + ", businesses' last season profit " + hired.Profit.ToString("0"));
            foreach (int style in styles ?? new[] { 0, 1, 2 })
            {
                (double Worth, double Profit) m = Median(style);
                TestContext.Out.WriteLine("MANAGED style " + style + ": net worth " + m.Worth.ToString("0") + " = " + (m.Worth / hired.Worth).ToString("0.000") + " of hired; profit " + m.Profit.ToString("0") + " = " + (hired.Profit == 0 ? 0 : m.Profit / hired.Profit).ToString("0.000") + " of hired");
                if (styles == null)
                {
                    Assert.That(m.Worth, Is.GreaterThanOrEqualTo(hired.Worth), "style " + style + ": a manager who works it himself is worth his pay: managed ends no poorer than hired");
                    Assert.That(m.Worth, Is.LessThanOrEqualTo(hired.Worth * 1.25), "style " + style + ": and not a windfall (a manager is paid, you are not)");
                }
            }
        }
    }
}
