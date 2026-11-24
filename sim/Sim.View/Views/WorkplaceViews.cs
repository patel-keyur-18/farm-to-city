using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// Supervisors, accidents, conflicts and associations (spec 028 §3.9, Preview 20): a Workplace section on the farm's People sheet and on each business's, saying what each rule needs and where
    /// you stand, and the hiring board's Supervisor post. Each line has a <i>Why?</i> with the exact chance and its factors.
    /// </summary>
    public static class WorkplaceSheet
    {
        public static WorkplaceDef Defs(ViewContext ctx) { return ctx.Sim.Config.Game.Workplace; }

        private static long Days(ViewContext ctx) { return ctx.Sim.Clock.SecondsPerSeason / Sim.Core.Definitions.ClockDef.SecondsPerDay; }

        /// <summary>A chance a day as a chance a season, in percent: "1.8%".</summary>
        public static string Season(ViewContext ctx, Fixed daily) { return ctx.Fmt.Decimal1(daily * Fixed.FromInt((int)Days(ctx)) * Fixed.FromInt(100)) + "%"; }

        public static string Name(ViewContext ctx, Supervisor sv) { return PeopleWords.Name(ctx, sv.Name); }

        /// <summary>What a supervisor gains you in a season at the farm's team today: output, accidents spared and a quarrel settled, in coins; with his cost, what is left.</summary>
        public static void Gains(ViewContext ctx, int stars, long wage, out long output, out long accidents, out long conflicts, out long left)
        {
            Simulation sim = ctx.Sim;
            WorkplaceDef d = Defs(ctx);
            List<IStaff> hands = WorkplaceRules.Workers(sim, 0);
            int covered = System.Math.Min(d.SupervisorSpan, hands.Count);
            long paid = 0;
            for (int i = 0; i < covered; i++) paid += HrRules.Paid(hands[i]);
            output = Money.MulRate(paid, d.OutputPerStar * Fixed.FromInt(stars));
            long each = covered == 0 ? 0 : WorkplaceRules.Bill(sim, hands[0], false) + WorkplaceRules.WageFor(sim, hands[0], WorkplaceRules.OutHours(sim, false));
            Fixed perSeason = d.AccidentChance * Fixed.FromInt((int)Days(ctx)) * (Fixed.One - d.RiskHalf) * Fixed.FromInt(covered);
            accidents = Money.MulRate(each, perSeason);
            Fixed quarrel = WorkplaceRules.ConflictChance(sim, 0) * Fixed.FromInt((int)Days(ctx));
            conflicts = covered == 0 ? 0 : Money.MulRate(paid / covered, quarrel * d.LetResigns);
            left = output + accidents + conflicts - wage;
        }

        /// <summary>The away line for a card nobody answered: what its default did.</summary>
        public static string Defaulted(ViewContext ctx, Sim.Core.Events.HrEventDefaulted hd)
        {
            var s = ctx.Strings;
            IStaff w = WorkRules.Find(ctx.Sim, hd.Place, hd.WorkerId);
            string who = w is Hand h ? PeopleWords.Name(ctx, h.Name) : w is Worker k ? PeopleWords.Name(ctx, k.Name) : s.Get("away.formerWorker");
            string place = hd.Place == 0 ? s.Get("workplace.theFarm") : s.Get("workplace.theBusiness", "business", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(hd.Place))));
            return s.Get("away.hr." + hd.Kind, "name", who, "place", place);
        }

        // ---- the board's post ----

        public static void AddJob(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            WorkplaceDef d = Defs(ctx);
            int n = WorkplaceRules.Workers(sim, 0).Count;
            Supervisor sv = WorkplaceRules.Supervisor(sim);
            long usual = HiringRules.AskedWage(sim, HiringRules.SupervisorPlace, Fixed.FromInt(50), false);
            var job = new LineVm(s.Get("board.supervisor"), s.Get("board.usual", "wage", ctx.Fmt.Coins(usual)), "supervisor", 0) { Icon = "person_hand_m" };
            job.Note = sv != null ? s.Get("supervisor.have", "name", Name(ctx, sv)) : n >= d.SupervisorMinWorkers ? s.Get("supervisor.covers", "span", ctx.Fmt.Integer(d.SupervisorSpan), "n", ctx.Fmt.Integer(n))
                : s.Get("supervisor.needs", "min", ctx.Fmt.Integer(d.SupervisorMinWorkers), "n", ctx.Fmt.Integer(n));
            if (sv == null && n < d.SupervisorMinWorkers) job.NoteTone = Tone.Warn;
            vm.Lines.Add(job);
            if (sv != null || n < d.SupervisorMinWorkers) return;
            Gains(ctx, 3, usual, out long o, out long a, out long c, out long left);
            vm.Lines.Add(new LineVm(s.Get("supervisor.pays"), s.Get(left >= 0 ? "supervisor.paysYes" : "supervisor.paysNo", "amount", ctx.Fmt.Coins(System.Math.Abs(left))), "supervisorPays", 3)
                { Note = s.Get("supervisor.paysNote") });
        }

        /// <summary>The post and its button when the farm has too few hands: greyed with the sim's own reason.</summary>
        public static void AddGreyPost(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (HiringRules.BusinessPlace(sim, HiringRules.SupervisorPlace) != RejectReason.TooFewWorkers) return;
            var line = new LineVm(s.Get("people.postMarket"), s.Get("supervisor.postWhy"), "post") { Icon = "person_hand_m" };
            line.Action = ActionVm.Checked(ctx, "postSupervisor", s.Get("action.post"), ctx.Fmt.Coins(MarketRules.Indexed(sim, sim.Config.Game.People.JobPost.Cost)), ActionKind.Secondary,
                t => new PostJob(t, HiringRules.SupervisorPlace, false));
            vm.Lines.Add(line);
        }

        // ---- the section ----

        /// <summary>The section's lines for a place on their own (for a test).</summary>
        public static SheetVm AddSectionLines(ViewContext ctx, long place)
        {
            var vm = new SheetVm();
            AddSection(ctx, vm, place);
            return vm;
        }

        /// <summary>The Workplace section: the supervisor, accidents, conflicts and the association, each with what it needs.</summary>
        public static void AddSection(ViewContext ctx, SheetVm vm, long place)
        {
            Simulation sim = ctx.Sim;
            if (!WorkplaceRules.On(sim)) return;
            var s = ctx.Strings;
            WorkplaceDef d = Defs(ctx);
            List<IStaff> list = WorkplaceRules.Workers(sim, place);
            int n = list.Count;
            if (n == 0) return;
            Supervisor sv = WorkplaceRules.Supervisor(sim);
            bool farm = place == 0;
            vm.Lines.Add(LineVm.Header(s.Get("workplace.header")));

            // The supervisor.
            if (farm && sv != null)
            {
                var row = new LineVm(s.Get("workplace.supervisor"), Name(ctx, sv), "supervisor", 0)
                    { Icon = "person_hand_m", Stars = PeopleWords.Stars(ctx, (int)sv.Skill.RoundToLong(), (int)sv.Skill.RoundToLong()),
                      Note = s.Get("workplace.covers", "n", ctx.Fmt.Integer(WorkplaceRules.CoveredCount(sim)), "of", ctx.Fmt.Integer(n), "wage", ctx.Fmt.Coins(sv.Wage)) };
                var go = ActionVm.Checked(ctx, "letGoSupervisor", s.Get("action.letGo"), s.Get("workplace.letGoDetail"), ActionKind.Danger, at => new DismissSupervisor(at));
                go.Confirm = new ConfirmVm(s.Get("workplace.letGoQ", "name", Name(ctx, sv)), s.Get("action.letGo"), s.Get("hand.keep"), at => new Command[] { new DismissSupervisor(at) }, noSends: false) { YesIsDanger = true };
                row.Action = go;
                vm.Lines.Add(row);
                vm.Lines.Add(new LineVm(s.Get("workplace.spared"), s.Get("workplace.sparedValue", "a", ctx.Fmt.Integer(sv.Spared), "c", ctx.Fmt.Integer(sv.Settled), "k", ctx.Fmt.Integer(sv.Kept)), "supervisorPays", sv.Skill.RoundToLong() / 20 + 1)
                    { Icon = "coin", Note = s.Get("workplace.sparedNote") });
            }
            else
            {
                string value = !farm ? s.Get("workplace.needsN", "min", ctx.Fmt.Integer(d.SupervisorMinWorkers), "n", ctx.Fmt.Integer(n))
                    : n >= d.SupervisorMinWorkers ? s.Get("workplace.noneHire") : s.Get("workplace.needsN", "min", ctx.Fmt.Integer(d.SupervisorMinWorkers), "n", ctx.Fmt.Integer(n));
                var row = new LineVm(s.Get("workplace.supervisor"), value, "supervisor", 0) { Icon = "person_hand_m", Note = s.Get(farm ? "workplace.supervisorNote" : "workplace.supervisorFarm") };
                if (farm && n >= d.SupervisorMinWorkers) row.Action = ActionVm.Open("supervisorBoard", s.Get("workplace.hireOne"), new SheetRequest("Word", HiringRules.SupervisorPlace));
                vm.Lines.Add(row);
            }

            // Accidents: everyone at work.
            Fixed chance = WorkplaceRules.AccidentChance(sim, list[0], true);
            vm.Lines.Add(new LineVm(s.Get("workplace.accidents"), s.Get("workplace.accidentsValue", "pct", Season(ctx, chance)), "accident", place)
                { Icon = "coin", Note = s.Get("workplace.accidentsNote", "hours", ctx.Fmt.Integer(WorkplaceRules.OutHours(sim, false))) });

            // Conflicts.
            string quarrel = n < d.ConflictMinWorkers ? s.Get("workplace.needsN", "min", ctx.Fmt.Integer(d.ConflictMinWorkers), "n", ctx.Fmt.Integer(n))
                : farm && sv != null ? s.Get("workplace.settled") : Season(ctx, WorkplaceRules.ConflictChance(sim, place));
            vm.Lines.Add(new LineVm(s.Get("workplace.conflicts"), quarrel, "conflict", place) { Icon = "coin", Note = s.Get(n < d.ConflictMinWorkers ? "workplace.conflictsNeed" : "workplace.conflictsNote") });

            // The association: the farm's.
            if (n >= d.AssocMinWorkers || (farm && n >= 1))
            {
                string value = n < d.AssocMinWorkers ? s.Get("workplace.needsN", "min", ctx.Fmt.Integer(d.AssocMinWorkers), "n", ctx.Fmt.Integer(n))
                    : s.Get(AssociationNow(sim, list) ? "workplace.assocLikely" : "workplace.assocNone");
                vm.Lines.Add(new LineVm(s.Get("workplace.assoc"), value, "association", place) { Icon = "coin", Note = s.Get(n < d.AssocMinWorkers ? "workplace.assocNeed" : "workplace.assocNote") });
            }
        }

        /// <summary>Whether one of the three triggers holds now (the next close may form an association).</summary>
        public static bool AssociationNow(Simulation sim, List<IStaff> list)
        {
            WorkplaceDef d = sim.Config.Game.Workplace;
            PlaceRelations r = sim.State.People.Workplace.Relations(0);
            return WorkplaceRules.PayLow(sim, list) || (r != null && r.TiredCloses >= d.AssocCloses) || sim.State.People.Reputation < d.AssocReputation;
        }
    }
}
