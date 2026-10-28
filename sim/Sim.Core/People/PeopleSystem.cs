using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.People
{
    /// <summary>
    /// People (spec 006 §3.9, ADR 0039). Daily: day labourers go home, the new day's pool is drawn. Hourly: a job post
    /// expires; live only, lapsed hands and waiting applicants count their grace and new applicants arrive; then, live or
    /// away, the hands carry out their standing orders. Renewals run inside the season close (step 6).
    /// </summary>
    internal sealed class PeopleSystem : ISimSystem
    {
        public const string HutJob = "hut.build";
        private const long Day = ClockDef.SecondsPerDay;

        public SystemOrder Order { get { return SystemOrder.Labour; } }

        /// <summary>The roster of neighbours (seeded names and traits, stream People) and the first day's pool.</summary>
        public static void Initialise(Simulation sim)
        {
            PeopleDefs p = sim.Config.Game.People;
            var s = new PeopleState();
            sim.State.People = s;
            var used = new bool[p.Names.Count];
            for (int i = 0; i < p.Pool.RosterSize; i++) s.Roster.Add(NewNeighbour(sim, i + 1, used));
            DrawPool(sim);
        }

        /// <summary>A neighbour with a seeded name none of <paramref name="used"/> has (stream People, counter = id − 1) and one trait.</summary>
        private static Neighbour NewNeighbour(Simulation sim, long id, bool[] used)
        {
            PeopleDefs p = sim.Config.Game.People;
            int left = 0;
            foreach (bool u in used) if (!u) left++;
            int k = (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.People, (ulong)(id - 1), 0), (ulong)left);
            int name = -1;
            for (int n = 0; n < used.Length; n++)
            {
                if (used[n]) continue;
                if (k-- == 0) { name = n; break; }
            }
            used[name] = true;
            int trait = (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.People, (ulong)(id - 1), 1), (ulong)p.Traits.Count);
            return new Neighbour { Id = id, Name = name, Trait = trait };
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Daily)
            {
                NewDay(sim);
                WorkRules.Daily(sim);
            }
            if (pass != ItemClass.Hourly) return;
            HiringRules.Expire(sim);
            if (!sim.State.Clock.IsAway)
            {
                Grace(sim);
                WaitingApplicants(sim);
                Regulars(sim);
                Arrivals(sim);
                HiringRules.BusinessArrivals(sim);
            }
            HiringRules.References(sim);
            WorkRules.Courses(sim);
            Release(sim);
            Orders.Run(sim);
        }

        // ---- labourers (§3.2, ADR 0048 §3) ----

        /// <summary>A new day's pool: labourers whose crop is ready by now go first, so they can be drawn again; the rest stay.</summary>
        private static void NewDay(Simulation sim)
        {
            Release(sim);
            DrawPool(sim);
        }

        /// <summary>
        /// While labourers are hired (live or away): every ripe field without a hand's order is harvested, in crop id order
        /// (overflow to the trader, so nothing is lost). Then those whose time is up go home. Runs before the hands' orders.
        /// </summary>
        private static void Release(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            bool hired = false, done = false;
            foreach (Neighbour n in s.Roster)
            {
                hired |= n.Hired;
                done |= n.Hired && t >= n.Until;
            }
            List<Citizen> town = PeopleRules.TownLabourers(sim);
            foreach (Citizen c in town)
            {
                hired |= c.LabourUntil != 0;
                done |= c.LabourUntil != 0 && t >= c.LabourUntil;
            }
            if (!hired) return;
            foreach (Crop c in sim.State.Farm.Crops.ToArray())
            {
                if (c.State != CropState.Ready || s.OrderFor(OrderTargetKind.Field, c.TileId) != null) continue;
                if (new Harvest(t, c.TileId, Overflow.SellRestToTrader).ApplyNow(sim).Accepted) sim.Emit(new LabourersHarvested(t, c.TileId, c.HarvestGrams));
            }
            if (!done) return;
            FarmSystem.Accrue(sim);
            int count = 0;
            foreach (Neighbour n in s.Roster)
            {
                if (!n.Hired || t < n.Until) continue;
                n.Tile = -1;
                n.Until = 0;
                count++;
            }
            foreach (Citizen c in town)
            {
                if (c.LabourUntil == 0 || t < c.LabourUntil) continue;
                c.LabouredUntil = c.LabourUntil;
                c.LabourUntil = 0;
                count++;
            }
            FarmSystem.Refresh(sim);
            sim.Emit(new LabourersLeft(t, count));
        }

        /// <summary>
        /// A seeded shuffle of the roster (stream DayLabourPool, counter = day); the first availablePerDay of those not already
        /// on a crop are free (ADR 0048 §3).
        /// </summary>
        private static void DrawPool(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            ulong day = (ulong)(t / Day);
            int free = sim.Config.Game.People.Pool.AvailablePerDay[(int)Calendar.SeasonAt(t, sim.Clock)];
            int n = s.Roster.Count;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = (int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.DayLabourPool, (ulong)i, day), (ulong)(i + 1));
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
            foreach (Neighbour x in s.Roster) x.FreeToday = false;
            for (int i = 0; i < n && free > 0; i++)
            {
                if (s.Roster[order[i]].Hired) continue;
                s.Roster[order[i]].FreeToday = true;
                free--;
            }
        }

        // ---- the hand's hut (§3.4) ----

        internal sealed class HutBuild : IJobHandler
        {
            public string JobType { get { return HutJob; } }
            public SystemOrder System { get { return SystemOrder.Labour; } }

            public void OnDue(Simulation sim, Job job)
            {
                PeopleState s = sim.State.People;
                Construction c = s.Constructions.Find(x => x.Id == job.TargetId);
                LandState land = sim.State.Land;
                var b = new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.People.HandHut, TileId = c.TileId, Cost = c.Cost };
                land.Buildings.Add(b);
                s.Constructions.Remove(c);
                sim.CompleteJob(job);
                sim.Emit(new HandHutBuilt(sim.State.Clock.SimTime, b.Id, c.TileId));
                ScheduleApplicant(sim);
            }
        }

        // ---- applicants (§3.5) ----

        /// <summary>
        /// Starts the wait for the next applicant when a hut stands empty and the queue has room (never cuts a wait short, but a new
        /// job post can bring it forward). Clears it otherwise.
        /// </summary>
        internal static void ScheduleApplicant(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (PeopleRules.EmptyHut(sim) == null || s.OutsidersFor(0) >= sim.Config.Game.People.Hand.MaxApplicants)
            {
                s.NextApplicantAt = 0;
                return;
            }
            long at = sim.State.Clock.SimTime + PeopleRules.ApplicantInterval(sim, s.NextApplicantId, s.Post != null);
            if (s.NextApplicantAt == 0 || at < s.NextApplicantAt) s.NextApplicantAt = at;
        }

        private static void Arrivals(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            if (s.NextApplicantAt == 0 || t < s.NextApplicantAt)
            {
                if (s.NextApplicantAt == 0) ScheduleApplicant(sim);
                return;
            }
            if (PeopleRules.EmptyHut(sim) == null || s.OutsidersFor(0) >= sim.Config.Game.People.Hand.MaxApplicants)
            {
                s.NextApplicantAt = 0;
                return;
            }
            Arrive(sim, 0, s.Post != null && s.Post.AboveMarket);
            s.NextApplicantAt = 0;
            ScheduleApplicant(sim);
        }

        /// <summary>A seeded profile (stream Applicants, key = applicant id): skill (in the place's role), two distinct traits, a name.</summary>
        internal static void Arrive(Simulation sim, long place, bool above)
        {
            PeopleDefs p = sim.Config.Game.People;
            PeopleState s = sim.State.People;
            long id = s.NextApplicantId++;
            int bonus = above ? p.JobPost.AboveMarketSkillBonus : 0;
            ulong seed = sim.State.Seed;
            int skill = p.Hand.SkillMin + bonus + (int)Rng.Below(Rng.Next(seed, RngStream.Applicants, (ulong)id, 1), (ulong)(p.Hand.SkillMax - p.Hand.SkillMin + 1));
            int t1 = (int)Rng.Below(Rng.Next(seed, RngStream.Applicants, (ulong)id, 2), (ulong)p.Traits.Count);
            int t2 = PeopleRules.SecondTrait(sim, Rng.Next(seed, RngStream.Applicants, (ulong)id, 3), t1);
            int name = (int)Rng.Below(Rng.Next(seed, RngStream.Applicants, (ulong)id, 4), (ulong)p.Names.Count);
            s.Applicants.Add(new Applicant
            {
                Id = id, Name = name, Skill = System.Math.Min(100, skill), Trait1 = t1, Trait2 = t2, AboveMarket = above, ArrivedAt = sim.State.Clock.SimTime,
                Place = place,
            });
            sim.Emit(new ApplicantArrived(sim.State.Clock.SimTime, id));
        }

        /// <summary>Applicants not hired within applicantWaitDays of live time leave.</summary>
        // ---- regulars (§3.3, ADR 0041) ----

        /// <summary>
        /// A neighbour hired for six full days asks to move in once a hut stands empty: they join the applicants (even a full
        /// queue) with their skill and both traits known, the first their day-labour trait. Once per person; live play only.
        /// </summary>
        private static void Regulars(Simulation sim)
        {
            PeopleDefs p = sim.Config.Game.People;
            PeopleState s = sim.State.People;
            if (PeopleRules.EmptyHut(sim) == null) return;
            long threshold = p.Pool.RegularThresholdDays * Day;
            ulong seed = sim.State.Seed;
            foreach (Neighbour n in s.Roster)
            {
                if (n.AskedToMoveIn || n.SecondsWorked < threshold) continue;
                n.AskedToMoveIn = true;
                long id = s.NextApplicantId++;
                int skill = p.Hand.SkillMin + (int)Rng.Below(Rng.Next(seed, RngStream.Applicants, (ulong)id, 1), (ulong)(p.Hand.SkillMax - p.Hand.SkillMin + 1));
                int t2 = PeopleRules.SecondTrait(sim, Rng.Next(seed, RngStream.Applicants, (ulong)id, 3), n.Trait);
                s.Applicants.Add(new Applicant
                {
                    Id = id, Name = n.Name, Skill = skill, Trait1 = n.Trait, Trait2 = t2, SecondTraitKnown = true, SkillKnown = true,
                    ArrivedAt = sim.State.Clock.SimTime, FromRosterId = n.Id,
                });
                sim.Emit(new RegularAsksToMoveIn(sim.State.Clock.SimTime, id, n.Id));
            }
        }

        /// <summary>A regular hired as the hand leaves the roster; a new neighbour takes their place, so the pool keeps its size.</summary>
        private static void ReplaceNeighbour(Simulation sim, long rosterId)
        {
            PeopleState s = sim.State.People;
            int at = s.Roster.FindIndex(n => n.Id == rosterId);
            if (at < 0) return;
            long id = 0;
            foreach (Neighbour n in s.Roster) id = System.Math.Max(id, n.Id);
            s.Roster.RemoveAt(at);
            var used = new bool[sim.Config.Game.People.Names.Count];
            foreach (Neighbour n in s.Roster) used[n.Name] = true;
            foreach (Hand h in s.Hands) used[h.Name] = true;
            s.Roster.Add(NewNeighbour(sim, id + 1, used));
        }

        private static void WaitingApplicants(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long wait = sim.Config.Game.People.Hand.ApplicantWaitDays * Day;
            bool left = false;
            for (int i = 0; i < s.Applicants.Count; i++)
            {
                Applicant a = s.Applicants[i];
                a.WaitedLiveSeconds += 3600;
                if (a.WaitedLiveSeconds < wait) continue;
                s.Applicants.RemoveAt(i--);
                sim.Emit(new ApplicantLeft(sim.State.Clock.SimTime, a.Id));
                left = true;
            }
            if (left && s.NextApplicantAt == 0) ScheduleApplicant(sim);
        }

        /// <summary>
        /// Takes the applicant on, the rest of the season prepaid (spec 006 §3.6): a migrant into the first empty hut; one of your
        /// townspeople stays in their own home (spec 015 §3.1), their exact farming skill kept.
        /// </summary>
        internal static Hand Hire(Simulation sim, Applicant a, long wage, long prepay)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            FarmSystem.Accrue(sim);
            Citizen town = a.CitizenId == 0 ? null : sim.State.Citizens.Citizen(a.CitizenId);
            var h = new Hand
            {
                Id = s.NextHandId++, Name = a.Name, Skill = town?.Skill ?? Fixed.FromInt(a.Skill), Trait1 = a.Trait1, Trait2 = a.Trait2,
                HutId = town == null ? PeopleRules.EmptyHut(sim).Id : 0, HiredAt = t, Wage = wage, Contract = ContractState.Active,
            };
            h.PrepaymentId = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, prepay, t, (t / S + 1) * S, "hand:" + h.Id).Id;
            s.Hands.Add(h);
            if (town == null) CitizenRules.HandHired(sim, h, a);
            else CitizenRules.TownHandHired(sim, h, town);
            s.Applicants.Remove(a);
            if (a.FromRosterId != 0) ReplaceNeighbour(sim, a.FromRosterId);
            s.Posts.RemoveAll(x => x.Place == 0); // a post lasts until a hand is hired
            FarmSystem.Refresh(sim);
            sim.Emit(new HandHired(t, a.Id, h.Id, wage, prepay));
            s.NextApplicantAt = 0;
            ScheduleApplicant(sim);
            return h;
        }

        // ---- contracts (§3.6, §3.7) ----

        /// <summary>
        /// Settlement step 6 (spec 005 §3.4), in hand id order: a contract still lapsed from the last close ends; a hand who has turned
        /// <c>retireAge</c> retires (spec 014 §3.2); an active one is renewed at the re-asked wage (a loyal hand keeps theirs; an ambitious one asks 10% more) if cash allows, else it lapses.
        /// Then skill grows for every hand under an active contract.
        /// </summary>
        internal static void SettleContracts(Simulation sim)
        {
            PeopleState s = sim.State.People;
            PeopleDefs p = sim.Config.Game.People;
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            FarmSystem.Accrue(sim);
            foreach (Hand h in new List<Hand>(s.Hands))
            {
                if (h.Contract == ContractState.Lapsed)
                {
                    Leave(sim, h);
                    sim.Emit(new HandLeft(t, h.Id));
                    continue;
                }
                if (CitizenRules.Retiring(sim, h))
                {
                    CitizenRules.Retire(sim, h); // spec 014 §3.2: his season done, he isn't renewed
                    continue;
                }
                long wage = RenewalWage(sim, h, out long housing);
                h.HousingInKind = housing; // spec 014 §3.6: the family house is part of his pay from this renewal
                if (sim.State.Ledger.CanAfford(wage))
                {
                    if (!PeopleRules.HasTrait(sim, h, x => x.KeepsWage)) h.Fairness = 0; // the asked wage paid (spec 015 §3.3)
                    h.Wage = wage;
                    h.PrepaymentId = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, wage, t, t + S, "hand:" + h.Id).Id;
                    sim.Emit(new ObligationPaid(t, "Wages", "hand:" + h.Id, wage));
                    sim.Emit(new ContractRenewed(t, h.Id, wage, wage));
                }
                else
                {
                    h.Wage = wage;
                    h.Contract = ContractState.Lapsed;
                    h.PrepaymentId = 0;
                    h.LapsedAt = t;
                    h.LapsedLiveSeconds = 0;
                    sim.Emit(new ObligationUnpaid(t, "Wages", "hand:" + h.Id, wage));
                    sim.Emit(new ContractLapsed(t, h.Id, wage, sim.State.Ledger.Cash));
                }
            }
            foreach (Hand h in s.Hands)
            {
                if (h.Contract != ContractState.Active) continue;
                h.SeasonsWorked++;
                Fixed gain = p.Hand.SkillGainPerSeason * p.Traits[h.Trait1].SkillGainFactor * p.Traits[h.Trait2].SkillGainFactor;
                Fixed skill = h.Skill + gain;
                h.Skill = skill > Fixed.FromInt(100) ? Fixed.FromInt(100) : skill;
                sim.Emit(new HandSkillGrew(t, h.Id, h.Skill));
            }
            FarmSystem.Refresh(sim);
        }

        /// <summary>
        /// The cash wage for the coming season: the asked wage for today's skill and price index, kept by a loyal hand, less the family
        /// house's rent while his family lives in one (<paramref name="housing"/>, spec 014 §3.6).
        /// </summary>
        internal static long RenewalWage(Simulation sim, Hand h, out long housing)
        {
            PeopleDefs p = sim.Config.Game.People;
            long wage;
            if (PeopleRules.HasTrait(sim, h, x => x.KeepsWage)) wage = h.Wage + h.HousingInKind;
            else
            {
                wage = PeopleRules.AskedWage(sim, h.Skill, false);
                Fixed factor = p.Traits[h.Trait1].RenewalWageFactor * p.Traits[h.Trait2].RenewalWageFactor;
                if (factor != Fixed.One) wage = Money.RoundToCoin(Money.MulRate(wage, factor));
            }
            housing = System.Math.Min(wage, MigrationRules.HousingInKind(sim, h));
            return wage - housing;
        }

        /// <summary>Lapsed hands wait lapseGraceDays of live time, then leave (never while the player is away).</summary>
        private static void Grace(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long grace = sim.Config.Game.People.Hand.LapseGraceDays * Day;
            foreach (Hand h in new List<Hand>(s.Hands))
            {
                if (h.Contract != ContractState.Lapsed) continue;
                h.LapsedLiveSeconds += 3600;
                if (h.LapsedLiveSeconds < grace) continue;
                Leave(sim, h);
                sim.Emit(new HandLeft(sim.State.Clock.SimTime, h.Id));
            }
        }

        /// <summary>The hand moves out: their orders go, the hut empties, the next applicant can come.</summary>
        internal static void Leave(Simulation sim, Hand h)
        {
            PeopleState s = sim.State.People;
            FarmSystem.Accrue(sim);
            s.Hands.Remove(h);
            CitizenRules.HandLeft(sim, h);
            s.Orders.RemoveAll(o => o.HandId == h.Id);
            FarmSystem.Refresh(sim);
            ScheduleApplicant(sim);
        }
    }
}
