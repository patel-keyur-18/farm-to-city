using UnityEngine;

namespace Game.Presentation.Art
{
    /// <summary>
    /// A tiny software painter for the flat, outlined, softly shaded placeholder sprites (ADR 0038): shapes are drawn into a
    /// supersampled buffer (4 × 4 samples a pixel, so edges are smooth) and averaged down to a texture. Like drawing with a vector
    /// pen: an outline, a base colour, a lit side and a highlight. Everything is code, so there are no art assets to hand-edit
    /// (ADR 0017); the Blender renders of US-171 replace the sprites later through <see cref="SpriteBank"/>.
    /// </summary>
    public sealed class Painter
    {
        private const int Ss = 4;
        private readonly int _w, _h, _sw, _sh;
        private readonly float[] _px; // premultiplied RGBA, supersampled

        public int Width { get { return _w; } }
        public int Height { get { return _h; } }

        public Painter(int width, int height)
        {
            _w = width;
            _h = height;
            _sw = width * Ss;
            _sh = height * Ss;
            _px = new float[_sw * _sh * 4];
        }

        // ---- shapes (coordinates in pixels, y up) ----

        private void Blend(int sx, int sy, Color c)
        {
            if (sx < 0 || sy < 0 || sx >= _sw || sy >= _sh || c.a <= 0f) return;
            int i = (sy * _sw + sx) * 4;
            float a = c.a, inv = 1f - a;
            _px[i] = c.r * a + _px[i] * inv;
            _px[i + 1] = c.g * a + _px[i + 1] * inv;
            _px[i + 2] = c.b * a + _px[i + 2] * inv;
            _px[i + 3] = a + _px[i + 3] * inv;
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.FloorToInt((cx - rx) * Ss), x1 = Mathf.CeilToInt((cx + rx) * Ss);
            int y0 = Mathf.FloorToInt((cy - ry) * Ss), y1 = Mathf.CeilToInt((cy + ry) * Ss);
            float scx = cx * Ss, scy = cy * Ss, srx = rx * Ss, sry = ry * Ss;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - scx) / srx, dy = (y + 0.5f - scy) / sry;
                    if (dx * dx + dy * dy <= 1f) Blend(x, y, c);
                }
        }

        /// <summary>An ellipse with an outline, a base, a lit upper-left and a small highlight: the toy-like "lit blob".</summary>
        public void Blob(float cx, float cy, float rx, float ry, Color baseColour, Color outline, float outlineWidth = 1.6f, bool shine = true)
        {
            Ellipse(cx, cy, rx + outlineWidth, ry + outlineWidth, outline);
            Ellipse(cx, cy, rx, ry, Shade(baseColour, 0.82f));
            Ellipse(cx - rx * 0.08f, cy + ry * 0.12f, rx * 0.88f, ry * 0.86f, baseColour);
            Ellipse(cx - rx * 0.20f, cy + ry * 0.28f, rx * 0.55f, ry * 0.50f, Shade(baseColour, 1.12f));
            if (shine) Ellipse(cx - rx * 0.32f, cy + ry * 0.45f, rx * 0.16f, ry * 0.12f, new Color(1f, 1f, 1f, 0.55f));
        }

        public void Polygon(Vector2[] pts, Color c)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * Ss)), y1 = Mathf.Min(_sh - 1, Mathf.CeilToInt(maxY * Ss));
            var xs = new System.Collections.Generic.List<float>();
            for (int y = y0; y <= y1; y++)
            {
                float sy = (y + 0.5f) / Ss;
                xs.Clear();
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy)) xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(xs[k] * Ss - 0.5f)); x <= Mathf.Min(_sw - 1, Mathf.FloorToInt(xs[k + 1] * Ss - 0.5f)); x++) Blend(x, y, c);
            }
        }

        /// <summary>A polygon with a rounded outline (the polygon grown by stroking its edges), then its fill.</summary>
        public void OutlinedPolygon(Vector2[] pts, Color fill, Color outline, float width = 1.6f)
        {
            for (int i = 0; i < pts.Length; i++) Line(pts[i], pts[(i + 1) % pts.Length], width * 2f, outline);
            Polygon(pts, fill);
        }

        public void Line(Vector2 a, Vector2 b, float thickness, Color c)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * Ss));
            float r = thickness / 2f;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                Ellipse(p.x, p.y, r, r, c);
            }
        }

        public void Diamond(float cx, float cy, float hw, float hh, Color c)
        {
            Polygon(new[] { new Vector2(cx, cy + hh), new Vector2(cx + hw, cy), new Vector2(cx, cy - hh), new Vector2(cx - hw, cy) }, c);
        }

        /// <summary>A soft ground shadow: a flat ellipse of falling opacity.</summary>
        public void Shadow(float cx, float cy, float rx, float ry, float opacity = 0.28f)
        {
            for (int i = 4; i >= 1; i--)
            {
                float k = i / 4f;
                Ellipse(cx, cy, rx * (0.6f + 0.4f * k), ry * (0.6f + 0.4f * k), new Color(0.1f, 0.12f, 0.05f, opacity / 4f));
            }
        }

        // ---- colours ----

        public static Color Shade(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }

        public static Color Hex(int rgb, float a = 1f) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a); }

        // ---- output ----

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var colours = new Color32[_w * _h];
            float norm = 1f / (Ss * Ss);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int j = 0; j < Ss; j++)
                        for (int i = 0; i < Ss; i++)
                        {
                            int k = ((y * Ss + j) * _sw + x * Ss + i) * 4;
                            r += _px[k]; g += _px[k + 1]; b += _px[k + 2]; a += _px[k + 3];
                        }
                    r *= norm; g *= norm; b *= norm; a *= norm;
                    colours[y * _w + x] = a <= 0.001f ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)Mathf.Clamp(r / a * 255f + 0.5f, 0, 255), (byte)Mathf.Clamp(g / a * 255f + 0.5f, 0, 255),
                            (byte)Mathf.Clamp(b / a * 255f + 0.5f, 0, 255), (byte)Mathf.Clamp(a * 255f + 0.5f, 0, 255));
                }
            tex.SetPixels32(colours);
            tex.Apply(false, true);
            return tex;
        }

        public Sprite ToSprite(float pixelsPerUnit, Vector2 pivot)
        {
            return Sprite.Create(ToTexture(), new Rect(0, 0, _w, _h), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
