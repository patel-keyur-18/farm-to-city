using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The village's school on its plot (spec 018, US-131), as the owner approved it (docs/art/s17-school-preview-*.png): the empty
    /// plot, the school being built, then the school facing the camera with its gate on the main street; in school hours its pupils
    /// in the yard and the teacher at the verandah.
    /// </summary>
    public sealed partial class MapPresenter
    {
        /// <summary>Places in the school's yard (render.py's world units from the acre's centre): the pupils, then the teacher.</summary>
        private static readonly Vector2[] SchoolSpots =
        {
            new Vector2(0.08f, 0.20f), new Vector2(0.20f, 0.13f), new Vector2(0.26f, -0.03f), new Vector2(0.12f, -0.27f), new Vector2(0.25f, -0.24f),
            new Vector2(0.02f, 0.02f), new Vector2(-0.01f, -0.12f),
        };

        private int _schoolStage = -1;

        private void RefreshSchool(MapVm map)
        {
            if (map.SchoolTile < 0 || map.SchoolStage == _schoolStage || !_places.TryGetValue(map.SchoolTile, out SpriteRenderer r)) return;
            _schoolStage = map.SchoolStage;
            r.sprite = SpriteBank.Rendered(map.SchoolStage == 2 ? "village_school_e" : map.SchoolStage == 1 ? "village_school_building_e" : "place_school_plot");
        }

        private Vector3 SchoolSpot(PersonVm p) { return OnTile(p.TileId, SchoolSpots[Mathf.Clamp(p.Spot, 0, SchoolSpots.Length - 1)]); }
    }
}
