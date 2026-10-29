using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;

namespace Sim.Core.Business
{
    /// <summary>
    /// Businesses (spec 016): where one may stand, founding, the worker, the mill's hour (the area's grinding, then your own wheat,
    /// then the market's), its sales, and its season close. Every coin amount is a year-0 price × the index when charged.
    /// </summary>
    public static class BusinessRules
    {
        public const string Job = "business.build";
        private const long Hour = 3600, Day = 86_400;

        public static BusinessDefs Defs(Simulation sim) { return sim.Config.Game.Businesses; }

        public static BusinessTypeDef TypeOf(Simulation sim, Business b) { return Defs(sim).Types[b.Type]; }

        public static bool Unlocked(Simulation sim, BusinessTypeDef t) { return sim.State.Era != null && sim.State.Era.Current >= t.Era; }

        /// <summary>The business on a tile (being built or open), or null.</summary>
        public static Business On(Simulation sim, int tileId) { return sim.State.Businesses?.On(tileId); }

        /// <summary>Where a business may stand (§3.3): an acre you own with nothing growing, no house, one business an acre.</summary>
        public static RejectReason Place(Simulation sim, int tileId)
        {
            if (sim.State.Businesses == null || tileId < 0 || tileId >= sim.State.Land.Tiles.Count) return RejectReason.UnknownTile;
            Tile tile = sim.State.Land.Tiles[tileId];
            if (!sim.State.Land.Parcels[tile.ParcelId].Owned) return RejectReason.NotOwned;
            if (tile.Terrain != Terrain.Farm) return RejectReason.TerrainNotAllowed;
            if (sim.State.Farm.CropOn(tileId) != null || HouseRules.HousesOn(sim, tileId) > 0) return RejectReason.TileOccupied;
            return On(sim, tileId) != null ? RejectReason.NoRoom : RejectReason.None;
        }

        /// <summary>The building and equipment at today's prices.</summary>
        public static long Cost(Simulation sim, BusinessTypeDef t) { return MarketRules.Indexed(sim, t.BuildingValue + t.EquipmentValue); }

        public static long Wage(Simulation sim, BusinessTypeDef t) { return MarketRules.Indexed(sim, t.Wage); }

        /// <summary>The area's grain a day (§3.5b): (catchment + your town) × the grain a person brings × days of life a real day, grams.</summary>
        public static long AreaGramsPerDay(Simulation sim)
        {
            BusinessDefs d = Defs(sim);
            int people = d.CatchmentPeople + (sim.State.Citizens?.Population ?? 0);
            return (d.GrainPerPersonPerRealDay * sim.Config.Game.Population.RealDaysPerGameDay * Fixed.FromInt(people) * Fixed.FromInt(1000)).RoundToLong();
        }

        /// <summary>Whether the area brings its grain at this fee: no more than the next village's chakki's fee plus the tolerance (§3.7).</summary>
        public static bool AreaComes(Simulation sim, Business b) { return b.Fee <= Defs(sim).NextVillageFee + Defs(sim).FeeTolerance; }

        /// <summary>The plan's season, line by line (§3.3), for the plan screen and its *Why?*s.</summary>
        public sealed class Projection
        {
            public long AreaGrams, OwnGrams, Fees, Sales, Wheat, Power, Repairs, Depreciation, Wage, Interest, Loan, Revenue, Costs, Profit, Cost, CashNeeded;
        }

        /// <summary>
        /// The season ahead as planned (§3.3): the area's fees and your wheat's margin at today's prices, against the wage (or none when you
        /// run it), power, repairs and depreciation; a season's running cash is the wage or, run by you, the power and repairs.
        /// </summary>
        public static BusinessPlan Project(Simulation sim, BusinessTypeDef t, bool runYourself, long ownPerDay, Fixed fee, long loan = 0)
        {
            Projection p = Projected(sim, t, runYourself, ownPerDay, fee, loan);
            return new BusinessPlan
            {
                Revenue = p.Revenue, Costs = p.Costs, Profit = p.Profit, CashNeeded = p.CashNeeded, MadeAt = sim.State.Clock.SimTime, AreaGrams = p.AreaGrams, OwnGrams = p.OwnGrams,
                Fees = p.Fees, Sales = p.Sales, Input = p.Wheat, Power = p.Power, Repairs = p.Repairs, Depreciation = p.Depreciation, Wage = p.Wage, Interest = p.Interest,
            };
        }

        /// <param name="loan">A business loan taken (§3.8): its first season's interest is a cost, and it pays for that much of the cash needed.</param>
        public static Projection Projected(Simulation sim, BusinessTypeDef t, bool runYourself, long ownPerDay, Fixed fee, long loan = 0, long[] mix = null)
        {
            if (t.ByRecipe) return ProjectedByRecipe(sim, t, runYourself, loan, mix ?? DefaultMix(sim, t), ownPerDay);
            int days = sim.Clock.DaysPerSeason;
            Fixed index = sim.State.Market.PriceIndex;
            long day = t.GramsPerHour * (t.CloseHour - t.OpenHour), areaDay = System.Math.Min(AreaGramsPerDay(sim), day);
            long area = areaDay * days;
            long own = System.Math.Min(ownPerDay, day - areaDay) * days; // your wheat fills what the area leaves (§3.5)
            long fees = Money.Value(area, fee * index, 1000);
            long wheat = MarketRules.Value(sim, t.Input, own, MarketRules.Floor(sim, t.Input));
            long sales = 0;
            foreach (BusinessOutput o in t.Outputs)
                sales += MarketRules.Value(sim, o.Good, (o.Yield * Fixed.FromInt(own)).RoundToLong(), MarketRules.Floor(sim, o.Good));
            long power = Money.Value(area + own, t.PowerPerKg * index, 1000);
            long repairs = Money.MulRate(MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare);
            long depreciation = MarketRules.Indexed(sim, t.BuildingValue) / t.BuildingLifeSeasons + MarketRules.Indexed(sim, t.EquipmentValue) / t.EquipmentLifeSeasons;
            long wage = runYourself ? 0 : Wage(sim, t);
            long interest = loan <= 0 || sim.State.Credit == null ? 0
                : Money.MulRate(loan, CreditRules.Rate(sim.Config.Game.Credit.Lenders[sim.Config.Game.Credit.IndexOfLender(Defs(sim).LenderId)], sim.State.Credit.Score));
            long revenue = fees + sales, costs = wheat + power + repairs + depreciation + wage + interest;
            return new Projection
            {
                AreaGrams = area, OwnGrams = own, Fees = fees, Sales = sales, Wheat = wheat, Power = power, Repairs = repairs, Depreciation = depreciation, Wage = wage,
                Interest = interest, Loan = loan, Revenue = revenue, Costs = costs, Profit = revenue - costs, Cost = Cost(sim, t),
                CashNeeded = Cost(sim, t) + (runYourself ? power + repairs : wage),
            };
        }

