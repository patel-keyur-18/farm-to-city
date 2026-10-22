using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Progress;

namespace Sim.View.Views
{
    /// <summary>The letter page's buttons: the platform runs them (Continue and Skip send the guide's commands).</summary>
    public static class LetterIds
    {
        public const string Continue = "letter:continue";
        public const string ShowMe = "letter:showMe";
        public const string Skip = "letter:skip";
        public const string Yes = SettingsIds.Yes;
        public const string NotNow = SettingsIds.NotNow;
    }

    /// <summary>One page of the letter (spec 009 §3.1, §7, AC-11).</summary>
    public sealed class LetterVm
    {
        public string ChapterId { get; internal set; }
        public string Title { get; internal set; }
        public string Text { get; internal set; }
        public string Signature { get; internal set; }

        /// <summary>"Page 2 of 5 · read it again from the Menu".</summary>
        public string Footer { get; internal set; }

        /// <summary>What <see cref="LetterIds.ShowMe"/> opens, or null (no Show me).</summary>
        public SheetRequest? ShowMe { get; internal set; }

        /// <summary>The page asks about notifications (<c>comeBack</c>, ADR 0045 §2): Yes, nudge me / Not now.</summary>
        public bool Asks { get; internal set; }

        public List<CardButton> Buttons { get; } = new List<CardButton>();
    }

    /// <summary>The first session's nudge pill (ADR 0045 §1, AC-13): one line, or the tour lines to rotate, and the highlight.</summary>
    public sealed class NudgeVm
    {
        public List<string> Lines { get; } = new List<string>();

        /// <summary>What a tap on each line opens (null: replay the highlight).</summary>
        public List<SheetRequest?> Opens { get; } = new List<SheetRequest?>();

        /// <summary>What glows: <c>bed:&lt;id&gt;</c>, <c>tile:&lt;id&gt;</c>, <c>hut</c> or <c>store</c>; null for nothing.</summary>
        public string Highlight { get; internal set; }
    }

    /// <summary>
    /// The guide (spec 009 §3.2–3.3, §3.7): watches the state and the events after every command and Sync, works out which
    /// first-session steps are done (it returns <c>CompleteStep</c> commands for the host to send) and which letter chapters
    /// are due, in priority order, each once. Seeing a chapter is the <c>SeeChapter</c> command sent when the page is closed, so
    /// "seen" saves and replays with the town. What happened since launch (events, sheets visited, hours of low Care) is kept
    /// here in memory only: it starts again after a relaunch, and a chapter whose moment passed comes the next time.
    /// </summary>
    public sealed class Guide
    {
        private readonly List<int> _queue = new List<int>(); // due chapters, by index (= priority)
        private readonly HashSet<string> _happened = new HashSet<string>(StringComparer.Ordinal); // membership only
        private readonly HashSet<string> _sheets = new HashSet<string>(StringComparer.Ordinal);
        private long _longestSown;
        private int _careLowHours;

        private static OnboardingDefs Defs(Simulation sim) { return sim.Config.Game?.Onboarding; }

        /// <summary>True while the first session's steps run: not skipped and not finished.</summary>
        public static bool FirstSessionActive(Simulation sim)
        {
            GuidanceState g = sim.State.Guidance;
            return g != null && !g.Skipped && g.Step < Defs(sim).Steps.Count;
        }

