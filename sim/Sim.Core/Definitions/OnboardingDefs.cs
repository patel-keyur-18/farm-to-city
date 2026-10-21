using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>What completes a first-session step (spec 009 §3.2, §4). The view layer evaluates it.</summary>
    public enum GoalKind
    {
        ChapterClosed,
        BedGrowing,
        SheetVisited,
        Event,
        CropSown,
    }

    /// <summary>What opens a chapter of the guided first week (spec 009 §3.3, §4). The view layer evaluates it.</summary>
    public enum TriggerKind
    {
        /// <summary>Opened by its first-session step, not by a condition.</summary>
        Step,
        Event,
        StoreAbovePercent,
        TraderFull,
        SeasonStart,
        CareBelowForHours,
        Rejection,
        CashBelowHutCost,

        /// <summary>The While you were away card was shown (a return with news, not every away advance).</summary>
        AwayCard,
    }

    /// <summary>A step's goal or a chapter's trigger: a kind and the parameters that kind uses (unused ones are empty).</summary>
    public sealed class ConditionDef
    {
        public int Kind { get; }

        /// <summary>Event or command type names (the <c>event</c> and <c>rejection</c> kinds), in content order.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>A sheet (<c>sheetVisited</c>), a season id (<c>seasonStart</c>) or a rejection reason (<c>rejection</c>).</summary>
        public string Value { get; }

        /// <summary>Percent (<c>storeAbovePercent</c>), hours (<c>careBelowForHours</c>, <c>cropSown</c>'s minimum growth).</summary>
        public int Number { get; }

        /// <summary>A chapter that must have been seen first (<c>cashBelowHutCost</c>), or −1.</summary>
        public int AfterChapter { get; }

        public ConditionDef(int kind, IReadOnlyList<string> names, string value, int number, int afterChapter)
        {
            Kind = kind;
            Names = names;
            Value = value;
            Number = number;
            AfterChapter = afterChapter;
        }

        public GoalKind Goal { get { return (GoalKind)Kind; } }
        public TriggerKind Trigger { get { return (TriggerKind)Kind; } }
    }

    /// <summary>One step of the first session, in order (spec 009 §3.2). Its text is <c>nudge.&lt;id&gt;</c>.</summary>
    public sealed class StepDef
    {
        public string Id { get; }
        public ConditionDef Goal { get; }

        /// <summary>What glows: <c>bed:&lt;n&gt;</c>, <c>readyBed</c>, <c>hut</c>, <c>store</c>, <c>tile:&lt;n&gt;</c>, or null.</summary>
        public string Highlight { get; }

        /// <summary>Index into <see cref="OnboardingDefs.Chapters"/>.</summary>
        public int Chapter { get; }

        /// <summary>
        /// The farm tour while this step waits on the garden (ADR 0045 §1): sheets the nudge pill rotates through, each with the
        /// line <c>tour.&lt;sheet&gt;</c> (the part before any ':'). Empty for no tour.
        /// </summary>
        public IReadOnlyList<string> Tour { get; }

        /// <summary>The page reacts to the goal ("There: your first basket"): it opens when the step is done, not when it starts.</summary>
        public bool PageAfter { get; }

        public StepDef(string id, ConditionDef goal, string highlight, int chapter, IReadOnlyList<string> tour, bool pageAfter = false)
        {
            PageAfter = pageAfter;
            Id = id;
            Goal = goal;
            Highlight = highlight;
            Chapter = chapter;
            Tour = tour;
        }
    }

    /// <summary>One page of the letter, in priority order (spec 009 §3.3). Its text is <c>letter.&lt;id&gt;</c>.</summary>
    public sealed class ChapterDef
    {
        public string Id { get; }

        /// <summary>Any one of these opens it (the first time).</summary>
        public IReadOnlyList<ConditionDef> Triggers { get; }

        /// <summary>The sheet <b>Show me</b> opens, or null for no Show me.</summary>
        public string Action { get; }

        public ChapterDef(string id, IReadOnlyList<ConditionDef> triggers, string action)
        {
            Id = id;
            Triggers = triggers;
            Action = action;
        }
    }

    /// <summary>
    /// What an era needs (spec 009 §3.6, ADR 0030, 0032), from <c>eras.json</c>. A zero or null condition isn't checked.
    /// </summary>
    public sealed class EraDef
    {
        public string Id { get; }
        public int AcresHarvestedInSeason { get; }
        public Fixed MeanCareAtLeast { get; }
        public int HandHutsAtLeast { get; }
        public int ProfitableSeasonsAtLeast { get; }

        public EraDef(string id, int acresHarvestedInSeason, Fixed meanCareAtLeast, int handHutsAtLeast, int profitableSeasonsAtLeast)
        {
            Id = id;
            AcresHarvestedInSeason = acresHarvestedInSeason;
            MeanCareAtLeast = meanCareAtLeast;
            HandHutsAtLeast = handHutsAtLeast;
            ProfitableSeasonsAtLeast = profitableSeasonsAtLeast;
        }
    }

    /// <summary>
    /// <c>onboarding.json</c> and <c>eras.json</c> (spec 009 §4). They come with the people content: the Hamlet needs a hand's
    /// hut, so content without people (the frozen golden content) has neither.
    /// </summary>
    public sealed class OnboardingDefs
    {
        public IReadOnlyList<StepDef> Steps { get; }
        public IReadOnlyList<ChapterDef> Chapters { get; }

        /// <summary>In order; the first is the start (homestead).</summary>
        public IReadOnlyList<EraDef> Eras { get; }

        public OnboardingDefs(IReadOnlyList<StepDef> steps, IReadOnlyList<ChapterDef> chapters, IReadOnlyList<EraDef> eras)
        {
            Steps = steps;
            Chapters = chapters;
            Eras = eras;
        }

        public int IndexOfChapter(string id)
        {
            for (int i = 0; i < Chapters.Count; i++) if (Chapters[i].Id == id) return i;
            return -1;
        }

        public int IndexOfStep(string id)
        {
            for (int i = 0; i < Steps.Count; i++) if (Steps[i].Id == id) return i;
            return -1;
        }
    }
}
