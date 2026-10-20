using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    public class AlertTests
    {
        private const long Hour = 3600;

        private static bool Has(Simulation sim, string start) { return Alerts.Build(ViewKit2.Context(sim)).Any(a => a.Text.StartsWith(start)); }

        // Spec 008 AC-12.
        [Test]
        public void Conditions()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long t = sim.State.Clock.SimTime;
            Assert.That(Alerts.Build(ViewKit2.Context(sim)), Is.Empty, "a new game has nothing to say");

            // Ready to collect, then spoiling within 12 h of the 72 h grace.
            Assert.That(sim.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(t + 3 * Hour);
            Assert.That(Has(sim, "1 ready to collect"), Is.True);
            Assert.That(Has(sim, "Radish on"), Is.False);
            sim.AdvanceTo(t + 2 * Hour + 61 * Hour);
            Assert.That(Has(sim, "Radish on"), Is.True, "spoils within 12 h");
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).First(a => a.Text.StartsWith("Radish on")).Level, Is.EqualTo(AlertLevel.Decision));

            // The store over 90%.
            Simulation full = ViewKit.NewGame(seed: 5);
            Assert.That(Has(full, "The store is nearly full"), Is.False);
            full.State.Market.Stock[full.Config.Game.Market.IndexOf("wheat")] += Store.CapacityGrams(full) * 95 / 100 - Store.UsedGrams(full);
            Assert.That(Has(full, "The store is nearly full"), Is.True);

            // A pest warning until sprayed.
            Simulation pests = null;
            for (ulong seed = 1; seed < 400 && pests == null; seed++)
            {
                Simulation s = ViewKit.NewGame(seed: seed);
                s.Execute(new Sow(s.State.Clock.SimTime, 20, "potato", SeedSource.Certified, "none"));
                s.AdvanceTo(s.State.Clock.SimTime + 13 * Hour);
                if (s.State.Farm.CropOn(20).Outbreak != Outbreak.None) pests = s;
            }
            Assert.That(pests, Is.Not.Null, "no seed with an outbreak");
            Assert.That(Has(pests, "Pests on"), Is.True);
            Assert.That(pests.Execute(new Spray(pests.State.Clock.SimTime, 20)).Accepted, Is.True);
            Assert.That(Has(pests, "Pests on"), Is.False, "cleared by spraying");

            // Cash short on the season's last day.
            Simulation poor = ViewKit.NewGame(seed: 5, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 1000") : j);
            Assert.That(Has(poor, "Next season's living costs"), Is.False);
            poor.AdvanceTo(poor.Clock.SecondsPerSeason - 12 * Hour);
            Assert.That(Has(poor, "Next season's living costs"), Is.True);
            Assert.That(Alerts.Build(ViewKit2.Context(poor))[0].Level, Is.EqualTo(AlertLevel.Urgent), "urgent first");
        }
    }

    public class AwayViewTests
    {
        private const long Hour = 3600;

        // Spec 008 AC-8.
        [Test]
        public void Summary()
        {
            Simulation sim = Situations.Ready();
            ViewContext ctx = ViewKit2.Context(sim);
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 30 * Hour, AdvanceMode.Away);
            List<SimEvent> events = sim.DrainEvents().ToList();
            events.Add(new AwayAdvanceCompleted(sim.State.Clock.SimTime, sim.State.Clock.SimTime - 30 * Hour, sim.State.Clock.SimTime, 30 * Hour, 30 * Hour, false));
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.Title, Is.EqualTo("Away 1 d 6 h"));
            Assert.That(vm.DayLabour, Is.EqualTo(events.OfType<DayLabourPaid>().Sum(e => e.Amount)));
            Assert.That(vm.HomeUse, Is.EqualTo(events.OfType<HomeUseEaten>().Sum(e => e.Value)));
            Assert.That(vm.TraderSales + vm.MarketSales, Is.EqualTo(events.OfType<MarketSales>().Sum(e => e.Amount) + events.OfType<SoldToTrader>().Sum(e => e.Amount)));
            Assert.That(vm.SickDays, Is.EqualTo(events.OfType<FounderSick>().Count()));
            Assert.That(vm.Lines.Count, Is.LessThanOrEqualTo(ctx.Ui.AwayRowsMax));
            Assert.That(vm.Lines.All(l => l.WhyKey == "away"), Is.True);

            // Money rows are most valuable first.
            var money = vm.Lines.Where(l => l.Icon == "coin").Select(l => long.Parse(new string(l.Value.Where(char.IsDigit).ToArray()))).ToList();
            Assert.That(money, Is.Ordered.Descending);

            // Ready things that were already waiting are not news: an absence with nothing new brings no card.
            Assert.That(CollectAll.ReadyCount(sim), Is.GreaterThan(0));
            Assert.That(AwayCard.Build(ctx, new SimEvent[] { new AwayAdvanceCompleted(sim.State.Clock.SimTime, sim.State.Clock.SimTime - 600, sim.State.Clock.SimTime, 600, 600, false) }), Is.Null);

            // Nothing happened: no card.
            Assert.That(AwayCard.Build(ViewKit2.Context(ViewKit.NewGame()), new SimEvent[] { new AwayAdvanceCompleted(70, 10, 70, 60, 60, false) }), Is.Null);
        }

        // Spec 008 AC-11: what doesn't fit is offered to the trader for exactly what it would fetch.
        [Test]
        public void CollectAll_SellsTheRest()
        {
            Simulation full = ViewKit.NewGame(seed: 5);
            long t = full.State.Clock.SimTime;
            full.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none"));
            full.Execute(new Sow(t, 28, "radish", SeedSource.Certified, "none"));
            full.AdvanceTo(t + 30 * Hour);
            full.State.Market.Stock[full.Config.Game.Market.IndexOf("wheat")] += Store.CapacityGrams(full) - Store.UsedGrams(full) - 30_000;
            foreach (Command c in CollectAll.Commands(full, full.State.Clock.SimTime)) full.Execute(c);
            CollectRestVm rest = CollectAllRest.Build(full);
            Assert.That(rest, Is.Not.Null);
            long cash = full.State.Ledger.Cash;
            foreach (Command c in rest.Commands) Assert.That(full.Execute(c).Accepted, Is.True);
            Assert.That(full.State.Ledger.Cash - cash, Is.EqualTo(rest.Coins), "exactly the floor value");
            Assert.That(CollectAll.ReadyCount(full), Is.EqualTo(0));
            CardVm card = Cards.SellRest(ViewKit2.Context(full), rest);
            Assert.That(card.Text, Does.Contain(ViewKit2.Context(full).Fmt.Coins(rest.Coins)));
            Assert.That(CollectAllRest.Build(full), Is.Null);
        }
    }

    public class MotionTests
    {
        // Spec 008 AC-9.
        [Test]
        public void EveryEventMapped()
        {
            var types = typeof(SimEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(SimEvent)) && !t.IsAbstract).Select(t => t.Name).ToList();
            Assert.That(types.Count, Is.GreaterThan(50));
            Assert.That(types.Where(n => !EventMap.ByType.ContainsKey(n)), Is.Empty, "unmapped event types");
            Assert.That(EventMap.ByType.Keys.Where(k => !types.Contains(k)), Is.Empty, "no stale entries");
        }
    }
}
