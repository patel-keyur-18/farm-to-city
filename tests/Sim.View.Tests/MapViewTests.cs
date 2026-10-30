using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Who the map draws where (spec 008 §3.4, spec 014 §3.6, ADR 0083 §5–6).</summary>
    public class MapViewTests
    {
        private const long Hour = 3600, Day = 86400;

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        /// <summary>Manoj's town at the next <paramref name="hour"/>:00, rice sown and three labourers hired (Manoj and Usha from the family house first, then a neighbour).</summary>
        internal static Simulation Hired(int hour)
        {
            Simulation sim = Situations.Town();
            long t = sim.State.Clock.SimTime, at = t / Day * Day + hour * Hour;
            sim.AdvanceTo(at <= t ? at + Day : at);
            Ok(sim, new Sow(sim.State.Clock.SimTime, 102, "rice", SeedSource.Certified, "none"));
            Ok(sim, new HireLabourers(sim.State.Clock.SimTime, 3));
            return sim;
        }

        private static Citizen Named(Simulation sim, ViewContext ctx, string name)
        {
            return sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == name);
        }

        // Spec 014 AC-17: a townsperson hired as a labourer is drawn as that citizen, from their family house, and not also at home.
        [Test]
        public void TownLabourers()
        {
            Simulation sim = Hired(10);
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen manoj = Named(sim, ctx, "Manoj"), usha = Named(sim, ctx, "Usha");
            Assert.That(manoj.Labouring(ctx.Now) && usha.Labouring(ctx.Now), Is.True, "your townspeople are hired first");
            Building house = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b));
            MapVm map = MapView.Build(ctx);

            PersonVm m = map.People.Single(p => p.Key == "labourer:town:" + manoj.Id), u = map.People.Single(p => p.Key == "labourer:town:" + usha.Id);
            Assert.That((m.Kind, m.Look, m.HomeHut, m.HomeTile), Is.EqualTo((PersonKind.Labourer, "town_m", house.Id, house.TileId)));
            Assert.That((u.Look, u.Female), Is.EqualTo(("town_f", true)));
            Assert.That(m.Patrol, Is.Not.Empty, "at work on the growing fields");
            Assert.That(map.People.Any(p => p.Key == "citizen:" + manoj.Id || p.Key == "citizen:" + usha.Id), Is.False, "not also at home");
            Assert.That(map.People.Count(p => p.Kind == PersonKind.Labourer && p.HomeHut == 0), Is.EqualTo(1), "the neighbour walks in from the edge");
        }

        // Spec 008 AC-18: people are on the fields from 7:00 to 19:00 and home outside it; labourers aren't drawn at night (neighbours
        // walk out to the west edge, townspeople home).
        [Test]
        public void WorkingDay()
        {
            Simulation sim = Hired(10);
            MapVm day = MapView.Build(ViewKit2.Context(sim));
            Assert.That(day.People.Single(p => p.Kind == PersonKind.Founder).Patrol, Is.Not.Empty);
            Assert.That(day.People.Where(p => p.Kind == PersonKind.Hand).All(p => p.Patrol.Count > 0), Is.True);
            Assert.That(day.People.Count(p => p.Kind == PersonKind.Labourer), Is.EqualTo(3));

            sim = Hired(21);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(Named(sim, ctx, "Manoj").Labouring(ctx.Now), Is.True, "still hired: only gone home for the night");
            MapVm night = MapView.Build(ctx);
            PersonVm founder = night.People.Single(p => p.Kind == PersonKind.Founder);
            Assert.That((founder.Patrol.Count, founder.TileId), Is.EqualTo((0, founder.HomeTile)));
            Assert.That(night.People.Where(p => p.Kind == PersonKind.Hand).All(p => p.Patrol.Count == 0 && p.TileId == p.HomeTile), Is.True);
            Assert.That(night.People.Any(p => p.Kind == PersonKind.Labourer), Is.False);

            // A hand whose order acted this hour stays home at night too (seen in the Editor: he walked out to the field he'd harvested).
            Hand h = sim.State.People.Hands[0];
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.KeepCrop(86, "rice", SeedSource.Certified, "none", SellRule.Store)));
            Assert.That(sim.State.People.Orders.Single(o => o.HandId == h.Id && o.Target == 86).LastAt, Is.EqualTo(sim.State.Clock.SimTime), "the order acted at once");
            PersonVm hand = MapView.Build(ViewKit2.Context(sim)).People.Single(p => p.Key == "hand:" + h.Id);
            Assert.That((hand.Patrol.Count, hand.TileId), Is.EqualTo((0, hand.HomeTile)));
            Assert.That(MapView.WorkingHours(7 * Hour) && !MapView.WorkingHours(19 * Hour) && !MapView.WorkingHours(6 * Hour + 3599), Is.True);
        }
    }
}
