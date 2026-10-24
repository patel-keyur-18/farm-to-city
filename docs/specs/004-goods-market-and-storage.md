# 004. Goods, market and storage

- Status: Approved (2026-10-17), with the Sprint 1 review decisions and the Sprint 3 planning review changes (ADR 0034) in §10; amended by spec 011 (ADR 0054, 0055; Phase 2): §3.1 (a daily price factor at 6:00), §3.2 (the stall paused midnight to 6:00), §3.3 (the trader is a truck from 6:00 to midnight with a total load; its counts reset at 6:00; `TraderAway`), §3.5 (fresh today, held for the truck); amended by spec 012 (ADR 0056, 0060; Phase 2): §3.4–3.5 (goods with a place: the feed room, the straw stack and the compost pit hold their goods outside the hut store; `PlaceFull`, `NoShed`)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Economy (Price model, Regional trader, Inflation); Land, farming and livestock (Storage and selling); Data model (`GoodDef`, `MarketDef`, `RegionDef`)
- Related ADRs: 0034 (glut share, indexed coin amounts, inputs in store as an asset, mandi labour, price rescale), 0031 (mandi trip, buying inputs ahead, minimum prices, controlled fertiliser price), 0030 (inherited store), 0012 (price bounds from regional trade, inflation index), 0013 (market clearing), 0024 (hut store and spoilage), 0008 (coin scale, per-season realism), 0002 (hourly clearing), 0028 (pacing)
- Depends on specs: 001 (hourly, daily and seasonal passes; the RNG). Uses 005 (ledger postings, net worth). Spec 003 sells its harvests and buys its inputs through this spec.

## 1. Goal

The player decides when, where and how much to sell: a small local market whose price falls when they flood it, a regional trader who always buys but at a lower price, and a store where goods wait for a better season while they slowly spoil.

**Lessons:** supply and demand set prices, and necessities swing hardest; liquidity has a price (sell now at the floor, or wait for more); storage costs you through spoilage; prices drift up with inflation and move with the seasons; transport costs set the band a remote village trades in.

## 2. Scope

**In:**
- **Goods** (Phase 1 set): the five crops, two garden greens and four fruits as produce; seeds, compost, chemical fertiliser, spray and saplings as inputs.
- **Regional prices:** base price × price index × seasonal factor, and the price index's seasonal inflation step.
- **The hourly local market** for produce (ADR 0013): demand curve, supply including the valley's farmers and the trader's imports, the band from transport cost, damped adjustment, the safety rail, unmet demand.
- **Selling:** offering goods on the local market (optionally with a minimum price), withdrawing them, selling to the trader at once, and the **mandi trip** (ADR 0031).
- **Buying inputs** from the village dealer, ahead into the store or at the moment of use (spec 003), at seasonal prices; chemical fertiliser's price is controlled.
- **The inherited store** (ADR 0030).
- **The hut store:** capacity, what happens when a harvest doesn't fit, and daily spoilage.
- **`Fixed.Log2`, `Fixed.Exp2` and `Fixed.Pow`** requirements (the price formula needs non-integer powers).

**Out** (and where it goes instead):
- Buyers other than the valley's aggregate demand (households, businesses) and the seeded rationing order among them: Phase 2 (citizens and businesses).
- Services and customer choice: Phase 2 (ADR 0013).
- The granary, cold storage and forward contracts: Phase 2 and later.
- Roads and the highway (Phase 1 is on a track); the road tiers exist in data only.
- Inventory accounting: Phase 2 statements (spec 005 open question 5). Stored goods count only in net worth.
- The market and store screens' layout: 008.

## 3. Rules and formulas

Quantities are `long`: grams for goods sold by the kg, millilitres for goods sold by the litre, units for goods sold each. A price is `Fixed` coins per kg, litre or unit. Money is `long` in 1/100 coin. **Value** of a quantity at a price:

```
Value(q, P) = round( q × P × 100 ÷ unitSize )       (unitSize 1,000 for g and ml, 1 for each; rounded once, ties away from zero)
```

### 3.1 Goods and regional prices (ADR 0012)

```
P_regional(good, t) = GoodDef.basePrice × PriceIndex × RegionDef.seasonalFactor(good, season(t))
Indexed(amount)      = MulRate(amount, PriceIndex)      every other coin amount in content, when charged (ADR 0034)
TransportCost(good) = RegionDef.transportCostPerKg[roadTier] × GoodDef.weightKg × PriceIndex
Floor   = P_regional − TransportCost        (the trader's buying price)
Ceiling = P_regional + TransportCost        (the import price)
```

