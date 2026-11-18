# 0128. Builders take the acres beside yours last, not never

- Status: Proposed (Sprint 23, US-145; the owner is asked to confirm in the story review)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Amends: ADR 0126 §2
- Related: ADR 0109, 0122, 0124, 0125, 0126

## Context

ADR 0126 §2 made builders skip every acre that shares an edge with land you own, so you keep first claim on growing beside your fields. It lands with US-145. When `BuildersCarryAZeroTaxTown` was re-run, the town left alone no longer grew: of twelve seeds, eleven ended the second year with about 9,000 in the treasury and every service near 0, the same as a town with no builders at all. The bot's town has no serviced for-sale acre that does not touch its land, so builders had nowhere to build. ADR 0109 promises that a town left alone always works.

Switching the rule off gave the old result (treasury 50,000 to 184,000, every service at 100%), so the rule alone is the cause.

## Decision

Builders choose in this order: your own residential parcels first (as before), then serviced acres for sale that do **not** touch your land, then, only when none of those is left, the acres that touch it. Each group is by nearness to the square, then id. You still keep first claim wherever builders have another place to go; you are never walled in with nowhere to build next to a town that has run out of room.

## Consequences

- `ZoningRules.Candidates` returns three groups; the sheet's "why no builder came" is unchanged.
- `BuildersCarryAZeroTaxTown` passes again; `ConstructionTests.Builders_After_And_Away_From_Yours` checks the order.
- If the owner would rather builders never touch the acres beside yours, the answer is to open more serviced land (lane sections) for builders. That is a balance question for the sprint-end pass; this ADR can then be superseded.
