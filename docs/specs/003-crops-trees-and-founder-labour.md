# 003. Crops, fruit trees and founder labour

- Status: Approved (2026-10-17), with the Sprint 1 review decisions in §10
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land, farming and livestock (Crops, Yield formula, Fruit trees, Ready to harvest, Inputs and trade-offs); Player journey and eras (The founder's routine); Data model (`CropDef`, `FounderDef`, `DietPlanDef`)
- Related ADRs: 0030 (inherited farm, start, Hamlet gate), 0031 (kitchen garden, buying inputs ahead, health care), 0010 (founder labour and routine), 0014 (yield formula, fertiliser, crop health, cap), 0021 (crop calendar, fruit trees), 0008 (per-season realism, upfront payment), 0009 (day labour, subsistence), 0002 (ready to harvest, no disasters while away), 0024 (hut store), 0028 (pacing, land priced by earning power)
- Depends on specs: 001 (time, weather, jobs), 002 (tiles, moisture, Soil factor, occupancy, slots, the well), 005 (ledger: purchases, sales, prepaid living costs, obligations, tree assets). Uses 004 (input prices, the store, selling). Spec 006 adds hired labour to the same Care pool.

## 1. Goal

The player sows, tends and harvests their own fields and trees, and sees exactly why each harvest came out the way it did; the founder's own body (food, rest, health) is the farm's first engine.

**Lessons:** yield is a product of many factors, and the weakest one hurts most; profit now (chemical fertiliser, monoculture) can cost land value later; your own labour is your first asset, and it has limits; a tree is a long-term investment with a payback period.

## 2. Scope

**In:**
- **Crops** (Phase 1 set): radish, wheat, rice, potato, lentils. Sowing windows by season, growth jobs, *ready to harvest*, harvest into the hut store, clearing a field.
- **The kitchen garden** (ADR 0031): two beds by the hut for quick greens (radish, spinach, coriander), with a realistic season output per bed.
- **The yield formula** of ADR 0014, every factor defined, the 1.6× cap, and the *Why?* breakdown.
- **Inputs:** certified or saved seed (seed ages per season of growth), no fertiliser / compost / chemical, pest risk with spraying. Inputs in the store are used first; the rest is bought at sowing (spec 004).
- **Fertility effects** of crops, fertiliser, compost and lentils (through spec 002's `AdjustFertility`).
- **Fruit trees** (banana, papaya, guava, mango) in yard and field-edge slots: planting, maturity, one fruit batch per fruiting season, picking, lifespan, removal, value and payback.
- **The founder:** the starting state (ADR 0030), daily labour (hours, nutrition, fatigue, health), the diet plan and living costs, work hours and rest, sick days, warm clothing in Winter, medicine, eating from the store, the labour split with day labour, and subsistence.
- **Care:** labour delivered against labour needed, including bucket-watering on the well's acres (spec 002 §3.2).

**Out** (and where it goes instead):
- Goods, prices, the market, the store's capacity and spoilage: 004. This spec says what goes into and out of the store.
- Hired labour (day labourers, the resident hand, standing orders): 006. It adds hours to the same Care pool (§3.6).
- The ledger mechanics (prepayments, obligations, asset capitalisation): 005. This spec says what is paid, when and to which account.
- Remaining crops (maize, tomato, onion, sugarcane, cotton), whole-acre orchards and apple trees: later phases (ADR 0017, 0021).
- Equipment (the Tools factor is 1.00 in Phase 1), irrigation beyond the well, livestock and their compost: Phase 2.
- Health from unsafe water (ADR 0010): with the town's water network (Phase 2–3). Doctors and the clinic: Township.
- The field, founder and tree panels' layout: 008. This spec defines their content and *Why?* lines.

## 3. Rules and formulas

Quantities of goods are `long` in grams. Money is `long` in 1/100 coin. Factors, rates and fractions are `Fixed`; every `Fixed` multiply and divide rounds to nearest, ties away from zero (spec 001 §3.1). A quantity in kg (Fixed) becomes grams by `round(kg × 1000)`.

### 3.1 Per-season realism (ADR 0008)

Every crop quantity in data is **per acre per season**: what an acre realistically yields, needs and uses in one real-world quarter of continuous cropping. A crop cycle takes its share:

```
cycleShare(crop) = growthSeconds ÷ ClockDef.SecondsPerSeason          (Fixed, rounded)
```

With 4-day seasons: radish (2 h) 0.0208333; wheat (6 h) 0.0625; rice and potato (24 h) 0.25; lentils (8 h) 0.0833333. The kitchen garden has its own rule (§3.13).

| Per cycle and acre | Formula |
| --- | --- |
| Base yield (kg) | `CropDef.seasonYieldPerAcre × cycleShare` |
| Seed needed (kg) | `CropDef.seedPerAcrePerSeason × cycleShare` |
| Fertiliser or compost used (kg) | `FertiliserDef.kgPerAcrePerSeason × cycleShare` |
| Spray used (ml) | `PestRiskDef.sprayMlPerAcrePerSeason × cycleShare` |
| Fertility used or restored (points) | `CropDef.fertilityPerSeason × cycleShare` (and likewise for fertiliser and compost) |

So a player who keeps an acre busy all season gets that season's realistic output; a field left waiting in *ready to harvest*, or idle, earns nothing for that time (ADR 0002).

### 3.2 Sowing

| Rule | Behaviour |
| --- | --- |
| Where | One crop per farm tile (acre) on owned land. The tile must be vacant (spec 002 occupancy) |
| When | Only in a season listed in `CropDef.sowSeasons` (ADR 0021). Radish lists all four. Outside the window `Sow` is rejected with `OutOfSeason`, and *Why?* names the crop's seasons |
| Seed | **Certified:** the cycle's seed need is taken from the certified seed good in the store, and whatever is missing is bought from the dealer at today's input price (spec 004). **Saved:** taken from the crop's own good in the store; rejected with `NoSavedSeed` if the store holds less, or `SeedCannotBeSaved` for a crop with `canSaveSeed` false (radish) |
| Fertiliser | Chosen at sowing: none, compost or chemical. Taken from the store first, the rest bought at today's input price |
| Payment | Whatever is bought is paid together, upfront, through the ledger (Dr 5000 Seeds and planting / 5010 Fertiliser and spraying, Cr Cash). Inputs from stock were expensed when bought (spec 004). If cash is short, `Sow` is rejected with `InsufficientFunds` and nothing changes |
| Effect | The tile becomes occupied (`SetOccupied`), a growth job is scheduled for `sownAt + growthMinutes × 60`, and a pest check is scheduled at the half-way point (§3.8). The Soil factor is fixed at sowing (§3.4). Emits `CropSown` |

### 3.3 Growth, ready to harvest, harvest and clearing

| Rule | Behaviour |
| --- | --- |
| Growth | A crop grows for exactly `growthMinutes` of real time (ADR 0002: weather changes yield, never duration). The growth job is a spec 001 job, so it appears in `UpcomingJobs` for notifications |
| Becomes ready | When the growth job falls due: the accrued factors are closed (§3.5–3.7), the pest outcome applied (§3.8), the yield computed (§3.4) and frozen, fertility changes applied (§3.10), and the crop enters **Ready**. Emits `CropReady` |
| Ready waits | A ready crop never spoils or loses yield (ADR 0002). It keeps the tile occupied, so the cost of waiting is the lost growing time |
| Harvest | `Harvest(tile)` moves the frozen yield into the hut store (spec 004 sets the full-store rules), frees the tile (`SetOccupied(false)`), and emits `CropHarvested` with the *Why?* breakdown. It also sets the age of that crop's saved seed (§3.9) |
| Clear | `ClearField(tile)` removes a growing or ready crop. Nothing is refunded or harvested. The tile becomes vacant |

### 3.4 The yield formula (ADR 0014)

```
Yield = Base × Soil × Water × Care × Seed × Weather × Tools × Fertiliser × CropHealth
        capped at FarmDef.yieldCap × Base
```

The product is taken left to right in `Fixed`, rounded at each step, then capped, then converted to grams.

| Factor | Definition | Range |
| --- | --- | --- |
| Base | `seasonYieldPerAcre × cycleShare` (§3.1) | kg |
| Soil | Spec 002 §3.4 `SoilFactor(tile)` at sowing: soil type × fertility × shade | 0.25–1.38 |
| Water | Time-weighted mean of the hourly water factor over the growth (§3.5) | `waterDryFloor`–1 |
| Care | Time-weighted mean of the care ratio over the growth (§3.6) | 0–`careCap` (1.1) |
| Seed | Certified 1.00; saved seed of age a seasons: `max(CropDef.savedSeedFloor, 1 − CropDef.savedSeedLossPerSeason × a)` (§3.9) | 0.80–1 |
| Weather | Time-weighted mean of `FarmDef.weatherYieldFactor[w]` over the growth (§3.7) | data |
| Tools | `FarmDef.toolsFactor`, 1.00 in Phase 1 (hand tools; the equipment ladder is Phase 2) | 1 |
| Fertiliser | `FertiliserDef.yieldFactor`: none 1.00, compost 1.05, chemical 1.25 | |
| CropHealth | 1.00, or the outbreak result (§3.8) | 0–1 |

- **The cap** is applied after the product: `min(product, yieldCap × Base)`. The breakdown records whether it applied.
- **The breakdown** stored with the crop (for *Why?* and `CropHarvested`): Base, every factor, the uncapped product, whether the cap applied, and the final grams.
- Example (wheat, loam at fertility 45, no shade, water 1.00, care 1.10, certified seed, weather 0.98, no fertiliser, healthy): 75 kg × 0.875 × 1.00 × 1.10 × 1.00 × 0.98 × 1.00 × 1.00 × 1.00 = **70.74 kg** (70,744 g, within Fixed rounding).

### 3.5 Water factor

Each crop has an ideal moisture band `[CropDef.moistureMin, CropDef.moistureMax]`. For a tile's moisture m (spec 002):

```
WaterFactor(m) = waterDryFloor + (1 − waterDryFloor) × m ÷ moistureMin           if m < moistureMin
               = 1                                                                if moistureMin ≤ m ≤ moistureMax
               = 1 − (1 − waterWetFloor) × (m − moistureMax) ÷ (100 − moistureMax)  if m > moistureMax
```

Defaults: `waterDryFloor` 0.40 (bone dry), `waterWetFloor` 0.60 (waterlogged). Example: wheat (band 35–65) at moisture 20: 0.40 + 0.60 × 20 ÷ 35 = **0.742857**.

### 3.6 Care, labour and bucket watering

**Care ratio** (one ratio for all of the founder's growing crops, like a shared pool):

```
supplyPerDay = FounderLabourHours(today) × (1 − dayLabourShare)  +  hired hours (spec 006)
demandPerDay = Σ over growing crops on owned tiles of
                 ( CropDef.labourPerAcrePerDay  +  24 × wellLift(tile) × FarmDef.bucketHoursPerMoisturePoint )
careRatio    = careCap                                  if demandPerDay = 0
             = min(careCap, supplyPerDay ÷ demandPerDay) otherwise
```

- `wellLift(tile)` is the moisture the well's floor added at the last hourly pass (spec 002 §3.2 step 4), 0 elsewhere. Carrying buckets is labour: about 1 extra hour a day per acre on a sunny Summer day, 2.4 on a heatwave day (defaults).
- Ready crops need no care. Crops on unowned land don't exist.
- The ratio is recomputed at every hourly pass, at every daily pass (new founder hours), and whenever the set of growing crops or the founder's plan changes (sow, ready, clear, plan commands), so sowing many acres at once can't borrow an idle hour's ratio.
- `careCap` (1.10) lets a well-staffed farm tend slightly better than "enough".

**Founder labour** (ADR 0010), fixed at each daily pass for the day ahead:

```
FounderLabourHours(day) = Hours × Nutrition × (1 − Fatigue) × Health      (0 on a sick day)
```

| Term | Rule | Default |
| --- | --- | --- |
| Hours | `WorkHoursDef.hours` of the chosen option. While the player is away the founder works the `normal` option (ADR 0010) | rest 0, short 6, normal 8, long 10 |
| Nutrition | The current diet plan's `DietPlanDef.nutrition` | subsistence 0.80, basic 0.90, balanced 1.00, rich 1.05 |
| Fatigue | §3.11 | 0–0.5 |
| Health | §3.11 | 0.5–1.0 |

With basic food, normal hours and full health the founder delivers 7.2 hours a day: enough for about 2 acres of wheat at full care (3.0 h/acre/day plus watering) and about 2.4 acres at care 1.0. Beyond that Care falls below 1, which is the reason to hire (ADR 0010).

### 3.7 Time-weighted factors (accrual)

Water, Care, Weather and the crop's wet-time share (§3.8) are means over the growth:

```
mean = Σ (rate_i × seconds_i) ÷ growthSeconds
```

- Each growing crop keeps its current water factor, weather factor and wet flag, and the care ratio is shared. Between two **accrual points** each rate is constant.
- Accrual points: every hourly pass (after spec 002's moisture update and the weather of the new hour), every daily pass, and every sow, ready, clear and plan command. At an accrual point every growing crop adds `rate × (now − lastAccrual)` to its sums, then the rates are refreshed.
- A crop sown between two passes uses the rates in force at sowing until the next accrual point. (Garden crops have no factors, §3.13.)
- Accrual points are sim events, never `AdvanceTo` chunk boundaries, so the result doesn't depend on how time is advanced (chunking invariance).

### 3.8 Pests, disease and spraying (ADR 0014)

At the **pest check** (a job at `sownAt + growthSeconds ÷ 2`, integer division):

```
risk = PestRiskDef.baseRisk(crop)
       × (1 + monocultureStep × min(repeats, monocultureMaxRepeats))
       × (1 + wetStep × wetShare)
       × (sprayedBeforeCheck ? sprayRiskFactor : 1)
```

- `repeats`: how many crops of the same kind were harvested on this tile in a row immediately before this one (0 after a different crop or fallow). Kept per tile.
- `wetShare`: the share of the growth so far spent in a weather listed in `FarmDef.wetWeathers` (rain, heavy rain, storm).
- **The roll:** a seeded uniform draw u in [0, 1) (stream Pest, counter = crop id). An outbreak happens if u < risk.
- **Severity:** a second draw v: if v < `severeShare` the outbreak is **severe** (CropHealth `severeHealth`, default 0.00); otherwise CropHealth = `outbreakHealthMin + (outbreakHealthMax − outbreakHealthMin) × w`, w a third draw (default 0.40–0.80).
- **Not while away (ADR 0002, 0014):** a severe result rolled during an away advance becomes a normal outbreak with w = 0 (CropHealth 0.40).
- **Warning:** an outbreak emits `PestWarning(tile, severity, readyAt)` at once. The crop's health is applied when it becomes ready, so the player can respond in the second half of the growth.
- **Spraying** (`Spray(tile)`, once per crop cycle, paid at the input price for the cycle's dose, Dr 5010):
  - before the check: the risk is multiplied by `sprayRiskFactor` (0.50);
  - after a warning: the damage is reduced: `CropHealth = 1 − (1 − rolledHealth) × sprayDamageFactor` (0.30). A severe outbreak sprayed in time ends at 0.70.
- Examples: wheat after wheat, in sun: 0.06 × 1.5 × 1 = 9% a cycle. Rice after rice after rice in a wet Monsoon (wet share 0.7): 0.10 × 2.0 × 1.7 = 34%.

### 3.9 Seed

| Rule | Behaviour |
| --- | --- |
| Certified | Seed factor 1.00. Uses `seedPerAcrePerSeason × cycleShare` kg of the crop's certified seed good (stock first, then bought) |
| Saved | Free, but uses the crop's own good from the store. Only crops with `canSaveSeed` (not radish: its seed needs the plant to flower for months) |
| Seed age | Saved seed ages by **seasons of growth**, like fertility (per-season realism, ADR 0008): a harvest sown with seed of age a (certified: 0) yields seed of age `a + cycleShare`. Kept per crop kind: the age of the last harvest of that crop. A simplification: mixing old and new seed in the store uses the latest age |
| Seed factor | `max(savedSeedFloor, 1 − savedSeedLossPerSeason × age)`. Wheat, rice and lentils lose 0.02 a season of growth (floor 0.90); potato tubers 0.05 (floor 0.80). So saved wheat stays above 0.95 for about 2.5 seasons of continuous growing (Indian advice: replace self-pollinated seed every 3–4 years), and saved potato falls to 0.90 in 2 |

### 3.10 Fertility from crops and inputs

Applied through spec 002's `AdjustFertility` when the crop becomes ready, for the cycle's share:

| Cause | Change per season of growth | Reason |
| --- | --- | --- |
| The crop itself | `−CropDef.fertilityPerSeason` (wheat 4, rice 4, potato 5, radish 2) | CropUse |
| Lentils | `+4` (lentils' `fertilityPerSeason` is −4: they restore) | Lentils |
| Chemical fertiliser | `FertiliserDef.fertilityPerSeason` = −3 (ADR 0014) | Fertiliser |
| Compost | `FertiliserDef.fertilityPerSeason` = +2 | Compost |

Example: 4 seasons' worth of chemically fertilised cycles lower fertility by exactly 12 points from fertiliser (ADR 0014), on top of the crop's own use. Fallow recovery (6 a season, spec 002) and lentils are the ways back.

### 3.11 The founder's household (ADR 0010)

**Living costs and the diet plan.**

| Rule | Behaviour |
| --- | --- |
| Plan | One `DietPlanDef` at a time: subsistence, basic, balanced, rich. The cost per season covers food and the household's basics (clothing, kerosene, sundries). New games start on **basic** |
| Paid upfront | At a new game, the first season's cost is paid at once, pro rata for what's left of season 0: `cost × (S − t₀) ÷ S` (integer division, rounded down). Each later season is paid at the season close as a Living obligation (spec 005 §3.4 step 5). Each payment is a prepayment (Dr 1100, Cr Cash) expensed daily to 5200 Living costs (spec 005 §3.2) |
| Changing plan | `SetDietPlan` takes effect at once: the current prepayment is closed (the unearned part refunded, spec 005 `ClosePrepayment`) and the new plan is paid for the rest of the season. Rejected with `InsufficientFunds` if the new payment can't be met after the refund |
| Subsistence | Chosen by the player, or automatic when the season's living obligation can't be paid (spec 005 `ObligationUnpaid`). Costs 0, nutrition 0.80. No debt is ever created (ADR 0009). The household returns to its previous plan at the next season close it can afford |
| Eat from own harvest | With `eatFromStore` on, at each living-cost payment the household first takes edible goods from the store worth up to `DietPlanDef.foodShare × cost`, valued at the trader's buying price (spec 004), most perishable first (highest `spoilagePerDay`, then good id). Cash pays only the remainder. The goods leave the store at once ("eaten over the season") |

**Work hours, fatigue and rest** (daily pass, before founder hours are fixed):

```
fatigue ← clamp(fatigue + Δ, 0, FounderDef.maxFatigue)
Δ = WorkHoursDef.fatigueChange                         if it is ≥ 0
  = WorkHoursDef.fatigueChange × BuildingDef.restRecovery(home)   if it is < 0     (hut 0.80)
```

Defaults: rest −0.15, short −0.05, normal 0, long +0.08 a day; `maxFatigue` 0.50. A sick day counts as rest.

**The start** (ADR 0030): basic diet, normal hours, no day labour, `eatFromStore` on, nutrition 0.90, fatigue 0, health 1.00, and 20,000 coins (spec 005).

**Health and sick days** (daily pass):

```
health ← clamp(health + recoveryPerDay
                      − nutritionPenalty × max(0, goodNutrition − nutrition)
                      − fatiguePenalty × max(0, fatigue − safeFatigue)
                      − (Winter and on subsistence ? noWarmClothingPenalty : 0),
               minHealth, 1)
sickChance = sickChanceAtMinHealth × (1 − health) ÷ (1 − minHealth)
```

Defaults: `recoveryPerDay` 0.02, `goodNutrition` 0.95, `nutritionPenalty` 0.20, `safeFatigue` 0.20, `fatiguePenalty` 0.10, `minHealth` 0.50, `sickChanceAtMinHealth` 0.25. A seeded draw (stream FounderSick, counter = day index) below `sickChance` makes the day a **sick day**: no labour, emits `FounderSick`.

- Basic food and normal hours: +0.02 − 0.01 = +0.01 a day, so health stays at 1.
- Subsistence: −0.01 a day net; about 50 days (12 seasons) to fall from 1.0 to 0.5.
- Long days without rest: fatigue reaches 0.5 in 7 days, then health falls 0.01 a day and labour is down 50% from fatigue alone.
- **Warm clothing** (ADR 0031): the plan's cost includes clothing, so only a household on subsistence goes without in Winter: a further −0.02 a day (`noWarmClothingPenalty`), so −0.03 a day net.
- **Medicine** (ADR 0031): `BuyMedicine` buys from the village chemist for `FounderDef.medicineCost` (500 coins; Dr 5250 Health costs, Cr Cash) and raises health by `medicineHealthGain` (0.10, capped at 1), at most once a day. It doesn't undo today's sick day (today's hours are already fixed); it lowers the chance of the next ones.

**Day labour** (ADR 0009):

| Rule | Behaviour |
| --- | --- |
| Split | `SetLabourSplit(share)`, share ∈ {0, 25, 50, 75, 100} %. That share of the founder's daily hours goes to neighbours' farms; the rest tends the founder's own fields |
| Pay | At each daily pass, for the day that ended: `hours × FounderDef.dayLabourWagePerHour`, Dr Cash, Cr 4100 Day-labour earnings. Emits `DayLabourPaid` |
| Wage | 1,125 coins an hour: a full 8-hour day is 9,000, and a full season 36,000, the farm hand's season wage (ADR 0008) |
| Away | Day labour continues while away, at normal hours |

### 3.12 Fruit trees (ADR 0021)

| Rule | Behaviour |
| --- | --- |
| Planting | `PlantTree(slot, tree)` on an empty slot (spec 002) of owned land. The sapling is bought at its input price and **capitalised** (Dr 1700 Trees, Cr Cash; spec 005 §3.3). Calls `SetTree(slot, treeId)`. Rejected with `TerrainNotAllowed` if `TreeDef.terrain` doesn't match (apple: hills, not in Phase 1) |
| Growing | A maturity job at `plantedAt + growthMinutes × 60`. Then `SetTreeMature(slot)`: from now it shades a field-edge tile (spec 002 §3.4). Emits `TreeMatured` |
| Fruiting | A mature tree carries **one batch per fruiting season**. At each season start (seasonal pass), every mature tree whose `fruitingSeasons` include the new season and whose previous batch was picked gets a ripe batch of `batchYieldPerTree` kg. A tree that matures during one of its fruiting seasons gets that season's batch at once. Emits `FruitRipe` |
| Waiting | Ripe fruit waits on the tree, like a ready crop (ADR 0002), and blocks the next batch until picked |
| Picking | `PickFruit(slot)` moves the batch into the store (spec 004 full-store rules). Records the batch's value at the trader's buying price as the tree's returns. Emits `FruitPicked` |
| Lifespan | `harvestLimit` batches (banana 4, papaya 8; 0 = whole game). After the last one the tree is **spent**: it is removed (`SetTree(slot, 0)`), its book value goes to 5600 Loss (spec 005), and `TreeSpent` is emitted |
| Removal | `RemoveTree(slot)` at any time, same accounting as spent. Unpicked fruit is lost |
| Payback (*Why?*) | Cost (book value), returns to date (Σ picked batches at the trader's buying price when picked), and the expected payback season: if returns ≥ cost, the season it was reached; otherwise now + ⌈(cost − returns) ÷ (batchYieldPerTree × today's buying price)⌉ fruiting seasons, mapped to calendar seasons |

### 3.13 The kitchen garden (ADR 0031)

| Rule | Behaviour |
| --- | --- |
| Beds | `BuildingDef.gardenBeds` beds by the founder's home (hut: 2), on the yard. Watered from the well and tended by the household: no Care, Water, Weather or pest factors, and no labour demand |
| Crops | `GardenCropDef`: radish (10 min, 5 kg a picking), spinach (15 min, 4 kg), coriander (5 min, 1.5 kg). All seasons |
| Sowing | `SowGarden(bed, crop)` on an empty bed, using `seedGramsPerSowing` of the crop's seed from the store, the rest bought (spec 004). A growth job; *ready* waits like a field crop |
| Season output | Each bed can give at most `FarmDef.gardenSeasonOutputKg` (100 kg) a season, across all its pickings. `PickGarden(bed)` moves `min(harvestKg, remaining)` into the store (spec 004 full-store rules); when `remaining` reaches 0 the bed **rests** until the next season start, and `SowGarden` is rejected with `BedRested` |
| Why it's realistic | A real kitchen garden of about 200 m² gives a family roughly 150–250 kg of vegetables a quarter. Two beds × 100 kg = 200 kg a season, so the total respects per-season realism (ADR 0008) and quick crops can't become a money loop, while each picking is a real basket |
| Value | About 1,500 coins of vegetables a season: food for the household (eat from store) or sales. The *Why?* line shows "Your garden saved N coins of groceries this season" |
| Onboarding | The first session's first crop is sown in the garden (spec 009) |

## 4. Data

**Definitions:**

`content/crops.json` (`CropDef`; quantities per acre per season, §3.1):

| Field | Type | Radish | Wheat | Rice | Potato | Lentils | Bounds |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `id`, `nameKey` | string | `radish` | `wheat` | `rice` | `potato` | `lentils` | |
| `outputGood` | good id (004) | radish | wheat | rice | potato | lentils | an edible produce good |
| `seedGood` | good id (004) | seed_radish | seed_wheat | seed_rice | seed_potato | seed_lentils | an input good |
| `sowSeasons` | list of seasons | all | winter | summer, monsoon | spring, winter | summer, winter | non-empty |
| `growthMinutes` | int | 120 | 360 | 1,440 | 1,440 | 480 | 1–20,160 |
| `seasonYieldPerAcre` | kg (Fixed) | 4,000 | 1,200 | 1,500 | 7,000 | 350 | > 0 |
| `seedPerAcrePerSeason` | kg (Fixed) | 8 | 27 | 10 | 700 | 6 | ≥ 0 |
| `moistureMin`, `moistureMax` | Fixed | 40, 75 | 35, 65 | 60, 95 | 40, 70 | 30, 60 | 0 < min < max < 100 |
| `labourPerAcrePerDay` | hours (Fixed) | 3.0 | 3.0 | 4.0 | 3.5 | 2.0 | 0–24 |
| `fertilityPerSeason` | points (Fixed) | 2 | 4 | 4 | 5 | −4 | −20 to 20 (negative restores, reason Lentils) |
| `canSaveSeed` | bool | false | true | true | true | true | |
| `savedSeedLossPerSeason` | Fixed | — | 0.02 | 0.02 | 0.05 | 0.02 | 0–0.5 |
| `savedSeedFloor` | Fixed | — | 0.90 | 0.90 | 0.80 | 0.90 | 0.5–1 |

`content/crops.json` also lists the garden crops (`GardenCropDef`):

| Field | Radish | Spinach | Coriander | Bounds |
| --- | --- | --- | --- | --- |
| `id`, `nameKey`, `good`, `seedGood` | garden_radish, radish, seed_radish | garden_spinach, spinach, seed_spinach | garden_coriander, coriander, seed_coriander | edible produce and input goods |
| `growthMinutes` | 10 | 15 | 5 | 1–120 |
| `harvestKg` | 5 | 4 | 1.5 | > 0 |
| `seedGramsPerSowing` | 20 | 15 | 10 | ≥ 0 |

`content/farming.json` (`FarmDef`, `FertiliserDef`, `PestRiskDef`, `TreeDef` lives in `trees.json`):

| Definition | Field | Default | Bounds |
| --- | --- | --- | --- |
| `FarmDef` | `yieldCap` | 1.6 | 1–3 |
| | `careCap` | 1.1 | 1–1.5 |
| | `toolsFactor` | 1.00 | 0.5–2 |
| | `weatherYieldFactor` (map weather id → Fixed) | sun 1.00, cloudy 0.98, rain 1.00, heavy_rain 0.92, heatwave 0.85, frost 0.88, storm 0.85 | 0–1.2; every weather id |
| | `wetWeathers` | rain, heavy_rain, storm | weather ids |
| | `waterDryFloor`, `waterWetFloor` | 0.40, 0.60 | 0–1 |
| | `bucketHoursPerMoisturePoint` | 0.1 | 0–2 |
| | `gardenSeasonOutputKg` (per bed) | 100 | > 0 |
| `FertiliserDef` | `id` | none, compost, chemical | |
| | `good` | —, compost, fertiliser_chemical | input good or none |
| | `kgPerAcrePerSeason` | 0, 2,000, 150 | ≥ 0 |
| | `yieldFactor` | 1.00, 1.05, 1.25 | 0.5–2 |
| | `fertilityPerSeason` | 0, +2, −3 | −20 to 20 |
| `PestRiskDef` | `baseRisk` by crop | radish 0.03, wheat 0.06, rice 0.10, potato 0.10, lentils 0.05 | 0–1 |
| | `monocultureStep`, `monocultureMaxRepeats` | 0.5, 2 | 0–2, 0–10 |
| | `wetStep` | 1.0 | 0–5 |
| | `sprayGood`, `sprayMlPerAcrePerSeason` | spray, 2,000 | |
| | `sprayRiskFactor`, `sprayDamageFactor` | 0.50, 0.30 | 0–1 |
| | `outbreakHealthMin`, `outbreakHealthMax`, `severeShare`, `severeHealth` | 0.40, 0.80, 0.05, 0.00 | 0–1, min ≤ max |

`content/trees.json` (`TreeDef`):

| Field | Banana | Papaya | Guava | Mango | Bounds |
| --- | --- | --- | --- | --- | --- |
| `saplingGood` (input, unit each) | sapling_banana | sapling_papaya | sapling_guava | sapling_mango | |
| `growthMinutes` | 10,080 (1 week) | 10,080 | 20,160 (2 weeks) | 34,560 (3.5 weeks) | ≥ 1 |
| `fruitingSeasons` | all | all | monsoon, winter | summer | non-empty |
| `batchYieldPerTree` (kg) | 8 | 12 | 30 | 120 | > 0 |
| `harvestLimit` | 4 | 8 | 0 | 0 | ≥ 0 (0 = no limit) |
| `fruitGood` | banana | papaya | guava | mango | edible produce good |
| `terrain` | any | any | any | any | any, hill (later) |

`content/founder.json` (`FounderDef`, `DietPlanDef`, `WorkHoursDef`):

| Definition | Field | Default | Bounds |
| --- | --- | --- | --- |
| `FounderDef` | `startDietPlan`, `startWorkHours` | basic, normal | ids |
| | `maxFatigue` | 0.50 | 0–1 |
| | `recoveryPerDay`, `goodNutrition`, `nutritionPenalty` | 0.02, 0.95, 0.20 | 0–1 |
| | `safeFatigue`, `fatiguePenalty` | 0.20, 0.10 | 0–1 |
| | `minHealth`, `sickChanceAtMinHealth` | 0.50, 0.25 | 0–1 |
| | `dayLabourWagePerHour` | 1,125 coins | > 0 |
| | `noWarmClothingPenalty` | 0.02 a day | 0–1 |
| | `medicineCost`, `medicineHealthGain` | 500 coins, 0.10 | > 0, 0–1 |
| | `startDayLabourShare`, `startEatFromStore` | 0, true | |
| `DietPlanDef` | `id`, `nameKey` | subsistence, basic, balanced, rich | `subsistence` required |
| | `costPerSeason` | 0, 18,000, 24,000, 36,000 coins | ≥ 0; subsistence 0 |
| | `nutrition` | 0.80, 0.90, 1.00, 1.05 | 0.8–1.05 (ADR 0010 floor and cap) |
| | `foodShare` | 0, 0.6, 0.6, 0.6 | 0–1 |
| `WorkHoursDef` | `id` | rest, short, normal, long | `normal` required |
| | `hours` | 0, 6, 8, 10 | 0–14 |
| | `fatigueChange` | −0.15, −0.05, 0, +0.08 | −1 to 1 |

`content/buildings.json` gains `restRecovery` (hut 0.80; well 0) and `gardenBeds` (hut 2; well 0) for the founder's home.

**State** (part of `GameState`):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Crop` | `id`, `tileId`, `cropDef`, `state` (Growing, Ready) | | One per occupied tile |
| | `sownAt`, `readyAt` | long | |
| | `seedSource`, `seedAge`, `fertiliser` | | `seedAge` Fixed seasons |
| | `soilFactor` | Fixed | Fixed at sowing |
| | `waterSum`, `careSum`, `weatherSum`, `wetSeconds`, `lastAccrual` | Fixed / long | §3.7 |
| | `waterRate`, `weatherRate`, `wet` | Fixed / bool | Current rates |
| | `spray` (None, BeforeCheck, AfterWarning), `pestChecked`, `outbreak` (None, Normal, Severe), `rolledHealth` | | §3.8 |
| | `breakdown` | factors, uncapped, capped flag, grams | Set when ready |
| `CropTileHistory` | `tileId`, `lastCrop`, `repeats` | | Monoculture count |
| `Crops` | `nextCropId`, `careRatio`, `lastAccrual`, `savedSeedAge` per crop | | Fixed seasons |
| `GardenBed` | `id`, `crop`, `state` (Empty, Growing, Ready), `readyAt`, `remainingKg` | | `remainingKg` resets at each season start |
| `Tree` | `id`, `slotId`, `treeDef`, `plantedAt`, `state` (Growing, Mature), `ripeKg`, `batches`, `returns` (money) | | |
| `Founder` | `dietPlan`, `previousPlan`, `workHours`, `dayLabourShare`, `eatFromStore` | | |
| | `nutrition`, `fatigue`, `health` | Fixed | |
| | `todayHours`, `sickToday`, `livingPrepaymentId`, `medicineDay` | | `medicineDay`: the day index of the last medicine |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `Sow` | `tileId`, `cropId`, `seed` (Certified, Saved), `fertiliserId` | `UnknownTile`; `NotOwned`; `NotFarmTile`; `TileOccupied`; `UnknownCrop`; `OutOfSeason`; `UnknownFertiliser`; `SeedCannotBeSaved`; `NoSavedSeed`; `InsufficientFunds` | §3.2. Emits `CropSown` |
| `SowGarden` | `bedId`, `gardenCropId` | `UnknownBed`; `BedBusy`; `BedRested`; `UnknownCrop`; `InsufficientFunds` | §3.13 |
| `PickGarden` | `bedId`, `overflow` | `UnknownBed`; `NotReady`; spec 004's store reasons | §3.13. Emits `GardenPicked` |
| `Harvest` | `tileId`, `overflow` (spec 004) | `NoCrop`; `NotReady`; spec 004's store reasons | §3.3. Emits `CropHarvested` |
| `ClearField` | `tileId` | `NoCrop` | §3.3. Emits `FieldCleared` |
| `Spray` | `tileId` | `NoCrop`; `NotGrowing`; `AlreadySprayed`; `InsufficientFunds` | §3.8 |
| `PlantTree` | `slotId`, `treeId` | `UnknownSlot`; `SlotOccupied`; `UnknownTree`; `TerrainNotAllowed`; `InsufficientFunds` | §3.12. Emits `TreePlanted` |
| `PickFruit` | `slotId`, `overflow` | `NoTree`; `NoRipeFruit`; spec 004's store reasons | §3.12. Emits `FruitPicked` |
| `RemoveTree` | `slotId` | `NoTree` | §3.12. Emits `TreeRemoved` |
| `SetDietPlan` | `planId` | `UnknownPlan`; `InsufficientFunds` | §3.11 |
| `SetWorkHours` | `optionId` | `UnknownOption` | From the next daily pass |
| `SetLabourSplit` | `sharePercent` | `InvalidShare` (not 0, 25, 50, 75 or 100) | Care ratio refreshed now; pay from the next daily pass |
| `SetEatFromStore` | `on` | — | From the next living-cost payment |
| `BuyMedicine` | — | `AlreadyTreatedToday`; `InsufficientFunds` | §3.11 |

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `CropSown` | `tileId`, `cropId`, `readyAt`, `cost` | `Sow` succeeds | HUD, notifications (007), play log |
| `CropReady` | `tileId`, `cropId`, `grams` | Growth ends | HUD, notifications, While-you-were-away |
| `CropHarvested` | `tileId`, `cropId`, `grams`, breakdown | `Harvest` | HUD (*Why?*), onboarding (009), play log |
| `FieldCleared` | `tileId`, `cropId` | `ClearField` | HUD |
| `PestWarning` | `tileId`, `severity`, `readyAt` | An outbreak is rolled | HUD (advisor), notifications |
| `TreePlanted` / `TreeMatured` / `FruitRipe` / `FruitPicked` / `TreeSpent` / `TreeRemoved` | `slotId`, `treeId`, and grams or value | §3.12 | HUD, notifications, report |
| `FounderSick` | `day` | A sick day is rolled | Founder panel, report |
| `DayLabourPaid` | `hours`, `amount` | Daily pass | Report, P&L |
| `DietPlanChanged` | `old`, `new`, `automatic` | Plan change or subsistence | Founder panel, report |
| `GardenPicked` | `bedId`, `good`, `grams`, `remainingKg` | `PickGarden` | HUD, onboarding (first harvest), report |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Which crop, when | Seed (potato's is dear) | Yield at harvest; long crops tie the field up | Labour demand; fertility; pest repeats | Clear the field (seed lost) |
| Certified or saved seed | Certified costs coins; saved uses stock | Saved seed loses 2% (potato 5%) of yield per season of growth | Store stock | Next cycle |
| Tend the kitchen garden | Seed (a few coins) | A basket every few minutes until the bed's season output is used | Household food (eat from store) | — |
| Buy medicine | 500 coins | Health +0.10: fewer sick days | — | — |
| Fertiliser | Compost or chemical costs coins | Chemical +25% now, −3 fertility a season; compost +5% and +2 fertility | Soil factor and land price (002) later | Fertility recovers slowly (fallow 6 a season) |
| Spray | Coins | Halves the risk, or cuts outbreak damage by 70% | — | Once a cycle |
| Harvest now or let it wait | None | Waiting costs growing time | Store space (004) | — |
| Plant a tree | The sapling | Fruit after 1–3.5 weeks, every fruiting season | Edge trees shade the acre 3% each | Remove (book value lost) |
| Diet plan | Coins per season | Nutrition → labour; health | Care, sick days | Change any time (refund pro rata) |
| Long hours | None | +25% labour now, fatigue builds | Health, sick days | Rest recovers |
| Day labour share | Less care on own fields | Cash every day | Care ratio | Change any time |

## 7. UI

Layouts are spec 008's. Content:

```
Field panel, growing crop                          Field panel, ready crop
+------------------------------------------+       +------------------------------------------+
| North acre: Wheat, growing   3 h 12 min  |       | North acre: Wheat, ready     70.7 kg     |
| Care 1.10   Water 0.98   Weather 0.97    |       | Yield breakdown            [Why?]        |
| Pest risk: low (sprayed)                 |       |  base 75.0 kg (1,200 kg/season x 1/16)   |
| [Spray: 63]          [Clear field]       |       |  soil 0.875  water 1.00  care 1.10       |
+------------------------------------------+       |  seed 1.00  weather 0.98  tools 1.00     |
                                                   |  fertiliser 1.00  health 1.00            |
Sow sheet (tap a vacant acre)                      |  = 70.7 kg (cap 120.0 not reached)       |
+------------------------------------------+       | [Harvest]                                |
| Sow on North acre (loam, fert. 45)       |       +------------------------------------------+
| Radish 2 h | Potato 24 h  (Spring)      |
| Wheat: Winter only  [Why?]               |       Founder panel
| Seed (o) certified: 175 kg in store      |       +------------------------------------------+
| Fertiliser ( ) none (o) compost ( ) chem |       | Labour today 7.2 h  [Why?]               |
| Expected 1,608 kg (soil 0.875, comp 1.05)|       |  8 h x nutrition 0.90 x (1-0.00) x 1.00  |
| Buy: compost 1,000   [Sow]               |       |  own fields 75% | day labour 25% (2,025) |
+------------------------------------------+       | Food: Basic (18,000/season)  [Change]    |
                                                   | Hours: Normal   Fatigue 0.00  Health 1.00|
Kitchen garden (tap a bed)                         | [Buy medicine: 500]                      |
+------------------------------------------+
| Bed 1: Spinach, ready       4.0 kg       |
| This season: 36 of 100 kg picked         |
| [Pick]     then sow: radish 10 min |     |
|            spinach 15 min | coriander 5  |
| Saved 1,240 coins of groceries [Why?]    |
+------------------------------------------+
Tree panel                                         | Eat from store: on                       |
+------------------------------------------+       +------------------------------------------+
| Mango (yard), mature, fruits in Summer   |
| Cost 400  Returns 6,000  Paid back Y1 Su |
| [Pick 120 kg]          [Remove]          |
+------------------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Loading content where a crop names an unknown good, a sowing season that doesn't exist, `moistureMin ≥ moistureMax`, a diet plan list without `subsistence`, a nutrition outside 0.8–1.05, or a `weatherYieldFactor` missing a weather id fails validation naming the field | `ContentTests.Crops_Validation` |
| AC-2 | cycleShare is exactly 0.0625 for wheat and 0.25 for rice; a wheat cycle's base yield is exactly 75 kg per acre and its certified seed need 1.6875 kg | `CropTests.CycleShare_PerSeasonRealism` |
| AC-3 | `Sow` succeeds on a vacant owned farm tile in season: cash falls by seed + fertiliser cost, posted to 5000 and 5010; the tile is occupied; a growth job is due at exactly `sownAt + growthMinutes × 60`. Each rejection reason (unowned, yard, occupied, out of season, no saved seed, insufficient funds) leaves the state hash unchanged | `CropTests.Sow_RulesAndCosts` |
| AC-4 | A crop becomes Ready exactly at its due time, emits `CropReady`, and its yield doesn't change however long it waits; `Harvest` moves exactly the frozen grams into the store and frees the tile | `CropTests.ReadyWaits_HarvestToStore` |
| AC-5 | The §3.4 example gives 70.74 kg to Fixed precision; with every factor at its maximum the yield equals exactly `yieldCap × Base` and the breakdown records that the cap applied | `YieldTests.Formula_AndCap` |
| AC-6 | `WaterFactor` gives 0.742857 (wheat at 20), 1.00 inside the band, and the floors at moisture 0 and 100 | `YieldTests.WaterFactor_Piecewise` |
| AC-7 | A crop whose growth spans a change of weather gets the exact time-weighted Weather factor (e.g. 2 h sun then 4 h heatwave for wheat: (2 × 1.00 + 4 × 0.85) ÷ 6 = 0.90) | `YieldTests.Weather_TimeWeighted` |
| AC-8 | With basic food, normal hours, health 1 and fatigue 0, the founder delivers exactly 7.2 h; at 25% day labour, 5.4 h go to the fields and 1.8 h × 1,125 = 2,025 coins are paid at the next daily pass (Dr Cash, Cr 4100) | `FounderTests.LabourHours_AndDayLabour` |
| AC-9 | Care ratio: 2 acres of wheat with 7.2 h supply give 1.10 (capped); 3 acres give 0.80; adding watering demand on the well acres in a heatwave lowers it accordingly; a crop's Care factor is the time-weighted mean across the changes | `CareTests.SharedPool_TimeWeighted` |
| AC-10 | Care, Water and Weather are identical whether a crop's growth is advanced in one call or in 500 random chunks (the spec 001 chunking check, with crops, trees and the founder over one game year) | `CropTests.ChunkingInvariance` |
| AC-11 | A crop sown between hourly passes gets factors from the rates in force at sowing until the next accrual point, and becomes ready exactly `growthMinutes × 60` s after sowing | `CropTests.ShortCrop_UsesCurrentRates` |
| AC-12 | Fertility after a wheat cycle with chemical fertiliser falls by exactly `(4 + 3) × 0.0625` = 0.4375, recorded under CropUse and Fertiliser; lentils raise it by 4 × cycleShare under Lentils; compost raises it by 2 × cycleShare | `CropTests.Fertility_FromCropsAndInputs` |
| AC-13 | Chemical fertiliser over cycles totalling 4 seasons of growth lowers fertility by 12 ± 0.5 points from the Fertiliser reason (ADR 0014) | `CropTests.Chemical_FourSeasons` |
| AC-14 | Pest risk equals the §3.8 product for a repeat count of 0, 1, 2 and 3 (capped at 2) and a wet share; spraying before the check halves it; over 10,000 seeded checks the outbreak rate is within 1 percentage point of the risk | `PestTests.Risk_Formula_AndRate` |
| AC-15 | An outbreak emits `PestWarning` at the check; spraying after it gives `CropHealth = 1 − (1 − h) × 0.3`; not spraying gives h; a severe outbreak gives 0 | `PestTests.Warning_AndCurativeSpray` |
| AC-16 | No severe outbreak is ever applied to a check that runs during an away advance (seeds forced to severe: the result is a normal outbreak at 0.40) | `PestTests.NoSevereWhileAway` |
| AC-17 | Saved seed ages by cycleShare per harvest: wheat seed after 16 saved cycles (age 1.0) gives 0.98, potato after 8 (age 2.0) gives 0.90, and both stop at their floors; sowing saved seed takes exactly the cycle's seed grams from the store; radish is rejected with `SeedCannotBeSaved` | `SeedTests.SavedSeed_AgesPerSeason` |
| AC-18 | Fatigue moves by the option's change (×0.8 for recovery in the hut) and stays within [0, 0.5]; health follows §3.11 exactly for basic, subsistence and long-hours cases and stays within [0.5, 1] | `FounderTests.Fatigue_Health_Dynamics` |
| AC-19 | At health 0.5, over 10,000 seeded days the sick-day rate is within 1 point of 25%; a sick day delivers 0 hours and emits `FounderSick` | `FounderTests.SickDays` |
| AC-20 | While away, the founder works normal hours whatever the chosen option; live, the chosen option applies | `FounderTests.Away_NormalHours` |
| AC-21 | A new game started at 10:00 pays basic living costs of exactly `18,000 × (345,600 − 36,000) ÷ 345,600` = 16,125.00 coins as a prepayment; changing to balanced mid-season refunds the unearned basic part and prepays balanced for the rest, and the season's Living costs expense equals the time-weighted mix | `FounderTests.LivingCosts_UpfrontAndChange` |
| AC-22 | When the season's living obligation is unpaid, the plan becomes subsistence (nutrition 0.80), no liability is created, and the previous plan returns at the next close that can pay it | `FounderTests.Subsistence_NeverStuck` |
| AC-23 | With `eatFromStore` on and 50 kg of radish (spoilage 0.25) and 200 kg of wheat in store, the living-cost payment takes radish first, then wheat, up to `foodShare × cost` at the trader's buying price, and cash pays exactly the remainder | `FounderTests.EatFromStore` |
| AC-24 | `PlantTree` capitalises the sapling (Dr 1700), sets the slot; the tree matures exactly `growthMinutes` later and then shades its field-edge tile (spec 002 AC-10) | `TreeTests.Plant_Mature_Shade` |
| AC-25 | A mature mango gets one 120 kg batch at the start of each Summer only, none while the previous batch is unpicked; a banana gets one batch every season; picking moves it to the store and adds its value to returns | `TreeTests.Fruiting_OnePerSeason` |
| AC-26 | A banana is spent after 4 picked batches: the slot is emptied, its book value posted to 5600, `TreeSpent` emitted; `RemoveTree` does the same at any time | `TreeTests.Lifespan_AndRemoval` |
| AC-27 | The payback season is reported exactly for a tree already paid back and for one not yet paid back | `TreeTests.Payback` |
| AC-28 | Every money movement in this spec reconciles: over 2 game years of seeded random play (sow, spray, harvest, sell via spec 004, trees, plan changes, day labour) the ledger reconciles at every season close | `CropTests.Ledger_Reconciles` |
| AC-29 | Performance (CI, Release): advancing 72 h with the default map fully sown, 8 trees and the founder takes under 20 ms | `PerfTests.Advance72h_WithCrops` (category `Perf`) |
| AC-30 | The golden replay `golden/003-farm.json` (sow, spray, harvest, trees, garden, plan changes, an away advance) reproduces its stored hash | `ReplayTests.Golden_003_Farm` |
| AC-31 | `Sow` uses certified seed and fertiliser from the store first and buys exactly the shortfall at today's input price; with enough stock it costs nothing | `CropTests.Sow_UsesStockFirst` |
| AC-32 | A garden bed gives `min(harvestKg, remaining)` per picking: 20 pickings of 5 kg radish use the 100 kg season output, the 21st `SowGarden` is rejected with `BedRested`, and the bed is available again at the next season start with 100 kg | `GardenTests.SeasonOutput_AndRest` |
| AC-33 | Garden crops grow in exactly their minutes, wait when ready, need no labour (the care ratio is unchanged by them), and the inherited garden seed lets the first sowing cost nothing | `GardenTests.Grow_Pick_NoLabour` |
| AC-34 | In Winter, a founder on subsistence loses exactly `noWarmClothingPenalty` more health a day than on the same plan in Summer; on basic, no penalty | `FounderTests.WarmClothing_Winter` |
| AC-35 | `BuyMedicine` pays 500 coins to 5250, raises health by 0.10 (capped at 1), and a second purchase the same day is rejected with `AlreadyTreatedToday` | `FounderTests.Medicine` |
| AC-36 | A new game starts with 20,000 coins, the basic plan, normal hours, `eatFromStore` on and full health (ADR 0030); the first living-cost payment uses the inherited grain first | `FounderTests.Start_State` |

## 9. Balancing hooks

**Anchors at the regional price** (spec 004 prices; full utilisation, all factors 1, certified seed, no fertiliser):

| Crop (per acre per season) | Output | Gross | Seed | Gross margin before labour |
| --- | --- | --- | --- | --- |
| Radish | 4,000 kg × 7 | 28,000 | 2,400 | 25,600 |
| Wheat | 1,200 kg × 25 | 30,000 | 1,215 | 28,785 |
| Rice | 1,500 kg × 22 | 33,000 | 600 | 32,400 |
| Potato | 7,000 kg × 8 | 56,000 | 14,000 | 42,000 |
| Lentils | 350 kg × 70 | 24,500 | 660 | 23,840 (and +4 fertility) |

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Net income of an average acre with hired labour (hand 36,000 a season ÷ about 2.5 acres = 14,400 an acre), competent play (about 75% of the anchor after soil, water, weather and idle time) | About 8,000–12,000 a season, so an acre at 1,20,000 costs 10–15 seasons of it (ADR 0028) | Economy model |
| The founder alone on about 2 of the inherited acres (neglected soil 0.875) | About 30,000–38,000 a season, above basic living costs (18,000) and close to full-time day labour (36,000), so farming your own land is worth it but not overwhelming | ±20% |
| Kitchen garden, 2 beds | About 200 kg (about 1,500 coins) of vegetables a season | ±30% |
| Chemical fertiliser over 4 seasons | −12 fertility (ADR 0014) | ±0.5 |
| Yield above 1.6× base | Never | 0 |
| Founder with basic food and normal hours | Tends about 2–2.4 acres of wheat at full care | ±0.5 acre |
| Share of harvests hit by an outbreak, rotated crops, unsprayed | 5–12% | Shape |
| Mango in a yard slot | Pays back its sapling in its first fruiting season | — |
| Banana on a field edge | Earns less than the 3% shade it costs the acre (yard slots suit short trees) | Shape |

**Pacing check against ADR 0030** (competent player; placeholders for the economy model):

| When | What happens | Cash |
| --- | --- | --- |
| Start | 20,000 coins; store: 600 kg certified seed potato, 75 kg wheat, 75 kg lentils, garden seed | 20,000 |
| Season 0 living costs | Basic, pro rata (about 16,100), part paid in stored grain | about −9,000 |
| Spring | Potato on about 2 acres, 3 cycles, about 1,200 kg an acre-cycle (soil 0.875, water about 0.9); the 2,000 kg store forces sales at the floor (5.30); seed bought after the stock runs out | about +38,000 sales, −10,000 seed |
| End of Spring | Hand's hut (about 15,000, spec 006) and a hand (36,000), with a moneylender loan of up to 25,000 | about 0–10,000 |
| Summer | Lentils on all 6 acres: about 12 hours a day needed, about 14 supplied (founder and hand), Care 1.1; about 8 cycles | about +80,000 sales; a profitable season |
| End of week 1 | The Hamlet gate is met: 6 acres harvested at Care ≥ 0.9, a hand's house, a profitable season | |

Without borrowing, the hand is affordable about half a season later, so the Hamlet comes at about 1.5 weeks. The balancer (F20.1) checks the target.

## 10. Open questions

All six were answered in the Sprint 1 review (2026-10-17):

1. **The Hamlet pace.** Resolved by ADR 0030 (the owner's idea): the founder inherits 6 protected farm acres, 20,000 coins and a store of seed and grain, and the Hamlet gate becomes "6 acres harvested in one season at average Care ≥ 0.9, a house for a hired hand, one profitable season". The week-1 challenge is labour, not land debt (pacing check in §9).
2. **Seasonal moisture.** Spec 002's hook was made realistic (Spring 30–50, Summer 15–35, Monsoon 65–85, Winter 40–60) and the economy model tunes the weather data to it (US-014). No rule change.
3. **Saved seed.** Seed ages per season of growth (cycleShare), not per cycle, with a per-crop loss and floor (cereals and lentils 0.02 / 0.90, potato 0.05 / 0.80); radish seed can't be saved. Tracking stays per crop kind (§3.9).
4. **Health.** Warm clothing in Winter (a penalty on subsistence) and medicine from the village chemist join Phase 1 (ADR 0031); unsafe water waits for the town's water network.
5. **Inputs.** Seed, fertiliser and spray can be bought ahead into the store (spec 004, ADR 0031); sowing and spraying use stock first, then buy the rest at today's price.
6. **The tiny radish harvest.** Resolved by the kitchen garden (§3.13, ADR 0031): quick greens in two beds with generous pickings up to a realistic season output; radish in the fields becomes a 2-hour crop (about 83 kg an acre a cycle).

**Also decided:** a crop-calendar card on the sow sheet (spec 008) and seasonal crop hints in the letters (spec 009) guide the player on which crop suits which season.

## 11. Cut first

1. Tree payback *Why?* (keep cost and returns).
2. Medicine (keep warm clothing).
3. Eat from own harvest.
4. Saved seed (certified only).
5. Curative spraying (keep preventive spraying only).
6. Sick days (keep health as a factor).
7. Bucket-watering labour on the well acres.
