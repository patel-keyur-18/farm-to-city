# 012. Cattle and fodder

- Status: Approved (owner, 2026-10-22). All gaps closed 2026-10-22 (ADR 0060)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land, farming and livestock (Livestock; Crops; Storage and selling); Assets and wealth (Livestock); Offline play (notifications)
- Amends: spec 002 (the yard: the shed, the straw stack and the compost pit); spec 003 §3.1–3.3 (fodder crops are cut, not harvested; straw from the wheat and rice harvests; Care demand from animals), §3.11 (milk as home use); spec 004 §3.5 (goods kept in their own place: straw stack, feed room, compost pit); spec 005 §3.1 (accounts 1750, 4020, 4030, 5020, 5030), §3.4 (report lines), §3.5 (net worth), §3.6 (a dairy loan), §3.9 (foreclosure seizes animals); spec 006 §3.8 (an "animals" order); spec 007 §3.5 (notification kinds), save schema 7; spec 008 (the shed sheet); spec 009 (letter chapters)
- Related ADRs: 0056 (buffalo and cows), 0002 (the clocks, the 72-hour away cap), 0008 (per-season realism), 0009 (never stuck), 0028 and 0038 (loans secured by what they buy; selling collateral repays the bank first), 0032 (home use), 0034 (inputs in store are an asset; every coin amount follows the price index), 0040 (the yard), 0051 (compost), 0053 (the grazing commons)
- Depends on specs: 001 (clock and passes), 003 (crops, Care, the household), 004 (store, input prices), 005 (ledger, loans), 006 (hands and orders), 007 (saves, notifications), 010 (the commons)

## 1. Goal

The player buys a buffalo or a cow, keeps her fed from their own land, and the village dairy co-op pays them every morning, so the farm earns steady cash between harvests.

**Lesson:** an animal is a working asset: it costs money up front, earns every day, and pays back fastest when its feed comes from your own land, which costs land and labour instead of cash. A cheaper animal can earn more per coin; a dearer one earns more per stall.

## 2. Scope

**In:**
- Two animals: Murrah buffalo and crossbred cow, bought and sold at the village cattle fair.
- One cattle shed for 4 animals in the yard, with a feed room and a straw stack. A compost pit in the yard.
- Daily feeding at 6:00, also while away: green fodder, straw, cattle feed; the grazing commons.
- Green fodder crops (berseem, sorghum), cut daily and never stored.
- Straw from wheat and rice harvests.
- Milk to the dairy co-op at 6:00, paid by fat; milk kept for the household.
- The lactation cycle: in milk, dry, a calf (sold at the fair).
- Health: hunger, a seeded disease, the vet, a vaccination each season. No animal dies.
- Dung to compost.
- 1.5 labour hours a day per animal from the Care pool; the hand's "animals" order.
- The dairy loan; animals on the balance sheet; foreclosure.
- The shed sheet with a *Why?* on every number, notifications, the away card, letter chapters, the map.

**Out** (and where it goes instead):
- Goats and poultry: later (ADR 0056). No pigs.
- Silage and chaff cutters: Phase 3 with the equipment ladder (spec 013 / F13.2).
- Raising calves into heifers, growing the herd from your own calves, breed improvement: later (Phase 3).
- Ageing and culling of animals: later. Animals don't age in Phase 2.
- Bullocks for ploughing: the equipment ladder (spec 013).
- A dairy business (processing milk, a milk round): spec 016 (open conflict noted in `STATUS.md`).
- More than one shed: Phase 3.

## 3. Rules and formulas

Money is `long` in 1/100 coin, quantities in grams (milk in millilitres), factors `Fixed` (ADR 0018), rounding as spec 001 §3.1. Every coin amount follows the price index (ADR 0034).

### 3.1 The time scale (§10 G1)

A season is 4 game days and stands for a real quarter (ADR 0008), so **one game day stands for `LivestockDef.realDaysPerGameDay` (22.8) real days** of an animal's life (365.25 ÷ 16). Content gives an animal's amounts **per real day**, as a farmer would say them ("8 L a day"); the sim uses them per game day:

```
perGameDay(x) = x × realDaysPerGameDay
```

The shed sheet shows game-day amounts with a *Why?*: "8 L a day × 23 days in a game day".

ADR 0056's per-game-day figures used 30 real days a game day; at 22.8 every amount is 24% smaller and the ratios (milk to feed, feed to price) are unchanged.

### 3.2 The animals (ADR 0056)

| Field | Buffalo (Murrah) | Cow (crossbred) | Notes |
| --- | --- | --- | --- |
| Price at the fair | 1,00,000 | 70,000 | × PriceIndex |
| Milk, mean over a lactation | 8 L a real day (182 L a game day) | 11 L (251 L) | |
| Fat | 7.0% | 4.0% | Sets the co-op's price (§3.5) |
| Season milk factor | 1.00 all year | Summer 0.85, else 1.00 | Crossbred cows suffer in the heat |
| In milk | 13 game days (about 300 real days) | 13 | |
| Dry | 5 game days (about 115 real days) | 3 (about 70) | Then a calf, and milk again |
| Ration in milk, per real day | green 27 kg, straw 6 kg, feed 5 kg | green 25, straw 5, feed 4.5 | |
| Ration dry | green and straw as above, feed × `dryFeedFactor` (0.4) | | |
| Labour | 1.5 hours a game day | 1.5 | Joins the Care demand (§3.9) |
| Dung to compost | 9.5 kg a real day (about 3.5 t a year) | 9.5 | §3.8 |
| Calf at the fair | 5,000 | 4,000 | §3.6 |

