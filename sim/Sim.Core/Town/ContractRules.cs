using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Town
{
    /// <summary>
    /// Forward contracts (spec 027 §3.7, US-156): at each close the trader offers a fixed price for part of last season's harvest of your two biggest staples, for delivery
    /// in the season after the one you are in. A contract is delivered by the selling you already do (the first kilos the trader buys of that crop in the delivery
    /// season are paid at the contract price); a shortfall is settled at the close at what the price rose above the contract. It never pays better than waiting on average.
    /// </summary>
    public static class ContractRules
    {
        public static ContractsDef Def(Simulation sim) { return sim.Config.Game?.Contracts; }

        public static ContractsState State(Simulation sim) { return sim.State.Township?.Contracts; }

        /// <summary>Contracts come with the events, at the Township (§3.7).</summary>
        public static bool Available(Simulation sim) { return Def(sim) != null && State(sim) != null && TownshipRules.Reached(sim); }

        private static bool Listed(Simulation sim, int good) { return Def(sim).Has(sim.Config.Game.Market.Goods[good].Id); }

        // ---- prices (§3.7) ----

        /// <summary>The trader's floor price for a season of the year: the regional price with that season's factor at today's price index, less transport (no daily swing, no event).</summary>
        public static Fixed Expected(Simulation sim, int good, int season)
        {
            MarketDefs defs = sim.Config.Game.Market;
            GoodDef g = defs.Goods[good];
            Fixed index = g.PriceControlled ? Fixed.One : sim.State.Market.PriceIndex;
            return g.BasePrice * index * defs.Region.SeasonalFactor(good, season) - MarketRules.TransportCost(sim, good);
        }

        /// <summary>The offered price: the expected price less the discount, never above it.</summary>
        public static Fixed OfferPrice(Simulation sim, Fixed expected)
        {
            Fixed p = expected * (Fixed.One - Def(sim).Discount);
            return p > expected ? expected : p;
        }

        // ---- the harvest tally and the 6:00 sample ----

        /// <summary>A harvest of a staple at the Township counts toward next close's offers.</summary>
        internal static void Harvested(Simulation sim, int good, long quantity)
        {
            if (!Available(sim) || quantity <= 0 || !Listed(sim, good)) return;
            ContractsState.Add(State(sim).HarvestThis, sim.Config.Game.Market.Goods[good].Id, quantity);
        }

        /// <summary>At 6:00: the floor price the trader pays today, kept for each contract in its delivery season (the shortfall's "season's average price").</summary>
        internal static void Sample(Simulation sim)
        {
            if (!Available(sim)) return;
            foreach (Contract c in State(sim).Open)
            {
                if (c.Delivery != sim.Period) continue;
                c.FloorSum += MarketRules.Floor(sim, sim.Config.Game.Market.IndexOf(c.Good));
                c.Samples++;
            }
        }

        // ---- delivery (§3.7) ----

        /// <summary>The contract a sale of this good would now fill (its delivery season, kilos left), or null.</summary>
        public static Contract Running(Simulation sim, int good)
        {
            if (!Available(sim)) return null;
            string id = sim.Config.Game.Market.Goods[good].Id;
            foreach (Contract c in State(sim).Open) if (c.Good == id && c.Delivery == sim.Period && c.Left > 0) return c;
            return null;
        }

        /// <summary>What a sale of <paramref name="quantity"/> to the trader pays beyond (or, if the market is dearer, below) the floor, because of the contract it would fill; 0 with none.</summary>
        public static long Extra(Simulation sim, int good, long quantity, Fixed floor)
        {
            Contract c = Running(sim, good);
            if (c == null) return 0;
            long take = quantity < c.Left ? quantity : c.Left, unit = sim.Config.Game.Market.Goods[good].UnitSize;
            return Money.Value(take, c.Price, unit) - Money.Value(take, floor, unit);
        }

        /// <summary>What the trader pays for a sale now: the floor's value, and the contract's difference on its first kilos. The Sell buttons show this.</summary>
        public static long SaleValue(Simulation sim, int good, long quantity, Fixed floor) { return MarketRules.Value(sim, good, quantity, floor) + Extra(sim, good, quantity, floor); }

        /// <summary>
        /// A sale to the trader of a contracted crop: the first kilos, up to what is left of the contract, are paid at the contract price. Returns the coins to add to (or, when the
        /// market is dearer, take from) the sale at the floor.
        /// </summary>
        internal static long Deliver(Simulation sim, int good, long quantity, Fixed floor)
        {
            Contract c = Running(sim, good);
            if (c == null) return 0;
            long take = quantity < c.Left ? quantity : c.Left, extra = Extra(sim, good, quantity, floor);
            c.Filled += take;
            c.Gain += extra;
            sim.Emit(new ContractFilled(sim.State.Clock.SimTime, c.Id, c.Good, take, c.Filled, c.Quantity, c.Price, extra));
            return extra;
        }

        /// <summary>The price a contract's shortfall is worked at: the average of the floor prices seen in its season, or today's with none yet.</summary>
        public static Fixed AverageFloor(Simulation sim, Contract c)
        {
            return c.Samples > 0 ? c.FloorSum / Fixed.FromInt(c.Samples) : MarketRules.Floor(sim, sim.Config.Game.Market.IndexOf(c.Good));
        }

        /// <summary>What the unfilled kilos cost at the close: the unfilled quantity × what the average price rose above the contract's. A price that fell costs nothing.</summary>
        public static long Shortfall(Simulation sim, Contract c)
        {
            long left = c.Left;
            if (left <= 0) return 0;
            Fixed delta = AverageFloor(sim, c) - c.Price;
            return delta > Fixed.Zero ? Money.Value(left, delta, sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(c.Good)].UnitSize) : 0;
        }

        /// <summary>What a contract has gained or lost against the market so far, as the Store sheet says it: the delivered gain, less the shortfall if it closed now.</summary>
        public static long Against(Simulation sim, Contract c) { return c.Gain - Shortfall(sim, c); }

        /// <summary>What the next close takes for contracts: what was carried, and the running delivery season's shortfall as it stands. For the dues list.</summary>
        public static long Due(Simulation sim)
        {
            if (!Available(sim)) return 0;
            ContractsState s = State(sim);
            long total = s.Owed;
            foreach (Contract c in s.Open) if (c.Delivery == sim.Period) total += Shortfall(sim, c);
            return total;
        }

        // ---- accepting (§3.7) ----

        /// <summary>The contracts held for one delivery season (the limit and "one a crop" are counted within a delivery season, so a contract still delivering does not block next season's offer).</summary>
        public static int HeldFor(Simulation sim, long delivery)
        {
            int n = 0;
            foreach (Contract c in State(sim).Open) if (c.Delivery == delivery) n++;
            return n;
        }

        internal static RejectReason Accept(Simulation sim, long id)
        {
            if (!Available(sim)) return RejectReason.NoTownship;
            ContractsState s = State(sim);
            Contract o = s.Offers.Find(x => x.Id == id);
            if (o == null)
            {
                if (s.Open.Exists(x => x.Id == id)) return RejectReason.AlreadyContracted;
                return id > 0 && id < s.NextId ? RejectReason.OfferLapsed : RejectReason.UnknownOffer;
            }
            if (s.Open.Exists(x => x.Delivery == o.Delivery && x.Good == o.Good)) return RejectReason.AlreadyContracted;
            if (HeldFor(sim, o.Delivery) >= Def(sim).MaxOpen) return RejectReason.TooManyContracts;
            s.Offers.Remove(o);
            s.Open.Add(o);
            sim.Emit(new ContractAccepted(sim.State.Clock.SimTime, o.Id, o.Good, o.Quantity, o.Price, o.Delivery));
            return RejectReason.None;
        }

        // ---- the close (§3.7) ----

        /// <summary>
        /// At a close, after the loans and the other charges: the delivery season's contracts are settled (a shortfall is paid from cash, what cash cannot pay is carried), last
        /// season's harvest becomes the basis, and the trader makes this season's offers.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            if (!Available(sim)) return;
            ContractsState s = State(sim);
            long closing = sim.Period - 1, now = sim.State.Clock.SimTime;
            Pay(sim, s, now, s.Owed, false); // what was carried comes first
            for (int i = 0; i < s.Open.Count; i++)
            {
                Contract c = s.Open[i];
                if (c.Delivery != closing) continue;
                long shortfall = Shortfall(sim, c);
                Pay(sim, s, now, shortfall, true);
                s.History.Add(new ContractRecord(c.Good, c.Quantity, c.Filled, c.Price, c.Delivery, shortfall, c.Gain - shortfall));
                while (s.History.Count > Def(sim).KeepHistory) s.History.RemoveAt(0);
                sim.Emit(new ContractSettled(now, c.Id, c.Good, c.Quantity, c.Filled, shortfall, c.Gain - shortfall));
                s.Open.RemoveAt(i--);
            }
            s.HarvestLast = s.HarvestThis;
            s.HarvestThis = new System.Collections.Generic.List<HarvestTally>();
            Offer(sim, s);
        }

        /// <summary>Pays what cash allows of a charge; a new shortfall's unpaid part is added to what is carried, a carried one's stays.</summary>
        private static void Pay(Simulation sim, ContractsState s, long now, long amount, bool isNew)
        {
            if (amount <= 0) return;
            long pay = System.Math.Min(amount, System.Math.Max(0, sim.State.Ledger.Cash));
            if (pay > 0)
                sim.State.Ledger.Post(now, sim.Period, "ledger.contractSettlement", "contract", new Posting(Account.ContractSettlement, pay), new Posting(Account.Cash, -pay));
            if (isNew) s.Owed += amount - pay;
            else s.Owed -= pay;
        }

        /// <summary>The offers for the season after this one: the biggest 2 of last season's staple harvests at 300 kg or more, 40% of each, at 3% under the expected price.</summary>
        private static void Offer(Simulation sim, ContractsState s)
        {
            s.Offers.Clear();
            ContractsDef d = Def(sim);
            MarketDefs market = sim.Config.Game.Market;
            long delivery = sim.Period + 1;
            int season = (int)((delivery) % ClockDef.SeasonsPerYear);
            var picked = new System.Collections.Generic.List<HarvestTally>();
            foreach (string id in d.Crops)
            {
                int g = market.IndexOf(id);
                long kg = ContractsState.Of(s.HarvestLast, id) / market.Goods[g].UnitSize;
                if (kg >= d.MinKg) picked.Add(new HarvestTally(id, ContractsState.Of(s.HarvestLast, id)));
            }
            picked.Sort((a, b) => b.Quantity != a.Quantity ? b.Quantity.CompareTo(a.Quantity) : System.String.CompareOrdinal(a.Good, b.Good));
            for (int i = 0; i < picked.Count && i < d.TopCrops; i++)
            {
                int g = market.IndexOf(picked[i].Good);
                long unit = market.Goods[g].UnitSize;
                long kg = Money.MulRate(picked[i].Quantity / unit, d.ShareOfHarvest);
                kg = (kg + d.RoundKg / 2) / d.RoundKg * d.RoundKg;
                if (kg < d.RoundKg) continue;
                Fixed expected = Expected(sim, g, season);
                if (expected <= Fixed.Zero) continue;
                var offer = new Contract { Id = s.NextId++, Good = picked[i].Good, Quantity = kg * unit, Expected = expected, Price = OfferPrice(sim, expected), Delivery = delivery };
                s.Offers.Add(offer);
                sim.Emit(new ContractOffered(sim.State.Clock.SimTime, offer.Id, offer.Good, offer.Quantity, offer.Price, offer.Expected, delivery));
            }
        }
    }
}
