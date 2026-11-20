using System;
using System.Collections.Generic;
using Game.Presentation.Map;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Ui
{
    /// <summary>
    /// The overlays' interface (US-153, spec 024 §3.8, as the owner approved it in Preview 6): the chooser beside the Overlays button, the key at
    /// the bottom, and the small card a tap opens. It draws view-models and knows no rules; the map's tint is <see cref="MapPresenter.ShowOverlay"/>.
    /// </summary>
    public sealed class OverlayPresenter
    {
        private readonly VisualElement _root;
        private VisualElement _chooser, _key, _card;

        public Action<OverlayKind> OnPick { get; set; }
        public Action OnOpenAcre { get; set; }
        public Action<string, long> OnWhy { get; set; }

        public OverlayPresenter(VisualElement root) { _root = root; }

        public bool ChooserOpen { get { return _chooser != null; } }
        public bool CardOpen { get { return _card != null; } }

        // ---- the chooser: five choices, one at a time ----

        public void ShowChooser(List<OverlayChoiceVm> choices)
        {
            HideChooser();
            _chooser = UiKit.Panel(30);
            _chooser.name = "OverlayChooser";
            UiKit.Padding(_chooser, 4, 16);
            _chooser.style.position = Position.Absolute;
            _chooser.style.right = 196; _chooser.style.bottom = 196;
            _chooser.style.width = 640;
            foreach (OverlayChoiceVm c in choices)
            {
                OverlayChoiceVm choice = c;
                VisualElement row = new VisualElement { name = "overlay:" + c.Kind };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 6;
                row.style.backgroundColor = c.Muted ? UiKit.Grey : UiKit.Sand;
                UiKit.Radius(row, 26);
                UiKit.Border(row, 4, c.Muted ? UiKit.GreyDark : UiKit.SandDark);
                row.style.borderBottomWidth = 8;
                UiKit.Padding(row, 2, 14);
                VisualElement swatch = new VisualElement { pickingMode = PickingMode.Ignore };
                swatch.style.width = 46; swatch.style.height = 46; swatch.style.flexShrink = 0;
                swatch.style.backgroundColor = OverlayColours.Step(c.Kind, 2, !c.Muted);
                UiKit.Radius(swatch, 14);
                UiKit.Border(swatch, 3, new Color(0f, 0f, 0f, 0.3f));
                row.Add(swatch);
                var words = new VisualElement { pickingMode = PickingMode.Ignore };
                words.style.marginLeft = 18; words.style.flexShrink = 1;
                Label name = UiKit.Text(c.Name, 34, c.Muted ? UiKit.GreyDark : UiKit.Ink, UiKit.Heading);
                Label sub = UiKit.Text(c.Sub, 22, c.Muted ? UiKit.GreyDark : UiKit.Wood, UiKit.Body);
                sub.style.whiteSpace = WhiteSpace.NoWrap;
                words.Add(name); words.Add(sub);
                row.Add(words);
                row.RegisterCallback<ClickEvent>(_ => { if (OnPick != null) OnPick(choice.Kind); });
                _chooser.Add(row);
            }
            _root.Add(_chooser);
        }

        public void HideChooser()
        {
            if (_chooser == null) return;
            _chooser.RemoveFromHierarchy();
            _chooser = null;
        }

        // ---- the key: one hue in five steps, with the numbers ----

        public void ShowKey(OverlayVm vm)
        {
            HideKey();
            _key = new VisualElement { name = "OverlayKey", pickingMode = PickingMode.Ignore };
            _key.style.position = Position.Absolute;
            _key.style.left = 0; _key.style.right = 0; _key.style.bottom = 28;
            _key.style.alignItems = Align.Center;
            VisualElement panel = UiKit.Panel(28);
            panel.pickingMode = PickingMode.Ignore;
            UiKit.Padding(panel, 14, 24);
            panel.Add(UiKit.Heading1(vm.Title, 38));
            if (vm.Labels.Count > 0)
            {
                var steps = new VisualElement { pickingMode = PickingMode.Ignore };
                steps.style.flexDirection = FlexDirection.Row;
                steps.style.marginTop = 6;
                for (int i = 0; i < vm.Labels.Count; i++)
                {
                    var col = new VisualElement { pickingMode = PickingMode.Ignore };
                    col.style.width = 150;
                    var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                    bar.style.height = 34;
                    bar.style.backgroundColor = OverlayColours.Step(vm.Kind, i, vm.Labels.Count > 0);
                    UiKit.Border(bar, 2, new Color(0f, 0f, 0f, 0.35f));
                    bar.style.borderLeftWidth = i == 0 ? 2 : 0;
                    col.Add(bar);
                    Label l = UiKit.Text(vm.Labels[i], 26, UiKit.Ink, UiKit.Body);
                    l.style.unityTextAlign = TextAnchor.UpperCenter;
                    l.style.whiteSpace = WhiteSpace.NoWrap;
                    col.Add(l);
                    steps.Add(col);
                }
                panel.Add(steps);
            }
            Label note = UiKit.Text(vm.Note, 26, UiKit.Wood, UiKit.Body);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.maxWidth = 750;
            note.style.marginTop = 4;
            panel.Add(note);
            _key.Add(panel);
            _root.Add(_key);
        }

        public void HideKey()
        {
            if (_key == null) return;
            _key.RemoveFromHierarchy();
            _key = null;
        }

        // ---- the tap card: the acre's number, a ? for its Why?, Open the acre ----

        /// <summary>Shows the card for an acre, near <paramref name="panelPoint"/> (the acre's place in panel coordinates); <paramref name="canOpen"/> adds Open the acre.</summary>
        public void ShowCard(OverlayCardVm vm, Vector2 panelPoint, bool canOpen)
        {
            HideCard();
            _card = UiKit.Panel(28);
            _card.name = "OverlayCard";
            UiKit.Padding(_card, 18, 26);
            _card.style.position = Position.Absolute;
            const float Width = 700f;
            _card.style.width = Width;
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            Label title = UiKit.Heading1(vm.Title, 40);
            title.style.flexGrow = 1; title.style.flexShrink = 1;
            title.style.whiteSpace = WhiteSpace.Normal;
            top.Add(title);
            if (vm.WhyKey != null)
            {
                string key = vm.WhyKey; long id = vm.WhyId;
                var chip = new Button(() => { if (OnWhy != null) OnWhy(key, id); }) { text = "" };
                chip.name = "why:" + key;
                chip.style.width = 64; chip.style.height = 64;
                chip.style.marginLeft = 14; chip.style.flexShrink = 0;
                chip.style.backgroundColor = UiKit.CreamDark;
                UiKit.Radius(chip, 32);
                UiKit.Border(chip, 3, UiKit.Wood);
                chip.style.alignItems = Align.Center; chip.style.justifyContent = Justify.Center;
                UiKit.Padding(chip, 0, 0);
                Label q = UiKit.Text("?", 42, UiKit.Wood, UiKit.Heading);
                q.style.marginTop = -4;
                chip.Add(q);
                top.Add(chip);
            }
            _card.Add(top);
            if (vm.Value != null) _card.Add(UiKit.Text(vm.Value, 68, UiKit.WoodBar, UiKit.Heading));
            Label line = UiKit.Text(vm.Line, 30, UiKit.Ink, UiKit.Body);
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.marginTop = 2; line.style.marginBottom = 8;
            _card.Add(line);
            if (canOpen)
            {
                Button open = UiKit.MakeButton(vm.OpenLabel, ActionLook.Primary, () => { if (OnOpenAcre != null) OnOpenAcre(); });
                open.name = "OpenAcre";
                open.style.marginRight = 0;
                _card.Add(open);
            }
            _root.Add(_card);

            // Beside the acre, never over it: to the right of an acre in the left half of the screen, to the left of one in the right half, level with it
            // and kept above the key and inside the screen.
            Rect r = _root.layout;
            float w = r.width > 0 ? r.width : 1920f, h = r.height > 0 ? r.height : 1080f;
            const float KeyTop = 330f; // the key's height and its gap from the screen's bottom: the card stays above it
            _card.style.left = panelPoint.x < w * 0.5f ? Mathf.Min(panelPoint.x + 90f, w - Width - 24f) : Mathf.Max(24f, panelPoint.x - 90f - Width);
            _card.style.top = Mathf.Clamp(panelPoint.y - 260f, 24f, Mathf.Max(24f, h - KeyTop - 520f)); // until its real height is known
            VisualElement card = _card;
            card.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float tall = card.layout.height;
                if (tall <= 0f) return;
                card.style.top = Mathf.Clamp(panelPoint.y - tall / 2f, 24f, Mathf.Max(24f, h - KeyTop - tall));
            });
        }

        public void HideCard()
        {
            if (_card == null) return;
            _card.RemoveFromHierarchy();
            _card = null;
        }

        public void HideAll() { HideChooser(); HideKey(); HideCard(); }
    }
}
