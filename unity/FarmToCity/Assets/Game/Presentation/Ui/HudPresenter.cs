using System;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Sim.View.Text;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Ui
{
    /// <summary>
    /// The minimal HUD of the first playable (spec 008 §3.3, ADR 0038): the time strip (hold it for the developer panel), cash with
    /// a count-up, the store fill (tap for the Store sheet) and Collect all in the bottom-left thumb corner. It draws a
    /// <see cref="HudVm"/>; it knows no game rules.
    /// </summary>
    public sealed class HudPresenter
    {
        private readonly Tweens _tweens;
        private readonly StringTable _strings;
        private readonly Formatter _fmt;
        private readonly VisualElement _layer;
        private readonly Label _date, _clock, _weather, _cash, _store;
        private readonly VisualElement _weatherIcon, _storeFill, _timeCard, _cashPill, _storePill, _collectHolder;
        private Button _collect;
        private readonly VisualElement _people, _badge;
        private readonly Label _badgeText;
        private int _badgeShown = -1;
        private string _weatherId;
        private long _shownCash = long.MinValue, _targetCash;
        private int _readyShown = -1;
        private bool _collectEnabled;

        public Action OnStore { get; set; }
        public Action OnPeople { get; set; }
        public Action OnSettings { get; set; }
        public Action OnCollect { get; set; }

        /// <summary>The developer panel's gesture: the time strip held for three seconds (development builds only).</summary>
        public Action OnTimeHeld { get; set; }

        public VisualElement CashTarget { get { return _cashPill; } }
        public VisualElement StoreTarget { get { return _storePill; } }
        public VisualElement PeopleTarget { get { return _people; } }

        public HudPresenter(VisualElement root, Tweens tweens, StringTable strings, Formatter fmt)
        {
            _tweens = tweens;
            _strings = strings;
            _fmt = fmt;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0; _layer.style.right = 0; _layer.style.top = 0; _layer.style.bottom = 0;
            root.Add(_layer);

            // Top-left: the time strip.
            _timeCard = UiKit.Pill();
            _timeCard.style.position = Position.Absolute;
            _timeCard.style.left = 28; _timeCard.style.top = 24;
            _timeCard.style.flexDirection = FlexDirection.Column;
            _timeCard.style.alignItems = Align.FlexStart;
            _date = UiKit.Text("", 30, UiKit.Cream, UiKit.Body);
            _timeCard.Add(_date);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            _weatherIcon = UiKit.Icon(IconBank.Weather("sun"), 72);
            _clock = UiKit.Text("", 72, UiKit.Gold, UiKit.Heading, outline: true);
            _weather = UiKit.Text("", 34, UiKit.Cream, UiKit.Heading);
            _weather.style.marginLeft = 16;
            row.Add(_weatherIcon);
            row.Add(_clock);
            row.Add(_weather);
            _timeCard.Add(row);
            _layer.Add(_timeCard);
            IVisualElementScheduledItem hold = null;
            _timeCard.RegisterCallback<PointerDownEvent>(_ => hold = _timeCard.schedule.Execute(() => { if (OnTimeHeld != null) OnTimeHeld(); }).StartingIn(3000));
            _timeCard.RegisterCallback<PointerUpEvent>(_ => { if (hold != null) hold.Pause(); });
            _timeCard.RegisterCallback<PointerLeaveEvent>(_ => { if (hold != null) hold.Pause(); });

            // Top-right: cash, and the store under it.
            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.position = Position.Absolute;
            right.style.right = 28; right.style.top = 24;
            right.style.alignItems = Align.FlexEnd;
            _cashPill = UiKit.Pill();
            _cashPill.Add(UiKit.Icon(IconBank.Coin, 64));
            _cash = UiKit.Text("", 68, UiKit.Gold, UiKit.Heading, outline: true);
            _cash.style.marginLeft = 12;
            _cashPill.Add(_cash);
            right.Add(_cashPill);

            _storePill = UiKit.Pill();
            _storePill.style.marginTop = 12;
            _storePill.style.flexDirection = FlexDirection.Row;
            _storePill.Add(UiKit.Icon(IconBank.Sack, 56));
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.marginLeft = 12;
            _store = UiKit.Text("", 40, UiKit.Cream, UiKit.Heading);
            col.Add(_store);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 14; bar.style.width = 190; bar.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            UiKit.Radius(bar, 7);
            _storeFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _storeFill.style.height = Length.Percent(100);
            UiKit.Radius(_storeFill, 7);
            bar.Add(_storeFill);
            col.Add(bar);
            _storePill.Add(col);
            _storePill.RegisterCallback<ClickEvent>(_ => { if (OnStore != null) OnStore(); });
            right.Add(_storePill);

            // Under the store: Settings (ADR 0041 §4), a round button with a gear until the Menu arrives in Sprint 7.
            VisualElement gear = RoundButton(110, IconBank.Gear, 70);
            gear.name = "Settings";
            gear.style.marginTop = 14;
            gear.RegisterCallback<ClickEvent>(_ => { if (OnSettings != null) OnSettings(); });
            right.Add(gear);
            _layer.Add(right);

            // Bottom-left: Collect all.
            _collectHolder = new VisualElement { pickingMode = PickingMode.Ignore };
            _collectHolder.style.position = Position.Absolute;
            _collectHolder.style.left = 28; _collectHolder.style.bottom = 28;
            _layer.Add(_collectHolder);

            // Bottom-right: People (ADR 0039 §8), a round wood button with a villager and a badge for waiting applicants.
            _people = new VisualElement();
            _people.style.position = Position.Absolute;
            _people.style.right = 28; _people.style.bottom = 28;
            _people.style.width = 150; _people.style.height = 150;
            _people.style.backgroundColor = UiKit.Sand;
            UiKit.Radius(_people, 75);
            UiKit.Border(_people, 5, UiKit.SandDark);
            _people.style.borderBottomWidth = 10;
            _people.style.alignItems = Align.Center;
            _people.style.justifyContent = Justify.Center;
            Sprite face = SpriteBank.Rendered("person_hand_m_front");
            if (face != null)
            {
                VisualElement pic = UiKit.Icon(face, 104);
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                _people.Add(pic);
            }
            Label word = UiKit.Text(strings.Get("hud.people"), 26, UiKit.Ink, UiKit.Heading);
            word.style.position = Position.Absolute;
            word.style.bottom = -6;
            _people.Add(word);
            _badge = new VisualElement { pickingMode = PickingMode.Ignore };
            _badge.style.position = Position.Absolute;
            _badge.style.right = -6; _badge.style.top = -6;
            _badge.style.width = 54; _badge.style.height = 54;
            _badge.style.backgroundColor = UiKit.Red;
            UiKit.Radius(_badge, 27);
            UiKit.Border(_badge, 4, UiKit.RedDark);
            _badge.style.alignItems = Align.Center;
            _badge.style.justifyContent = Justify.Center;
            _badgeText = UiKit.Text("", 32, Color.white, UiKit.Heading);
            _badge.Add(_badgeText);
            _badge.style.display = DisplayStyle.None;
            _people.Add(_badge);
            _people.RegisterCallback<PointerDownEvent>(_ => _people.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f)));
            _people.RegisterCallback<PointerUpEvent>(_ => _people.style.scale = new Scale(Vector3.one));
            _people.RegisterCallback<PointerLeaveEvent>(_ => _people.style.scale = new Scale(Vector3.one));
            _people.RegisterCallback<ClickEvent>(_ => { if (OnPeople != null) OnPeople(); });
            _layer.Add(_people);

            // HUD figures never wrap: on a phone's fractional UI scale the measured width can round a hair short, and the last
            // digit then dropped onto a hidden second line ("12:4", "4,38").
            foreach (Label l in new[] { _date, _clock, _weather, _cash, _store })
            {
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.flexShrink = 0;
            }
        }

        private static VisualElement RoundButton(float size, Sprite icon, float iconSize)
        {
            var b = new VisualElement();
            b.style.width = size; b.style.height = size;
            b.style.backgroundColor = UiKit.Sand;
            UiKit.Radius(b, size / 2);
            UiKit.Border(b, 5, UiKit.SandDark);
            b.style.borderBottomWidth = 9;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            VisualElement pic = UiKit.Icon(icon, iconSize);
            pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            b.Add(pic);
            b.RegisterCallback<PointerDownEvent>(_ => b.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f)));
            b.RegisterCallback<PointerUpEvent>(_ => b.style.scale = new Scale(Vector3.one));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.scale = new Scale(Vector3.one));
            return b;
        }

        /// <summary>The element's centre in screen pixels (y up), for flying icons to land on it.</summary>
        public Vector2 ScreenCentre(VisualElement e)
        {
            Rect r = e.worldBound;
            Vector2 centrePanel = r.center;
            float k = e.panel.scaledPixelsPerPoint;
            return new Vector2(centrePanel.x * k, Screen.height - centrePanel.y * k);
        }

        public void Refresh(HudVm hud, bool animate)
        {
            _date.text = hud.DateText;
            _clock.text = hud.ClockText;
            _weather.text = hud.WeatherText;
            if (_weatherId != hud.WeatherId)
            {
                _weatherId = hud.WeatherId;
                _weatherIcon.style.backgroundImage = new StyleBackground(IconBank.Weather(hud.WeatherId));
            }
            _store.text = _strings.Get("hud.store") + " " + hud.StoreText;
            _storeFill.style.width = Length.Percent(hud.StorePercent);
            _storeFill.style.backgroundColor = hud.StorePercent >= 90 ? UiKit.Red : hud.StorePercent >= 70 ? UiKit.Sand : UiKit.Green;

            _targetCash = hud.Cash;
            if (_shownCash == long.MinValue || !animate) { _shownCash = hud.Cash; _cash.text = hud.CashText; }
            else if (_shownCash != hud.Cash)
            {
                long from = _shownCash, to = hud.Cash;
                _tweens.Run(_cash, 0.8f, Ease.OutCubic, k =>
                {
                    _shownCash = from + (long)((to - from) * (double)k);
                    _cash.text = _fmt.ShortCoins(_shownCash);
                }, () => { _shownCash = to; _cash.text = _fmt.ShortCoins(to); });
            }

            _people.style.display = hud.HasPeople ? DisplayStyle.Flex : DisplayStyle.None;
            if (_badgeShown != hud.PeopleBadge)
            {
                bool popped = hud.PeopleBadge > _badgeShown && _badgeShown >= 0 && animate;
                _badgeShown = hud.PeopleBadge;
                _badgeText.text = hud.PeopleBadge.ToString();
                _badge.style.display = hud.PeopleBadge > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (popped) Bounce(_people);
            }

            bool enabled = hud.ReadyCount > 0;
            if (_readyShown != hud.ReadyCount || _collectEnabled != enabled || _collect == null)
            {
                _readyShown = hud.ReadyCount;
                _collectEnabled = enabled;
                _collectHolder.Clear();
                _collect = UiKit.MakeButton(hud.CollectText, enabled ? ActionLook.Primary : ActionLook.Secondary, () => { if (OnCollect != null) OnCollect(); }, enabled: enabled);
                _collect.style.minWidth = 380;
                _collect.style.marginTop = 0;
                _collectHolder.Add(_collect);
                _collectHolder.pickingMode = PickingMode.Position;
            }
        }

        /// <summary>The cash pill bounces when money arrives (spec 008 §3.12).</summary>
        public void BounceCash() { Bounce(_cashPill); }

        public void BounceStore() { Bounce(_storePill); }

        private void Bounce(VisualElement e)
        {
            _tweens.Run(e, 0.3f, Ease.OutBack, k => { float s = 1f + 0.12f * Mathf.Sin(k * Mathf.PI); e.style.scale = new Scale(new Vector3(s, s, 1f)); }, () => e.style.scale = new Scale(Vector3.one));
        }

        /// <summary>The HUD hides while a full-screen tool (the developer panel) is up.</summary>
        public void SetVisible(bool visible) { _layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
    }
}