        /// <summary>Reads a batch of events (a command's, a live Sync's, or an away advance's).</summary>
        public void Observe(Simulation sim, IReadOnlyList<SimEvent> events)
        {
            OnboardingDefs d = Defs(sim);
            if (d == null) return;
            bool away = false;
            int hours = 0;
            foreach (SimEvent e in events)
            {
                _happened.Add(e.GetType().Name);
                if (e is AwayAdvanceCompleted) away = true;
                if (e is HourStarted) hours++;
                if (e is CropSown c) _longestSown = Math.Max(_longestSown, c.ReadyAt - c.SimTime);
            }
            // Care below 1.0 with crops to tend, counted in live hours only (spec 009 §3.3, "labour").
            if (!away && hours > 0) _careLowHours = FarmRules.LabourDemand(sim) > Fixed.Zero && sim.State.Farm.CareRatio < Fixed.One ? _careLowHours + hours : 0;

            GuidanceState g = sim.State.Guidance;
            for (int i = 0; i < d.Chapters.Count; i++)
            {
                if (g.HasSeen(i) || _queue.Contains(i)) continue;
                foreach (ConditionDef t in d.Chapters[i].Triggers)
                    if (Fires(sim, t, events)) { Enqueue(i); break; }
            }
        }

        /// <summary>A rejected command (the <c>rejection</c> trigger: money short for a sowing, a hut or a hire).</summary>
        public void Rejected(Simulation sim, Command command, RejectReason reason)
        {
            OnboardingDefs d = Defs(sim);
            if (d == null) return;
            for (int i = 0; i < d.Chapters.Count; i++)
            {
                if (sim.State.Guidance.HasSeen(i) || _queue.Contains(i)) continue;
                foreach (ConditionDef t in d.Chapters[i].Triggers)
                    if (t.Trigger == TriggerKind.Rejection && t.Value == reason.ToString() && Contains(t.Names, command.GetType().Name)) { Enqueue(i); break; }
            }
        }

        /// <summary>The host showed the While you were away card (the <c>awayCard</c> trigger: "Welcome back" follows it).</summary>
        public void AwayCardShown(Simulation sim) { EnqueueKind(sim, TriggerKind.AwayCard); }

        private void EnqueueKind(Simulation sim, TriggerKind kind)
        {
            OnboardingDefs d = Defs(sim);
            if (d == null) return;
            for (int i = 0; i < d.Chapters.Count; i++)
            {
                if (sim.State.Guidance.HasSeen(i) || _queue.Contains(i)) continue;
                foreach (ConditionDef t in d.Chapters[i].Triggers)
                    if (t.Trigger == kind) { Enqueue(i); break; }
            }
        }

        /// <summary>A sheet the player opened (the <c>sheetVisited</c> goal): "Founder", "Store"...</summary>
        public void SheetVisited(string kind, Simulation sim = null)
        {
            _sheets.Add(kind);
            if (kind != "Store" || sim?.Config.Game?.Onboarding == null || MarketRules.TraderBuys(sim)) return;
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == GoodKind.Produce && sim.State.Market.InStore(g) > sim.State.Market.OnOffer(g)) { EnqueueKind(sim, TriggerKind.StoreTruckAway); return; }
        }

        /// <summary>The steps whose goals hold now, in order, as commands to send (a goal reached early skips its step, AC-3).</summary>
        public List<Command> StepCommands(Simulation sim, long at)
        {
            var list = new List<Command>();
            if (!FirstSessionActive(sim)) return list;
            OnboardingDefs d = Defs(sim);
            for (int s = sim.State.Guidance.Step; s < d.Steps.Count && GoalHolds(sim, d.Steps[s]); s++) list.Add(new CompleteStep(at, d.Steps[s].Id));
            return list;
        }

        /// <summary>
        /// The chapter to open now, or null: a finished step's page that reacts to it, then the current step's page, then the due
        /// pages in priority order.
        /// </summary>
        public string Next(Simulation sim)
        {
            OnboardingDefs d = Defs(sim);
            if (d == null) return null;
            GuidanceState g = sim.State.Guidance;
            if (!g.Skipped)
                for (int s = 0; s < g.Step && s < d.Steps.Count; s++)
                    if (d.Steps[s].PageAfter && !g.HasSeen(d.Steps[s].Chapter)) return d.Chapters[d.Steps[s].Chapter].Id;
            if (FirstSessionActive(sim) && !d.Steps[g.Step].PageAfter)
            {
                int ch = d.Steps[g.Step].Chapter;
                if (!g.HasSeen(ch)) return d.Chapters[ch].Id;
            }
            _queue.RemoveAll(g.HasSeen);
            return _queue.Count == 0 ? null : d.Chapters[_queue[0]].Id;
        }