- `basePrice` is the year-0 price and is never edited for inflation (ADR 0012).
- Phase 1's road tier is `track`. Example: potato in Spring at index 1.00: P_regional = 8 × 1.00 × 0.85 = 6.80; transport 1.50; floor 5.30, ceiling 8.30. Wheat in Winter: 25 × 1.10 = 27.50; floor 26.00, ceiling 29.00. Transport is per kg, so the band is wide for cheap, heavy goods and narrow for dear ones.
- **Inputs** are bought from the village dealer at their `P_regional` (delivery included); they have no local market and no band. Their seasonal factors make seed dearer in its sowing season and fertiliser and spray dearer in the Spring and Monsoon rushes. A good with `priceControlled` (chemical fertiliser: a government-fixed price) ignores the price index: `P_regional = basePrice × seasonalFactor`.

**Price index** (inflation): starts at `EconomyDef.startIndex` (1.00). At each season start (seasonal pass, system Market):

```
i = (inflationMinPerYear + (inflationMaxPerYear − inflationMinPerYear) × u) ÷ 4      u: seeded draw (stream Inflation, counter = season index)
PriceIndex ← PriceIndex × (1 + i)
```

Defaults 2–6% a year, so about 0.5–1.5% a season. Emits `PriceIndexChanged`.

**Every coin amount in content is a year-0 price** (ADR 0034): land prices, building costs, living costs, the day-labour wage, wages, medicine and cart hire are charged at `Indexed(amount)` at the moment they are charged. Not indexed: price-controlled goods, loans and their terms (nominal), ledger book values (historical cost), and the credit-limit figures (base limit, era caps).

### 3.2 The hourly local market (ADR 0013)

At every hourly pass (system Market), for each produce good in good id order. Rates are per hour, in kg:

```
D_ref     = RegionDef.demandPerDay ÷ 24                          valley households' demand at P_regional
S_valley  = RegionDef.valleySupplyPerDay ÷ 24                    neighbours' produce
S_import  = RegionDef.importCapacityPerDay ÷ 24                  the trader's imports (> 0 for every produce good)
S_founder = min(eligible, max(eligible ÷ MarketDef.offerSpreadHours, minLotKg))   the founder's offer, if its minimum price allows (below)
S         = S_founder + S_valley + S_import                      never zero

P         ← P × P_regional ÷ P_regional(last clearing)            only when P_regional changed (season, index; ADR 0034)
P_target  = P_regional × (D_ref ÷ S) ^ (1 ÷ e)                   e = GoodDef.elasticity
P_target  ← max(P_target, Floor)   while the trader has buying capacity left today
P_target  ← min(P_target, Ceiling)
P_target  ← clamp(P_target, railMin × P_regional, railMax × P_regional)         the safety rail
P         ← P + MarketDef.adjustmentRate × (P_target − P)
P         ← clamp(P, railMin × P_regional, railMax × P_regional)

D         = D_ref × (P ÷ P_regional) ^ (−e)                       demand at the new price
sold      = min(S_founder, D × S_founder ÷ S)                     the founder's pro-rata share of what's bought (grams, rounded down)
toTrader  = min(S_founder − sold, traderCapacityLeft)  this hour's unsold share, only if P_target was raised to the Floor this hour and minPrice ≤ Floor (ADR 0034)
unmet     = max(0, D − S)
```

