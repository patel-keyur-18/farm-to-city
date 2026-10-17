# 0013. Market clearing mechanics for tradeable goods

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0012 (price bounds from regional trade), ADR 0002 (hourly clearing)
- GDD sections: Economy (price model), Businesses (customer choice), Balancing and testing

## Context

The price formula `P_local = P_regional × (Demand / Supply)^(1/e)` left three things undefined:
- what happens when supply is zero (division by zero)
- whether demand reacts to price, which would make price and demand depend on each other
- whether services, which can't be stored or traded, are priced by this market

## Decision

1. **Demand curve.** `D(P) = D_ref × (P / P_regional)^(−e)`.
   - `D_ref` is the quantity households and businesses want at the regional price.
   - The price formula is this curve solved for D(P) = S, so "Demand" in the formula means `D_ref`.
2. **Supply includes the trader.**
   - `S = local goods offered + the trader's import capacity at the ceiling`.
   - Every tradeable good must have trader capacity greater than zero, and the content linter enforces this.
   - Goods the trader doesn't deal in use a floor of one unit in the formula, so S is never zero.
3. **Damped price adjustment.**
   - Each hourly pass, the price moves a fixed fraction of the way toward the target: `P_next = P + k × (P_target − P)`, with `k = 0.25` (data).
   - All the maths is fixed-point.
   - It's deterministic, needs no solver, and doesn't oscillate.
4. **Rationing.**
   - The quantity sold is `min(D(P), S)`.
   - When supply is short, buyers are served in a fixed order seeded from the game's random seed, so results are deterministic.
   - Unmet demand is recorded per good per hour. It feeds the shortage health check and competitor-entry decisions.
5. **Scope.** The hourly market covers **tradeable goods** (raw, processed, finished). **Services** are priced by the businesses that provide them, and customers choose between providers using the customer-choice model (price, quality, distance, reputation, waiting time).

## Consequences

- **New definitions:**
  - `MarketDef.adjustmentRate` (k)
  - `GoodDef.tradeable` (whether the trader deals in the good)
- **New state:**
  - per-good market price
  - unmet demand per good, kept for a rolling window
- **The goods-and-market spec** must include property-based tests:
  - the price converges to the target under constant demand and supply
  - the price never leaves the safety rail
  - supply is never zero
  - results are identical for the same seed