        /// <summary>
        /// A recipe type's season ahead (§3.9c): the day's mix sold to the shops (and the trader for what keeps), its inputs at what
        /// they'd cost (<see cref="InputPrice"/>) less what it makes itself, with the supplies; fuel; against the wage, repairs and wear.
        /// </summary>
        private static Projection ProjectedByRecipe(Simulation sim, BusinessTypeDef t, bool runYourself, long loan, long[] mix, long milkPerDay)
        {
            int days = sim.Clock.DaysPerSeason;
            Fixed index = sim.State.Market.PriceIndex;
            var made = new long[mix.Length];
            long milkCost = 0;
            if (t.TakesMilk) made = DairyDay(sim, t, mix, milkPerDay, true, out milkCost); // made as far as the milk goes
            else
            {
                foreach (int p in t.Products) made[p] += mix[p];
                foreach (RecipeDef r in t.Recipes) // a product made into another (pav into rusk) is made as well
                    if (First(t, r.Output) == r)
                        foreach (KeyValuePair<int, long> kv in r.Inputs)
                            if (t.Makes(kv.Key)) made[kv.Key] += Need(kv.Value, mix[r.Output], r.OutputGrams);
            }
            long sales = 0, inputs = milkCost * days, fuel = 0, total = 0;
            foreach (int p in t.Products)
            {
                long sold = System.Math.Min(t.TakesMilk ? made[p] : mix[p], ShopGramsPerDay(sim, t, p) + sim.Config.Game.Market.Region.TraderBuyPerDay(p));
                sales += MarketRules.Value(sim, p, sold * days, MarketRules.Floor(sim, p));
                RecipeDef r = First(t, p);
                foreach (KeyValuePair<int, long> kv in r.Inputs)
                    if (!t.Makes(kv.Key) && !t.TakesMilk) inputs += MarketRules.Value(sim, kv.Key, Need(kv.Value, made[p], r.OutputGrams) * days, InputPrice(sim, kv.Key));
                fuel += Money.Value(made[p] * days, r.Fuel * index, r.OutputGrams);
                inputs += Money.Value(made[p] * days, r.Supplies * index, r.OutputGrams); // supplies go into the goods' cost, as in its books
                total += made[p] * days;
            }
            long repairs = Money.MulRate(MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare);
            long depreciation = MarketRules.Indexed(sim, t.BuildingValue) / t.BuildingLifeSeasons + MarketRules.Indexed(sim, t.EquipmentValue) / t.EquipmentLifeSeasons;
            long wage = runYourself ? 0 : Wage(sim, t);
            long interest = loan <= 0 || sim.State.Credit == null ? 0
                : Money.MulRate(loan, CreditRules.Rate(sim.Config.Game.Credit.Lenders[sim.Config.Game.Credit.IndexOfLender(Defs(sim).LenderId)], sim.State.Credit.Score));
            long costs = inputs + fuel + repairs + depreciation + wage + interest;
            // Working capital: a day's inputs and fuel, bought before the next 6:00 round pays for them (§3.8, owner 2026-10-28).
            long aDay = (inputs + fuel + days - 1) / days;
            return new Projection
            {
                OwnGrams = total, Sales = sales, Wheat = inputs, Power = fuel, Repairs = repairs, Depreciation = depreciation, Wage = wage, Interest = interest, Loan = loan,
                Revenue = sales, Costs = costs, Profit = sales - costs, Cost = Cost(sim, t), CashNeeded = Cost(sim, t) + (runYourself ? fuel + repairs : wage) + aDay,
            };
        }

        /// <summary>
        /// What a recipe's input costs a kg (§3.9c): the trader's price when a business of yours makes it (your mill's atta), else the
        /// market's import price.
        /// </summary>
        public static Fixed InputPrice(Simulation sim, int good)
        {
            int kind = MilkKind(sim, good);
            if (kind >= 0) return Livestock.LivestockRules.MilkPrice(sim, kind); // your milk, at the co-op's price
            if (sim.State.Businesses != null)
                foreach (Business b in sim.State.Businesses.Businesses)
                {
                    if (b.Status == BusinessStatus.Closed) continue;
                    foreach (BusinessOutput o in TypeOf(sim, b).Outputs) if (o.Good == good) return MarketRules.Floor(sim, good);
                }
            return MarketRules.Ceiling(sim, good);
        }

        /// <summary>The livestock kind whose milk <paramref name="good"/> is, or −1.</summary>
        public static int MilkKind(Simulation sim, int good)
        {
            var kinds = sim.Config.Game.Livestock?.Animals;
            if (kinds == null) return -1;
            for (int k = 0; k < kinds.Count; k++) if (sim.Config.Game.Market.IndexOf("milk_" + kinds[k].Id) == good) return k;
            return -1;
        }

        /// <summary>The milk neighbours sell the dairy (§3.9b): the village's dearest, buffalo milk.</summary>
        public static int NeighbourMilkKind(Simulation sim)
        {
            var kinds = sim.Config.Game.Livestock.Animals;
            int best = 0;
            for (int k = 1; k < kinds.Count; k++) if (Livestock.LivestockRules.MilkPrice(sim, k) > Livestock.LivestockRules.MilkPrice(sim, best)) best = k;
            return best;
        }

        /// <summary>A litre of neighbours' milk: the co-op's price that morning and the dairy's premium (§3.9b).</summary>
        public static Fixed NeighbourMilkPrice(Simulation sim, BusinessTypeDef t)
        {
            return Livestock.LivestockRules.MilkPrice(sim, NeighbourMilkKind(sim)) + t.NeighbourMilkPremium * sim.State.Market.PriceIndex;
        }

        /// <summary>The herd's milk a real day by kind (ml), less what the household drinks (the dearest milk first): the plan's estimate.</summary>
        public static long[] HerdMilk(Simulation sim)
        {
            var kinds = sim.Config.Game.Livestock?.Animals;
            var ml = new long[kinds?.Count ?? 0];
            if (kinds == null || sim.State.Livestock == null) return ml;
            Time.Season season = Time.Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            foreach (Livestock.Animal a in sim.State.Livestock.Animals)
                ml[a.Kind] += (Livestock.LivestockRules.BaseMilkMl(sim, a, season) * a.Health).RoundToLong();
            long home = (Livestock.LivestockRules.PerGameDay(sim, Livestock.LivestockRules.Defs(sim).Livestock.HouseholdLitresPerDay) * Fixed.FromInt(1000)).RoundToLong();
            var order = new List<int>();
            for (int k = 0; k < kinds.Count; k++) order.Add(k);
            order.Sort((x, y) => { int c = Livestock.LivestockRules.MilkPrice(sim, y).CompareTo(Livestock.LivestockRules.MilkPrice(sim, x)); return c != 0 ? c : x.CompareTo(y); });
            foreach (int k in order)
            {
                long q = System.Math.Min(home, ml[k]);
                ml[k] -= q;
                home -= q;
            }
            return ml;
        }

