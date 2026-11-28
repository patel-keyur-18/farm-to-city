using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Machines on the map (ADR 0088, art approved 2026-11-04): drawn at 70% of their modelled size (the ploughing pair at 75%) so
    /// they sit inside an acre; at work on a growing field by day, the tractor parked with its trolley on the farm lane at night. A man
    /// walks behind the plough at work, at people's size (spec 032 §3.9): a picture only, not tappable.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private const float MachineScale = 0.7f, PloughScale = 0.75f;
        // The ploughman's feet from the plough team's pivot, in world units: just behind the plough's handle (Preview 30).
        private static readonly Vector3 PloughmanBehind = new Vector3(0.085f, 0.084f, 0f);
        private SpriteRenderer _ploughman, _ploughmanShadow; // there is at most one ox plough
        private readonly List<SpriteRenderer> _machines = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _machineShadows = new List<SpriteRenderer>();

        private void RefreshMachines(MapVm map)
        {
            if (_ploughman != null) { _ploughman.gameObject.SetActive(false); _ploughmanShadow.gameObject.SetActive(false); }
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
                Vector2 at = m.Parked ? new Vector2(m.ParkX, m.ParkY) : new Vector2(0.06f, -0.08f);
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
                if (m.Operator == null) continue;
                if (_ploughman == null)
                {
                    _ploughmanShadow = NewRenderer("ploughmanShadow", SpriteBank.ContactShadow, Vector3.zero, 0);
                    _ploughmanShadow.transform.localScale = new Vector3(0.11f, 0.11f, 1f);
                    _ploughman = NewRenderer("ploughman", null, Vector3.zero, 0);
                }
                _ploughman.gameObject.SetActive(true);
                _ploughmanShadow.gameObject.SetActive(true);
                _ploughman.sprite = SpriteBank.Rendered(m.Operator + "_front");
                _ploughman.transform.position = _ploughmanShadow.transform.position = pos + PloughmanBehind;
                _ploughman.color = _tint;
                // He stands further back than the team, so the handle is drawn in front of him; the team's shadow goes under both.
                _ploughman.sortingOrder = r.sortingOrder - 1;
                _ploughmanShadow.sortingOrder = r.sortingOrder - 2;
                s.sortingOrder = r.sortingOrder - 3;
            }
        }
    }
}
