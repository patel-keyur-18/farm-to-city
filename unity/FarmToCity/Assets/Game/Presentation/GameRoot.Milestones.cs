using System.Collections.Generic;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Sim.Core.Events;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// The wealth milestones' toast (spec 019 §3.6): after a close, a slim toast in the Codex toast's place says which were reached for 6
    /// seconds, then slides away. It waits while a sheet, a card or a letter is open, and for the Codex toast. A tap opens Your wealth, where the
    /// list is. Nothing stays on screen and nothing is given: the list keeps what was reached.
    /// </summary>
    public sealed partial class GameRoot
    {
        private const int MilestoneToastMs = 6000;

        private VisualElement _mileToast;
        private Label _mileKicker, _mileText;
        private MilestoneToastVm _mileVm;
        private IVisualElementScheduledItem _mileHide, _mileWait;
        private readonly List<MilestoneReached> _milePending = new List<MilestoneReached>();

        private void BuildMilestoneToast()
        {
            _mileToast = UiKit.Panel(26);
            _mileToast.name = "MilestoneToast";
            _mileToast.style.flexDirection = FlexDirection.Row;
            _mileToast.style.alignItems = Align.Center;
            UiKit.Border(_mileToast, 4, UiKit.Saffron);
            UiKit.Padding(_mileToast, 8, 24);
            _mileToast.style.minWidth = 820;
            VisualElement icon = UiKit.Icon(IconBank.Star(2), 96);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            icon.style.marginRight = 18;
            icon.pickingMode = PickingMode.Ignore;
            _mileToast.Add(icon);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.flexGrow = 1;
            _mileKicker = UiKit.Text("", 28, UiKit.Saffron, UiKit.Body);
            _mileText = UiKit.Text("", 40, UiKit.Ink, UiKit.Heading);
            words.Add(_mileKicker);
            words.Add(_mileText);
            _mileToast.Add(words);
            Label more = UiKit.Text("›", 56, UiKit.Ink, UiKit.Heading);
            more.style.marginLeft = 20;
            more.pickingMode = PickingMode.Ignore;
            _mileToast.Add(more);
            _mileToast.RegisterCallback<ClickEvent>(_ => OnMilestoneToastTap());
            var row = new VisualElement { pickingMode = PickingMode.Ignore, name = "MilestoneToastRow" };
            row.style.position = Position.Absolute;
            row.style.left = 0; row.style.right = 0; row.style.bottom = 40;
            row.style.alignItems = Align.Center;
            row.Add(_mileToast);
            _mileToast.style.display = DisplayStyle.None;
            _safeRoot.Add(row);
        }

        /// <summary>After each batch of events: the milestones reached in it join the toast that waits.</summary>
        private void MilestoneAfter(List<SimEvent> events)
        {
            foreach (SimEvent e in events) if (e is MilestoneReached m) _milePending.Add(m);
            if (_milePending.Count > 0 && _mileWait == null) _mileWait = _safeRoot.schedule.Execute(ShowPendingMilestones).Every(500);
        }

        private bool MilestoneToastShowing { get { return _mileToast != null && _mileToast.style.display != DisplayStyle.None; } }

        private void ShowPendingMilestones()
        {
            if (_milePending.Count == 0 || _letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _sheet.WhyOpen || _placeBar != null) return;
            if (_codexToast != null && _codexToast.style.display != DisplayStyle.None) return; // one toast at a time
            _ctx.LocalNow = System.DateTime.Now;
            MilestoneToastVm vm = MilestoneToastVm.Build(_ctx, new List<MilestoneReached>(_milePending));
            _milePending.Clear();
            _mileWait.Pause();
            _mileWait = null;
            if (vm == null || _mileToast == null) return;
            _mileVm = vm;
            _mileKicker.text = vm.Kicker;
            _mileText.text = vm.Text;
            _mileToast.style.display = DisplayStyle.Flex;
            _mileToast.style.opacity = 1f;
            if (_tweens.ReducedMotion) _mileToast.style.translate = new Translate(0, 0);
            else _tweens.Run(_mileToast, 0.35f, Ease.OutBack, k => _mileToast.style.translate = new Translate(0, 160 * (1f - k)));
            if (_mileHide != null) _mileHide.Pause();
            _mileHide = _safeRoot.schedule.Execute(HideMilestoneToast).StartingIn(MilestoneToastMs);
        }

        private void HideMilestoneToast()
        {
            if (!MilestoneToastShowing) return;
            if (_tweens.ReducedMotion) { _mileToast.style.display = DisplayStyle.None; return; }
            _tweens.Run(_mileToast, 0.3f, Ease.Linear, k => { _mileToast.style.translate = new Translate(0, 160 * k); _mileToast.style.opacity = 1f - k; },
                () => _mileToast.style.display = DisplayStyle.None);
        }

        /// <summary>A sheet, card, letter or <i>Why?</i> opened while the toast showed: it goes at once rather than cover the sheet's buttons.</summary>
        private void MilestoneCovered()
        {
            if (!MilestoneToastShowing) return;
            if (!(_letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _sheet.WhyOpen)) return;
            if (_mileHide != null) _mileHide.Pause();
            _mileToast.style.display = DisplayStyle.None;
        }

        private void OnMilestoneToastTap()
        {
            if (_mileVm == null) return;
            SheetRequest r = _mileVm.Opens;
            if (_mileHide != null) _mileHide.Pause();
            _mileToast.style.display = DisplayStyle.None;
            OpenSheet(r);
        }
    }
}
