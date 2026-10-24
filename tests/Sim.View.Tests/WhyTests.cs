using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-5 and AC-15 (ADR 0043 §3): every number has a Why? in words, never a factor.</summary>
    public class WhyTests
    {
        /// <summary>Every sheet the player can open in this game (spec 008 §3.5), by name.</summary>
        public static IEnumerable<KeyValuePair<string, SheetVm>> AllSheets(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            foreach (Tile t in sim.State.Land.Tiles)
                yield return Named("tile " + t.Id, FieldSheets.ForTile(ctx, t.Id));
            foreach (TreeSlot s in sim.State.Land.Slots) yield return Named("slot " + s.Id, FieldSheets.ForSlot(ctx, s.Id));
            foreach (GardenBed b in sim.State.Farm.Beds) yield return Named("bed " + b.Id, FieldSheets.ForBed(ctx, b.Id));
            yield return Named("store", StoreSheet.Build(ctx));
            yield return Named("people", PeopleSheet.Build(ctx));
            foreach (var h in sim.State.People.Hands) yield return Named("hand " + h.Id, HandSheet.Build(ctx, h.Id));
            yield return Named("forecast", ForecastView.Sheet(ctx));
            foreach (var extra in Panels.All(ctx)) yield return extra;
        }

        private static KeyValuePair<string, SheetVm> Named(string name, SheetVm s) { return new KeyValuePair<string, SheetVm>(name, s); }

        /// <summary>A factor: a decimal number with no unit after it (5.30 /kg, 3.4 h, 70.7 kg, 6.3%, fertility 45.2 / 100, +0.4 points and a milk price of 57.50 coins are fine; 0.875 is not).</summary>
        private static readonly Regex Factor = new Regex(@"\d\.\d+(?!\d|\s*(?:/kg|h\b|kg|%|L\b|Cr\b|M\b|/ 100|of 100|points?\b|coins?\b))|×");

        public static void AssertWords(string where, WhyVm why)
        {
            Assert.That(why.Rows, Is.Not.Empty, where);
            foreach (string t in new[] { why.Title, why.Value, why.Footnote }.Concat(why.Rows.SelectMany(r => new[] { r.Label, r.Value, r.Note })))
                if (t != null) Assert.That(Factor.IsMatch(t), Is.False, where + ": a factor in \"" + t + "\"");
        }

        // Spec 008 AC-15.
        [Test]
        public void EveryNumberHasWhy()
        {
            int checkedRows = 0;
            var missing = new List<string>();
            foreach (Simulation sim in Situations.All())
            {
                ViewContext ctx = ViewKit2.Context(sim);
                ulong hash = sim.State.ComputeHash();
                foreach (var named in AllSheets(ctx))
                {
                    foreach (LineVm l in named.Value.Lines)
                    {
                        if (l.IsHeader || l.Value == null || !Regex.IsMatch(l.Value, @"\d")) continue;
                        string where = named.Key + " / " + l.Label + " = " + l.Value;
                        if (!WhyView.Has(l.WhyKey)) { if (!missing.Contains(where)) missing.Add(where); continue; }
                        AssertWords(where, WhyView.Build(ctx, l.WhyKey, l.WhyId));
                        checkedRows++;
                    }
                }
                foreach (string key in new[] { "cash", "store", "calendar" }) AssertWords("HUD " + key, WhyView.Build(ctx, key, 0));
                Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "building sheets and Why? sheets is read only (AC-1)");
            }
            Assert.That(missing, Is.Empty, "rows with a number and no Why?");
            Assert.That(checkedRows, Is.GreaterThan(100));
        }

        // Spec 008 AC-5: each source lists its formula's causes, in words.
        [Test]
        public void Sources()
        {
            Simulation sim = Situations.Ready();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(WhyView.Effect(ctx, Fixed.Parse("1.1")), Is.EqualTo("10% more"));
            Assert.That(WhyView.Effect(ctx, Fixed.Parse("0.85")), Is.EqualTo("15% less"));
            Assert.That(WhyView.Effect(ctx, Fixed.One), Is.EqualTo("no change"));

            Crop ready = sim.State.Farm.Crops.First(c => c.State == CropState.Ready);
            WhyVm yield = WhyView.Build(ctx, "yield", ready.TileId);
            Assert.That(yield.Rows.Select(r => r.Label).Skip(1).Take(8), Is.EqualTo(new[]
                { "Soil", "Water", "Care", "Certified seed", "Weather during growth", "Tools (none yet)", "Compost fertiliser", "Pests and disease" }));
            Assert.That(yield.Rows.Skip(1).Take(8).All(r => Regex.IsMatch(r.Value, @"^(\d+% (more|less)|no change)$")), Is.True);

            WhyVm labour = WhyView.Build(ctx, "labour", 0);
            Assert.That(labour.Rows.Select(r => r.Label), Does.Contain("Hours you work").And.Contain("Your food").And.Contain("Tiredness").And.Contain("Health"));

            int potato = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "potato");
            WhyVm trader = WhyView.Build(ctx, "traderPrice", potato);
            Assert.That(trader.Value, Is.EqualTo(ctx.Fmt.PricePerKg(Sim.Core.Market.MarketRules.Floor(sim, potato))), "potato in Spring (spec 004 AC-2, today's market spec 011 §3.1b)");
            Assert.That(trader.Rows.Select(r => r.Label), Is.EqualTo(new[]
                { "Usual price in the valley", "Prices this year (they rise a little each season)", "Season: Spring", "Today's market (set at 6:00)", "The valley's price today", "Carting it to town costs" }));

            Tile unowned = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && !sim.State.Land.Parcels[t.ParcelId].Owned);
            WhyVm land = WhyView.Build(ctx, "landPrice", unowned.Id);
            Assert.That(land.Rows.Count, Is.EqualTo(6));

            WhyVm worth = WhyView.Build(ctx, "netWorth", 0);
            Assert.That(worth.Rows.Select(r => r.Label), Does.Contain("Cash").And.Contain("Land at today's price").And.Contain("Buildings").And.Contain("Goods in store"));

            TreeSlot slot = sim.State.Land.Slots.First(s => s.TreeId != 0);
            WhyVm payback = WhyView.Build(ctx, "payback", slot.Id);
            Assert.That(payback.Rows.Select(r => r.Label).Take(3), Is.EqualTo(new[] { "It cost", "Its fruit has earned", "Still to earn" }));
        }
    }
}
