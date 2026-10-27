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
        public static Projection Projected(Simulation sim, BusinessTypeDef t, bool runYourself, long ownPerDay, Fixed fee, long loan = 0)
        {
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

        /// <summary>The town's best-skilled adult without a town job (spec 016 §3.6), lowest id on a tie; null if none.</summary>
        public static Citizen BestWorker(Simulation sim)
        {
            Citizen best = null;
            if (sim.State.Citizens == null) return null;
            foreach (Citizen c in sim.State.Citizens.Citizens)
                if (HouseholdRules.WorksOutside(sim, c) && (best == null || c.Skill > best.Skill)) best = c;
            return best;
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
            BusinessPlan plan = Project(sim, t, runYourself, t.OwnPerDay, t.Fee, loan);
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
                OpenHour = t.OpenHour, CloseHour = t.CloseHour, Fee = t.Fee, OwnPerDay = t.OwnPerDay, Worker = runYourself ? WorkerKind.Founder : WorkerKind.None,
                Ledger = new Ledger(), Stock = new long[goods], StockCost = new long[goods], Plan = plan, Day = now / Day,
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

        /// <summary>Built (§3.3 step 7): the worker starts and is paid the rest of the season from the business's cash; it opens.</summary>
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
                if (b.Worker == WorkerKind.None) Restaff(sim, b);
                sim.Emit(new BusinessOpened(sim.State.Clock.SimTime, b.Id, b.Worker, b.WorkerCitizen));
            }
        }

        /// <summary>The town's best-skilled adult without a town job takes the job; else a daily worker from a neighbouring village.</summary>
        internal static void Staff(Simulation sim, Business b)
        {
            Citizen c = BestWorker(sim);
            if (c == null)
            {
                b.Worker = WorkerKind.Neighbour;
                return;
            }
            b.Worker = WorkerKind.Citizen;
            b.WorkerCitizen = c.Id;
            c.Job = CitizenJob.Business;
            c.Employer = b.Id;
            c.Story.Add(new StoryLine(StoryKind.Hired, sim.State.Clock.SimTime, 0));
            HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId)); // a wage now (spec 014 §3.5)
        }

        /// <summary>A worker who has gone, or turned elder (and retires), is replaced at once by the next best, or a neighbour.</summary>
        private static void KeepStaffed(Simulation sim, Business b)
        {
            if (b.Worker != WorkerKind.Citizen) return;
            Citizen c = sim.State.Citizens.Citizen(b.WorkerCitizen);
            bool working = c != null && c.Living && c.Job == CitizenJob.Business && c.Employer == b.Id;
            if (working && !HouseholdRules.IsElder(sim, c)) return;
            if (working)
            {
                c.Job = CitizenJob.Retired;
                c.Employer = 0;
                c.Story.Add(new StoryLine(StoryKind.Retired, sim.State.Clock.SimTime, 0));
            }
            b.Worker = WorkerKind.None;
            b.WorkerCitizen = 0;
            Staff(sim, b);
        }

        /// <summary>
        /// With no worker, one is taken on when the rest of the season's wage can be paid upfront, on the overdraft if need be (§3.6,
        /// §3.10): at opening, and when you put cash in after a worker left unpaid.
        /// </summary>
        internal static void Restaff(Simulation sim, Business b)
        {
            if (b.Status != BusinessStatus.Open || b.Worker != WorkerKind.None) return;
            long wage = People.PeopleRules.RestOfSeason(sim, Wage(sim, TypeOf(sim, b)));
            if (!BusinessBooks.Draw(sim, b, wage)) return;
            Staff(sim, b);
            PayWage(sim, b, wage);
        }

        /// <summary>The worker leaves: a townsperson goes back to outside work (their household's budget follows).</summary>
        internal static void LoseWorker(Simulation sim, Business b)
        {
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

        private static void PayWage(Simulation sim, Business b, long wage)
        {
            if (wage <= 0) return;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.wages", "business:" + b.Id, new Posting(Account.HiredLabour, wage), new Posting(Account.Cash, -wage));
        }

        // ---- the hour (§3.4, §3.5) ----

        /// <summary>Hourly pass (§3.11): each open, staffed business works the hour that just ended, in id order.</summary>
        internal static void Hourly(Simulation sim)
        {
            foreach (Business b in sim.State.Businesses.Businesses)
                if (b.Status == BusinessStatus.Open) Work(sim, b);
        }

        /// <summary>Whether the business works in the hour of day <paramref name="hour"/>: open then and staffed.</summary>
        public static bool OpenAt(Business b, int hour) { return b.Worker != WorkerKind.None && hour >= b.OpenHour && hour < b.CloseHour; }

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
            if (!OpenAt(b, hour)) return;
            long capacity = t.GramsPerHour, now = sim.State.Clock.SimTime;
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

        private static long PowerCost(Simulation sim, Business b, long grams) { return Money.Value(grams, TypeOf(sim, b).PowerPerKg * sim.State.Market.PriceIndex, 1000); }

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
                if (b.Stock[g] <= 0 || !MarketRules.TraderBuys(sim)) continue;
                long sell = System.Math.Min(b.Stock[g], MarketRules.TraderCapacityLeft(sim, g));
                if (sell <= 0) continue;
                long amount = MarketRules.Value(sim, g, sell, MarketRules.Floor(sim, g));
                Sold(sim, b, g, sell, amount);
                Store.CountTraderPurchase(sim, g, sell);
            }
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
                if (b.Status == BusinessStatus.Open && b.Worker != WorkerKind.Founder && b.Worker != WorkerKind.None)
                {
                    long wage = Wage(sim, TypeOf(sim, b));
                    if (BusinessBooks.Draw(sim, b, wage)) PayWage(sim, b, wage);
                    else
                    {
                        LoseWorker(sim, b);
                        sim.Emit(new WagesMissed(sim.State.Clock.SimTime, b.Id, wage));
                    }
                }
                BusinessBooks.Sweep(sim, b);
                b.Ledger.DropEntriesBefore(closing - sim.Config.Game.Ledger.RetainSeasons + 1);
            }
        }
    }
}
