# NNN. The crop ladder: new crops in every season

> **Draft.** The spec number is given when the ad hoc phase starts (after Phase 2's last spec). Move this file to `docs/specs/NNN-crop-ladder.md` on approval. Every gap in §10 is closed.

- Status: Gaps G1–G14 closed by the owner (2026-10-26); awaiting approval of the whole spec at the phase's start (US-L1)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Time and world simulation (Task clock, Seasons); Land, farming and livestock (Crops); UI/UX and screens (the sow sheet); Roadmap
- Related ADRs: the crop-ladder ADR (draft beside this file); 0021 (crop calendar), 0008 (per-season realism), 0014 (yield), 0016 (balance rule), 0031 (seasonal input prices), 0033 (land payback), 0034 (ready grace, spoilage), 0048 and 0050 (the hand's choice), 0051 (fertility shown to one decimal), 0055 and 0058 (rice and potato in 15 h; the long crop ≥ 12 h), 0062 (neighbours grow mustard)
- Depends on specs: 003 (crops; every rule used unchanged), 004 (goods and the market), 008 (the sow sheet and the crop-calendar card), 009 (season hints), 011 (supermarket orders), 006 (the hand's choice), 007 (saves match content by id)
- Amends: spec 003 §4 (the crop tables), spec 008 §3.7 (row order, ready time, an hourly figure), spec 009 §3.4 (the hint names at most 5 crops)

## 1. Goal

Whenever the player looks in, there's a crop that will be ready by their next visit, whether that's in an hour, this afternoon, this evening or tomorrow. Every crop stays true to the valley's real farming.

**Lessons:**
- On the same land in the same season, a short crop pays in small, frequent harvests and a long crop in a few big ones. Choose by when you'll be back.
- Vegetables earn well but need hands. Grains and oilseeds need fewer.
- Legumes feed the soil.

## 2. Scope

**In:**
- **Six field crops** with their produce and seed goods: fenugreek (methi), amaranth (chaulai), okra (bhindi), green peas (matar), maize (makka) and mustard (sarson). All are available from the Homestead.
- **Their data:** crop, goods, seasonal prices, the trader's daily limits, local demand, pest risk, strings.
- **The sow sheet and the crop-calendar card:**
  - This season's crops are listed in order of growing time.
  - Each row shows its ready time and what the sowing earns for each hour it grows.
  - Each crop has a one-line note.
- **The season hint** names at most five crops.
- **"Legumes fed the soil"** replaces "Lentils fed the soil" in *Why?*.
- **Crop lists in code:** the presentation and the bots stop naming crops one by one.
- **Balance:** the balance rule (§3.4), two new bots and every existing gate.
- **Art:** each new crop's four growth stages and its produce icon, in Blender.

**Out** (and where it goes instead):
- **Other crops:**
  - Onion: with the granary (F13.3), where its storage and price story belongs.
  - Tomato, sugarcane and cotton: later phases (GDD roadmap).
- **The kitchen garden** is unchanged: no methi or amaranth beds.
- **Crop by-products:** maize straw or stover as fodder (spec 012 is unchanged), maize flour (the mill grinds wheat, spec 016), pressing mustard oil. All later.
- **Onboarding steps** (spec 009 §3.2) are unchanged. The first session still sows a long crop of at least 12 h.
- **Any change to a formula** in spec 003, 004, 006 or 011.

## 3. Rules and formulas

### 3.1 Nothing in the farm's maths changes

Every rule of spec 003 applies to the new crops as written: cycle share and per-season realism (§3.1), sowing (§3.2), growth, ready grace and spoilage (§3.3), the yield formula and its cap (§3.4), water (§3.5), Care (§3.6), accrual (§3.7), pests and spraying (§3.8), seed (§3.9) and fertility (§3.10). Spec 004's market, spec 006's hand, spec 011's truck and orders and spec 005's ledger apply unchanged.

**Why this keeps the economy whole.** Every crop quantity is per acre per season, and a cycle takes `growthSeconds ÷ SecondsPerSeason` of it. So a short crop doesn't earn more than a long one; it earns the same season's worth in more, smaller harvests:

| Crop | One sowing (all factors 1) | At the regional price | Sowings in a season |
| --- | --- | --- | --- |
| Methi, 1 h | 17.2 kg | about 275 | 96 |
| Okra, 4 h | 87.5 kg | about 1,225 | 24 |
| Potato, 15 h | 1,093.8 kg | about 8,750 | 6.4 |

A player who comes back late loses growing time on a short crop (the field sits ready), and a player who's away all day loses little on a long one. That's the choice the ladder offers.

### 3.2 The ladder

**Rungs:** quick (up to 1 h), short (1–3 h), half-day (3–5 h), day (6–9 h), overnight (12 h or more). After this spec, every season has a sowable field crop (fodder aside) on every rung:

| Season | Quick | Short | Half-day | Day | Overnight |
| --- | --- | --- | --- | --- | --- |
| Spring | **Methi 1 h** | Radish 2 h | **Okra 4 h** | **Maize 6 h** | Potato 15 h |
| Summer | **Amaranth 1 h** | Radish 2 h | **Okra 4 h** | Lentils 8 h | Rice 15 h |
| Monsoon | **Amaranth 1 h** | Radish 2 h | **Okra 4 h** | **Maize 6 h** | Rice 15 h |
| Winter | **Methi 1 h** | Radish 2 h | **Peas 4 h** | Wheat 6 h, lentils 8 h, **mustard 8 h** | Potato 15 h |

New crops are in bold.

### 3.3 Realism rules for the content

These govern the content values in §4 and any later tuning (ADR 0027: values tune in `content/`; these rules don't change without re-approval).

| Rule | Behaviour |
| --- | --- |
| Sowing windows | Real Indian sowing months, mapped to the game's seasons (ADR 0021: Spring is late rabi and early zaid, about Feb–Apr; Summer is zaid, Apr–Jun; Monsoon is kharif, Jun–Oct; Winter is rabi, Oct–Jan). The real basis is in the crop-ladder ADR |
| Game time | By the real days to the first harvest, in bands anchored on radish (40–45 days, 2 h) and wheat (120–150 days, 6 h): **quick greens** cut at 25–35 days, 1 h; **vegetables** first picked at 45–75 days, 4 h; **grains and oilseeds** at 90–140 days, 6–8 h. Rice and potato keep 15 h (ADR 0055) |
| Season yield | At or below a real acre's output in a quarter of continuous cropping, as radish is (ADR 0008) |
| Price | A farm-gate price per kg at the start index. Produce is **cheaper in its harvest season** and dearer out of it (`seasonalFactor` 0.85–0.95 against 1.05–1.20). Seed is **1.10 in its sowing seasons and 0.95 otherwise** (ADR 0031's pattern) |
| Labour | Hand-picked vegetables (okra, peas) need the most hours; grains and oilseeds the fewest, as on a real farm |
| Spoilage | Leafy greens like the garden's (0.35 a day); vegetables 0.15–0.20; dry grain and oilseed under 0.01 |
| Seed saving | Only open-pollinated grain and legume seed can be saved (peas, mustard). Greens are cut before they flower (like radish). Okra and maize seed is hybrid, and saved hybrid seed doesn't breed true |
| Household food | Everything new is eaten at home except mustard seed (`edible: false`). Its oil needs a press |

### 3.4 The balance rule

Measured on two price bases, both with every factor at 1 (soil 0.875 on the floor basis, as on an inherited acre) and certified seed:

1. **Regional:** for an acre kept busy all season, at the regional price.
2. **Floor:** one sowing's profit per growing hour, at the trader's floor with the season's `seasonalFactor` for the crop and its seed. These are the figures the sow sheet and the hand use (spec 008 §3.7, ADR 0048).

```
GM(crop)  = seasonYieldPerAcre × basePrice − seedPerAcrePerSeason × seedBasePrice
Net(crop) = GM(crop) − labourPerAcrePerDay × ClockDef.DaysPerSeason × FounderDef.dayLabourWagePerHour
PerHour(crop, season) = (BaseCycle × 0.875 × Floor(good, season) − seedCycle × seedPrice(season)) ÷ growthHours
```

| Check | Rule |
| --- | --- |
| Never the leader | For every season a new crop is sown in: `GM(new) ≤ max GM`, `Net(new) ≤ max Net` and `PerHour(new, season) ≤ max PerHour(season)` over the existing crops of that season. "Existing" means radish, wheat, rice, potato and lentils |
| Never a dud | `GM(new) ≥ 0.9 × min GM(existing)` (≥ 21,456), so no new crop is a trap for a player who sows it |
| Its own niche | Each new crop is the best pick (by spec 003's sow-sheet profit at the floor, ADR 0048) for at least one realistic case: a 1 h return, a dry rain-fed acre, a light-labour season or low fertility. The balancer reports which (§9) |

With the §4 values:

| Crop (acre, season) | Gross | Seed | GM | Labour at 1,125 an hour | Net | Season leader's GM / Net |
| --- | --- | --- | --- | --- | --- | --- |
| Methi | 26,400 | 3,000 | 23,400 | 13,500 | 9,900 | Spring and Winter: potato 42,000 / 26,250 |
| Amaranth | 28,000 | 750 | 27,250 | 13,500 | 13,750 | Summer and Monsoon: rice 32,400; Net lentils 14,840 / rice 14,400 |
| Okra | 29,400 | 6,000 | 23,400 | 20,250 | 3,150 | Spring potato; Summer and Monsoon rice 32,400 |
| Peas | 36,000 | 4,320 | 31,680 | 18,000 | 13,680 | Winter: potato 42,000 / 26,250 |
| Maize | 25,000 | 2,000 | 23,000 | 13,500 | 9,500 | Spring potato; Monsoon rice 32,400 / 14,400 |
| Mustard | 28,000 | 150 | 27,850 | 9,000 | 18,850 | Winter: potato 42,000 / 26,250 |

For comparison (spec 003 §9): radish 25,600 / 12,100, wheat 28,785 / 15,285, rice 32,400 / 14,400, potato 42,000 / 26,250, lentils 23,840 / 14,840. Okra's Net with hired labour (3,150) sits in ADR 0033's band for a hired acre (2,000–5,000): vegetables pay the family that picks them.

**On the floor basis** (coins per growing hour; leaders in bold):

| Season | Existing crops | New crops |
| --- | --- | --- |
| Spring | **potato 178**, radish 176 | maize 176, methi 172, okra 171 |
| Summer | **rice 288**, radish 214, lentils 200 | amaranth 207, okra 144 |
| Monsoon | **rice 258**, radish 201 | amaranth 194, maize 165, okra 157 |
| Winter | **potato 305**, wheat 270, lentils 211, radish 137 | mustard 234, peas 205, methi 148 |

Spring is the tight season: potato is cheap at its rabi harvest (0.85) and its seed dear (1.10), so it earns only about as much an hour as radish. The first draft's yields put okra (227), maize (208) and methi (190) above it; the yields in §4 were lowered to keep them just under (owner, 2026-10-26: "not too profitable"). In Spring every crop now earns about the same an hour, so the choice is when you'll be back, your hands and your soil. On a dry rain-fed acre in Summer, where rice suffers for water, amaranth and okra can lead: that is their niche, and real.

### 3.5 For an average player

| Rule | Behaviour |
| --- | --- |
| Nothing new to learn | The new crops are sown, tended, harvested and sold exactly like the old ones |
| Affordable from the first minute | One sowing's certified seed costs at most about 250 coins (okra); 20,000 coins start |
| No trap | No new crop loses money at the floor price on an average acre (sow-sheet profit > 0 at fertility 45, moisture inside its band). Fast-spoiling crops say so on the sow sheet. The household eats the most perishable food first (spec 003 §3.11) |
| Easy to compare | The sow sheet (§3.6) orders crops by growing time, says when each will be ready, and gives each sowing's earnings for each hour it grows, so a 1 h and a 15 h crop compare fairly |
| Small harvests stay sellable | One acre's quick-green harvest (about 17 kg) is far below the trader's daily limit for that good, so it never gluts on its own |
| No flood of notifications | A crop's "ready" notification is grouped with others within 30 minutes and never sent in quiet hours (spec 007 §3.5); a field under a hand's standing order sends none |
| Not stale | Prices move daily and by season, greens spoil and vegetables need hands, so the best crop for an acre changes with the season, the day's price and how many people work for you |

### 3.6 The sow sheet and the crop-calendar card (amends spec 008 §3.7)

| Column or rule | Behaviour |
| --- | --- |
| Order | This season's sowable crops by growth time (ties in `crops.json` order), then the crops of other seasons (greyed) in `crops.json` order. Fodder keeps its place rules (spec 012 §3.8b) |
| Ready time | "Ready at 16:40" when the ready time falls today; "Ready tomorrow 07:10" when it falls tomorrow; else "Ready in N days". From `now + growthSeconds`, in the device's local time (the sim's time of day, spec 001) |
| An hour's growing (new) | "About N an hour": the row's profit (spec 008 §3.7) ÷ growth hours, rounded to 10 coins, ties away from zero. *Why?*: "This sowing's profit about P over H hours. A shorter crop earns about as much an hour if you come back when it's ready." |
| Note (new) | One line from strings (`crop.<id>.note`), for every crop, old and new. For example, methi: "Quick greens: small harvests every hour. Spoils fast once picked."; okra: "Picked every other day: needs plenty of hands."; mustard: "Needs little water or work. Sold for oil, not eaten at home." |

### 3.7 The season hint (amends spec 009 §3.4)

`hint.sow` names at most **5** of the season's sowable field crops, in growing-time order, then "and N more" (`hint.sowMore`). For example, Winter: "You can sow methi, radish, peas, wheat, lentils and 2 more now." The "best price" and "dearer seed" remarks are computed as before over every good, the new ones included.

### 3.8 Fertility wording

The reason `FertilityReason.Lentils` (saved value 3, unchanged) now covers every crop with a negative `fertilityPerSeason`: lentils, peas and methi. Its *Why?* line becomes "Legumes fed the soil this season" (`why.fertility.lentils`), and the lesson line names "compost, legumes and rest".

### 3.9 Where the new crops appear without new rules

| System | How |
| --- | --- |
| The hand's choice (ADR 0048, 0050) | Considered like any in-season crop, by the sow-sheet profit per growing second |
| Supermarket orders (spec 011 §3.3) | Eligible when in season and 18 h or shorter (all six). The greens order stays garden-only |
| Eating from the store (spec 003 §3.11) | Edible goods eaten most-perishable first; mustard never |
| Notifications, *While you were away*, the store, the market, the mandi | By good and crop id from content |
| The crop-calendar card, every *Why?* | Rows built from content |
| Neighbours' fields (spec 010 §3.7, Could) | Some neighbours grow maize in Spring and the Monsoon, and mustard in Winter |

### 3.10 Saves

Saves name content by id and remap it on load (spec 007 §3.1, ADR 0041). A save from before this spec loads into a build with the new content:
- The new goods start with no stock, their market at the regional price, and no history.
- The new crops start with no saved-seed age.
- No schema bump unless the migration test (AC-13) shows a per-good field that the remap doesn't fill. In that case, a schema step fills it with the new-game defaults.

## 4. Data

All values are placeholders the economy model tunes within §3.3 and §3.4 (ADR 0027).

**`content/crops.json`** (`CropDef`, no new fields):

| Field | Methi | Amaranth | Okra | Peas | Maize | Mustard |
| --- | --- | --- | --- | --- | --- | --- |
| `id` / `nameKey` | `methi` / `crop.methi` | `amaranth` / `crop.amaranth` | `okra` / `crop.okra` | `peas` / `crop.peas` | `maize` / `crop.maize` | `mustard` / `crop.mustard` |
| `outputGood`, `seedGood` | methi, seed_methi | amaranth, seed_amaranth | okra, seed_okra | peas, seed_peas | maize, seed_maize | mustard, seed_mustard |
| `sowSeasons` | spring, winter | summer, monsoon | spring, summer, monsoon | winter | spring, monsoon | winter |
| `growthMinutes` | 60 | 60 | 240 | 240 | 360 | 480 |
| `seasonYieldPerAcre` (kg) | 1,650 | 2,000 | 2,100 | 1,800 | 1,250 | 500 |
| `seedPerAcrePerSeason` (kg) | 30 | 3 | 4 | 48 | 8 | 1.5 |
| `moistureMin`, `moistureMax` | 35, 65 | 30, 75 | 40, 70 | 35, 60 | 40, 70 | 25, 55 |
| `labourPerAcrePerDay` (h) | 3.0 | 3.0 | 4.5 | 4.0 | 3.0 | 2.0 |
| `fertilityPerSeason` | −1 (restores) | 2 | 3 | −3 (restores) | 5 | 3 |
| `canSaveSeed` | false | false | false | true | false | true |
| `savedSeedLossPerSeason`, `savedSeedFloor` | — | — | — | 0.02, 0.90 | — | 0.02, 0.90 |

New crops are appended after `sorghum`, so existing crop indices don't move.

**`content/goods.json`** (`GoodDef`):

| Good | `kind` | `basePrice` | `elasticity` | `spoilagePerDay` | `dailyVolatility`, `dailyBand` | Other |
| --- | --- | --- | --- | --- | --- | --- |
| methi | produce | 16 | 1.0 | 0.35 | 0.04, 0.12 | `sales`: crop |
| amaranth | produce | 14 | 1.0 | 0.35 | 0.04, 0.12 | crop |
| okra | produce | 14 | 1.0 | 0.20 | 0.04, 0.12 | crop |
| peas | produce | 20 | 1.0 | 0.15 | 0.04, 0.12 | crop |
| maize | produce | 20 | 0.4 | 0.005 | 0.015, 0.05 | crop |
| mustard | produce | 56 | 0.4 | 0.003 | 0.015, 0.05 | crop; `edible`: false |
| seed_methi | input | 100 | | 0.001 | | |
| seed_amaranth | input | 250 | | 0.001 | | |
| seed_okra | input | 1,500 | | 0.001 | | |
| seed_peas | input | 90 | | 0.001 | | |
| seed_maize | input | 250 | | 0.001 | | |
| seed_mustard | input | 100 | | 0.001 | | |

**`content/region.json`** (`RegionDef`; seasons in the order Spring, Summer, Monsoon, Winter):

| Good | `seasonalFactor` | `traderBuyPerDay` (kg) | `demandPerDay` (kg) | `valleySupplyPerDay` | `importCapacityPerDay` |
| --- | --- | --- | --- | --- | --- |
| methi | 0.95, 1.15, 1.15, 0.85 | 150 | 60 | 54 | 6 |
| amaranth | 1.10, 0.95, 0.90, 1.10 | 150 | 50 | 45 | 5 |
| okra | 1.00, 0.90, 0.95, 1.20 | 300 | 120 | 108 | 12 |
| peas | 0.95, 1.20, 1.20, 0.85 | 300 | 100 | 90 | 10 |
| maize | 0.95, 1.05, 0.90, 1.05 | 2,000 | 200 | 180 | 20 |
| mustard | 0.95, 1.05, 1.05, 0.95 | 500 | 40 | 36 | 4 |
| seed_methi | 1.10, 0.95, 0.95, 1.10 | | | | |
| seed_amaranth | 0.95, 1.10, 1.10, 0.95 | | | | |
| seed_okra | 1.10, 1.10, 1.10, 0.95 | | | | |
| seed_peas | 0.95, 0.95, 0.95, 1.10 | | | | |
| seed_maize | 1.10, 0.95, 1.10, 0.95 | | | | |
| seed_mustard | 0.95, 0.95, 0.95, 1.10 | | | | |

Valley supply is 0.9 × demand and import capacity 0.1 × demand, as for every existing good.

**`content/farming.json`** (`PestRiskDef.baseRisk`): methi 0.03, amaranth 0.03, okra 0.08 (fruit borer, yellow vein mosaic), peas 0.05 (powdery mildew), maize 0.06 (fall armyworm), mustard 0.06 (aphids).

**`content/strings/en.json`:**
- `crop.<id>` and `good.<id>`: "Methi", "Amaranth", "Okra", "Green peas", "Maize", "Mustard" (names: §10 G11).
- `good.seed_<id>`: "Methi seed" and so on.
- `crop.<id>.note` for all 13 crops, old and new.
- `sow.readyAt`, `sow.readyTomorrow`, `sow.readyInDays`, `sow.perHour`, `why.sow.perHour`, `hint.sowMore`.
- `why.fertility.lentils` and `why.fertility.lesson` reworded (§3.8).

**State:** none new. Per-good and per-crop arrays grow with content (§3.10).

## 5. Commands and events

None new. `Sow`, `Harvest`, `Spray`, `ClearField`, selling and orders take the new crop and good ids as they are.

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| A quick crop (methi, amaranth) | Seed, about 10–30 coins a sowing | A small harvest in an hour; the field is idle if you're late | Spoils fast in store; the household eats it first | Next hour |
| A half-day vegetable (okra, peas) | Seed, about 180–250 coins | A good harvest by the afternoon or evening | Labour: Care falls if hands are short. Peas feed the soil | Next sowing |
| A day crop (maize, mustard) | Seed, about 12–125 coins | A harvest by the evening; stores well | Maize wears the soil (5 a season); mustard needs little water or work | Next sowing |
| Ladder or long crop | — | Short crops pay if you come back; long ones if you're away | — | Every sowing |

## 7. UI

The sow sheet in Spring at 15:00, on a loam acre at fertility 45 and moisture 45, with §4's values (spec 008 §3.7's formula at the floor; potato 15 h).

```
Sow on North acre (loam, fertility 45.0, moisture 45)                 Spring
+--------------------------------------------------------------------------+
| Methi       1 h   Ready at 16:00          good    Profit about 170       |
|   Quick greens: small harvests every hour. Spoils fast once picked.      |
|   About 170 an hour · 3.0 h a day · Soil 45.0 → 45.0        [Sow · 30]   |
| Radish      2 h   Ready at 17:00          good    Profit about 350       |
| Okra        4 h   Ready at 19:00          good    Profit about 680       |
|   Picked every other day: needs plenty of hands.                         |
|   About 170 an hour · 4.5 h a day · Soil 45.0 → 44.9       [Sow · 250]   |
| Maize       6 h   Ready at 21:00          good    Profit about 1,060     |
| Potato     15 h   Ready tomorrow 06:00    good    Profit about 2,670     |
|   About 180 an hour · 3.5 h a day · Soil 45.0 → 44.2   [Sow · from store] |
| Wheat · Winter only    Peas · Winter only    Rice · Summer, Monsoon  ... |
| Free hours today: 3.4 of 7.2                                     [Why?]  |
+--------------------------------------------------------------------------+
```

- The rows' *Why?* lines are spec 008 §3.7's, plus "About N an hour" (§3.6).
- The crop-calendar card's season grid gains the six rows.
- Store, market, mandi, orders and away-card rows need no layout change: they list goods from content.

## 8. Acceptance criteria

Sim tests are in `tests/Sim.Core.Tests`; view tests in `tests/Sim.View.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Every non-fodder crop (old and new) has a produce output good with `sales` crop, an input seed good, a `seasonalFactor` for both, a `traderBuyPerDay`, `demandPerDay`, `valleySupplyPerDay` and `importCapacityPerDay` for its output, a pest `baseRisk`, and strings `crop.<id>`, `crop.<id>.note`, `good.<id>`, `good.<seed>`; removing any one fails loading or the test, naming it | `ContentTests.CropLadder_Complete` |
| AC-2 | Every season has a sowable non-fodder crop on every rung of §3.2 (≤ 60, 61–180, 181–300, 301–540, ≥ 720 minutes) | `ContentTests.CropLadder_EverySeason` |
| AC-3 | For each new crop, a season of back-to-back cycles at every factor 1 harvests `seasonYieldPerAcre` and uses `seedPerAcrePerSeason` to within one cycle's rounding (per-season realism) | `CropTests.CropLadder_SeasonTotals` |
| AC-4 | Each new crop sows only in its `sowSeasons`; outside them `Sow` is rejected with `OutOfSeason`, the state hash unchanged, and the *Why?* names its seasons | `CropTests.CropLadder_SowWindows` |
| AC-5 | Saved seed of peas and mustard can be sown and ages per spec 003 §3.9; methi, amaranth, okra and maize are rejected with `SeedCannotBeSaved` | `SeedTests.CropLadder_SavedSeed` |
| AC-6 | A peas cycle raises fertility by exactly 3 × cycleShare and a methi cycle by 1 × cycleShare under reason `Lentils`; a maize cycle lowers it by 5 × cycleShare under `CropUse`; the *Why?* line reads "Legumes fed the soil this season" | `CropTests.CropLadder_Fertility`; `FarmViewTests.Fertility_LegumesLine` |
| AC-7 | With eating from the store on and only mustard and maize in store, the daily pass eats maize and never mustard | `FounderTests.CropLadder_MustardNotEaten` |
| AC-8 | From the loaded content, every new crop meets §3.4's checks in every season it's sown in: not above that season's leader on GM, Net or the floor's profit per growing hour, and at least 0.9 × the lowest existing GM | `GateTests.CropLadder_BalanceRule` |
| AC-9 | The ladder bot (§9) ends at 0.90–1.20 × the competent bot's median net worth over 8 seasons and 100 seeds; the diversifier at most 1.20 × | `GateTests.CropLadder_Bots` |
| AC-10 | No seed of the ladder bot's runs harvests more than a season's realistic output on any acre (the fresh-start check's over-cap count is 0) | `GateTests.CropLadder_NeverOverCap` |
| AC-11 | In the Monsoon the supermarket's eligible crops are radish, rice, amaranth, okra and maize (crops of 18 h or less in season, `crops.json` order); the greens order still draws only spinach or coriander | `OrderTests.CropLadder_Eligible` |
| AC-12 | The hand's `BestCrop` considers every in-season non-fodder crop, the new ones included, and picks by the same profit-per-second rule (a test sets the prices so each new crop wins once) | `PeopleTests.BestCrop_ConsidersNewCrops` |
| AC-13 | The latest schema fixture save, written before this content, loads: the new goods have no stock and their regional price; saving and loading again gives the same state hash; play continues with the new crops sowable | `MigrationTests.CropLadder_OldSaveLoads` |
| AC-14 | The golden replays (001, 003, 004, 005) reproduce their stored hashes unchanged (they run on frozen content) | existing `ReplayTests.Golden_*` |
| AC-15 | The sow sheet in Spring at 15:00 lists methi (ready at 16:00), radish (17:00), okra (19:00), maize (21:00), potato ("ready tomorrow 06:00"), then the other seasons' crops greyed in `crops.json` order | `SowSheetTests.CropLadder_OrderAndReadyAt` |
| AC-16 | Each row's "About N an hour" equals its profit ÷ growth hours, rounded to 10 coins, ties away from zero, and has a *Why?* | `SowSheetTests.CropLadder_PerHour` |
| AC-17 | The Winter hint names methi, radish, peas, wheat and lentils, then "and 2 more" | `GuideViewTests.Hint_NamesAtMostFive` |
| AC-18 | Every crop in `crops.json` has sprites `crop_<id>_g1`, `_g2`, `_g3`, `_ready` and `icon_good_<id>` in `sprites.json` | `ContentTests.EveryCropHasSprites` |
| AC-19 | The every-number-has-a-*Why?* sweep passes with the new rows | existing `WhyCoverageTests` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| The competent bot's median net worth after 8 seasons | Within 5% of the value before this spec (its own crops are unchanged; a hand's picks may change) | ±5% |
| Hamlet pace (`GateTests.HamletPace`) | Median day unchanged | 5–9 (the gate) |
| Stress bots (`MoneyLoops_StressBots`, `Stress_Debt`, `PeopleStress`, `Phase1_StressBots`, `Dairy_Bots`) | Unchanged | Their gates (≤ 1.20; absentee ≥ 0.80) |
| **Ladder bot** (new): checks in every 1–2 hours from 07:00 to 23:00 and sows the crop that will be ready by its next check-in; the overnight crop at 23:00 | About the competent bot. Short crops lose a little growing time when it's late | 0.90–1.20 × |
| **Diversifier bot** (new): every acre a different in-season crop, sold through every outlet, to exploit separate demand curves | Not a money loop | ≤ 1.20 × |
| Truck and orders (`TruckAndOrders`) | Orders spread over more crops; the share holds | Orders < 20% of sales; truck full < 1 day in 10 |
| Prices on the rails (`BoundedPrices`) | None | 0% |
| New crops in the hand's picks (competent bot with a hand, 8 seasons) | Some, not most (report only) | About 10–40% of his sowings |
| Each new crop's niche (§3.4) | Reported by the balancer: the case where it's the best pick | One case each |
| Spring's crops per growing hour at the floor | All within a few percent of potato (171–178 in §3.4) | No new crop above potato |

## 10. Open questions

All fourteen gaps were closed with the owner one at a time on 2026-10-26.

- **G1. The crop list. Closed (owner, 2026-10-26): option A, all six:** methi, amaranth, okra, green peas, maize and mustard. Mustard is cheap to draw (its middle stage exists, ADR 0062) and gives Winter a low-water, low-labour crop. Amaranth is the only realistic quick green for Summer and the Monsoon. Onion waits for the granary (F13.3). Amaranth, then mustard, are still first to cut if the sprint runs over (§11).
- **G2. The game times. Closed (owner, 2026-10-26): option A,** banded by real days to the first harvest (§3.3): methi and amaranth 1 h, okra and green peas 4 h, maize 6 h, mustard 8 h. Strictly proportional times were rejected: okra and peas would sit beside radish, the 4 h rung would vanish, and the times would read oddly ("2 h 12 min"). The economy model may tune the times inside their bands without re-approval (ADR 0027).
- **G3. The balance rule. Closed (owner, 2026-10-26): option A,** §3.4's three checks: never above a season's leader on GM or Net, never below 0.9 × the weakest existing GM, and a niche of its own, reported by the balancer. It keeps land payback (ADR 0033), the Hamlet pace and the hand's economics unchanged by construction; `GateTests.CropLadder_BalanceRule` checks it from content so later tuning can't break it silently. Rejected: a 10% allowance above the leader (every old gate would need re-tuning), GM only (a low-labour crop could lead once labour is counted), and no rule.
- **G4. The sow sheet compares crops fairly. Closed (owner, 2026-10-26): as recommended.** Rows in growing-time order, the ready time, "About N an hour" (profit ÷ growing hours, the hand's own measure, ADR 0048) with its *Why?*, and a note per crop (§3.6). Amends spec 008 §3.7.
- **G5. The hand's choice. Closed (owner, 2026-10-26): keep the rule** (ADR 0048: profit per growing second, seed and fertiliser only). §3.4's floor check keeps every new crop at or below each season's leader, so his picks on a normal acre stay with the existing crops; on a dry Summer acre amaranth or okra may win, which is realistic. The bots measure it (`PeopleStress`, the hand's-picks report); counting labour would be a spec 006 change, raised only if a gate fails.
- **G6. Saved seed. Closed (owner, 2026-10-26):** peas and mustard can be saved (0.02 a season, floor 0.90); methi and amaranth can't (cut before they flower); okra and maize can't (hybrid seed doesn't breed true).
- **G7. Household food. Closed (owner, 2026-10-26):** mustard seed is not eaten at home (`edible: false`: its oil needs a press); maize is (makki ki roti).
- **G8. Wording. Closed (owner, 2026-10-26):** "Legumes fed the soil this season" for lentils, peas and methi; the saved reason value is unchanged (§3.8).
- **G9. The season hint. Closed (owner, 2026-10-26):** at most 5 crops in growing-time order, then "and N more" (§3.7).
- **G10. Neighbours. Closed (owner, 2026-10-26):** some neighbours grow maize in Spring and the Monsoon and mustard in Winter (US-L7, Could: first to cut).
- **G11. Names. Closed (owner, 2026-10-26):** English first, as for the existing crops ("Methi" is already the English name, so: Methi, Amaranth, Okra, Green peas, Maize, Mustard); the Hindi name opens the crop's note ("Bhindi: picked every other day…"), and spec 009 explains village words on first use.
- **G12. The kitchen garden. Closed (owner, 2026-10-26):** unchanged; no methi or amaranth beds (ADR 0031's season output stays as it is).
- **G13. Phase and milestone. Closed (owner, 2026-10-26):** one sprint right after Phase 2's gate, numbered next in line (S19 as planned today), under the existing `Phase 2 · Hamlet & Village` milestone (it extends Phase 2's farm; `CLAUDE.md` allows only existing milestones).
- **G14. Saves. Closed (owner, 2026-10-26):** no schema bump expected; `MigrationTests.CropLadder_OldSaveLoads` runs on the sprint's first day, and a schema step with new-game defaults is added only if it finds a per-good field the id remap doesn't fill.

**Also settled with the owner's closing guidance (2026-10-26):** "make sense, align with the game's core idea, not stale, achievable by an average player, not too profitable, as realistic as possible with the existing calculations". A final check found the first draft's okra, maize and methi out-earned potato per hour in Spring at the floor; their yields were lowered (methi 1,800 → 1,650, okra 2,600 → 2,100, maize 1,450 → 1,250 kg) and okra's labour set to 4.5 h, and §3.4 now checks the floor basis too (AC-8).

## 11. Cut first

If this spec runs over, drop these first, in order:

1. Neighbours growing the new crops (§3.9).
2. The crop notes for the five existing crops (keep the new ones).
3. Amaranth (radish covers Summer's and the Monsoon's quick rung at 2 h).
4. Mustard (Winter keeps wheat and lentils on the day rung).
5. The diversifier bot (keep the ladder bot and every existing gate).
