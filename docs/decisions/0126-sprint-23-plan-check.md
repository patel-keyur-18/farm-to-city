# 0126. Sprint 23 plan check: 49 points with the farm's promises, builders leave the acres beside your land, land payback read at the land value paid, the land stress bots made to act

- Status: Accepted (owner, 2026-11-18: "approved all, proceed")
- Date: 2026-11-18
- Deciders: Keyur Patel
- Amends: spec 024 §3.2's builder rules (§2 below); ADR 0028's land-payback band (§3 below); `sprints.md` (Sprint 23 and 24)
- Related: ADR 0107, 0119, 0122, 0124, 0125

## Context

ADR 0124 flagged three things for the owner: land payback 6.0 seasons against ADR 0028's 8–15; builders carrying a zero-tax town and taking the serviced neighbour acres; and the zone-and-flip and buy-and-hold stress bots that cannot act. The owner also moved the farm's Phase 3 promises (US-254 to US-257) from Sprint 24 into Sprint 23 and said a few more points are fine.

## Decisions

1. **Sprint 23 is 49 points** (the table's 39 plus US-254 3, US-255 2, US-256 3, US-257 2). The committed core is 42; the other three farm items are the stretch and are cut first. Sprint 24 becomes 32 points.
2. **Builders skip any acre that shares an edge with land you own.** You keep first claim on growing beside your own fields (the adjacency rule is how a farm grows, ADR 0002's land rule). The ring and the village edge give builders somewhere else to build. This is a rule change to spec 024 §3.2's builder choice (value order, then id): an acre touching your land is passed over, and the sheet's "why no builder came" says so. It lands with US-145, where builders first wait for bricks. The zero-tax town carried by builders' families is left as designed; `BuildersCarryAZeroTaxTown` is re-run after construction (bricks slow builders) and reported.
3. **Land payback is read at the land value actually paid.** ADR 0124 measured 6.0 seasons at land value 1.0 and 10.9 at 1.8. ADR 0028's 8–15 is restated as "at the land value of the median acre the player buys". Sprint 23's balance pass measures it on ring acres. No price changes now; if the median bought ring acre falls under 8 seasons, the ring parcels' location factor is raised (content only) and the numbers come to the owner.
4. **The land stress bots are made to act.** With decision 2 and a developer cash command (Development builds only), the zone-and-flip and buy-and-hold bots can buy; any that ends above 1.2 times the competent bot is flagged. Until then the land exploit coverage was zero (ADR 0124 §7).
5. **Specs:** 026 (festivals, petitions, reputation, crime, inequality) and 027 (events, insurance, forward contracts) are written in Sprint 23 for Sprint 24; 028 in Sprint 24, 029 in Sprint 25.

## Consequences

- `sprints.md` has the Sprint 23 section; the Sprint 24 row drops the farm items; the feature files' sprints for US-254 to US-257 read S23; US-257 is 2 points and no longer a tube well.
- Spec 024 §3.2 gets decision 2's sentence when US-145 lands.
