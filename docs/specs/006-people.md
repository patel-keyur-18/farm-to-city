# 006. People: day labourers, the resident hand and standing orders

- Status: Approved (2026-10-18), with the review answers in §10; amended by the Sprint 5 review (2026-10-19, ADR 0039)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Citizens and workforce (Hiring starts in Homestead, Wages); Land, farming and livestock (Standing orders); Player journey and eras (Homestead, the Hamlet gate); Data model
- Related ADRs: 0022 (hiring from Homestead), 0008 (season contracts paid upfront, accrual), 0010 (labour, Care), 0030 (the Hamlet gate: a hand's house), 0031 (minimum prices), 0032 (time-weighted labour), 0033 (hired-labour economics), 0002 (away rules), 0009 (never stuck)
- Depends on specs: 001 (scheduler, RNG, away), 002 (yard tiles, buildings), 003 (the Care pool, sowing and harvesting), 004 (the store, offers with a minimum price, the trader), 005 (prepaid wages, obligations, depreciation)

## 1. Goal

The founder runs out of hours at about 2 acres; hiring help is the first step in growing a farm into a business, and the player learns that wages are a fixed cost paid upfront, that hired labour pays only where the work earns more than it costs, and that trust is built by hiring the same people again.

**Lessons:** fixed versus variable labour costs (a season contract against day labour); hiring under uncertainty (skills shown as ranges); a prepaid wage is an asset that becomes an expense day by day; delegation through rules (standing orders) that keep working while you're away.

## 2. Scope

**In:**
- **Day labourers** (ADR 0022): a roster of neighbours from nearby villages; a seasonal pool of who is free each day; hiring for the rest of the day, paid upfront; their hours join the Care pool.
- **Regulars:** a labourer hired often asks to move to the village, as a resident-hand applicant whose skill and traits are known.
- **The hand's hut:** a building in the labour quarters beside the yard (ADR 0040), built in a day for about 15,000 coins, capitalised and depreciated.
- **The resident hand:** applicants walk in while a hut stands empty, or come faster after a job is posted (at the market wage or above it); skill shown as a range and one trait; an interview reveals another; an offer at the wage, accepted or declined.
- **The season contract:** prepaid wages, renewal at each season close (spec 005 step 6), lapse when it can't be paid, letting a hand go with a refund.
- **The hand's work:** hours a day by skill and traits, into the Care pool; skill grows with experience.
- **Standing orders** for a hand: keep a crop on a field (re-sow it in season), harvest when ready, pick ripe fruit, and a sell rule (store, offer at a minimum price, or sell to the trader). They run live and while away.
- **Population:** each resident hand is a citizen; the town's population counts them.

**Out** (and where it goes instead):
- The full hiring board (posting jobs, negotiation, reference checks, probation, benefits, HR events, quitting, poaching): Phase 2 (F11.4, GDD Citizens and workforce).
- Families, households, needs and happiness of hands: Phase 2 (F11.1–F11.3). In Phase 1 a hand is a single worker without needs.
- Health, fatigue and sick days of hands: Phase 2. Only the founder has them in Phase 1 (spec 003).
- Orders for the founder (the founder's own fields are tended through the Care pool and the player's taps): later, if playtests ask for it.
- The Hamlet unlock itself (checking the gate, the letter, the era change): spec 009 and F09.3. This spec provides the hut, the hand and the counts the gate reads.
- The panels' layout: spec 008. This spec defines their content.

## 3. Rules and formulas

Money is `long` in 1/100 coin; factors are `Fixed` and round to nearest, ties away from zero (spec 001 §3.1). Pro-rata amounts round down (integer division). Seeded draws use the spec 001 RNG with the streams named below.

### 3.1 Labour into the Care pool (spec 003 §3.6)

```
supplyPerDay = FounderFieldHours  +  Σ hired day labourers today × LabourPoolDef.hoursPerDay
                                  +  Σ hands under contract × HandHours(hand)
HandHours(hand) = HandDef.hoursPerDay × (HandDef.productivityBase + HandDef.productivityPerSkill × skill ÷ 100) × Π traitHoursFactor
```

- Defaults: `hoursPerDay` 8 for both; `productivityBase` 0.70, `productivityPerSkill` 0.50, so a hand of skill 50 delivers 8 × 0.95 = **7.6 hours a day**, skill 30 delivers 6.8 and skill 70 delivers 8.4. Traits: hardworking ×1.10, lazy ×0.85, others ×1.00.
- Hired hours join and leave the pool at accrual points (spec 003 §3.7, ADR 0032): a hire, a contract start, renewal, lapse or dismissal, and the daily pass when day labourers go home. A change gains only the time it was in force.
- With a skill-50 hand, the founder (7.2) and the hand (7.6) supply about 14.8 hours a day, enough for the ADR 0030 Summer plan (lentils on 6 acres: about 12 hours plus about 1.5 of carrying on the well acres, Care about 1.1).

### 3.2 Day labourers (ADR 0022)

| Rule | Behaviour |
| --- | --- |
| Roster | `LabourPoolDef.rosterSize` neighbours (default 8), created at a new game with a seeded name and one trait (stream People, counter = roster index). They never move away while they commute |
| Who is free today | At each daily pass, a seeded shuffle of the roster (stream DayLabourPool, counter = day index); the first `LabourPoolDef.availablePerDay[season]` are free today. Defaults Spring 2, Summer 4, Monsoon 3, Winter 2: scarce in the rabi and kharif harvests (Spring and Winter) |
| Hire | `HireDayLabourers(count)` hires the first `count` free labourers who aren't already hired today, for the rest of the day. Rejected with `NoneAvailable` if fewer are free |
| Pay | Upfront, pro rata for the rest of the day: `LabourPoolDef.dailyWage × (nextDailyPass − t) ÷ 86,400`, Dr 5100 Hired labour, Cr Cash. No prepayment: it is the day's cost. `InsufficientFunds` if cash is short |
| Wage | `dailyWage` 9,375 coins a game day: a hand's season wage ÷ 4 days (7,500) with a 25% premium (ADR 0022, ADR 0039 §11). A game day stands for about 22 real days, so this is about ₹420 a real day (ADR 0008) |
| Work | Each adds `hoursPerDay` (8) to the Care pool until the next daily pass, live or away: their assigned task is "tend the fields" (ADR 0039). They need no housing and aren't counted in the population |
| Economics | A day labourer costs more than an acre of potato or wheat earns from their hours (ADR 0033): hiring by the day is for rescuing a busy harvest's Care, not for routine work |

### 3.3 Regulars

| Rule | Behaviour |
| --- | --- |
| Time worked | Each roster member adds the seconds they were hired (`secondsWorked`), pro rata like their pay (ADR 0039) |
| Asks to move in | When a roster member's `secondsWorked` reaches `LabourPoolDef.regularThresholdDays` (default 6) × 86,400 (six full day-equivalents) and the founder has an empty hand's hut, they ask to move in: they join the applicants (§3.5) with their **skill and both traits known exactly**, at the asked wage for their skill. Emits `RegularAsksToMoveIn`. Once per person; they leave the roster when hired |
| No hut | The request waits until a hut stands empty (the count is kept) |

### 3.4 The hand's hut

| Rule | Behaviour |
| --- | --- |
| Where | In the inherited **labour quarters** (terrain `quarters`, tile 37, beside the 2-acre yard; ADR 0040): `huts.placesPerAcre` (2) hut places; at most `HandDef.maxHandHuts` (2) huts in Phase 1. Never on a farm acre or in the yard. The quarters can't be sold or foreclosed, so a hut is never lost with its land. Rejected with `NotQuarters`, `NoRoom` (the quarters are full) or `TooManyHuts` |
| Build | `BuildHandHut(tileId)`: `BuildingDef(hand_hut).value` (15,000 coins: materials and the builders' wages, upfront, ADR 0008), capitalised to 1600 Buildings (spec 005 §3.3). A construction job of `BuildingDef.buildHours` (24: about 3 real weeks) |
| Ready | When the job ends the hut houses `BuildingDef.housesHands` (1) hand. Emits `HandHutBuilt`. Depreciated straight-line over `usefulLifeSeasons` (60) from the next season close |
| *Why?* | "Materials 15,000; your family builds it" |
| Why about 15,000 | A one-room kutcha hut with a tin or tiled roof costs about ₹40,000–80,000 to build; the game's 15,000 is set by ADR 0030's pacing (the hut and the hand's first season come to about 51,000, against about 42,000 in hand at the end of the first Spring, so a small moneylender loan bridges the gap) and stands for materials with the family's own labour on the build |
| The Hamlet gate | "A house for a hired hand" (ADR 0030) is met by a finished hand's hut (spec 009 reads it) |

### 3.5 Applicants for a resident hand

| Rule | Behaviour |
| --- | --- |
| When | While at least one finished hand's hut is empty and fewer than `HandDef.maxApplicants` (3) are waiting, a new applicant arrives `HandDef.applicantEveryHours` (12) ± a seeded 0–6 hours after the last (stream Applicants, counter = applicant id). The first arrives that long after the hut is finished. Emits `ApplicantArrived` |
| Profile | A seeded name, a true farming skill in `[skillMin, skillMax]` (20–70), and two traits from `TraitDef` (distinct). Shown: the skill as a range `[skill − skillRangeHalfWidth, skill + skillRangeHalfWidth]` clamped to 0–100 (±15) and the first trait |
| Interview | `Interview(applicantId)` reveals the second trait. Free; once per applicant (`AlreadyInterviewed`) |
| Asked wage | `HandDef.marketWage × (wageBase + wagePerSkill × skill ÷ 100) × PriceIndex`, rounded to whole coins. Defaults 30,000 × (0.80 + 0.40 × skill ÷ 100): skill 50 asks 30,000 a season (about ₹10,000 a month with the hut, ADR 0039 §11), skill 30 asks 27,600, skill 70 asks 32,400 |
| Offer | `OfferContract(applicantId)` at the asked wage. Seeded acceptance (stream Applicants, counter = applicant id, draw 2) with probability `HandDef.acceptChance` (0.85): accepted → §3.6; declined → the applicant leaves (`OfferDeclined`) |
| Leaving | An applicant not hired within `HandDef.applicantWaitDays` (2) leaves (`ApplicantLeft`). Applicants never arrive or leave while away: the queue waits (ADR 0002: events needing a decision wait for the player) |
| Post a job | `PostJob(wageOffer)` sends word through the village and the trader (owner decision, 2026-10-18). Needs an empty hut **or one being built**. Costs `JobPostDef.cost` (200 coins, Dr 5700, Cr Cash) and lasts until a hand is hired or `JobPostDef.days` (4) pass (`JobPostExpired`). While it lasts applicants come twice as often (`applicantEveryHours` × `JobPostDef.rateFactor` 0.5, jitter halved). `wageOffer`: **market** (as walk-ins) or **aboveMarket**: the wage is `JobPostDef.aboveMarketFactor` (1.10) × the asked wage, applicants' skill is drawn from `[skillMin + 10, skillMax + 10]` (30–80) and they accept with `JobPostDef.aboveMarketAccept` (0.95). One post at a time (`AlreadyPosted`). Paying above market brings better workers sooner (GDD Wages) |

### 3.6 The season contract (ADR 0008)

| Rule | Behaviour |
| --- | --- |
| Start | On acceptance the hand moves into an empty hut, becomes a citizen (population + 1), and the rest of the season is prepaid: `wage × (seasonEnd − t) ÷ S` (Dr 1110 Prepaid wages, Cr Cash; expensed daily to 5100, spec 005 §3.2). `InsufficientFunds` at the offer if cash is short; the applicant waits |
| Renewal | At each season close (spec 005 §3.4 step 6, in contract id order, after loans and living costs) the next season's wage is paid upfront. The wage is renegotiated: `asked wage` for the hand's skill now, at today's price index; a **loyal** hand keeps their old wage. Emits `ContractRenewed` |
| Lapse | If the renewal can't be paid, the contract lapses (`ContractLapsed`): the hand stops working (their hours leave the Care pool) but stays in the hut for up to `HandDef.lapseGraceDays` (2). `RenewContract(handId)` pays the **whole** season's wage, the waiting days included (back pay, ADR 0039: the days already passed are expensed to 5100 at once, the rest prepaid), and resumes work. A contract still lapsed at the next season close ends. If not renewed in time the hand leaves (`HandLeft`, population − 1, the hut empties). No debt is ever created (ADR 0009) |
| Letting go | `DismissHand(handId)` at any time: the prepayment is closed (spec 005 `ClosePrepayment`: the unearned part refunded), less severance: none in the hand's first season (probation), afterwards `HandDef.severanceShare` (0.10) × the season wage, Dr 5100, Cr Cash out of the refund (never more than the refund). The hand leaves. Emits `HandDismissed` |
| Away | A paid contract keeps working while away; renewal at a close that falls while away is paid if cash allows, otherwise it lapses and the grace waits for the player's return (the hand doesn't leave while away) |

### 3.7 Skill and traits

| Rule | Behaviour |
| --- | --- |
| Skill growth | At each season close a hand under contract gains `HandDef.skillGainPerSeason` (3) skill (×1.5 if **ambitious**), up to 100. Their hours and their asked wage at the next renewal rise with it |
| Traits (Phase 1 set) | hardworking (hours ×1.10), lazy (hours ×0.85), loyal (keeps their wage at renewal), ambitious (skill grows ×1.5; asks 10% more at renewal), careless (an order's harvest is sold with `Overflow.SellRestToTrader` even if the rule says store; shown in *Why?*), steady (no effect: a neutral draw). Drawn evenly. The GDD's other four (creative, frugal, sociable, perfectionist) come in Phase 2 with their systems (F11.4) |

### 3.8 Standing orders

Any hand under contract can have orders, from Homestead (ADR 0022; the GDD now says so). An order belongs to one hand and covers one target. The grammar:

```
order    := field-order | tree-order
field-order := KEEP crop ON tile [WITH seed fertiliser] THEN sell-rule
tree-order  := PICK slot THEN sell-rule
sell-rule   := STORE | OFFER AT minPrice | SELL TO TRADER
```

Examples: "keep wheat on the north field with certified seed and compost; offer above 26 coins, otherwise keep it in store", "pick the mango tree; sell to the trader".

| Rule | Behaviour |
| --- | --- |
| Set | `SetStandingOrder(handId, order)` replaces the hand's order for that target; `ClearStandingOrder(handId, target)`. A hand holds up to `HandDef.maxOrders` (6) orders. Rejected with `NoHand`, `NotUnderContract`, `UnknownTarget`, `TooManyOrders`, or spec 003's crop checks (`UnknownCrop`, `SeedCannotBeSaved`, `UnknownFertiliser`) |
| When | At every hourly pass (system Labour, after Agriculture and Market), live or away, for each hand under contract in id order, each order in target order; and **at once when an order is set** if the hand still has this hour's action (ADR 0040). Each hand does at most `HandDef.orderActionsPerHour` (1) action (a harvest, a sowing or a pick) per sim hour, counted per hand (`actionHour`, `actionsInHour`) |
| The player wins | An order acts only on a field that is ready or empty (or a tree that is ripe) and never undoes the player's crop: a field the player sowed with another crop is harvested by the order when ready and then re-sown with the order's crop. Changing the order when sowing by hand is the UI's choice (spec 008) |
| Harvest | A ready crop on the tile is harvested (spec 003 `Harvest`), with `Overflow.SellRestToTrader` so a full store never blocks it. A ripe tree is picked the same way |
| Re-sow | An empty tile is sown with the order's crop if it is in season (spec 003 §3.2: stock first, the rest bought). If it's out of season, the tile waits (`OrderWaiting(OutOfSeason)` once per season). If cash is short, nothing is sown (`OrderWaiting(InsufficientFunds)`): orders never borrow (ADR 0009) |
| Sell rule | STORE: the harvest stays in the store. OFFER AT p: the harvested quantity is added to the good's market offer with minimum price p (spec 004 §3.2 and §3.3, ADR 0031): it sells only while the price is at or above p, and the trader takes it only if p ≤ Floor. SELL TO TRADER: sold at the floor now, up to the trader's capacity; the rest is stored |
| Not underway | Orders pause while the contract has lapsed; they resume on renewal |
| Report | Each action emits `OrderExecuted(handId, target, action, quantity, amount)`, so *While you were away* lists what the hand did, and the order keeps it as its last action (`lastAction`, `lastAt`, `lastQuantity`, `lastAmount`) for the hand sheet's "Last done" |

### 3.9 Processing order and determinism

- Daily pass, system Labour: day labourers' day ends (accrual), the roster's availability for the new day is drawn, applicants leave if their wait is over (live only).
- Hourly pass, system Labour: applicants arrive (live only), standing orders run.
- Seasonal pass: renewals run inside spec 005's settlement (step 6); skill growth runs after.
- People state is appended to `StateHash` after spec 003's section. Lists are kept in id order; nothing iterates a dictionary.

## 4. Data

`content/people.json`:

| Definition | Field | Type | Default | Bounds |
| --- | --- | --- | --- | --- |
| `LabourPoolDef` | `rosterSize` | int | 8 | 0–30 |
| | `availablePerDay` (Spring, Summer, Monsoon, Winter) | int ×4 | 2, 4, 3, 2 | 0–rosterSize |
| | `dailyWage` | money | 9,375 | > 0 |
| | `hoursPerDay` | Fixed | 8 | 0–14 |
| | `regularThresholdDays` (full day-equivalents) | int | 6 | 1–100 |
| `HandDef` | `hoursPerDay` | Fixed | 8 | 0–14 |
| | `productivityBase`, `productivityPerSkill` | Fixed | 0.70, 0.50 | 0–2 |
| | `marketWage` | money | 30,000 | > 0 |
| | `wageBase`, `wagePerSkill` | Fixed | 0.80, 0.40 | 0–2 |
| | `skillMin`, `skillMax`, `skillRangeHalfWidth` | int | 20, 70, 15 | 0–100, min ≤ max |
| | `skillGainPerSeason` | int | 3 | 0–20 |
| | `maxApplicants`, `applicantEveryHours`, `applicantJitterHours`, `applicantWaitDays` | int | 3, 12, 6, 2 | ≥ 1 |
| | `acceptChance` | Fixed | 0.85 | 0–1 |
| | `severanceShare` | Fixed | 0.10 | 0–1 |
| | `lapseGraceDays` | int | 2 | 0–8 |
| | `maxOrders` | int | 6 | 1–20 |
| | `maxHandHuts`, `orderActionsPerHour` | int | 2, 1 | 0–4, 1–6 |
| `JobPostDef` | `cost`, `days`, `rateFactor` | money, int, Fixed | 200, 4, 0.5 | > 0, 1–14, 0.1–1 |
| | `aboveMarketFactor`, `aboveMarketSkillBonus`, `aboveMarketAccept` | Fixed, int, Fixed | 1.10, 10, 0.95 | 1–2, 0–50, 0–1 |
| `TraitDef` (list) | `id`, `nameKey`, `hoursFactor`, `keepsWage`, `skillGainFactor`, `renewalWageFactor`, `careless` | | hardworking 1.10; lazy 0.85; loyal keepsWage; ambitious 1.5 and 1.10; careless; steady | factors 0.5–2 |
| `NameDef` (list) | `id`, `nameKey` | | 40 Indian given names in the string table | unique |

`content/buildings.json` gains `hand_hut`: `value` 15,000, `usefulLifeSeasons` 60, `buildHours` 24, `housesHands` 1, `yardTreeSlots` 0, `storeCapacityKg` 0, `restRecovery` 0, `gardenBeds` 0. Existing buildings get `buildHours` 0 and `housesHands` 0. `content/land.json` gains `hutPlacesPerAcre` (1) (ADR 0039; `yardBuildings` dropped).

**State** (part of `GameState`):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Neighbour` (roster) | `id`, `nameId`, `trait`, `secondsWorked`, `askedToMoveIn`, `hiredToday` | | In id order |
| `People` | `freeToday` (roster ids), `nextApplicantAt`, `nextApplicantId`, `nextHandId`, `nextContractId`, `population` | | |
| `JobPost` | `wageOffer`, `postedAt`, `expiresAt` | | At most one |
| `Applicant` | `id`, `nameId`, `skill`, `traits[2]`, `secondTraitKnown`, `skillKnown`, `askedWage`, `arrivedAt`, `fromRosterId` | | Waiting applicants in id order |
| `Hand` | `id`, `nameId`, `skill`, `traits[2]`, `hutBuildingId`, `hiredAt`, `seasonsWorked` | | |
| `Contract` | `id`, `handId`, `wage`, `state` (Active, Lapsed), `prepaymentId`, `lapsedAt` | | |
| `StandingOrder` | `handId`, `target` (tile or slot), `crop`, `seed`, `fertiliser`, `sellRule`, `minPrice`, `lastAction`, `lastAt`, `lastQuantity`, `lastAmount` | | In (hand, target) order |
| `Construction` | `buildingDef`, `tileId`, `jobId`, `cost` | | |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `HireDayLabourers` | `count` | `InvalidArgument` (≤ 0); `NoneAvailable`; `InsufficientFunds` | §3.2. Emits `DayLabourersHired` |
| `BuildHandHut` | `tileId` | `UnknownTile`; `NotQuarters`; `NoRoom`; `TooManyHuts`; `InsufficientFunds` | §3.4. Emits `ConstructionStarted` |
| `PostJob` | `wageOffer` (market, aboveMarket) | `NoHut` (no empty or building hut); `AlreadyPosted`; `InsufficientFunds` | §3.5. Emits `JobPosted` |
| `Interview` | `applicantId` | `UnknownApplicant`; `AlreadyInterviewed` | §3.5. Emits `ApplicantInterviewed` |
| `OfferContract` | `applicantId` | `UnknownApplicant`; `NoEmptyHut`; `InsufficientFunds` | §3.5–3.6. Emits `OfferAccepted` and `HandHired`, or `OfferDeclined` |
| `RenewContract` | `handId` | `NoHand`; `NotLapsed`; `InsufficientFunds` | §3.6 |
| `DismissHand` | `handId` | `NoHand` | §3.6 |
| `SetStandingOrder` | `handId`, order | see §3.8 | §3.8 |
| `ClearStandingOrder` | `handId`, `target` | `NoHand`; `NoOrder` | §3.8 |

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `DayLabourersHired` | ids, `cost` | §3.2 | HUD, report, play log (first hire) |
| `RegularAsksToMoveIn` | neighbour id, name | §3.3 | HUD (letter-style card), notifications |
| `ConstructionStarted` / `HandHutBuilt` | building, tile, cost | §3.4 | HUD, notifications (built), Hamlet gate (009) |
| `ApplicantArrived` / `ApplicantLeft` / `ApplicantInterviewed` | applicant | §3.5 | Hiring panel, notifications (arrived) |
| `JobPosted` / `JobPostExpired` | wage offer, cost | §3.5 | Hiring panel, report |
| `OfferAccepted` / `OfferDeclined` / `HandHired` | applicant, hand, wage | §3.5–3.6 | HUD, play log (first hire), population |
| `ContractRenewed` / `ContractLapsed` / `HandLeft` / `HandDismissed` | hand, wage, refund, severance | §3.6 | HUD, report, notifications (lapsed) |
| `OrderExecuted` / `OrderWaiting` | hand, target, action, quantity, amount / reason | §3.8 | While you were away, report |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Hire day labourers | 9,375 a day each, pro rata | 8 hours of Care today | A regular may ask to move in | Each day is separate |
| Build a hand's hut | 15,000 | A hand can move in; the Hamlet gate's house | Depreciation 250 a season; a yard building place | Permanent (Phase 1) |
| Post a job | 200 coins | Applicants twice as often; above market: better and surer applicants, a 10% dearer wage | — | Expires after 4 days |
| Interview | None | Know the second trait | — | — |
| Hire a hand | The rest of the season's wage, upfront | About 7.6 hours a day of Care all season, live and away | Renewal every season close; population + 1 | Let go: refund less severance |
| Renew or let lapse | The next season's wage | Work continues | Lapse: the hand idles, then leaves | Renew within the grace |
| Standing orders | None | The farm runs while you're away; sales at your minimum price | Inputs bought from cash; the store fills | Change any time |

## 7. UI

Layouts are spec 008's. Content:

```
Hiring panel                                        Hand panel
+---------------------------------------------+     +---------------------------------------------+
| Day labourers today: 3 free (Monsoon)       |     | Ramesh, farm hand, skill 54  [Why?]         |
| 9,375 a day each; rest of today: 5,078      |     |  hours 8 x (0.70 + 0.50 x 0.54) = 7.8 h/day |
| [Hire 1] [Hire 2] [Hire 3]                  |     |  hardworking x1.10 -> 8.5 h/day             |
| Regulars: Sunita 5 of 6 days                |     | Contract: 30,000 a season, paid to Winter   |
|---------------------------------------------|     |  prepaid 27,000 left (expensed 9,000 a day) |
| Applicants (hut empty)                      |     | Orders:                                     |
|  Ramesh  skill 39-69  hardworking  ?        |     |  North acre: keep wheat, offer above 26     |
|   asks 36,600 a season [Interview] [Offer]  |     |  Mango (yard): pick, sell to trader         |
|  Kavita  skill 20-50  steady       loyal    |     | [Add order] [Let go: refund 27,000 - 0]     |
|   asks 33,840 a season [Offer]              |     +---------------------------------------------+
+---------------------------------------------+
```

*Why?* lines: a hand's hours (skill, traits); the asked wage (market wage × skill × price index); a renewal's new wage; a lapse ("Renewal needed 30,000; you had 22,400"); each order's last action.

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Loading `people.json` with `availablePerDay` above `rosterSize`, `skillMin > skillMax`, an unknown trait, or a `hand_hut` without `housesHands` fails validation naming the field | `ContentTests.People_Validation` |
| AC-2 | The roster and each day's free labourers are the same for the same seed and differ by day; the free count equals `availablePerDay` for the season | `DayLabourTests.Pool_Seeded` |
| AC-3 | Hiring 2 labourers at 18:00 costs exactly `2 × 9,375 × 21,600 ÷ 86,400` = 4,687.50, posted to 5100; adds 16 hours a day to the Care pool until the daily pass, time-weighted; hiring more than are free is rejected with `NoneAvailable` and changes nothing | `DayLabourTests.Hire_CostAndCare` |
| AC-4 | A labourer hired for 6 full day-equivalents in total asks to move in once a hand's hut is empty, as an applicant with skill and both traits known; not before the hut exists; 6 hires of an hour each are not enough (ADR 0039) | `DayLabourTests.Regular_AsksToMoveIn` |
| AC-5 | `BuildHandHut` costs 15,000 (Dr 1600), finishes exactly 24 hours later, depreciates 250 a season from the next close, and is rejected on a farm acre, the yard and unowned land (`NotQuarters`) and when the quarters are full (`NoRoom`) (ADR 0040) | `HutTests.Build_CostTimeDepreciation` |
| AC-6 | With an empty hut, applicants arrive at the seeded times, at most 3 wait, each leaves after 2 days if not hired; none arrive or leave during an away advance | `ApplicantTests.Arrival_Queue_Away` |
| AC-7 | An applicant's shown range contains the true skill and is 30 wide (clamped at 0 and 100); the interview reveals exactly the second trait; the asked wage follows §3.5 (skill 50: 36,000) | `ApplicantTests.Profile_Interview_Wage` |
| AC-8 | Over 10,000 seeded offers the acceptance rate is within 1 point of 85%; an accepted offer starting at mid-season prepays exactly half the wage (Dr 1110) and population rises by 1 | `ContractTests.Offer_Accept_Prepay` |
| AC-9 | A skill-50 steady hand adds exactly 7.6 hours a day to the Care pool; hardworking 8.36; lazy 6.46 | `ContractTests.HandHours` |
| AC-10 | At a season close renewal is paid after loans and living costs (spec 005 AC-13 order), at the re-asked wage (a loyal hand keeps theirs); when cash is short the contract lapses, the hand's hours leave the pool, and after 2 days the hand leaves; `RenewContract` within the grace pays the whole season (the waiting days expensed at once) and resumes work; a contract still lapsed at the next close ends | `ContractTests.Renewal_Lapse_Grace` |
| AC-11 | `DismissHand` refunds the unearned wage less severance (none in the first season; 10% of the wage after), and severance never exceeds the refund | `ContractTests.Dismiss_RefundAndSeverance` |
| AC-12 | Skill grows by 3 a season (4.5 ambitious, capped at 100) and the next renewal asks the wage for the new skill at the current price index | `ContractTests.SkillGrowth_AndWage` |
| AC-13 | A "keep wheat on tile 18" order harvests the ready crop and re-sows it in season, live and during a 72-hour away advance, with the same final state hash either way; out of season the tile waits and emits `OrderWaiting(OutOfSeason)` once; a hand does at most one action an hour; a field the player sowed with another crop is not undone | `OrderTests.KeepCrop_LiveAndAway` |
| AC-14 | An OFFER AT p order adds the harvest to the market offer with minimum price p (spec 004 AC-18 rules); SELL TO TRADER sells at the floor up to capacity and stores the rest; STORE keeps it; a careless hand's harvest overflows to the trader | `OrderTests.SellRules` |
| AC-15 | With too little cash an order sows nothing, emits `OrderWaiting(InsufficientFunds)` and never creates a liability; orders pause while the contract has lapsed | `OrderTests.NeverBorrows_PausesOnLapse` |
| AC-16 | Over 2 game years of seeded play with day labourers, a hand, renewals, a lapse, a dismissal and orders, the ledger reconciles at every close and the chunking-invariance check holds | `PeopleTests.Ledger_And_Chunking` |
| AC-17 | Performance (CI, Release): advancing 72 h with 2 hands, 12 orders and the default farm takes under 20 ms | `PerfTests.Advance72h_WithPeople` (category `Perf`) |
| AC-18 | A posted job halves the time between applicants, costs 200 (Dr 5700) and expires after 4 days or at a hire; above market, applicants' skill lies in 30–80, they accept at 95% over 10,000 seeded offers, and the hired wage is 1.10 × the asked wage | `HiringTests.PostJob_RateAndQuality` |
| AC-19 | A job posted while the hut is being built brings the first applicant only once the hut is finished, sooner than a walk-in would come; posting with no hut, or twice, is rejected and changes nothing | `HiringTests.PostJob_WhileBuilding` |
| AC-20 | A standing order's last action (action, time, quantity, amount) equals its last `OrderExecuted` | `OrderTests.LastAction` |
| AC-21 | A people-only *While you were away* view built from the events of a 30-hour away advance lists each hand's harvests, sales, sowings and waiting reasons, day labour paid and contracts renewed or lapsed, with totals equal to the events' sums | `Sim.View.Tests` `AwayViewTests.People` |
| AC-22 | The view shows skill as stars and a band word from `ui.json` (an applicant's range as solid and faded stars), hours as "tends about N acres" rounded to a half, and never a raw factor | `Sim.View.Tests` `PeopleViewTests.Words` |
| AC-23 | Gate checks (category Balance): the competent bot hires a hand and meets §9's hooks; the day-labour spammer, hand churner and lapse gamer stay at most 1.20 × the competent median net worth over 2 years; the absentee stays at least 0.80 × | `GateTests.PeopleStress` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| The ADR 0030 pacing with a hand from the end of Spring | Summer lentils on 6 acres at Care about 1.1; farm profit about +75,000; the Hamlet gate met by the end of week 1 (with a moneylender loan of about 10,000) or week 1.5 without | Economy model (F00.3, US-016) |
| A hired acre's net (ADR 0033) | About 2,000–5,000 a season: positive on light-labour crops (lentils), near zero on potato | Economy model |
| Day labour versus a hand | A day labourer costs 25% more an hour than a hand: worth it only for a few days of a busy harvest | Shape |
| Applicants before a good hire | About 2–3 days of waiting with an empty hut | ±1 day |
| A regular moves in | After about 6 days of hiring the same neighbours (about 2 seasons of occasional help) | Shape |
| Orders while away | A 72-hour absence with orders loses no ready harvest on ordered fields | 0 |

## 10. Open questions

All seven were answered in the review with the owner (2026-10-18):

1. **Lapse:** a lapsed hand stops working and stays for 2 days of grace (`lapseGraceDays`); `RenewContract` resumes work; after that the hand leaves; never while away (§3.6).
2. **The hut on the yard:** a yard tile holds up to 4 buildings (the hut, the well, two hands' huts), so Phase 1 has at most 2 hands; data, for Phase 2 to raise (§3.4).
3. **The hut's price:** 15,000, materials with the family's own labour on the build; the *Why?* says so (§3.4).
4. **The day-labourer wage:** 25% above a hand's day; a rescue, not routine (§3.2). 11,250 at approval; 9,375 since ADR 0039 §11.
5. **Applicants:** both walk-ins while a hut stands empty and a posted job (`PostJob`, market or above market) that brings applicants twice as often, better ones above market, and can be posted while the hut is being built (§3.5, AC-18, AC-19). The Phase 2 hiring board builds on it.
6. **Standing orders from Homestead** for any hand under contract (ADR 0022); the GDD line was updated (§3.8).
7. **Traits:** the six of §3.7 in Phase 1; creative, frugal, sociable and perfectionist join in Phase 2 with their systems (F11.4).

### Changes after approval (Sprint 3 planning review, 2026-10-18, ADR 0034)

- **Coin amounts follow the price index** (spec 004 §3.1): wages are fixed at `Indexed(wage)` when a contract is signed or renewed (the GDD's lag), the day-labourer wage at each hire, and the hand's hut at `Indexed(value)` when built. Its book value then stays at cost.

### Sprint 5 planning review (2026-10-19, ADR 0039)

Ten gaps decided with the owner, one by one:

1. **Blender slice now** (US-183, 5 points): the rig and the sprites this sprint needs (hand's hut, hut, well, three people), after a style checkpoint; the rest of US-171 in Sprint 6.
2. **Hands' huts on the edge of an inherited farm acre** (one place per acre, at most 2 huts); not in the yard; `NotInherited`, `NoRoom`, `TooManyHuts` (§3.4, AC-5).
3. **Regulars counted by seconds hired:** 6 full day-equivalents, not 6 hires (§3.3, AC-4).
4. **Back pay on renewal after a lapse:** the whole season, waiting days included; still lapsed at the next close ends the contract (§3.6, AC-10).
5. **A day labourer's task is tending the fields** (the Care pool); the hire row previews Care in words.
6. **Orders follow the player** (never undo the player's crop; the UI offers to change the order) **and take time** (one action per hand per hour) (§3.8, AC-13).
7. **Stars and band words for skill, "tends about N acres" for hours,** numbers in *Why?* (AC-22).
8. **A People button** on the HUD; orders given from the map; people visible on the map (presentation only).
9. **A people-only *While you were away* card** now and each order's last action in state (AC-20, AC-21).
10. **Gate checks for people:** the competent bot hires; four stress bots (AC-23).
11. **The hand's wage 30,000 a season** (was 36,000), the day wage 9,375 (was 11,250), found by the gate checks: at 36,000 a founder who never hired ended about 8% richer after 11 years. At 30,000 hiring pays modestly (+7% by year 11). 15,000–20,000 was rejected: below rural minimum wages, and hiring would win by 34–47%, so the decision would vanish. Hiring is made more rewarding later through land, Phase 2's processing and markets, and skill growth, never by underpaying.

### Device review (2026-10-19, ADR 0040)

The owner's device run: the yard becomes 2 acres (tile 29); hands' huts move to the labour quarters (tile 37, two places), superseding ADR 0039 §2; an order starts at once (a per-hand action counter); people work their fields and go home when there's nothing to do (presentation). AC-5 and AC-13 are amended; `OrderTests.StartsAtOnce_OneActionAnHour_PlayerWins` covers the immediate start.

### Implementation notes (Sprint 5, Claude; no rule change)

1. **People are optional content,** like credit (ADR 0038): `people.json` is read only when `buildings.json` has a `hand_hut`. The frozen golden content has none, so goldens 001 and 003 to 005 keep their hashes. People state is hashed after the credit section (appended last), not right after spec 003's.
2. **`hutPlacesPerAcre` lives in `people.json`** (`huts.placesPerAcre`, 2 places on the quarters since ADR 0040) rather than `land.json`, so the land file and the frozen content stay unchanged. `maxHandHuts` and `orderActionsPerHour` are in `HandDef` as §4 says.
3. **Applicant timing:** the next applicant is due `applicantEveryHours` ± up to `applicantJitterHours` (uniform, in seconds) after the last arrival or after a hut empties, × `rateFactor` while a job is posted; arrivals are checked at hourly passes. Draws use stream Applicants with key = applicant id (draw 0 interval, 1 skill, 2 and 3 traits, 4 name, 5 acceptance).
4. **Live-time waits:** an applicant's 2-day wait and a lapsed hand's 2-day grace count only hours that pass live (like the credit final notice), so nothing leaves while the player is away.
5. **Careless** means the hand sells an order's harvest to the trader (up to capacity) whatever the sell rule says; orders always harvest with `SellRestToTrader`, so a full store never blocks them (§3.8).
6. **The asked wage is computed when an offer is made** (at the current price index), not stored on the applicant. An above-market hire's premium is kept for that season only; renewals re-ask the usual wage.
7. **Orders from the map** use certified seed and no fertiliser (the UI's default); the command accepts any seed and fertiliser. A player-sown field with another crop is harvested by the order when ready, then re-sown with the order's crop (§3.8).
8. **Regulars** (US-067) are counted from Sprint 5 (`secondsWorked`) but ask to move in only from Sprint 6.
9. **The competent bot** (gate checks) builds its hut in the labour quarters (tile 37), orders only the acres its hours tend at Care ≥ 1, uses compost below fertility 55, and grows wheat on the hand's extra acres in Winter.

## 11. Cut first

1. Regulars (keep the day-labour pool).
2. Interviews (show both traits).
3. Skill growth.
4. Severance (refund the full unearned wage).
5. Tree orders (field orders only).
6. The OFFER AT sell rule (STORE and SELL TO TRADER only).
