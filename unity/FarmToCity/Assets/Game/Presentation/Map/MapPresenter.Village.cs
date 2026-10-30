using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The village redrawn (ADR 0061, US-206), as the owner approved it (docs/art/s12-village-preview-*.png): a main street from the
    /// village lane to the fields between the centre's second and third rows, a side lane across it, every building off-centre in
    /// its acre facing its street (the shops and public buildings where their fronts face the camera), front and back yards with
    /// haystacks, charpais, tethered cattle, a handpump and trees. Offsets are render.py's world units (+x lower right, +y upper
    /// right on screen) from the acre's centre.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private struct VillagePiece
        {
            public string Sprite;
            public Vector2 At;
            public VillagePiece(string sprite, float x, float y) { Sprite = sprite; At = new Vector2(x, y); }
        }

        /// <summary>Each place's building first, then its yard's props.</summary>
        private static readonly Dictionary<string, VillagePiece[]> VillageLayout = new Dictionary<string, VillagePiece[]>
        {
            { "house_1", new[] { new VillagePiece("village_house_0_w", -0.12f, 0.05f), new VillagePiece("village_haystack", 0.25f, 0.22f), new VillagePiece("village_charpai", 0.22f, -0.2f) } },
            { "dealer", new[] { new VillagePiece("village_dealer_e", 0.10f, 0f), new VillagePiece("village_tree", -0.3f, 0.28f) } },
            { "temple", new[] { new VillagePiece("village_temple_e", -0.1f, 0f), new VillagePiece("village_peepal", 0.26f, 0.24f) } },
            { "pond", new[] { new VillagePiece("village_pond", 0.05f, 0f) } },
            { "council_hall", new[] { new VillagePiece("village_council_hall_s", -0.02f, -0.1f), new VillagePiece("village_tree", -0.3f, 0.3f) } },
            { "grocery", new[] { new VillagePiece("village_grocery_s", 0f, -0.12f), new VillagePiece("village_handpump", 0.36f, -0.36f) } },
            { "tea_stall", new[] { new VillagePiece("village_tea_stall_s", 0f, -0.18f), new VillagePiece("village_charpai", 0.26f, 0.08f) } },
            { "banyan", new[] { new VillagePiece("village_banyan", 0f, 0.05f) } },
            { "house_2", new[] { new VillagePiece("village_house_1_n", -0.05f, 0.12f), new VillagePiece("village_charpai", -0.22f, -0.26f), new VillagePiece("village_cow", 0.22f, -0.2f) } },
            { "chemist", new[] { new VillagePiece("village_chemist_e", 0.10f, 0f) } },
            { "house_3", new[] { new VillagePiece("village_house_2_n", 0f, 0.1f), new VillagePiece("village_buffalo", -0.26f, -0.24f) } },
            { "school_plot", new[] { new VillagePiece("place_school_plot", 0f, 0f) } },
            { "house_4", new[] { new VillagePiece("village_house_0_w", -0.12f, 0f), new VillagePiece("village_haystack", 0.25f, -0.22f) } },
            { "dairy", new[] { new VillagePiece("village_dairy_e", 0.08f, 0f) } },
            { "house_5", new[] { new VillagePiece("village_house_1_w", -0.1f, 0f), new VillagePiece("village_tree", 0.28f, -0.25f) } },
            { "house_6", new[] { new VillagePiece("village_house_2_s", 0f, 0.05f), new VillagePiece("village_haystack", -0.28f, 0.25f) } },
        };

        private readonly List<SpriteRenderer> _villageProps = new List<SpriteRenderer>();

        /// <summary>A point on an acre in render.py's world units, in the map's space.</summary>
        private Vector3 OnTile(int tile, Vector2 p) { return TileCentre(tile) + new Vector3((p.x + p.y) * HalfWidth, (p.y - p.x) * _grid.HalfHeight, 0f); }

        private void BuildVillage(MapVm map)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (TileVm t in map.Tiles)
            {
                if (t.Kind != TileKind.Village) continue;
                minX = Mathf.Min(minX, t.X); minY = Mathf.Min(minY, t.Y); maxX = Mathf.Max(maxX, t.X); maxY = Mathf.Max(maxY, t.Y);
                if (t.Place == null) continue;
                if (!VillageLayout.TryGetValue(t.PlaceId ?? "", out VillagePiece[] pieces))
                    pieces = new[] { new VillagePiece(t.Place == "house" ? "place_house_" + t.Variant : "place_" + t.Place, 0f, 0f) };
                for (int i = 0; i < pieces.Length; i++)
                {
                    Vector3 pos = OnTile(t.Id, pieces[i].At);
                    float offY = pos.y - TileCentre(t.Id).y;
                    SpriteRenderer r = NewRenderer(pieces[i].Sprite, SpriteBank.Rendered(pieces[i].Sprite), pos, Order(t.Id, _width, 45, offY));
                    if (pieces[i].Sprite.Contains("tree")) _swayers.Add(r.transform);
                    if (i == 0) _places[t.Id] = r; else _villageProps.Add(r);
                }
            }
            if (minX > maxX) return;
            // The main street between the centre's second and third rows, from the village lane (the column west of it) to the
            // fields; the side lane between its second and third columns, end to end.
            int row = minY + 1, col = minX + 1;
            for (int x = minX - 1; x <= maxX; x++)
            {
                int tile = row * _width + x;
                Vector2 at = x < minX ? new Vector2(0.5f, -0.5f) : new Vector2(0f, -0.5f); // from the lane's middle to the centre
                _villageProps.Add(NewRenderer("street", SpriteBank.Rendered("street_x"), OnTile(tile, at), GroundBase + Order(tile, _width, 3)));
            }
            for (int y = minY; y <= maxY; y++)
            {
                int tile = y * _width + col;
                _villageProps.Add(NewRenderer("lane", SpriteBank.Rendered("street_y"), OnTile(tile, new Vector2(0.5f, 0f)), GroundBase + Order(tile, _width, 3)));
            }
        }

        /// <summary>A village acre's ground: worn grass in two variants, the banyan's earth square.</summary>
        private static Sprite VillageGround(TileVm t)
        {
            return SpriteBank.Rendered(t.Place == "banyan" ? "ground_square" : "ground_village_" + ((t.X + t.Y) % 2)) ?? SpriteBank.Sand;
        }
    }
}
