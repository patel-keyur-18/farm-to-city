using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>Skill and hours in words and stars, never raw factors (ADR 0039 §7); names of people and places.</summary>
    public static class PeopleWords
    {
        /// <summary>Stars for a skill range: solid to the low end, faded to the high end (both rounded to the nearest star).</summary>
        public static StarsVm Stars(ViewContext ctx, int low, int high)
        {
            int per = ctx.Ui.SkillStarPoints;
            int solid = System.Math.Min(5, (low + per / 2) / per);
            int upto = System.Math.Min(5, System.Math.Max(solid, (high + per / 2) / per));
            return new StarsVm(solid, upto - solid);
        }

        /// <summary>Beginner, Capable, Skilled or Expert (thresholds in ui.json).</summary>
        public static string Band(ViewContext ctx, int skill)
        {
            string[] keys = { "skill.beginner", "skill.capable", "skill.skilled", "skill.expert" };
            int i = 0;
            while (i < 3 && skill >= ctx.Ui.SkillBands[i]) i++;
            return ctx.Strings.Get(keys[i]);
        }

        /// <summary>"Capable" or "Capable to Skilled".</summary>
        public static string Range(ViewContext ctx, int low, int high)
        {
            string a = Band(ctx, low), b = Band(ctx, high);
            return a == b ? a : ctx.Strings.Get("skill.range", "low", a, "high", b);
        }

        /// <summary>"Tends about 2½ acres": hours a day ÷ a reference acre's hours, to the nearest half.</summary>
        public static string Tends(ViewContext ctx, Fixed hours)
        {
            long halves = (hours * Fixed.FromInt(2) / Fixed.FromInt(ctx.Ui.ReferenceAcreHours)).RoundToLong();
            string acres = halves / 2 == 0 ? "½" : (halves / 2) + (halves % 2 == 1 ? "½" : "");
            return ctx.Strings.Get("people.tends", "acres", acres);
        }

        /// <summary>What a hand gets through a day: "Tends about 3 acres", or away on a course.</summary>
        public static string Work(ViewContext ctx, Hand h)
        {
            return h.CourseUntil != 0 ? ctx.Strings.Get("staff.awayOnCourse") : h.Hr.SickUntil != 0 ? ctx.Strings.Get("staff.offSick") : Tends(ctx, PeopleRules.HandHours(ctx.Sim, h));
        }

        public static string Name(ViewContext ctx, int name) { return ctx.Strings.Get(ctx.Sim.Config.Game.People.Names[name].NameKey); }

        public static bool Female(ViewContext ctx, int name) { return ctx.Sim.Config.Game.People.Names[name].Female; }

        public static string Trait(ViewContext ctx, int trait) { return ctx.Strings.Get(ctx.Sim.Config.Game.People.Traits[trait].NameKey); }

        /// <summary>"Field 3": the player's farm acres numbered in map order.</summary>
        public static string Field(ViewContext ctx, int tileId)
        {
            int n = 0;
            foreach (Tile t in ctx.Sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Farm || !ctx.Sim.State.Land.Parcels[t.ParcelId].Owned) continue;
                n++;
                if (t.Id == tileId) break;
            }
            return ctx.Strings.Get("place.field", "n", ctx.Fmt.Integer(n));
        }

        public static string Target(ViewContext ctx, OrderTargetKind kind, long target)
        {
            if (kind == OrderTargetKind.Field) return Field(ctx, (int)target);
            if (kind == OrderTargetKind.Animals) return ctx.Strings.Get("order.shed");
            Tree tree = ctx.Sim.State.Farm.TreeIn(target);
            return tree == null ? ctx.Strings.Get("place.treeSlot") : ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Trees[tree.Def].NameKey);
        }

        /// <summary>"Keep wheat · sell at the market, not below 26" or "Pick · sell to the trader".</summary>
        public static string Order(ViewContext ctx, StandingOrder o)
        {
            // The animals order (spec 012 §3.11) and a fodder field (§3.8b) sell nothing.
            if (o.Kind == OrderTargetKind.Animals) return ctx.Strings.Get("order.lookAfter", "days", ctx.Strings.Plural("why.days", o.Days));
            bool fodder = o.Kind == OrderTargetKind.Field && o.Crop >= 0 && ctx.Sim.Config.Game.Farm.Crops[o.Crop].Fodder;
            string sell = o.Sell == SellRule.Store ? ctx.Strings.Get("order.sellStore")
                : o.Sell == SellRule.Trader ? ctx.Strings.Get("order.sellTrader")
                : ctx.Strings.Get("order.sellOffer", "min", ctx.Fmt.Integer(o.MinPrice.RoundToLong()));
            string what = o.Kind != OrderTargetKind.Field ? ctx.Strings.Get("order.pick")
                : o.Crop < 0 ? ctx.Strings.Get("order.handChooses", "name", Name(ctx, ctx.Sim.State.People.Hand(o.HandId).Name))
                : ctx.Strings.Get("order.keep", "crop", ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Crops[o.Crop].NameKey));
            if (o.Kind == OrderTargetKind.Field) what += With(ctx, ctx.Sim.Config.Game.Farm.Fertilisers[o.Fertiliser].Id);
            // The hand's pick on the field now, when it isn't the order's crop (out of season, or his choice; ADR 0048).
            Crop now = o.Kind == OrderTargetKind.Field ? ctx.Sim.State.Farm.CropOn((int)o.Target) : null;
            if (now != null && now.Def != o.Crop) what += " " + ctx.Strings.Get("order.now", "crop", ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Crops[now.Def].NameKey));
            return what + (fodder ? "" : " · " + sell) + " · " + ctx.Strings.Get(o.Once ? "order.once" : "order.every");
        }

        /// <summary>" with compost" for an order's fertiliser, nothing without one.</summary>
        public static string With(ViewContext ctx, string fertiliserId)
        {
            return fertiliserId == "none" ? "" : " " + ctx.Strings.Get("order.with." + fertiliserId);
        }

        /// <summary>"Last: harvested 1,200 kg, 3 h ago", or the waiting reason, or null before the first action.</summary>
        public static string Last(ViewContext ctx, StandingOrder o)
        {
            if (o.Waiting != OrderWait.None) return ctx.Strings.Get("order.wait." + o.Waiting);
            if (o.LastAction == OrderAction.None) return null;
            string ago = ctx.Fmt.Duration(ctx.Now - o.LastAt);
            if (o.LastAction == OrderAction.Bought) return ctx.Strings.Get("order.lastBought", "qty", ctx.Fmt.Kg(o.LastQuantity), "amount", ctx.Fmt.Coins(o.LastAmount), "ago", ago);
            return o.LastAction == OrderAction.Sowed ? ctx.Strings.Get("order.lastSowed", "ago", ago)
                : ctx.Strings.Get(o.LastAction == OrderAction.Picked ? "order.lastPicked" : "order.lastHarvested", "qty", ctx.Fmt.Kg(o.LastQuantity), "ago", ago);
        }

        /// <summary>The hand who takes orders for a target: the one holding it, else the first hand.</summary>
        public static Hand OrderHand(Simulation sim, OrderTargetKind kind, long target)
        {
            PeopleState p = sim.State.People;
            if (p == null || p.Hands.Count == 0) return null;
            StandingOrder o = p.OrderFor(kind, target);
            return o != null ? p.Hand(o.HandId) : p.Hands[0];
        }

        /// <summary>Care today as a word, with <paramref name="extraHours"/> more hours a day in the pool.</summary>
        internal static string CareWith(ViewContext ctx, Fixed extraHours)
        {
            Simulation sim = ctx.Sim;
            Fixed demand = FarmSystem.LabourDemand(sim);
            Fixed cap = sim.Config.Game.Farm.Farm.CareCap;
            Fixed ratio = demand == Fixed.Zero ? cap : (PeopleRules.CareSupply(sim) + extraHours) / demand;
            return ctx.Rating(ratio > cap ? cap : ratio);
        }
    }

    /// <summary>
    /// The People sheet (ADR 0039 §8, laid out like the Store by the owner's choice, US-215): picture buttons to hire labourers, build a
    /// hut and send word; then pictured rows for the labourers at work, the hands, the people asking for work and the huts.
    /// </summary>
    public static class PeopleSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            PeopleDefs defs = sim.Config.Game.People;
            var s = ctx.Strings;
            var vm = new SheetVm
            {
                Icon = "people", Title = s.Get("people.title"),
                Subtitle = s.Get("people.subtitle", "population", ctx.Fmt.Integer(sim.State.Citizens?.Population ?? p.Population), "huts", ctx.Fmt.Integer(PeopleRules.HutCount(sim)),
                    "max", ctx.Fmt.Integer(defs.Hand.MaxHandHuts)),
            };
            TownSheet.Tabs(ctx, vm, "tab:workers");
            vm.Lines.Add(new LineVm(s.Get("people.care"), ctx.Rating(sim.State.Farm.CareRatio), "care"));
            vm.Lines.Add(new LineVm(s.Get("people.reputation"), StaffView.Reputation(ctx), "reputation"));

            // The hub's picture buttons, each saying what it opens.
            int free = PeopleRules.FreeNow(sim);
            long hutCost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[defs.HandHut].Value);
            int huts = PeopleRules.HutCount(sim);
            bool room = huts < defs.Hand.MaxHandHuts, cash = sim.State.Ledger.CanAfford(hutCost);
            var buttons = new List<ActionVm>
            {
                Picture(ActionVm.Open("hire", s.Get("people.hireButton"), new SheetRequest("Hire", 0),
                    free == 0 ? s.Get("people.hireButtonNone") : s.Plural("people.hireButtonFree", free)), "person_labourer_m"),
                Picture(new ActionVm("buildHut", s.Get("people.hutButton"),
                    room ? s.Get("people.hutButtonDetail", "cost", ctx.Fmt.Coins(hutCost), "built", ctx.Fmt.Integer(huts), "max", ctx.Fmt.Integer(defs.Hand.MaxHandHuts))
                        : s.Get("people.hutButtonFull", "max", ctx.Fmt.Integer(defs.Hand.MaxHandHuts)),
                    ActionKind.Open, room && cash, !room ? ctx.Reject(RejectReason.TooManyHuts) : !cash ? ctx.Reject(RejectReason.InsufficientFunds) : null, null,
                    new SheetRequest("HutPlacement", 0)), "hand_hut"),
                Picture(ActionVm.Open("word", s.Get("people.wordButton"), new SheetRequest("Word", 0),
                    p.Applicants.Count > 0 ? s.Plural("people.wordButtonAsking", p.Applicants.Count)
                        : p.Posts.Count > 0 ? s.Plural("people.wordButtonPosts", p.Posts.Count) : s.Get("people.wordButtonDetail")), "letter"),
            };
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });

            // Labourers at work: your townspeople by name (ADR 0083 §6), then the neighbours together; out of working hours, home for the night.
            bool night = !MapView.WorkingHours(ctx.Now % 86400);
            long morning = ctx.Now / 86400 * 86400 + 7 * 3600 + (ctx.Now % 86400 >= 7 * 3600 ? 86400 : 0); // the next 7:00
            string Night(long hiredUntil) => s.Get(hiredUntil > morning ? "people.nightNote" : "people.nightOff"); // back only if still hired
            var town = new List<Citizen>();
            foreach (Citizen c in PeopleRules.TownLabourers(sim)) if (c.Labouring(ctx.Now)) town.Add(c);
            int working = 0;
            long until = 0;
            foreach (Neighbour n in p.Roster) if (n.Hired) { working++; if (n.Until > until) until = n.Until; }
            if (town.Count + working > 0) vm.Lines.Add(LineVm.Header(s.Plural("people.labourersN", town.Count + working)));
            foreach (Citizen c in town)
            {
                long home = sim.State.Citizens.Household(c.HouseholdId)?.HomeId ?? 0;
                vm.Lines.Add(new LineVm(TownWords.Name(ctx, c), s.Get("people.workingTill", "time", ForecastView.AtTime(ctx, c.LabourUntil)), "free")
                {
                    Icon = TownWords.Portrait(ctx, c), Note = night ? Night(c.LabourUntil) : TownWords.Home(ctx, home),
                    Action = ActionVm.Open("citizen:" + c.Id, s.Get("action.see"), new SheetRequest("Citizen", c.Id)),
                });
            }
            if (working > 0)
                vm.Lines.Add(new LineVm(s.Plural("people.neighboursN", working), s.Get("people.workingTill", "time", ForecastView.AtTime(ctx, until)), "free")
                    { Icon = "person_labourer_m", Note = night ? Night(until) : s.Get("people.workingNote") });

            // Hands.
            vm.Lines.Add(LineVm.Header(s.Get("people.hands")));
            if (p.Hands.Count == 0) vm.Lines.Add(new LineVm(s.Get("people.noHands"), s.Get(huts == 0 ? "people.noHandsHut" : p.Applicants.Count > 0 ? "people.noHandsChoose" : "people.noHandsWait")));
            foreach (Hand h in p.Hands)
            {
                Hand hand = h;
                int skill = (int)h.Skill.RoundToLong();
                bool lapsed = h.Contract == ContractState.Lapsed;
                var targets = new List<string>();
                foreach (StandingOrder o in p.Orders) if (o.HandId == h.Id) targets.Add(PeopleWords.Target(ctx, o.Kind, o.Target));
                var line = new LineVm(PeopleWords.Name(ctx, h.Name), PeopleWords.Band(ctx, skill) + " · " + PeopleWords.Work(ctx, h),
                    lapsed ? "hand.lapse" : "hand.hours", h.Id)
                {
                    Stars = PeopleWords.Stars(ctx, skill, skill), Icon = HandIcon(ctx, h.Name),
                    Note = lapsed ? s.Get("people.unpaid") : (night ? s.Get("people.nightNote") + " · " : "")
                        + (targets.Count == 0 ? s.Get("people.handNoOrders") : s.Get("people.handOrders", "list", string.Join(", ", targets))),
                    NoteTone = lapsed ? Tone.Warn : Tone.Plain,
                };
                line.Action = lapsed
                    ? ActionVm.Checked(ctx, "renew:" + h.Id, s.Get("action.renew"), ctx.Fmt.Coins(h.Wage), ActionKind.Primary, at => new RenewContract(at, hand.Id))
                    : ActionVm.Open("hand:" + h.Id, s.Get("action.see"), new SheetRequest("Hand", h.Id));
                vm.Lines.Add(line);
            }

            // Who's asking for work: chosen on the hiring board (spec 015 §7).
            if (p.Applicants.Count > 0)
                vm.Lines.Add(new LineVm(s.Get("people.applicants"), s.Plural("people.askingN", p.Applicants.Count), "applicant")
                    { Icon = "letter", Action = ActionVm.Open("board", s.Get("action.see"), new SheetRequest("Word", p.Applicants[0].Place)) });

            // Huts.
            if (huts > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("people.huts")));
                int number = 0;
                foreach (Building b in sim.State.Land.Buildings)
                {
                    if (!PeopleRules.IsHandHut(sim, b)) continue;
                    Hand who = p.Hands.Find(h => h.HutId == b.Id);
                    var line = new LineVm(s.Get("people.hutN", "n", ctx.Fmt.Integer(++number)),
                        who == null ? s.Get("people.hutEmpty") : s.Get("people.hutLives", "name", PeopleWords.Name(ctx, who.Name))) { Icon = "hand_hut" };
                    if (who == null) line.Note = s.Get("people.hutEmptyNote");
                    else line.Action = ActionVm.Open("hut:" + b.Id, s.Get("action.see"), new SheetRequest("Hand", who.Id));
                    vm.Lines.Add(line);
                }
                foreach (Construction c in p.Constructions)
                    vm.Lines.Add(new LineVm(s.Get("people.hutN", "n", ctx.Fmt.Integer(++number)), s.Get("people.hutBuilding"))
                        { Icon = "hand_hut_building", Note = s.Get("people.hutReadyNote", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now)) });
            }
            return vm;
        }

        /// <summary>Hire labourers (ADR 0049 §3, ADR 0066): one row per how many, each with its cost and Care before and after.</summary>
        public static SheetVm Hire(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            PeopleState p = sim.State.People;
            PeopleDefs defs = sim.Config.Game.People;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "person_labourer_m", Title = s.Get("hire.title"), Subtitle = s.Get("hire.subtitle") };
            int free = PeopleRules.FreeNow(sim), working = 0;
            foreach (Neighbour n in p.Roster) if (n.Hired) working++;
            Season season = Calendar.SeasonAt(ctx.Now, sim.Clock);
            vm.Lines.Add(new LineVm(s.Get("people.freeToday"),
                s.Get("people.freeTodayValue", "n", ctx.Fmt.Integer(free), "season", s.Get("season." + season.ToString().ToLowerInvariant())), "free"));
            if (working > 0) vm.Lines.Add(new LineVm(s.Get("people.workingToday"), s.Get("people.workingTodayValue", "n", ctx.Fmt.Integer(working)), "free"));
            long until = PeopleRules.LabourUntil(sim);
            if (until == 0)
            {
                bool fodder = sim.State.Farm.Crops.Exists(c => c.State == CropState.Growing && sim.Config.Game.Farm.Crops[c.Def].Fodder);
                vm.Lines.Add(fodder ? new LineVm(s.Get("hire.fodderOnly"), s.Get("hire.fodderOnlyValue")) : new LineVm(s.Get("people.noCrops"), s.Get("people.noCropsValue")));
                return vm;
            }
            vm.Lines.Add(new LineVm(s.Get("hire.until"), ForecastView.AtTime(ctx, until), "hire", 1));
            if (free == 0) vm.Lines.Add(new LineVm(s.Get("hire.noneFree"), s.Get("hire.noneFreeValue")));
            string now = PeopleWords.CareWith(ctx, Fixed.Zero);
            for (int k = 1; k <= free; k++)
            {
                int count = k;
                var line = new LineVm(s.Plural("people.hireN", count),
                    s.Get("people.carePreview", "from", now, "to", PeopleWords.CareWith(ctx, defs.Pool.HoursPerDay.MulRatio(count, 1))), "hire", count)
                    { Icon = count % 2 == 1 ? "person_labourer_m" : "person_labourer_f" };
                line.Action = ActionVm.Checked(ctx, "hire:" + count, s.Get("action.hire"), ctx.Fmt.Coins(PeopleRules.LabourCost(sim, count, until)), ActionKind.Primary,
                    at => new HireLabourers(at, count));
                vm.Lines.Add(line);
            }
            return vm;
        }

        private static string HandIcon(ViewContext ctx, int name) { return PeopleWords.Female(ctx, name) ? "person_hand_f" : "person_hand_m"; }

        private static ActionVm Picture(ActionVm a, string icon)
        {
            a.Icon = icon;
            return a;
        }
    }

    /// <summary>One hand (spec 006 §7, ADR 0039 §8): skill, work, traits, contract, orders; Renew or Let go.</summary>
    /// <summary>
    /// A worker's output and training (spec 015 §7) on the hand's card and a business's: output in words and stars with its Why?
    /// (skill, motivation, tiredness), or away on a course; Send on a course on the row.
    /// </summary>
    public static class StaffView
    {
        /// <summary>
        /// Time until someone away (a course, sick leave) is back: the sim brings them back at the first hourly pass at or after
        /// <paramref name="until"/>, so count to that hour, never below zero.
        /// </summary>
        public static long BackIn(ViewContext ctx, long until)
        {
            const long Hour = 3600;
            return System.Math.Max(0, (until + Hour - 1) / Hour * Hour - ctx.Now);
        }

        /// <summary>The output row for a farm hand (place 0) or a business's worker (place = the business's id).</summary>
        public static LineVm Output(ViewContext ctx, long place, IStaff w)
        {
            var s = ctx.Strings;
            string why = place == 0 ? "staffOutput" : "staffOutputAt";
            long whyId = place == 0 ? w.Id : place;
            LineVm line;
            if (w.CourseUntil != 0)
                line = new LineVm(s.Get("staff.output"), s.Get("staff.onCourse", "time", ctx.Fmt.Duration(StaffView.BackIn(ctx, w.CourseUntil))), why, whyId);
            else if (w.Hr.SickUntil != 0)
                line = new LineVm(s.Get("staff.output"), s.Get("staff.sick", "time", ctx.Fmt.Duration(StaffView.BackIn(ctx, w.Hr.SickUntil))), why, whyId);
            else
            {
                int band = Band(ctx, WorkRules.RelativeOutput(ctx.Sim, w));
                line = new LineVm(s.Get("staff.output"), s.Get("staff.output." + band), why, whyId) { Stars = new StarsVm(band + 1, 0) };
            }
            long id = w.Id;
            var work = ctx.Sim.Config.Game.People.Work;
            line.Action = ActionVm.Checked(ctx, "course", s.Get("staff.course"), s.Get("staff.courseDetail", "cost", ctx.Fmt.Coins(WorkRules.CourseCost(ctx.Sim)),
                "gain", ctx.Fmt.Integer(work.CourseGain), "days", s.Plural("why.days", work.CourseDays)), ActionKind.Secondary, at => new SendOnCourse(at, place, id));
            return line;
        }

        /// <summary>Employer reputation in words and its score: "Fair (52)" (spec 015 §3.8).</summary>
        public static string Reputation(ViewContext ctx)
        {
            int r = ctx.Sim.State.People.Reputation;
            var d = ctx.Sim.Config.Game.People.Reputation;
            return ctx.Strings.Get(r >= d.High ? "reputation.good" : r <= d.Low ? "reputation.poor" : "reputation.fair", "score", ctx.Fmt.Integer(r));
        }

        /// <summary>0 "Barely working" to 4 "Works very well": output against a typical worker's by ui.json's bands.</summary>
        public static int Band(ViewContext ctx, Fixed relative)
        {
            long pct = (relative * Fixed.FromInt(100)).RoundToLong();
            int i = 0;
            while (i < 4 && pct >= ctx.Ui.OutputBands[i]) i++;
            return i;
        }

        /// <summary>A hand's long days (spec 015 §3.4): their day and its overtime, with Start or Stop.</summary>
        public static LineVm LongDays(ViewContext ctx, Hand h)
        {
            var s = ctx.Strings;
            var p = ctx.Sim.Config.Game.People;
            long id = h.Id, pay = WorkRules.OvertimeDay(ctx.Sim, h);
            int normal = (int)p.Hand.HoursPerDay.RoundToLong();
            var line = new LineVm(s.Get("staff.longDays"), h.LongDays && WorkRules.Away(h) ? s.Get("staff.longDaysPaused")
                : h.LongDays ? s.Get("staff.longDaysOn", "hours", ctx.Fmt.Integer(normal + p.Work.LongDayHours), "amount", ctx.Fmt.Coins(pay))
                : s.Get("staff.longDaysOff", "hours", ctx.Fmt.Integer(normal)), "staffLongDays", h.Id);
            line.Action = h.LongDays
                ? ActionVm.Checked(ctx, "longDaysOff", s.Get("action.stop"), null, ActionKind.Secondary, at => new SetLongDays(at, id, false))
                : ActionVm.Checked(ctx, "longDaysOn", s.Get("action.start"), s.Get("staff.aDay", "amount", ctx.Fmt.Coins(pay)), ActionKind.Secondary, at => new SetLongDays(at, id, true));
            return line;
        }
    }

    public static class HandSheet
    {
        public static SheetVm Build(ViewContext ctx, long handId)
        {
            Simulation sim = ctx.Sim;
            Hand h = sim.State.People.Hand(handId);
            if (h == null) return new SheetVm { Icon = "people", Title = ctx.Reject(RejectReason.NoHand) };
            int skill = (int)h.Skill.RoundToLong();
            var vm = new SheetVm
            {
                Icon = PeopleWords.Female(ctx, h.Name) ? "person_hand_f" : "person_hand_m", Title = PeopleWords.Name(ctx, h.Name),
                Subtitle = ctx.Strings.Get("hand.subtitle", "time", ctx.Fmt.Duration(ctx.Now - h.HiredAt)),
            };
            vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.skill"), PeopleWords.Band(ctx, skill), "hand.skill", h.Id) { Stars = PeopleWords.Stars(ctx, skill, skill) });
            vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.work"), PeopleWords.Work(ctx, h), "hand.hours", h.Id));
            if (h.Contract == ContractState.Active)
            {
                vm.Lines.Add(StaffView.Output(ctx, 0, h));
                vm.Lines.Add(StaffView.LongDays(ctx, h));
            }
            // Where he lives and with whom (spec 014 §7): his hut alone, sending half his wage home, or his family's house.
            Citizen me = sim.State.Citizens?.OfHand(h.Id);
            Household home = me == null ? null : sim.State.Citizens.Household(me.HouseholdId);
            if (home != null)
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.home"), TownWords.Home(ctx, home.HomeId) + " · " + TownWords.LivesWith(ctx, home), "hand.home", h.Id)
                    { Opens = new SheetRequest(home.Members.Count == 1 ? "Citizen" : "Household", home.Members.Count == 1 ? me.Id : home.Id) });
            // The family house as part of the pay, once a house (spec 014 §3.6, ADR 0083 §2).
            Hand housed = home == null ? null : MigrationRules.HousedBy(sim, home);
            if (housed != null)
            {
                long rent = MarketRules.Indexed(sim, sim.Config.Game.Population.RentPerSeason);
                string value = housed != h ? ctx.Strings.Get("hand.housingOther", "name", PeopleWords.Name(ctx, housed.Name))
                    : ctx.Strings.Get(h.HousingInKind > 0 ? "hand.housingInPay" : "hand.housingNext", "house", ctx.Fmt.Coins(h.HousingInKind > 0 ? h.HousingInKind : rent));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.housing"), value, "hand.wage", h.Id));
            }
            foreach (int t in new[] { h.Trait1, h.Trait2 })
                vm.Lines.Add(new LineVm(PeopleWords.Trait(ctx, t), ctx.Strings.Get(sim.Config.Game.People.Traits[t].NameKey + ".why")));
            if (h.Contract == ContractState.Active)
            {
                long left = Sim.Core.Finance.Accrual.RefundIfClosed(sim, h.PrepaymentId);
                var contract = new LineVm(ctx.Strings.Get("hand.contract"), ctx.Strings.Get("hand.contractPaid", "wage", ctx.Fmt.Coins(h.Wage)), "hand.wage", h.Id);
                if (HrRules.Leaves(sim, h)) { contract.Note = ctx.Strings.Get("staff.leaving"); contract.NoteTone = Tone.Warn; }
                vm.Lines.Add(contract);
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.prepaid"), ctx.Fmt.Coins(left), "hand.prepaid", h.Id));
            }
            else
            {
                long grace = sim.Config.Game.People.Hand.LapseGraceDays * ClockDef.SecondsPerDay - h.LapsedLiveSeconds;
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.contract"), ctx.Strings.Get("hand.contractUnpaid", "time", ctx.Fmt.Duration(grace), "wage", ctx.Fmt.Coins(h.Wage)), "hand.lapse", h.Id));
            }

            vm.Lines.Add(LineVm.Header(ctx.Strings.Get("hand.orders")));
            bool any = false;
            foreach (StandingOrder o in sim.State.People.Orders)
            {
                if (o.HandId != h.Id) continue;
                any = true;
                StandingOrder order = o;
                string last = PeopleWords.Last(ctx, o);
                var line = new LineVm(PeopleWords.Target(ctx, o.Kind, o.Target), PeopleWords.Order(ctx, o) + (last == null ? "" : "\n" + last), "order", h.Id);
                line.Action = ActionVm.Checked(ctx, "clear:" + o.Kind + ":" + o.Target, ctx.Strings.Get("action.remove"), null, ActionKind.Secondary,
                    at => new ClearStandingOrder(at, h.Id, order.Kind, order.Target));
                vm.Lines.Add(line);
            }
            if (!any) vm.Lines.Add(new LineVm(ctx.Strings.Get("hand.noOrders"), ctx.Strings.Get("hand.noOrdersHint")));
            if (Sim.Core.Livestock.LivestockRules.Shed(sim) != null && sim.State.People.OrderFor(OrderTargetKind.Animals, 0) == null)
            {
                var shed = new LineVm(ctx.Strings.Get("order.shed"), ctx.Strings.Get("order.lookAfterOffer"));
                shed.Action = ActionVm.Checked(ctx, "animalsOrder", ctx.Strings.Get("action.giveOrder"), null, ActionKind.Secondary,
                    at => new SetStandingOrder(at, handId, OrderSpec.LookAfterAnimals(3)));
                vm.Lines.Add(shed);
            }

            if (h.Contract == ContractState.Lapsed)
                vm.Actions.Add(ActionVm.Checked(ctx, "renew", ctx.Strings.Get("action.renew"), ctx.Fmt.Coins(h.Wage), ActionKind.Primary, at => new RenewContract(at, handId)));
            long refund = h.PrepaymentId == 0 ? 0 : Sim.Core.Finance.Accrual.RefundIfClosed(sim, h.PrepaymentId);
            long severance = h.SeasonsWorked == 0 ? 0 : System.Math.Min(refund, Sim.Core.Finance.Money.MulRate(h.Wage, sim.Config.Game.People.Hand.SeveranceShare));
            ActionVm letGo = ActionVm.Checked(ctx, "letGo", ctx.Strings.Get("action.letGo"),
                ctx.Strings.Get("hand.letGoDetail", "refund", ctx.Fmt.Coins(refund - severance)), ActionKind.Danger, at => new DismissHand(at, handId));
            letGo.Confirm = new ConfirmVm(ctx.Strings.Get("hand.letGoQ", "name", PeopleWords.Name(ctx, h.Name)), ctx.Strings.Get("action.letGo"),
                ctx.Strings.Get("hand.keep"), at => new Command[] { new DismissHand(at, handId) }, noSends: false) { YesIsDanger = true };
            vm.Actions.Add(letGo);
            return vm;
        }
    }

    /// <summary>
    /// Giving a hand an order from the map (ADR 0039 §8): a field's crop first, on the sow sheet (<see cref="Crops"/>, ADR 0059 §3),
    /// then how long, then how to sell. Arg carries the steps chosen so far: "radish+compost|once|26" (the crop, or "auto" for the
    /// hand's choice, then the fertiliser), or "+compost" while the fertiliser is chosen. Trees go straight to selling (Arg "tree").
    /// </summary>
    public static class OrderSheet
    {
        /// <summary>The crop step: a field order with no crop chosen yet (shown by <see cref="Crops"/>).</summary>
        public static bool IsCropStep(OrderTargetKind kind, string arg)
        {
            return kind == OrderTargetKind.Field && Head(arg).Split('+')[0].Length == 0;
        }

        private static string Head(string arg) { return string.IsNullOrEmpty(arg) ? "" : arg.Split('|')[0]; }

        /// <summary>The fertiliser in Arg's first step ("radish+compost"), "none" when there's none.</summary>
        private static string FertiliserOf(string arg)
        {
            string[] head = Head(arg).Split('+');
            return head.Length > 1 && head[1].Length > 0 ? head[1] : "none";
        }

        /// <summary>
        /// The crop step (ADR 0059 §3): the player's sow sheet for this field, with the fertiliser choice and every crop's figures,
        /// "Let him choose" (with his pick now) first, and Choose on each crop, in season or not (out of season he sows his best
        /// pick in between, ADR 0048).
        /// </summary>
        public static SowSheetVm Crops(ViewContext ctx, long target, string arg)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            var s = ctx.Strings;
            // A changed order starts from its own fertiliser until another is tapped.
            StandingOrder existing = sim.State.People?.OrderFor(OrderTargetKind.Field, target);
            string fert = existing != null && Head(arg).IndexOf('+') < 0 ? defs.Farm.Fertilisers[existing.Fertiliser].Id : FertiliserOf(arg);
            SowSheetVm vm = SowSheet.Build(ctx, (int)target, fert);
            Hand h = PeopleWords.OrderHand(sim, OrderTargetKind.Field, target);
            if (h == null)
            {
                vm.Title = s.Get("order.noHand");
                vm.Rows.Clear();
                vm.Fertilisers.Clear();
                return vm;
            }
            string name = PeopleWords.Name(ctx, h.Name);
            vm.Title = s.Get("order.title", "name", name, "place", PeopleWords.Target(ctx, OrderTargetKind.Field, target));
            vm.FreeHoursText = null; // the hand's work, not the player's
            vm.FertiliserHeading = s.Get("order.fertiliserHeading", "name", name);
            if (existing != null) vm.Lines.Add(new LineVm(s.Get("order.current"), PeopleWords.Order(ctx, existing)));
            // The hand's choice (ADR 0048 §1): the in-season crop that pays best on this field with this fertiliser, every time he sows.
            int best = FarmRules.BestCrop(sim, sim.State.Land.Tiles[(int)target], defs.Farm.Fertilisers[defs.Farm.IndexOfFertiliser(fert)]);
            var choice = new LineVm(s.Get("order.handChooses", "name", name),
                best < 0 ? s.Get("order.handChoosesWhy") : s.Get("order.handChoosesNow", "crop", s.Get(defs.Farm.Crops[best].NameKey)));
            choice.Action = ActionVm.Open("crop:" + OrderSpec.HandChooses, s.Get("action.choose"), new SheetRequest("Order", target, OrderSpec.HandChooses + "+" + fert));
            vm.Lines.Add(choice);
            foreach (CropRowVm r in vm.Rows)
            {
                CropDef c = defs.Farm.Crops[defs.Farm.IndexOfCrop(r.CropId)];
                r.Sow = ActionVm.Open("crop:" + c.Id, s.Get("action.choose"), new SheetRequest("Order", target, c.Id + "+" + fert));
                if (!r.SowableNow) r.SeasonNote = s.Get("order.othersBest", "seasons", FieldSheets.SeasonsText(ctx, c.SowSeasons), "name", name);
            }
            if (existing != null)
            {
                long holder = existing.HandId;
                vm.Actions.Add(ActionVm.Checked(ctx, "clearOrder", s.Get("action.removeOrder"), null, ActionKind.Danger, at => new ClearStandingOrder(at, holder, OrderTargetKind.Field, target)));
            }
            return vm;
        }

        public static SheetVm Build(ViewContext ctx, OrderTargetKind kind, long target, string arg)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            Hand h = PeopleWords.OrderHand(sim, kind, target);
            var vm = new SheetVm { Icon = "people" };
            if (h == null)
            {
                vm.Title = ctx.Strings.Get("order.noHand");
                return vm;
            }
            if (IsCropStep(kind, arg)) throw new System.ArgumentException("a field's crop is chosen on OrderSheet.Crops");
            string name = PeopleWords.Name(ctx, h.Name);
            vm.Title = ctx.Strings.Get("order.title", "name", name, "place", PeopleWords.Target(ctx, kind, target));
            StandingOrder existing = sim.State.People.OrderFor(kind, target);
            if (existing != null) vm.Subtitle = PeopleWords.Order(ctx, existing);

            // Arg: the steps chosen so far, "crop+fertiliser|once|26" (a tree: "tree|every|40"). Crop, then how long, then how to sell.
            string[] parts = arg.Split('|');
            string head = kind == OrderTargetKind.Field ? parts[0] : "tree";
            string crop = kind == OrderTargetKind.Field ? head.Split('+')[0] : null;
            string fert = FertiliserOf(arg);
            bool auto = crop == OrderSpec.HandChooses;
            if (crop != null) vm.Subtitle = (auto ? ctx.Strings.Get("order.handChooses", "name", name)
                : ctx.Strings.Get("order.keep", "crop", ctx.Strings.Get(defs.Farm.Crops[defs.Farm.IndexOfCrop(crop)].NameKey))) + PeopleWords.With(ctx, fert);
            if (parts.Length < 2)
            {
                // How long: this season only (the order clears after its harvest) or every season (a standing order).
                foreach (string how in new[] { "once", "every" })
                {
                    var line = new LineVm(ctx.Strings.Get("order." + how), ctx.Strings.Get("order." + how + "Why"));
                    line.Action = ActionVm.Open("how:" + how, ctx.Strings.Get("action.choose"), new SheetRequest("Order", target, head + "|" + how));
                    vm.Lines.Add(line);
                }
                AddClear(ctx, vm, h, existing, kind, target);
                return vm;
            }
            bool once = parts[1] == "once";
            Fixed? min = parts.Length > 2 ? Fixed.FromInt(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)) : (Fixed?)null;
            vm.Subtitle = (vm.Subtitle == null ? "" : vm.Subtitle + " · ") + ctx.Strings.Get(once ? "order.once" : "order.every");

            // The minimum price is for the order's crop; the hand's choice changes with the season, so it has none.
            int good = kind == OrderTargetKind.Field ? auto ? -1 : defs.Farm.Crops[defs.Farm.IndexOfCrop(crop)].OutputGood
                : sim.State.Farm.TreeIn(target) == null ? -1 : defs.Farm.Trees[sim.State.Farm.TreeIn(target).Def].FruitGood;
            long today = good < 0 ? 1 : System.Math.Max(1, sim.State.Market.MarketPrice(good).FloorToLong()); // never above today's price, so the offer can sell
            long minimum = min.HasValue ? min.Value.RoundToLong() : today;
            OrderSpec Spec(SellRule rule, long m)
            {
                OrderSpec spec = crop != null ? OrderSpec.KeepCrop((int)target, crop, SeedSource.Certified, fert, rule, Fixed.FromInt((int)m)) : OrderSpec.PickTree(target, rule, Fixed.FromInt((int)m));
                return once ? spec.ThisSeasonOnly() : spec;
            }

            if (crop != null && !auto && defs.Farm.Crops[defs.Farm.IndexOfCrop(crop)].Fodder)
            {
                // Fodder is cut for the animals every morning, never sold (spec 012 §3.8b): one way to set it.
                var keep = new LineVm(ctx.Strings.Get("order.fodderKeep"), ctx.Strings.Get("order.fodderKeepWhy"));
                keep.Action = ActionVm.Checked(ctx, "set:store", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Store, 0)));
                vm.Lines.Add(keep);
                AddClear(ctx, vm, h, existing, kind, target);
                return vm;
            }
            var store = new LineVm(ctx.Strings.Get("order.sellStore"), ctx.Strings.Get("order.sellStoreWhy"));
            store.Action = ActionVm.Checked(ctx, "set:store", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Store, 0)));
            vm.Lines.Add(store);
            if (!auto)
            {
                var offer = new LineVm(ctx.Strings.Get("order.sellOffer", "min", ctx.Fmt.Integer(minimum)),
                    ctx.Strings.Get("order.sellOfferWhy", "today", ctx.Fmt.Integer(today)), "minPrice");
                offer.Action = ActionVm.Checked(ctx, "set:offer", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Offer, minimum)));
                vm.Lines.Add(offer);
                string prefix = head + "|" + parts[1] + "|";
                var lower = new LineVm(ctx.Strings.Get("order.lower"), ctx.Strings.Get("order.minNow", "min", ctx.Fmt.Integer(minimum)));
                lower.Action = new ActionVm("min:down", "−", null, ActionKind.Open, minimum > 1, minimum > 1 ? null : ctx.Reject(RejectReason.InvalidArgument), null,
                    new SheetRequest("Order", target, prefix + (minimum - 1)));
                vm.Lines.Add(lower);
                var raise = new LineVm(ctx.Strings.Get("order.raise"), ctx.Strings.Get("order.minNow", "min", ctx.Fmt.Integer(minimum)));
                raise.Action = ActionVm.Open("min:up", "+", new SheetRequest("Order", target, prefix + (minimum + 1)));
                vm.Lines.Add(raise);
            }
            var trader = new LineVm(ctx.Strings.Get("order.sellTrader"), ctx.Strings.Get("order.sellTraderWhy"));
            trader.Action = ActionVm.Checked(ctx, "set:trader", ctx.Strings.Get("action.setOrder"), null, ActionKind.Primary, at => new SetStandingOrder(at, h.Id, Spec(SellRule.Trader, 0)));
            vm.Lines.Add(trader);
            AddClear(ctx, vm, h, existing, kind, target);
            return vm;
        }

        private static void AddClear(ViewContext ctx, SheetVm vm, Hand h, StandingOrder existing, OrderTargetKind kind, long target)
        {
            if (existing == null) return;
            long holder = existing.HandId;
            vm.Actions.Add(ActionVm.Checked(ctx, "clearOrder", ctx.Strings.Get("action.removeOrder"), null, ActionKind.Danger, at => new ClearStandingOrder(at, holder, kind, target)));
        }
    }

    /// <summary>Where a hand's hut can go (ADR 0039 §2): each inherited farm acre, green or red with the reason.</summary>
    public sealed class HutPlaceVm
    {
        public int TileId { get; internal set; }
        public bool Allowed { get; internal set; }
        public string Reason { get; internal set; }
    }

    public static class HutPlacement
    {
        public static List<HutPlaceVm> Places(ViewContext ctx)
        {
            var list = new List<HutPlaceVm>();
            Simulation sim = ctx.Sim;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Quarters || !sim.State.Land.Parcels[t.ParcelId].Protected) continue;
                CommandResult r = sim.Preview(new BuildHandHut(ctx.Now, t.Id));
                list.Add(new HutPlaceVm { TileId = t.Id, Allowed = r.Accepted, Reason = r.Accepted ? null : ctx.Reject(r.Reason) });
            }
            return list;
        }

        /// <summary>Why a hut can't go on any tile (the ghost over a tile that isn't in <see cref="Places"/>).</summary>
        public static string ReasonFor(ViewContext ctx, int tileId)
        {
            CommandResult r = ctx.Sim.Preview(new BuildHandHut(ctx.Now, tileId));
            return r.Accepted ? null : ctx.Reject(r.Reason);
        }

        /// <summary>The Confirm card's button: build here, with the cost.</summary>
        public static ActionVm Confirm(ViewContext ctx, int tileId)
        {
            long cost = MarketRules.Indexed(ctx.Sim, ctx.Sim.Config.Game.Buildings[ctx.Sim.Config.Game.People.HandHut].Value);
            return ActionVm.Checked(ctx, "buildHere", ctx.Strings.Get("action.buildHere"), ctx.Fmt.Coins(cost), ActionKind.Primary, at => new BuildHandHut(at, tileId));
        }
    }

    /// <summary>The people part of While you were away (US-182, spec 006 AC-21): what each hand did, day labour, contracts.</summary>
    public sealed class AwayVm
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public List<LineVm> Lines { get; } = new List<LineVm>();

        /// <summary>Totals over the events, for tests and the card's footer: sales by the hands' orders and inputs they bought.</summary>
        public long OrderSales { get; internal set; }
        public long OrderInputs { get; internal set; }
        public long DayLabourPaid { get; internal set; }
    }

    public static class AwayView
    {
        /// <summary>Builds the card from an away advance's events (the sim's LastAwayReport), or null when no people events happened.</summary>
        public static AwayVm People(ViewContext ctx, IReadOnlyList<SimEvent> events)
        {
            Simulation sim = ctx.Sim;
            if (sim.State.People == null) return null;
            var vm = new AwayVm { Title = ctx.Strings.Get("away.title") };
            var hands = new List<long>();
            long away = 0;
            foreach (SimEvent e in events)
            {
                if (e is AwayAdvanceCompleted done) away = done.AppliedSeconds;
                if (e is OrderExecuted o && !hands.Contains(o.HandId)) hands.Add(o.HandId);
            }
            vm.Subtitle = ctx.Strings.Get("away.subtitle", "time", ctx.Fmt.Duration(away));
            foreach (long id in hands)
            {
                Hand h = sim.State.People.Hand(id);
                string name = h == null ? ctx.Strings.Get("away.formerHand") : PeopleWords.Name(ctx, h.Name);
                int harvests = 0, sowings = 0, picks = 0;
                long grams = 0, sales = 0, inputs = 0;
                foreach (SimEvent e in events)
                {
                    if (!(e is OrderExecuted o) || o.HandId != id) continue;
                    if (o.Action == OrderAction.Sowed) { sowings++; inputs -= o.Amount; }
                    else
                    {
                        if (o.Action == OrderAction.Picked) picks++; else harvests++;
                        grams += o.Quantity;
                        sales += o.Amount;
                    }
                }
                vm.OrderSales += sales;
                vm.OrderInputs += inputs;
                if (harvests + picks > 0)
                    vm.Lines.Add(new LineVm(name, ctx.Strings.Plural("away.collected", harvests + picks, "qty", ctx.Fmt.Kg(grams))));
                if (sales > 0) vm.Lines.Add(new LineVm(name, ctx.Strings.Get("away.sold", "amount", ctx.Fmt.Coins(sales))));
                if (sowings > 0) vm.Lines.Add(new LineVm(name, ctx.Strings.Plural("away.sowed", sowings, "cost", ctx.Fmt.Coins(inputs))));
            }
            var waits = new List<string>();
            foreach (SimEvent e in events)
            {
                if (e is OrderWaiting w)
                {
                    string text = ctx.Strings.Get("away.waiting", "place", PeopleWords.Target(ctx, w.Kind, w.Target), "reason", ctx.Strings.Get("order.wait." + w.Reason));
                    if (!waits.Contains(text)) waits.Add(text);
                }
                if (e is DayLabourPaid d) vm.DayLabourPaid += d.Amount;
                if (e is ContractRenewed r) vm.Lines.Add(new LineVm(HandName(ctx, r.HandId), ctx.Strings.Get("away.renewed", "amount", ctx.Fmt.Coins(r.Paid))));
                if (e is ContractLapsed l) vm.Lines.Add(new LineVm(HandName(ctx, l.HandId), ctx.Strings.Get("away.lapsed", "amount", ctx.Fmt.Coins(l.Needed))));
                if (e is HandLeft) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.formerHand"), ctx.Strings.Get("away.left")));
                if (e is CourseFinished cf)
                    vm.Lines.Add(new LineVm(StaffName(ctx, cf.Place, cf.WorkerId), ctx.Strings.Get("away.course", "skill", PeopleWords.Band(ctx, (int)cf.Skill.RoundToLong()))));
                if (e is LongDaysStopped ls) vm.Lines.Add(new LineVm(StaffName(ctx, ls.Place, ls.WorkerId), ctx.Strings.Get("away.longDaysStopped")));
            }
            foreach (string w in waits) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.order"), w));
            if (vm.DayLabourPaid > 0) vm.Lines.Add(new LineVm(ctx.Strings.Get("away.dayLabour"), ctx.Fmt.Coins(vm.DayLabourPaid)));
            if (vm.Lines.Count > ctx.Ui.AwayRowsMax) vm.Lines.RemoveRange(ctx.Ui.AwayRowsMax, vm.Lines.Count - ctx.Ui.AwayRowsMax);
            return vm.Lines.Count == 0 ? null : vm;
        }

        private static string StaffName(ViewContext ctx, long place, long id)
        {
            if (place == 0) return HandName(ctx, id);
            Worker w = ctx.Sim.State.People.WorkerAt(place);
            return w == null || w.Id != id ? ctx.Strings.Get("away.formerWorker") : PeopleWords.Name(ctx, w.Name);
        }

        private static string HandName(ViewContext ctx, long handId)
        {
            Hand h = ctx.Sim.State.People.Hand(handId);
            return h == null ? ctx.Strings.Get("away.formerHand") : PeopleWords.Name(ctx, h.Name);
        }
    }
}
