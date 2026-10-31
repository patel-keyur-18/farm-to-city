using System;
using System.Collections.Generic;
using Sim.View.Content;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The weather on the map (spec 008 §3.4, ADR 0083 §13–18), as the owner approved it in the preview (<c>docs/art/s15-222-weather-*.png</c>).
    /// <see cref="WeatherLook"/> decides what shows each frame; this draws it:
    /// <list type="bullet">
    /// <item>the colour, saturation and brightness through one URP Color Adjustments volume (the HUD, drawn after it, is untouched);</item>
    /// <item>a pale wash, mist, haze and the lightning glow as screen-space sprites on the camera;</item>
    /// <item>rain and splashes as particle systems on the camera (frozen with reduced motion);</item>
    /// <item>cloud shadows and rime fixed to the ground, so they don't slide when you pan;</item>
    /// <item>rain and thunder sounds behind the Sound switch, fading with the weather.</item>
    /// </list>
    /// Everything above the map sprites, below the UI. Like a CSS overlay stack on a page, but in the camera's space.
    /// </summary>
    public sealed class WeatherPresenter : MonoBehaviour
    {
        private const int Above = 32000; // above every map sprite's sorting order
        private const float Points = 390f; // the preview's points: an iPhone 13 screen is 390 high

        /// <summary>The developer panel's Weather look ▸ pick (a weather id), or null: look only, cleared when play starts (ADR 0083 §18).</summary>
        public static string DevOverride;

        private WeatherLookDef _def;
        private WeatherLook _look;
        private Camera _camera;
        private MapPresenter _map;
        private Func<string> _spell;
        private Func<bool> _reduced, _sound;
        private Func<float> _dayLight;
        private float _refOrtho;

        private ColorAdjustments _grade;
        private SpriteRenderer _wash, _mist, _haze, _glow, _rime;
        private readonly List<SpriteRenderer> _clouds = new List<SpriteRenderer>();
        private readonly List<Vector2> _cloudHome = new List<Vector2>();
        private ParticleSystem _rain, _splash;
        private AudioSource _rainSound, _thunder;
        private Bounds _world;
        private float _time;

        public static WeatherPresenter Create(Transform parent, Camera camera, MapPresenter map, WeatherLookDef def, Func<string> spell, Func<bool> reduced,
            Func<bool> sound, Func<float> dayLight)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            WeatherPresenter w = go.AddComponent<WeatherPresenter>();
            w._def = def;
            w._look = new WeatherLook(def);
            w._camera = camera;
            w._map = map;
            w._spell = spell;
            w._reduced = reduced;
            w._sound = sound;
            w._dayLight = dayLight;
            w._refOrtho = camera.orthographicSize;
            DevOverride = null;
            w.Build();
            return w;
        }

        // ---- building ----

        private void Build()
        {
            // The grade: one global volume, post-processing on the map camera (the UI Toolkit HUD is drawn after it).
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _grade = profile.Add<ColorAdjustments>(true);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            Sprite white = MakeSprite(Fill(4, 4, (x, y) => 1f), 4);
            Sprite soft = MakeSprite(Fill(64, 64, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f; return Mathf.Clamp01(1 - d * d); }), 64);
            Sprite fade = MakeSprite(Fill(4, 64, (x, y) => Mathf.Clamp01((y - 19f) / 44f)), 64); // clear at the bottom 30%, full at the top

            Transform cam = _camera.transform;
            _wash = Screen("wash", white, cam, Above + 1);
            _mist = Screen("mist", white, cam, Above + 2);
            _haze = Screen("haze", fade, cam, Above + 3);
            _glow = Screen("glow", soft, cam, Above + 20);

            // Ground layers: rime over the whole valley as it's drawn, cloud shadows wandering over it.
            _world = _map.WorldBounds();
            Texture2D speckle = Speckle(256);
            _rime = Ground("rime", Sprite.Create(speckle, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 128, 0, SpriteMeshType.FullRect), Above - 2);
            _rime.drawMode = SpriteDrawMode.Tiled;
            _rime.size = _world.size;
            _rime.transform.position = new Vector3(_world.center.x, _world.center.y, 0);
            var seed = new System.Random(222);
            for (int i = 0; i < 8; i++)
            {
                SpriteRenderer c = Ground("cloud" + i, soft, Above - 1);
                float size = (280 + (float)seed.NextDouble() * 320) * _refOrtho * 2 / Points / 4f; // the soft sprite is 1 unit, 4 points of 390 at reference zoom
                c.transform.localScale = new Vector3(size * 4f, size * 2.2f, 1);
                _clouds.Add(c);
                _cloudHome.Add(new Vector2((float)seed.NextDouble(), (float)seed.NextDouble()));
            }

            _rain = Particles("rain", cam, MakeStreak(), Above + 10, true);
            _splash = Particles("splashes", cam, MakeRing(), Above + 9, false);

            _rainSound = gameObject.AddComponent<AudioSource>();
            _rainSound.clip = Resources.Load<AudioClip>("Sounds/weather_rain");
            _rainSound.loop = true;
            _rainSound.volume = 0;
            _rainSound.playOnAwake = false;
            _thunder = gameObject.AddComponent<AudioSource>();
            _thunder.clip = Resources.Load<AudioClip>("Sounds/weather_thunder");
            _thunder.playOnAwake = false;
            _thunder.volume = 0.55f;
        }

        private static Texture2D Fill(int w, int h, Func<int, int, float> alpha)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * alpha(x, y)));
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        private static Sprite MakeSprite(Texture2D t, float ppu) { return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect); }

        /// <summary>White rime crystals scattered on clear (the preview's: mostly specks, now and then a little cross), tiled over the valley.</summary>
        private static Texture2D Speckle(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            var r = new System.Random(7);
            for (int i = 0; i < n * n / 40; i++)
            {
                int x = r.Next(n), y = r.Next(n);
                byte a = (byte)(90 + r.Next(165));
                bool big = r.NextDouble() < 0.15, cross = r.NextDouble() < 0.03;
                for (int dy = 0; dy <= (big ? 1 : 0); dy++)
                    for (int dx = 0; dx <= (big ? 1 : 0); dx++) px[((y + dy) % n) * n + (x + dx) % n] = new Color32(255, 255, 255, a);
                if (cross) for (int k = -1; k <= 1; k++) { px[y * n + (x + k + n) % n] = new Color32(255, 255, 255, a); px[((y + k + n) % n) * n + x] = new Color32(255, 255, 255, a); }
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        private static Texture2D MakeStreak() { return Fill(4, 32, (x, y) => Mathf.Sin(Mathf.PI * (y + 0.5f) / 32f) * (x == 0 || x == 3 ? 0.4f : 1f)); }

        private static Texture2D MakeRing()
        {
            return Fill(16, 16, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)); return Mathf.Clamp01(1 - Mathf.Abs(d - 5.5f) / 1.5f); });
        }

        private static SpriteRenderer Screen(string name, Sprite sprite, Transform cam, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0, 0, 10);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.clear;
            return r;
        }

        private SpriteRenderer Ground(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.clear;
            return r;
        }

        private ParticleSystem Particles(string name, Transform cam, Texture2D texture, int order, bool streaks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0, 0, 10);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0;
            main.maxParticles = streaks ? Mathf.Max(1, _def.StreakCap) : 200;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var r = go.GetComponent<ParticleSystemRenderer>();
            // A sprite's own material (the 2D renderer's default sprite shader) is always in the build; Shader.Find might not be.
            r.sharedMaterial = new Material(_map.SpriteMaterial()) { mainTexture = texture };
            r.sortingOrder = order;
            r.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (streaks)
            {
                ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
                v.enabled = true;
                v.space = ParticleSystemSimulationSpace.Local;
            }
            else
            {
                ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
                s.enabled = true;
                s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1.4f));
                ParticleSystem.ColorOverLifetimeModule c = ps.colorOverLifetime;
                c.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                c.color = g;
            }
            return ps;
        }

        // ---- every frame ----

        private void LateUpdate()
        {
            bool reduced = _reduced();
            _look.Override = DevOverride;
            WeatherFrame f = _look.Step(Time.unscaledDeltaTime, _spell(), reduced, _dayLight());
            if (f.To == null) return;
            if (f.Moving) _time += Time.unscaledDeltaTime;
            float day = Mathf.Clamp01((f.Light - _def.NightLight) / Mathf.Max(0.01f, 1f - _def.NightLight));
            _ground = 0.22f + 0.23f * day;
            float w = f.Weight;
            WeatherStyle a = f.From, b = f.To;
            float Mix(Func<WeatherStyle, float> v) { return v(a) * (1 - w) + v(b) * w; }

            // The grade.
            Rgb fa = a.Filter, fb = b.Filter;
            _grade.colorFilter.Override(new Color(fa.R + (fb.R - fa.R) * w, fa.G + (fb.G - fa.G) * w, fa.B + (fb.B - fa.B) * w, 1));
            _grade.saturation.Override((Mix(s => s.Saturation) - 1f) * 100f);
            _grade.postExposure.Override(Mathf.Log(Mix(s => s.Brightness), 2f));

            // The screen layers, sized to the view.
            float h = _camera.orthographicSize * 2f, wd = h * _camera.aspect, pt = h / Points;
            foreach (SpriteRenderer r in new[] { _wash, _mist, _haze }) r.transform.localScale = new Vector3(wd / r.sprite.bounds.size.x * 1.02f, h / r.sprite.bounds.size.y * 1.02f, 1);
            _wash.color = Blend(a.Overlay, b.Overlay, w, A(Mix(s => s.OverlayAlpha) * f.Light, Lum(w < 0.5f ? a.Overlay : b.Overlay)));
            _mist.color = Blend(a.Mist, b.Mist, w, A(Mix(s => s.MistAlpha) * f.Light, Lum(w < 0.5f ? a.Mist : b.Mist)));
            float shimmer = f.Moving ? Mix(s => s.Shimmer) * pt : 0f; // the far fields' haze sways a little (look only, no screen copy)
            _haze.transform.localPosition = new Vector3(Mathf.Sin(_time * 2.4f) * shimmer, Mathf.Sin(_time * 1.3f) * shimmer, 10);
            _haze.color = new Color(1f, 0.925f, 0.784f, A(Mix(s => s.Haze) * f.Light * (f.Moving ? 1f - 0.15f * (0.5f + 0.5f * Mathf.Sin(_time * 3.1f)) : 1f), 0.9f));
            _glow.transform.localScale = Vector3.one * wd * 1.8f;
            _glow.transform.localPosition = new Vector3(-wd * 0.15f, h * 0.5f, 10);
            _glow.color = new Color(0.88f, 0.91f, 1f, A(f.Glow, 0.93f));

            // The ground layers.
            _rime.color = new Color(1, 1, 1, A(Mix(s => s.Rime) * (f.Light < 1f ? 0.6f + 0.4f * f.Light : 1f), 1f));
            for (int i = 0; i < _clouds.Count; i++)
            {
                float alpha = (i < Count(a) ? a.Clouds.Alpha * (1 - w) : 0) + (i < Count(b) ? b.Clouds.Alpha * w : 0);
                _clouds[i].color = new Color(0.06f, 0.1f, 0.14f, A(alpha, 0.1f));
                float drift = _time * Mix(s => s.Clouds == null ? 0 : s.Clouds.Speed) * pt;
                Vector2 home = _cloudHome[i];
                float x = Wrap(_world.min.x + home.x * _world.size.x + drift, _world.min.x, _world.max.x);
                float y = Wrap(_world.min.y + home.y * _world.size.y + drift * 0.45f, _world.min.y, _world.max.y);
                _clouds[i].transform.position = new Vector3(x, y, 0);
            }

            Rain(f, pt, wd, h);
            StreaksDef wind = f.To.Streaks;
            _map.Gust = wind == null || wind.Gust <= 0 ? 0f : f.Gust / wind.Gust; // −1 to 1: trees lean up to 4° at a gust's peak
            Sound(f);
        }

        private float _ground = 0.45f; // the ground's typical brightness on screen (sRGB): set each frame from the time of day

        /// <summary>
        /// The preview blended in sRGB, as browsers do; the game renders in linear colour, where the same alpha lifts dark ground far more
        /// (and a dark shadow darkens less). This gives the alpha that looks the same as the approved preview over typical ground.
        /// </summary>
        private float A(float alpha, float colour)
        {
            if (alpha <= 0f || QualitySettings.activeColorSpace != ColorSpace.Linear) return alpha;
            float x = _ground, t = x + alpha * (colour - x);
            float d = Mathf.GammaToLinearSpace(colour) - Mathf.GammaToLinearSpace(x);
            return Mathf.Abs(d) < 1e-4f ? alpha : Mathf.Clamp01((Mathf.GammaToLinearSpace(t) - Mathf.GammaToLinearSpace(x)) / d);
        }

        private static float Lum(Rgb c) { return (c.R + c.G + c.B) / 3f; }

        private static int Count(WeatherStyle s) { return s.Clouds == null ? 0 : s.Clouds.Count; }

        private static float Wrap(float v, float lo, float hi) { float span = hi - lo; return lo + ((v - lo) % span + span) % span; }

        private static Color Blend(Rgb a, Rgb b, float w, float alpha) { return new Color(a.R + (b.R - a.R) * w, a.G + (b.G - a.G) * w, a.B + (b.B - a.B) * w, alpha); }

        private bool _frozen;

        /// <summary>Rain streaks and splashes: the fading-in weather's, its strength eased; frozen in place with reduced motion.</summary>
        private void Rain(WeatherFrame f, float pt, float wd, float h)
        {
            StreaksDef s = f.To.Streaks ?? f.From.Streaks;
            float strength = f.To.Streaks != null ? f.Weight : 1 - f.Weight;
            int count = s == null ? 0 : _look.Streaks(f.To.Streaks != null ? f.To : f.From);
            Transform t = _rain.transform;
            if (count == 0 || strength < 0.01f)
            {
                if (_rain.isPlaying || _rain.isPaused) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_splash.isPlaying || _splash.isPaused) _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _frozen = false;
                return;
            }
            float slant = s.Slant + f.Gust, speed = s.Speed * pt, life = (h * 1.2f) / speed;
            ParticleSystem.MainModule main = _rain.main;
            main.startLifetime = life;
            main.startSize = s.Width * pt;
            main.startColor = new Color(f.Light < 1 ? 0.79f : 0.9f, f.Light < 1 ? 0.83f : 0.93f, f.Light < 1 ? 0.9f : 0.96f,
                s.Alpha * strength * (f.Moving ? 1f : _def.ReducedStreakAlpha) * (f.Light < 1 ? 0.7f : 1f)); // a thin line reads by its edge: no area conversion
            ParticleSystem.EmissionModule em = _rain.emission;
            em.rateOverTime = count / life;
            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.scale = new Vector3(wd * 1.4f, 0.01f, 0.01f);
            shape.position = new Vector3(-slant * h * 0.6f, h * 0.6f, 0);
            ParticleSystem.VelocityOverLifetimeModule v = _rain.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(speed * slant * 0.9f, speed * slant * 1.1f);
            v.y = new ParticleSystem.MinMaxCurve(-speed * 1.2f, -speed * 0.8f);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var renderer = _rain.GetComponent<ParticleSystemRenderer>();
            renderer.lengthScale = s.Length / s.Width;
            renderer.velocityScale = 0;

            float splashes = f.To.SplashesPerSecond * f.Weight + f.From.SplashesPerSecond * (1 - f.Weight);
            ParticleSystem.MainModule sm = _splash.main;
            sm.startLifetime = 0.25f;
            sm.startSize = 9 * pt;
            sm.startColor = new Color(0.95f, 0.97f, 0.98f, 0.5f * (f.Light < 1 ? 0.7f : 1f));
            ParticleSystem.EmissionModule se = _splash.emission;
            se.rateOverTime = f.Moving ? splashes : 0;
            ParticleSystem.ShapeModule ss = _splash.shape;
            ss.scale = new Vector3(wd, h * 0.85f, 0.01f);
            ss.position = new Vector3(0, -h * 0.075f, 0);

            if (f.Moving)
            {
                if (_frozen || !_rain.isPlaying) { _rain.Play(); _frozen = false; }
                if (!_splash.isPlaying && splashes > 0) _splash.Play();
            }
            else if (!_frozen)
            {
                // Reduced motion: the rain stands still, lighter; no splashes (spec 008 §3.4).
                if (!_rain.isPlaying && !_rain.isPaused) _rain.Simulate(life, true, true);
                _rain.Pause();
                _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _frozen = true;
            }
        }

        /// <summary>A soft rain loop in rain and heavier weather, thunder after a glow; both behind the Sound switch (ADR 0083 §14).</summary>
        private void Sound(WeatherFrame f)
        {
            bool on = _sound();
            float Volume(WeatherStyle s) { return s.Streaks == null ? 0f : s.Streaks.Count >= 400 ? 0.6f : s.Streaks.Count >= 300 ? 0.55f : 0.35f; }
            float target = on ? Volume(f.From) * (1 - f.Weight) + Volume(f.To) * f.Weight : 0f;
            _rainSound.volume = Mathf.MoveTowards(_rainSound.volume, target, Time.unscaledDeltaTime * 0.5f); // fades in and out over a second or two
            if (_rainSound.volume > 0.001f && !_rainSound.isPlaying && _rainSound.clip != null) _rainSound.Play();
            if (_rainSound.volume <= 0.001f && _rainSound.isPlaying) _rainSound.Stop();
            if (f.Thunder && on && _thunder.clip != null) { _thunder.pitch = 0.85f + UnityEngine.Random.value * 0.3f; _thunder.Play(); }
        }

        /// <summary>The weather showing now, for the developer panel's label.</summary>
        public string Showing { get { return DevOverride ?? _spell(); } }
    }
}