**Lactation curve.** Milk on day d of a lactation is `mean × LactationCurve(d)`, a piecewise-linear curve in data: `[[0, 1.10], [3, 1.20], [13, 0.66]]` (rises to a peak about two real months after calving, then falls). Its mean over 13 days is 1.00 within 1%, checked by a content test.

**Names.** Each animal gets a seeded name from `LivestockDef.names` (Lakshmi, Gauri, Kamdhenu, Nandini, Ganga, Radha, Sundari, Kali, …; stream Livestock, counter = animal id). The player can't rename in Phase 2.

### 3.3 The shed, the feed room, the straw stack and the compost pit

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| The shed | `BuildShed()` builds the cattle shed on the yard's south acre (tile 103), beside the house. `BuildingDef(cattle_shed).value` 40,000 (a brick floor, a tin roof, a manger and a feed room; the family builds it), capitalised to 1600 Buildings, a construction job of `buildHours` 24, depreciated over `usefulLifeSeasons` 60. Emits `ShedBuilt` | One shed in Phase 2 (`AlreadyBuilt`); `InsufficientFunds` |
| Room | `BuildingDef.animals` (4) animals | |
| Feed room | Part of the shed: holds cattle feed up to `feedRoomKg` 2,400 kg (4 game days for 4 buffalo in milk) | |
| Straw stack | Beside the shed, built with it: straw up to `strawStackKg` 10,000 kg (about 18 game days for 4 buffalo) | |
| Compost pit | Part of the inherited yard from the start (every farm has one): compost up to `BuildingDef(hut).compostPitKg` 8,000 kg | |
| Where goods live | A good with `GoodDef.place` (`feedRoom`, `strawStack`, `compostPit`) is stocked in that place and counts against its capacity, not the hut store's (§10 G5). Buying, average cost (1200 Inputs in store), spoilage and net worth work as for any input (spec 004 §3.4–3.5). `Buy` beyond a place's room is rejected with `PlaceFull`; buying cattle feed or straw needs the shed (`NoShed`) | |
| Protected | The shed stands on the inherited yard, so it is never seized or used as collateral (ADR 0009) | |

### 3.4 Buying and selling animals

