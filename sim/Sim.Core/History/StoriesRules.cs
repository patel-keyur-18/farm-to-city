using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Market;
using Sim.Core.People;

namespace Sim.Core.History
{
    /// <summary>
    /// Citizen stories (spec 029 §3.6, ADR 0165, 0167): four arcs of two to four steps over what the town already does. At most one step a
    /// season across the whole town, in the order stall, first reader, migrant family, long life, the lowest citizen id first. A hand's stall
    /// is founded by the story itself, so a hand who gives notice is never left without one (gap 11).
    /// </summary>
    public static class StoriesRules
    {
        public static StoriesState State(Simulation sim) { return sim.State.Chronicle?.Stories; }

        public static StoriesDefs Defs(Simulation sim) { return sim.Config.Game.Stories; }

        private static bool On(Simulation sim)
        {
            return ChronicleRules.On(sim) && Defs(sim) != null && sim.State.Citizens != null && sim.State.People != null && sim.State.Businesses != null && sim.State.Shops != null;
        }

        /// <summary>The season that opens at this close.</summary>
        private static long Season(Simulation sim) { return sim.Period; }

        private static StoryArcState Find(StoriesState st, long citizen, StoryArc arc, bool active)
        {
            StoryArcState found = st.Arcs.Find(a => a.Citizen == citizen && a.Arc == arc && (!active || !a.Done));
            if (found != null || active || !st.Retired.Contains(citizen * 8 + (int)arc)) return found;
            return new StoryArcState { Citizen = citizen, Arc = arc, Step = 99, Done = true }; // a finished arc that was pruned: it happened once and never again
        }

        /// <summary>Whether the person has an arc going (one at a time for the same person).</summary>
        public static bool InArc(Simulation sim, long citizen)
        {
            StoriesState st = State(sim);
            // A migrant family at step 2 only waits for a child, who may never come: it must not keep the family's head from a stall or a long life (review of Sprint 26).
            return st != null && st.Arcs.Exists(a => a.Citizen == citizen && !a.Done && !(a.Arc == StoryArc.Migrant && a.Step >= 2));
        }

        /// <summary>The stall arc a person has given notice in (step 1, not yet gone), or null: the People sheet says they will leave at the close.</summary>
        public static StoryArcState Notice(Simulation sim, long citizen)
        {
            StoriesState st = State(sim);
            return st?.Arcs.Find(a => a.Citizen == citizen && a.Arc == StoryArc.Stall && a.Step == 1 && !a.Done);
        }

        // ---- who may leave to start a stall ----

        private static Hand HandOf(Simulation sim, Citizen c)
        {
            return c.Job == CitizenJob.Hand ? sim.State.People.Hands.Find(x => x.Id == c.HandId && x.HutId == 0) : null; // a townsperson who is your hand: nobody is moved out of a hut
        }

        private static Worker WorkerOf(Simulation sim, Citizen c, out Business.Business b)
        {
            b = c.Job == CitizenJob.Business ? sim.State.Businesses.Find(c.Employer) : null;
            Worker w = b == null ? null : sim.State.People.WorkerAt(b.Id);
            return w != null && w.CitizenId == c.Id ? w : null;
        }

        /// <summary>The trade of the stall: a tea stall's worker opens a tea stall, a sweet shop's a sweet shop, any other hand a vegetable stall.</summary>
        private static ShopKind TradeOf(Simulation sim, Business.Business b)
        {
            ShopKind k = b == null ? ShopKind.Veg : BusinessRules.TypeOf(sim, b).Kind;
            return k == ShopKind.Chai || k == ShopKind.Sweets ? k : ShopKind.Veg;
        }

        /// <summary>The person works for you, and for how many seasons, with their staff's motivation; else false.</summary>
        private static bool Staff(Simulation sim, Citizen c, out int seasons, out Fixed motivation, out ShopKind trade)
        {
            seasons = 0; motivation = Fixed.Zero; trade = ShopKind.Veg;
            Hand h = HandOf(sim, c);
            if (h != null) { seasons = h.SeasonsWorked; motivation = WorkRules.Motivation(sim, h); return true; }
            Worker w = WorkerOf(sim, c, out Business.Business b);
            if (w == null) return false;
            seasons = w.SeasonsWorked; motivation = WorkRules.Motivation(sim, w); trade = TradeOf(sim, b);
            return true;
        }

