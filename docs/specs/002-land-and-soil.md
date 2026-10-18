# 002. Land and soil

- Status: Implemented (2026-10-17, Sprint 1). Approved 2026-10-17. Amended by ADR 0032 (well acres, 2026-10-18)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land, farming and livestock (Land, Fruit trees, Irrigation ladder); Assets and wealth (Farmland, Liquidity); City building (land bought parcel by parcel); Data model (`Tile` / `Parcel`)
- Related ADRs: 0032 (the well waters two well acres), 0030 (inherited 6-acre farm, 8 × 6 map, owned-only fallow), 0028 (land priced by earning power, land-purchase loans), 0008 (upfront payment, starting package), 0009 (protected inherited plot), 0014 (fertility and the Soil factor), 0017 (Phase 1 scope), 0021 (tree slots), 0024 (hut store)
- Depends on specs: 001 (time, weather, scheduler). Spec 005 (ledger) records the money; spec 003 (crops and trees) uses the soil, moisture, occupancy and slots defined here.

## 1. Goal

The player inherits two neglected acres, learns that every acre is different (soil, drainage, fertility and moisture all change what it can grow), and grows the farm by buying neighbouring parcels.

**Lesson:** land is a long-term asset whose value depends on how you treat it: resting land rebuilds it, and cheap land is cheap for a reason.

## 2. Scope

**In:**
- **The homestead map:** a small, hand-authored grid of 1-acre tiles, grouped into parcels that are bought whole.
- **The inherited start:** protected parcels with 6 farm acres and a yard holding the hut and the well (ADR 0030).
- **Soil properties per tile:** fertility, moisture, soil type and drainage.
  - moisture changes every hour with the weather, the soil and the drainage
  - the well keeps its two well acres from drying out completely (bucket watering, ADR 0032); the other acres are rain-fed
  - fertility recovers while a tile lies fallow, and other systems adjust it with a recorded reason
- **The Soil factor** used by the yield formula (spec 003), including the shade from field-edge trees.
- **Buying an adjacent parcel,** paid upfront, and a **quick sale** of a bought parcel at a discount.
- **Tree slots:** yard slots from the hut and field-edge slots on every owned farm acre.

**Out** (and where it goes instead):
- Crops, what they do to fertility, and the Water factor from moisture: 003 crops and yield.
- Trees themselves (saplings, growth, fruit): 003. This spec defines only the slots and the shade rule.
- The ledger postings behind buying and selling: 005 ledger and credit. This spec says what is paid and when.
- Hut store capacity and spoilage: 004 goods, market and storage.
- The hand's hut and other construction: 006 people (hand's hut), and later building specs.
- Land value from services, zoning and appreciation as the town grows: Phase 3 (F15.3). In Phase 1 a parcel's price depends only on its soil, drainage, fertility and location.
- Irrigation beyond the well, droughts and floods: Phase 2 (irrigation) and the Events spec.
- Leasing land: not in Phase 1 (ADR 0028).
- The map screen and overlays: 008 HUD. This spec defines what the field panel and the buy sheet show.

## 3. Rules and formulas

All soil quantities are `Fixed` (ADR 0018). Every `Fixed` multiply rounds to nearest, ties away from zero (spec 001 §3.1). Money is `long` in 1/100 coin.

### 3.1 Map, tiles and parcels

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Tile | One acre. Has a `terrain` (Farm or Yard), a soil type, a drainage class, fertility and moisture. Tile id = `y × width + x`; tiles are always processed in id order | Map from `MapDef`, default 8 × 6 tiles |
| Parcel | A group of tiles bought and sold together. Every tile belongs to exactly one parcel | 1–4 tiles per parcel |
| Inherited parcels | Listed in `StartDef.inheritedParcels`. Owned from the start and **protected**: they can never be sold, seized or used as collateral (ADR 0009, 0030). Everything on them (hut, well, trees) is protected with them | At least one; together they hold a yard tile and the well |
| Adjacency | A parcel can be bought only if one of its tiles shares an edge (not a corner) with an owned tile | 4-neighbour |
| Yard tiles | Hold buildings and yard tree slots. They are never sown and never counted as farmed acres | |

