using System.Collections.Generic;

namespace Sim.View.Content
{
    /// <summary>A colour as three channels in 0..1.2 (tints multiply a sprite's colour, so a channel can lift slightly above 1).</summary>
    public readonly struct Rgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static Rgb Lerp(Rgb a, Rgb b, float t) { return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t); }

        public static Rgb operator *(Rgb a, Rgb b) { return new Rgb(a.R * b.R, a.G * b.G, a.B * b.B); }
    }

    /// <summary>One entry of the motion catalogue (spec 008 §3.12): which trigger plays which motion, how long, and its reduced form.</summary>
    public sealed class MotionDef
    {
        /// <summary>An event name (<c>CropSown</c>) or a UI action (<c>TapTile</c>, <c>SheetOpen</c>).</summary>
        public string Trigger { get; }

        /// <summary>What it does: Squash, Press, Pop, Bob, FlyToStore, FlyCoins, CountDown, Shake, Droop, Roll, Grow, SlideUp, SlideDown, Scale, CardIn.</summary>
        public string Kind { get; }
        public int DurationMs { get; }
        public string Easing { get; }

        /// <summary>The reduced-motion form: None, Instant, Flash, Fade, Static, BadgeOnly or Hidden; never a bounce or a fly.</summary>
        public string ReducedKind { get; }
        public int ReducedMs { get; }

        public MotionDef(string trigger, string kind, int durationMs, string easing, string reducedKind, int reducedMs)
        {
            Trigger = trigger;
            Kind = kind;
            DurationMs = durationMs;
            Easing = easing;
            ReducedKind = reducedKind;
            ReducedMs = reducedMs;
        }
    }

    /// <summary>The look and feel data of <c>content/ui.json</c> (spec 008 §4). All of it can be tuned without re-approval.</summary>
    public sealed class UiDef
    {
        /// <summary>24 tints, one per sim hour; interpolated between hours.</summary>
        public IReadOnlyList<Rgb> DayTint { get; }

        /// <summary>Spring, Summer, Monsoon, Winter.</summary>
        public IReadOnlyList<Rgb> SeasonTint { get; }
        public Rgb ValleyTint { get; }
        public IReadOnlyList<MotionDef> Motions { get; }
        public int MaxFlyingPerEvent { get; }
        public int MaxFlyingTotal { get; }
        public int SpoilWarningHours { get; }
        public int StoreNearlyFullPercent { get; }
        public int AwayRowsMax { get; }

        /// <summary>The While you were away card shows only after at least this long away, or at the cap (ADR 0046 §2).</summary>
        public int AwayCardMinHours { get; }

        /// <summary>A yield factor at or above this percentage reads "good", at or above the fair one "fair", else "poor".</summary>
        public int RatingGoodPercent { get; }
        public int RatingFairPercent { get; }

        /// <summary>Skill where Capable, Skilled and Expert begin (below the first: Beginner), ADR 0039 §7.</summary>
        public IReadOnlyList<int> SkillBands { get; }

        /// <summary>Skill points per star (5 stars at 100).</summary>
        public int SkillStarPoints { get; }

        /// <summary>Hours a day one reference acre needs (wheat): "tends about N acres" = hours ÷ this.</summary>
        public int ReferenceAcreHours { get; }

        public UiDef(IReadOnlyList<Rgb> dayTint, IReadOnlyList<Rgb> seasonTint, Rgb valleyTint, IReadOnlyList<MotionDef> motions,
            int maxFlyingPerEvent, int maxFlyingTotal, int spoilWarningHours, int storeNearlyFullPercent, int awayRowsMax,
            int ratingGoodPercent = 95, int ratingFairPercent = 80, IReadOnlyList<int> skillBands = null, int skillStarPoints = 20, int referenceAcreHours = 3, int awayCardMinHours = 8)
        {
            SkillBands = new List<int>(skillBands ?? new[] { 35, 55, 75 }).AsReadOnly();
            SkillStarPoints = skillStarPoints;
            ReferenceAcreHours = referenceAcreHours;
            DayTint = new List<Rgb>(dayTint).AsReadOnly();
            SeasonTint = new List<Rgb>(seasonTint).AsReadOnly();
            ValleyTint = valleyTint;
            Motions = new List<MotionDef>(motions).AsReadOnly();
            MaxFlyingPerEvent = maxFlyingPerEvent;
            MaxFlyingTotal = maxFlyingTotal;
            SpoilWarningHours = spoilWarningHours;
            StoreNearlyFullPercent = storeNearlyFullPercent;
            AwayRowsMax = awayRowsMax;
            AwayCardMinHours = awayCardMinHours;
            RatingGoodPercent = ratingGoodPercent;
            RatingFairPercent = ratingFairPercent;
        }

        public MotionDef Motion(string trigger)
        {
            foreach (MotionDef m in Motions)
                if (m.Trigger == trigger) return m;
            return null;
        }

        /// <summary>The day tint at a time of day, interpolated between the two hours around it.</summary>
        public Rgb TintAt(long secondOfDay)
        {
            long s = ((secondOfDay % 86400) + 86400) % 86400;
            int h = (int)(s / 3600);
            float frac = (s % 3600) / 3600f;
            return Rgb.Lerp(DayTint[h], DayTint[(h + 1) % 24], frac);
        }
    }
}
