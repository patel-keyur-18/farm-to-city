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
                case "store": return IconBank.Sack;
                case "gear": return IconBank.Gear;
                case "garden": return SpriteBank.Bed(null, Sim.View.Views.CropStage.None);
                case "land": return SpriteBank.Sign;
                case "field": return SpriteBank.SoilTile;
                case "tree": return SpriteBank.Tree("mango", TreeStage.Mature);
                case "banana": case "papaya": case "guava": case "mango": return SpriteBank.Tree(icon, TreeStage.Mature);
                case "potato": case "radish": case "wheat": case "rice": case "lentils": return SpriteBank.Crop(icon, CropStage.Growing3);
                default: return SpriteBank.Bed(icon, CropStage.Growing3);
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
            var veil = new VisualElement();
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
            var veil = new VisualElement();
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
            if (line.Action == null)
            {
                row.Add(UiKit.Text(line.Label, 31, UiKit.Ink, UiKit.Body));
                Label v = UiKit.Text(line.Value, 33, UiKit.Ink, UiKit.Heading);
                v.style.unityTextAlign = TextAnchor.MiddleRight;
                v.style.flexShrink = 1;
                // A short value never splits ("Goo / d"): on a phone's fractional UI scale the measured width can round a hair short.
                if (line.Value != null && line.Value.Length <= 24 && line.Value.IndexOf('\n') < 0) { v.style.whiteSpace = WhiteSpace.NoWrap; v.style.flexShrink = 0; }
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
            row.Add(text);
            VisualElement button = ActionColumn(line.Action);
            button.style.alignItems = Align.FlexEnd;
            button.style.flexShrink = 0;
            row.Add(button);
            return row;
        }

        private VisualElement ActionColumn(ActionVm a)
        {
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.alignItems = Align.FlexStart;
            col.style.maxWidth = 520;
            ActionLook look = a.Kind == ActionKind.Danger ? ActionLook.Danger : a.Kind == ActionKind.Secondary || a.Kind == ActionKind.Open ? ActionLook.Secondary : ActionLook.Primary;
            if (a.Kind == ActionKind.Open) look = ActionLook.Primary;
            col.Add(UiKit.MakeButton(a.Label, look, () => { if (OnAction != null) OnAction(a); }, a.Detail, a.Enabled));
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
                key.Append('|').Append(l.Label).Append('=').Append(l.Value).Append(l.Stars.HasValue ? l.Stars.Value.Solid * 10 + l.Stars.Value.Faded : -1);
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
                key.Append('|').Append(r.Name).Append(r.TimeText).Append(r.SowableNow).Append(r.SeasonNote).Append(r.SuitsText).Append(r.EarnsText)
                   .Append(r.LabourText).Append(r.CostText).Append(r.Sow == null ? "" : r.Sow.Enabled + r.Sow.DisabledReason);
            if (Unchanged(key.ToString(), refresh)) return;
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, null, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.Add(UiKit.Text(vm.FreeHoursText, 32, UiKit.Wood, UiKit.Body));

            var chips = new VisualElement { pickingMode = PickingMode.Ignore };
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.marginTop = 10; chips.style.marginBottom = 6;
            foreach (FertiliserVm f in vm.Fertilisers)
            {
                FertiliserVm fert = f;
                Button chip = UiKit.MakeButton(f.Name, f.Selected ? ActionLook.Primary : ActionLook.Secondary, () => { if (OnFertiliser != null) OnFertiliser(fert.Id); });
                chip.style.minHeight = 64; chip.style.minWidth = 160; chip.style.marginTop = 0;
                chips.Add(chip);
            }
            scroll.Add(chips);

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
                name.style.width = 330;
                name.Add(UiKit.Heading1(r.Name, 46));
                name.Add(UiKit.Text(r.TimeText, 30, UiKit.Wood, UiKit.Body));
                card.Add(name);

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.style.flexGrow = 1;
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
                    Label earns = UiKit.Text(r.EarnsText, 36, UiKit.Ink, UiKit.Heading);
                    earns.style.marginLeft = 16;
                    chipRow.Add(earns);
                    info.Add(chipRow);
                    info.Add(UiKit.Text(r.LabourText + " · " + r.CostText, 28, UiKit.Wood, UiKit.Body));
                }
                else info.Add(UiKit.Text(r.SeasonNote, 32, UiKit.Warn, UiKit.Body));
                card.Add(info);

                if (r.SowableNow) card.Add(ActionColumn(r.Sow));
                scroll.Add(card);
            }
            sheet.Add(scroll);
            Show(sheet, refresh);
        }
    }
}