### 3.2 Moisture (hourly)

Moisture runs from 0 (bone dry) to 100 (saturated). At every hourly pass (system Agriculture), each tile on the map, owned or not, is updated in id order using the weather of the hour that just ended (the spell containing `t − 3600`):

1. **Weather:** `d = LandDef.moistureByWeather[w]` (points per hour, signed).
   - If `d > 0`: `m ← m + d × SoilDef.gainFactor` (how much rain soaks in).
   - If `d < 0`: `m ← m + d × SoilDef.lossFactor` (how fast the soil dries).
2. **Clamp** to `[0, 100]`.
3. **Drainage:** if `m > DrainageDef.drainAbove`, then `m ← m − (m − drainAbove) × DrainageDef.drainRatePerHour`.
4. **Well:** on the well acres (`WellDef.irrigatedTiles`, ADR 0032), `m ← max(m, WellDef.moistureFloor)`. This models the founder carrying buckets; spec 003 charges the labour hours for it. Every other tile, inherited or bought, is rain-fed.

Examples (loam, moderate drainage, defaults below):
- `m = 50`, an hour of rain: `50 + 3.0 × 1.0 = 53.0`. Below 70, so no drainage. Result **53.0**.
- `m = 80`, an hour of sun: `80 − 0.7 × 1.0 = 79.3`, then `79.3 − 9.3 × 0.15 = 77.905`. Result **77.905**.

What moisture does to a crop (too dry, waterlogged) is the Water factor in spec 003.

### 3.3 Fertility

Fertility runs from 0 to 100.

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Starting fertility | From `MapDef` per tile. The inherited acres start **neglected** (default 45) | 0–100 |
| Adjustments by other systems | `AdjustFertility(tile, delta, reason)`: adds `delta`, clamps to `[0, 100]`, and adds the *applied* change to the tile's per-season total for that reason (for *Why?*). Reasons: CropUse, Fertiliser, Compost, Lentils (spec 003), Fallow (this spec) | |
| Fallow recovery | At each daily pass, an **owned** farm tile that had **no crop at any moment in the previous 24 hours** gains `LandDef.fallowRecoveryPerSeason ÷ ClockDef.daysPerSeason` (a Fixed division, rounded), but never rises above its `SoilDef.fallowCap`. A tile already at or above the cap is unchanged. Unowned land is neighbours' farms, cropped in a steady state, so it keeps its map fertility (ADR 0030) | Default 6 points per season (1.5 a day), cap by soil |
| Occupancy | Spec 003 marks a tile occupied when sown and vacant when harvested or cleared. The tile records `vacantSince` (sim time, or −1 while occupied) | |

The fallow rule uses "no crop for the whole previous day", so harvesting just before a daily pass and resowing just after earns nothing.

### 3.4 Soil factor (used by spec 003)

```
SoilFactor(tile) = SoilDef.yieldFactor × FertilityFactor(fertility) × ShadeFactor(tile)
```

- **FertilityFactor** is piecewise linear through the points in `LandDef.fertilityFactorCurve`, default `(0, 0.50)`, `(60, 1.00)`, `(100, 1.15)`. Between points: `y0 + (y1 − y0) × (f − x0) ÷ (x1 − x0)`, Fixed, rounded.
  - Examples on loam: fertility 30 → 0.75; 45 → 0.875; 60 → 1.00; 80 → 1.075.
- **ShadeFactor** = `max(LandDef.minShadeFactor, 1 − LandDef.shadePerMatureTree × n)`, where n is the number of **mature** trees in that tile's field-edge slots. Default 0.03 per tree: 0 trees → 1.00, 1 → 0.97, 2 → 0.94.
- Shade is folded into the Soil factor so the yield formula of ADR 0014 keeps its factor list. The *Why?* breakdown still shows shade as its own line.

