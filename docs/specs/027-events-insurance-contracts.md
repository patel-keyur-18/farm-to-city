# 027. Events, insurance and forward contracts

- Status: Approved by the owner (2026-11-20): G1–G9 (§10) closed one per message on my recommendations; ADR 0141. The two UI previews (12 and 13, §7) are shown as artifacts that look exactly like the game and approved before any UI code.
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Events, disasters and town history (event families, event rules, insurance); Economy (contracts)
- Related ADRs: 0002 (the 72 h away cap), 0004 (the player never loses), 0014 (yield formula, pests), 0042 (weather spells), 0043 (*Why?* in words), 0057 (specs a sprint ahead), 0063 (gentle deaths), 0107 (Phase 3 plan, §13 scope), 0109 (taxes and policies), 0125 and 0130 (utilities: the water tower), 0127 (spec 026), 0136 (silage and fodder), 0139 (a pass that throws never stops the clock)
- Depends on specs: 003 (yield, pests), 011 (the trader's truck and floor price), 014 (households, sick days), 015 (HR events), 016 (businesses and their books), 017 (shops), 021 (treasury), 023 (clinic, coverage), 025 (utilities, water tower), 026 (reputation, inequality, approval: events read them)
- Stories: US-154 events (8), US-155 insurance (5), US-156 forward contracts (3); Sprint 24; save schema 22 (shared with spec 026)

## 1. Goal

The town's preparation is tested. Things happen that the player did not choose (a dry spell, a fire, a price swing, a family at the door), each with two or three choices whose cost is shown, and what the player prepared (irrigation, a water tower, a clinic, a cash reserve, insurance, a second supplier) changes how hard it lands. Insurance and forward contracts are the two ways to buy certainty: a small sure cost against a large uncertain one.

**Lesson:** a certain small cost versus an uncertain large one (insurance); hedging (a price fixed ahead protects you if the price falls and costs you if it rises); preparation and reserves; that insurance covers luck, not neglect.

## 2. Scope

**In (ADR 0107 §13):**
- **Events** (US-154): the event engine (one roll a day, at most two waiting, a default for every card, the away rules) and 19 events in the seven families: weather, economic, business, workforce, social, opportunity, disaster. Events start at the Township.
- **Disasters** (US-154): a **fire** (softer within the water tower's reach, covered by property insurance) and an **epidemic**, each signalled 48 hours ahead and never fired while you are away.
- **Insurance** (US-155): **crop insurance** (per crop sown, a season policy, PMFBY-like), **property insurance** (buildings on your own books, against fire) and **business insurance** (a business's building and its lost profit after a fire or a strike). Premiums are priced above the expected payout.
- **Forward contracts** (US-156): the trader offers a fixed price for a quantity of a staple next season; a shortfall is settled at the difference to the market; the price is never better than the expected price.

**Out** (with where it goes instead):
- River floods (need an embankment) and recessions: Phase 4.
- A fire station: the Town. Life and health insurance: not in Phase 3.
- Futures traded with anyone but the trader: not in Phase 3.
- The investor's offer event: its card and effects come with spec 028 (US-247, Sprint 25; corrected by ADR 0144, it said spec 029). This spec builds the engine it plugs into.
- Poaching and the other HR events stay as spec 015 has them (their own flow, not this engine).
- The Chronicle's own screen: spec 029 (this spec records and announces what happened).
- Festivals as an event: spec 026 (the player holds them).

## 3. Rules and formulas

Everything is Fixed-point (ADR 0018). Money is in 1/100 coin and indexed by the price index at the moment of the spend. Rounding: division rounds half away from zero unless a rule says otherwise. Each number has a *Why?* in words, never a factor (ADR 0043). Values live in `content/events.json`, `content/insurance.json` and `content/contracts.json`.

### 3.1 The engine (US-154)

| Rule | Behaviour | Units | Bounds |
| --- | --- | --- | --- |
| Starts at the Township | No event before the Township unlocks (the Village and Hamlet paces are measured against Phase 2's gates, ADR 0097, 0110, 0120). None in the first 2 days after the unlock. | era | |
| One roll a day, at 6:00 | A seeded roll (stream `Events`, key = day). A **minor** event comes with chance `minorChance` **0.9**; a **major** (★) with `majorChance` **1/16** (about one a game year). A major is drawn first; if one comes no minor that day. | chance | 0–1 |
| Which event | Drawn by weight among the events open to you now (§3.3). A kind does not repeat within a season. | weight | |
| At most 2 waiting | A third day's roll is skipped, so nothing piles up. | cards | `maxWaiting` 2 |
| A card, not a queue | A card shows 2–3 choices with their cost and a hint of the long-term effect, one marked **default** (the safest, named), and says what happens if you do not answer. | | |
| The clock | A minor card waits **24 real hours**, then takes its default and says so. A major waits **48 hours**. A disaster's warning is 48 hours and its strike comes at the end (§3.4). | hours | 24 / 48 |
| Away | A minor event takes its default at once and goes on the away card. Majors and disasters never roll while you are away; that day's roll is skipped, not saved up. | | |
| Every choice is a command | `AnswerEvent(eventId, choice)`, checked like any spend: funds, a refusal in words. | | |
| The four mercy rules | (1) A kind does not repeat within a season. (2) **At most one major a season**; after a major hits crops (drought, hail, waterlogging) no other crop major the next season. (3) No event in the first 2 days after the Township unlock, and none on a day a petition's promise falls due (spec 026). (4) A major that would cost over **40%** of your cash is not drawn: you can always afford the safe choice. | | |
| Seeded and deterministic | The same seed and the same commands give the same events (Rng stream `Events` = 21, keys: day, slot, kind). | | |

### 3.2 The catalogue (US-154)

An event is a row of data: a family, a gate, a weight (§3.3), 2–3 choices, a default. Each choice's effect is drawn from a **closed list of nine effect kinds**: `Cash`, `CropHealth` (an *event health* multiplier on the standing crops of a filter), `PriceShift` (a good, a percentage, days), `CostShift` (diesel, power or a business's inputs, a percentage, days or a season), `Happiness` (who, points, days), `Offer` (a worker, a family or a parcel offered), `Flag` (a named flag until a time), `Downtime` (a building or a machine shut for hours), `Grant` (an amount at the next close). No scripts: a new event is data and a string. ★ marks a major (48 h clock, one a season at most). **Default** in bold.

| Family | Event | Gate | Choices (cost → effect) |
| --- | --- | --- | --- |
| Weather | ★ **Drought** | Spring or Summer, after a dry spell | **Let it ride** → rain-fed standing crops −12% · Hire water tankers (120 an acre of rain-fed standing crop) → no loss |
| | **Heatwave** | Spring or Summer, a heatwave spell on | **Let it ride** → all standing crops −6% · Water at dawn (60 a standing field) → no loss |
| | ★ **Hailstorm** | during a storm spell, Spring or Monsoon | no warning: a third of standing fields (seeded) −35%; then **Keep the crop** · Clear and resow (new seed, the field starts again) |
| | ★ **Waterlogging** | Monsoon, 4+ hours of heavy rain in the last day | **Let it drain** → crops on wet fields −10% · Dig drains (1,500) → no loss |
| Economic | **Price boom** | you hold or grow a crop | one crop +20% at the trader for 3 days: **Hold** · Sell what you hold now |
| | **Price slump** | same | one crop −15% for 3 days: **Hold** · Sell anyway |
| | **Fuel spike** | you pay diesel or power | diesel and power +35% for the season: **Pay it** · Park your hired-out tractor (US-256) and save its diesel |
| Business | **Supplier strike** | you run a business | its inputs +30% for 3 days: **Pay the extra** · Pause the line 3 days · Second supplier (1,000: no extra, and a strike next season costs half) |
| | **Health inspection** | a food business | **Pay the fine (1,000)** · Fix the kitchen (3,000, no fine for a year) |
| | **Viral rush** | a shop | demand +40% for a day: **Carry on** (the stock you have serves about half of them: +20%) · Stock up (1,500 of ingredients, serve all: +40%) |
| | **Breakdown** | a machine or a business's equipment | down 24 h: **Wait** · Rush repair (2,500, back in 6 h) |
| Workforce | **Skilled migrant** | a vacancy on the hiring board | a 4-star applicant asks 20% over the going wage: **Pass** · Hire (no post fee) |
| Social | **Wedding** | you have households | **Send regards** · Attend and give a gift (500: that family +2 happiness, approval +1) |
| | **Dispute** | two households within reach | **Let it pass** (both −1 happiness for a season) · Mediate (1 h of your day, both +2) · Side with one (+3 and −3) |
| | **New family** | a free house | **Not now** · Welcome them (they move in) |
| Opportunity | ★ **Regional grant** | a profitable last season | **Skip** · Apply (400 for the paperwork: 6,000 at the next close if your books and credit are sound; the odds in words) |
| | ★ **Land auction** | a parcel for sale beside your land | **Pass** · Bid (85% of its land value; answer within a day) |
| | *Investor's offer* | spec 028 | not built here |
| Disaster | ★ **Fire** | §3.4 | §3.4 |
| | ★ **Epidemic** | §3.4 | §3.4 |

*Crop damage is one number:* an **event health** multiplier on a standing crop, multiplied into its CropHealth (ADR 0014). It is kept on the crop (`Crop.EventHealth`, 1 by default) and counts in the insured yield index (§3.5). Irrigated, drip and pumped fields are untouched by a drought; the card says why ("your pump keeps these 6 acres watered").

*Why these:* every family in the GDD's table has its events, each cost is a small real number against a season's profit (the Village's median farm profit is 46,000), each has a preparation that softens it, and the default always costs nothing so the player learns what paying to prevent a loss buys.

### 3.3 Weights by town state (US-154)

At 6:00 each eligible event has a weight `base × the modifiers below`, the product clamped to **0.25–4**. Base is **10** for every event, **4** for the disasters. The *Why?* on a card names the modifier that made it likely ("this came because it hasn't rained for 3 days").

| Event | Modifiers |
| --- | --- |
| Drought | ×2 if no rain hour in the last 3 days · ×1.5 more if none in the last 6 |
| Heatwave | gate: a heatwave spell is on · ×1.5 in Summer |
| Hailstorm | gate: a storm spell is on (the damage lands as the card comes: a third of the standing fields, rounded up, seeded; the choice is then to keep them or resow them) |
| Waterlogging | gate: 4+ hours of heavy rain in the last day |
| Price boom | ×2 for a crop whose price is below its season's norm |
| Price slump | ×2 for a crop whose price is above its season's norm |
| Fuel spike | ×1.5 in the season after the price index rose 5% or more |
| Supplier strike | ×1.5 if the town's happiness is below 55 · ×0.5 for a season after a second supplier |
| Health inspection | ×2 if the business's stars are under 3 · ×0.5 on good quality · ×0 for a year after the kitchen is fixed |
| Viral rush | ×(1 + stars ÷ 5) |
| Breakdown | ×3 for a machine whose maintenance you skipped (the same 4% against 0.5% a day as spec 013) |
| Skilled migrant | ×(1 + employer reputation ÷ 50) |
| Wedding | ×(your households ÷ 10), at most ×3 |
| Dispute | ×2 when inequality is high (spec 026) · ×1.5 when happiness is under 55 |
| New family | ×(livability score ÷ 50) (spec 026) |
| Regional grant | ×(business climate ÷ 50) (spec 026) |
| Land auction | ×1 (needs a parcel for sale beside your land) |
| Fire | ×2 in Summer, and only while a heatwave spell is on or just past; needs a building |
| Epidemic | ×2 if any of your households is outside a water reach · ×2 if there is no staffed clinic in reach |

### 3.4 Disasters (US-154)

Both are ★ majors in the same draw (weight 4 against 10), under the four mercy rules.

**The warning.** The card is the warning, **48 hours** before the strike. Its choices are the preparation. The strike lands at the first **live 6:00** after the 48 hours. If you were away at that 6:00 it **slides** to your next live 6:00, at most 3 days, then it passes and nothing is lost. A disaster never fires while you are away. There is no push notification.

**Fire.**
- **Gate:** you own at least one building. **Which:** seeded among your buildings (family houses, huts, the shed, the godown, business buildings, solar panels), weighted by book value. Civic buildings are the treasury's and are never picked.
- **Damage:** **35% of the building's book value**; ×0.5 if the **water tower** reaches it (400 m); ×0.5 if you took the prepare choice. There is no fire station until the Town.
- **Downtime:** a business building shuts for **2 days**; a house, hut or shed has none.
- **Warning choices:** **Do nothing** · *Clear dry straw, keep buckets ready* (800: halves the damage).
- **When it strikes**, a second card: **Repair at once** (the bill, back in 2 days) or **Repair slowly** (half the bill, 6 days out). If you cannot pay the bill the first is greyed with a reason. **Unanswered for 24 hours the card takes *Repair slowly* (owner, 2026-11-20, Preview 12 question 2)**, paying what cash allows if even half is short.
- **Books:** the repair bill goes to a new *Disaster loss* line under other costs, outside farm profit.

**Epidemic.**
- For **6 days** a household's sick-day chance is ×2.5, and ×0.5 of that where a staffed clinic covers the home (net ×1.25). Sick workers are off (spec 015). Only sick days change: nobody's health is lowered, so the `health < 0.7` death rule can never fire from it: **nobody dies from an epidemic** (ADR 0063).
- **Warning choices:** **Let it run** · *Hold a health camp* (25 a person, your gift, outside farm profit: ×1.5 instead of ×2.5) · *Close the school and the shops for 3 days* (no coins: ×1.5, shops −30% demand for the 3 days, lessons paused).
- The card shows what the clinic saved ("the clinic halved this for 38 families").

### 3.5 Crop insurance (US-155)

A **season policy** by one switch on the Money page and on the close card: *Crop insurance: On / Off*. It covers field crops sown **after** you switch it on (a pest warning already showing cannot be insured). Fodder crops and orchard trees are not insured. A crop's policy is fixed when it is sown (`Crop.Insured`, `Crop.SumInsured`, `Crop.Premium`).

| Rule | Behaviour |
| --- | --- |
| **What is insured: luck, not neglect.** | The insured yield index `I = Weather × CropHealth × EventHealth`. Soil, water, care, seed and fertiliser are not insured. |
| Sum insured | The sow sheet's harvest value at the trader's floor price when sown. |
| Trigger and payout | At readiness, if `I < threshold` (**0.80**, a 20% deductible), pay `sum insured × (0.80 − I)`, in cash, to farm income *Insurance claims*. |
| Premium | `sum insured × (pest part + event part) × loading`, charged at sowing from your cash. **Pest part** = the crop's pest risk at sowing × **0.23** (the average shortfall in an outbreak: 95% × 0.20 + 5% × 0.80), the risk read as `PestRules.Risk` with the sowing's repeats and the season's usual wet share (Spring 0.17, Summer 0.12, Monsoon 0.65, Winter 0.04, from the weather's weights), unsprayed. **Event part** = a flat **1.5%** for hail, drought and waterlogging (a placeholder for the balance pass). **Loading** = **1.30**. |
| Cash short | If cash cannot pay the premium the sowing goes uninsured and the away card says so (`CropNotInsured`). |
| Books | The premium is a farm cost *Crop insurance premiums*; a claim is farm income *Insurance claims* (not passive). Both on the season page with a *Why?*. |

*Examples (my estimates, confirmed at the balance pass):* wheat in Winter, sum insured 28,800: premium about 3.8% (about 1,100); a pest outbreak at health 0.6 (`I` about 0.58) pays about 6,300; hail (`I` about 0.63) about 4,900; a severe outbreak pays 0.80 × the sum insured. Rice in the Monsoon, sum insured 36,000: premium about 6.9% (about 2,500).

### 3.6 Property and business insurance (US-155)

Two policies; every building belongs to exactly one. Both are **season policies** bought at the close: you switch on, the premium is charged with the other dues (shown in *next season due*) and covers the season that begins. Switching on mid-season starts at the next close. An unpaid premium lapses the policy (no debt). **A risk already signalled is not insurable:** a policy that started after a fire's warning does not pay for that fire (the card says so). There is no refund and no no-claims bonus.

| | Property insurance | Business insurance |
| --- | --- | --- |
| Covers | the buildings on **your own books** (yard, huts, family houses, the cattle shed, the godown, solar panels) | a business's own building and equipment, and its lost profit |
| Paid from | your cash | **that business's cash** |
| Switch | the Money page | the business's sheet |
| Premium a season | **0.2%** of the insured book value | **0.2%** of its building and equipment book value + **3%** of its last season's net profit |
| Pays on a fire | **90%** of the repair bill (a 10% excess) | **90%** of the repair bill |
| Pays for lost days | none | **80%** of its average daily profit (last season's net profit ÷ days) for each day shut by a fire or by *Pause the line* |

*Worked example:* a mill with a building and equipment worth 100,000 that made 35,000 last season pays 200 + 1,050 = **1,250** a season; a fire's repair bill of 14,000 pays 12,600, plus 80% of 35,000 ÷ 4 × 2 days = 3,500, so **16,100** back; uninsured it costs 14,000 and 4,375 of profit.

*Books:* *Insurance premiums* (other costs, outside farm profit) and *Insurance claims received* (other income), beside *Disaster loss*, so the season page shows the net.

### 3.7 Forward contracts (US-156)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Which crops | The staples the trader buys by the kilo: wheat, rice, maize, mustard, lentils, potato. | 6 |
| The offer | At each season close, for each of your **top 2 crops** by last season's harvest, the trader offers one contract: **40%** of last season's harvest of that crop, rounded to 100 kg, for delivery in the **season after the one you are in**. A crop under **300 kg** gets no offer. You accept any time in the season you are in, or let it lapse. At most **2** contracts are open at once, one a crop. | 2 |
| The price | `expected price × 0.97`, never better than the expected price: the trader's floor price for the delivery season (the regional price with that season's factor, at today's price index, less transport). Fixed in coins a kilo; inflation during the lead is yours. | 3% |
| Delivering | By the normal selling you already do: in the delivery season, sales of that crop to the trader are paid at the **contract price** until the quantity is filled (the first kilos, never chosen). The trader's daily limit and the 4,000 kg truck still apply. | |
| A shortfall | At the close of the delivery season, `unfilled kg × max(0, the season's average floor price − the contract price)`, charged from your cash; the rest, if you cannot pay, carried as a due like unpaid tax. If the price fell, a shortfall costs nothing and the better price is lost. | |
| Books | Delivered kilos are crop sales at the contract price. The shortfall is a farm cost *Forward contract settlement*. The contract card shows "against the market: you gained or lost X". | |

*Why it is not riskless:* the 3% discount makes a contract slightly worse than waiting on average; a shortfall costs you when the price rose; you cannot pick which kilos count; the market price is the season's average, so it cannot be timed. An *accept every contract* bot ends no better than the median (§9).

## 4. Data

**New definitions** (`content/events.json`, `content/insurance.json`, `content/contracts.json`, loaded by `Sim.Content`; every value bounded):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| engine | `minorChance`, `majorChance` | Fixed | 0.9, 0.0625 | 0–1 | |
| engine | `maxWaiting`, `minorHours`, `majorHours`, `warningHours`, `slideDays` | int | 2, 24, 48, 48, 3 | 1–10, 1–168 | |
| engine | `graceDaysAfterUnlock`, `cashShareCap` | | 2, 0.4 | | mercy rules |
| engine | `weightClamp` | [Fixed, Fixed] | [0.25, 4] | | |
| event | `id`, `family`, `major`, `baseWeight`, `gate`, `modifiers`, `choices`, `default` | | §3.2–3.3 | | strings `event.<id>.*` |
| choice | `id`, `costKey`, `effects[]` | | | ≤ 3 per event | effects from the nine kinds |
| fire | `damageShare`, `towerFactor`, `prepareFactor`, `prepareCost`, `downtimeDays`, `slowRepairShare`, `slowRepairDays` | | 0.35, 0.5, 0.5, 800, 2, 0.5, 6 | | |
| epidemic | `days`, `sickFactor`, `clinicFactor`, `campFactor`, `closeFactor`, `campPerPerson`, `closeDays`, `closeShopsDemand` | | 6, 2.5, 0.5, 1.5, 1.5, 25, 3, 0.3 | | |
| cropInsurance | `threshold`, `pestShortfall`, `eventPart`, `loading` | Fixed | 0.80, 0.23, 0.015, 1.30 | | |
| propertyInsurance | `rate`, `excess` | Fixed | 0.002, 0.10 | | |
| businessInsurance | `rate`, `profitShare`, `excess`, `dailyProfitShare` | Fixed | 0.002, 0.03, 0.10, 0.80 | | |
| contracts | `crops`, `topCrops`, `shareOfHarvest`, `minKg`, `roundKg`, `discount`, `maxOpen` | | 6 staples, 2, 0.40, 300, 100, 0.03, 2 | | |

**New state** (save schema 22, `EventsState`, `InsuranceState`, `ContractsState`; Sprint 24's single bump, shared with spec 026):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| EventsState | `Waiting` | list (≤ 2) | id, kind, raisedAt, expiresAt, target (building, field, crop, business), params |
| EventsState | `History` | list (8) | kind, answeredAt, choice (or the default), cost or gain |
| EventsState | `LastSeason[kind]`, `MajorSeason`, `CropQuietUntil` | | the mercy rules |
| EventsState | `Effects` | list | kind, target, factor or amount, until (a price shift, a drying change, a happiness lift, a flag) |
| EventsState | `Disaster` | one or none | kind, target, warnedAt, strikeAt, slides |
| EventsState | `Downtime` | list | building or machine id, until |
| InsuranceState | `CropOn`, `PropertyOn`, `PropertyStart` | bool, bool, long | |
| InsuranceState | `Business[id]` | | on, start |
| InsuranceState | `Claims` | list (4) | policy, amount, at |
| Crop | `EventHealth`, `Insured`, `SumInsured`, `Premium` | Fixed, bool, long, long | |
| ContractsState | `Offers`, `Open`, `History` | lists | crop, kg, price, delivery season, filled, status |

An older save loads with the engine idle (no event before the Township), no policy on, no offers and `EventHealth` 1 on every crop, so a town is unchanged until its first Township roll or close (fixture: `schema22-risk.ftc`, written in Sprint 24).

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `AnswerEvent` | event id, choice | `UnknownEvent`; `EventClosed`; `InvalidStep`; `InsufficientFunds` | applies the choice's effects; removes the card |
| `SetCropInsurance` | on | `NoTownship` | switches the policy for crops sown after now |
| `SetPropertyInsurance` | on | `NoTownship`; `NoBuildings` | on from the next close; off at once |
| `SetBusinessInsurance` | business id, on | `UnknownBusiness`; `BusinessClosed` | the same, for one business |
| `AcceptContract` | offer id | `UnknownOffer`; `OfferLapsed`; `TooManyContracts`; `AlreadyContracted` | opens the contract |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `EventRaised` | id, kind, major | a 6:00 roll | the card, the bell |
| `EventAnswered` / `EventDefaulted` | id, kind, choice | an answer / its clock | the away card, history |
| `DisasterWarned` / `FireStruck` / `EpidemicBegan` / `EpidemicEnded` | kind, target, strikeAt | the warning, the strike | the card, the map, alerts |
| `PolicyStarted` / `PolicyLapsed` / `PremiumCharged` | policy, amount | a close, a sowing | the season page |
| `ClaimPaid` / `CropNotInsured` | policy, amount, the crop | readiness, a sowing | the away card, the season page |
| `ContractOffered` / `ContractAccepted` / `ContractFilled` / `ContractSettled` | crop, kg, price, gain or loss | a close, an answer, a sale, the delivery close | the Store sheet, the season page |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Pay to prevent (tankers, drains, dawn watering) | 60–1,500 | no crop loss | cash lower now | none |
| Let it ride (the default) | nothing | crop loss of 6–12% (hail 35%) | the claim, if insured | none |
| Crop insurance on | a premium of about 4–7% of a crop's value at each sowing | a claim when luck takes more than 20% | cash lower at sowing | off for later sowings |
| Property or business insurance on | 0.2% of book value (+3% of profit) a season | 90% of a fire's repair bill (+ 80% of lost days) | the premium is paid even if nothing happens | off at once; no refund |
| Accept a forward contract | nothing now | the price fixed for 40% of last season's harvest | a shortfall costs the difference if the price rose | none |
| Repair a fire at once or slowly | the bill, or half of it | 2 or 6 days out | the insured share comes back at once | none |

## 7. UI

Both previews are exact artifacts that look like the game, approved before any UI code (the rule). The pictures and the new icons are rendered in Blender (about 10 icons and one map sprite: the seven event families, a flame with smoke, a shield for insurance, a scroll for contracts).

**Preview 12: events**
```
+---------------------------- Drought ----------------------------+
|  [picture]   This came because it hasn't rained for 3 days.   ? |
|                                                                  |
|  [ Let it ride            no cost · rain-fed crops -12% ]  (default) |
|  [ Hire water tankers     120 an acre · 8 acres = 960   ]            |
|   Your pump keeps 6 acres watered.                               |
|   If you do not answer: Let it ride in 47 h                      |
+------------------------------------------------------------------+
```
- A card in the existing frame; a disaster warning has an amber frame and a countdown ("A fire risk in 47 h"), and says which preparation helps ("the water tower reaches 3 of your buildings").
- Up to 2 waiting; closing a card keeps it on the bell with a count. A Menu **Events** sheet lists what is waiting and your last 8 answers with what each cost or paid.
- The away card has a line for each default taken. The map draws a fire (flames and smoke) on the building for the 2 days it is shut; hail and heatwave use the weather look already there.

**Preview 13: insurance and contracts**
```
+---------------------------- Insurance ---------------------------+
| Crop insurance        On   about 4% of a crop's value             ? |
| Property insurance    On   9 buildings · 2,100 a season           ? |
| Claims    Fire, the shed: 12,600 paid                             ? |
+--------------------------- Trader's offers ----------------------+
| Wheat  1,000 kg  22.0 a kg  (expected 22.7)  delivery Winter  [Accept] |
| Your contracts: Rice 800 kg · 300 filled · 500 left · 3 days      |
+------------------------------------------------------------------+
```
- **Money page:** an *Insurance* section with the two switches (no confirm card: they cost nothing when tapped), the premium and the last 4 claims. **A business's sheet:** a *Business insurance* row. **The sow sheet:** one line when crop insurance is on ("Insured: premium 1,100, pays if luck takes more than 20%"). **The close card and *next season due*:** the premiums.
- **Store sheet, trader section:** *Trader's offers* with **Accept** (a tap confirm card that says it is binding) and *Your contracts* (kilos filled, left, days to go).
- **The season page:** *Insurance premiums*, *Insurance claims received*, *Disaster loss*, *Forward contract settlement*, each with a *Why?*.

*Why?* rows: why an event came (its modifier), each choice's cost, the odds in words, the fire's damage (book value, tower, preparation), the epidemic's clinic saving, a premium's parts (the pest part, the event part, the loading), a claim's `I` and threshold, a contract's price against the expected price, the delivery and the settlement.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | One roll a day at 6:00, seeded: the same seed and commands give the same events; a minor about 9 days in 10 and a major about 1 in 16; none before the Township or in its first 2 days | `EventEngineTests.Roll_Deterministic_Rates`, `EventEngineTests.NotBeforeTownship` |
| AC-2 | At most 2 events wait (a third day's roll is skipped); a minor card takes its default after 24 h, a major after 48 h; the default is named and costs nothing | `EventEngineTests.Waiting_Defaults` |
| AC-3 | Away, a minor takes its default and is reported; no major or disaster rolls while away and the day is not saved up | `EventEngineTests.AwayRules` |
| AC-4 | The four mercy rules: no repeat of a kind in a season; one major a season and the crop-major quiet; no event on a promise day; a major over 40% of cash is not drawn | `EventEngineTests.MercyRules` |
| AC-5 | Each event's gate and weight follow §3.3 (the modifiers, the clamp); a *Why?* names the modifier | `EventWeightTests.Modifiers`, `EventWeightTests.Clamp` |
| AC-6 | Every choice's effect applies as §3.2 says and is refused in words when it cannot be paid; irrigated, pumped and drip fields are untouched by a drought | `EventEffectTests.Weather`, `EventEffectTests.Economic`, `EventEffectTests.BusinessSocialOpportunity` |
| AC-7 | The event health multiplier is kept on the crop, reaches the yield, and is 1 by default (an old save) | `EventEffectTests.EventHealth_Yield` |
| AC-8 | A disaster is signalled 48 h ahead, strikes at the first live 6:00, slides while you are away (at most 3 days, then passes), never fires while away | `DisasterTests.Warning_Strike_Slide` |
| AC-9 | Fire: the building by book value, 35% damage, halved by the tower's reach and by preparation, a business building shut 2 days, the repair choices, the books | `DisasterTests.Fire_Damage_Repair` |
| AC-10 | Epidemic: sick chance ×2.5 (×0.5 in a staffed clinic's reach), the camp and the closure, health never below 0.75, nobody dies of it | `DisasterTests.Epidemic` |
| AC-11 | Crop insurance: the policy is fixed at sowing, the premium formula, the cash-short sowing goes uninsured, the claim at readiness, luck covered and neglect not | `CropInsuranceTests.Premium`, `CropInsuranceTests.Claim_Luck_NotNeglect`, `CropInsuranceTests.CashShort` |
| AC-12 | Property and business insurance: premiums at the close with the dues, start at the next close, the signalled-risk rule, 90% of a repair bill, 80% of lost days, a lapse on an unpaid premium | `PropertyInsuranceTests.Premium_Start_Lapse`, `PropertyInsuranceTests.Claim`, `PropertyInsuranceTests.SignalledNotInsurable`, `BusinessInsuranceTests.Claim_LostDays` |
| AC-13 | Forward contracts: the offer's crops, quantity and price (never above the expected price), accept, delivery at the contract price on the first kilos, the shortfall at the season's average difference, carried when unpaid | `ContractTests.Offer`, `ContractTests.Delivery`, `ContractTests.Shortfall`, `ContractTests.NeverBetterThanExpected` |
| AC-14 | Premiums, claims, losses and settlements keep the ledger exact; insurance is never passive income; nothing flows to you from the treasury | `RiskLedgerTests.Reconciles` |
| AC-15 | Schema 22 round-trips; an older save loads with the engine idle, no policy and no offers; the fixture carries on through two closes | `MigrationTests.Schema22_Risk` |
| AC-16 | Every number has a *Why?* in words; no raw factors | `WhyTests.EveryNumberHasWhy` (over the event, insurance and contracts situations) |
| AC-17 | The sheets and cards respond to scripted touch in the Editor | the stories' Editor tours |
| AC-18 | A town that answers nothing (every default) still works: it levels, no state goes negative, no zero-time loop over the new commands | `RiskTests.LeftAlone`, `GateTests.Risk` |
| AC-19 | An *insure everything* bot and an *accept every contract* bot are no better on the median than an uninsured, uncontracted one | `GateTests.InsureEverything`, `GateTests.AcceptEveryContract` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Minor and major event frequency | about 0.9 a day and about 1 a game year | ±0.1 a day, ±0.3 a year |
| Cost of an unprepared year | a drought or hail year costs a farm 8–20% of a season's profit; insured it costs the premium | measured by bots, reported |
| Crop insurance | premium 3–7% of the sum insured; expected payout below the premium (loading 1.30) | measured payout ÷ premium within 0.7–0.95 |
| Property and business insurance | expected payout below the premium; a fire year pays more than it cost | measured |
| Forward contracts | the contract price 3% below the expected; the hedged median no better than the unhedged | ±1% |
| The Township's pace (day 101 competent) | unchanged within 2 days: events start at the Township | ±2 days |
| Archetype bots (ADR 0016) | within 20% on net worth and happiness | 1.13 holds |
| A town left alone | solvent and levelled after a year of defaults | none short |
| `eventPart` (1.5%) | set from the measured hail, drought and waterlogging shortfall | ±0.5 points |

## 10. Open questions

Each is closed with the owner, one per message, with my recommendation, before code.

- **G1 Engine. Closed (owner, 2026-11-20, my recommendation):** one roll a day at 6:00, at most 2 waiting, defaults on a 24 h or 48 h clock, away rules (a minor takes its default, majors and disasters never roll away), every choice a command.
- **G2 Catalogue. Closed (owner, 2026-11-20, my recommendation):** 19 events in seven families, effects from a closed list of nine kinds, the numbers of §3.2.
- **G3 Weights and the start. Closed (owner, 2026-11-20, my recommendation):** the modifiers of §3.3 with the 0.25–4 clamp, the four mercy rules, events from the Township unlock only.
- **G4 Disasters. Closed (owner, 2026-11-20, my recommendation):** fire (35%, the tower and preparation halve it, 2 days' downtime, two repair choices) and an epidemic that nobody dies of, each warned 48 h ahead and sliding while away.
- **G5 Crop insurance. Closed (owner, 2026-11-20, my recommendation):** insured yield index of luck, 0.80 threshold, premium from the crop's own pest risk with a 1.30 loading, bought by a switch that covers later sowings.
- **G6 Property and business insurance. Closed (owner, 2026-11-20, my recommendation):** two policies with every building in one, season policies at the close, 90% of a repair bill and 80% of lost days, the signalled-risk rule.
- **G7 Forward contracts. Closed (owner, 2026-11-20, my recommendation):** staples only, the trader's offer for the top 2 crops, 40% of last season's harvest, 3% below the expected price, delivery by normal selling, a shortfall at the season's average difference.
- **G8 Screens. Closed (owner, 2026-11-20, my recommendation):** event cards and an Events sheet, the insurance section on the Money page and on the business and sow sheets, the trader's offers on the Store sheet; Previews 12 and 13 first.
- **G9 Scale in Sprint 24. Closed (owner, 2026-11-20, my recommendation):** 16 points beside spec 026's 11 and the hills' 5 (32); one schema 22; the build order and the cut line of §11.

## 11. Cut first

If Sprint 24 runs over, cut in this order:
1. The *Events* sheet's history (the card and the bell stay).
2. Business insurance's strike cover (fire stays).
3. The social and workforce events (wedding, dispute, new family, skilled migrant); their slots stay in the data.
4. **Forward contracts** (US-156, 3 points).
5. The economic events' price shifts (boom, slump, fuel spike).

**Never cut:** the engine, its mercy rules and the away rules (nothing fires while you are away); crop insurance; fire with property insurance; every number's *Why?*; the schema 22 fixtures; the exploit checks (AC-19).
