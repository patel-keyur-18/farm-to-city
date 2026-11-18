# 0125. Spec 025's eight gaps closed: utilities, construction, the ring, hills, the cold store and the farm's promises

- Status: Accepted (owner, 2026-11-18: each gap decided one per message with a recommendation; G7 moved into Sprint 23 by the owner)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Amends: spec 025 §3.1, §3.2, §3.3, §3.4, §3.5, §3.6, §3.8, §10 (done in the same commit)
- Related: ADR 0052 (the map by era), 0064 (layouts previewed), 0086 and 0087 (borewell, one pumped source), 0107 §13 and §15, 0108, 0116, 0121, 0124

## Decisions

1. **G1 Who builds utilities.** Only the treasury, on spec 021's confirm card; you may give to it; never a private public utility (ADR 0107 §6's wall). The card gains a one-tap top-up and a budget guard that counts the whole ladder's upkeep: with every step and default taxes a town runs 40,000–65,000 a year short (a 6.6 lakh ladder against about +59,000 a year; upkeep about 31,000 a season, 24,500 with the grid).
2. **G2 Materials from the Township only.** Phases 1–2, the goldens and the Village's pace are untouched. Spec 025's first table did not add up to today's costs (a house 70,000 against 60,000, a post 40,000 against 30,000, a playground 40,000 against 20,000, no hall row); quantities are re-derived so materials plus masons equal today's cost within 2%, and AC-5 walks every building in content.
3. **G3 The ring.** 24 × 18 acres, four wide east and west, three north and south, **in the map from save schema 21 for every game, locked until the Township** (no live renumbering at the unlock: commands in the log, saves by content id and the compacted replay base all name tiles). One migration, new id = (y + 3) × 24 + (x + 4). The batches equal draw calls and Unity culls what is off screen, so the furthest zoom-out is capped at today's window and the render statistics' "all" view stays within 10%. "About 80 parcels" means about 80 acres for sale.
4. **G4 The generator.** Fuel 8,000 a season (was 6,000): the grid then saves 8,500 and repays its extra 1,50,000 in about 17 seasons; the spec's "45,000 a season" was wrong. Smoke −0.06 within 250 m and health −5%; the total capped at −0.10 with the kiln. The tower's 400 m is the one water reach for coverage, sickness, builders and land value (the pond and wells stay 300 m). The generator stands on an edge plot.
5. **G5 The kiln.** Any acre you own; the footprint is shown on the ghost and the card; land value −0.04 and health −3% within 150 m; a new good, *kiln fuel*; its output, workers and the trader's buying limit are set by the bots against 4–8 seasons hired and 2–4 run by you (the placeholders lose money at the hand's wage).
6. **G6 Hills.** Drawn and locked in Sprint 23's map; apples and the hill parcels in Sprint 24 after a realism review (150 kg a tree and 8 coins a kg do not hold: about 24 t an acre against 3–4, and mango is 50).
7. **G7 The farm's promises** (US-254 to US-257) are in **Sprint 23**, cut first (owner: a few more points are fine). The tube well is dropped (the borewell is it), the solar pump stays only if its payback is under about 20 seasons, the tractor for neighbours gets a demand cap and an exploit check.
8. **G8 The cold store.** Rent 1.00 a kg a season, 20 t cap. Potato nets about +0.24 a kg (0.85 to 1.15 in the seasonal factor, less rent 2.00 and about 0.16 of spoilage), about 2.5% a season on the money locked, no loop. Only mango clearly pays among fruit. The sheet says "keeps X of every 100 kg".

## Addendum (2026-11-18, US-253 as built)

- **Tile ids stay, not renumbered.** Renumbering to (y + 3) × 24 + (x + 4) would have touched about 130 test files, every fixture save and about fifty presenter sites. Instead the 16 × 12 map's tiles keep ids 0–191 and the ring's 240 tiles follow with explicit coordinates; `MapDef` has `X`, `Y` and `TileAt`. An older save's land is padded from content, closed.
- **The zoom cap is 85%, not 90%.** Measured in the Editor against the Sprint 22 readings: 90% is +13.5% draw calls at the "all" view, 85% +9%, 80% +2%. The farm and village views are lighter than before. Visible sprites grew 15% (ring ground and scrub) and the scene holds 2,250 sprites against 1,492; triangle counts moved with the weather as in earlier sprints, so they are reported and not trusted.
- **Lanes are works**, `CommissionWorks` on `lane_w1`–`lane_e3` (20,000 each); there is no separate `BuildLane` command.
- **A closed parcel** (`opens` in `map.json`: `township` or `hills`) cannot be bought (`LandClosed`), is not simulated, is skipped by builders and shows as scrub. The ring's land for sale draws as meadow.

## Consequences

- Spec 025 is amended; it goes to the owner for approval as a whole with the Sprint 23 plan.
- New content: goods *brick*, *cement*, *timber*, *kiln fuel*; `region.json` limits; `township.json` `utilities` and `smoke`; the map at 24 × 18. Save schema 21 ships once, with a fixture for every new part.
- The ring's layout and every new plot (tower, drains, collection, generator, grid, lanes, hills) are previewed as an artifact and approved before any ring code.
