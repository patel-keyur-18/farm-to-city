using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-138 (spec 021 §7): the Town treasury sheet, giving, the town's books and a Why? on every number.</summary>
    public class TreasuryViewTests
    {
        private const long Coin = 100;

        // The Menu gains the treasury's row from the Township, and not before.
        [Test]
        public void MenuRow_FromTheTownship()
        {
            Simulation before = Situations.Town();
            Assert.That(MenuSheet.Build(ViewKit2.Context(before)).Lines.Any(l => l.Label == "Town treasury"), Is.False);
            Simulation sim = Situations.Township();
            LineVm row = MenuSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Town treasury");
            Assert.That(row.Action.Opens.Value.Kind, Is.EqualTo(TreasurySheet.Kind));
        }

        // AC-18 (the model): a banner saying whose money it is, the balance, whether the close is covered, two picture buttons, money in and out.
        [Test]
        public void Sheet()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = TreasurySheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Town treasury"));
            Assert.That(vm.Lines.First().Banner, Is.True);
            Assert.That(vm.Lines.First().Label, Does.StartWith("Town money: not yours"));
            Assert.That(vm.Lines.Single(l => l.Label == "Balance").Value, Is.EqualTo(ctx.Fmt.Coins(sim.State.Treasury.Cash)));
            Assert.That(vm.Lines.Single(l => l.Label == "At the close").Value, Is.EqualTo("Covered ✓"));
            LineVm buttons = vm.Lines.Single(l => l.Buttons != null);
            Assert.That(buttons.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Give to the town", "The town's books" }));
            Assert.That(vm.Lines.Single(l => l.Label == "Council's office · service 100%").Meter, Is.EqualTo(100));
            Assert.That(vm.Lines.Single(l => l.Label == "Health").Note, Is.EqualTo("Nothing to pay yet"));
            Assert.That(vm.Lines.Single(l => l.Label != null && l.Label.StartsWith("District grant, tied")).Value, Is.EqualTo("Paid when the clinic is staffed"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // A close that can't be covered says so, with the department that would go unpaid first.
        [Test]
        public void Sheet_Short()
        {
            Simulation sim = Situations.Township();
            TreasuryState t = sim.State.Treasury;
            var drain = new DrainTreasury();
            Assert.That(sim.Execute(drain).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = TreasurySheet.Build(ctx);
            LineVm banner = vm.Lines.Single(l => l.Banner && l.NoteTone == Tone.Warn);
            Assert.That(banner.Label, Does.StartWith("Short by"));
            Assert.That(vm.Lines.Single(l => l.Label == "At the close").Value, Does.StartWith("Short by"));
        }

        // AC-12: the works card says the town pays from its own money and shows the town's balance after; your cash isn't on it.
        [Test]
        public void WorksCard()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "treasury.json" ? j.Replace("\"works\": []", "\"works\": [ { \"id\": \"fixture\", \"cost\": 24000, \"lifeSeasons\": 4 } ]") : f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j,
                options: new SimOptions { DevCommandsEnabled = true });
            var eras = sim.Config.Game.Onboarding.Eras;
            sim.State.Era.Current = eras.ToList().FindIndex(e => e.Id == "village");
            Assert.That(sim.Execute(new DebugNextEra()).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "fixture"));
            Assert.That(card.Title, Is.EqualTo("Commission this work?"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Cost, from the town's money", "The town's balance after" }));
            Assert.That(card.CashChange, Is.EqualTo(0), "your cash isn't touched");
            Assert.That(card.Warning, Does.Contain("Your own cash isn't touched"));
        }

        // The gift sheet: an amount from the minimum to all your cash; below it the button is greyed with a reason; a valid amount's card shows the sim's own numbers.
        [Test]
        public void Give()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = TreasurySheet.Give(ctx);
            AmountVm amount = vm.Lines.First(l => l.Amount != null).Amount;
            Assert.That((amount.Min, amount.Max), Is.EqualTo((1_000 * Coin, sim.State.Ledger.Cash)));
            AmountPick low = amount.Pick(999 * Coin);
            Assert.That(low.Action.Enabled, Is.False);
            Assert.That(low.Note, Does.Contain("At least"));
            AmountPick ok = amount.Pick(5_000 * Coin);
            Assert.That(ok.Action.Enabled, Is.True);
            ConfirmCardVm card = ConfirmCards.For(ctx, ok.Action.Create(ctx.Now));
            Assert.That(card.Title, Is.EqualTo("Give to the town?"));
            Assert.That(card.CashChange, Is.EqualTo(-5_000 * Coin));
            Assert.That(card.Warning, Does.Contain("can't take it back"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Your gift", "The town's balance after", "Cash after" }));
        }

        // AC-13/AC-17 (the model): the books list the closed seasons; each statement's rows add up and sit beside last year's; every number has a Why?.
        [Test]
        public void Books_And_Statements()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm books = TreasurySheet.Books(ctx);
            TreasuryReport r = sim.State.Treasury.Reports.Last();
            LineVm season = books.Lines.First(l => l.Action != null);
            Assert.That(season.Action.Opens.Value.Id, Is.EqualTo(r.Period));
            foreach (string tab in new[] { TreasurySheet.InOut, TreasurySheet.CashFlow, TreasurySheet.Has })
            {
                SheetVm st = TreasurySheet.Statement(ctx, r.Period, tab);
                Assert.That(st.Lines, Is.Not.Empty, tab);
                foreach (LineVm l in st.Lines.Where(l => !l.IsHeader && l.Value != null))
                {
                    Assert.That(l.Note, Is.EqualTo("First year"), "no season a year ago yet: " + l.Label);
                    Assert.That(WhyView.Has(l.WhyKey), Is.True, tab + " / " + l.Label);
                    WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                    Assert.That(why.Rows, Is.Not.Empty, l.Label);
                    Assert.That(why.Rows.Any(w => w.Label.ToLowerInvariant().Contains("other")), Is.False, "never an Other row: " + l.Label);
                }
            }
            SheetVm inOut = TreasurySheet.Statement(ctx, r.Period, TreasurySheet.InOut);
            long moneyIn = -r.Balances.Where(kv => Accounts.GroupOf(kv.Key) == AccountGroup.Income).Sum(kv => kv.Value);
            Assert.That(inOut.Lines.First(l => l.IsHeader).Label, Is.EqualTo("Money in · " + ctx.Fmt.Coins(moneyIn)));
            // Cash flow: the town's work holds the gifts (its own row inside it); opening + the work = closing.
            SheetVm cash = TreasurySheet.Statement(ctx, r.Period, TreasurySheet.CashFlow);
            LineVm total = cash.Lines.Single(l => l.Label == "Money in less money out");
            Assert.That(cash.Lines.First(l => l.IsHeader).Label, Is.EqualTo("The town's work · " + total.Value));
            Assert.That(total.Value, Is.EqualTo(ctx.Fmt.Coins(r.Closing - r.Opening)), "no works built: the work is all the cash that moved");
            Assert.That(cash.Lines.Any(l => l.Label == "Of which gifts from you"), Is.True);
            // Itemised: the start-up grant's Why? lists the one movement behind it, once (the close's sweep to the fund balance isn't one).
            LineVm grant = inOut.Lines.First(l => l.Label == "District start-up grant");
            WhyVm gw = WhyView.Build(ctx, grant.WhyKey, grant.WhyId);
            Assert.That(gw.Rows.Count(x => x.Value == grant.Value), Is.EqualTo(1), "one grant, one row");
            // Each department's row is itemised: the account's movements in the Why?, adding up to its value.
            LineVm office = inOut.Lines.Single(l => l.Label == "Council's office");
            WhyVm w2 = WhyView.Build(ctx, office.WhyKey, office.WhyId);
            Assert.That(w2.Rows.Count(x => x.Value != null && Regex.IsMatch(x.Value, @"\d")), Is.GreaterThan(0));
        }

        // AC-17: every figure on the treasury's sheets has a Why? that is words, not a factor.
        [Test]
        public void EveryTreasuryNumberHasWhy()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            int checkedRows = 0;
            foreach (var named in WhyTests.AllSheets(ctx).Where(n => n.Key.StartsWith("treasury")))
                foreach (LineVm l in named.Value.Lines)
                {
                    if (l.IsHeader || l.Value == null || !Regex.IsMatch(l.Value, @"\d")) continue;
                    Assert.That(WhyView.Has(l.WhyKey), Is.True, named.Key + " / " + l.Label + " = " + l.Value);
                    WhyTests.AssertWords(named.Key + " / " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
                    checkedRows++;
                }
            Assert.That(checkedRows, Is.GreaterThan(12));
        }

        /// <summary>A test-only command that takes the town's cash away, so the next close can't be covered.</summary>
        internal sealed class DrainTreasury : Command
        {
            internal override CommandResult Apply(Simulation sim)
            {
                long cash = sim.State.Treasury.Ledger.Cash;
                sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.townWorks", "works:test",
                    new Posting(Account.Buildings, cash), new Posting(Account.Cash, -cash));
                return CommandResult.Ok;
            }
        }
    }
}
