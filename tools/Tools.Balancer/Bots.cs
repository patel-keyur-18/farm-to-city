using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Tools.Balancer
{
    /// <summary>
    /// A player bot: once an hour it looks at the farm and issues commands through <see cref="Simulation.Execute"/>, like
    /// a player would. Bots use only public commands and read-only state (ADR 0034 §7), so anything a bot does, a player
    /// can do in the game.
    /// </summary>
    public abstract class Bot
    {
        /// <summary>The well acres first, then the rain-fed inherited acres (ADR 0032).</summary>
        protected static readonly int[] Acres = { 20, 28, 18, 19, 26, 27 };

        public abstract string Name { get; }

        /// <summary>Called at a new game, before the first hour.</summary>
        public virtual void Start(Simulation sim) { }

        /// <summary>One round at sim time t (the sim has been advanced to t).</summary>
        public abstract void Play(Simulation sim, long t);

        protected static bool Ok(Simulation sim, Command c) { return sim.Execute(c).Accepted; }

        protected static int Season(Simulation sim, long t) { return (int)Sim.Core.Time.Calendar.SeasonAt(t, sim.Clock); }
    }

    /// <summary>
    /// The competent founder (US-013, F00.3): the best crop in season on as many acres as Care ≥ 1.0 allows (the well acres
    /// first, compost on tired soil), the kitchen garden, fruit trees in the yard, spare hours sold as day labour, and from the
    /// end of the first Spring a resident hand with standing orders (ADR 0039 §10).
    /// Selling: a cart to the mandi when it beats the trader after its labour, a small minimum-price offer on the local
    /// market, the rest to the trader.
    /// </summary>
    public class FarmerBot : Bot
    {
        protected static readonly string[] BestCrop = { "potato", "lentils", "rice", "potato" }; // Spring, Summer, Monsoon, Winter

        /// <summary>The hand's extra acres: wheat in Winter, so a bigger farm doesn't glut the valley with potato.</summary>
        protected static readonly string[] HandCrop = { "potato", "lentils", "rice", "wheat" };

        public override string Name { get { return "competent"; } }

        public int MandiTrips { get; private set; }

        /// <summary>True when the bot keeps its produce in the store instead of selling it (a stress bot's shelter attempt).</summary>
        protected virtual bool HoldsProduce(Simulation sim) { return false; }

        public override void Start(Simulation sim)
        {
            // Two mangoes and two guavas in the yard: cheap, long-lived, and in slots that shade no field.
            int n = 0;
            foreach (Sim.Core.Land.TreeSlot s in sim.State.Land.Slots)
            {
                if (s.Kind != Sim.Core.Land.SlotKind.Yard || s.TreeId != 0 || n >= 4) continue;
                Ok(sim, new PlantTree(sim.State.Clock.SimTime, s.Id, n < 2 ? "mango" : "guava"));
                n++;
            }
        }

        public override void Play(Simulation sim, long t)
        {
            SprayOutbreaks(sim, t);
            if (sim.State.Farm.Founder.Health < Fixed.Parse("0.9")) Ok(sim, new BuyMedicine(t));
            Harvest(sim, t);
            if (!HoldsProduce(sim)) Sell(sim, t);
            Employ(sim, t);
            Sow(sim, t, BestCrop[Season(sim, t)]);
            Garden(sim, t);
            SplitLabour(sim, t);
        }

        protected static void Harvest(Simulation sim, long t)
        {
            foreach (int tile in Acres)
            {
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) Ok(sim, new Harvest(t, tile, Overflow.SellRestToTrader));
            }
            foreach (Tree tree in sim.State.Farm.Trees)
                if (tree.RipeGrams > 0) Ok(sim, new PickFruit(t, tree.SlotId, Overflow.SellRestToTrader));
        }

        /// <summary>A curative spray on every warned crop: the dose costs far less than the damage it saves.</summary>
        protected static void SprayOutbreaks(Simulation sim, long t)
        {
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Growing && c.Outbreak != Outbreak.None && c.Spray == SprayState.None) Ok(sim, new Spray(t, c.TileId));
        }

        /// <summary>Sows the crop on vacant acres while the founder's hours cover the demand (Care ≥ 1.0).</summary>
        protected static void Sow(Simulation sim, long t, string cropId)
        {
            if (sim.State.Farm.Founder.AutoSubsistence) return; // feed the household first: day labour until the plan is back
            FarmDefs farm = sim.Config.Game.Farm;
            CropDef crop = farm.Crops[farm.IndexOfCrop(cropId)];
            Fixed supply = FounderRules.LabourRate(sim) + FounderRules.MandiCut(sim) + PeopleRules.HiredHours(sim);
            foreach (int tile in Acres)
            {
                if (sim.State.Farm.CropOn(tile) != null) continue;
                if (FarmRules.LabourDemand(sim) + crop.LabourPerAcrePerDay > supply) break;
                if (!Ok(sim, new Sow(t, tile, crop.Id, SeedSource.Certified, Fertiliser(sim, tile)))) break;
            }
        }

        /// <summary>Compost on tired soil (fertility below 55): it keeps a fully cropped farm from running down.</summary>
        protected static string Fertiliser(Simulation sim, int tile)
        {
            return sim.State.Land.Tiles[tile].Fertility < Fixed.FromInt(55) ? "compost" : "none";
        }

        /// <summary>The sell rule the bot's hands follow (the competent bot sells by itself, so its hands store).</summary>
        protected virtual SellRule OrderSell { get { return SellRule.Store; } }

        /// <summary>The applicant's expected hours a day: the middle of the shown skill range and the traits known so far.</summary>
        protected static Fixed Expected(Simulation sim, Applicant a)
        {
            PeopleDefs p = sim.Config.Game.People;
            PeopleRules.SkillRange(sim, a, out int lo, out int hi);
            Fixed hours = p.Hand.HoursPerDay * (p.Hand.ProductivityBase + p.Hand.ProductivityPerSkill.MulRatio(lo + hi, 200));
            hours = hours * p.Traits[a.Trait1].HoursFactor;
            return a.SecondTraitKnown ? hours * p.Traits[a.Trait2].HoursFactor : hours;
        }

        /// <summary>
        /// Hiring like a good player (ADR 0039 §10): a hand's hut at the end of the first Spring (a small moneylender loan if
        /// cash is short), a market-wage post, interviews, an offer to the best applicant once two wait or one has waited a
        /// day, renewal within the grace, and orders on the inherited acres each season.
        /// </summary>
        protected virtual void Employ(Simulation sim, long t)
        {
            PeopleState p = sim.State.People;
            if (p == null) return;
            long S = sim.Clock.SecondsPerSeason;
            if (PeopleRules.HutCount(sim) == 0 && t >= S - ClockDef.SecondsPerDay / 2)
            {
                long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[sim.Config.Game.People.HandHut].Value);
                Borrow(sim, t, cost + 40_000 * Money.Coin);
                Ok(sim, new BuildHandHut(t, 37)); // the labour quarters (ADR 0040)
            }
            if (p.Post == null && p.Hands.Count == 0 && PeopleRules.HutCount(sim) > 0 && !_posted) _posted = Ok(sim, new PostJob(t, PostAboveMarket));
            if (PeopleRules.EmptyHut(sim) != null && p.Applicants.Count > 0)
            {
                foreach (Applicant a in p.Applicants.ToArray()) if (!a.SecondTraitKnown) Ok(sim, new Interview(t, a.Id));
                Applicant best = null;
                foreach (Applicant a in p.Applicants) if (Acceptable(sim, a) && (best == null || Expected(sim, a) > Expected(sim, best))) best = a;
                if (best != null && (p.Applicants.Count >= 2 || t - best.ArrivedAt >= ClockDef.SecondsPerDay))
                {
                    Borrow(sim, t, PeopleRules.RestOfSeason(sim, PeopleRules.AskedWage(sim, Fixed.FromInt(best.Skill), best.AboveMarket)) + 20_000 * Money.Coin);
                    Ok(sim, new OfferContract(t, best.Id));
                }
            }
            foreach (Hand h in p.Hands.ToArray())
                if (h.Contract == ContractState.Lapsed && sim.State.Ledger.CanAfford(h.Wage)) Ok(sim, new RenewContract(t, h.Id));
            int season = Season(sim, t);
            if (p.Hands.Count > 0 && season != _orderSeason)
            {
                _orderSeason = season;
                // Only as many acres as the hours tend at Care ≥ 1 (about an hour of carrying a day on a well acre).
                FarmDefs farm = sim.Config.Game.Farm;
                Fixed labour = farm.Crops[farm.IndexOfCrop(BestCrop[season])].LabourPerAcrePerDay;
                Fixed left = FounderRules.LabourRate(sim) + PeopleRules.HiredHours(sim);
                foreach (int tile in Acres)
                {
                    Hand h = p.Hands[0];
                    Fixed need = System.Linq.Enumerable.Contains(sim.Config.Game.Land.WellTiles, tile) ? labour + Fixed.One : labour;
                    if (need > left)
                    {
                        if (p.OrderFor(OrderTargetKind.Field, tile) != null) Ok(sim, new ClearStandingOrder(t, h.Id, OrderTargetKind.Field, tile));
                        continue;
                    }
                    left -= need;
                    bool extra = System.Array.IndexOf(Acres, tile) >= 2;
                    Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(tile, extra ? HandCrop[season] : BestCrop[season], SeedSource.Certified, Fertiliser(sim, tile), OrderSell, OrderMinPrice(sim))));
                }
            }
        }

        private bool _posted;
        private int _orderSeason = -1;

        protected virtual bool PostAboveMarket { get { return false; } }

        protected virtual bool Acceptable(Simulation sim, Applicant a) { return true; }

        protected virtual Fixed OrderMinPrice(Simulation sim) { return Fixed.Zero; }

        /// <summary>Borrows from the moneylender whatever cash falls short of <paramref name="need"/> (minimum 1,000 coins).</summary>
        protected static void Borrow(Simulation sim, long t, long need)
        {
            long gap = need - sim.State.Ledger.Cash;
            if (gap <= 0 || sim.State.Credit == null) return;
            long amount = Math.Max(gap + Money.Coin * 100 - gap % (Money.Coin * 100), 1_000 * Money.Coin);
            long room = CreditRules.Limit(sim, 0).Limit;
            amount = Math.Min(amount, room - room % Money.Coin);
            if (amount >= 1_000 * Money.Coin) Ok(sim, new TakeLoan(t, "moneylender", amount, 4));
        }

        protected static void Garden(Simulation sim, long t)
        {
            foreach (GardenBed b in sim.State.Farm.Beds)
            {
                if (b.State == BedState.Ready) Ok(sim, new PickGarden(t, b.Id, Overflow.SellRestToTrader));
                if (b.State == BedState.Empty) Ok(sim, new SowGarden(t, b.Id, b.Id == 1 ? "garden_spinach" : "garden_radish"));
            }
        }

        /// <summary>Spare hours go to day labour, in steps of 25%.</summary>
        protected static void SplitLabour(Simulation sim, long t)
        {
            Fixed rate = FounderRules.LabourRate(sim), need = FarmRules.LabourDemand(sim) - PeopleRules.HiredHours(sim);
            int share = 0;
            while (share < 100 && rate.MulRatio(100 - share - 25, 100) >= need) share += 25;
            if (share != sim.State.Farm.Founder.DayLabourShare) Ok(sim, new SetLabourSplit(t, share));
        }

        /// <summary>The household's next day of food is kept back; the rest is surplus.</summary>
        protected static long Reserve(Simulation sim, int good)
        {
            GoodDef g = sim.Config.Game.Market.Goods[good];
            if (!sim.State.Farm.Founder.EatFromStore || !g.Edible) return 0;
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan];
            long allowance = MarketRules.Indexed(sim, Money.MulRate(plan.CostPerSeason, plan.FoodShare)) / sim.Clock.DaysPerSeason;
            Fixed floor = MarketRules.Floor(sim, good);
            return floor <= Fixed.Zero ? 0 : (Fixed.FromInt(allowance * 10) / floor).RoundToLong(); // 1/100 coin → grams
        }

        private void Sell(Simulation sim, long t)
        {
            MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;

            // The mandi: the good whose full cart beats the trader by most, after the trip's labour at the day-labour wage.
            if (m.Trip == null && m.LastTripDay != t / ClockDef.SecondsPerDay && !sim.State.Farm.Founder.SickToday)
            {
                MandiDef mandi = defs.Mandi;
                long labour = Money.MulRate(MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour), mandi.FounderHours);
                long hire = MarketRules.Indexed(sim, mandi.CartHirePerTrip);
                int best = -1;
                long bestGain = 0, bestQ = 0;
                for (int g = 0; g < defs.Goods.Count; g++)
                {
                    if (defs.Goods[g].Kind != GoodKind.Produce) continue;
                    long q = Math.Min(m.InStore(g) - m.OnOffer(g) - Reserve(sim, g), mandi.CartCapacityGrams);
                    if (q <= 0) continue;
                    long gross = MarketRules.Value(sim, g, q, MarketRules.RegionalPrice(sim, g));
                    long gain = gross - Money.MulRate(gross, mandi.CommissionRate) - hire - labour
                        - MarketRules.Value(sim, g, Math.Min(q, MarketRules.TraderCapacityLeft(sim, g)), MarketRules.Floor(sim, g));
                    if (gain > bestGain) { best = g; bestGain = gain; bestQ = q; }
                }
                if (best >= 0 && Ok(sim, new GoToMandi(t, new[] { new MandiLoad(defs.Goods[best].Id, bestQ) }))) MandiTrips++;
            }

            for (int g = 0; g < defs.Goods.Count; g++)
            {
                if (defs.Goods[g].Kind != GoodKind.Produce) continue;
                long surplus = m.InStore(g) - m.OnOffer(g) - Reserve(sim, g);
                if (surplus <= 0) continue;

                // A small offer at a minimum halfway between the floor and the regional price: what the valley buys near
                // the regional price in about a day (a tenth of its demand).
                Fixed floor = MarketRules.Floor(sim, g), regional = MarketRules.RegionalPrice(sim, g);
                long target = defs.Region.DemandPerDay(g) / 10;
                long add = Math.Min(surplus, target - m.OnOffer(g));
                if (add > 0 && Ok(sim, new Offer(t, defs.Goods[g].Id, add, floor + (regional - floor).MulRatio(1, 2)))) surplus -= add;

                long sell = Math.Min(surplus, MarketRules.TraderCapacityLeft(sim, g));
                if (sell > 0) Ok(sim, new SellToTrader(t, defs.Goods[g].Id, sell));
            }
        }
    }
}

