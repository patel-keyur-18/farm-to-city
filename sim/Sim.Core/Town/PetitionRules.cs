using System;
using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// Petitions and approval (US-150, spec 026 §3.2, §3.3): at each close the town's needs are read, petitions are settled and raised, and approval moves.
    /// A petition is a need that three of your households (or a fifth of them) share; you promise it by a close or decline it, and doing the thing answers it.
    /// Approval (0–100) draws families or turns them away (the pull, §3.7) and changes nothing else.
    /// </summary>
    public static class PetitionRules
    {
        public static PetitionDef Def(Simulation sim) { return TownLifeRules.Def(sim)?.Petition; }

        /// <summary>Whether a household has the need a topic names now (§3.2).</summary>
        public static bool Needs(Simulation sim, PetitionDef d, int topic, Household x)
        {
            switch (topic)
            {
                case PetitionDef.Clinic: return ClinicRules.Effect(sim, x) <= Fixed.Zero;
                case PetitionDef.School: return HasUnschooledChild(sim, x);
                case PetitionDef.Hall: return CivicRules.Effect(sim, CivicDef.Hall, x) <= Fixed.Zero;
                case PetitionDef.Watchman: return CivicRules.Effect(sim, CivicDef.Watchman, x) <= Fixed.Zero;
                case PetitionDef.Playground: return HasChild(sim, x) && CivicRules.Effect(sim, CivicDef.Playground, x) <= Fixed.Zero;
                case PetitionDef.Water:
                    {
                        Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                        return b != null && b.TileId >= 0 && !UtilityRules.HasWater(sim, b.TileId);
                    }
                case PetitionDef.Rent: return x.Income > 0 && x.Rent > Money.MulRate(x.Income, d.RentShare);
                case PetitionDef.Work:
                    {
                        HappinessRules.Jobs(sim, x, out int inTown, out int adults);
                        return adults > 0 && inTown == 0;
                    }
            }
            return false;
        }

        private static bool HasChild(Simulation sim, Household x)
        {
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (c != null && HouseholdRules.IsChild(sim, c)) return true;
            }
            return false;
        }

        /// <summary>A child of school age who can't read and isn't enrolled (the school is shut, or too far).</summary>
        private static bool HasUnschooledChild(Simulation sim, Household x)
        {
            SchoolDef sd = SchoolRules.Def(sim);
            foreach (long id in x.Members)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (c == null || !c.Living) continue;
                int age = CitizenRules.Age(sim, c);
                if (age >= sd.PupilAgeMin && age <= sd.PupilAgeMax && !c.Literate && !(SchoolRules.IsOpen(sim) && SchoolRules.Covered(sim, c))) return true;
            }
            return false;
        }

        /// <summary>Families needed to raise one: <c>minHouseholds</c>, or a fifth of your households if that is more.</summary>
        public static int Needed(Simulation sim, PetitionDef d, int households)
        {
            return Math.Max(d.MinHouseholds, (int)CeilShare(households, d.MinShare));
        }

        private static long CeilShare(int households, Fixed share)
        {
            Fixed v = Fixed.FromInt(households) * share;
            long f = v.FloorToLong();
            return v > Fixed.FromInt((int)f) ? f + 1 : f;
        }

        /// <summary>How many of your households share the topic's need now, and the side of the town (0 north … 3 west, from the square) most of them live on.</summary>
        public static int Count(Simulation sim, PetitionDef d, int topic, out int side)
        {
            int n = 0;
            var sides = new int[4];
            MapDef map = sim.Config.Game.Map;
            int square = sim.Config.Game.Township.LandValue.SquareTile;
            foreach (Household x in TownLifeRules.Yours(sim))
            {
                if (!Needs(sim, d, topic, x)) continue;
                n++;
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b == null || b.TileId < 0) continue;
                int dx = map.X(b.TileId) - map.X(square), dy = map.Y(b.TileId) - map.Y(square);
                sides[Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 1 : 3) : (dy < 0 ? 0 : 2)]++;
            }
            side = 0;
            for (int i = 1; i < 4; i++) if (sides[i] > sides[side]) side = i;
            return n;
        }

        // ---- the close ----

        /// <summary>At each close (after the town is read): settle what was answered or left, move approval, then raise new petitions.</summary>
        internal static void AtClose(Simulation sim, TownLifeDef td, TownLifeState s)
        {
            PetitionDef d = td.Petition;
            long now = sim.State.Clock.SimTime;
            int households = TownLifeRules.Yours(sim).Count;
            int needed = Needed(sim, d, households);
            int points = 0;

            foreach (TownPetition p in s.Petitions)
            {
                if (p.Ended) continue;
                int count = Count(sim, d, p.Topic, out _);
                bool gone = count < needed;
                if (p.Status == PetitionStatus.Open)
                {
                    if (gone) points += End(sim, d, s, p, PetitionStatus.Done, d.Done, d.QuietCloses, now);
                    else if (s.Closes - p.RaisedClose >= d.IgnoredAfter) points += End(sim, d, s, p, PetitionStatus.Ignored, d.Ignored, d.QuietCloses, now);
                    else p.Households = count;
                }
                else if (s.Closes >= p.PromisedBy)
                    points += gone ? End(sim, d, s, p, PetitionStatus.Kept, d.Kept, d.QuietCloses, now) : End(sim, d, s, p, PetitionStatus.Broken, d.Broken, d.QuietCloses, now);
                else p.Households = count;
            }

            Fixed from = s.Approval;
            s.Approval = Clamp100(Fixed.FromInt((int)Step(d, HappinessRules.Town(sim))) + s.Approval + Fixed.FromInt(points));
            s.ApprovalHistory.Add((int)s.Approval.RoundToLong());
            while (s.ApprovalHistory.Count > d.KeepHistory) s.ApprovalHistory.RemoveAt(0);
            if (s.Approval != from) sim.Emit(new ApprovalMoved(now, from.RoundToLong(), s.Approval.RoundToLong()));

            for (int topic = 0; topic < PetitionDef.Topics; topic++)
            {
                if (s.Closes <= s.QuietUntil[topic] || Standing(s, topic) != null) continue;
                int count = Count(sim, d, topic, out int side);
                if (count < needed) continue;
                var p = new TownPetition { Id = s.NextPetitionId++, Topic = topic, Side = side, Households = count, RaisedAt = now, RaisedClose = s.Closes, Status = PetitionStatus.Open };
                s.Petitions.Add(p);
                sim.Emit(new PetitionRaised(now, p.Id, topic, side, count));
            }
            Trim(s, d);
        }

        /// <summary>The petition for a topic still waiting for an answer or a date.</summary>
        public static TownPetition Standing(TownLifeState s, int topic)
        {
            foreach (TownPetition p in s.Petitions) if (p.Topic == topic && !p.Ended) return p;
            return null;
        }

        private static int End(Simulation sim, PetitionDef d, TownLifeState s, TownPetition p, PetitionStatus how, int points, int quiet, long now)
        {
            p.Status = how;
            p.ClosedAt = now;
            s.QuietUntil[p.Topic] = s.Closes + quiet;
            sim.Emit(new PetitionEnded(now, p.Id, p.Topic, how, points));
            return points;
        }

        /// <summary>(happiness − neutral) ÷ step, toward zero, at most ±stepMax (§3.3).</summary>
        public static long Step(PetitionDef d, Fixed happiness)
        {
            Fixed v = (happiness - Fixed.FromInt(d.NeutralHappiness)).MulRatio(1, d.Step);
            long n = v >= Fixed.Zero ? v.FloorToLong() : -(Fixed.Zero - v).FloorToLong();
            return Math.Max(-d.StepMax, Math.Min(d.StepMax, n));
        }

        private static Fixed Clamp100(Fixed v) { return v < Fixed.Zero ? Fixed.Zero : v > Fixed.FromInt(100) ? Fixed.FromInt(100) : v; }

        /// <summary>Keeps every standing petition and the last few that ended, oldest out first.</summary>
        private static void Trim(TownLifeState s, PetitionDef d)
        {
            int ended = 0;
            for (int i = s.Petitions.Count - 1; i >= 0; i--)
                if (s.Petitions[i].Ended && ++ended > d.KeepEnded) s.Petitions.RemoveAt(i);
        }

        // ---- your answer ----

        /// <summary>Promise a petition by <paramref name="closes"/> closes (one of the content's choices), or decline it with 0 (§3.2). A promise can't be withdrawn.</summary>
        internal static RejectReason Answer(Simulation sim, int id, int closes)
        {
            if (!TownLifeRules.Known(sim)) return RejectReason.InvalidStep;
            TownLifeState s = TownLifeRules.State(sim);
            PetitionDef d = Def(sim);
            TownPetition p = s.Petitions.Find(x => x.Id == id);
            if (p == null) return RejectReason.UnknownPetition;
            if (p.Status != PetitionStatus.Open) return RejectReason.PetitionClosed;
            long now = sim.State.Clock.SimTime;
            if (closes == 0)
            {
                Fixed from = s.Approval;
                s.Approval = Clamp100(s.Approval + Fixed.FromInt(d.Declined));
                End(sim, d, s, p, PetitionStatus.Declined, d.Declined, d.DeclinedQuietCloses, now);
                if (s.Approval != from) sim.Emit(new ApprovalMoved(now, from.RoundToLong(), s.Approval.RoundToLong()));
                return RejectReason.None;
            }
            if (!Contains(d.PromiseCloses, closes)) return RejectReason.InvalidArgument;
            p.Status = PetitionStatus.Promised;
            p.PromisedBy = s.Closes + closes;
            return RejectReason.None;
        }

        private static bool Contains(IReadOnlyList<int> list, int v) { foreach (int n in list) if (n == v) return true; return false; }
    }
}
