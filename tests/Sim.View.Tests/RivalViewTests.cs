using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 017 §3.7, §7 (US-122): newcomers and the village's price cuts on the shop's card, the pitches, alerts, the away card and the map.</summary>
    public class RivalViewTests
    {
        /// <summary>The shops' town with a newcomer's chai cart on pitch III and a vegetable cart by the square, come this season.</summary>
        private static Simulation WithNewcomers()
        {
            Simulation sim = Situations.Shops();
            long t = sim.State.Clock.SimTime;
            sim.State.Shops.Newcomers.Add(new Rival { Def = -1, Id = "newcomer_chai", Kind = ShopKind.Chai, Pitch = 2, OpenedAt = t, PriceFactor = Fixed.One, Stars = Fixed.FromRatio(5, 2) });
            sim.State.Shops.Newcomers.Add(new Rival { Def = -1, Id = "newcomer_veg", Kind = ShopKind.Veg, Pitch = -1, OpenedAt = t, PriceFactor = Fixed.One, Stars = Fixed.FromRatio(5, 2), Gap = true });
            sim.AdvanceTo(t + 3600);
            return sim;
        }

        [Test]
        public void Newcomers()
        {
            Simulation sim = WithNewcomers();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = sim.State.Businesses.Businesses.Single(b => BusinessRules.TypeOf(sim, b).Id == "tea_stall");

            // The card's Customers: the newcomer beside the village's tea stall, with its share's Why? in words.
            SheetVm customers = BusinessSheet.Build(ctx, tea.Id, ShopSheet.Customers);
            LineVm row = customers.Lines.Single(l => l.Label == "A newcomer's chai cart, on Pitch III");
            Assert.That(row.Icon, Is.EqualTo("newcomer_chai"));
            WhyVm why = WhyView.Build(ctx, row.WhyKey, row.WhyId);
            WhyTests.AssertWords("newcomer share", why);
            Assert.That(why.Rows.Any(r => r.Label != null && r.Label.StartsWith("Someone from outside set up on a free pitch")), Is.True);
            LineVm village = customers.Lines.Single(l => l.Label == "The village tea stall");
            Assert.That(WhyView.Build(ctx, village.WhyKey, village.WhyId).Rows.Any(r => r.Label != null && r.Label.StartsWith("When it loses a third of its customers")), Is.True, "the village's reaction explained");

            // The pitches: III is the newcomer's, not offered.
            int vegType = BusinessRules.Defs(sim).Types.ToList().FindIndex(x => x.Id == "veg_stall");
            LineVm third = ShopPlaceSheet.Build(ctx, vegType, "").Lines.Single(l => l.Label == "Pitch III");
            Assert.That((third.Value, third.Action), Is.EqualTo(("Taken: a newcomer's chai cart", (ActionVm)null)));

            // The square's sheet: pitch III the newcomer's, and the cart by the square, each with its share's Why?.
            SheetVm square = FieldSheets.ForTile(ctx, sim.Config.Game.Shops.SquareTile);
            LineVm onPitch = square.Lines.Single(l => l.Label == "Pitch III"), cart = square.Lines.Single(l => l.Label == "By the square");
            Assert.That((onPitch.Value, onPitch.Action, onPitch.Icon), Is.EqualTo(("A newcomer's chai cart, from outside the village", (ActionVm)null, "newcomer_chai")));
            Assert.That(cart.Value, Is.EqualTo("A newcomer's vegetable cart, from outside the village"));
            WhyTests.AssertWords("cart share", WhyView.Build(ctx, cart.WhyKey, cart.WhyId));

            // Your price above the usual: its Why? warns a newcomer is already here.
            sim.Execute(new SetShop(sim.State.Clock.SimTime, tea.Id, Fixed.Parse("1.1")));
            ctx = ViewKit2.Context(sim);
            Assert.That(WhyView.Build(ctx, "shopPrice", tea.Id).Rows.Any(r => r.Label == "Above the usual price: a newcomer already sells against you at the usual price."), Is.True);
            Business veg = sim.State.Businesses.Businesses.Single(b => BusinessRules.TypeOf(sim, b).Id == "veg_stall");
            sim.Execute(new SetShop(sim.State.Clock.SimTime, veg.Id, Fixed.Parse("1.2")));
            sim.State.Shops.Newcomers.RemoveAll(x => x.Kind == ShopKind.Veg);
            ctx = ViewKit2.Context(sim);
            Assert.That(WhyView.Build(ctx, "shopPrice", veg.Id).Rows.Any(r => r.Label != null && r.Label.StartsWith("Above the usual price, at each season's end there's a 50% chance")), Is.True);

            // This season's alert, opening your shop of that kind.
            AlertVm alert = Alerts.Build(ctx).Single(a => a.Text.Contains("chai cart"));
            Assert.That(alert.Text, Is.EqualTo("Someone from outside set up a chai cart on Pitch III: your prices are above the usual, so they sell at it."));
            Assert.That(alert.Action.Opens.Value.Kind, Is.EqualTo("Business"));
        }

        [Test]
        public void AwayAndMap()
        {
            Simulation sim = WithNewcomers();
            ViewContext ctx = ViewKit2.Context(sim);
            var events = new List<SimEvent>
            {
                new RivalOpened(1, ShopKind.Veg, -1, true),
                new RivalClosed(2, ShopKind.Sweets, 0),
                new RivalPriceCut(3, "village_tea", ShopKind.Chai, Fixed.FromRatio(95, 100), false),
                new RivalPriceCut(4, "grocer", ShopKind.Veg, Fixed.One, true),
            };
            Assert.That(ShopAway.Rows(ctx, events).Select(l => l.Value), Is.EqualTo(new[]
            {
                "Someone from outside set up a vegetable cart by the square: customers here were going unserved.",
                "The newcomer's sweets cart packed up: too few customers.",
                "The village tea stall cut its price to " + ShopWords.Price(ctx, ShopKind.Chai, Fixed.FromRatio(95, 100)) + ": it lost customers to you.",
                "The village grocer put its price back up to the usual price: its customers came back.",
            }));
            Assert.That(ShopAway.Rows(ctx, events).All(l => WhyView.Has(l.WhyKey)), Is.True);

            MapVm map = MapView.Build(ctx);
            Assert.That(map.NewcomerOnPitch, Is.EqualTo(new[] { null, null, "chai" }));
            Assert.That((map.NewcomerCart, map.CartTile), Is.EqualTo(("veg", sim.Config.Game.Shops.CartTile)));
        }
    }
}
