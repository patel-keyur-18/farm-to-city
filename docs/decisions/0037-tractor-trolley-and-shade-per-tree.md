# 0037. The mandi cart is a tractor-trolley; shade is set per tree; first-season pacing accepted

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: ADR 0031 point 2 (the cart's size and hire), spec 002 §3.4 (shade per tree), spec 003 §4 (`TreeDef.shade`), spec 004 §3.6 and §4 (`MandiDef` defaults)
- Related: ADR 0008 (per-season realism), ADR 0016 (balance), ADR 0021 (fruit trees), ADR 0030 (pacing), ADR 0034 (Phase 0 gate checks, mandi labour)
- GDD sections: Land and farming (Storage and selling; Fruit trees)

## Context

The Sprint 3 gate run and the 15-year economy model (2026-10-18) raised three findings:

1. **The mandi never paid.** With a 1,000 kg cart at 800 hire, 3% commission and 0.5 h of the founder's labour, a full load of potato netted about 5,234 against the trader's 5,300. The competent bot never went in 15 years. Most Indian farmers today hire a tractor-trolley (2–3 tonnes, about ₹1,000–1,500 for a trip of 15–20 km); 1,000 kg is a bullock cart.
2. **Field-edge trees dominated.** Every mature tree shaded its acre by 3%. A mango earns about 6,000 a year and cost its acre about 2,000 of shade, so 12 field-edge mangoes would add about as much as the whole farm. Real mango and guava canopies (8–10 m) cut crops under bund trees by 10–30%; bananas and papayas shade little.
3. **The first season ends poorer:** about 36,000 at the end of Spring (was 42,000), because the inherited seed is now a cost, pests and spoilage take their share, and living costs follow the index. The hand's hut and first season (about 51,000) need a moneylender loan of about 15,000.

## Decision

1. **The mandi cart is a tractor-trolley:** `MandiDef.cartCapacityKg` 2,000, `cartHirePerTrip` 1,000 coins. A full load of potato in Spring nets 12,192 (13,600 × 0.97 − 1,000) against the trader's 10,600; half a load nets less than the trader, so only full carts pay. Wheat and lentils still don't (their transport margin is small).
2. **Shade is set per tree** (`TreeDef.shade`): mango and guava 0.08, banana and papaya 0.03; `LandDef.minShadeFactor` 0.80 (was 0.85); `LandDef.shadePerMatureTree` is removed. A tree's shade applies to its field-edge acre from maturity. Big trees suit the yard; small trees suit field edges.
3. **The first-season pacing is accepted:** the first loan becomes a natural step before hiring (spec 009's `borrowing` chapter); re-checked when spec 006's hiring is built (Sprint 5).

## Consequences

- **Content:** `market.json`, `trees.json`, `land.json`; the frozen golden content is refreshed and goldens 003 and 004 re-pinned (003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2`).
- **Measured after the change:** the competent bot takes the mandi 86 times in 15 years (about a third of its sales), roughly even with the trader after the founder's labour; the gate checks still pass (stress bots at most 1.038 × competent).
- **Specs 002, 003 and 004** and the GDD updated; spec 002 AC-10 and spec 004 AC-21 and AC-22 follow the new values.
