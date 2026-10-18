using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Phase0
{
    /// <summary>
    /// The CC0 art style check (US-024): the 8 × 6 homestead built from Kenney's CC0 isometric tiles, tinted for the
    /// valley and each season, with a code-driven motion sample (a tap bounce, a sack flying to the store, a coin
    /// count-up and an eased panel; GDD Art, audio and feel). Throwaway: Phase 0 only.
    /// </summary>
    public sealed class StyleCheck : MonoBehaviour
    {
        private const int W = 8, H = 6;

        // The valley's warm light, then each season's tint on top (Spring, Summer, Monsoon, Winter).
        private static readonly Color Valley = new Color(1.00f, 0.97f, 0.88f);
        private static readonly Color[] SeasonTint =
        {
            new Color(1.00f, 1.00f, 1.00f), new Color(1.06f, 0.94f, 0.72f), new Color(0.82f, 1.00f, 0.84f), new Color(0.93f, 0.95f, 1.00f),
        };
        private static readonly string[] SeasonName = { "Spring", "Summer", "Monsoon", "Winter" };

        private Camera _camera;
        private readonly System.Collections.Generic.List<SpriteRenderer> _ground = new System.Collections.Generic.List<SpriteRenderer>();
        private Transform _crop, _sack, _store;
        private Vector3 _sackFrom;
        private Label _coins, _season;
        private VisualElement _panel;
        private float _t0;
        private int _shownCoins, _fromCoins, _toCoins = 5796;

        public string Status { get; private set; } = "";

        public static StyleCheck Start() { return new GameObject("StyleCheck").AddComponent<StyleCheck>(); }

        private static Sprite Load(string path, float pixelsPerUnit, Vector2 pivot)
        {
            var tex = Resources.Load<Texture2D>(path);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit);
        }

        private void Awake()
        {
            _camera = Camera.main;
            _camera.orthographic = true;
            _camera.backgroundColor = new Color(0.55f, 0.75f, 0.85f);

            // Landscape tiles are 132 px wide; the top face's centre sits 33 px below the top edge.
            const string L = "Art/Kenney/IsometricLandscape/landscapeTiles_";
            Sprite grass = Load(L + "067", 132, new Vector2(0.5f, 66f / 99f));
            Sprite soil = Load(L + "083", 132, new Vector2(0.5f, 66f / 99f));
            Sprite water = Load(L + "066", 132, new Vector2(0.5f, 50f / 83f));
            Sprite sand = Load(L + "059", 132, new Vector2(0.5f, 66f / 99f));
            const string F = "Art/Kenney/IsometricFarm/";
            Vector2 farmPivot = new Vector2(0.5f, 0.25f); // the miniature pack draws small on a 256 × 512 canvas: shown at twice its scale
            Sprite young = Load(F + "cornYoung_N", 128, farmPivot), grown = Load(F + "corn_N", 128, farmPivot);
            Sprite hay = Load(F + "hayBales_N", 128, farmPivot), sackSprite = Load(F + "sack_N", 128, farmPivot), fence = Load(F + "fenceLow_N", 128, farmPivot);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // Row 0 is the river, row 1 its sandy bank; the inherited acres are soil; the yard is grass.
                    Sprite s = y == 0 ? water : y == 1 ? sand : (x >= 2 && x <= 4 && y >= 2) ? soil : grass;
                    _ground.Add(Place(s, x, y, 0));
                }
            // Crops on the two well acres, at two stages; hay and sacks in the yard; a fence along it.
            Place(young, 2, 3, 1);
            _crop = Place(grown, 3, 3, 1).transform;
            Place(young, 4, 4, 1);
            Place(hay, 6, 4, 1);
            _store = Place(sackSprite, 6, 3, 1).transform;
            for (int y = 2; y < H; y++) Place(fence, 5, y, 1);
            _sack = Place(sackSprite, 3, 3, 2).transform;
            _sackFrom = _sack.position;

            Vector3 c = Iso(W / 2f - 0.5f, H / 2f - 0.5f);
            _camera.transform.position = new Vector3(c.x, c.y, -10);
            _camera.orthographicSize = 2.1f;
            BuildUi();
            _t0 = Time.realtimeSinceStartup;
        }

        private static Vector3 Iso(float x, float y) { return new Vector3((x - y) * 0.5f, (x + y) * 0.25f, 0); }

        private SpriteRenderer Place(Sprite s, int x, int y, int layer)
        {
            var go = new GameObject(s.name);
            go.transform.SetParent(transform);
            go.transform.position = Iso(x, y);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingOrder = (W + H - x - y) * 10 + layer; // nearer tiles (smaller x + y) draw on top
            return r;
        }

        private void BuildUi()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DefaultTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.sortingOrder = 10;
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            VisualElement root = doc.rootVisualElement;
            root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            _coins = new Label();
            _coins.style.position = Position.Absolute;
            _coins.style.right = 40; _coins.style.top = 32;
            _coins.style.fontSize = 56;
            _coins.style.color = new Color(0.98f, 0.85f, 0.3f);
            _coins.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_coins);

            _season = new Label();
            _season.style.position = Position.Absolute;
            _season.style.left = 40; _season.style.top = 32;
            _season.style.fontSize = 48;
            _season.style.color = new Color(0.2f, 0.15f, 0.1f);
            root.Add(_season);

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = 520; _panel.style.right = 520; _panel.style.bottom = 0; _panel.style.height = 150;
            _panel.style.backgroundColor = new Color(0.98f, 0.95f, 0.88f, 0.96f);
            _panel.style.borderTopLeftRadius = _panel.style.borderTopRightRadius = 32;
            var text = new Label("Sold at the mandi: 1,000 kg potato\nNet 5,796 after cart hire");
            text.style.fontSize = 40;
            text.style.color = new Color(0.27f, 0.2f, 0.12f);
            text.style.marginLeft = text.style.marginTop = 32;
            _panel.Add(text);
            root.Add(_panel);
        }

        // ---- Easing (code-driven motion: no animation assets) ----

        private static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        private static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        private static float OutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }

        private void Update()
        {
            float t = Time.realtimeSinceStartup - _t0;

            // The season changes every 4 s: the ground takes the valley tint × the season's.
            int season = (int)(t / 4f) % 4;
            Color tint = Valley * SeasonTint[season];
            tint.a = 1;
            foreach (SpriteRenderer r in _ground) r.color = tint;
            _season.text = SeasonName[season];

            // A 4-second loop: tap bounce (0–0.35 s), the sack flies to the store (0.4–1.2 s), coins count up
            // (1.2–2.2 s), the panel eases in (2.2–2.6 s) and out (3.4–3.8 s).
            float k = t % 4f;
            float bounce = k < 0.35f ? (k < 0.12f ? 1 + 0.18f * OutCubic(k / 0.12f) : 1 + 0.18f * (1 - OutBack((k - 0.12f) / 0.23f))) : 1;
            _crop.localScale = new Vector3(bounce, bounce, 1);

            float f = InOutCubic((k - 0.4f) / 0.8f);
            Vector3 p = Vector3.Lerp(_sackFrom, _store.position, f);
            p.y += Mathf.Sin(f * Mathf.PI) * 0.8f; // an arc
            _sack.position = p;
            _sack.gameObject.SetActive(k >= 0.4f && k < 1.2f);

            if (k < 1.2f) { _fromCoins = _shownCoins; }
            float c = OutCubic((k - 1.2f) / 1.0f);
            int target = _fromCoins + (k >= 1.2f ? 5796 : 0);
            _shownCoins = k < 1.2f ? _shownCoins : Mathf.RoundToInt(Mathf.Lerp(_fromCoins, target, c));
            if (k >= 3.99f) _fromCoins = _shownCoins;
            _coins.text = _shownCoins.ToString("N0") + " coins";

            float show = k < 2.2f ? 0 : k < 2.6f ? OutCubic((k - 2.2f) / 0.4f) : k < 3.4f ? 1 : 1 - OutCubic((k - 3.4f) / 0.4f);
            _panel.style.translate = new Translate(0, Length.Percent((1 - show) * 100f));
            Status = "Style check: " + SeasonName[season];
        }
    }
}
