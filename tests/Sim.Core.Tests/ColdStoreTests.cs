using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-242 (spec 025 §3.6, ADR 0134): cold-store space rented by the tonne, potato and fruit kept at a share of their spoilage, the close's rent, and the worked "is it worth it?".</summary>
    public class ColdStoreTests
    {
        private const long Tonne = 1_000_000; // grams

        private static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true, edit: (f, j) => f != "founder.json" ? j // on subsistence, not eating from the store, so the store's grams are exact
                : j.Replace("\"startDietPlan\": \"basic\"", "\"startDietPlan\": \"subsistence\"").Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false"));
            TreasuryTests.Township(sim);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 5_000_000 * Coin), new Posting(Account.OpeningEquity, -5_000_000 * Coin));
            sim.DrainEvents();
            return sim;
        }

        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        // From the Township only; the cap; the rent follows the index and is paid for the days left.
        [Test]
        public void Rent_FromTheTownship()
        {
            Simulation early = TownshipTests.Start(); // not at the Township
            Assert.That(early.Execute(new RentColdStore(early.State.Clock.SimTime, 1)).Reason, Is.EqualTo(Commands.RejectReason.EraLocked));

            Simulation sim = Township();
            long t = sim.State.Clock.SimTime, cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new RentColdStore(t, 21)).Reason, Is.EqualTo(Commands.RejectReason.InvalidArgument), "20 t at most");
            long cost = ColdStoreRules.RentForRestOfSeason(sim, 3);
            Ok(sim, new RentColdStore(t, 3));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(cost));
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.EqualTo(3));
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidRent), Is.EqualTo(cost), "prepaid, expensed through the season");
            Assert.That(ColdStoreRules.SeasonRent(sim, 1), Is.EqualTo(MarketRules.Indexed(sim, 1000 * 100L)), "1.00 a kg a season, at today's prices");
            Assert.That(sim.DrainEvents().OfType<ColdStoreRented>().Single().Tonnes, Is.EqualTo(3));
        }

        // The close takes a full season of the tonnes booked, upfront; renews until stopped; less from the next close.
        [Test]
        public void Close_RentsUpfront_AndRenews()
        {
            Simulation sim = Township();
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 2));
            long season = TreasuryTests.NextClose(sim);
            Assert.That(ColdStoreRules.NextCloseRent(sim), Is.EqualTo(ColdStoreRules.SeasonRent(sim, 2)));
            sim.DrainEvents();
            sim.AdvanceTo(season);
            ObligationPaid paid = sim.DrainEvents().OfType<ObligationPaid>().Single(e => e.Kind == "Cold store");
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.EqualTo(2), "renewed");
            Assert.That(paid.Amount, Is.EqualTo(ColdStoreRules.SeasonRent(sim, 2)).Within(2).Percent, "a full season (the index moved a little)");
            Assert.That(sim.State.Finance.Prepayments.Count(x => x.OwnerRef == "coldstore" && x.Start == season), Is.EqualTo(1));

            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 0));
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.EqualTo(2), "less from the next close");
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.Zero, "ended: nothing more charged");
        }

        // Potato spoils at a tenth of its rate in the cold store; the rest at the full rate.
        [Test]
        public void Potato_KeepsAtATenth()
        {
            Simulation hut = Township(), cold = Township();
            Ok(cold, new RentColdStore(cold.State.Clock.SimTime, 2));
            int potato = Good(hut, "potato");
            long lossHut = DayLoss(hut, potato, 1 * Tonne), lossCold = DayLoss(cold, potato, 1 * Tonne);
            Assert.That(lossHut, Is.InRange(25_000, 35_000), "the hut loses about 3% a day");
            Assert.That(lossCold, Is.InRange(2_500, 3_500), "the cold store about 0.3%");
        }

        private static long DayLoss(Simulation sim, int good, long grams)
        {
            sim.State.Market.Stock[good] = grams;
            sim.AdvanceTo(sim.State.Clock.SimTime + 86400);
            return grams - sim.State.Market.Stock[good];
        }

        // Space fills fastest-spoiling first; what does not fit stays in the hut; the hut's room counts what is in the cold store.
        [Test]
        public void Fills_FastestFirst_AndTheHutLosesWhatIsHere()
        {
            Simulation sim = Township();
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 1));
            int guava = Good(sim, "guava"), potato = Good(sim, "potato"), mango = Good(sim, "mango");
            sim.State.Market.Stock[guava] = 500_000;
            sim.State.Market.Stock[mango] = 400_000;
            sim.State.Market.Stock[potato] = 600_000;
            long[] cold = ColdStoreRules.Cold(sim);
            Assert.That(cold[guava], Is.EqualTo(500_000), "guava spoils fastest: first");
            Assert.That(cold[mango], Is.EqualTo(400_000));
            Assert.That(cold[potato], Is.EqualTo(100_000), "what is left of the tonne");
            Assert.That(ColdStoreRules.UsedGrams(sim), Is.EqualTo(Tonne));
            long hutWith = Store.UsedGrams(sim);
            sim.State.Godown.ColdRentedTonnes = 0;
            Assert.That(Store.UsedGrams(sim) - hutWith, Is.EqualTo(Tonne), "with no space the whole tonne is in the hut");
            Assert.That(ColdStoreRules.Cold(sim), Is.Null);
        }

        // Never below what is stored; a close with the goods in it renews anyway; unpaid, the space ends and the goods go back to the hut.
        [Test]
        public void Goods_KeepTheirSpace_Unpaid_Ends()
        {
            Simulation sim = Township();
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 3));
            int potato = Good(sim, "potato");
            sim.State.Market.Stock[potato] = 2_500_000;
            Assert.That(sim.Execute(new RentColdStore(sim.State.Clock.SimTime, 2)).Reason, Is.EqualTo(Commands.RejectReason.StockTooHigh));
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 3));
            sim.State.Godown.ColdRentNextTonnes = 0; // as if booked down before the potatoes came in
            Assert.That(ColdStoreRules.NextCloseTonnes(sim), Is.EqualTo(3), "kept for the potatoes stored there");
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.EqualTo(3));

            // Cash short at the next close: the space ends.
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.OpeningEquity, cash), new Posting(Account.Cash, -cash));
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(sim.DrainEvents().OfType<ObligationUnpaid>().Any(e => e.Kind == "Cold store"), Is.True);
            Assert.That(sim.State.Godown.ColdRentedTonnes, Is.Zero);
            Assert.That(ColdStoreRules.Cold(sim), Is.Null, "the goods are back in the hut");
        }

        // The close's due line is the close's own figure.
        [Test]
        public void NextSeasonDue_ListsTheColdStore()
        {
            Simulation sim = Township();
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 2));
            CloseDue due = CloseDues.Next(sim).Single(d => d.Kind == DueKind.ColdStore);
            Assert.That(due.Amount, Is.EqualTo(ColdStoreRules.NextCloseRent(sim)).Within(3).Percent, "priced at the index the close will have");
        }

        // The worked answer: potato Spring to Monsoon barely nets, mango Summer to Monsoon pays, guava goes the wrong way (ADR 0125 G8).
        [Test]
        public void Worth_PotatoThin_MangoPays_GuavaNot()
        {
            Simulation sim = Township();
            Assert.That(ColdStoreRules.BestHold(sim, Good(sim, "potato"), out ColdStoreRules.Worth potato), Is.True);
            Assert.That((potato.FromSeason, potato.Seasons), Is.EqualTo((0, 2)), "Spring to Monsoon");
            Assert.That(potato.Net / potato.StartPrice, Is.InRange(Fixed.FromRatio(1, 100), Fixed.FromRatio(6, 100)), "a thin margin");
            Assert.That(ColdStoreRules.BestHold(sim, Good(sim, "mango"), out ColdStoreRules.Worth mango), Is.True);
            Assert.That((mango.FromSeason, mango.Seasons), Is.EqualTo((1, 1)), "Summer to Monsoon");
            Assert.That(mango.Net / mango.StartPrice, Is.GreaterThan(potato.Net / potato.StartPrice), "mango pays better");
            Assert.That(ColdStoreRules.BestHold(sim, Good(sim, "guava"), out ColdStoreRules.Worth _), Is.False, "guava's price falls as it keeps");
            Assert.That(ColdStoreRules.BestHold(sim, Good(sim, "wheat"), out ColdStoreRules.Worth _), Is.False, "wheat is not kept here");
        }

        // AC-11, no loop (ADR 0125 G8): at the 20 t cap the best hold of the best good returns far less than a season's rent is worth in risk: potato under the
        // deposit's 1.5% a season, mango a modest few percent, nothing a bot could compound.
        [Test]
        public void NoMoneyLoop()
        {
            Simulation sim = Township();
            Fixed deposit = Fixed.FromRatio(15, 1000);
            ColdStoreRules.BestHold(sim, Good(sim, "potato"), out ColdStoreRules.Worth potato);
            Fixed potatoPerSeason = potato.Net / potato.StartPrice / Fixed.FromInt(potato.Seasons);
            Assert.That(potatoPerSeason, Is.LessThan(deposit), "keeping potato pays less a season than the savings deposit, with a price risk on top");
            ColdStoreRules.BestHold(sim, Good(sim, "mango"), out ColdStoreRules.Worth mango);
            Fixed mangoPerSeason = mango.Net / mango.StartPrice / Fixed.FromInt(mango.Seasons);
            Assert.That(mangoPerSeason, Is.InRange(deposit, Fixed.FromRatio(10, 100)), "the one that pays: a few percent a season, and only mango");
            long rentFor20 = ColdStoreRules.SeasonRent(sim, 20);
            Assert.That(rentFor20, Is.EqualTo(MarketRules.Indexed(sim, 20_000 * 100L)), "20 t at 1.00 a kg");
        }

        // The rented space is saved from schema 21 and the town reads back the same.
        [Test]
        public void Rented_Survives_A_Save()
        {
            Simulation sim = Township();
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 4));
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 3));
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, 0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That((r.State.Godown.ColdRentedTonnes, r.State.Godown.ColdRentNextTonnes), Is.EqualTo((4, 3)));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
