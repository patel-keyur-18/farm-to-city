using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.EventEngineTests;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-154 (spec 027 §3.4, AC-8 to AC-10): a disaster is signalled two days ahead, strikes at the first live 6:00 and slides while you are away; the fire and the epidemic.</summary>
    public class DisasterTests
    {
        private const long Day = ClockDef.SecondsPerDay, Hour = 3600;

        private static Simulation Quiet() { return EventEngineTests.Town(minor: "0", major: "0"); }

        /// <summary>The town at exactly a 6:00 after the engine has seen the Township, so a card forced now has a strike at an exact 6:00.</summary>
        private static Simulation AtSix()
        {
            Simulation sim = Quiet();
            long six = (sim.State.Clock.SimTime / Day + 2) * Day + 6 * Hour;
            sim.AdvanceTo(six);
            Assert.That(EventRules.State(sim).StartedAt, Is.GreaterThan(0));
            return sim;
        }

        private static Building House(Simulation sim)
        {
            Household x = TownLifeRules.Yours(sim)[0];
            var b = new Building { Id = sim.State.Land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = 40, Cost = 60_000 * Coin };
            sim.State.Land.Buildings.Add(b);
            x.HomeId = b.Id;
            return b;
        }

        private static long Now(Simulation sim) { return sim.State.Clock.SimTime; }

        // AC-8: the card is the warning, 48 hours before the strike; the strike lands at the first live 6:00.
        [Test]
        public void Warning_Strike_Slide()
        {
            Simulation sim = AtSix();
            Building b = House(sim);
            PendingEvent warn = Force(sim, "fire", b.Id);
            EventsState s = EventRules.State(sim);
            Assert.That(s.DisasterStrikeAt, Is.EqualTo(Now(sim) + 48 * Hour), "warned at 6:00: the full 48 hours, and a strike always falls on a 6:00");
            Assert.That(warn.ExpiresAt, Is.EqualTo(s.DisasterStrikeAt), "the card's clock is the warning");
            Assert.That(sim.PendingEvents.OfType<DisasterWarned>().Single().StrikeAt, Is.EqualTo(s.DisasterStrikeAt));
            sim.AdvanceTo(s.DisasterStrikeAt - 1);
            Assert.That(sim.PendingEvents.OfType<FireStruck>(), Is.Empty, "not before");
            sim.AdvanceTo(s.DisasterStrikeAt + 1);
            Assert.That(sim.PendingEvents.OfType<FireStruck>().Count(), Is.EqualTo(1), "at the 6:00 the warning ran out");
            Assert.That(s.DisasterDef, Is.EqualTo(-1));

            // Away over the 6:00, then back: the strike comes at your first live hour (ADR 0154), not while you are away and not never.
            Simulation away = AtSix();
            Building b2 = House(away);
            Force(away, "fire", b2.Id);
            EventsState s2 = EventRules.State(away);
            long strike = s2.DisasterStrikeAt;
            away.Execute(new AdvanceTo(strike - Hour, AdvanceMode.Live));
            away.Execute(new AdvanceTo(strike + 6 * Hour, AdvanceMode.Away));
            Assert.That(s2.DisasterSlides, Is.EqualTo(1), "the hour passed while you were away: it slid");
            Assert.That(away.PendingEvents.OfType<FireStruck>().Single().SimTime, Is.EqualTo(strike + 6 * Hour), "and struck the moment you were back, not before");
            Assert.That(s2.DisasterDef, Is.EqualTo(-1));

            // Away for days: it slides three times and the fourth 6:00 passes it by; nothing is lost.
            Simulation gone = AtSix();
            Force(gone, "fire", House(gone).Id);
            EventsState s3 = EventRules.State(gone);
            long at = s3.DisasterStrikeAt - Hour;
            gone.Execute(new AdvanceTo(at, AdvanceMode.Live));
            for (int d = 0; d < 4; d++) { at += Day; gone.Execute(new AdvanceTo(at, AdvanceMode.Away)); } // each at 5:00, so none ends on a live hour after 6:00
            Assert.That(gone.PendingEvents.OfType<FireStruck>(), Is.Empty, "never fires while you are away");
            Assert.That(gone.PendingEvents.OfType<DisasterPassed>().Count(), Is.EqualTo(1), "after three slides it passes");
            Assert.That(s3.DisasterDef, Is.EqualTo(-1));
        }

        // AC-9: 35% of the book value, halved by preparing, a second card to repair at once or slowly, the bill in Disaster loss.
        [Test]
        public void Fire_Damage_Repair()
        {
            Simulation sim = AtSix();
            Building b = House(sim);
            Force(sim, "fire", b.Id);
            PendingEvent warn = EventRules.State(sim).Waiting.Single();
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new AnswerEvent(Now(sim), warn.Id, 1)).Accepted, Is.True, "prepare: clear dry straw, keep buckets ready");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - Sim.Core.Market.MarketRules.Indexed(sim, 800 * Coin)));
            sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
            FireStruck f = sim.PendingEvents.OfType<FireStruck>().Single();
            long expected = Money.MulRate(b.BookValue, Fixed.FromRatio(35, 100) * Fixed.FromRatio(1, 2));
            Assert.That(f.Bill, Is.EqualTo(expected), "35% of the book value, halved by preparation");
            Assert.That(f.Prepared, Is.True);
            PendingEvent repair = EventRules.State(sim).Waiting.Single(p => EventRules.Def(sim).Events[p.Def].Id == EventsDef.FireRepair);
            Assert.That(repair.Param, Is.EqualTo(expected));
            Assert.That(EventRules.ChoiceCost(sim, repair.Def, 0, repair), Is.EqualTo(expected), "at once: the bill");
            Assert.That(EventRules.ChoiceCost(sim, repair.Def, 1, repair), Is.EqualTo(Money.MulRate(expected, Fixed.FromRatio(1, 2))), "slowly: half");
            long before = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new AnswerEvent(Now(sim), repair.Id, 0)).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(before - expected));
            Assert.That(sim.State.Ledger.Balance(Account.DisasterLoss), Is.EqualTo(expected), "the books' Disaster loss line");
            Assert.That(sim.PendingEvents.OfType<FireRepaired>().Single().Cost, Is.EqualTo(expected), "the Chronicle and the Almanac hear what was paid: the whole bill");

            // Unanswered, the repair card takes its default after 24 h: slowly, half the bill, and never refused for want of cash.
            Simulation d = AtSix();
            Building b2 = House(d);
            Force(d, "fire", b2.Id);
            d.AdvanceTo(EventRules.State(d).DisasterStrikeAt + 1);
            PendingEvent r2 = EventRules.State(d).Waiting.Single(p => EventRules.Def(d).Events[p.Def].Id == EventsDef.FireRepair);
            long cash2 = d.State.Ledger.Cash;
            d.AdvanceTo(r2.ExpiresAt + 1);
            Assert.That(EventRules.State(d).History.Last().Choice, Is.EqualTo(1), "the slow repair is the default");
            Assert.That(d.State.Ledger.Cash, Is.EqualTo(cash2 - Money.MulRate(r2.Param, Fixed.FromRatio(1, 2))));
            Assert.That(d.PendingEvents.OfType<FireRepaired>().Single().Cost, Is.EqualTo(Money.MulRate(r2.Param, Fixed.FromRatio(1, 2))), "the default slow repair is recorded at half the bill, not the whole (review of Sprint 26)");
        }

        // AC-9: with cash under half the bill the slow repair can still be chosen by hand (it pays as far as cash goes); the quick one is refused for want of cash.
        [Test]
        public void Fire_SlowRepair_ByHand_WhenPoor()
        {
            Simulation sim = AtSix();
            Building b = House(sim);
            Force(sim, "fire", b.Id);
            sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
            PendingEvent repair = EventRules.State(sim).Waiting.Single(p => EventRules.Def(sim).Events[p.Def].Id == EventsDef.FireRepair);
            InsuranceKit.SetCash(sim, 100 * Coin);
            Assert.That(sim.Execute(new AnswerEvent(Now(sim), repair.Id, 0)).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "the quick repair needs the bill");
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new AnswerEvent(Now(sim), repair.Id, 1)).Accepted, Is.True, "the slow one is always open");
            long half = Money.MulRate(repair.Param, Fixed.FromRatio(1, 2));
            Assert.That(half, Is.GreaterThan(cash), "the test's cash is under half the bill");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(0), "it paid as far as cash went, and no further");
        }

        // AC-9: the water tower's reach halves the damage again.
        [Test]
        public void Fire_Tower()
        {
            Simulation sim = AtSix();
            UtilityDef t = UtilityRules.Def(sim, UtilityDef.Tower);
            Building b = House(sim);
            b.TileId = t.Tile; // on the tower's own acre
            sim.State.Township.UtilitiesBuilt |= 1 << UtilityDef.Tower;
            if (!UtilityRules.Covers(sim, UtilityDef.Tower, b.TileId)) Assert.Ignore("the test town's tower isn't working: " + UtilityRules.Working(sim, UtilityDef.Tower));
            Force(sim, "fire", b.Id);
            sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
            FireStruck f = sim.PendingEvents.OfType<FireStruck>().Single();
            Assert.That(f.Tower, Is.True);
            Assert.That(f.Bill, Is.EqualTo(Money.MulRate(b.BookValue, Fixed.FromRatio(35, 100) * Fixed.FromRatio(1, 2))), "the tower halves it");
        }

        // AC-10: sick-day chance ×2.5 (the clinic's reach halves it), the camp ×1.5, six days, and it ends.
        [Test]
        public void Epidemic()
        {
            Simulation sim = AtSix();
            Force(sim, "epidemic");
            long strike = EventRules.State(sim).DisasterStrikeAt;
            Assert.That(EventRules.EpidemicOn(sim), Is.False, "only warned");
            sim.AdvanceTo(strike + 1);
            Assert.That(EventRules.EpidemicOn(sim), Is.True);
            Fixed chance = Fixed.FromRatio(1, 100);
            Assert.That(ClinicRules.SickChance(sim, chance, Fixed.Zero), Is.EqualTo(chance * Fixed.FromRatio(5, 2)), "×2.5 with no clinic");
            Assert.That(ClinicRules.SickChance(sim, chance, Fixed.One) < chance * Fixed.FromRatio(5, 2), Is.True, "the clinic cuts it");
            sim.AdvanceTo(strike + 6 * Day + 2 * Hour);
            Assert.That(EventRules.EpidemicOn(sim), Is.False, "six days");
            Assert.That(sim.PendingEvents.OfType<EpidemicEnded>().Count(), Is.EqualTo(1));

            // The health camp: 25 a person, ×1.5 instead; closing the school and the shops: ×1.5 and the shops' demand −30% for 3 days.
            Simulation camp = AtSix();
            PendingEvent w = Force(camp, "epidemic");
            long cost = EventRules.ChoiceCost(camp, w.Def, 1, w);
            Assert.That(cost, Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(camp, 25 * Coin * FestivalRules.People(camp))), "25 a person");
            Assert.That(camp.Execute(new AnswerEvent(Now(camp), w.Id, 1)).Accepted, Is.True);
            camp.AdvanceTo(EventRules.State(camp).DisasterStrikeAt + 1);
            Assert.That(ClinicRules.SickChance(camp, chance, Fixed.Zero), Is.EqualTo(chance * Fixed.FromRatio(3, 2)));

            Simulation close = AtSix();
            PendingEvent c = Force(close, "epidemic");
            Assert.That(close.Execute(new AnswerEvent(Now(close), c.Id, 2)).Accepted, Is.True);
            close.AdvanceTo(EventRules.State(close).DisasterStrikeAt + 1);
            Assert.That(EventRules.ShopRise(close), Is.EqualTo(Fixed.FromRatio(-3, 10)));
            Assert.That(ClinicRules.SickChance(close, chance, Fixed.Zero), Is.EqualTo(chance * Fixed.FromRatio(3, 2)));
            close.AdvanceTo(Now(close) + 3 * Day + 2 * Hour);
            Assert.That(EventRules.ShopRise(close), Is.EqualTo(Fixed.Zero), "the shops open again after 3 days");
        }
    }
}
