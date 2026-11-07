using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 012 §7 on screen (US-205): the cattle acre's sheet through the Hamlet, the shed, the buy card, an animal's card, fodder.</summary>
    public class CattleViewTests
    {
        private const int Acre = 104;

        internal static Simulation Hamlet(out ViewContext ctx)
        {
            Simulation sim = ViewKit.NewGame();
            sim.State.Era.Current = sim.Config.Game.Livestock.UnlockEra;
            sim.State.Ledger.Post(0, sim.Period, "ledger.opening", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 10_00_000_00),
                new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OpeningEquity, -10_00_000_00));
            ctx = ViewKit2.Context(sim);
            return sim;
        }

        /// <summary>Every row with a number has a Why? that builds (ADR 0043 §2).</summary>
        private static void WhysBuild(ViewContext ctx, SheetVm vm)
        {
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.WhyKey);
                Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Title, Is.Not.Empty, l.WhyKey + " " + l.WhyId);
            }
        }

        // ADR 0088 (US-234): the shed sheet offers a second shed; it is placed on lit acres with refusals in words; its field becomes a
        // cattle acre on the map and opens the shed sheet; the animals show by shed; the stores add up.
        [Test]
        public void SecondShed()
        {
            Simulation sim = Hamlet(out ViewContext ctx);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildShed(t)).Accepted, Is.True);
            Assert.That(ShedPlacement.Places(ViewKit2.Context(sim)).Any(p => p.Allowed), Is.False, "not while the first is being built");
            sim.AdvanceTo(t + 25 * 3600);
            t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new Sow(t, 101, "radish", Sim.Core.Farm.SeedSource.Certified, "none")).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);

            SheetVm sheet = FieldSheets.ForTile(ctx, Acre);
            Assert.That(sheet.Lines.Any(l => l.IsHeader && l.Label == "A second shed"), Is.True);
            Assert.That(sheet.Lines.Single(l => l.Label == "Room for more").Value, Is.EqualTo("4 more animals · 2,400 kg more feed · 10,000 kg more straw"));
            ActionVm build = sheet.Lines.Single(l => l.Action?.Id == "secondShed").Action;
            Assert.That((build.Enabled, build.Label, build.Opens.Value.Kind), Is.EqualTo((true, "Choose the field", "ShedPlacement")));
            var places = ShedPlacement.Places(ctx);
            Assert.That(places.Single(p => p.TileId == 100).Allowed, Is.True);
            Assert.That(places.Single(p => p.TileId == 101).Reason, Is.EqualTo("Something is growing here"));
            Assert.That(places.Any(p => p.TileId == Acre), Is.False, "the cattle acre isn't a field");
            Assert.That(ShedPlacement.ReasonFor(ctx, 87), Is.EqualTo(ctx.Reject(RejectReason.TerrainNotAllowed)));

            Assert.That(sim.Execute(ShedPlacement.Confirm(ctx, 100).Create(t)).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);
            MapVm map = MapView.Build(ctx);
            Assert.That((map.Tiles[100].Kind, map.SecondCattle.TileId, map.SecondCattle.Shed), Is.EqualTo((TileKind.Cattle, 100, 1)));
            Assert.That(FieldSheets.ForTile(ctx, 100).Lines.Single(l => l.Label == "Your family is building it: ready in").Value, Is.Not.Empty);
            sim.AdvanceTo(t + 25 * 3600);
            t = sim.State.Clock.SimTime;
            foreach (string k in new[] { "buffalo", "cow", "buffalo", "cow", "cow" }) Assert.That(sim.Execute(new BuyAnimal(t, k)).Accepted, Is.True, k);
            ctx = ViewKit2.Context(sim);

            SheetVm both = FieldSheets.ForTile(ctx, 100);
            Assert.That((both.Title, both.Subtitle), Is.EqualTo(("Your cattle sheds", "5 of 8 stalls")));
            Assert.That(both.Lines.Where(l => l.IsHeader).Select(l => l.Label), Does.Contain("In the cattle yard's shed · 4 of 4").And.Contain("In the second shed · 1 of 4"));
            Assert.That(both.Lines.Any(l => l.Label == "A second shed"), Is.False);
            Assert.That(both.Lines.Single(l => l.Label == "Straw stack").Value, Does.Contain(" of 20,000 kg"));
            WhysBuild(ctx, both);
            map = MapView.Build(ctx);
            Assert.That(map.Cattle.Animals.Count, Is.EqualTo(4));
            Assert.That(map.SecondCattle.Animals.Select(a => (a.Kind, a.Place)), Is.EqualTo(new[] { ("cow", 1) }));
            Assert.That(map.SecondCattle.Shed, Is.EqualTo(2));
        }

        [Test]
        public void ShedSheet_ThroughTheHamlet()
        {
            // Before the Hamlet: what the acre is for, and the compost pit.
            Simulation sim = ViewKit.NewGame();
            SheetVm locked = FieldSheets.ForTile(ViewKit2.Context(sim), Acre);
            Assert.That((locked.Title, locked.Actions.Count), Is.EqualTo(("Your cattle yard", 0)));
            Assert.That(locked.Lines.Single().Label, Is.EqualTo("Compost pit"));

            // A Hamlet: the plan and Build the shed.
            sim = Hamlet(out ViewContext ctx);
            SheetVm plan = FieldSheets.ForTile(ctx, Acre);
            Assert.That(plan.Lines.Select(l => l.Label), Is.EqualTo(new[] { "A cattle shed", "Room", "Feed room and straw stack", "Building it takes", "Compost pit" }));
            ActionVm build = plan.Actions.Single();
            Assert.That((build.Label, build.Enabled), Is.EqualTo(("Build the shed", true)));
            WhysBuild(ctx, plan);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(build.Create(t)).Accepted, Is.True);
            Assert.That(FieldSheets.ForTile(ctx, Acre).Lines.Select(l => l.Label), Is.EqualTo(new[] { "Ready in", "Compost pit" }));

            // Built: the shed sheet with no animals, a buy button for each, the feeds with Fill.
            sim.AdvanceTo(t + 25 * 3600);
            SheetVm shed = FieldSheets.ForTile(ctx, Acre);
            Assert.That((shed.Title, shed.Subtitle), Is.EqualTo(("Your cattle shed", "0 of 4 stalls")));
            Assert.That(shed.Lines.Single(l => l.Buttons != null).Buttons.Select(b => (b.Label, b.Opens.Value.Kind)),
                Is.EqualTo(new[] { ("Buy a buffalo", "BuyAnimal"), ("Buy a cow", "BuyAnimal") }));
            Assert.That(shed.Lines.Where(l => l.Action?.Label == "Fill").Select(l => l.Label), Is.EqualTo(new[] { "Straw stack", "Feed room" }));
            WhysBuild(ctx, shed);
        }

        [Test]
        public void BuyCard_AnimalCard_AndSell()
        {
            Simulation sim = Hamlet(out ViewContext ctx);
            long t = sim.State.Clock.SimTime;
            sim.Execute(new BuildShed(t));
            sim.AdvanceTo(t + 25 * 3600);
            t = sim.State.Clock.SimTime;

            // The buy card: price, milk, the co-op's price, ration, feed bought vs home-grown, work, payback; the shed's stock with Fill.
            SheetVm card = AnimalBuySheet.Build(ctx, 0);
            Assert.That(card.Title, Is.EqualTo("Buy a buffalo"));
            Assert.That(card.Lines.Where(l => !l.IsHeader).Select(l => l.Label), Is.EqualTo(new[]
                { "Price", "Milk", "Fat and the co-op's price", "She eats a day", "Her feed a day", "Work", "Pays for herself in", "Straw stack", "Feed room" }));
            Assert.That(card.Lines.First(l => l.Label == "Pays for herself in").Value, Does.StartWith("About"), "a buffalo pays her way");
            WhysBuild(ctx, card);
            ActionVm buy = card.Actions.First();
            Assert.That(buy.Enabled, Is.True);
            Assert.That(sim.Execute(buy.Create(t)).Accepted, Is.True);
            Assert.That(card.Actions.Any(a => a.Id == "buyLoan"), Is.True, "the dairy loan");
            // Spec 012 §3.7: a first animal into an empty feed room is flagged at once, with Fill.
            AlertVm low = Alerts.Build(ctx).First(a => a.Text.StartsWith("Straw for"));
            Assert.That((low.Level, low.Action.Opens.Value.Kind), Is.EqualTo((AlertLevel.Decision, "BuyInput")));

            // Her row on the shed opens her card; the card sells her at the fair after asking.
            SheetVm shed = FieldSheets.ForTile(ctx, Acre);
            LineVm row = shed.Lines.Single(l => l.Opens.HasValue && l.Opens.Value.Kind == "Animal");
            Assert.That(row.Value, Does.StartWith("In milk, day 1 of 13"));
            Assert.That(shed.Subtitle, Is.EqualTo("1 of 4 stalls"));
            SheetVm her = AnimalSheet.Build(ctx, row.Opens.Value.Id);
            Assert.That(her.Title, Is.EqualTo(row.Label));
            WhysBuild(ctx, her);
            ActionVm sell = her.Actions.Single();
            Assert.That((sell.Label, sell.Enabled, sell.Confirm != null), Is.EqualTo(("Sell at the fair", true, true)));

            // The next morning: milk on the shed and her row; the green split; the animals' work in Care's Why?.
            sim.AdvanceTo(t + 24 * 3600);
            shed = FieldSheets.ForTile(ctx, Acre);
            Assert.That(shed.Lines.First().Value, Does.Contain("the co-op paid"));
            Assert.That(shed.Lines.First(l => l.Label == "Green fodder").Value, Does.Contain("commons"));
            WhysBuild(ctx, shed);
            Assert.That(WhyView.Build(ctx, "care", 0).Rows.Any(r => r.Label == "Of that, your animals (1)"), Is.True);
            Assert.That(WhyView.Build(ctx, "netWorth", 0).Rows.Any(r => r.Label == "Animals"), Is.True);
        }

        [Test]
        public void Fodder_SowCard_Fill_AndStore()
        {
            Simulation sim = ViewKit.NewGame();
            Assert.That(SowSheet.Build(ViewKit2.Context(sim), 84).Rows.Any(r => r.CropId == "berseem"), Is.False, "fodder comes with cattle");
            sim = Hamlet(out ViewContext ctx);
            Assert.That(SowSheet.Build(ctx, 84).Rows.Any(r => r.CropId == "berseem"), Is.False, "a Winter crop: in Spring it's on the crop calendar (spec 020 G22)");
            sim.AdvanceTo(3 * sim.Clock.SecondsPerSeason + 9 * 3600L); // Winter
            SowSheetVm sow = SowSheet.Build(ctx, 84);
            CropRowVm berseem = sow.Rows.Single(r => r.CropId == "berseem");
            Assert.That(berseem.HarvestText, Does.StartWith("About").And.EndWith("of green fodder a day"));
            Assert.That(berseem.GetText, Does.StartWith("Feeds about"));
            Assert.That(berseem.PerHourText, Is.Null, "no hourly figure: its worth is the fodder it saves (spec 020 §3.6)");

            // Fill: straw and feed only with the shed, to their places.
            Assert.That(BuyInputsSheet.Build(ctx).Lines.Any(l => l.Label == "Straw"), Is.False);
            long t = sim.State.Clock.SimTime;
            sim.Execute(new BuildShed(t));
            sim.AdvanceTo(t + 25 * 3600);
            LineVm straw = BuyInputsSheet.Build(ctx).Lines.Single(l => l.Label == "Straw");
            Assert.That(straw.Note, Is.EqualTo("Goes to the straw stack"));
            int g = sim.Config.Game.Market.IndexOf("straw");
            Assert.That(BuyInputsSheet.Amounts(ctx, g).First().Key, Is.EqualTo("Fill it up"));
        }
    }
}
