# 026. Town life: festivals, petitions, approval, reputation, crime and inequality

- Status: Approved by the owner (2026-11-18): G1–G8 (§10) closed one per message on my recommendations; ADR 0127. Every sheet is previewed as an artifact that looks exactly like the game before any UI code.
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: People and the town (Safety and fairness, Festivals, Town reputation, Citizen voice); Government (Approval); Map overlays
- Related ADRs: 0004 (the player never loses an election), 0043 (*Why?* in words), 0057 (specs a sprint ahead), 0063 (needs layers), 0107 (Phase 3 plan, §13 scope), 0108 and 0114 (the treasury), 0109 (taxes and policies), 0116 and 0120 (the Township as built), 0121 (land value), 0125 (spec 025's gaps)
- Depends on specs: 014 (people and needs), 021 (treasury), 022 (taxes and policies), 023 (the Township, civic buildings, coverage), 024 (land value, overlays), 025 (utilities and the kiln: reputation reads them)
- Stories: US-149 festivals (3), US-150 petitions (3), US-151 reputation and fairness (5); Sprint 24; save schema 22

## 1. Goal

The town answers the player's choices in ways the player can see: citizens ask for what they lack and remember whether they were heard, the town earns a name that draws families and grants, and fairness (jobs, wages, the gap between richest and poorest) shows up as safety and happiness. The lesson: public goods, fairness and trust are not free, and the cheapest way to a safe town is a town where people have work.

## 2. Scope

**In (ADR 0107 §13):**
- **Festivals** (US-149): one festival a season, held at the community hall or the banyan square, paid by the treasury or as your gift; a happiness lift for the day, more customers at the shops, a record for the Chronicle.
- **Petitions** (US-150): households raise petitions from unmet needs, one open per topic; you promise by a date or decline; doing the thing answers it. **Approval** (0–100) moves with happiness and with how petitions end, and in Phase 3 it moves migration only.
- **Reputation** (US-151): five scores (livability, business climate, education, health, environment), each from named parts; they draw families and add to the grants.
- **Crime** (US-151): from unemployment and inequality, lowered by a watchman in reach and, more cheaply, by jobs and fair wages; it feeds the Safety part, land value and the crime overlay.
- **Inequality** (US-151): the richest tenth's income over the poorest tenth's; high inequality lowers happiness below the median and makes the poorest families leave sooner.

**Out** (with where it goes instead):
- Protests, elections, escalation of ignored petitions, council votes: Phase 4 (the player never loses office, ADR 0004).
- Scandal from conflicts of interest: Phase 4.
- Visitors and tourists from outside, trade fairs: the Town.
- The Chronicle's own screen and entries: spec 029 (this spec only records and announces what happened).
- Investors reading business climate: spec 028 (this spec only exposes the score).
- Individual petitions from the old village's cohort (it stays an aggregate, ADR 0107 §2): the cohort counts in the town's totals but raises none.

## 3. Rules and formulas

Everything is Fixed-point (ADR 0018). Money is in 1/100 coin and indexed by the price index at the moment of the spend. Rounding: division rounds half away from zero unless a rule says otherwise. Each number has a *Why?* in words, never a factor (ADR 0043). All values live in `content/town_life.json`.

### 3.1 Festivals (US-149)

| Rule | Behaviour | Units | Bounds |
| --- | --- | --- | --- |
| Four festivals, one a season | Spring *Seed Fair*, Summer *Mango Mela*, Monsoon *Rain Night*, Winter *Harvest Mela*. The player chooses the venue, the payer and the day; the kind follows the season. | festival | one scheduled or held a season |
| Venue | The **community hall** (built, caretaker in post) or the **banyan square** (always). The venue sets the reach (hall `reach` 1,000 m, square `squareReach` 500 m, Manhattan, an acre is 100 m). | venue | 2 |
| Day | One of the next 3 real days (starts at 0:00, so a 72 h absence never skips it); the festival is that whole day. | day | 1–3 days ahead |
| Cost | `perPerson` × your town's people (your households and the old village's cohort) × the price index. Seed Fair 80, Mango Mela 100, Rain Night 80, Harvest Mela 120 a person. Paid at once to suppliers outside the town. | coins | 250 people: 20,000 to 30,000 |
| Payer | **The treasury** (from its cash, never below zero, with the same budget guard as a work: would the town still pay its bills at the next close) **or your gift** (your cash, booked to *Gifts to the village*, outside farm profit). Nothing flows back to you. | payer | 2 |
| Happiness lift | On the festival day a household's happiness is raised by `inReach` **8** points inside the venue's reach and `outside` **3** points elsewhere in the town. The next day it is gone. | points | 0–100 after the lift |
| Customers | On the festival day the shops' demand is higher: chai and sweets **30%**, vegetables **10%**. | % of the day's demand | 0–50 |
| Record | A festival held is kept (kind, venue, day, people, cost, who paid) and announced for the Chronicle (spec 029); the hall's sheet lists the last four. | record | last 8 kept |
| A festival can't be undone | The cost is spent when it is scheduled; the confirm card says so. | | |