### 3.5 Parcel price, buying and quick sale

```
TilePrice  = round_coin( LandDef.acreBasePrice × SoilDef.priceFactor × DrainageDef.priceFactor
                         × FertilityPriceFactor(fertility) × ParcelDef.locationFactor )
ParcelPrice = Σ TilePrice over the parcel's tiles

FertilityPriceFactor(f) = LandDef.fertilityPriceMin + (LandDef.fertilityPriceMax − LandDef.fertilityPriceMin) × f ÷ 100
```

- The factors are multiplied as `Fixed` (rounded at each step), applied to the money amount once (rounded to 1/100 coin), then rounded to a whole coin (ties away from zero).
- Example: 1 acre of loam, moderate drainage, fertility 50, location 1.0: `1,20,000 × 1.00 × 1.00 × 1.00 × 1.0` = **1,20,000 coins**.
- Yard tiles are priced like farm tiles of their soil.
- **Market value** (for net worth, spec 005) is the current `ParcelPrice`, including for the inherited parcels. Phase 1 prices change through fertility and the price index: `acreBasePrice` is a year-0 price charged at `Indexed(acreBasePrice)` (spec 004 §3.1, ADR 0034). Book value (cost basis) stays at cost.

| Rule | Behaviour |
| --- | --- |
| Buy | Paid in full, upfront, at the current `ParcelPrice` (ADR 0008). The parcel becomes owned, its farm tiles gain field-edge slots, and its cost basis is recorded for the ledger (spec 005) |
| Financing | A purchase can be part-financed by a land-purchase loan secured by the parcel itself (spec 005 §3.6, ADR 0028). This spec only needs the cash leg to succeed |
| Quick sale | A bought parcel can be sold at once to a neighbour for `round_coin(ParcelPrice × LandDef.quickSaleFactor)` (default 0.80). The discount teaches liquidity: land is slow to turn into cash |
| Sale blocked | While any tile of the parcel has a crop (growing or ready) or any of its slots holds a tree, or while it holds a building. The player clears it first. Inherited parcels can never be sold |
| Adjacency after a sale | Selling never strands other owned parcels: a sale is rejected if any remaining bought parcel would no longer connect to the inherited land through owned tiles |

### 3.6 Tree slots

| Slot kind | Where | Count | Effect |
| --- | --- | --- | --- |
| Yard | On yard tiles, around a building | `BuildingDef.yardTreeSlots` per building (hut: 4) | None on crops |
| Field edge | On each owned **farm** tile | `LandDef.fieldEdgeSlotsPerAcre` (default 2) | Mature trees shade the tile's crop (§3.4) |

- Slots exist only on owned land. Buying a parcel creates its slots; selling removes them (only empty slots can be sold, §3.5).
- Slot ids are assigned in creation order and never reused.

### 3.7 Processing order and determinism

- Moisture: hourly pass, system Agriculture, tiles in id order.
- Fallow recovery: daily pass, system Agriculture, tiles in id order (after the hourly pass at the same time, per spec 001).
- All land state is written to `StateHash` after the spec 001 sections: tiles in id order, then parcels, buildings and slots in id order.

## 4. Data

**Definitions:**

