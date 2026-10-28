using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.People
{
    /// <summary>What a farm hand and a business's worker share for their output (spec 015 §3.4, §3.5).</summary>
    public interface IStaff
    {
        long Id { get; }
        Fixed Skill { get; }
        int Trait1 { get; }
        int Trait2 { get; }
        long Wage { get; }
        int Fairness { get; }
        Fixed Fatigue { get; }
        long CourseUntil { get; }
    }

    /// <summary>
    /// A worker's output (spec 015 §3.4, §3.5): skill factor × motivation × (1 − fatigue); long days and their overtime; courses.
    /// Neutral by default: a fair hire in a content home, at normal hours, works exactly as spec 006's hand.
    /// </summary>
    public static class WorkRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        /// <summary>The skill factor: productivityBase + productivityPerSkill × skill ÷ 100, times each trait's hours factor.</summary>
        public static Fixed SkillFactor(Simulation sim, IStaff w)
        {
            PeopleDefs p = sim.Config.Game.People;
            Fixed f = p.Hand.ProductivityBase + p.Hand.ProductivityPerSkill * w.Skill.MulRatio(1, 100);
            return f * p.Traits[w.Trait1].HoursFactor * p.Traits[w.Trait2].HoursFactor;
        }

        /// <summary>
        /// A business worker's share of its rate: their output over a skill-50 worker's (the market wage's skill), so the rate, the plan
        /// and the targets (ADR 0073, 0079, 0080) are a typical hire's; a skilled one works faster, a beginner slower.
        /// </summary>
        public static Fixed RelativeOutput(Simulation sim, IStaff w)
        {
            HandDef h = sim.Config.Game.People.Hand;
            return Output(sim, w) / (h.ProductivityBase + h.ProductivityPerSkill * Fixed.FromInt(50).MulRatio(1, 100));
        }

        /// <summary>Fair pay: +fairAbove hired above the asked wage, −fairBelow hired below it until a renewal pays it, else 0.</summary>
        public static Fixed Fairness(Simulation sim, IStaff w)
        {
            WorkDef d = sim.Config.Game.People.Work;
            return w.Fairness > 0 ? d.FairAbove : w.Fairness < 0 ? -d.FairBelow : Fixed.Zero;
        }

        /// <summary>A happy home: −homePerPoint for each point their household's happiness is below homeBelow (a commuter's isn't ours to see).</summary>
        public static Fixed Home(Simulation sim, IStaff w)
        {
            WorkDef d = sim.Config.Game.People.Work;
            Household x = HomeOf(sim, w);
            if (x == null || x.Happiness >= Fixed.FromInt(d.HomeBelow)) return Fixed.Zero;
            return -(d.HomePerPoint * (Fixed.FromInt(d.HomeBelow) - x.Happiness));
        }

        /// <summary>A sociable colleague's cheer: the other hands on your fields, or the others at the same business.</summary>
        public static Fixed Colleagues(Simulation sim, IStaff w)
        {
            PeopleState s = sim.State.People;
            var traits = sim.Config.Game.People.Traits;
            Fixed sum = Fixed.Zero;
            if (w is Hand)
            {
                foreach (Hand o in s.Hands)
                    if (o != w && o.Contract == ContractState.Active) sum += traits[o.Trait1].ColleagueMotivation + traits[o.Trait2].ColleagueMotivation;
            }
            else
            {
                long place = ((Worker)w).Place;
                foreach (Worker o in s.Workers)
                    if (o != w && o.Place == place) sum += traits[o.Trait1].ColleagueMotivation + traits[o.Trait2].ColleagueMotivation;
            }
            return sum;
        }

        /// <summary>Motivation = 1 + fairness + home + colleagues, within motivationMin–motivationMax (spec 015 §3.4).</summary>
        public static Fixed Motivation(Simulation sim, IStaff w)
        {
            WorkDef d = sim.Config.Game.People.Work;
            Fixed m = Fixed.One + Fairness(sim, w) + Home(sim, w) + Colleagues(sim, w);
            return m < d.MotivationMin ? d.MotivationMin : m > d.MotivationMax ? d.MotivationMax : m;
        }

        /// <summary>Motivation × (1 − fatigue): exactly 1 for a fair hire in a content home who isn't tired.</summary>
        public static Fixed Condition(Simulation sim, IStaff w) { return Motivation(sim, w) * (Fixed.One - w.Fatigue); }

        /// <summary>Output = skill factor × motivation × (1 − fatigue) (spec 015 §3.4); 0 while away on a course.</summary>
        public static Fixed Output(Simulation sim, IStaff w)
        {
            return w.CourseUntil != 0 ? Fixed.Zero : SkillFactor(sim, w) * Condition(sim, w);
        }

        /// <summary>The household of a hand (in his hut or his family's house) or of a townsperson at a business; null for a commuter.</summary>
        public static Household HomeOf(Simulation sim, IStaff w)
        {
            CitizensState c = sim.State.Citizens;
            if (c == null) return null;
            Citizen who = w is Hand h ? c.OfHand(h.Id) : ((Worker)w).CitizenId == 0 ? null : c.Citizen(((Worker)w).CitizenId);
            return who == null ? null : c.Household(who.HouseholdId);
        }

        /// <summary>The business a worker works at, or null for a farm hand.</summary>
        private static Business.Business PlaceOf(Simulation sim, IStaff w) { return w is Worker k ? sim.State.Businesses.Find(k.Place) : null; }

        /// <summary>A hand's long day's overtime (spec 015 §3.4): longDayHours at overtimeFactor × their hourly wage, in whole coins.</summary>
        public static long OvertimeDay(Simulation sim, Hand h)
        {
            WorkDef d = sim.Config.Game.People.Work;
            return Money.RoundToCoin(Money.MulRate(h.Wage + h.HousingInKind, d.OvertimeFactor.MulRatio(d.LongDayHours, 1) / SeasonHours(sim, sim.Config.Game.People.Hand.HoursPerDay)));
        }

        /// <summary>
        /// An hour a business is open past the village's usual day: overtimeFactor × the worker's hourly wage (the season's wage over
        /// the usual day's hours), paid as the hour is worked, in whole coins.
        /// </summary>
        public static long OvertimeHour(Simulation sim, Worker w)
        {
            Definitions.BusinessTypeDef t = Business.BusinessRules.TypeOf(sim, sim.State.Businesses.Find(w.Place));
            return Money.RoundToCoin(Money.MulRate(w.Wage, sim.Config.Game.People.Work.OvertimeFactor / SeasonHours(sim, Fixed.FromInt(t.CloseHour - t.OpenHour))));
        }

        /// <summary>A normal season's working hours: hours a day × the season's days.</summary>
        private static Fixed SeasonHours(Simulation sim, Fixed perDay) { return perDay.MulRatio(sim.Clock.SecondsPerSeason / Day, 1); }

        /// <summary>A course's price today (spec 015 §3.5).</summary>
        public static long CourseCost(Simulation sim) { return Money.RoundToCoin(MarketRules.Indexed(sim, sim.Config.Game.People.Work.CourseCost)); }

        /// <summary>Pays <paramref name="amount"/> for a worker: a hand's from your cash, a business worker's from the business (on its overdraft if need be).</summary>
        internal static bool Pay(Simulation sim, IStaff w, long amount, string memo)
        {
            if (amount <= 0) return true;
            Business.Business b = PlaceOf(sim, w);
            if (b == null)
            {
                if (!sim.State.Ledger.CanAfford(amount)) return false;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, memo, "hand:" + w.Id, new Posting(Account.HiredLabour, amount), new Posting(Account.Cash, -amount));
                return true;
            }
            if (!Business.BusinessBooks.Draw(sim, b, amount)) return false;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, memo, "business:" + b.Id, new Posting(Account.HiredLabour, amount), new Posting(Account.Cash, -amount));
            return true;
        }

        /// <summary>Whether a worker can be paid <paramref name="amount"/> now (your cash, or the business's cash and overdraft).</summary>
        public static bool CanPay(Simulation sim, IStaff w, long amount)
        {
            Business.Business b = PlaceOf(sim, w);
            return b == null ? sim.State.Ledger.CanAfford(amount) : Business.BusinessBooks.CanSpend(sim, b) >= amount;
        }

        internal static void SetCourse(IStaff w, long until)
        {
            if (w is Hand h) h.CourseUntil = until;
            else ((Worker)w).CourseUntil = until;
        }

        /// <summary>Whether a business's worker works long days: its hours run past the village's usual day.</summary>
        public static bool LongDays(Simulation sim, Worker w)
        {
            Business.Business b = sim.State.Businesses.Find(w.Place);
            return b.Status == Business.BusinessStatus.Open && Business.BusinessRules.LongHours(sim, b) > 0;
        }

        /// <summary>
        /// The daily pass (spec 015 §3.10): fatigue moves for the day that ended (+fatigueUp after a long day worked, −fatigueDown
        /// otherwise, within 0–fatigueMax); then a hand's overtime for tomorrow is paid upfront, or their long days stop when it can't be.
        /// </summary>
        internal static void Daily(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (!s.Hands.Exists(h => h.Fatigue != Fixed.Zero || h.LongDays) && !s.Workers.Exists(w => w.Fatigue != Fixed.Zero || LongDays(sim, w))) return;
            FarmSystem.Accrue(sim);
            foreach (Hand h in s.Hands)
            {
                h.Fatigue = Tire(sim, h.Fatigue, h.LongDays && h.Contract == ContractState.Active && h.CourseUntil == 0);
                if (h.LongDays && (h.Contract != ContractState.Active || (h.CourseUntil == 0 && !Pay(sim, h, OvertimeDay(sim, h), "ledger.overtime")))) // away on a course: no long day to pay
                {
                    h.LongDays = false;
                    sim.Emit(new LongDaysStopped(sim.State.Clock.SimTime, 0, h.Id));
                }
            }
            foreach (Worker w in s.Workers) w.Fatigue = Tire(sim, w.Fatigue, LongDays(sim, w) && w.CourseUntil == 0);
            FarmSystem.Refresh(sim);
        }

        private static Fixed Tire(Simulation sim, Fixed fatigue, bool longDay)
        {
            WorkDef d = sim.Config.Game.People.Work;
            Fixed f = longDay ? fatigue + d.FatigueUp : fatigue - d.FatigueDown;
            return f < Fixed.Zero ? Fixed.Zero : f > d.FatigueMax ? d.FatigueMax : f;
        }

        /// <summary>Hourly, live or away: a course that has ended adds courseGain to the worker's role skill (at most 100).</summary>
        internal static void Courses(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            Fixed gain = Fixed.FromInt(sim.Config.Game.People.Work.CourseGain), max = Fixed.FromInt(100);
            foreach (Hand h in s.Hands)
            {
                if (h.CourseUntil == 0 || t < h.CourseUntil) continue;
                FarmSystem.Accrue(sim);
                h.Skill = h.Skill + gain > max ? max : h.Skill + gain;
                h.CourseUntil = 0;
                FarmSystem.Refresh(sim);
                sim.Emit(new CourseFinished(t, 0, h.Id, h.Skill));
            }
            foreach (Worker w in s.Workers)
            {
                if (w.CourseUntil == 0 || t < w.CourseUntil) continue;
                w.Skill = w.Skill + gain > max ? max : w.Skill + gain;
                w.CourseUntil = 0;
                sim.Emit(new CourseFinished(t, w.Place, w.Id, w.Skill));
            }
        }

        /// <summary>The farm hand (place 0) or a business's worker (place = the business's id), or null.</summary>
        public static IStaff Find(Simulation sim, long place, long id)
        {
            PeopleState s = sim.State.People;
            if (s == null) return null;
            if (place == 0)
            {
                Hand h = s.Hand(id);
                return h != null && h.Contract == ContractState.Active ? h : null;
            }
            Worker w = s.WorkerAt(place);
            return w != null && w.Id == id ? w : null;
        }
    }
}
