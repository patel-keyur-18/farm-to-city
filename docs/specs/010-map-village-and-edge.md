# 010. Map, village and edge

- Status: Approved (owner, 2026-10-21). All gaps closed 2026-10-21
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land, farming and livestock (Land, the village around the farm); Art, audio and feel (the map)
- Related ADRs: 0052 (the map grows by era), 0053 (the village layout, road acres, neighbours not for sale), 0030 (the inherited farm), 0040 (yard and labour quarters), 0047 (migrations and fixtures), 0035 (frame budget), 0041 (camera kept in settings)
- Depends on specs: 002 (land and soil: amended here), 007 (saves: schema 5), 008 (the map view: amended here)

## 1. Goal

The player's farm now sits in a living village: a river to the north, the village centre across the lane, roads the trader and the mandi cart use, and neighbours' fields, some for sale and most not.

**Lesson:** land is scarce and illiquid. Most of a village's land is never on the market, and what is for sale is priced by what it can earn.

## 2. Scope

**In:**
- The 16 × 12 map (192 tiles) with today's 8 × 6 farm unchanged inside it (ADR 0052).
- New terrain kinds: road, village and commons (ADR 0053).
- Neighbours' parcels for sale or not for sale yet; buying rules for the new land.
- The village centre's places (houses, shops, temple, council hall, school plot, banyan and threshing floor, pond) as content, drawn on the map, with a sheet when tapped.
- Save schema 5: the migration that remaps tile ids, and the farm-lane parcel `s5`.
- The camera: bounds on the larger map, opening on the yard.
- Natural terrain past the edge (presentation only).

**Out** (and where it goes instead):
- The trader's truck on the roads, and the truck stop: spec 011.
- What the commons does for animals: spec 012.
- Houses with citizens living in them: spec 014. In this spec houses are scenery.
- The player's own shops and the existing grocery and tea stall as businesses: specs 016 and 017.
- Building the school on its plot: spec 018.
- Later rings (Township and beyond): Phase 3 (F15.5).
- Roads the player builds, the highway link: Phase 3.
- People walking along roads (they keep walking straight to their fields): later.

## 3. Rules and formulas

### 3.1 The map

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Size | `MapDef.width` 16, `MapDef.height` 12. Tile id = `y × width + x`. Tiles are processed in id order (unchanged) | Phase 2: 16 × 12 |
| Terrain | Farm, Yard, Quarters (unchanged), and new: **Road** (a road acre: road and verges on half of it), **Village** (the village centre), **Commons** (the grazing commons on the river bank), **Bank** (the rest of the river bank) | |
| Parcels | Every tile belongs to exactly one parcel (unchanged). A parcel has `forSale` (default true). The village's own land (road, village, commons and bank tiles) is in parcels with `forSale: false`, and those parcels hold no farm tiles | 1–4 tiles a parcel for farmland; the village's parcels any size |
| Buying | `BuyParcel` adds one rule: a parcel with `forSale: false` is rejected with `NotForSale`. Adjacency is unchanged: one of the parcel's tiles shares an edge with an **owned** tile. Roads are never owned, so they don't connect land | |
| Not for sale yet | A neighbour's parcel (farm tiles) with `forSale: false`. It is drawn and priced like any farm land but can't be bought in Phase 2. Later eras may change `forSale` | |
| Sowing and building | Road, Village, Commons and Bank tiles are never owned, so `Sow` rejects them (`NotOwned`), `BuildHandHut` rejects them (`NotQuarters`, its existing first check), and they have no tree slots | |
| Soil on new tiles | Every tile keeps the soil properties of spec 002. Moisture runs every hour on every tile (unchanged). Fertility recovers only on owned farm tiles (unchanged); neighbours' land keeps its map fertility (ADR 0030) | |
| Prices | Unchanged (spec 002 §3.5, ADR 0028, 0033): soil, drainage, fertility, the parcel's `locationFactor`, the price index | |

### 3.2 The layout (content, `content/map.json`)

Columns 0–9 and A–F (A = 10 … F = 15), rows 0–11. North (row 0) is the river side. On screen, row 0 runs along the top-right edge and column 0 along the top-left edge (the isometric grid of spec 008 §3.4).

