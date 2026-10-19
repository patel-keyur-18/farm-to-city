using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>Small painted icons for the HUD (weather, the store sack, the basket), in the same outlined style as the map sprites.</summary>
    public static class IconBank
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Color Outline = Painter.Hex(0x4A3320);

        private static Sprite Get(string key, System.Func<Sprite> make)
        {
            // "s == null" is Unity's check for a destroyed object: with fast enter-play-mode (no domain reload) the cache outlives the
            // textures painted in the last play session, so re-create them.
            if (!Cache.TryGetValue(key, out Sprite s) || s == null) Cache[key] = s = make();
            return s;
        }

        private static void Cloud(Painter p, float x, float y, float s, Color c)
        {
            p.Blob(x - 13f * s, y - 2f * s, 11f * s, 8f * s, c, Outline, 1.8f, false);
            p.Blob(x + 13f * s, y - 2f * s, 11f * s, 8f * s, c, Outline, 1.8f, false);
            p.Blob(x, y + 5f * s, 14f * s, 11f * s, c, Outline, 1.8f);
        }

        public static Sprite Weather(string id)
        {
            return Get("weather:" + id, () =>
            {
                var p = new Painter(64, 64);
                Color white = Painter.Hex(0xFFFFFF), grey = Painter.Hex(0xB8C2CC), dark = Painter.Hex(0x7E8B99);
                switch (id)
                {
                    case "sun":
                    case "heatwave":
                    {
                        Color ray = id == "sun" ? Painter.Hex(0xFFB627) : Painter.Hex(0xFF6A3D), disc = id == "sun" ? Painter.Hex(0xFFD33F) : Painter.Hex(0xFF8A3D);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            p.Line(new Vector2(32 + Mathf.Cos(a) * 19, 32 + Mathf.Sin(a) * 19), new Vector2(32 + Mathf.Cos(a) * 27, 32 + Mathf.Sin(a) * 27), 6f, Outline);
                            p.Line(new Vector2(32 + Mathf.Cos(a) * 19, 32 + Mathf.Sin(a) * 19), new Vector2(32 + Mathf.Cos(a) * 27, 32 + Mathf.Sin(a) * 27), 3.4f, ray);
                        }
                        p.Blob(32, 32, 14, 14, disc, Outline, 2.2f);
                        break;
                    }
                    case "cloudy": Cloud(p, 32, 30, 1.1f, white); break;
                    case "rain":
                    case "heavy_rain":
                    case "storm":
                    {
                        Cloud(p, 32, 40, 1.0f, id == "rain" ? grey : dark);
                        int drops = id == "rain" ? 3 : 5;
                        for (int i = 0; i < drops; i++)
                        {
                            float x = 16 + i * (32f / (drops - 1 > 0 ? drops - 1 : 1)), y = 14 - (i % 2) * 4;
                            p.Line(new Vector2(x, y + 7), new Vector2(x - 2.4f, y), 4.2f, Outline);
                            p.Line(new Vector2(x, y + 7), new Vector2(x - 2.4f, y), 2.2f, Painter.Hex(0x4DA6FF));
                        }
                        if (id == "storm")
                            p.OutlinedPolygon(new[] { new Vector2(34, 26), new Vector2(26, 12), new Vector2(31, 12), new Vector2(27, 2), new Vector2(40, 18), new Vector2(34, 18) }, Painter.Hex(0xFFE04A), Outline, 1.2f);
                        break;
                    }
                    case "frost":
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * Mathf.PI / 3f;
                            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 24f;
                            p.Line(new Vector2(32, 32) - d, new Vector2(32, 32) + d, 7f, Outline);
                        }
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * Mathf.PI / 3f;
                            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 24f;
                            p.Line(new Vector2(32, 32) - d, new Vector2(32, 32) + d, 4f, Painter.Hex(0xD8F1FF));
                        }
                        p.Blob(32, 32, 6, 6, Painter.Hex(0xFFFFFF), Outline, 1.6f);
                        break;
                    }
                    default: Cloud(p, 32, 30, 1.1f, white); break;
                }
                return p.ToSprite(64f, new Vector2(0.5f, 0.5f));
            });
        }

        public static Sprite Coin { get { return SpriteBank.Rendered("icon_coin") ?? Get("coin", () => { var p = new Painter(64, 64); p.Blob(32, 32, 24, 24, Painter.Hex(0xFFC93C), Outline, 3f); p.Ellipse(32, 32, 15, 15, Painter.Hex(0xE8A81E)); p.Ellipse(31, 33, 13.5f, 13.5f, Painter.Hex(0xFFD968)); p.Line(new Vector2(32, 24), new Vector2(32, 41), 4f, Painter.Hex(0xC98A10)); return p.ToSprite(64f, new Vector2(0.5f, 0.5f)); }); } }

        public static Sprite Sack
        {
            get
            {
                return Get("sack", () =>
                {
                    var p = new Painter(64, 64);
                    p.Shadow(32, 8, 20, 5, 0.25f);
                    p.Blob(32, 28, 22, 20, Painter.Hex(0xD9B77A), Outline, 2.6f);
                    p.Blob(32, 49, 9, 8, Painter.Hex(0xC9A25E), Outline, 2.2f, false);
                    p.Line(new Vector2(22, 43), new Vector2(42, 43), 4f, Outline);
                    p.Line(new Vector2(22, 43), new Vector2(42, 43), 2.2f, Painter.Hex(0xB5453A));
                    return p.ToSprite(64f, new Vector2(0.5f, 0.5f));
                });
            }
        }

        public static Sprite Basket
        {
            get
            {
                return Get("basket", () =>
                {
                    var p = new Painter(64, 64);
                    p.Shadow(32, 8, 22, 5, 0.25f);
                    p.Blob(24, 36, 8, 8, Painter.Hex(0xE9506A), Outline, 1.8f);
                    p.Blob(36, 38, 8, 8, Painter.Hex(0xFFB627), Outline, 1.8f);
                    p.Blob(31, 40, 8, 8, Painter.Hex(0x58BF4A), Outline, 1.8f);
                    p.OutlinedPolygon(new[] { new Vector2(12, 34), new Vector2(52, 34), new Vector2(46, 10), new Vector2(18, 10) }, Painter.Hex(0xC98B49), Outline, 2.2f);
                    for (int i = 0; i < 3; i++) p.Line(new Vector2(20 + i * 12, 12), new Vector2(18 + i * 13, 32), 1.6f, Painter.Hex(0x9A6A3C));
                    return p.ToSprite(64f, new Vector2(0.5f, 0.5f));
                });
            }
        }

        /// <summary>A plain white rounded arrow-less tick for the sheet's close button.</summary>
        public static Sprite Close
        {
            get
            {
                return Get("close", () =>
                {
                    var p = new Painter(48, 48);
                    p.Line(new Vector2(12, 12), new Vector2(36, 36), 9f, Outline);
                    p.Line(new Vector2(12, 36), new Vector2(36, 12), 9f, Outline);
                    p.Line(new Vector2(12, 12), new Vector2(36, 36), 5.4f, Painter.Hex(0xFFF3D1));
                    p.Line(new Vector2(12, 36), new Vector2(36, 12), 5.4f, Painter.Hex(0xFFF3D1));
                    return p.ToSprite(48f, new Vector2(0.5f, 0.5f));
                });
            }
        }
    
        /// <summary>The Settings gear (ADR 0041 §4). ponytail: painted until US-171's Blender icons replace every icon.</summary>
        public static Sprite Gear
        {
            get
            {
                return SpriteBank.Rendered("icon_gear") ?? Get("gear", () =>
                {
                    var p = new Painter(64, 64);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.Line(new Vector2(32, 32) + d * 12f, new Vector2(32, 32) + d * 25f, 11f, Outline);
                        p.Line(new Vector2(32, 32) + d * 12f, new Vector2(32, 32) + d * 23.5f, 7f, Painter.Hex(0xC9C2B4));
                    }
                    p.Blob(32, 32, 17, 17, Painter.Hex(0xC9C2B4), Outline, 2.6f);
                    p.Blob(32, 32, 7, 7, Painter.Hex(0x6B5A45), Outline, 2f);
                    return p.ToSprite(64f, new Vector2(0.5f, 0.5f));
                });
            }
        }

        /// <summary>A skill star (ADR 0039 §7): 0 empty, 1 faded (possible), 2 solid (sure).</summary>
        public static Sprite Star(int kind)
        {
            return Get("star:" + kind, () =>
            {
                var p = new Painter(48, 48);
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 21f : 9f;
                    pts[i] = new Vector2(24 + Mathf.Cos(a) * r, 23 + Mathf.Sin(a) * r);
                }
                Color fill = kind == 2 ? Painter.Hex(0xFFC93C) : kind == 1 ? new Color(1f, 0.86f, 0.45f, 0.55f) : new Color(0.85f, 0.80f, 0.70f, 0.6f);
                p.OutlinedPolygon(pts, fill, kind == 0 ? new Color(0.55f, 0.48f, 0.38f, 0.7f) : Outline, 2.2f);
                return p.ToSprite(100, new Vector2(0.5f, 0.5f));
            });
        }
    }
}
