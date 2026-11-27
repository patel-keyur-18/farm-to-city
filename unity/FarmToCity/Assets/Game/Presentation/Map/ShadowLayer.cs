using System.Collections.Generic;
using Game.Presentation.Art;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The baked shadows of buildings and trees (spec 032 §3.3, US-283): every sprite on the map whose render has a <c>_shadow</c> sprite gets it as a
    /// child drawn just under it (the same world anchor, so the two line up), black, at an alpha of <see cref="Strength"/> (the day's light times the
    /// weather's <c>shadow</c>, from <see cref="Sim.View.Views.WeatherFrame.Shadow"/>). New sprites are found by a sweep twice a second, so no
    /// placement code needs to know about shadows. In the device check (<see cref="ArtCheck"/>) casters without a shadow of their own draw the house's.
    /// </summary>
    public sealed class ShadowLayer : MonoBehaviour
    {
        private struct Entry { public SpriteRenderer Owner, Shadow; public string Name; }

        private readonly Dictionary<SpriteRenderer, Entry> _entries = new Dictionary<SpriteRenderer, Entry>();
        private readonly HashSet<SpriteRenderer> _shadows = new HashSet<SpriteRenderer>();
        private readonly List<SpriteRenderer> _all = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _gone = new List<SpriteRenderer>();
        private float _nextSweep;

        /// <summary>How strong the shadows are now, 0 to 1 (0 at night and under a storm).</summary>
        public float Strength { get; set; } = 1f;

        /// <summary>The shadows drawn now.</summary>
        public int Count { get { return _entries.Count; } }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextSweep) { _nextSweep = Time.unscaledTime + 0.5f; Sweep(); }
            foreach (KeyValuePair<SpriteRenderer, Entry> p in _entries)
            {
                Entry e = p.Value;
                if (e.Owner == null || e.Shadow == null) continue;
                bool shown = e.Owner.enabled && e.Owner.sprite != null && e.Owner.gameObject.activeInHierarchy;
                e.Shadow.enabled = shown;
                e.Shadow.sortingOrder = e.Owner.sortingOrder - 1;
                e.Shadow.color = new Color(0f, 0f, 0f, Strength * e.Owner.color.a);
            }
        }

        private void Sweep()
        {
            GetComponentsInChildren(true, _all);
            foreach (SpriteRenderer r in _all)
            {
                if (r == null || _shadows.Contains(r)) continue;
                Sprite s = r.sprite;
                if (_entries.TryGetValue(r, out Entry e))
                {
                    if (s != null && s.name == e.Name) continue; // the same sprite: its shadow stands
                    Remove(r);
                }
                if (s == null) continue;
                Sprite shadow = SpriteBank.ShadowOf(s);
                float scale = 1f;
                if (shadow == null)
                {
                    scale = ArtCheck.StandInShadowScale(s);
                    if (scale <= 0f) continue;
                    shadow = SpriteBank.Rendered("family_house_s_shadow");
                    if (shadow == null) continue;
                }
                var go = new GameObject(s.name + "Shadow");
                go.transform.SetParent(r.transform, false);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shadow;
                sr.sortingOrder = r.sortingOrder - 1;
                _shadows.Add(sr);
                _entries[r] = new Entry { Owner = r, Shadow = sr, Name = s.name };
            }
            _gone.Clear();
            foreach (KeyValuePair<SpriteRenderer, Entry> p in _entries) if (p.Key == null) _gone.Add(p.Key);
            foreach (SpriteRenderer r in _gone) Remove(r);
        }

        private void Remove(SpriteRenderer owner)
        {
            if (_entries.TryGetValue(owner, out Entry e))
            {
                if (e.Shadow != null) { _shadows.Remove(e.Shadow); Destroy(e.Shadow.gameObject); }
                _entries.Remove(owner);
            }
        }
    }
}