- **Minimum price** (ADR 0031): an offer may carry `minPrice`. `eligible = offered` if `minPrice ≤ P` (the price at the start of the hour), otherwise 0: the seller keeps the goods off the stall this hour. The price then settles around the minimum and a little sells each hour; the damping keeps it from swinging. Without a minimum, `eligible = offered`.
- **Payment:** `Value(sold, P) + Value(toTrader, Floor)`, Dr Cash, Cr the good's sales account (4000 Crop sales or 4010 Fruit sales). The goods leave the store and the offer. Emits `MarketSales` when anything sold.
- **Pro rata:** when supply exceeds demand, every seller sells the same share of what they bring. In Phase 1 the only buyer is the valley's aggregate demand, so no rationing order is needed; ADR 0013's seeded order arrives with households and businesses.
- **The trader holds the floor:** a target below the floor means the market is glutted; the trader buys the hour's unsold share of the founder's offer at the floor, up to its daily capacity, so a glut drains hour by hour (ADR 0034). When the capacity is used up, the floor no longer holds and the price can fall towards the rail.
- **Minimum lot:** each hour at least `min(eligible, minLotKg)` reaches the stall (default 0.1 kg; 1 kg would glut thin markets like wheat's), so an offer always finishes.
- **Following the region:** when P_regional changed since the last clearing (a new season or an index step), P is scaled by the same ratio first, so a neutral market stays exactly at P_regional (AC-5); damping then handles supply effects only.
- **Defaults** make the market neutral when the founder offers nothing: `valleySupply + import = demand`, so P settles at P_regional.
- **Damping:** with k = 0.25 the price closes 25% of the gap each hour, needs no solver and never overshoots (ADR 0013).
- **Rail hits** (a target or price clamped by the rail) are counted per good for the balancer (ADR 0012: under 0.1% of good-hours).
- **Unmet demand** per good is kept for the last `MarketDef.unmetWindowHours` hours (default 96, one season).
- Example (potato, Spring, defaults below, nothing offered): D_ref = 20.83, S = 18.75 + 2.08 = 20.83, so P_target = P_regional and nothing moves. Offering 240 kg adds S_founder = 10 kg/h: S = 30.83, P_target = 6.80 × (20.83 ÷ 30.83)^(1/0.6) = 6.80 × 0.520 = 3.54, raised to the floor 5.30. The trader takes what local buyers don't.

### 3.3 Selling

| Way | Rule | Price |
| --- | --- | --- |
| **Offer** | `Offer(good, q)` puts q of stored produce on the stall. It sells hour by hour (§3.2) while it stays in the store (and keeps spoiling). `Withdraw(good, q)` takes it back at any time | The hourly price for the local share; the floor for what the trader takes when glutted |
| **Sell to the trader now** | `SellToTrader(good, q)` sells at once, up to the trader's remaining capacity today. Only stock that isn't on offer can be sold this way | The current Floor |
| **Mandi trip** | `GoToMandi(loads)` carts produce to the town market (§3.6) | The regional price less commission, minus a fixed cart hire |

- `Offer(good, q, minPrice?)` adds to the offer; the minimum price applies to the good's whole offer (the latest one given).
- **Trader capacity** per good per day (`RegionDef.traderBuyPerDay`) is shared by both ways and resets at each daily pass.
- Selling never makes Cash negative (it only adds), and every sale is one ledger entry.

### 3.4 Buying inputs

| Way | Rule |
| --- | --- |
| **Ahead** | `Buy(good, q)` buys an input (not saplings) into the store at today's input price: Dr 1200 Inputs in store, Cr Cash, adding to the good's stock cost (average cost, ADR 0034). Rejected with `InsufficientFunds` or `StoreFull` |
| **At use** | `UseInput(good, q, account, reference)` is an internal operation for spec 003 (seed and fertiliser at sowing, spray): it takes stock from the store first, expensed at average cost (Dr `account`, Cr 1200), and buys only the shortfall at today's price (Dr `account`, Cr Cash). The caller checks the shortfall's cost with `CanAfford` and rejects with `InsufficientFunds` |
| **Saplings** | Bought at planting only (spec 003), capitalised to 1700 Trees; never stocked |

### 3.5 The hut store (ADR 0024)

| Rule | Behaviour |
| --- | --- |
| Capacity | `BuildingDef.storeCapacityKg` of the founder's home (hut: 2,000 kg). It counts every good in store by weight (`weightKg` per unit), produce and inputs, offered or not |
| The inheritance | A new game starts with `StartDef.store` in the store (ADR 0030): 600 kg certified seed potato, 75 kg wheat, 75 kg lentils, 0.1 kg each of radish, spinach and coriander seed. The inherited inputs enter the opening balance at their start-time input price (Dr 1200, Cr 3000; ADR 0034) |
| Adding | `StoreAdd(good, q)` for harvests and fruit (spec 003). The caller chooses the overflow rule: **StoreOnly**: rejected with `StoreFull` if q doesn't fit, nothing changes; **SellRestToTrader**: what fits is stored and the rest is sold to the trader at the floor now; rejected with `TraderFull` if the trader can't take the rest |
| Taking | `StoreTake(good, q)` for saved seed, inputs and the household's food (spec 003). Takes stock that isn't on offer first, then reduces the offer |
| Spoilage | At each daily pass, for each good in id order: `lost = round(q × GoodDef.spoilagePerDay)` grams (Fixed, rounded once), taken from stock not on offer first, then from the offer. Emits `GoodsSpoiled(good, lost)` when lost > 0 (for *While you were away*). Spoiled inputs are expensed at average cost to their use account (5000 seed, 5010 the rest; ADR 0034). Ready crops and ripe fruit spoil the same way after their grace (spec 003 §3.3) |
| Valuation | The store's value for net worth (spec 005 §3.5) is `Σ Value(q, Floor)` over produce (what the trader would pay now) plus `Σ Value(q, InputPrice)` over inputs (replacement cost: the trader doesn't buy inputs, so buying ahead is an asset swap; Sprint 2 planning review) |

Examples: 100 kg of radish (0.25 a day) is 75 kg after one day and 31.64 kg after four. 1,000 kg of wheat (0.005) loses 5 kg a day, about 2% a season.

### 3.6 The mandi trip (ADR 0031)

```
GoToMandi(loads)       loads: (good, q) pairs of produce from the store, total ≤ MandiDef.cartCapacityKg
cart hire              MandiDef.cartHirePerTrip, paid at departure (Dr 5700, Cr Cash)
trip                   a job of MandiDef.tripHours; the goods leave the store at departure
founder                exactly MandiDef.founderHours of delivered labour: during the trip the founder's rate is cut by
                       founderHours × 24 ÷ tripHours hours a day, never below 0 (time-weighted, spec 003 §3.6; ADR 0034)
proceeds at return     Σ Value(q, P_regional(good, return time)) × (1 − MandiDef.commissionRate)
                       Dr Cash net, Dr 5700 the commission, Cr the sales account the gross value
```

