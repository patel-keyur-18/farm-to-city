using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Market;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// ADR 0104 (owner, 2026-11-12): the Store shows what a cart away at the mandi will pay and when, as it does for the supermarket; Your land's
    /// For sale lists only land that is for sale.
    /// </summary>
    public class MandiLandViewTests
    {
        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static Simulation Town()
        {
            Simulation sim = Situations.Town();
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("radish")] += 10_000;
            return sim;
        }

        // No cart away: no line. A cart away: what it pays and when, above the trader's rows; gone once it is back.
        [Test]
        public void StoreShowsTheTripOnlyWhileAway()
        {
            Simulation sim = Town();
            SheetVm store = StoreSheet.Build(ViewKit2.Context(sim));
            Assert.That(store.Lines.Any(l => l.Label == "Mandi trip pays you"), Is.False, "no trip, no line");

            Assert.That(sim.Execute(new GoToMandi(T(sim), new[] { new MandiLoad("radish", 2000) })).Accepted, Is.True);
            store = StoreSheet.Build(ViewKit2.Context(sim));
            LineVm trip = store.Lines.Single(l => l.Label == "Mandi trip pays you");
            Assert.That(trip.Value, Does.StartWith("about ").And.Contain(" coins at "));
            Assert.That(store.Lines.IndexOf(trip), Is.LessThan(store.Lines.FindIndex(l => l.Label == "Sell to the trader")), "above the trader's rows");
            Assert.That(trip.WhyKey, Is.EqualTo("mandi"));
            WhyVm why = WhyView.Build(ViewKit2.Context(sim), trip.WhyKey, trip.WhyId);
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("Radish"), "what the cart carries");

            sim.AdvanceTo(sim.State.Market.Trip.ReturnAt + 1);
            Assert.That(StoreSheet.Build(ViewKit2.Context(sim)).Lines.Any(l => l.Label == "Mandi trip pays you"), Is.False, "back: the line goes");
        }

        // The figure shown is what the cart pays on arrival, less the commission (hire was paid at departure).
        [Test]
        public void TripFigureIsWhatItPays()
        {
            Simulation sim = Town();
            Assert.That(sim.Execute(new GoToMandi(T(sim), new[] { new MandiLoad("radish", 2000) })).Accepted, Is.True);
            long back = sim.State.Market.Trip.ReturnAt;
            sim.AdvanceTo(back - 1);
            long shown = MarketRules.MandiTripPays(sim);
            Assert.That(shown, Is.GreaterThan(0));
            sim.DrainEvents();
            sim.AdvanceTo(back);
            MandiTripReturned r = sim.DrainEvents().OfType<MandiTripReturned>().Single();
            Assert.That(r.Net, Is.EqualTo(shown));
            Assert.That(MarketRules.MandiTripPays(sim), Is.EqualTo(0), "no trip, nothing");
        }

        // "tomorrow" when the cart is back after midnight.
        [Test]
        public void TripAfterMidnightSaysTomorrow()
        {
            Simulation sim = Town();
            long day = Sim.Core.Definitions.ClockDef.SecondsPerDay;
            sim.AdvanceTo((T(sim) / day) * day + 20 * 3600); // 20:00
            Assert.That(sim.Execute(new GoToMandi(T(sim), new[] { new MandiLoad("radish", 2000) })).Accepted, Is.True);
            Assert.That(sim.State.Market.Trip.ReturnAt / day, Is.GreaterThan(T(sim) / day), "back the next day");
            Assert.That(StoreSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Mandi trip pays you").Value, Does.EndWith(" tomorrow"));
        }

        // Your land → For sale: only parcels that are for sale (the village's and neighbours' not-yet land stay out), and one that isn't next to your land says so.
        [Test]
        public void ForSaleListsOnlyWhatIsForSale()
        {
            Simulation sim = Situations.Town();
            var parcels = sim.Config.Game.Map.Parcels;
            Assert.That(sim.State.Land.Parcels.Any(p => !p.Owned && !parcels[p.Id].ForSale), Is.True, "the map has land that isn't for sale");
            SheetVm vm = LandsSheet.Build(ViewKit2.Context(sim), LandsSheet.ForSale);
            var rows = vm.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("show:")).ToList();
            Assert.That(rows.Count, Is.EqualTo(sim.State.Land.Parcels.Count(p => !p.Owned && parcels[p.Id].ForSale)));
            foreach (LineVm row in rows)
            {
                int tile = int.Parse(row.Action.Id.Substring("show:".Length));
                Assert.That(parcels[sim.State.Land.Tiles[tile].ParcelId].ForSale, Is.True, "tile " + tile);
            }
            Assert.That(rows.Any(r => r.Note != null && r.Note.StartsWith("Not next to your land")), Is.True, "a for-sale parcel away from your land is marked");
            Assert.That(rows.Any(r => r.Note == null), Is.True, "and one beside it is not");
        }
    }
}