`content/land.json` (`LandDef`, `SoilDef`, `DrainageDef`, `WellDef`):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `SoilDef` | `id` | string | `loam`, `clay`, `sandy` | | |
| | `nameKey` | string | | | |
| | `yieldFactor` | Fixed | 1.00 / 0.95 / 0.85 | 0.5–1.2 | Soil factor |
| | `gainFactor` | Fixed | 1.0 / 1.1 / 0.8 | 0.2–2 | Rain soaking in |
| | `lossFactor` | Fixed | 1.0 / 0.7 / 1.5 | 0.2–3 | Drying out |
| | `priceFactor` | Fixed | 1.00 / 0.90 / 0.75 | 0.3–2 | |
| | `fallowCap` | Fixed | 75 / 70 / 55 | 0–100 | Fallow recovery stops here |
| `DrainageDef` | `id` | string | `poor`, `moderate`, `good` | | |
| | `drainAbove` | Fixed | 85 / 70 / 65 | 0–100 | |
| | `drainRatePerHour` | Fixed | 0.02 / 0.15 / 0.25 | 0–1 | Share of the excess drained each hour |
| | `priceFactor` | Fixed | 0.85 / 1.00 / 1.05 | 0.3–2 | |
| `LandDef` | `moistureByWeather` | map weather id → Fixed | sun −0.7, cloudy −0.2, rain 3.0, heavy_rain 8.0, heatwave −1.2, frost −0.1, storm 5.0 (tuned, US-014) | −10 to 10 | Points per hour. Every weather id in `weather.json` must appear |
| | `fertilityFactorCurve` | list of (fertility, factor) | (0, 0.50), (60, 1.00), (100, 1.15) | x ascending from 0 to 100; factors 0–2 | |
| | `fallowRecoveryPerSeason` | Fixed | 6 | 0–25 | Points |
| | `acreBasePrice` | money (coins) | 1,20,000 | > 0 | About 8–15 seasons of an owner-operated acre's net farm income (ADR 0028, 0033); the economy model measures about 7–8 |
| | `fertilityPriceMin`, `fertilityPriceMax` | Fixed | 0.80, 1.20 | 0.1–3, min ≤ max | |
| | `quickSaleFactor` | Fixed | 0.80 | 0.3–1 | |
| | `fieldEdgeSlotsPerAcre` | int | 2 | 0–4 | |
| | `shadePerMatureTree` | Fixed | 0.03 | 0–0.2 | |
| | `minShadeFactor` | Fixed | 0.85 | 0.5–1 | |
| `WellDef` | `moistureFloor` | Fixed | 30 | 0–100 | Applies to the well acres only |
| | `irrigatedTiles` | list of tile ids | 20, 28 | Non-empty, no duplicates; farm tiles of the inherited parcels (ADR 0032) | The well acres |

ADR 0021's `TreeSlotDef` (slot counts and shade) is realised as `LandDef.fieldEdgeSlotsPerAcre`, `LandDef.shadePerMatureTree`, `LandDef.minShadeFactor` and `BuildingDef.yardTreeSlots`, because yard slots depend on the building (more for bigger houses).

`content/map.json` (`MapDef`, `ParcelDef`):

| Definition | Field | Type | Notes |
| --- | --- | --- | --- |
| `MapDef` | `width`, `height` | int | 2–16 each |
| | `tiles` | list in id order of (`terrain`, `soil`, `drainage`, `fertility`, `moisture`) | Starting values |
| `ParcelDef` | `id` | string | |
| | `tiles` | list of tile ids | Non-empty; every tile in exactly one parcel |
| | `locationFactor` | Fixed | 0.5–2. Near the village track costs more |

`content/buildings.json` (`BuildingDef`, the fields this spec needs; later specs add more):

| Field | Type | Default (hut / well) | Notes |
| --- | --- | --- | --- |
| `id`, `nameKey` | string | `hut`, `well` | |
| `yardTreeSlots` | int | 4 / 0 | |
| `value` | money | 40,000 / 15,000 | Opening book value (spec 005 depreciates it) |
| `usefulLifeSeasons` | int | 80 / 120 | Spec 005 |

`content/start.json` (`StartDef`, land part; spec 005 adds `startingCash`):

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `inheritedParcels` | list of parcel ids | `home_a`, `home_b` | Non-empty, no duplicates; together they hold at least one yard tile and the well (ADR 0030) |
| `buildings` | list of (building id, tile id) | hut and well on the yard tile 21 | Yard tiles of the inherited parcels only |

Default map (8 × 6, 19 parcels of 1–4 acres; `H` = inherited, `y` = yard; tile id = y × 8 + x):

