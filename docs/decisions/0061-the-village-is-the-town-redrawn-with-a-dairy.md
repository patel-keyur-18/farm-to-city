# 0061. The village is the town: its old families aren't counted; the centre redrawn with a street and the dairy co-op

- Status: Accepted
- Date: 2026-10-22
- Deciders: Keyur Patel
- Amends: spec 010 §3.3 and §3.6–3.7 (the village centre's places and how they're drawn); spec 012 §3.5 and §3.13 (the dairy co-op has a building; the cattle chapter shows it); spec 014 §3.1 (who counts)
- Related: ADR 0053 (the village layout), ADR 0003 (the founder), ADR 0056 and 0060 (cattle), ADR 0028 (era pacing)
- GDD sections: Land, farming and livestock (the village around the farm); Citizens and workforce; Art, audio and feel

## Context

Closing spec 014's first gap, the owner asked whether the village's homes and temple can count as the town, since the founder lives in the village. Looking at the Sprint 11 captures, the owner also found the village centre cramped and unnatural: a 4 × 4 block of buildings, one centred on each acre, all facing the same way, on a beige slab.

Counting the ~35 people already living in the centre's 7 houses would start the town at about 35, past the Homestead's 0–3 and the Hamlet's 4–15, and meet the Village era's "5 households" on day one (ADR 0028, spec 009). It would also make their happiness the player's job from the first session.

## Decision

1. **One village, one town.** The farm is part of the village; the HUD and the letter call it your village. The centre's places (temple, banyan square, grocer, tea stall, chemist, council hall, school plot, dealer, dairy co-op) are the town's: they serve the player's citizens (the temple and the square meet belonging; the school plot is where the first school goes).
2. **The old families aren't counted.** The families already living in the centre's houses stay named, visible neighbours (the day labourers, the grocer, the priest): not simulated as citizens and not in the population. The population is the player's people: hands and families in houses the founder builds, shown as "Your people". They may join the citizen model in Phase 3, when a Township's size makes it harmless (noted, not decided).
3. **The centre redrawn on the same 16 acres** (presentation only; every place keeps its tappable acre, so there's no save migration and spec 010's land counts don't change):
   - a street through the middle of the village, from the village lane in the west to the fields in the east, with lanes branching to the houses;
   - buildings rendered from all four sides in Blender and set facing the street or lane they stand on, off-centre, in clusters with courtyard walls;
   - grass and packed earth instead of the slab; an open square round the banyan; the pond across most of its acre with ghat steps and the temple on its bank under a peepal; haystacks, a handpump, charpais, tethered buffalo, trees and open ground between clusters; a township-like feel.
4. **The village dairy co-op gets a building** (place kind `dairy`, on the acre of the seventh house). Tapping it shows the co-op's price a litre by fat, what it collected this morning, and, once cattle unlock, the way to the shed and the cattle fair. The `cattle` letter chapter shows the dairy and the animals together. A dairy business the player owns stays with spec 016.
5. **In Sprint 12** as US-206 (8 points), with the cattle art.

## Consequences

- Spec 014 §3.1 is closed as above.
- Sprint 12 grows to 44 points and runs about 2 weeks.
- `content/map.json`: `house_7` becomes `dairy` (content only; places have no state).
