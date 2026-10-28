using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>What a contract is offered at (spec 015 §3.3).</summary>
    public enum OfferLevel
    {
        Asked = 0,

        /// <summary>10% below the asked wage: may refuse, once countered at the asked wage.</summary>
        Low = 1,

        /// <summary>10% above it: surer, and remembered as fair pay.</summary>
        High = 2,
    }

    /// <summary>
    /// One hiring board for every role (spec 015 §3.1–3.3): a place is 0 for your fields (a farm hand) or a business's id (its
    /// miller, baker or dairyman). Your townspeople apply when you post, known exactly; migrants come at the post's pace, a
    /// business's commuting from a neighbouring village.
    /// </summary>
    public static class HiringRules
    {
        private const long Hour = 3600;

        // Draws of the Applicants stream (key = applicant id): 0 the wait, 1–4 the profile (PeopleSystem), 5 the first offer, 6 after a counter.
        private const ulong FirstOffer = 5, AfterCounter = 6;

        /// <summary>A business's role reads farming (the dairyman) or crafting (the miller, the baker); the farm hand farming.</summary>
        public static bool Farms(Simulation sim, long place)
        {
            return place == 0 || BusinessRules.TypeOf(sim, sim.State.Businesses.Find(place)).WorkerFarms;
        }

        /// <summary>A citizen's skill in the place's role, rounded.</summary>
        public static int SkillFor(Simulation sim, Citizen c, long place)
        {
            return (int)(Farms(sim, place) ? c.Skill : c.Crafting).RoundToLong();
        }

        /// <summary>The role's market wage a season at skill 50, year-0 (spec 015 §3.1).</summary>
        public static long MarketWage(Simulation sim, long place)
        {
            return place == 0 ? sim.Config.Game.People.Hand.MarketWage : BusinessRules.TypeOf(sim, sim.State.Businesses.Find(place)).Wage;
        }

        /// <summary>The asked wage (spec 015 §3.3): the role's market wage × (wageBase + wagePerSkill × skill ÷ 100) × the price index; × 1.10 through an above-market post.</summary>
        public static long AskedWage(Simulation sim, long place, Fixed skill, bool aboveMarket)
        {
            return PeopleRules.AskedWage(sim, MarketWage(sim, place), skill, aboveMarket);
        }

        /// <summary>The wage at an offer level: the asked wage, or 10% less or more, in whole coins.</summary>
        public static long OfferWage(Simulation sim, Applicant a, OfferLevel level)
        {
            long asked = AskedWage(sim, a.Place, Fixed.FromInt(a.Skill), a.AboveMarket);
            HiringDef h = sim.Config.Game.People.Hiring;
            if (level == OfferLevel.Asked) return asked;
            return Money.RoundToCoin(Money.MulRate(asked, level == OfferLevel.Low ? h.LowFactor : h.HighFactor));
        }

        /// <summary>
        /// The chance an offer is taken (spec 015 §3.3): regulars always (ADR 0041); a townsperson always at the asked wage or more, 0.6
        /// below it; after a counter, the asked wage or more is taken; else 0.85 at the asked wage (0.95 through an above-market post),
        /// 0.95 above it, 0.40 below it.
        /// </summary>
        public static Fixed AcceptChance(Simulation sim, Applicant a, OfferLevel level)
        {
            PeopleDefs p = sim.Config.Game.People;
            if (a.FromRosterId != 0) return Fixed.One;
            if (level == OfferLevel.Low) return a.CitizenId != 0 ? p.Hiring.TownLowAccept : p.Hiring.LowAccept;
            if (a.CitizenId != 0 || a.Countered) return Fixed.One;
            if (level == OfferLevel.High) return p.Hiring.HighAccept;
            return a.AboveMarket ? p.JobPost.AboveMarketAccept : p.Hand.AcceptChance;
        }

        /// <summary>The shown skill range: exact for townspeople and regulars, ±5 once a reference is back, else ±15 (spec 015 §3.2).</summary>
        public static int RangeHalfWidth(Simulation sim, Applicant a)
        {
            if (a.SkillKnown) return 0;
            return a.ReferenceDone ? sim.Config.Game.People.Hiring.ReferenceHalfWidth : sim.Config.Game.People.Hand.SkillRangeHalfWidth;
        }

        /// <summary>What the reference says (spec 015 §3.2): "was let go" when either trait is lazy or careless, so it hints at a hidden one.</summary>
        public static bool LeftOnGoodTerms(Simulation sim, Applicant a)
        {
            var traits = sim.Config.Game.People.Traits;
            return !Poor(traits[a.Trait1]) && !Poor(traits[a.Trait2]);
        }

        private static bool Poor(TraitDef t) { return t.Careless || t.HoursFactor < Fixed.One; }

        /// <summary>A business can take a hire: yours, not closed, with nobody hired there (you may be running it).</summary>
        public static RejectReason BusinessPlace(Simulation sim, long place)
        {
            Business.Business b = sim.State.Businesses?.Find(place);
            if (b == null || b.Status == BusinessStatus.Closed) return RejectReason.UnknownBusiness;
            if (b.Worker == WorkerKind.Citizen || b.Worker == WorkerKind.Neighbour) return RejectReason.AlreadyStaffed;
            return RejectReason.None;
        }

        /// <summary>Your townspeople who could take the job: adults without a town job, not already applying for it, most skilled first.</summary>
        public static List<Citizen> TownCandidates(Simulation sim, long place)
        {
            var list = new List<Citizen>();
            if (sim.State.Citizens == null) return list;
            PeopleState s = sim.State.People;
            foreach (Citizen c in sim.State.Citizens.Citizens)
                if (HouseholdRules.WorksOutside(sim, c) && s.Applicants.Find(a => a.CitizenId == c.Id && a.Place == place) == null) list.Add(c);
            list.Sort((x, y) =>
            {
                int k = SkillFor(sim, y, place).CompareTo(SkillFor(sim, x, place));
                return k != 0 ? k : x.Id.CompareTo(y.Id);
            });
            return list;
        }

        /// <summary>When you post, your townspeople hear at once and apply, the most skilled first, up to maxApplicants of them (spec 015 §3.1).</summary>
        internal static void TownApply(Simulation sim, long place)
        {
            PeopleState s = sim.State.People;
            int room = sim.Config.Game.People.Hand.MaxApplicants - (s.ApplicantsFor(place).Count - s.OutsidersFor(place));
            foreach (Citizen c in TownCandidates(sim, place))
            {
                if (room-- <= 0) break;
                long id = s.NextApplicantId++;
                s.Applicants.Add(new Applicant
                {
                    Id = id, Name = c.Name, Skill = SkillFor(sim, c, place), Trait1 = c.Trait1, Trait2 = c.Trait2, SecondTraitKnown = true, SkillKnown = true,
                    ArrivedAt = sim.State.Clock.SimTime, Place = place, CitizenId = c.Id, AboveMarket = s.PostAt(place)?.AboveMarket ?? false,
                });
                sim.Emit(new ApplicantArrived(sim.State.Clock.SimTime, id));
            }
        }

        /// <summary>
        /// Hourly, live only: a business's post brings a migrant every applicantEveryHours ± jitter (twice as fast above the market),
        /// up to maxApplicants of them waiting (spec 015 §3.1). A farm hand's post works as spec 006 (PeopleSystem).
        /// </summary>
        internal static void BusinessArrivals(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            int max = sim.Config.Game.People.Hand.MaxApplicants;
            foreach (JobPost post in s.Posts)
            {
                if (post.Place == 0) continue;
                bool room = s.OutsidersFor(post.Place) < max;
                if (room && post.NextApplicantAt != 0 && t >= post.NextApplicantAt)
                {
                    PeopleSystem.Arrive(sim, post.Place, post.AboveMarket);
                    post.NextApplicantAt = 0;
                    room = s.OutsidersFor(post.Place) < max;
                }
                if (!room) post.NextApplicantAt = 0;
                else if (post.NextApplicantAt == 0) post.NextApplicantAt = t + PeopleRules.ApplicantInterval(sim, s.NextApplicantId, post.AboveMarket);
            }
        }

        /// <summary>Reference checks come back after their 12 hours, live or away (spec 015 §3.10).</summary>
        internal static void References(Simulation sim)
        {
            long t = sim.State.Clock.SimTime;
            foreach (Applicant a in sim.State.People.Applicants)
            {
                if (a.ReferenceAt == 0 || a.ReferenceDone || t < a.ReferenceAt) continue;
                a.ReferenceDone = true;
                sim.Emit(new ReferenceChecked(t, a.Id, LeftOnGoodTerms(sim, a)));
            }
        }

        /// <summary>Posts past their days end (applicants already here stay till their wait runs out).</summary>
        internal static void Expire(Simulation sim)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            for (int i = 0; i < s.Posts.Count; i++)
            {
                if (t < s.Posts[i].ExpiresAt) continue;
                long place = s.Posts[i].Place;
                s.Posts.RemoveAt(i--);
                sim.Emit(new JobPostExpired(t, place));
            }
        }

        internal static void AddPost(PeopleState s, JobPost post)
        {
            int i = 0;
            while (i < s.Posts.Count && s.Posts[i].Place < post.Place) i++;
            s.Posts.Insert(i, post);
        }

        /// <summary>A townsperson takes a job: their other applications go.</summary>
        private static void Withdraw(PeopleState s, long citizenId)
        {
            if (citizenId != 0) s.Applicants.RemoveAll(a => a.CitizenId == citizenId);
        }

        /// <summary>
        /// The offer (spec 015 §3.3), after the command's checks: refused below the asked wage the first time, they counter at it and
        /// wait; any other refusal and they leave. Taken, they're hired at the offered wage, remembered as fair or not.
        /// </summary>
        internal static void Offer(Simulation sim, Applicant a, OfferLevel level, long wage)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            ulong draw = Rng.Next(sim.State.Seed, RngStream.Applicants, (ulong)a.Id, a.Countered ? AfterCounter : FirstOffer);
            if (!Rng.Chance(draw, AcceptChance(sim, a, level)))
            {
                if (level == OfferLevel.Low && !a.Countered)
                {
                    a.Countered = true;
                    sim.Emit(new CounterOffer(t, a.Id, OfferWage(sim, a, OfferLevel.Asked)));
                    return;
                }
                s.Applicants.Remove(a);
                sim.Emit(new OfferDeclined(t, a.Id));
                if (a.Place == 0) PeopleSystem.ScheduleApplicant(sim);
                return;
            }
            int fairness = level == OfferLevel.Low ? -1 : level == OfferLevel.High ? 1 : 0;
            Withdraw(s, a.CitizenId);
            if (a.Place == 0)
            {
                long prepay = PeopleRules.RestOfSeason(sim, wage);
                PeopleSystem.Hire(sim, a, wage, prepay).Fairness = fairness;
                return;
            }
            HireWorker(sim, a, wage, fairness);
        }

        /// <summary>
        /// A business's worker starts (spec 015 §3.1): the rest of the season's wage paid upfront from its cash (on its overdraft if need
        /// be, checked by the command); you stop running it if you were. The place's post and other applicants go.
        /// </summary>
        internal static void HireWorker(Simulation sim, Applicant a, long wage, int fairness)
        {
            PeopleState s = sim.State.People;
            long t = sim.State.Clock.SimTime;
            Business.Business b = sim.State.Businesses.Find(a.Place);
            Citizen c = a.CitizenId == 0 ? null : sim.State.Citizens.Citizen(a.CitizenId);
            var w = new Worker
            {
                Id = s.NextWorkerId++, Place = b.Id, CitizenId = a.CitizenId, Name = a.Name,
                Skill = c == null ? Fixed.FromInt(a.Skill) : Farms(sim, b.Id) ? c.Skill : c.Crafting,
                Trait1 = a.Trait1, Trait2 = a.Trait2, Wage = wage, Fairness = fairness, HiredAt = t,
            };
            s.Workers.Add(w);
            Farm.FarmSystem.Accrue(sim); // you may stop running it: your hours go back to the farm
            long prepay = PeopleRules.RestOfSeason(sim, wage);
            BusinessBooks.Draw(sim, b, prepay);
            BusinessRules.PayWage(sim, b, prepay);
            b.Worker = c == null ? WorkerKind.Neighbour : WorkerKind.Citizen;
            b.WorkerCitizen = a.CitizenId;
            if (c != null)
            {
                c.Job = CitizenJob.Business;
                c.Employer = b.Id;
                c.Story.Add(new StoryLine(StoryKind.Hired, t, 0));
                HouseholdRules.SetBudget(sim, sim.State.Citizens.Household(c.HouseholdId)); // a wage now (spec 014 §3.5)
            }
            s.Applicants.RemoveAll(x => x.Place == b.Id);
            s.Posts.RemoveAll(x => x.Place == b.Id);
            Farm.FarmSystem.Refresh(sim);
            sim.Emit(new WorkerHired(t, b.Id, w.Id, wage, prepay));
        }

        /// <summary>The worker goes (left unpaid, let go, retired or gone): a townsperson back to outside work with their skill kept.</summary>
        internal static void WorkerGone(Simulation sim, Business.Business b)
        {
            PeopleState s = sim.State.People;
            Worker w = s?.WorkerAt(b.Id);
            if (w == null) return;
            s.Workers.Remove(w);
            Citizen c = w.CitizenId == 0 ? null : sim.State.Citizens?.Citizen(w.CitizenId);
            if (c == null) return;
            if (Farms(sim, b.Id)) c.Skill = w.Skill;
            else c.Crafting = w.Skill;
        }

        /// <summary>
        /// The new season's wage (spec 015 §3.3, §3.5): the asked wage for today's skill and price index, kept by a loyal worker; an
        /// ambitious one asks 10% more. Paying the asked wage clears a low hire's memory.
        /// </summary>
        internal static long RenewalWage(Simulation sim, Worker w)
        {
            PeopleDefs p = sim.Config.Game.People;
            var traits = p.Traits;
            if (traits[w.Trait1].KeepsWage || traits[w.Trait2].KeepsWage) return w.Wage;
            long wage = AskedWage(sim, w.Place, w.Skill, false);
            Fixed factor = traits[w.Trait1].RenewalWageFactor * traits[w.Trait2].RenewalWageFactor;
            return factor == Fixed.One ? wage : Money.RoundToCoin(Money.MulRate(wage, factor));
        }

        /// <summary>Paid for the new season: the wage set, the memory cleared unless they kept their own, skill grown on the job (spec 015 §3.5).</summary>
        internal static void Renewed(Simulation sim, Worker w, long wage)
        {
            PeopleDefs p = sim.Config.Game.People;
            var traits = p.Traits;
            if (!traits[w.Trait1].KeepsWage && !traits[w.Trait2].KeepsWage) w.Fairness = 0;
            w.Wage = wage;
            w.SeasonsWorked++;
            Fixed skill = w.Skill + p.Hand.SkillGainPerSeason * traits[w.Trait1].SkillGainFactor * traits[w.Trait2].SkillGainFactor;
            w.Skill = skill > Fixed.FromInt(100) ? Fixed.FromInt(100) : skill;
        }

        /// <summary>
        /// Save schema 10 → 11 (spec 015 AC-10): a crafting skill drawn for every citizen; each business's worker taken on by spec 016's
        /// stopgap becomes a hired worker at the role's wage, neutral (fair, no seasons yet); a commuter gets a seeded name and two traits
        /// at skill 50. Reputation starts at 50.
        /// </summary>
        internal static bool Migrate(Simulation sim)
        {
            PeopleState s = sim.State.People;
            if (s == null || sim.State.Citizens == null) return false;
            s.Reputation = sim.Config.Game.People.Hiring.ReputationStart;
            foreach (Citizen c in sim.State.Citizens.Citizens)
            {
                c.Crafting = CitizenRules.DrawCrafting(sim, c.Id);
                if (c.Skill == Fixed.Zero) c.Skill = CitizenRules.DrawFarming(sim, c.Id); // a child's, drawn at birth from now (ADR 0081)
            }
            if (sim.State.Businesses == null) return true;
            PeopleDefs p = sim.Config.Game.People;
            foreach (Business.Business b in sim.State.Businesses.Businesses)
            {
                if (b.Worker != WorkerKind.Citizen && b.Worker != WorkerKind.Neighbour) continue;
                Citizen c = b.Worker == WorkerKind.Citizen ? sim.State.Citizens.Citizen(b.WorkerCitizen) : null;
                var w = new Worker
                {
                    Id = s.NextWorkerId++, Place = b.Id, CitizenId = c?.Id ?? 0, HiredAt = b.ReadyAt,
                    Wage = MarketRules.Indexed(sim, BusinessRules.TypeOf(sim, b).Wage),
                };
                if (c != null)
                {
                    w.Name = c.Name;
                    w.Skill = Farms(sim, b.Id) ? c.Skill : c.Crafting;
                    w.Trait1 = c.Trait1;
                    w.Trait2 = c.Trait2;
                }
                else
                {
                    ulong seed = sim.State.Seed, key = (ulong)b.Id;
                    w.Name = (int)Rng.Below(Rng.Next(seed, RngStream.People, key, 100), (ulong)p.Names.Count);
                    w.Skill = Fixed.FromInt(50);
                    w.Trait1 = (int)Rng.Below(Rng.Next(seed, RngStream.People, key, 101), (ulong)p.Traits.Count);
                    w.Trait2 = PeopleRules.SecondTrait(sim, Rng.Next(seed, RngStream.People, key, 102), w.Trait1);
                }
                s.Workers.Add(w);
            }
            return true;
        }
    }
}
