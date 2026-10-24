# 0064. The cattle acre: an inherited barren acre beside the yard for the cattle

- Status: Accepted
- Date: 2026-10-23
- Deciders: Keyur Patel
- Amends: spec 012 §3.3, §3.13, §7 (the shed's place), spec 010 §3.2 (a fourth inherited parcel), spec 002 (a new terrain), ADR 0030 (what the founder inherits)
- Related: ADR 0061, 0062 (a natural, spacious village), ADR 0009 (the inherited land is protected)
- GDD sections: Land, farming and livestock; Player journey and eras (the Homestead's start)

## Context

The first render put the shed, four animals, the straw stack and the compost pit on the yard's south acre (tile 103), between the house and the hands' huts, which already hold the tulsi, the haystack, the woodpile and trees. The owner found it cramped and asked for the cattle to have their own place beside the home. Three options were weighed: let the player place the shed on any owned acre (a field lost), a fixed inherited field (102), or the yard. The owner chose a fourth: the founder inherits one more acre for it.

## Decision

1. **The cattle acre.** The founder inherits one more acre, tile 104 (east of the yard's south acre, beside the hands' quarters), as its own protected parcel `home_d`. Its terrain is `cattle`: poor, sandy ground (usar) that is never farmed or sold, and never seized.
2. **Before the Hamlet** it shows rough grass and a few bushes, the farm's compost pit in its far corner (the pit belongs to the farm from the start, spec 012 §3.3), and a small sign: "Cattle yard: opens with the Hamlet". It grows nothing.
3. **From the Hamlet** `BuildShed()` builds the shed there (spec 012's `shedTile` 104). The acre is drawn as a village cattle yard (a baada), spread out: the shed and its feed room at the back, each animal at her own peg and manger in different places, a water trough, a neem tree with shade, the straw stack and the compost pit in their own corners, a low hedge with a gate to the lane. The yard keeps only what it has today.
4. **Inherited farm acres stay 6.** The cattle acre isn't a farm acre, so the Hamlet gate and the dairy balance (US-203) are unchanged; no field is lost to the cattle.
5. **Neighbours' parcel `e1`** gives up tile 104 and keeps 88, 89 and 105.
6. **Save schema 8** migrates a schema-7 or older town: tile 104 becomes the cattle acre in `home_d` (owned, protected); a crop or tree on it is paid for as when the farm lane was taken (spec 010 §3.4); a hand's order on it ends; `e1` keeps its other three acres and what was paid for them.
7. **Every map design looks spacious and natural** (owner, 2026-10-23): layouts are previewed to the owner and approved before they are final.

## Consequences

- Spec 014's citizens move to save schema 9.
- New story US-213 (the cattle acre: content, the terrain, the migration, the before-Hamlet look), 3 points; Sprint 12 is 52 points.
- US-204's shed is redrawn as the cattle acre and previewed before it's final.
