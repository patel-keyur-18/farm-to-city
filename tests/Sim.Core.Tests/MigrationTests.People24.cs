using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.History;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        private static string People24Fixture { get { return Path.Combine(Dir, "schema24-people.ftc"); } }

        private static void Pin24(string file, Simulation sim)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(file, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 24, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(file, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>
        /// Sprint 26 (spec 028 US-259 and US-260, spec 029): a Township with a kirana on khata and marketing (and a household that owes), the farm's six hands with a supervisor and an accident, a quarrel and an association
        /// card waiting, a place's relations, citizens' own stalls and arcs in the stories, and the Chronicle of what happened. Written while schema 24 is the current one: <c>FTC_UPDATE_SCHEMA24=1</c>.
        /// </summary>
        private static void WritePeople24Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(24), "this fixture is the schema-24 one");
            Simulation sim = InvoiceTests.Township();
            for (int i = 0; i < 20; i++) KiranaTests.Family(sim, ShopRules.Defs(sim).SquareTile, 8_000);
            Business.Business shop = KiranaTests.Open(sim, true, 0);
            Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, true));
            Ok(sim, new SetKhataLimit(sim.State.Clock.SimTime, shop.Id, ShopRules.Defs(sim).KhataLimits[ShopRules.Defs(sim).KhataLimits.Count - 1]));
            Ok(sim, new SetMarketing(sim.State.Clock.SimTime, shop.Id, ShopRules.Defs(sim).MarketBudgets[1]));
            InvoiceTests.ToHour(sim, 14, 4);
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 2 * 3600); // a close on, so the shop's khata and the Chronicle have something; the cards come after, with their day still to run
            long give = 9_000_000 * Coin - sim.State.Ledger.Cash; // a balanced entry, not a command: the file must stay saveable
            if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            Hand first = HireHand(sim);
            while (P(sim).Hands.Count < 6)
            {
                Hand h = first.Clone();
                h.Id = P(sim).NextHandId++;
                h.PrepaymentId = 0;
                h.Name = (h.Name + P(sim).Hands.Count * 7) % sim.Config.Game.People.Names.Count;
                P(sim).Hands.Add(h);
            }
            WorkplaceKit.Supervise(sim, 60);
            P(sim).Workplace.Supervisor.Spared = 2; P(sim).Workplace.Supervisor.Settled = 1; P(sim).Workplace.Supervisor.Kept = 1;
            P(sim).Hands[0].Hr.SickUntil = WorkplaceKit.Now(sim) + 6 * 3600;
            HrEvent a = WorkplaceKit.Card(sim, HrEventKind.Accident, 0, P(sim).Hands[0].Id, amount: 166_700); a.Hours = 8; a.Cost = 200_000;
            WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, P(sim).Hands[1].Id, P(sim).Hands[2].Id);
            WorkplaceKit.Card(sim, HrEventKind.Association, 0, P(sim).Hands[3].Id);
            P(sim).Workplace.Places.Add(new PlaceRelations { Place = 0, TiredCloses = 2, QuietUntil = WorkplaceKit.Now(sim) + 86_400, Refusals = 1, CutSeason = sim.Period });
            StoriesState st = sim.State.Chronicle.Stories;
            Citizen c = sim.State.Citizens.Citizens.First(x => x.Living);
            st.Arcs.Add(new StoryArcState { Citizen = c.Id, Arc = StoryArc.Stall, Step = 2, At = sim.Period, Other = (int)ShopKind.Veg });
            st.Arcs.Add(new StoryArcState { Citizen = c.Id + 1, Arc = StoryArc.LongLife, Step = 1, At = sim.Period });
            sim.State.Shops.Newcomers.Add(new Rival { Def = -1, Id = "newcomer_veg", Kind = ShopKind.Veg, Pitch = -1, OpenedAt = WorkplaceKit.Now(sim), PriceFactor = Maths.Fixed.One, Stars = Maths.Fixed.One, Owner = c.Id });
            Pin24(People24Fixture, sim);
        }

        // Sprint 26: the people and the shops of schema 24 (the supervisor, the three timed cards with their bill and their deadline, a place's relations, a kirana's khata and marketing, a citizen's own stall and the
        // arcs, the Chronicle) load in every later build, round-trip to the same hash and carry on through two closes.
        [Test]
        public void Schema24_People()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_SCHEMA24") == "1") WritePeople24Fixture();
            Assert.That(File.Exists(People24Fixture), Is.True, "run once with FTC_UPDATE_SCHEMA24=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(People24Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(24));
            Supervisor sv = r.State.People.Workplace.Supervisor;
            Assert.That((sv != null, sv?.Spared, sv?.Settled, sv?.Kept), Is.EqualTo((true, (int?)2, (int?)1, (int?)1)), "the supervisor and what he has spared");
            Assert.That(r.State.People.HrEvents.Select(e => e.Kind), Is.EquivalentTo(new[] { HrEventKind.Accident, HrEventKind.Conflict, HrEventKind.Association }), "the three timed cards wait");
            HrEvent accident = r.State.People.HrEvents.Single(e => e.Kind == HrEventKind.Accident);
            Assert.That((accident.Cost, accident.Hours, accident.ExpiresAt > accident.At), Is.EqualTo((200_000L, 8, true)), "the accident's bill, its hours and its deadline are kept");
            PlaceRelations farm = r.State.People.Workplace.Places.Single();
            Assert.That((farm.TiredCloses, farm.Refusals), Is.EqualTo((2, 1)), "a place's relations");
            Business.Business shop = r.State.Businesses.Businesses.Single(b => config.Game.Businesses.Types[b.Type].Kind == ShopKind.Grocery);
            Assert.That((shop.Khata, shop.KhataLimit > 0, shop.Marketing > 0), Is.EqualTo((true, true, true)), "the kirana's khata and marketing are kept");
            Assert.That(r.State.Shops.Newcomers.Single(n => n.Owner != 0).Kind, Is.EqualTo(ShopKind.Veg), "a citizen's own stall is theirs");
            Assert.That(r.State.Chronicle.Stories.Arcs.Select(x => x.Arc), Is.EquivalentTo(new[] { StoryArc.Stall, StoryArc.LongLife }), "the story arcs are kept");
            Assert.That(r.State.Chronicle.Entries.Count, Is.GreaterThan(0), "the Chronicle is kept");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in r.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 2 * 86_400);
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 2 * 86_400);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "two closes on");
        }
    }
}
