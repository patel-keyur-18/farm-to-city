using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Market;
using Sim.Core.Saves;
using Sim.View.Views;

namespace Sim.View.Tests
{
    public partial class FirstSessionTests
    {
        private static List<string> ReadAll(Simulation sim, Guide guide)
        {
            var read = new List<string>();
            for (string id = GuideKit.ReadNext(sim, guide); id != null; id = GuideKit.ReadNext(sim, guide)) read.Add(id);
            return read;
        }

        // Spec 011 AC-17 (ADR 0058): firstOrders at the first 6:00 orders; truckAway when the Store opens with the truck away (or the
        // first TraderAway refusal); each once.
        [Test]
        public void TruckAndOrdersChapters()
        {
            Simulation sim = GuideKit.Skipped(out Guide guide); // 8:00, the truck is here
            ReadAll(sim, guide);
            guide.SheetVisited("Store", sim);
            Assert.That(guide.Next(sim), Is.Null, "the truck is here");

            GuideKit.Live(sim, guide, 24 * 3600 - 8 * 3600 + 1800); // 0:30
            Assert.That(sim.State.Market.TruckHere, Is.False);
            guide.SheetVisited("Store", sim);
            Assert.That(ReadAll(sim, guide).Count(id => id == "truckAway"), Is.EqualTo(1));
            guide.SheetVisited("Store", sim);
            Assert.That(guide.Next(sim), Is.Null, "once");

            GuideKit.Live(sim, guide, 6 * 3600); // past 6:00: the first orders
            Assert.That(ReadAll(sim, guide), Does.Contain("firstOrders"));
            GuideKit.Live(sim, guide, 24 * 3600);
            Assert.That(ReadAll(sim, guide), Does.Not.Contain("firstOrders"), "once");

            Simulation other = GuideKit.Skipped(out Guide g2);
            ReadAll(other, g2);
            g2.Rejected(other, new SellToTrader(other.State.Clock.SimTime, "wheat", 1000), RejectReason.TraderAway);
            Assert.That(ReadAll(other, g2), Is.EqualTo(new[] { "truckAway" }), "or the first refusal");
        }
    }

    /// <summary>Spec 011 §7: the Orders sheet, the Store's truck and owed rows, today's price, the away rows, the notification.</summary>
    public class OrdersViewTests
    {
        private static Simulation Morning(out ViewContext ctx)
        {
            Simulation sim = GuideKit.Skipped(out Guide guide);
            GuideKit.Live(sim, guide, 24 * 3600 - 8 * 3600 + 6 * 3600 + 60); // 6:01 next day
            ctx = ViewKit2.Context(sim);
            return sim;
        }

        [Test]
        public void OrdersSheet_FreshDeliverOwed()
        {
            Simulation sim = Morning(out ViewContext ctx);
            SheetVm sheet = OrdersSheet.Build(ctx);
            Assert.That(sheet.Subtitle, Is.EqualTo("Harvest today, deliver by midnight, paid at 6:00 tomorrow"));
            Assert.That(sim.State.Market.SupermarketOrders.Count, Is.EqualTo(3));
            // One row per order (owner's Sprint 11 device review): its state is on the same row, no second row.
            Assert.That(sheet.Lines.Count, Is.EqualTo(3));
            Assert.That(sheet.Lines.All(l => l.WhyKey == "orderPrice" && l.Value.StartsWith("Wants ") && l.Badge == null && l.Action == null), Is.True);
            Assert.That(sheet.Lines.Select(l => l.Note), Has.All.Match("^(Sow by \\d\\d:\\d\\d to harvest it today|Pick it from the garden today|Too late to sow for today)$"));
            Assert.That(HudView.Build(ctx).OrdersBadge, Is.EqualTo(3));
            Assert.That(HudView.Build(ctx).HasOrders, Is.True);

            SupermarketOrder o = sim.State.Market.SupermarketOrders[0];
            LineVm Row() { return OrdersSheet.Build(ctx).Lines.Single(l => l.WhyKey == "orderPrice" && l.WhyId == o.Id); }
            sim.State.Market.Stock[o.Good] += 5_000; // a fresh harvest (the store's own rule is in OrderTests)
            sim.State.Market.FreshToday[o.Good] += 5_000;
            LineVm fresh = Row();
            Assert.That(fresh.Note, Is.EqualTo("5.0 kg harvested today, ready to deliver"));
            Assert.That(fresh.Action.Id, Is.EqualTo("deliver:" + o.Id));
            Assert.That(fresh.Action.Enabled, Is.True);
            GuideKit.Ok(sim, fresh.Action.Create(sim.State.Clock.SimTime));
            Assert.That(Row().Note, Does.EndWith(" · 5.0 kg of " + ctx.Fmt.Kg(o.Amount) + " delivered"), "part delivered: on the same row");
            Assert.That(Row().Action, Is.Null);

            // The rest: the row says Delivered, with no button and nothing about sowing.
            sim.State.Market.Stock[o.Good] += o.Left;
            sim.State.Market.FreshToday[o.Good] += o.Left;
            GuideKit.Ok(sim, Row().Action.Create(sim.State.Clock.SimTime));
            LineVm done = Row();
            Assert.That(done.Badge, Is.EqualTo("Delivered"));
            Assert.That(done.Note, Is.EqualTo("All " + ctx.Fmt.Kg(o.Amount) + " delivered"));
            Assert.That(done.Action, Is.Null);
            Assert.That(HudView.Build(ctx).OrdersBadge, Is.EqualTo(2));
            LineVm owed = OrdersSheet.Build(ctx).Lines.Single(l => l.WhyKey == "owed");
            Assert.That(owed.Label + " " + owed.Value, Does.Match("^Supermarket pays you [\\d,]+ coins at 6:00 tomorrow$"));
            Assert.That(StoreSheet.Build(ctx).Lines.Any(l => l.WhyKey == "owed"), Is.True);
            Assert.That(MoneySheet.Build(ctx).Lines.Any(l => l.WhyKey == "owed"), Is.True);
            foreach (string key in new[] { "orderPrice", "owed", "truck" }) Assert.That(WhyView.Has(key), Is.True, key);
            Assert.That(WhyView.Build(ctx, "orderPrice", o.Id).Rows.Select(r => r.Label), Does.Contain("Supermarket premium"));
        }

