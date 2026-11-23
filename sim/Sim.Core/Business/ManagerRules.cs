using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.Core.Business
{
    /// <summary>
    /// Managers (spec 028 §3.2, ADR 0157): a worker record that runs a business and works it himself (Sprint 25 balance, ADR 0164): he is its one worker,
    /// nobody else is hired. He sets what his style says each morning (price or fee, quality, stock, inputs ahead), mis-sets some days by his stars,
    /// is paid a base wage upfront and a share of the profit at the close, and reports against the target you set.
    /// </summary>
    public static class ManagerRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        public static ManagersDef Defs(Simulation sim) { return sim.Config.Game.Managers; }

        /// <summary>Managers are on from the Township, with the content and the hiring board.</summary>
        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.People != null && sim.State.Businesses != null && Town.TownshipRules.Reached(sim); }

        public static Worker Of(Simulation sim, long businessId) { return sim.State.People?.ManagerAt(businessId); }

        public static ManagerStyleDef StyleOf(Simulation sim, Worker m) { return Defs(sim).Styles[m.Mgr.Style]; }

        /// <summary>The style an applicant has (spec 028 §3.2): drawn from their id, so it never changes.</summary>
        public static int StyleOf(Simulation sim, Applicant a)
        {
            return (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Applicants, (ulong)a.Id, 7), (ulong)Defs(sim).Styles.Count);
        }

        public static int Stars(Simulation sim, Worker m) { return Defs(sim).Stars(m.Skill); }

        /// <summary>The share of days he mis-sets: his stars' chance, moved by his mood (glad of fair pay, sour after misses), within 0 and 1.</summary>
        public static Fixed MissChance(Simulation sim, Worker m)
        {
            Fixed c = Defs(sim).MissChance(Stars(sim, m)) - (WorkRules.Motivation(sim, m) - Fixed.One);
            return c < Fixed.Zero ? Fixed.Zero : c > Fixed.One ? Fixed.One : c;
        }

        /// <summary>Whether today is a day the business's manager mis-set (it makes or sells only part of what it would).</summary>
        public static bool MisSet(Simulation sim, Business b)
        {
            if (Defs(sim) == null) return false;
            Worker m = sim.State.People?.ManagerAt(b.Id);
            return m != null && m.Mgr.MisToday;
        }

        /// <summary>An amount of work, cut to its share on a mis-set day.</summary>
        public static long Scale(Simulation sim, Business b, long amount) { return MisSet(sim, b) ? Money.MulRate(amount, Defs(sim).MissOutput) : amount; }

        /// <summary>
        /// What a manager of this business asks a season at skill 50, year-0: the market wage the asked wage follows. It is a share of what a worker of the business's trade earns
        /// (he works it himself, and takes a tenth of the profit besides); with no business named, the farm hand's wage as the yardstick.
        /// </summary>
        public static long MarketWage(Simulation sim, long businessId)
        {
            Business b = businessId > 0 ? sim.State.Businesses?.Find(businessId) : null;
            long role = b == null ? sim.Config.Game.People.Hand.MarketWage : BusinessRules.TypeOf(sim, b).Wage;
            return Money.RoundToCoin(Money.MulRate(role, Defs(sim).WageOfRole));
        }

        // ---- hiring and letting go ----

        /// <summary>A manager starts (spec 028 §3.2, gap 6): the base wage for the rest of the season paid from the business's cash; you stop running it if you were.</summary>
        internal static void Hire(Simulation sim, Applicant a, long wage, int fairness, Business b)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            Citizen c = a.CitizenId == 0 ? null : sim.State.Citizens.Citizen(a.CitizenId);
            var w = new Worker
            {
                Id = s.NextWorkerId++, Place = b.Id, CitizenId = a.CitizenId, Name = a.Name, Skill = c == null ? Fixed.FromInt(a.Skill) : c.Crafting,
                Trait1 = a.Trait1, Trait2 = a.Trait2, Wage = wage, Fairness = fairness, HiredAt = t, Mgr = new ManagerRecord { Style = StyleOf(sim, a) },
            };
            s.Workers.Add(w);
            s.Applicants.Remove(a);
            if (a.CitizenId != 0) s.Applicants.RemoveAll(x => x.CitizenId == a.CitizenId);
            long prepay = PeopleRules.RestOfSeason(sim, wage);
            BusinessBooks.Draw(sim, b, prepay);
            BusinessRules.PayWage(sim, b, prepay);
            Farm.FarmSystem.Accrue(sim); // if you ran it, your hours go back to the farm: he works it now
            b.Worker = c == null ? WorkerKind.Neighbour : WorkerKind.Citizen;
            b.WorkerCitizen = a.CitizenId;
            s.Applicants.RemoveAll(x => x.Place == b.Id);
            s.Posts.RemoveAll(x => x.Place == b.Id);
            BenefitRules.ChargeNew(sim, b.Id);
            Farm.FarmSystem.Refresh(sim);
            if (c != null)
            {
                c.Job = CitizenJob.Business;
                c.Employer = b.Id;
                c.Story.Add(new StoryLine(StoryKind.Hired, t, 0));
                HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId));
            }
            sim.Emit(new ManagerHired(t, b.Id, w.Id, wage, prepay));
        }

        /// <summary>He goes (let go, left, or unpaid): his waiting events go with him, a townsperson is back to outside work with their skill kept.</summary>
        internal static void Remove(Simulation sim, Worker m)
        {
            PeopleState s = sim.State.People;
            HrRules.Gone(sim, m);
            s.Workers.Remove(m);
            Business b = sim.State.Businesses?.Find(m.Place);
            if (b != null && b.Worker != WorkerKind.Founder)
            {
                NightRules.Remove(sim, b, true); // no day worker, no night shift
                b.Worker = WorkerKind.None; // the seat is empty: you run it yourself, or hire a worker
                b.WorkerCitizen = 0;
            }
            Citizen c = m.CitizenId == 0 ? null : sim.State.Citizens?.Citizen(m.CitizenId);
            if (c == null) return;
            c.Crafting = m.Skill;
            if (c.Job == CitizenJob.Business && c.Employer == m.Place)
            {
                c.Job = CitizenJob.None;
                c.Employer = 0;
                HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId));
            }
        }

        /// <summary>Lets him go (spec 028 §3.2): he keeps the season's pay; after his first season, severance of 10% of the wage from the business's cash (as a worker's).</summary>
        internal static CommandResult Dismiss(Simulation sim, long businessId)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            Worker m = Of(sim, b.Id);
            if (m == null) return CommandResult.Reject(RejectReason.NoManager);
            long severance = 0;
            if (m.SeasonsWorked > 0)
            {
                severance = System.Math.Min(Money.MulRate(m.Wage, sim.Config.Game.People.Hand.SeveranceShare), BusinessBooks.CanSpend(sim, b));
                if (severance > 0 && BusinessBooks.Draw(sim, b, severance))
                    b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.severance", "business:" + b.Id, new Posting(Account.HiredLabour, severance), new Posting(Account.Cash, -severance));
            }
            HrRules.Repute(sim, m.SeasonsWorked == 0 ? sim.Config.Game.People.Reputation.ProbationLetGo : severance > 0 ? sim.Config.Game.People.Reputation.Severance : 0);
            Remove(sim, m);
            sim.Emit(new ManagerLeft(sim.State.Clock.SimTime, b.Id, m.Id, severance));
            return CommandResult.Ok;
        }

        internal static CommandResult SetTarget(Simulation sim, long businessId, ManagerTarget target)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            Worker m = Of(sim, b.Id);
            if (m == null) return CommandResult.Reject(RejectReason.NoManager);
            m.Mgr.Target = target;
            return CommandResult.Ok;
        }

        // ---- the morning ----

        /// <summary>
        /// At the truck's hour, before the stalls fill and the makers start (spec 028 §3.2): each open, managed business's manager sets what
        /// his style says, draws whether he mis-sets the day, and hires if nobody works it. Once a day, in business order.
        /// </summary>
        internal static void Morning(Simulation sim)
        {
            if (!On(sim)) return;
            long day = sim.State.Clock.SimTime / Day;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status != BusinessStatus.Open) continue;
                Worker m = Of(sim, b.Id);
                if (m == null || m.Mgr.SetDay == day) continue;
                m.Mgr.SetDay = day;
                m.Mgr.MisToday = false;
                if (WorkRules.Away(m)) continue; // sick or on a course: nothing is set today
                m.Mgr.Days++;
                if (Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Managers, (ulong)b.Id, (ulong)day), MissChance(sim, m)))
                {
                    m.Mgr.MisToday = true;
                    m.Mgr.MisDays++;
                }
                SetByStyle(sim, b, StyleOf(sim, m));
            }
        }

        /// <summary>What a style sets on each kind of business (gap 5): shops their price and quality, the mill its fee, the makers days of inputs ahead; a kiln nothing.</summary>
        private static void SetByStyle(Simulation sim, Business b, ManagerStyleDef style)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            if (t.IsShop)
            {
                ShopRules.SetLook(sim, b, style.PriceFactor, style.Quality);
                if (ShopRules.Stocked(t))
                {
                    long[] one = ShopRules.DefaultPutOut(sim, t, b.TileId);
                    for (int g = 0; g < one.Length; g++) if (one[g] > 0) b.PutOut[g] = one[g] * style.StockDays;
                }
            }
            else if (t.IsTuition) TuitionRules.SetNearest(sim, b, style.PriceFactor);
            else if (t.Input >= 0 && !t.ByRecipe && !t.IsKiln) b.Fee = t.Fee * style.PriceFactor;
            if (t.ByRecipe && !t.TakesMilk && b.BuyFromMarket) BuyAhead(sim, b, t, style.StockDays);
        }

        /// <summary>Tops up the bought inputs to <paramref name="days"/> days of the day's mix, at today's import price, as far as the business's cash goes.</summary>
        private static void BuyAhead(Simulation sim, Business b, BusinessTypeDef t, int days)
        {
            long now = sim.State.Clock.SimTime;
            foreach (int g in BusinessRules.BoughtInputs(t))
            {
                long perDay = 0;
                foreach (RecipeDef r in t.Recipes)
                    foreach (KeyValuePair<int, long> kv in r.Inputs)
                        if (kv.Key == g) perDay += (long)((System.Numerics.BigInteger)kv.Value * b.Mix[r.Output] + r.OutputGrams - 1) / r.OutputGrams;
                long want = perDay * days - b.Stock[g];
                if (want <= 0) continue;
                long value = MarketRules.Value(sim, g, want, BusinessRules.ImportPrice(sim, b, g));
                if (value <= 0 || value > BusinessBooks.CanSpend(sim, b)) continue; // never past the overdraft: he waits
                BusinessBooks.Draw(sim, b, value);
                b.Ledger.Post(now, sim.Period, "ledger.boughtAtMarket", "business:" + b.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
                b.Stock[g] += want;
                b.StockCost[g] += value;
            }
        }

        // ---- the close ----

        /// <summary>His share of the season's profit (spec 028 §3.2), paid from the business's cash before the tax: a tenth of what it made before his share, never when it lost.</summary>
        internal static void PayShare(Simulation sim, Business b, long closing)
        {
            Worker m = Of(sim, b.Id);
            if (m == null || b.Status != BusinessStatus.Open) { return; }
            long profit = BusinessBooks.NetProfit(BusinessBooks.Report(b, closing));
            long share = profit > 0 ? Money.MulRate(profit, Defs(sim).WageProfitShare) : 0;
            if (share > 0)
            {
                BusinessBooks.Draw(sim, b, share, closing);
                share = System.Math.Min(share, b.Ledger.Cash);
            }
            if (share > 0) b.Ledger.Post(sim.State.Clock.SimTime, closing, "ledger.managerShare", "business:" + b.Id, new Posting(Account.HiredLabour, share), new Posting(Account.Cash, -share));
            m.Mgr.ReportShare = share;
        }

        /// <summary>The goal for the season that ends (spec 028 §3.2): last season's profit or sales raised, or the plan's when it is new; in his first season a profit goal is what the business made less his pay, so he is judged on what he adds; never below 0.</summary>
        public static long Goal(Simulation sim, Business b, Worker m)
        {
            ManagersDef d = Defs(sim);
            BusinessReport last = b.Reports.Count == 0 ? null : b.Reports[b.Reports.Count - 1];
            if (m.Mgr.Target == ManagerTarget.Growth)
                return Money.MulRate(last == null ? b.Plan.Revenue : Sales(last), d.GrowthGoalRise);
            long profit = last == null ? b.Plan.Profit : BusinessBooks.NetProfit(last);
            if (m.SeasonsWorked == 0) return System.Math.Max(0, profit - m.Wage);
            return profit > 0 ? Money.MulRate(profit, d.ProfitGoalRise) : 0; // never a goal of a loss: at least to break even
        }

        /// <summary>What a season sold: the business's income.</summary>
        public static long Sales(BusinessReport r)
        {
            long sum = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
                if (Accounts.GroupOf(kv.Key) == AccountGroup.Income) sum -= kv.Value;
            return sum;
        }

        /// <summary>At the close, after the tax and before the books close: the season against the target (met or missed), two misses in a row souring his mood.</summary>
        internal static void Report(Simulation sim, Business b, long closing)
        {
            Worker m = Of(sim, b.Id);
            if (m == null || b.Status != BusinessStatus.Open) return;
            BusinessReport now = BusinessBooks.Report(b, closing);
            long goal = Goal(sim, b, m), result = m.Mgr.Target == ManagerTarget.Growth ? Sales(now) : BusinessBooks.NetProfit(now);
            ManagerRecord r = m.Mgr;
            r.ReportPeriod = closing + 1;
            r.ReportTarget = r.Target;
            r.ReportGoal = goal;
            r.ReportResult = result;
            r.ReportMet = result >= goal;
            r.ReportDays = r.Days;
            r.ReportMisDays = r.MisDays;
            r.Misses = r.ReportMet ? 0 : r.Misses + 1;
            if (r.Misses >= 2) HrRules.SetMood(sim, m, Defs(sim).MissedMood);
            r.Days = 0;
            r.MisDays = 0;
            sim.Emit(new ManagerReported(sim.State.Clock.SimTime, b.Id, m.Id, r.ReportMet, r.ReportShare));
        }

        /// <summary>After the loans are settled: his contract renews at the new season's wage, paid upfront, or he goes (took an offer, or the wage cannot be paid).</summary>
        internal static void Renew(Simulation sim, Business b)
        {
            Worker m = Of(sim, b.Id);
            if (m == null || b.Status != BusinessStatus.Open) return;
            if (HrRules.Leaves(sim, m))
            {
                Remove(sim, m);
                sim.Emit(new ManagerLeft(sim.State.Clock.SimTime, b.Id, m.Id, 0)); // took an offer from elsewhere: no severance
                return;
            }
            long wage = Town.PolicyRules.WageFloor(sim, m.Wage, MarketWage(sim, b.Id));
            m.Wage = wage;
            if (BusinessBooks.CanSpend(sim, b) >= wage - m.Hr.Advance)
            {
                HrRules.RepayAdvance(sim, m, b.Ledger, "business:" + b.Id);
                BusinessBooks.Draw(sim, b, wage);
                BusinessRules.PayWage(sim, b, wage);
                HiringRules.Renewed(sim, m);
                return;
            }
            Remove(sim, m);
            sim.Emit(new WagesMissed(sim.State.Clock.SimTime, b.Id, wage));
            HrRules.Repute(sim, sim.Config.Game.People.Reputation.Lapse);
        }
    }
}
