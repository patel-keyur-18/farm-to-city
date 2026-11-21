using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.EventEngineTests;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>The kit the insurance tests share: a quiet Township with the engine started, a sown field, a business, a fire.</summary>
    internal static class InsuranceKit
    {
        internal const long Day = ClockDef.SecondsPerDay, Hour = 3600;

        internal static long Now(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>The Township at a 6:00 after the engine has seen it (so a card forced now strikes at an exact 6:00); no random events.</summary>
        internal static Simulation AtSix()
        {
            Simulation sim = EventEngineTests.Town(minor: "0", major: "0");
            long six = (Now(sim) / Day + 2) * Day + 6 * Hour;
            sim.AdvanceTo(six);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>The Township at a 6:00 on the first day of a season (a close has just run), so nothing closes for three days: a fire and its repair fit before the next.</summary>
        internal static Simulation AtSeasonStart()
        {
            Simulation sim = AtSix();
            Close(sim);
            sim.AdvanceTo((Now(sim) / Day) * Day + 6 * Hour);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>Takes all but <paramref name="keep"/> of the cash out of the books (an expense), so a premium cannot be paid.</summary>
        internal static void SetCash(Simulation sim, long keep)
        {
            long take = sim.State.Ledger.Cash - keep;
            if (take > 0) sim.State.Ledger.Post(Now(sim), sim.Period, "test", "test", new Posting(Account.EventCosts, take), new Posting(Account.Cash, -take));
        }

        /// <summary>Sows wheat on the first free owned farm acre that is not a well acre; the tile, or −1.</summary>
        internal static int SowWheat(Simulation sim)
        {
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Farm || t.Occupied || !sim.State.Land.Parcels[t.ParcelId].Owned || sim.Config.Game.Land.WellTiles.Contains(t.Id)) continue;
                if (sim.Execute(new Sow(Now(sim), t.Id, "wheat", SeedSource.Certified, "none")).Accepted) return t.Id;
            }
            return -1;
        }

        /// <summary>A tea stall on the books: open, with a last season's profit of <paramref name="profit"/> coins on its report.</summary>
        internal static Business.Business Shop(Simulation sim, long profit)
        {
            string tea = BusinessRules.Defs(sim).Types.First(t => t.Kind == ShopKind.Chai).Id;
            CommandResult found = sim.Execute(new FoundBusiness(Now(sim), tea, 0, 60_000 * Coin, true, null, 0));
            Assert.That(found.Accepted, Is.True, found.Reason.ToString());
            Business.Business b = sim.State.Businesses.Businesses.Last();
            b.Status = BusinessStatus.Open;
            b.ReadyAt = Now(sim);
            if (profit != 0)
                b.Reports.Add(new BusinessReport { Period = sim.Period - 1, Balances = new List<KeyValuePair<Account, long>> { new KeyValuePair<Account, long>(Account.Sales, -profit * Coin) }.AsReadOnly() });
            return b;
        }

        /// <summary>Turns a policy on and makes it cover this season as if its cover had begun long ago.</summary>
        internal static void Cover(Simulation sim, Policy p)
        {
            p.On = true;
            p.PaidSeason = sim.Period;
            p.Since = 0;
        }

        /// <summary>Runs the next season close.</summary>
        internal static void Close(Simulation sim) { sim.AdvanceTo((Now(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 2 * Hour); }

        internal static PendingEvent Repair(Simulation sim) { return EventRules.State(sim).Waiting.Single(p => EventRules.Def(sim).Events[p.Def].Id == EventsDef.FireRepair); }
    }

    /// <summary>US-155 crop insurance (spec 027 §3.5, AC-11): fixed at sowing, luck covered and neglect not.</summary>
    public class CropInsuranceTests
    {
        // The premium: the pest part and the event part, loaded, charged at the sowing; a crop keeps the policy it was sown with.
        [Test]
        public void Premium()
        {
            Simulation sim = InsuranceKit.AtSix();
            Assert.That(sim.Execute(new SetCropInsurance(InsuranceKit.Now(sim), true)).Accepted, Is.True);
            int tile = InsuranceKit.SowWheat(sim);
            Assert.That(tile, Is.GreaterThanOrEqualTo(0));
            Crop c = sim.State.Farm.CropOn(tile);
            Assert.That(c.Insured, Is.True);
            CropDef wheat = sim.Config.Game.Farm.Crops[c.Def];
            long sum = FarmRules.Estimate(sim, sim.State.Land.Tiles[tile], wheat, sim.Config.Game.Farm.Fertilisers[c.Fertiliser]).Get;
            Assert.That(c.SumInsured, Is.EqualTo(sum), "the sow sheet's harvest value at the trader's floor");
            CropInsuranceDef d = sim.Config.Game.Insurance.Crop;
            Fixed rate = (InsuranceRules.PestPart(sim, c.Def, c.Repeats) + d.EventPart) * d.Loading;
            Assert.That(c.Premium, Is.EqualTo(Money.MulRate(sum, rate)), "(pest part + event part) × loading");
            Assert.That(c.Premium, Is.GreaterThan(0));
            Assert.That(sim.State.Ledger.Balance(Account.CropInsurancePremiums), Is.EqualTo(c.Premium), "a farm cost on the books");
            Assert.That(sim.PendingEvents.OfType<PremiumCharged>().Single().Amount, Is.EqualTo(c.Premium));

            Assert.That(sim.Execute(new SetCropInsurance(InsuranceKit.Now(sim), false)).Accepted, Is.True);
            Assert.That(sim.State.Farm.CropOn(tile).Insured, Is.True, "switching off does not touch a crop already sown");
            int next = InsuranceKit.SowWheat(sim);
            Assert.That(sim.State.Farm.CropOn(next).Insured, Is.False, "and the next sowing is not insured");
        }

        // The claim at readiness: luck under the threshold pays the sum insured × the shortfall; soil, water and care are never insured.
        [Test]
        public void Claim_Luck_NotNeglect()
        {
            Simulation sim = InsuranceKit.AtSix();
            sim.Execute(new SetCropInsurance(InsuranceKit.Now(sim), true));
            int luck = InsuranceKit.SowWheat(sim), neglect = InsuranceKit.SowWheat(sim);
            Crop a = sim.State.Farm.CropOn(luck), b = sim.State.Farm.CropOn(neglect);
            int hail = EventRules.Def(sim).IndexOf("hailstorm");
            a.EventHealth = Fixed.FromRatio(1, 2); // hail took half
            a.HitBy = hail + 1;
            b.SoilFactor = b.SoilFactor * Fixed.FromRatio(1, 2); // a worn-out field: its yield falls, but that is neglect, not luck
            long cash = sim.State.Ledger.Cash;
            sim.AdvanceTo(System.Math.Max(a.ReadyAt, b.ReadyAt) + 1);
            sim.DrainEvents();
            a = sim.State.Farm.CropOn(luck);
            b = sim.State.Farm.CropOn(neglect);
            Assert.That(a.State, Is.EqualTo(CropState.Ready));
            Fixed threshold = sim.Config.Game.Insurance.Crop.Threshold;
            Assert.That(InsuranceRules.Index(a), Is.LessThan(threshold));
            long pay = Money.MulRate(a.SumInsured, threshold - InsuranceRules.Index(a));
            Assert.That(sim.State.Ledger.Balance(Account.InsuranceClaims), Is.EqualTo(-pay), "farm income, not passive");
            Assert.That(InsuranceRules.Index(b), Is.GreaterThanOrEqualTo(threshold), "neglect is not in the index");
            Assert.That(b.Breakdown.Soil, Is.LessThan(a.Breakdown.Soil), "the worn field really did yield less");
            InsuranceState st = InsuranceRules.State(sim);
            Assert.That(st.Claims.Single().Cause, Is.EqualTo(hail + 1), "it says hail hit");
            Assert.That(st.Claims.Single().Amount, Is.EqualTo(pay));
            Assert.That(st.Claims.Count, Is.EqualTo(1), "only the field luck hit paid");
        }

        // Cash short at the sowing: it goes uninsured and says so.
        [Test]
        public void CashShort()
        {
            Simulation sim = InsuranceKit.AtSix();
            sim.Execute(new SetCropInsurance(InsuranceKit.Now(sim), true));
            CropDef wheat = sim.Config.Game.Farm.Crops[sim.Config.Game.Farm.IndexOfCrop("wheat")];
            long sowing = FarmRules.SowingCost(sim, wheat, SeedSource.Certified, sim.Config.Game.Farm.Fertilisers[sim.Config.Game.Farm.IndexOfFertiliser("none")]);
            InsuranceKit.SetCash(sim, sowing); // the sowing can be paid, the premium after it cannot
            int tile = InsuranceKit.SowWheat(sim);
            Assert.That(tile, Is.GreaterThanOrEqualTo(0));
            Assert.That(sim.State.Farm.CropOn(tile).Insured, Is.False);
            Assert.That(sim.PendingEvents.OfType<CropNotInsured>().Count(), Is.EqualTo(1));
        }
    }

    /// <summary>US-155 property insurance (spec 027 §3.6, AC-12): a season policy bought at the close.</summary>
    public class PropertyInsuranceTests
    {
        // On mid-season it starts at the next close; the close charges the premium; unpaid, it ends; off, it ends at once.
        [Test]
        public void Premium_Start_Lapse()
        {
            Simulation sim = InsuranceKit.AtSix();
            Assert.That(sim.Execute(new SetPropertyInsurance(InsuranceKit.Now(sim), true)).Accepted, Is.True);
            InsuranceState st = InsuranceRules.State(sim);
            Assert.That(st.Property.Covers(sim.Period), Is.False, "mid-season: it starts at the next close");
            InsuranceKit.Close(sim);
            Assert.That(st.Property.Covers(sim.Period), Is.True);
            Assert.That(st.Property.Since, Is.GreaterThan(0));
            long premium = InsuranceRules.PropertyPremium(sim);
            Assert.That(premium, Is.EqualTo(Money.MulRate(InsuranceRules.PropertyValue(sim), sim.Config.Game.Insurance.Property.Rate)), "0.2% of the book value");
            Assert.That(sim.State.Ledger.Balance(Account.InsurancePremiums), Is.EqualTo(premium), "outside farm profit");
            Assert.That(sim.PendingEvents.OfType<InsuranceStarted>().Count(), Is.EqualTo(1));

            // The next close with no cash: the policy ends, and it is no debt.
            InsuranceKit.SetCash(sim, 0);
            sim.DrainEvents();
            InsuranceKit.Close(sim);
            Assert.That(st.Property.On, Is.False);
            Assert.That(sim.PendingEvents.OfType<InsuranceLapsed>().Count(), Is.EqualTo(1));

            // Off at once, no refund.
            Simulation off = InsuranceKit.AtSix();
            InsuranceKit.Cover(off, InsuranceRules.State(off).Property);
            Assert.That(InsuranceRules.State(off).Property.Covers(off.Period), Is.True);
            off.Execute(new SetPropertyInsurance(InsuranceKit.Now(off), false));
            Assert.That(InsuranceRules.State(off).Property.Covers(off.Period), Is.False);

            // Nothing of yours stands: it cannot be switched on.
            Simulation none = InsuranceKit.AtSix();
            none.State.Land.Buildings.Clear();
            Assert.That(none.Execute(new SetPropertyInsurance(InsuranceKit.Now(none), true)).Reason, Is.EqualTo(RejectReason.NoBuildings));
        }

        private static Building House(Simulation sim)
        {
            var b = new Building { Id = sim.State.Land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = 40, Cost = 60_000 * Coin };
            sim.State.Land.Buildings.Add(b);
            return b;
        }

        // A fire pays 90% of the repair bill that was paid, at once or slowly.
        [Test]
        public void Claim()
        {
            foreach (int choice in new[] { 0, 1 })
            {
                Simulation sim = InsuranceKit.AtSix();
                Building b = House(sim);
                InsuranceKit.Cover(sim, InsuranceRules.State(sim).Property);
                EventEngineTests.Force(sim, "fire", b.Id);
                sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
                PendingEvent repair = InsuranceKit.Repair(sim);
                Assert.That(EventRules.State(sim).FireInsured, Is.True);
                long cash = sim.State.Ledger.Cash;
                long paid = EventRules.ChoiceCost(sim, repair.Def, choice, repair);
                Assert.That(sim.Execute(new AnswerEvent(InsuranceKit.Now(sim), repair.Id, choice)).Accepted, Is.True);
                long claim = Money.MulRate(paid, Fixed.FromRatio(90, 100));
                Assert.That(sim.State.Ledger.Balance(Account.InsuranceClaimsReceived), Is.EqualTo(-claim), "other income, outside farm profit");
                Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - paid + claim), "you carry the 10% excess");
                Assert.That(sim.PendingEvents.OfType<ClaimPaid>().Single().Amount, Is.EqualTo(claim));
                Assert.That(InsuranceRules.State(sim).Claims.Single().Target, Is.EqualTo(b.Id));
            }
        }

        // A risk already signalled is not insurable: cover that began after the warning pays nothing.
        [Test]
        public void SignalledNotInsurable()
        {
            Simulation sim = InsuranceKit.AtSix();
            Building b = House(sim);
            InsuranceKit.Cover(sim, InsuranceRules.State(sim).Property);
            EventEngineTests.Force(sim, "fire", b.Id);
            InsuranceRules.State(sim).Property.Since = InsuranceKit.Now(sim) + 1; // the cover began after the warning
            sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
            Assert.That(EventRules.State(sim).FireInsured, Is.False);
            PendingEvent repair = InsuranceKit.Repair(sim);
            sim.Execute(new AnswerEvent(InsuranceKit.Now(sim), repair.Id, 0));
            Assert.That(sim.State.Ledger.Balance(Account.InsuranceClaimsReceived), Is.EqualTo(0));
            Assert.That(sim.PendingEvents.OfType<ClaimPaid>(), Is.Empty);
        }
    }

    /// <summary>US-155 business insurance (spec 027 §3.6, AC-12): the business pays its own premium; a fire and a pause pay lost days.</summary>
    public class BusinessInsuranceTests
    {
        // The premium: 0.2% of its building and equipment, and 3% of last season's profit; from the business's own cash.
        [Test]
        public void Premium_FromItsOwnCash()
        {
            Simulation sim = InsuranceKit.AtSix();
            Business.Business b = InsuranceKit.Shop(sim, 35_000);
            long book = InsuranceRules.BusinessBook(b);
            Assert.That(book, Is.GreaterThan(0));
            BusinessInsuranceDef d = sim.Config.Game.Insurance.Business;
            Assert.That(InsuranceRules.BusinessPremium(sim, b), Is.EqualTo(Money.MulRate(book, d.Rate) + Money.MulRate(35_000 * Coin, d.ProfitShare)));
            Assert.That(sim.Execute(new SetBusinessInsurance(InsuranceKit.Now(sim), b.Id, true)).Accepted, Is.True);
            long founder = sim.State.Ledger.Cash;
            InsuranceKit.Close(sim);
            Policy p = InsuranceRules.State(sim).BusinessPolicy(b.Id);
            Assert.That(p.Covers(sim.Period), Is.True);
            Assert.That(b.Ledger.Balance(Account.InsurancePremiums), Is.GreaterThan(0), "in the business's own books");
            Assert.That(sim.State.Ledger.Balance(Account.InsurancePremiums), Is.EqualTo(0), "never in the founder's");
            Assert.That(sim.Execute(new SetBusinessInsurance(InsuranceKit.Now(sim), 9999, true)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
        }

        // AC-12: a business that cannot pay its premium from its own cash and overdraft loses the policy at the close (no debt, no claim after).
        [Test]
        public void Premium_Unpaid_Lapses()
        {
            Simulation sim = InsuranceKit.AtSix();
            Business.Business b = InsuranceKit.Shop(sim, 35_000);
            Assert.That(sim.Execute(new SetBusinessInsurance(InsuranceKit.Now(sim), b.Id, true)).Accepted, Is.True);
            long cash = b.Ledger.Cash; // all its cash spent, then all its overdraft drawn and spent
            b.Ledger.Post(InsuranceKit.Now(sim), sim.Period, "test", "test", new Posting(Account.EventCosts, cash), new Posting(Account.Cash, -cash));
            long limit = BusinessBooks.CanSpend(sim, b);
            BusinessBooks.Draw(sim, b, limit);
            b.Ledger.Post(InsuranceKit.Now(sim), sim.Period, "test", "test", new Posting(Account.EventCosts, b.Ledger.Cash), new Posting(Account.Cash, -b.Ledger.Cash));
            Assert.That(BusinessBooks.CanSpend(sim, b), Is.LessThan(InsuranceRules.BusinessPremium(sim, b)), "it cannot pay");
            InsuranceKit.Close(sim);
            Policy p = InsuranceRules.State(sim).BusinessPolicy(b.Id);
            Assert.That(p.On, Is.False, "the policy ended");
            Assert.That(p.Covers(sim.Period), Is.False);
            Assert.That(sim.PendingEvents.OfType<InsuranceLapsed>().Count(), Is.EqualTo(1));
            Assert.That(b.Ledger.Balance(Account.InsurancePremiums), Is.EqualTo(0), "nothing was charged");
        }

        // A fire on the shop: 90% of the repair bill paid, and 80% of its daily profit for each day shut into its own books.
        [Test]
        public void Claim_LostDays()
        {
            Simulation sim = InsuranceKit.AtSeasonStart();
            Business.Business b = InsuranceKit.Shop(sim, 35_000);
            InsuranceKit.Cover(sim, InsuranceRules.State(sim).BusinessPolicyOrNew(b.Id));
            long book = InsuranceRules.BusinessBook(b);
            EventEngineTests.Force(sim, "fire", b.Id, 1);
            sim.AdvanceTo(EventRules.State(sim).DisasterStrikeAt + 1);
            FireStruck f = sim.PendingEvents.OfType<FireStruck>().Single();
            Assert.That(f.Business, Is.True, "a shop burns like a house");
            Assert.That(f.Bill, Is.EqualTo(Money.MulRate(book, Fixed.FromRatio(35, 100) * (UtilityRulesCovers(sim, b) ? Fixed.FromRatio(1, 2) : Fixed.One))));
            Assert.That(EventRules.Shut(sim, b), Is.True, "shut for the repair");
            PendingEvent repair = InsuranceKit.Repair(sim);
            long founder = sim.State.Ledger.Cash, shop = b.Ledger.Cash;
            Assert.That(sim.Execute(new AnswerEvent(InsuranceKit.Now(sim), repair.Id, 0)).Accepted, Is.True);
            long repairShare = Money.MulRate(f.Bill, Fixed.FromRatio(90, 100));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(founder - f.Bill + repairShare));
            long lost = Money.MulRate(Money.MulRate(35_000 * Coin, Fixed.FromRatio(80, 100)), Fixed.FromRatio(2 * 86400, (int)sim.Clock.SecondsPerSeason));
            Assert.That(b.Ledger.Cash, Is.EqualTo(shop + lost), "two days of a four-day season's profit, 80% of it");
            Assert.That(b.Ledger.Balance(Account.InsuranceClaimsReceived), Is.EqualTo(-lost));
            Assert.That(InsuranceRules.State(sim).Claims.Single().Days, Is.EqualTo(2));
        }

        private static bool UtilityRulesCovers(Simulation sim, Business.Business b) { return UtilityRules.Covers(sim, UtilityDef.Tower, b.TileId); }

        // A supplier strike's Pause the line pays its lost days; the other choices pay nothing.
        [Test]
        public void Claim_Pause()
        {
            Simulation sim = InsuranceKit.AtSeasonStart();
            Business.Business b = InsuranceKit.Shop(sim, 35_000);
            InsuranceKit.Cover(sim, InsuranceRules.State(sim).BusinessPolicyOrNew(b.Id));
            long shop = b.Ledger.Cash;
            PendingEvent strike = EventEngineTests.Force(sim, "supplier_strike", b.Id);
            Assert.That(sim.Execute(new AnswerEvent(InsuranceKit.Now(sim), strike.Id, 1)).Accepted, Is.True);
            long lost = Money.MulRate(Money.MulRate(35_000 * Coin, Fixed.FromRatio(80, 100)), Fixed.FromRatio(3 * 86400, (int)sim.Clock.SecondsPerSeason));
            Assert.That(b.Ledger.Cash, Is.EqualTo(shop + lost), "three days shut, 80% of the lost profit");
        }
    }

    /// <summary>US-155 (spec 027 AC-14): premiums and claims keep both ledgers exact, and never count as passive income.</summary>
    public class RiskLedgerTests
    {
        [Test]
        public void Reconciles()
        {
            Simulation sim = InsuranceKit.AtSix();
            sim.Execute(new SetCropInsurance(InsuranceKit.Now(sim), true));
            sim.Execute(new SetPropertyInsurance(InsuranceKit.Now(sim), true));
            Business.Business b = InsuranceKit.Shop(sim, 35_000);
            sim.Execute(new SetBusinessInsurance(InsuranceKit.Now(sim), b.Id, true));
            int tile = InsuranceKit.SowWheat(sim);
            sim.State.Farm.CropOn(tile).EventHealth = Fixed.FromRatio(1, 2);
            long before = Hub.Passive(sim, out _, out _, out _);
            sim.AdvanceTo(sim.State.Farm.CropOn(tile).ReadyAt + 1);
            InsuranceKit.Close(sim);
            InsuranceKit.Close(sim);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(b.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Finance.Reports.Last().Lines.Any(l => l.Key == "farmProfit"), Is.True);
            long claims = InsuranceRules.State(sim).ClaimsReceived;
            Assert.That(claims, Is.GreaterThan(0));
            Assert.That(Hub.Passive(sim, out _, out _, out _), Is.EqualTo(before), "no claim is passive income");
        }
    }
}
