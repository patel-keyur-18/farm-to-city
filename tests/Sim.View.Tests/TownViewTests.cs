using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 018 (US-132): literacy on the Town tab, the citizen card's schooling and the school's adult class.</summary>
    public class TownViewTests
    {
        private static Citizen Named(ViewContext ctx, string n) { return ctx.Sim.State.Citizens.Citizens.Single(c => c.Living && PeopleWords.Name(ctx, c.Name) == n); }

        // AC-3: the Town tab's literacy, whole percent rounded down, with its *Why?* in words.
        [Test]
        public void Literacy()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (Citizen c in sim.State.Citizens.Citizens) c.Education = 0;
            Named(ctx, "Manoj").Education = 1;
            ctx = ViewKit2.Context(sim);

            LineVm line = TownSheet.Build(ctx).Lines.Single(l => l.Label == "Literacy");
            Assert.That(line.Value, Is.EqualTo("16%"), "1 of 6 aged 7+ (the hand, Kamala, Manoj, Usha, Ravi, Asha), rounded down");
            WhyVm why = WhyView.Build(ctx, line.WhyKey, line.WhyId);
            WhyTests.AssertWords("literacy", why);
            Assert.That(why.Rows.Select(r => (r.Label, r.Value)).Take(2), Is.EqualTo(new[] { ("Can read, aged 7 and older", "1"), ("Everyone aged 7 and older", "6") }));
            Assert.That(why.Rows.Any(r => r.Label != null && r.Label.Contains("no school")), Is.True, "no school yet: says how to get one");

            Assert.That(CitizenSheet.Build(ctx, Named(ctx, "Manoj").Id).Lines.Single(l => l.Label == "Schooling").Value, Is.EqualTo("Primary school"));
            Assert.That(CitizenSheet.Build(ctx, Named(ctx, "Ravi").Id).Lines.Single(l => l.Label == "Schooling").Value, Is.EqualTo("No school yet"));
            Assert.That(CitizenSheet.Build(ctx, Named(ctx, "Usha").Id).Lines.Single(l => l.Label == "Schooling").Value, Is.EqualTo("Can't read"));
        }

        // Spec 018 §7: the open school's sheet names the next to finish and the adult class (who would join, then Start, then who's learning).
        [Test]
        public void SchoolAndClass()
        {
            Simulation sim = Situations.Town();
            foreach (Citizen c in sim.State.Citizens.Citizens) c.Education = 0;
            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, sim.Period, "ledger.opening", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 10_00_000_00),
                new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OpeningEquity, -10_00_000_00));
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(t + 48 * 3600);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm school = PlaceSheetOf(ctx);
            Assert.That(school.Lines.Single(l => l.Label == "Pupils").Value, Is.EqualTo("2 of 30 seats"), "Ravi and Asha");
            Assert.That(school.Lines.Single(l => l.Label == "Next to finish primary").Value, Does.StartWith("Asha, in "), "Ravi (12) turns 14 first");
            Assert.That(school.Lines.Single(l => l.Label == "Would join").Value, Is.EqualTo("4 of 15 places"), "the hand, Manoj, Usha, then Kamala the elder");
            WhyVm why = WhyView.Build(ctx, "adultClass", 0);
            WhyTests.AssertWords("adult class", why);
            Assert.That(why.Rows.Select(r => r.Label), Has.Some.StartsWith("Manoj"));
            ActionVm start = school.Actions.Single(a => a.Id == "adultClass");
            Assert.That(start.Enabled, Is.True);
            Assert.That(CitizenSheet.Build(ctx, Named(ctx, "Ravi").Id).Lines.Single(l => l.Label == "Schooling").Value, Is.EqualTo("At school, year 1 of 4"));

            Ok(sim, new StartAdultClass(sim.State.Clock.SimTime));
            ctx = ViewKit2.Context(sim);
            school = PlaceSheetOf(ctx);
            Assert.That(school.Lines.Single(l => l.Label == "Learning to read").Value, Does.StartWith("4 · ends in "));
            Assert.That(school.Actions.Any(a => a.Id == "adultClass"), Is.False, "running: no Start");
            Assert.That(CitizenSheet.Build(ctx, Named(ctx, "Usha").Id).Lines.Single(l => l.Label == "Schooling").Value, Is.EqualTo("In the adult class"));
        }

        // Spec 018 §5: who learned to read, on the away card: a pupil by name (several counted), the adult class counted.
        [Test]
        public void LiterateAwayLines()
        {
            Simulation sim = Situations.Town();
            ViewContext ctx = ViewKit2.Context(sim);
            Citizen ravi = Named(ctx, "Ravi"), asha = Named(ctx, "Asha"), usha = Named(ctx, "Usha"), manoj = Named(ctx, "Manoj");
            var one = new System.Collections.Generic.List<Sim.Core.Events.SimEvent> { new Sim.Core.Events.CitizenLiterate(1, ravi.Id, ravi.Name, Sim.Core.Events.LiterateHow.School) };
            Assert.That(TownAway.Rows(ctx, one).Select(l => l.Value), Is.EqualTo(new[] { "Ravi finished primary school and can read." }));
            var many = new System.Collections.Generic.List<Sim.Core.Events.SimEvent>
            {
                new Sim.Core.Events.CitizenLiterate(1, ravi.Id, ravi.Name, Sim.Core.Events.LiterateHow.School),
                new Sim.Core.Events.CitizenLiterate(1, asha.Id, asha.Name, Sim.Core.Events.LiterateHow.School),
                new Sim.Core.Events.CitizenLiterate(2, usha.Id, usha.Name, Sim.Core.Events.LiterateHow.AdultClass),
                new Sim.Core.Events.CitizenLiterate(2, manoj.Id, manoj.Name, Sim.Core.Events.LiterateHow.AdultClass),
            };
            Assert.That(TownAway.Rows(ctx, many).Select(l => l.Value), Is.EqualTo(new[] { "2 children finished primary school and can read.", "The adult class is over: 2 more can read." }));
        }

        private static SheetVm PlaceSheetOf(ViewContext ctx) { return FieldSheets.ForTile(ctx, ctx.Sim.Config.Game.School.Tile); }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }
    }
}
