using System.Collections;
using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>
    /// The device check's stand-ins (US-283, spec 032 §3.5, ADR 0182). While a category isn't re-rendered yet, the check on the phone swaps a realistic
    /// sprite in for every one of its sprites, to read the frame time and memory the whole redesign would cost: every house sprite draws the realistic
    /// family house (the crops since US-274, the trees since US-275 and the people since US-276 are rendered and stand for themselves, ADR 0185, 0186 and 0188). A load of dummy
    /// ASTC textures brings sprite memory up to the redesign's computed size. <b>Development builds and the Editor only; off unless the developer panel's
    /// switch is on</b> (it takes effect on the next launch). Release builds compile it to nothing.
    /// </summary>
    public static class ArtCheck
    {
        public const string Pref = "dev.artcheck";

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static int _on = -1;

        /// <summary>Whether the stand-ins are on (read once from the saved switch; the panel's button flips it for the next launch).</summary>
        public static bool On
        {
            get { if (_on < 0) _on = PlayerPrefs.GetInt(Pref, 0); return _on == 1; }
            set { _on = value ? 1 : 0; PlayerPrefs.SetInt(Pref, _on); PlayerPrefs.Save(); }
        }

        /// <summary>The name of the render that stands in for <paramref name="name"/>, or the name itself.</summary>
        public static string StandIn(string name)
        {
            if (!On || string.IsNullOrEmpty(name)) return name;
            if (name.StartsWith("family_house_", System.StringComparison.Ordinal) || name.StartsWith("pucca_house_", System.StringComparison.Ordinal)
                || name.StartsWith("village_house_", System.StringComparison.Ordinal))
            {
                int n = name.Length;
                char f = name[n - 1];
                return n > 2 && name[n - 2] == '_' && (f == 's' || f == 'e' || f == 'n' || f == 'w') ? "family_house_" + f : name; // every facing, the done house
            }
            return name;
        }

        // ---- the memory ballast ----

        /// <summary>The finished redesign's rendered sprites at 8x in ASTC 4x4 with mips, from <c>sprites.json</c> (605 sprites, 51 Mpx today): 271 MB (US-283).</summary>
        public const long BallastBytes = 271L * 1000 * 1000;

        private static readonly System.Collections.Generic.List<Texture2D> Ballasts = new System.Collections.Generic.List<Texture2D>();

        /// <summary>Loads dummy ASTC 4x4 textures (2048 x 1024, mips) until <see cref="BallastBytes"/> are held, a few a frame. iOS only: the Editor has nothing to measure.</summary>
        public static IEnumerator LoadBallast()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!On || Ballasts.Count > 0) yield break;
            long held = 0;
            byte[] filler = null;
            var rng = new System.Random(2711);
            while (held < BallastBytes)
            {
                var t = new Texture2D(2048, 1024, TextureFormat.ASTC_4x4, true);
                if (filler == null) { filler = new byte[t.GetRawTextureData<byte>().Length]; rng.NextBytes(filler); }
                t.LoadRawTextureData(filler);
                t.Apply(false, true); // uploaded, the CPU copy freed: the memory is the GPU's, as a sprite's is
                Ballasts.Add(t);
                held += filler.Length;
                if (Ballasts.Count % 4 == 0) yield return null;
            }
            Debug.Log("ArtCheck: ballast " + Ballasts.Count + " textures, " + (held / 1000000) + " MB");
#else
            yield break;
#endif
        }

        /// <summary>The ballast held now, in MB (for the developer panel's memory line).</summary>
        public static long BallastMegabytes { get { long b = 0; foreach (Texture2D t in Ballasts) b += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t); return b / 1000000; } }
#else
        public static bool On { get { return false; } set { } }
        public static string StandIn(string name) { return name; }
        public static IEnumerator LoadBallast() { yield break; }
        public static long BallastMegabytes { get { return 0; } }
#endif
    }
}