        /// <summary>The gate for giving notice (§3.6): worked for you long enough, savings put aside, paid fairly, and a stall of the trade can be founded.</summary>
        public static bool Eligible(Simulation sim, Citizen c)
        {
            StoriesDefs d = Defs(sim);
            if (!Town.TownshipRules.Reached(sim)) return false; // spec 029 §3.6 (ADR 0168): not before the Township, or a free rival could stand in the way of the gate's three businesses
            if (!c.Living || InArc(sim, c.Id) || Find(State(sim), c.Id, StoryArc.Stall, false) != null) return false;
            if (!Staff(sim, c, out int seasons, out Fixed motivation, out ShopKind trade)) return false;
            Household x = sim.State.Citizens.Household(c.HouseholdId);
            return x != null && seasons >= d.StallSeasons && x.Savings >= MarketRules.Indexed(sim, d.StallSavings) && motivation >= d.StallMotivation && ShopRules.CanFound(sim, trade) && !WaitsForFirstProfit(sim, trade);
        }

        /// <summary>
        /// A shop of yours of the trade that has not yet had a profitable season (spec 029 §3.6, ADR 0168): a citizen's stall never opens against it, so the Township gate's "3 businesses each with a profitable
        /// season" is never lost to a rival that came before your shop had its chance (measured: 3 of 48 competent towns never reached the Township, 24 of 200).
        /// </summary>
        public static bool WaitsForFirstProfit(Simulation sim, ShopKind trade)
        {
            foreach (Business.Business b in sim.State.Businesses.Businesses)
                if (b.Status != BusinessStatus.Closed && b.ProfitableSeasons == 0 && BusinessRules.TypeOf(sim, b).Kind == trade) return true;
            return false;
        }

        // ---- the close ----

        /// <summary>
        /// Before the shops' close: a hand or worker who gave notice at the last close goes now (no severance, their post empty) and the story
        /// founds the stall (step 2). Before the village's own newcomers roll, so the kind is still free: nobody is stranded.
        /// </summary>
        internal static void Leaving(Simulation sim)
        {
            if (!On(sim)) return;
            StoriesState st = State(sim);
            long season = Season(sim), now = sim.State.Clock.SimTime;
            foreach (StoryArcState a in st.Arcs)
            {
                if (a.Done || a.Arc != StoryArc.Stall || a.Step != 1 || a.At >= season) continue;
                Citizen c = sim.State.Citizens.Citizen(a.Citizen);
                var kind = (ShopKind)a.Other;
                Hand h = c == null ? null : HandOf(sim, c);
                Worker w = c == null || h != null ? null : WorkerOf(sim, c, out _);
                if (c == null || !c.Living || (h == null && w == null) || !ShopRules.CanFound(sim, kind)) { a.Done = true; continue; } // their post changed: the notice lapses
                if (WaitsForFirstProfit(sim, kind)) continue; // a shop of yours of his trade opened since the notice and has had no profitable season: he stays on another season
                if (h != null) { PeopleSystem.Leave(sim, h); sim.Emit(new HandLeft(now, h.Id)); }
                else
                {
                    Business.Business b = sim.State.Businesses.Find(c.Employer);
                    long workerId = w.Id;
                    BusinessRules.LoseWorker(sim, b);
                    sim.Emit(new WorkerLeft(now, b.Id, workerId, 0));
                }
                if (!c.Living) { a.Done = true; continue; }
                ShopRules.FoundOwned(sim, kind, c.Id);
                Step(sim, a, 2, 0, now);
                break; // one step a season
            }
        }

        private struct Cand
        {
            public int Priority; public long Citizen; public int Step; public StoryArcState Arc; public int N; public int Other; public StoryArc Kind;
        }

