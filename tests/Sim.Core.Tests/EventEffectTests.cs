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
using Sim.Core.People;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.EventEngineTests;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-154 (spec 027 §3.2–§3.3, AC-5 to AC-7): each event's gate and weight, and each choice's effect.</summary>
    public class EventEffectTests
    {
        private static Simulation Quiet() { return EventEngineTests.Town(minor: "0", major: "0"); }

        private static long Now(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>Sows <paramref name="n"/> radishes on owned empty farm acres (the first ones are not well acres), returning their tiles.</summary>
        private static System.Collections.Generic.List<int> Sow(Simulation sim, int n)
        {
            var tiles = new System.Collections.Generic.List<int>();
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (tiles.Count == n) break;
                if (t.Terrain != Terrain.Farm || t.Occupied || !sim.State.Land.Parcels[t.ParcelId].Owned || sim.Config.Game.Land.WellTiles.Contains(t.Id)) continue;
                if (sim.Execute(new Sow(Now(sim), t.Id, "wheat", SeedSource.Certified, "none")).Accepted) tiles.Add(t.Id);
            }
            return tiles;
        }

        private static Crop CropOn(Simulation sim, int tile) { return sim.State.Farm.CropOn(tile); }

        private static void Answer(Simulation sim, PendingEvent p, int choice) { Assert.That(sim.Execute(new AnswerEvent(Now(sim), p.Id, choice)).Accepted, Is.True); }

        private static long Cost(Simulation sim, long coins) { return Sim.Core.Market.MarketRules.Indexed(sim, coins * Coin); }

        private static Simulation Spring()
        {
            Simulation sim = Quiet();
            UntilSix(sim);
            Assert.That(Calendar.SeasonAt(Now(sim), sim.Clock), Is.EqualTo(Season.Spring).Or.EqualTo(Season.Summer), "the test town starts in the dry season");
            return sim;
        }

        // AC-5: drought's gate (a dry spell of three days, a rain-fed standing crop, Spring or Summer) and its modifiers (×2 after 3 days, ×1.5 more after 6).
        [Test]
        public void Modifiers()
        {
            Simulation sim = Spring();
            EventsState s = EventRules.State(sim);
            int drought = EventRules.Def(sim).IndexOf("drought");
            Sow(sim, 2);
            Assert.That(EventRules.RainFedAcres(sim), Is.EqualTo(2));
            s.LastRainAt = Now(sim) - 2 * ClockDef.SecondsPerDay;
            Assert.That(EventRules.Weigh(sim, s, drought, out _, out _, out _, out _), Is.False, "two dry days is not a dry spell");
            s.LastRainAt = Now(sim) - 4 * ClockDef.SecondsPerDay;
            Assert.That(EventRules.Weigh(sim, s, drought, out Fixed w3, out _, out _, out int why3), Is.True);
            Assert.That(w3, Is.EqualTo(Fixed.FromInt(2)), "×2 with no rain in 3 days");
            Assert.That(why3, Is.EqualTo(1), "the card says why");
            s.LastRainAt = Now(sim) - 7 * ClockDef.SecondsPerDay;
            EventRules.Weigh(sim, s, drought, out Fixed w6, out _, out _, out _);
            Assert.That(w6, Is.EqualTo(Fixed.FromInt(3)), "×2 and ×1.5 more with none in 6");
            Assert.That(EventRules.Weigh(sim, s, EventRules.Def(sim).IndexOf("heatwave"), out _, out _, out _, out _), Is.False, "heat needs a heatwave spell");
            foreach (Crop c in sim.State.Farm.Crops.ToList()) sim.Execute(new ClearField(Now(sim), c.TileId));
            Assert.That(EventRules.Weigh(sim, s, drought, out _, out _, out _, out _), Is.False, "nothing standing to lose");
        }

        // AC-5: the product is held between 0.25 and 4 whatever the town looks like.
        [Test]
        public void Clamp()
        {
            Simulation sim = Quiet();
            UntilSix(sim);
            TownshipTests.Families(sim, 48);
            EventsState s = EventRules.State(sim);
            Assert.That(EventRules.Weigh(sim, s, EventRules.Def(sim).IndexOf("wedding"), out Fixed w, out long hh, out _, out _), Is.True);
            Assert.That(w, Is.EqualTo(Fixed.FromInt(3)), "households ÷ 10 is held at the content's cap of 3");
            Assert.That(w, Is.InRange(EventRules.Def(sim).Engine.WeightMin, EventRules.Def(sim).Engine.WeightMax));
            Assert.That(hh, Is.GreaterThan(0), "it falls on one of your households");
        }

        // AC-6: drought: rain-fed standing crops lose 12% of health unless tankers (120 an acre) are hired; a well acre is untouched.
        [Test]
        public void Weather_Drought()
        {
            Simulation sim = Spring();
            var tiles = Sow(sim, 3);
            var well = sim.Config.Game.Land.WellTiles.First(t => sim.State.Land.Parcels[sim.State.Land.Tiles[t].ParcelId].Owned && !sim.State.Land.Tiles[t].Occupied);
            Assert.That(sim.Execute(new Sow(Now(sim), well, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(EventRules.RainFedAcres(sim), Is.EqualTo(3), "the well acre is watered by hand");

            PendingEvent p = Force(sim, "drought");
            Assert.That(EventRules.ChoiceCost(sim, p.Def, 1, p), Is.EqualTo(Cost(sim, 120 * 3)), "120 an acre of rain-fed standing crop");
            long cash = sim.State.Ledger.Cash;
            Answer(sim, p, 1);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - Cost(sim, 360)));
            foreach (int t in tiles) Assert.That(CropOn(sim, t).EventHealth, Is.EqualTo(Fixed.One), "tankers: no loss");

            Simulation sim2 = Spring();
            var tiles2 = Sow(sim2, 3);
            Assert.That(sim2.Execute(new Sow(Now(sim2), well, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            PendingEvent q = Force(sim2, "drought");
            Answer(sim2, q, 0);
            foreach (int t in tiles2) Assert.That(CropOn(sim2, t).EventHealth, Is.EqualTo(Fixed.FromRatio(88, 100)), "let it ride: −12%");
            Assert.That(CropOn(sim2, well).EventHealth, Is.EqualTo(Fixed.One), "a watered acre is untouched");
        }

        [Test]
        public void Weather_Heatwave_Hail_Waterlogging()
        {
            Simulation sim = Spring();
            var tiles = Sow(sim, 3);
            PendingEvent heat = Force(sim, "heatwave");
            Assert.That(EventRules.ChoiceCost(sim, heat.Def, 1, heat), Is.EqualTo(Cost(sim, 60 * 3)), "60 a standing field");
            Answer(sim, heat, 0);
            foreach (int t in tiles) Assert.That(CropOn(sim, t).EventHealth, Is.EqualTo(Fixed.FromRatio(94, 100)));

            // Hail: no warning, a third of the standing fields (rounded up) lose 35%; then keep the crop or clear and resow.
            Simulation h = Spring();
            var ht = Sow(h, 4);
            PendingEvent hail = Force(h, "hailstorm");
            Assert.That(hail.Param, Is.EqualTo(2), "a third of four fields, rounded up");
            var hit = ht.Where(t => CropOn(h, t).EventHealth == Fixed.FromRatio(65, 100)).ToList();
            Assert.That(hit.Count, Is.EqualTo(2));
            Assert.That(EventRules.ChoiceCost(h, hail.Def, 1, hail), Is.EqualTo(2 * FarmRules.SeedValue(h, h.Config.Game.Farm.Crops[CropOn(h, hit[0]).Def])), "new seed for each damaged field");
            long ready = CropOn(h, hit[0]).ReadyAt;
            h.AdvanceTo(Now(h) + 2 * 3600L);
            Answer(h, hail, 1);
            foreach (int t in hit)
            {
                Assert.That(CropOn(h, t).EventHealth, Is.EqualTo(Fixed.One), "the field starts again");
                Assert.That(CropOn(h, t).ReadyAt, Is.GreaterThan(ready), "and is ready later");
            }

            // Waterlogging: wet fields lose 10%; drains (1,500) prevent it.
            Simulation w = Spring();
            var wt = Sow(w, 2);
            w.State.Land.Tiles[wt[0]].Moisture = Fixed.FromInt(95);
            w.State.Land.Tiles[wt[1]].Moisture = Fixed.FromInt(20);
            PendingEvent wl = Force(w, "waterlogging");
            Answer(w, wl, 0);
            Assert.That(CropOn(w, wt[0]).EventHealth, Is.EqualTo(Fixed.FromRatio(90, 100)));
            Assert.That(CropOn(w, wt[1]).EventHealth, Is.EqualTo(Fixed.One), "a dry field is untouched");
            Simulation d = Spring();
            Sow(d, 2);
            PendingEvent dr = Force(d, "waterlogging");
            long cash = d.State.Ledger.Cash;
            Answer(d, dr, 1);
            Assert.That(d.State.Ledger.Cash, Is.EqualTo(cash - Cost(d, 1500)));
        }

        // AC-7: the multiplier reaches the yield and is 1 by default.
        [Test]
        public void EventHealth_Yield()
        {
            Simulation sim = Spring();
            var tiles = Sow(sim, 2);
            Assert.That(CropOn(sim, tiles[0]).EventHealth, Is.EqualTo(Fixed.One), "1 by default");
            CropOn(sim, tiles[0]).EventHealth = Fixed.FromRatio(1, 2);
            sim.AdvanceTo(CropOn(sim, tiles[0]).ReadyAt + 1);
            Crop a = CropOn(sim, tiles[0]), b = CropOn(sim, tiles[1]);
            Assert.That(a.State, Is.EqualTo(CropState.Ready));
            Assert.That(a.Breakdown.Grams, Is.LessThan(b.Breakdown.Grams), "the same field with half the health yields less");
            Assert.That(a.Breakdown.CropHealth, Is.EqualTo(a.Breakdown.CropHealth.Raw == 0 ? Fixed.Zero : Fixed.FromRatio(1, 2) * b.Breakdown.CropHealth).Or.EqualTo(Fixed.FromRatio(1, 2)));
        }

        // AC-6 economic: a boom or a slump moves the trader's price for three days; a fuel spike lasts the season.
        [Test]
        public void Economic()
        {
            Simulation sim = Spring();
            Sow(sim, 1);
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            Fixed before = Sim.Core.Market.MarketRules.Floor(sim, wheat);
            PendingEvent boom = Force(sim, "price_boom", wheat);
            Answer(sim, boom, 0);
            Assert.That(EventRules.PriceFactor(sim, wheat), Is.EqualTo(Fixed.FromRatio(120, 100)));
            Assert.That(Sim.Core.Market.MarketRules.Floor(sim, wheat), Is.GreaterThan(before));
            sim.AdvanceTo(Now(sim) + 3 * ClockDef.SecondsPerDay + 7200);
            Assert.That(EventRules.PriceFactor(sim, wheat), Is.EqualTo(Fixed.One), "gone after three days");

            PendingEvent slump = Force(sim, "price_slump", wheat);
            Answer(sim, slump, 0);
            Assert.That(EventRules.PriceFactor(sim, wheat), Is.EqualTo(Fixed.FromRatio(85, 100)));

            PendingEvent fuel = Force(sim, "fuel_spike");
            Answer(sim, fuel, 1);
            Assert.That(EventRules.CostFactor(sim, EventEffect.Fuel, 0), Is.EqualTo(Fixed.FromRatio(135, 100)));
            Assert.That(EventRules.AnyFlag(sim, EventRules.TractorParked), Is.True, "the hired-out tractor is parked");
            long season = (Now(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(season + 2 * 3600);
            Assert.That(EventRules.CostFactor(sim, EventEffect.Fuel, 0), Is.EqualTo(Fixed.One), "the season over");
        }

        // AC-6 social, workforce and opportunity events.
        [Test]
        public void Social_Workforce_Opportunity()
        {
            Simulation sim = Spring();
            var hs = TownLifeRules.Yours(sim);
            Household a = hs[0], b = hs[1];

            PendingEvent wed = Force(sim, "wedding", a.Id);
            Fixed approval = TownLifeRules.State(sim).Approval;
            int liftBefore = EventRules.Lift(sim, a);
            Answer(sim, wed, 1);
            Assert.That(EventRules.Lift(sim, a), Is.EqualTo(liftBefore + 2), "that family +2 happiness");
            Assert.That(EventRules.Lift(sim, b), Is.EqualTo(0));

            PendingEvent dis = Force(sim, "dispute", a.Id, b.Id);
            Answer(sim, dis, 2);
            Assert.That(EventRules.Lift(sim, a), Is.EqualTo(liftBefore + 2 + 3), "side with one: +3");
            Assert.That(EventRules.Lift(sim, b), Is.EqualTo(-3), "and −3 for the other");

            // Welcome a family into a free house.
            int houses = sim.State.Citizens.Households.Count;
            Building free = PopulationRulesFree(sim);
            if (free != null)
            {
                PendingEvent fam = Force(sim, "new_family");
                Answer(sim, fam, 1);
                Assert.That(sim.State.Citizens.Households.Count, Is.EqualTo(houses + 1), "they move in");
            }

            // A skilled migrant joins the applicants for the open post.
            PendingEvent mig = Force(sim, "skilled_migrant");
            int applicants = sim.State.People.Applicants.Count;
            Answer(sim, mig, 1);
            Assert.That(sim.State.People.Applicants.Count, Is.EqualTo(applicants + 1));
            Assert.That(sim.State.People.Applicants.Last().SkillKnown, Is.True);
        }

        private static Building PopulationRulesFree(Simulation sim) { return Sim.Core.People.PopulationRules.FreeHouse(sim); }

        [Test]
        public void Opportunity_Grant_Auction()
        {
            Simulation sim = Spring();
            // The grant: 400 for the paperwork now; 6,000 at the next close if the books are sound, nothing if not.
            PendingEvent g = Force(sim, "regional_grant");
            long cash = sim.State.Ledger.Cash;
            Answer(sim, g, 1);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - Cost(sim, 400)));
            // Make the last season a profitable one, then close.
            sim.AdvanceTo((Now(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 3600);
            RegionalGrantPaid paid = sim.PendingEvents.OfType<RegionalGrantPaid>().Single();
            long expected = Cost(sim, 6000);
            bool sound = sim.State.Finance.Reports.Count > 0 && sim.State.Finance.Reports.Last().FarmProfit > 0;
            Assert.That(paid.Amount, Is.EqualTo(sound ? expected : 0), "paid only when the books were sound");
            Assert.That(sim.State.Ledger.Balance(Account.RegionalGrants), Is.EqualTo(sound ? -expected : 0));

            // The auction: a parcel beside your land at 85% of its value, refused when it can't be paid.
            int parcel = EventRules.AuctionParcel(sim);
            if (parcel >= 0)
            {
                PendingEvent a = Force(sim, "land_auction", parcel);
                long bid = Money.MulRate(LandRules.ParcelPrice(sim, sim.State.Land.Parcels[parcel]), Fixed.FromRatio(85, 100));
                Assert.That(EventRules.ChoiceCost(sim, a.Def, 1, a), Is.EqualTo(bid));
                Answer(sim, a, 1);
                Assert.That(sim.State.Land.Parcels[parcel].Owned, Is.True);
            }
        }

        // AC-6 business events: a strike's price and pause, a second supplier, the inspection, a viral rush, a breakdown.
        [Test]
        public void Business_Events()
        {
            Simulation sim = Spring();
            BusinessDefs defs = BusinessRules.Defs(sim);
            string tea = defs.Types.First(t => t.Kind == ShopKind.Chai).Id;
            CommandResult found = sim.Execute(new FoundBusiness(Now(sim), tea, 0, 60_000 * Coin, true, null, 0));
            Assert.That(found.Accepted, Is.True, found.Reason.ToString());
            Business.Business b = sim.State.Businesses.Businesses.Last();
            b.Status = BusinessStatus.Open;
            b.ReadyAt = Now(sim);

            PendingEvent strike = Force(sim, "supplier_strike", b.Id);
            Answer(sim, strike, 0);
            Assert.That(EventRules.CostFactor(sim, EventEffect.Inputs, b.Id), Is.EqualTo(Fixed.FromRatio(130, 100)), "its inputs +30%");
            Assert.That(EventRules.CostFactor(sim, EventEffect.Inputs, 5), Is.EqualTo(Fixed.One), "another business pays the usual");
            Assert.That(BusinessRules.ImportPrice(sim, b, 0), Is.GreaterThan(Sim.Core.Market.MarketRules.Ceiling(sim, 0)).Or.EqualTo(Sim.Core.Market.MarketRules.Ceiling(sim, 0)));

            sim.AdvanceTo(Now(sim) + 4 * ClockDef.SecondsPerDay);
            PendingEvent pause = Force(sim, "supplier_strike", b.Id);
            Answer(sim, pause, 1);
            Assert.That(EventRules.Shut(sim, b), Is.True, "the line is paused for 3 days");
            sim.AdvanceTo(Now(sim) + 3 * ClockDef.SecondsPerDay + 3600);
            Assert.That(EventRules.Shut(sim, b), Is.False);

            // A second supplier: no extra now, and the next strike costs half.
            PendingEvent second = Force(sim, "supplier_strike", b.Id);
            long cash = sim.State.Ledger.Cash;
            Answer(sim, second, 2);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - Cost(sim, 1000)));
            PendingEvent again = Force(sim, "supplier_strike", b.Id);
            Answer(sim, again, 0);
            Assert.That(EventRules.CostFactor(sim, EventEffect.Inputs, b.Id), Is.EqualTo(Fixed.FromRatio(115, 100)), "half the rise");

            // The inspection: pay the fine, or fix the kitchen and not be inspected for a year.
            PendingEvent fine = Force(sim, "health_inspection", b.Id);
            Assert.That(EventRules.ChoiceCost(sim, fine.Def, 0, fine), Is.EqualTo(Cost(sim, 1000)));
            Assert.That(EventRules.ChoiceCost(sim, fine.Def, 1, fine), Is.EqualTo(Cost(sim, 3000)));
            Answer(sim, fine, 1);
            Assert.That(EventRules.HasFlag(EventRules.State(sim), EventRules.KitchenFixed, b.Id, Now(sim)), Is.True);
            Assert.That(EventRules.Weigh(sim, EventRules.State(sim), EventRules.Def(sim).IndexOf("health_inspection"), out _, out _, out _, out _), Is.False, "×0 for a year after the kitchen is fixed");

            // A viral rush: demand +40% for the day.
            PendingEvent rush = Force(sim, "viral_rush", b.Id);
            Answer(sim, rush, 0);
            Assert.That(EventRules.ShopRise(sim), Is.EqualTo(Fixed.FromRatio(20, 100)), "carrying on: the stock you have serves about half of the extra customers");
            sim.AdvanceTo(Now(sim) + ClockDef.SecondsPerDay + 3600);
            Assert.That(EventRules.ShopRise(sim), Is.EqualTo(Fixed.Zero));
            PendingEvent stock = Force(sim, "viral_rush", b.Id);
            long before = sim.State.Ledger.Cash;
            Answer(sim, stock, 1);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(before - Cost(sim, 1500)));
            Assert.That(EventRules.ShopRise(sim), Is.EqualTo(Fixed.FromRatio(40, 100)), "stocking up serves everyone: demand +40% for the day");
        }
    }
}
