# 0031. Phase 1 adds a kitchen garden, the mandi trip, buying inputs ahead, minimum prices and simple health care

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Extends: ADR 0017 (Phase 1 scope), ADR 0024
- Related: ADR 0008 (per-season realism), ADR 0010 (founder health), ADR 0012 (price band), ADR 0013 (market), ADR 0015 (onboarding), ADR 0021 (crop calendar), ADR 0030 (inherited farm)
- GDD sections: Land, farming and livestock (Crops, Inputs, Storage and selling); Player journey (founder's routine); Economy; Roadmap (Phase 1 scope)

## Context

Reviewing specs 003 and 004, the owner asked that the game stay realistic, and accepted more Phase 1 scope where it makes the farm truer to life. Five additions came out of that review.

## Decision

1. **Kitchen garden** (*baadi*). Two beds on the yard grow quick greens (radish, spinach, coriander; 5–15 minutes). Each harvest gives a fixed, realistic basket (a few kg) until the bed's realistic season output is reached, then the bed rests until the next season. The total stays per-season realistic (ADR 0008) and there is no money loop. Produce is edible and sellable. It is the onboarding crop (ADR 0015). Radish is also a field crop with a realistic duration (about 2 hours).
2. **The mandi trip.** The founder can cart up to 1,000 kg to the town mandi: a real-time trip of about 6 hours that comes out of the founder's labour, a fixed cart hire per trip, and the regional price less about 3% commission and fees. Full loads beat the trader's floor; small loads don't (economies of scale). ADR 0012's floor is unchanged. Roads (Phase 3) later shorten the trip and cut the hire.
3. **Buying inputs ahead.** Seed, fertiliser and spray can be bought into the store before they're needed, and sowing and spraying use stock first. Input prices follow the seasons (seeds dearer in their sowing season; fertiliser and spray in the Spring and Monsoon rushes). Chemical fertiliser has a fixed, government-controlled price that ignores inflation; its peak-season premium stands in for informal scarcity mark-ups.
4. **Minimum prices on market offers.** An offer can carry a minimum price; it counts as supply only while the market price is at or above it, and the trader takes it at the floor only if the minimum allows. Spec 006's standing orders reuse the rule.
5. **Simple health care.** In Winter a household on subsistence has no warm clothing and loses extra health. The founder can buy medicine from the village chemist to recover faster. Unsafe water waits for the town's water network (Phase 2–3).

Also decided in the same review (no ADR needed, recorded in the specs): saved seed ages per season of growth with per-crop loss and floor, and radish seed can't be saved; inputs cost the dealer's (regional) price; the local market shares demand pro rata among sellers; the moisture hook in spec 002 follows real Indian seasons.

## Consequences

- **Phase 1 scope table** (GDD): Crops gains the kitchen garden; Inputs gains buying ahead and seasonal input prices; Market gains the mandi trip and minimum prices; Founder gains warm clothing and medicine.
- **Specs:** 003 (garden, seed age, health) and 004 (mandi, buying ahead, minimum prices, controlled prices) describe the rules and their acceptance criteria.
- **Chart of accounts** (spec 005): adds 5700 Transport and selling costs (cart hire, mandi commission and fees) and 5250 Health costs (medicine).
- **Planning:** new stories in F02.2 (kitchen garden) and F04.1 (mandi trip, buying ahead, minimum prices); medicine and warm clothing join US-052.