namespace Tools.Balancer
{
    using Sim.Core.Land;

    /// <summary>Stress bot: holds ready crops in the field for the Monsoon price (ADR 0034 §5 should make it pay less).</summary>
    public sealed class FieldHolderBot : FarmerBot
    {
        public override string Name { get { return "field-holder"; } }

        public override void Play(Simulation sim, long t)
        {
            if (Season(sim, t) == 2) { base.Play(sim, t); return; } // harvests only in the Monsoon
            SprayOutbreaks(sim, t);
            foreach (Tree tree in sim.State.Farm.Trees)
                if (tree.RipeGrams > 0 && Season(sim, t) == 2) Ok(sim, new PickFruit(t, tree.SlotId, Overflow.SellRestToTrader));
            Sow(sim, t, "potato");
            Garden(sim, t);
            SplitLabour(sim, t);
        }
    }

    /// <summary>Stress bot: spends its cash on inputs just before each close to dodge living costs (ADR 0034 §6 should stop it).</summary>
    public sealed class SubsistenceDodgerBot : FarmerBot
    {
        public override string Name { get { return "subsistence-dodger"; } }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            Ok(sim, new SetDietPlan(t, "subsistence")); // always rejected: automatic only
            long toClose = sim.Clock.SecondsPerSeason - t % sim.Clock.SecondsPerSeason;
            if (toClose > 3600) return;
            // Turn cash into seed in the store: an asset swap that leaves nothing to pay living costs with.
            int seed = sim.Config.Game.Market.IndexOf("seed_lentils");
            Fixed price = MarketRules.InputPrice(sim, seed);
            long grams = (Fixed.FromInt(sim.State.Ledger.Cash / 100) / price).MulRatio(1000, 1).RoundToLong() - 1000;
            grams = Math.Min(grams, Store.Fits(sim, seed));
            if (grams > 0) Ok(sim, new Buy(t, "seed_lentils", grams));
        }
    }

    /// <summary>Stress bot: buys adjacent land, leaves it fallow to raise its price, and sells it two seasons later.</summary>
    public sealed class LandFlipperBot : FarmerBot
    {
        private long _boughtAt = -1;
        private string _parcel;

        public override string Name { get { return "land-flipper"; } }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (_parcel == null)
            {
                foreach (Parcel p in sim.State.Land.Parcels)
                {
                    if (p.Owned) continue;
                    string id = sim.Config.Game.Map.Parcels[p.Id].Id;
                    if (Ok(sim, new BuyParcel(t, id))) { _parcel = id; _boughtAt = t; break; }
                }
            }
            else if (t - _boughtAt >= 2 * sim.Clock.SecondsPerSeason && Ok(sim, new SellParcel(t, _parcel))) _parcel = null;
        }
    }

    /// <summary>Stress bot: withdraws and re-offers everything every hour at the regional price, trying to game the market.</summary>
    public sealed class OfferChurnerBot : FarmerBot
    {
        public override string Name { get { return "offer-churner"; } }

        public override void Play(Simulation sim, long t)
        {
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
            {
                if (goods[g].Kind != GoodKind.Produce) continue;
                long on = sim.State.Market.OnOffer(g);
                if (on > 0) Ok(sim, new Withdraw(t, goods[g].Id, on));
                long free = sim.State.Market.InStore(g) - sim.State.Market.OnOffer(g) - Reserve(sim, g);
                if (free > 0) Ok(sim, new Offer(t, goods[g].Id, free, MarketRules.RegionalPrice(sim, g)));
            }
            base.Play(sim, t);
        }
    }

    /// <summary>Stress bot: goes to the mandi every day with whatever produce it has, worth it or not.</summary>
    public sealed class MandiEveryDayBot : FarmerBot
    {
        public override string Name { get { return "mandi-every-day"; } }

        public override void Play(Simulation sim, long t)
        {
            MarketState m = sim.State.Market;
            var goods = sim.Config.Game.Market.Goods;
            var loads = new List<MandiLoad>();
            long room = sim.Config.Game.Market.Mandi.CartCapacityGrams;
            for (int g = 0; g < goods.Count && room > 0; g++)
            {
                if (goods[g].Kind != GoodKind.Produce) continue;
                long q = Math.Min(m.InStore(g) - m.OnOffer(g) - Reserve(sim, g), room);
                if (q <= 0) continue;
                loads.Add(new MandiLoad(goods[g].Id, q));
                room -= q;
            }
            if (loads.Count > 0) Ok(sim, new GoToMandi(t, loads));
            base.Play(sim, t);
        }
    }
}