| Rule | Behaviour |
| --- | --- |
| Buy | `BuyAnimal(kind, financing?)` at the village cattle fair, at `AnimalDef.price × PriceIndex`, capitalised: Dr 1750 Livestock, Cr Cash (or the loan, §3.12). Needs a finished shed with room (`NoShed`, `ShedFull`). She walks in at once, **just calved** (day 0 of her lactation; her calf stays with the seller), vaccinated for this season, health 1.0, fed for today. Emits `AnimalBought` |
| Sell | `SellAnimal(animalId)` at the fair for `price × PriceIndex × AnimalDef.sellShare` (0.80) × her health: Dr Cash, Cr 1750 at book value, the difference to 4800 or 5600 (spec 005 §3.3). If the bank's loan is secured on her, the bank is repaid first (ADR 0038, as `SellParcel`); `LoanSecured` if the proceeds can't cover it. Emits `AnimalSold` |
| Book value | Cost, not depreciated in Phase 2 (animals don't age; as trees, spec 005 §3.3) |

### 3.5 Milk and the dairy co-op

At the hourly pass at `LivestockDef.feedHour` (6:00), system Agriculture, for each animal in id order, the milk since the last collection:

```
litres = perGameDay(AnimalDef.milkPerDay) × LactationCurve(day) × SeasonMilk(kind, season)
         × FedFactor × health × careFactor × (sick ? 0 : 1) × owned ÷ 86,400        (0 while dry)
FedFactor  = max(LivestockDef.minMilkFactor, fedScore)                               (fedScore from this morning's feeding a day ago, §3.7)
careFactor = min(1, the day's time-weighted Care ratio)                              (§3.9)
```

`owned` is the seconds since the last collection or since she was bought. Each factor is rounded once (Fixed); litres are rounded to millilitres.

| Rule | Behaviour |
| --- | --- |
| The co-op's price | `pricePerLitre = (MilkDef.basePerLitre + MilkDef.perFatPoint × fat%) × PriceIndex`: buffalo (12 + 6.5 × 7.0) = 57.50, cow (12 + 6.5 × 4.0) = 38.00. It pays by fat, as Indian co-ops do (§10 G6) |
| The household's milk | While `eatFromStore` is on, the household keeps `LivestockDef.householdLitresPerDay` (1.5 L a real day, 34 L a game day), buffalo milk first, valued at the co-op's price, as home use: Dr Cash, Cr 4020 Milk sales (ADR 0032: drinking it or selling it gives the same farm profit). It is part of the day's food allowance (spec 003 §3.11): the next daily pass eats from the store only what's left of the allowance after the milk. The household never takes more than the allowance (§10 G6) |
| The co-op | Buys the rest at once: Dr Cash, Cr 4020 Milk sales. Milk is never stored. Emits `MilkCollected(litres, amount, homeLitres)` |
| Away | The same, live or away. The away card: "The co-op paid 41,950 for 728 L of milk" |

Example (one buffalo, day 3 of lactation, fully fed, healthy, Care 1.0): 182.4 L × 1.20 = 218.9 L; the household keeps 34.2 L (1,966.50 of groceries), the co-op buys 184.68 L for 10,619.10.

### 3.6 The lactation cycle

| Rule | Behaviour |
| --- | --- |
| Days | At each 6:00 pass, after milking, each animal's `stageDay` grows by 1 |
| Dry | After `milkDays` she goes dry (no milk, feed × `dryFeedFactor`). Emits `AnimalDry`. *Why?*: "Dry before her next calf: 5 days" |
| Calving | After `dryDays` she calves: day 0 of a new lactation. Her calf is sold at the fair at once for `calfPrice × PriceIndex`: Dr Cash, Cr 4030 Livestock sales. Emits `CalfBorn(animalId, amount)` |
| Breeding | Implied: the co-op's technician serves her in time; its cost is inside the vet cost per season (§3.10) |

### 3.7 Feeding (every morning, live or away)

At the 6:00 pass, after milking and the day count, each animal in id order is fed for the day ahead.

**Need per game day:** `perGameDay(ration)` for green, straw and feed (feed × `dryFeedFactor` while dry).

**Green fodder** comes in this order:
1. **Grazing on the commons** covers `LivestockDef.commonsShare[season]` of every animal's green need: Spring 0.10, Summer 0.05, Monsoon 0.40, Winter 0.10 (§10 G3). Free.
2. **Your fodder fields' cut** (§3.8b), shared across animals in id order.
3. **Bought from neighbours** if `buyGreenFodder` is on (default on): the rest at `GoodDef(green_fodder).basePrice` (3 coins a kg) × PriceIndex, Dr 5020 Animal feed, Cr Cash, as far as cash allows. Delivered cut; never stored.

What the animals don't eat of the day's cut is sold standing to neighbours, who cut and carry it themselves, at `GoodDef(green_fodder).sellPrice` (1 coin a kg) × PriceIndex: Dr Cash, Cr 4000 Crop sales ("Neighbours bought 900 kg of berseem you didn't need: 900"). With no animals the whole cut is sold this way. Real farmers sell spare green fodder cheaply, so a fodder acre is never wasted, but it pays far less as a cash crop than wheat or potato (about 16,000 a season gross against wheat's 30,000), so it only pays to grow when it feeds your own animals.

**Straw** comes from the straw stack and **feed** from the feed room, expensed at average cost (Dr 5020, Cr 1200; home-grown straw has no cost).

**The fed score:**

```
fedScore = Σ weight(f) × min(1, given(f) ÷ need(f))        weights: green 0.4, straw 0.2, feed 0.4
```

A dry animal's feed weight still counts (her ration is smaller). The score sets tomorrow's milk (§3.5).

**Hunger and health:**

| Rule | Behaviour | Default |
| --- | --- | --- |
| A hungry day | `fedScore < LivestockDef.hungryBelow` | 0.70 |
| Health falls | From the `hungerGraceDays`-th hungry day in a row, health −`hungerHealthLoss` a day | 2 days; −0.10 |
| Health recovers | A fed day: +`healthRecoveryPerDay` | +0.05 |
| Bounds | Health stays in `[minHealth, 1]`; it lowers milk and raises disease risk (§3.10) | 0.50 |
| Never dies | No animal dies of hunger or illness in Phase 2, and none is lost while away (ADR 0056; §10 G9) | |

**Feed running low.** After feeding, and at `BuyAnimal`, if the straw or the feed would last less than `LivestockDef.lowDays` (2) more game days at today's need, emits `FeedLow(feed, daysLeft)` once until that place is refilled above it. The shed sheet and the HUD alerts show a red "!" on that feed while it's low, and the buy card shows the days of straw and feed in stock with **Fill** buttons, so a first animal never arrives to an empty feed room unnoticed. One hungry morning does no harm (`hungerGraceDays` 2), so filling after she arrives is in time.

### 3.8 Dung to compost; straw from the harvest

| Rule | Behaviour |
| --- | --- |
| Dung | At each 6:00 pass every animal adds `perGameDay(dungCompostKgPerDay)` × owned ÷ 86,400 of compost to the compost pit at no cost (the costs were the feed). What doesn't fit is spread on the yard and lost. Sowing with compost takes from the pit first (spec 003 §3.2) |
| Straw | A `Harvest` of a crop with `CropDef.strawPerKg` (wheat 1.5, rice 1.2) adds `round(grams × strawPerKg)` of straw to the straw stack, by anyone (the player, a hand, labourers). No shed, or a full stack: the rest is left on the field and ploughed in, with no effect (§10 G4) |

### 3.8b Green fodder crops (§10 G3)

Two new `CropDef`s with a `fodder` block. They are sown with the sow sheet like any crop (seed, fertiliser, the Soil factor at sowing, Care) but they are **cut daily, not harvested**.

| Field | Berseem | Sorghum (multi-cut) |
| --- | --- | --- |
| Sown in | Winter | Summer |
| Cut in | Winter, Spring | Summer, Monsoon |
| First cut after | 6 h (`growthMinutes` 360) | 6 h |
| Green fodder an acre a season | 16,000 kg | 16,000 kg |
| Seed an acre (sown once) | 10 kg (180 coins a kg) | 15 kg (90) |
| Moisture band | 45–80 | 30–70 |
| Labour an acre a day | 3.0 h (cutting, chaffing, carrying) | 3.0 |
| Fertility a season | −3 (a legume: it feeds the soil) | +5 |

| Rule | Behaviour |
| --- | --- |
| Cutting | At each 6:00 pass after the first cut is due, a fodder field gives `seasonYieldPerAcre × elapsed ÷ S × Soil × Water × Care × Weather × Fertiliser` of green fodder, the factors as the day's time-weighted means (spec 003 §3.4–3.7), capped at `yieldCap` × base. Fodder has no pest check (fodder is cut long before pests build up) |
| How long | The field stays sown until the end of its last cut season; then it is cleared (`FodderEnded`). `ClearField` ends it early. Seed is paid once at sowing (a fodder field is sown once). Fertiliser is paid at sowing for the time it will stand; its fertility change and the crop's are applied pro rata to the time it stood, at each season close and at its end |
| Never stored | The cut goes straight to the animals (§3.7). It isn't a good in the store; what they don't eat is sold standing to neighbours that morning (§3.7) |
| Orders | A hand's field order can keep fodder ("KEEP green fodder ON field 28"): he sows berseem in Winter and sorghum in Summer, whichever is in season when the field is empty, so berseem (Winter–Spring) then sorghum (Summer–Monsoon) feed the animals all year; if neither can be sown now he sows his best pick as any order does (ADR 0048), so the field never stands idle. A hand never picks fodder by himself in other orders (`BestCrop` skips fodder: its value is the bought fodder it saves, which the sow sheet shows) |
| The sow sheet | A fodder card shows "Feeds about 6 buffalo · saves about 1,850 coins a day of bought fodder" with a *Why?* |

### 3.9 Labour

| Rule | Behaviour |
| --- | --- |
| Demand | Each animal adds `AnimalDef.labourHoursPerDay` (1.5) to the Care demand (spec 003 §3.6), dry or in milk. Fodder fields add their crop's labour like any growing crop |
| Effect | The day's time-weighted Care ratio, capped at 1, multiplies the milk (§3.5) |
| Example | 4 buffalo need 6 hours a day: most of a skill-50 hand's 7.6 |

### 3.10 Health: vaccination, disease and the vet (§10 G7)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Vaccination | At each season start (seasonal pass, after spec 005's settlement), each animal is vaccinated by the co-op's vet for `vaccineCost` × PriceIndex: Dr 5030 Vet and breeding, Cr Cash. If cash is short the animal goes unvaccinated this season (`VaccinationMissed`); nothing is owed. A bought animal is already vaccinated | 300 a season (vaccines, deworming and the technician's service) |
| Disease | At each 6:00 pass, a seeded draw per animal (stream Livestock, counter = day index × 8 + animal slot) below `diseaseChance × (vaccinated ? 1 : unvaccinatedFactor) × (1 + sickHealthFactor × (1 − health))` makes her sick today | 0.01 a day; × 3; 2 |
| Sick | The vet is called at once for `vetVisitCost` × PriceIndex (Dr 5030, Cr Cash); her milk for the coming day is lost (treatment, milk thrown away); health −`sickHealthLoss`. If cash is short no vet comes: she is sick for 2 days and loses 2 × `sickHealthLoss`; nothing is owed (ADR 0009). Emits `AnimalSick(animalId, cost, vetCame)` | 1,500; −0.10 |
| Never dies | Disease never kills an animal in Phase 2 | |

### 3.11 The hand's "animals" order (amends spec 006 §3.8)

```
animals-order := LOOK AFTER the shed [KEEP straw and feed for N days]
```

| Rule | Behaviour |
| --- | --- |
| Set | `SetStandingOrder(handId, AnimalsOrder(days))`, one per farm; `days` 2–4 (default 3). Uses one of the hand's order slots. `NoShed` without a shed |
| What he does | At the 6:00 pass, after feeding: if the straw or the feed would last less than `days` game days, he buys to fill that place (straw from neighbours, feed from the dealer, at today's price), as far as cash allows (`OrderWaiting(InsufficientFunds)` otherwise). Emits `OrderExecuted(Bought)`. He doesn't buy animals or sell them |
| His hours | Unchanged: they go into the Care pool, which looks after every animal and field (§3.9) |

### 3.12 Money: accounts, the dairy loan, net worth and foreclosure (§10 G8)

| Rule | Behaviour |
| --- | --- |
| Accounts (spec 005 §3.1) | New: **1750 Livestock** (asset); **4020 Milk sales**, **4030 Livestock sales** (calves) (income); **5020 Animal feed**, **5030 Vet and breeding** (expenses) |
| Report lines (spec 005 §3.4) | Farm income adds 4020 and 4030; farm costs add 5020 and 5030. The season report's *Why?* splits them out ("Your animals: milk 1,32,400, calves 5,000, feed −58,200, vet −1,800") |
| Net worth (spec 005 §3.5) | Adds Σ livestock at book value. Straw, feed and compost in their places count as inputs in store |
| The dairy loan | `BuyAnimal` takes an optional `Financing` (as `BuyParcel`, ADR 0028): a bank loan where the animal counts as collateral for that limit at `LenderDef.livestockLtv` (0.75: the bank wants a quarter down, as dairy loans do). Animals already owned count as bank collateral at the same rate |
| Foreclosure (spec 005 §3.9) | Seizes animals after bought parcels and before produce, in id order, each at its sell price (§3.4). The shed and the compost pit are protected with the yard |

### 3.13 When cattle unlock (§10 G2)

The shed can be built from the **Hamlet** era (spec 009's `EraUnlocked(hamlet)`). The `hamlet` letter adds a line, and a new chapter `cattle` opens with the first visit to the yard after the Hamlet: "A buffalo in the yard pays every morning. Build a shed, and grow her green fodder…" (action: the shed sheet). Before the Hamlet, tapping the yard's south acre shows "A cattle shed can go here once you're a Hamlet", and the Becoming a Hamlet progress sheet lists "A cattle shed: buffalo and cows" among its rewards.

### 3.14 Notifications (amends spec 007 §3.5)

| Kind | Importance | When | Text |
| --- | --- | --- | --- |
| `FeedLow` | Needs you | `FeedLow` at 6:00 (quiet hours move it to 07:00) | "Straw for 2 more days. Fill the stack, or give {hand} the animals order" (without a hand: "Fill the stack from the shed") |
| `AnimalSick` | Information | `AnimalSick` | "Lakshmi was ill. The vet came; she's resting" |
| `CalfBorn` | Information | `CalfBorn` | "Gauri had a calf, sold at the fair" |

### 3.15 Processing order

At the 6:00 hourly pass, system Agriculture, before the Market system's truck and orders: (1) milk collection and payment, animals in id order; (2) dung to the pit; (3) stage days, dry, calving; (4) disease draws; (5) fodder fields cut, tiles in id order; (6) feeding, animals in id order, then hunger and health; (7) the hands' animals orders; (8) `FeedLow`. At the seasonal pass, after spec 005's settlement: vaccinations. Animals are kept in id order in state; nothing iterates a dictionary. Livestock state is appended to `StateHash` after the people section.

## 4. Data

**New or changed definitions** (`content/livestock.json` unless noted):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `LivestockDef` | `realDaysPerGameDay` | Fixed | 22.8 | > 0 | §3.1 |
| | `feedHour` | int | 6 | 0–23 | |
| | `householdLitresPerDay` | Fixed (L) | 1.5 | ≥ 0 | Per real day |
| | `commonsShare` | Fixed per season | 0.10, 0.05, 0.40, 0.10 | 0–1 | |
| | `minMilkFactor`, `hungryBelow` | Fixed | 0.30, 0.70 | 0–1 | |
| | `hungerGraceDays`, `hungerHealthLoss`, `healthRecoveryPerDay`, `minHealth` | int, Fixed | 2, 0.10, 0.05, 0.50 | | |
| | `feedWeights` | Fixed × 3 | green 0.4, straw 0.2, feed 0.4 | sum 1 | |
| | `lowDays` | int | 2 | ≥ 1 | |
| | `diseaseChance`, `unvaccinatedFactor`, `sickHealthFactor`, `sickHealthLoss` | Fixed | 0.01, 3, 2, 0.10 | | |
| | `vaccineCost`, `vetVisitCost` | money | 300, 1,500 | ≥ 0 | × PriceIndex |
| | `lactationCurve` | [[day, factor]] | §3.2 | mean 1.00 ± 1% | |
| | `names` | string[] | 16 names | ≥ 8 | |
| `AnimalDef` | `id`, `nameKey`, `price`, `milkPerDay`, `fatPercent`, `seasonMilk`, `milkDays`, `dryDays`, `ration` (green, straw, feed per real day), `dryFeedFactor`, `labourHoursPerDay`, `dungCompostKgPerDay`, `calfPrice`, `sellShare` | | §3.2 | | buffalo, cow |
| `MilkDef` | `basePerLitre`, `perFatPoint` | money | 12, 6.5 | ≥ 0 | |
| `BuildingDef` (`buildings.json`) | `cattle_shed`: `value` 40,000, `buildHours` 24, `usefulLifeSeasons` 60, `animals` 4, `feedRoomKg` 2,400, `strawStackKg` 10,000 | | | | |
| | `hut.compostPitKg` | kg | 8,000 | | |
| `GoodDef` (`goods.json`) | new `cattle_feed` (input, 28 a kg, place `feedRoom`), `straw` (input, 6 a kg, place `strawStack`), (green fodder is never stocked, so its prices are `LivestockDef.greenFodderBuyPrice` 3 and `greenFodderSellPrice` 1 in `livestock.json`, implementation note US-199), `seed_berseem` (input, 180 a kg), `seed_sorghum` (input, 90 a kg); `compost` gets place `compostPit`; new field `place` | | | | |
| `CropDef` (`crops.json`) | `strawPerKg` (wheat 1.5, rice 1.2); new crops `berseem`, `sorghum` with a `fodder` block (`cutSeasons`) | | §3.8b | | |
| `LenderDef` | `livestockLtv` | Fixed | bank 0.75, moneylender 0 | 0–1 | |
| `NotifyDef` | kinds | | `FeedLow`, `AnimalSick`, `CalfBorn` | | |

**New or changed state** (save schema 7; a schema-6 save migrates with no shed, no animals, `buyGreenFodder` on, and its compost moved from the hut store's count to the pit's: the same stock, counted in a new place):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `LivestockState` | `animals` | list, id order | `NextAnimalId` |
| | `shedTile`, `shedReady` | int, bool | The building itself is spec 002's building list |
| | `buyGreenFodder` | bool | |
| | `lowWarned` | bool × 2 | Straw, feed |
| | `lastCollection` | long (s) | |
| `Animal` | `id`, `kind`, `name`, `boughtAt`, `cost`, `stage` (Milking, Dry), `stageDay`, `health`, `fedScore`, `hungryDays`, `vaccinatedSeason`, `sickToday`, `collateralLoanId` | | |
| `FieldCrop` | `fodder` fields: `lastCut`, the day's accruals | | Spec 003's crop state, reused |

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `BuildShed` | — | `EraLocked` (before the Hamlet), `AlreadyBuilt`, `InsufficientFunds` | §3.3 |
| `BuyAnimal` | `kind`, `financing?` | `EraLocked`, `NoShed`, `ShedFull`, `UnknownAnimal`, `InsufficientFunds`, the loan's own reasons | §3.4 |
| `SellAnimal` | `animalId` | `UnknownAnimal`, `LoanSecured` | §3.4 |
| `Buy` (changed) | as spec 004 | adds `PlaceFull`, `NoShed` | Goods with a place go there |
| `SetBuyGreenFodder` | `on` | — | §3.7 |
| `SetStandingOrder` (changed) | adds `AnimalsOrder(days)` | `NoShed`, `TooManyOrders` | §3.11 |
| `Sow` (changed) | berseem and sorghum | as spec 003 | §3.8b |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `ShedBuilt` | — | The build job ends | Map, notifications (as `HutBuilt`) |
| `AnimalBought`, `AnimalSold` | `animalId`, `kind`, `amount` | Commands | Map, HUD, play log |
| `MilkCollected` | `litres`, `amount`, `homeLitres` | 6:00 | HUD (coins fly), away card |
| `AnimalDry`, `CalfBorn` | `animalId`, (`amount`) | §3.6 | Away card, notifications |
| `AnimalSick` | `animalId`, `cost`, `vetCame` | §3.10 | Away card, notifications |
| `VaccinationMissed` | `animalId` | Season start, cash short | Away card, alerts |
| `FeedLow` | `feed`, `daysLeft` | §3.7 | Notifications, HUD alert |
| `AnimalsFed` | `green` (own, commons, bought), `straw`, `feed`, `boughtAmount` | 6:00 | Away card |
| `FodderEnded` | `tile` | §3.8b | Map |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Build the shed | 40,000 | Room for 4 animals, a feed room, a straw stack | Depreciation | No (protected, never sold) |
| Buy a buffalo or a cow | 1,00,000 or 70,000, or a dairy loan | About 10,000 a day of milk while in milk; a calf a year | 1.5 h a day of labour; feed every day | Sell at 80% |
| Grow fodder on an acre | Seed; the acre isn't growing a cash crop | Saves about 1,850 coins a day of bought green fodder per buffalo it feeds; what they don't eat sells standing at 1 a kg | Labour 3 h a day; berseem feeds the soil | Clear the field |
| Keep the straw | — | Free dry fodder | The stack's room | — |
| Fill the feed room | Cash now | Milk stays up while away | An asset in store | Use it |
| Give the hand the animals order | An order slot | Straw and feed never run out (if cash allows) | Cash leaves automatically | Remove the order |

## 7. UI

```
The yard: the shed (4 stalls, animals visible), the straw stack, the compost pit; tapping any opens the shed sheet.

Shed sheet                                                            [×]
+---------------------------------------------------------------------+
| Your cattle shed                                    3 of 4 stalls   |
| Milk today   546 L · co-op paid 28,400 at 6:00               [?]    |
| ANIMALS                                                             |
| [buffalo] Lakshmi  In milk, day 4 of 13 · 219 L today  ❤ 100% [?]   |
| [cow]     Gauri    Dry, calf in 2 days                 ❤ 90%  [?]   |
| [buffalo] Ganga    In milk, day 9 · 160 L · ill today  ❤ 80%  [?]   |
|                                         [Buy a buffalo 1,00,000]    |
|                                         [Buy a cow 70,000]          |
| FEED (every morning at 6:00)                                        |
| Green fodder  1,700 kg a day: commons 170, berseem 1,200,           |
|               bought 330 (990 coins)                         [?]    |
|               [✓] Buy from neighbours when short                    |
| Straw stack   4.2 t of 10 t · 11 days                 [Fill]  [?]   |
| Feed room     0.9 t of 2.4 t · 3 days                 [Fill]  [?]   |
| Compost pit   2.1 t of 8 t                                    [?]   |
| Work          4.5 h a day of your Care pool                  [?]    |
+---------------------------------------------------------------------+
```

- **An animal's row** opens her card: milk today and the formula (mean × curve × fed × health × care), fed score by feed, health and why it changed, vaccinated this season, what she cost, what she has earned, her sell price [Sell at the fair], and the payback.
- *Why?* on every number: milk ("8 L a day × 23 days × 1.20 peak × fed 100% × health 100% × Care 100%"), the co-op's price ("12 + 6.50 × 7.0% fat"), days left, the green split, the bought cost, the labour, compost.
- **Buy** opens a card per animal: price, milk, fat and price per litre, ration and its cost bought vs home-grown, labour, the dairy loan option (as the land sheet's loan), and payback (bought feed, home-grown).
- **Fill** buys up to the place's room at today's price (a number picker as Buy inputs).
- **The map:** buffalo and cows in their stalls (grazing on the commons in the day, ADR art style), the straw stack's height following its stock, the berseem and sorghum fields green, a milk can by the lane at 6:00 (cut first).
- **The away card** gets the milk, feed bought, calves and vet visits.
- **The Care *Why?*** (spec 008) gets a row for the animals ("4 animals: 6.0 h a day"), so the player sees why Care fell after buying one.
- **A fodder field's sheet** shows "Cut every morning for your animals: 3,200 kg today · 2,400 eaten, 800 sold to neighbours", the factors' *Why?*, when it ends ("Cut till the end of Spring"), and Clear. No ready marker: the map shows it green and growing.
- **Buy inputs** lists cattle feed and straw (with the shed), and says where each goes ("to the feed room", "to the straw stack", "to the compost pit"); the Store sheet's Seed and inputs section stops listing compost, which the shed sheet and the compost pit show.
- **Net worth's *Why?*** gets a Livestock row; Money gets the dairy loan like any bank loan.
- **The HUD** alerts (!) when feed is low.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Before the Hamlet `BuildShed` is rejected with `EraLocked`; after it the shed builds in 24 h for 40,000 capitalised to 1600; a second is `AlreadyBuilt` | `LivestockTests.Shed_BuildAndLimits` |
| AC-2 | `BuyAnimal` needs a finished shed with room; she costs price × index (Dr 1750), arrives in milk on day 0, vaccinated, health 1; the fifth is `ShedFull` | `LivestockTests.BuyAnimal` |
| AC-3 | A healthy, fully fed buffalo with Care ≥ 1 gives 182.4 L × curve(d) a game day; the household keeps 34.2 L as home use within the allowance; the co-op pays the rest at 57.50 × index; both post to 4020 and the ledger balances | `LivestockTests.Milk_CoopAndHome` |
| AC-4 | A cow's Summer milk is × 0.85; a dry animal gives none; after 13 days in milk she is dry and after the dry days she calves, the calf sold for calfPrice (4030) | `LivestockTests.Lactation_Cycle` |
| AC-5 | Feeding takes green from the commons share, then the fodder fields, then buys the rest (if on and cash allows); straw from the stack and feed from the room at average cost (5020); the fed score follows the weights; with buying off and no fodder the green weight scores 0 | `LivestockTests.Feeding_Order` |
| AC-6 | Two hungry days in a row lower health 0.10 a day from the second; fed days recover 0.05; health never goes below 0.50; no animal is ever removed by hunger or illness | `LivestockTests.Hunger_NeverDies` |
| AC-7 | `FeedLow` is emitted once when straw or feed falls under 2 days and again only after a refill; the planner makes a Needs-you notification moved out of quiet hours | `LivestockTests.FeedLow`, `NotificationTests.Plan_FeedLow` |
| AC-8 | A berseem acre sown in Winter is cut daily from 6 h after sowing through Spring and ends at the Spring close; the cut follows Soil × Water × Care × Weather × Fertiliser; it is never in the store; what the animals don't eat (all of it with no animals) is sold at 1 × index (4000); a fodder order sows berseem in Winter and sorghum in Summer; a hand never picks fodder in other orders | `FodderTests.Berseem_CutDaily`, `FodderTests.FodderOrder_AllYear` |
| AC-9 | Harvesting 100 kg of wheat adds 150 kg of straw to the stack (by player, hand or labourers); with no shed or a full stack the rest is lost | `LivestockTests.Straw_FromHarvest` |
| AC-10 | Dung adds 216.6 kg of compost a game day per animal to the pit; sowing with compost uses the pit first; compost in the pit doesn't count against the hut store | `LivestockTests.Dung_ToCompostPit` |
| AC-11 | Each animal adds 1.5 h to the Care demand; with Care 0.5 the milk is halved | `LivestockTests.Labour_Care` |
| AC-12 | Season start vaccinates each animal (5030) if cash allows, else `VaccinationMissed`; disease draws are seeded, × 3 unvaccinated; a sick day calls the vet (1,500) and loses the next day's milk; with no cash no vet comes, she is sick 2 days and nothing is owed; illness never kills | `LivestockTests.Health_VaccineDisease` |
| AC-13 | The hand's animals order buys straw and feed to fill when under its days, at today's price, and waits with `InsufficientFunds` when cash is short | `OrderTests.AnimalsOrder` |
| AC-14 | `BuyAnimal` with a bank `Financing` counts the animal at 0.75 of its price as collateral; selling her repays the bank first or is `LoanSecured`; foreclosure seizes animals after bought land at the sell price; the shed is never seized | `LivestockTests.DairyLoan_Foreclosure` |
| AC-15 | Net worth adds animals at book value; the season report's farm income and costs include 4020, 4030, 5020, 5030 | `LivestockTests.Books` |
| AC-16 | A schema-6 save loads at schema 7 with no animals and its compost counted in the pit; it advances and round-trips; a year of a dairy bot gives the same hash in one chunk or many; the golden replays are unchanged | `MigrationTests.Schema6_Livestock`, `LivestockTests.ChunkingInvariance`, `ReplayTests.*` |
| AC-17 | The lactation curve's mean over the milk days is 1.00 ± 1%; every animal and fodder value in content is inside its bounds | `ContentTests.Livestock` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| A buffalo's payback, all feed bought | About 2.5 game years | 2–3.5 |
| A buffalo's payback, home-grown green and straw | About 1.3 game years | 1–2 |
| A cow's payback vs a buffalo's | Faster per coin, less per stall | Cow 10–30% faster |
| A buffalo's net a season vs an owner-operated acre's | About equal (real Indian smallholder figures) | 0.7–1.5 × |
| Competent bot (buys 2 buffalo after the Hamlet) net worth after 2 years | Up | Stress bots within 1.20 × (ADR 0016) |
| A new `DairyBot` (all bought feed, max loan) | Never stuck; repays | Within 1.20 × |
| Hamlet day | Unchanged | Day 5–9 |

## 10. Open questions (closed with the owner, 2026-10-22; ADR 0060)

- **G1. The time scale.** **Closed (owner, 2026-10-22):** one game day = 22.8 real days (a quarter ÷ 4, ADR 0008), not ADR 0056's 30; content gives amounts per real day; about one calf a game year (§3.1, §3.2).
- **G2. When cattle unlock.** **Closed (owner, 2026-10-22):** from the Hamlet; the `cattle` chapter on the first yard tap after it; a locked note on the yard's south acre before it; the Becoming a Hamlet sheet lists cattle as a reward (§3.13).
- **G3. Green fodder.** **Closed (owner, 2026-10-22):** berseem (sown Winter, cut Winter–Spring) and multi-cut sorghum (sown Summer, cut Summer–Monsoon), cut daily from 6 h after sowing; the commons' share by season; green fodder bought from neighbours for any shortfall (on by default). Review for sense (Claude, same day, owner's "make everything make sense"): what the animals don't eat is sold standing at 1 coin a kg instead of being lost, the cut follows Weather like any crop, and a hand's fodder order rotates berseem and sorghum through the year (§3.7, §3.8b).
- **G4. Straw.** **Closed (owner, 2026-10-22):** straw into the stack only once the shed exists; surplus left on the field (not sold, so crop income is unchanged); straw can be bought (§3.8).
- **G5. Stocks and who refills them.** **Closed (owner, 2026-10-22):** the feed room 2.4 t (over the 72 h away cap for 4 buffalo), the stack 10 t, a compost pit in every yard; goods kept in their own place; the player fills, or the hand's animals order keeps them stocked; a low-feed warning at purchase and on the sheet (§3.3, §3.7, §3.11).
- **G6. Milk.** **Closed (owner, 2026-10-22):** the co-op pays at once at 6:00 by fat (12 + 6.5 × fat%); the household drinks 1.5 L a real day as home use inside its food allowance, no nutrition bonus (§3.5).
- **G7. Health.** **Closed (owner, 2026-10-22):** vaccination automatic each season if cash allows; seeded disease with the vet called automatically (no vet when cash is short: a longer illness, nothing owed); hunger lowers health; nothing dies (§3.7, §3.10).
- **G8. Money.** **Closed (owner, 2026-10-22):** animals at cost (no depreciation, no ageing in Phase 2); sold at 80% × health; the dairy loan counts the animal at 0.75; calves sold at birth; foreclosure seizes animals after bought land (§3.4, §3.6, §3.12).
- **G9. Long neglect and silage.** **Closed (owner, 2026-10-22):** no animal is lost to neglect in Phase 2; silage later with the equipment ladder (§2, §3.7).

### Implementation notes (US-199, US-200; no rule change)

- Green fodder's two prices live in `livestock.json` (it is never a good in the store).
- Fodder crops can be sown from the Hamlet, with cattle (`EraLocked` before), so the first week's sow sheet and season hints stay as they were; the sow sheet's fodder card comes with US-205.
- A fodder field's growth job ends its stand at the close of its last cut season; its soil change is applied at each cut and on clearing, pro rata.
- A save loads content that has gained goods or crops since it was written (they start empty).
- The vet vaccinates on the first 6:00 of each season, after the morning's milk money (so the milk can pay for it), not at the seasonal pass; the disease draw is keyed by the animal and the day.
- The dairy loan uses spec 005's limit as a land loan does: the animal counts at `livestockLtv` (0.75) of her fair value, and the bank's score factor applies, so at the starting score (650) and with no other collateral or income the bank lends up to 56,200 on a 1,00,000 buffalo (about 44,000 down); at score 750 up to 75,000. The buy card shows the limit and its *Why?* (US-205). Foreclosure takes animals only until the debt is covered (`AnimalSeized`).
- The hand's animals order is checked at every hourly pass with his other orders (it buys only when a place is under its days), so it also acts the moment it's given.

### Changes after approval (owner, 2026-10-22, ADR 0061)

- **The dairy co-op has a building** in the village centre (place kind `dairy`, spec 010 §3.3). Tapping it shows the co-op's price a litre by fat ("Buffalo milk at 7% fat: 57.50 a litre · cow milk at 4%: 38.00"), what it collected this morning, and, once cattle unlock, **Your shed** and **The cattle fair** (the buy cards). Before the Hamlet it says "The co-op buys the village's milk every morning at 6:00. Once you're a Hamlet you can keep a buffalo or a cow."
- **The `cattle` chapter** opens on the first tap of the yard or the dairy after the Hamlet, and its Show me goes to the dairy, then the shed.

## 11. Cut first

1. The milk can and the animals walking to the commons (they stay in the shed).
2. The lactation curve (flat milk while in milk).
3. Disease (vaccination stays, as a cost).
4. The hand's animals order (the player fills).
5. Sorghum (berseem only, bought green fodder in the other seasons).