        /// <summary>The page was closed (Continue, Show me, Yes or Not now): it's seen.</summary>
        public Command Close(string chapterId, long at) { return new SeeChapter(at, chapterId); }

        /// <summary>"I'll find my own way": the page is seen and the first session ends (spec 009 §3.2).</summary>
        public Command[] Skip(string chapterId, long at) { return new Command[] { new SeeChapter(at, chapterId), new SkipFirstSession(at) }; }

        /// <summary>The nudge pill now, or null (no step, its page not read yet, or a step that waits only on a page).</summary>
        public NudgeVm Nudge(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            if (!FirstSessionActive(sim)) return null;
            StepDef st = Defs(sim).Steps[sim.State.Guidance.Step];
            if (!(st.PageAfter || sim.State.Guidance.HasSeen(st.Chapter)) || st.Goal.Goal == GoalKind.ChapterClosed) return null;
            // The sale nudge never points at an empty store or a trader who's bought his fill today (ADR 0046 §3).
            if (st.Highlight == "store" && !TraderWouldBuy(sim)) return null;
            var vm = new NudgeVm { Highlight = Highlight(sim, st.Highlight) };
            vm.Lines.Add(ctx.Strings.Get("nudge." + st.Id));
            vm.Opens.Add(null);
            bool waiting = false, ready = false;
            foreach (GardenBed b in sim.State.Farm.Beds)
            {
                if (b.State == BedState.Growing) waiting = true;
                if (b.State == BedState.Ready) ready = true;
            }
            if (waiting && !ready)
                foreach (string sheet in st.Tour)
                {
                    vm.Lines.Add(ctx.Strings.Get("tour." + sheet.Split(':')[0]));
                    vm.Opens.Add(Letter.Sheet(sim, sheet));
                }
            return vm;
        }