namespace Tools.Balancer
{
    using Sim.Core.Land;

    /// <summary>
    /// Stress bot (ADR 0016, 0038): leverage. Borrows the bank's whole limit against every parcel it buys, as soon as it can,
    /// and the moneylender's limit when cash is short; repays on schedule. It tries to turn debt into growth.
    /// </summary>
    public sealed class LeverageBot : FarmerBot
    {
        public override string Name { get { return "leverage"; } }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (sim.State.Credit == null) return;
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (p.Owned) continue;
                string id = sim.Config.Game.Map.Parcels[p.Id].Id;
                long price = LandRules.ParcelPrice(sim, p);
                long limit = CreditRules.Limit(sim, 1, price).Limit;
                long loan = Math.Min(price, limit);
                loan -= loan % Money.Coin;
                if (loan < 1_000_000 || !sim.State.Ledger.CanAfford(price - loan)) continue;
                if (Ok(sim, new BuyParcel(t, id, new Financing("coop_bank", loan, 16)))) break;
            }
            if (sim.State.Ledger.Cash < 500_000)
            {
                long room = CreditRules.Limit(sim, 0).Limit;
                if (room >= 100_000) Ok(sim, new TakeLoan(t, "moneylender", room, 4));
            }
        }
    }

    /// <summary>
    /// Stress bot (ADR 0016, 0038): strategic default. Borrows the moneylender's limit whenever it can and never repays by choice:
    /// it holds its produce unsold and turns cash into seed before each close, so a default leaves the lender nothing. It tries to
    /// live on debt that foreclosure writes off.
    /// </summary>
    public sealed class StrategicDefaulterBot : FarmerBot
    {
        public override string Name { get { return "strategic-defaulter"; } }

        protected override bool HoldsProduce(Simulation sim)
        {
            return sim.State.Credit != null && sim.State.Credit.Loans.Exists(l => l.IsOpen);
        }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (sim.State.Credit == null) return;
            if (!sim.State.Credit.Loans.Exists(l => l.IsOpen))
            {
                long room = CreditRules.Limit(sim, 0).Limit;
                if (room >= 100_000) Ok(sim, new TakeLoan(t, "moneylender", room, 1));
            }
            long toClose = sim.Clock.SecondsPerSeason - t % sim.Clock.SecondsPerSeason;
            if (toClose > 3600 || !sim.State.Credit.Loans.Exists(l => l.IsOpen)) return;
            int seed = sim.Config.Game.Market.IndexOf("seed_lentils");
            Fixed price = MarketRules.InputPrice(sim, seed);
            long grams = (Fixed.FromInt(sim.State.Ledger.Cash / 100) / price).MulRatio(1000, 1).RoundToLong() - 1000;
            grams = Math.Min(grams, Store.Fits(sim, seed));
            if (grams > 0) Ok(sim, new Buy(t, "seed_lentils", grams));
        }
    }
}

