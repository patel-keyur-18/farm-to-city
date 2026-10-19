using System;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Touch and mouse for the map (spec 008 §3.4): one finger drags to pan, two fingers pinch to zoom, a short tap picks what is
    /// under the finger. In the Editor the mouse does the same (left drag pans, the wheel zooms, a click picks). Taps on the UI never
    /// reach the map. The camera limits come from <see cref="MapCamera"/> (pure and tested).
    /// </summary>
    public sealed class MapInput : MonoBehaviour
    {
        private const float DragStartPixels = 14f, TapSeconds = 0.4f;

        private Camera _camera;
        private MapPresenter _map;
        private MapCamera _view;
        private UIDocument _document;
        private Action<Vector2> _onTap;

        private bool _down, _dragging, _wasPressed;
        private Vector2 _downAt, _last;
        private float _downTime;
        private float _lastPinch;
        private bool _pinching;

        /// <summary>Set by the host while a sheet is open: a tap on the map then closes the sheet instead of picking.</summary>
        public Func<bool> Blocked { get; set; }

        public MapCamera View { get { return _view; } }

        /// <summary>
        /// Placement mode (the hand's hut ghost, ADR 0039 §2): while set, a finger on the map moves the ghost to the world point under
        /// it instead of panning, and a tap places it there. Pinch still zooms.
        /// </summary>
        public Action<Vector2> Placing { get; set; }

        public static MapInput Create(GameObject host, Camera camera, MapPresenter map, UIDocument document, Action<Vector2> onTap)
        {
            MapInput i = host.AddComponent<MapInput>();
            i._camera = camera;
            i._map = map;
            i._document = document;
            i._onTap = onTap;
            camera.orthographic = true;
            i._view = new MapCamera(map.Grid, camera.aspect);
            i.Apply();
            return i;
        }

        /// <summary>Starts on the inherited land: the middle of its 4 × 2 acres.</summary>
        public void Focus(float tileX, float tileY)
        {
            Vector2 c = new Vector2((tileX - tileY) * _map.Grid.HalfWidth, -(tileX + tileY) * _map.Grid.HalfHeight);
            _view.FocusWorld(c.x, c.y, _view.MaxHalfHeight * 0.62f);
            Apply();
        }

        private bool _revealing;
        private Vector2 _revealFrom, _revealTo;
        private float _revealStart;

        /// <summary>Slides the view so a world point sits in the top fifth of the screen, above the bottom sheet (spec 008 §3.2).</summary>
        public void Reveal(Vector2 world)
        {
            (float x, float y) = _view.CentreFor(world.x, world.y, 0.22f);
            _revealFrom = new Vector2(_view.CentreX, _view.CentreY);
            _revealTo = new Vector2(x, y);
            _revealStart = Time.unscaledTime;
            _revealing = true;
        }

        /// <summary>The camera shows the area above the bottom sheet, so a tap-to-select can shift the view; not needed in Phase 1.</summary>
        private void Apply()
        {
            _camera.transform.position = new Vector3(_view.CentreX, _view.CentreY, -10f);
            _camera.orthographicSize = _view.HalfHeight;
        }

        private bool OverUi(Vector2 screen)
        {
            if (_document == null || _document.rootVisualElement == null || _document.rootVisualElement.panel == null) return false;
            VisualElement root = _document.rootVisualElement;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = root.panel.Pick(panelPos);
            return picked != null && picked != root;
        }

        private Vector2 WorldPoint(Vector2 screen) { return _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f)); }

        private float WorldPerPixel { get { return 2f * _view.HalfHeight / Screen.height; } }

        private void Update()
        {
            if (_revealing)
            {
                float k = Motion.Ease.OutCubic((Time.unscaledTime - _revealStart) / 0.3f);
                Vector2 want = Vector2.Lerp(_revealFrom, _revealTo, k);
                _view.Pan(want.x - _view.CentreX, want.y - _view.CentreY);
                Apply();
                if (k >= 1f) _revealing = false;
            }
            Touchscreen touch = Touchscreen.current;
            int touches = 0;
            Vector2 a = default, b = default;
            if (touch != null)
                foreach (var t in touch.touches)
                    if (t.press.isPressed)
                    {
                        if (touches == 0) a = t.position.ReadValue(); else if (touches == 1) b = t.position.ReadValue();
                        touches++;
                    }

            if (touches >= 2) { Pinch(a, b); _wasPressed = true; return; }
            _pinching = false;

            // A phone has no mouse: with no finger down the pointer is simply up. (Returning early here lost every release on
            // iOS, so taps never fired and the next drag jumped from the last finger position.)
            Vector2 pos = _last;
            bool pressed = false;
            Mouse m = Mouse.current;
            if (touches == 1) { pos = a; pressed = true; }
            else if (m != null)
            {
                pos = m.position.ReadValue();
                pressed = m.leftButton.isPressed;
                float scroll = m.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !OverUi(pos))
                {
                    _view.Zoom(Mathf.Exp(-scroll * 0.0015f), WorldPoint(pos).x, WorldPoint(pos).y);
                    Apply();
                }
            }
            bool pressedNow = pressed && !_wasPressed, released = _down && !pressed;
            _wasPressed = pressed;

            if (pressedNow && !OverUi(pos))
            {
                _down = true; _dragging = false; _revealing = false; _downAt = _last = pos; _downTime = Time.unscaledTime;
            }
            else if (_down && pressed && Placing != null)
            {
                Placing(WorldPoint(pos));
                _last = pos;
            }
            else if (_down && pressed)
            {
                if (!_dragging && Vector2.Distance(pos, _downAt) > DragStartPixels) _dragging = true;
                if (_dragging)
                {
                    Vector2 d = pos - _last;
                    _view.Pan(-d.x * WorldPerPixel, -d.y * WorldPerPixel);
                    Apply();
                }
                _last = pos;
            }
            if (released)
            {
                bool tap = !_dragging && Time.unscaledTime - _downTime < TapSeconds && !_pinching;
                _down = false;
                if (Placing != null) Placing(WorldPoint(_last));
                else if (tap && _onTap != null) _onTap(_downAt);
            }
        }

        private void Pinch(Vector2 a, Vector2 b)
        {
            float dist = Vector2.Distance(a, b);
            Vector2 mid = (a + b) / 2f;
            if (!_pinching) { _pinching = true; _lastPinch = dist; _last = mid; _down = false; return; }
            if (_lastPinch > 1f && dist > 1f)
            {
                Vector2 w = WorldPoint(mid);
                _view.Zoom(_lastPinch / dist, w.x, w.y);
                Vector2 d = mid - _last;
                _view.Pan(-d.x * WorldPerPixel, -d.y * WorldPerPixel);
                Apply();
            }
            _lastPinch = dist;
            _last = mid;
        }

        /// <summary>The world position of a screen point, for anchoring effects.</summary>
        public Vector2 ScreenToWorld(Vector2 screen) { return WorldPoint(screen); }

        public Vector2 WorldToScreen(Vector2 world) { return _camera.WorldToScreenPoint(world); }
    }
}
