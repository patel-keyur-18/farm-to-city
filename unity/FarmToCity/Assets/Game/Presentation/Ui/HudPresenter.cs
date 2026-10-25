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
    /// The HUD (spec 008 §3.3, ADR 0043 §4): the time strip with the weather run (tap: the forecast; hold 3 s: the developer panel),
    /// cash (tap: Money and loans; hold: its Why?), the store fill (tap: the Store; hold: its Why?), Collect all and the alerts
    /// button in the bottom-left thumb corner, Menu and People bottom-right. It draws a <see cref="HudVm"/>; it knows no game rules.
    /// </summary>
    public sealed class HudPresenter
    {
        private readonly Tweens _tweens;
        private readonly StringTable _strings;
        private readonly Formatter _fmt;
        private readonly VisualElement _layer;
        private readonly Label _date, _clock, _weather, _cash, _store, _forecast, _alertCount;
        private readonly VisualElement _alerts;
        private int _alertsShown = -1;
        private bool _urgentShown, _held;
        private readonly VisualElement _weatherIcon, _storeFill, _timeCard, _cashPill, _storePill, _collectHolder;
        private Button _collect;
        private readonly VisualElement _people, _badge, _orders, _ordersBadge, _townPill;
        private readonly Label _population, _happiness;
        private readonly Label _badgeText, _ordersBadgeText;
        private int _badgeShown = -1, _ordersShown = -1;
        private string _weatherId;
        private long _shownCash = long.MinValue, _targetCash;
        private int _readyShown = -1;
        private bool _collectEnabled;

        public Action OnStore { get; set; }
        public Action OnPeople { get; set; }

        /// <summary>The Your people pill: the Town tab (spec 014 §7).</summary>
        public Action OnTown { get; set; }
        public Action OnCollect { get; set; }
        public Action OnForecast { get; set; }
        public Action OnMoney { get; set; }
        public Action OnMenu { get; set; }
        public Action OnAlerts { get; set; }
        public Action OnOrders { get; set; }

        /// <summary>An urgent alert has just appeared (the bell, F08.5).</summary>
        public Action OnUrgent { get; set; }

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
            _forecast = UiKit.Text("", 28, UiKit.Cream, UiKit.Body);
            _timeCard.Add(_forecast);
            _layer.Add(_timeCard);
            // A tap opens the forecast; holding for 3 seconds opens the developer panel (development builds), and then the tap is not a tap.
            TapAndHold(_timeCard, () => { if (OnForecast != null) OnForecast(); }, () => { if (OnTimeHeld != null) OnTimeHeld(); }, 3000);

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
            // Plain taps: a Why? opens only from its ? (ADR 0044 §3); the store's and the cash's are on their sheets.
            _storePill.RegisterCallback<ClickEvent>(_ => { if (OnStore != null) OnStore(); });
            _cashPill.RegisterCallback<ClickEvent>(_ => { if (OnMoney != null) OnMoney(); });
            right.Add(_storePill);

            // Your people (spec 014 §7): how many live on your land and how happy the town is.
            _townPill = UiKit.Pill();
            _townPill.name = "Town";
            _townPill.style.marginTop = 12;
            _townPill.style.flexDirection = FlexDirection.Row;
            _townPill.style.alignItems = Align.Center;
            _townPill.Add(UiKit.Icon(SpriteBank.Rendered("icon_population"), 56));
            _population = UiKit.Text("", 40, UiKit.Cream, UiKit.Heading);
            _population.style.marginLeft = 8; _population.style.marginRight = 18;
            _population.style.whiteSpace = WhiteSpace.NoWrap; // "71" once wrapped to "7" over "1"
            _townPill.Add(_population);
            _townPill.Add(UiKit.Icon(SpriteBank.Rendered("icon_happiness"), 56));
            _happiness = UiKit.Text("", 40, UiKit.Cream, UiKit.Heading);
            _happiness.style.marginLeft = 8;
            _happiness.style.whiteSpace = WhiteSpace.NoWrap;
            _townPill.Add(_happiness);
            _townPill.RegisterCallback<ClickEvent>(_ => { if (OnTown != null) OnTown(); });
            right.Add(_townPill);
            _layer.Add(right);

            // Above Collect all: the alerts button, shown only when something needs the player (ADR 0043 §4).
            _alerts = RoundButton(116, IconBank.Bell, 76);
            _alerts.name = "Alerts";
            _alerts.style.position = Position.Absolute;
            _alerts.style.left = 28; _alerts.style.bottom = 150;
            var alertBadge = new VisualElement { pickingMode = PickingMode.Ignore };
            alertBadge.style.position = Position.Absolute;
            alertBadge.style.right = -8; alertBadge.style.top = -8;
            alertBadge.style.width = 50; alertBadge.style.height = 50;
            alertBadge.style.backgroundColor = UiKit.WoodBar;
            UiKit.Radius(alertBadge, 25);
            UiKit.Border(alertBadge, 3, UiKit.WoodDark);
            alertBadge.style.alignItems = Align.Center;
            alertBadge.style.justifyContent = Justify.Center;
            _alertCount = UiKit.Text("", 30, Color.white, UiKit.Heading);
            alertBadge.Add(_alertCount);
            _alerts.Add(alertBadge);
            _alerts.RegisterCallback<ClickEvent>(_ => { if (OnAlerts != null) OnAlerts(); });
            _alerts.style.display = DisplayStyle.None;
            _layer.Add(_alerts);

            // Bottom-right: the Menu (ADR 0043 §4).
            VisualElement menu = RoundButton(150, IconBank.Ledger, 84);
            menu.name = "Menu";
            menu.style.position = Position.Absolute;
            menu.style.right = 28; menu.style.bottom = 28;
            menu[0].style.marginTop = -20; // room for the word under the book
            Label menuWord = UiKit.Text(strings.Get("hud.menu"), 26, UiKit.Ink, UiKit.Heading);
            menuWord.style.position = Position.Absolute;
            menuWord.style.bottom = -6;
            menu.Add(menuWord);
            menu.RegisterCallback<ClickEvent>(_ => { if (OnMenu != null) OnMenu(); });
            _layer.Add(menu);

            // Bottom-left: Collect all.
            _collectHolder = new VisualElement { pickingMode = PickingMode.Ignore };
            _collectHolder.style.position = Position.Absolute;
            _collectHolder.style.left = 28; _collectHolder.style.bottom = 28;
            _layer.Add(_collectHolder);

            // Beside the Menu: People (ADR 0039 §8), a shortcut with a villager and a badge for waiting applicants.
            _people = new VisualElement();
            _people.name = "People";
            _people.style.position = Position.Absolute;
            _people.style.right = 198; _people.style.bottom = 28;
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

            // Beside People: the supermarket's orders (spec 011 §7), a paper shop bag with a badge for orders still open today.
            _orders = RoundButton(150, IconBank.Orders, 96);
            _orders.name = "Orders";
            _orders.style.position = Position.Absolute;
            _orders.style.right = 368; _orders.style.bottom = 28;
            _orders[0].style.marginTop = -18;
            Label ordersWord = UiKit.Text(strings.Get("hud.orders"), 26, UiKit.Ink, UiKit.Heading);
            ordersWord.style.position = Position.Absolute;
            ordersWord.style.bottom = -6;
            _orders.Add(ordersWord);
            _ordersBadge = new VisualElement { pickingMode = PickingMode.Ignore };
            _ordersBadge.style.position = Position.Absolute;
            _ordersBadge.style.right = -6; _ordersBadge.style.top = -6;
            _ordersBadge.style.width = 54; _ordersBadge.style.height = 54;
            _ordersBadge.style.backgroundColor = UiKit.Red;
            UiKit.Radius(_ordersBadge, 27);
            UiKit.Border(_ordersBadge, 4, UiKit.RedDark);
            _ordersBadge.style.alignItems = Align.Center;
            _ordersBadge.style.justifyContent = Justify.Center;
            _ordersBadgeText = UiKit.Text("", 32, Color.white, UiKit.Heading);
            _ordersBadge.Add(_ordersBadgeText);
            _ordersBadge.style.display = DisplayStyle.None;
            _orders.Add(_ordersBadge);
            _orders.RegisterCallback<ClickEvent>(_ => { if (OnOrders != null) OnOrders(); });
            _orders.style.display = DisplayStyle.None;
            _layer.Add(_orders);

            // HUD figures never wrap: on a phone's fractional UI scale the measured width can round a hair short, and the last
            // digit then dropped onto a hidden second line ("12:4", "4,38").
            foreach (Label l in new[] { _date, _clock, _weather, _cash, _store, _forecast })
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
            if (icon != null)
            {
                VisualElement pic = UiKit.Icon(icon, iconSize);
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                b.Add(pic);
            }
            b.RegisterCallback<PointerDownEvent>(_ => b.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f)));
            b.RegisterCallback<PointerUpEvent>(_ => b.style.scale = new Scale(Vector3.one));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.scale = new Scale(Vector3.one));
            return b;
        }

        /// <summary>A tap runs <paramref name="tap"/>; holding for <paramref name="ms"/> runs <paramref name="hold"/> instead.</summary>
        private void TapAndHold(VisualElement e, Action tap, Action hold, long ms)
        {
            // The hold fires only while the finger is still down on this element: any up, cancel, leave or click ends the press.
            bool down = false;
            IVisualElementScheduledItem timer = null;
            Action release = () => { down = false; if (timer != null) timer.Pause(); };
            e.RegisterCallback<PointerDownEvent>(_ =>
            {
                down = true;
                _held = false;
                if (timer != null) timer.Pause();
                timer = e.schedule.Execute(() => { if (!down) return; down = false; _held = true; hold(); }).StartingIn(ms);
            });
            e.RegisterCallback<PointerUpEvent>(_ => release());
            e.RegisterCallback<PointerCancelEvent>(_ => release());
            e.RegisterCallback<PointerLeaveEvent>(_ => release());
            e.RegisterCallback<ClickEvent>(_ =>
            {
                release();
                if (_held) { _held = false; return; }
                tap();
            });
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
            _forecast.text = hud.ForecastText;
            _store.text = _strings.Get("hud.store") + " " + hud.StoreText;
            if (_alertsShown != hud.AlertCount || _urgentShown != hud.AlertUrgent)
            {
                bool newUrgent = hud.AlertUrgent && !_urgentShown && _alertsShown >= 0;
                _alertsShown = hud.AlertCount;
                _urgentShown = hud.AlertUrgent;
                _alertCount.text = hud.AlertCount.ToString();
                _alerts.style.display = hud.AlertCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _alerts.style.backgroundColor = hud.AlertUrgent ? UiKit.Red : UiKit.Sand;
                UiKit.Border(_alerts, 5, hud.AlertUrgent ? UiKit.RedDark : UiKit.SandDark);
                _alerts.style.borderBottomWidth = 9;
                if (newUrgent) { Bounce(_alerts); if (OnUrgent != null) OnUrgent(); }
            }
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
            _townPill.style.display = hud.HasTown ? DisplayStyle.Flex : DisplayStyle.None;
            _population.text = hud.Population.ToString();
            _happiness.text = hud.Happiness.ToString();
            if (_badgeShown != hud.PeopleBadge)
            {
                bool popped = hud.PeopleBadge > _badgeShown && _badgeShown >= 0 && animate;
                _badgeShown = hud.PeopleBadge;
                _badgeText.text = hud.PeopleBadge.ToString();
                _badge.style.display = hud.PeopleBadge > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (popped) Bounce(_people);
            }

            _orders.style.display = hud.HasOrders ? DisplayStyle.Flex : DisplayStyle.None;
            if (_ordersShown != hud.OrdersBadge)
            {
                bool popped = hud.OrdersBadge > _ordersShown && _ordersShown >= 0 && animate;
                _ordersShown = hud.OrdersBadge;
                _ordersBadgeText.text = hud.OrdersBadge.ToString();
                _ordersBadge.style.display = hud.OrdersBadge > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (popped) Bounce(_orders);
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
