using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Commands;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The <em>Town life</em> tab of the town hall's sheet (US-151, spec 026 §7): approval, the five scores of your town's name (each a ? with its parts), what the name
    /// does, crime and inequality. A view: it reads the state and changes nothing.
    /// </summary>
    public static class TownLifeSheet
    {
        public const string Tab = "life", PromiseKind = "Promise";

        public static void Rows(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!TownLifeRules.Known(sim))
            {
                vm.Lines.Add(new LineVm(s.Get("life.notYet"), null) { Dim = true });
                return;
            }
            TownLifeState st = TownLifeRules.State(sim);

            vm.Lines.Add(LineVm.Header(s.Get("life.approvalHeader")));
            vm.Lines.Add(new LineVm(s.Get("life.approval"), ctx.Fmt.Integer(st.Approval.RoundToLong()), "lifeApproval", 0) { Note = s.Get("life.approvalNote") });
            if (st.ApprovalHistory.Count >= 2)
            {
                var trend = new TrendVm { FirstCaption = s.Get("life.trendFirst", "n", ctx.Fmt.Integer(st.ApprovalHistory.Count)), LastCaption = s.Get("life.trendNow") };
                foreach (int a in st.ApprovalHistory) trend.Values.Add(a);
                vm.Lines.Add(new LineVm(null, null) { Trend = trend });
            }

            Petitions(ctx, vm, st);

            vm.Lines.Add(LineVm.Header(s.Get("life.nameHeader")));
            for (int i = 0; i < TownLifeDef.Scores; i++)
            {
                int score = (int)st.Reputation[i].RoundToLong();
                vm.Lines.Add(new LineVm(s.Get("life.score." + i), ctx.Fmt.Integer(score), "lifeScore", i) { Meter = score });
            }
            vm.Lines.Add(new LineVm(PullLine(ctx), null, "lifePull", 0) { Dim = true });

            vm.Lines.Add(LineVm.Header(s.Get("life.fairHeader")));
            vm.Lines.Add(new LineVm(s.Get("life.crime"), ctx.Fmt.Integer(st.Crime.RoundToLong()), "lifeCrime", 0) { Note = s.Get("life.crimeNote") });
            if (st.RatioKnown) vm.Lines.Add(new LineVm(s.Get("life.ratio", "n", ctx.Fmt.Decimal1(st.Ratio)), null, "lifeRatio", 0) { Note = s.Get("life.ratioNote") });
            else vm.Lines.Add(new LineVm(s.Get("life.ratioFew"), null, "lifeRatio", 0) { Dim = true });
        }

        /// <summary>"Families come about 13% faster and the district's grants are 3.6% higher": what the name does (spec 026 §3.4, §3.7).</summary>
        public static string PullLine(ViewContext ctx)
        {
            var s = ctx.Strings;
            Fixed pull = TownLifeRules.Pull(ctx.Sim);
            long diff = (pull - Fixed.FromInt(100)).RoundToLong();
            Fixed grants = (TownLifeRules.GrantFactor(ctx.Sim) - Fixed.One) * Fixed.FromInt(100);
            string families = diff == 0 ? s.Get("life.pull.same") : diff > 0 ? s.Get("life.pull.faster", "n", ctx.Fmt.Integer(diff)) : s.Get("life.pull.slower", "n", ctx.Fmt.Integer(-diff));
            return grants > Fixed.Zero ? s.Get("life.pull.withGrants", "families", families, "g", ctx.Fmt.Decimal1(grants)) : families;
        }

        // ---- petitions (US-150) ----

        /// <summary>"6 families on the east side can't reach the clinic" (spec 026 §3.2).</summary>
        public static string Text(ViewContext ctx, TownPetition p)
        {
            var s = ctx.Strings;
            return s.Plural("petition.topic." + p.Topic, p.Households, "side", s.Get("petition.side." + p.Side));
        }

        /// <summary>The name of the season that the k-th coming close ends (1 is this season's), and the time until it.</summary>
        public static string CloseName(ViewContext ctx, int k, out long inSeconds)
        {
            ClockDef clock = ctx.Sim.Clock;
            long now = ctx.Sim.State.Clock.SimTime, season = clock.SecondsPerSeason;
            long at = (now / season + k) * season;
            inSeconds = at - now;
            return ctx.Strings.Get("season." + Calendar.SeasonAt(at - 1, clock).ToString().ToLowerInvariant());
        }

        private static void Petitions(ViewContext ctx, SheetVm vm, TownLifeState st)
        {
            var s = ctx.Strings;
            int standing = 0;
            int waiting = 0;
            foreach (TownPetition p in st.Petitions) { if (!p.Ended) standing++; if (p.Status == PetitionStatus.Open) waiting++; }
            vm.Lines.Add(LineVm.Header(waiting > 0 ? s.Get("petition.headerWaiting", "n", ctx.Fmt.Integer(waiting)) : s.Get("petition.header")));
            if (standing == 0) vm.Lines.Add(new LineVm(s.Get("petition.none"), null) { Dim = true });
            foreach (TownPetition p in st.Petitions)
            {
                if (p.Ended) continue;
                int id = p.Id;
                var line = new LineVm(Text(ctx, p), null, "lifePetition", id);
                if (p.Status == PetitionStatus.Open)
                {
                    line.Note = s.Get("petition.raised", "n", s.Plural("petition.families", p.Households));
                    line.Action = ActionVm.Open("promise:" + id, s.Get("petition.promise"), new SheetRequest(PromiseKind, id));
                    line.Also = ActionVm.Checked(ctx, "decline:" + id, s.Get("petition.decline"), null, ActionKind.Secondary, at => new AnswerPetition(at, id, 0));
                }
                else
                {
                    string close = CloseName(ctx, System.Math.Max(1, p.PromisedBy - st.Closes), out _);
                    PetitionDef d = PetitionRules.Def(ctx.Sim);
                    line.Note = s.Get("petition.promised", "close", close, "kept", Signed(ctx, d.Kept), "broken", Signed(ctx, d.Broken));
                    line.Badge = s.Get("petition.promisedTag");
                }
                vm.Lines.Add(line);
            }
            int shown = 0;
            for (int i = st.Petitions.Count - 1; i >= 0 && shown < 3; i--)
            {
                TownPetition p = st.Petitions[i];
                if (!p.Ended) continue;
                shown++;
                if (shown == 1) vm.Lines.Add(LineVm.Header(s.Get("petition.endedHeader")));
                vm.Lines.Add(new LineVm(Text(ctx, p), s.Get("petition.end." + p.Status, "n", Signed(ctx, Points(ctx, p.Status))), "lifePetition", p.Id) { Dim = true });
            }
        }

        private static int Points(ViewContext ctx, PetitionStatus how)
        {
            PetitionDef d = PetitionRules.Def(ctx.Sim);
            return how == PetitionStatus.Done ? d.Done : how == PetitionStatus.Kept ? d.Kept : how == PetitionStatus.Declined ? d.Declined : how == PetitionStatus.Ignored ? d.Ignored : d.Broken;
        }

        /// <summary>"+6" or "−8" (a real minus sign).</summary>
        public static string Signed(ViewContext ctx, int n) { return n >= 0 ? "+" + ctx.Fmt.Integer(n) : "−" + ctx.Fmt.Integer(-n); }

        /// <summary>The Promise sheet (spec 026 §3.2): the petition, and a choice of when to keep it by. Nothing is spent; a promise can't be withdrawn.</summary>
        public static SheetVm Promise(ViewContext ctx, long id)
        {
            var s = ctx.Strings;
            TownLifeState st = TownLifeRules.State(ctx.Sim);
            TownPetition p = st == null ? null : st.Petitions.Find(x => x.Id == id);
            if (p == null || p.Status != PetitionStatus.Open) return new SheetVm { Icon = "menu", Title = ctx.Reject(RejectReason.PetitionClosed) };
            PetitionDef d = PetitionRules.Def(ctx.Sim);
            var vm = new SheetVm { Icon = "menu", Title = s.Get("petition.promiseTitle"), Subtitle = Text(ctx, p) };
            foreach (int k in d.PromiseCloses)
            {
                int closes = k;
                string name = CloseName(ctx, closes, out long inSeconds);
                var line = new LineVm(s.Get("petition.by", "close", name), null) { Note = s.Get("petition.byIn", "time", ctx.Fmt.Duration(inSeconds)) };
                line.Action = ActionVm.Checked(ctx, "promise:" + id + ":" + closes, s.Get("petition.promise"), null, ActionKind.Primary, at => new AnswerPetition(at, (int)id, closes));
                vm.Lines.Add(line);
            }
            vm.Lines.Add(new LineVm(s.Get("petition.promiseNote", "kept", Signed(ctx, d.Kept), "broken", Signed(ctx, d.Broken)), null) { Dim = true });
            return vm;
        }
    }
}
