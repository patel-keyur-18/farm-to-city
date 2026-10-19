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
                "storeNearlyFullPercent", "awayRowsMax", "ratingGoodPercent", "ratingFairPercent", "skillBands", "skillStarPoints", "referenceAcreHours");

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
            return new UiDef(day, season, valley, motions, perEvent, total, spoil, full, away, good, fair, bands, star, acre);
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