```
      x=0    x=1    x=2    x=3    x=4    x=5    x=6    x=7
y=0   sandy  sandy  loam   loam   clay   clay   sandy  sandy     nw1, n1, n2, ne1
y=1   loam   loam   loam   clay   loam   clay   sandy  loam      nw1, n3, n4, ne1
y=2   loam   loam   H:loam H:loam H:loam H:yard clay   clay      w1, home_a (18–21), e1
y=3   loam   clay   H:loam H:loam H:loam loam   sandy  clay      w2, home_b (26–28), e2, e1
y=4   loam   clay   clay   loam   loam   clay   sandy  sandy     sw, s1, s2, e2, se1
y=5   clay   clay   loam   loam   loam   loam   sandy  sandy     s3, s4, s2, s5, se1
```

The exact parcel layout, drainage and starting values are content, tuned with the economy model.

**State** (part of `GameState`):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Tile` | `id`, `terrain`, `soilId`, `drainageId` | | Static after new game |
| | `fertility`, `moisture` | Fixed | |
| | `parcelId` | int | |
| | `vacantSince` | long | Sim time the tile became vacant, or −1 while occupied |
| | `fertilityChanges` | list of (reason, Fixed) | Totals for the current season, in reason order; reset at season start |
| `Parcel` | `id`, `owned`, `protected` | | |
| | `costBasis` | long | 1/100 coin. The price paid, or the opening value for an inherited parcel |
| | `acquiredAt` | long | Sim time |
| `Building` | `id`, `defId`, `tileId` | | Hut and well for now |
| `TreeSlot` | `id`, `kind`, `tileId`, `treeId` | | `treeId` is 0 when empty (spec 003 sets it) |
| `Land` | `nextSlotId`, `nextBuildingId` | long | |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `BuyParcel` | `parcelId`, optional `financing` (spec 005) | `UnknownParcel`; `AlreadyOwned`; `NotAdjacent`; `InsufficientFunds` (cash plus any approved financing is less than the price) | Pays the price upfront (spec 005), marks the parcel owned, creates field-edge slots. Emits `ParcelBought` |
| `SellParcel` | `parcelId` | `UnknownParcel`; `NotOwned`; `Protected`; `ParcelInUse` (crop, tree or building); `WouldStrandLand` | Pays the quick-sale price into cash (spec 005), removes ownership and slots. Emits `ParcelSold` |

**Internal operations** (called by other systems, never by the UI):
- `AdjustFertility(tileId, delta, reason)` (spec 003)
- `SetOccupied(tileId, occupied)` (spec 003)
- `SetTree(slotId, treeId)` (spec 003)
- `SeizeParcel(parcelId)` (spec 005 foreclosure): like a sale without the in-use checks; whatever is on the parcel goes with it

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `ParcelBought` | `parcelId`, `price`, `acres` | A purchase succeeds | Ledger (005), HUD, play log (007), Hamlet unlock (009) |
| `ParcelSold` | `parcelId`, `price`, `costBasis` | A quick sale succeeds | Ledger, HUD |
| `ParcelSeized` | `parcelId`, `quickSaleValue` | Foreclosure takes a parcel (005) | Ledger, HUD, Chronicle (later) |

Moisture and fertility changes emit no events: they change every hour, and the HUD reads them from state.

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Buy a parcel | Its full price, upfront (or a loan, spec 005) | More acres to farm; more output if there is labour to tend it | Labour (care falls past about 3 acres, ADR 0010); net worth (cash becomes land); credit (land is collateral) | Partly: a quick sale returns only 80% |
| Cheap sandy or poorly drained land, or dear loam | Lower or higher price | Sandy dries fast and caps fallow recovery low; poor drainage waterlogs in the Monsoon | Yield (003), moisture | As above |
| Leave a field fallow | A lost crop cycle | Fertility recovers 6 points a season, up to the soil's cap | Yield next season (Soil factor) | Fully: sow again any time |
| Plant field-edge trees | The sapling (003) | Fruit income later | Shade costs the acre about 3% yield per mature tree | Removing a tree loses it |

## 7. UI

The map and HUD are spec 008; this spec defines the field panel content, the buy sheet and their *Why?* lines.

```
Field panel (tap an acre)
+---------------------------------------------------+
| North acre (loam, moderate drainage)    [Owned]   |
| Fertility   45 / 100   [Why?]                     |
|   started this season 43.5; fallow +1.5           |
| Moisture    62 / 100   (drains above 75)          |
| Soil factor 0.875      [Why?]                     |
|   loam 1.00 x fertility 0.875 x shade 1.00        |
| Edge slots  [mango, mature] [empty]               |
| [Sow]                     [Sell parcel: 94,080]   |
+---------------------------------------------------+