```
        0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
row 0   G  G  G  G  G  G  G  G  B  B  B  B  B  B  B  B   ← the river, past the edge
row 1   n  n  n  n  n  n  n  n  n  n  n  n  n  n  n  n
row 2   s  s  s  s  s  s  s  s  s  s  n  n  n  n  n  n
row 3   s  s  f  f  f  f  f  f  f  f  L  h  h  t  P  n
row 4   s  s  f  f  f  f  f  f  f  f  L  h  g  c  T  n
row 5   s  s  f  f  H  H  H  y  f  f  L  h  m  C  S  n
row 6   s  s  f  f  H  H  H  y  f  f  L  h  h  d  h  n
row 7   s  s  f  f  f  f  f  q  f  f  L  n  n  n  n  n
row 8   s  s  f  f  f  f  f  R  f  f  L  n  n  n  n  n
row 9   s  s  s  s  s  s  s  R  s  s  L  n  n  n  n  n
row 10  s  s  s  s  s  s  s  R  s  s  L  n  n  n  n  n
row 11  D  D  D  D  D  D  D  D  D  D  D  D  D  D  D  D   ← the district road
```

| Mark | What | Acres | For sale |
| --- | --- | --- | --- |
| `H`, `y`, `q` | The inherited farm, yard and labour quarters (`home_a`, `home_b`, `home_c`), protected | 9 | Never |
| `f` | Today's neighbours' parcels, unchanged (`nw1` … `s4`) | 38 | Yes |
| `s` | New neighbours' parcels for sale (§4: `r1`–`r5`, `w3`–`w6`, `s6`–`s9`) | 40 | Yes |
| `n` | Neighbours' farms **not for sale yet** | 46 | No (Phase 2) |
| `D`, `L`, `R` | Road acres: district road (row 11), village lane (column A, rows 3–10), farm lane (column 7, rows 8–10, taking `s5`) | 27 | Never |
| `h g t c m C d S T P` | The village centre (columns B–E, rows 3–6): `h` houses, `g` grocery, `t` temple, `c` tea stall, `m` chemist, `C` council hall, `d` the input dealer (seed, fertiliser, spray; §10 G5), `S` the empty school plot, `T` the banyan and threshing floor, `P` the pond | 16 | Never |
| `G` | The grazing commons: the west half of the river bank (§10 G2) | 8 | Never |
| `B` | The rest of the river bank: grass, reeds, a bathing ghat and the washing steps (§10 G2) | 8 | Never |
|  | **Buyable in Phase 2** (`f` + `s`) | **78** | |

- Today's tiles keep their soil, drainage, fertility, moisture and parcels; old `(x, y)` is new `(x + 2, y + 3)` (ADR 0052). Today's tile 45 (`s5`) becomes the farm lane's first acre.
- Row 0 is the river bank (16 acres, never for sale), with the commons in its west half. Row 1 is neighbours' riverside farmland with poor drainage (fertile, waterlogged in the Monsoon). The river itself is past the edge (§3.6), not tiles.
- **New parcels' soils** (placeholders, tuned with the economy model; §10 G4): row 2 riverside loam (fertility 55–60, moderate drainage, about 1,30,000 an acre); the west strip (columns 0–1) mixed loam and clay (48–55, moderate, about 1,03,000–1,15,000); the south strip (rows 9–10) sandy and loam near the road (42–48, good drainage, about 97,000 sandy to 1,30,000 loam).
- **Location factor by geography, for every parcel, old ones included** (§10 G4): `locationFactor` = **1.05** if any of its tiles shares an edge with a road, the lane or a village tile, or it lies on riverside row 2; **0.95** for the west strip (columns 0–1, rows 3–10) unless it touches a road; **1.0** otherwise. The values stay per parcel in content, and a content test checks them against this rule. Today's parcels change: `ne1`, `e1`, `se1`, `s2` 1.05; `n1`, `n2`, `n3`, `n4`, `sw`, `s1`, `s3`, `s4` 1.0. Owned land's market value moves with it; its cost basis doesn't.

### 3.3 Places

| Rule | Behaviour |
| --- | --- |
| Places | `MapDef.places`: a list of (`id`, `kind`, `tile`) on Village tiles. Kinds: `house`, `grocery`, `tea_stall`, `chemist`, `dealer`, `temple`, `council_hall`, `school_plot`, `banyan`, `pond`. Content only: places have no sim state in this spec |
| Tapping a place | Opens its sheet (§7). The chemist's sheet has **Buy medicine** (spec 003, the founder's), the dealer's has **Buy inputs** (spec 004). The others say what the place is and when it matters (the school plot: "The first school can be built here once the hamlet grows into a village"; the council hall: "The village council meets here once the hamlet becomes a village"). The existing ways in stay (Store → Buy inputs, the founder's Buy medicine). Buying costs no travel time or labour, as today |