        /// <summary>After the shops' close: every step now due, the first in order taken if the town has not had its step this season.</summary>
        internal static void AtClose(Simulation sim)
        {
            if (!On(sim)) return;
            StoriesState st = State(sim);
            long season = Season(sim), now = sim.State.Clock.SimTime;
            while (st.Arcs.Count > 64)
            {
                int old = st.Arcs.FindIndex(a => a.Done); // the oldest finished ones go past 64
                if (old < 0) break;
                StoryArcState gone = st.Arcs[old];
                st.Retired.Add(gone.Citizen * 8 + (int)gone.Arc);
                st.Arcs.RemoveAt(old);
            }
            if (st.LastStep == season + 1) return;
            var list = new List<Cand>();
            Stalls(sim, st, season, list);
            Readers(sim, st, season, list);
            Migrants(sim, st, season, list);
            LongLives(sim, st, season, list);
            if (list.Count == 0) return;
            Cand best = list[0];
            foreach (Cand c in list)
                if (c.Priority < best.Priority || (c.Priority == best.Priority && c.Citizen < best.Citizen)) best = c;
            StoryArcState arc = best.Arc ?? new StoryArcState { Citizen = best.Citizen, Arc = best.Kind, Other = best.Other };
            if (best.Arc == null) st.Arcs.Add(arc);
            Step(sim, arc, best.Step, best.N, now);
        }