Buy sheet (tap an unowned parcel next to your land)
+---------------------------------------------------+
| East parcel, 2 acres                              |
| Price 2,28,960 coins   [Why?]                     |
|   acre 1: loam 1.00 x drainage 1.05 x fert. 0.96  |
|   acre 2: clay 0.90 x drainage 1.00 x fert. 1.00  |
|   x location 1.00                                 |
| Cash 58,200  -> short by 1,70,760                 |
| [Buy]   [Buy with a loan...]  (spec 005)          |
| Resale now: 1,83,168 (quick sale at 80%)          |
+---------------------------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given a new game with the default content, then the inherited parcels (`home_a`, `home_b`) are owned and protected, hold 6 farm tiles and 1 yard tile, the hut and the well stand on the yard tile, the inherited farm tiles have fertility 45, the hut provides 4 yard slots, and the farm tiles have 12 field-edge slots | `LandTests.NewGame_InheritedPlot` |
| AC-2 | Loading `map.json` where a tile is in two parcels, or in none, or a tile names an unknown soil, or `land.json` misses a weather id in `moistureByWeather`, or `start.json` lists inherited parcels without a yard or twice, fails validation naming the field | `ContentTests.Land_Validation` |
| AC-3 | Given loam, moderate drainage, moisture 50, when an hour of rain passes, then moisture is exactly 53.0. Given moisture 80 and an hour of sun, then exactly 77.905 (the tuned content, US-014) | `MoistureTests.HourlyUpdate_Exact` |
| AC-4 | Given three tiles at moisture 95 with poor, moderate and good drainage, after 24 hours of sun their moisture is strictly ordered poor > moderate > good | `MoistureTests.Drainage_Order` |
| AC-5 | Over 3 game years for 20 seeds, every tile's moisture stays within [0, 100] and fertility within [0, 100] | `MoistureTests.StaysInBounds` |
| AC-6 | Through a Summer of only sun and heatwave, the well acres never fall below the well floor (30), and a rain-fed inherited tile and a bought tile of the same soil fall below it. Loading a well tile that isn't an inherited farm tile fails validation | `MoistureTests.WellFloor_WellAcresOnly` |
| AC-7 | Given a vacant farm tile at fertility 45 (cap 75), after 4 daily passes it is exactly 51. A tile occupied at any moment of a day gains nothing for that day. A tile at the cap stays at the cap. An unowned tile keeps its fertility | `FertilityTests.Fallow_RecoversProRata` |
| AC-8 | `AdjustFertility` clamps to [0, 100], records the applied (clamped) change under its reason, and the season totals reset at the next season start | `FertilityTests.Adjust_ClampsAndRecordsReasons` |
| AC-9 | On loam with no trees, the Soil factor at fertility 30, 45, 60 and 80 is exactly 0.75, 0.875, 1.00 and 1.075; on sandy at 60 it is 0.85 | `SoilFactorTests.PiecewiseLinear` |
| AC-10 | With 0, 1 and 2 mature edge trees on a tile, the shade factor is exactly 1.00, 0.97 and 0.94. With `fieldEdgeSlotsPerAcre` = 4 and `minShadeFactor` = 0.90 in test content, 4 mature trees give 0.90 (the floor, not 0.88). Saplings cast no shade | `SoilFactorTests.Shade_MatureTreesOnly` |
| AC-11 | Given a 1-acre loam parcel with moderate drainage, fertility 50 and location 1.0, its price is exactly 1,20,000 coins; a 2-acre mixed parcel's price equals the sum of its tile prices, rounded per tile | `ParcelTests.Price_Formula` |
| AC-12 | Buying an adjacent parcel with enough cash succeeds: cash falls by exactly the price, the parcel is owned, its farm tiles each gain 2 edge slots, and `ParcelBought` is emitted. A non-adjacent parcel is rejected with `NotAdjacent`, an owned one with `AlreadyOwned`, an unaffordable one with `InsufficientFunds`, and none of these change state | `ParcelTests.Buy_RulesAndPayment` |
| AC-13 | After buying a parcel, parcels adjacent to it become buyable; diagonal neighbours do not | `ParcelTests.Adjacency_Grows` |
| AC-14 | A quick sale pays exactly 80% of the current price, rounded to the coin. Selling an inherited parcel is rejected with `Protected`; a parcel with a crop, a tree or a building with `ParcelInUse`; a sale that would disconnect another bought parcel with `WouldStrandLand` | `ParcelTests.Sell_QuickSale_AndGuards` |
| AC-15 | Land state is part of `StateHash`, and the spec 001 chunking-invariance check (1 game year in one call against 500 chunks) still holds with land and a fallow schedule | `LandTests.ChunkingInvariance_WithLand` |
| AC-16 | Performance (CI, Release): advancing 72 h with clock, weather and the default map takes under 20 ms | `PerfTests.Advance72h_WithLand` (category `Perf`) |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Mean moisture of an unwatered loam acre, by season (realistic Indian seasons; rabi crops grow on the moisture left by the monsoon) | Spring 30–50, Summer 15–35, Monsoon 65–85, Winter 40–60. Tuned by the economy model (US-014), with a test | ±5 |
| Seasons of fallow to bring a neglected acre (45) to 60 | 2–3 seasons | ±1 season |
| Price of an average acre, in seasons of an owner-operated acre's net farm income | 8–15 seasons (payback within 2–4 game years; ADR 0033). With a hired hand it is far slower (about 25–50 seasons) | Economy model (F00.3) |
| Share of a 2-acre farm's yield lost to 4 mature edge trees | About 6% of the shaded acres | ±1 point |

