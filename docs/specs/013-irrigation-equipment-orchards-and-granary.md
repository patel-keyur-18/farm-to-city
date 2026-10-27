# 013. Irrigation, equipment, orchards and granary

- Status: Draft (Sprint 14): gaps G1–G8 open with the owner, one per message (§10)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Farming (irrigation ladder, equipment ladder, fruit trees: whole-acre orchards; storage and selling: the granary); Finance (depreciation, buy or lease, capital versus operating spending)
- Amends: spec 002 (moisture: canal and drip floors, §3.1), spec 003 (the Tools factor and labour per acre with machines, §3.2; orchards as tree acres, §3.3), spec 004 (the granary's capacity and slower spoilage, §3.4), spec 005 (machines as assets, leases as rent), spec 012 (bullocks take shed places, §3.2)
- Related ADRs: 0032 (the well acres), 0014 (the yield formula and its Tools factor), 0021 and 0033 (trees and their prices), 0024 and 0034 (the store, spoilage), 0037 (the hired tractor-trolley), 0057 (spec order), 0064 (map layouts previewed)
- Depends on specs: 002, 003, 004, 005, 006, 012

## 1. Goal

The player turns a farm of rain-fed acres and hand tools into a capital-backed farm: a canal channel and drip lines that steady the water, an ox plough and a tractor bought, leased or hired by the day, a whole acre of mango or guava, and a granary that lets grain wait for a better price, each a choice between paying now and paying as you go.

**Lesson:** capital spending versus operating spending; depreciation and resale value; buy, lease or hire depends on how much you use it; maintenance skipped is a breakdown later; a long-term asset (an orchard) costs years of crops before it pays; storage turns time into price.

## 2. Scope

**In:**
- The irrigation ladder per acre: rain-fed, the well and buckets (as now), a canal channel, drip (§3.1).
- The equipment ladder: hand tools (as now), an ox plough with a pair of bullocks, a tractor; bought, leased a season or hired a day; maintenance, breakdowns, depreciation, resale; a combine harvester hired by the acre at harvest (§3.2).
- Whole-acre orchards of mango, guava, banana or papaya (§3.3).
- The granary (§3.4).

**Out** (and where it goes instead):
- Buying a harvester, cold storage, a tube well per acre, solar pumps: Phase 3.
- Droughts, floods and hailstorms as events: the event engine (F16.1).
- Apple orchards: hill parcels, Phase 3.
- Hands driving the tractor for neighbours (custom hiring as a business): Phase 3.

## 3. Rules and formulas

Every coin amount is a year-0 price that follows the price index when charged (ADR 0034). Defaults are placeholders for the balancer.

### 3.1 The irrigation ladder (per acre; §10 G1, G2)

| Step | Era | Cost | Running | Moisture (spec 002 §3.2 step 4) | Labour |
| --- | --- | --- | --- | --- | --- |
| Rain-fed | Start | — | — | Weather and soil only | — |
| Well and buckets | Start (the 2 well acres) | — | Your bucket labour (spec 003 §3.6) | Floor 30 | 1–2.4 h a day an acre in dry weather |
| **Canal channel** | Hamlet | 6,000 an acre (a lined field channel from the minor canal along the river side; an acre must border a channelled acre or the canal) | Water fee 400 a season an acre (the irrigation department), paid at the close | Floor 45 in Monsoon, Winter and Spring; **dry in Summer** (the canal is closed for repairs, as Indian canals are in May–June) | None |
| **Drip** | Village | 40,000 an acre (pipes, drippers, filter), less a 50% government subsidy (PMKSY) for the first 2 acres; depreciates over 30 seasons | Power 300 a season an acre, maintenance 1% of cost a season | Held at the crop's `moistureMin` all year, never above its `moistureMax` (drip doesn't waterlog); fertiliser's yield bonus +5% (fertigation) | None; Care demand −0.5 h a day an acre (no weeding between lines) |

