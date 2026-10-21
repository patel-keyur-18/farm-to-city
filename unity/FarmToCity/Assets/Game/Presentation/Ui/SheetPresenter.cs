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
            var x = new Button(close) { text = "" };
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
                case "people": return SpriteBank.Rendered("person_founder_front");
                case "person_hand_m": case "person_hand_f": return SpriteBank.Rendered(icon + "_front");
                case "store": case "sack": return IconBank.Sack;
                case "gear": return IconBank.Gear;
                case "coin": return IconBank.Coin;
                case "menu": return IconBank.Ledger;
                case "alert": return IconBank.Bell;
                case "cart": return IconBank.Cart;
                case "letter": return IconBank.Letter;
                case "person_founder": return SpriteBank.Rendered("person_founder_front");
                case "seed_radish": case "seed_wheat": case "seed_rice": case "seed_potato": case "seed_lentils": case "seed_spinach": case "seed_coriander":
                case "compost": case "fertiliser_chemical": case "spray": case "spinach": case "coriander":
                    return IconBank.Good(icon);
                case "garden": return SpriteBank.Bed(null, Sim.View.Views.CropStage.None);
                case "land": return SpriteBank.Sign;
                case "field": return SpriteBank.SoilTile;
                case "tree": return SpriteBank.Tree("mango", TreeStage.Mature);
                case "banana": case "papaya": case "guava": case "mango": return SpriteBank.Tree(icon, TreeStage.Mature);
                case "potato": case "radish": case "wheat": case "rice": case "lentils": return SpriteBank.Crop(icon, CropStage.Growing3);
                default:
                    if (icon.StartsWith("weather_", StringComparison.Ordinal)) return IconBank.Weather(icon.Substring(8));
                    return SpriteBank.Bed(icon, CropStage.Growing3);
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
            veil.style.position = Position.Absolute;
            veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
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

        /// <summary>A card over everything (spec 007 §7, ADR 0041): a title, a sentence and its buttons; any button closes it.</summary>
        public void Card(CardVm vm, Action<string> onButton)
        {
            var veil = new VisualElement { name = "Veil" };
            veil.style.position = Position.Absolute;
            veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
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
            buttons.style.marginTop = 18;
            foreach (CardButton b in vm.Buttons)
            {
                ActionLook look = b.Kind == ActionKind.Danger ? ActionLook.Danger : b.Kind == ActionKind.Primary ? ActionLook.Primary : ActionLook.Secondary;
                string id = b.Id;
                Button button = UiKit.MakeButton(b.Label, look, () => { veil.RemoveFromHierarchy(); onButton(id); });
                button.name = "card:" + id;
                buttons.Add(button);
            }
            card.Add(buttons);
            veil.Add(card);
            _holder.parent.Add(veil);
            card.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            _tweens.Run(card, 0.2f, Ease.OutBack, k => card.style.scale = new Scale(new Vector3(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f)));
        }

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
            veil.style.position = Position.Absolute;
            veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
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
            if (line.IsHeader)
            {
                // A section heading: a wood title with a rule under it, a little air above.
                Label h = UiKit.Heading1(line.Label, 40, UiKit.Wood);
                h.style.marginTop = 18;
                h.style.borderBottomWidth = 3;
                h.style.borderBottomColor = UiKit.Wood;
                return h;
            }
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 2;
            row.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
            Tappable(row, line);
            if (line.Action == null)
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
            text.Add(UiKit.Heading1(line.Label, 40));
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
            VisualElement left = WithIcon(line.Icon, 84, text);
            left.style.flexGrow = 1; // the ? chip and the button sit together on the right
            row.Add(left);
            AddWhyChip(row, line);
            VisualElement button = ActionColumn(line.Action);
            button.style.alignItems = Align.FlexEnd;
            button.style.flexShrink = 0;
            row.Add(button);
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
            var chip = new Button(() => { if (OnWhy != null) OnWhy(line.WhyKey, line.WhyId); }) { text = "" };
            chip.name = "why:" + line.WhyKey;
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
        }

        /// <summary>
        /// A Why? card stacked over the sheet (spec 008 §3.6, §3.12): the number, one row per cause in words with its effect, and the
        /// lesson. A tap anywhere closes it.
        /// </summary>
        public void ShowWhy(WhyVm vm)
        {
            var veil = new VisualElement();
            veil.name = "WhyVeil";
            veil.style.position = Position.Absolute;
            veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
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
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.style.flexShrink = 1;
            Label why = UiKit.Text(WhyLabel, 30, UiKit.Wood, UiKit.Heading);
            titles.Add(why);
            titles.Add(UiKit.Heading1(vm.Title, 48));
            head.Add(titles);
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
            veil.style.position = Position.Absolute;
            veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
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
            var key = new System.Text.StringBuilder(vm.Title).Append('|').Append(vm.Subtitle);
            foreach (LineVm l in vm.Lines)
            {
                key.Append('|').Append(l.Label).Append('=').Append(l.Value).Append(l.WhyKey).Append(l.Stars.HasValue ? l.Stars.Value.Solid * 10 + l.Stars.Value.Faded : -1);
                if (l.Action != null) key.Append('|').Append(l.Action.Label).Append(l.Action.Detail).Append(l.Action.Enabled).Append(l.Action.DisabledReason);
            }
            foreach (ActionVm a in vm.Actions) key.Append('|').Append(a.Label).Append(a.Detail).Append(a.Enabled).Append(a.DisabledReason);
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, vm.Icon, Close);
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

        public void Show(SowSheetVm vm, bool refresh = false)
        {
            var key = new System.Text.StringBuilder(vm.Title).Append('|').Append(vm.Subtitle).Append('|').Append(vm.FreeHoursText);
            foreach (FertiliserVm f in vm.Fertilisers) key.Append('|').Append(f.Id).Append(f.Selected);
            foreach (CropRowVm r in vm.Rows)
                key.Append('|').Append(r.Name).Append(r.TimeText).Append(r.SowableNow).Append(r.SeasonNote).Append(r.SuitsText).Append(r.HarvestText)
                   .Append(r.SpendText).Append(r.GetText).Append(r.ProfitText).Append(r.InputsText)
                   .Append(r.LabourText).Append(r.CostText).Append(r.Sow == null ? "" : r.Sow.Enabled + r.Sow.DisabledReason);
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, null, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.Add(UiKit.Text(vm.FreeHoursText, 32, UiKit.Wood, UiKit.Body));

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

            foreach (CropRowVm r in vm.Rows)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.style.flexDirection = FlexDirection.Row;
                card.style.alignItems = Align.Center;
                card.style.paddingTop = card.style.paddingBottom = 10;
                card.style.borderBottomWidth = 2;
                card.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
                card.style.opacity = r.SowableNow ? 1f : 0.62f;

                var name = new VisualElement { pickingMode = PickingMode.Ignore };
                name.style.width = 230;
                name.style.flexShrink = 0;
                name.Add(UiKit.Heading1(r.Name, 46));
                name.Add(UiKit.Text(r.TimeText, 30, UiKit.Wood, UiKit.Body));
                card.Add(name);

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.style.flexGrow = 1;
                info.style.flexShrink = 1;
                info.style.flexBasis = 0;   // the figures wrap inside; the Sow button keeps its width on the right
                info.style.minWidth = 0;
                if (r.SowableNow)
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
                    // Money in, money out, what's left (owner, Sprint 6): three figures side by side, each in its own colour.
                    var money = new VisualElement { pickingMode = PickingMode.Ignore };
                    money.style.flexDirection = FlexDirection.Row;
                    money.style.flexWrap = Wrap.Wrap;
                    money.style.marginTop = 4;
                    money.Add(Figure(r.SpendText, UiKit.RedDark));
                    money.Add(Figure(r.GetText, UiKit.GreenDark));
                    money.Add(Figure(r.ProfitText, r.Profit >= 0 ? UiKit.Ink : UiKit.Warn));
                    info.Add(money);
                    info.Add(UiKit.Text(r.InputsText + " · " + r.LabourText, 27, UiKit.Wood, UiKit.Body));
                }
                else info.Add(UiKit.Text(r.SeasonNote, 32, UiKit.Warn, UiKit.Body));
                card.Add(info);

                if (r.SowableNow)
                {
                    VisualElement sow = ActionColumn(r.Sow);
                    sow.style.flexShrink = 0;
                    sow.style.marginLeft = 12;
                    card.Add(sow);
                }
                scroll.Add(card);
            }
            sheet.Add(scroll);
            Show(sheet, refresh);
        }
    }
}
