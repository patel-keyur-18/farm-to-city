using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 016 §3.3, §7 (US-115): the Businesses sheet, research, the plan, placement and the business card.</summary>
    public class BusinessViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        // AC-14 (the plan shown; compared after 4 seasons in US-116): from the Menu to an open mill, every step in words.
        [Test]
        public void Founding_Flow()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(MenuSheet.Build(ctx).Lines.Select(l => l.Label), Does.Contain("Businesses"));
            SheetVm list = BusinessesSheet.Build(ctx);
            var buttons = list.Lines.First(l => l.Buttons != null).Buttons;
            ActionVm start = buttons[0];
            Assert.That((start.Label, start.Enabled), Is.EqualTo(("Start a flour mill", true)));
            Assert.That(buttons.Skip(1).Select(b => (b.Label, b.Enabled, b.DisabledReason)),
                Is.EqualTo(new[] { ("Start a bakery", false, "Comes with the Village"), ("Start a dairy", false, "Comes with the Village") }), "Village businesses greyed until then");
            Assert.That(list.Lines.Any(l => l.Label == "None yet"), Is.True);

            SheetVm research = BusinessResearchSheet.Build(ctx, 0);
            Assert.That(research.Lines.Single(l => l.Label == "Grain the area brings a day").Value, Does.StartWith("about 3,"));
            Assert.That(research.Lines.Single(l => l.Label == "Atta at the trader today").Value, Does.Contain("/kg"));
            SheetVm plan = BusinessPlanSheet.Build(ctx, 0, false);
            Assert.That(plan.Lines.Single(l => l.Label == "Who runs it").Value, Is.EqualTo("A hired miller"));
            BusinessRules.Projection p = BusinessRules.Projected(sim, sim.Config.Game.Businesses.Types[0], false, 200_000, Sim.Core.Maths.Fixed.FromInt(4));
            Assert.That(plan.Lines.Single(l => l.Label == "Profit").Value, Is.EqualTo(ctx.Fmt.Coins(p.Profit)));
            Assert.That(plan.Lines.Single(l => l.Label == "Cash to put in").Value, Is.EqualTo(ctx.Fmt.Coins(p.CashNeeded)));
            Assert.That(BusinessPlanSheet.Build(ctx, 0, true).Lines.Any(l => l.Label.StartsWith("The miller")), Is.False, "no wage when you run it");
            Assert.That(BusinessPlanSheet.Build(ctx, 0, true).Lines.Single(l => l.Label == "Your hours there").Value, Does.Match(@"^\d+ h a day: about .+ a season as day labour$"), "what your hours are worth");
            ActionVm place = plan.Actions.Single();
            Assert.That(place.Opens.Value.Kind, Is.EqualTo("BusinessPlacement"));

            int tile = BusinessPlacement.Places(ctx).First(x => x.Allowed).TileId;
            Assert.That(BusinessPlacement.ReasonFor(ctx, 86), Is.EqualTo("Something is growing here"));
            Ok(sim, BusinessPlacement.Confirm(ctx, tile, 0, false).Create(sim.State.Clock.SimTime));
            Business b = sim.State.Businesses.Businesses.Single();
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), tile).Title, Is.EqualTo("Flour mill"), "the acre opens its card");
            Assert.That(BusinessesSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Flour mill").Value, Does.StartWith("Being built"));
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400 + 2) * 86_400 + 12 * 3600);
            SheetVm card = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(card.Lines.Single(l => l.Label == "Who works here").Note, Does.StartWith("Nobody works here: hire someone on the board"), "spec 015: the stopgap is gone");
            Assert.That(card.Lines.Single(l => l.Label == "Who works here").Action.Opens.Value, Is.EqualTo(new SheetRequest("Word", b.Id)));
            Assert.That(card.Lines.Single(l => l.Label == "Run it yourself").Action.Enabled, Is.True);
            Ok(sim, new PostJob(sim.State.Clock.SimTime, b.Id, false));
            Ok(sim, new OfferContract(sim.State.Clock.SimTime, sim.State.People.ApplicantsFor(b.Id)[0].Id));
            card = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(card.Subtitle, Is.EqualTo("Open now · until 18:00"));
            Assert.That(card.Tabs.Select(t => t.Label), Is.EqualTo(new[] { "Today", "Books", "Settings" }));
            Assert.That(card.Lines.Single(l => l.Label == "Ground for the area today").Value, Does.Contain("in fees"));
            Assert.That(card.Lines.Single(l => l.Label == "Who works here").Action.Id, Is.EqualTo("letGo"));
            Assert.That(card.Lines.Single(l => l.Label == "Who works here").Value, Is.EqualTo("Manoj (miller)").Or.EqualTo("Usha (miller)"), "the most skilled townsperson, hired from the board");

            SheetVm settings = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings);
            LineVm hours = settings.Lines.Single(l => l.Label == "Open");
            Assert.That(hours.Value, Is.EqualTo("8:00–18:00"));
            Ok(sim, hours.Action.Create(sim.State.Clock.SimTime));
            Assert.That((b.OpenHour, b.CloseHour), Is.EqualTo((8, 14)));
            Assert.That(BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings).Lines.Single(l => l.Label == "Open").Value, Is.EqualTo("8:00–14:00"));
        }

        // AC-14 and §3.2 (US-116): the Books tab's statements, a past season's report, cash in and out, and after 4 full seasons the plan
        // against the actual, line by line, with why they differ in words; every number with a Why?.
        [Test]
        public void Plan_VsActual()
        {
            Simulation sim = Situations.Mill();
            Business b = sim.State.Businesses.Businesses.Single();
            SheetVm books = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Books);
            Assert.That(books.Lines.Where(l => l.IsHeader).Select(l => l.Label), Is.EqualTo(new[]
                { "Profit and loss, this season so far", "Cash, this season so far", "What it owns and owes now", "Plan vs actual, a season" }));
            Assert.That(books.Lines.Single(l => l.Label == "After 4 full seasons").Value, Is.EqualTo("0 of 4 so far"));
            Assert.That(books.Lines.Single(l => l.Label == "Its worth to you").Value, Is.EqualTo(ViewKit2.Context(sim).Fmt.Coins(BusinessBooks.Equity(b))));

            // Cash out to you, back to its books.
            SheetVm take = BusinessCashSheet.Build(ViewKit2.Context(sim), b.Id, BusinessCashSheet.Take);
            long cash = b.Ledger.Cash;
            Ok(sim, take.Lines.First().Action.Create(sim.State.Clock.SimTime));
            Assert.That(b.Ledger.Cash, Is.EqualTo(cash - 5_000_00));
            Assert.That(take.Lines.First().Value, Is.EqualTo("Your capital back"));
            Ok(sim, BusinessCashSheet.Build(ViewKit2.Context(sim), b.Id, BusinessCashSheet.Put).Lines.First().Action.Create(sim.State.Clock.SimTime));
            Assert.That(b.Ledger.Cash, Is.EqualTo(cash));

            long season = sim.Clock.SecondsPerSeason;
            sim.AdvanceTo((sim.State.Clock.SimTime / season + 5) * season + 3600);
            ViewContext ctx = ViewKit2.Context(sim);
            books = BusinessSheet.Build(ctx, b.Id, BusinessSheet.Books);
            Assert.That(books.Lines.Count(l => l.Opens == null && l.Action?.Opens?.Kind == "BusinessReport"), Is.EqualTo(4), "the last 4 seasons, each opens its report");
            LineVm fees = books.Lines.Last(l => l.Label == "Grinding fees");
            Assert.That(fees.Value, Does.Contain(" · plan "));
            Assert.That(fees.Note, Is.Not.Null.And.Not.Empty);
            LineVm wages = books.Lines.Last(l => l.Label == "Wages");
            Assert.That(wages.Note, Is.EqualTo("Prices have moved since the plan").Or.EqualTo("As planned"));
            Assert.That(books.Lines.Last(l => l.Label == "Atta and bran sold").Note, Is.EqualTo("None of your wheat was milled: none in store, or set to 0 a day"), "this town has no wheat");
            LineVm profit = books.Lines.Last(l => l.Label == "Net profit");
            Assert.That(profit.Note, Does.Match("As planned|more than planned, most of it from|less than planned, most of it from"));
            Assert.That(BusinessCashSheet.Build(ctx, b.Id, BusinessCashSheet.Take).Subtitle, Does.Contain("keep"), "what the next close needs");

            var sheets = new[] { books, BusinessReportSheet.Build(ctx, b.Id, "1"), BusinessReportSheet.Build(ctx, b.Id, "4") };
            Assert.That(sheets[1].Title, Does.StartWith("Flour mill: "));
            foreach (SheetVm sheet in sheets)
                foreach (LineVm l in sheet.Lines.Where(l => !l.IsHeader))
                {
                    Assert.That(WhyView.Has(l.WhyKey), Is.True, sheet.Title + ": " + l.Label);
                    WhyTests.AssertWords(sheet.Title + ": " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
                }
            BusinessReport last = b.Reports.Last();
            Assert.That(sheets[1].Lines.Single(l => l.Label == "Cash at the end").Value, Is.EqualTo(ctx.Fmt.Coins(last.Closing)));
        }

        // §3.8, §3.10 (US-117): a plan with a business loan; the card's overdraft and loan; a short business's alert and note; closing
        // from its sheet, the figures in words.
        [Test]
        public void Loan_Trouble_Closing()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            var mill = sim.Config.Game.Businesses.Types[0];
            SheetVm plan = BusinessPlanSheet.Build(ctx, 0, false, true);
            long loan = BusinessBooks.MaxLoan(sim, mill);
            BusinessRules.Projection p = BusinessRules.Projected(sim, mill, false, mill.OwnPerDay, mill.Fee, loan);
            Assert.That(plan.Lines.Single(l => l.Label == "How you pay for it").Value, Is.EqualTo("A business loan of " + ctx.Fmt.Coins(loan)));
            Assert.That(plan.Lines.Single(l => l.Label == "Cash to put in").Value, Is.EqualTo(ctx.Fmt.Coins(p.CashNeeded - loan)), "the loan pays for part");
            Assert.That(plan.Lines.Single(l => l.Label == "Interest on the loan").Value, Is.EqualTo("−" + ctx.Fmt.Coins(p.Interest)));
            Assert.That(plan.Actions.Single().Opens.Value.Arg, Is.EqualTo("hire:loan"));
            Assert.That(BusinessPlanSheet.Build(ctx, 0, false).Lines.Single(l => l.Label == "How you pay for it").Action.Opens.Value.Arg, Is.EqualTo("hire:loan"));

            int tile = BusinessPlacement.Places(ctx).First(x => x.Allowed).TileId;
            Ok(sim, BusinessPlacement.Confirm(ctx, tile, 0, false, true).Create(sim.State.Clock.SimTime));
            Business b = sim.State.Businesses.Businesses.Single();
            Assert.That(BusinessBooks.LoanOf(sim, b).Principal, Is.EqualTo(loan));
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400 + 2) * 86_400 + 12 * 3600);
            ctx = ViewKit2.Context(sim);
            SheetVm card = BusinessSheet.Build(ctx, b.Id);
            Assert.That(card.Lines.Single(l => l.Label == "Overdraft").Value, Is.EqualTo("0 coins of " + ctx.Fmt.Coins(BusinessBooks.OverdraftLimit(sim, b))));
            Assert.That(card.Lines.Single(l => l.Label == "Business loan").Value, Does.StartWith("Owes "));
            Assert.That(MoneySheet.Build(ctx).Lines.Any(l => l.Label == "Cooperative bank, for the flour mill"), Is.True, "your guarantee, on Money and loans");

            // Nobody working: the alert and the card say what to do.
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.None), "nobody hired from the board yet");
            ctx = ViewKit2.Context(sim);
            Assert.That(Alerts.Build(ctx).Any(a => a.Text == "Nobody works at the flour mill: hire someone, or run it yourself" && a.Action.Opens.Value.Kind == "Business"), Is.True);
            Assert.That(BusinessSheet.Build(ctx, b.Id).Lines.Single(l => l.Label == "The mill's cash").Note, Is.Null);
            Assert.That(BusinessSheet.Build(ctx, b.Id).Lines.Single(l => l.Label == "Who works here").Value, Is.EqualTo("Nobody yet"));

            SheetVm settings = BusinessSheet.Build(ctx, b.Id, BusinessSheet.Settings);
            Assert.That(settings.Lines.Single(l => l.Label == "Close the flour mill").Action.Opens.Value.Kind, Is.EqualTo("BusinessClose"));
            SheetVm close = BusinessCloseSheet.Build(ctx, b.Id);
            BusinessCloseSheet.Figures(sim, b, out _, out long sale, out long debts, out long net);
            Assert.That(close.Lines.Single(l => l.Label == "Building and equipment, at 60% of book value").Value, Is.EqualTo(ctx.Fmt.Coins(sale)));
            Assert.That(close.Lines.Single(l => l.Label == "Its overdraft and loan, repaid").Value, Is.EqualTo("−" + ctx.Fmt.Coins(debts)));
            Assert.That(close.Lines.Any(l => l.Label == (net >= 0 ? "Comes to you" : "Still owed: your bank loan over 4 seasons")), Is.True);
            long before = sim.State.Ledger.Cash;
            Ok(sim, close.Actions.Single().Create(sim.State.Clock.SimTime));
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed));
            if (net >= 0) Assert.That(sim.State.Ledger.Cash - before, Is.EqualTo(net).Within(100), "what the sheet said came to you");
            Assert.That(BusinessesSheet.Build(ViewKit2.Context(sim)).Lines.Any(l => l.Label == "None open"), Is.True);
        }
    }
}