        [Test]
        public void StoreAndGood_TruckStallAndToday()
        {
            Simulation sim = GuideKit.Skipped(out Guide guide);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(StoreSheet.Build(ctx).Lines.Single(l => l.WhyKey == "truck").Value, Is.EqualTo("Here till midnight · 4,000 kg room"));
            GuideKit.Live(sim, guide, 24 * 3600 - 8 * 3600 + 1800); // 0:30
            SheetVm store = StoreSheet.Build(ctx);
            Assert.That(store.Lines.Single(l => l.WhyKey == "truck").Value, Is.EqualTo("Back at 6:00"));
            Assert.That(store.Lines.Any(l => l.Label == "Stall closed till 6:00"), Is.True);
            ActionVm Orders(SheetVm sheet) { return sheet.Lines.Single(l => l.Buttons != null).Buttons.Single(b => b.Id == "orders"); }
            Assert.That(Orders(store).Detail, Is.EqualTo("Closed till 6:00"));
            Assert.That(Orders(store).Count, Is.Zero);
            GuideKit.Live(sim, guide, 6 * 3600);
            store = StoreSheet.Build(ctx);
            Assert.That(store.Lines.Any(l => l.Label == "Stall closed till 6:00"), Is.False);
            Assert.That(Orders(store).Label + " · " + Orders(store).Detail, Is.EqualTo("Supermarket orders · 3 to deliver"));
            Assert.That(Orders(store).Count, Is.EqualTo(3), "the picture button's red count");
            Assert.That(Orders(store).Opens.Value.Kind, Is.EqualTo("Orders"));
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            string Today() { return GoodSheet.Build(ctx, wheat).Lines.Single(l => l.WhyKey == "traderPrice").Value; }
            Assert.That(Today(), Does.Match("^[\\d.]+ /kg today$"), "the first morning: no yesterday yet");
            GuideKit.Live(sim, guide, 24 * 3600);
            Assert.That(Today(), Does.Match("^[\\d.]+ /kg today \\(yesterday [\\d.]+ /kg, (▲\\d+%|▼\\d+%|no change)\\)$"));
        }

        [Test]
        public void AwayCard_OrdersLapsed()
        {
            Simulation sim = Morning(out ViewContext ctx);
            GuideKit.Ok(sim, new Sync(sim.State.Clock.LastWallUtc + 30 * 3600));
            AwayCard.Vm card = AwayCard.Build(ctx, sim.LastAwayReport);
            Assert.That(card, Is.Not.Null);
            var rows = card.Lines.Concat(card.More).Where(l => l.Label.StartsWith("Supermarket order: ")).ToList();
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(rows.All(r => r.Value == "Ran out, nothing delivered"), Is.True);
        }

        [Test]
        public void Notification_NamesTheGoods()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            var n = new PlannedNotification { Kind = NotifyKind.OrdersPosted, Count = 1, CropId = "potato,radish,spinach" };
            (string title, string body) = NotificationText.For(ctx, n);
            Assert.That(title, Is.EqualTo("3 supermarket orders: potato, radish and spinach"));
            Assert.That(Regex.IsMatch(title + body, "\\d{2,}"), Is.False, "no amounts");
        }

        // Spec 011 §7: the truck's road runs from the district road's east end up the farm lane to the stop by the labour quarters.
        [Test]
        public void Map_TruckRouteAndVan()
        {
            Simulation sim = Morning(out ViewContext ctx);
            MapVm map = MapView.Build(ctx);
            Assert.That(map.TruckHere, Is.True);
            Assert.That(map.VanHere, Is.True, "today's orders are open");
            int w = map.Width;
            Assert.That(map.TruckRoute.First() % w, Is.EqualTo(w - 1), "in from the east edge");
            int stop = map.TruckRoute.Last();
            Assert.That(map.Tiles[stop].Kind, Is.EqualTo(TileKind.Road));
            Assert.That(map.Tiles.Where(t => t.Kind == TileKind.Quarters).Any(t => System.Math.Abs(t.X - stop % w) + System.Math.Abs(t.Y - stop / w) == 1), Is.True, "beside the quarters");
            for (int i = 1; i < map.TruckRoute.Count; i++)
            {
                int a = map.TruckRoute[i - 1], b = map.TruckRoute[i];
                Assert.That(System.Math.Abs(a % w - b % w) + System.Math.Abs(a / w - b / w), Is.EqualTo(1), "acre by acre");
                Assert.That(map.Tiles[b].Kind, Is.EqualTo(TileKind.Road));
            }
        }
    }
}