        private static bool TraderWouldBuy(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            MarketState m = sim.State.Market;
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == GoodKind.Produce && m.InStore(g) > m.OnOffer(g) && MarketRules.TraderCapacityLeft(sim, g) > 0) return true;
            return false;
        }

        private void Enqueue(int chapter)
        {
            int at = _queue.BinarySearch(chapter);
            if (at < 0) _queue.Insert(~at, chapter);
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (string n in names) if (n == name) return true;
            return false;
        }

        private bool AnyHappened(IReadOnlyList<string> names)
        {
            foreach (string n in names) if (_happened.Contains(n)) return true;
            return false;
        }

        private bool Fires(Simulation sim, ConditionDef t, IReadOnlyList<SimEvent> events)
        {
            switch (t.Trigger)
            {
                case TriggerKind.Event:
                    foreach (SimEvent e in events) if (Contains(t.Names, e.GetType().Name)) return true;
                    return false;
                case TriggerKind.SeasonStart:
                    foreach (SimEvent e in events) if (e is SeasonStarted s && s.Season.ToString().ToLowerInvariant() == t.Value) return true;
                    return false;
                case TriggerKind.StoreAbovePercent:
                    long cap = Store.CapacityGrams(sim);
                    return cap > 0 && Store.UsedGrams(sim) * 100 > cap * t.Number;
                case TriggerKind.TraderFull:
                    if (!MarketRules.TraderBuys(sim)) return false; // the truck away isn't "full" (spec 011: truckAway)
                    var goods = sim.Config.Game.Market.Goods;
                    for (int g = 0; g < goods.Count; g++)
                        if (goods[g].Kind == GoodKind.Produce && sim.Config.Game.Market.Region.TraderBuyPerDay(g) > 0 && MarketRules.TraderCapacityLeft(sim, g) == 0) return true;
                    return false;
                case TriggerKind.CareBelowForHours:
                    return _careLowHours >= t.Number;
                case TriggerKind.CashBelowHutCost:
                    GameDefs game = sim.Config.Game;
                    if (game.People == null || !sim.State.Guidance.HasSeen(t.AfterChapter) || EraRules.Huts(sim) > 0 || sim.State.People.Constructions.Count > 0) return false;
                    return sim.State.Ledger.Cash < MarketRules.Indexed(sim, game.Buildings[game.People.HandHut].Value);
                default: return false; // Step: opened by its step; Rejection: see Rejected; AwayCard: see AwayCardShown
            }
        }

        private bool GoalHolds(Simulation sim, StepDef st)
        {
            ConditionDef goal = st.Goal;
            switch (goal.Goal)
            {
                case GoalKind.ChapterClosed: return sim.State.Guidance.HasSeen(st.Chapter);
                case GoalKind.BedGrowing:
                    foreach (GardenBed b in sim.State.Farm.Beds) if (b.State != BedState.Empty) return true;
                    return _happened.Contains("GardenPicked");
                case GoalKind.SheetVisited: return _sheets.Contains(goal.Value);
                case GoalKind.Event: return AnyHappened(goal.Names);
                case GoalKind.CropSown:
                    long need = goal.Number * 3600L;
                    if (_longestSown >= need) return true;
                    foreach (Crop c in sim.State.Farm.Crops) if (c.ReadyAt - c.SownAt >= need) return true;
                    return false;
                default: return false;
            }
        }

        /// <summary>The step's highlight with <c>readyBed</c> resolved to the bed that's ready (or the first bed).</summary>
        private static string Highlight(Simulation sim, string highlight)
        {
            if (highlight != "readyBed") return highlight;
            foreach (GardenBed b in sim.State.Farm.Beds) if (b.State == BedState.Ready) return "bed:" + b.Id;
            return sim.State.Farm.Beds.Count == 0 ? null : "bed:" + sim.State.Farm.Beds[0].Id;
        }
    }

    /// <summary>The letter: a page (AC-11), the list to read again (AC-12), and where Show me goes.</summary>
    public static class Letter
    {
        /// <summary>
        /// A page. <paramref name="reading"/>: opened again from the list (Continue only, no command). The <c>comeBack</c> page asks
        /// about notifications while they are off and nothing has asked yet (ADR 0045 §2).
        /// </summary>
        public static LetterVm Page(ViewContext ctx, string chapterId, bool reading, SettingsInfo info, int notifyAsked)
        {
            Simulation sim = ctx.Sim;
            OnboardingDefs d = sim.Config.Game.Onboarding;
            int index = d.IndexOfChapter(chapterId);
            ChapterDef ch = d.Chapters[index];
            var s = ctx.Strings;
            GuidanceState g = sim.State.Guidance;
            int seen = g.Seen.Count + (g.HasSeen(index) ? 0 : 1);
            int page = g.HasSeen(index) ? g.Seen.IndexOf(index) + 1 : seen;
            var vm = new LetterVm
            {
                ChapterId = chapterId,
                Title = s.Get("letterTitle." + chapterId),
                Text = Text(ctx, chapterId, info, out bool asks),
                Signature = s.Get("letter.signature"),
                Footer = s.Get("letter.footer", "page", ctx.Fmt.Integer(page), "count", ctx.Fmt.Integer(seen)),
                ShowMe = Sheet(sim, ch.Action),
            };
            vm.Asks = asks && !reading && notifyAsked == 0 && !info.NotificationsOn;
            if (!vm.Asks && asks) vm.Text = s.Get("letter.comeBack.on", "crop", LongCropName(ctx), "time", LongCropTime(ctx, info));
            if (vm.Asks)
            {
                vm.Buttons.Add(new CardButton(LetterIds.Yes, s.Get("card.yesNudge"), ActionKind.Primary));
                vm.Buttons.Add(new CardButton(LetterIds.NotNow, s.Get("card.notNow"), ActionKind.Secondary));
                return vm;
            }
            if (vm.ShowMe != null) vm.Buttons.Add(new CardButton(LetterIds.ShowMe, s.Get("letter.showMe"), ActionKind.Secondary));
            vm.Buttons.Add(new CardButton(LetterIds.Continue, s.Get("letter.continue"), ActionKind.Primary));
            if (!reading && Guide.FirstSessionActive(sim) && d.Steps[g.Step].Chapter == index && g.Step > 0 && g.Step < d.Steps.Count - 1)
                vm.Buttons.Add(new CardButton(LetterIds.Skip, s.Get("letter.skip"), ActionKind.Secondary));
            return vm;
        }

        /// <summary>Menu → Letter: the pages seen, in the order seen, then "Becoming a Hamlet" (AC-12). Reading sends nothing.</summary>
        public static SheetVm List(ViewContext ctx)
        {
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "letter", Title = s.Get("letter.listTitle"), Subtitle = s.Get("letter.listSubtitle") };
            OnboardingDefs d = ctx.Sim.Config.Game.Onboarding;
            foreach (int c in ctx.Sim.State.Guidance.Seen)
            {
                string id = d.Chapters[c].Id;
                vm.Lines.Add(new LineVm(s.Get("letterTitle." + id), null) { Action = ActionVm.Open("read:" + id, s.Get("letter.read"), new SheetRequest("LetterPage", c)) });
            }
            vm.Lines.Add(new LineVm(s.Get("progress.title"), null) { Action = ActionVm.Open("progress", s.Get("action.open"), new SheetRequest("Progress", 0)) });
            return vm;
        }

        /// <summary>
        /// The string keys the onboarding content needs, each with the field that needs it (spec 009 AC-1, AC-10): a page's title
        /// and text (a season page's opening line), a step's nudge, a tour line. Missing ones are returned as "field: key".
        /// </summary>
        public static List<string> MissingStrings(OnboardingDefs d, Text.StringTable t)
        {
            var missing = new List<string>();
            void Need(string field, string key) { if (!t.Has(key)) missing.Add(field + ": " + key); }
            for (int i = 0; i < d.Chapters.Count; i++)
            {
                string id = d.Chapters[i].Id, f = "onboarding.chapters[" + i + "]";
                Need(f, "letterTitle." + id);
                Need(f, id.StartsWith("season.", StringComparison.Ordinal) ? "hint.intro." + id.Substring(7) : "letter." + id);
                if (id == "comeBack") Need(f, "letter.comeBack.on");
                if (d.Chapters[i].Action != null && !KnownAction(d.Chapters[i].Action)) missing.Add(f + ".action: unknown sheet " + d.Chapters[i].Action);
            }
            for (int i = 0; i < d.Steps.Count; i++)
            {
                StepDef st = d.Steps[i];
                string f = "onboarding.steps[" + i + "]";
                if (st.Goal.Goal != GoalKind.ChapterClosed) Need(f, "nudge." + st.Id);
                foreach (string sheet in st.Tour)
                {
                    Need(f + ".tour", "tour." + sheet.Split(':')[0]);
                    if (!KnownAction(sheet)) missing.Add(f + ".tour: unknown sheet " + sheet);
                }
            }
            return missing;
        }

        private static bool KnownAction(string action)
        {
            switch (action.Split(':')[0])
            {
                case "bed": case "field": case "founder": case "store": case "alerts": case "report": case "people": case "money": case "progress": case "orders": return true;
                default: return false;
            }
        }

        /// <summary>A content action ("store", "bed:1", "field:20") as the sheet it opens.</summary>
        public static SheetRequest? Sheet(Simulation sim, string action)
        {
            if (action == null) return null;
            string[] p = action.Split(':');
            long id = p.Length > 1 ? long.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
            switch (p[0])
            {
                case "bed": return new SheetRequest("Bed", id);
                case "field": return new SheetRequest("Tile", id);
                case "founder": return new SheetRequest("Founder", 0);
                case "store": return new SheetRequest("Store", 0);
                case "alerts": return new SheetRequest("Alerts", 0);
                case "report":
                    var reports = sim.State.Finance.Reports;
                    return reports.Count == 0 ? (SheetRequest?)new SheetRequest("Money", 0) : new SheetRequest("Report", reports[reports.Count - 1].Period);
                case "people": return new SheetRequest("People", 0);
                case "money": return new SheetRequest("Money", 0);
                case "progress": return new SheetRequest("Progress", 0);
                case "orders": return new SheetRequest("Orders", 0);
                default: throw new ArgumentException("unknown letter action '" + action + "'");
            }
        }

        private static string Text(ViewContext ctx, string id, SettingsInfo info, out bool asks)
        {
            asks = id == "comeBack";
            var s = ctx.Strings;
            if (asks) return s.Get("letter.comeBack", "crop", LongCropName(ctx), "time", LongCropTime(ctx, info));
            if (id.StartsWith("season.", StringComparison.Ordinal)) return Hints.Season(ctx, id.Substring(7));
            return s.Get("letter." + id);
        }

        private static Crop LongCrop(ViewContext ctx)
        {
            Crop best = null;
            foreach (Crop c in ctx.Sim.State.Farm.Crops) if (best == null || c.ReadyAt - c.SownAt > best.ReadyAt - best.SownAt) best = c;
            return best;
        }

        private static string LongCropName(ViewContext ctx)
        {
            Crop c = LongCrop(ctx);
            return c == null ? ctx.Strings.Get("letter.yourCrop") : ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Crops[c.Def].NameKey).ToLowerInvariant();
        }

        private static string LongCropTime(ViewContext ctx, SettingsInfo info)
        {
            Crop c = LongCrop(ctx);
            long readyIn = c == null ? 0 : Math.Max(0, c.ReadyAt - ctx.Now);
            return WallTime.Ready(ctx, info.NowWallUtc + readyIn, info);
        }
    }

    /// <summary>
    /// The seasonal crop hints (spec 009 §3.4, AC-6) and the seed-keeping hint (§3.5, AC-7), built from content: the crops sown in
    /// the season, the field good at its highest seasonal price, and the seeds that cost more next season.
    /// </summary>
    public static class Hints
    {
        public static string Season(ViewContext ctx, string seasonId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            FarmDefs farm = sim.Config.Game.Farm;
            MarketDefs market = sim.Config.Game.Market;
            int season = SeasonIndex(seasonId), next = (season + 1) % 4;
            var crops = new List<string>();
            int best = -1;
            Fixed bestFactor = Fixed.One;
            var dearer = new List<string>();
            foreach (CropDef c in farm.Crops)
            {
                string name = s.Get(c.NameKey).ToLowerInvariant();
                if (c.SowSeasons[season]) crops.Add(name);
                int good = c.OutputGood;
                Fixed f = market.Region.SeasonalFactor(good, season);
                if (f > bestFactor) { best = good; bestFactor = f; }
                int seed = c.SeedGood;
                if (market.Region.SeasonalFactor(seed, next) > market.Region.SeasonalFactor(seed, season)) dearer.Add(name);
            }
            var parts = new List<string> { s.Get("hint.intro." + seasonId), s.Get("hint.sow", "crops", And(ctx, crops)) };
            if (best >= 0) parts.Add(s.Get("hint.best", "good", Cap(s.Get(market.Goods[best].NameKey))));
            if (dearer.Count > 0) parts.Add(s.Get("hint.dearer", "seeds", And(ctx, dearer)));
            if (seasonId == "winter" && SeedKeeping(sim)) parts.Add(s.Get("hint.seedKeeping"));
            return string.Join(" ", parts);
        }

        /// <summary>The seed-keeping hint applies: wheat or lentils in store and the household eating from it (§3.5).</summary>
        public static bool SeedKeeping(Simulation sim)
        {
            if (!sim.State.Farm.Founder.EatFromStore) return false;
            MarketDefs m = sim.Config.Game.Market;
            foreach (string g in new[] { "wheat", "lentils" })
            {
                int i = m.IndexOf(g);
                if (i >= 0 && sim.State.Market.InStore(i) > 0) return true;
            }
            return false;
        }

        private static int SeasonIndex(string id)
        {
            switch (id)
            {
                case "spring": return 0;
                case "summer": return 1;
                case "monsoon": return 2;
                case "winter": return 3;
                default: throw new ArgumentException("unknown season '" + id + "'");
            }
        }

        private static string Cap(string word) { return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word.Substring(1); }

        /// <summary>"a", "a and b", "a, b and c".</summary>
        private static string And(ViewContext ctx, List<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " " + ctx.Strings.Get("list.and") + " " + items[items.Count - 1];
        }
    }

    /// <summary>"Becoming a Hamlet" (spec 009 §3.6, §7, AC-9): this season's acres and their Care, the hut, the profitable seasons.</summary>
    public static class ProgressSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            OnboardingDefs d = sim.Config.Game.Onboarding;
            EraState e = sim.State.Era;
            var vm = new SheetVm { Icon = "hut", Title = s.Get("progress.title") };
            if (e.Current + 1 >= d.Eras.Count)
            {
                vm.Subtitle = s.Get("progress.done");
                return vm;
            }
            EraDef next = d.Eras[e.Current + 1];
            vm.Subtitle = s.Get("progress.subtitle");
            long period = sim.Period;
            vm.Lines.Add(new LineVm(s.Get("progress.acres"), s.Get("progress.acresValue", "n", ctx.Fmt.Integer(EraRules.Acres(sim, period)),
                "need", ctx.Fmt.Integer(next.AcresHarvestedInSeason)), "era", 1));
            if (EraRules.Acres(sim, period) > 0)
                vm.Lines.Add(new LineVm(s.Get("progress.care"), s.Get("progress.careValue", "pct", Percent(ctx, EraRules.MeanCare(sim, period)),
                    "need", Percent(ctx, next.MeanCareAtLeast)), "era", 2));
            vm.Lines.Add(new LineVm(s.Get("progress.hut"), HutText(ctx), "era", 3));
            vm.Lines.Add(new LineVm(s.Get("progress.profit"), ProfitText(ctx), "era", 4));
            return vm;
        }

        internal static string Percent(ViewContext ctx, Fixed f) { return ctx.Fmt.Integer(f.MulRatio(100, 1).RoundToLong()) + "%"; }

        private static string HutText(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            if (EraRules.Huts(sim) > 0) return ctx.Strings.Get("progress.hutDone");
            var cs = sim.State.People?.Constructions;
            if (cs != null && cs.Count > 0)
                return ctx.Strings.Get("progress.hutBuilding", "time", ctx.Fmt.Duration(Math.Max(0, cs[0].ReadyAt - ctx.Now)));
            return ctx.Strings.Get("progress.hutNone");
        }

        private static string ProfitText(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            foreach (Sim.Core.Finance.SeasonReport r in sim.State.Finance.Reports)
                if (r.FarmProfit > 0) return ctx.Strings.Get("progress.profitYes", "season", ReportSheet.PeriodName(ctx, r.Period), "profit", ctx.Fmt.Coins(r.FarmProfit));
            return ctx.Strings.Get("progress.profitNo");
        }
    }

    public static partial class WhyView
    {
        /// <summary>A row of "Becoming a Hamlet": 1 acres, 2 Care, 3 the hut, 4 the profitable season.</summary>
        private static WhyVm EraWhy(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.era.title", null);
            Text(vm, ctx, id == 1 ? "why.era.acres" : id == 2 ? "why.era.care" : id == 3 ? "why.era.hut" : "why.era.profit");
            Text(vm, ctx, "why.era.lesson");
            return vm;
        }
    }
}