        /// <summary>The milk the day's mix needs (ml), by each product's first recipe.</summary>
        public static long MilkFor(BusinessTypeDef t, long[] mix)
        {
            long n = 0;
            foreach (int p in t.Products)
            {
                RecipeDef r = First(t, p);
                foreach (KeyValuePair<int, long> kv in r.Inputs) if (!t.Makes(kv.Key)) n += Need(kv.Value, mix[p], r.OutputGrams);
            }
            return n;
        }

        /// <summary>A new business's own input a day: the dairy takes the milk its starting mix needs; the mill its type's wheat a day.</summary>
        public static long DefaultOwnPerDay(Simulation sim, BusinessTypeDef t) { return t.TakesMilk ? MilkFor(t, DefaultMix(sim, t)) : t.OwnPerDay; }

        /// <summary>
        /// A day at the dairy as the plan sees it (§3.9b, §3.9c): the milk in at 6:00 (your herd's up to the day's amount, then
        /// neighbours' if allowed), each recipe in order toward the mix within the day's rate. Returns what's made; the milk's cost.
        /// </summary>
        private static long[] DairyDay(Simulation sim, BusinessTypeDef t, long[] mix, long perDay, bool neighbours, out long milkCost)
        {
            var kinds = sim.Config.Game.Livestock.Animals;
            var stock = new long[mix.Length];
            long room = perDay, capacity = t.GramsPerHour * (t.CloseHour - t.OpenHour);
            long[] herd = HerdMilk(sim);
            milkCost = 0;
            for (int k = 0; k < kinds.Count && room > 0; k++)
            {
                long q = System.Math.Min(room, herd[k]);
                stock[sim.Config.Game.Market.IndexOf("milk_" + kinds[k].Id)] += q;
                milkCost += Money.Value(q, Livestock.LivestockRules.MilkPrice(sim, k), 1000);
                room -= q;
            }
            if (neighbours && room > 0)
            {
                stock[sim.Config.Game.Market.IndexOf("milk_" + kinds[NeighbourMilkKind(sim)].Id)] += room;
                milkCost += Money.Value(room, NeighbourMilkPrice(sim, t), 1000);
            }
            var made = new long[mix.Length];
            foreach (RecipeDef r in t.Recipes)
            {
                long q = System.Math.Min(mix[r.Output] - made[r.Output], (long)((System.Numerics.BigInteger)capacity * r.OutputGrams / r.InputGrams));
                foreach (KeyValuePair<int, long> kv in r.Inputs) q = System.Math.Min(q, (long)((System.Numerics.BigInteger)stock[kv.Key] * r.OutputGrams / kv.Value));
                if (q <= 0) continue;
                made[r.Output] += q;
                foreach (KeyValuePair<int, long> kv in r.Inputs) stock[kv.Key] -= Need(kv.Value, q, r.OutputGrams);
                capacity -= RecipeInputs(r, q);
            }
            return made;
        }

        /// <summary>The goods a recipe type buys in (atta for the bakery), in recipe order, each once.</summary>
        public static List<int> BoughtInputs(BusinessTypeDef t)
        {
            var list = new List<int>();
            foreach (RecipeDef r in t.Recipes)
                foreach (KeyValuePair<int, long> kv in r.Inputs)
                    if (!t.Makes(kv.Key) && !list.Contains(kv.Key)) list.Add(kv.Key);
            return list;
        }

        /// <summary>A product's first recipe (the preferred one), or null.</summary>
        public static RecipeDef First(BusinessTypeDef t, int product)
        {
            foreach (RecipeDef r in t.Recipes) if (r.Output == product) return r;
            return null;
        }

        /// <summary>Grams of an input for <paramref name="made"/> grams of output, rounded up.</summary>
        private static long Need(long perBatch, long made, long outputGrams) { return (long)((System.Numerics.BigInteger)perBatch * made + outputGrams - 1) / outputGrams; }

        /// <summary>The people the village shops serve (§3.9c): the area's and your town's.</summary>
        public static int AreaPeople(Simulation sim) { return Defs(sim).CatchmentPeople + (sim.State.Citizens?.Population ?? 0); }

        /// <summary>What the shops buy of a good each morning, grams: kg a person a day of life × 22.8 × the people.</summary>
        public static long ShopGramsPerDay(Simulation sim, BusinessTypeDef t, int good)
        {
            foreach (KeyValuePair<int, Fixed> kv in t.Shops)
                if (kv.Key == good)
                    return (kv.Value * sim.Config.Game.Population.RealDaysPerGameDay * Fixed.FromInt(AreaPeople(sim)) * Fixed.FromInt(1000)).RoundToLong();
            return 0;
        }

        /// <summary>
        /// The day's mix a new business starts with, and the plan's (§3.9c, owner 2026-10-28): what the shops will buy each morning, and
        /// for what keeps, what the trader takes as well.
        /// </summary>
        public static long[] DefaultMix(Simulation sim, BusinessTypeDef t)
        {
            var mix = new long[sim.Config.Game.Market.Goods.Count];
            foreach (int p in t.Products) mix[p] = ShopGramsPerDay(sim, t, p) + (Pays(sim, t, p) ? sim.Config.Game.Market.Region.TraderBuyPerDay(p) : 0);
            return mix;
        }

        /// <summary>
        /// Whether a product sells for more than it costs to make at today's prices (owner, 2026-10-28, ADR 0080): ghee from whole milk
        /// doesn't, so the starting mix leaves the trader's ghee out.
        /// </summary>
        public static bool Pays(Simulation sim, BusinessTypeDef t, int product) { return MarketRules.Floor(sim, product) > UnitCost(sim, t, product); }

        /// <summary>A kg (or litre) of a good to this business at today's prices: bought inputs at what they cost, made ones by their recipe.</summary>
        public static Fixed UnitCost(Simulation sim, BusinessTypeDef t, int good)
        {
            RecipeDef r = First(t, good);
            if (r == null) return t.TakesMilk && MilkKind(sim, good) >= 0 ? NeighbourMilkPrice(sim, t) : InputPrice(sim, good); // the dairy's next litre is neighbours'
            Fixed batch = (r.Fuel + r.Supplies) * sim.State.Market.PriceIndex;
            foreach (KeyValuePair<int, long> kv in r.Inputs) batch += UnitCost(sim, t, kv.Key).MulRatio(kv.Value, 1000);
            return batch.MulRatio(1000, r.OutputGrams);
        }

        // ---- founding (§3.3) ----

