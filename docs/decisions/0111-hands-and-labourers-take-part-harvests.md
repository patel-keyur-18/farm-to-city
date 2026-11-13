# 0111. Hands and labourers take part-harvests; paid labourers work their paid time

- Status: Accepted (owner, 2026-11-13)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: ADR 0107 §10 (labourers who find nothing fits no longer "go home"); spec 006 §3.2, §3.8 (the harvest rows), AC-27, AC-28; ADR 0105 (its open question)
- Related ADRs: 0034 (the 72 h grace), 0049 (labourers hired till the crops are in), 0105 (`Overflow.StorePart`), 0107

## Context

ADR 0105 lets the player harvest a ready field crop in parts but left the hands and the labourers on `SellRestToTrader`: whatever did not fit the store was sold to the trader at the floor at once (or the hand waited while the truck was away). So a full store silently sold a crop, even under an *Offer at a price* order. ADR 0107 §10 decided that hands and labourers take parts too, and that labourers go home when nothing fits.

Labourers are paid upfront until the last growing crop's ready hour (spec 006 §3.2). Going home early would need a refund, and would take their 8 Care hours out of the pool while other crops still grow.

## Decisions

1. **Every hand's field order takes parts** (`Overflow.StorePart`), whatever its sell rule: what fits the store is taken now, the rest stands ready in the field under the 72 h grace, and the next hourly pass takes what fits then. A part counts as one of the hand's actions that hour (`orderActionsPerHour`). Nothing is sold to the trader unless the order's sell rule (or a careless hand) says so, and an *Offer at a price* order no longer sells its overflow below its price. With nothing fitting he waits (`OrderWaiting` StoreFull, as before). A *this season only* order clears when its last part is in, not at the first.
2. **Labourers take parts the same way**, each hourly pass, every ripe field with no hand's order. **With nothing fitting they do not go home: they are paid until their time is up, so they stay and work it** (they keep tending every field, their Care hours stay in the pool) and the ripe field waits. No refund and no early leave (supersedes ADR 0107 §10's "go home").
3. **Trees and orchards are unchanged** (orchards already pick in parts, ADR 0090 §4; a tree's fruit is picked whole).
4. **An alert** says what still stands: "Store full: 640 kg of wheat still standing on the north field. It will be taken as room appears." with *Sell some* and *Rent space*. It shows for a ripe field that a hand's order or hired labourers would harvest and that does not all fit; it is read from the state, so it also clears by itself. The away card's people section says the same in one line.
5. **No save change** (`TakenGrams` is schema 17). Bots re-run; a golden whose hash moves is re-pinned by its `expectedHash` alone and said in the commit.

## Consequences

- The labourers' wage for hours with nothing to harvest is the player's cost of a full store; the alert says why and offers the two levers.
- Spec 006 §3.2 and §3.8 carry the rules; AC-27 and AC-28 name the tests (`PartHarvestTests`, `TruckHarvestViewTests`).
