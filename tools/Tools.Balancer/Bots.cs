using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>The well acres first, then the rain-fed inherited acres in id order (ADR 0032), from the content's map.</summary>
        protected static int[] AcresOf(Simulation sim) { return Home.Acres(sim.Config.Game); }

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
            Deliver(sim, t);
            if (!HoldsProduce(sim)) Sell(sim, t);
            Employ(sim, t);
            Town.Build(sim, t, HomeReserve(sim), MayBorrow); // homes first (the Village), on the bank's home loan
            if (KeepsCattle) Cattle(sim, t);
            Sow(sim, t, CropFor(sim, Season(sim, t)));
            Garden(sim, t);
            SplitLabour(sim, t);
        }

        protected static void Harvest(Simulation sim, long t)
        {
            foreach (int tile in AcresOf(sim))
            {
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) Ok(sim, new Harvest(t, tile, Overflow.SellRestToTrader));
            }
            foreach (Tree tree in sim.State.Farm.Trees)
                if (tree.RipeGrams > 0) Ok(sim, new PickFruit(t, tree.SlotId, Overflow.SellRestToTrader));
        }

        /// <summary>Fills today's supermarket orders from fresh stock (spec 011 §3.3): they pay more than the trader.</summary>
        protected static void Deliver(Simulation sim, long t)
        {
            foreach (SupermarketOrder o in new List<SupermarketOrder>(sim.State.Market.SupermarketOrders))
            {
                long q = Math.Min(o.Left, Supermarket.FreshStock(sim, o.Good));
                if (q > 0 && Supermarket.DeliveryOpen(sim)) Ok(sim, new DeliverOrder(t, o.Id, q));
            }
        }

        /// <summary>A curative spray on every warned crop: the dose costs far less than the damage it saves.</summary>
        protected static void SprayOutbreaks(Simulation sim, long t)
        {
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Growing && c.Outbreak != Outbreak.None && c.Spray == SprayState.None) Ok(sim, new Spray(t, c.TileId));
        }

        /// <summary>Sows the crop on vacant acres while the founder's hours cover the demand (Care ≥ 1.0).</summary>
        protected void Sow(Simulation sim, long t, string cropId)
        {
            if (sim.State.Farm.Founder.AutoSubsistence) return; // feed the household first: day labour until the plan is back
            FarmDefs farm = sim.Config.Game.Farm;
            CropDef crop = farm.Crops[farm.IndexOfCrop(cropId)];
            Fixed supply = FounderRules.LabourRate(sim) + FounderRules.MandiCut(sim) + PeopleRules.HiredHours(sim);
            foreach (int tile in AcresOf(sim))
            {
                if (sim.State.Farm.CropOn(tile) != null || Town.Reserved(sim, tile) || KeepsClear(sim, tile)) continue;
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
            if (PeopleRules.HutCount(sim) == 0 && t >= S - ClockDef.SecondsPerDay / 2 && ReadyToHire(sim))
            {
                long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[sim.Config.Game.People.HandHut].Value);
                Borrow(sim, t, cost + 40_000 * Money.Coin);
                Ok(sim, new BuildHandHut(t, Home.Quarters(sim.Config.Game))); // the labour quarters (ADR 0040)
            }
            if (p.Post == null && p.Hands.Count == 0 && PeopleRules.HutCount(sim) > 0 && !_posted) _posted = Ok(sim, new PostJob(t, PostAboveMarket));
            // A hand from outside, into the hut (Phase 1's play; the board's townspeople are left to the business bots).
            List<Applicant> outside = p.Applicants.FindAll(a => a.Place == 0 && a.CitizenId == 0);
            if (PeopleRules.EmptyHut(sim) != null && outside.Count > 0 && ReadyToHire(sim))
            {
                foreach (Applicant a in outside) if (!a.SecondTraitKnown) Ok(sim, new Interview(t, a.Id));
                Applicant best = null;
                foreach (Applicant a in outside) if (Acceptable(sim, a) && (best == null || Expected(sim, a) > Expected(sim, best))) best = a;
                if (best != null && (outside.Count >= 2 || t - best.ArrivedAt >= ClockDef.SecondsPerDay))
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
                foreach (int tile in AcresOf(sim))
                {
                    Hand h = p.Hands[0];
                    if (Town.Reserved(sim, tile) || KeepsClear(sim, tile)) continue; // kept clear for a family house or a business
                    Fixed need = System.Linq.Enumerable.Contains(sim.Config.Game.Land.WellTiles, tile) ? labour + Fixed.One : labour;
                    if (need > left)
                    {
                        if (p.OrderFor(OrderTargetKind.Field, tile) != null) Ok(sim, new ClearStandingOrder(t, h.Id, OrderTargetKind.Field, tile));
                        continue;
                    }
                    left -= need;
                    bool extra = System.Array.IndexOf(AcresOf(sim), tile) >= 2;
                    Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(tile, extra ? HandCrop[season] : BestCrop[season], SeedSource.Certified, Fertiliser(sim, tile), OrderSell, OrderMinPrice(sim))));
                }
            }
        }

        private bool _posted;
        private int _orderSeason = -1;

        protected virtual bool PostAboveMarket { get { return false; } }

        // ---- Cattle (spec 012 §9): after the Hamlet, a shed and two buffalo, a fodder acre, feed kept stocked ----

        /// <summary>Whether the bot keeps cattle once they unlock (every farmer bot does, so stress ratios compare like with like).</summary>
        protected virtual bool KeepsCattle { get { return true; } }

        /// <summary>How many buffalo it keeps.</summary>
        protected virtual int Herd { get { return 2; } }

        /// <summary>Whether it grows its own green fodder (the dairy stress bot buys everything).</summary>
        protected virtual bool GrowsFodder { get { return true; } }

        /// <summary>The share of an animal's price it borrows from the bank when it can (0: buys with cash).</summary>
        protected virtual Fixed DairyLoanShare { get { return Fixed.Parse("0.75"); } }

        /// <summary>
        /// Cash kept back before building or buying for the dairy: the next close's living costs and hands' wages, a season of
        /// bought feed for the herd after the purchase, and a margin, so the household is never left short at a close.
        /// </summary>
        /// <summary>Cash kept back before building a family house: the next close's living costs and hands' wages.</summary>
        protected virtual long HomeReserve(Simulation sim)
        {
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan];
            long wages = 0;
            if (sim.State.People != null) foreach (Hand h in sim.State.People.Hands) wages += h.Wage;
            return MarketRules.Indexed(sim, plan.CostPerSeason) + wages;
        }

        private long Reserve(Simulation sim)
        {
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan];
            long wages = 0;
            if (sim.State.People != null) foreach (Hand h in sim.State.People.Hands) wages += h.Wage;
            int animals = (sim.State.Livestock?.Animals.Count ?? 0) + 1;
            return MarketRules.Indexed(sim, plan.CostPerSeason) + wages + animals * 25_000 * Money.Coin + 30_000 * Money.Coin;
        }

        protected void Cattle(Simulation sim, long t)
        {
            var defs = sim.Config.Game.Livestock;
            if (defs == null || !Sim.Core.Livestock.LivestockRules.Unlocked(sim)) return;
            var herd = sim.State.Livestock;
            if (Sim.Core.Livestock.LivestockRules.Shed(sim) == null)
            {
                long shed = MarketRules.Indexed(sim, sim.Config.Game.Buildings[defs.Shed].Value);
                if (herd.ShedReadyAt == 0 && sim.State.Ledger.Cash >= shed + Reserve(sim)) Ok(sim, new BuildShed(t));
                return;
            }
            if (herd.Animals.Count < Herd)
            {
                long price = MarketRules.Indexed(sim, defs.Animals[defs.IndexOfAnimal("buffalo")].Price);
                long loan = 0;
                if (DairyLoanShare > Fixed.Zero && sim.State.Credit != null)
                {
                    int bank = sim.Config.Game.Credit.IndexOfLender("coop_bank");
                    long limit = CreditRules.Limit(sim, bank, 0, price).Limit;
                    loan = Math.Min(limit, Money.MulRate(price, DairyLoanShare));
                    loan -= loan % (100 * Money.Coin);
                    if (loan < sim.Config.Game.Credit.Lenders[bank].MinAmount) loan = 0;
                }
                if (sim.State.Ledger.Cash - (price - loan) >= Reserve(sim))
                    Ok(sim, new BuyAnimal(t, "buffalo", loan > 0 ? new Financing("coop_bank", loan, 8) : null));
            }
            if (herd.Animals.Count == 0) return;
            // Feed: the hand's animals order when there is a hand, otherwise the bot fills what lasts under 2 days.
            PeopleState p = sim.State.People;
            if (p != null && p.Hands.Count > 0 && p.OrderFor(OrderTargetKind.Animals, 0) == null)
                Ok(sim, new SetStandingOrder(t, p.Hands[0].Id, OrderSpec.LookAfterAnimals(3)));
            if (p == null || p.OrderFor(OrderTargetKind.Animals, 0) == null)
                foreach (int good in new[] { defs.Straw, defs.Feed })
                    if (Sim.Core.Livestock.LivestockRules.DaysLeft(sim, good) < 2)
                    {
                        long need = 0;
                        foreach (var a in herd.Animals)
                        {
                            var d = defs.Animals[a.Kind];
                            need += ((good == defs.Straw ? d.Straw : d.Feed) * defs.Livestock.RealDaysPerGameDay * Fixed.FromInt(3000)).RoundToLong();
                        }
                        Ok(sim, new Buy(t, sim.Config.Game.Market.Goods[good].Id, Math.Min(need, Store.Fits(sim, good))));
                    }
            // A fodder acre: the last inherited acre, sorghum in Summer, berseem in Winter.
            if (GrowsFodder)
            {
                int[] acres = AcresOf(sim);
                int tile = acres[acres.Length - 1];
                string fodder = Season(sim, t) == 1 ? "sorghum" : Season(sim, t) == 3 ? "berseem" : null;
                if (fodder != null && sim.State.Farm.CropOn(tile) == null)
                {
                    if (p != null && p.OrderFor(OrderTargetKind.Field, tile) is StandingOrder o) Ok(sim, new ClearStandingOrder(t, o.HandId, OrderTargetKind.Field, tile));
                    Ok(sim, new Sow(t, tile, fodder, SeedSource.Certified, "none"));
                }
            }
        }

        /// <summary>The founder's crop this season (0 Spring … 3 Winter).</summary>
        protected virtual string CropFor(Simulation sim, int season) { return BestCrop[season]; }

        /// <summary>Grams of a good kept back from selling (a mill's wheat).</summary>
        protected virtual long Kept(Simulation sim, int good) { return 0; }

        /// <summary>An acre the bot keeps unsown (a business's acre to be).</summary>
        protected virtual bool KeepsClear(Simulation sim, int tile) { return false; }

        /// <summary>False for a bot that never borrows (the cautious saver, the never-stuck check).</summary>
        protected virtual bool MayBorrow { get { return true; } }

        /// <summary>Whether to build the hand's hut and hire now (the competent bot: as soon as it plans to).</summary>
        protected virtual bool ReadyToHire(Simulation sim) { return true; }

        protected virtual bool Acceptable(Simulation sim, Applicant a) { return true; }

        protected virtual Fixed OrderMinPrice(Simulation sim) { return Fixed.Zero; }

        /// <summary>Borrows from the moneylender whatever cash falls short of <paramref name="need"/> (minimum 1,000 coins).</summary>
        protected void Borrow(Simulation sim, long t, long need)
        {
            long gap = need - sim.State.Ledger.Cash;
            if (gap <= 0 || sim.State.Credit == null || !MayBorrow) return;
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
                    long q = Math.Min(m.InStore(g) - m.OnOffer(g) - Reserve(sim, g) - Kept(sim, g), mandi.CartCapacityGrams);
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
                long surplus = m.InStore(g) - m.OnOffer(g) - Reserve(sim, g) - Kept(sim, g);
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

    /// <summary>
    /// Stress bot (ADR 0016, 0045): the cautious saver. Never borrows; builds the hand's hut and hires only once cash covers
    /// the hut, a season of the hand's market wage and a season of the household's basic living costs.
    /// </summary>
    public sealed class CautiousSaverBot : FarmerBot
    {
        public override string Name { get { return "cautious-saver"; } }

        protected override bool MayBorrow { get { return false; } }

        protected override bool ReadyToHire(Simulation sim)
        {
            GameDefs g = sim.Config.Game;
            long hut = PeopleRules.HutCount(sim) > 0 ? 0 : g.Buildings[g.People.HandHut].Value;
            long living = g.Farm.DietPlans[g.Farm.IndexOfDietPlan("basic")].CostPerSeason;
            return sim.State.Ledger.Cash >= MarketRules.Indexed(sim, hut + g.People.Hand.MarketWage + living);
        }
    }

    /// <summary>
    /// Stress bot (ADR 0016, 0045): random. Each hour a seeded random player command (the zero-time check's picker), plus
    /// harvesting what's ready so the farm isn't left to rot by chance alone. It must finish below the competent bot.
    /// </summary>
    public sealed class RandomBot : Bot
    {
        private ulong _n;

        public override string Name { get { return "random"; } }

        public override void Play(Simulation sim, long t)
        {
            for (int i = 0; i < 3; i++) sim.Execute(Gate.RandomCommand(sim, t, Rng.Next(sim.State.Seed, (RngStream)960, _n++, 0)));
            HarvestReady(sim, t);
            Town.Build(sim, t, 30_000 * Money.Coin, false); // spec 014 §9: every bot builds homes after the Hamlet
        }

        private static void HarvestReady(Simulation sim, long t)
        {
            foreach (int tile in AcresOf(sim))
            {
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) Ok(sim, new Harvest(t, tile, Overflow.SellRestToTrader));
            }
        }
    }

    /// <summary>
    /// The recoverable-bankruptcy check (ADR 0045 §4): plays the strategic defaulter until it's foreclosed, then the competent
    /// bot. <see cref="ForeclosedAt"/> is when the foreclosure completed (−1 if never).
    /// </summary>
    public sealed class RecoveringBot : FarmerBot
    {
        private readonly StrategicDefaulterBot _defaulter = new StrategicDefaulterBot();

        public long ForeclosedAt { get; private set; } = -1;

        public override string Name { get { return "recovering"; } }

        public override void Start(Simulation sim) { _defaulter.Start(sim); }

        public override void Play(Simulation sim, long t)
        {
            if (ForeclosedAt < 0 && sim.State.Credit.Foreclosures > 0) ForeclosedAt = t;
            if (ForeclosedAt < 0) _defaulter.Play(sim, t);
            else base.Play(sim, t);
        }
    }

    /// <summary>The founder farming alone, never hiring: the reference the economy model compares hiring against.</summary>
    public sealed class SoloBot : FarmerBot
    {
        public override string Name { get { return "solo"; } }

        protected override void Employ(Simulation sim, long t) { }
    }

    /// <summary>Stress bot (ADR 0039 §10, ADR 0049 §3): hires every free labourer till the crops are in, and sows to match. Hired labour costs more than it earns.</summary>
    public sealed class DayLabourSpammerBot : FarmerBot
    {
        public override string Name { get { return "day-labour-spammer"; } }

        public override void Play(Simulation sim, long t)
        {
            int free = sim.State.People == null ? 0 : PeopleRules.FreeNow(sim);
            if (free > 0 && PeopleRules.LabourUntil(sim) > 0) Ok(sim, new HireLabourers(t, free));
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

    /// <summary>The inherited farm from the content's map, so bots work on any map size (spec 010).</summary>
    /// <summary>
    /// Homes for the town (spec 014 §9, US-211): after the Hamlet a bot builds family houses, one at a time with cash it can spare,
    /// until 5 households live on its land (the Village's count, §3.13); on the labour quarters' free place first, then two to an
    /// acre on the rain-fed acres before the fodder acre, which it stops sowing. A hand living alone who asks to bring his family: not now.
    /// </summary>
    public static class Town
    {
        public const int Households = 5;

        private static int[] HouseAcres(GameDefs d)
        {
            int[] a = Home.Acres(d);
            return new[] { a[a.Length - 2], a[a.Length - 3] };
        }

        /// <summary>Households, houses being built and empty houses (a family is coming).</summary>
        public static int Homes(Simulation sim)
        {
            CitizensState town = sim.State.Citizens;
            int n = town.Households.Count + town.Constructions.Count;
            foreach (Sim.Core.Land.Building b in sim.State.Land.Buildings)
                if (HouseRules.IsHouse(sim, b) && !town.Households.Exists(x => x.HomeId == b.Id)) n++;
            return n;
        }

        private static bool Wanted(Simulation sim)
        {
            return sim.State.Citizens != null && HouseRules.Unlocked(sim) && Homes(sim) < Households;
        }

        /// <summary>The acre to keep clear for the next house, or −1: the first planned acre with room once the quarters are taken.</summary>
        private static int NextAcre(Simulation sim)
        {
            int quarters = Home.Quarters(sim.Config.Game);
            if (HouseRules.Place(sim, quarters) == RejectReason.None) return -1;
            foreach (int tile in HouseAcres(sim.Config.Game))
                if (HouseRules.HousesOn(sim, tile) < sim.Config.Game.Population.HousesPerAcre) return tile;
            return -1;
        }

        /// <summary>An acre not to sow: one with a house, or the next one planned while more homes are wanted.</summary>
        public static bool Reserved(Simulation sim, int tile)
        {
            if (sim.State.Citizens == null) return false;
            if (HouseRules.HousesOn(sim, tile) > 0) return true;
            return Wanted(sim) && tile == NextAcre(sim);
        }

        /// <summary>
        /// Builds the next house when cash covers it (or its down payment) and the reserve; a bot that borrows takes the bank's home loan
        /// over 8 seasons (ADR 0071: the rent, 10% of its cost a season, beats the interest).
        /// </summary>
        public static void Build(Simulation sim, long t, long reserve, bool borrow)
        {
            CitizensState town = sim.State.Citizens;
            if (town == null || !HouseRules.Unlocked(sim)) return;
            // Not now: the hand stays in his hut and a renting family takes the house, one household more for the same money
            // (the rent equals the wage he would give up, §3.6).
            if (town.OfferHandId != 0) sim.Execute(new AnswerFamilyRequest(t, town.OfferHandId, false));
            if (!Wanted(sim) || town.Constructions.Count > 0) return;
            int acre = NextAcre(sim);
            PeopleState p = sim.State.People;
            if (acre >= 0 && p?.OrderFor(OrderTargetKind.Field, acre) is StandingOrder o) sim.Execute(new ClearStandingOrder(t, o.HandId, OrderTargetKind.Field, acre));
            long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[sim.Config.Game.Population.House].Value);
            long loan = 0;
            if (borrow && sim.State.Credit != null)
            {
                int bank = sim.Config.Game.Credit.IndexOfLender("coop_bank");
                loan = System.Math.Min(cost, CreditRules.Limit(sim, bank, 0, 0, cost).Limit);
                loan -= loan % (1_000 * Money.Coin);
                if (loan < sim.Config.Game.Credit.Lenders[bank].MinAmount) loan = 0;
            }
            if (sim.State.Ledger.Cash < cost - loan + reserve) return;
            int tile = acre >= 0 ? acre : Home.Quarters(sim.Config.Game);
            sim.Execute(new BuildFamilyHouse(t, tile, loan > 0 ? new Financing("coop_bank", loan, 8) : null));
        }
    }

    public static class Home
    {
        public static int[] Acres(GameDefs defs)
        {
            var list = new System.Collections.Generic.List<int>(defs.Land.WellTiles);
            foreach (int t in Tiles(defs)) if (defs.Map.Tiles[t].Terrain == Terrain.Farm && !list.Contains(t)) list.Add(t);
            return list.ToArray();
        }

        /// <summary>The inherited farm acres in id order.</summary>
        public static int[] FarmTiles(GameDefs defs)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (int t in Tiles(defs)) if (defs.Map.Tiles[t].Terrain == Terrain.Farm) list.Add(t);
            return list.ToArray();
        }

        public static int Quarters(GameDefs defs)
        {
            foreach (int t in Tiles(defs)) if (defs.Map.Tiles[t].Terrain == Terrain.Quarters) return t;
            return -1;
        }

        private static System.Collections.Generic.List<int> Tiles(GameDefs defs)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (int p in defs.Start.InheritedParcels) list.AddRange(defs.Map.Parcels[p].Tiles);
            list.Sort();
            return list;
        }
    }

    /// <summary>
    /// Stress bot (spec 012 §9): fills the shed with buffalo on the biggest dairy loans it can get, buys every kilo of feed and grows
    /// no fodder. It should stay solvent and never beat the competent bot by more than 1.20 ×.
    /// </summary>
    public sealed class DairyBot : FarmerBot
    {
        public override string Name { get { return "dairy-max"; } }
        protected override int Herd { get { return 4; } }
        protected override bool GrowsFodder { get { return false; } }
        protected override Fixed DairyLoanShare { get { return Fixed.Parse("0.75"); } }
    }

    /// <summary>
    /// Spec 016 §9 (US-117, US-119): the competent founder who keeps the last inherited acres clear and founds businesses on them as
    /// their era comes, one after another (the flour mill, then the bakery beside it), run by hired workers or by the founder, with or
    /// without a business loan. A business short of cash gets cash put in; at each season's start, cash beyond a season's running
    /// costs and a margin comes out to the founder.
    /// </summary>
    public class BusinessBot : FarmerBot
    {
        private readonly bool _self, _borrow;
        private readonly string[] _types;

        public BusinessBot(bool runsIt, bool borrows, params string[] types)
        {
            _self = runsIt;
            _borrow = borrows;
            _types = types;
        }

        /// <summary>The dairy works only your herd's milk (neighbours' milk stopped).</summary>
        public bool OwnMilkOnly { get; set; }

        public override string Name
        {
            get
            {
                string what = string.Join("+", _types.Select(x => x == "flour_mill" ? "mill" : x));
                return what + (_self ? "-yours" : "-hired") + (_borrow ? "-loan" : "") + (OwnMilkOnly ? "-own-milk" : "");
            }
        }

        private static Sim.Core.Definitions.BusinessTypeDef Type(Simulation sim, string id)
        {
            return Sim.Core.Business.BusinessRules.Defs(sim).Types.First(x => x.Id == id);
        }

        /// <summary>The rain-fed acres before the two kept for family houses (the last is the fodder acre): the first business's, then the next's.</summary>
        private static int Acre(Simulation sim, int i) { int[] a = AcresOf(sim); return a[a.Length - 4 - i]; }

        private static Sim.Core.Business.Business Of(Simulation sim, string id)
        {
            return sim.State.Businesses?.Businesses.Find(b => b.Status != Sim.Core.Business.BusinessStatus.Closed && Sim.Core.Business.BusinessRules.TypeOf(sim, b).Id == id);
        }

        /// <summary>The next business to found: the first of the list not standing whose era has come, once those before it stand.</summary>
        private int Next(Simulation sim)
        {
            if (sim.State.Businesses == null) return -1;
            for (int i = 0; i < _types.Length; i++)
            {
                if (Of(sim, _types[i]) != null) continue;
                return Sim.Core.Business.BusinessRules.Unlocked(sim, Type(sim, _types[i])) ? i : -1;
            }
            return -1;
        }

        private bool Mills(Simulation sim)
        {
            return _types.Contains("flour_mill") && sim.State.Businesses != null && Sim.Core.Business.BusinessRules.Unlocked(sim, Type(sim, "flour_mill"));
        }

        /// <summary>Wheat in Winter once the Hamlet allows a mill: its own wheat to mill.</summary>
        protected override string CropFor(Simulation sim, int season)
        {
            return season == 3 && Mills(sim) ? "wheat" : base.CropFor(sim, season);
        }

        /// <summary>Its own wheat for the mill: a year's daily amount kept back from selling (wheat comes in Winter only).</summary>
        protected override long Kept(Simulation sim, int good)
        {
            Sim.Core.Business.Business mill = sim.State.Businesses == null ? null : Of(sim, "flour_mill");
            if (mill == null || good != Sim.Core.Business.BusinessRules.TypeOf(sim, mill).Input) return 0;
            return mill.OwnPerDay * sim.Clock.DaysPerSeason * 4;
        }

        /// <summary>The next business first: until it's founded, its cash is kept back from building houses.</summary>
        protected override long HomeReserve(Simulation sim)
        {
            long reserve = base.HomeReserve(sim);
            int i = Next(sim);
            if (i < 0) return reserve;
            var type = Type(sim, _types[i]);
            long loan = _borrow ? Sim.Core.Business.BusinessBooks.MaxLoan(sim, type) : 0;
            return reserve + Sim.Core.Business.BusinessRules.Projected(sim, type, _self, Sim.Core.Business.BusinessRules.DefaultOwnPerDay(sim, type), type.Fee, loan).CashNeeded - loan;
        }

        protected override bool KeepsClear(Simulation sim, int tile)
        {
            int i = Next(sim);
            return i >= 0 && tile == Acre(sim, i);
        }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (sim.State.Businesses == null) return;
            int next = Next(sim);
            if (next >= 0) Found(sim, t, next);
            foreach (string id in _types)
            {
                Sim.Core.Business.Business b = Of(sim, id);
                if (b != null) Manage(sim, t, b);
            }
        }

        private void Manage(Simulation sim, long t, Sim.Core.Business.Business b)
        {
            // The day's mix: what the shops buy, and for what keeps, what the trader takes as well when it pays (rusk does; ghee from whole milk doesn't).
            var bt = Sim.Core.Business.BusinessRules.TypeOf(sim, b);
            foreach (int p in bt.Products)
            {
                long want = Sim.Core.Business.BusinessRules.ShopGramsPerDay(sim, bt, p)
                    + (Sim.Core.Business.BusinessRules.Pays(sim, bt, p) ? sim.Config.Game.Market.Region.TraderBuyPerDay(p) : 0);
                if (b.Status == Sim.Core.Business.BusinessStatus.Open && b.Mix[p] != want) Ok(sim, new SetMix(t, b.Id, sim.Config.Game.Market.Goods[p].Id, want));
            }
            // The dairy's milk a day follows its mix; neighbours' milk unless it works its own only.
            if (bt.TakesMilk && b.Status == Sim.Core.Business.BusinessStatus.Open)
            {
                long milk = Sim.Core.Business.BusinessRules.MilkFor(bt, b.Mix);
                if (b.OwnPerDay != milk || b.BuyFromMarket == OwnMilkOnly) Ok(sim, new SetBusiness(t, b.Id, b.OpenHour, b.CloseHour, b.Fee, milk, !OwnMilkOnly));
            }
            if (!_self) Hire(sim, t, b);
            // Short, or overdrawn at a season's start: cash in to clear it (and pay a worker again), as an owner would.
            bool seasonStart = t % sim.Clock.SecondsPerSeason < 3600 + 600;
            if (Sim.Core.Business.BusinessBooks.Short(sim, b) || (seasonStart && Sim.Core.Business.BusinessBooks.Overdrawn(b) > 0))
            {
                long put = Sim.Core.Business.BusinessBooks.Overdrawn(b) + (b.Worker == Sim.Core.Business.WorkerKind.None ? MarketRules.Indexed(sim, Sim.Core.Business.BusinessRules.TypeOf(sim, b).Wage) : 0);
                put = (put + Money.Coin - 1) / Money.Coin * Money.Coin;
                if (put > 0 && sim.State.Ledger.Cash >= put + 10_000 * Money.Coin) Ok(sim, new PutCash(t, b.Id, put));
            }
            // Your cash running low (loan payments come at the close): draw what the business can spare, as an owner would.
            bool low = sim.State.Ledger.Cash < 20_000 * Money.Coin;
            if ((seasonStart || low) && Sim.Core.Business.BusinessBooks.Overdrawn(b) == 0)
            {
                var type = Sim.Core.Business.BusinessRules.TypeOf(sim, b);
                long keep = Sim.Core.Business.BusinessRules.Wage(sim, type) + 20_000 * Money.Coin;
                long spare = (b.Ledger.Cash - keep) / (1000 * Money.Coin) * (1000 * Money.Coin);
                if (spare > 0) Ok(sim, new TakeCash(t, b.Id, spare));
            }
        }

        /// <summary>
        /// Nobody working (spec 015): word sent at the usual wage; a townsperson hired at once at what they ask, the most skilled;
        /// else the best migrant by what they seem, once two ask or one has waited a day.
        /// </summary>
        private static void Hire(Simulation sim, long t, Sim.Core.Business.Business b)
        {
            if (b.Status == Sim.Core.Business.BusinessStatus.Closed || HiringRules.BusinessPlace(sim, b.Id) != RejectReason.None) return;
            PeopleState p = sim.State.People;
            if (p.PostAt(b.Id) == null && p.ApplicantsFor(b.Id).Count == 0 && sim.State.Ledger.CanAfford(MarketRules.Indexed(sim, sim.Config.Game.People.JobPost.Cost)))
                Ok(sim, new PostJob(t, b.Id, false));
            List<Applicant> list = p.ApplicantsFor(b.Id);
            Applicant best = list.Find(a => a.CitizenId != 0);
            if (best == null)
            {
                foreach (Applicant a in list) if (best == null || a.Skill > best.Skill) best = a;
                if (best == null || (list.Count < 2 && t - best.ArrivedAt < ClockDef.SecondsPerDay)) return;
            }
            if (Sim.Core.Business.BusinessBooks.CanSpend(sim, b) < PeopleRules.RestOfSeason(sim, HiringRules.OfferWage(sim, best, OfferLevel.Asked))) return;
            Ok(sim, new OfferContract(t, best.Id));
        }

        private void Found(Simulation sim, long t, int i)
        {
            var defs = Sim.Core.Business.BusinessRules.Defs(sim);
            var type = Type(sim, _types[i]);
            int acre = Acre(sim, i);
            PeopleState p = sim.State.People;
            if (p?.OrderFor(OrderTargetKind.Field, acre) is StandingOrder o) Ok(sim, new ClearStandingOrder(t, o.HandId, OrderTargetKind.Field, acre));
            if (Sim.Core.Business.BusinessRules.Place(sim, acre) != RejectReason.None) return;
            long loan = _borrow ? Sim.Core.Business.BusinessBooks.MaxLoan(sim, type) : 0;
            long need = Sim.Core.Business.BusinessRules.Projected(sim, type, _self, Sim.Core.Business.BusinessRules.DefaultOwnPerDay(sim, type), type.Fee, loan).CashNeeded - loan;
            if (sim.State.Ledger.Cash < need + 30_000 * Money.Coin) return;
            Ok(sim, new FoundBusiness(t, type.Id, acre, need, _self, loan > 0 ? new Financing(defs.LenderId, loan, defs.TermSeasons) : null));
        }
    }

    /// <summary>Spec 016 §9 (US-117): the flour mill alone, from the Hamlet.</summary>
    public sealed class MillBot : BusinessBot
    {
        public MillBot(bool runsIt, bool borrows = false) : base(runsIt, borrows, "flour_mill") { }
    }
}
