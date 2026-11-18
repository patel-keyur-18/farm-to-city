# 0122. Zoning and builders as built: per parcel, residential only, builders' homes are not yours

- Status: Proposed (2026-11-18); the owner reviews US-142 with the story
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0064, 0107, 0119, 0121

## Context

Spec 024 §3.3 (ADR 0119) lets you zone land and has private builders fill it. US-142 built it; the owner approved Preview 7 and the three recommendations below (2026-11-18).

## Decisions

1. **Zoning is per parcel.** The sim sells whole parcels, so a zone covers all of a parcel's acres and the sheet says how many (`ZoneParcel`, `UnzoneParcel`; spec 024 said "an acre"). The inherited acres can never be zoned, a listed one can't, and a parcel with a crop, tree or building must be empty first.
2. **Residential and Farming only.** The commercial zone (a newcomer's shop on the acre) and builders' pucca houses are cut (first in the sprint's cut list); builders build kutcha homes and pucca upgrades are US-152.
3. **Families turned away.** From the Township, when the hourly migration draw finds no free house, one family is counted at the pace families would have come (18 ± 6 h, seeded), at most 16. A builder needs `familiesPerAcre` (2) for each acre of the parcel and uses them up.
4. **Builders buy at the season close,** after land value is read, **at most one parcel a season** across the town: yours if you zoned it residential (it sells at its land value, the money is yours), else a neighbour's parcel for sale by the district road or the lane, nearest the square first, then id. A parcel needs a road and water near it. A parcel that needs more families than are waiting is skipped for the next that needs fewer. A builder-bought parcel is no longer for sale and never yours again.
5. **The builders' homes** are 2 an acre of kutcha family houses with `Owner = builder` and book cost 0 (off your books; no depreciation). The town's purse gets a permit fee of 2% of the price (`Fees`). Their families move in like any family.
6. **Not yours.** A builder's family is a resident (grants, shops, happiness) but not one of your households (`EraRules.Households`, so not the gates and not the town-size part of land value). It pays its rent to the builder (no cash to you, no rent-received entry), has no renewal card for you (its lease renews at the market rise by itself), its house is off your tax bill (`TaxRules.IsTaxed`; carried item 6 of Sprint 22), and a hand living alone is never offered a builder's home.
7. **Screens (Preview 7):** *Zoned for* and the families waiting (a *Why?* in words), *Offer to builders* with a tap card, *Take it back to farming*, a Builders section on the Town sheet, a tag on a builder's family and its rent line, the builders' acre sheet, the away lines. Builder homes stand on bare ground on the map, drawn as the existing family houses.

## Consequences

- Schema 20 gains `Parcel.Zone`/`BuilderOwned`, `Building.Owner` and the Township's turned-away counter and builder totals (hashed once used).
- The Township bot and builders now compete for the same neighbour parcels, so the Township's pace, land payback (ADR 0028) and ADR 0016's 20% rule are re-measured in the balance pass.
- Spec 024 §3.3 is amended in place.
