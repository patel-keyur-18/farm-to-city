using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-17 (ADR 0043 §4–§7): the panels list one row per choice with the command it sends.</summary>
    public class PanelTests
    {
        private static T Cmd<T>(ActionVm a, Simulation sim) where T : Command { return (T)a.Create(sim.State.Clock.SimTime); }

        [Test]
        public void Choices()
        {
            Simulation sim = Situations.Ready();
            ViewContext ctx = ViewKit2.Context(sim);

            // The Menu: a row per section, each opening its sheet.
            SheetVm menu = MenuSheet.Build(ctx);
            Assert.That(menu.Lines.Select(l => l.Action.Opens.Value.Kind), Is.EqualTo(new[] { "Store", "Money", "People", "Lands", "Founder", "Settings" }));

            // Borrowing: amounts between the minimum and the limit, in whole thousands; terms with their total interest.
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                LenderDef l = sim.Config.Game.Credit.Lenders[i];
                long limit = CreditRules.Limit(sim, i).Limit;
                foreach (long a in BorrowSheet.Amounts(sim, i, limit))
                {
                    Assert.That(a, Is.GreaterThanOrEqualTo(l.MinAmount).And.LessThanOrEqualTo(limit));
                    Assert.That(a % (1000 * Money.Coin), Is.EqualTo(0));
                    foreach (int term in BorrowSheet.Terms(l))
                    {
                        LoanPlanVm plan = BorrowSheet.Plan(sim, i, a, term);
                        long principalBack = plan.Payments.Sum() - plan.TotalInterest;
                        Assert.That(principalBack, Is.EqualTo(a), "the payments repay the loan exactly");
                        Assert.That(plan.TotalInterest, Is.GreaterThan(0));
                    }
                }
            }
            Assert.That(BorrowSheet.Terms(sim.Config.Game.Credit.Lenders[0]), Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(BorrowSheet.Terms(sim.Config.Game.Credit.Lenders[1]), Is.EqualTo(new[] { 2, 4, 8 }));
            SheetVm terms = BorrowSheet.Build(ctx, 0, (20_000 * Money.Coin).ToString());
            TakeLoan take = Cmd<TakeLoan>(terms.Lines[1].Action, sim);
            Assert.That((take.LenderId, take.Amount, take.TermSeasons), Is.EqualTo(("moneylender", 20_000 * Money.Coin, 2)));

            // Just after borrowing, the next payment is the first close's interest (a part season: interest only), as the plan said.
            Assert.That(sim.Execute(take).Accepted, Is.True);
            Loan loan = sim.State.Credit.Loans.Last();
            Assert.That(Repay.NextPayment(sim, loan), Is.EqualTo(BorrowSheet.Plan(sim, 0, 20_000 * Money.Coin, 2).Payments[0]).Within(1));
            Assert.That(Repay.NextPayment(sim, loan), Is.GreaterThan(0));

            // Repay all of it from a sheet built a while ago (the screen keeps an unchanged sheet): the payment is what's owed when
            // pressed, so the loan closes and Money and loans says nothing is owed (owner, Sprint 7 device run).
            SheetVm repay = Repay.Build(ctx, loan.Id);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(sim.Execute(Cmd<RepayLoan>(repay.Lines[1].Action, sim)).Accepted, Is.True);
            Assert.That(loan.IsOpen, Is.False, "repaid in full");
            SheetVm money = MoneySheet.Build(ctx);
            Assert.That(money.Lines.Any(l => l.Action != null && l.Action.Id == "repay:" + loan.Id), Is.False);
            Assert.That(money.Lines.Select(l => l.Label), Does.Contain("No loans"));

            // Net worth is mostly land at today's price: the land shows on its own row under it, so the big number explains itself.
            int worth = money.Lines.FindIndex(l => l.WhyKey == "netWorth");
            Assert.That(money.Lines[worth + 1].Label, Is.EqualTo("Of that, your land"));
            Assert.That(money.Lines[worth + 1].Value, Is.EqualTo(ctx.Fmt.Coins(Reporting.NetWorth(sim).LandAtMarket)));

            // A longer term costs more interest in all (the lesson).
            long amount = 20_000 * Money.Coin;
            Assert.That(BorrowSheet.Plan(sim, 0, amount, 4).TotalInterest, Is.GreaterThan(BorrowSheet.Plan(sim, 0, amount, 1).TotalInterest));

            // Buying with a bank loan needs land you could buy: it shares Buy's reason unless only cash is short.
            int checkedLand = 0;
            foreach (var tile in sim.State.Land.Tiles.Where(t => !sim.State.Land.Parcels[t.ParcelId].Owned))
            {
                SheetVm land = FieldSheets.ForTile(ctx, tile.Id);
                ActionVm buy = land.Actions.Single(x => x.Id == "buy"), viaLoan = land.Actions.Single(x => x.Id == "buyLoan");
                if (!buy.Enabled && buy.DisabledReason != ctx.Reject(RejectReason.InsufficientFunds))
                {
                    Assert.That(viaLoan.Enabled, Is.False);
                    Assert.That(viaLoan.DisabledReason, Is.EqualTo(buy.DisabledReason));
                    checkedLand++;
                }
            }
            Assert.That(checkedLand, Is.GreaterThan(0), "some land is not next to the farm");

            // The founder: four plans, four hour options, four day-labour shares, the switch and medicine.
            SheetVm f = FounderSheet.Build(ctx);
            Assert.That(f.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("plan:")).Count(), Is.EqualTo(3), "three plans to choose");
            Assert.That(f.Lines.Single(l => l.Label == "Bare minimum").Action, Is.Null, "subsistence is never chosen (ADR 0034), only explained");
            Assert.That(f.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("hours:")).Count(), Is.EqualTo(4));
            Assert.That(f.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("share:")).Count(), Is.EqualTo(4));
            ActionVm chosen = f.Lines.Single(l => l.Action != null && l.Action.Id == "share:25").Action;
            Assert.That(chosen.Enabled, Is.False, "the current share is shown as chosen");
            Assert.That(Cmd<SetLabourSplit>(f.Lines.Single(l => l.Action != null && l.Action.Id == "share:50").Action, sim).SharePercent, Is.EqualTo(50));
            Assert.That(Cmd<SetEatFromStore>(f.Lines.Single(l => l.Action != null && l.Action.Id == "eat").Action, sim).On, Is.False);

            // A good: offer a part of what's free, then the lowest price.
            int radish = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "potato");
            if (sim.State.Market.InStore(radish) == 0) radish = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "radish");
            SheetVm parts = GoodSheet.Offer(ctx, radish, null);
            string q = parts.Lines[0].Action.Opens.Value.Arg;
            SheetVm prices = GoodSheet.Offer(ctx, radish, q);
            Offer offer = Cmd<Offer>(prices.Lines[1].Action, sim);
            Assert.That(offer.MinPrice, Is.EqualTo(Sim.Core.Market.MarketRules.Floor(sim, radish)));
            Assert.That(Cmd<Offer>(prices.Lines[0].Action, sim).MinPrice, Is.Null);

            // Inputs: seed in acres, a Buy per amount.
            int seedPotato = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "seed_potato");
            SheetVm seed = BuyInputsSheet.One(ctx, seedPotato);
            Assert.That(seed.Lines.Select(l => l.Label), Is.EqualTo(new[] { "To sow 1 acre", "To sow 2 acres", "To sow 6 acres" }));
            Assert.That(Cmd<Buy>(seed.Lines[0].Action, sim).Quantity, Is.EqualTo(Sim.Core.Farm.FarmRules.CycleGrams(sim.Clock, sim.Config.Game.Farm.Crops.First(c => c.Id == "potato"),
                sim.Config.Game.Farm.Crops.First(c => c.Id == "potato").SeedPerAcrePerSeason)));

            // The mandi: a load per good, and Go sends exactly those loads.
            int g0 = (int)MandiSheet.Build(ctx, null).Lines.First(l => l.Action != null).Action.Opens.Value.Id;
            SheetVm load = MandiSheet.Load(ctx, g0, null);
            SheetVm withLoad = MandiSheet.Build(ctx, load.Lines.Last().Action.Opens.Value.Arg);
            GoToMandi go = Cmd<GoToMandi>(withLoad.Actions.Single(), sim);
            Assert.That(go.Loads.Count, Is.EqualTo(1));
            Assert.That(go.Loads[0].GoodId, Is.EqualTo(sim.Config.Game.Market.Goods[g0].Id));
        }
    }
}
