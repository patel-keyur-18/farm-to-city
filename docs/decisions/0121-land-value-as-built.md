# 0121. Land value as built: from the Township, per acre, read at the close

- Status: Accepted (owner, 2026-11-18, US-143 approved)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0028, 0110, 0117, 0119

## Context

Spec 024 §3.1–§3.5 and §3.4 (ADR 0119) set one land value for every acre that moves rent's place, the house tax's base and the price of land, and add a slow sale. US-143 built them and the owner approved Preview 6 (the sheets).

## Decisions

1. **Land value exists from the Township on** (the old village joining reads it for the first time; every close reads it again, in the citizens' close before the season's leases renew). Before it, every acre is 1.00 and price, rent and tax are exactly as Phases 1 and 2 built them, so the Village's pace and the golden hashes are untouched. It lives in `TownshipState.LandValues` (one `Fixed` a tile; empty before the unlock).
2. **The parts built are road, school, clinic, square, water, jobs and town size.** The generator's smoke arrives with spec 025's generator and crime with spec 027; neither has anything to measure yet. Content is `township.json` `landValue` (not `land.json`), next to the reaches it reuses.
3. **Services count once they stand** (the school open, the clinic open), by Manhattan metres as the coverage rule. Water is the pond or any well (a pump or a borewell waters fields and doesn't count). A job is an open business with a hired worker (a founder-run business offers none), at most five.
4. **Price, rent and tax:** a parcel's price is the sum of its acres' prices, each × its own land value (whole coins once); rent's place is `0.85 + 1.75 × (LV − 1)` between 0.85 and 1.50, so a plain acre rents 15% under the base; the house tax's base is the house's value × its acre's land value, still capped at 15% of the rent.
5. **A slow sale is two commands, `ListParcel` and `CancelSale`** (spec 024's table said `SellParcel (slow)`): it sells in 2 days at 100% of the land value fixed at listing; the acre is locked (no sowing, building, irrigation or second listing; later zoning and leasing join the same `Parcel.Listed` test). If a loan now secures the acre or selling it would strand land when the days are up, the listing is cancelled and the away card says so.
6. **Screens:** a *Land value of this acre* row under the price of land for sale (per acre tapped; a parcel's acres can differ), the same in the notes of Your land, and a *Sell this land* choice sheet from the Township (before it, the old hold-to-sell card at once). Its *Why?* is in words and percentages, never a factor (ADR 0043): "14% more" with each part as "+10%", what is not near enough yet, and the bounds. The owner's preview showed the sheet's row as ×1.14; the *Why?* rule won there.
7. **The map overlay is US-153** (Sprint 23); this story only reads the value and says why.

## Consequences

- Land prices rise about 4–20% on today's map; ADR 0028's payback (8–15 seasons) and the Township's pace are re-measured in the balance pass after US-142 and US-261.
- Spec 024 §3 is amended in place (items 2, 3, 5, 6 above).
