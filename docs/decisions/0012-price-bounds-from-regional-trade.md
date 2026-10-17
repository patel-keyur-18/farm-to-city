# 0012. Price bounds come from regional trade; inflation indexes the regional price

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0008 (rupee-scale coins)
- GDD sections: Economy (price model, regional trader, inflation), Balancing and testing, Data model

## Context

The GDD bounded local prices twice, and the two bounds disagreed:
- a formula clamp of 0.5–2× `P_base`
- a regional trader that buys at the regional price minus transport and sells at the regional price plus transport

Also, `P_base` was fixed while inflation runs at 2–6% a year, so base prices would go stale over a 10+ game-year campaign.

## Decision

1. **Regional price.** `P_regional = P_base × PriceIndex × SeasonalFactor(good, season)`.
   - `P_base` is the year-0 price in data, and it is never edited for inflation.
   - `PriceIndex` is the general inflation level, updated each season.
   - Seasonal factors live in data.
2. **The working band comes from trade:**
   - Floor = `P_regional − TransportCost`. Below it, the trader buys.
   - Ceiling = `P_regional + TransportCost`. Above it, the trader imports.
   - Transport cost depends on the town's road link. A highway connection lowers it and narrows the band.
3. **Trader capacity.** The trader buys and sells only a limited quantity per day (data). A large glut or shortage can briefly push the local price beyond the band until the next delivery.
4. **Safety rail.** The local price is finally clamped to 0.5–2× `P_regional`. This only guards against bugs. The balancer asserts that it almost never triggers. If it does, that is a balance bug to fix in data.

## Consequences

- **The price formula uses `P_regional`, not `P_base`.**
- **New definitions:**
  - `RegionDef`: seasonal factors, trader capacity, and transport cost by road tier
  - an inflation-index process in the economy model
- **Balancer checks:**
  - "Bounded prices" means the safety rail triggers in under 0.1% of good-hours (threshold in data)
  - no market sits in permanent shortage
- **Lessons this teaches:**
  - why the highway matters
  - import and export decisions
  - why prices drift up over the years