namespace Tools.Balancer
{
    using Sim.Core.People;

    /// <summary>The founder farming alone, never hiring: the reference the economy model compares hiring against.</summary>
    public sealed class SoloBot : FarmerBot
    {
        public override string Name { get { return "solo"; } }

        protected override void Employ(Simulation sim, long t) { }
    }

    /// <summary>Stress bot (ADR 0039 §10): hires every free day labourer every day and sows to match. Day labour costs more than it earns.</summary>
    public sealed class DayLabourSpammerBot : FarmerBot
    {
        public override string Name { get { return "day-labour-spammer"; } }

        public override void Play(Simulation sim, long t)
        {
            int free = sim.State.People == null ? 0 : PeopleRules.FreeNow(sim);
            if (free > 0) Ok(sim, new HireDayLabourers(t, free));
            base.Play(sim, t);
        }
    }

    /// <summary>
    /// Stress bot: churns hands to fish for skill. Posts above market, takes only applicants whose range starts at 55, and lets each
    /// hand go late in the probation season (no severance) unless they turned out excellent.
    /// </summary>
    public sealed class HandChurnerBot : FarmerBot
    {
        public override string Name { get { return "hand-churner"; } }

        protected override bool PostAboveMarket { get { return true; } }

        protected override bool Acceptable(Simulation sim, Applicant a)
        {
            PeopleRules.SkillRange(sim, a, out int lo, out _);
            return lo >= 55;
        }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            PeopleState p = sim.State.People;
            long S = sim.Clock.SecondsPerSeason;
            if (p == null || t % S < S - 2 * 3600) return;
            foreach (Hand h in p.Hands.ToArray())
                if (h.SeasonsWorked == 0 && h.Skill < Fixed.FromInt(75)) Ok(sim, new DismissHand(t, h.Id));
            if (p.Post == null && p.Hands.Count == 0) Ok(sim, new PostJob(t, true));
        }
    }

    /// <summary>
    /// Stress bot: games the lapse. Just before each close it turns cash into seed bought ahead so the renewal lapses, then renews
    /// within the grace. Back pay on renewal (ADR 0039 §4) makes this save nothing.
    /// </summary>
    public sealed class LapseGamerBot : FarmerBot
    {
        public override string Name { get { return "lapse-gamer"; } }

        public override void Play(Simulation sim, long t)
        {
            PeopleState p = sim.State.People;
            long S = sim.Clock.SecondsPerSeason;
            if (p != null && p.Hands.Count > 0 && t % S >= S - 2 * 3600)
            {
                long keep = p.Hands[0].Wage / 2;
                int g = sim.Config.Game.Market.IndexOf("compost");
                Fixed price = MarketRules.InputPrice(sim, g);
                long spend = sim.State.Ledger.Cash - keep;
                long grams = spend <= 0 || price <= Fixed.Zero ? 0 : (Fixed.FromInt(spend * 10) / price).RoundToLong();
                grams = Math.Min(grams, Store.Fits(sim, g));
                if (grams > 0) Ok(sim, new Buy(t, "compost", grams));
                return;
            }
            base.Play(sim, t);
        }
    }

    /// <summary>
    /// Floor check: the absentee player. Plays only in the first two hours of every three days and lets the hand's orders do the
    /// rest, selling to the trader. Delegation must be viable (at least 0.80 × competent).
    /// </summary>
    public sealed class AbsenteeBot : FarmerBot
    {
        public override string Name { get { return "absentee"; } }

        protected override SellRule OrderSell { get { return SellRule.Trader; } }

        public override void Play(Simulation sim, long t)
        {
            if (t % (3 * ClockDef.SecondsPerDay) < 2 * 3600 || t < sim.Clock.SecondsPerSeason + 2 * ClockDef.SecondsPerDay) base.Play(sim, t);
        }
    }
}
