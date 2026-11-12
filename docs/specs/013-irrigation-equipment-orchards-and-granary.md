# 013. Irrigation, equipment, orchards and granary

- Status: Approved 2026-10-27 (ADR 0075); every gap closed with the owner, one per message; built in Sprint 16 (ADR 0057)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Farming (irrigation ladder, equipment ladder, fruit trees: whole-acre orchards; storage and selling: the granary); Finance (depreciation, buy or lease, capital versus operating spending)
- Amends: spec 002 (moisture: canal and drip floors, §3.1), spec 003 (the Tools factor and labour per acre with machines, §3.2; orchards as tree acres, §3.3), spec 004 (the granary's capacity and slower spoilage, §3.4), spec 005 (machines as assets, leases as rent), spec 012 (bullocks take shed places, §3.2)
- Amended by: ADR 0086 (the borewell), ADR 0087 (Sprint 16 plan check: the canal, one source an acre, the godown in the mandi town, renting and letting), ADR 0103 (the godown's sheet says how goods get there and the rent can be ended in one step), ADR 0088 (the bullocks' own shed; machines drawn at work and at 70%), ADR 0089 (equipment as built: the combine to Phase 3)
- Related ADRs: 0032 (the well acres), 0014 (the yield formula and its Tools factor), 0021 and 0033 (trees and their prices), 0024 and 0034 (the store, spoilage), 0037 (the hired tractor-trolley), 0057 (spec order), 0064 (map layouts previewed)
- Depends on specs: 002, 003, 004, 005, 006, 012

## 1. Goal

The player turns a farm of rain-fed acres and hand tools into a capital-backed farm: a canal channel and drip lines that steady the water, an ox plough and a tractor bought, leased or hired by the day, a whole acre of mango or guava, and a granary that lets grain wait for a better price, each a choice between paying now and paying as you go.

**Lesson:** capital spending versus operating spending (buckets cost your hours, a pump costs power, a canal a fee, drip a lot up front); depreciation and resale value; buy, lease or hire depends on how much you use it; maintenance skipped is a breakdown later; a long-term asset (an orchard) costs years of crops before it pays; storage turns time into price.

## 2. Scope

**In:**
- The irrigation ladder per acre: rain-fed, the well and buckets (as now), a pump on the well, a canal channel, drip (owner, 2026-10-27, §3.1).
- The equipment ladder: hand tools (as now), an ox plough with a pair of bullocks, a tractor; bought, leased a season or hired a day; maintenance, breakdowns, depreciation, resale (§3.2). The combine harvester waits for Phase 3 (ADR 0089).
- Whole-acre orchards of mango, guava, banana or papaya (§3.3).
- The granary (§3.4).

**Out** (and where it goes instead):
- A combine harvester, hired or bought (ADR 0089: harvesting is a free tap in Phase 2, so a hired combine would buy nothing), cold storage, a tube well per acre, solar pumps: Phase 3.
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
| **Pump on the well** | Hamlet | An electric pump and pipes, 15,000, depreciated over 40 seasons | Power 250 a season an acre watered | Floor 40 on up to `wellPumpAcres` 4 of your fields, chosen by the player (it starts on the 2 well acres; any field, by buried pipe: owner, 2026-11-04) | None (no buckets) |
| **Canal channel** | Hamlet | 6,000 an acre (a lined field channel from the minor canal along the river side; an acre must border a channelled acre or the canal) | Water fee 400 a season an acre (the irrigation department), paid at the close | Floor 45 in Monsoon, Winter and Spring; **dry in Summer** (the canal is closed for repairs, as Indian canals are in May–June) | None |
| **Drip** | Village | 40,000 an acre (pipes, drippers, filter), less a 50% government subsidy (PMKSY) for the first 2 acres; depreciates over 30 seasons | Maintenance 1% of cost a season; no power of its own (it runs on the pump's water, and less of it; power 300 a season an acre until Sprint 16's balance step, ADR 0093) | Held at the crop's `moistureMin` all year, never above its `moistureMax` (drip doesn't waterlog); fertiliser's yield bonus +15% (fertigation; +5% until Sprint 16's balance step, ADR 0093) | None; Care demand −0.5 h a day an acre (no weeding between lines) |