### 3.4 Save schema 5: the migration

| Rule | Behaviour |
| --- | --- |
| Tile ids | A schema-4 save has 8 × 6 tiles. Each old tile id `i` becomes `Remap(i) = (i / 8 + 3) × 16 + (i mod 8 + 2)` (integer division). Every field holding a tile id is remapped: tiles (fertility, moisture, `vacantSince`, season totals), crops, tree slots, buildings, hand's-hut constructions, people's current tiles, standing-order targets, labourers' fields |
| New tiles | Tiles that didn't exist take their values from `content/map.json` |
| Parcels | Keep their ids, ownership, cost basis and loans. New parcels start unowned |
| `s5` | Its tile becomes a road acre. A save that owns `s5` is paid, in this order: (1) compensation of `2 × ParcelPrice` (`LandDef.roadTakeFactor` 2.0) at the migration's sim time; (2) a growing or ripe crop on it at its expected harvest × Floor, and each tree in its slots at book value; then crop and trees are removed. A loan secured on `s5` is repaid from the payment first (ADR 0038). The ledger records a sale (gain over cost basis: "Land taken for the farm lane"), and `ParcelTakenForRoad` is emitted for a one-time notice. New towns never own `s5` |
| Content references | `LandDef.well.irrigatedTiles` becomes 86 and 102, the start's buildings tile 87; the tests and fixtures that name tile ids are renumbered |
| Camera | The remembered camera position (settings, ADR 0041) is reset to open on the yard after a migration |
| Fixtures | A schema-5 fixture (the schema-4 fixture town migrated, then a new bot town) is added; schemas 3 and 4 still load (ADR 0047) |

### 3.5 The camera (amends spec 008 §3.4)

| Rule | Behaviour |
| --- | --- |
| Opening | The camera opens centred on the yard, at the zoom Phase 1 opened at (about 3 × 3 acres more than the closest zoom) |
| Zoom | From about 3 acres across (unchanged) out to the whole 16 × 12 map |
| Pan | Clamped so the camera's centre stays within the map's diamond. The edge terrain fills the rest of the screen (§3.6) |

### 3.6 The edge (presentation only)

| Rule | Behaviour |
| --- | --- |
| What | A band of land that can't be played, drawn around the map: **the river** past the north edge (beyond row 0's bank, top-right on screen); **hills** behind, past the west edge (column 0, top-left on screen); **scrub and neighbours' fields** past the south and east edges (the near side on screen), with the district road running on off both ends |
| Coverage | At every allowed camera position and zoom, every part of the screen outside the map shows edge terrain. The sky-blue background is never seen |
| State | None. The edge doesn't respond to taps |

### 3.7 Neighbours' land (presentation)

| Rule | Behaviour |
| --- | --- |
| Look | Neighbours' farm tiles show a seasonal crop by their soil (wheat or potato in Winter, rice in the Monsoon, lentils or fallow in Summer, potato or radish in Spring), drawn with the farm's half-grown crop sprite (no growth stages). The view picks each parcel's crop from its id and the season, so it is stable and changes at the season change. It has no sim state (§10 G6) |
| For sale | A small sign board on one tile of each parcel for sale. Tapping any tile of it opens the buy sheet (spec 002 §7) |
| Not for sale | Tapping opens a card: "<Name>'s farm · not for sale". Shows the size and soil, no price. Names are fixed per parcel in content (`parcels[].owner`, a string key) |
| Owned land | Unowned land is no longer desaturated (amends spec 008 §3.4). The player's land is marked by a subtle boundary line; the for-sale signs mark what can be bought |

### 3.8 Processing order

Unchanged: the hourly and daily passes walk tiles in id order, then parcels, buildings and slots in id order. `StateHash` covers the larger arrays the same way.

## 4. Data

