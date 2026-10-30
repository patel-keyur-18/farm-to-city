using Sim.View.Content;

namespace Sim.View.Views
{
    /// <summary>What the map draws for the weather this frame (spec 008 §3.4, ADR 0083 §13–18).</summary>
    public struct WeatherFrame
    {
        /// <summary>The weather fading out and the one fading in; equal once the fade is done.</summary>
        public WeatherStyle From;
        public WeatherStyle To;

        /// <summary>How far the fade is, 0 to 1, eased (smoothstep).</summary>
        public float Weight;

        /// <summary>False with reduced motion: nothing drifts, falls, sways or glows.</summary>
        public bool Moving;

        /// <summary>The lightning glow's strength now, 0 when none (always 0 with reduced motion).</summary>
        public float Glow;

        /// <summary>The wind's swing now, −1 to 1 times the weather's gust (0 when still).</summary>
        public float Gust;

        /// <summary>A glow began this frame (thunder follows it 1–3 s later), with reduced motion too: then only the thunder.</summary>
        public bool Lightning;

        /// <summary>Distant thunder starts this frame.</summary>
        public bool Thunder;

        /// <summary>The added light's share at this time of day (1 by day, <see cref="WeatherLookDef.NightLight"/> at night).</summary>
        public float Light;
    }

    /// <summary>
    /// The weather on the map, frame by frame: follows the sim's current spell (or the developer panel's pick, look only), eases at a change,
    /// and keeps the lightning a soft glow that never flashes (ADR 0083 §15: rise ≥ 0.3 s, at most once in minGap–maxGap seconds). Pure
    /// presentation: real seconds, its own seeded draws, no sim state.
    /// </summary>
    public sealed class WeatherLook
    {
        private readonly WeatherLookDef _def;
        private uint _rng;
        private float _time, _changedAt = -1000f, _glowAt = -1000f, _nextGlow = float.MaxValue, _thunderAt = float.MaxValue;
        private WeatherStyle _from, _to;

        /// <summary>The developer panel's pick (a weather id), or null for the sim's spell. Look only: no command, no state (ADR 0083 §18).</summary>
        public string Override { get; set; }

        public WeatherLook(WeatherLookDef def, uint seed = 2222)
        {
            _def = def;
            _rng = seed == 0 ? 1u : seed;
        }

        private float Next()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng >> 8) / 16777216f;
        }

        /// <summary>
        /// Moves the look on by <paramref name="seconds"/> of real time. <paramref name="spell"/> is the current weather's id,
        /// <paramref name="dayLight"/> the day tint's brightness (1 by day, about 0.55 at night).
        /// </summary>
        public WeatherFrame Step(float seconds, string spell, bool reduced, float dayLight = 1f)
        {
            _time += seconds;
            WeatherStyle target = _def.Style(Override ?? spell) ?? _def.Style(spell) ?? _to;
            if (_to == null) { _from = _to = target; _changedAt = -1000f; }
            else if (target != null && target != _to)
            {
                _from = Weight() >= 0.5f ? _to : _from; // a change mid-fade starts from what mostly shows
                _to = target;
                _changedAt = _time;
            }

            var f = new WeatherFrame { From = _from, To = _to, Weight = Weight(), Moving = !reduced };
            float night = System.Math.Max(0f, System.Math.Min(1f, (1f - dayLight) / 0.45f));
            f.Light = 1f - night * (1f - _def.NightLight);

            // Lightning: scheduled whenever the weather has it; its glow only drawn with motion on, its thunder either way.
            GlowDef g = _to == null ? null : _to.Glow;
            if (g == null) _nextGlow = float.MaxValue;
            else
            {
                if (_nextGlow == float.MaxValue) _nextGlow = System.Math.Max(_time, _glowAt + g.MinGap) + g.MinGap + Next() * (g.MaxGap - g.MinGap);
                if (_time >= _nextGlow)
                {
                    _glowAt = _time;
                    _nextGlow = _time + g.MinGap + Next() * (g.MaxGap - g.MinGap);
                    _thunderAt = _time + 1f + Next() * 2f;
                    f.Lightning = true;
                }
                if (!reduced) f.Glow = Envelope(g, _time - _glowAt) * f.Weight;
            }
            if (_time >= _thunderAt) { f.Thunder = true; _thunderAt = float.MaxValue; }

            StreaksDef s = _to == null ? null : _to.Streaks;
            if (!reduced && s != null && s.Gust > 0) f.Gust = (float)(System.Math.Sin(_time * 0.7) * System.Math.Sin(_time * 1.9)) * s.Gust * f.Weight;
            return f;
        }

        /// <summary>The fade's progress, smoothstepped.</summary>
        private float Weight()
        {
            float k = (_time - _changedAt) / _def.EaseSeconds;
            k = k < 0 ? 0 : k > 1 ? 1 : k;
            return k * k * (3 - 2 * k);
        }

        /// <summary>The glow at <paramref name="t"/> seconds after it began: a straight rise to the peak, then a straight fade.</summary>
        public static float Envelope(GlowDef g, float t)
        {
            if (t < 0) return 0;
            if (t < g.RiseSeconds) return g.Peak * t / g.RiseSeconds;
            t -= g.RiseSeconds;
            return t < g.FadeSeconds ? g.Peak * (1 - t / g.FadeSeconds) : 0;
        }

        /// <summary>Rain streaks on screen: the weather's count, never over the cap.</summary>
        public int Streaks(WeatherStyle w) { return w == null || w.Streaks == null ? 0 : System.Math.Min(w.Streaks.Count, _def.StreakCap); }
    }
}
