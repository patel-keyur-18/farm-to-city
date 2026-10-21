# 0052. The map grows by era: 16 × 12 acres in Phase 2, ending in natural terrain

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0030 (the 8 × 6 map); spec 002 §3.1 and §4 (`MapDef`, the default map); spec 007 (save schema); spec 008 (the map view and camera)
- Related: ADR 0053 (the village layout on this map), ADR 0040 (yard and labour quarters), ADR 0047 (frozen fixture saves, migrations), ADR 0035 (frame budget)
- GDD sections: Land, farming and livestock (Land); Roadmap and milestones (Phase 2)

## Context

Phase 1 plays on an 8 × 6 map (48 acres): the 9 inherited acres and about 38 acres of neighbours' parcels to buy. By the end of the playtest week the player had bought most of what an owner-operated farm can use, and there was no room for what Phase 2 adds: a village with households, shops and a school, roads for the trader and the mandi, grazing for animals, and more land to grow into. Past the map's edge the view showed the sky-blue background, which breaks the picture of a farm in a valley.

The owner decided the following with Claude on 2026-10-21, one question at a time.

## Options

1. **Keep 8 × 6** and put the village off-screen. It's cheap, but the village is what Phase 2 is about, and it would never be seen.
2. **One map for the whole game now** (for example 48 × 36). It would need the Township, Town and City layouts designed before their systems exist, and it would cost frame time on the iPhone 13 for land nobody can use yet.
3. **The map grows by era.** Each phase adds the ring of land its era needs and designs it when that era's systems are designed (rolling wave, ADR 0019).

## Decision

Option 3.

1. **The map grows by era.** Phase 2 (Hamlet and Village) plays on **16 × 12 acres** (192 tiles, 1 tile = 1 acre). A town from Homestead to Village has this map. Township, Town and City each add a ring around it when the town reaches that era, designed in their phases (F15.5 and later).
2. **Today's 8 × 6 farm sits inside it, unchanged,** at columns 2–9 and rows 3–8. Its soils, drainage, starting fertility, parcels, parcel ids, yard, labour quarters and well acres stay as they are. Only the tile ids change: tile id = `y × 16 + x`, and an old tile `(x, y)` becomes `(x + 2, y + 3)`. For example, the well acres 20 and 28 become 86 and 102, the yard 21 and 29 becomes 87 and 103, and the labour quarters 37 become 119.
3. **About 60–80 acres can be bought in Phase 2:** today's buyable parcels plus the neighbours' parcels for sale (ADR 0053). The adjacency rule is kept: a parcel can be bought only if it shares an edge with an owned tile. The village centre, road acres, commons and river bank can never be bought.
4. **A save migration remaps tile ids** in every field that holds one: tiles, crops, trees and their slots, buildings, standing orders, people's places and anything else keyed by tile. It is tested on the frozen fixture saves (ADR 0047). Parcels keep their ids.
5. **Natural terrain past the edge.** The map is framed by land that can't be played: hills behind, the valley's river along one side, scrub and neighbours' fields. The camera stops at the edge of the playable map, so the sky-blue background is never seen. The frame is presentation only: it has no sim state.
6. **The camera opens on the farm** (the yard), not on the whole map. The player pans out to the village.

## Consequences

- **Accepted costs:**
  - the save migration and its tests;
  - new terrain art (river bank, hills, scrub, roads, the village), made in Blender with the ADR 0041 pipeline;
  - an iPhone 13 frame-time check at 192 tiles against ADR 0035's budget;
  - the tests and fixtures that name tile ids are renumbered.
- **Spec 010** (map, village and edge) fixes the layout, the migration, the camera bounds and the edge terrain. Spec 002's default map and spec 007's save schema are amended there.
- **Land stays scarce.** Today's 38 buyable acres become about 60–80. Land prices are unchanged (ADR 0028, 0033), so the extra land is the room to grow that hiring needs (ADR 0039 §11), not a windfall.
- Larger later maps reuse the same migration step: each new ring shifts or extends tile ids once, at the era change.