        internal static CommandResult Found(Simulation sim, string typeId, int tileId, long capital, bool runYourself, Financing financing = null)
        {
            BusinessDefs d = Defs(sim);
            int type = d == null ? -1 : d.IndexOfType(typeId);
            if (type < 0) return CommandResult.Reject(RejectReason.UnknownBusiness);
            BusinessTypeDef t = d.Types[type];
            if (!Unlocked(sim, t)) return CommandResult.Reject(RejectReason.EraLocked);
            RejectReason place = Place(sim, tileId);
            if (place != RejectReason.None) return CommandResult.Reject(place);
            long loan = financing == null ? 0 : financing.Amount;
            BusinessPlan plan = Project(sim, t, runYourself, DefaultOwnPerDay(sim, t), t.Fee, loan);
            int lender = -1;
            if (financing != null)
            {
                RejectReason r = BusinessBooks.CheckLoan(sim, t, financing, out lender);
                if (r != RejectReason.None) return CommandResult.Reject(r);
            }
            if (capital + loan < plan.CashNeeded) return CommandResult.Reject(RejectReason.CapitalTooLow);
            if (!sim.State.Ledger.CanAfford(capital)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long now = sim.State.Clock.SimTime;
            BusinessesState s = sim.State.Businesses;
            int goods = sim.Config.Game.Market.Goods.Count;
            var b = new Business
            {
                Id = s.NextId++, Type = type, TileId = tileId, Status = BusinessStatus.Building, FoundedAt = now, ReadyAt = now + t.BuildHours * Hour,
                OpenHour = t.OpenHour, CloseHour = t.CloseHour, Fee = t.Fee, OwnPerDay = DefaultOwnPerDay(sim, t), Worker = runYourself ? WorkerKind.Founder : WorkerKind.None,
                Ledger = new Ledger(), Stock = new long[goods], StockCost = new long[goods], Plan = plan, Day = now / Day,
                Mix = DefaultMix(sim, t), MadeToday = new long[goods], BuyFromMarket = t.ByRecipe, // the bakery buys atta your mill can't give; the dairy, neighbours' milk
            };
            string reference = "business:" + b.Id;
            sim.State.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.InvestmentInBusinesses, capital), new Posting(Account.Cash, -capital));
            b.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.Cash, capital), new Posting(Account.OwnersCapital, -capital));
            s.Businesses.Add(b);
            if (financing != null) b.LoanId = CreditSystem.Create(sim, lender, loan, financing.TermSeasons, b.Id).Id; // to the business, your guarantee (§3.8)
            long building = MarketRules.Indexed(sim, t.BuildingValue), equipment = MarketRules.Indexed(sim, t.EquipmentValue);
            b.Ledger.Post(now, sim.Period, "ledger.businessBuilt", reference,
                new Posting(Account.Buildings, building), new Posting(Account.Equipment, equipment), new Posting(Account.Cash, -building - equipment));
            LandSystem.SetOccupied(sim, tileId, true);
            sim.State.People?.Orders.RemoveAll(o => o.Kind == OrderTargetKind.Field && o.Target == tileId);
            sim.ScheduleJob(Job, b.Id, b.ReadyAt);
            sim.Emit(new BusinessFounded(now, b.Id, t.Id, tileId, capital));
            return CommandResult.Ok;
        }

        /// <summary>Built (§3.3 step 7): it opens, run by you or by whoever you hired on the board (spec 015 §3.1); with nobody it waits for a hire.</summary>
        internal sealed class Build : IJobHandler
        {
            public string JobType { get { return Job; } }
            public SystemOrder System { get { return SystemOrder.BusinessFinance; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                Business b = sim.State.Businesses.Find(job.TargetId);
                sim.CompleteJob(job);
                if (b == null || b.Status != BusinessStatus.Building) return;
                b.Status = BusinessStatus.Open;
                sim.Emit(new BusinessOpened(sim.State.Clock.SimTime, b.Id, b.Worker, b.WorkerCitizen));
            }
        }

        /// <summary>
        /// A townsperson worker who has gone from the town, or turned elder (and retires), leaves the job (spec 015 §3.9): the business
        /// waits for a hire or for you.
        /// </summary>
        private static void KeepStaffed(Simulation sim, Business b)
        {
            if (b.Worker != WorkerKind.Citizen) return;
            Citizen c = sim.State.Citizens.Citizen(b.WorkerCitizen);
            bool working = c != null && c.Living && c.Job == CitizenJob.Business && c.Employer == b.Id;
            if (working && !HouseholdRules.IsElder(sim, c)) return;
            long worker = sim.State.People.WorkerAt(b.Id)?.Id ?? 0;
            LoseWorker(sim, b);
            if (working)
            {
                c.Job = CitizenJob.Retired;
                c.Story.Add(new StoryLine(StoryKind.Retired, sim.State.Clock.SimTime, 0));
                HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId));
            }
            sim.Emit(new WorkerLeft(sim.State.Clock.SimTime, b.Id, worker, 0));
        }

        /// <summary>The worker leaves: a townsperson goes back to outside work (their household's budget follows).</summary>
        internal static void LoseWorker(Simulation sim, Business b)
        {
            HiringRules.WorkerGone(sim, b);
            if (b.Worker == WorkerKind.Citizen)
            {
                Citizen c = sim.State.Citizens.Citizen(b.WorkerCitizen);
                if (c != null && c.Job == CitizenJob.Business && c.Employer == b.Id)
                {
                    c.Job = CitizenJob.None;
                    c.Employer = 0;
                    HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId));
                }
            }
            if (b.Worker != WorkerKind.Founder) b.Worker = WorkerKind.None;
            b.WorkerCitizen = 0;
        }

        internal static void PayWage(Simulation sim, Business b, long wage)
        {
            if (wage <= 0) return;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.wages", "business:" + b.Id, new Posting(Account.HiredLabour, wage), new Posting(Account.Cash, -wage));
        }

        // ---- the hour (§3.4, §3.5) ----

        /// <summary>Hourly pass (§3.11): each open, staffed business works the hour that just ended, in id order.</summary>
        internal static void Hourly(Simulation sim)
        {
            bool morning = MarketRules.HourOfDay(sim) == (sim.Config.Game.Market.Truck?.ArriveHour ?? 6);
            foreach (Business b in sim.State.Businesses.Businesses)
                if (b.Status == BusinessStatus.Open)
                {
                    if (morning) Morning(sim, b);
                    Work(sim, b);
                }
        }

        /// <summary>Whether the business works in the hour of day <paramref name="hour"/>: open then and staffed (its worker not away on a course).</summary>
        public static bool OpenAt(Simulation sim, Business b, int hour)
        {
            if (b.Worker == WorkerKind.None || hour < b.OpenHour || hour >= b.CloseHour) return false;
            Worker w = HiredWorker(sim, b);
            return w == null || !WorkRules.Away(w);
        }

        /// <summary>Whether <paramref name="hour"/> is past the village's usual day (the type's hours): a hired worker's overtime (spec 015 §3.4).</summary>
        public static bool Overtime(Simulation sim, Business b, int hour)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            return hour < t.OpenHour || hour >= t.CloseHour;
        }

        /// <summary>Hours a day it's open past the village's usual day: a long day for its worker.</summary>
        public static int LongHours(Simulation sim, Business b)
        {
            int n = 0;
            for (int h = b.OpenHour; h < b.CloseHour; h++) if (Overtime(sim, b, h)) n++;
            return n;
        }

        /// <summary>Someone works it today: you, or a hired worker not away on a course or sick leave.</summary>
        public static bool Staffed(Simulation sim, Business b)
        {
            if (b.Worker == WorkerKind.None) return false;
            Worker w = HiredWorker(sim, b);
            return w == null || !WorkRules.Away(w);
        }

        /// <summary>The business's hired worker, or null when nobody is hired or you run it yourself.</summary>
        public static Worker HiredWorker(Simulation sim, Business b) { return b.Worker == WorkerKind.Founder ? null : sim.State.People?.WorkerAt(b.Id); }

        /// <summary>The rate an hour (spec 015 §3.4): its grams an hour × its worker's share (a skill-50 worker's is 1); yours as rated when you run it.</summary>
        public static long Capacity(Simulation sim, Business b)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            Worker w = HiredWorker(sim, b);
            return w == null ? t.GramsPerHour : WorkRules.RelativeOutput(sim, w).MulRatio(t.GramsPerHour, 1).RoundToLong();
        }

        /// <summary>The product of the worker's two traits' <paramref name="factor"/> (creative's sale, frugal's costs); 1 when you run it.</summary>
        private static Fixed TraitFactor(Simulation sim, Business b, System.Func<TraitDef, Fixed> factor)
        {
            Worker w = HiredWorker(sim, b);
            if (w == null) return Fixed.One;
            var traits = sim.Config.Game.People.Traits;
            return factor(traits[w.Trait1]) * factor(traits[w.Trait2]);
        }

        /// <summary>A sale's price: 5% more when a creative worker made it (spec 015 §3.6).</summary>
        private static long SalePrice(Simulation sim, Business b, long amount)
        {
            Fixed f = TraitFactor(sim, b, x => x.SaleFactor);
            return f == Fixed.One ? amount : Money.MulRate(amount, f);
        }

        /// <summary>Power, fuel and supplies: 10% less with a frugal worker (spec 015 §3.6).</summary>
        private static Fixed CostFactor(Simulation sim, Business b) { return TraitFactor(sim, b, x => x.CostFactor); }

        private static void Work(Simulation sim, Business b)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            long start = sim.State.Clock.SimTime - Hour;
            if (start / Day != b.Day)
            {
                b.Day = start / Day;
                b.OwnToday = 0;
                b.AreaToday = 0;
            }
            int hour = (int)(start % Day / Hour);
            KeepStaffed(sim, b);
            if (!OpenAt(sim, b, hour)) return;
            Worker hired = HiredWorker(sim, b);
            if (hired != null && Overtime(sim, b, hour) && !WorkRules.Pay(sim, hired, WorkRules.OvertimeHour(sim, hired), "ledger.overtime")) return; // past the overdraft: no overtime
            if (t.ByRecipe)
            {
                long made = 0;
                foreach (int p in t.Products) made += b.MadeToday[p];
                long used = Make(sim, b, t);
                if (made == 0 && used > 0) HrRules.FirstBatch(sim, b);
                BusinessBooks.Sweep(sim, b);
                if (used > 0) sim.Emit(new BusinessWorked(sim.State.Clock.SimTime, b.Id, 0, used));
                return;
            }
            long capacity = Capacity(sim, b), now = sim.State.Clock.SimTime;
            Fixed index = sim.State.Market.PriceIndex;
            string reference = "business:" + b.Id;

            // The area's grain first (§3.5b): it comes over the village's usual hours, and is lost to the next village if not ground then.
            long area = 0;
            if (hour >= t.OpenHour && hour < t.CloseHour && AreaComes(sim, b))
            {
                area = System.Math.Min(capacity, AreaGramsPerDay(sim) / (t.CloseHour - t.OpenHour));
                long fees = Money.Value(area, b.Fee * index, 1000);
                if (PowerCost(sim, b, area) > fees + BusinessBooks.CanSpend(sim, b)) area = fees = 0; // a fee below the power's, with no cash left (§3.10)
                if (fees > 0) b.Ledger.Post(now, sim.Period, "ledger.grindingFees", reference, new Posting(Account.Cash, fees), new Posting(Account.ServiceIncome, -fees));
                Power(sim, b, area);
                b.AreaToday += area;
                b.AreaSeason += area;
            }
            long left = capacity - area;

            // Then your own wheat, at today's market price, up to the daily amount (§3.5c).
            long own = System.Math.Min(left, b.OwnPerDay - b.OwnToday);
            own = System.Math.Min(own, sim.State.Market.Stock[t.Input] - sim.State.Market.Offered[t.Input]);
            own = Affordable(sim, b, t.Input, own, MarketRules.Floor(sim, t.Input));
            if (own > 0)
            {
                long value = MarketRules.Value(sim, t.Input, own, MarketRules.Floor(sim, t.Input));
                BusinessBooks.Draw(sim, b, value + PowerCost(sim, b, own));
                Store.Take(sim, t.Input, own);
                if (value > 0)
                {
                    sim.State.Ledger.Post(now, sim.Period, "ledger.soldToBusiness", reference, new Posting(Account.Cash, value), new Posting(Account.CropSales, -value));
                    b.Ledger.Post(now, sim.Period, "ledger.boughtFromFarm", reference, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                }
                Grind(sim, b, own, value);
                b.OwnToday += own;
                b.OwnSeason += own;
                left -= own;
            }

            // Then the market's, if allowed: at the local import price (wheat bought in is dearer than your own).
            if (b.BuyFromMarket && left > 0)
            {
                Fixed price = MarketRules.Ceiling(sim, t.Input);
                long bought = Affordable(sim, b, t.Input, left, price);
                if (bought > 0)
                {
                    long value = MarketRules.Value(sim, t.Input, bought, price);
                    BusinessBooks.Draw(sim, b, value + PowerCost(sim, b, bought));
                    b.Ledger.Post(now, sim.Period, "ledger.boughtAtMarket", reference, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                    Grind(sim, b, bought, value);
                    b.OwnSeason += bought;
                }
            }
            if (own > 0 && b.OwnToday == own) HrRules.FirstBatch(sim, b); // the day's first grinding of your wheat
            Sell(sim, b);
            BusinessBooks.Sweep(sim, b);
            if (area > 0 || own > 0) sim.Emit(new BusinessWorked(now, b.Id, area, own));
        }

        /// <summary>How much of <paramref name="grams"/> its cash and overdraft pay for at <paramref name="price"/>: past the limit, inputs stop (§3.10).</summary>
        private static long Affordable(Simulation sim, Business b, int good, long grams, Fixed price)
        {
            if (grams <= 0) return 0;
            long unit = MarketRules.Value(sim, good, 1000, price + sim.State.Market.PriceIndex * TypeOf(sim, b).PowerPerKg);
            if (unit <= 0) return grams;
            long spend = BusinessBooks.CanSpend(sim, b);
            long can = spend <= 0 ? 0 : (long)((System.Numerics.BigInteger)spend * 1000 / unit);
            return System.Math.Min(grams, can);
        }

        private static long PowerCost(Simulation sim, Business b, long grams) { return Money.Value(grams, TypeOf(sim, b).PowerPerKg * sim.State.Market.PriceIndex * CostFactor(sim, b), 1000); }

        private static void Power(Simulation sim, Business b, long grams)
        {
            long cost = PowerCost(sim, b, grams);
            BusinessBooks.Draw(sim, b, cost);
            if (cost > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.power", "business:" + b.Id, new Posting(Account.Power, cost), new Posting(Account.Cash, -cost));
        }

        /// <summary>Input into its outputs by the yields; the input's cost is shared by weight; power paid.</summary>
        private static void Grind(Simulation sim, Business b, long grams, long cost)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            long given = 0;
            for (int i = 0; i < t.Outputs.Count; i++)
            {
                BusinessOutput o = t.Outputs[i];
                long q = (o.Yield * Fixed.FromInt(grams)).RoundToLong();
                long c = i == t.Outputs.Count - 1 ? cost - given : (long)((System.Numerics.BigInteger)cost * q / grams);
                b.Stock[o.Good] += q;
                b.StockCost[o.Good] += c;
                given += c;
            }
            Power(sim, b, grams);
        }

        /// <summary>
        /// Sells what's made (§3.5c): bran to your feed room first if you keep cattle (it replaces cattle feed kg for kg, at the feed's
        /// price), then to the trader's truck at its morning price, inside what it buys a day and its load.
        /// </summary>
        private static void Sell(Simulation sim, Business b)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            foreach (BusinessOutput o in t.Outputs)
            {
                int g = o.Good;
                if (b.Stock[g] <= 0) continue;
                var cattle = sim.Config.Game.Livestock;
                if (g == sim.Config.Game.Market.IndexOf("bran") && cattle != null && Livestock.LivestockRules.Shed(sim) != null && sim.State.Livestock.Animals.Count > 0)
                {
                    int feed = cattle.Feed;
                    Fixed price = MarketRules.InputPrice(sim, feed);
                    long q = System.Math.Min(b.Stock[g], Store.Fits(sim, feed));
                    long value = MarketRules.Value(sim, feed, q, price);
                    if (q > 0 && sim.State.Ledger.CanAfford(value))
                    {
                        sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtFromBusiness", "business:" + b.Id,
                            new Posting(Account.InputsInStore, value), new Posting(Account.Cash, -value));
                        sim.State.Market.Stock[feed] += q;
                        sim.State.Market.StockCost[feed] += value;
                        Livestock.LivestockSystem.Refilled(sim, sim.Config.Game.Market.Goods[feed].Place);
                        Sold(sim, b, g, q, value);
                    }
                }
                ToYourBusinesses(sim, b, g);
                if (b.Stock[g] <= 0 || !MarketRules.TraderBuys(sim)) continue;
                long sell = System.Math.Min(b.Stock[g], MarketRules.TraderCapacityLeft(sim, g));
                if (sell <= 0) continue;
                long amount = SalePrice(sim, b, MarketRules.Value(sim, g, sell, MarketRules.Floor(sim, g)));
                Sold(sim, b, g, sell, amount);
                Store.CountTraderPurchase(sim, g, sell);
            }
        }

        // ---- recipes (§3.9, §3.9b, §3.9c) ----

        /// <summary>
        /// The hour's batches (§3.9c): each recipe in order makes toward its product's mix, up to the rate (grams of inputs a real
        /// hour), from its stock and, if allowed, inputs bought at the market's import price, as far as its cash and overdraft pay;
        /// fuel is expensed, supplies go into the product's cost. Returns the grams of inputs used.
        /// </summary>
        private static long Make(Simulation sim, Business b, BusinessTypeDef t)
        {
            long capacity = Capacity(sim, b), used = 0;
            foreach (RecipeDef r in t.Recipes)
            {
                long want = b.Mix[r.Output] - b.MadeToday[r.Output];
                if (want <= 0 || capacity <= 0) continue;
                long q = System.Math.Min(want, (long)((System.Numerics.BigInteger)capacity * r.OutputGrams / r.InputGrams));
                foreach (KeyValuePair<int, long> kv in r.Inputs)
                    if (!b.BuyFromMarket || t.TakesMilk || t.Makes(kv.Key)) q = System.Math.Min(q, (long)((System.Numerics.BigInteger)b.Stock[kv.Key] * r.OutputGrams / kv.Value));
                for (int pass = 0; pass < 2 && q > 0; pass++) // as far as the money goes (a smaller batch buys no more)
                {
                    long cost = BatchCost(sim, b, r, q), spend = BusinessBooks.CanSpend(sim, b);
                    if (cost > spend) q = cost <= 0 ? 0 : (long)((System.Numerics.BigInteger)q * System.Math.Max(0, spend) / cost);
                }
                if (q <= 0) continue;
                used += Batch(sim, b, r, q);
                capacity -= RecipeInputs(r, q);
            }
            return used;
        }

        private static long RecipeInputs(RecipeDef r, long q)
        {
            long n = 0;
            foreach (KeyValuePair<int, long> kv in r.Inputs) n += Need(kv.Value, q, r.OutputGrams);
            return n;
        }

        /// <summary>What <paramref name="q"/> grams of the product cost in cash now: inputs to buy, fuel and supplies.</summary>
        private static long BatchCost(Simulation sim, Business b, RecipeDef r, long q)
        {
            long cost = Money.Value(q, (r.Fuel + r.Supplies) * sim.State.Market.PriceIndex * CostFactor(sim, b), r.OutputGrams);
            foreach (KeyValuePair<int, long> kv in r.Inputs)
            {
                long buy = Need(kv.Value, q, r.OutputGrams) - b.Stock[kv.Key];
                if (buy > 0) cost += MarketRules.Value(sim, kv.Key, buy, MarketRules.Ceiling(sim, kv.Key));
            }
            return cost;
        }

        private static long Batch(Simulation sim, Business b, RecipeDef r, long q)
        {
            long now = sim.State.Clock.SimTime, inputCost = 0, used = 0;
            string reference = "business:" + b.Id;
            foreach (KeyValuePair<int, long> kv in r.Inputs)
            {
                int g = kv.Key;
                long need = Need(kv.Value, q, r.OutputGrams), buy = need - b.Stock[g];
                if (buy > 0)
                {
                    long value = MarketRules.Value(sim, g, buy, MarketRules.Ceiling(sim, g));
                    BusinessBooks.Draw(sim, b, value);
                    if (value > 0) b.Ledger.Post(now, sim.Period, "ledger.boughtAtMarket", reference, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                    b.Stock[g] += buy;
                    b.StockCost[g] += value;
                }
                long part = (long)((System.Numerics.BigInteger)b.StockCost[g] * need / b.Stock[g]);
                b.Stock[g] -= need;
                b.StockCost[g] -= part;
                inputCost += part;
                used += need;
                if (b.MadeToday[g] > 0) b.MadeToday[g] = System.Math.Max(0, b.MadeToday[g] - need); // pav made into rusk is made again for the shops
            }
            Fixed index = sim.State.Market.PriceIndex * CostFactor(sim, b);
            long fuel = Money.Value(q, r.Fuel * index, r.OutputGrams), supplies = Money.Value(q, r.Supplies * index, r.OutputGrams);
            BusinessBooks.Draw(sim, b, fuel + supplies);
            if (fuel > 0) b.Ledger.Post(now, sim.Period, "ledger.fuel", reference, new Posting(Account.Power, fuel), new Posting(Account.Cash, -fuel));
            if (supplies > 0) b.Ledger.Post(now, sim.Period, "ledger.supplies", reference, new Posting(Account.BusinessStock, supplies), new Posting(Account.Cash, -supplies));
            b.Stock[r.Output] += q;
            b.StockCost[r.Output] += inputCost + supplies;
            b.MadeToday[r.Output] += q;
            b.OwnSeason += q;
            return used;
        }

        /// <summary>
        /// The morning round (§3.9c), at the truck's hour: the village shops collect each product up to what they buy, at the
        /// trader's price that morning; then the trader takes what keeps; then what's left spoils by its rate (inputs keep till used;
        /// the dairy's milk till the next milking); the day's counts start again.
        /// </summary>
        private static void Morning(Simulation sim, Business b)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            if (t.ByRecipe)
            {
                foreach (int p in t.Products)
                {
                    long q = System.Math.Min(b.Stock[p], ShopGramsPerDay(sim, t, p));
                    if (q > 0) Sold(sim, b, p, q, SalePrice(sim, b, MarketRules.Value(sim, p, q, MarketRules.Floor(sim, p))));
                }
                foreach (int p in t.Products)
                {
                    if (b.Stock[p] <= 0 || !MarketRules.TraderBuys(sim)) continue;
                    long q = System.Math.Min(b.Stock[p], MarketRules.TraderCapacityLeft(sim, p));
                    if (q <= 0) continue;
                    Sold(sim, b, p, q, SalePrice(sim, b, MarketRules.Value(sim, p, q, MarketRules.Floor(sim, p))));
                    Store.CountTraderPurchase(sim, p, q);
                }
                System.Array.Clear(b.MadeToday, 0, b.MadeToday.Length);
            }
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
            {
                if (b.Stock[g] <= 0 || (t.ByRecipe ? !t.Makes(g) : g == t.Input)) continue;
                Spoil(sim, b, g, goods[g].SpoilagePerDay.MulRatio(b.Stock[g], 1).RoundToLong());
            }
            BusinessBooks.Sweep(sim, b);
        }

        /// <summary>Stock lost to spoilage, at its cost, as part of the cost of goods (a real business's waste).</summary>
        private static void Spoil(Simulation sim, Business b, int good, long grams)
        {
            grams = System.Math.Min(grams, b.Stock[good]);
            if (grams <= 0) return;
            long cost = (long)((System.Numerics.BigInteger)b.StockCost[good] * grams / b.Stock[good]);
            b.Stock[good] -= grams;
            b.StockCost[good] -= cost;
            if (cost > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.spoiled", "business:" + b.Id, new Posting(Account.CostOfGoodsSold, cost), new Posting(Account.BusinessStock, -cost));
            sim.Emit(new GoodsSpoiled(sim.State.Clock.SimTime, sim.Config.Game.Market.Goods[good].Id, grams, SpoilSource.Business, b.Id));
        }

        /// <summary>
        /// Your mill's atta goes to your bakeries first (§3.9c), up to what their mix still needs today less what they hold, as an
        /// internal sale at the trader's price (the mill's sales, the bakery's stock).
        /// </summary>
        private static void ToYourBusinesses(Simulation sim, Business from, int good)
        {
            foreach (Business to in sim.State.Businesses.Businesses)
            {
                if (to == from || to.Status != BusinessStatus.Open || from.Stock[good] <= 0) continue;
                BusinessTypeDef t = TypeOf(sim, to);
                long need = -to.Stock[good];
                foreach (RecipeDef r in t.Recipes)
                    foreach (KeyValuePair<int, long> kv in r.Inputs)
                        if (kv.Key == good) need += Need(kv.Value, System.Math.Max(0, to.Mix[r.Output] - to.MadeToday[r.Output]), r.OutputGrams);
                long q = System.Math.Min(from.Stock[good], need);
                if (q <= 0) continue;
                long value = MarketRules.Value(sim, good, q, MarketRules.Floor(sim, good));
                q = System.Math.Min(q, BusinessBooks.CanSpend(sim, to) >= value ? q : 0);
                if (q <= 0) continue;
                BusinessBooks.Draw(sim, to, value);
                to.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtFromBusiness", "business:" + to.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                to.Stock[good] += q;
                to.StockCost[good] += value;
                Sold(sim, from, good, q, value);
            }
        }

        /// <summary>
        /// The dairy keeps your milk (§3.9b) at the 6:00 milking, before the co-op: yesterday's milk not made up spoils, then each
        /// kind in order up to its milk a day, at the co-op's price (your farm's milk sales either way; the dairy pays from its cash).
        /// Returns the millilitres taken of each kind.
        /// </summary>
        internal static long[] MilkIn(Simulation sim, long[] available)
        {
            var taken = new long[available.Length];
            if (sim.State.Businesses == null) return taken;
            var kinds = sim.Config.Game.Livestock.Animals;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status != BusinessStatus.Open || !TypeOf(sim, b).TakesMilk || !Staffed(sim, b)) continue; // nobody to work it today: the co-op takes it
                long room = b.OwnPerDay;
                for (int k = 0; k < kinds.Count; k++)
                {
                    int good = sim.Config.Game.Market.IndexOf("milk_" + kinds[k].Id);
                    if (good < 0) continue;
                    Spoil(sim, b, good, b.Stock[good]);
                    Fixed price = Livestock.LivestockRules.MilkPrice(sim, k);
                    long q = System.Math.Min(room, available[k] - taken[k]);
                    long value = Money.Value(q, price, 1000);
                    if (q > 0 && value > BusinessBooks.CanSpend(sim, b)) q = Money.QuantityFor(System.Math.Max(0, BusinessBooks.CanSpend(sim, b)), price, 1000); // as far as its cash goes; the co-op takes the rest
                    if (q <= 0) continue;
                    value = Money.Value(q, price, 1000);
                    BusinessBooks.Draw(sim, b, value);
                    b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtFromFarm", "business:" + b.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                    b.Stock[good] += q;
                    b.StockCost[good] += value;
                    taken[k] += q;
                    room -= q;
                }
                if (b.BuyFromMarket && room > 0) Neighbours(sim, b, room);
            }
            return taken;
        }

        /// <summary>Neighbours' milk to fill the day's amount (§3.9b), at the co-op's price and the premium, as far as its cash goes.</summary>
        private static void Neighbours(Simulation sim, Business b, long ml)
        {
            BusinessTypeDef t = TypeOf(sim, b);
            int good = sim.Config.Game.Market.IndexOf("milk_" + sim.Config.Game.Livestock.Animals[NeighbourMilkKind(sim)].Id);
            Fixed price = NeighbourMilkPrice(sim, t);
            long spend = BusinessBooks.CanSpend(sim, b);
            long value = Money.Value(ml, price, 1000);
            if (value > spend) ml = Money.QuantityFor(System.Math.Max(0, spend), price, 1000);
            value = Money.Value(ml, price, 1000);
            if (ml <= 0 || value <= 0) return;
            BusinessBooks.Draw(sim, b, value);
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.boughtFromNeighbours", "business:" + b.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
            b.Stock[good] += ml;
            b.StockCost[good] += value;
        }

        internal static void Sold(Simulation sim, Business b, int good, long grams, long amount)
        {
            long cost = (long)((System.Numerics.BigInteger)b.StockCost[good] * grams / b.Stock[good]);
            b.Stock[good] -= grams;
            b.StockCost[good] -= cost;
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            if (amount > 0) b.Ledger.Post(now, sim.Period, "ledger.sales", reference, new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
            if (cost > 0) b.Ledger.Post(now, sim.Period, "ledger.costOfGoods", reference, new Posting(Account.CostOfGoodsSold, cost), new Posting(Account.BusinessStock, -cost));
        }

        // ---- the founder's hours (§3.6) ----

        /// <summary>Hours a day the founder spends at businesses they run themselves: their open hours, up to the founder's day (× nutrition).</summary>
        public static Fixed FounderHours(Simulation sim)
        {
            if (sim.State.Businesses == null) return Fixed.Zero;
            Fixed day = sim.State.Farm.Founder.TodayHours, hours = Fixed.Zero;
            foreach (Business b in sim.State.Businesses.Businesses)
                if (b.Status == BusinessStatus.Open && b.Worker == WorkerKind.Founder) hours += Fixed.FromInt(b.CloseHour - b.OpenHour);
            return (hours < day ? hours : day) * Farm.FounderRules.Nutrition(sim);
        }

        // ---- the season close (§3.2, §3.11) ----

        /// <summary>
        /// Each business closes its books with the founder's (in id order, spec 016 §3.2, §3.11), before the loans are settled: repairs
        /// (on its overdraft if need be; past the limit they wait), depreciation and the overdraft's interest for the season that ended,
        /// then its report and the close to retained earnings.
        /// </summary>
        internal static void CloseBooks(Simulation sim, long closing)
        {
            if (sim.State.Businesses == null) return;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status == BusinessStatus.Closed) continue;
                BusinessTypeDef t = TypeOf(sim, b);
                long now = sim.State.Clock.SimTime;
                string reference = "business:" + b.Id;
                if (b.Status == BusinessStatus.Open)
                {
                    long repairs = Money.MulRate(MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare);
                    BusinessBooks.Draw(sim, b, repairs, closing);
                    repairs = System.Math.Min(repairs, b.Ledger.Cash);
                    if (repairs > 0) b.Ledger.Post(now, closing, "ledger.repairs", reference, new Posting(Account.Repairs, repairs), new Posting(Account.Cash, -repairs));
                    long building = b.Ledger.Balance(Account.Buildings), equipment = b.Ledger.Balance(Account.Equipment);
                    long depreciation = System.Math.Min(building / t.BuildingLifeSeasons + equipment / t.EquipmentLifeSeasons,
                        building + equipment + b.Ledger.Balance(Account.AccumulatedDepreciation));
                    if (depreciation > 0)
                        b.Ledger.Post(now, closing, "ledger.depreciation", reference, new Posting(Account.Depreciation, depreciation), new Posting(Account.AccumulatedDepreciation, -depreciation));
                }
                long interest = Money.MulRate(BusinessBooks.Overdrawn(b), Defs(sim).OverdraftRate);
                if (interest > 0) b.Ledger.Post(now, closing, "ledger.overdraftInterest", reference, new Posting(Account.Interest, interest), new Posting(Account.Overdraft, -interest));
                BusinessBooks.CloseSeason(sim, b, closing);
            }
        }

        /// <summary>
        /// After the loans are settled: each open business pays its worker the new season's wage upfront, on its overdraft if need be;
        /// past the limit the wage goes unpaid and the worker leaves (§3.10). Cash left pays the overdraft off.
        /// </summary>
        internal static void PayUpfront(Simulation sim, long closing)
        {
            if (sim.State.Businesses == null) return;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status == BusinessStatus.Closed) continue;
                Worker w = sim.State.People?.WorkerAt(b.Id);
                if (b.Status == BusinessStatus.Open && w != null && HrRules.Leaves(sim, w))
                {
                    LoseWorker(sim, b); // took an offer from elsewhere (spec 015 §3.7): no severance
                    sim.Emit(new WorkerLeft(sim.State.Clock.SimTime, b.Id, w.Id, 0));
                }
                else if (b.Status == BusinessStatus.Open && w != null)
                {
                    long wage = w.Wage;
                    if (BusinessBooks.CanSpend(sim, b) >= wage - w.Hr.Advance)
                    {
                        HrRules.RepayAdvance(sim, w, b.Ledger, "business:" + b.Id);
                        BusinessBooks.Draw(sim, b, wage);
                        PayWage(sim, b, wage);
                        HiringRules.Renewed(sim, w);
                    }
                    else
                    {
                        LoseWorker(sim, b);
                        sim.Emit(new WagesMissed(sim.State.Clock.SimTime, b.Id, wage));
                        HrRules.Repute(sim, sim.Config.Game.People.Reputation.Lapse);
                    }
                }
                BusinessBooks.Sweep(sim, b);
                b.Ledger.DropEntriesBefore(closing - sim.Config.Game.Ledger.RetainSeasons + 1);
            }
        }
    }
}