- **The minor canal (owner, 2026-11-03):** it runs west to east along the edge between rows 4 and 5, from the map's west edge (a distributary off the hills) to the village lane (column 10), drawn on the tile edge as a thin lined channel with a footpath (no acre taken). Acres sharing that edge border the canal: row 4 (`w3`, `nw1`, `n3`, `n4`, `ne1`) and row 5 (`w4`, `w1`, `home_a`'s 84–86, the yard, `e1`); others reach it through channelled neighbours (`home_b` through `home_a`). Its drawing is previewed to the owner first (ADR 0064).
- **Water sources:** the pump and drip run on pumped water: the well's pump waters `wellPumpAcres` 4 acres, by flooding or by drip (drip needs the pump's pressure); a **borewell** with its own pump (Village, 30,000, 60 seasons) waters `borewellAcres` 4 more (§10 G2, ADR 0086). The canal is its own source along its channels.
- **The borewell (ADR 0086):** drilled on any acre you own (a small pump room at the field's edge; the acre is still farmed). It waters any 4 acres you own, chosen by you, exactly as the well's pump does: moisture 40 or more all year by flooding, no bucket labour, and it can feed drip. Power 250 a season an acre watered, paid at the close; Dr Buildings at the build, depreciated over 60 seasons.
- **One pumped source an acre (owner, 2026-11-03):** an acre is on the pump's list or the borewell's, never both (each chooser offers only acres not on the other), so together they water 8 different acres. A channelled acre may also be pumped: its floor is the higher of the two (pumped water covers the canal's Summer closure) and its canal fee still runs. Drip can be laid only on an acre on the pump's or the borewell's list, and a drip acre can't be taken off its list (`DripNeedsWater`).
- An irrigated acre's water factor rarely falls below 1 except in the Summer canal closure; that steadiness is what the player buys (§9).
- A channel or drip line is a building on the acre: capitalised (Dr Buildings), depreciated (spec 005 §3.3), sold with the acre at book value, seized with bought land.
- **As built (US-127, 2026-11-04):**
  - Running costs (power, the canal's fee, drip's power and 1% upkeep) are paid at each close for the season ahead, for the acres on each list then (an acre added mid-season runs free until the close). Power goes to 5810 (shown as *Power and water*), upkeep to 5820 (*Repairs and upkeep*), both farm costs. Unpaid, that source is off until the next close (the well's buckets take over on the well acres).
  - A channel carries water only while it reaches the canal through channelled fields you own: one cut off by a field sold is dry.
  - Channels, drip and a borewell never stop a field being sold: they go with it at book value (`SellParcel`, foreclosure), and its acres leave the pump's and the borewell's lists.
  - Drip's subsidy is the government's share of the price on your first 2 drip acres; only what you pay is capitalised.
  - Fertigation: with drip running and a fertiliser given, the fertiliser's yield factor rises by 5% (still under the yield cap).
- **On the map (art approved by the owner, 2026-11-04):** the canal along its edge, in place of the bunds and hedges there, ending in a headwall at the village lane; a lined channel on a channelled field's west edge, with a low gate on the field's own edge where it leaves the canal; drip lines under the crop (under a grown crop only the white header and blue filter show); the pump on the well's parapet (no pump house: the yard has no room); a cement outlet at the back corner of each pumped field (the side corners are the tree slots); the borewell as the valley's tube-well room at its field's back corner. The canal is drawn dry (a mud bed, cracks, a last puddle) in its dry season, and a channel dry whenever it carries no water for you (owner, 2026-11-04).

### 3.2 The equipment ladder (§10 G3, G4, G5)

| Machine | Era | Buy | Lease a season | Hire a day | Upkeep | Covers | Effect on the acres it covers |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Hand tools | Start | — | — | — | — | All | Tools 1.00 (as now) |
| **Ox plough** (a pair of bullocks, a plough, a cart) | Hamlet | 70,000 (bullocks 60,000 as livestock, plough and cart 10,000 over 20 seasons) | — | 900 a day with its owner | Bullocks eat as cattle and take 2 places in the second shed (spec 012, ADR 0088); 1 h a day of care | 6 acres | Labour −15% (spec 003 §3.6 `labourPerAcrePerDay`), Tools 1.03 (timely sowing); the cart takes 500 kg to the mandi (no trolley hire for small loads) |
| **Tractor** (35 HP with a cultivator and a trolley) | Village | 6,00,000 (bank loan: the tractor as collateral at 0.70) over 60 seasons | 30,000 a season | 2,000 a day with a driver | Diesel 600 a season an acre covered; maintenance 1.5% of price a season | 20 acres | Labour −35%, Tools 1.06; mandi trips without trolley hire (2,000 kg) |

- **Coverage:** a machine covers its acres in id order (well acres first); the best machine covering an acre applies. A leased machine is a season's rent (Dr Rent, paid upfront at the close). A hired day covers all your acres for that day only (a sowing or a harvest day).
- **As built (ADR 0089):** your fields are owned farm acres that aren't a homestead, a business's yard or the second cattle acre; machines take them best first. A crop keeps the Tools factor of the machine it was sown with. Diesel is paid upfront at each close for the fields a machine works (for the days left when bought or leased mid-season); unpaid, it stands idle until the next close. A lease runs until you give it back (no refund) or can't pay at a close. One machine of each kind. The bullocks are bought and sold only with the plough (`PartOfMachine`), named from their own list, no milk or calves; without them the plough stands. Bought machines are capitalised to 1610 and depreciated to it (its balance is their book value); net worth shows them at book; foreclosure takes them after the animals. Your working cart or tractor-trolley carries a mandi load that fits without the trolley hire.
- **Maintenance** (bought machines): on by default (paid at the close); skipped, it saves the cost and raises the breakdown chance.
- **Breakdowns:** at each daily pass a bought machine breaks with probability `breakdownMaintained` 0.005 or `breakdownSkipped` 0.04 a day (seeded, `equipment` stream); broken, it covers nothing for 24 h and costs 5% of its price to repair (Dr Repairs). Never while away beyond the repair (the 24 h runs on; no losses from it alone).
- **On the map (ADR 0088):** machines are drawn at 70% (the ploughing pair at 75%), at work by day (7:00–19:00) on the growing fields they cover, a field an hour; at night the bullocks rest in their shed and the tractor parks on the farm lane's acre 167. The bullock cart isn't drawn.
- **Depreciation and resale:** straight-line over its life (spec 005 §3.3); sold at `resaleShare` 0.80 of book value (the market value is shown beside the book value, GDD Finance).

### 3.3 Whole-acre orchards (§10 G6, G7)

| Tree | Trees an acre | Saplings | First fruit | Batches | Labour an acre a day |
| --- | --- | --- | --- | --- | --- |
| Mango | 40 | 40 × 350 = 14,000 | about 24 days (spec 003's `growthMinutes`) | Each Summer, whole game | 1.0 young, 2.0 bearing |
| Guava | 100 | 18,000 | about 14 days | Monsoon and Winter, whole game | 1.0, 2.5 |
| Banana | 400 | 24,000 | about 7 days | Every season, 4 harvests then replant | 2.0, 3.0 |
| Papaya | 400 | 24,000 | about 7 days | Every season, 8 harvests | 2.0, 3.0 |

- Planted with `PlantOrchard(acre, tree)` on an owned acre with nothing growing: the acre becomes an orchard (no field crop, no field-edge slots), its trees capitalised (Dr Trees) and counted in net worth at book value, its shade on neighbours as spec 003's field-edge trees.
- **Intercropping while young** (§10 G7): until the first fruit, a short crop (radish, methi, amaranth, lentils, peas; spec 020 G18) can grow between the rows at half the acre's yield and half its labour.
- Fruit per batch = trees × `batchYieldPerTree` (spec 003) × Water × Care × Weather (the same factors as crops, averaged since the last batch; ADR 0090 §5); it waits ripe 72 h then spoils (ADR 0034).
- **A batch picked in parts or sold on the trees** (ADR 0090 §4). A batch (3,000–4,800 kg) is more than the store (2,000 kg) and the trader (100–400 kg a day) can take. `PickOrchard` takes what fits the store and what the trader can still take today; the rest waits on the trees. `SellOrchardOnTrees` sells what's left to a fruit contractor at `contractorShare` 0.7 of the trader's price, at any hour. Picked by the founder, a hand's order on the field, or labourers on a field with no order; only the player sells to the contractor.
- From the Hamlet (ADR 0090 §2); a bearing orchard shades each field beside it as one mature edge tree of its kind (ADR 0090 §1); it goes with its land at book value when the parcel is sold or seized; machines don't work it.
- Removing an orchard (`ClearOrchard`) writes the trees off (Dr Loss) and frees the acre.

### 3.4 The granary (§10 G8)

| Rule | Behaviour |
| --- | --- |
| Building | Your own pucca godown in the mandi town, off the map (as the next village's chakki and the town's halwai): bought for 45,000, 80 seasons, yours at once (an existing godown, not built); one (Hamlet). The yard, the fields, the lane and the village have no room for it without crowding (owner, 2026-11-03); a place on the map may come later. Shown in the Store sheet with its picture |
| Capacity | 10,000 kg of dry goods: wheat, rice, lentils, maize, mustard, atta, seed and saved seed (maize and mustard: spec 020 G18), except seed potato (tubers need cold storage like potato; owner, 2026-11-03). They go to the granary first, then the hut store |
| Spoilage | Goods in the granary spoil at `granarySpoilageFactor` 0.2 of their rate (wheat 0.005 → 0.001 a day) |
| Renting space (owner, 2026-11-03) | Instead of or as well as buying: space in the State Warehousing Corporation's warehouse in the mandi town (off the map, from the Hamlet), booked in whole tonnes, 1–20 t, at `warehouseRentPerKgSeason` 0.30 (30 a quintal a season, about 10 a quintal a month); 10 t is 3,000 a season. Paid upfront at each close like a lease (Dr Rent); booked mid-season, the days left in this season are paid now. More space any time (paid for the days left); less from the next close, never below what is stored there. Dry goods fill your own godown first, then the rented space, then the hut store, fastest-spoiling first; what you use or sell comes out of the hut store first; the same goods and spoilage factor. Buying pays for itself against renting 10 t in about 15 seasons |
| Letting your godown (owner, 2026-11-03) | **Let it out next season** (`LetGodown(on)`): the whole godown, whole seasons, renewed each close until switched off. At each close a grain trader from the town takes it with `godownTenantChance` 0.8 (seeded, `godown` stream) and pays `godownLetRentPerKgSeason` 0.24 × its 10,000 kg = 2,400 upfront (Cr Rent received; under the warehouse's 0.30, so renting space to let your own never pays). While let, your goods fill rented space, then the hut store. If your dry goods (the hut store's too) don't all fit the space you rent for the season that opens, it isn't let that season and the row says why; no tenant, an alert says it is yours to use. Counted as passive income (spec 019 §3.4). About 5% of its cost a season, against about 18% for an acre you farm |
| Not for | Potato, vegetables, fruit, milk (they need cold storage, Phase 3) |
| Books | A building (spec 005); the goods are the same stock, valued as now |
| As built (ADR 0103, owner, 2026-11-12) | There is nothing to press to put goods in or take them out: harvested, bought and saved goods of the six dry kinds (and seed) are in the godown, then the rented space, then the hut, by themselves, and the sheet says so first (*How it works*), lists each good as "x here · y in the hut", and names what stays home. Ending the rent: a status line (*Renews · rent* / *Ends* / *Renews anyway*, from the close's own figures), **Stop renting** and **Keep renting**; no confirm card for a change that costs nothing. Rules and money unchanged |

### 3.5 Processing order

Hourly: moisture (spec 002) with the canal and drip floors after the well's. Daily: breakdowns and repairs (machines in id order), spoilage with the granary's factor. Seasonal (with spec 005's close): water fees, drip power and maintenance, machine maintenance, leases paid upfront, depreciation.

## 4. Data

**New definitions:** `content/farming.json`: `irrigation` (canal: cost an acre, fee, floor, dry seasons; drip: cost, subsidy share and acres, power, maintenance, life, fertigation bonus, labour cut; the pump: cost, life, power, `wellPumpAcres` 4; the borewell: cost, life, power, `borewellAcres` 4), `equipment` (per machine: era, price, life, lease, hire, upkeep, covers, labour cut, tools factor, cart kg, breakdown chances, repair share, `resaleShare`), `orchard` (the era, trees an acre, labour young and bearing, the trees' moisture band, the intercrops and their share, `contractorShare`). `buildings.json`: `granary` (value, life, capacity, the goods it holds, spoilage factor, `warehouseRentPerKgSeason`, the most tonnes to rent, `godownTenantChance`, `godownLetRentPerKgSeason`), `pump`, `borewell`, `canal_channel`, `drip`. `livestock.json`: `bullock` (price, feed, shed places 1 each, no milk).

**New state** (save schema 14 with spec 017; ADR 0083 and ADR 0084 each moved it up one): per tile, irrigation (none, canal, drip); `Machine` (id, kind, owned or leased, bought at, book value, maintained, broken until); hired days (kind, day); `Orchard` on a tile (tree, trees, planted at, batches, intercrop); the granary's goods (a store place, spec 012's mechanism); the warehouse space rented (tonnes) and its goods; the godown let next season (on or off) and let this season.

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `InstallPump`, `SetPumpAcres` | (tiles) | `EraLocked`, `TooManyAcres`, `AlreadyWatered` (on the borewell), `DripNeedsWater`, `InsufficientFunds` | §3.1 |
| `BuildChannel` | tile | `NotOwned`, `NotAdjacent`, `EraLocked`, `InsufficientFunds` | §3.1 |
| `DrillBorewell` | tile | `NotOwned`, `EraLocked`, `AlreadyBuilt`, `InsufficientFunds` | §3.1 (ADR 0086) |
| `SetBorewellAcres` | (tiles) | `TooManyAcres`, `NotOwned`, `AlreadyWatered` (on the pump), `DripNeedsWater` | §3.1 (ADR 0086) |
| `LayDrip` | tile | `NoWaterSource`, `EraLocked`, `InsufficientFunds` | §3.1 |
| `BuyMachine`, `LeaseMachine`, `HireMachine` | kind (+ financing) | `EraLocked`, `NoShedRoom` (bullocks), `InsufficientFunds`, `CreditRefused` | §3.2 |
| `SellMachine`, `SetMaintenance` | machine (, on) | `UnknownMachine` | §3.2 |
| `PlantOrchard`, `ClearOrchard` | tile, tree | `TileOccupied`, `NotOwned`, `NotFarmTile`, `EraLocked`, `UnknownTree`, `NoOrchard`, `InsufficientFunds` | §3.3 |
| `PickOrchard`, `SellOrchardOnTrees` | tile (, overflow) | `NoOrchard`, `NoRipeFruit`, `StoreFull`, `TraderAway`, `TraderFull` | §3.3 (ADR 0090 §4) |
| `Sow` on an orchard | tile, crop | `TreesBearing`, `NotAnIntercrop` | §3.3 (ADR 0090 §3) |
| `BuyGodown` | — | `EraLocked`, `AlreadyBuilt`, `InsufficientFunds` | §3.4 |
| `LetGodown` | on | `NoGodown` | §3.4 |
| `RentWarehouse` | tonnes (0 ends it at the close) | `EraLocked`, `InvalidArgument` (over 20 t), `InsufficientFunds`, `StockTooHigh` (less than what is stored) | §3.4 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `ChannelBuilt`, `DripLaid`, `MachineBought`, `MachineLeased`, `MachineHired`, `MachineSold` | | Motion |
| `MachineBroke`, `MachineRepaired` | machine, cost | Alerts, away card |
| `OrchardPlanted`, `OrchardBearing` | tile, tree | Motion, away card |
| `GodownBought` | | Alerts, the Store |
| `GodownLet`, `GodownNotLet` | rent, or why not (no tenant, your goods in it) | Alerts, away card, the season report |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| A pump on the well | 15,000 | No bucket labour; 4 acres at moisture 40 or more all year | Power each season | Permanent |
| Canal channel on an acre | 6,000 | Steadier water most of the year; a fee each season | No bucket labour | Permanent (sold with the acre) |
| Drip on an acre | 20,000–40,000 | Water at the crop's need all year, +5% with fertiliser | Power and upkeep | Permanent |
| Buy a tractor | 6,00,000 or a loan | Labour −35%, Tools 1.06, no trolley hire | Diesel, maintenance, breakdowns | Sell at 80% of book |
| Lease or hire instead | A season's or a day's charge | The same effect while it lasts | No resale, no breakdown risk | Ends by itself |
| Plant a mango orchard | 14,000 and the acre's crops for 1.5 years | Fruit every Summer, for the whole game | Gluts the mango market if too many | Clear at a loss |
| Buy a godown in town | 45,000 | Grain waits for its dearer season at a fifth of the spoilage (rice and maize from the Monsoon, mustard from Winter; wheat is dearest at its Winter harvest, ADR 0093) | More room in the hut store | Permanent |

## 7. UI

```
Field card (irrigation):  Irrigation: The pump + Canal channel [?]  [Pump on the well · 15,000] [Water from the pump · waters 3 of 4 acres] [Drill a borewell here · 30,000] [Dig a canal channel · 6,000] [Lay drip · 20,000 · half paid by the government]
Menu → Equipment:  Ox plough  [Buy 70,000] [Hire a day 900]
                   Tractor    [Buy 6,00,000] [Lease 30,000 a season] [Hire a day 2,000]
                   Yours: Tractor · book 5,70,000 · market 4,56,000 · maintained [Skip] · covers 8 acres [?]
Field card (empty acre):  [Plant an orchard] → mango 40 trees 14,000 · first fruit in about 24 days [?]
Field card (orchard):     Banana orchard · 400 trees, fruit is ripe · Ripe fruit on the trees 3,177 kg [?] · Fruit waiting spoils in 2 d 13 h [?]
                          You can pick today 1,328 kg [?] · A fruit contractor pays 69,310 coins [?] · Cost, Earned so far, Pays for itself [?]
                          [Pick what can go today · 1,328 kg] [Sell it all on the trees · 69,310 coins] [Clear the orchard]
Store:  Your godown in the mandi town  [Buy · 45,000] → godown: wheat 3,200 kg · rice 1,100 kg (10,000 kg) [?]
```

*Why?* rows: the water floor and fee; labour saved and Tools factor; book versus market value; the orchard's payback; the granary's spoilage saved. Art in Blender (channels, drip lines, bullocks, the tractor, orchard trees in rows, the granary), map layouts previewed to the owner first (ADR 0064).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | A canal acre stays at or above 45 moisture except in Summer; its fee is paid at the close; the pump holds its 4 acres at 40 with no bucket labour | `IrrigationTests.Canal`, `IrrigationTests.Pump` |
| AC-2 | A drip acre holds the crop's band all year; beyond the well's 4 acres it needs a borewell | `IrrigationTests.Drip_Source` |
| AC-3 | A machine cuts labour and raises Tools on the acres it covers, the best one applying | `EquipmentTests.Coverage` |
| AC-4 | Skipped maintenance raises breakdowns; a broken machine covers nothing for 24 h and costs its repair | `EquipmentTests.Breakdowns` |
| AC-5 | A bought machine depreciates straight-line and sells at 80% of book; a lease is a season's rent | `EquipmentTests.Books` |
| AC-6 | An orchard bears by spec 003's rules on every tree; intercrops grow at half until the first fruit | `OrchardTests.Bearing_Intercrop`, `OrchardTests.Place_Clear`, `OrchardTests.Sold_With_The_Land`, `OrchardTests.ChunkingInvariance`, `OrchardViewTests.FieldCard` (ADR 0090) |
| AC-7 | The granary takes dry goods first and spoils them at a fifth of the rate | `GranaryTests.Capacity_Spoilage` |
| AC-8 | Same seed and commands give the same farm, in one chunk or many | `EquipmentTests.ChunkingInvariance` |
| AC-9 | A schema-13 save loads into 14 with rain-fed acres, hand tools and no granary | `MigrationTests.Schema13_Farm` |
| AC-10 | From the Village a borewell on an owned acre holds the 4 acres you choose at moisture 40 or more with no bucket labour, its power paid at the close; a fifth acre is refused (`TooManyAcres`); before the Village it's refused (`EraLocked`) | `IrrigationTests.Borewell` (ADR 0086) |
| AC-11 | Renting books whole tonnes, charges the days left this season and a full season at each close, fills after your own godown and before the hut store, and never shrinks below what is stored | `GranaryTests.Rent` |
| AC-12 | A godown set to let, empty at the close, is let with the seeded chance for 2,400 paid upfront, takes none of your goods while let, isn't let while it holds your goods, and renews until switched off | `GranaryTests.LetOut` |
| AC-13 | A ripe batch is picked as far as the store and the trader take today, the rest waiting on the trees; a fruit contractor buys what's left at `contractorShare` of the trader's price; a batch gone counts as a harvest | `OrchardTests.Bearing_Intercrop`, `OrchardViewTests.FieldCard` (ADR 0090 §4) |
| AC-13 | The ox plough needs 2 free places in the second shed for its bullocks, which come and go with it, give no milk and are fed as cattle; without them it stands; a working cart or tractor-trolley takes a mandi load that fits without the trolley hire (ADR 0088, 0089) | `EquipmentTests.Coverage`, `EquipmentTests.Cart_ToTheMandi` |
| AC-14 | Menu → Equipment shows what each machine does, Buy (the tractor with a loan), Lease and Hire a day with refusals in words, yours with its state, book and resale value, maintenance and Sell; a field shows what works it; machines are drawn at work by day and the tractor parked on the lane at night | `EquipmentViewTests.Sheet`, `EquipmentViewTests.Yours`, `EquipmentViewTests.Map` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| A canal acre's season profit against a rain-fed one | +3–8% (steadier water: outside Summer the rain mostly keeps a loam acre in its crop's band, and the canal is shut in Summer), payback 5–8 seasons (+10–25% and 2–4 until Sprint 16's balance step, ADR 0093) | Measured by the farm bot: +4%, payback 8 |
| Drip's payback with the subsidy | About 12 seasons (3 years) on field crops, faster on thirsty crops (4–8 seasons until Sprint 16's balance step, ADR 0093) | Measured by the farm bot: 11.2 |
| A tractor bought against hired days, for 8 acres | Hiring cheaper below about 10 acres; buying pays over 60 seasons only on a big farm | The lesson |
| A mango orchard against crops on the same acre | Behind for about 6 seasons, ahead within 12 | |
| Rice from the Monsoon to Spring, maize from the Monsoon to Summer, or mustard from Winter to Summer in the granary (wheat until Sprint 16's balance step: it ripens in Winter, its dearest season, ADR 0093) | Earns more than its spoilage and the granary's cost on 5+ tonnes | `GranaryTests.Rice_Waits_Pays`: 5 t of rice +11,104 in Spring against 1,125 of the godown's wear or 3,000 of rent |

## 10. Open questions (all closed with the owner, 2026-10-27)

- **G1. The irrigation ladder.** **Closed (owner, 2026-10-27):** rain-fed; the well and buckets; a pump on the well (Hamlet, 15,000: no bucket labour, 4 acres at 40); a canal channel (Hamlet, 6,000 an acre and a 400 fee a season, dry in Summer); drip (Village, 40,000 an acre, half subsidised on 2 acres, on pumped water) (§3.1).
- **G2. Pumped water.** **Closed (owner, 2026-10-27):** the well's pump waters 4 acres by flooding or drip; a borewell with its own pump (Village, 30,000, 60 seasons) 6 more; the canal any acre along its channels; no water budget or water table in Phase 2 (§3.1). **Amended (owner, 2026-11-03, ADR 0086):** the borewell waters **4** acres you choose (not 6), from any acre you own, by flooding at 40 or by drip, power 250 a season an acre.
- **G3. Which machines.** **Closed (owner, 2026-10-27; the combine moved to Phase 3 by ADR 0089):** an ox plough (Hamlet) to buy or hire, a tractor (Village) to buy, lease or hire, and the combine harvester hired by the acre at harvest; buying a harvester in Phase 3 (§3.2).
- **G4. Bullocks.** **Closed (owner, 2026-10-27):** the pair live in the cattle shed (2 of its 4 places), fed like cattle (green fodder, straw, feed), their dung to compost, 1 h a day of care, no milk; the plough needs the shed (Hamlet, spec 012) (§3.2).
- **G5. Breakdowns.** **Closed (owner, 2026-10-27):** maintenance paid at each close (on by default); a seeded daily chance of 0.5% maintained or 4% skipped; broken, 24 h out and 5% of the price to repair; nothing worse; leased and hired machines never break on you (§3.2).
- **G6. Orchards.** **Closed (owner, 2026-10-27):** whole acres of mango (40 trees), guava (100), banana and papaya (400), real densities and spec 003's tree rules per tree; the balancer checks the glut a large planting causes (§3.3, §9).
- **G7. Intercropping.** **Closed (owner, 2026-10-27):** until the first fruit, a short crop (radish, spinach, coriander, lentils) grows between the rows at half the acre's yield and labour; it stops when the trees bear (§3.3). **Amended (spec 020 G18, 2026-10-31):** radish, methi, amaranth, lentils and peas (spinach and coriander are kitchen-garden crops, not field crops).
- **G8. The granary.** **Closed (owner, 2026-10-27):** one pucca granary on the yard from the Hamlet (45,000), 10,000 kg of grains, pulses, atta and seed, filled before the hut store, at a fifth of the spoilage; potato, vegetables, fruit and milk need cold storage (Phase 3); its place previewed to the owner (§3.4). **Amended (owner, 2026-11-03):** seed potato left out of it; the yard, a field corner, the lane and the village were tried on the map and all crowd it, so it is your own godown in the mandi town, off the map.

## 11. Cut first

1. Breakdowns (machines never break).
2. Leasing (buy or hire only).
3. Intercropping.
4. The borewell (drip on the well's 4 acres only).