*Why these numbers:* a festival costs about four-fifths of a season's untied grant (100 a resident), so one is a real choice and never free; the lift is the size of a clinic's effect on its need for one day; the shops gain what a crowd buys.

### 3.2 Petitions (US-150)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Raised at each close | For each topic with no open petition, if **3 or more** of your households (or one in five, whichever is more) share the unmet need, a petition is raised. Cohort households never count. | 3 or 20% |
| Topics | A service out of reach or full: **clinic, school, community hall, watchman, playground, water** (one topic each, raised only once the player could have built it: the clinic from the Village, the civic buildings and water from the Township). **Rent too high:** a household's rent over 25% of its income (the existing leave risk starts at 30%). **No work:** a household with a working-age adult and no town job. | 9 topics |
| What it says | Who and where: "6 families on the east side can't reach the clinic" (the side is the compass side of the town square where most of them live). | |
| Open petitions | At most one per topic. A new one for a topic cannot be raised for 1 season after the last one closed. | |
| Your answers | **Promise** it by a date (this season's close, the next one or the one after: 1, 2 or 4 closes), or **Decline**. **Doing the thing** (building the clinic, easing the rent, hiring) answers it with no button: at the next close the need is gone and the petition is **Done**. | 1, 2 or 4 closes |
| How it ends | **Done**: the need is gone at a close before any promise, approval +4. **Kept**: the promise's close finds the need gone, +6. **Declined**: −2, and the topic is quiet for 4 closes. **Ignored**: unanswered after 2 closes, −4, closed. **Broken**: the promise's close finds the need still there, −8, closed. | |
| One tap is enough | A petition answered by Promise stays *Promised* (the date shown) until the close that settles it. | |
| Cohort and Phase 4 | Protests, votes and escalation are Phase 4. | |

*Why these numbers:* three families is about a tenth of a Township's first households, so a few neighbours can be heard; being ignored costs half of what keeping a promise earns, and breaking one costs more than doing nothing, because a broken promise is worse than silence.

### 3.3 Approval (US-150)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Start | 50 at the Township's opening. | 0–100 |
| Each close | `approval += clamp((townHappiness − 70) ÷ 5, −4, +4)` (rounded toward 0) plus the season's petition endings (3.2), then clamped to 0–100. | step ±4 a close plus petitions |
| What it does (Phase 3) | **Migration only.** See 3.7's pull. It changes no tax, policy slot or election; the player can't lose office (ADR 0004). It shows on the Town hall sheet beside the score's *Why?* with the last six closes. | |

*Why:* 70 is where the town's own happiness is neutral (the Village sits at about 73, the Township about 84), so a content town drifts up a little each season and a sour one down; petitions add the moments people remember.

### 3.4 Reputation (US-151)

Five scores, each 0–100, read at every close from named parts (a part is 0–100; the score is the weighted sum, weights sum to 100). Before the Township none shows.

| Score | Parts and weights |
| --- | --- |
| **Livability** | town happiness 50 · homes with water in reach 30 (utilities, spec 025; none built: 0) · homes with their shelter met 20 |
| **Business climate** | the business tax rate (0% is 100, the cap 20% is 0, linear) 40 · open businesses that made a profit last season 30 · the road (a track 40, the paved link road 100) 30 |
| **Education** | literacy of your people aged 7+ 50 · homes in reach of a school with a teacher 30 · children of school age enrolled 20 |
| **Health** | homes in reach of a staffed clinic 40 · the *Healthcare* need met, mean over households 30 · the Health department's condition 30 |
| **Environment** | homes with no kiln smoke on them 40 (no kiln: 100) · homes within reach of drains 30 · homes within reach of a collection post 30 (spec 025; none built: 0) |

| Effect | Behaviour | Bounds |
| --- | --- | --- |
| **Draws families** | see 3.7: the mean of the five scores is the reputation's share of the pull. | pull 50–150% |
| **Adds to the grants** | both treasury grants a resident are raised by `(mean − 50) ÷ 5` percent when the mean is above 50 (never lowered): at 100 the grants are 10% higher. | 0–10% |
| **Investors** | the business climate score is exposed for spec 028; no effect here. | |

*Why:* the parts are what a visitor would notice first: are people content, can they get water, can their children read, is there a clinic, is the air clean. Weights favour what a mayor controls directly.

### 3.5 Crime (US-151)

`crime = clamp(crimeBase + 2 × unemployed% + inequalityScore × 3 ÷ 10, 0, 100)` read at each close, where `crimeBase` is **20**, `unemployed%` is the working-age adults of your households without a town job (the cohort counted as employed), and `inequalityScore` is from 3.6. A neutral town (10% unemployed, a ratio of 3, a score of 22) has a crime of about 47, which is the 50 the existing `safetyBase` of 0.5 stands for.

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| **The Safety part** | The Township's Safety part (spec 023 §3.7) becomes `1 − crimeAt(home) ÷ 100`, where `crimeAt(home) = crime − watchmanGain × 100 × effect(home)` (floor 0). With crime 50 this is today's 0.5, rising to 1.0 in reach of a staffed watchman, so a town of no change plays as before. Before the watchman's post is built the part is not counted (as today). | 0–1 |
| **Jobs and fair wages are cheaper** | Each working-age adult put into work lowers `unemployed%`; a rising floor wage (policy) lowers the ratio. The watchman reaches only his reach and his capacity. | |
| **Land value** | A term is added to an acre's land value: +0.02 for every 10 points of `crimeAt` under 50, −0.02 for every 10 over 50, at most ±0.10 (spec 024's list; "no crime yet" ends). | ±0.10 |
| **Overlay** | The crime overlay (spec 024 §3.8) lights up: one hue, five steps by `crimeAt`; a tap shows the acre's number with a *Why?*. | |
| **Events** | A season's crime is read, never rolled: no theft is simulated, no loss to anyone's cash (Phase 3 keeps crime as a pressure on happiness and land value, not a random loss). | |