## 10. Open questions

All six are resolved. Questions 1–5 were resolved at approval (2026-10-17), following the recommendations; question 6 and the change to question 1 in the Sprint 1 review (2026-10-17):

1. **Land prices and the Hamlet pace (ADR 0028).** The Hamlet target is now the end of the first week (about 2 seasons), with the Village at 2–3 weeks and the Township at 4–6 weeks. Land is priced by earning power: an acre costs about 8–15 seasons of its net farm income (placeholder 1,20,000 coins). A bank loan can be secured by the parcel it buys (spec 005 §3.6). There is no leasing in Phase 1.
2. **The well waters the well acres** (moisture floor 30), and spec 003 charges the bucket-carrying labour. Since ADR 0032 these are two named inherited acres (tiles 20 and 28); the other inherited acres and all bought land are rain-fed until irrigation arrives in Phase 2.
3. **Shade goes inside the Soil factor,** so ADR 0014's formula and the 1.6× cap are unchanged. *Why?* shows shade as its own line.
4. **Quick sale in Phase 1** at 80% of the price, immediately. The slow, better-priced sale comes with land value in Phase 3.
5. **The map is hand-authored** for Phase 1 (6 × 5 acres at approval, 8 × 6 since ADR 0030; parcels of 1–4 acres).
6. **Fertility on land nobody owns (Sprint 1 review):** unowned land is neighbours' farms, so it keeps its map fertility and unbought parcels don't drift in price (ADR 0030). Implemented: fallow recovery applies to owned tiles only.
7. **The inherited farm (Sprint 1 review, ADR 0030):** the player inherits 6 protected farm acres and the yard in two parcels, on an 8 × 6 map; the Hamlet no longer depends on buying land. The well's floor covered every inherited acre until ADR 0032 limited it to two well acres.
8. **The moisture hook** was made realistic (Sprint 1 review): Spring 30–50, Summer 15–35, Monsoon 65–85, Winter 40–60, tuned in US-014.
10. **Moisture tuned (Sprint 2, US-014.T6).** `moistureByWeather`, moderate drainage (above 70 at 0.15 an hour, close to field capacity) and spec 001's climate weights were tuned so an unwatered loam acre averages about Spring 33, Summer 28, Monsoon 67 and Winter 52 over 200 years (`dotnet run --project tools/Tools.Balancer -- moisture`), inside every range. Clay holds about 20 points more and sandy about 17 less. Data only: the rule is unchanged. `MoistureTests.SeasonalMeans_Realistic` guards it.
9. **The well acres (Sprint 2 planning review, ADR 0032):** the well waters two named inherited acres (`WellDef.irrigatedTiles`: 20 and 28), not all six. A hand-drawn well realistically waters 1–2 acres, and bucket labour on six acres made the Hamlet gate's Care of 0.9 unreachable. Implemented in Sprint 2 (rule change approved by the owner, 2026-10-18).

