using System;
using Game.Presentation.Art;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Motion
{
    /// <summary>
    /// The flying icons of spec 008 §3.12: produce flies on an arc from the field to the store button, coins fly to the cash. At most
    /// 5 icons an event and 20 at once; each is a small UI element tweened in screen space, so it never blocks input or the sim.
    /// </summary>
    public sealed class Fx
    {
        private readonly Tweens _tweens;
        private readonly VisualElement _layer;
        private readonly int _perEvent, _total;
        private int _active;

        public Fx(VisualElement root, Tweens tweens, int perEvent, int total)
        {
            _tweens = tweens;
            _perEvent = perEvent;
            _total = total;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0; _layer.style.right = 0; _layer.style.top = 0; _layer.style.bottom = 0;
            root.Add(_layer);
        }

        /// <summary>Flies up to <paramref name="count"/> icons from a screen point to another (y up in screen pixels), 0.6 s each, staggered by 0.05 s.</summary>
        public void Fly(Sprite icon, Vector2 fromScreen, Vector2 toScreen, int count, Action arrived)
        {
            count = Mathf.Min(count, _perEvent);
            float k = _layer.panel.scaledPixelsPerPoint;
            for (int i = 0; i < count; i++)
            {
                if (_active >= _total) break;
                _active++;
                var e = UiKit(icon);
                _layer.Add(e);
                Vector2 a = ToPanel(fromScreen, k), b = ToPanel(toScreen, k);
                float delay = i * 0.05f;
                bool last = i == count - 1;
                Vector2 jitter = new Vector2((i - count / 2f) * 12f, (i % 2) * 10f);
                e.style.display = DisplayStyle.None;
                _layer.schedule.Execute(() =>
                {
                    e.style.display = DisplayStyle.Flex;
                    _tweens.Run(e, 0.6f, Ease.InOutCubic, t =>
                    {
                        Vector2 p = Vector2.Lerp(a + jitter, b, t);
                        p.y -= Mathf.Sin(t * Mathf.PI) * 90f; // an arc (panel y grows down, so minus is up)
                        e.style.left = p.x - 28f;
                        e.style.top = p.y - 28f;
                        float s = Mathf.Lerp(1f, 0.7f, t);
                        e.style.scale = new Scale(new Vector3(s, s, 1f));
                    }, () =>
                    {
                        _layer.Remove(e);
                        _active--;
                        if (last && arrived != null) arrived();
                    });
                }).StartingIn((long)(delay * 1000));
            }
        }

        private static Vector2 ToPanel(Vector2 screen, float k) { return new Vector2(screen.x / k, (Screen.height - screen.y) / k); }

        private static VisualElement UiKit(Sprite s)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.width = 56; e.style.height = 56;
            e.style.backgroundImage = new StyleBackground(s);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            return e;
        }
    }
}
