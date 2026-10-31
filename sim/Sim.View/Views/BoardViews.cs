using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// The hiring board (spec 015 §7), in the Store's layout: a tab for each job (a farm hand, and each business's worker), the
    /// post, and who's asking, each with Talk, a reference and an offer. Opened as the "Word" sheet with the place's id.
    /// </summary>
    public static class HiringBoard
    {
        /// <summary>The job's name: "Farm hand", "Miller".</summary>
        public static string Role(ViewContext ctx, long place)
        {
            if (place == 0) return ctx.Strings.Get("board.hand");
            Business b = ctx.Sim.State.Businesses.Find(place);
            string role = ctx.Strings.Get("business.role." + BusinessRules.TypeOf(ctx.Sim, b).Worker);
            return char.ToUpperInvariant(role[0]) + role.Substring(1);
        }

        /// <summary>The places with a job: your fields, then each business not closed, in id order.</summary>
        public static List<long> Places(Simulation sim)
        {
            var list = new List<long> { 0 };
            if (sim.State.Businesses == null) return list;
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed) list.Add(b.Id);
            return list;
        }

        public static SheetVm Build(ViewContext ctx, long place)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            var s = ctx.Strings;
            List<long> places = Places(sim);
            if (!places.Contains(place)) place = 0;
            var vm = new SheetVm { Icon = "letter", Title = s.Get("board.title"), Subtitle = s.Get("board.subtitle"), Tab = "tab:" + place };
            if (places.Count > 1)
            {
                vm.Tabs = new List<ActionVm>();
                foreach (long x in places) vm.Tabs.Add(ActionVm.Open("tab:" + x, Role(ctx, x), new SheetRequest("Word", x)));
            }
            AddJob(ctx, vm, place);
            AddPost(ctx, vm, place);
            AddApplicants(ctx, vm, place);
            return vm;
        }

        /// <summary>What the job is and its usual wage; who has it now.</summary>
        private static void AddJob(ViewContext ctx, SheetVm vm, long place)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long usual = HiringRules.AskedWage(sim, place, Fixed.FromInt(50), false);
            string where = place == 0 ? s.Get("board.handWhere") : s.Get("board.businessWhere", "business", BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, sim.State.Businesses.Find(place))).ToLowerInvariant());
            vm.Lines.Add(new LineVm(Role(ctx, place), s.Get("board.usual", "wage", ctx.Fmt.Coins(usual)), "jobUsual", place)
                { Icon = place == 0 ? "person_hand_m" : BusinessRules.TypeOf(sim, sim.State.Businesses.Find(place)).Id, Note = where });
            if (place == 0) return;
            Business b = sim.State.Businesses.Find(place);
            Worker w = sim.State.People.WorkerAt(place);
            if (w != null)
                vm.Lines.Add(new LineVm(s.Get("board.worksHere"), PeopleWords.Name(ctx, w.Name) + " · " + PeopleWords.Band(ctx, (int)w.Skill.RoundToLong()), "businessWorker", place)
                    {
                        Icon = PeopleWords.Female(ctx, w.Name) ? "person_hand_f" : "person_hand_m", Stars = PeopleWords.Stars(ctx, (int)w.Skill.RoundToLong(), (int)w.Skill.RoundToLong()),
                        Note = s.Get("board.letGoOnCard"), Action = ActionVm.Open("business:" + place, s.Get("action.see"), new SheetRequest("Business", place)),
                    });
            else if (b.Worker == WorkerKind.Founder)
                vm.Lines.Add(new LineVm(s.Get("board.worksHere"), s.Get("business.workerYou"), "businessWorker", place) { Icon = "person_founder", Note = s.Get("board.youRunIt") });
        }

        private static void AddPost(ViewContext ctx, SheetVm vm, long place)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            var s = ctx.Strings;
            if (place != 0 && HiringRules.BusinessPlace(sim, place) != RejectReason.None) return;
            JobPost post = p.PostAt(place);
            if (post != null)
            {
                vm.Lines.Add(new LineVm(s.Get(post.AboveMarket ? "people.postAbove" : "people.postMarket"),
                    s.Get("people.postActive", "time", ctx.Fmt.Duration(post.ExpiresAt - ctx.Now)), "post") { Icon = "letter" });
                return;
            }
            long cost = MarketRules.Indexed(sim, sim.Config.Game.People.JobPost.Cost);
            foreach (bool above in new[] { false, true })
            {
                bool am = above;
                long at = place;
                var line = new LineVm(s.Get(am ? "people.postAbove" : "people.postMarket"), s.Get(am ? "board.postAboveWhy" : place == 0 ? "people.postMarketWhy" : "board.postMarketWhy"), "post")
                    { Icon = am ? "person_hand_f" : "person_hand_m" };
                line.Action = ActionVm.Checked(ctx, (am ? "postAbove:" : "postMarket:") + place, s.Get("action.post"), ctx.Fmt.Coins(cost), ActionKind.Secondary, t => new PostJob(t, at, am));
                vm.Lines.Add(line);
            }
        }

        private static void AddApplicants(ViewContext ctx, SheetVm vm, long place)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            var s = ctx.Strings;
            if (place != 0 && HiringRules.BusinessPlace(sim, place) != RejectReason.None) return;
            vm.Lines.Add(LineVm.Header(s.Get("people.applicants")));
            List<Applicant> list = p.ApplicantsFor(place);
            list.Sort((x, y) => x.CitizenId != 0 == (y.CitizenId != 0) ? x.Id.CompareTo(y.Id) : x.CitizenId != 0 ? -1 : 1); // your townspeople first
            if (list.Count == 0)
            {
                long next = place == 0 ? p.NextApplicantAt : p.PostAt(place)?.NextApplicantAt ?? 0;
                string when = next > ctx.Now ? s.Get("people.nextIn", "time", ctx.Fmt.Duration(next - ctx.Now))
                    : place == 0 && PeopleRules.EmptyHut(sim) == null ? s.Get("board.handNeedsHome") : s.Get(p.PostAt(place) != null ? "people.nextSoon" : "board.postFirst");
                vm.Lines.Add(new LineVm(s.Get("people.nobody"), when));
            }
            foreach (Applicant a in list)
            {
                Applicant ap = a;
                PeopleRules.SkillRange(sim, a, out int lo, out int hi);
                string traits = PeopleWords.Trait(ctx, a.Trait1) + " · " + (a.SecondTraitKnown ? PeopleWords.Trait(ctx, a.Trait2) : "?");
                var line = new LineVm(PeopleWords.Name(ctx, a.Name), PeopleWords.Range(ctx, lo, hi) + " · " + traits, "applicant", a.Id)
                    { Stars = PeopleWords.Stars(ctx, lo, hi), Icon = PeopleWords.Female(ctx, a.Name) ? "person_hand_f" : "person_hand_m", Note = From(ctx, a) };
                if (a.Countered)
                {
                    line.Note = s.Get("board.countered", "wage", ctx.Fmt.Coins(HiringRules.OfferWage(sim, a, OfferLevel.Asked)));
                    line.NoteTone = Tone.Warn;
                }
                line.Action = ActionVm.Open("offer:" + a.Id, s.Get("board.offer"), new SheetRequest("JobOffer", a.Id, place.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                vm.Lines.Add(line);
                if (!a.SecondTraitKnown)
                {
                    var talk = new LineVm(s.Get("people.talkTo", "name", PeopleWords.Name(ctx, a.Name)), s.Get("people.talkWhy"));
                    talk.Action = ActionVm.Checked(ctx, "interview:" + a.Id, s.Get("action.interview"), null, ActionKind.Secondary, t => new Interview(t, ap.Id));
                    vm.Lines.Add(talk);
                }
                if (a.SkillKnown) continue;
                if (a.ReferenceAt == 0)
                {
                    var check = new LineVm(s.Get("board.reference", "name", PeopleWords.Name(ctx, a.Name)), s.Get("board.referenceWhy"), "jobReference", a.Id);
                    check.Action = ActionVm.Checked(ctx, "reference:" + a.Id, s.Get("board.ask"),
                        ctx.Fmt.Coins(MarketRules.Indexed(sim, sim.Config.Game.People.Hiring.ReferenceCost)), ActionKind.Secondary, t => new CheckReference(t, ap.Id));
                    vm.Lines.Add(check);
                }
                else if (!a.ReferenceDone)
                    vm.Lines.Add(new LineVm(s.Get("board.referenceOut"), s.Get("board.referenceBack", "time", ctx.Fmt.Duration(a.ReferenceAt - ctx.Now)), "jobReference", a.Id));
                else
                    vm.Lines.Add(new LineVm(s.Get("board.referenceSays"), s.Get(HiringRules.LeftOnGoodTerms(sim, a) ? "board.goodTerms" : "board.letGo"), "jobReference", a.Id)
                        { NoteTone = HiringRules.LeftOnGoodTerms(sim, a) ? Tone.Plain : Tone.Warn });
            }
        }

        /// <summary>"From your town", "Would live in your hut", "Comes daily from a neighbouring village", or a regular's.</summary>
        private static string From(ViewContext ctx, Applicant a)
        {
            var s = ctx.Strings;
            if (a.CitizenId != 0) return s.Get("board.fromTown");
            if (a.FromRosterId != 0) return s.Get("board.regular");
            return s.Get(a.Place == 0 ? "board.liveInHut" : "board.commutes");
        }

        /// <summary>The offer (spec 015 §3.3): at the asked wage, 10% less or 10% more, each with how likely it's taken and what's paid now.</summary>
        public static SheetVm Offer(ViewContext ctx, long applicantId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Applicant a = sim.State.People.Applicant(applicantId);
            if (a == null) return new SheetVm { Icon = "letter", Title = s.Get("reject.UnknownApplicant") };
            return Offer(ctx, a, (t, l) => new OfferContract(t, applicantId, l));
        }

        /// <summary>Offering work as a hand from a townsperson's card (ADR 0083 §3): the board's offer, no post, sent as <see cref="OfferTownsperson"/>.</summary>
        public static SheetVm TownOffer(ViewContext ctx, long citizenId)
        {
            Simulation sim = ctx.Sim;
            Citizen c = sim.State.Citizens?.Citizen(citizenId);
            if (c == null || !HouseholdRules.WorksOutside(sim, c)) return new SheetVm { Icon = "letter", Title = ctx.Reject(RejectReason.NotAWorker) };
            Applicant a = sim.State.People.Applicants.Find(x => x.CitizenId == c.Id && x.Place == 0) ?? HiringRules.TownApplicant(sim, c, 0);
            return Offer(ctx, a, (t, l) => new OfferTownsperson(t, citizenId, l));
        }

        private static SheetVm Offer(ViewContext ctx, Applicant a, System.Func<long, OfferLevel, Command> send)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            string house = Housing(ctx, a);
            var vm = new SheetVm
            {
                Icon = PeopleWords.Female(ctx, a.Name) ? "person_hand_f" : "person_hand_m", Title = s.Get("board.offerTitle", "name", PeopleWords.Name(ctx, a.Name)),
                Subtitle = s.Get("board.offerSubtitle", "role", Role(ctx, a.Place).ToLowerInvariant()),
            };
            if (a.Countered)
                vm.Lines.Add(new LineVm(s.Get("board.counterLine"), ctx.Fmt.Coins(HiringRules.OfferWage(sim, a, OfferLevel.Asked)), "jobWage", a.Id) { Note = s.Get("board.counterNote"), NoteTone = Tone.Warn });
            bool business = a.Place != 0;
            foreach (OfferLevel level in new[] { OfferLevel.Asked, OfferLevel.Low, OfferLevel.High })
            {
                OfferLevel l = level;
                long wage = HiringRules.OfferWage(sim, a, level);
                string at = level == OfferLevel.Asked ? "board.atAsked" : level == OfferLevel.Low ? "board.atLow" : "board.atHigh";
                string note = level == OfferLevel.Asked ? "board.noteAsked" : level == OfferLevel.Low ? "board.noteLow" : "board.noteHigh";
                var line = new LineVm(s.Get(at, "wage", ctx.Fmt.Coins(wage)), Chance(ctx, HiringRules.AcceptChance(sim, a, level)), "jobWage", a.Id) { Note = s.Get(note) };
                if (house != null) line.Note += ". " + (house.Length == 0 ? HouseInPay(ctx, wage) : house);
                line.Action = ActionVm.Checked(ctx, "offer" + level + ":" + a.Id, s.Get("action.offer"),
                    s.Get(business ? "board.nowBusiness" : "board.now", "now", ctx.Fmt.Coins(PeopleRules.RestOfSeason(sim, wage))), level == OfferLevel.Asked ? ActionKind.Primary : ActionKind.Secondary,
                    t => send(t, l));
                vm.Lines.Add(line);
            }
            return vm;
        }

        /// <summary>
        /// The house as part of a townsperson's pay (spec 014 §3.6, ADR 0083): null when it doesn't apply (a migrant, a business job, not
        /// in a family house); empty when the house would be part of this hand's pay (see <see cref="HouseInPay"/>); else the sentence
        /// that it's already in another hand's pay.
        /// </summary>
        private static string Housing(ViewContext ctx, Applicant a)
        {
            Simulation sim = ctx.Sim;
            if (a.Place != 0 || a.CitizenId == 0) return null;
            Household x = sim.State.Citizens.Household(sim.State.Citizens.Citizen(a.CitizenId).HouseholdId);
            if (x == null || !HouseholdRules.InFamilyHouse(sim, x)) return null;
            Hand other = MigrationRules.HousedBy(sim, x);
            return other == null ? "" : ctx.Strings.Get("board.houseInOthersPay", "name", PeopleWords.Name(ctx, other.Name));
        }

        /// <summary>"From next season: 24,000 cash + the house (6,000)".</summary>
        private static string HouseInPay(ViewContext ctx, long wage)
        {
            long house = MarketRules.Indexed(ctx.Sim, ctx.Sim.Config.Game.Population.RentPerSeason);
            return ctx.Strings.Get("board.houseInPay", "cash", ctx.Fmt.Coins(System.Math.Max(0, wage - house)), "house", ctx.Fmt.Coins(house));
        }

        /// <summary>A chance in words, never a number (ADR 0039 §7): certain, very likely, likely, maybe.</summary>
        private static string Chance(ViewContext ctx, Fixed p)
        {
            string key = p >= Fixed.One ? "board.sure" : p >= Fixed.Parse("0.9") ? "board.veryLikely" : p >= Fixed.Parse("0.75") ? "board.likely" : "board.maybe";
            return ctx.Strings.Get(key);
        }
    }
}
