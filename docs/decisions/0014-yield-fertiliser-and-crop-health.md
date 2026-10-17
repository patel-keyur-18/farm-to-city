# 0014. Yield formula: fertiliser, crop health and a yield cap

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (no severe events while away), ADR 0008 (per-season realism)
- GDD sections: Land, farming and livestock (yield formula, inputs)

## Context

The GDD gave chemical fertiliser +25% yield and said pests can wipe out a field, but neither appeared in `Yield = Base × Soil × Water × Care × Seed × Weather × Tools`. Stacking every bonus could also push yield to 2–3× base, which would break the economy model.

## Decision

```
Yield = Base × Soil × Water × Care × Seed × Weather × Tools × Fertiliser × CropHealth
        capped at YieldCap × Base   (YieldCap = 1.6, in data)
```

1. **Fertiliser** (values in data):

   | Choice | Factor this cycle | Effect on fertility |
   | --- | --- | --- |
   | None | 1.00 | Natural drift |
   | Compost | 1.05 | Raises fertility over the following seasons |
   | Chemical | 1.25 | −3 fertility points per season of use, applied pro rata per crop cycle |

   Fertility feeds the Soil factor, so the damage shows up in later harvests.

2. **CropHealth** starts at 1.0. Each crop cycle rolls a seeded pest and disease risk.
   - **Risk rises** with monoculture (the same crop repeated on the field) and wet weather spells during growth.
   - **Risk falls** with rotation and spraying, which costs coins.
   - **An outbreak** sets CropHealth to 0.4–0.8.
   - **A rare severe outbreak** sets it to about 0, wiping out the field.
   - Outbreaks are signalled during growth by an advisor warning, so the player can still respond.
   - Severe outbreaks never strike while the player is away.

3. **Yield cap.** The product of all the factors is capped at `YieldCap × Base`. The *Why?* breakdown says when the cap applied.

## Consequences

- **New definitions:**
  - `FertiliserDef`: factor, fertility change per season
  - `PestRiskDef`: base risk, monoculture and wetness modifiers, spray effect, outbreak bands
  - `YieldCap` in the farming parameters
- **The crops-and-yield spec** turns these into acceptance criteria. For example: "chemical fertiliser for 4 seasons lowers fertility by 12 ± 0.5 points".
- **The economy model** can trust that yield never exceeds 1.6× base.
