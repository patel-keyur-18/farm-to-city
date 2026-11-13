using System;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Sim.Core.Commands;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Ui
{
    /// <summary>
    /// The bottom sheet (spec 008 §3.2): at most one open, full height with the same margin top and bottom (owner feedback, device
    /// run 2: half height was cramped), sliding up and down. It draws <see cref="SheetVm"/> and <see cref="SowSheetVm"/> and hands a pressed button's command to the host; a greyed
    /// button shows the sim's reason under it.
    /// </summary>
    public sealed class SheetPresenter
    {
        private readonly Tweens _tweens;
        private readonly VisualElement _holder;
        private const float Margin = 14f;

        private VisualElement _sheet;
        private bool _closing;
        private string _key;

        /// <summary>The amount each picker on the open sheet is set to, kept across refreshes (cleared when a sheet opens afresh).</summary>
        private readonly System.Collections.Generic.Dictionary<string, long> _amounts = new System.Collections.Generic.Dictionary<string, long>();

        /// <summary>An amount box has the keyboard: refreshes wait until it's done, so typing is never lost.</summary>
        private bool _typing;

        /// <summary>A button was pressed: the action (the host makes the command with the current sim time, or opens another sheet).</summary>
        public Action<ActionVm> OnAction { get; set; }

        /// <summary>The sow sheet's fertiliser choice changed.</summary>
        public Action<string> OnFertiliser { get; set; }

        public Action OnClosed { get; set; }

        /// <summary>A row's ? chip was tapped (the only way a Why? opens, ADR 0044 §3): key and id.</summary>
        public Action<string, long> OnWhy { get; set; }

        /// <summary>A row that opens a sheet was tapped (a good in the Store).</summary>
        public Action<SheetRequest> OnOpenRow { get; set; }

        /// <summary>The sheet before this one, if any: the header shows a back button (ADR 0044 §1).</summary>
        public Func<bool> CanGoBack { get; set; }

        public Action OnBack { get; set; }

        public bool IsOpen { get { return _sheet != null && !_closing; } }

        public SheetPresenter(VisualElement root, Tweens tweens)
        {
            _tweens = tweens;
            _holder = new VisualElement { pickingMode = PickingMode.Ignore };
            _holder.style.position = Position.Absolute;
            _holder.style.left = 0; _holder.style.right = 0; _holder.style.bottom = 0; _holder.style.top = 0;
            _holder.style.justifyContent = Justify.FlexEnd;
            _holder.style.alignItems = Align.Center;
            _holder.style.paddingTop = _holder.style.paddingBottom = Margin;
            root.Add(_holder);
        }

        private VisualElement NewSheet(string title, string subtitle, string icon, Action close)
        {
            VisualElement panel = UiKit.Panel(36);
            panel.style.width = Length.Percent(78);
            panel.style.maxWidth = 1500;
            panel.style.flexGrow = 1; // the whole height between the margins
            UiKit.Padding(panel, 16, 30);
            panel.pickingMode = PickingMode.Position;

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            if (CanGoBack != null && CanGoBack())
            {
                var back = new Button(() => { if (OnBack != null) OnBack(); }) { text = "", name = "Back" };
                back.style.width = 76; back.style.height = 76; back.style.backgroundColor = UiKit.Sand;
                back.style.marginRight = 20;
                UiKit.Radius(back, 38);
                UiKit.Border(back, 4, UiKit.SandDark);
                back.style.alignItems = Align.Center;
                back.style.justifyContent = Justify.Center;
                UiKit.Padding(back, 0, 0);
                Label arrow = UiKit.Text("←", 46, UiKit.Ink, UiKit.Body);
                arrow.style.marginTop = -6;
                back.Add(arrow);
                header.Add(back);
            }
            Sprite thumb = Thumbnail(icon);
            if (thumb != null)
            {
                VisualElement pic = UiKit.Icon(thumb, 84);
                pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                pic.style.marginRight = 20;
                header.Add(pic);
            }
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.style.flexGrow = 1;
            titles.Add(UiKit.Heading1(title, 52));
            if (!string.IsNullOrEmpty(subtitle)) titles.Add(UiKit.Text(subtitle, 30, UiKit.Wood, UiKit.Body));
            header.Add(titles);
            var x = new Button(close) { text = "", name = "close" }; // named for the Editor's scripted touch (TouchDriver.TapNamed)
            x.style.width = 76; x.style.height = 76; x.style.backgroundColor = UiKit.Red;
            UiKit.Radius(x, 38);
            UiKit.Border(x, 4, UiKit.RedDark);
            x.style.backgroundImage = new StyleBackground(IconBank.Close);
            x.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            header.Add(x);
            panel.Add(header);
            return panel;
        }

        private static Sprite Thumbnail(string icon)
        {
            switch (icon)
            {
                case null: return null;
                case "hut": return SpriteBank.Rendered("hut") ?? SpriteBank.Hut;
                case "cattle_shed": return SpriteBank.Rendered("cattle_shed") ?? SpriteBank.Hut;
                case "people": return SpriteBank.Rendered("person_founder_front");
                case "person_hand_m": case "person_hand_f": case "person_labourer_m": case "person_labourer_f": return SpriteBank.Rendered(icon + "_front");
                case "hand_hut": case "hand_hut_building": case "granary": return SpriteBank.Rendered(icon) ?? SpriteBank.Hut; // granary: your godown in town (ADR 0087)
                case "family_house": case "family_house_building": case "flour_mill": case "flour_mill_building": case "bakery": case "bakery_building": case "dairy": case "dairy_building": return SpriteBank.Rendered(icon + "_s") ?? SpriteBank.Hut;
                case "veg_stall": case "tea_stall": case "sweet_shop": return SpriteBank.Rendered(icon + "_s") ?? SpriteBank.Hut; // shops (spec 017)
                case "school": case "school_building": return IconBank.Good(icon); // spec 018
                case "newcomer_veg": case "newcomer_chai": case "newcomer_sweets": return IconBank.Good(icon); // spec 017 §3.7
                case "veg_stall_building": case "tea_stall_building": case "sweet_shop_building": return SpriteBank.Rendered(icon.Substring(0, icon.Length - 9) + "_s") ?? SpriteBank.Hut;
                case "pitch": return SpriteBank.Rendered("pitch_empty_1");
                case "star": return IconBank.Star(2); // a shop's stars (spec 017 §3.5)
                case "place_halwai": return SpriteBank.Rendered("sweet_shop_s");
                case "population": case "happiness": return SpriteBank.Rendered("icon_" + icon);
                case "pump": case "borewell": case "channel": case "drip": return SpriteBank.Rendered("icon_" + icon); // irrigation (spec 013 §7)
                case "tractor": case "plough_team": return SpriteBank.Rendered(icon); // equipment (spec 013 §7)
                case "store": case "sack": return IconBank.Sack;
                case "gear": return IconBank.Gear;
                case "coin": return IconBank.Coin;
                case "savings": return SpriteBank.Rendered("icon_savings") ?? IconBank.Coin; // the savings deposit (spec 019 §3.5)
                case "menu": return IconBank.Ledger;
                case "alert": return IconBank.Bell;
                case "cart": return IconBank.Cart;
                case "orders": return IconBank.Orders;
                case "letter": return IconBank.Letter;
                case "codex": case "deed": return IconBank.Good(icon); // the Codex and its Assets group (spec 018 §7)
                case "person_founder": return SpriteBank.Rendered("person_founder_front");
                case "compost": case "fertiliser_chemical": case "milk": case "spray": case "spinach": case "coriander": case "atta": case "bran": case "pav": case "rusk": case "paneer": case "curd": case "ghee":
                case "rasgulla": case "atta_laddoo":
                    return IconBank.Good(icon);
                case "garden": return SpriteBank.Bed(null, Sim.View.Views.CropStage.None);
                case "land": return SpriteBank.Sign;
                case "road": return SpriteBank.Rendered("ground_road_3");
                case "commons": return SpriteBank.Rendered("ground_commons");
                case "bank": return SpriteBank.Rendered("ground_bank_1");
                case "place_house": return SpriteBank.Rendered("place_house_0");
                case "field": return SpriteBank.SoilTile;
                case "tree": return SpriteBank.Tree("mango", TreeStage.Mature);
                case "banana": case "papaya": case "guava": case "mango": return SpriteBank.Tree(icon, TreeStage.Mature);
                default:
                    if (icon.StartsWith("weather_", StringComparison.Ordinal)) return IconBank.Weather(icon.Substring(8));
                    if (icon.StartsWith("place_", StringComparison.Ordinal)) return SpriteBank.Rendered(icon) ?? SpriteBank.Rendered("village_" + icon.Substring(6) + "_s");
                    if (icon.StartsWith("animal_", StringComparison.Ordinal) || icon.StartsWith("seed_", StringComparison.Ordinal)) return IconBank.Good(icon);
                    if (icon.StartsWith("portrait_", StringComparison.Ordinal)) return SpriteBank.Rendered(icon);
                    return SpriteBank.Crop(icon, CropStage.Growing3) ?? SpriteBank.Bed(icon, CropStage.Growing3);
            }
        }

        /// <summary>
        /// The host redraws the open sheet after every Sync (once a second). Rebuilding unchanged content would throw away the scroll
        /// position and any press in progress, so a refresh with the same text is skipped and a changed one keeps the scroll.
        /// </summary>
        private bool Unchanged(string key, bool refresh)
        {
            if (refresh && IsOpen && key == _key) return true;
            _key = key;
            return false;
        }

        private void Show(VisualElement sheet, bool refresh)
        {
            bool had = _sheet != null;
            if (had)
            {
                ScrollView oldScroll = _sheet.Q<ScrollView>(), newScroll = sheet.Q<ScrollView>();
                if (refresh && oldScroll != null && newScroll != null)
                {
                    Vector2 offset = oldScroll.scrollOffset;
                    newScroll.schedule.Execute(() => newScroll.scrollOffset = offset); // after layout, so the offset isn't clamped to 0
                }
                _holder.Remove(_sheet);
            }
            _sheet = sheet;
            _closing = false;
            _holder.Add(sheet);
            if (!had)
            {
                sheet.style.translate = new Translate(0, Length.Percent(110));
                _tweens.Run(sheet, 0.25f, Ease.OutCubic, k => sheet.style.translate = new Translate(0, Length.Percent((1f - k) * 110f)));
            }
        }

        /// <summary>
        /// A small question card over the sheet (ADR 0039 §6): "Change Ramesh's order to potato too?" with two buttons.
        /// </summary>
        public void Confirm(ConfirmVm vm, Action yes, Action no)
        {
            var veil = new VisualElement { name = "Veil" };
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.45f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement card = UiKit.Panel(32);
            UiKit.Padding(card, 26, 36);
            card.style.maxWidth = 1100;
            Label q = UiKit.Heading1(vm.Question, 46);
            q.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(q);
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;
            buttons.style.marginTop = 14;
            buttons.Add(UiKit.MakeButton(vm.YesLabel, vm.YesIsDanger ? ActionLook.Danger : ActionLook.Primary, () => { _holder.parent.Remove(veil); yes(); }));
            buttons.Add(UiKit.MakeButton(vm.NoLabel, ActionLook.Secondary, () => { _holder.parent.Remove(veil); no(); }));
            card.Add(buttons);
            veil.Add(card);
            _holder.parent.Add(veil);
            card.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            _tweens.Run(card, 0.2f, Ease.OutBack, k => card.style.scale = new Scale(new Vector3(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f)));
        }

        /// <summary>
        /// A confirm card (spec 008 §3.15, ADR 0101 §12): the question, its numbers, a large safe button and the action. A tap card's
        /// action is a normal tap; a hold card's red button fills while it is held and acts when full, and a shorter press does nothing.
        /// </summary>
        public void ConfirmCard(ConfirmCardVm vm, Action yes, Action no, Func<string, string> text)
        {
            var veil = new VisualElement { name = "Veil" };
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.55f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement card = UiKit.Panel(32);
            card.name = "ConfirmCard";
            UiKit.Padding(card, 26, 40);
            card.style.width = Length.Percent(70);
            card.style.maxWidth = 1250;
            Label title = UiKit.Heading1(vm.Title, 50);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(title);
            if (!string.IsNullOrEmpty(vm.Subtitle))
            {
                Label sub = UiKit.Text(vm.Subtitle, 30, UiKit.Wood, UiKit.Body);
                sub.style.unityTextAlign = TextAnchor.MiddleCenter;
                sub.style.whiteSpace = WhiteSpace.Normal;
                card.Add(sub);
            }
            foreach (ConfirmRowVm row in vm.Rows)
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                r.style.flexDirection = FlexDirection.Row;
                r.style.justifyContent = Justify.SpaceBetween;
                r.style.paddingTop = 8; r.style.paddingBottom = 8;
                r.style.borderBottomWidth = 2; r.style.borderBottomColor = UiKit.CreamDark;
                if (row.Total) { r.style.backgroundColor = UiKit.CreamDark; UiKit.Radius(r, 14); UiKit.Padding(r, 8, 14); r.style.marginTop = 8; r.style.borderBottomWidth = 0; }
                Label l = UiKit.Text(row.Label, row.Total ? 36 : 32, UiKit.Ink, row.Total ? UiKit.Heading : UiKit.Body);
                l.style.whiteSpace = WhiteSpace.Normal; l.style.flexShrink = 1;
                Label v = UiKit.Text(row.Value, row.Total ? 38 : 34, UiKit.Ink, UiKit.Heading);
                v.style.whiteSpace = WhiteSpace.NoWrap; v.style.marginLeft = 20;
                r.Add(l); r.Add(v);
                card.Add(r);
            }
            if (!string.IsNullOrEmpty(vm.Warning))
            {
                var warn = new VisualElement { pickingMode = PickingMode.Ignore };
                warn.style.backgroundColor = new Color(0.98f, 0.89f, 0.85f);
                UiKit.Radius(warn, 18); UiKit.Padding(warn, 8, 18); warn.style.marginTop = 14;
                Label w = UiKit.Text(vm.Warning, 30, UiKit.Warn, UiKit.Heading);
                w.style.whiteSpace = WhiteSpace.Normal; w.style.unityTextAlign = TextAnchor.MiddleCenter;
                warn.Add(w);
                card.Add(warn);
            }
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 14;
            void Close() { veil.RemoveFromHierarchy(); }
            Button safe = UiKit.MakeButton(vm.SafeLabel, ActionLook.Primary, () => { Close(); no(); });
            safe.name = "confirm:safe";
            safe.style.flexGrow = 1;
            Button go = UiKit.MakeButton(vm.GoLabel, vm.Hold ? ActionLook.Danger : ActionLook.Primary, vm.Hold ? (Action)(() => { }) : () => { Close(); yes(); });
            go.name = "confirm:go";
            go.style.flexGrow = 1;
            if (vm.Hold) HoldToAct(go, vm.HoldMillis / 1000f, () => { Close(); yes(); });
            buttons.Add(safe);
            buttons.Add(go);
            card.Add(buttons);
            if (vm.Hold)
            {
                Label hint = UiKit.Text(text("confirm.holdHint"), 26, UiKit.Wood, UiKit.Body);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter; hint.style.marginTop = 6;
                card.Add(hint);
            }
            veil.Add(card);
            _holder.parent.Add(veil);
            card.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            _tweens.Run(card, 0.2f, Ease.OutBack, k => card.style.scale = new Scale(new Vector3(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f)));
        }

        /// <summary>The red button fills left to right while it's held; lifted or cancelled early, it empties and nothing is sent.</summary>
        private void HoldToAct(Button b, float seconds, Action done)
        {
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = Length.Percent(0);
            fill.style.backgroundColor = new Color(0.56f, 0.17f, 0.12f, 0.55f);
            b.Insert(0, fill);
            void Reset() { _tweens.Cancel(fill); fill.style.width = Length.Percent(0); }
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                Reset();
                _tweens.Run(fill, seconds, Ease.Linear, k => fill.style.width = Length.Percent(k * 100f), done);
            });
            b.RegisterCallback<PointerUpEvent>(e => Reset());
            b.RegisterCallback<PointerCancelEvent>(e => Reset());
            b.RegisterCallback<PointerLeaveEvent>(e => Reset());
        }

        /// <summary>A card over everything (spec 007 §7, ADR 0041): a title, a sentence and its buttons; any button closes it.</summary>
        public void Card(CardVm vm, Action<string> onButton)
        {
            var veil = new VisualElement { name = "Veil" };
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.45f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement card = UiKit.Panel(32);
            UiKit.Padding(card, 26, 40);
            card.style.maxWidth = 1150;
            Label title = UiKit.Heading1(vm.Title, 50);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(title);
            Label text = UiKit.Text(vm.Text, 34, UiKit.Ink, UiKit.Body);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 10;
            card.Add(text);
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;
            buttons.style.flexWrap = Wrap.Wrap; // the renewal card has up to five choices (US-240)
            buttons.style.marginTop = 18;
            foreach (CardButton b in vm.Buttons)
            {
                ActionLook look = b.Kind == ActionKind.Danger ? ActionLook.Danger : b.Kind == ActionKind.Primary ? ActionLook.Primary : ActionLook.Secondary;
                string id = b.Id;
                Button button = UiKit.MakeButton(b.Label, look, () => { veil.RemoveFromHierarchy(); onButton(id); }, b.Detail);
                button.name = "card:" + id;
                buttons.Add(button);
            }
            card.Add(buttons);
            veil.Add(card);
            _holder.parent.Add(veil);
            card.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            _tweens.Run(card, 0.2f, Ease.OutBack, k => card.style.scale = new Scale(new Vector3(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f)));
        }

        /// <summary>True while a Why? popup (or a Codex card) is open: the Codex's toast waits for it.</summary>
        public bool WhyOpen { get { VisualElement r = _holder.parent; return r != null && r.Q("WhyVeil") != null; } }

        /// <summary>True while a card, the away card or a letter page is over the map (the letter waits for it to close).</summary>
        public bool OverlayOpen
        {
            get { VisualElement r = _holder.parent; return r != null && (r.Q("AwayVeil") != null || r.Q("Veil") != null); }
        }

        /// <summary>
        /// A page of Dadi's letter (spec 009 §7): over the map like a sheet of paper, the title, the text (scrolls if long), the
        /// signature, the footer and its buttons; any button closes it.
        /// </summary>
        public void Letter(LetterVm vm, Action<string> onButton)
        {
            var veil = new VisualElement { name = "Veil" };
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.45f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement page = UiKit.Panel(18);
            page.name = "LetterPage";
            page.style.backgroundColor = new Color(1f, 0.97f, 0.88f, 1f);
            UiKit.Border(page, 4, UiKit.Sand);
            UiKit.Padding(page, 26, 44);
            page.style.width = Length.Percent(62);
            page.style.maxHeight = Length.Percent(92);
            page.Add(UiKit.Heading1(vm.Title, 50));
            var scroll = NewScroll();
            scroll.style.flexShrink = 1;
            Label text = UiKit.Text(vm.Text, 34, UiKit.Ink, UiKit.Body);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginTop = 8;
            scroll.Add(text);
            Label sign = UiKit.Text(vm.Signature, 36, UiKit.Wood, UiKit.Heading);
            sign.style.unityTextAlign = TextAnchor.MiddleRight;
            sign.style.marginTop = 12;
            scroll.Add(sign);
            page.Add(scroll);
            Label footer = UiKit.Text(vm.Footer, 24, UiKit.GreyDark, UiKit.Body);
            footer.style.whiteSpace = WhiteSpace.NoWrap;
            footer.style.marginTop = 6;
            page.Add(footer);
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.Center;
            buttons.style.marginTop = 10;
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            foreach (CardButton b in vm.Buttons)
            {
                ActionLook look = b.Kind == ActionKind.Primary ? ActionLook.Primary : ActionLook.Secondary;
                string id = b.Id;
                Button button = UiKit.MakeButton(b.Label, look, () => { veil.RemoveFromHierarchy(); onButton(id); });
                button.name = "letter:" + id;
                button.style.flexShrink = 0;
                // "I'll find my own way" on the left, the page's own buttons on the right.
                if (id == LetterIds.Skip) buttons.Insert(0, button);
                else buttons.Add(button);
            }
            buttons.Insert(vm.Buttons.Exists(x => x.Id == LetterIds.Skip) ? 1 : 0, spacer);
            page.Add(buttons);
            veil.Add(page);
            _holder.parent.Add(veil);
            page.style.translate = new Translate(0, 60);
            page.style.opacity = 0f;
            _tweens.Run(page, 0.3f, Ease.OutCubic, k => { page.style.translate = new Translate(0, 60 * (1f - k)); page.style.opacity = k; });
        }

        public void Close()
        {
            if (_sheet == null || _closing) return;
            _closing = true;
            VisualElement s = _sheet;
            _tweens.Run(s, 0.18f, Ease.InCubic, k => s.style.translate = new Translate(0, Length.Percent(k * 110f)), () =>
            {
                if (_sheet == s) { _holder.Remove(s); _sheet = null; _closing = false; }
                if (OnClosed != null) OnClosed();
            });
        }

        private static ScrollView NewScroll()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            return scroll;
        }

        /// <summary>Five stars in a row: solid, then faded, then empty.</summary>
        private static VisualElement Stars(StarsVm s)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginRight = 12;
            for (int i = 0; i < 5; i++)
            {
                VisualElement star = UiKit.Icon(IconBank.Star(i < s.Solid ? 2 : i < s.Solid + s.Faded ? 1 : 0), 34);
                star.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                row.Add(star);
            }
            return row;
        }

        private VisualElement Row(LineVm line)
        {
            if (line.Amount != null) return AmountRow(line);
            if (line.Tree != null) return FamilyTree(line.Tree);
            if (line.Advisor != null) return AdvisorLine(line.Advisor);
            if (line.Pie == null && line.Trend == null) return PlainRow(line);
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.Add(PlainRow(line));
            if (line.Trend != null) box.Add(TrendChart(line.Trend));
            if (line.Pie != null) box.Add(PieChart(line.Pie));
            return box;
        }

        /// <summary>
        /// An amount to drag or type (the savings sheet, owner 2026-11-13): the amount box and its button, a slider with a coin to drag (the
        /// main way), its ends, and the line saying what the amount would do. Nothing is focused when it opens: the number pad comes up only
        /// when the box is tapped.
        /// </summary>
        private VisualElement AmountRow(LineVm line)
        {
            AmountVm a = line.Amount;
            long value = _amounts.TryGetValue(a.Id, out long kept) ? System.Math.Min(a.Max, System.Math.Max(a.Min, kept)) : a.Start;
            var box = new VisualElement { pickingMode = PickingMode.Ignore, name = "amount:" + a.Id };
            box.style.paddingTop = 6; box.style.paddingBottom = 10;
            box.style.borderBottomWidth = 2;
            box.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);

            Label range = UiKit.Text(line.Label, 26, UiKit.Wood, UiKit.Body);
            range.style.unityTextAlign = TextAnchor.MiddleRight;
            box.Add(range);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            var field = new TextField { name = "amountField:" + a.Id, keyboardType = TouchScreenKeyboardType.NumberPad };
            field.style.flexGrow = 1;
            field.style.marginRight = 18;
            field.style.fontSize = 44;
            UiKit.SetFont(field, UiKit.Heading);
            VisualElement input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Painter.Hex(0xFFFAF0);
                UiKit.Border(input, 4, UiKit.Wood);
                UiKit.Radius(input, 20);
                UiKit.Padding(input, 6, 20);
                input.style.color = UiKit.Ink;
            }
            top.Add(field);
            var buttonSlot = new VisualElement { pickingMode = PickingMode.Ignore };
            top.Add(buttonSlot);
            box.Add(top);

            var slider = new VisualElement { name = "slider:" + a.Id };
            slider.style.height = 96;
            slider.style.marginLeft = slider.style.marginRight = 46; // the coin (radius 40) never clipped at either end
            slider.style.marginTop = 26;
            box.Add(slider);

            var ends = new VisualElement { pickingMode = PickingMode.Ignore };
            ends.style.flexDirection = FlexDirection.Row;
            ends.style.justifyContent = Justify.SpaceBetween;
            ends.Add(UiKit.Text(a.MinCaption, 25, UiKit.Wood, UiKit.Body));
            ends.Add(UiKit.Text(a.MaxCaption, 25, UiKit.Wood, UiKit.Body));
            box.Add(ends);
            Label note = UiKit.Text("", 28, UiKit.Ink, UiKit.BodyBold);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 4;
            box.Add(note);

            string shown = null;
            string Digits(long v) { return Sim.View.Text.Formatter.WholeCoins(v).ToString(System.Globalization.CultureInfo.InvariantCulture); }
            float Fraction(long v) { return a.Max <= a.Min ? 1f : Mathf.Clamp01((float)(v - a.Min) / (a.Max - a.Min)); }
            void Choose(long v, bool fromField)
            {
                value = v;
                _amounts[a.Id] = v;
                if (!fromField) field.SetValueWithoutNotify(field.focusController?.focusedElement == field ? Digits(v) : a.Words(v));
                AmountPick pick = a.Pick(v);
                string look = pick.Action.Label + pick.Action.Enabled;
                if (look != shown) // rebuilt only when it changes: leaving the box mustn't swap the button under the finger tapping it
                {
                    shown = look;
                    buttonSlot.Clear();
                    buttonSlot.Add(UiKit.MakeButton(pick.Action.Label, a.Id == "out" ? ActionLook.Secondary : ActionLook.Primary,
                    () => { ActionVm go = a.Pick(value).Action; _amounts.Remove(a.Id); if (OnAction != null) OnAction(go); }, null, pick.Action.Enabled)); // each move starts afresh
                }
                note.text = pick.Note ?? "";
                note.style.color = pick.Tone == Tone.Warn ? UiKit.Warn : pick.Tone == Tone.Good ? UiKit.GreenDark : UiKit.Ink;
                slider.MarkDirtyRepaint();
            }

            Color track = Painter.Hex(0xE2CFA6), fill = a.Id == "out" ? UiKit.Sand : UiKit.Green;
            Color amber = Painter.Hex(0xC97A12);
            slider.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                Rect r = slider.contentRect;
                float y = r.height * 0.62f, x0 = 0f, x1 = r.width, x = Mathf.Lerp(x0, x1, Fraction(value));
                p.lineCap = LineCap.Round;
                p.lineWidth = 22f; p.strokeColor = UiKit.SandDark;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
                p.lineWidth = 16f; p.strokeColor = track;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
                p.lineWidth = 16f; p.strokeColor = fill;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(Mathf.Max(x0 + 1f, x), y)); p.Stroke();
                if (a.Mark >= 0)
                {
                    float m = Mathf.Lerp(x0, x1, Fraction(a.Mark));
                    p.lineCap = LineCap.Butt; p.lineWidth = 6f; p.strokeColor = amber;
                    p.BeginPath(); p.MoveTo(new Vector2(m, y - 34f)); p.LineTo(new Vector2(m, y + 34f)); p.Stroke();
                    p.lineCap = LineCap.Round;
                }
                p.fillColor = Painter.Hex(0x7A5216);
                p.BeginPath(); p.Arc(new Vector2(x, y + 4f), 40f, 0f, 360f); p.Fill();
                p.fillColor = Painter.Hex(0xE8B22E);
                p.BeginPath(); p.Arc(new Vector2(x, y), 36f, 0f, 360f); p.Fill();
                p.fillColor = Painter.Hex(0xFFE58A);
                p.BeginPath(); p.Arc(new Vector2(x - 8f, y - 8f), 16f, 0f, 360f); p.Fill();
            };
            if (a.Mark >= 0)
            {
                Label mark = UiKit.Text(a.MarkCaption, 24, amber, UiKit.BodyBold);
                mark.pickingMode = PickingMode.Ignore;
                mark.style.position = Position.Absolute;
                mark.style.top = -30;
                mark.style.left = Length.Percent(Mathf.Clamp(Fraction(a.Mark) * 100f, 0f, 70f));
                slider.Add(mark);
            }

            // Drag: the coin follows the finger in steps; the scroll view doesn't take the drag (the pointer is captured).
            long At(Vector2 local) { return a.Snap(a.Min + (long)((a.Max - a.Min) * (double)Mathf.Clamp01(local.x / Mathf.Max(1f, slider.contentRect.width)))); }
            slider.RegisterCallback<PointerDownEvent>(e =>
            {
                field.Blur();
                slider.CapturePointer(e.pointerId);
                Choose(At(e.localPosition), false);
                e.StopPropagation();
            });
            slider.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!slider.HasPointerCapture(e.pointerId)) return;
                long v = At(e.localPosition);
                if (v != value) Choose(v, false);
                e.StopPropagation();
            });
            slider.RegisterCallback<PointerUpEvent>(e => { if (slider.HasPointerCapture(e.pointerId)) slider.ReleasePointer(e.pointerId); e.StopPropagation(); });

            // Type: any whole number of coins; the slider follows. The box only takes focus when tapped.
            field.RegisterValueChangedCallback(e =>
            {
                string digits = System.Text.RegularExpressions.Regex.Replace(e.newValue ?? "", "[^0-9]", "");
                if (digits.Length > 9) digits = digits.Substring(0, 9); // more than any cash: the line says the most you can
                if (digits != e.newValue) field.SetValueWithoutNotify(digits);
                long coins = digits.Length == 0 ? 0 : long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
                Choose(coins * 100, true);
            });
            field.RegisterCallback<FocusInEvent>(_ => { _typing = true; field.SetValueWithoutNotify(Digits(value)); }); // plain digits to edit
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                _typing = false;
                Choose(string.IsNullOrEmpty(field.value) ? a.Start : value, false); // back to "32,000 coins"
            });
            Choose(value, false);
            return box;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Color> SliceColours = new System.Collections.Generic.Dictionary<string, Color>
        {
            { "food", Painter.Hex(0xE8A33C) }, { "rent", Painter.Hex(0x8A5A2B) }, { "clothing", Painter.Hex(0xC2577A) }, { "health", Painter.Hex(0x4FA3A5) },
            { "schooling", Painter.Hex(0x5B7FC7) }, { "leisure", Painter.Hex(0x79C84A) }, { "saved", Painter.Hex(0xFFD968) },
            { "land", Painter.Hex(0x8A5A2B) }, { "buildings", Painter.Hex(0xE8892A) }, { "livestock", Painter.Hex(0x3F7D20) }, { "goods", Painter.Hex(0x79C84A) },
            { "cash", Painter.Hex(0xFFD968) }, { "businesses", Painter.Hex(0x5B7FC7) }, { "owed", Painter.Hex(0x4FA3A5) }, // your wealth (spec 019 §3.2)
        };

        /// <summary>Net worth's trend (spec 019 §3.2): a line over a tan field with a dot on each close, the first and last captions under it.</summary>
        private static VisualElement TrendChart(TrendVm trend)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.paddingTop = 8; box.style.paddingBottom = 8;
            var chart = new VisualElement { pickingMode = PickingMode.Ignore };
            chart.style.height = 190;
            chart.style.backgroundColor = UiKit.CreamDark;
            UiKit.Radius(chart, 18);
            chart.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                Rect r = chart.contentRect;
                long lo = long.MaxValue, hi = long.MinValue;
                foreach (long v in trend.Values) { if (v < lo) lo = v; if (v > hi) hi = v; }
                if (hi == lo) { hi++; lo--; }
                int n = trend.Values.Count;
                const float pad = 26f;
                Vector2 At(int i) => new Vector2(pad + (r.width - 2 * pad) * i / Mathf.Max(1, n - 1), r.height - pad - (r.height - 2 * pad) * (float)(trend.Values[i] - lo) / (hi - lo));
                p.strokeColor = UiKit.Green;
                p.lineWidth = 8f;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                for (int i = 0; i < n; i++) { if (i == 0) p.MoveTo(At(i)); else p.LineTo(At(i)); }
                p.Stroke();
                p.fillColor = UiKit.Green;
                for (int i = 0; i < n; i++) { p.BeginPath(); p.Arc(At(i), 11f, 0f, 360f); p.Fill(); }
            };
            box.Add(chart);
            var caps = new VisualElement { pickingMode = PickingMode.Ignore };
            caps.style.flexDirection = FlexDirection.Row;
            caps.style.justifyContent = Justify.SpaceBetween;
            caps.style.marginTop = 4;
            caps.Add(UiKit.Text(trend.FirstCaption, 24, UiKit.Wood, UiKit.Body));
            caps.Add(UiKit.Text(trend.LastCaption, 24, UiKit.Wood, UiKit.Body));
            box.Add(caps);
            return box;
        }

        /// <summary>A household's budget (spec 014 §7): a pie drawn with the vector painter, and its legend beside it.</summary>
        private static VisualElement PieChart(System.Collections.Generic.List<PieSliceVm> slices)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = row.style.paddingBottom = 8;
            var pie = new VisualElement { pickingMode = PickingMode.Ignore };
            pie.style.width = 200; pie.style.height = 200; pie.style.flexShrink = 0;
            pie.style.marginRight = 30;
            pie.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                Vector2 c = pie.contentRect.center;
                float r = Mathf.Min(pie.contentRect.width, pie.contentRect.height) * 0.5f - 3f;
                int total = 0;
                foreach (PieSliceVm sl in slices) total += sl.Percent;
                float at = -90f;
                foreach (PieSliceVm sl in slices)
                {
                    float sweep = total == 0 ? 0 : 360f * sl.Percent / total;
                    if (sweep <= 0f) continue;
                    p.fillColor = SliceColours.TryGetValue(sl.Colour, out Color col) ? col : UiKit.Grey;
                    p.BeginPath();
                    p.MoveTo(c);
                    p.Arc(c, r, at, at + sweep);
                    p.ClosePath();
                    p.Fill();
                    at += sweep;
                }
                p.strokeColor = UiKit.Cream;
                p.lineWidth = 3f;
                p.BeginPath();
                p.Arc(c, r, 0f, 360f);
                p.Stroke();
            };
            row.Add(pie);
            var legend = new VisualElement { pickingMode = PickingMode.Ignore };
            legend.style.flexGrow = 1;
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.flexWrap = Wrap.Wrap;
            foreach (PieSliceVm sl in slices)
            {
                var item = new VisualElement { pickingMode = PickingMode.Ignore };
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.width = Length.Percent(50);
                item.style.marginBottom = 6;
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.width = 28; dot.style.height = 28; dot.style.marginRight = 12; dot.style.flexShrink = 0;
                dot.style.backgroundColor = SliceColours.TryGetValue(sl.Colour, out Color col) ? col : UiKit.Grey;
                UiKit.Radius(dot, 8);
                item.Add(dot);
                item.Add(UiKit.Text(sl.Label + " " + sl.Percent + "% · " + sl.Value, 28, UiKit.Ink, UiKit.Body));
                legend.Add(item);
            }
            row.Add(legend);
            return row;
        }

        /// <summary>
        /// The household's own family (spec 014 §7): a row per generation, centred, a spouse joined by a line, a short stem between
        /// generations; a tap on anyone opens their card.
        /// </summary>
        private VisualElement FamilyTree(FamilyTreeVm tree)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.alignItems = Align.Center;
            box.style.paddingTop = 10; box.style.paddingBottom = 10;
            box.style.marginBottom = 6;
            box.style.backgroundColor = UiKit.CreamDark;
            UiKit.Radius(box, 26);
            for (int g = 0; g < tree.Generations.Count; g++)
            {
                if (g > 0)
                {
                    var stem = new VisualElement { pickingMode = PickingMode.Ignore };
                    stem.style.width = 5; stem.style.height = 26; stem.style.backgroundColor = UiKit.Wood;
                    box.Add(stem);
                }
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.justifyContent = Justify.Center;
                foreach (TreePersonVm p in tree.Generations[g])
                {
                    if (p.SpouseOfPrevious)
                    {
                        var tie = new VisualElement { pickingMode = PickingMode.Ignore };
                        tie.style.width = 40; tie.style.height = 5; tie.style.backgroundColor = UiKit.Wood;
                        row.Add(tie);
                    }
                    else if (row.childCount > 0)
                    {
                        var gap = new VisualElement { pickingMode = PickingMode.Ignore };
                        gap.style.width = 34;
                        row.Add(gap);
                    }
                    long id = p.CitizenId;
                    var person = new Button(() => { if (OnOpenRow != null) OnOpenRow(new SheetRequest("Citizen", id)); }) { text = "", name = "tree:" + id };
                    person.style.flexDirection = FlexDirection.Row;
                    person.style.alignItems = Align.Center;
                    person.style.backgroundColor = UiKit.Cream;
                    UiKit.Radius(person, 22);
                    UiKit.Border(person, 3, UiKit.Sand);
                    UiKit.Padding(person, 4, 14);
                    person.style.marginLeft = person.style.marginRight = 0;
                    Sprite face = Thumbnail(p.Icon);
                    if (face != null)
                    {
                        VisualElement pic = UiKit.Icon(face, 64);
                        pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                        pic.style.marginRight = 8;
                        pic.pickingMode = PickingMode.Ignore;
                        person.Add(pic);
                    }
                    Label name = UiKit.Text(p.Text, 30, UiKit.Ink, UiKit.Heading);
                    name.pickingMode = PickingMode.Ignore;
                    person.Add(name);
                    row.Add(person);
                }
                box.Add(row);
            }
            return box;
        }

        /// <summary>Tabs over the rows (People: Workers and Town): the showing one filled, the other a plain button.</summary>
        private VisualElement TabStrip(System.Collections.Generic.List<ActionVm> tabs, string showing)
        {
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.marginTop = 10;
            foreach (ActionVm t in tabs)
            {
                ActionVm tab = t;
                bool on = t.Id == showing;
                Button b = UiKit.MakeButton(t.Label, on ? ActionLook.Primary : ActionLook.Secondary, () => { if (!on && OnAction != null) OnAction(tab); });
                b.name = t.Id;
                b.style.flexGrow = 1; b.style.flexBasis = 0;
                b.style.marginTop = 0; b.style.marginRight = 14;
                strip.Add(b);
            }
            return strip;
        }

        /// <summary>
        /// An advisor's line (spec 018 §3.4): on the season report a panel under the title (portrait, name and role, the quote); under an
        /// alert one indented line, "Name: “…”", with a small portrait. Only words: nothing to tap.
        /// </summary>
        private static VisualElement AdvisorLine(AdvisorVm a)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore, name = "advisor" };
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            VisualElement portrait = UiKit.Icon(IconBank.Good(a.Portrait), a.Compact ? 72 : 128);
            portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            portrait.style.flexShrink = 0;
            portrait.style.marginRight = a.Compact ? 14 : 24;
            box.Add(portrait);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.flexShrink = 1;
            words.style.flexGrow = 1;
            if (!a.Compact) words.Add(UiKit.Text(a.Who, 32, UiKit.Wood, UiKit.Heading));
            Label text = UiKit.Text(a.Text, a.Compact ? 29 : 31, UiKit.Ink, UiKit.Body);
            text.style.whiteSpace = WhiteSpace.Normal;
            words.Add(text);
            box.Add(words);
            if (a.Compact)
            {
                box.style.marginLeft = 40;
                box.style.paddingTop = box.style.paddingBottom = 4;
                box.style.borderBottomWidth = 2;
                box.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
                return box;
            }
            box.style.backgroundColor = UiKit.CreamDark;
            UiKit.Border(box, 3, UiKit.Wood);
            UiKit.Radius(box, 24);
            UiKit.Padding(box, 14, 24);
            box.style.marginTop = 6;
            box.style.marginBottom = 12;
            return box;
        }

        private VisualElement PlainRow(LineVm line)
        {
            if (line.Buttons != null) return PictureButtons(line.Buttons);
            if (line.IsHeader)
            {
                // A section heading: a wood title with a rule under it, a little air above.
                Label h = UiKit.Heading1(line.Label, 40, UiKit.Wood);
                if (line.Icon == null)
                {
                    h.style.marginTop = 18;
                    h.style.borderBottomWidth = 3;
                    h.style.borderBottomColor = UiKit.Wood;
                    return h;
                }
                VisualElement withIcon = WithIcon(line.Icon, 56, h); // a Codex group under its icon (spec 018 §7)
                withIcon.style.marginTop = 18;
                withIcon.style.borderBottomWidth = 3;
                withIcon.style.borderBottomColor = UiKit.Wood;
                return withIcon;
            }
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 2;
            row.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
            Tappable(row, line);
            if (line.Action == null && line.Note == null && line.Badge == null)
            {
                row.Add(WithIcon(line.Icon, 56, UiKit.Text(line.Label, 31, UiKit.Ink, UiKit.Body)));
                Label v = UiKit.Text(line.Value, 33, UiKit.Ink, UiKit.Heading);
                v.style.unityTextAlign = TextAnchor.MiddleRight;
                v.style.flexShrink = 1;
                v.style.flexGrow = 1; // right-aligned against the ? chip, not floating mid-row
                v.style.marginLeft = 24;
                if (line.Stars.HasValue)
                {
                    var right = new VisualElement { pickingMode = PickingMode.Ignore };
                    right.style.flexDirection = FlexDirection.Row;
                    right.style.alignItems = Align.Center;
                    right.Add(Stars(line.Stars.Value));
                    right.Add(v);
                    row.Add(right);
                }
                else row.Add(v);
                AddWhyChip(row, line);
                return row;
            }
            // A row with its own button (the store's Sell): name and details on the left, the button on the right.
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.style.flexShrink = 1;
            text.style.flexGrow = 1; // the full width up to the ? chip: a wrapped note measured narrower than its value spilled into the next row
            if (line.Tag == null) text.Add(UiKit.Heading1(line.Label, 40));
            else
            {
                // Not yet looked at (a Codex card's New): an orange tag right after the name.
                var named = new VisualElement { pickingMode = PickingMode.Ignore };
                named.style.flexDirection = FlexDirection.Row;
                named.style.alignItems = Align.Center;
                named.Add(UiKit.Heading1(line.Label, 40));
                Label tag = UiKit.Text(line.Tag, 26, Color.white, UiKit.Heading);
                tag.name = "tag";
                tag.style.backgroundColor = UiKit.Saffron;
                tag.style.marginLeft = 14;
                UiKit.Radius(tag, 18);
                UiKit.Padding(tag, 2, 16);
                named.Add(tag);
                text.Add(named);
            }
            if (line.Stars.HasValue)
            {
                var starLine = new VisualElement { pickingMode = PickingMode.Ignore };
                starLine.style.flexDirection = FlexDirection.Row;
                starLine.style.alignItems = Align.Center;
                starLine.Add(Stars(line.Stars.Value));
                Label value = UiKit.Text(line.Value, 30, UiKit.Wood, UiKit.Body);
                value.style.flexShrink = 1;
                starLine.Add(value);
                text.Add(starLine);
            }
            else text.Add(UiKit.Text(line.Value, 30, UiKit.Wood, UiKit.Body));
            if (line.Note != null)
            {
                // The row's state (an order: when to sow, what's fresh, how much is delivered), in its colour.
                Label note = UiKit.Text(line.Note, 30, line.NoteTone == Tone.Good ? UiKit.GreenDark : line.NoteTone == Tone.Warn ? UiKit.Warn : UiKit.Ink, UiKit.Heading);
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.marginTop = 2;
                text.Add(note);
            }
            VisualElement left = WithIcon(line.Icon, 84, text);
            left.style.flexGrow = 1; // the ? chip and the button sit together on the right
            if (line.Dim) left.style.opacity = 0.55f; // not yet reached (a milestone)
            row.Add(left);
            AddWhyChip(row, line);
            if (line.Badge != null)
            {
                // Something done ("Delivered"): a green tag where the button was.
                Label tag = UiKit.Text(line.Badge, 32, Color.white, UiKit.Heading);
                tag.name = "badge";
                tag.style.backgroundColor = UiKit.GreenDark;
                tag.style.flexShrink = 0;
                tag.style.minWidth = 350;
                tag.style.unityTextAlign = TextAnchor.MiddleCenter;
                UiKit.Radius(tag, 24);
                UiKit.Padding(tag, 10, 24);
                row.Add(tag);
            }
            else if (line.Action != null)
            {
                if (line.Also != null)
                {
                    VisualElement also = ActionColumn(line.Also);
                    also.style.alignItems = Align.FlexEnd;
                    also.style.flexShrink = 0;
                    also.Q<Button>().style.minWidth = 200;
                    row.Add(also);
                }
                VisualElement button = ActionColumn(line.Action);
                button.style.alignItems = Align.FlexEnd;
                button.style.flexShrink = 0;
                if (line.Also != null) button.Q<Button>().style.minWidth = 200;
                row.Add(button);
            }
            return row;
        }

        /// <summary>
        /// Big picture buttons side by side (the Store's shortcuts, ADR 0059 §2): a picture, what it opens, a line under it, and a
        /// red count in the corner when there's something waiting.
        /// </summary>
        private VisualElement PictureButtons(System.Collections.Generic.List<ActionVm> actions)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 14; row.style.marginBottom = 8;
            foreach (ActionVm a in actions)
            {
                ActionVm action = a;
                var b = new Button(() => { if (OnAction != null) OnAction(action); }) { text = "", name = "tile:" + a.Id };
                b.SetEnabled(a.Enabled); // greyed, its reason in place of the detail (a hut with no room or cash)
                b.style.flexGrow = 1; b.style.flexBasis = 0;
                b.style.marginLeft = 0; b.style.marginRight = 16;
                b.style.flexDirection = FlexDirection.Row;
                b.style.alignItems = Align.Center;
                b.style.backgroundColor = UiKit.CreamDark;
                UiKit.Radius(b, 26);
                UiKit.Border(b, 4, UiKit.Sand);
                UiKit.Padding(b, 14, 18);
                Sprite pic = Thumbnail(a.Icon);
                if (pic != null)
                {
                    VisualElement icon = UiKit.Icon(pic, 92);
                    icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                    icon.style.marginRight = 16;
                    icon.style.flexShrink = 0;
                    icon.pickingMode = PickingMode.Ignore;
                    b.Add(icon);
                }
                var words = new VisualElement { pickingMode = PickingMode.Ignore };
                words.style.flexShrink = 1;
                Label title = UiKit.Heading1(a.Label, 38);
                title.pickingMode = PickingMode.Ignore;
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.unityTextAlign = TextAnchor.MiddleLeft; // a Button centres its text by default
                words.Add(title);
                string under = a.Enabled || a.DisabledReason == null ? a.Detail : a.DisabledReason;
                if (!string.IsNullOrEmpty(under))
                {
                    Label detail = UiKit.Text(under, 28, UiKit.Wood, UiKit.Body);
                    detail.pickingMode = PickingMode.Ignore;
                    detail.style.whiteSpace = WhiteSpace.Normal;
                    detail.style.unityTextAlign = TextAnchor.MiddleLeft;
                    words.Add(detail);
                }
                b.Add(words);
                if (a.Count > 0)
                {
                    Label count = UiKit.Text(a.Count.ToString(), 30, Color.white, UiKit.Heading);
                    count.pickingMode = PickingMode.Ignore;
                    count.style.position = Position.Absolute;
                    count.style.top = -14; count.style.right = -10;
                    count.style.width = 52; count.style.height = 52;
                    count.style.unityTextAlign = TextAnchor.MiddleCenter;
                    count.style.backgroundColor = UiKit.Red;
                    UiKit.Radius(count, 26);
                    UiKit.Border(count, 3, Color.white);
                    b.Add(count);
                }
                row.Add(b);
            }
            return row;
        }

        private static bool HasWhy(LineVm line) { return line.WhyKey != null && WhyView.Has(line.WhyKey); }

        /// <summary>
        /// A row that opens a sheet (a good in the Store) opens it on a tap; a tap on the row's own button or ? stays theirs. Rows never
        /// open a Why? themselves: a held finger while scrolling did (owner, Sprint 7 device run; ADR 0044 §3).
        /// </summary>
        private void Tappable(VisualElement row, LineVm line)
        {
            if (!line.Opens.HasValue) return;
            row.pickingMode = PickingMode.Position;
            row.RegisterCallback<ClickEvent>(e => { if (!OnButton(e.target) && OnOpenRow != null) OnOpenRow(line.Opens.Value); });
        }

        private static bool OnButton(IEventHandler target)
        {
            for (var e = target as VisualElement; e != null; e = e.parent) if (e is Button) return true;
            return false;
        }

        /// <summary>The small round ? at the end of a row that has a Why? (ADR 0043 §3).</summary>
        private void AddWhyChip(VisualElement row, LineVm line)
        {
            if (!HasWhy(line)) return;
            AddWhyChip(row, line.WhyKey, line.WhyId);
        }

        /// <summary>The same ? for a Why? that isn't a sheet row's (a sow row's hourly figure, spec 020 §3.6).</summary>
        private Button AddWhyChip(VisualElement row, string whyKey, long whyId)
        {
            if (whyKey == null || !WhyView.Has(whyKey)) return null;
            var chip = new Button(() => { if (OnWhy != null) OnWhy(whyKey, whyId); }) { text = "" };
            chip.name = "why:" + whyKey;
            chip.style.width = 56; chip.style.height = 56;
            chip.style.marginLeft = 14; chip.style.marginRight = 6;
            chip.style.flexShrink = 0;
            chip.style.backgroundColor = UiKit.CreamDark;
            UiKit.Radius(chip, 28);
            UiKit.Border(chip, 3, UiKit.Wood);
            chip.style.alignItems = Align.Center;
            chip.style.justifyContent = Justify.Center;
            UiKit.Padding(chip, 0, 0);
            Label q = UiKit.Text("?", 36, UiKit.Wood, UiKit.Heading);
            q.style.marginTop = -4;
            chip.Add(q);
            row.Add(chip);
            return chip;
        }

        /// <summary>
        /// A Why? card stacked over the sheet (spec 008 §3.6, §3.12): the number, one row per cause in words with its effect, and the
        /// lesson. A tap anywhere closes it.
        /// </summary>
        public void ShowWhy(WhyVm vm)
        {
            var veil = new VisualElement();
            veil.name = "WhyVeil";
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.45f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement card = UiKit.Panel(32);
            UiKit.Padding(card, 22, 36);
            card.style.width = Length.Percent(66);
            card.style.maxWidth = 1300;
            card.style.maxHeight = Length.Percent(88);
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.style.flexShrink = 0; // a long title wraps and pushes the rows down; the rows scroll instead (US-122)
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.style.flexShrink = 1;
            Label why = UiKit.Text(vm.Kicker ?? WhyLabel, 30, UiKit.Wood, UiKit.Heading);
            titles.Add(why);
            titles.Add(UiKit.Heading1(vm.Title, 48));
            head.Add(vm.Icon == null ? titles : WithIcon(vm.Icon, 84, titles)); // a Codex card: its group's icon
            if (!string.IsNullOrEmpty(vm.Value))
            {
                Label value = UiKit.Text(vm.Value, 52, UiKit.Ink, UiKit.Heading);
                value.style.marginLeft = 20;
                head.Add(value);
            }
            card.Add(head);
            var scroll = NewScroll();
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 8;
            foreach (WhyRowVm r in vm.Rows)
            {
                if (r.Heading)
                {
                    // A Codex card's section: "Your numbers", "In the real world".
                    Label h = UiKit.Text(r.Label, 32, UiKit.Wood, UiKit.Heading);
                    h.style.marginTop = 12;
                    scroll.Add(h);
                    continue;
                }
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = row.style.paddingBottom = 8;
                row.style.borderBottomWidth = 2;
                row.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
                Label label = UiKit.Text(r.Label, 32, UiKit.Ink, UiKit.Body);
                label.style.flexShrink = 1;
                label.style.whiteSpace = WhiteSpace.Normal;
                row.Add(label);
                if (!string.IsNullOrEmpty(r.Value))
                {
                    bool less = r.Value.EndsWith("less") || r.Value.StartsWith("−");
                    bool more = r.Value.EndsWith("more") || r.Value.StartsWith("+");
                    Label v = UiKit.Text(r.Value, 34, less ? UiKit.Warn : more ? UiKit.GreenDark : UiKit.Ink, UiKit.Heading);
                    v.style.marginLeft = 20;
                    row.Add(v);
                }
                scroll.Add(row);
            }
            if (!string.IsNullOrEmpty(vm.Footnote))
            {
                Label foot = UiKit.Text(vm.Footnote, 30, UiKit.Wood, UiKit.Body);
                foot.style.whiteSpace = WhiteSpace.Normal;
                foot.style.unityFontStyleAndWeight = FontStyle.Italic;
                foot.style.marginTop = 12;
                scroll.Add(foot);
            }
            card.Add(scroll);
            Button ok = UiKit.MakeButton(OkLabel, ActionLook.Primary, () => veil.RemoveFromHierarchy());
            ok.style.alignSelf = Align.Center;
            card.Add(ok);
            veil.Add(card);
            veil.RegisterCallback<ClickEvent>(e => { if (e.target == veil) veil.RemoveFromHierarchy(); });
            _holder.parent.Add(veil);
            card.style.opacity = 0;
            card.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f));
            _tweens.Run(card, 0.2f, Ease.OutBack, k => { card.style.opacity = Mathf.Clamp01(k * 2f); card.style.scale = new Scale(new Vector3(0.92f + 0.08f * k, 0.92f + 0.08f * k, 1f)); });
        }

        /// <summary>The Why? card's button label (from the string table, set by the host).</summary>
        public string OkLabel { get; set; } = "OK";

        public string WhyLabel { get; set; } = "Why?";

        /// <summary>
        /// The While you were away card (spec 008 §3.9, ADR 0043 §8): the rows, "and N more" to see the rest, Collect all, See the
        /// report when a season closed, and Close.
        /// </summary>
        public void Away(AwayCard.Vm vm, string collectLabel, string reportLabel, string closeLabel, string moreLabel, Action collect, Action report)
        {
            var veil = new VisualElement();
            veil.name = "AwayVeil";
            UiKit.Veil(veil);
            veil.style.backgroundColor = new Color(0.16f, 0.1f, 0.05f, 0.4f);
            veil.style.justifyContent = Justify.Center;
            veil.style.alignItems = Align.Center;
            VisualElement card = UiKit.Panel(36);
            UiKit.Padding(card, 22, 36);
            card.style.width = Length.Percent(64);
            card.style.maxHeight = Length.Percent(90);
            card.Add(UiKit.Heading1(vm.Title, 54));
            if (!string.IsNullOrEmpty(vm.Subtitle)) card.Add(UiKit.Text(vm.Subtitle, 30, UiKit.Warn, UiKit.Body));
            var scroll = NewScroll();
            scroll.style.flexShrink = 1;
            foreach (LineVm l in vm.Lines) scroll.Add(Row(l));
            if (vm.More.Count > 0)
            {
                Button more = null;
                more = UiKit.MakeButton(moreLabel, ActionLook.Secondary, () =>
                {
                    int at = scroll.IndexOf(more);
                    scroll.Remove(more);
                    foreach (LineVm l in vm.More) scroll.Insert(at++, Row(l));
                });
                more.style.alignSelf = Align.FlexStart;
                scroll.Add(more);
            }
            card.Add(scroll);
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = 8;
            buttons.style.flexShrink = 0; // the list scrolls; the buttons stay whole inside the card
            if (vm.ReadyCount > 0) buttons.Add(UiKit.MakeButton(collectLabel, ActionLook.Primary, () => { veil.RemoveFromHierarchy(); collect(); }));
            if (vm.ReportPeriod >= 0) buttons.Add(UiKit.MakeButton(reportLabel, ActionLook.Secondary, () => { veil.RemoveFromHierarchy(); report(); }));
            buttons.Add(UiKit.MakeButton(closeLabel, vm.ReadyCount > 0 ? ActionLook.Secondary : ActionLook.Primary, () => veil.RemoveFromHierarchy()));
            card.Add(buttons);
            veil.Add(card);
            _holder.parent.Add(veil);
            card.style.translate = new Translate(0, -120);
            _tweens.Run(card, 0.35f, Ease.OutBack, k => card.style.translate = new Translate(0, -120 * (1f - k)));
        }

        /// <summary>One money figure on a sow row: bold, in its colour, with room after it.</summary>
        private static Label Figure(string text, Color colour)
        {
            Label l = UiKit.Text(text, 30, colour, UiKit.Heading);
            l.style.marginRight = 26;
            return l;
        }

        /// <summary>A good's rendered picture before a row's label, when the row has one (the store).</summary>
        private static VisualElement WithIcon(string goodId, float size, VisualElement label)
        {
            if (goodId == null) return label;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.flexShrink = 1;
            VisualElement pic = UiKit.Icon(IconBank.Good(goodId), size);
            pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            pic.style.marginRight = 16;
            pic.style.flexShrink = 0;
            box.Add(pic);
            box.Add(label);
            return box;
        }

        private VisualElement ActionColumn(ActionVm a)
        {
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.alignItems = Align.FlexStart;
            col.style.maxWidth = 520;
            ActionLook look = a.Kind == ActionKind.Danger ? ActionLook.Danger : a.Kind == ActionKind.Secondary || a.Kind == ActionKind.Open ? ActionLook.Secondary : ActionLook.Primary;
            if (a.Kind == ActionKind.Open) look = ActionLook.Primary;
            VisualElement button = UiKit.MakeButton(a.Label, look, () => { if (OnAction != null) OnAction(a); }, a.Detail, a.Enabled);
            button.style.minWidth = 350; // a column of row buttons lines up instead of ragged widths (panel points)
            col.Add(button);
            if (!a.Enabled && !string.IsNullOrEmpty(a.DisabledReason))
            {
                Label why = UiKit.Text(a.DisabledReason, 27, UiKit.Warn, UiKit.Body);
                why.style.marginTop = 6;
                why.style.maxWidth = 420;
                col.Add(why);
            }
            return col;
        }

        public void Show(SheetVm vm, bool refresh = false)
        {
            var key = new System.Text.StringBuilder(vm.Title).Append('|').Append(vm.Subtitle).Append('|').Append(vm.Tab).Append('|').Append(vm.SwitchOn);
            foreach (LineVm l in vm.Lines)
            {
                if (l.Trend != null) foreach (long v in l.Trend.Values) key.Append('|').Append(v);
                if (l.Pie != null) foreach (PieSliceVm p in l.Pie) key.Append('|').Append(p.Label).Append(p.Percent).Append(p.Value);
                if (l.Tree != null) foreach (var g in l.Tree.Generations) foreach (TreePersonVm p in g) key.Append('|').Append(p.Text);
                key.Append('|').Append(l.Label).Append('=').Append(l.Value).Append(l.Note).Append(l.Badge).Append(l.Tag).Append(l.WhyKey).Append(l.Stars.HasValue ? l.Stars.Value.Solid * 10 + l.Stars.Value.Faded : -1);
                if (l.Buttons != null) foreach (ActionVm b in l.Buttons) key.Append('|').Append(b.Label).Append(b.Detail).Append(b.Count).Append(b.Enabled);
                if (l.Action != null) key.Append('|').Append(l.Action.Label).Append(l.Action.Detail).Append(l.Action.Enabled).Append(l.Action.DisabledReason);
                if (l.Amount != null) key.Append('|').Append(l.Amount.Id).Append(l.Amount.Min).Append(',').Append(l.Amount.Max).Append(',').Append(l.Amount.Mark);
            }
            foreach (ActionVm a in vm.Actions) key.Append('|').Append(a.Label).Append(a.Detail).Append(a.Enabled).Append(a.DisabledReason);
            if (refresh && _typing && IsOpen) return; // the number pad is up: don't rebuild under the player's thumb
            if (!refresh) _amounts.Clear();
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, vm.Icon, Close);
            if (vm.Tabs != null) sheet.Add(TabStrip(vm.Tabs, vm.Tab));
            if (vm.Switch != null)
            {
                // A smaller strip under the tabs (This season | Last season), indented and slimmer so it reads as part of the tab above.
                VisualElement sw = TabStrip(vm.Switch, vm.SwitchOn);
                sw.style.marginLeft = 60; sw.style.marginRight = 46;
                foreach (VisualElement b in sw.Children())
                {
                    b.style.minHeight = 56;
                    b.style.borderBottomWidth = 5;
                    b.style.paddingTop = b.style.paddingBottom = 2;
                    b.Query<Label>().ForEach(l => l.style.fontSize = 30);
                }
                sheet.Add(sw);
            }
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexShrink = 1;
            foreach (LineVm l in vm.Lines) scroll.Add(Row(l));
            scroll.style.flexGrow = 1; // the buttons stay at the bottom of the full-height sheet
            sheet.Add(scroll);
            if (vm.Actions.Count > 0)
            {
                // The buttons stay fixed under the rows: they are what the player came for.
                var actions = new VisualElement { pickingMode = PickingMode.Ignore };
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.flexWrap = Wrap.Wrap;
                actions.style.marginTop = 6;
                actions.style.flexShrink = 0;
                foreach (ActionVm a in vm.Actions) actions.Add(ActionColumn(a));
                sheet.Add(actions);
            }
            Show(sheet, refresh);
        }

        /// <summary>
        /// The crop calendar (spec 020 §3.6b): a column per season (this one tinted, with "now"), a row per rung of the ladder and
        /// fodder, each crop as its picture and name; a tap opens the crop's page. No money on the grid.
        /// </summary>
        public void Show(CropCalendarVm vm, bool refresh = false)
        {
            var key = new System.Text.StringBuilder(vm.Title).Append('|').Append(vm.Now);
            foreach (CalendarRowVm row in vm.Rows)
            {
                key.Append('|').Append(row.Label);
                foreach (var cell in row.Cells) { key.Append('/'); foreach (CalendarCropVm c in cell) key.Append(c.CropId).Append(','); }
            }
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, null, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            var nowTint = new Color(UiKit.Green.r, UiKit.Green.g, UiKit.Green.b, 0.18f);
            var line = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);

            VisualElement GridRow()
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                r.style.flexDirection = FlexDirection.Row;
                r.style.alignItems = Align.Stretch;
                r.style.borderBottomWidth = 2;
                r.style.borderBottomColor = line;
                return r;
            }
            VisualElement Column(VisualElement r, int season)
            {
                var c = new VisualElement { pickingMode = PickingMode.Ignore };
                c.style.flexGrow = 1; c.style.flexBasis = 0; c.style.minWidth = 0;
                c.style.justifyContent = Justify.Center;
                c.style.paddingTop = c.style.paddingBottom = 8;
                c.style.paddingLeft = 8;
                if (season == vm.Now) c.style.backgroundColor = nowTint;
                r.Add(c);
                return c;
            }
            VisualElement RungLabel(VisualElement r, string text)
            {
                var l = UiKit.Text(text ?? "", 30, UiKit.Wood, UiKit.Heading);
                l.style.width = 190; l.style.flexShrink = 0;
                l.style.alignSelf = Align.Center;
                r.Add(l);
                return l;
            }

            VisualElement head = GridRow();
            RungLabel(head, "");
            for (int season = 0; season < vm.Seasons.Count; season++)
            {
                VisualElement c = Column(head, season);
                c.style.alignItems = Align.Center;
                c.Add(UiKit.Text(vm.Seasons[season], 34, season == vm.Now ? UiKit.GreenDark : UiKit.Ink, UiKit.Heading));
                if (season == vm.Now) c.Add(UiKit.Text(vm.NowText, 26, UiKit.GreenDark, UiKit.Heading));
            }
            scroll.Add(head);

            foreach (CalendarRowVm row in vm.Rows)
            {
                VisualElement r = GridRow();
                RungLabel(r, row.Label);
                for (int season = 0; season < row.Cells.Count; season++)
                {
                    VisualElement c = Column(r, season);
                    var cell = row.Cells[season];
                    if (cell.Count == 0)
                    {
                        Label dash = UiKit.Text("–", 30, UiKit.Wood, UiKit.Body);
                        dash.style.alignSelf = Align.Center;
                        c.Add(dash);
                        continue;
                    }
                    bool many = cell.Count > 1; // smaller pictures when crops share a cell (Winter's day rung)
                    foreach (CalendarCropVm crop in cell)
                    {
                        ActionVm open = crop.Open;
                        var b = new Button(() => { if (OnAction != null) OnAction(open); }) { text = "", name = "crop:" + crop.CropId + ":" + season };
                        b.style.flexDirection = FlexDirection.Row;
                        b.style.alignItems = Align.Center;
                        b.style.backgroundColor = Color.clear;
                        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
                        UiKit.Padding(b, many ? 2 : 4, 0);
                        b.style.marginLeft = b.style.marginRight = 0;
                        VisualElement pic = UiKit.Icon(IconBank.Good(crop.Icon), many ? 40 : 60);
                        pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                        pic.style.marginRight = 10;
                        pic.style.flexShrink = 0;
                        b.Add(pic);
                        Label name = UiKit.Text(crop.Name, many ? 28 : 32, UiKit.Ink, UiKit.Heading);
                        name.style.flexShrink = 1;
                        b.Add(name);
                        c.Add(b);
                    }
                }
                scroll.Add(r);
            }
            sheet.Add(scroll);
            Show(sheet, refresh);
        }

        public void Show(SowSheetVm vm, bool refresh = false)
        {
            var key = new System.Text.StringBuilder(vm.Title).Append('|').Append(vm.Subtitle).Append('|').Append(vm.FreeHoursText);
            foreach (FertiliserVm f in vm.Fertilisers) key.Append('|').Append(f.Id).Append(f.Selected);
            foreach (LineVm l in vm.Lines) key.Append('|').Append(l.Label).Append('=').Append(l.Value);
            foreach (ActionVm a in vm.Actions) key.Append('|').Append(a.Label).Append(a.Enabled);
            foreach (CropRowVm r in vm.Rows)
                key.Append('|').Append(r.Name).Append(r.TimeText).Append(r.SuitsText).Append(r.HarvestText)
                   .Append(r.SpendText).Append(r.GetText).Append(r.ProfitText).Append(r.InputsText)
                   .Append(r.LabourText).Append(r.SoilText).Append(r.CostText).Append(r.Sow == null ? "" : r.Sow.Enabled + r.Sow.DisabledReason)
                   .Append(r.ReadyText).Append(r.PerHourText).Append(r.NoteText);
            key.Append('|').Append(vm.AllCrops != null);
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, null, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            if (vm.FreeHoursText != null) scroll.Add(UiKit.Text(vm.FreeHoursText, 32, UiKit.Wood, UiKit.Body));

            // The fertiliser choice (owner, Sprint 6): three equal cards, each with what it does; the chosen one is green, and every
            // crop row below shows its effect on the harvest and its cost.
            Label fertHead = UiKit.Heading1(vm.FertiliserHeading, 36, UiKit.Wood);
            fertHead.style.marginTop = 14;
            scroll.Add(fertHead);
            var chips = new VisualElement { pickingMode = PickingMode.Ignore };
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.marginTop = 6; chips.style.marginBottom = 6;
            foreach (FertiliserVm f in vm.Fertilisers)
            {
                FertiliserVm fert = f;
                Button chip = UiKit.MakeButton(f.Name, f.Selected ? ActionLook.Primary : ActionLook.Secondary, () => { if (OnFertiliser != null) OnFertiliser(fert.Id); }, f.EffectText);
                chip.style.flexGrow = 1; chip.style.flexBasis = 0; chip.style.marginTop = 0; chip.style.marginRight = 14;
                chips.Add(chip);
            }
            scroll.Add(chips);
            Label note = UiKit.Text(vm.NoteText, 27, UiKit.Wood, UiKit.Body);
            note.style.marginTop = 6; note.style.marginBottom = 4;
            scroll.Add(note);
            foreach (LineVm l in vm.Lines) scroll.Add(Row(l)); // a hand's order: the order now, "Let him choose"

            foreach (CropRowVm r in vm.Rows)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.Center;
                card.style.paddingTop = card.style.paddingBottom = 10;
                card.style.borderBottomWidth = 2;
                card.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);

                var name = new VisualElement { pickingMode = PickingMode.Ignore };
                name.style.width = 260; // "Green peas" clear of the figures, "Fodder sorghum" on two lines
                name.style.flexShrink = 0;
                name.style.paddingRight = 14;
                Label cropName = UiKit.Heading1(r.Name, 46);
                cropName.style.whiteSpace = WhiteSpace.Normal;
                name.Add(cropName);
                name.Add(UiKit.Text(r.TimeText, 30, UiKit.Wood, UiKit.Body));
                card.Add(name);

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.style.flexGrow = 1;
                info.style.flexShrink = 1;
                info.style.flexBasis = 0;   // the figures wrap inside; the Sow button keeps its width on the right
                info.style.minWidth = 0;
                {
                    var chipRow = new VisualElement { pickingMode = PickingMode.Ignore };
                    chipRow.style.flexDirection = FlexDirection.Row;
                    chipRow.style.alignItems = Align.Center;
                    var tag = UiKit.Text(r.SuitsText, 28, Color.white, UiKit.Heading);
                    tag.style.backgroundColor = r.Suits == Suits.Good ? UiKit.GreenDark : r.Suits == Suits.Fair ? UiKit.SandDark : UiKit.RedDark;
                    UiKit.Radius(tag, 18);
                    UiKit.Padding(tag, 2, 16);
                    chipRow.Add(tag);
                    Label harvest = UiKit.Text(r.HarvestText, 34, UiKit.Ink, UiKit.Heading);
                    harvest.style.marginLeft = 16;
                    chipRow.Add(harvest);
                    info.Add(chipRow);
                    // When it's ready if sown now (spec 020 §3.6): the ladder's first question.
                    if (r.ReadyText != null)
                    {
                        Label ready = UiKit.Text(r.ReadyText, 30, UiKit.GreenDark, UiKit.Heading);
                        ready.style.marginTop = 4;
                        info.Add(ready);
                    }
                    // Money in, money out (owner, Sprint 6), each in its own colour; then what's left and what each hour of growing
                    // earns, with its ? (spec 020 §3.6, layout A).
                    var money = new VisualElement { pickingMode = PickingMode.Ignore };
                    money.style.flexDirection = FlexDirection.Row;
                    money.style.flexWrap = Wrap.Wrap;
                    money.style.marginTop = 4;
                    money.Add(Figure(r.SpendText, UiKit.RedDark));
                    money.Add(Figure(r.GetText, UiKit.GreenDark));
                    info.Add(money);
                    var profitRow = new VisualElement { pickingMode = PickingMode.Ignore };
                    profitRow.style.flexDirection = FlexDirection.Row;
                    profitRow.style.alignItems = Align.Center;
                    profitRow.style.marginTop = 2;
                    Label profit = Figure(r.PerHourText == null ? r.ProfitText : r.ProfitText + " · " + r.PerHourText, r.Profit >= 0 ? UiKit.Ink : UiKit.Warn);
                    profit.style.whiteSpace = WhiteSpace.Normal;
                    profit.style.flexShrink = 1;
                    profit.style.marginRight = 0;
                    profitRow.Add(profit);
                    Button perHourWhy = AddWhyChip(profitRow, r.PerHourWhyKey, r.PerHourWhyId);
                    if (perHourWhy != null) perHourWhy.name = "why:" + r.PerHourWhyKey + ":" + r.CropId; // one per crop, for scripted touch
                    info.Add(profitRow);
                    if (r.NoteText != null)
                    {
                        Label cropNote = UiKit.Text(r.NoteText, 27, UiKit.Wood, UiKit.Body);
                        cropNote.style.whiteSpace = WhiteSpace.Normal;
                        info.Add(cropNote);
                    }
                    info.Add(UiKit.Text(r.InputsText + " · " + r.LabourText, 27, UiKit.Wood, UiKit.Body));
                    info.Add(UiKit.Text(r.SoilText, 27, r.SoilFalls ? UiKit.Warn : UiKit.GreenDark, UiKit.Body));
                }
                card.Add(info);

                VisualElement sow = ActionColumn(r.Sow);
                sow.style.flexShrink = 0;
                sow.style.marginLeft = 12;
                card.Add(sow);
                scroll.Add(card);
            }
            // Every crop by season is on the crop calendar (spec 020 §3.6): a link at the foot, ← comes back.
            if (vm.AllCrops != null)
            {
                ActionVm all = vm.AllCrops;
                var link = new Button(() => { if (OnAction != null) OnAction(all); }) { text = "", name = "allCrops" };
                link.style.alignSelf = Align.FlexEnd;
                link.style.backgroundColor = Color.clear;
                link.style.borderTopWidth = link.style.borderBottomWidth = link.style.borderLeftWidth = link.style.borderRightWidth = 0;
                link.style.marginTop = 10; link.style.marginBottom = 10;
                UiKit.Padding(link, 12, 18); // a finger-sized target around the words
                link.Add(UiKit.Text(all.Label, 34, UiKit.GreenDark, UiKit.Heading));
                scroll.Add(link);
            }
            sheet.Add(scroll);
            if (vm.Actions.Count > 0)
            {
                var actions = new VisualElement { pickingMode = PickingMode.Ignore };
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.marginTop = 6;
                actions.style.flexShrink = 0;
                foreach (ActionVm a in vm.Actions) actions.Add(ActionColumn(a));
                sheet.Add(actions);
            }
            Show(sheet, refresh);
        }
    }
}
