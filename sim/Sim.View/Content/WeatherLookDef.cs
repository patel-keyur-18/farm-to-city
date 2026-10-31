using System.Collections.Generic;

namespace Sim.View.Content
{
    /// <summary>Cloud shadows drifting over the ground (spec 008 §3.4): how many, how dark, how fast (points a second).</summary>
    public sealed class CloudsDef
    {
        public int Count { get; internal set; }
        public float Alpha { get; internal set; }
        public float Speed { get; internal set; }
    }

    /// <summary>Rain streaks: how many on screen, how strong, and their size, slant and fall in points on an iPhone 13 screen (390 high).</summary>
    public sealed class StreaksDef
    {
        public int Count { get; internal set; }
        public float Alpha { get; internal set; }
        public float Length { get; internal set; }
        public float Slant { get; internal set; }
        public float Speed { get; internal set; }
        public float Width { get; internal set; }

        /// <summary>How far the wind swings the slant in a storm's gusts (0: steady).</summary>
        public float Gust { get; internal set; }
    }

    /// <summary>Lightning as a soft glow (ADR 0083 §15): its peak, rise and fade, and the gap between glows.</summary>
    public sealed class GlowDef
    {
        public float Peak { get; internal set; }
        public float RiseSeconds { get; internal set; }
        public float FadeSeconds { get; internal set; }
        public float MinGap { get; internal set; }
        public float MaxGap { get; internal set; }
    }

    /// <summary>One weather's look (spec 008 §3.4): the colour it lays over the map, its saturation and brightness, and its layers (null: none).</summary>
    public sealed class WeatherStyle
    {
        public string Id { get; internal set; }
        public Rgb Multiply { get; internal set; } = new Rgb(1, 1, 1);
        public float MultiplyAlpha { get; internal set; }
        public Rgb Overlay { get; internal set; } = new Rgb(1, 1, 1);
        public float OverlayAlpha { get; internal set; }
        public float Saturation { get; internal set; } = 1f;
        public float Brightness { get; internal set; } = 1f;
        public CloudsDef Clouds { get; internal set; }
        public StreaksDef Streaks { get; internal set; }
        public float SplashesPerSecond { get; internal set; }
        public Rgb Mist { get; internal set; } = new Rgb(1, 1, 1);
        public float MistAlpha { get; internal set; }
        public float Haze { get; internal set; }

        /// <summary>The far fields' heat shimmer, in points.</summary>
        public float Shimmer { get; internal set; }
        public float Rime { get; internal set; }
        public GlowDef Glow { get; internal set; }

        /// <summary>The colour the weather multiplies the map by: white eased towards <see cref="Multiply"/> by its alpha.</summary>
        public Rgb Filter { get { return Rgb.Lerp(new Rgb(1, 1, 1), Multiply, MultiplyAlpha); } }
    }

    /// <summary>The weather on the map (<c>ui.json</c> <c>weatherLook</c>, ADR 0083 §13–18), as the owner approved it in the preview.</summary>
    public sealed class WeatherLookDef
    {
        public float EaseSeconds { get; internal set; }
        public int StreakCap { get; internal set; }

        /// <summary>How much of a weather's added light (overlay, haze, mist) is left at night.</summary>
        public float NightLight { get; internal set; }

        /// <summary>Reduced motion: still rain streaks drawn at this share of their strength.</summary>
        public float ReducedStreakAlpha { get; internal set; }
        public IReadOnlyList<WeatherStyle> Weathers { get; internal set; }

        public WeatherStyle Style(string id)
        {
            foreach (WeatherStyle w in Weathers) if (w.Id == id) return w;
            return null;
        }
    }
}