### Implementation notes (Sprint 1, 2026-10-17)

None of these changes a formula or a rule.

1. **"Exactly" in AC-3.** In Q31.32, decimals such as 0.7 and 0.15 aren't exact binary fractions, so the 80 → 77.905 example lands within a unit in the last place (2⁻³²) of `Fixed.Parse("77.905")`. The test checks it to that precision and also pins the raw value, so any change in the arithmetic still fails it. The other examples (53.0, the Soil factors, the shade factors) are exact. (Sprint 2: the examples follow the tuned content.)
2. **The hour's weather.** At a spell boundary the weather buffer has already dropped the spell that just ended, so the land system records the weather of each hour as it starts (`LandState.HourWeather`) and applies it at the next hourly pass. The result is exactly §3.2's "the spell containing t − 3600".
3. **Vacant since the start.** Every tile starts vacant since t = 0, so the first daily pass already counts as a fallow day for the inherited acres.
4. **The well** is the building whose id is `well`; every start must place it on the inherited land. Since ADR 0032 the floor applies to `WellDef.irrigatedTiles` only (Sprint 2).
5. **Tree maturity.** A slot carries `TreeMature`. Spec 003 calls `SetTree(slot, treeId)` when it plants a sapling (or 0 to empty the slot) and `SetTreeMature(slot)` when the tree matures; only mature trees shade.
6. **Optional content.** Land and ledger content is an optional `GameDefs` in `SimConfig`. A simulation without it runs only time and weather and hashes exactly as before, so the spec 001 golden replay keeps its frozen hash.
7. **The default map** (`content/map.json`) is 8 × 6 with 19 parcels of 1–4 acres around the inherited `home_a` (tiles 18–21, the yard is 21) and `home_b` (26–28). Its soils follow §4; drainage, fertility, moisture and location factors are placeholders for the economy model.

### Changes after approval (Sprint 3 planning review, 2026-10-18, ADR 0034)

- **Land prices follow the price index** (§3.5): `TilePrice` uses `Indexed(acreBasePrice)`, for buying, quick sale and net worth. Cost basis stays at cost, so a sale after inflation shows a gain (4800).

## 11. Cut first

1. `WouldStrandLand` (allow any sale of a non-inherited parcel).
2. Quick sale (`SellParcel`); foreclosure still seizes land.
3. Shade from field-edge trees (the factor is always 1).
4. Per-reason fertility totals (keep the clamp; *Why?* shows only the current value).
5. Drainage classes (every tile drains like moderate).
