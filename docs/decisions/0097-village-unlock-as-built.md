# 0097. The Village unlock as built: what counts, when it's checked, what it opens, and the measured pace

- Status: Accepted for §1–7 (recorded as built in US-102 for the owner's review; the letter's text approved by the owner on 2026-11-10); §8, the pace, is with the owner
- Date: 2026-11-10
- Deciders: Keyur Patel
- Amends: spec 018 §3.6 (what counts, when it's checked, the progress sheet's list), §9 (the Village day measured); spec 005 §3.6 (the era cap follows the era reached); spec 009 §3.6 (the progress sheet is the next era's)
- Related: ADR 0028 (the Village at 2–3 weeks), 0071 (5 households by day 16 with the home loan), 0073 (the hired mill), 0077 (spec 018, G1 and G8), 0095 §9 (when the gate is checked)
- GDD sections: Player journey and eras (Village)

## Context

US-102 built spec 018 §3.6 and ADR 0095 §9. Building it raised points the spec didn't settle:

- Which business's seasons count, whether a closed business keeps its count, and what a schema-14 business starts with.
- When "a household forms or moves in" happens in the code.
- The co-op bank's Village cap (25,00,000, `lenders.json`) was loaded but never applied: the code always used the Homestead's cap, waiting for this story.
- The progress sheet's list of what an era opens was hard-coded to the cattle shed. The Village's list, read from the content as approved, would have left the Hamlet's list partial.
- The away card's era line said "Your farm is now a Hamlet" for any era.
- `GateTests.Village` needs a bot that goes for the gate.

## Decision

1. **Households** are every household on your land (spec 014 §3.13): a hand alone in his hut counts.
2. **A business's profitable seasons** are counted at its own close, when its P&L's net profit is above 0 (spec 016 §3.2), in any order. The business with the most counts. A closed business keeps its count. A schema-14 business is counted from the reports it kept (up to 4, `KeptReports`).
3. **The gate is checked** after a field harvest and at each close (as before), when the school opens, when a hand is hired into his hut, when a family moves into a free house, and when a married son and his bride move into one. It is the same check for every era, so the Hamlet's gate can also open at these moments.
4. **The co-op bank's cap** is the one for the latest era reached that `lenders.json` names. The Village raises it from 5,00,000 to 25,00,000 (spec 018 §3.5).
5. **The progress sheet** is the next era's: "Becoming a Village" once you're a Hamlet, "Your Village" once you're there. Each row has its *Why?* saying what to do next, and the business row names the business ("2 of 2 · Flour mill").
   - Its list of what an era opens is read from the content for every era: the businesses and shops, water (pump, canal, drip, borewell), machines, family houses, the school, orchards, the godown, and a lender's larger cap.
   - The Hamlet's list therefore now names the flour mill, the pump and canal channels, the ox plough, family houses, the school, orchards and the godown beside the cattle shed.
6. **The letters.** The Hamlet's letter names the Village's three goals. The `village` page names the business whose seasons counted, as approved (2026-11-10). It opens with the Hamlet's banner, "A Village!". The away card's line for any era reads "Your farm grew into a new era."
7. **`VillageBot`** goes for the gate in the order that measured quickest: five homes first (the bank's home loan), then a hired flour mill on its loan, then the school on a co-op loan as far as the bank's room allows. The saver variant borrows nothing and hires as the cautious saver does.
8. **The pace (with the owner).** Measured with the other orders tried:

| Bot (`GateTests.Village`) | Village day, median | Range | Spec 018 §9 |
| --- | --- | --- | --- |
| Competent: homes, mill, school (16 seeds) | 32 | 29–39 | 14–21 |
| Competent: school, homes, mill (8 seeds) | 32 | 28–36 | |
| Competent: mill first (8 seeds) | 38 | 36–40, one seed never | |
| Cautious saver: homes, mill, school (8 seeds, 24 seasons) | 56 | 52–57 | 28–42 |

Alone, each condition comes in time: 5 households by day 16 (ADR 0071), the school open by day 13, the hired mill's 2 profitable seasons by day 16–20. Together they need about 5,50,000 by day 21: four family houses (2,40,000), the mill (1,50,000) and the school (1,05,000). The Hamlet farm earns about 45,000–75,000 a season. The co-op bank lends on income and on what the loan buys, not on the protected inherited acres, so its room is about 40,000 at the Hamlet. The mill's loan takes all of it from day 8 to day 26, and the home loans wait. Literacy when the Village opens: a median of 46% (28–70%), against spec 018 §9's 50–70%.

## Consequences

- Save schema 15 adds each business's profitable seasons. A schema-14 town's history becomes its own replay base, as it already does for literacy and the shops.
- `EraTests.Village`, `EraTests.Village_OnHousehold` (sim) and `EraTests.Village` (view) cover AC-8. `GateTests.Village` covers AC-9 and prints each condition's day, so a miss shows its cause.
- `eras.json` loses its `manual` flag: every era after the first has conditions, all optional, at least one.
- Until the owner decides §8, `GateTests.Village` fails against spec 018 §9's ranges.
