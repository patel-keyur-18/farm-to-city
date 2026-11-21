using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>The overlay hues (spec 024 §3.8, G7): one hue per overlay in five light-to-dark steps, never blue-to-red, so the key reads for colour-blind players too.</summary>
    public static class OverlayColours
    {
        private static readonly int[][] Ramps =
        {
            new[] { 0xEEF6D8, 0xCDE5A0, 0x9FCB62, 0x5FA23A, 0x2C6A26 }, // fertility: green
            new[] { 0xFDF1CF, 0xF8DC92, 0xEFBD4C, 0xD38A16, 0x9A5A05 }, // land value: amber
            new[] { 0xE6EEF8, 0xC1D6EE, 0x8FB4DE, 0x4F84C0, 0x1F4F8F }, // coverage: blue
            new[] { 0xF0E8F7, 0xD9C6EC, 0xB996D8, 0x8F5CBF, 0x5E2D95 }, // happiness: violet
            new[] { 0xF9E3DA, 0xF1C0AD, 0xE59470, 0xCB5E3C, 0x93321D }, // crime: terracotta red, once the town is read (spec 026)
        };

        private const int CrimeGrey = 0xCFCFC8;

        /// <param name="live">False for crime before the town's first reading: one grey step, nothing to read.</param>
        public static Color Step(OverlayKind kind, int step, bool live = true)
        {
            if (kind == OverlayKind.Crime && !live) return Painter.Hex(CrimeGrey);
            int[] ramp = Ramps[(int)kind - 1];
            return Painter.Hex(ramp[Mathf.Clamp(step, 0, ramp.Length - 1)]);
        }
    }

    /// <summary>
    /// The overlay on the map (US-153): every acre it tints gets its step's colour over the ground and every other acre is dimmed, drawn as ONE
    /// mesh of 432 coloured diamonds (vertex colours), with the coverage overlay's smoke hatch as a second small mesh. The map's sprites do not
    /// batch (a draw call each), so 432 tint sprites would have cost 430 draw calls; the mesh costs two. Both are made once and recoloured.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private const int OverlayOrder = 8980, HatchOrder = 8990;
        private static readonly Color32 Dimmed = new Color32(26, 31, 15, 92);
        private const float TintAlpha = 0.86f, Inset = 0.965f;
        private Mesh _tintMesh, _hatchMesh;
        private MeshRenderer _tintRenderer, _hatchRenderer;
        private Color32[] _tintColours;

        public bool OverlayShown { get; private set; }

        private MeshRenderer NewMesh(string name, Mesh mesh, Texture texture, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(SpriteMaterial()) { mainTexture = texture }; // the sprite shader is always in the build; Shader.Find might not be
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void BuildOverlay()
        {
            int n = _width * _height;
            float hw = _grid.HalfWidth * Inset, hh = _grid.HalfHeight * Inset;
            var verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];
            var tris = new int[n * 6];
            for (int t = 0; t < n; t++)
            {
                Vector3 c = TileCentre(t);
                int v = t * 4;
                verts[v] = new Vector3(c.x, c.y + hh, 0); verts[v + 1] = new Vector3(c.x + hw, c.y, 0);
                verts[v + 2] = new Vector3(c.x, c.y - hh, 0); verts[v + 3] = new Vector3(c.x - hw, c.y, 0);
                for (int k = 0; k < 4; k++) uvs[v + k] = new Vector2(0.5f, 0.5f);
                int i = t * 6;
                tris[i] = v; tris[i + 1] = v + 1; tris[i + 2] = v + 2; tris[i + 3] = v; tris[i + 4] = v + 2; tris[i + 5] = v + 3;
            }
            _tintMesh = new Mesh { name = "overlayTint" };
            _tintMesh.vertices = verts;
            _tintMesh.uv = uvs;
            _tintMesh.triangles = tris;
            _tintMesh.RecalculateBounds();
            _tintColours = new Color32[n * 4];
            _tintRenderer = NewMesh("overlayTint", _tintMesh, Texture2D.whiteTexture, OverlayOrder);
            _hatchMesh = new Mesh { name = "smokeHatch" };
            _hatchRenderer = NewMesh("smokeHatch", _hatchMesh, SpriteBank.OverlayHatch.texture, HatchOrder);
        }

        public void ShowOverlay(OverlayVm vm)
        {
            if (_tintMesh == null) BuildOverlay();
            OverlayShown = true;
            int n = _width * _height;
            for (int t = 0; t < n; t++)
            {
                int step = t < vm.Steps.Length ? vm.Steps[t] : OverlayVm.NoStep;
                Color32 c = Dimmed;
                if (step >= 0) { Color col = OverlayColours.Step(vm.Kind, step, vm.Labels.Count > 0); col.a = TintAlpha; c = col; }
                for (int k = 0; k < 4; k++) _tintColours[t * 4 + k] = c;
            }
            _tintMesh.colors32 = _tintColours;
            _tintRenderer.enabled = true;
            ShowHatch(vm);
        }

        /// <summary>The smoke's hatch: the acre's diamond textured with the red stripes, for each acre the smoke reaches.</summary>
        private void ShowHatch(OverlayVm vm)
        {
            var smoke = new System.Collections.Generic.List<int>();
            for (int t = 0; t < vm.Smoke.Length && t < _width * _height; t++) if (vm.Smoke[t]) smoke.Add(t);
            _hatchMesh.Clear();
            _hatchRenderer.enabled = smoke.Count > 0;
            if (smoke.Count == 0) return;
            float hw = _grid.HalfWidth, hh = _grid.HalfHeight;
            var verts = new Vector3[smoke.Count * 4];
            var uvs = new Vector2[smoke.Count * 4];
            var tris = new int[smoke.Count * 6];
            for (int j = 0; j < smoke.Count; j++)
            {
                Vector3 c = TileCentre(smoke[j]);
                int v = j * 4;
                verts[v] = new Vector3(c.x, c.y + hh, 0); verts[v + 1] = new Vector3(c.x + hw, c.y, 0);
                verts[v + 2] = new Vector3(c.x, c.y - hh, 0); verts[v + 3] = new Vector3(c.x - hw, c.y, 0);
                // The sprite is 140 × 74 with its diamond between x 4–136 and y 4–70: the diamond's corners in the texture.
                uvs[v] = new Vector2(70f / 140f, 70f / 74f); uvs[v + 1] = new Vector2(136f / 140f, 37f / 74f);
                uvs[v + 2] = new Vector2(70f / 140f, 4f / 74f); uvs[v + 3] = new Vector2(4f / 140f, 37f / 74f);
                int i = j * 6;
                tris[i] = v; tris[i + 1] = v + 1; tris[i + 2] = v + 2; tris[i + 3] = v; tris[i + 4] = v + 2; tris[i + 5] = v + 3;
            }
            _hatchMesh.vertices = verts;
            _hatchMesh.uv = uvs;
            _hatchMesh.triangles = tris;
            _hatchMesh.RecalculateBounds();
        }

        public void HideOverlay()
        {
            OverlayShown = false;
            if (_tintRenderer == null) return;
            _tintRenderer.enabled = false;
            _hatchRenderer.enabled = false;
        }

        /// <summary>The tile under a world point, or -1 (an acre's own sheet is not what an overlay's tap opens).</summary>
        public int TileAt(Vector2 world) { return TileOfCell(_grid.Pick(world.x, world.y)); }

        /// <summary>Where a tile is on screen's world plane, for the tap card.</summary>
        public Vector2 TileWorld(int tile) { Vector3 c = TileCentre(tile); return new Vector2(c.x, c.y); }
    }
}