*Why:* a person with work and a fair wage has less reason to steal, and a watchman only watches the streets near his post; that is why jobs cost less per point removed than a post, and why the post still helps the families who live near the square.

### 3.6 Inequality (US-151)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| **The ratio** | At each close, your households' season incomes (`Household.Income`) are sorted (ties by household id); the ratio is the **mean income of the richest tenth ÷ the mean income of the poorest tenth** (at least 1 household each), capped at 10. It needs 10 households or more; below that the sheet says "too few families to compare". The old village's cohort is not in the ratio (identical households; it would only dilute it). | 1–10 |
| **The score** | `inequalityScore = (ratio − 1) × 100 ÷ 9` | 0–100 |
| **Happiness** | A household earning **below the town's median** loses 1 happiness point for each whole point the ratio is above 4, at most 6. | 0–6 points |
| **Pushing out** | At a ratio of 6 or more the poorest tenth's patience is one season shorter before they leave (`LeaseRules.Patience`). | 1 season |
| **Fair wages** | The minimum-wage policy and a fair employer's raises (spec 015) lift the bottom, which lowers the ratio; nothing else is special-cased. | |

*Why:* four is a society where the best-off tenth earn four times the poorest, the comfortable end of what villages are; from there people below the middle feel it, and past six they start to look elsewhere.

### 3.7 The pull on migration

