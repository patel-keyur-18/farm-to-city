using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 018 §3.3, AC-6 (US-133, ADR 0095 §7): the Concept Codex's cards, found once at their moment with the player's numbers.</summary>
    public class CodexTests
    {
        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static void Ok(Simulation sim, Command c) { CommandResult r = sim.Execute(c); Assert.That(r.Accepted, Is.True, c.GetType().Name + ": " + r); }

        /// <summary>A close's report; its by-account figures are as the season report keeps them, costs negative (income less costs).</summary>
        private static SeasonClosed Closed(Simulation sim, long period, long net = 0, long opening = 0, long closing = 0, Account? account = null, long amount = 0, NetWorthSnapshot worth = null)
        {
            var byAccount = new List<KeyValuePair<Account, long>>();
            if (account.HasValue) byAccount.Add(new KeyValuePair<Account, long>(account.Value, amount));
            var report = new SeasonReport
            {
                Period = period, ByAccount = byAccount, Lines = new List<ReportLine> { new ReportLine("farmProfit", 0, new Account[0]), new ReportLine("netResult", net, new Account[0]) },
                CashFlow = new CashFlowSummary { Opening = opening, Closing = closing }, NetWorth = worth ?? new NetWorthSnapshot(),
            };
            return new SeasonClosed(T(sim), period, report);
        }

        private static BusinessReport Report(long period, long sales, long wage, long cogs = 0)
        {
            return new BusinessReport
            {
                Period = period,
                Balances = new List<KeyValuePair<Account, long>>
                {
                    new KeyValuePair<Account, long>(Account.Sales, -sales), new KeyValuePair<Account, long>(Account.HiredLabour, wage),
                    new KeyValuePair<Account, long>(Account.CostOfGoodsSold, cogs),
                },
            };
        }

        /// <summary>Finds <paramref name="id"/> at <paramref name="events"/> (with <paramref name="numbers"/> when given), records it, and never again.</summary>
        private static void Finds(Simulation sim, string id, IReadOnlyList<SimEvent> events, long[] numbers = null)
        {
            CodexFind f = CodexWatch.Observe(sim, events).SingleOrDefault(x => x.Id == id);
            Assert.That(f, Is.Not.Null, id + " at its moment");
            if (numbers != null) Assert.That(f.Numbers, Is.EqualTo(numbers), id + "'s numbers");
            Assert.That(f.Numbers.Length, Is.EqualTo(sim.Config.Game.Codex.Cards[sim.Config.Game.Codex.IndexOf(id)].Numbers.Count));
            Ok(sim, new FindCodexCards(T(sim), new[] { f }));
            Assert.That(CodexWatch.Observe(sim, events).Any(x => x.Id == id), Is.False, id + " is found once");
            Assert.That(sim.State.Guidance.Found(id).FoundAt, Is.EqualTo(T(sim)));
        }

        // AC-6: each Phase 2 card is found once, by its trigger, after the result, with the player's numbers.
        [Test]
        public void Triggers()
        {
            Simulation sim = Situations.Shops();
            sim.State.Guidance.Skipped = true;
            GameDefs g = sim.Config.Game;
            long t = T(sim);
            Assert.That(g.Codex.Cards.Count, Is.EqualTo(25));
            Assert.That(CodexWatch.Observe(sim, new SimEvent[] { new HourStarted(t) }), Is.Empty, "nothing without its moment");

            // Farming.
            var capped = new YieldBreakdown { Capped = true, Fertiliser = Fixed.Parse("1.2"), Uncapped = Fixed.Parse("1.8") };
            Finds(sim, "diminishingReturns", new SimEvent[] { new CropHarvested(t, 84, "potato", 1000, capped) }, new[] { Fixed.Parse("1.8").Raw, g.Farm.Farm.YieldCap.Raw });
            Assert.That(CodexWatch.Observe(sim, new SimEvent[] { new CropHarvested(t, 84, "potato", 1000, new YieldBreakdown { Capped = true, Fertiliser = Fixed.One }) }).Any(x => x.Id == "diminishingReturns"), Is.False);
            sim.State.Land.Tiles[84].Fertility = Fixed.FromInt(40);
            CropDef potato = g.Farm.Crops[g.Farm.IndexOfCrop("potato")];
            Finds(sim, "soilFertility", new SimEvent[] { new CropHarvested(t, 84, "potato", 1000, new YieldBreakdown()) }, new[] { Fixed.FromInt(40).Raw, potato.FertilityPerSeason.Raw });
            Finds(sim, "opportunityCost", new SimEvent[] { new DayLabourPaid(t, Fixed.FromInt(8), 5_000_00) });

            // Money.
            Finds(sim, "profitNotCash", new SimEvent[] { Closed(sim, 3, net: 4_000_00, opening: 50_000_00, closing: 10_000_00) }, new[] { 4_000_00L, -40_000_00L });
            Finds(sim, "interest", new SimEvent[] { Closed(sim, 3, account: Account.Interest, amount: -500_00, worth: new NetWorthSnapshot { LoanPrincipal = 20_000_00 }) }, new[] { 500_00L, 20_000_00L });
            Finds(sim, "creditScore", new SimEvent[] { new CreditScoreChanged(t, 650, 640, "missed") }, new[] { 650L, 640L });
            Finds(sim, "leverage", new SimEvent[] { new LoanTaken(t, 1, "coop_bank", 900_000_00, Fixed.Parse("0.025"), 8) });
            Ledger ledger = sim.State.Ledger;
            long cash = ledger.Cash;
            ledger.Post(t, sim.Period, "test", "test", new Posting(Account.Cash, -(cash - 100_00)), new Posting(Account.OpeningEquity, cash - 100_00));
            CodexFind liquid = CodexWatch.Rejected(sim, RejectReason.InsufficientFunds).Single();
            Assert.That((liquid.Id, liquid.Numbers[0]), Is.EqualTo(("liquidity", 100_00L)));
            Assert.That(liquid.Numbers[1], Is.GreaterThan(100_00L), "assets in hand");
            Assert.That(CodexWatch.Rejected(sim, RejectReason.EraLocked), Is.Empty, "only a refusal for money");
            Ok(sim, new FindCodexCards(t, new[] { liquid }));
            Assert.That(CodexWatch.Observe(sim, new SimEvent[] { new PriceIndexChanged(t, Fixed.One, Fixed.Parse("1.04")) }).Any(x => x.Id == "inflation"), Is.False, "under 5%");
            Finds(sim, "inflation", new SimEvent[] { new PriceIndexChanged(t, Fixed.One, Fixed.Parse("1.06")) }, new[] { Fixed.Parse("0.06").Raw });
            Business bakery = sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == "bakery");
            bakery.Ledger.Post(t, sim.Period, "test", "test", new Posting(Account.Cash, 7_000_00), new Posting(Account.Overdraft, -7_000_00));
            Finds(sim, "workingCapital", new SimEvent[] { new HourStarted(t) }, new[] { 7_000_00L, BusinessBooks.OverdraftLimit(sim, bakery) });

            // Business.
            Business dairy = sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == "dairy");
            dairy.Reports.Add(Report(10, 30_000_00, 5_000_00, 12_000_00));
            Finds(sim, "grossMargin", new SimEvent[] { Closed(sim, 10) }, new[] { 30_000_00L, 12_000_00L, Fixed.FromRatio(18, 30).Raw });
            dairy.Reports.Add(Report(11, 20_000_00, 5_000_00));
            Finds(sim, "fixedVariable", new SimEvent[] { Closed(sim, 11) }, new[] { 30_000_00L, 20_000_00L, 5_000_00L });
            Finds(sim, "breakEven", new SimEvent[] { new BusinessFounded(t, bakery.Id, "bakery", bakery.TileId, 1) }, new[] { bakery.Plan.Revenue, bakery.Plan.Costs });
            dairy.Reports.Add(Report(12, 20_000_00, 5_000_00));
            dairy.Reports.Add(Report(13, 20_000_00, 5_000_00));
            Finds(sim, "forecastActual", new SimEvent[] { Closed(sim, 13) });
            int pav = g.Market.IndexOf("pav");
            bakery.MadeToday[pav] = 1000;
            Finds(sim, "unitEconomics", new SimEvent[] { new BusinessWorked(t, bakery.Id, 0, 0) });

            // Assets.
            Finds(sim, "depreciation", new SimEvent[] { Closed(sim, 3, account: Account.Depreciation, amount: -300_00, worth: new NetWorthSnapshot { BuildingsAtBook = 9_000_00 }) }, new[] { 300_00L, 9_000_00L });
            Sim.Core.Land.Parcel bought = sim.State.Land.Parcels.First(p => !p.Owned && g.Map.Parcels[p.Id].ForSale);
            bought.Owned = true;
            bought.CostBasis = 1_00;
            Finds(sim, "appreciation", new SimEvent[] { Closed(sim, 3) }, new[] { 1_00L, Sim.Core.Land.LandRules.ParcelPrice(sim, bought) });
            Household home = sim.State.Citizens.Households.First(x => sim.State.Land.Buildings.Exists(b => b.Id == x.HomeId && b.Cost > 0));
            long houseCost = sim.State.Land.Buildings.First(b => b.Id == home.HomeId).Cost;
            Finds(sim, "yield", new SimEvent[] { new RentPaid(t, home.Id, 6_000_00) }, new[] { 6_000_00L, Fixed.FromRatio(6_000_00, houseCost).Raw });

            // People.
            Finds(sim, "hiringUncertainty", new SimEvent[] { new ReferenceChecked(t, 1, true) });
            var a = new Applicant { Id = 9_001, Skill = 50, Trait1 = 0, Trait2 = 0, Place = 0 };
            sim.State.People.Applicants.Add(a);
            Finds(sim, "marketWage", new SimEvent[] { new CounterOffer(t, a.Id, 40_000_00) }, new[] { HiringRules.OfferWage(sim, a, OfferLevel.Low), 40_000_00L });
            Worker w = sim.State.People.Workers.First();
            sim.State.People.HrEvents.Add(new HrEvent { Id = 9_002, Kind = HrEventKind.Poaching, WorkerId = w.Id, Amount = w.Wage + 5_000_00 });
            Finds(sim, "retention", new SimEvent[] { new HrEventRaised(t, 9_002, HrEventKind.Poaching, 0, w.Id) }, new[] { w.Wage, w.Wage + 5_000_00 });
            Finds(sim, "trainingRoi", new SimEvent[] { new CourseFinished(t, 0, w.Id, Fixed.FromInt(60)) }, new[] { 60L });
            Finds(sim, "employerReputation", new SimEvent[] { new ReputationChanged(t, 55, 5) }, new[] { 55L });

            // Public.
            ledger.Post(t, sim.Period, "test", "test", new Posting(Account.Cash, 500_000_00), new Posting(Account.OpeningEquity, -500_000_00));
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(sim.State.Citizens.School.ReadyAt);
            Finds(sim, "publicGoods", new SimEvent[] { new SchoolOpened(T(sim)) }, new[] { SchoolRules.State(sim).Cost, (long)SchoolRules.Pupils(sim).Count });
            Finds(sim, "longTermInvestment", new SimEvent[] { new CitizenLiterate(T(sim), 1, 0, LiterateHow.School) }, new[] { (long)g.School.PrimaryYears, SchoolRules.State(sim).Cost });
            Assert.That(CodexWatch.Observe(sim, new SimEvent[] { new CitizenLiterate(T(sim), 1, 0, LiterateHow.AdultClass) }), Is.Empty);

            Assert.That(sim.State.Guidance.Codex.Count, Is.EqualTo(25), "every card");
            ViewContext ctx = ViewKit2.Context(sim);
            for (int i = 0; i < g.Codex.Cards.Count; i++) // each card reads with the player's numbers, no placeholder left
            {
                WhyVm card = CodexSheet.Card(ctx, i);
                Assert.That(card.Rows.Select(r => r.Label), Has.None.Contains("{"), g.Codex.Cards[i].Id);
                Assert.That(card.Rows.Count, Is.EqualTo(5));
            }
        }

        // AC-6 (ADR 0095 §7): two or more cards in a pass get one toast that opens Menu → Codex with them marked New; one card's toast opens
        // it; nothing during the first session; found cards are saved and replay; reading takes the New tag off.
        [Test]
        public void Burst()
        {
            Simulation sim = ViewKit.NewGame();
            long t = T(sim);
            var moments = new SimEvent[] { new CreditScoreChanged(t, 650, 660, "paid"), new ReputationChanged(t, 52, 2), new PriceIndexChanged(t, Fixed.One, Fixed.Parse("1.07")) };
            Assert.That(Guide.FirstSessionActive(sim), Is.True);
            Assert.That(CodexWatch.Observe(sim, moments), Is.Empty, "never during the first session");
            Ok(sim, new SkipFirstSession(t));

            List<CodexFind> found = CodexWatch.Observe(sim, moments);
            Assert.That(found.Select(f => f.Id), Is.EqualTo(new[] { "creditScore", "inflation", "employerReputation" }), "in the Codex's order");
            ViewContext ctx = ViewKit2.Context(sim);
            CodexToastVm toast = CodexToastVm.Build(ctx, found);
            Assert.That((toast.Text, toast.Opens.Kind), Is.EqualTo(("3 new in your Codex", CodexSheet.Kind)));
            Ok(sim, CodexWatch.Command(t, found));
            Assert.That(sim.DrainEvents().OfType<CodexCardFound>().Count(), Is.EqualTo(3));
            Assert.That(sim.Execute(new FindCodexCards(t, new[] { new CodexFind("creditScore", new long[] { 1, 2 }) })).Reason, Is.EqualTo(RejectReason.CodexCardState), "once");
            Assert.That(sim.Execute(new FindCodexCards(t, new[] { new CodexFind("nope", new long[0]) })).Reason, Is.EqualTo(RejectReason.UnknownCodexCard));

            SheetVm codex = CodexSheet.Build(ctx);
            Assert.That(codex.Subtitle, Is.EqualTo("3 of 25 ideas found"));
            Assert.That(codex.Lines.Where(l => !l.IsHeader).Select(l => l.Label + "|" + l.Tag), Is.EqualTo(new[] { "Credit score|New", "Inflation|New", "Employer reputation|New" }));
            Assert.That(codex.Lines.Where(l => l.IsHeader).Select(l => l.Label), Is.EqualTo(new[] { "Money", "People" }), "unfound groups hidden");
            int inflation = sim.Config.Game.Codex.IndexOf("inflation");
            Assert.That(CodexSheet.Card(ctx, inflation).Rows[2].Label, Is.EqualTo("Prices in the region are now 7.0% above where your farm started."));
            Ok(sim, CodexSheet.Read(sim, inflation, t));
            Assert.That(CodexSheet.Read(sim, inflation, t), Is.Null, "read once");
            Assert.That(CodexSheet.Build(ctx).Lines.Single(l => l.Label == "Inflation").Tag, Is.Null);

            List<CodexFind> one = CodexWatch.Observe(sim, new SimEvent[] { new ReferenceChecked(t, 1, true) });
            CodexToastVm single = CodexToastVm.Build(ctx, one);
            Assert.That((single.Kicker, single.Text, single.Opens.Kind, single.Opens.Id), Is.EqualTo(("New in your Codex", "Hiring under uncertainty", CodexSheet.CardKind, (long)sim.Config.Game.Codex.IndexOf("hiringUncertainty"))));
            Ok(sim, CodexWatch.Command(t, one));

            LoadResult load = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 1_800_000_000), sim.Config);
            Assert.That(load.Ok, Is.True, load.Detail);
            Assert.That(load.State.Guidance.Codex.Select(f => (f.Id, f.Read, string.Join(",", f.Numbers))), Is.EqualTo(sim.State.Guidance.Codex.Select(f => (f.Id, f.Read, string.Join(",", f.Numbers)))));
            Assert.That(load.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Simulation replay = Simulation.Replay(sim.Config, sim.State.CommandLog.Entries.Select(e => e.Command).ToList());
            Assert.That(replay.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "found and read cards replay");
        }

        // Spec 018 §9: a player finds 12–18 of the cards by the Village, not a flood. The Village bot (homes, the mill, then the school;
        // with loans and saving) plays with the Codex watching every hour's events, as the game does; each card counts once.
        [Test]
        public void ByTheVillage()
        {
            var counts = new List<int>();
            foreach (bool saver in new[] { false, true })
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    int found = -1;
                    Tools.Balancer.Economy.RunBot(ViewKit.Config(), new Tools.Balancer.VillageBot(saver), 24, seed, sim =>
                    {
                        if (found >= 0) return;
                        List<CodexFind> cards = CodexWatch.Observe(sim, sim.PendingEvents);
                        if (cards.Count > 0) sim.Execute(new FindCodexCards(sim.State.Clock.SimTime, cards));
                        if (sim.PendingEvents.Any(e => e is EraUnlocked u && u.EraId == "village")) found = sim.State.Guidance.Codex.Count;
                    });
                    counts.Add(found);
                }
            TestContext.Out.WriteLine("Codex cards by the Village (loans, then saving): " + string.Join(" ", counts));
            Assert.That(counts, Has.None.EqualTo(-1), "every run reaches the Village");
            counts.Sort();
            Assert.That(counts[counts.Count / 2], Is.InRange(12, 18), "the median");
        }

        // Spec 018 §4: codex.json loads and is checked: known triggers once each, thresholds where they belong, known formats; every card has its words.
        [Test]
        public void Content()
        {
            string json = ViewKit.ReadContent("codex.json");
            ContentException Error(string edited) { return Assert.Throws<ContentException>(() => ContentLoader.LoadCodex(edited)); }
            Assert.That(Error(json.Replace("\"id\": \"inflation\"", "\"id\": \"inflation\", \"trigger\": \"creditScore\"")).FieldPath, Does.EndWith(".trigger"));
            Assert.That(Error(json.Replace("\"id\": \"leverage\",", "\"id\": \"leverage\", \"threshold\": 2,")).FieldPath, Does.EndWith(".threshold"));
            Assert.That(Error(json.Replace("\"numbers\": [\"percent\"]", "\"numbers\": [\"lakhs\"]")).FieldPath, Does.Contain("numbers"));
            Assert.That(Error(json.Replace("\"group\": \"public\"", "\"group\": \"civic\"")).FieldPath, Does.EndWith(".group"));
            CodexDefs d = ContentLoader.LoadCodex(json);
            var strings = ViewKit2.Context(ViewKit.NewGame()).Strings;
            foreach (CodexCardDef c in d.Cards)
                foreach (string part in new[] { "title", "text", "numbers", "world" })
                    Assert.That(strings.Has("codex." + c.Id + "." + part), Is.True, c.Id + "." + part);
            foreach (string group in d.Groups) Assert.That(strings.Has("codex.group." + group), Is.True, group);
        }
    }
}