| Rule | Behaviour |
| --- | --- |
| One at a time | One trip a day, and not while a trip is under way: `TripUnderway`, `AlreadyWentToday` |
| Who | The founder only in Phase 1 (spec 006 may send a hand on a standing order). Not on a sick day (`FounderSick`). Loads come from stock not on offer |
| Price | The town market's regional price: one cart doesn't move it (Phase 1). It isn't capped by the trader's capacity, which is the point of going |
| Why it pays | Transport is a fixed hire for a tractor-trolley (ADR 0037), not per kg, so full loads beat the trader's floor and small loads don't. Example, potato in Spring: 2,000 kg gives 6.80 × 0.97 × 2,000 − 1,000 = 12,192 (trader: 10,600); 1,000 kg gives 5,596 (trader: 5,300) before the founder's 0.5 h (about 560 coins), so only a full cart pays |
| Away | A trip started live completes while away like any job; its proceeds appear in *While you were away* |

### 3.7 Fixed-point powers

The market needs `x^y` for non-integer y. `Sim.Core.Maths.Fixed` gains:

| Function | Domain | Requirement |
| --- | --- | --- |
| `Log2(x)` | x > 0 | Absolute error ≤ 2⁻²⁸ |
| `Exp2(y)` | y ≤ 30 (smaller results that underflow give 0) | Relative error ≤ 2⁻²⁸ |
| `Pow(x, y)` | x > 0, or x = 0 with y > 0 (gives 0) | `Exp2(y × Log2(x))`. For x in [2⁻¹⁰, 2¹⁰] and \|y\| ≤ 4, relative error ≤ 2⁻²⁰ (about 1 in a million) |

- Integer arithmetic only: no `Math.*`, no `float` or `double`, so every platform gives bit-identical results (spec 001 §3.1).
- Invalid arguments throw (`ArgumentOutOfRangeException`), like overflow.

### 3.8 Processing order and determinism

