# 029. The Chronicle, the Almanac and citizen stories

- Status: Approved (owner, 2026-11-23, with the Sprint 26 plan, ADR 0166; §3.6's hand's stall is re-confirmed at US-159, gap 11). G1 to G7 (§10) closed one per message on my recommendations (owner, 2026-11-23); ADR 0165. Preview 19 (§7) is shown as an artifact that looks exactly like the game and approved before any UI code.
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Events, disasters and town history (the Chronicle, the Town Almanac, citizen stories); Moments that matter
- Related ADRs: 0036 (strings, Hindi later), 0043 (*Why?* in words), 0057 (specs a sprint ahead), 0063 (people age a life-year a season, only elders die), 0077 (literacy), 0098 (the Codex's toast, the model for a story's), 0101 (the year-end flow), 0107 (Phase 3 plan, §13 scope), 0127 and 0145 to 0148 (town life, events), 0153 (spec 028)
- Depends on specs: 014 (households, citizens, their story lines), 016 (businesses and their books), 017 (shops, newcomers), 018 (the school, literacy), 019 (the season page and *Your year*), 023 (the Township: clinic, road), 026 (festivals, petitions), 027 (events, disasters, loans)
- Stories: US-157 Chronicle (5), US-158 Almanac (3), US-159 Citizen stories (5); Sprint 26; save schema 24 (shared with spec 028's US-259 and US-260)

## 1. Goal

The town remembers. Every important thing that happens (the first child, a record harvest, a fire survived, a business sold) is written down in a warm voice as it happens; at each year's end the Almanac says what the year was; and a few people of the town, a hand who saves for a stall, a child who learns to read, an elder, a migrant family, get small stories the player sees unfold. The point is attachment: the player should care about *these* people and *this* town, and should see what their decisions cost and earned.

**Lesson:** the return on what you put in (best return) and what a single bad day costs (costliest loss), the part a good record plays in learning from a year, and that the people of a business are why it works.

## 2. Scope

**In (ADR 0107 §13):**
- **The Chronicle** (US-157): 22 kinds of moment (§3.2), each from an event the simulation already raises, kept as a compact record and worded from a string template when the sheet opens; the Menu's Chronicle sheet.
- **The Almanac** (US-158): at each Winter close after *Your year*, a card with the year's population, net worth, best return, costliest loss and a citizen spotlight; the last 8 are kept and read from the Chronicle sheet.
- **Citizen stories** (US-159): four arcs over real events (a hand's stall, a family's first literate child, an elder's long life, a migrant family that stays), one short toast for each step, at most one a season; the hand's stall adds one rule (§3.6).

**Out** (with where it goes instead):
- Illustrations, animation and music for a moment: Phase 5 polish (ADR 0107); the existing icons only.
- The scholarship child who returns as a doctor: needs a college, a Town building (Phase 4).
- Comparisons with other players' towns: not in the game (offline).
- A story for every citizen: only the four arcs, for your own people.
- Hindi wording: the strings come later (ADR 0036); this spec keeps every sentence in `content/strings/en.json`.

## 3. Rules and formulas

Everything is Fixed-point where a rate is used (ADR 0018); money is in 1/100 coin, shown in today's coins; division rounds half away from zero. Every number a sheet shows has a *Why?* in words, never a factor (ADR 0043). Values live in `content/chronicle.json` and `content/stories.json`.

### 3.1 What the Chronicle keeps (US-157, G1)

| Rule | Behaviour |
| --- | --- |
| An entry | `At` (sim time), `Kind` (one of the 22), up to three numbers or ids (`A`, `B`, `C`: a name index, a crop, an amount, a citizen id). About 24 bytes. **The sentence is made from a string template when the sheet opens**, so wording and language can change without touching a save. |
| How many | 400 entries. When full, the oldest entry of an ordinary (repeating) kind goes first; the once-only kinds, the eras and the founding are **pinned** and never dropped. |
| Order and reading | Newest first, grouped by game year; a line's icon follows its kind; a **?** opens the line's *Why?* with its numbers (§7). |
| Older saves | The Chronicle starts empty when schema 24 loads and writes **"The Chronicle begins"** at that moment; it back-fills only what state can still say with a date (the eras reached and the school, clinic and road built), dated at the load. Nothing else is invented. |
| Away | Moments that happen while the app was closed are written with their own times. |

### 3.2 The 22 kinds (US-157, G2)

**Once only (14), pinned:**

| Kind | Written when | Keeps | Line (English, a template) |
| --- | --- | --- | --- |
| Founding | The first day of a new town | acres | "You came to the valley with {n} acres and a plan." |
| Hamlet, Village, Township | `EraUnlocked` | households | "The farm became a {era}: {n} families now." |
| First settler | The first citizen who is not the founder's household arrives (`StoryKind.Arrived`) | name | "{name} was the first to settle on your land." |
| First child | The first `StoryKind.Born`/`ChildBorn` in town | name | "{name} was the first child born here." |
| First literate child | The first `CitizenLiterate` by school | name | "{name} was the first child in the valley to learn to read at your school." |
| First adult reader | The first `CitizenLiterate` by adult class (*in place of the first graduate: a graduate and a literate child are the same moment, so the second one is the adult class*) | name | "{name} learned to read as a grown-up, in your class." |
| First festival | The first `FestivalHeld` | festival | "The first {festival}: the whole town came." |
| School opened | `SchoolOpened` | - | "The school opened its doors." |
| Clinic built | `ClinicBuilt` | - | "The clinic opened: the town has a doctor." |
| Road built | `RoadBuilt` | - | "The road to the district was paved." |
| First orchard bearing | The first `OrchardBearing` | crop | "The first {tree} orchard bore fruit." |
| First profitable business | The first business season report with a profit above 0 | business | "Your {business} made its first profit." |

**Repeating (8), at most one of a kind a season** (a second in the same season is dropped, the biggest kept):

| Kind | Written when | Keeps | Line |
| --- | --- | --- | --- |
| Record harvest | §3.3 | crop, kg | "A record {crop} harvest: {kg}." |
| Business founded | A business finishes building (`BusinessOpened`) | business | "Your {business} opened." |
| Business sold | `BusinessSold` | business, price | "You sold your {business} for {price}." |
| Business closed | A business closes without a sale | business | "Your {business} closed its doors." |
| Fire survived | `FireStruck`, written when the repair is paid or the house stands again | cost | "Fire took part of a house; the town rebuilt it for {cost}." |
| Epidemic survived | `EpidemicEnded` | sick | "The sickness passed; {n} families were ill and nobody was lost." |
| Loan cleared | `LoanRepayment` when a loan's balance reaches 0 | amount | "You paid off {amount} to {lender}." |
| Petition kept | `PetitionEnded` with status kept | topic | "You kept your word to the town: {topic}." |

An arc's step (§3.6) writes a **Story** line of its own (not counted among the 22).

### 3.3 A record harvest (US-157, G3)

At each season close, for every crop, the **kilos harvested that season** (the sum of `CropHarvested` for the crop over all your fields) is a **record** when it is **at least 1,000 kg** and **at least 5% above the best earlier season of the same crop**. One record line a season: the crop with the biggest rise. The best of each crop is kept in the Chronicle's state; a crop's first season that reaches 1,000 kg sets its mark and is not a record. *Why?* names the crop, this season's kilos and the old best in words.

### 3.4 The Almanac (US-158, G4)

At each **Winter close**, after *Your year* and before Dadi's letters and the Codex toast (ADR 0101 §5), the Almanac is written (it is saved, so it never changes) and opens as a card; the last **8** are kept.

| Part | Rule |
| --- | --- |
| Population | Your households, and the old village's as its head-count, at the close. |
| Net worth | At the close, in year-0 coins, and the change from last year. |
| Best return | Of your **businesses** (the year's four season profits ÷ the capital you put in) and your **crops** (the year's income from the crop ÷ the year's seed and planting cost for it), the one with the biggest share, provided its profit is **at least 10,000 coins**; else **"a quiet year"**. Shown as "{name}: it earned {n} coins for every 100 you put in", with the profit and the money put in. |
| Costliest loss | The biggest single line from a **closed list of one-off losses**: a repair after a fire, a loss on selling or closing a business or a machine, spoiled goods, something seized or defaulted, a business's losing season (its loss). **Ordinary costs (wages, seed, rent, fuel) never count.** Shown with its cause. None, and the line says so, when the year had no such loss. |
| Citizen spotlight | §3.5. |

Each shows a **?** with its working (§7). Crops need a small accumulator (this year's income and seed and planting cost per crop), added at each sowing and season close.

### 3.5 The citizen spotlight (US-158, G5)

Look at **your own people's year** in a fixed order and take the first person who fits; ties go to the **lowest id**. No randomness: the same person, the same line, whenever the Almanac is opened.

1. A citizen whose **arc moved this year** (the latest step).
2. The first child to become literate this year.
3. The youngest baby born this year.
4. Your longest-serving worker (a hand or a business worker or manager).
5. The oldest living elder.

If none fits, the card says the year was a quiet one for the people. Each has one warm line and a *Why?* saying why this person.

### 3.6 Citizen stories (US-159, G6)

An **arc** is two to four **steps**, each a short toast with the person's portrait (§7); **at most one step a season** across the town (priority in the order below), and **one arc at a time for the same person**. Each step is also a Story line in the Chronicle. All numbers in `content/stories.json`.

| Arc | Steps | Rules |
| --- | --- | --- |
| **A stall of his own** (the one new rule) | 1. **Saving**: "{name} has put aside enough for a stall of their own." 2. **Leaving and opening**: "{name} has gone to start a {stall}." 3. **After two seasons**: "{name}'s {stall} is doing well." or "{name}'s stall closed; they would work for wages again." | A townsperson who has been your hand or a business's worker for **3 or more seasons**, whose household's savings are **at least 10,000 coins** (year-0), and who is on **fair or better terms** (a motivation of 1.0 or more). At each close, **each such person has a 20% chance** (seeded, never while you are away) to give a season's notice: step 1, and a line on the People sheet says they will go at the next close. At that close they leave (no severance owed; their post is empty, so you hire or run it yourself) and **the next newcomer stall that opens within 4 seasons is theirs** (named, on their trade's kind: a tea maker opens a tea stall, a stallholder a vegetable stall, a halwai a sweet shop, any other hand a vegetable stall): step 2. After 2 more seasons the stall is still open (step 3, doing well) or has closed (step 3, closed); a closed stall's owner is a townsperson looking for work again. **The stall is the village's, a rival, not yours** (it competes by the usual shop rules, spec 017). |
| **The first reader in the family** | 1. A child of one of your households starts school and **nobody in the family can read** yet. 2. They become literate: "the first in their family". 3. At 15 or older they take a paid job or a hire. | Read from `StoryKind` lines and `CitizenLiterate`. |
| **A long life** | 60, 70 and 80 years (a life-year is a season): "{name} is {age} and still part of the daily life of the {place}." If they die at 70 or more: "{name} died at {age}, remembered by {n} family members." | Read from the citizen's age (ADR 0063: only elders die, gently). |
| **A migrant family that stays** | 1. A migrant household stays **2 seasons**. 2. **4 seasons**. 3. A child is born to them. | Read from `StoryKind.Arrived` and `ChildBorn`. |

Priority when several steps fall on one close: the hand's stall, the first reader, the migrant family, the long life.

### 3.7 What happens when the player does nothing

Nothing is asked of the player. A hand's notice is a line, not a card; the town keeps working; the Chronicle fills by itself. A hand who leaves is a cost the player can see coming and can answer by hiring ahead or paying benefits (spec 028 §3.8, loyalty).

## 4. Data

**New definitions** (static content):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| chronicle | `keep` | int | 400 | 100 to 2,000 | Entries kept |
| chronicle | `pinned` | list of kinds | the 14 once-only | | Never dropped |
| chronicle | `recordMinGrams`, `recordRise` | int, Fixed | 1,000,000, 0.05 | | §3.3 |
| chronicle | `almanacKeep`, `minProfit` | int, long | 8, 10,000 | | §3.4 |
| chronicle | `lossKinds[]` | list of loss kinds | fire repair, sale or closing loss, spoiled goods, seizure or default, a business's losing season | | Costliest loss's kinds (G9; running worst, no ledger scan) |
| stories | `handSeasons`, `handSavings`, `handChance`, `handNotice`, `handWindow`, `handFollow` | int, long, Fixed, int, int, int | 3, 10,000, 0.20, 1, 4, 2 | | The hand's stall |
| stories | `elderAges[]`, `migrantSeasons[]` | list | 60, 70, 80; 2, 4 | | |
| stories | `perSeason` | int | 1 | 1 to 3 | At most one step a season |

**New state** (saved, schema 24, each hashed only when used):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `ChronicleState` | `Entries` (At, Kind, A, B, C), `Done` (a bit per once-only kind), `LastSeason[kind]`, `SeasonGrams[crop]`, `BestGrams[crop]` | | Written and read in id order |
| `AlmanacState` | `Entries[]` (year, households, net worth, change, best return (kind, id, profit, put in), costliest loss (kind, amount, cause), spotlight (citizen id, why)), `YearCrops[crop]` (income, cost) | | The last 8 |
| `StoriesState` | `Arcs[]` (person id, arc, step, at, other), `LastStepSeason` | | |

## 5. Commands and events

**Commands:** none. The Chronicle and the stories are read-only for the player; a hand's notice has no card (§3.7).

**Domain events** (what the simulation announces):

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `ChronicleWritten` | kind | An entry is written | The Chronicle sheet, the bell |
| `AlmanacWritten` | year | At each Winter close | The Almanac card |
| `StoryStepped` | arc, step, citizen | An arc step happens | The toast, the Chronicle |
| `HandGaveNotice` / `HandLeftToStartStall` | citizen | Step 1 and the close after | The People sheet, the away card |

## 6. Decision card

The player makes no decision in this feature; what they see is the cost of the ones they made. The one decision it creates is in spec 028's benefits: keeping a good hand (a raise, benefits) against losing them to a stall of their own.

## 7. UI

- **Menu → Chronicle**, two tabs: **Timeline** (newest first, by game year; an icon, a warm line, a date, a **?** on any line) and **Almanacs** (the last 8).
- **The Almanac card** opens at the Winter close after *Your year*: a title "Year {n} in the valley", the population, the net worth with its change, the best return (a **?**), the costliest loss (a **?**), the citizen spotlight with their portrait and a line; buttons **Open the Chronicle** and **Close**.
- **A story's toast**: 6 seconds, the person's portrait card and one line, like a Codex card (ADR 0098): it waits for open sheets and letters, never stays, never blocks a tap.
- **The People sheet** shows "X will leave at the close to start a stall" on a person who gave notice.
- **Preview 19** (the Timeline, an Almanac card, a story's toast, the People sheet's notice) is made first as an artifact that looks exactly like the game and approved before any UI code.
- **Why? rows:** every timeline line's numbers; the Almanac's best return (profit, money put in, the share in words), costliest loss (the cause), spotlight (why this person); a story step (what led to it).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Each once-only kind is written once, at its event, with its numbers; a second occurrence writes nothing | `ChronicleTests.Once_Only` |
| AC-2 | Each repeating kind writes at most one a season, the biggest kept | `ChronicleTests.Repeating` |
| AC-3 | A record harvest: the 1,000 kg floor, 5% above the best, one a season, a first season sets the mark | `ChronicleTests.Record_Harvest` |
| AC-4 | 400 entries: the oldest ordinary entry goes first, the pinned never | `ChronicleTests.Cap` |
| AC-5 | An older save loads with an empty Chronicle, "The Chronicle begins" and the readable firsts | `MigrationTests.Schema24_Chronicle` |
| AC-6 | The Almanac is written at the Winter close, saved, the last 8 kept, the same when opened again | `AlmanacTests.Written_Saved` |
| AC-7 | Best return: ratio of the year's profit to the money put in, the 10,000 floor, crops by income over seed cost, "a quiet year" | `AlmanacTests.Best_Return` |
| AC-8 | Costliest loss: only the closed list; ordinary costs never count; none when none | `AlmanacTests.Costliest_Loss` |
| AC-9 | The spotlight follows the fixed order, ties by the lowest id, the same each time | `AlmanacTests.Spotlight` |
| AC-10 | The hand's stall: eligibility, the 20% chance, the notice, the leaving, the newcomer being theirs, the follow-up, a closed stall | `StoryTests.Hand_Stall` |
| AC-11 | The first reader, the long life and the migrant family step at their moments | `StoryTests.Reader`, `StoryTests.Long_Life`, `StoryTests.Migrant_Family` |
| AC-12 | At most one story step a season, in the stated priority; one arc at a time for a person | `StoryTests.One_A_Season` |
| AC-13 | The Chronicle, the Almanac card, the toast and the People notice respond to scripted touch in the Editor | the Editor tour and render statistics |
| AC-14 | Every number on the new sheets has a *Why?* in words; no raw factors | `WhyTests.EveryNumberHasWhy` (over the new situations) |
| AC-15 | Schema 24 round-trips; an older save loads with all of it empty and a town unchanged (hashed only when used); the fixture carries on through two closes | `MigrationTests.Schema24_People` |
| AC-16 | The ledger stays exact: a hand's leaving moves no money; nothing is created by a story | `GrowthLedgerTests.Reconciles` |
| AC-17 | A town that answers nothing still works; no zero-time loop | `GateTests.Growth` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Chronicle size | about 25 to 40 entries a game year in a town of 25 households | measured, reported |
| Story steps | about one a season in a Township town; none stacking | measured |
| A hand's stall | about one hand in five leaves in a town's first 12 seasons; replaced by the board within a season | measured |
| Almanac | a best return exists in at least 9 of 10 Township years | measured |
| Save size | the Chronicle adds under 12 KB at 400 entries | exact |

## 10. Open questions

All closed (owner, 2026-11-23, my recommendations; ADR 0165):

- **G1 What the Chronicle keeps.** Compact entries (kind and up to three numbers), the sentence made at view time, 400 kept with the firsts pinned, schema 24.
- **G2 Which moments.** 22 kinds, the firsts once (14) and eight repeating at most one a season. *Note: "first graduate" became "first adult reader", because a graduate and a literate child are the same moment.*
- **G3 A record harvest.** A crop's season total at least 1,000 kg and 5% over its best, one a season.
- **G4 The Almanac's verdicts.** Best return as a ratio (businesses and crops, a 10,000 floor); costliest loss from a closed list of one-off losses.
- **G5 The spotlight.** A fixed story priority, ties by lowest id, never random.
- **G6 The arcs.** Four arcs over real events; the only new rule is the hand's stall (a seeded 20% chance, a season's notice, the next newcomer stall).
- **G7 The screens.** A Menu sheet with two tabs, the Almanac card at the Winter close, a toast for a step; no animation or music.
- **G8 Events that lack a line's facts. Closed (owner, 2026-11-23, my recommendation, ADR 0166 gap 10):** add the fields to the events (`EpidemicEnded` the sick, a loan cleared event with the lender, the fire's repair payment), one `ChronicleSystem` reading the day's events with their own times.
- **G9 The costliest loss. Closed (owner, 2026-11-23, my recommendation, ADR 0166 gap 12):** the Almanac keeps a running worst one-off loss for the year, updated at the moment it happens; no ledger scan; `lossLines` in §4 is a list of kinds, not accounts.

## 11. Cut first

If Sprint 26 runs over (with spec 028's US-259 and US-260 and US-248, US-249): the hand's stall rule and its notice (the arc becomes a story only when a hand leaves by the existing rules), then the long-life arc, then the Almanac's crop returns (businesses only). **Never cut:** the Chronicle's record and sheet, the Almanac card, every number's *Why?*.
