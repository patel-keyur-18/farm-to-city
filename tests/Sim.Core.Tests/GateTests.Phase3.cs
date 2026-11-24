using System.Collections.Concurrent;
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
    /// US-248 (ADR 0107 §11, ADR 0166 gap 13): the Phase 3 exploit checks that had no named test of their own. The other items of the story's list are `MilkTheTreasury` (the treasury never
    /// reaches your wallet), `RiskExploits` (insure everything, every forward contract), `EveryTaxAtZero` (a town with every tax at 0), `InvoiceLoops`, `SellBuyBack`, `StackBenefits`,
    /// `NightNoDemand`, `KhataWorst`, `MarketingSpiral`; these five add the zero-time loops over the Township's commands, a found-and-sell bot, a landlord at the rent cap, late payment of your
    /// own invoices, and the Township's health (never stuck, exact books, bounded prices). `FTC_SEEDS` sets the number of seeds (200 by default, to game year 8).
    /// </summary>
    public partial class GateTests
    {
        private static int P3Seeds { get { string v = System.Environment.GetEnvironmentVariable("FTC_SEEDS"); return v == null ? 200 : int.Parse(v); } }

        /// <summary>Game year 8 (4 seasons a year, 32 seasons): the Township is reached about season 25 by the competent bot.</summary>
        private const int YearEight = 32;

        private static double RealWorth(Simulation sim) { return (double)Reporting.NetWorth(sim).Total / 100.0 / ((double)sim.State.Market.PriceIndex.Raw / 4294967296.0); }

        private static double MidOf(System.Collections.Generic.IEnumerable<double> v) { var l = v.OrderBy(x => x).ToList(); return l[l.Count / 2]; }

        // ---- the zero-time loops over the Township's commands ----

        private static readonly string[] WorkIds = { "watchman_post", "playground", "community_hall", "water_tower", "drains", "collection_post" };
        private static readonly string[] BusinessIds = { "bakery", "veg_stall", "tea_stall", "flour_mill", "kirana", "tuition" };
        private static readonly string[] Terms = { "cash", "half", "season" };

        /// <summary>A seeded random command of the Township's: land, taxes, policies, works, festivals, insurance, benefits, shops, invoices, businesses, leases, loans.</summary>
        private static Command TownshipCommand(Simulation sim, long t, ulong r)
        {
            GameDefs defs = sim.Config.Game;
            ulong Raw(int k) { return Rng.Next(r, (RngStream)(980 + k), 0, 0); }
            int Pick(int k, int n) { return n <= 0 ? 0 : (int)Rng.Below(Raw(k), (ulong)n); }
            var bs = sim.State.Businesses.Businesses;
            long bid = bs.Count == 0 ? 1 : bs[Pick(1, bs.Count)].Id, other = bs.Count == 0 ? 2 : bs[Pick(20, bs.Count)].Id;
            string pid = defs.Map.Parcels[Pick(2, defs.Map.Parcels.Count)].Id;
            long money = (Pick(3, 200) + 1) * 1000 * Money.Coin;
            var hh = sim.State.Citizens.Households;
            long hid = hh.Count == 0 ? 1 : hh[Pick(21, hh.Count)].Id;
            bool on = Pick(12, 2) == 0;
            switch (Pick(0, 41))
            {
                case 0: return new BuyParcel(t, pid);
                case 1: return new ListParcel(t, pid);
                case 2: return new CancelSale(t, pid);
                case 3: return new SellParcel(t, pid);
                case 4: return new ZoneParcel(t, pid);
                case 5: return new UnzoneParcel(t, pid);
                case 6: return new LeaseOutLand(t, pid);
                case 7: return new EndLease(t, pid);
                case 8: return new SetTaxRate(t, (TaxKind)Pick(4, 2), Fixed.FromRatio(Pick(5, 25), 1000));
                case 9: return new UndoTaxRate(t, (TaxKind)Pick(4, 2));
                case 10: return new EnactPolicy(t, (PolicyId)Pick(6, 5));
                case 11: return new RepealPolicy(t, (PolicyId)Pick(6, 5));
                case 12: return new GiveToTreasury(t, money);
                case 13: return new CommissionWorks(t, WorkIds[Pick(7, WorkIds.Length)], Pick(8, 2) == 0 ? 0 : money);
                case 14: return new ScheduleFestival(t, Pick(9, 2), Pick(10, 2), Pick(11, 4) + 1);
                case 15: return new SetCropInsurance(t, on);
                case 16: return new SetPropertyInsurance(t, on);
                case 17: return new SetBusinessInsurance(t, bid, on);
                case 18: return new SetBenefit(t, bid, (Benefit)(1 << Pick(13, 4)), on);
                case 19: return new SetNightShift(t, bid, on);
                case 20: return new SetKhata(t, bid, on);
                case 21: { var l = ShopRules.Defs(sim).KhataLimits; return new SetKhataLimit(t, bid, l[Pick(14, l.Count)]); }
                case 22: { var l = ShopRules.Defs(sim).MarketBudgets; return new SetMarketing(t, bid, l[Pick(14, l.Count)]); }
                case 23: return new SetShop(t, bid, Fixed.FromRatio(80 + Pick(15, 50), 100), Pick(16, 2));
                case 24: return new SetTerms(t, bid, other, null, Terms[Pick(17, 3)]);
                case 25: return new SetTuitionFee(t, bid, Pick(18, 4));
                case 26: return new PutCash(t, bid, money);
                case 27: return new TakeCash(t, bid, money);
                case 28: return new FoundBusiness(t, BusinessIds[Pick(19, BusinessIds.Length)], Pick(22, 400), money * 3, false, null, Pick(23, 6) - 1);
                case 29: return new CloseBusiness(t, bid);
                case 30: return new ListBusiness(t, bid, on);
                case 31: return new CancelBusinessSale(t, bid);
                case 32: return new RunBusinessYourself(t, bid);
                case 33: { var offers = sim.State.Township.Contracts.Offers; return new AcceptContract(t, offers.Count == 0 ? 1 : offers[Pick(24, offers.Count)].Id); }
                case 34: return new RenewLease(t, hid, (LeaseChoice)(1 + Pick(25, 4)));
                case 35: return new TakeLoan(t, Pick(26, 2) == 0 ? "coop_bank" : "moneylender", money, 2 + Pick(27, 6));
                case 36: return new StartAdultClass(t);
                case 37: return new RentColdStore(t, Pick(28, 5));
                case 38: return new BuyBackShare(t, bid, (Sim.Core.Business.StakeKind)Pick(29, 2));
                case 39: return new DepositSavings(t, money);
                default: return new WithdrawSavings(t, money);
            }
        }

        // US-248 item 1: command sequences at one instant never raise net worth in a Township (the land, the treasury's gifts, the businesses, insurance, benefits, shops, invoices, leases, loans).
        // From each seed's Township bot at season 28 to 31, 5 sequences of 12 random commands: the net worth never rises by more than a coin a command that was accepted (rounding).
        // The accepted commands are tallied by kind, so a check that never touched a command shows.
        [Test]
        public void TownshipZeroTime()
        {
            SimConfig config = LiveTownship();
            int states = P3Seeds;
            var failures = new ConcurrentBag<string>();
            var tally = new ConcurrentDictionary<string, int>();
            long worst = long.MinValue;
            object gate = new object();
            System.Threading.Tasks.Parallel.For(0, states, i =>
            {
                ulong seed = (ulong)(i + 1);
                Simulation sim = Economy.RunBot(config, new TownshipBot(false), 28 + i % 4, seed, null).Sim;
                long t = sim.State.Clock.SimTime;
                for (int q = 0; q < 5; q++)
                {
                    long before = Reporting.NetWorth(sim).Total;
                    int accepted = 0;
                    for (int c = 0; c < 12; c++)
                    {
                        Command cmd = TownshipCommand(sim, t, Rng.Next(seed, (RngStream)975, (ulong)q, (ulong)c));
                        if (sim.Execute(cmd).Accepted) { accepted++; tally.AddOrUpdate(cmd.GetType().Name, 1, (k, n) => n + 1); }
                    }
                    long gain = Reporting.NetWorth(sim).Total - before;
                    lock (gate)
                    {
                        worst = System.Math.Max(worst, gain - accepted);
                        if (gain > accepted) failures.Add("seed " + seed + " sequence " + q + ": net worth +" + gain + " with " + accepted + " commands accepted");
                    }
                }
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "seed " + seed);
                foreach (Sim.Core.Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "seed " + seed + " business " + b.Id);
                Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "seed " + seed + " treasury");
            });
            TestContext.Out.WriteLine("ZEROTIME " + states + " Township states × 5 sequences of 12 commands; accepted by kind: " + string.Join(", ", tally.OrderBy(k => k.Key, System.StringComparer.Ordinal).Select(k => k.Key + " " + k.Value)));
            TestContext.Out.WriteLine("ZEROTIME worst net-worth change beyond rounding " + worst);
            Assert.That(tally.Count, Is.GreaterThanOrEqualTo(20), "the check really ran many kinds of command");
            Assert.That(failures.OrderBy(f => f, System.StringComparer.Ordinal).ToList(), Is.Empty);
        }

        // ---- found and sell, over and over ----

        /// <summary>The median net worth in today's coins at game year 8 of a Township bot, one run a seed.</summary>
        private static double YearEightWorth(SimConfig config, System.Func<TownshipBot> make, out int sold)
        {
            int seeds = P3Seeds;
            var worth = new double[seeds];
            var sales = new int[seeds];
            System.Threading.Tasks.Parallel.For(0, seeds, i =>
            {
                Simulation sim = Economy.RunBot(config, make(), YearEight, (ulong)(i + 1), null).Sim;
                worth[i] = RealWorth(sim);
                sales[i] = sim.State.Township?.Exit == null ? 0 : sim.State.Township.Exit.Sales.Count;
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "seed " + (i + 1));
            });
            sold = sales.Sum();
            return MidOf(worth);
        }

        // US-248 item 5 (AC-31): a bot that founds a business, runs it a season, sells it and founds the next, over and over, is not over 20% above the competent bot (ADR 0016) at game year 8.
        // Selling costs its people's severance, the days between, the tax on the gain and a rise only with profit, and a business with no profit never sells over book.
        [Test]
        public void FoundAndSell()
        {
            SimConfig config = LiveTownship();
            double plain = YearEightWorth(config, () => new TownshipBot(false), out int plainSales);
            double sells = YearEightWorth(config, () => new TownshipBot(false) { FoundAndSell = true }, out int sales);
            TestContext.Out.WriteLine("FOUNDANDSELL median net worth at year 8: competent " + plain.ToString("0") + " (" + plainSales + " sales), found-and-sell " + sells.ToString("0") + " (" + sales + " sales) = " + (sells / plain).ToString("0.000") + " × competent");
            Assert.That(sales, Is.GreaterThan(P3Seeds / 2), "the bot really sold (the check is not empty)");
            Assert.That(sells, Is.LessThanOrEqualTo(plain * 1.2), "found-and-sell is no more than 20% above the competent bot");
        }

        // US-248 item 7 (spec 014 §3.14, ADR 0110): a landlord who lets every lease rise to the cap (the market rise: no answer to a renewal card) against a fair one who keeps every rent. Printed with
        // the households each ends with; the rise is capped by the market, 10% a year and what 30% of a family's income allows, so it never pushes a family out.
        [Test]
        public void LandlordCap()
        {
            SimConfig config = LiveTownship();
            double cap = YearEightWorth(config, () => new TownshipBot(false), out int a);
            double fair = YearEightWorth(config, () => new TownshipBot(false) { FairRent = true }, out int b);
            TestContext.Out.WriteLine("LANDLORD median net worth at year 8: every rent to the cap " + cap.ToString("0") + ", fair (every rent kept) " + fair.ToString("0") + " = " + (cap / fair).ToString("0.000") + " × fair");
            Assert.That(cap, Is.LessThanOrEqualTo(fair * 1.2), "a landlord at the cap is no more than 20% above a fair one");
        }

        // ---- late payment of your own invoices ----

        // US-248 item 8 (spec 028 §3.1: no late fee, the stop rule): a buyer of yours that never pays its own supplier on time (its cash drawn out to your own wallet each day, so every invoice goes late and
        // then overdue and the mill stops delivering) makes no money against one that pays at the term's end: the lateness is a receivable and a payable that cancel and
        // the bakery with no atta stops selling.
        [Test]
        public void LateInvoices()
        {
            long Play(bool late)
            {
                Simulation sim = InvoiceTests.MillAndBakery(out Sim.Core.Business.Business mill, out Sim.Core.Business.Business bakery);
                Ok(sim, new SetTerms(sim.State.Clock.SimTime, mill.Id, bakery.Id, null, "season"));
                PlayDays(sim, 16, d =>
                {
                    if (!late) return;
                    long take = (bakery.Ledger.Cash - 1_000 * Coin) / Coin * Coin;
                    if (take > 0) sim.Execute(new TakeCash(sim.State.Clock.SimTime, bakery.Id, take));
                });
                GrowthReconciles(sim);
                return Worth(sim);
            }
            long onTime = Play(false), late = Play(true);
            TestContext.Out.WriteLine("LATEINVOICES net worth after 4 seasons: on time " + onTime / Coin + ", never on time " + late / Coin + " (" + (late - onTime) / Coin + ")");
            Assert.That(late, Is.LessThanOrEqualTo(onTime + onTime / 100), "paying late makes no money");
        }

        // ---- the Township's health: never stuck, exact books, bounded prices ----

        // US-248 item 9 (ADR 0045 §4 for the Township): the competent Township bot on every seed to game year 8: no ledger fails to reconcile at a close (the sim's strict checks throw), the treasury is never
        // below zero, nobody is stuck (the Village reached in every seed and the founder's net worth above zero at the end of year 8; the Township's own day is `Township`'s), and the price rail clamps under 0.1% of good-hours.
        [Test]
        public void TownshipHealth()
        {
            SimConfig config = LiveTownship();
            int seeds = P3Seeds;
            var failures = new ConcurrentBag<string>();
            var reached = new bool[seeds]; var village = new bool[seeds];
            var hits = new long[seeds]; var hours = new long[seeds];
            var cash = new long[seeds]; var worth = new long[seeds];
            System.Threading.Tasks.Parallel.For(0, seeds, i =>
            {
                try
                {
                    Simulation sim = Economy.RunBot(config, new TownshipBot(false), YearEight, (ulong)(i + 1), s =>
                    {
                        if (s.State.Treasury != null && s.State.Treasury.Opened && s.State.Treasury.Cash < 0) failures.Add("seed " + (i + 1) + ": the treasury below zero");
                    }).Sim;
                    reached[i] = TownshipRules.Reached(sim); village[i] = sim.State.Era.Current >= 2;
                    cash[i] = sim.State.Ledger.Cash; worth[i] = Reporting.NetWorth(sim).Total;
                    var goods = sim.Config.Game.Market.Goods;
                    for (int g = 0; g < goods.Count; g++)
                    {
                        if (goods[g].Kind != GoodKind.Produce) continue;
                        hits[i] += sim.State.Market.RailHitsOf(g);
                        hours[i] += sim.State.Market.MarketHours;
                    }
                }
                catch (System.InvalidOperationException e) { failures.Add("seed " + (i + 1) + ": " + e.Message); }
            });
            double rail = (double)hits.Sum() / System.Math.Max(1, hours.Sum());
            TestContext.Out.WriteLine("HEALTH " + seeds + " seeds to year 8: Village reached in " + village.Count(x => x) + ", Township in " + reached.Count(x => x) + ", cash below zero in " + cash.Count(c => c < 0) + ", net worth not above zero in " + worth.Count(w => w <= 0) + ", rail share " + rail.ToString("0.00000"));
            Assert.That(failures.OrderBy(f => f, System.StringComparer.Ordinal).ToList(), Is.Empty, "exact books and a treasury never below zero");
            Assert.That(village.Count(x => x), Is.EqualTo(seeds), "the Village is reached in every seed by year 8");
            Assert.That(worth.Count(w => w <= 0), Is.Zero, "nobody ends year 8 with no net worth");
            Assert.That(rail, Is.LessThan(0.001), "the 0.5 to 2 rail clamps under 0.1% of good-hours");
        }
    }
}