**New or changed definitions:**

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `MapDef` | `width`, `height` | int | 16, 12 | ≥ 1 | Was 8 × 6 |
| | `tiles[].terrain` | enum | | `farm`, `yard`, `quarters`, `road`, `village`, `commons`, `bank` | |
| | `parcels[].forSale` | bool | true | | False for the village's land and neighbours not for sale |
| | `parcels[].owner` | string key | | Neighbours' farms only | The name key for the not-for-sale card |
| | `places` | list of (`id`, `kind`, `tile`) | §3.2 | `tile` is a Village tile; ids unique | |
| `LandDef` | `well.irrigatedTiles` | list of tile ids | 86, 102 | | Was 20, 28 |
| | `roadTakeFactor` | Fixed | 2.0 | ≥ 1 | Compensation for land a road takes (§3.4) |
| `StartDef` | `buildings` | list | hut and well on 87 | | Was 21 |
| `UiDef` | `mapEdge` | (`bandTiles`, sprites per side) | 6 tiles | ≥ the widest view past the map | Presentation only |

New parcels for sale (tile ids): `r1` 32, 33; `r2` 34, 35; `r3` 36, 37; `r4` 38, 39; `r5` 40, 41; `w3` 48, 49, 64, 65; `w4` 80, 81, 96, 97; `w5` 112, 113, 128, 129; `w6` 144, 145, 160, 161; `s6` 146, 147, 162, 163; `s7` 148, 149, 164, 165; `s8` 150, 166; `s9` 152, 153, 168, 169. Not-for-sale parcels are grouped in 2–4 acres in content.

**New or changed state:** none beyond the larger tile array. Schema 5 (§3.4).

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `BuyParcel` (changed) | as spec 002 | adds `NotForSale` (checked after `AlreadyOwned`, before `NotAdjacent`) | As spec 002 |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `ParcelTakenForRoad` | `parcelId`, `compensation`, `onIt` (crop and trees paid), `loanRepaid` | A migrated save owned `s5` (§3.4) | Ledger, the one-time notice, play log |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Buy a new neighbour's parcel (riverside, west or south) | Its price, upfront or with a land loan | More land to farm or to grow fodder on (spec 012) | Labour (Care falls without hands); credit (land is collateral) | Partly: a quick sale returns 80% |

## 7. UI

```
+---------------------------------------------------------------+
|  ~~~~~~~~ the river ~~~~~~~~                                    |
|   hills  /G G\ n n n n n ...                                   |
|         /s s s s s s s s s s\ n n                              |
|        / s s f f f f f f f f L h h t P\                        |
|          ... the farm ...     L  village  ...                  |
|        \ s s s s s s s R s s L n n n n /                       |
|         \D D D D D D D D D D D D D D D D/  scrub, fields       |
+---------------------------------------------------------------+

Place sheet (chemist)            Not-for-sale card
+---------------------------+    +------------------------------+
| The chemist               |    | Ramesh's farm                |
| Medicine for the family   |    | Not for sale                 |
| [Buy medicine · 240]      |    | 4 acres · loam               |
+---------------------------+    +------------------------------+
```

