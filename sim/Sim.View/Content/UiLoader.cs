using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Content;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.View.Content
{
    /// <summary>Loads and validates <c>content/ui.json</c> (spec 008 §4), with the same field-path errors as the other content files.</summary>
    public static class UiLoader
    {
        private static readonly string[] Reduced = { "None", "Instant", "Flash", "Fade", "Static", "BadgeOnly", "Hidden" };
        private static readonly string[] Easings = { "Linear", "OutBack", "OutCubic", "InOutCubic", "InCubic" };

        public static UiDef Load(string json)
        {
            const string root = "ui";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "dayTint", "seasonTint", "valleyTint", "motions", "maxFlyingPerEvent", "maxFlyingTotal", "spoilWarningHours",
                "storeNearlyFullPercent", "awayRowsMax", "ratingGoodPercent", "ratingFairPercent", "skillBands", "skillStarPoints", "referenceAcreHours", "awayCardMinHours", "happyAbove", "outputBands", "weatherLook");

            List<Rgb> day = Colours(Required(o, root, "dayTint"), root + ".dayTint", 24);
            List<Rgb> season = Colours(Required(o, root, "seasonTint"), root + ".seasonTint", 4);
            Rgb valley = Colour(Required(o, root, "valleyTint"), root + ".valleyTint");

            var motions = new List<MotionDef>();
            List<JToken> list = NonEmptyArray(o, root, "motions");
            for (int i = 0; i < list.Count; i++)
            {
                string p = Index(root + ".motions", i);
                JObject m = Object(list[i], p);
                OnlyFields(m, p, "trigger", "kind", "durationMs", "easing", "reducedKind", "reducedMs");
                string trigger = String(Required(m, p, "trigger"), Join(p, "trigger"));
                if (motions.Exists(x => x.Trigger == trigger)) throw new ContentException(Join(p, "trigger"), "duplicate trigger '" + trigger + "'");
                int ms = Int(Required(m, p, "durationMs"), Join(p, "durationMs"));
                Range(ms, 0, 3000, Join(p, "durationMs"));
                string easing = String(Required(m, p, "easing"), Join(p, "easing"));
                if (System.Array.IndexOf(Easings, easing) < 0) throw new ContentException(Join(p, "easing"), "expected one of " + string.Join(", ", Easings));
                string reduced = String(Required(m, p, "reducedKind"), Join(p, "reducedKind"));
                if (System.Array.IndexOf(Reduced, reduced) < 0) throw new ContentException(Join(p, "reducedKind"), "reduced motion can't bounce or fly: expected one of " + string.Join(", ", Reduced));
                int rms = Int(Required(m, p, "reducedMs"), Join(p, "reducedMs"));
                Range(rms, 0, 100, Join(p, "reducedMs"));
                motions.Add(new MotionDef(trigger, String(Required(m, p, "kind"), Join(p, "kind")), ms, easing, reduced, rms));
            }

            int perEvent = Int(Required(o, root, "maxFlyingPerEvent"), root + ".maxFlyingPerEvent");
            Range(perEvent, 1, 50, root + ".maxFlyingPerEvent");
            int total = Int(Required(o, root, "maxFlyingTotal"), root + ".maxFlyingTotal");
            Range(total, 1, 50, root + ".maxFlyingTotal");
            int spoil = Int(Required(o, root, "spoilWarningHours"), root + ".spoilWarningHours");
            Range(spoil, 1, 72, root + ".spoilWarningHours");
            int full = Int(Required(o, root, "storeNearlyFullPercent"), root + ".storeNearlyFullPercent");
            Range(full, 50, 100, root + ".storeNearlyFullPercent");
            int away = Int(Required(o, root, "awayRowsMax"), root + ".awayRowsMax");
            Range(away, 3, 20, root + ".awayRowsMax");
            int good = Int(Required(o, root, "ratingGoodPercent"), root + ".ratingGoodPercent");
            Range(good, 1, 200, root + ".ratingGoodPercent");
            int fair = Int(Required(o, root, "ratingFairPercent"), root + ".ratingFairPercent");
            Range(fair, 1, good - 1, root + ".ratingFairPercent");
            var bands = new List<int>();
            List<JToken> bt = Array(Required(o, root, "skillBands"), root + ".skillBands");
            if (bt.Count != 3) throw new ContentException(root + ".skillBands", "expected 3 skill thresholds");
            for (int i = 0; i < 3; i++)
            {
                int b = Int(bt[i], Index(root + ".skillBands", i));
                Range(b, i == 0 ? 1 : bands[i - 1] + 1, 100, Index(root + ".skillBands", i));
                bands.Add(b);
            }
            int star = Int(Required(o, root, "skillStarPoints"), root + ".skillStarPoints");
            Range(star, 10, 50, root + ".skillStarPoints");
            int acre = Int(Required(o, root, "referenceAcreHours"), root + ".referenceAcreHours");
            Range(acre, 1, 12, root + ".referenceAcreHours");
            int awayHours = Int(Required(o, root, "awayCardMinHours"), root + ".awayCardMinHours");
            Range(awayHours, 0, 72, root + ".awayCardMinHours");
            int happy = Int(Required(o, root, "happyAbove"), root + ".happyAbove");
            Range(happy, 1, 100, root + ".happyAbove");
            var output = new List<int>();
            List<JToken> ot = Array(Required(o, root, "outputBands"), root + ".outputBands");
            if (ot.Count != 4) throw new ContentException(root + ".outputBands", "expected 4 output thresholds");
            for (int i = 0; i < 4; i++)
            {
                int b = Int(ot[i], Index(root + ".outputBands", i));
                Range(b, i == 0 ? 1 : output[i - 1] + 1, 200, Index(root + ".outputBands", i));
                output.Add(b);
            }
            WeatherLookDef weather = o["weatherLook"] == null ? null : WeatherLook(Object(o["weatherLook"], root + ".weatherLook"), root + ".weatherLook");
            return new UiDef(day, season, valley, motions, perEvent, total, spoil, full, away, good, fair, bands, star, acre, awayHours, happy, output, weather);
        }

        private static float F(JToken t, string path, string lo, string hi)
        {
            Fixed v = Number(t, path);
            Range(v, Fixed.Parse(lo), Fixed.Parse(hi), path);
            return v.Raw / (float)(1L << Fixed.FractionBits);
        }

        private static float F(JObject o, string path, string name, string lo, string hi) { return F(Required(o, path, name), Join(path, name), lo, hi); }

        /// <summary>
        /// The weather's look (spec 008 §3.4, ADR 0083 §13–18). The no-flashing rule is checked here, so no tuning can break it: a glow
        /// rises over at least 0.3 s and comes at most once a second (WCAG 2.3.1: never more than 3 flashes a second).
        /// </summary>
        private static WeatherLookDef WeatherLook(JObject o, string path)
        {
            OnlyFields(o, path, "_note", "easeSeconds", "streakCap", "nightLight", "reducedStreakAlpha", "weathers");
            var def = new WeatherLookDef
            {
                EaseSeconds = F(o, path, "easeSeconds", "0.5", "10"), StreakCap = Int(Required(o, path, "streakCap"), Join(path, "streakCap")),
                NightLight = F(o, path, "nightLight", "0", "1"), ReducedStreakAlpha = F(o, path, "reducedStreakAlpha", "0", "1"),
            };
            Range(def.StreakCap, 0, 1000, Join(path, "streakCap"));
            var list = new List<WeatherStyle>();
            string wp = Join(path, "weathers");
            foreach (JProperty p in Object(Required(o, path, "weathers"), wp).Properties())
            {
                string q = Join(wp, p.Name);
                JObject w = Object(p.Value, q);
                OnlyFields(w, q, "multiply", "multiplyAlpha", "overlay", "overlayAlpha", "saturation", "brightness", "clouds", "streaks", "splashesPerSecond",
                    "mist", "mistAlpha", "haze", "shimmer", "rime", "glow");
                var s = new WeatherStyle
                {
                    Id = p.Name, Multiply = Colour(Required(w, q, "multiply"), Join(q, "multiply")), MultiplyAlpha = F(w, q, "multiplyAlpha", "0", "1"),
                    Saturation = F(w, q, "saturation", "0", "2"), Brightness = F(w, q, "brightness", "0.5", "1.5"),
                };
                if (w["overlay"] != null) { s.Overlay = Colour(w["overlay"], Join(q, "overlay")); s.OverlayAlpha = F(w, q, "overlayAlpha", "0", "1"); }
                if (w["mist"] != null) { s.Mist = Colour(w["mist"], Join(q, "mist")); s.MistAlpha = F(w, q, "mistAlpha", "0", "1"); }
                if (w["splashesPerSecond"] != null) s.SplashesPerSecond = F(w, q, "splashesPerSecond", "0", "300");
                if (w["haze"] != null) s.Haze = F(w, q, "haze", "0", "1");
                if (w["shimmer"] != null) s.Shimmer = F(w, q, "shimmer", "0", "4");
                if (w["rime"] != null) s.Rime = F(w, q, "rime", "0", "1");
                if (w["clouds"] != null)
                {
                    string cp = Join(q, "clouds");
                    JObject c = Object(w["clouds"], cp);
                    OnlyFields(c, cp, "count", "alpha", "speed");
                    s.Clouds = new CloudsDef { Count = Int(Required(c, cp, "count"), Join(cp, "count")), Alpha = F(c, cp, "alpha", "0", "1"), Speed = F(c, cp, "speed", "0", "200") };
                    Range(s.Clouds.Count, 0, 12, Join(cp, "count"));
                }
                if (w["streaks"] != null)
                {
                    string sp = Join(q, "streaks");
                    JObject r = Object(w["streaks"], sp);
                    OnlyFields(r, sp, "count", "alpha", "length", "slant", "speed", "width", "gust");
                    s.Streaks = new StreaksDef
                    {
                        Count = Int(Required(r, sp, "count"), Join(sp, "count")), Alpha = F(r, sp, "alpha", "0", "1"), Length = F(r, sp, "length", "1", "80"),
                        Slant = F(r, sp, "slant", "-1", "1"), Speed = F(r, sp, "speed", "0", "3000"), Width = F(r, sp, "width", "0.5", "4"), Gust = F(r, sp, "gust", "0", "1"),
                    };
                    Range(s.Streaks.Count, 0, def.StreakCap, Join(sp, "count"));
                }
                if (w["glow"] != null)
                {
                    string gp = Join(q, "glow");
                    JObject g = Object(w["glow"], gp);
                    OnlyFields(g, gp, "peak", "riseSeconds", "fadeSeconds", "minGap", "maxGap");
                    s.Glow = new GlowDef
                    {
                        Peak = F(g, gp, "peak", "0", "0.6"), RiseSeconds = F(g, gp, "riseSeconds", "0.3", "3"), FadeSeconds = F(g, gp, "fadeSeconds", "0.3", "5"),
                        MinGap = F(g, gp, "minGap", "1", "120"), MaxGap = F(g, gp, "maxGap", "1", "300"),
                    };
                    if (s.Glow.MaxGap < s.Glow.MinGap) throw new ContentException(Join(gp, "maxGap"), "must be at least minGap");
                    if (s.Glow.MinGap < s.Glow.RiseSeconds + s.Glow.FadeSeconds) throw new ContentException(Join(gp, "minGap"), "a glow must end before the next starts");
                }
                list.Add(s);
            }
            def.Weathers = list.AsReadOnly();
            return def;
        }

        private static List<JToken> NonEmptyArray(JObject o, string path, string name)
        {
            List<JToken> a = Array(Required(o, path, name), Join(path, name));
            if (a.Count == 0) throw new ContentException(Join(path, name), "must not be empty");
            return a;
        }

        private static List<Rgb> Colours(JToken t, string path, int count)
        {
            List<JToken> a = Array(t, path);
            if (a.Count != count) throw new ContentException(path, "expected " + count + " colours, found " + a.Count);
            var list = new List<Rgb>();
            for (int i = 0; i < a.Count; i++) list.Add(Colour(a[i], Index(path, i)));
            return list;
        }

        private static Rgb Colour(JToken t, string path)
        {
            List<JToken> c = Array(t, path);
            if (c.Count != 3) throw new ContentException(path, "expected [r, g, b]");
            float F(int i)
            {
                Fixed v = Number(c[i], Index(path, i));
                Range(v, Fixed.Zero, Fixed.Parse("1.2"), Index(path, i));
                return v.Raw / (float)(1L << Fixed.FractionBits);
            }
            return new Rgb(F(0), F(1), F(2));
        }
    }
}