        private static void Stalls(Simulation sim, StoriesState st, long season, List<Cand> list)
        {
            StoriesDefs d = Defs(sim);
            foreach (StoryArcState a in st.Arcs)
            {
                if (a.Done || a.Arc != StoryArc.Stall || a.Step != 2 || season < a.At + d.StallJudgedAfter) continue;
                bool open = sim.State.Shops.Newcomers.Exists(x => x.Owner == a.Citizen);
                list.Add(new Cand { Priority = 0, Citizen = a.Citizen, Step = 3, Arc = a, N = open ? 1 : 0 });
            }
            if (sim.State.Clock.IsAway) return; // nobody gives notice while you are away
            foreach (Citizen c in sim.State.Citizens.Citizens)
            {
                if (!Eligible(sim, c)) continue;
                if (!Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Stories, (ulong)c.Id, (ulong)season), d.StallChance)) continue;
                Staff(sim, c, out _, out _, out ShopKind trade);
                list.Add(new Cand { Priority = 0, Citizen = c.Id, Step = 1, Kind = StoryArc.Stall, Other = (int)trade });
            }
        }

        private static void Readers(Simulation sim, StoriesState st, long season, List<Cand> list)
        {
            StoriesDefs d = Defs(sim);
            CitizensState cs = sim.State.Citizens;
            foreach (StoryArcState a in st.Arcs)
            {
                if (a.Done || a.Arc != StoryArc.Reader) continue;
                Citizen c = cs.Citizen(a.Citizen);
                if (c == null || !c.Living) { a.Done = true; continue; }
                if (a.Step == 1 && c.Literate) list.Add(new Cand { Priority = 1, Citizen = c.Id, Step = 2, Arc = a });
                else if (a.Step == 2 && CitizenRules.Age(sim, c) >= d.ReaderAdultAge && (c.Job == CitizenJob.Hand || c.Job == CitizenJob.Business))
                    list.Add(new Cand { Priority = 1, Citizen = c.Id, Step = 3, Arc = a });
            }
            foreach (Citizen c in cs.Citizens)
            {
                if (!c.Living || c.Literate || c.SchoolYears <= 0 || Find(st, c.Id, StoryArc.Reader, false) != null || InArc(sim, c.Id)) continue;
                Household x = cs.Household(c.HouseholdId);
                if (x == null || x.Members.Exists(m => m != c.Id && cs.Citizen(m)?.Literate == true)) continue; // nobody in the family can read yet
                list.Add(new Cand { Priority = 1, Citizen = c.Id, Step = 1, Kind = StoryArc.Reader });
            }
        }

        private static long ArrivedAt(Citizen c)
        {
            foreach (StoryLine s in c.Story) if (s.Kind == StoryKind.Arrived) return s.At;
            return -1;
        }

        /// <summary>The one a migrant household's arc is kept on: its lowest citizen id among those who arrived, when two or more did (a family, not a lone hand).</summary>
        private static Citizen Head(Simulation sim, Household x)
        {
            Citizen head = null;
            int arrived = 0;
            foreach (long id in x.Members)
            {
                Citizen m = sim.State.Citizens.Citizen(id);
                if (m == null || !m.Living || ArrivedAt(m) < 0) continue;
                arrived++;
                if (head == null) head = m;
            }
            return arrived >= 2 ? head : null;
        }

        private static void Migrants(Simulation sim, StoriesState st, long season, List<Cand> list)
        {
            StoriesDefs d = Defs(sim);
            CitizensState cs = sim.State.Citizens;
            foreach (StoryArcState a in st.Arcs)
            {
                if (a.Done || a.Arc != StoryArc.Migrant) continue;
                Citizen c = cs.Citizen(a.Citizen);
                if (c == null || !c.Living) { a.Done = true; continue; }
                long arrivedSeason = ArrivedAt(c) / sim.Clock.SecondsPerSeason;
                if (a.Step == 1 && season - arrivedSeason >= d.MigrantSecond) list.Add(new Cand { Priority = 2, Citizen = c.Id, Step = 2, Arc = a });
                else if (a.Step == 2 && BornSince(cs, cs.Household(c.HouseholdId), ArrivedAt(c))) list.Add(new Cand { Priority = 2, Citizen = c.Id, Step = 3, Arc = a });
            }
            foreach (Household x in cs.Households)
            {
                Citizen head = Head(sim, x);
                if (head == null || Find(st, head.Id, StoryArc.Migrant, false) != null || InArc(sim, head.Id)) continue;
                if (season - ArrivedAt(head) / sim.Clock.SecondsPerSeason >= d.MigrantFirst) list.Add(new Cand { Priority = 2, Citizen = head.Id, Step = 1, Kind = StoryArc.Migrant });
            }
        }

        private static bool BornSince(CitizensState cs, Household x, long at)
        {
            if (x == null) return false;
            foreach (long id in x.Members)
            {
                Citizen m = cs.Citizen(id);
                if (m != null && m.Story.Exists(s => s.Kind == StoryKind.ChildBorn && s.At > at)) return true;
            }
            return false;
        }

        private static void LongLives(Simulation sim, StoriesState st, long season, List<Cand> list)
        {
            StoriesDefs d = Defs(sim);
            CitizensState cs = sim.State.Citizens;
            foreach (Citizen c in cs.Citizens)
            {
                int age = CitizenRules.Age(sim, c);
                StoryArcState a = Find(st, c.Id, StoryArc.LongLife, false);
                if (c.Living)
                {
                    if (a != null && a.Done) continue;
                    int next = a == null ? 0 : a.Step;
                    if (next >= d.LifeAges.Count || age < d.LifeAges[next] || (a == null && InArc(sim, c.Id))) continue;
                    list.Add(new Cand { Priority = 3, Citizen = c.Id, Step = next + 1, Arc = a, N = age, Kind = StoryArc.LongLife });
                }
                else if (c.Gone == CitizenGone.Died && age >= d.LifeDiedFrom && (a == null || !a.Done) && season - c.GoneAt / sim.Clock.SecondsPerSeason <= 2)
                {
                    Household x = cs.Household(c.HouseholdId);
                    int family = x == null ? 0 : x.Members.FindAll(m => cs.Citizen(m)?.Living == true).Count;
                    list.Add(new Cand { Priority = 3, Citizen = c.Id, Step = 4, Arc = a, N = age * 1000 + family, Kind = StoryArc.LongLife });
                }
            }
        }

        /// <summary>Takes a step: the arc moves, the season's step is spent, the Chronicle gets its line and the toast its event.</summary>
        private static void Step(Simulation sim, StoryArcState a, int step, int n, long now)
        {
            StoriesState st = State(sim);
            a.Step = step; a.At = Season(sim);
            if (a.Arc == StoryArc.Stall && step == 3) a.Done = true;
            if (a.Arc == StoryArc.Reader && step == 3) a.Done = true;
            if (a.Arc == StoryArc.Migrant && step == 3) a.Done = true;
            if (a.Arc == StoryArc.LongLife && step == 4) a.Done = true;
            st.LastStep = Season(sim) + 1;
            string kind = a.Arc == StoryArc.Stall ? ((ShopKind)a.Other).ToString().ToLowerInvariant() : "";
            ChronicleRules.Add(sim, ChronicleKind.Story, now, a.Citizen, Code(a.Arc, step, n), kind);
            sim.Emit(new StoryStepped(now, (int)a.Arc, step, a.Citizen, n, kind));
        }

        /// <summary>A Story line's code: arc × 1,000,000 + step × 100,000 + n (an age, a family count or whether the stall stands).</summary>
        public static long Code(StoryArc arc, int step, int n) { return (long)arc * 1_000_000 + step * 100_000L + n; }
    }
}
