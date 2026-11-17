# 0119. Spec 024: zoning, land value, rentals, upgrades and overlays (recommendations)

- Status: Proposed (2026-11-17). The owner reviews spec 024 at the Sprint 21 pull request; the gaps were not closed one per message because the sprint was finished in one go at the owner's request.
- Date: 2026-11-17
- Deciders: Keyur Patel
- Related ADRs: 0107, 0110, 0116, 0117, 0118

## Context

ADR 0107 §3, §8 and §13 fixed the scope of zoning, land value, rents by land value, building upgrades and overlays. Spec 024 turns it into rules and numbers, so Sprint 22 can build land value and zoning and Sprint 23 the upgrades and overlays.

## Decisions (each a recommendation, with the reason an average player can read)

1. **One number, land value (`LV`), from what is near an acre**, 0.80 to 1.80, read once a season. It sets rent's place (0.85–1.50), the house tax's base, builders' choice and the price of land, so one idea explains four numbers.
2. **Builders buy one acre a season** while a family waits (road and water present); a builder's homes are the builder's. The permit fee (2%) goes to the treasury. A town never zoned still grows on the ring.
3. **Selling is quick (80% now) or slow (100% in 2 days, cancellable)**, as spec 002 promised.
4. **Leasing an acre out pays 2.5% of land value a season**, upfront: a safe small income, below an owner-operated acre.
5. **Pucca houses** (2,40,000, rent about 7,000) when `LV` ≥ 1.15, water, and rent ≤ 30% of the household's income; yours for the difference.
6. **Five overlays**, one at a time, a tap on an acre shows its number and its *Why?*.
7. **Save schema 20** (with spec 023's): zones, leases, slow sales and building kinds.

## Consequences

- Spec 023's reach (1,500 / 2,000 m) is reused for land value's services, so one set of reaches serves coverage and value.
- ADR 0028's land payback is re-measured by the bots once land value moves prices; a slip past the 8–15 seasons target comes to the owner.
- Nothing is coded; the sheets are previewed first.
