# 0119. Spec 024: zoning, land value, rentals, upgrades and overlays

- Status: Accepted (owner, 2026-11-17, G1–G11 closed one per message)
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

## Decisions added when the gaps were closed (2026-11-17)

8. **Land value exists from the Township on** (G9): Phases 1–2's prices, rents, the Village's pace and the golden hashes are untouched.
9. **Builders' demand is a turned-away counter** (G2, G10): the hourly migration draw counts a family when no free house exists, from the Township; a purchase needs 2 families (an acre holds 2 homes) and uses them up; one acre a season. Builders' families are residents (grants, shops, happiness) but not "your households" (the gates). Before the ring they buy neighbours' for-sale acres touching the road or lane, outward from the square.
10. **The permit fee is 2%**, small by design (about 4% of the treasury's income); a household's grants and tax are the real growth income (G3).
11. **A slow sale is priced at listing and locks the acre** (G4); quick 80% × land value from the Township.
12. **Leasing out starts at the next close**, pays 2.5% (the spec's value; recommended 2.0%, kept by the owner), the soil stays as it was, orders are never removed silently, the rent is passive income outside farm profit (G5).
13. **Pucca upgrade**: thresholds as written, base rent × the place, 48 h with the family in, an honest payback line, no extra benefit now (G6).
14. **Overlays**: one hue in five steps for colour-blind players, a tap card, from the Township, view-only (G7).
15. **The house tax** is value at today's prices × land value, billed at the close, capped at 15% of rent (G8).
16. **Water** is the pond, any well or a tower within 300 m, for builders and land value alike (G11).
