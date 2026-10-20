using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Motion
{
    /// <summary>
    /// The feedback sounds (F08.5, ADR 0043 §10): seven Kenney CC0 clips in <c>Resources/Sounds</c>, at most one per event and never
    /// stacking (the same clip again within a short gap is skipped). The Sound switch in Settings mutes them all.
    /// </summary>
    public sealed class Sounds
    {
        private const float Gap = 0.09f, Volume = 0.7f;
        private readonly AudioSource[] _voices;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private int _next;

        public bool Enabled { get; set; } = true;

        public Sounds(GameObject host)
        {
            _voices = new AudioSource[4];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = host.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].volume = Volume;
            }
        }

        public void Play(string id, float pitch = 1f)
        {
            if (!Enabled) return;
            if (_lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < Gap) return;
            if (!_clips.TryGetValue(id, out AudioClip clip)) _clips[id] = clip = Resources.Load<AudioClip>("Sounds/" + id);
            if (clip == null) return;
            _lastPlayed[id] = Time.unscaledTime;
            AudioSource v = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            v.pitch = pitch;
            v.PlayOneShot(clip);
        }

        /// <summary>Coins in: the jingle's pitch rises in three steps with the amount (whole coins).</summary>
        public void CoinsIn(long coins) { Play("coins_in", coins < 1_000 ? 1f : coins < 10_000 ? 1.12f : 1.25f); }

        public void Harvest()
        {
            Play("harvest_pop");
            Play("harvest_rustle");
        }
    }
}
