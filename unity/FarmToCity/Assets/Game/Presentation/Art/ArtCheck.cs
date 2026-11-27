using System.Collections;
using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>
    /// The device check's stand-ins (US-283, spec 032 §3.5, ADR 0182). Only the family house, the wheat acre and the hand and his wife are rendered
    /// realistic so far, so on the phone the check swaps them in for every sprite of their category, to read the frame time and memory the whole
    /// redesign would cost: every house sprite draws the realistic family house, every crop the wheat of its stage, every person the hand or his wife,
    /// and every other shadow-casting building or tree draws the house's shadow scaled to its width. A load of dummy ASTC textures brings sprite
    /// memory to the redesign's computed size. <b>Development builds and the Editor only; off unless the developer panel's switch is on</b> (it
    /// takes effect on the next launch). Release builds compile it to nothing.
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

        private static readonly System.Collections.Generic.HashSet<string> Female = new System.Collections.Generic.HashSet<string>
        { "hand_f", "labourer_f", "town_f", "girl", "elder_f", "advisor_bank", "advisor_planner" };

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
            if (name.StartsWith("crop_", System.StringComparison.Ordinal))
            {
                string stage = name.Substring(name.LastIndexOf('_') + 1);
                if (stage == "g1" || stage == "g2" || stage == "g3" || stage == "ready") return "crop_wheat_" + stage;
                return stage == "cut" ? "crop_wheat_ready" : name;
            }
            if (name.StartsWith("person_", System.StringComparison.Ordinal))
            {
                int at = name.IndexOf("_front", System.StringComparison.Ordinal), facing = 0;
                if (at < 0) { at = name.IndexOf("_back", System.StringComparison.Ordinal); facing = 1; }
                if (at < 0) return name;
                string kind = name.Substring(7, at - 7);
                return "person_hand_" + (Female.Contains(kind) ? "f" : "m") + (facing == 0 ? "_front" : "_back");
            }
            return name;
        }

        private static readonly string[] CasterStarts =
        {
            "tree_", "village_banyan", "village_peepal", "village_tree", "hut", "hand_hut", "well", "cattle_shed", "cold_store", "granary", "chhappar", "brick_kiln",
            "bakery_", "dairy_", "flour_mill_", "kirana", "restaurant", "sweet_shop", "tea_stall", "tuition", "veg_stall", "newcomer_", "village_chemist",
            "village_clinic", "village_council_hall", "village_dairy", "village_dealer", "village_generator", "village_grocery", "village_hall", "village_quarters",
            "village_school", "village_tea_stall", "village_temple", "village_tower", "village_watchman", "village_drains", "village_collection",
        };

        /// <summary>The scale of the house's shadow that stands in for a caster's own (its width over the house's), or 0 if the sprite casts none (spec 032 §3.3).</summary>
        public static float StandInShadowScale(Sprite sprite)
        {
            if (!On || sprite == null || string.IsNullOrEmpty(sprite.name) || sprite.name == "tree_pit") return 0f;
            foreach (string start in CasterStarts)
            {
                if (!sprite.name.StartsWith(start, System.StringComparison.Ordinal)) continue;
                float house = SpriteBank.WorldWidth("family_house_s");
                float w = SpriteBank.WorldWidth(sprite.name);
                return house <= 0f || w <= 0f ? 0f : Mathf.Clamp(w / house, 0.3f, 3f);
            }
            return 0f;
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
        public static float StandInShadowScale(Sprite sprite) { return 0f; }
        public static IEnumerator LoadBallast() { yield break; }
        public static long BallastMegabytes { get { return 0; } }
#endif
    }
}