A family arrives `wait` hours after a house falls free: today `familyEveryHours ± jitter` (18 ± 6). From the Township: `wait = (18 ± 6) × 100 ÷ pull`, with `pull = 100 + (approval − 50) ÷ 2 + (mean of the five scores − 50) ÷ 2`, clamped to 50–150. At 50 and 50 nothing changes; at 100 and 100 a family comes in 12 hours instead of 18; at 0 and 0 in 36. Households that move out are unchanged except 3.6's patience.

## 4. Data

**New definitions** (`content/town_life.json`, loaded by `Sim.Content`):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| festival | `id`, `season`, `perPerson` | string, string, int | 4 festivals above | 20–300 | strings `festival.<id>` |
| festivals | `inReach`, `outside` | int | 8, 3 | 0–20 | happiness points |
| festivals | `shops.chai`, `shops.sweets`, `shops.veg` | Fixed | 0.30, 0.30, 0.10 | 0–0.5 | demand rise for the day |
| festivals | `daysAhead` | int | 3 | 1–3 | |
| petition | `topic`, `unlockEra`, `minHouseholds`, `minShare` | | 3, 0.20 | | one row per topic |
| petition | `rentShare` | Fixed | 0.25 | 0.10–0.30 | |
| petition | `promiseCloses` | [int] | [1, 2, 4] | | |
| petition | `done`, `kept`, `declined`, `ignored`, `broken` | int | 4, 6, −2, −4, −8 | | approval points |
| petition | `quietCloses`, `ignoredAfter` | int | 4, 2 | | |
| approval | `start`, `neutralHappiness`, `step`, `stepMax` | | 50, 70, 5, 4 | | |
| reputation | the five scores' parts and weights | | §3.4 | sums to 100 | |
| reputation | `grantBonusDivisor`, `pullDivisor` | int | 5, 2 | | |
| crime | `base`, `perUnemployed`, `inequalityShare` | | 20, 2, 0.3 | | |
| crime | `landValueStep`, `landValueMax` | Fixed | 0.02, 0.10 | | |
| inequality | `needsHouseholds`, `happinessAbove`, `maxPoints`, `pushOutAt` | | 10, 4, 6, 6 | | |

