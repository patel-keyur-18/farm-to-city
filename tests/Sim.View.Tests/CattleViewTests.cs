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

        private static Simulation Hamlet(out ViewContext ctx)
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
            SowSheetVm sow = SowSheet.Build(ctx, 84);
            CropRowVm berseem = sow.Rows.Single(r => r.CropId == "berseem");
            Assert.That(berseem.HarvestText, Does.StartWith("About").And.EndWith("of green fodder a day"));
            Assert.That(berseem.GetText, Does.StartWith("Feeds about"));

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
