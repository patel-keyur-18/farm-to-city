using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The square's pitches (spec 017 §3.4, layout D approved 2026-11-05): round the banyan, pitch I at the acre's left corner, II on its
    /// front edge, III at its right corner, at 85%. Each is drawn empty, or with the shop of yours on it; its customers stand at the
    /// counter in front. Positions are tile-local (−0.5 to 0.5; +x lower right, +y upper right).
    /// </summary>
    public sealed partial class MapPresenter
    {
        private static readonly Vector2[] PitchAt = { new Vector2(-0.36f, -0.34f), new Vector2(0.12f, -0.36f), new Vector2(0.36f, 0.32f) };
        private const float PitchScale = 0.85f;

        /// <summary>Where a pitch's customers stand: before its counter (its front faces −y, pitch III's +x), a little apart.</summary>
        private static readonly Vector2[][] PitchCustomersAt =
        {
            new[] { new Vector2(-0.30f, -0.46f), new Vector2(-0.22f, -0.44f) },
            new[] { new Vector2(0.10f, -0.47f), new Vector2(0.18f, -0.46f) },
            new[] { new Vector2(0.46f, 0.26f), new Vector2(0.45f, 0.36f) },
        };

        /// <summary>Where whoever runs a pitch's shop stands: behind its counter.</summary>
        private static readonly Vector2[] PitchWorkAt = { new Vector2(-0.38f, -0.24f), new Vector2(0.10f, -0.26f), new Vector2(0.26f, 0.30f) };

        private int _squareTile = -1;
        private readonly List<SpriteRenderer> _pitches = new List<SpriteRenderer>();
        private readonly Dictionary<long, int> _pitchOf = new Dictionary<long, int>();

        private void RefreshPitches(MapVm map, bool animate)
        {
            _pitchOf.Clear();
            _squareTile = map.SquareTile;
            if (map.SquareTile < 0) return;
            var on = new string[map.Pitches];
            foreach (BusinessVm b in map.Businesses)
                if (b.Pitch >= 0 && b.Pitch < on.Length)
                {
                    on[b.Pitch] = b.TypeId;
                    _pitchOf[b.Id] = b.Pitch;
                }
            for (int p = 0; p < map.Pitches && p < PitchAt.Length; p++)
            {
                string name = on[p] != null ? on[p] + "_pitch_" + (p + 1) : "pitch_empty_" + (p + 1);
                Sprite s = SpriteBank.Rendered(name) ?? SpriteBank.Rendered("pitch_empty_" + (p + 1));
                if (p == _pitches.Count)
                {
                    SpriteRenderer r = NewRenderer("pitch" + p, s, OnAcre(map.SquareTile, PitchAt[p]), AcreOrder(map.SquareTile, PitchAt[p]));
                    r.transform.localScale = Vector3.one * PitchScale;
                    _pitches.Add(r);
                }
                SpriteRenderer pitch = _pitches[p];
                if (pitch.sprite != s)
                {
                    pitch.sprite = s;
                    if (animate && on[p] != null) Pop(pitch.transform, PitchScale);
                }
                pitch.color = _tint;
            }
        }

        /// <summary>A spot behind the counter of one of your shops on the square (the founder or worker there), or null off the square.</summary>
        private Vector3? PitchWork(int tile)
        {
            if (tile != _squareTile || _pitchOf.Count == 0) return null;
            var mine = new List<int>(_pitchOf.Values);
            int p = mine[Random.Range(0, mine.Count)];
            return p < PitchWorkAt.Length ? OnAcre(tile, PitchWorkAt[p]) : (Vector3?)null;
        }

        /// <summary>A customer's spot at a shop on a pitch, or null for one on an acre.</summary>
        private Vector3? PitchCustomer(long businessId, int tile, int place)
        {
            if (!_pitchOf.TryGetValue(businessId, out int p) || p >= PitchCustomersAt.Length) return null;
            Vector2[] at = PitchCustomersAt[p];
            return OnAcre(tile, at[Mathf.Clamp(place, 0, at.Length - 1)]);
        }
    }
}