**New state** (save schema 22, `TownLifeState`; Sprint 24's single bump, with hills and apples):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| TownLifeState | `Approval`, `Reputation[5]`, `Parts[5][3]`, `Crime`, `Ratio`, `Score` | Fixed | read at each close |
| TownLifeState | `ApprovalHistory` | list (6) | |
| TownLifeState | `Petitions` | list | id, topic, side, households, raisedAt, status, promisedBy, closedAt |
| TownLifeState | `QuietUntil[9]` | long | topic |
| TownLifeState | `Festival`, `Festivals` | scheduled (kind, venue, payer, day, cost), history (8) | |

An older save loads with approval 50, the scores read at its next close and no petitions, so a Township town is unchanged until its first close (fixture: `schema22-townlife.ftc`, written in Sprint 24).

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `ScheduleFestival` | venue, payer (town or you), days ahead | `NoTownship`; `FestivalThisSeason`; `VenueNotReady` (hall not staffed); `InsufficientFunds` (the payer's cash) | pays; schedules the season's festival |
| `AnswerPetition` | petition id, **promise** (closes: 1, 2, 4) or **decline** | `PetitionClosed`; `InvalidStep` | sets the petition's status |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `FestivalScheduled` / `FestivalHeld` | kind, venue, day, cost, payer | on scheduling / at the day's end | alerts (the day), Chronicle (spec 029) |
| `PetitionRaised` | id, topic, side, households | a close | alerts (the bell), the Town hall |
| `PetitionEnded` | id, status (done, kept, declined, ignored, broken) | a close, or the answer | approval, the sheet |
| `ApprovalMoved` | from, to | a close | the sheet (state only) |
| `ReputationRead` | five scores | a close | the sheet (state only) |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Hold a festival | 20,000–30,000 for a town of 250 (the treasury's or yours) | one day: happiness +8 or +3, shops +30%/+10% | the treasury is lower at the close; the Chronicle has an entry | none |
| Pay for it from the treasury or as a gift | the treasury's cash or yours | the same | the budget guard warns if the town would be short; a gift is outside farm profit | none |
| Promise a petition | nothing | kept: approval +6; broken: −8 | forces a decision before a date | until the date; cannot be withdrawn |
| Decline a petition | nothing | approval −2; the topic is quiet for 4 closes | the families' need stays | none |
| Do the thing | its own cost | the petition ends Done at the close, +4 | the need is met | as the thing |
| Create jobs, raise the floor wage | wages | lower crime and a lower ratio | the town earns more | policy: repeal after a season |

## 7. UI

The **Town hall** sheet (Menu) gets a **Town life** tab: approval and its last six closes, the open petitions with *Promise / Decline*, the five reputation bars (a tap opens the parts), crime and inequality. The **community hall's** and **square's** sheets get *Hold a festival* with the venue, the payer (*The town pays* / *I pay as a gift*) and the days, then the confirm card (cost, payer's cash after, the guard). A bell alert for a petition and a festival-day note on the map's top bar. The crime overlay lights up. Every sheet is previewed as an artifact first (Sprint 24 Preview 1).

```
+---------------------------- Town life ----------------------------+
| Approval 58    (50 → 52 → 55 → 58)                              ? |
| Petitions                                                          |
|  6 families on the east side can't reach the clinic     [Promise] |
|  Reputation   Livability 62 ?   Business 55 ?   Education 48 ?    |
|  Crime 44 ?   Inequality: richest tenth earn 3 times the poorest ? |
+--------------------------------------------------------------------+
```

*Why?* rows: approval, each petition's count, each reputation score and each part, crime and its three parts, the ratio, the festival's cost, the lift and the shops' rise, the pull.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | One festival a season; it costs `perPerson` × people × index, from the payer's cash only; the treasury never goes below zero; a gift is outside farm profit; nothing flows to you | `FestivalTests.Cost_Payer_Wall` |
| AC-2 | On the day households in reach get +8 and the rest +3, the shops' demand rises, the next day neither; the hall needs its caretaker | `FestivalTests.Day_Lift_Shops`, `FestivalTests.HallNeedsKeeper` |
| AC-3 | A festival held is recorded and announced; a 72 h absence does not skip a scheduled day | `FestivalTests.Record_Away` |
| AC-4 | A petition is raised only when 3 families (or a fifth) share a need and no petition for that topic is open; the cohort never raises one | `PetitionTests.Raised_Threshold_Cohort` |
| AC-5 | Promise kept, promise broken, declined, ignored and done end as 3.2 says, with the approval change | `PetitionTests.Endings` |
| AC-6 | Approval moves with happiness and petitions, stays 0–100, and changes only migration (no tax, policy slot or election) | `ApprovalTests.Moves`, `ApprovalTests.OnlyMigration` |
| AC-7 | The five scores equal their weighted parts; each part has a *Why?*; before the Township none is read | `ReputationTests.Parts`, `ReputationTests.NotBeforeTownship` |
| AC-8 | The pull changes the wait for a family (12–36 h), and at 50 and 50 changes nothing; the grants rise 0–10% above a mean of 50 and are never lowered | `ReputationTests.Pull`, `ReputationTests.Grants` |
| AC-9 | Crime follows unemployment and inequality; the watchman lowers it in his reach only; at crime 50 the Safety part is today's | `CrimeTests.Formula`, `CrimeTests.SafetyPart`, `CrimeTests.WatchmanReach` |
| AC-10 | Land value has the crime term within ±0.10 and the crime overlay equals the sim's numbers | `LandValueTests.Crime`, `OverlayViewTests.Crime` |
| AC-11 | The ratio is the richest tenth over the poorest, from 10 households, capped at 10; below the median lose happiness above 4; the poorest leave a season sooner at 6 | `InequalityTests.Ratio`, `InequalityTests.Effects` |
| AC-12 | Schema 22 round-trips; an older save loads with approval 50 and no petitions; the fixture carries on through two closes | `MigrationTests.Schema22_TownLife` |
| AC-13 | Every number has a *Why?* in words; no raw factors | `WhyTests.EveryNumberHasWhy` (over the Town life situation) |
| AC-14 | The sheets and cards respond to scripted touch in the Editor | the stories' Editor tours |
| AC-15 | A town left alone (no festival, no answers) still works: it levels and no state goes negative; no zero-time loop over the new commands | `TownLifeTests.LeftAlone`, `GateTests.TownLife` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Crime points removed a season per 1,000 coins | jobs and the floor wage at least as cheap as the watchman | measured by bots, reported |
| A town left alone | approval 50–65, reputation means 40–60 (no collapse, no free lunch) | ±10 |
| The Township's pace (day 101 competent) | unchanged within 2 days: the pull starts at 50 and 50 | ±2 days |
| Land payback (6.0 seasons, ADR 0124) | the crime term moves it under 0.3 seasons | ±0.3 |
| Archetype bots (ADR 0016) | within 20% on net worth and happiness; an *everything festival* bot no better than the median | 1.13 holds |
| Festival cost against the grants | one festival a season never leaves a town left alone short | none short |
| `crimeBase` | a typical town sits at about 50 | ±5 |

## 10. Open questions

Each is closed with the owner, one per message, with my recommendation, before code.

- **G1 Festivals. Closed (owner, 2026-11-18, my recommendation):** four named festivals, kind fixed by the season, the owner picks venue, payer and day; cost per person; the lift of 8 and 3 points for one day; shops +30%/+10%; no undo.
- **G2 Petitions. Closed (owner, 2026-11-18, my recommendation):** nine topics, a threshold of 3 families or a fifth, answers *Promise* (1, 2 or 4 closes) or *Decline*, "do it" answering by itself, no third button.
- **G3 Approval. Closed (owner, 2026-11-18, my recommendation):** starts at 50, moves ±4 a close with happiness, plus the petition endings; changes migration only.
- **G4 Reputation. Closed (owner, 2026-11-18, my recommendation):** the five scores' parts and weights (§3.4), the pull and the grant bonus (up to 10%).
- **G5 Crime. Closed (owner, 2026-11-18, my recommendation):** the formula, the Safety part rewritten as `1 − crimeAt ÷ 100` (the same at 50), the land-value term and the overlay; crime is a pressure, never a theft.
- **G6 Inequality. Closed (owner, 2026-11-18, my recommendation):** the decile ratio from your households only; −1 point a whole point above 4 below the median, the poorest leave a season sooner from 6.
- **G7 Screens. Closed (owner, 2026-11-18, my recommendation):** a *Town life* tab on the Town hall, *Hold a festival* on the hall and the square, the crime overlay; previewed first.
- **G8 Scale in Sprint 24. Closed (owner, 2026-11-18, my recommendation):** 11 points of stories with spec 027 and the hills; schema 22 shared; the cut line is §11.

## 11. Cut first

1. Inequality's push-out rule (3.6 *Pushing out*); the ratio and its happiness effect stay.
2. The land-value term for crime.
3. The festival's shop lift (the happiness lift stays).
4. The *Rent too high* and *No work* topics (service topics stay).
5. The *Environment* score's drains and collection parts (they wait for spec 025's buildings).

## 12. As built

**US-151 (Sprint 24, ADR 0145):** the town is read at each close from the Township (`TownLifeRules.AtClose`, `TownLifeState` in save schema 22, in `township.json` `townLife`). Differences from the plan above: inequality is **income a person among households with someone of working age** (§3.6; a retired household is left out), because raw household income measured household size and capped the ratio in most bot towns; `crimeBase` stays 20 (measured median 49); nothing is read or applied until the first close from the Township. The Town life tab is the third tab of the town treasury's sheet; the crime overlay lights up once the town is read. Approval, petitions and festivals are US-150 and US-149.

**US-150 (Sprint 24, ADR 0146):** petitions and approval. Differences from the plan above: **eight topics** (the table listed eight and said nine); all start at the Township (the closes are read from there); a promise is judged at its own close only (kept +6 or broken −8, never early); a petition ends Done when fewer than the raising number of families (3, or a fifth) still share the need; approval is a whole number moving `(happiness − 70) ÷ 5` toward zero, at most 4 a close, with its last six closes kept (`townLife.petitions` in `township.json`). Decline is a card; Promise is a choice sheet of 1, 2 or 4 closes with no card. Approval and petitions live on the Town life tab, and each open petition rings the bell.
