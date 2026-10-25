using System.Collections.Generic;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>
    /// Every sprite of the map: Blender renders from <c>tools/blender/render.py</c> (ADR 0039, ADR 0041 §8), looked up by state, so the
    /// map code never names a file. Only two effects are still drawn in code: the contact shadow and the selection ring.
    /// </summary>
    public static class SpriteBank
    {
        /// <summary>Pixels per world unit at 1x: one tile is 132 px wide, so one world unit is one tile wide.</summary>
        public const float Ppu = 132f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Sprite Get(string key, System.Func<Sprite> make)
        {
            // "s == null" is Unity's check for a destroyed object: with fast enter-play-mode (no domain reload) the cache outlives the
            // textures made in the last play session, so re-create them.
            if (!Cache.TryGetValue(key, out Sprite s) || s == null) Cache[key] = s = make();
            return s;
        }

        // ---- Blender renders ----

        [System.Serializable] private class RenderedInfo { public string name; public int w, h; public float pivotX, pivotY; }
        [System.Serializable] private class RenderedManifest { public float pixelsPerUnit; public RenderedInfo[] sprites; }

        private static Dictionary<string, RenderedInfo> _rendered;
        private static float _renderedPpu;

        /// <summary>A sprite rendered by the Blender rig at its true map scale, or null if there is no such render.</summary>
        public static Sprite Rendered(string name)
        {
            if (_rendered == null)
            {
                _rendered = new Dictionary<string, RenderedInfo>();
                var json = Resources.Load<TextAsset>("Art/Blender/sprites");
                if (json != null)
                {
                    var m = JsonUtility.FromJson<RenderedManifest>(json.text);
                    _renderedPpu = m.pixelsPerUnit;
                    foreach (RenderedInfo i in m.sprites) _rendered[i.name] = i;
                }
            }
            if (!_rendered.TryGetValue(name, out RenderedInfo info)) return null;
            return Get("blender:" + name, () =>
            {
                var tex = Resources.Load<Texture2D>("Art/Blender/" + name);
                return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(info.pivotX, info.pivotY), _renderedPpu);
            });
        }

        /// <summary>Every rendered sprite's name (the Editor check that each state has its render).</summary>
        public static IEnumerable<string> RenderedNames { get { Rendered(""); return _rendered.Keys; } }

        // ---- ground ----

        public static Sprite Grass { get { return Rendered("ground_grass"); } }
        public static Sprite SoilTile { get { return Rendered("ground_soil"); } }
        public static Sprite Sand { get { return Rendered("ground_earth"); } }
        public static Sprite Water { get { return Rendered("ground_water"); } }

        /// <summary>Tufts and flowers over grass: three variants so the lawn isn't one colour.</summary>
        public static Sprite GrassDetail(int variant) { return Rendered("grass_detail_" + ((variant % 3) + 3) % 3); }

        // ---- crops, beds, trees ----

        /// <summary>The render's name for a stage: g1, g2, g3, ready (tools/blender/render.py STAGES); spoiling looks ready.</summary>
        private static string StageName(CropStage stage)
        {
            switch (stage)
            {
                case CropStage.Growing1: return "g1";
                case CropStage.Growing2: return "g2";
                case CropStage.Growing3: return "g3";
                default: return "ready";
            }
        }

        /// <summary>A field's crop across the whole acre.</summary>
        public static Sprite Crop(string cropId, CropStage stage) { return Rendered("crop_" + cropId + "_" + StageName(stage)); }

        public static Sprite Bed(string gardenCropId, CropStage stage)
        {
            return gardenCropId == null || stage == CropStage.None ? Rendered("bed_empty") : Rendered("bed_" + gardenCropId + "_" + StageName(stage));
        }

        public static Sprite Tree(string treeId, TreeStage stage)
        {
            return Rendered("tree_" + treeId + "_" + (stage == TreeStage.Spoiling ? "ripe" : stage.ToString().ToLowerInvariant()));
        }

        /// <summary>An empty tree place on the player's land: a ring of stones.</summary>
        public static Sprite PlantingSpot { get { return Rendered("tree_pit"); } }

        public static Sprite Hut { get { return Rendered("hut"); } }
        public static Sprite Well { get { return Rendered("well"); } }
        public static Sprite Sign { get { return Rendered("sign_sale"); } }

        public static Sprite MarkerReady { get { return Rendered("marker_ready"); } }
        public static Sprite MarkerSpoiling { get { return Rendered("marker_spoil"); } }
        public static Sprite MarkerPest { get { return Rendered("marker_pest"); } }

        // ---- effects (code-drawn) ----

        /// <summary>A soft oval contact shadow for a rendered sprite.</summary>
        public static Sprite ContactShadow
        {
            get
            {
                return Get("contactShadow", () =>
                {
                    var p = new Painter(132, 66);
                    p.Shadow(66, 33, 60, 30, 0.30f);
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }

        /// <summary>The first session's highlight (spec 009 §7): a gold arrow with a white edge, its pivot at the tip.</summary>
        public static Sprite GuideArrow
        {
            get
            {
                return Get("guideArrow", () =>
                {
                    var p = new Painter(64, 84);
                    Vector2[] d = { new Vector2(32, 4), new Vector2(60, 40), new Vector2(43, 40), new Vector2(43, 80), new Vector2(21, 80), new Vector2(21, 40), new Vector2(4, 40) };
                    p.OutlinedPolygon(d, new Color(0.98f, 0.76f, 0.2f, 1f), new Color(1f, 1f, 1f, 1f), 3.2f);
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0f));
                });
            }
        }

        /// <summary>An acre lit for placing (spec 014 §3.13): a soft filled diamond, inset so neighbouring lit acres stay apart.</summary>
        public static Sprite TileFill
        {
            get
            {
                return Get("tileFill", () =>
                {
                    var p = new Painter(140, 74);
                    Vector2[] d = { new Vector2(70, 63), new Vector2(118, 37), new Vector2(70, 11), new Vector2(22, 37) };
                    p.OutlinedPolygon(d, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0.95f), 2.4f);
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }

        public static Sprite SelectionRing
        {
            get
            {
                return Get("ring", () =>
                {
                    var p = new Painter(140, 74);
                    Vector2[] d = { new Vector2(70, 70), new Vector2(136, 37), new Vector2(70, 4), new Vector2(4, 37) };
                    p.OutlinedPolygon(d, new Color(0, 0, 0, 0), new Color(1f, 1f, 1f, 0.95f), 2.4f);
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }
    }
}
