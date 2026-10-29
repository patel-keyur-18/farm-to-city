using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.People
{
    /// <summary>
    /// HR events and employer reputation (spec 015 §3.7, §3.8; ADR 0081). A contract renews at its current wage; at a close a worker
    /// paid below what they'd ask may ask for a raise, and a skilled one may get an offer from elsewhere. Sick leave, a family
    /// emergency and a bad batch come on any day. Each waits for the player, at most one a worker a season (stream HrEvents).
    /// </summary>
    public static class HrRules
    {
        private const long Day = ClockDef.SecondsPerDay;
        private const ulong RaiseDraw = 0, PoachDraw = 1, SickDraw = 2, EmergencyDraw = 3, BatchDraw = 4;

        public static long Season(Simulation sim) { return sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason; }

        // ---- reputation (§3.8) ----

        /// <summary>Moves employer reputation by <paramref name="delta"/>, within 0–100.</summary>
        internal static void Repute(Simulation sim, int delta)
        {
            PeopleState s = sim.State.People;
            if (s == null || delta == 0) return;
            int r = System.Math.Max(0, System.Math.Min(100, s.Reputation + delta));
            if (r == s.Reputation) return;
            s.Reputation = r;
            sim.Emit(new ReputationChanged(sim.State.Clock.SimTime, r, delta));
        }

        /// <summary>At 80 or above, applicants accept 10% likelier; at 20 or below, 10% less likely.</summary>
        public static Fixed AcceptShift(Simulation sim)
        {
            ReputationDef d = sim.Config.Game.People.Reputation;
            int r = sim.State.People.Reputation;
            return r >= d.High ? d.AcceptShift : r <= d.Low ? -d.AcceptShift : Fixed.Zero;
        }

        // ---- wages ----

        /// <summary>What they're paid a season: the cash wage, and for a hand the family house's rent taken as part of it.</summary>
        public static long Paid(IStaff w) { return w.Wage + (w is Hand h ? h.HousingInKind : 0); }

        /// <summary>What they'd ask today for their skill (an ambitious worker 10% more), whole coins.</summary>
        public static long Asked(Simulation sim, IStaff w)
        {
            var traits = sim.Config.Game.People.Traits;
            long asked = w is Worker k ? HiringRules.AskedWage(sim, k.Place, w.Skill, false) : PeopleRules.AskedWage(sim, w.Skill, false);
            Fixed factor = traits[w.Trait1].RenewalWageFactor * traits[w.Trait2].RenewalWageFactor;
            return factor == Fixed.One ? asked : Money.RoundToCoin(Money.MulRate(asked, factor));
        }

        public static bool KeepsWage(Simulation sim, IStaff w)
        {
            var traits = sim.Config.Game.People.Traits;
            return traits[w.Trait1].KeepsWage || traits[w.Trait2].KeepsWage;
        }

        /// <summary>What they'd ask is more than raiseGap (5%) above what they're paid: a season's skill growth alone isn't.</summary>
        public static bool Underpaid(Simulation sim, IStaff w)
        {
            return Asked(sim, w) > Money.MulRate(Paid(w), Fixed.One + sim.Config.Game.People.Hr.RaiseGap);
        }

        /// <summary>At a close: took an offer from elsewhere, or was refused a raise and is still underpaid (they look elsewhere at the contract's end).</summary>
        public static bool Leaves(Simulation sim, IStaff w) { return w.Hr.Leaving || (w.Hr.RaiseRefusals > 0 && Underpaid(sim, w)); }

        /// <summary>
        /// The wage memory after a renewal or a raise (spec 015 §3.3): paid what they ask clears a low hire's memory; paid less, a high
        /// hire's goes.
        /// </summary>
        private static void Remember(Simulation sim, IStaff w)
        {
            bool fair = !Underpaid(sim, w);
            int f = w.Fairness;
            if (fair && f < 0) f = 0;
            if (!fair && f > 0) f = 0;
            if (w is Hand h) h.Fairness = f;
            else ((Worker)w).Fairness = f;
        }

        /// <summary>An event's lasting mood plus a refused raise's −0.1 while they're still paid less than they ask (§3.4).</summary>
        public static Fixed Mood(Simulation sim, IStaff w)
        {
            Fixed m = w.Hr.MoodSeason >= Season(sim) ? w.Hr.Mood : Fixed.Zero;
            if (w.Hr.RaiseRefusals > 0 && Underpaid(sim, w)) m += sim.Config.Game.People.Hr.RaiseRefusedMood;
            return m;
        }

        private static void SetMood(Simulation sim, IStaff w, Fixed mood)
        {
            w.Hr.Mood = mood;
            w.Hr.MoodSeason = Season(sim);
        }

        // ---- the close (§3.10) ----

        /// <summary>A worker's renewal at a close: the wage memory, and +1 reputation when they're paid what they ask or more.</summary>
        internal static void Renewed(Simulation sim, IStaff w)
        {
            Remember(sim, w);
            if (!Underpaid(sim, w)) Repute(sim, sim.Config.Game.People.Reputation.FairRenewal);
        }

        /// <summary>After the season's skill growth: this season's raise request or offer from elsewhere (one at most).</summary>
        internal static void Draw(Simulation sim, IStaff w)
        {
            if (Waiting(sim, w)) return;
            HrDef d = sim.Config.Game.People.Hr;
            long day = sim.State.Clock.SimTime / Day;
            if (Underpaid(sim, w) && !KeepsWage(sim, w) && Rng.Chance(Roll(sim, w, day, RaiseDraw), d.RaiseChance))
            {
                Raise(sim, HrEventKind.RaiseRequest, w, Asked(sim, w));
                return;
            }
            if (w.Skill < Fixed.FromInt(d.PoachSkill)) return;
            Fixed chance = d.PoachChance;
            if (Underpaid(sim, w)) chance = chance.MulRatio(d.PoachUnderpaidFactor, 1);
            if (w.Hr.RaiseRefusals >= 2) chance = chance.MulRatio(d.PoachRefusedFactor, 1);
            long offer = Money.RoundToCoin(Money.MulRate(Asked(sim, w), Fixed.One + d.PoachMatch)); // 15% over what their skill is worth
            if (offer > Paid(w) && Rng.Chance(Roll(sim, w, day, PoachDraw), chance)) Raise(sim, HrEventKind.Poaching, w, offer);
        }

        /// <summary>The cash wage to pay at a renewal: the current one, less an advance being repaid (posted back from prepaid wages).</summary>
        internal static long RepayAdvance(Simulation sim, IStaff w, Ledger books, string reference)
        {
            long a = w.Hr.Advance;
            if (a <= 0) return 0;
            books.Post(sim.State.Clock.SimTime, sim.Period, "ledger.advanceRepaid", reference, new Posting(Account.Cash, a), new Posting(Account.PrepaidWages, -a));
            w.Hr.Advance = 0;
            return a;
        }

        // ---- the day ----

        /// <summary>
        /// Daily, live or away: sick leave (2% a season, ×2 when over 0.3 tired) and a family emergency (2%) for each worker at work,
        /// each a season's chance spread over its days. A bad batch comes with a business's first batch of the day (<see cref="FirstBatch"/>).
        /// </summary>
        internal static void Daily(Simulation sim)
        {
            PeopleState s = sim.State.People;
            HrDef d = sim.Config.Game.People.Hr;
            long day = sim.State.Clock.SimTime / Day, days = sim.Clock.SecondsPerSeason / Day;
            var staff = new List<IStaff>();
            foreach (Hand h in s.Hands) if (h.Contract == ContractState.Active) staff.Add(h);
            foreach (Worker w in s.Workers) staff.Add(w);
            foreach (IStaff w in staff)
            {
                if (w.CourseUntil != 0 || w.Hr.SickUntil != 0 || Waiting(sim, w)) continue;
                Fixed sick = d.SickChance.MulRatio(w.Fatigue > d.SickTiredAbove ? d.SickTiredFactor : 1, days);
                if (Rng.Chance(Roll(sim, w, day, SickDraw), sick))
                {
                    FallSick(sim, w);
                    continue;
                }
                if (Rng.Chance(Roll(sim, w, day, EmergencyDraw), d.EmergencyChance.MulRatio(1, days)))
                    Raise(sim, HrEventKind.FamilyEmergency, w, Money.RoundToCoin(Money.MulRate(w.Wage, d.AdvanceShare)));
            }
        }

        /// <summary>
        /// After a business's first batch of the day, before anything is sold: a bad batch (3% a season, spread over its days) when its
        /// hired worker isn't a perfectionist.
        /// </summary>
        internal static void FirstBatch(Simulation sim, Business.Business b)
        {
            Worker w = BusinessRules.HiredWorker(sim, b);
            var traits = sim.Config.Game.People.Traits;
            if (w == null || WorkRules.Away(w) || traits[w.Trait1].NoBadBatch || traits[w.Trait2].NoBadBatch || Waiting(sim, w)) return;
            long day = sim.State.Clock.SimTime / Day, days = sim.Clock.SecondsPerSeason / Day;
            if (Rng.Chance(Roll(sim, w, day, BatchDraw), sim.Config.Game.People.Hr.BadBatchChance.MulRatio(1, days))) BadBatch(sim, w);
        }

        private static ulong Roll(Simulation sim, IStaff w, long day, ulong kind)
        {
            ulong key = w is Worker ? (1UL << 32) + (ulong)w.Id : (ulong)w.Id;
            return Rng.Next(sim.State.Seed, RngStream.HrEvents, key, (ulong)day * 8 + kind);
        }

        /// <summary>Whether they've had their event this season, or one still waits.</summary>
        private static bool Waiting(Simulation sim, IStaff w)
        {
            return w.Hr.EventSeason == Season(sim) + 1 || EventOf(sim, Place(w), w.Id) != null;
        }

        public static long Place(IStaff w) { return w is Worker k ? k.Place : 0; }

        public static HrEvent EventOf(Simulation sim, long place, long workerId)
        {
            return sim.State.People.HrEvents.Find(e => e.Place == place && e.WorkerId == workerId);
        }

        private static HrEvent Raise(Simulation sim, HrEventKind kind, IStaff w, long amount)
        {
            PeopleState s = sim.State.People;
            var e = new HrEvent { Id = s.NextHrEventId++, Kind = kind, Place = Place(w), WorkerId = w.Id, At = sim.State.Clock.SimTime, Amount = amount };
            s.HrEvents.Add(e);
            w.Hr.EventSeason = Season(sim) + 1;
            sim.Emit(new HrEventRaised(e.At, e.Id, kind, e.Place, w.Id));
            return e;
        }

        /// <summary>Sick leave starts at once (sickDays off, no work); whether it's paid waits for the player.</summary>
        private static void FallSick(Simulation sim, IStaff w)
        {
            int days = sim.Config.Game.People.Hr.SickDays;
            FarmSystem.Accrue(sim);
            w.Hr.SickUntil = sim.State.Clock.SimTime + days * Day;
            FarmSystem.Refresh(sim);
            Raise(sim, HrEventKind.SickLeave, w, (long)((System.Numerics.BigInteger)w.Wage * days * Day / sim.Clock.SecondsPerSeason));
        }

        /// <summary>
        /// A bad batch (§3.7): up to a day's make (a recipe's mix; the mill an hour's grinding) of the product it holds most of is set aside
        /// at its cost until the player throws it away or sells it cheap.
        /// </summary>
        private static void BadBatch(Simulation sim, Worker w)
        {
            Business.Business b = sim.State.Businesses.Find(w.Place);
            if (b.Status != BusinessStatus.Open) return;
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            int good = -1;
            var goods = new List<int>();
            if (t.ByRecipe) goods.AddRange(t.Products);
            else foreach (BusinessOutput o in t.Outputs) goods.Add(o.Good);
            foreach (int g in goods) if (b.Stock[g] > 0 && (good < 0 || b.Stock[g] > b.Stock[good])) good = g;
            if (good < 0) return;
            long grams = System.Math.Min(b.Stock[good], t.ByRecipe ? System.Math.Max(b.Mix[good], 1) : t.GramsPerHour);
            long cost = (long)((System.Numerics.BigInteger)b.StockCost[good] * grams / b.Stock[good]);
            b.Stock[good] -= grams;
            b.StockCost[good] -= cost;
            long cheap = Money.MulRate(MarketRules.Value(sim, good, grams, MarketRules.Floor(sim, good)), sim.Config.Game.People.Hr.BadBatchSellShare);
            HrEvent e = Raise(sim, HrEventKind.BadBatch, w, cheap);
            e.Good = good;
            e.Grams = grams;
            e.Cost = cost;
        }

        /// <summary>Hourly, live or away: sick leave that has ended.</summary>
        internal static void Recover(Simulation sim)
        {
            long t = sim.State.Clock.SimTime;
            PeopleState s = sim.State.People;
            foreach (Hand h in s.Hands)
            {
                if (h.Hr.SickUntil == 0 || t < h.Hr.SickUntil) continue;
                FarmSystem.Accrue(sim);
                h.Hr.SickUntil = 0;
                FarmSystem.Refresh(sim);
            }
            foreach (Worker w in s.Workers) if (w.Hr.SickUntil != 0 && t >= w.Hr.SickUntil) w.Hr.SickUntil = 0;
        }

        /// <summary>
        /// A worker goes (left, let go, retired, poached): their waiting events go with them, a bad batch thrown away, an advance not
        /// repaid written off as wages.
        /// </summary>
        internal static void Gone(Simulation sim, IStaff w)
        {
            PeopleState s = sim.State.People;
            foreach (HrEvent e in s.HrEvents.FindAll(x => x.Place == Place(w) && x.WorkerId == w.Id))
            {
                if (e.Kind == HrEventKind.BadBatch) Throw(sim, e);
                s.HrEvents.Remove(e);
            }
            if (w.Hr.Advance <= 0) return;
            Business.Business b = w is Worker k ? sim.State.Businesses.Find(k.Place) : null;
            Ledger books = b == null ? sim.State.Ledger : b.Ledger;
            books.Post(sim.State.Clock.SimTime, sim.Period, "ledger.advanceWrittenOff", b == null ? "hand:" + w.Id : "business:" + b.Id,
                new Posting(Account.HiredLabour, w.Hr.Advance), new Posting(Account.PrepaidWages, -w.Hr.Advance));
            w.Hr.Advance = 0;
        }

        // ---- the answer (§3.7) ----

        /// <summary>What the first choice (pay, match, paid leave, an advance) costs now: the rest of the season's raise, the advance; else 0.</summary>
        public static long CostNow(Simulation sim, HrEvent e)
        {
            IStaff w = WorkRules.Find(sim, e.Place, e.WorkerId);
            if (w == null) return 0;
            switch (e.Kind)
            {
                case HrEventKind.RaiseRequest:
                case HrEventKind.Poaching:
                    return PeopleRules.RestOfSeason(sim, System.Math.Max(0, e.Amount - Paid(w)));
                case HrEventKind.FamilyEmergency: return e.Amount;
                default: return 0;
            }
        }

        /// <summary>
        /// Answers an event with choice 0 (pay the raise; match the offer; paid leave; the advance; throw the batch away) or 1 (not now;
        /// let them go; unpaid; no; sell it cheap), and applies its effects.
        /// </summary>
        internal static CommandResult Answer(Simulation sim, long eventId, int choice)
        {
            PeopleState s = sim.State.People;
            HrEvent e = s?.HrEvents.Find(x => x.Id == eventId);
            if (e == null) return CommandResult.Reject(RejectReason.UnknownEvent);
            if (choice != 0 && choice != 1) return CommandResult.Reject(RejectReason.InvalidArgument);
            IStaff w = WorkRules.Find(sim, e.Place, e.WorkerId);
            if (w == null) return CommandResult.Reject(RejectReason.UnknownEvent);
            HrDef d = sim.Config.Game.People.Hr;
            ReputationDef rep = sim.Config.Game.People.Reputation;
            long cost = choice == 0 ? CostNow(sim, e) : 0;
            if (cost > 0 && !WorkRules.CanPay(sim, w, cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            switch (e.Kind)
            {
                case HrEventKind.RaiseRequest:
                    if (choice == 0)
                    {
                        PayFromNow(sim, w, e.Amount - Paid(w));
                        w.Hr.RaiseRefusals = 0;
                        Remember(sim, w);
                    }
                    else
                    {
                        w.Hr.RaiseRefusals++;
                        Repute(sim, rep.RaiseRefused);
                    }
                    break;
                case HrEventKind.Poaching:
                    if (choice == 0)
                    {
                        PayFromNow(sim, w, e.Amount - Paid(w));
                        SetMood(sim, w, d.MatchedMood);
                        Remember(sim, w);
                    }
                    else w.Hr.Leaving = true;
                    break;
                case HrEventKind.SickLeave:
                    if (choice == 0)
                    {
                        SetMood(sim, w, d.SickPaidMood);
                        Repute(sim, rep.SickPaid);
                    }
                    else
                    {
                        Refund(sim, w, e.Amount);
                        SetMood(sim, w, d.SickUnpaidMood);
                    }
                    break;
                case HrEventKind.FamilyEmergency:
                    if (choice == 0)
                    {
                        Advance(sim, w, e.Amount);
                        SetMood(sim, w, d.AdvanceMood);
                        Repute(sim, rep.Advance);
                    }
                    else SetMood(sim, w, d.NoAdvanceMood);
                    break;
                case HrEventKind.BadBatch:
                    if (choice == 0) Throw(sim, e);
                    else
                    {
                        Business.Business b = sim.State.Businesses.Find(e.Place);
                        string reference = "business:" + b.Id;
                        long now = sim.State.Clock.SimTime;
                        if (e.Amount > 0) b.Ledger.Post(now, sim.Period, "ledger.badBatchSold", reference, new Posting(Account.Cash, e.Amount), new Posting(Account.Sales, -e.Amount));
                        if (e.Cost > 0) b.Ledger.Post(now, sim.Period, "ledger.costOfGoods", reference, new Posting(Account.CostOfGoodsSold, e.Cost), new Posting(Account.BusinessStock, -e.Cost));
                        Repute(sim, rep.BadBatchSold);
                        BusinessBooks.Sweep(sim, b);
                    }
                    break;
            }
            s.HrEvents.Remove(e);
            sim.Emit(new HrEventAnswered(sim.State.Clock.SimTime, e.Id, e.Kind, choice));
            return CommandResult.Ok;
        }

        private static void Throw(Simulation sim, HrEvent e)
        {
            Business.Business b = sim.State.Businesses.Find(e.Place);
            if (b == null || e.Cost <= 0) return;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.badBatch", "business:" + b.Id, new Posting(Account.CostOfGoodsSold, e.Cost), new Posting(Account.BusinessStock, -e.Cost));
        }

        /// <summary>
        /// A higher season's pay from now (a raise, a matched offer): the cash wage goes up by <paramref name="more"/>, and the rest of this
        /// season's share of it is paid now (a hand's into their prepaid wage, a business worker's as wages).
        /// </summary>
        private static void PayFromNow(Simulation sim, IStaff w, long more)
        {
            if (more <= 0) return;
            long now = PeopleRules.RestOfSeason(sim, more);
            if (w is Hand h)
            {
                Reprepay(sim, h, now);
                h.Wage += more;
                return;
            }
            var k = (Worker)w;
            Business.Business b = sim.State.Businesses.Find(k.Place);
            BusinessBooks.Draw(sim, b, now);
            BusinessRules.PayWage(sim, b, now);
            k.Wage += more;
        }

        /// <summary>Unpaid sick days: their wage for those days back (a hand's out of their prepaid wage, at most what's left of it).</summary>
        private static void Refund(Simulation sim, IStaff w, long amount)
        {
            if (w is Hand h)
            {
                Reprepay(sim, h, -System.Math.Min(amount, Accrual.RefundIfClosed(sim, h.PrepaymentId)));
                return;
            }
            Business.Business b = sim.State.Businesses.Find(((Worker)w).Place);
            if (amount > 0)
                b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.sickUnpaid", "business:" + b.Id, new Posting(Account.Cash, amount), new Posting(Account.HiredLabour, -amount));
            BusinessBooks.Sweep(sim, b);
        }

        /// <summary>An advance (a prepaid wage, repaid from the next renewal), paid now from your cash or the business's.</summary>
        private static void Advance(Simulation sim, IStaff w, long amount)
        {
            if (amount <= 0) return;
            Business.Business b = w is Worker k ? sim.State.Businesses.Find(k.Place) : null;
            if (b != null) BusinessBooks.Draw(sim, b, amount);
            Ledger books = b == null ? sim.State.Ledger : b.Ledger;
            books.Post(sim.State.Clock.SimTime, sim.Period, "ledger.advance", b == null ? "hand:" + w.Id : "business:" + b.Id,
                new Posting(Account.PrepaidWages, amount), new Posting(Account.Cash, -amount));
            w.Hr.Advance += amount;
        }

        /// <summary>A hand's prepaid wage reopened to the season's end with <paramref name="change"/> more (or less) in it.</summary>
        private static void Reprepay(Simulation sim, Hand h, long change)
        {
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            FarmSystem.Accrue(sim);
            long left = h.PrepaymentId != 0 && sim.State.Finance.FindPrepayment(h.PrepaymentId) != null ? Accrual.Close(sim, h.PrepaymentId) : 0;
            h.PrepaymentId = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, left + change, t, (t / S + 1) * S, "hand:" + h.Id).Id;
            FarmSystem.Refresh(sim);
        }
    }
}
