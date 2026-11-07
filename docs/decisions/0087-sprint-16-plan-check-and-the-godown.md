# 0087. Sprint 16 plan check: the canal's place, one pumped source an acre, and the godown in the mandi town (bought, rented or let)

- Status: Accepted (owner, 2026-11-03)
- Date: 2026-11-03
- Deciders: Keyur Patel
- Amends: spec 013 §3.1 (the minor canal's place; one pumped source an acre), §3.4 (seed potato; the godown in the mandi town, renting, letting), §4, §5 (`SetPumpAcres`, `SetBorewellAcres`, `BuyGodown`, `RentWarehouse`, `LetGodown`), §6, §7, §8 (AC-11, AC-12), §10 G7 and G8; spec 017 §3.1 (the stall's goods); spec 019 §3.4 (passive income); spec 020 G18
- Related: ADR 0064 (layouts previewed, never cramped), 0065 (the village layout), 0075 (spec 013), 0078 (passive income), 0084 (the crop ladder), 0086 (the borewell)
- GDD sections: Farming (the irrigation ladder; storage and selling)

## Context

Sprint 16's plan, stories and acceptance criteria were checked against the crop ladder (spec 020 G15, G18) and ADR 0086 before any code. Five gaps came out of it, and the granary's layout preview then showed that spec 013's "granary on the yard" has no room on the map.

## Decisions

1. **Seed potato stays out of the granary.** Seed tubers need cold storage as potatoes do; every other seed goes in.
2. **The minor canal runs along the edge between rows 4 and 5**, from the west edge to the village lane (column 10), drawn on the tile edge (no acre taken). Row 4 and row 5 acres border it, so `home_a`'s 84–86 can take a channel with no land to buy and `home_b` reaches it through them. Its drawing is previewed with US-127's art.
3. **One pumped source an acre.** An acre is on the pump's list or the borewell's, never both (`AlreadyWatered`), so they water 8 different acres. A channelled acre may also be pumped (the higher floor applies; its canal fee still runs). Drip only on a pumped acre, and a drip acre can't leave its list (`DripNeedsWater`).
4. **US-127 is 8 points (was 5)** with the borewell and the canal on the map.
5. **The vegetable stall doesn't sell maize:** the game's maize is the dry grain, a granary good; fresh cobs would be another good.
6. **The granary is your own godown in the mandi town, off the map.** On the map it crowded everywhere it was tried: in the yard it covered the house's door and touched the beds and the tulsi; at a field acre's back corner it blocked the crops; on the quarters it sat between the huts; on the farm lane the trader's truck and the van cover it; beside the village's dairy co-op it ran over the fields (previews in `docs/art/s16-granary-preview-*.png`). Bought for 45,000 (`BuyGodown`), 80 seasons, yours at once (an existing godown, not built), from the Hamlet, 10,000 kg at a fifth of the spoilage, shown in the Store sheet with its picture (art approved 2026-11-03). A place on the map may come later.
7. **Renting warehouse space:** in the State Warehousing Corporation's warehouse in the mandi town, 1–20 t in whole tonnes at 0.30 a kg a season (3,000 a season for 10 t), paid upfront at each close, the days left when booked mid-season; more any time, less from the next close and never below what is stored (`RentWarehouse`, AC-11). Dry goods fill your godown, then rented space, then the hut store. Buying pays for itself against renting 10 t in about 15 seasons.
8. **Letting your godown:** switched on, the whole godown is let for whole seasons; at each close a trader takes it with a seeded 0.8 chance and pays 0.24 a kg a season (2,400) upfront, counted as passive income (spec 019 §3.4). Not let while your dry goods, the hut store's included, don't fit the rented space; your goods go elsewhere while it is let (`LetGodown`, AC-12).
9. **US-130 is 6 points (was 3).** Sprint 16 is 43 points.

## Consequences

- No formula changes to existing rules: renting and letting are new rules at the owner's request, with their own data and tests.
- Rent paid is an operating cost and the godown a capital asset, which is spec 013's lesson; letting adds a small, risky passive income (about 5% of its cost a season, against about 18% for an acre farmed) that bots can't exploit (0.24 is under 0.30).
- ADR 0086 draws the borewell's pump room "at the field's edge, the acre still farmed". The owner rejected a building over a field's crops for the granary, so the pump room's place is previewed with US-127's art before it is final.
- Save schema 14 holds the godown, the rented tonnes, the let flags and the goods in each place.
