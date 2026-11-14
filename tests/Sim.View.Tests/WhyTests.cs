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
            yield return Named("hire", PeopleSheet.Hire(ctx));
            foreach (long place in HiringBoard.Places(sim)) yield return Named("board " + place, HiringBoard.Build(ctx, place));
            foreach (var a in sim.State.People.Applicants) yield return Named("offer " + a.Id, HiringBoard.Offer(ctx, a.Id));
            foreach (var h in sim.State.People.Hands) yield return Named("hand " + h.Id, HandSheet.Build(ctx, h.Id));
            if (sim.State.Citizens != null)
            {
                foreach (string sort in new[] { TownSheet.ByHappiness, TownSheet.BySize, TownSheet.ByJob }) yield return Named("town " + sort, TownSheet.Build(ctx, sort));
                foreach (var x in sim.State.Citizens.Households) yield return Named("household " + x.Id, HouseholdSheet.Build(ctx, x.Id));
                foreach (var c in sim.State.Citizens.Citizens)
                {
                    yield return Named("citizen " + c.Id, CitizenSheet.Build(ctx, c.Id));
                    yield return Named("departed " + c.Id, CitizenSheet.Departed(ctx, c.Id));
                }
            }
            if (sim.State.Businesses != null)
            {
                yield return Named("businesses", BusinessesSheet.Build(ctx));
                for (int i = 0; i < sim.Config.Game.Businesses.Types.Count; i++)
                {
                    yield return Named("research " + i, BusinessResearchSheet.Build(ctx, i));
                    yield return Named("plan " + i, BusinessPlanSheet.Build(ctx, i, false));
                    yield return Named("plan self " + i, BusinessPlanSheet.Build(ctx, i, true));
                    yield return Named("plan loan " + i, BusinessPlanSheet.Build(ctx, i, false, true));
                    if (sim.Config.Game.Businesses.Types[i].IsShop) yield return Named("shop place " + i, ShopPlaceSheet.Build(ctx, i, "hire"));
                }
                if (sim.Config.Game.Shops != null) yield return Named("square", FieldSheets.ForTile(ctx, sim.Config.Game.Shops.SquareTile));
                foreach (var b in sim.State.Businesses.Businesses)
                {
                    yield return Named("business " + b.Id, BusinessSheet.Build(ctx, b.Id, BusinessSheet.Today));
                    yield return Named("business books " + b.Id, BusinessSheet.Build(ctx, b.Id, BusinessSheet.Books));
                    yield return Named("business settings " + b.Id, BusinessSheet.Build(ctx, b.Id, BusinessSheet.Settings));
                    yield return Named("business customers " + b.Id, BusinessSheet.Build(ctx, b.Id, ShopSheet.Customers));
                    yield return Named("put cash " + b.Id, BusinessCashSheet.Build(ctx, b.Id, BusinessCashSheet.Put));
                    yield return Named("take cash " + b.Id, BusinessCashSheet.Build(ctx, b.Id, BusinessCashSheet.Take));
                    yield return Named("close " + b.Id, BusinessCloseSheet.Build(ctx, b.Id));
                    for (int back = 1; back <= b.Reports.Count; back++)
                        yield return Named("business report " + b.Id + " " + back, BusinessReportSheet.Build(ctx, b.Id, back.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            if (Sim.Core.Town.TreasuryRules.IsOpen(sim)) // the town treasury (spec 021)
            {
                yield return Named("treasury", TreasurySheet.Build(ctx));
                yield return Named("treasury give", TreasurySheet.Give(ctx));
                yield return Named("treasury books", TreasurySheet.Books(ctx));
                foreach (var r in sim.State.Treasury.Reports)
                    foreach (string tab in new[] { TreasurySheet.InOut, TreasurySheet.CashFlow, TreasurySheet.Has })
                        yield return Named("treasury " + r.Period + " " + tab, TreasurySheet.Statement(ctx, r.Period, tab));
            }
            yield return Named("forecast", ForecastView.Sheet(ctx));
            for (int i = 0; i < sim.Config.Game.Farm.Crops.Count; i++) yield return Named("crop page " + i, CropCalendar.Page(ctx, i)); // spec 020 §3.6b
            if (sim.State.People != null)
                foreach (Tile t in sim.State.Land.Tiles)
                    if (t.Terrain == Terrain.Farm && PeopleWords.OrderHand(sim, Sim.Core.People.OrderTargetKind.Field, t.Id) != null)
                    {
                        yield return Named("order other " + t.Id, OrderSheet.Other(ctx, t.Id, "+none"));
                        break;
                    }
            foreach (var extra in Panels.All(ctx)) yield return extra;
        }

        private static KeyValuePair<string, SheetVm> Named(string name, SheetVm s) { return new KeyValuePair<string, SheetVm>(name, s); }

        /// <summary>A factor: a decimal number with no unit after it (5.30 /kg, 3.4 h, 70.7 kg, 6.3%, fertility 45.2 / 100, +0.4 points, 2.5 stars and a milk price of 57.50 coins are fine; 0.875 is not).</summary>
        private static readonly Regex Factor = new Regex(@"\d\.\d+(?!\d|\s*(?:/kg|/L\b|h\b|kg|%|L\b|Cr\b|M\b|/ 100|of 100|points?\b|coins?\b|stars?\b|seasons\b))|×");

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
                // The sow sheet's hourly figures (spec 020 AC-19), on every field you own.
                foreach (Tile t in sim.State.Land.Tiles)
                {
                    if (t.Terrain != Terrain.Farm || !sim.State.Land.Parcels[t.ParcelId].Owned) continue;
                    foreach (CropRowVm r in SowSheet.Build(ctx, t.Id).Rows)
                    {
                        if (r.PerHourText == null) continue;
                        string where = "sow " + t.Id + " / " + r.CropId + " = " + r.PerHourText;
                        if (!WhyView.Has(r.PerHourWhyKey)) { if (!missing.Contains(where)) missing.Add(where); continue; }
                        AssertWords(where, WhyView.Build(ctx, r.PerHourWhyKey, r.PerHourWhyId));
                        checkedRows++;
                    }
                }
                foreach (string key in new[] { "cash", "store", "calendar", "townHappiness" }) AssertWords("HUD " + key, WhyView.Build(ctx, key, 0));
                Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "building sheets and Why? sheets is read only (AC-1)");
            }
            Assert.That(missing, Is.Empty, "rows with a number and no Why?: " + string.Join(" | ", missing));
            Assert.That(checkedRows, Is.GreaterThan(100));
        }

        // Spec 002 AC-17 (ADR 0085): the moisture Why? says that dry soil dries slower, by how much, while the weather dries it.
        [Test]
        public void Moisture_DrySoilRow()
        {
            Simulation sim = ViewKit.NewGame();
            long t = sim.State.Clock.SimTime;
            while (sim.Config.Game.Land.MoistureByWeather[sim.Weather.IndexOf(sim.CurrentWeather.Id)] >= Fixed.Zero) sim.AdvanceTo(t += 3600);
            ViewContext ctx = ViewKit2.Context(sim);
            Tile tile = sim.State.Land.Tiles[84];
            string label = ctx.Strings.Get("why.moisture.drySoil", "level", "50");

            tile.Moisture = Fixed.FromInt(25);
            WhyVm dry = WhyView.Build(ctx, "moisture", tile.Id);
            AssertWords("dry soil", dry);
            WhyRowVm row = dry.Rows.Single(r => r.Label == label);
            Assert.That(row.Value, Is.EqualTo("50%"), "25 of 50: dries half as fast");

            tile.Moisture = Fixed.FromInt(60);
            Assert.That(WhyView.Build(ctx, "moisture", tile.Id).Rows.Any(r => r.Label == label), Is.False, "above the level: the full drying");
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
