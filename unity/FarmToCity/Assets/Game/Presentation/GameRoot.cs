using System;
using Game.Services;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Time;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// The composition root (ADR 0017), like a Spring Boot application class: it loads content, builds the
    /// simulation and the services, and wires the UI. Nothing is placed in the scene by hand: Unity calls
    /// <see cref="Boot"/> after the bootstrap scene loads, and everything else is created here in code.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public Simulation Sim { get; private set; }
        public IEntitlementService Entitlements { get; } = new AlwaysUnlockedEntitlementService();

        private long _lastSyncWall;
        private Label _date, _time, _weather;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        private static long WallNow() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        private void Awake()
        {
            WeatherSet weather = ContentLoader.LoadWeather(ContentSource.Read("weather.json"));
            GameDefs game = ContentLoader.LoadGame(ContentSource.Read("land.json"), ContentSource.Read("map.json"),
                ContentSource.Read("buildings.json"), ContentSource.Read("start.json"), ContentSource.Read("finance.json"), weather);
            var config = new SimConfig(ContentLoader.LoadClock(ContentSource.Read("clock.json")), weather, game: game);

            // ponytail: no saves yet (spec 007, Sprint 6), so every launch is a new game seeded from the clock.
            _lastSyncWall = WallNow();
            Sim = Simulation.NewGame(config, new NewGame((ulong)DateTime.UtcNow.Ticks, _lastSyncWall,
                (long)DateTime.Now.TimeOfDay.TotalSeconds));
            BuildHud();
            Refresh();
        }

        private void Update()
        {
            // Sync once per wall-clock second: the sim's time unit is a whole second (spec 001 §3.4).
            long now = WallNow();
            if (now == _lastSyncWall) return;
            _lastSyncWall = now;
            Sim.Execute(new Sync(now));
            Sim.DrainEvents(); // the hello build shows state only; later HUDs react to the events
            Refresh();
        }

        private void BuildHud()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DefaultTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            VisualElement root = document.rootVisualElement;
            root.style.alignItems = Align.Center;
            // An explicit built-in font, inherited by every label, so text never depends on the theme's default.
            root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            var card = new VisualElement();
            card.style.marginTop = 48;
            card.style.paddingLeft = card.style.paddingRight = 48;
            card.style.paddingTop = card.style.paddingBottom = 24;
            card.style.backgroundColor = new Color(0.98f, 0.95f, 0.88f, 0.92f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 24;
            card.style.alignItems = Align.Center;
            root.Add(card);

            _date = AddLabel(card, 44);
            _time = AddLabel(card, 96);
            _weather = AddLabel(card, 44);
        }

        private static Label AddLabel(VisualElement parent, int size)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.color = new Color(0.27f, 0.2f, 0.12f);
            parent.Add(label);
            return label;
        }

        private void Refresh()
        {
            CalendarDate d = Calendar.From(Sim.State.Clock.SimTime, Sim.Clock);
            // ponytail: plain English until the string tables and the locale formatter arrive (spec 008).
            _date.text = "Year " + d.Year + " · " + d.Season + " · Day " + d.DayOfSeason;
            _time.text = d.Hour.ToString("00") + ":" + d.Minute.ToString("00");
            _weather.text = Pretty(Sim.CurrentWeather.Id);
        }

        private static string Pretty(string id)
        {
            string s = id.Replace('_', ' ');
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
