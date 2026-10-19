using System.Collections.Generic;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>
    /// Every sprite of the map (ADR 0038): the Kenney ground tiles from Resources, and flat, outlined, softly shaded placeholder
    /// sprites painted in code for crops, trees, the hut, the well, beds and markers. One lookup for each (state → sprite), so the
    /// Blender renders of US-171 replace them without touching the map code. Sprites are painted once and cached.
    /// </summary>
    public static class SpriteBank
    {
        /// <summary>Pixels per world unit: one tile is 132 px wide (Kenney's tile), so one world unit is one tile wide.</summary>
        public const float Ppu = 132f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color Outline = Painter.Hex(0x4A3320);
        private static readonly Color LeafLight = Painter.Hex(0x7BD05A), LeafMid = Painter.Hex(0x4FB544), LeafDark = Painter.Hex(0x2F8A3A);
        private static readonly Color Soil = Painter.Hex(0x7A4E2D), SoilDark = Painter.Hex(0x5C3A22);

        private static Sprite Get(string key, System.Func<Sprite> make)
        {
            // "s == null" is Unity's check for a destroyed object: with fast enter-play-mode (no domain reload) the cache outlives the
            // textures painted in the last play session, so re-create them.
            if (!Cache.TryGetValue(key, out Sprite s) || s == null) Cache[key] = s = make();
            return s;
        }

        // ---- Blender renders (tools/blender/render.py, ADR 0039) ----

        [System.Serializable] private class RenderedInfo { public string name; public int w, h; public float pivotX, pivotY; }
        [System.Serializable] private class RenderedManifest { public float pixelsPerUnit; public RenderedInfo[] sprites; }

        private static Dictionary<string, RenderedInfo> _rendered;
        private static float _renderedPpu;

        /// <summary>A sprite rendered by the Blender rig at its true map scale, or null if it isn't rendered yet.</summary>
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

        /// <summary>A soft oval contact shadow for a rendered sprite: rx, ry in tile widths.</summary>
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

        // ---- ground (Kenney, CC0) ----

        private static Sprite Kenney(string number, float centreFromBottom, float height)
        {
            return Get("kenney" + number, () =>
            {
                var tex = Resources.Load<Texture2D>("Art/Kenney/IsometricLandscape/landscapeTiles_" + number);
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, centreFromBottom / height), Ppu);
            });
        }

        // The Blender ground (ADR 0041 §8) when rendered, else Kenney's.
        public static Sprite Grass { get { return Rendered("ground_grass") ?? Kenney("067", 66f, 99f); } }
        public static Sprite SoilTile { get { return Rendered("ground_soil") ?? Kenney("083", 66f, 99f); } }
        public static Sprite Sand { get { return Rendered("ground_earth") ?? Kenney("059", 66f, 99f); } }
        public static Sprite Water { get { return Rendered("ground_water") ?? Kenney("066", 50f, 83f); } }

        // ---- tile-space helper: a, b in -0.5..0.5 inside a 132 x 66 diamond centred at (cx, cy) ----

        private static Vector2 P(float cx, float cy, float a, float b) { return new Vector2(cx + (a - b) * 66f, cy - (a + b) * 33f); }

        /// <summary>Parallel furrows across a field: a soft overlay on the soil tile (none on the Blender soil, whose ridges are modelled).</summary>
        public static Sprite Furrows
        {
            get
            {
                if (Rendered("ground_soil") != null) return null;
                return Get("furrows", () =>
                {
                    var p = new Painter(132, 66);
                    for (int i = -3; i <= 3; i++)
                    {
                        float a = i * 0.13f;
                        p.Line(P(66, 33, a, -0.44f), P(66, 33, a, 0.44f), 1.8f, new Color(0.25f, 0.14f, 0.07f, 0.22f));
                        p.Line(P(66, 33, a + 0.03f, -0.44f), P(66, 33, a + 0.03f, 0.44f), 1.0f, new Color(1f, 0.9f, 0.7f, 0.10f));
                    }
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }

        /// <summary>Grass tufts and a few flowers: three variants so the lawn isn't a flat colour.</summary>
        public static Sprite GrassDetail(int variant)
        {
            variant = ((variant % 3) + 3) % 3;
            Sprite rendered = Rendered("grass_detail_" + variant);
            if (rendered != null) return rendered;
            return Get("grassDetail" + variant, () =>
            {
                var p = new Painter(132, 66);
                var rng = new System.Random(77 + variant);
                for (int i = 0; i < 9; i++)
                {
                    float a = (float)rng.NextDouble() - 0.5f, b = (float)rng.NextDouble() - 0.5f;
                    a *= 0.85f; b *= 0.85f;
                    Vector2 q = P(66, 33, a, b);
                    Color g = new Color(0.24f, 0.55f + (float)rng.NextDouble() * 0.1f, 0.18f, 0.55f);
                    for (int k = -1; k <= 1; k++) p.Line(q, q + new Vector2(k * 2.6f, 5.2f - Mathf.Abs(k)), 1.5f, g);
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector2 q = P(66, 33, ((float)rng.NextDouble() - 0.5f) * 0.7f, ((float)rng.NextDouble() - 0.5f) * 0.7f);
                    p.Ellipse(q.x, q.y + 3f, 2.1f, 2.1f, i == 0 ? Painter.Hex(0xFFF3A0, 0.9f) : Painter.Hex(0xFFFFFF, 0.9f));
                }
                return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
            });
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
                    // The outline polygon fills; punch the inside out again by repainting nothing: draw only the stroke.
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }

        // ---- crops ----

        private struct CropLook
        {
            public Color Leaf, Accent;
            public bool Stalk;
            public float Height;
        }

        private static CropLook LookOf(string cropId)
        {
            switch (cropId)
            {
                case "radish": case "garden_radish": return new CropLook { Leaf = Painter.Hex(0x58BF4A), Accent = Painter.Hex(0xE9506A), Stalk = false, Height = 0.8f };
                case "spinach": case "garden_spinach": return new CropLook { Leaf = Painter.Hex(0x2E9E47), Accent = Painter.Hex(0x1F7D38), Stalk = false, Height = 0.7f };
                case "garden_coriander": return new CropLook { Leaf = Painter.Hex(0x86D45E), Accent = Painter.Hex(0xFFFFFF), Stalk = false, Height = 0.8f };
                case "wheat": return new CropLook { Leaf = Painter.Hex(0x8BC34A), Accent = Painter.Hex(0xE8B83A), Stalk = true, Height = 1.25f };
                case "rice": return new CropLook { Leaf = Painter.Hex(0x66CC52), Accent = Painter.Hex(0xE2C85A), Stalk = true, Height = 1.1f };
                case "lentils": return new CropLook { Leaf = Painter.Hex(0x9CD05A), Accent = Painter.Hex(0xC8B04A), Stalk = false, Height = 0.8f };
                default: return new CropLook { Leaf = Painter.Hex(0x3E9E40), Accent = Painter.Hex(0xFFFFFF), Stalk = false, Height = 0.95f }; // potato
            }
        }

        private static void Tuft(Painter p, Vector2 at, string cropId, float size, bool ready)
        {
            CropLook look = LookOf(cropId);
            float s = size * look.Height;
            p.Shadow(at.x, at.y, 8f * size + 2f, 3.2f * size + 1f, 0.22f);
            Color dark = Painter.Shade(look.Leaf, 0.72f);
            if (look.Stalk)
            {
                for (int k = -2; k <= 2; k++)
                {
                    Vector2 top = at + new Vector2(k * 3.2f * size, 15f * s - Mathf.Abs(k) * 1.2f * size);
                    p.Line(at + new Vector2(k * 1.2f, 0), top, 2.8f, Outline);
                    p.Line(at + new Vector2(k * 1.2f, 0), top, 1.5f, ready ? Painter.Shade(look.Accent, 0.9f) : look.Leaf);
                    if (ready) p.Blob(top.x, top.y + 1f, 2.0f, 4.2f, look.Accent, Outline, 0.9f, false);
                }
                return;
            }
            // A rosette: leaves fanning up and out.
            float[] dx = { -6f, -3f, 0f, 3f, 6f }, dh = { 7f, 10.5f, 12.5f, 10.5f, 7f };
            for (int i = 0; i < dx.Length; i++)
            {
                Color c = i % 2 == 0 ? look.Leaf : dark;
                p.Blob(at.x + dx[i] * s, at.y + dh[i] * 0.5f * s + 2f, 3.4f * s + 0.6f, dh[i] * 0.5f * s + 1.4f, c, Outline, 1.0f, false);
            }
            if (ready)
            {
                if (cropId == "radish" || cropId == "garden_radish") p.Blob(at.x, at.y + 1.8f, 4.4f * s, 3.4f * s, look.Accent, Outline, 1.0f);
                else if (cropId == "potato") { p.Ellipse(at.x - 3f, at.y + 12f * s, 1.9f, 1.9f, Color.white); p.Ellipse(at.x + 3.5f, at.y + 10f * s, 1.9f, 1.9f, Color.white); }
                else if (cropId == "lentils") p.Blob(at.x + 4f, at.y + 6f * s, 2.0f, 3.2f, look.Accent, Outline, 0.9f, false);
                else if (cropId == "garden_coriander") p.Ellipse(at.x, at.y + 13f * s, 2.4f, 2.4f, look.Accent);
            }
        }

        /// <summary>The render's name for a stage: g1, g2, g3, ready (tools/blender/render.py STAGES).</summary>
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

        private static float StageSize(CropStage stage)
        {
            switch (stage)
            {
                case CropStage.Growing1: return 0.5f;
                case CropStage.Growing2: return 0.68f;
                case CropStage.Growing3: return 0.95f;
                default: return 1.05f;
            }
        }

        /// <summary>A field's crop: nine tufts across the acre, bigger with each stage; ready adds the crop's own colour.</summary>
        public static Sprite Crop(string cropId, CropStage stage)
        {
            if (stage == CropStage.Spoiling) stage = CropStage.Ready;
            Sprite rendered = Rendered("crop_" + cropId + "_" + StageName(stage));
            if (rendered != null) return rendered;
            return Get("crop:" + cropId + ":" + stage, () =>
            {
                var p = new Painter(132, 118);
                const float cx = 66f, cy = 38f;
                var order = new List<Vector2>();
                for (int ia = -1; ia <= 1; ia++) for (int ib = -1; ib <= 1; ib++) order.Add(new Vector2(ia, ib));
                order.Sort((u, v) => (u.x + u.y).CompareTo(v.x + v.y));
                float size = StageSize(stage);
                foreach (Vector2 o in order)
                {
                    Vector2 at = P(cx, cy, o.x * 0.28f, o.y * 0.28f);
                    if (stage == CropStage.Growing1) p.Ellipse(at.x, at.y, 5.2f, 2.6f, SoilDark);
                    Tuft(p, at, cropId, size, stage == CropStage.Ready);
                }
                return p.ToSprite(Ppu, new Vector2(0.5f, cy / 118f));
            });
        }

        // ---- the kitchen garden's beds ----

        public static Sprite Bed(string gardenCropId, CropStage stage)
        {
            return Get("bed:" + gardenCropId + ":" + stage, () =>
            {
                var p = new Painter(76, 62);
                const float cx = 38f, cy = 22f;
                p.Shadow(cx, cy - 3f, 30f, 9f, 0.25f);
                Vector2[] frame = { new Vector2(cx, cy + 15), new Vector2(cx + 31, cy), new Vector2(cx, cy - 15), new Vector2(cx - 31, cy) };
                p.OutlinedPolygon(frame, Painter.Hex(0xB07A44), Outline, 1.4f);
                Vector2[] inner = { new Vector2(cx, cy + 11), new Vector2(cx + 24, cy), new Vector2(cx, cy - 11), new Vector2(cx - 24, cy) };
                p.Polygon(inner, Soil);
                p.Polygon(new[] { new Vector2(cx, cy + 11), new Vector2(cx + 24, cy), new Vector2(cx + 12, cy - 5), new Vector2(cx - 12, cy + 5) }, Painter.Shade(Soil, 1.12f));
                if (gardenCropId != null && stage != CropStage.None)
                {
                    float size = stage == CropStage.Growing1 ? 0.45f : stage == CropStage.Growing2 ? 0.7f : 0.95f;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 at = new Vector2(cx - 14f + i * 7f, cy + (i % 2 == 0 ? 2f : -2f));
                        Tuft(p, at, gardenCropId, size * 0.72f, stage == CropStage.Ready);
                    }
                }
                return p.ToSprite(Ppu, new Vector2(0.5f, cy / 62f));
            });
        }

        // ---- trees ----

        private static float TreeScale(TreeStage stage)
        {
            switch (stage)
            {
                case TreeStage.Sapling: return 0.34f;
                case TreeStage.Young: return 0.66f;
                default: return 1f;
            }
        }

        public static Sprite Tree(string treeId, TreeStage stage)
        {
            Sprite rendered = Rendered("tree_" + treeId + "_" + (stage == TreeStage.Spoiling ? "ripe" : stage.ToString().ToLowerInvariant()));
            if (rendered != null) return rendered;
            return Get("tree:" + treeId + ":" + stage, () =>
            {
                var p = new Painter(128, 176);
                const float cx = 64f, baseY = 14f;
                float s = TreeScale(stage);
                bool fruit = stage == TreeStage.Ripe || stage == TreeStage.Spoiling;
                bool spoiling = stage == TreeStage.Spoiling;
                p.Shadow(cx, baseY, 22f * (0.5f + s * 0.5f), 7f * (0.5f + s * 0.5f), 0.3f);
                Color trunk = Painter.Hex(0x9A6A3C);
                switch (treeId)
                {
                    case "banana":
                    {
                        Color stem = Painter.Hex(0xA6C95A);
                        p.OutlinedPolygon(new[] { new Vector2(cx - 5 * s - 2, baseY), new Vector2(cx + 5 * s + 2, baseY), new Vector2(cx + 3 * s, baseY + 52 * s), new Vector2(cx - 3 * s, baseY + 52 * s) }, stem, Outline, 1.3f);
                        float top = baseY + 50 * s;
                        for (int i = 0; i < 5; i++)
                        {
                            float k = i - 2f;
                            Vector2 end = new Vector2(cx + k * 18f * s, top + 14f * s - Mathf.Abs(k) * 7f * s);
                            Vector2 mid = new Vector2(cx + k * 10f * s, top + 22f * s - Mathf.Abs(k) * 3f * s);
                            p.Line(new Vector2(cx, top), mid, 7f * s + 2f, Outline);
                            p.Line(mid, end, 6f * s + 2f, Outline);
                            p.Line(new Vector2(cx, top), mid, 5f * s + 1f, i % 2 == 0 ? LeafMid : LeafLight);
                            p.Line(mid, end, 4f * s + 1f, i % 2 == 0 ? LeafMid : LeafLight);
                        }
                        if (fruit)
                            for (int i = 0; i < 4; i++)
                                p.Blob(cx + 10f * s + i * 3f, top - 6f * s - i * 5f, 3.4f, 5.2f, spoiling ? Painter.Hex(0x8A6A2A) : Painter.Hex(0xFFD93B), Outline, 1.0f);
                        break;
                    }
                    case "papaya":
                    {
                        p.OutlinedPolygon(new[] { new Vector2(cx - 4 * s - 1, baseY), new Vector2(cx + 4 * s + 1, baseY), new Vector2(cx + 2.5f * s, baseY + 66 * s), new Vector2(cx - 2.5f * s, baseY + 66 * s) }, Painter.Hex(0xB89A5E), Outline, 1.3f);
                        float top = baseY + 64 * s;
                        for (int i = 0; i < 7; i++)
                        {
                            float ang = (i / 6f) * Mathf.PI;
                            Vector2 end = new Vector2(cx + Mathf.Cos(ang) * 30f * s, top + Mathf.Sin(ang) * 18f * s + 3f);
                            p.Line(new Vector2(cx, top), end, 6f * s + 2f, Outline);
                            p.Line(new Vector2(cx, top), end, 4f * s + 1f, i % 2 == 0 ? LeafMid : LeafDark);
                            p.Blob(end.x, end.y, 5f * s + 1f, 3.4f * s + 1f, i % 2 == 0 ? LeafMid : LeafLight, Outline, 1.0f, false);
                        }
                        if (fruit) for (int i = 0; i < 3; i++) p.Blob(cx - 5f + i * 5f, top - 6f - (i % 2) * 2f, 4.6f, 6f, spoiling ? Painter.Hex(0x9A6A30) : Painter.Hex(0xFF9A2E), Outline, 1.0f);
                        break;
                    }
                    default: // guava and mango: a trunk and a round canopy
                    {
                        bool mango = treeId == "mango";
                        p.OutlinedPolygon(new[] { new Vector2(cx - 6 * s - 1, baseY), new Vector2(cx + 6 * s + 1, baseY), new Vector2(cx + 4 * s, baseY + 44 * s), new Vector2(cx - 4 * s, baseY + 44 * s) }, trunk, Outline, 1.4f);
                        float top = baseY + 40 * s;
                        float r = (mango ? 36f : 30f) * s + 4f;
                        Color dark = mango ? Painter.Hex(0x2D7A38) : Painter.Hex(0x3E9B45), mid = mango ? Painter.Hex(0x3F9444) : Painter.Hex(0x58B84C);
                        p.Blob(cx - r * 0.55f, top + r * 0.35f, r * 0.62f, r * 0.55f, dark, Outline, 1.5f, false);
                        p.Blob(cx + r * 0.55f, top + r * 0.30f, r * 0.62f, r * 0.55f, dark, Outline, 1.5f, false);
                        p.Blob(cx, top + r * 0.62f, r * 0.85f, r * 0.72f, mid, Outline, 1.6f);
                        if (fruit)
                        {
                            Color fc = spoiling ? Painter.Hex(0x8B6B2C) : mango ? Painter.Hex(0xFFA52E) : Painter.Hex(0xF4E27A);
                            float[] fx = { -0.45f, 0.05f, 0.5f, -0.15f, 0.3f };
                            float[] fy = { 0.35f, 0.25f, 0.5f, 0.75f, 0.9f };
                            for (int i = 0; i < fx.Length; i++) p.Blob(cx + fx[i] * r, top + fy[i] * r, 3.8f, 4.6f, fc, Outline, 1.0f);
                        }
                        break;
                    }
                }
                return p.ToSprite(Ppu, new Vector2(0.5f, baseY / 176f));
            });
        }

        /// <summary>
        /// An empty tree place on the player's land: a dug pit ringed with stones, so the player can see where a tree goes and tap
        /// it. Same canvas and pivot as <see cref="Tree"/>, so a sapling replaces it in place.
        /// </summary>
        public static Sprite PlantingSpot
        {
            get
            {
                return Get("plantingSpot", () =>
                {
                    var p = new Painter(128, 176);
                    const float cx = 64f, baseY = 14f;
                    // Small (owner, 2026-10-19): a hint on the ground, not a feature of the field.
                    p.Ellipse(cx, baseY, 11f, 5f, Outline);
                    p.Ellipse(cx, baseY, 9.8f, 4.1f, SoilDark);
                    p.Ellipse(cx, baseY + 0.6f, 7f, 2.8f, Soil);
                    // A ring of pale stones round the pit: a tree place, never mistaken for a sown crop.
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        p.Blob(cx + Mathf.Cos(a) * 11f, baseY + Mathf.Sin(a) * 5f, 2.6f, 2.0f, i % 2 == 0 ? Painter.Hex(0xD8D0BE) : Painter.Hex(0xBDB4A0), Outline, 0.8f, false);
                    }
                    return p.ToSprite(Ppu, new Vector2(0.5f, baseY / 176f));
                });
            }
        }

        // ---- buildings ----

        public static Sprite Hut
        {
            get
            {
                return Get("hut", () =>
                {
                    var p = new Painter(168, 160);
                    const float cx = 84f, by = 26f;
                    const float hw = 44f, hh = 22f, wall = 36f;
                    p.Shadow(cx + 6f, by - 2f, 56f, 17f, 0.3f);
                    Color wl = Painter.Hex(0xD3A867), wr = Painter.Hex(0xEBC98C);
                    // Left wall, right wall.
                    p.OutlinedPolygon(new[] { new Vector2(cx - hw, by), new Vector2(cx, by - hh), new Vector2(cx, by - hh + wall), new Vector2(cx - hw, by + wall) }, wl, Outline, 1.5f);
                    p.OutlinedPolygon(new[] { new Vector2(cx, by - hh), new Vector2(cx + hw, by), new Vector2(cx + hw, by + wall), new Vector2(cx, by - hh + wall) }, wr, Outline, 1.5f);
                    // A door and a window on the right wall.
                    p.OutlinedPolygon(new[] { new Vector2(cx + 8, by - hh + 4), new Vector2(cx + 22, by - hh + 11), new Vector2(cx + 22, by - hh + 30), new Vector2(cx + 8, by - hh + 23) }, Painter.Hex(0x7A4A28), Outline, 1.0f);
                    p.OutlinedPolygon(new[] { new Vector2(cx + 28, by - hh + 14), new Vector2(cx + 38, by - hh + 19), new Vector2(cx + 38, by - hh + 28), new Vector2(cx + 28, by - hh + 23) }, Painter.Hex(0x8FD0E8), Outline, 1.0f);
                    // The thatched roof: a pyramid with an overhang.
                    float ow = 12f, ry = by + wall - 2f, apex = ry + 40f;
                    Vector2 left = new Vector2(cx - hw - ow, ry), right = new Vector2(cx + hw + ow, ry), front = new Vector2(cx, ry - hh - ow / 2f);
                    Vector2 top = new Vector2(cx, apex);
                    p.OutlinedPolygon(new[] { left, front, top }, Painter.Hex(0xBE8A32), Outline, 1.6f);
                    p.OutlinedPolygon(new[] { front, right, top }, Painter.Hex(0xE0AD48), Outline, 1.6f);
                    for (int i = 1; i <= 5; i++)
                    {
                        float t = i / 6f;
                        p.Line(Vector2.Lerp(left, front, t) + new Vector2(0, 0), Vector2.Lerp(top, front, t * 0.15f + 0.1f), 1.1f, new Color(0.45f, 0.28f, 0.08f, 0.55f));
                        p.Line(Vector2.Lerp(front, right, t), Vector2.Lerp(front, top, 0.18f + t * 0.12f) + new Vector2(t * 10f, 0), 1.1f, new Color(0.62f, 0.4f, 0.1f, 0.5f));
                    }
                    p.Blob(top.x, top.y + 1f, 3.2f, 3.2f, Painter.Hex(0xC99A3C), Outline, 1.2f, false);
                    return p.ToSprite(Ppu, new Vector2(0.5f, by / 160f));
                });
            }
        }

        public static Sprite Well
        {
            get
            {
                return Get("well", () =>
                {
                    var p = new Painter(84, 104);
                    const float cx = 42f, cy = 18f;
                    p.Shadow(cx + 3f, cy - 4f, 26f, 8f, 0.3f);
                    Color stone = Painter.Hex(0xB9B4A6);
                    p.OutlinedPolygon(new[] { new Vector2(cx - 21, cy), new Vector2(cx - 21, cy + 17), new Vector2(cx + 21, cy + 17), new Vector2(cx + 21, cy) }, Painter.Shade(stone, 0.85f), Outline, 1.4f);
                    p.Blob(cx, cy, 21f, 10.5f, Painter.Shade(stone, 0.85f), Outline, 1.4f, false);
                    p.Blob(cx, cy + 17f, 21f, 10.5f, stone, Outline, 1.4f, false);
                    p.Ellipse(cx, cy + 17f, 14f, 6.6f, Painter.Hex(0x2E5F7A));
                    p.Ellipse(cx - 3f, cy + 18.5f, 6f, 2.4f, Painter.Hex(0x5BA7C9));
                    foreach (float x in new[] { -17f, 17f })
                        p.OutlinedPolygon(new[] { new Vector2(cx + x - 2.4f, cy + 14), new Vector2(cx + x + 2.4f, cy + 14), new Vector2(cx + x + 2.4f, cy + 54), new Vector2(cx + x - 2.4f, cy + 54) }, Painter.Hex(0x9A6A3C), Outline, 1.2f);
                    p.OutlinedPolygon(new[] { new Vector2(cx - 26, cy + 52), new Vector2(cx, cy + 66), new Vector2(cx + 26, cy + 52), new Vector2(cx, cy + 44) }, Painter.Hex(0xCC4B3A), Outline, 1.5f);
                    p.Line(new Vector2(cx, cy + 46), new Vector2(cx, cy + 26), 1.4f, Outline);
                    p.Blob(cx, cy + 22f, 4.6f, 4.0f, Painter.Hex(0x8C6A3C), Outline, 1.1f, false);
                    return p.ToSprite(Ppu, new Vector2(0.5f, cy / 104f));
                });
            }
        }

        public static Sprite Sign
        {
            get
            {
                return Get("sign", () =>
                {
                    var p = new Painter(56, 76);
                    p.Shadow(28f, 6f, 10f, 3.4f, 0.28f);
                    p.OutlinedPolygon(new[] { new Vector2(25, 6), new Vector2(31, 6), new Vector2(31, 46), new Vector2(25, 46) }, Painter.Hex(0x9A6A3C), Outline, 1.3f);
                    p.OutlinedPolygon(new[] { new Vector2(6, 36), new Vector2(50, 36), new Vector2(50, 62), new Vector2(6, 62) }, Painter.Hex(0xF1D9A0), Outline, 1.5f);
                    p.Ellipse(28f, 49f, 6f, 6f, Painter.Hex(0xE9A630));
                    p.Ellipse(28f, 49f, 3.2f, 3.2f, Painter.Hex(0xFFD86B));
                    return p.ToSprite(Ppu, new Vector2(0.5f, 6f / 76f));
                });
            }
        }

        // ---- markers and icons ----

        private static Sprite Bubble(string key, Color fill, System.Action<Painter, float, float> icon)
        {
            return Get(key, () =>
            {
                var p = new Painter(64, 76);
                p.Polygon(new[] { new Vector2(24, 20), new Vector2(40, 20), new Vector2(32, 6) }, Outline);
                p.Blob(32f, 42f, 22f, 22f, fill, Outline, 2.2f);
                icon(p, 32f, 42f);
                return p.ToSprite(Ppu, new Vector2(0.5f, 6f / 76f));
            });
        }

        public static Sprite MarkerReady { get { return Bubble("markReady", Painter.Hex(0xFFFFFF), (p, x, y) => { p.Line(new Vector2(x - 9, y), new Vector2(x - 2, y - 7), 5f, Outline); p.Line(new Vector2(x - 2, y - 7), new Vector2(x + 10, y + 8), 5f, Outline); p.Line(new Vector2(x - 9, y), new Vector2(x - 2, y - 7), 3.2f, Painter.Hex(0x4CC24A)); p.Line(new Vector2(x - 2, y - 7), new Vector2(x + 10, y + 8), 3.2f, Painter.Hex(0x4CC24A)); }); } }

        public static Sprite MarkerSpoiling { get { return Bubble("markSpoil", Painter.Hex(0xFFD27A), (p, x, y) => { p.OutlinedPolygon(new[] { new Vector2(x - 3, y - 2), new Vector2(x + 3, y - 2), new Vector2(x + 2, y + 12), new Vector2(x - 2, y + 12) }, Painter.Hex(0xC2410C), Outline, 0.8f); p.Blob(x, y - 9f, 3.2f, 3.2f, Painter.Hex(0xC2410C), Outline, 0.8f, false); }); } }

        public static Sprite MarkerPest { get { return Bubble("markPest", Painter.Hex(0xFF8A80), (p, x, y) => { p.Blob(x, y, 8f, 6.4f, Painter.Hex(0x7A1F1F), Outline, 1.0f, false); foreach (float s in new[] { -1f, 1f }) { p.Line(new Vector2(x + s * 6, y + 2), new Vector2(x + s * 12, y + 8), 1.6f, Outline); p.Line(new Vector2(x + s * 6, y - 3), new Vector2(x + s * 12, y - 8), 1.6f, Outline); } }); } }

        public static Sprite Coin
        {
            get
            {
                return Get("coin", () =>
                {
                    var p = new Painter(40, 40);
                    p.Blob(20f, 20f, 15f, 15f, Painter.Hex(0xFFC93C), Outline, 2.0f);
                    p.Ellipse(20f, 20f, 9f, 9f, Painter.Hex(0xE8A81E));
                    p.Ellipse(19f, 21f, 8f, 8f, Painter.Hex(0xFFD968));
                    return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
                });
            }
        }

        /// <summary>A round produce icon in a colour, for the produce that flies to the store.</summary>
        public static Sprite Produce(Color colour)
        {
            return Get("produce:" + ColorUtility.ToHtmlStringRGB(colour), () =>
            {
                var p = new Painter(40, 40);
                p.Blob(20f, 20f, 14f, 14f, colour, Outline, 2.0f);
                p.Ellipse(20f, 33f, 3.4f, 2.2f, LeafMid);
                return p.ToSprite(Ppu, new Vector2(0.5f, 0.5f));
            });
        }

        public static Color ProduceColour(string goodId)
        {
            switch (goodId)
            {
                case "radish": return Painter.Hex(0xE9506A);
                case "wheat": return Painter.Hex(0xE8B83A);
                case "rice": return Painter.Hex(0xEDE3B0);
                case "potato": return Painter.Hex(0xC59A5A);
                case "lentils": return Painter.Hex(0xD28A3C);
                case "spinach": return Painter.Hex(0x2E9E47);
                case "coriander": return Painter.Hex(0x86D45E);
                case "banana": return Painter.Hex(0xFFD93B);
                case "papaya": return Painter.Hex(0xFF9A2E);
                case "guava": return Painter.Hex(0xE5E48A);
                case "mango": return Painter.Hex(0xFFA52E);
                default: return Painter.Hex(0xC8B070);
            }
        }
    }
}
