# 0033. Land payback is measured on an owner-operated acre; saplings priced with planting

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: ADR 0028 point 2 (what "an acre's net farm income" means); spec 003 §9 (the with-hand target); spec 004 §4 (sapling prices)
- Related: ADR 0008 (per-season realism), ADR 0016 (balance), ADR 0021 (fruit trees), ADR 0030 (inherited farm)
- GDD sections: Land (price by earning power)

## Context

The Sprint 2 economy model (US-013) ran a competent founder on the real simulation for 15 game years with every number from content. The founder alone matched spec 003 §9 (about 1.8 acres busy and about 30,700 farm profit a season), but an acre worked with a hired hand (36,000 a season ÷ 2.5 acres) netted only about 2,600 a season, against spec 003's 8,000–12,000. At 1,20,000 an acre that is about 50 seasons of payback, against ADR 0028's 8–15.

The numbers are realistic: a hand's wage eats most of an acre's margin (potato needs about 3.5 hours a day per acre), and real Indian farmland pays back over decades. An owner-operated acre (the founder's or the family's own labour) nets about 17,000 a season, so it pays back in about 7–8 seasons.

The model also showed saplings priced 3–6 times real nursery prices.

## Options

1. Measure ADR 0028's rule on an owner-operated acre and keep 1,20,000.
2. Keep the with-hand measure and cut the acre price to about 40,000.
3. Raise yields or prices until a hired acre nets 8,000–12,000.

## Decision

1. **Option 1.** ADR 0028's "an average acre costs about 8–15 seasons of its net farm income" is measured on an **owner-operated acre**: the founder's own labour, unpaid. `acreBasePrice` stays 1,20,000. A hired-labour acre nets about 2,000–5,000 a season: hiring pays where labour is light (Summer lentils on 6 acres earn about 45,000 more than farming alone) and barely where it's heavy (potato).
2. **Saplings are priced with their planting** (the pit, manure and first watering): banana 60, papaya 60, guava 180, mango 350 (were 150, 120, 250, 400). Each still pays back in its first batch; the tree decision turns on the slot, the wait and, at the field edge, the shade.

## Consequences

- Spec 002 §4 and §9, spec 003 §9 and the GDD say "an owner-operated acre". The economy model reports both measures.
- Growth by buying land needs the household's own hours, light-labour crops, or (Phase 2) businesses; the Hamlet gate is unaffected (ADR 0030 needs no land purchase).
- Spec 004 §4 lists the new sapling prices.