- Hourly: the Market system (order 4) runs after Agriculture (spec 002 moisture, spec 003 crops), goods in id order.
- Daily: spoilage, then the trader's capacity reset.
- Seasonal: the price index step (before spec 005's season close, which reads Floor for net worth).
- Market and store state is appended to `StateHash` after the spec 002 and 005 sections.

## 4. Data

**Definitions:**

`content/goods.json` (`GoodDef`):

| Good | Kind | Unit | basePrice (coins) | elasticity | spoilagePerDay | Sales account |
| --- | --- | --- | --- | --- | --- | --- |
| radish | produce | kg | 7 | 1.2 | 0.25 | crop |
| wheat | produce | kg | 25 | 0.4 | 0.005 | crop |
| rice | produce | kg | 22 | 0.4 | 0.005 | crop |
| potato | produce | kg | 8 | 0.6 | 0.03 | crop |
| lentils | produce | kg | 70 | 0.5 | 0.003 | crop |
| banana | produce | kg | 30 | 1.2 | 0.30 | fruit |
| papaya | produce | kg | 25 | 1.2 | 0.30 | fruit |
| guava | produce | kg | 40 | 1.0 | 0.25 | fruit |
| mango | produce | kg | 50 | 1.5 | 0.20 | fruit |
| spinach | produce | kg | 20 | 1.0 | 0.35 | crop |
| coriander | produce | kg | 60 | 1.2 | 0.40 | crop |
| seed_radish, seed_wheat, seed_rice, seed_potato, seed_lentils, seed_spinach, seed_coriander | input | kg | 300, 45, 60, 20, 110, 200, 150 | — | 0.001 | — |
| compost, fertiliser_chemical | input | kg | 2, 30 (`priceControlled`) | — | 0.001 | — |
| spray | input | litre | 500 | — | 0.001 | — |
| sapling_banana, sapling_papaya, sapling_guava, sapling_mango | input | each | 60, 60, 180, 350 (with planting, ADR 0033) | — | — | — |

Other `GoodDef` fields: `id`, `nameKey`, `edible` (produce: true), `weightKg` per unit (default 1; saplings 2), `tradeable` (all true), `priceControlled` (default false). Bounds: basePrice > 0; elasticity 0.1–3 (produce); spoilagePerDay 0–1; weightKg 0–100.

`content/region.json` (`RegionDef`; one region in Phase 1):

| Field | Default | Bounds |
| --- | --- | --- |
| `roadTier` | track | a key of `transportCostPerKg` |
| `transportCostPerKg` | track 1.50, road 1.00, highway 0.50 | ≥ 0 |
| `seasonalFactor` per good (Spring, Summer, Monsoon, Winter) | radish 1.00, 1.15, 1.10, 0.85 · wheat 0.95, 1.00, 1.05, 1.10 · rice 1.05, 1.05, 0.95, 0.95 · potato 0.85, 1.00, 1.15, 1.10 · lentils 1.00, 0.95, 1.05, 1.00 · banana and papaya 1.00 · guava 1.10, 1.10, 0.95, 0.95 · mango 1.20, 0.90, 1.20, 1.20 · spinach 1.10, 1.15, 1.05, 0.85 · coriander 1.05, 1.15, 1.10, 0.90. Inputs: each seed 1.10 in its crop's sowing seasons and 0.95 otherwise (radish, spinach and coriander seed 1.00 all year); compost, fertiliser and spray 1.05 in Spring and Monsoon, 1.00 otherwise | 0.5–2 |
| `demandPerDay` (kg) | radish 150, wheat 300, rice 300, potato 500, lentils 40, banana 60, papaya 40, guava 40, mango 80, spinach 60, coriander 15 | > 0 |
| `valleySupplyPerDay` (kg) | 90% of demand | ≥ 0 |
| `importCapacityPerDay` (kg) | 10% of demand | > 0 for every produce good (linter, ADR 0013) |
| `traderBuyPerDay` (kg) | radish 300, wheat 2,000, rice 2,000, potato 3,000, lentils 500, banana 150, papaya 100, guava 150, mango 400, spinach 100, coriander 30 | ≥ 0 |

`content/market.json` (`MarketDef`, `EconomyDef`):

| Definition | Field | Default | Bounds |
| --- | --- | --- | --- |
| `MarketDef` | `adjustmentRate` | 0.25 | 0.01–1 |
| | `railMin`, `railMax` | 0.5, 2.0 | 0 < min < 1 < max |
| | `offerSpreadHours` | 24 | 1–96 |
| | `unmetWindowHours` | 96 | 1–384 |
| | `minLotKg` | 0.1 | > 0 |
| `MandiDef` | `tripHours` | 6 | 1–24 |
| | `cartCapacityKg` | 2,000 (a tractor-trolley, ADR 0037) | > 0 |
| | `cartHirePerTrip` | 1,000 coins | ≥ 0 |
| | `commissionRate` | 0.03 (commission and market fee) | 0–0.2 |
| | `founderHours` | 0.5 (one real working day under per-season scaling) | 0–`tripHours` |
| `EconomyDef` | `startIndex` | 1.00 | > 0 |
| | `inflationMinPerYear`, `inflationMaxPerYear` | 0.02, 0.06 | 0–0.5, min ≤ max |

`content/buildings.json` gains `storeCapacityKg` (hut 2,000; well 0).

`content/start.json` gains `store`: a list of (good id, quantity in the good's unit), the inherited store above. It must fit the hut store.

**State** (part of `GameState`):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Economy` | `priceIndex` | Fixed | |
| `MarketGood` (per produce good, in good order) | `price`, `lastRegional` | Fixed | Starts at P_regional |
| | `offered`, `minPrice` | long, Fixed or none | Part of the store |
| | `traderBoughtToday` | long | Reset daily |
| | `unmet` | ring of Fixed kg per hour | Last `unmetWindowHours` |
| | `railHits`, `hours` | long | For the balancer |
| `Store` | `quantity` per good, `stockCost` per input | long | In good order; `stockCost` is the inputs' book value (1200) |
| `MandiTrip` | `loads`, `departedAt`, `returnAt`, `jobId`, `lastTripDay` | | At most one under way |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `Offer` | `goodId`, `quantity`, optional `minPrice` | `UnknownGood`; `NotProduce`; `InvalidQuantity` (≤ 0); `InsufficientStock` (more than the stock not on offer) | §3.3 |
| `Withdraw` | `goodId`, `quantity` | `UnknownGood`; `InvalidQuantity`; `NotOffered` (more than is on offer) | §3.3 |
| `SellToTrader` | `goodId`, `quantity` | `UnknownGood`; `NotProduce`; `InvalidQuantity`; `InsufficientStock`; `TraderFull` (more than its capacity left today) | Sells at the Floor now. Emits `SoldToTrader` |
| `Buy` | `goodId`, `quantity` | `UnknownGood`; `NotAnInput` (produce or saplings); `InvalidQuantity`; `StoreFull`; `InsufficientFunds` | §3.4. Emits `InputsBought` |
| `GoToMandi` | list of (`goodId`, `quantity`) | `UnknownGood`; `NotProduce`; `InvalidQuantity`; `InsufficientStock` (stock not on offer); `OverCartCapacity`; `TripUnderway`; `AlreadyWentToday`; `FounderSick`; `InsufficientFunds` (cart hire) | §3.6. Emits `MandiTripStarted`, then `MandiTripReturned` |

**Internal operations** (for spec 003 and 005): `StoreAdd(good, q, overflow)`, `StoreTake(good, q)`, `UseInput(good, q, account, ref)`, `Floor(good)`, `InputPrice(good)`, `StoreValue()`.

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `MarketSales` | `goodId`, local kg and price, trader kg and floor, `amount` | An hourly pass sells any of the founder's offer | HUD (coins fly to the wallet), report, play log (first sale) |
| `SoldToTrader` | `goodId`, `quantity`, `price`, `amount` | `SellToTrader`, or a harvest's overflow | HUD, report, play log (first sale) |
| `GoodsSpoiled` | `goodId`, `quantity` | Daily spoilage | Report, store panel |
| `PriceIndexChanged` | `old`, `new` | Season start | Advisor (later), report |
| `InputsBought` | `goodId`, `quantity`, `amount` | `Buy`, or a shortfall bought at use | Report |
| `MandiTripStarted` / `MandiTripReturned` | loads, cart hire / gross, commission, net | §3.6 | HUD (cart leaves and returns, coins fly), report, play log |

Hourly price moves emit no events; the HUD reads prices from state.

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Sell to the trader now | The floor price (5–20% below the regional price) | Cash at once; store space freed | Trader capacity used for the day | — |
| Offer on the market | None | Better prices if you offer little at a time; flooding drives the price to the floor | Goods keep spoiling while on offer | Withdraw any time |
| Store for a better season | Spoilage (radish ¼ a day, wheat 0.5%) | Seasonal factors (potato +35% from Spring to Monsoon) and inflation | Store space; harvests may not fit | Sell any time |
| Offer with a minimum price | None | Sells only at or above it, a little each hour; may not sell at all | Goods keep spoiling | Change or withdraw any time |
| Go to the mandi | Cart hire, and 0.5 h of the founder's labour over a 6-hour trip | The regional price for a full 2,000 kg load (about 15% over the floor for potato) | Less care on the fields that day | — |
| Buy inputs ahead | Cash now, store space | Avoids the sowing-season price rise | — | Inputs barely spoil |
| Harvest when the store is full | Choose: leave the crop waiting, or sell the overflow at the floor | — | Field stays busy if it waits (003) | — |

## 7. UI

Layouts are spec 008's. Content:

```
Market sheet (tap the store or a good)
+-----------------------------------------------------------+
| Potato   in store 1,850 kg (93% full)   on offer 240 kg   |
| Market price 5.30 /kg   (regional 6.80)       [Why?]      |
|   regional 8.00 x index 1.00 x Spring 0.85                |
|   valley demand 500 kg/day; your offer pushes it to floor |
| Trader buys at 5.30 /kg (can take 2,760 kg more today)    |
|   floor = regional 6.80 - transport 1.50 (track road)     |
| Spoils 3% a day: about 55 kg tomorrow                     |
| [Offer ...]   [Withdraw]   [Sell to trader: 1,610 kg]    |
+-----------------------------------------------------------+

Harvest when the store is full
+---------------------------------------------+
| 3,300 kg potato won't fit (room: 150 kg)    |
| [Store 150, sell 3,150 to trader: 16,695]   |
| [Leave it in the field]                     |
+---------------------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Loading content with an unknown good in a crop or tree, a produce good with `importCapacityPerDay` 0, an elasticity of 0, a seasonal factor outside 0.5–2, or `railMin ≥ 1` fails validation naming the field | `ContentTests.Market_Validation` |
| AC-2 | P_regional, Floor and Ceiling match §3.1's examples exactly (potato Spring 6.80 / 5.30 / 8.30; wheat Winter 27.50 / 26.00 / 29.00) | `PriceTests.Regional_Band` |
| AC-3 | Over 10 game years the price index rises every season by a factor within [1.005, 1.015], and the same seed gives the same index path | `PriceTests.Index_SeasonalInflation` |
| AC-4 | `Log2`, `Exp2` and `Pow` meet §3.6's error bounds against stored high-precision reference values (at least 200 points across the domain), reject invalid arguments, and use no floating point (the test also checks a handful of results bit for bit) | `FixedTests.Pow_Exp_Log` |
| AC-5 | With nothing offered and the default region, every good's price stays exactly at P_regional, including across season changes and index steps | `MarketTests.Neutral_WhenNothingOffered` |
| AC-6 | Property: under constant D_ref and S the price converges monotonically to P_target (no overshoot), within 1% after 20 hours | `MarketTests.Converges_NoOvershoot` |
| AC-7 | Property: over 1,000 seeded random offer and withdraw sequences, the price never leaves the safety rail, S is never 0, and two runs with the same seed give identical prices and sales | `MarketTests.Rail_SupplyNeverZero_Deterministic` |
| AC-8 | Offering a glut moves the target to the floor; each hour the trader buys exactly that hour's unsold share at the floor, up to its daily capacity; once the capacity is used, the price can fall below the floor; the capacity resets at the daily pass; with the minimum lot an offer always finishes | `MarketTests.TraderHoldsFloor_UntilCapacity` |
| AC-9 | A small offer sells hour by hour at the local price as the founder's pro-rata share of demand, paid exactly `Value(sold, P)` to Cash and Crop sales (fruit to Fruit sales) | `MarketTests.Offer_SellsProRata` |
| AC-10 | `SellToTrader` pays exactly `Value(q, Floor)`, uses trader capacity, rejects more than the stock not on offer (`InsufficientStock`) or the capacity left (`TraderFull`) without changing state | `MarketTests.SellToTrader` |
| AC-11 | A sapling bought at planting costs exactly `Value(1, P_regional)` and is capitalised (spec 003); `Buy` rejects saplings and produce with `NotAnInput` | `MarketTests.Saplings_AtPlantingOnly` |
| AC-12 | `StoreAdd` with StoreOnly rejects a harvest that doesn't fit and changes nothing; with SellRestToTrader it stores what fits and sells exactly the rest at the floor, or rejects with `TraderFull` | `StoreTests.Capacity_AndOverflow` |
| AC-13 | Daily spoilage takes exactly `round(q × rate)` per good (100 kg radish → 75 kg → … 31.640625 kg after 4 days within gram rounding), from stock not on offer first, and emits `GoodsSpoiled` | `StoreTests.Spoilage_Daily` |
| AC-14 | `StoreValue()` equals Σ `Value(q, Floor)` over produce plus Σ `Value(q, InputPrice)` over inputs, and is the stored-goods term of spec 005's net worth | `StoreTests.Valuation_AtTraderPrice` |
| AC-15 | Unmet demand is recorded per good per hour and only the last 96 hours are kept | `MarketTests.UnmetDemand_Window` |
| AC-16 | The chunking-invariance check holds over one game year with offers, trader sales and spoilage, and the golden replay `golden/004-market.json` reproduces its stored hash | `MarketTests.ChunkingInvariance`, `ReplayTests.Golden_004_Market` |
| AC-17 | Performance (CI, Release): advancing 72 h with the market (11 produce goods), the store and the default farm takes under 20 ms | `PerfTests.Advance72h_WithMarket` (category `Perf`) |
| AC-18 | An offer with `minPrice` above the current price adds nothing to supply and sells nothing that hour; once the price is at or above it, it sells as usual; the trader never takes it at a floor below its minimum | `MarketTests.Offer_MinPrice` |
| AC-19 | `Buy` puts inputs into the store at today's input price (seed 10% dearer in its sowing season), counts against capacity, and posts Dr 1200 Inputs in store; `UseInput` takes stock first, expensed at average cost to 5000 or 5010, and buys exactly the shortfall; spoiled inputs are expensed at average cost | `MarketTests.Buy_AheadAndAtUse` |
| AC-20 | Chemical fertiliser's input price is unchanged by 10 game years of inflation except for its seasonal factor; seed prices follow the index | `PriceTests.PriceControlled_IgnoresIndex` |
| AC-21 | A mandi trip with 2,000 kg of potato in Spring pays exactly `Value(2,000 kg, P_regional at return) × 0.97` net of commission, charges 1,000 coins of cart hire at departure (both to 5700), returns after exactly 6 h, and reduces the founder's delivered labour by exactly 0.5 h, whatever the departure time | `MandiTests.Trip_PaysAndCosts` |
| AC-22 | `GoToMandi` rejects more than 2,000 kg, a second trip the same day, a trip while one is under way, a sick day, and stock that isn't there or is on offer, without changing state | `MandiTests.Trip_Rejections` |
| AC-23 | A new game's store holds exactly the inherited goods of `StartDef.store`, and loading a `store` that doesn't fit the hut fails validation | `StoreTests.InheritedStore` |
| AC-24 | Every indexed coin amount (land, buildings, living costs, wages, day labour, medicine, cart hire) is charged at exactly `MulRate(amount, PriceIndex)`; price-controlled goods and loans ignore the index | `PriceTests.Indexed_CoinAmounts` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Safety-rail triggers (ADR 0012) | Under 0.1% of good-hours for every bot | Balancer |
| Price received for a whole 2-acre harvest sold at once | Close to the floor (5–20% under regional) | Shape |
| Price received when selling a little each hour | Close to the regional price | Shape |
| Storing potato from Spring to Monsoon | Floor rises from 5.30 to 7.70 (+45%) against about 22% lost to spoilage over the two seasons: storing pays, and store space is the real limit | Shape |
| Hut store | Holds about one potato harvest of one acre, or a season of wheat from 2 acres | ±50% |
| Price index after 10 game years | 1.22–1.79 (2–6% a year) | — |
| Mandi trip versus the trader, a full 2,000 kg load | Cheap, heavy goods gain most (potato about 15% over the floor; break-even about 1,100 kg after the founder's labour). Measured: the competent bot takes the mandi about a third of its sales (ADR 0037). Dear goods gain little or nothing (wheat: the 3% commission outweighs the trader's 1.50 a kg transport) | Shape |
| Buying seed a season ahead | Saves about 15% of the seed bill | Shape |

## 10. Open questions

All four were answered in the Sprint 1 review (2026-10-17):

1. **Who buys the harvest.** The trader stays the main buyer and the local market small: that is how Indian smallholders sell. The **mandi trip** is added to Phase 1 (§3.6, ADR 0031) for full loads at the town's price.
2. **Input prices.** The village dealer's price is the regional price, delivery included. Inputs can be bought ahead, with seasonal prices; chemical fertiliser has a controlled price that ignores inflation, with a peak-season premium (§3.1, §3.4, ADR 0031).
3. **Pro rata** among sellers when supply exceeds demand. ADR 0013's seeded order is for scarce supply among many buyers, which arrives with households and businesses (Phase 2).
4. **Minimum prices** on offers are added now (§3.2); spec 006's standing orders reuse the rule.

**Sprint split (Sprint 2 planning review, 2026-10-18; no rule change):** crops need goods, prices and the store, so a core of this spec is built in Sprint 2: `GoodDef`, the price part of `RegionDef`, P_regional, Floor and input prices (the price index stays at `startIndex` until the inflation step), the store with capacity, both overflow rules and the inheritance, the trader's daily capacity, `StoreValue` and `UseInput`. Its ACs (AC-1's goods checks, AC-2, AC-12, AC-14, AC-23) pass in Sprint 2. Inflation, spoilage, the hourly market, `Offer`, `SellToTrader`, `Buy`, minimum prices and the mandi trip stay in Sprint 3. **Price precision (implementation note):** prices are `Fixed` products of decimal factors that aren't exact in binary (0.85, 1.10), so they match the decimal examples to within a few units in the last place (2⁻³²); `Value` then rounds once to 1/100 coin, so no amount differs. The AC-2 test allows 16 units in the last place. Home use (spec 003, ADR 0032) values eaten food at the Floor and credits the sales account. **Stored inputs** count in net worth at today's input price (owner decision, 2026-10-18).

### Changes after approval (Sprint 3 planning review, 2026-10-18, ADR 0034)

Rule changes approved by the owner before coding:

1. **A glut drains hour by hour.** The trader takes the hour's unsold share (`S_founder − sold`), not the whole offer, and a minimum lot (`minLotKg`, 1 kg) lets every offer finish. Before, offers above about 80 kg of potato dumped at the floor in one hour.
2. **Indexed coin amounts.** Every coin amount in content is a year-0 price charged at `Indexed(amount)`; price-controlled goods, loans, book values and credit-limit figures aren't indexed (new AC-24).
3. **Inputs in store are an asset** (account 1200, average cost); `Buy` is an asset swap in the P&L too; the inherited inputs are on the opening balance sheet.
4. **The mandi trip's labour** is `founderHours` (0.5 h), taken as a time-weighted rate cut over the trip; not on a sick day; loads from stock not on offer.
5. **The price follows the region:** P is rescaled when P_regional changes, so AC-5 holds across season changes.
6. Golden 004 runs on a frozen content snapshot committed next to it.

### Changes after approval (Sprint 3 findings, 2026-10-18, ADR 0037)

- **The cart is a tractor-trolley:** 2,000 kg for 1,000 coins of hire (was a 1,000 kg bullock cart at 800). A full cart now beats the trader for cheap, heavy goods; half a load doesn't.

### Implementation notes (Sprint 3, 2026-10-18)

None of these changes a formula or a rule.

1. **Fixed powers.** `Log2` squares the mantissa in Q1.63 (33 bits, rounded: absolute error ≤ 2⁻³²); `Exp2` multiplies hard-coded Q1.63 constants 2^(2⁻ᵏ) (computed to 80 digits). A Q31.32 result below about 2⁻⁴ has fewer significant bits than a 2⁻²⁸ relative bound asks for, so AC-4 checks "relative bound, or within one unit in the last place". The tests use `double` only as the reference.
2. **The index draw** is `Rng.Next(seed, Inflation, 0, season index)`, top 32 bits as u in [0, 1).
3. **Supply and demand in grams a day.** The clearing sums whole grams a day (the founder's lot × 24 + the valley's + imports), so a neutral market gives a ratio of exactly 1 and AC-5 holds bit for bit; the lot is whole grams an hour.
4. **The minimum lot** is 0.1 kg (data): 1 kg alone raised wheat's thin, inelastic market (e = 0.4, 12.5 kg an hour) 8% and pushed it to the floor, so "selling a little each hour" wasn't possible.
5. **Minimum price and the hour's price.** Eligibility is checked against the price at the start of the hour, and the hour's sales are paid at the new price (§3.2), so an hour's lot can sell a little under the minimum (for example 6.43 against 6.50) before the offer leaves the stall. The trader never takes it below the minimum.
6. **Initialisation order.** The market is set up before the land at a new game, because land prices and building costs read the price index.

## 11. Cut first

1. Unmet-demand history (keep only the current hour).
2. Minimum prices on offers.
3. Buying inputs ahead (keep buying at use).
4. `Withdraw` (offers can't be taken back; they sell or spoil).
5. Seasonal price factors (all 1.00; inflation alone moves prices).
6. The mandi trip.
7. Offering on the local market (sell to the trader only; storage and seasons still teach).
