using System;

namespace Sim.View.Views
{
    /// <summary>
    /// The isometric map's geometry (spec 008 §3.4): tile ↔ world position for a 2:1 diamond grid, hit-testing for taps, and the
    /// camera's zoom and pan limits. Pure maths, so it is tested without Unity. World units are arbitrary (the presentation scales
    /// them); y points up.
    /// </summary>
    public sealed class IsoGrid
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Half the width of one tile's diamond, in world units; the half height is half of that (2:1).</summary>
        public float HalfWidth { get; }
        public float HalfHeight { get { return HalfWidth / 2f; } }

        public IsoGrid(int width, int height, float halfWidth = 1f)
        {
            Width = width;
            Height = height;
            HalfWidth = halfWidth;
        }

        /// <summary>The centre of a tile's diamond. Tile (0,0) is the top corner; x runs down-right, y runs down-left.</summary>
        public (float x, float y) Centre(int tx, int ty)
        {
            return ((tx - ty) * HalfWidth, -(tx + ty) * HalfHeight);
        }

        /// <summary>Fractional tile coordinates under a world point (the inverse of <see cref="Centre"/>).</summary>
        public (float tx, float ty) ToTile(float wx, float wy)
        {
            float a = wx / HalfWidth, b = -wy / HalfHeight; // a = tx − ty, b = tx + ty
            return ((a + b) / 2f, (b - a) / 2f);
        }

        /// <summary>The tile under a world point, or −1 outside the map (a point inside a diamond belongs to that tile).</summary>
        public int Pick(float wx, float wy)
        {
            (float fx, float fy) = ToTile(wx, wy);
            int tx = (int)Math.Floor(fx + 0.5f), ty = (int)Math.Floor(fy + 0.5f);
            if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return -1;
            return ty * Width + tx;
        }

