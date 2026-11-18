using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-146 (spec 023 §3.3, ADR 0116): the council hall's Roads section, the petition's card and loan sheet, and every number's Why?.</summary>
    public class RoadViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        /// <summary>A Village town; <paramref name="households"/> households of yours in all (empty ones fill the count).</summary>
        private static Simulation Village(int households)
        {
            Simulation sim = Situations.Town();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            CitizensState town = sim.State.Citizens;
            while (town.Households.Count < households) town.Households.Add(new Household { Id = town.NextHouseholdId++, HomeId = 0 });
            return sim;
        }

        private static SheetVm Hall(Simulation sim)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = sim.Config.Game.Map.Places.First(p => p.Kind == "council_hall").Tile;
            return FieldSheets.ForTile(ctx, tile);
        }

        // Before the Village the hall has no Roads section; after it, with too few households, the petition is greyed with the sim's reason.
        [Test]
        public void Locked()
        {
            Simulation hamlet = Situations.Town();
            var eras = hamlet.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") hamlet.State.Era.Current = i;
            Assert.That(Hall(hamlet).Lines.Any(l => l.WhyKey == "road"), Is.False, "the Hamlet's hall has no petition");

            Simulation sim = Village(3);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = Hall(sim);
            LineVm[] rows = vm.Lines.Where(l => l.WhyKey == "road").ToArray();
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "Paved link road", "Households of yours", "The community share, a gift to the village", "The district builds it in", "The trader pays at your farm gate" }));
            Assert.That(vm.Subtitle, Does.Contain("district builds roads"), "a village now, not a hamlet");
            Assert.That(rows[0].Value, Is.EqualTo("Not yet"));
            Assert.That(rows[1].Value, Is.EqualTo("3 of 10"));
            Assert.That(rows[3].Value, Is.EqualTo("4 days"));
            Assert.That(rows[4].Value, Is.EqualTo("+0.50 /kg"));
            ActionVm petition = vm.Actions.Single(a => a.Id == "petitionRoad");
            Assert.That(petition.Enabled, Is.False);
            Assert.That(petition.DisabledReason, Does.Contain("households"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // The petition: enabled with 10 households; its card shows the cash now, the share, the days and the cash after, and says the road isn't yours.
        [Test]
        public void Card()
        {
            Simulation sim = Village(12);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 5_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -5_000_000));
            SheetVm vm = Hall(sim);
            Assert.That(vm.Actions.Single(a => a.Id == "petitionRoad").Enabled, Is.True);
            Assert.That(vm.Actions.Single(a => a.Id == "roadLoan").Opens.Value.Kind, Is.EqualTo(RoadSheet.LoanKind));
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new PetitionRoad(ctx.Now));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Hold, Is.False);
            Assert.That(card.Title, Is.EqualTo("Petition for the road?"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Your cash now", "The community share", "The district builds it in", "Cash after" }));
            Assert.That(card.Rows[2].Value, Is.EqualTo("4 days"));
            Assert.That(card.Warning, Does.Contain("not yours"));
            Assert.That(card.CashChange, Is.EqualTo(-RoadRules.Price(sim)));
        }

        // While it is built the hall says when; once built it says what the road earned you, which the season page repeats.
        [Test]
        public void BuildingAndBuilt()
        {
            Simulation sim = Village(12);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 5_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -5_000_000));
            Ok(sim, new PetitionRoad(sim.State.Clock.SimTime));
            LineVm[] rows = Hall(sim).Lines.Where(l => l.WhyKey == "road").ToArray();
            Assert.That(rows[0].Value, Is.EqualTo("Being built"));
            Assert.That(rows[1].Label, Is.EqualTo("Ready in"));
            Assert.That(Hall(sim).Actions.Any(a => a.Id == "petitionRoad"), Is.False, "nothing to press while it is built");

            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400);
            rows = Hall(sim).Lines.Where(l => l.WhyKey == "road").ToArray();
            Assert.That(rows[0].Value, Is.EqualTo("Built"));
            Assert.That(rows[1].Label, Is.EqualTo("This season it earned you about"));
            Assert.That(rows[1].Value, Is.EqualTo("0 coins"));

            // A sale to the trader earns the road its saving: kilos × 0.50 × the price index.
            int wheat = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "wheat");
            sim.State.Market.Stock[wheat] = 1_000_000;
            long before = RoadRules.State(sim).EarnedThis;
            Sim.Core.Market.Store.SellToTrader(sim, wheat, 100_000, "test"); // 100 kg
            long earned = RoadRules.State(sim).EarnedThis - before;
            Assert.That(earned, Is.EqualTo(Sim.Core.Market.MarketRules.Value(sim, wheat, 100_000, Sim.Core.Maths.Fixed.Parse("0.50") * sim.Config.Game.Market.Goods[wheat].WeightKg * sim.State.Market.PriceIndex)));
            Assert.That(earned, Is.GreaterThan(0));
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm season = MoneySheet.Build(ctx);
            Assert.That(season.Lines.Any(l => l.Label == "Of that, the paved road earned you about"), Is.True);
        }

        // AC-13: every row of the Roads section has a Why? with rows or words in each state, and a trader price names the road.
        [Test]
        public void EveryRowHasWhy()
        {
            foreach (int state in new[] { 0, 1, 2 })
            {
                Simulation sim = Village(12);
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 5_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -5_000_000));
                if (state > 0) Ok(sim, new PetitionRoad(sim.State.Clock.SimTime));
                if (state > 1) sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400);
                ViewContext ctx = ViewKit2.Context(sim);
                foreach (LineVm row in Hall(sim).Lines.Where(l => l.WhyKey == "road"))
                {
                    Assert.That(WhyView.Has(row.WhyKey), Is.True);
                    WhyVm why = WhyView.Build(ctx, row.WhyKey, row.WhyId);
                    Assert.That(why, Is.Not.Null, row.Label);
                    Assert.That(why.Title, Is.Not.Empty, row.Label);
                    Assert.That(why.Rows.Count, Is.GreaterThan(0), row.Label);
                }
                int wheat = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "wheat");
                WhyVm price = WhyView.Build(ctx, "traderPrice", wheat);
                Assert.That(string.Join(" ", price.Rows.Select(r => r.Label)), Does.Contain(state == 2 ? "paved" : "dirt track"), "the trader's price names the road");
            }
        }

        // The district road on the south edge is drawn paved once built; the lanes stay dirt.
        [Test]
        public void MapPaves()
        {
            Simulation sim = Village(12);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 5_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -5_000_000));
            Assert.That(MapView.Build(ViewKit2.Context(sim)).Tiles.Any(t => t.Paved), Is.False);
            Ok(sim, new PetitionRoad(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400);
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.RoadPaved, Is.True);
            var paved = map.Tiles.Where(t => t.Paved).ToList();
            Assert.That(paved.Count, Is.EqualTo(map.Width + 2), "the district road across the whole map (24 acres, row 14) and the link road's two acres to the highway");
            Assert.That(paved.All(t => t.Kind == TileKind.Road), Is.True);
            Assert.That(paved.Count(t => t.Y == 14), Is.EqualTo(map.Width));
            Assert.That(map.Tiles.Where(t => t.Kind == TileKind.Road && !paved.Contains(t)).All(t => !t.Paved), Is.True, "the village lane stays dirt");
        }
    }
}
