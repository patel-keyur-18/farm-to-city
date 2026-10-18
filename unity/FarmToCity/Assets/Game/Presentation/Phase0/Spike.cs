using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Presentation.Phase0
{
    /// <summary>
    /// The 2,000-agent performance spike (US-021, ADR 0034 §8), built in code and thrown away after Phase 0: a 64 × 64
    /// isometric map in 16 × 16 chunk tilemaps, 2,000 pooled sprites walking tile to tile from one struct array, and the
    /// citizen stub's hourly pass once a second. It measures the CPU main thread and GPU frame times with
    /// FrameTimingManager over a warm-up and two 30-second views (the whole map, then play zoom), or a 5-minute heat run.
    /// </summary>
    public sealed class Spike : MonoBehaviour
    {
        private const int MapSize = 64, Chunk = 16, Agents = 2000;
        private const float WarmUp = 10f, ViewSeconds = 20f;

        private struct Agent
        {
            public Vector3 Pos, Target;
            public int X, Y;
            public float Speed;
        }

        private Grid _grid;
        private Camera _camera;
        private Agent[] _agents;
        private Transform[] _sprites;
        private CitizenStub _stub;
        private float _start, _lastStub;
        private bool _heat;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        // Per frame: main-thread work, render-thread work, GPU, and the whole frame interval (FrameTimingManager).
        private readonly List<float> _cpu = new List<float>(), _render = new List<float>(), _gpu = new List<float>(), _frame = new List<float>(), _stubMs = new List<float>();
        private readonly StringBuilder _report = new StringBuilder();
        // The views, in order: sprites on the whole map, sprites at play zoom, then (spike only) the map with no agents
        // (the screen and the tiles alone) and the agents with the map hidden (the agents alone): together they split
        // the render thread's time between the screen wait, the tilemap and the 2,000 sprites. Round 3, 2026-10-18.
        private enum View { WarmUp, Sprites, SpritesZoom, NoAgents, NoMap, Done }
        private View[] _views;
        private int _phase;
        private float _phaseStart;

        public string Status { get; private set; } = "";
        public bool Done { get { return _views[_phase] == View.Done; } }
        public string Report { get { return _report.ToString(); } }

        public static Spike Start(bool heatRun)
        {
            var go = new GameObject("Spike");
            var s = go.AddComponent<Spike>();
            s._heat = heatRun;
            return s;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _camera = Camera.main;
            _camera.orthographic = true;

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(transform);
            _grid = gridGo.AddComponent<Grid>();
            _grid.cellLayout = GridLayout.CellLayout.Isometric;
            _grid.cellSize = new Vector3(1f, 0.5f, 1f);

            Tile[] tiles = { MakeTile(new Color(0.55f, 0.72f, 0.36f)), MakeTile(new Color(0.62f, 0.76f, 0.40f)),
                MakeTile(new Color(0.76f, 0.66f, 0.43f)), MakeTile(new Color(0.42f, 0.62f, 0.78f)) };
            for (int cy = 0; cy < MapSize / Chunk; cy++)
                for (int cx = 0; cx < MapSize / Chunk; cx++)
                {
                    var chunk = new GameObject("Chunk " + cx + "," + cy);
                    chunk.transform.SetParent(gridGo.transform);
                    var map = chunk.AddComponent<Tilemap>();
                    var renderer = chunk.AddComponent<TilemapRenderer>();
                    renderer.mode = TilemapRenderer.Mode.Chunk;
                    for (int y = 0; y < Chunk; y++)
                        for (int x = 0; x < Chunk; x++)
                        {
                            int gx = cx * Chunk + x, gy = cy * Chunk + y;
                            map.SetTile(new Vector3Int(gx, gy, 0), tiles[(gx * 7 + gy * 13) % 11 == 0 ? 3 : (gx + gy) % 3]);
                        }
                }

            // One sprite, one material: the 2,000 agents batch together.
            Sprite agentSprite = MakeAgentSprite();
            _agents = new Agent[Agents];
            _sprites = new Transform[Agents];
            var rnd = new System.Random(5); // presentation only; the sim never uses System.Random
            for (int i = 0; i < Agents; i++)
            {
                var a = new GameObject("Agent");
                a.transform.SetParent(transform);
                var sr = a.AddComponent<SpriteRenderer>();
                sr.sprite = agentSprite;
                sr.sortingOrder = 10;
                int x = rnd.Next(MapSize), y = rnd.Next(MapSize);
                _agents[i] = new Agent { X = x, Y = y, Pos = Cell(x, y), Target = Cell(x, y), Speed = 0.6f + (float)rnd.NextDouble() * 0.6f };
                _sprites[i] = a.transform;
            }
            _stub = new CitizenStub(Agents, MapSize);
            _start = _phaseStart = _lastStub = Time.realtimeSinceStartup;
            WholeMapView();
            _views = _heat ? new[] { View.WarmUp, View.Sprites, View.SpritesZoom, View.Done }
                : new[] { View.WarmUp, View.Sprites, View.SpritesZoom, View.NoAgents, View.NoMap, View.Done };
        }

        private void ShowSprites(bool on) { foreach (Transform t in _sprites) t.gameObject.SetActive(on); }

        private Vector3 Cell(int x, int y)
        {
            Vector3 w = _grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
            w.z = w.y * 0.01f; // sort by depth
            return w;
        }

        private void WholeMapView()
        {
            Vector3 c = _grid.GetCellCenterWorld(new Vector3Int(MapSize / 2, MapSize / 2, 0));
            _camera.transform.position = new Vector3(c.x, c.y, -10f);
            _camera.orthographicSize = MapSize * 0.25f + 1f; // the whole diamond, top to bottom
        }

        private void PlayView()
        {
            Vector3 c = _grid.GetCellCenterWorld(new Vector3Int(MapSize / 2, MapSize / 2, 0));
            _camera.transform.position = new Vector3(c.x, c.y, -10f);
            _camera.orthographicSize = 4f; // a few acres, as in play
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            // The agents: plain struct array, one loop, transforms written once each.
            for (int i = 0; i < _agents.Length; i++)
            {
                ref Agent a = ref _agents[i];
                Vector3 d = a.Target - a.Pos;
                float step = a.Speed * dt;
                if (d.sqrMagnitude <= step * step)
                {
                    a.Pos = a.Target;
                    int h = (i * 73856093) ^ (Time.frameCount * 19349663);
                    a.X = Mathf.Clamp(a.X + ((h & 1) == 0 ? 1 : -1) * ((h >> 1) & 1), 0, MapSize - 1);
                    a.Y = Mathf.Clamp(a.Y + ((h >> 2 & 1) == 0 ? 1 : -1) * ((h >> 3) & 1), 0, MapSize - 1);
                    a.Target = Cell(a.X, a.Y);
                }
                else a.Pos += d.normalized * step;
                if (_views[_phase] != View.NoAgents) _sprites[i].localPosition = a.Pos;
            }

            // The citizen stub's hourly pass, once a second (3,600 × the real rate).
            float stubMs = 0;
            if (now - _lastStub >= 1f)
            {
                _lastStub = now;
                var w = Stopwatch.StartNew();
                _stub.Hour();
                stubMs = (float)w.Elapsed.TotalMilliseconds;
            }

            FrameTimingManager.CaptureFrameTimings();
            bool timed = FrameTimingManager.GetLatestTimings(1, _timing) > 0;
            // cpuMainThreadFrameTime already ends when the main thread's work is done (it excludes waiting for the screen).
            float cpu = timed ? (float)_timing[0].cpuMainThreadFrameTime : dt * 1000f;
            float render = timed ? (float)_timing[0].cpuRenderThreadFrameTime : 0f;
            float gpu = timed ? (float)_timing[0].gpuFrameTime : 0f;
            float frame = timed ? (float)_timing[0].cpuFrameTime : dt * 1000f;

            float inPhase = now - _phaseStart;
            float viewLength = _heat ? 150f : ViewSeconds; // heat run: 2 × 2.5 min
            View view = _views[_phase];
            if (view == View.WarmUp)
            {
                Status = "Spike: warming up " + (int)(WarmUp - inPhase) + " s";
                if (inPhase >= WarmUp) Next(_phase + 1);
                return;
            }
            if (view == View.Done) return;
            _cpu.Add(cpu); _render.Add(render); _gpu.Add(gpu); _frame.Add(frame);
            if (stubMs > 0) _stubMs.Add(stubMs);
            Status = Name(view) + " " + (int)(viewLength - inPhase) + " s · main " + cpu.ToString("0.0")
                + " ms · render " + render.ToString("0.0") + " ms · GPU " + gpu.ToString("0.0") + " ms" + (timed ? "" : " (no frame timing)");
            if (inPhase < viewLength) return;
            Summarise(Name(view));
            Next(_phase + 1);
            switch (_views[_phase])
            {
                case View.SpritesZoom: PlayView(); break;
                case View.NoAgents: WholeMapView(); ShowSprites(false); break;
                case View.NoMap: ShowSprites(true); _grid.gameObject.SetActive(false); break;
                case View.Done: _grid.gameObject.SetActive(true); Finish(); break;
            }
        }

        private static string Name(View v)
        {
            switch (v)
            {
                case View.Sprites: return "Whole map, 2,000 sprite agents";
                case View.SpritesZoom: return "Play zoom, sprite agents";
                case View.NoAgents: return "Whole map, no agents (baseline)";
                default: return "Whole map, agents without the map";
            }
        }

        private void Next(int phase) { _phase = phase; _phaseStart = Time.realtimeSinceStartup; }

        private void Summarise(string view)
        {
            float[] cpu = _cpu.ToArray(), render = _render.ToArray(), gpu = _gpu.ToArray();
            Array.Sort(cpu); Array.Sort(render); Array.Sort(gpu);
            // A dropped frame at 60 Hz takes about 33 ms; 16.7 ms frames with vsync jitter are normal, so count over 25 ms.
            int dropped = 0;
            float total = 0;
            foreach (float v in _frame) { if (v > 25f) dropped++; total += v; }
            float fps = total > 0 ? 1000f * _frame.Count / total : 0;
            bool pass = P95(cpu) <= 8.3f && P95(gpu) <= 8.3f && dropped * 100 < _frame.Count;
            _report.AppendLine(view + ": " + (pass ? "main thread and GPU within 8.3 ms" : "OVER BUDGET"));
            _report.AppendLine("  CPU main thread ms: avg " + Avg(cpu).ToString("0.00") + ", p95 " + P95(cpu).ToString("0.00") + ", max " + Max(cpu).ToString("0.00"));
            _report.AppendLine("  Render thread ms:   avg " + Avg(render).ToString("0.00") + ", p95 " + P95(render).ToString("0.00") + ", max " + Max(render).ToString("0.00"));
            _report.AppendLine("  GPU ms:             avg " + Avg(gpu).ToString("0.00") + ", p95 " + P95(gpu).ToString("0.00") + ", max " + Max(gpu).ToString("0.00"));
            _report.AppendLine("  Dropped frames (over 25 ms): " + dropped + " of " + _frame.Count + " · " + fps.ToString("0.0") + " fps");
            _cpu.Clear(); _render.Clear(); _gpu.Clear(); _frame.Clear();
        }

        private void Finish()
        {
            float[] s = _stubMs.ToArray();
            Array.Sort(s);
            _report.AppendLine("Citizen stub (2,000 citizens, once a second): avg " + Avg(s).ToString("0.00") + " ms, max " + Max(s).ToString("0.00") + " ms");
            _report.AppendLine((_heat ? "Heat run: " : "Spike: ") + (Time.realtimeSinceStartup - _start).ToString("0") + " s in all, " + Screen.width + " × " + Screen.height);
            UnityEngine.Debug.Log("Phase 0 spike result\n" + _report);
            Status = "Spike done";
        }

        private static float Avg(float[] v) { if (v.Length == 0) return 0; float s = 0; foreach (float x in v) s += x; return s / v.Length; }
        private static float P95(float[] sorted) { return sorted.Length == 0 ? 0 : sorted[Mathf.Min(sorted.Length - 1, (int)(sorted.Length * 0.95f))]; }
        private static float Max(float[] sorted) { return sorted.Length == 0 ? 0 : sorted[sorted.Length - 1]; }

        private static Tile MakeTile(Color c)
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - w / 2f) / (w / 2f), dy = Mathf.Abs(y + 0.5f - h / 2f) / (h / 2f);
                    bool inside = dx + dy <= 1f;
                    bool edge = dx + dy > 0.92f;
                    px[y * w + x] = inside ? (Color32)(edge ? c * 0.85f : c) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            return tile;
        }

        private static Sprite MakeAgentSprite()
        {
            const int w = 8, h = 14;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = y >= 10 ? new Color32(120, 80, 50, 255) : new Color32(200, 70, 60, 255); // head, body
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32);
        }
    }
}
