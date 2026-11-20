using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The village clinic and the doctor's quarters on their plots (spec 023 §3.1, US-148), as the school: the empty plot, the site while
    /// it is built, then the building facing the camera with its gate on the main street.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private int _clinicStage = -1, _quartersStage = -1;
        private readonly int[] _civicStage = { -1, -1, -1 };
        private static readonly string[] CivicBuilt = { "village_watchman_e", "village_hall_e", "village_playground_e" };
        private readonly int[] _utilityStage = { -1, -1, -1, -1, -1 };
        private static readonly string[] UtilityIds = { "tower", "generator", "grid", "drains", "collection" };
        private static readonly string[] CivicPlot = { "place_watchman_plot", "place_hall_plot", "place_playground_plot" };

        private void RefreshClinic(MapVm map)
        {
            if (map.ClinicTile >= 0 && map.ClinicStage != _clinicStage && _places.TryGetValue(map.ClinicTile, out SpriteRenderer c))
            {
                _clinicStage = map.ClinicStage;
                c.sprite = SpriteBank.Rendered(map.ClinicStage == 2 ? "village_clinic_e" : map.ClinicStage == 1 ? "village_clinic_building_e" : "place_clinic_plot");
            }
            if (map.QuartersTile >= 0 && map.QuartersStage != _quartersStage && _places.TryGetValue(map.QuartersTile, out SpriteRenderer q))
            {
                _quartersStage = map.QuartersStage;
                q.sprite = SpriteBank.Rendered(map.QuartersStage == 2 ? "village_quarters_e" : map.QuartersStage == 1 ? "village_quarters_building_e" : "place_quarters_plot");
            }
            for (int i = 0; i < _civicStage.Length; i++) // the civic buildings (spec 023 §3.7): the plot, then the building
                if (map.CivicTile[i] >= 0 && map.CivicStage[i] != _civicStage[i] && _places.TryGetValue(map.CivicTile[i], out SpriteRenderer p))
                {
                    _civicStage[i] = map.CivicStage[i];
                    p.sprite = SpriteBank.Rendered(map.CivicStage[i] == 2 ? CivicBuilt[i] : CivicPlot[i]);
                }
            for (int u = 0; u < _utilityStage.Length; u++) // the utilities (spec 025 §3.1): the plot, then what stands; the generator without smoke once the grid came
                if (map.UtilityTile[u] >= 0 && map.UtilityStage[u] != _utilityStage[u] && _places.TryGetValue(map.UtilityTile[u], out SpriteRenderer w))
                {
                    _utilityStage[u] = map.UtilityStage[u];
                    w.sprite = SpriteBank.Rendered(map.UtilityStage[u] == 3 ? "village_generator_off_e" : map.UtilityStage[u] == 2 ? "village_" + UtilityIds[u] + "_e" : "place_" + UtilityIds[u] + "_plot");
                }
        }
    }
}