- Drip draws from the well: the well feeds drip on up to `wellDripAcres` 4 acres; a **borewell** (30,000, 60 seasons) feeds 6 more (§10 G2).
- An irrigated acre's water factor rarely falls below 1 except in the Summer canal closure; that steadiness is what the player buys (§9).
- A channel or drip line is a building on the acre: capitalised (Dr Buildings), depreciated (spec 005 §3.3), sold with the acre at book value, seized with bought land.

### 3.2 The equipment ladder (§10 G3, G4, G5)

| Machine | Era | Buy | Lease a season | Hire a day | Upkeep | Covers | Effect on the acres it covers |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Hand tools | Start | — | — | — | — | All | Tools 1.00 (as now) |
| **Ox plough** (a pair of bullocks, a plough, a cart) | Hamlet | 70,000 (bullocks 60,000 as livestock, plough and cart 10,000 over 20 seasons) | — | 900 a day with its owner | Bullocks eat as cattle and take 2 shed places (spec 012); 1 h a day of care | 6 acres | Labour −15% (spec 003 §3.6 `labourPerAcrePerDay`), Tools 1.03 (timely sowing); the cart takes 500 kg to the mandi (no trolley hire for small loads) |
| **Tractor** (35 HP with a cultivator and a trolley) | Village | 6,00,000 (bank loan: the tractor as collateral at 0.70) over 60 seasons | 30,000 a season | 2,000 a day with a driver | Diesel 600 a season an acre covered; maintenance 1.5% of price a season | 20 acres | Labour −35%, Tools 1.06; mandi trips without trolley hire (2,000 kg) |
| **Combine harvester** | Village | — (Phase 3) | — | 2,500 an acre at harvest (wheat, rice) | — | The acres hired | The harvest needs no labourers; half the straw is lost (it's cut high) |

- **Coverage:** a machine covers its acres in id order (well acres first); the best machine covering an acre applies. A leased machine is a season's rent (Dr Rent, paid upfront at the close). A hired day covers all your acres for that day only (a sowing or a harvest day).
- **Maintenance** (bought machines): on by default (paid at the close); skipped, it saves the cost and raises the breakdown chance.
- **Breakdowns:** at each daily pass a bought machine breaks with probability `breakdownMaintained` 0.005 or `breakdownSkipped` 0.04 a day (seeded, `equipment` stream); broken, it covers nothing for 24 h and costs 5% of its price to repair (Dr Repairs). Never while away beyond the repair (the 24 h runs on; no losses from it alone).
- **Depreciation and resale:** straight-line over its life (spec 005 §3.3); sold at `resaleShare` 0.80 of book value (the market value is shown beside the book value, GDD Finance).

### 3.3 Whole-acre orchards (§10 G6, G7)

| Tree | Trees an acre | Saplings | First fruit | Batches | Labour an acre a day |
| --- | --- | --- | --- | --- | --- |
| Mango | 40 | 40 × 350 = 14,000 | about 24 days (spec 003's `growthMinutes`) | Each Summer, whole game | 1.0 young, 2.0 bearing |
| Guava | 100 | 18,000 | about 14 days | Monsoon and Winter, whole game | 1.0, 2.5 |
| Banana | 400 | 24,000 | about 7 days | Every season, 4 harvests then replant | 2.0, 3.0 |
| Papaya | 400 | 24,000 | about 7 days | Every season, 8 harvests | 2.0, 3.0 |

- Planted with `PlantOrchard(acre, tree)` on an owned acre with nothing growing: the acre becomes an orchard (no field crop, no field-edge slots), its trees capitalised (Dr Trees) and counted in net worth at book value, its shade on neighbours as spec 003's field-edge trees.
- **Intercropping while young** (§10 G7): until the first fruit, a short crop (radish, spinach, coriander, lentils) can grow between the rows at half the acre's yield and half its labour.
- Fruit per batch = trees × `batchYieldPerTree` (spec 003) × Water × Care × Weather (the same factors as crops); picked by the founder, a hand's orchard order, or labourers at harvest; it waits ripe 72 h then spoils (ADR 0034).
- Removing an orchard (`ClearOrchard`) writes the trees off (Dr Loss) and frees the acre.

### 3.4 The granary (§10 G8)

| Rule | Behaviour |
| --- | --- |
| Building | A pucca granary (godown) on the yard: 45,000, 80 seasons, built in 24 h; one (Hamlet) |
| Capacity | 10,000 kg of dry goods: wheat, rice, lentils, atta, seed and saved seed. They go to the granary first, then the hut store |
| Spoilage | Goods in the granary spoil at `granarySpoilageFactor` 0.2 of their rate (wheat 0.005 → 0.001 a day) |
| Not for | Potato, vegetables, fruit, milk (they need cold storage, Phase 3) |
| Books | A building (spec 005); the goods are the same stock, valued as now |

### 3.5 Processing order

Hourly: moisture (spec 002) with the canal and drip floors after the well's. Daily: breakdowns and repairs (machines in id order), spoilage with the granary's factor. Seasonal (with spec 005's close): water fees, drip power and maintenance, machine maintenance, leases paid upfront, depreciation.

## 4. Data

**New definitions:** `content/farming.json`: `irrigation` (canal: cost an acre, fee, floor, dry seasons; drip: cost, subsidy share and acres, power, maintenance, life, fertigation bonus, labour cut; `wellDripAcres`; the borewell), `equipment` (per machine: era, price, life, lease, hire, upkeep, covers, labour cut, tools factor, cart kg, breakdown chances, repair share, `resaleShare`), `orchard` (trees an acre, labour young and bearing, the intercrop share). `buildings.json`: `granary` (value, life, capacity, the goods it holds, spoilage factor), `borewell`, `canal_channel`, `drip`. `livestock.json`: `bullock` (price, feed, shed places 1 each, no milk).

**New state** (save schema 11 with spec 017): per tile, irrigation (none, canal, drip); `Machine` (id, kind, owned or leased, bought at, book value, maintained, broken until); hired days (kind, day); `Orchard` on a tile (tree, trees, planted at, batches, intercrop); the granary's goods (a store place, spec 012's mechanism).

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `BuildChannel` | tile | `NotOwned`, `NotAdjacent`, `EraLocked`, `InsufficientFunds` | §3.1 |
| `LayDrip` | tile | `NoWaterSource`, `EraLocked`, `InsufficientFunds` | §3.1 |
| `BuyMachine`, `LeaseMachine`, `HireMachine` | kind (+ financing) | `EraLocked`, `NoShedRoom` (bullocks), `InsufficientFunds`, `CreditRefused` | §3.2 |
| `SellMachine`, `SetMaintenance` | machine (, on) | `UnknownMachine` | §3.2 |
| `PlantOrchard`, `ClearOrchard` | tile, tree | `TileOccupied`, `NotOwned`, `InsufficientFunds` | §3.3 |
| `BuildGranary` | — | `EraLocked`, `AlreadyBuilt`, `InsufficientFunds` | §3.4 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `ChannelBuilt`, `DripLaid`, `MachineBought`, `MachineLeased`, `MachineHired`, `MachineSold` | | Motion |
| `MachineBroke`, `MachineRepaired` | machine, cost | Alerts, away card |
| `OrchardPlanted`, `OrchardBearing` | tile, tree | Motion, away card |
| `GranaryBuilt` | | Motion |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Canal channel on an acre | 6,000 | Steadier water most of the year; a fee each season | No bucket labour | Permanent (sold with the acre) |
| Drip on an acre | 20,000–40,000 | Water at the crop's need all year, +5% with fertiliser | Power and upkeep | Permanent |
| Buy a tractor | 6,00,000 or a loan | Labour −35%, Tools 1.06, no trolley hire | Diesel, maintenance, breakdowns | Sell at 80% of book |
| Lease or hire instead | A season's or a day's charge | The same effect while it lasts | No resale, no breakdown risk | Ends by itself |
| Plant a mango orchard | 14,000 and the acre's crops for 1.5 years | Fruit every Summer, for the whole game | Gluts the mango market if too many | Clear at a loss |
| Build the granary | 45,000 | Grain waits for Winter prices at a fifth of the spoilage | More room in the hut store | Permanent |

## 7. UI

```
Field card (irrigation):  Water: rain-fed [?]  [Dig a canal channel · 6,000]  [Lay drip · 20,000]
Menu → Equipment:  Ox plough  [Buy 70,000] [Hire a day 900]
                   Tractor    [Buy 6,00,000] [Lease 30,000 a season] [Hire a day 2,000]
                   Yours: Tractor · book 5,70,000 · market 4,56,000 · maintained [Skip] · covers 8 acres [?]
Field card (empty acre):  [Plant an orchard] → mango 40 trees 14,000 · first fruit in about 24 days [?]
Yard:  [Build a granary 45,000] → granary: wheat 3,200 kg · rice 1,100 kg (10,000 kg) [?]
```

*Why?* rows: the water floor and fee; labour saved and Tools factor; book versus market value; the orchard's payback; the granary's spoilage saved. Art in Blender (channels, drip lines, bullocks, the tractor, orchard trees in rows, the granary), map layouts previewed to the owner first (ADR 0064).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | A canal acre stays at or above 45 moisture except in Summer; its fee is paid at the close | `IrrigationTests.Canal` |
| AC-2 | A drip acre holds the crop's band all year; beyond the well's 4 acres it needs a borewell | `IrrigationTests.Drip_Source` |
| AC-3 | A machine cuts labour and raises Tools on the acres it covers, the best one applying | `EquipmentTests.Coverage` |
| AC-4 | Skipped maintenance raises breakdowns; a broken machine covers nothing for 24 h and costs its repair | `EquipmentTests.Breakdowns` |
| AC-5 | A bought machine depreciates straight-line and sells at 80% of book; a lease is a season's rent | `EquipmentTests.Books` |
| AC-6 | An orchard bears by spec 003's rules on every tree; intercrops grow at half until the first fruit | `OrchardTests.Bearing_Intercrop` |
| AC-7 | The granary takes dry goods first and spoils them at a fifth of the rate | `GranaryTests.Capacity_Spoilage` |
| AC-8 | Same seed and commands give the same farm, in one chunk or many | `EquipmentTests.ChunkingInvariance` |
| AC-9 | A schema-10 save loads into 11 with rain-fed acres, hand tools and no granary | `MigrationTests.Schema10_Farm` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| A canal acre's season profit against a rain-fed one | +10–25% (steadier water), payback 2–4 seasons | |
| Drip's payback with the subsidy | 4–8 seasons | |
| A tractor bought against hired days, for 8 acres | Hiring cheaper below about 10 acres; buying pays over 60 seasons only on a big farm | The lesson |
| A mango orchard against crops on the same acre | Behind for about 6 seasons, ahead within 12 | |
| Wheat stored from Winter to Summer in the granary | Earns more than its spoilage and the granary's cost a season on 5+ tonnes | |

## 10. Open questions (closed one per message with the owner)

- **G1. The irrigation ladder.** Recommendation: rain-fed, well and buckets, a canal channel (Hamlet, dry in Summer) and drip (Village), as the GDD's ladder, per acre.
- **G2. Water for drip.** Recommendation: the well feeds drip on 4 acres, a borewell 6 more; no water budget beyond that in Phase 2.
- **G3. Which machines.** Recommendation: an ox plough (Hamlet) and a tractor (Village) to buy, lease or hire; the combine harvester hired by the acre only.
- **G4. Bullocks.** Recommendation: the ox plough's bullocks are animals in the cattle shed (2 of its 4 places), fed like cattle, no milk.
- **G5. Breakdowns.** Recommendation: a small daily chance (higher when maintenance is skipped), 24 h out and 5% of the price to repair; never a loss beyond that.
- **G6. Orchards.** Recommendation: whole acres of mango (40 trees), guava (100), banana and papaya (400), by spec 003's tree rules.
- **G7. Intercropping.** Recommendation: a short crop between the young trees at half the yield until the first fruit (the real practice that carries an orchard's cash flow).
- **G8. The granary.** Recommendation: one on the yard from the Hamlet, 10,000 kg of grains and pulses at a fifth of the spoilage; cold storage in Phase 3.

## 11. Cut first

1. Breakdowns (machines never break).
2. Leasing (buy or hire only).
3. Intercropping.
4. The borewell (drip on the well's 4 acres only).
