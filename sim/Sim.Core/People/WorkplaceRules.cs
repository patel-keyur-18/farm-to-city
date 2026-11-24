using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>
    /// Supervisors, accidents, conflicts and associations (spec 028 §3.9, ADR 0166 gaps 3 to 5): the events ride the HR-event engine but carry a deadline and a default, so a card nobody answers
    /// is answered in the hourly pass, live or away. A supervisor is the farm's (a business has one day worker); conflicts are the farm's and a business with two workers; associations the farm's.
    /// </summary>
    public static class WorkplaceRules
    {
        private const long Hour = 3600, Day = ClockDef.SecondsPerDay;
        private const ulong AccidentDraw = 6, SevereDraw = 7;
        private const ulong ConflictKey = 2UL << 32, AssocKey = 3UL << 32, PairKey = 4UL << 32;

        public static WorkplaceDef Defs(Simulation sim) { return sim.Config.Game.Workplace; }

        /// <summary>The workplace rules apply from the Township (with the benefits), when the people are in the game.</summary>
        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.People != null && BenefitRules.On(sim); }

        private static long Season(Simulation sim) { return sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason; }

        // ---- who is at a place ----

        /// <summary>The workers at a place: your active hands (0), or a business's day worker, night worker and manager, in id order.</summary>
        public static List<IStaff> Workers(Simulation sim, long place)
        {
            if (place == 0 || sim.State.People == null) return BenefitRules.Staff(sim, 0);
            return BenefitRules.Staff(sim, place);
        }

        public static long PlaceOf(IStaff w) { return w is Worker k ? k.Place : 0; }

        // ---- the supervisor ----

        public static Supervisor Supervisor(Simulation sim) { return sim.State.People?.Workplace.Supervisor; }

        /// <summary>His stars: 1 at skill 0 to 19, one more for each twenty, 5 from 80.</summary>
        public static int Stars(Fixed skill) { return System.Math.Min(5, 1 + (int)(skill.RoundToLong() / 20)); }

        /// <summary>Whether the supervisor covers this worker: one of your first five hands (by id) while he stands.</summary>
        public static bool Covered(Simulation sim, IStaff w)
        {
            Supervisor sv = Supervisor(sim);
            if (sv == null || !(w is Hand)) return false;
            List<IStaff> hands = Workers(sim, 0);
            int at = hands.IndexOf(w);
            return at >= 0 && at < Defs(sim).SupervisorSpan;
        }

        /// <summary>How many hands he covers, out of how many you have.</summary>
        public static int CoveredCount(Simulation sim) { return Supervisor(sim) == null ? 0 : System.Math.Min(Defs(sim).SupervisorSpan, Workers(sim, 0).Count); }

        /// <summary>The factor on a worker's output: +1.2% a star of his supervisor's, × 0.85 in a season your refusal of an association cut it (1 when neither).</summary>
        public static Fixed OutputFactor(Simulation sim, IStaff w)
        {
            WorkplaceState ws = sim.State.People?.Workplace;
            if (ws == null || (ws.Supervisor == null && ws.Places.Count == 0)) return Fixed.One;
            WorkplaceDef d = Defs(sim);
            Fixed f = Fixed.One;
            if (Covered(sim, w)) f += d.OutputPerStar * Fixed.FromInt(Stars(ws.Supervisor.Skill));
            PlaceRelations r = ws.Relations(PlaceOf(w));
            if (r != null && r.CutSeason == Season(sim) + 1) f = f * d.RefuseOutput;
            return f;
        }

        /// <summary>The farm's hands while a quarrel's talk takes your hours: the cut from your labour, like a mandi trip's.</summary>
        public static Fixed TalkCut(Simulation sim)
        {
            WorkplaceState ws = sim.State.People?.Workplace;
            if (ws == null || ws.TalkUntil == 0 || sim.State.Clock.SimTime >= ws.TalkUntil) return Fixed.Zero;
            WorkplaceDef d = Defs(sim);
            return Fixed.FromInt(d.TalkHours).MulRatio(24, d.TalkWindowHours);
        }

        /// <summary>The wage a supervisor of a skill asks at the post (a hand's formula on the post's wage).</summary>
        public static long Wage(Simulation sim, int skill, bool above) { return PeopleRules.AskedWage(sim, Defs(sim).SupervisorWage, Fixed.FromInt(skill), above); }

        internal static void Hire(Simulation sim, Applicant a, long wage)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            long pay = PeopleRules.RestOfSeason(sim, wage);
            sim.State.Ledger.Post(t, sim.Period, "ledger.supervisorWage", "supervisor", new Posting(Account.HiredLabour, pay), new Posting(Account.Cash, -pay));
            s.Workplace.Supervisor = new Supervisor { Name = a.Name, Skill = Fixed.FromInt(a.Skill), Wage = wage, HiredAt = t };
            s.Applicants.RemoveAll(x => x.Place == HiringRules.SupervisorPlace);
            s.Posts.RemoveAll(x => x.Place == HiringRules.SupervisorPlace);
            sim.Emit(new SupervisorHired(t, wage, pay));
        }

        internal static CommandResult Dismiss(Simulation sim)
        {
            if (!On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            if (Supervisor(sim) == null) return CommandResult.Reject(RejectReason.NoSupervisor);
            sim.State.People.Workplace.Supervisor = null;
            sim.Emit(new SupervisorLeft(sim.State.Clock.SimTime, false));
            return CommandResult.Ok;
        }

        // ---- accidents (§3.9) ----

        /// <summary>The factors on a worker's accident chance today: night, overtime, maintenance skipped on a machine you own (the farm's hands), and a supervisor's half.</summary>
        public static Fixed AccidentChance(Simulation sim, IStaff w, bool withSupervisor)
        {
            WorkplaceDef d = Defs(sim);
            Fixed c = d.AccidentChance;
            if (w is Worker k && k.Night) c = c * d.NightFactor;
            if (Overtime(sim, w)) c = c * d.OvertimeFactor;
            if (w is Hand && MaintenanceSkipped(sim)) c = c * d.SkippedFactor;
            return withSupervisor && Covered(sim, w) ? c * d.RiskHalf : c;
        }

        public static bool Overtime(Simulation sim, IStaff w) { return w is Hand h ? h.LongDays : WorkRules.LongDays(sim, (Worker)w); }

        public static bool MaintenanceSkipped(Simulation sim)
        {
            if (sim.State.Equipment == null) return false;
            foreach (Equipment.Machine m in sim.State.Equipment.Machines) if (!m.Leased && !m.Maintain) return true;
            return false;
        }

        /// <summary>The hours an accident keeps a worker out: 8, with the clinic 3, a severe one 32.</summary>
        public static int OutHours(Simulation sim, bool severe)
        {
            WorkplaceDef d = Defs(sim);
            return severe ? d.SevereHours : Town.ClinicRules.EffectTown(sim) != Fixed.Zero ? d.ClinicOutHours : d.OutHours;
        }

        /// <summary>The bill at today's prices; health cover pays it.</summary>
        public static long Bill(Simulation sim, IStaff w, bool severe)
        {
            if (BenefitRules.Has(sim, w, Benefit.Health)) return 0;
            WorkplaceDef d = Defs(sim);
            return Money.RoundToCoin(MarketRules.Indexed(sim, severe ? d.SevereBill : d.Bill));
        }

        /// <summary>A worker's wage for <paramref name="hours"/> out, in whole coins (a season has 24 hours × its days).</summary>
        public static long WageFor(Simulation sim, IStaff w, int hours)
        {
            return Money.RoundToCoin((long)((System.Numerics.BigInteger)HrRules.Paid(w) * hours * Hour / sim.Clock.SecondsPerSeason));
        }

        /// <summary>
        /// Daily, live or away, after the HR events' rolls: each worker at work rolls their accident; each place of two or more rolls a quarrel (a supervisor on the farm settles it: no card).
        /// </summary>
        internal static void Daily(Simulation sim)
        {
            if (!On(sim)) return;
            PeopleState s = sim.State.People;
            WorkplaceDef d = Defs(sim);
            long day = sim.State.Clock.SimTime / Day, now = sim.State.Clock.SimTime;
            var staff = new List<IStaff>();
            foreach (Hand h in s.Hands) if (h.Contract == ContractState.Active) staff.Add(h);
            foreach (Worker w in s.Workers) staff.Add(w);
            foreach (IStaff w in staff)
            {
                if (WorkRules.Away(w) || Open(sim, HrEventKind.Accident, PlaceOf(w), w.Id)) continue;
                Fixed full = AccidentChance(sim, w, false), half = AccidentChance(sim, w, true);
                ulong roll = HrRules.Roll(sim, w, day, AccidentDraw);
                if (!Rng.Chance(roll, half))
                {
                    if (half != full && Rng.Chance(roll, full)) Supervisor(sim).Spared++;
                    continue;
                }
                Hurt(sim, w, Rng.Chance(HrRules.Roll(sim, w, day, SevereDraw), d.SevereShare), now);
            }
            Conflicts(sim, 0, day, now);
            foreach (Business.Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed) Conflicts(sim, b.Id, day, now);
        }

        private static bool Open(Simulation sim, HrEventKind kind, long place, long worker)
        {
            return sim.State.People.HrEvents.Exists(e => e.Kind == kind && e.Place == place && (worker == 0 || e.WorkerId == worker || e.OtherId == worker));
        }

        private static HrEvent Raise(Simulation sim, HrEventKind kind, long place, long worker)
        {
            PeopleState s = sim.State.People;
            long now = sim.State.Clock.SimTime;
            var e = new HrEvent { Id = s.NextHrEventId++, Kind = kind, Place = place, WorkerId = worker, At = now, ExpiresAt = now + Defs(sim).ExpiryHours * Hour };
            s.HrEvents.Add(e);
            return e;
        }

        internal static void Hurt(Simulation sim, IStaff w, bool severe, long now)
        {
            int hours = OutHours(sim, severe);
            long bill = Bill(sim, w, severe);
            if (bill > 0 && !PayBill(sim, w, bill)) bill = 0; // cash that can't cover it: he bears it himself
            FarmSystem.Accrue(sim);
            w.Hr.SickUntil = now + hours * Hour;
            FarmSystem.Refresh(sim);
            HrEvent e = Raise(sim, HrEventKind.Accident, PlaceOf(w), w.Id);
            e.Amount = WageFor(sim, w, hours);
            e.Cost = bill;
            e.Hours = hours;
            e.Severe = severe;
            sim.Emit(new HrEventRaised(now, e.Id, e.Kind, e.Place, w.Id));
            sim.Emit(new AccidentHappened(now, e.Place, w.Id, hours, severe, bill));
        }

        private static bool PayBill(Simulation sim, IStaff w, long bill) { return WorkRules.Pay(sim, w, bill, "ledger.accidentBill"); }

        // ---- conflicts ----

        /// <summary>The chance a place quarrels today: 3% a season × (1 + 2 × the team's average tiredness), over its days.</summary>
        public static Fixed ConflictChance(Simulation sim, long place)
        {
            WorkplaceDef d = Defs(sim);
            List<IStaff> list = Workers(sim, place);
            if (list.Count < d.ConflictMinWorkers) return Fixed.Zero;
            return d.ConflictChance * (Fixed.One + d.ConflictFatigue * AverageFatigue(list)).MulRatio(1, sim.Clock.SecondsPerSeason / Day);
        }

        public static Fixed AverageFatigue(List<IStaff> list)
        {
            if (list.Count == 0) return Fixed.Zero;
            Fixed sum = Fixed.Zero;
            foreach (IStaff w in list) sum += w.Fatigue;
            return sum.MulRatio(1, list.Count);
        }

        private static void Conflicts(Simulation sim, long place, long day, long now)
        {
            List<IStaff> list = Workers(sim, place);
            if (list.Count < Defs(sim).ConflictMinWorkers || Open(sim, HrEventKind.Conflict, place, 0)) return;
            ulong roll = Rng.Next(sim.State.Seed, RngStream.HrEvents, ConflictKey + (ulong)place, (ulong)day * 8);
            if (!Rng.Chance(roll, ConflictChance(sim, place))) return;
            if (place == 0 && Supervisor(sim) != null) // he settles it: you never see a card
            {
                Supervisor(sim).Settled++;
                sim.Emit(new ConflictRaised(now, place, true));
                return;
            }
            ulong pick = Rng.Next(sim.State.Seed, RngStream.HrEvents, PairKey + (ulong)place, (ulong)day * 8);
            int i = (int)Rng.Below(pick, (ulong)list.Count);
            HrEvent e = Raise(sim, HrEventKind.Conflict, place, list[i].Id);
            e.OtherId = list[(i + 1) % list.Count].Id;
            sim.Emit(new HrEventRaised(now, e.Id, e.Kind, place, e.WorkerId));
            sim.Emit(new ConflictRaised(now, place, false));
        }

        // ---- associations ----

        /// <summary>What an association is about at a close: pay under the reference, a tired team, or a poor name; null when none holds.</summary>
        public static bool PayLow(Simulation sim, List<IStaff> list)
        {
            if (list.Count == 0) return false;
            long asked = 0, paid = 0;
            foreach (IStaff w in list) { asked += HrRules.Asked(sim, w); paid += HrRules.Paid(w); }
            return asked > 0 && paid < Money.MulRate(asked, Fixed.One - Defs(sim).AssocPayUnder);
        }

        /// <summary>The chance one forms at a close: its own, halved by a supervisor.</summary>
        public static Fixed FormChance(Simulation sim, bool withSupervisor)
        {
            WorkplaceDef d = Defs(sim);
            return withSupervisor && Supervisor(sim) != null ? d.AssocForms * d.RiskHalf : d.AssocForms;
        }

        /// <summary>At each close: the supervisor's wage for the new season (unpaid, he goes), then the farm's team's tiredness and whether it forms an association.</summary>
        internal static void AtClose(Simulation sim)
        {
            if (!On(sim)) return;
            PeopleState s = sim.State.People;
            WorkplaceDef d = Defs(sim);
            long now = sim.State.Clock.SimTime, season = Season(sim);
            Supervisor sv = s.Workplace.Supervisor;
            if (sv != null)
            {
                if (sim.State.Ledger.CanAfford(sv.Wage))
                    sim.State.Ledger.Post(now, sim.Period, "ledger.supervisorWage", "supervisor", new Posting(Account.HiredLabour, sv.Wage), new Posting(Account.Cash, -sv.Wage));
                else
                {
                    s.Workplace.Supervisor = null;
                    sim.Emit(new SupervisorLeft(now, true));
                    HrRules.Repute(sim, sim.Config.Game.People.Reputation.Lapse);
                }
            }
            List<IStaff> hands = Workers(sim, 0);
            PlaceRelations r = s.Workplace.Relations(0);
            if (hands.Count > 0 && AverageFatigue(hands) > d.AssocFatigue) { r = s.Workplace.RelationsFor(0); r.TiredCloses++; }
            else if (r != null) r.TiredCloses = 0;
            if (hands.Count < d.AssocMinWorkers || (r != null && season < r.QuietUntil) || Open(sim, HrEventKind.Association, 0, 0)) return;
            if (!(PayLow(sim, hands) || (r != null && r.TiredCloses >= d.AssocCloses) || s.Reputation < d.AssocReputation)) return;
            ulong roll = Rng.Next(sim.State.Seed, RngStream.HrEvents, AssocKey, (ulong)season * 8);
            if (!Rng.Chance(roll, FormChance(sim, true)))
            {
                if (s.Workplace.Supervisor != null && Rng.Chance(roll, FormChance(sim, false))) s.Workplace.Supervisor.Kept++;
                return;
            }
            HrEvent e = Raise(sim, HrEventKind.Association, 0, 0); // the whole place's: no worker's card, so no worker leaving takes it away (review of Sprint 26)
            sim.Emit(new HrEventRaised(now, e.Id, e.Kind, 0, e.WorkerId));
            sim.Emit(new AssociationFormed(now, 0));
        }

        // ---- the cards' deadlines (gap 4) ----

        /// <summary>The answer a card's default gives: pay the days' wage, let it be, refuse.</summary>
        public static int DefaultChoice(HrEventKind kind) { return kind == HrEventKind.Accident ? 0 : 2; }

        /// <summary>Hourly, live or away: a card past its time is answered by its default.</summary>
        internal static void Expire(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s == null || s.HrEvents.Count == 0) return;
            long now = sim.State.Clock.SimTime;
            foreach (HrEvent e in s.HrEvents.FindAll(x => x.Timed && x.ExpiresAt != 0 && now >= x.ExpiresAt))
            {
                int choice = DefaultChoice(e.Kind);
                bool applied = Answer(sim, e.Id, choice).Accepted;
                if (!applied) { choice = e.Kind == HrEventKind.Accident ? 1 : 2; applied = Answer(sim, e.Id, choice).Accepted; } // pay what can't be paid: the unpaid answer
                if (s.HrEvents.Contains(e)) s.HrEvents.Remove(e);
                if (applied) sim.Emit(new HrEventDefaulted(now, e.Id, e.Kind, choice, e.Place, e.WorkerId)); // told only when something was applied: a card whose people are gone just ends
            }
        }

        // ---- answering ----

        /// <summary>What a choice costs you now: an association's raise for the rest of the season; talking costs your hours, not coins.</summary>
        public static long CostNow(Simulation sim, HrEvent e, int choice)
        {
            if (e.Kind != HrEventKind.Association || choice == 2) return 0;
            Fixed raise = choice == 0 ? Defs(sim).AssocRaise : Defs(sim).AssocNegotiateRaise;
            long total = 0;
            foreach (IStaff w in Workers(sim, e.Place)) total += PeopleRules.RestOfSeason(sim, Money.RoundToCoin(Money.MulRate(HrRules.Paid(w), raise)));
            return total;
        }

        internal static CommandResult Answer(Simulation sim, long eventId, int choice)
        {
            PeopleState s = sim.State.People;
            HrEvent e = s?.HrEvents.Find(x => x.Id == eventId);
            if (e == null || !e.Timed) return CommandResult.Reject(RejectReason.UnknownEvent);
            if (choice < 0 || choice > (e.Kind == HrEventKind.Accident ? 1 : 2)) return CommandResult.Reject(RejectReason.InvalidArgument);
            WorkplaceDef d = Defs(sim);
            IStaff w = e.Kind == HrEventKind.Association ? null : WorkRules.Find(sim, e.Place, e.WorkerId);
            if (w == null && e.Kind != HrEventKind.Association) return CommandResult.Reject(RejectReason.UnknownEvent);
            long now = sim.State.Clock.SimTime;
            switch (e.Kind)
            {
                case HrEventKind.Accident:
                    if (choice == 0)
                    {
                        HrRules.SetMood(sim, w, d.PayMood);
                        HrRules.Repute(sim, d.PayReputation);
                    }
                    else
                    {
                        HrRules.Refund(sim, w, e.Amount);
                        HrRules.SetMood(sim, w, d.UnpaidMood);
                        HrRules.Repute(sim, d.UnpaidReputation);
                    }
                    break;
                case HrEventKind.Conflict:
                    IStaff other = WorkRules.Find(sim, e.Place, e.OtherId);
                    if (other == null) return CommandResult.Reject(RejectReason.UnknownEvent);
                    Quarrel(sim, e, choice, w, other, now);
                    break;
                default:
                    CommandResult r = Ask(sim, e, choice);
                    if (!r.Accepted) return r;
                    break;
            }
            s.HrEvents.Remove(e);
            sim.Emit(new HrEventAnswered(now, e.Id, e.Kind, choice));
            return CommandResult.Ok;
        }

        private static void Quarrel(Simulation sim, HrEvent e, int choice, IStaff a, IStaff b, long now)
        {
            WorkplaceDef d = Defs(sim);
            ulong roll = Rng.Next(sim.State.Seed, RngStream.HrEvents, PairKey + (ulong)e.Id, 1);
            if (choice == 0)
            {
                FarmSystem.Accrue(sim);
                sim.State.People.Workplace.TalkUntil = now + d.TalkWindowHours * Hour;
                FarmSystem.Refresh(sim);
                bool settled = Rng.Chance(roll, d.TalkResolves);
                sim.Emit(new HrOutcome(now, HrEventKind.Conflict, 0, settled));
                if (settled) return;
                HrRules.SetMood(sim, a, d.LetMood);
                HrRules.SetMood(sim, b, d.LetMood);
                return;
            }
            if (choice == 1)
            {
                HrRules.SetMood(sim, b, d.MoveMood); // he is moved apart, which he did not ask for
                return;
            }
            HrRules.SetMood(sim, a, d.LetMood);
            HrRules.SetMood(sim, b, d.LetMood);
            if (Rng.Chance(roll, d.LetResigns)) (Rng.Chance(Rng.Next(sim.State.Seed, RngStream.HrEvents, PairKey + (ulong)e.Id, 2), Fixed.FromRatio(1, 2)) ? a : b).Hr.Leaving = true;
        }

        private static CommandResult Ask(Simulation sim, HrEvent e, int choice)
        {
            PeopleState s = sim.State.People;
            WorkplaceDef d = Defs(sim);
            long season = Season(sim);
            PlaceRelations r = s.Workplace.RelationsFor(e.Place);
            bool agrees = choice == 1 && Rng.Chance(Rng.Next(sim.State.Seed, RngStream.HrEvents, AssocKey + (ulong)e.Id, 1), d.AssocAgrees);
            if (choice == 1) sim.Emit(new HrOutcome(sim.State.Clock.SimTime, HrEventKind.Association, 1, agrees));
            if (choice == 0 || agrees)
            {
                long cost = CostNow(sim, e, choice);
                if (cost > 0 && !sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
                Fixed raise = choice == 0 ? d.AssocRaise : d.AssocNegotiateRaise;
                foreach (IStaff w in Workers(sim, e.Place))
                {
                    long more = Money.RoundToCoin(Money.MulRate(HrRules.Paid(w), raise));
                    HrRules.PayFromNow(sim, w, more);
                }
                if (choice == 1) AddBenefit(sim, e.Place);
                r.QuietUntil = season + d.AssocQuietCloses;
                r.Refusals = 0;
                return CommandResult.Ok;
            }
            // refused, or negotiated and not agreed: it counts as a refusal
            foreach (IStaff w in Workers(sim, e.Place)) HrRules.SetMood(sim, w, d.RefuseMood);
            r.Refusals++;
            r.CutSeason = season + 1;
            if (r.Refusals >= 2) Stoppage(sim, e.Place);
            return CommandResult.Ok;
        }

        /// <summary>The first benefit the farm doesn't give yet that it can: a negotiated settlement's one benefit.</summary>
        private static void AddBenefit(Simulation sim, long place)
        {
            foreach (Benefit b in BenefitRules.All)
            {
                if ((BenefitRules.FlagsOfEmployer(sim, place) & (int)b) != 0) continue;
                if (BenefitRules.Set(sim, place, b, true).Accepted) return;
            }
        }

        private static void Stoppage(Simulation sim, long place)
        {
            long until = sim.State.Clock.SimTime + Defs(sim).StoppageHours * Hour;
            FarmSystem.Accrue(sim);
            foreach (IStaff w in Workers(sim, place)) if (w.Hr.SickUntil < until) w.Hr.SickUntil = until;
            FarmSystem.Refresh(sim);
        }
    }
}