- Every number on the buy sheet keeps its *Why?* (spec 002 §7). The not-for-sale card shows soil and size, no price.
- Map markers, the season palette and the day and night tint extend to the new tiles and the edge.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | The shipped map is 16 × 12; every tile is in exactly one parcel; the 27 road tiles, 16 village tiles, the 8 commons and 8 bank tiles are where §3.2 puts them, in parcels with `forSale: false` and no farm tile; the inherited parcels hold tiles 84–87, 100–103 and 119; the well acres are 86 and 102 | `ContentTests.Map_Phase2Layout` |
| AC-2 | For every tile `i` of the 8 × 6 map, the new map's tile `Remap(i)` has the same terrain, soil, drainage, starting fertility and moisture and the same parcel id, except tile 45, which is a road acre | `ContentTests.Map_OldFarmInsideUnchanged` |
| AC-3 | Buyable farm tiles (`forSale` parcels, not inherited) number 60–80 | `ContentTests.Map_BuyableAcres` |
| AC-3b | Every parcel's `locationFactor` matches §3.2's geography rule | `ContentTests.Map_LocationFactors` |
| AC-4 | `BuyParcel` on a not-for-sale parcel is rejected with `NotForSale` and nothing changes; on the village's land too; a for-sale parcel whose only contact with owned land is across a road acre is rejected with `NotAdjacent` | `LandTests.Buy_NotForSale`, `LandTests.Buy_RoadsDontConnect` |
| AC-5 | `Sow` on a road, village, commons or bank tile is rejected with `NotOwned`, `BuildHandHut` with `NotQuarters`; none of them has a tree slot | `LandTests.VillageLand_NeverWorked` |
| AC-6 | The frozen schema-4 fixture loads at schema 5: every tile's plain values sit at `Remap(i)`; every crop, slot, building, construction, person and order points at the remapped tile; new tiles hold their content values; the town then advances a game day and round-trips byte for byte | `MigrationTests.Schema5_RemapsTiles` (and `MigrationTests.EverySchemaLoads`) |
| AC-7 | A schema-4 save that owns `s5` with a growing crop, a tree and a land loan secured on it loads with `s5` a road acre; cash rises by 2 × its price plus the crop's expected value and the tree's book value, less the loan repaid; the loan is closed; the ledger balances; `ParcelTakenForRoad` is emitted once | `MigrationTests.Schema5_FarmLaneParcel` |
| AC-8 | One game year on the new map with the Phase 1 bot gives the same hash run in one chunk or many (chunking invariance), and the golden replays on frozen content are unchanged | `LandTests.ChunkingInvariance_Phase2Map`, `ReplayTests.*` |
| AC-9 | The 16 × 12 grid's pick returns each tile's id at its centre and −1 outside; the camera opens centred on the yard; zoomed out all the way the whole map fits; pan keeps the centre inside the map | `MapTests.Grid16x12_PickAndBounds`, `MapTests.Camera_OpensOnYard` |
| AC-10 | For a sample of allowed camera positions and zooms (both extremes, each corner), the visible rectangle outside the map lies inside the edge band | `MapTests.Edge_CoversView` |
| AC-11 | Tapping a place opens its sheet: the chemist's offers Buy medicine, the dealer's Buy inputs, the rest their description; a for-sale parcel opens the buy sheet; a not-for-sale parcel opens its card with the owner's name and no price | `MapTests.PlaceAndParcelCards` |

On the device (test checklist, not automated): frame time on the iPhone 13 with the whole map in view stays within ADR 0035's budget (main thread p95 ≤ 8.3 ms, half the 60 fps frame).

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Competent bot's acres after 2 years | May rise: it can now buy past today's 38 | Land payback stays 8–15 seasons on an owner-operated acre (ADR 0033) |
| Hamlet unlock day (median) | Unchanged | Day 5–9 (ADR 0045) |
| Stress bots against competent | Unchanged | Within 1.20 × (ADR 0016) |

## 10. Open questions (closed with the owner, 2026-10-21)

- **G1. Which neighbours sell.** The layout in §3.2 puts the 40 new acres for sale to the west, the south and along row 2 (the farm's natural frontier), and keeps the riverside rows 0–1 and everything east of the lane not for sale. **Closed (owner, 2026-10-21): as drawn** (78 acres buyable, 46 not for sale yet).
- **G2. The commons and the river bank.** **Closed (owner, 2026-10-21):** row 0 is a full river-bank strip (16 acres, never for sale) with the 8-acre commons in its west half (columns 0–7); row 1 is neighbours' riverside farmland, not for sale yet, with poor drainage; the river lies past the north edge (§3.6).
- **G3. A save that owns `s5`.** **Closed (owner, 2026-10-21):** the village takes it for the lane and pays 2 × its current price, plus anything on it (§3.4).
- **G4. The new acres' soils and prices.** **Closed (owner, 2026-10-21):** the soil placeholders of §3.2, and one geography rule for every parcel's location factor (1.05 on a road, the lane, the village or riverside row 2; 0.95 the far west strip; 1.0 otherwise).
- **G5. The input dealer and place sheets.** **Closed (owner, 2026-10-21):** the dealer is added to the centre (column D, row 6); every place opens a sheet: the chemist's with Buy medicine, the dealer's with Buy inputs, the rest one line on what it is (§3.3).
- **G6. Neighbours' fields.** **Closed (owner, 2026-10-21):** seasonal crops on neighbours' land (the half-grown sprite, chosen by parcel and season), for-sale signs, a named not-for-sale card with size and soil, no desaturation of unowned land; the player's land gets a boundary line (§3.7).

## 11. Cut first

1. Neighbours' names on the not-for-sale card (a plain "Neighbour's farm" instead).
2. Neighbours' seasonal crops (plain tilled ground by season instead).
3. Place sheets other than the chemist's and the dealer's.
4. Animation in the edge band (river shimmer, swaying reeds).
