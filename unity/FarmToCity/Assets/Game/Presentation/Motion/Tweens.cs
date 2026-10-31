using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Motion
{
    /// <summary>The easing names of spec 008 §3.12.</summary>
    public static class Ease
    {
        public static float Linear(float t) { return Mathf.Clamp01(t); }
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }

        /// <summary>Overshoot 1.70158, the spec's OutBack.</summary>
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }

        public static Func<float, float> ByName(string name)
        {
            switch (name)
            {
                case "OutBack": return OutBack;
                case "OutCubic": return OutCubic;
                case "InOutCubic": return InOutCubic;
                case "InCubic": return InCubic;
                default: return Linear;
            }
        }
    }

    /// <summary>
    /// Code-driven tweens (ADR 0017: no animation assets). A tween with a key restarts when started again instead of stacking
    /// (spec 008 §3.12 caps). Tweens never block input or the simulation: they only change what is drawn. Like a tiny animation
    /// scheduler ticked once a frame.
    /// </summary>
    public sealed class Tweens : MonoBehaviour
    {
        private sealed class Item
        {
            public object Key;
            public float Start, Duration;
            public Func<float, float> Ease;
            public Action<float> Step;
            public Action Done;
        }

        private readonly List<Item> _items = new List<Item>();

        /// <summary>Reduced motion (spec 008 §3.13): tweens finish at once or in a tenth of a second.</summary>
        public bool ReducedMotion { get; set; }

        public int Active { get { return _items.Count; } }

        public static Tweens Create(Transform parent)
        {
            var go = new GameObject("Tweens");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Tweens>();
        }

        /// <summary>Runs <paramref name="step"/> with eased 0..1 over <paramref name="seconds"/>; a tween with the same key is replaced.</summary>
        public void Run(object key, float seconds, Func<float, float> ease, Action<float> step, Action done = null)
        {
            if (key != null) _items.RemoveAll(i => i.Key == key);
            if (ReducedMotion) seconds = Mathf.Min(seconds, 0.1f);
            if (seconds <= 0f)
            {
                step(1f);
                if (done != null) done();
                return;
            }
            step(0f);
            _items.Add(new Item { Key = key, Start = Time.unscaledTime, Duration = seconds, Ease = ease ?? Motion.Ease.Linear, Step = step, Done = done });
        }

        public void Cancel(object key) { _items.RemoveAll(i => i.Key == key); }

        private void Update()
        {
            if (_items.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                // Keyed by a sprite's transform that has since been destroyed (a site finished in the same moment it popped): drop it,
                // or the exception would stop every other tween on screen (seen 2026-10-30).
                if (it.Key is UnityEngine.Object o && o == null) { _items.RemoveAt(i--); continue; }
                float t = Mathf.Clamp01((now - it.Start) / it.Duration);
                it.Step(it.Ease(t));
                if (t < 1f) continue;
                _items.RemoveAt(i--);
                if (it.Done != null) it.Done();
            }
        }
    }
}
