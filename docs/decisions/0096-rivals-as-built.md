# 0096. Rivals as built: newcomers, the grocer's and tea stall's prices, a pitch's rent past the overdraft, the tea stall's target

- Status: Accepted (owner approved the newcomers' carts and the cart's spot on 2026-11-08; the rest recorded as built in US-122 for the owner's review)
- Date: 2026-11-10
- Deciders: Keyur Patel
- Amends: spec 017 §3.4 (a pitch's rent past the overdraft), §3.7 (newcomers and the reaction as built), §9 (the tea stall's target measured at the usual price, standard)
- Related: ADR 0074 (spec 017), 0091 (shops as built), 0093 (Sprint 16 balance), 0095 §1–3 (the plan)
- GDD sections: Businesses (customer choice)

## Context

US-122 built ADR 0095 §1–3. Building and measuring it raised points the plan didn't settle:

- A newcomer needs terms the content's rivals have from `shops.json` (hours, capacity, where it stands), and the alerts need to know why it came after the close has reset the season's counts.
- The grocer's and tea stall's 5% steps drifted in fixed-point arithmetic (0.85 + 0.05 wasn't exactly 0.90).
- `GateTests.Shops` crashed: a struggling stall's pitch rent was posted past its overdraft's limit and took its cash negative. The bug was in Sprint 16's code; the price cuts made a stall struggle enough to reach it.
- The business bot kept a stall's goods as founded. Once a newcomer halved its customers, the unsold sweets and vegetables spoiled, and the sweet shop and vegetable stall at 1.1 lost 25,000–30,000 a season. One vegetable stall run was foreclosed.
- ADR 0093 set the tea stall's hired target, 5,000–12,000 a season, for good chai at 10% above the reference. ADR 0095 §1 makes that price draw a newcomer. Measured, good chai at the usual price earns a median of 3,167. It earned 3,194 even with the tea stall's cut switched off, so the cut isn't the cause: good chai's full-cream milk costs more than its extra customers bring at the usual price.

## Decision

1. **A newcomer** stands on the first free pitch, or as the cart on the verge of the lane between the village tea stall and the temple (approved preview `docs/art/s17-newcomer-preview-lane.png`, never on the lane itself). It keeps the village's shop hours (8:00–20:00), serves whoever comes, and sells at the reference: standard, 2.5 stars. Its cart counts 100 m beyond the square.
2. **When a newcomer comes:**
   - Only once that kind of shop is open to the player: from the Village, or from the Hamlet in the bots' `ShopsInHamlet` content.
   - At most one of each kind.
   - Never in the same close as one of that kind packs up.
   - The seeded draw uses its own stream (`Rival`), keyed by the kind and the season.
   - It remembers whether a gap or your prices drew it, so its alert can say which.
3. **The grocer and the tea stall price in hundredths**, so their 5% steps land exactly on 0.95, 0.90 and 0.85 and back on 1.00. Each change is alerted for the rest of that season. The away card says "cut its price to…" or "put its price back up to…".
4. **A pitch's rent past the overdraft's limit goes unpaid that season**, as a wage does (spec 016 §3.10). The shop is short, and its card and the alert already say so.
5. **The bots put out what their customers take.** Under 80% of a day's goods sold, the next morning the bot puts out what sold plus 15%. Sold out, it goes back to the goods as founded. This covers the vegetable stall's goods and the sweet shop's sweets; the tea stall makes its cups to order. A sensible owner does the same, and the shop's card shows what sold of what was put out.
6. **The tea stall's target is measured at the usual price, standard quality:** hired, a median of 7,335 a season (5,901–12,349), inside ADR 0093's 5,000–12,000. Good chai pays only when you run the stall yourself (a median of 17,463). This is an observation for the balance step, not a change.

Measured on 8 seeds × 12 seasons, hired, after these decisions:

| Shop | At the usual price | 10% above | Above is no better (ADR 0095 §1) |
| --- | --- | --- | --- |
| Tea stall (standard) | 7,335 | −294 | Yes |
| Sweet shop | 10,944 | −5,972 | Yes |
| Vegetable stall | 3,723 | 2,987 | Yes |

With no shop of yours, no newcomer came in 12 seasons in any of the 8 runs (0–1 of each kind allowed).

## Consequences

- Save schema 15 adds the newcomers and each shop's season at its price. It also adds each rival's last share and when it last changed its price. A schema-14 town's history can't replay into these rules, so it becomes its own replay base.
- `GateTests.Shops` measures all three shops at 1.0 and 1.1. `GateTests.Newcomers_NoShops` measures the newcomers when you open nothing.
- **For the balance step:** good chai doesn't pay hired at any price, and the vegetable stall's hired profit is thin (about 3,700).
