using Game.Presentation.Art;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The river flows (US-273, ADR 0184): the water acres' sprites are a loop of <see cref="SpriteBank.WaterFrames"/> frames rendered so each leads into the
    /// next and neighbouring acres meet; every water acre shows the same frame, so they share a texture and batch. Reduced motion holds frame 0.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private int _waterShown = -1;

        private void AnimateWater()
        {
            int frame = Sim.View.Views.WaterFlow.Frame(Time.unscaledTime, _tweens != null && _tweens.ReducedMotion);
            if (frame == _waterShown) return;
            _waterShown = frame;
            SpriteBank.WaterFrame = frame;
            foreach (SpriteRenderer r in _ground) SetWater(r, "ground_water", frame);
            foreach (EdgeSpot e in _edge) SetWater(e.Ground, "edge_water", frame);
        }

        private static void SetWater(SpriteRenderer r, string prefix, int frame)
        {
            Sprite s = r != null ? r.sprite : null;
            if (s == null || string.IsNullOrEmpty(s.name) || !s.name.StartsWith(prefix, System.StringComparison.Ordinal)) return;
            Sprite next = SpriteBank.Rendered(prefix + "_" + frame);
            if (next != null) r.sprite = next;
        }
    }
}
