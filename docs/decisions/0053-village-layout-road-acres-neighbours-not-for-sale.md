# 0053. The village around the farm: road acres, the commons, and neighbours not for sale

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: spec 002 §3.1 (tile terrain kinds, what can be bought)
- Related: ADR 0052 (the 16 × 12 map), ADR 0054 (the trader's truck uses the roads), ADR 0056 (the commons feed animals), ADR 0011 (the founder funds the first school)
- GDD sections: Land, farming and livestock (Land); Community and civic services (the village centre)

## Context

A 16 × 12 map (ADR 0052) needs a village to be read as one: houses, the shops the founder already uses (the chemist, the dealer), a place for the first school, roads the trader and the mandi cart travel, and the common land villages graze their animals on. In a real Indian village most land is privately farmed and rarely comes up for sale, roads take land along their length, and the centre and the commons belong to everyone.

The owner decided the following with Claude on 2026-10-21, from a layout drawn in the session.

## Decision

1. **The layout.** North is the river side. Columns are 0–9 and A–F (A = 10 … F = 15), rows 0–11.

```
        0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
row 0   G  G  .  .  .  .  .  .  .  .  .  .  .  .  .  .   ← the river, past the north edge
row 1   G  G  .  .  .  .  .  .  .  .  .  .  .  .  .  .
row 2   .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .
row 3   .  .  F  F  F  F  F  F  F  F  L  V  V  V  V  .
row 4   .  .  F  F  F  F  F  F  F  F  L  V  V  V  V  .
row 5   .  .  F  F  F  F  F  F  F  F  L  V  V  V  V  .
row 6   .  .  F  F  F  F  F  F  F  F  L  V  V  V  V  .
row 7   .  .  F  F  F  F  F  F  F  F  L  .  .  .  .  .
row 8   .  .  F  F  F  F  F  R  F  F  L  .  .  .  .  .
row 9   .  .  .  .  .  .  .  R  .  .  L  .  .  .  .  .
row 10  .  .  .  .  .  .  .  R  .  .  L  .  .  .  .  .
row 11  D  D  D  D  D  D  D  D  D  D  D  D  D  D  D  D   ← the district road
```

   `F` today's 8 × 6 farm (ADR 0052), `V` the village centre, `L` the village lane, `R` the farm lane, `D` the district road, `G` the grazing commons (its exact extent is set in spec 010), `.` neighbours' farms.

2. **The village centre** (columns B–E, rows 3–6) holds the houses, the grocery, the tea stall, the chemist, the temple, the council hall and an empty school plot, with the banyan tree and threshing floor (T) and the pond (P). It is never for sale. Which acre holds what is content, set in spec 010. The school plot is where the founder builds the first school (spec 018, ADR 0011).
3. **The grazing commons** lie on the river bank in the north-west corner. Village animals graze there, which lowers how much green fodder the founder's animals need (ADR 0056). The commons and the river bank are never for sale.
4. **Neighbours' farms** fill the rest. Each neighbour's parcel is either **for sale** or **not for sale yet**. With today's buyable parcels, about 77 acres are for sale in Phase 2. Parcels not for sale may come up for sale in later eras, designed in those phases. As today, neighbours' land keeps its fertility (ADR 0030).
5. **Road acres.** An acre a road passes through is a **road tile**: the road and its verges (trees, a ditch, a stall or the tempo stop) take half of the acre. A road tile is never for sale and never sown. There are 27 road acres:
   - the **district road** along the south edge (row 11, 16 acres);
   - the **village lane** up column A (rows 3–10, 8 acres);
   - the **farm lane** up column 7 (rows 8–10, 3 acres) to the labour quarters at (7, 7). It takes today's parcel `s5`.

## Consequences

- **Spec 010** sets the tiles: the commons' extent, where T, P and each centre building stand, which neighbours' parcels are for sale, the new parcels' soils and starting values, and what happens to a save that owns `s5` (a gap to close with the owner).
- The roads give the trader's truck (ADR 0054) and the mandi trip a visible route, and give later eras a place for the highway link (Township gate).
- The chemist and the dealer, which Phase 1 only named, get a place on the map. The grocery and tea stall are there from the start; whether they become rivals to the player's own shops is decided in spec 017.
- Land prices are unchanged. "Not for sale yet" teaches that land is illiquid and rarely on the market, as it is in a real village.
