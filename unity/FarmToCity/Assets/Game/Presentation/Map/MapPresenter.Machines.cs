using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Machines on the map (ADR 0088, art approved 2026-11-04): drawn at 70% of their modelled size (the ploughing pair at 75%) so
    /// they sit inside an acre; at work on a growing field by day, the tractor parked with its trolley on the farm lane at night.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private const float MachineScale = 0.7f, PloughScale = 0.75f;
        private readonly List<SpriteRenderer> _machines = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _machineShadows = new List<SpriteRenderer>();

        private void RefreshMachines(MapVm map)
        {
            while (_machines.Count < map.Machines.Count)
            {
                SpriteRenderer shadow = NewRenderer("machineShadow" + _machines.Count, SpriteBank.ContactShadow, Vector3.zero, 0);
                _machineShadows.Add(shadow);
                _machines.Add(NewRenderer("machine" + _machines.Count, null, Vector3.zero, 0));
            }
            for (int i = 0; i < _machines.Count; i++)
            {
                bool shown = i < map.Machines.Count;
                _machines[i].gameObject.SetActive(shown);
                _machineShadows[i].gameObject.SetActive(shown);
                if (!shown) continue;
                MachineVm m = map.Machines[i];
                bool pair = m.Sprite == "plough_team";
                float scale = pair ? PloughScale : MachineScale;
                // At work: a little toward the field's front, over its crop; parked: in the middle of the lane's acre.
                Vector2 at = m.Parked ? Vector2.zero : new Vector2(0.06f, -0.08f);
                Vector3 pos = OnAcre(m.TileId, at);
                SpriteRenderer r = _machines[i];
                r.sprite = SpriteBank.Rendered(m.Sprite);
                r.transform.position = pos;
                r.transform.localScale = new Vector3(scale, scale, 1f);
                r.sortingOrder = AcreOrder(m.TileId, at) + 2;
                r.color = _tint;
                SpriteRenderer s = _machineShadows[i];
                s.transform.position = pos;
                s.transform.localScale = new Vector3(0.4f * scale, 0.4f * scale, 1f);
                s.sortingOrder = r.sortingOrder - 1;
            }
        }
    }
}
