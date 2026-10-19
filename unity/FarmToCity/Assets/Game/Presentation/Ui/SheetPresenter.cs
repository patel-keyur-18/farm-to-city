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
    /// The bottom sheet (spec 008 §3.2): at most one open, up to 60% of the height so the map stays visible above, sliding up and
    /// down. It draws <see cref="SheetVm"/> and <see cref="SowSheetVm"/> and hands a pressed button's command to the host; a greyed
    /// button shows the sim's reason under it.
    /// </summary>
    public sealed class SheetPresenter
    {
        private readonly Tweens _tweens;
        private readonly VisualElement _holder;
        private VisualElement _sheet;
        private bool _closing;

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
            root.Add(_holder);
        }

        private VisualElement NewSheet(string title, string subtitle, string icon, Action close)
        {
            VisualElement panel = UiKit.Panel(36);
            panel.style.width = Length.Percent(78);
            panel.style.maxWidth = 1500;
            panel.style.maxHeight = Length.Percent(60);
            panel.style.marginBottom = 14;
            panel.style.flexShrink = 0;
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
                case "hut": return SpriteBank.Hut;
                case "store": return IconBank.Sack;
                case "garden": return SpriteBank.Bed(null, Sim.View.Views.CropStage.None);
                case "land": return SpriteBank.Sign;
                case "field": return SpriteBank.SoilTile;
                case "tree": return SpriteBank.Tree("mango", TreeStage.Mature);
                case "banana": case "papaya": case "guava": case "mango": return SpriteBank.Tree(icon, TreeStage.Mature);
                case "potato": case "radish": case "wheat": case "rice": case "lentils": return SpriteBank.Crop(icon, CropStage.Growing3);
                default: return SpriteBank.Bed(icon, CropStage.Growing3);
            }
        }

        private void Show(VisualElement sheet)
        {
            bool had = _sheet != null;
            if (had) _holder.Remove(_sheet);
            _sheet = sheet;
            _closing = false;
            _holder.Add(sheet);
            if (!had)
            {
                sheet.style.translate = new Translate(0, Length.Percent(110));
                _tweens.Run(sheet, 0.25f, Ease.OutCubic, k => sheet.style.translate = new Translate(0, Length.Percent((1f - k) * 110f)));
            }
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

        private static VisualElement Row(string label, string value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 2;
            row.style.borderBottomColor = new Color(UiKit.Wood.r, UiKit.Wood.g, UiKit.Wood.b, 0.25f);
            row.Add(UiKit.Text(label, 31, UiKit.Ink, UiKit.Body));
            Label v = UiKit.Text(value, 33, UiKit.Ink, UiKit.Heading);
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            v.style.flexShrink = 1;
            row.Add(v);
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

        public void Show(SheetVm vm)
        {
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, vm.Icon, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
            scroll.style.flexShrink = 1;
            foreach (LineVm l in vm.Lines) scroll.Add(Row(l.Label, l.Value));
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
            Show(sheet);
        }

        public void Show(SowSheetVm vm)
        {
            VisualElement sheet = NewSheet(vm.Title, vm.Subtitle, null, Close);
            var scroll = NewScroll();
            scroll.style.marginTop = 8;
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
            Show(sheet);
        }
    }
}
