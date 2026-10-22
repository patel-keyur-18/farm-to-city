using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The trader's truck and the supermarket's van (spec 011 §7): the truck drives in along the district road and up the farm lane
    /// at 6:00, parks at the stop by the labour quarters and drives off when it leaves; the van waits at the stop while today's
    /// orders are open. Both are drawn from the state each refresh; the motion is presentation only.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private const float TruckSpeed = 1.6f; // route points (acres) a second
        private SpriteRenderer _truck, _truckShadow, _van, _vanShadow;
        private readonly List<Vector3> _route = new List<Vector3>();
        private float _truckAt = -1f, _truckGoal;
        private string _truckFacing = "_front";
        private bool _vanHere;

        /// <summary>Where the van stands (the supermarket's collection point at the truck stop).</summary>
        public Vector2 VanWorld { get { return _route.Count == 0 ? Vector2.zero : (Vector2)(_route[_route.Count - 1] + VanOffset()); } }

        private Vector3 VanOffset()
        {
            // An acre back along the lane from where the truck parks, so the two never overlap.
            Vector3 back = _route.Count < 2 ? Vector3.zero : _route[_route.Count - 2] - _route[_route.Count - 1];
            return back * 0.9f;
        }

        private void RefreshVehicles(MapVm map, bool animate)
        {
            if (map.TruckRoute.Count == 0) return;
            if (_truck == null)
            {
                foreach (int tile in map.TruckRoute) _route.Add(TileCentre(tile));
                // It drives in from past the map's edge: one acre further out than the road's last acre.
                if (_route.Count > 1) _route.Insert(0, _route[0] + (_route[0] - _route[1]));
                _truckShadow = NewRenderer("truckShadow", SpriteBank.ContactShadow, Vector3.zero, 0);
                _truckShadow.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
                _truck = NewRenderer("truck", SpriteBank.Rendered("truck_front"), Vector3.zero, 0);
                _vanShadow = NewRenderer("vanShadow", SpriteBank.ContactShadow, Vector3.zero, 0);
                _vanShadow.transform.localScale = new Vector3(0.34f, 0.34f, 1f);
                _van = NewRenderer("van", SpriteBank.Rendered("van_back"), VanWorld, 0);
                animate = false;
            }
            _truckGoal = map.TruckHere ? _route.Count - 1 : 0f;
            if (!animate || _truckAt < 0f) _truckAt = _truckGoal;
            _vanHere = map.VanHere;
            _van.gameObject.SetActive(_vanHere);
            _vanShadow.gameObject.SetActive(_vanHere);
            _van.transform.position = VanWorld;
            _vanShadow.transform.position = VanWorld;
            int vanOrder = Mathf.RoundToInt(-VanWorld.y / _grid.HalfHeight * 100f) + 60;
            _van.sortingOrder = vanOrder;
            _vanShadow.sortingOrder = vanOrder - 1;
            _van.color = _tint;
            PlaceTruck();
        }

        private void AnimateVehicles()
        {
            if (_truck == null || Mathf.Approximately(_truckAt, _truckGoal)) return;
            float step = TruckSpeed * Time.unscaledDeltaTime;
            _truckAt = _truckAt < _truckGoal ? Mathf.Min(_truckGoal, _truckAt + step) : Mathf.Max(_truckGoal, _truckAt - step);
            PlaceTruck();
        }

        /// <summary>The truck at its point along the route, facing where it drives (two facings rendered, mirrored for the other two).</summary>
        private void PlaceTruck()
        {
            bool shown = _truckAt > 0.001f;
            _truck.gameObject.SetActive(shown);
            _truckShadow.gameObject.SetActive(shown);
            if (!shown) return;
            int i = Mathf.Min(Mathf.FloorToInt(_truckAt), _route.Count - 2);
            Vector3 pos = Vector3.Lerp(_route[i], _route[i + 1], _truckAt - i);
            Vector3 d = _route[i + 1] - _route[i];
            if (_truckGoal < _truckAt) d = -d; // driving away
            if (!Mathf.Approximately(_truckAt, _truckGoal) || _truckAt >= _route.Count - 1.001f)
            {
                _truckFacing = d.y > 0 ? "_back" : "_front";
                _truck.flipX = d.x > 0;
            }
            _truck.sprite = SpriteBank.Rendered("truck" + _truckFacing);
            _truck.transform.position = pos;
            _truckShadow.transform.position = pos;
            int order = Mathf.RoundToInt(-pos.y / _grid.HalfHeight * 100f) + 60;
            _truck.sortingOrder = order;
            _truckShadow.sortingOrder = order - 1;
            _truck.color = _tint;
        }

        private Hit PickVehicle(Vector2 w)
        {
            if (_truck != null && _truck.gameObject.activeSelf && Vector2.Distance(w, (Vector2)_truck.transform.position + new Vector2(0, 0.1f)) < 0.2f)
                return new Hit(HitKind.Truck, 0);
            if (_van != null && _van.gameObject.activeSelf && Vector2.Distance(w, (Vector2)_van.transform.position + new Vector2(0, 0.08f)) < 0.16f)
                return new Hit(HitKind.Van, 0);
            return Hit.None;
        }
    }
}
