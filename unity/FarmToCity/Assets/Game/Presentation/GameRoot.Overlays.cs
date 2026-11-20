using Game.Presentation.Map;
using Game.Presentation.Ui;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// The map overlays (US-153, spec 024 §3.8): the Overlays button opens a chooser, one overlay is up at a time, a tap on an acre opens a small
    /// card instead of the acre's sheet, and opening any sheet ends the overlay. An overlay only reads the sim: it sends no command and costs no time.
    /// </summary>
    public sealed partial class GameRoot
    {
        private OverlayPresenter _overlayUi;
        private OverlayKind _overlay;
        private OverlayVm _overlayVm;
        private int _overlayTile = -1;
        private long _overlayBuilt;

        /// <summary>An overlay is read again at most this often while it is up (fertility and happiness move slowly).</summary>
        private const long OverlayEverySeconds = 5;

        private void BuildOverlays()
        {
            _overlayUi = new OverlayPresenter(_safeRoot) { OnPick = ShowOverlay, OnOpenAcre = OpenOverlayAcre, OnWhy = ShowWhy };
        }

        private void OnOverlaysButton()
        {
            if (_overlay != OverlayKind.None) { EndOverlay(); return; }
            if (_overlayUi.ChooserOpen) { _overlayUi.HideChooser(); return; }
            if (_sheet.IsOpen) _sheet.Close();
            _overlayUi.ShowChooser(OverlayView.Choices(_ctx));
        }

        private void ShowOverlay(OverlayKind kind)
        {
            _overlayUi.HideChooser();
            _overlayUi.HideCard();
            _overlay = kind;
            _overlayTile = -1;
            _map.ClearSelection();
            ReadOverlay();
            _overlayUi.ShowKey(_overlayVm);
            _hud.SetOverlayOpen(true);
        }

        private void ReadOverlay()
        {
            _ctx.LocalNow = System.DateTime.Now;
            _overlayVm = OverlayView.Build(_ctx, _overlay);
            _overlayBuilt = WallNow();
            _map.ShowOverlay(_overlayVm);
        }

        /// <summary>Ends the overlay: the tint, the key, the card and the green button.</summary>
        private void EndOverlay()
        {
            if (_overlay == OverlayKind.None && !_overlayUi.ChooserOpen) return;
            _overlay = OverlayKind.None;
            _overlayTile = -1;
            _overlayUi.HideAll();
            _map.HideOverlay();
            _map.ClearSelection();
            _hud.SetOverlayOpen(false);
        }

        /// <summary>Each refresh while an overlay is up: read it again every few seconds, and end it when the Township is gone (a restored snapshot).</summary>
        private void RefreshOverlay()
        {
            if (_overlay == OverlayKind.None) return;
            if (!OverlayView.Available(Sim)) { EndOverlay(); return; }
            if (WallNow() - _overlayBuilt >= OverlayEverySeconds) ReadOverlay();
        }

        /// <summary>A tap on the map while the chooser or an overlay is up.</summary>
        private void OnOverlayTap(Vector2 screen)
        {
            if (_overlayUi.ChooserOpen) { _overlayUi.HideChooser(); return; }
            Vector2 world = _input.ScreenToWorld(screen);
            int tile = _map.TileAt(world);
            if (tile < 0) { _overlayUi.HideCard(); _overlayTile = -1; _map.ClearSelection(); return; }
            _overlayTile = tile;
            _map.Select(new Hit(HitKind.Tile, tile), _mapVm);
            OverlayCardVm card = OverlayView.Card(_ctx, _overlay, tile);
            bool opens = _map.Pick(_map.TileWorld(tile)).Kind != HitKind.None; // an acre the normal tap would open
            _overlayUi.ShowCard(card, PanelPoint(_map.TileWorld(tile)), opens);
        }

        /// <summary>A world point as a point in the UI root, for placing a card beside an acre.</summary>
        private Vector2 PanelPoint(Vector2 world)
        {
            Vector2 s = _input.WorldToScreen(world);
            Vector2 p = RuntimePanelUtils.ScreenToPanel(_safeRoot.panel, new Vector2(s.x, Screen.height - s.y));
            return _safeRoot.WorldToLocal(p);
        }

        /// <summary>Open the acre: the overlay ends and the acre's own sheet opens, as a tap on it would have.</summary>
        private void OpenOverlayAcre()
        {
            int tile = _overlayTile;
            EndOverlay();
            if (tile >= 0) OpenSheet(new SheetRequest("ShowTile", tile));
        }
    }
}
