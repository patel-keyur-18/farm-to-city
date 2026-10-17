# 0030. Inherit a 6-acre farm, start with 20,000 coins, and gate the Hamlet on labour

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Supersedes: ADR 0008's starting package (2 acres; savings for 2 seasons); ADR 0028's Hamlet reasoning (the Hamlet no longer depends on buying land); spec 002's single inherited parcel
- Amends: ADR 0009 (the protected inheritance is 6 acres, not 2); ADR 0028 (land-purchase loans stay, for growth after the Hamlet)
- Related: ADR 0010 (founder labour), ADR 0022 (hiring from Homestead), ADR 0015 (onboarding), spec 002, spec 003 (open question 1), spec 005
- GDD sections: Player journey and eras (Homestead start, Hamlet gate), Land, Assets and wealth, Data model (`StartDef`)

## Context

Writing spec 003 showed that the Hamlet target (end of week 1, ADR 0028) couldn't be met. The gate needed 6 acres farmed, so the player had to buy 4 acres (about 4,60,000 coins). By the end of week 1 a competent player had about 90,000. Even after the land-purchase loan and the moneylender, about 2,28,000 had to come from cash. No tuning within per-season realism (ADR 0008) could close a gap that size.

The owner proposed inheriting enough land to grow from, protected forever, with little or no money.

## Options

1. Gate on 4 acres and raise the land-purchase loan's LTV to 0.80.
2. Gate on 4 acres and keep the loans (the Hamlet at about week 2).
3. Keep the gate at 6 acres and move the Hamlet to 2–3 weeks.
4. Count sharecropped or leased land as farmed.
5. **Inherit a 6-acre farm, start with little cash, and make the week-1 challenge labour instead of land** (the owner's idea).

## Decision

Option 5.

1. **The inheritance:** 6 farm acres and the yard (hut and well), in two parcels (`home_a` with the yard, `home_b`), all **protected**: never sold, never seized, never collateral (ADR 0009). `StartDef.inheritedParcels` is a list.
2. **Starting cash: 20,000 coins** (placeholder for the economy model). It pays the first season's basic living costs with the stored grain, and leaves a little for seed. It is deliberately less than a hired hand's season wage (36,000).
3. **The inherited store:** about 600 kg of certified seed potato (an input good, so the household never eats it), 75 kg of wheat and 75 kg of lentils (food, or saved seed), and small packets of garden seed (radish, spinach, coriander). The first harvest sale is the first money the player earns.
4. **The well** keeps every inherited acre above its moisture floor (spec 002); spec 003 charges the bucket-carrying labour.
5. **The Hamlet gate becomes:** 6 acres harvested in one season with an average Care of at least 0.9, a house for a hired hand, and one profitable season. The founder alone tends about 2.4 acres, so the gate needs a hired hand: hiring, not buying land, is the first lesson in growth.
6. **The map grows to 8 × 6** (19 parcels, 41 acres to buy later), so buying land remains the next step after the Hamlet. Land-purchase loans (ADR 0028) stay as they are.
7. **Unowned land keeps its map fertility.** It's neighbours' farms, cropped in a steady state, so it doesn't recover like fallow, and unbought parcels don't drift in price (spec 002 open question 6).

## Pacing check (competent player, placeholders)

| When | What happens | Cash |
| --- | --- | --- |
| Start | 20,000 coins and the inherited store | 20,000 |
| Spring | Founder grows potato on about 2 acres (stored seed, then bought seed); the hut store's 2,000 kg limit forces some sales at the floor | about 36,000–40,000 |
| End of Spring | Build the hand's hut (about 15,000, spec 006) and hire a hand (36,000), with a moneylender loan of up to 25,000 | about 0–10,000 |
| Summer | Founder and hand grow lentils on 6 acres: 12 hours a day needed, about 14 supplied, Care 1.1 | about 80,000 sales, a profitable season |
| End of week 1 | Hamlet gate met | |

Without borrowing, the hand is affordable about half a season later, so the Hamlet comes at about 1.5 weeks. The balancer (F20.1) confirms the target, and the economy model (US-013, US-014) tunes the numbers.

## Consequences

- **GDD:** the era table (Homestead start, Hamlet gate), Land, Assets and wealth, and the Data model change in the same commit.
- **Spec 002:** inherited parcels as a list, the 8 × 6 map, fallow recovery on owned land only, AC-1 (6 farm acres) and AC-7 (unowned land unchanged). Implemented in Sprint 1.
- **Spec 005:** starting cash 20,000; AC-3 updated. Implemented in Sprint 1.
- **Spec 003:** the founder starts on the basic diet, eating from the store; the pacing check is rewritten.
- **Spec 004:** `StartDef` gains the inherited store.
- **Spec 009 and F09.3:** the new Hamlet gate; the first letter introduces the land, the seed and the store.
- **What it teaches:** land is the asset you already have; labour is what limits you first; borrowing to hire is an investment with a payback.