        /// <summary>The world rectangle the whole map covers: left, bottom, right, top.</summary>
        public (float left, float bottom, float right, float top) Bounds()
        {
            float left = -(Height - 0.5f) * HalfWidth - HalfWidth * 0.5f, right = (Width - 0.5f) * HalfWidth + HalfWidth * 0.5f;
            float top = HalfHeight, bottom = -(Width + Height - 1) * HalfHeight - HalfHeight;
            return (left, bottom, right, top);
        }
    }

    /// <summary>
    /// The map camera (spec 008 §3.4): pinch zoom between the whole map and about 3 acres, drag to pan, clamped to the map. The
    /// camera looks at <see cref="CentreX"/>, <see cref="CentreY"/> and shows <see cref="HalfHeight"/> world units above and
    /// below it (an orthographic size).
    /// </summary>
    public sealed class MapCamera
    {
        private readonly IsoGrid _grid;
        private readonly float _aspect;

        public float CentreX { get; private set; }
        public float CentreY { get; private set; }

        /// <summary>The orthographic size: half the visible height in world units.</summary>
        public float HalfHeight { get; private set; }

        /// <summary>Zoomed out all the way: the whole map fits (or, with <see cref="Limit"/>, the window the ring's cap allows).</summary>
        public float MaxHalfHeight { get; private set; }

        private (float left, float bottom, float right, float top) _b;

        /// <summary>Zoomed in all the way: about three acres across the screen.</summary>
        public float MinHalfHeight { get; private set; }

        public MapCamera(IsoGrid grid, float aspect)
        {
            _grid = grid;
            _aspect = aspect;
            var b = grid.Bounds();
            _b = b;
            float w = b.right - b.left, h = b.top - b.bottom;
            // The whole map fits both ways; the closest zoom shows about three tile diamonds across.
            MaxHalfHeight = Math.Max(h / 2f, w / aspect / 2f) * 1.05f;
            MinHalfHeight = 3f * grid.HalfWidth * 2f / aspect / 2f;
            if (MinHalfHeight > MaxHalfHeight) MinHalfHeight = MaxHalfHeight;
            HalfHeight = MaxHalfHeight;
            CentreX = (b.left + b.right) / 2f;
            CentreY = (b.top + b.bottom) / 2f;
        }

        /// <summary>
        /// Limits the camera to a rectangle of the grid (tiles <paramref name="x0"/>, <paramref name="y0"/> to <paramref name="x1"/>, <paramref name="y1"/>)
        /// and its furthest zoom-out to <paramref name="scale"/> times the window of a <paramref name="windowW"/> × <paramref name="windowH"/> map
        /// (spec 025 §3.4, ADR 0125): the draw calls follow what is in the window, so the ring costs nothing at the furthest zoom.
        /// </summary>
        public void Limit(int x0, int y0, int x1, int y1, int windowW, int windowH, float scale)
        {
            float hw = _grid.HalfWidth, hh = _grid.HalfHeight;
            _b = (_grid.Centre(x0, y1).x - hw, _grid.Centre(x1, y1).y - hh - 2f * hh, _grid.Centre(x1, y0).x + hw, _grid.Centre(x0, y0).y + hh);
            var w = new IsoGrid(windowW, windowH, hw).Bounds();
            MaxHalfHeight = Math.Max((w.top - w.bottom) / 2f, (w.right - w.left) / _aspect / 2f) * 1.05f * scale;
            if (MinHalfHeight > MaxHalfHeight) MinHalfHeight = MaxHalfHeight;
            HalfHeight = Math.Max(MinHalfHeight, Math.Min(MaxHalfHeight, HalfHeight));
            Clamp();
        }

        /// <summary>Puts the camera on a tile at the closest comfortable zoom, for the start (the inherited parcels).</summary>
        public void Focus(int tx, int ty, float halfHeight)
        {
            (float x, float y) = _grid.Centre(tx, ty);
            HalfHeight = Math.Max(MinHalfHeight, Math.Min(MaxHalfHeight, halfHeight));
            CentreX = x;
            CentreY = y;
            Clamp();
        }

        /// <summary>Centres the camera on a world point at a given zoom (clamped).</summary>
        public void FocusWorld(float x, float y, float halfHeight)
        {
            HalfHeight = Math.Max(MinHalfHeight, Math.Min(MaxHalfHeight, halfHeight));
            CentreX = x;
            CentreY = y;
            Clamp();
        }

        /// <summary>
        /// Where the camera should centre so a world point sits <paramref name="fromTop"/> of the way down the screen (0 = the top
        /// edge), so a bottom sheet doesn't cover what was tapped.
        /// </summary>
        public (float x, float y) CentreFor(float worldX, float worldY, float fromTop)
        {
            return (worldX, worldY - HalfHeight * (1f - 2f * fromTop));
        }

        /// <summary>Pinch: multiplies the visible size by <paramref name="factor"/> (below 1 zooms in), keeping the point under the fingers fixed.</summary>
        public void Zoom(float factor, float pivotWorldX, float pivotWorldY)
        {
            float next = Math.Max(MinHalfHeight, Math.Min(MaxHalfHeight, HalfHeight * factor));
            float k = next / HalfHeight;
            CentreX = pivotWorldX + (CentreX - pivotWorldX) * k;
            CentreY = pivotWorldY + (CentreY - pivotWorldY) * k;
            HalfHeight = next;
            Clamp();
        }

        /// <summary>Drag: moves the view by a world-space delta.</summary>
        public void Pan(float dx, float dy)
        {
            CentreX += dx;
            CentreY += dy;
            Clamp();
        }

        private void Clamp()
        {
            var b = _b;
            float halfW = HalfHeight * _aspect;
            CentreX = ClampAxis(CentreX, b.left, b.right, halfW);
            // The bottom has extra room so a tap near the south edge can be lifted above a bottom sheet.
            CentreY = ClampAxis(CentreY, b.bottom - HalfHeight * 1.3f, b.top, HalfHeight);
        }

        // The view stays inside the map; when the view is larger than the map on this axis, the map is centred.
        private static float ClampAxis(float c, float lo, float hi, float half)
        {
            if (half * 2f >= hi - lo) return (lo + hi) / 2f;
            return Math.Max(lo + half, Math.Min(hi - half, c));
        }
    }
}
