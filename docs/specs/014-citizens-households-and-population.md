# 014. Citizens, households and population

- Status: Draft (Sprint 12). Gaps in §10 to be closed with the owner one at a time
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Citizens and workforce (Citizen profile, Population flows); Economy (Household spending); Community and civic services (Needs layers); Time and world simulation (Life clock); UI/UX (People)
- Amends: spec 006 (a hand is a citizen with an age and a household; retirement), spec 002 (the family house on owned land), spec 004 (households' food adds to the local market's demand), spec 005 (account 4200 Rent received), spec 007 (save schema), spec 008 (the HUD's population and happiness; the Town tab)
- Related ADRs: 0002 (the life clock: 1 life-year a season), 0003 (the founder doesn't age), 0022 (the first citizens are hands), 0011 (school and literacy), 0053 (the village centre is neighbours), 0057 (spec order)
- Depends on specs: 001, 003, 004, 005, 006, 010

## 1. Goal

The player's farm grows into a settlement of real families: people move in for work and a home, have children, grow old and pass on, and each household's spending, needs and happiness are visible and explained.

**Lesson:** a household's income decides what it can spend on, and the poorer it is the larger the share that goes on food (Engel's law); a town grows when it offers jobs, homes and the basics, and people leave when it doesn't.

## 2. Scope

**In:**
- Citizens: identity, gender, age and life stage on the life clock, education, skills, traits, health, happiness, job, story.
- Households: members, home, income, a budget by Engel's law, savings, needs met, happiness.
- The family house: a home the founder builds on owned land and rents out.
- Births, ageing, retirement, deaths of the old; marriages that start new households.
- Migration in (families for an empty house) and out (unhappy households).
- Needs layers and happiness for Phase 2 (what exists by the Village era).
- Households buying food at the local market.
- Phase 1 hands as citizens (save migration).
- The People sheet's Town tab, the household and citizen cards, the family tree, the HUD's population and happiness.

**Out** (and where it goes instead):
- Hiring beyond the hand (job posts for every role, interviews, probation, HR events): spec 015.
- Jobs in businesses: spec 016. Shops, customer choice: spec 017.
- The school, literacy and children's education: spec 018 (children are "not in school yet" until then).
- Clinics, life expectancy from healthcare, water and sanitation: Phase 3 (civic services).
- Festivals, crime, petitions, inequality: Phase 3.
- Cohort simulation for large towns: Phase 4 (F17.1).
- Child deaths, accidents, adults dying young: not planned (§10 G7).

## 3. Rules and formulas

Money is `long` in 1/100 coin; factors `Fixed`; seeded draws from new streams Citizens, Births, Deaths, Migration.

### 3.1 Who is a citizen (§10 G1)

| Rule | Behaviour |
| --- | --- |
| The town | People who live on the founder's land: in a hand's hut or a family house. They are the population |
| The founder | Not a citizen (ADR 0003): doesn't age, isn't counted |
| The old village centre | The houses, grocer, chemist and temple east of the farm are neighbours (ADR 0053): drawn and named, never simulated as citizens, never counted. Their people are the day-labour roster and the market's existing demand |
| Day labourers | Neighbours who commute: not citizens (spec 006) |

### 3.2 The life clock (ADR 0002; §10 G3)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Ageing | Every citizen ages `lifeYearsPerSeason` at each season close | 1 |
| Life stages | Child 0–17, adult 18–59, elder 60+ | |
| Birthdays | A citizen's age shows as whole years; their birth season is kept | |
| Retirement | An elder doesn't take a new job; a hand turning 60 finishes his season and retires (his contract isn't renewed; he stays home) | 60 |
| On screen | "People age a year each season (a year of their life every 4 days)" in the citizen card's *Why?*. Animals don't age in Phase 2 (spec 012) | |

### 3.3 Citizens

| Field | Rule |
| --- | --- |
| Identity | Seeded name by gender from `people.json` names (men and women in equal numbers, ADR 0039 art direction), gender, birth season, household, home |
| Skills (0–100) | Farming (spec 006's skill) for Phase 2; the GDD's other skills arrive with the jobs that use them (specs 015–016). Children 0; adults by migration draw |
| Traits | Two, as spec 006 (hardworking, lazy, loyal, ambitious, careless, steady) |
| Education | None or primary for adults, drawn at arrival (60% primary, 40% none); children "not in school yet" until spec 018 |
| Health | 0.5–1.0: falls when food isn't met, recovers when it is (as the founder's, spec 003 §3.11, slower) |
| Job | None, the founder's hand, "works outside" (§3.6), or later a business (spec 016) |
| Story | Arrival, marriage, children, retirement, passing, kept as lines for the card |

### 3.4 Households and homes (§10 G2)

| Rule | Behaviour | Default |
| --- | --- | --- |
| A household | A family sharing a home: a couple, their children, sometimes an elder parent. Each citizen belongs to exactly one | |
| A hand's hut | Houses one hand alone (spec 006): a household of one whose family stays in his home village; he sends half his wage home (`remitShare`) | 0.5 |
| The family house | `BuildFamilyHouse(tile)` on an owned acre that isn't growing anything: `BuildingDef(family_house).value` 60,000 (a two-room pucca house), 48 h to build, houses `houseCapacity` people; an acre takes `housesPerAcre` houses and is no longer farmed while any stands. Also on the labour quarters' free places | 6 people; 4 an acre |
| Rent | A household in a family house pays `rentPerSeason` × index at each season close (Dr Cash, Cr 4200 Rent received); a household that can't pay it falls behind and its security need is unmet (§3.8); after 2 seasons behind it leaves (§3.10). A hand's household in a family house pays nothing (a benefit; spec 015 makes it a choice) | 6,000 |
| Moving up | A hand whose family house is free can "send for his family" (§3.10) | |
| Overcrowding | More people than `houseCapacity` leaves shelter partly unmet (§3.8) | |

**Why 60,000 and 6,000:** a two-room rural house costs about ₹3–5 lakh to build and rents for about ₹2,000 a month; the game keeps the founder's houses cheap as it does huts (family labour on the build) and keeps the rent in proportion, so a house pays back in about 10 seasons, like land (ADR 0028).

### 3.5 Household budget: Engel's law (§10 G4)

At each season close, after rent, each household sets its budget from its income for the season ahead:

```
income      = Σ members' wages for the season (paid upfront, spec 006) + expected outside earnings (§3.6) − remittances
perPerson   = income ÷ adultEquivalents          (adult 1.0, child 0.6, elder 0.8)
foodShare   = EngelCurve(perPerson)              (piecewise linear, content)
food        = income × foodShare,  rent,  clothing 8%,  health 5%,  education 5%,  leisure the rest's half,  savings the rest
```

| Rule | Behaviour | Default |
| --- | --- | --- |
| Engel curve | Points (income a person a season → food share): [[0, 0.65], [8,000, 0.55], [20,000, 0.40], [60,000, 0.25]] | |
| Food need | `foodBasketPerPerson` × index × adult equivalents: what eating enough costs a season | 4,500 |
| Food met | min(1, food budget ÷ food need). Below 1 the household eats less: survival partly unmet, health falls | |
| Savings | What's left adds to the household's savings; a bad season draws on them before food is cut | |
| Why | The household card shows the pie and "Food takes 55% because they earn 9,000 a person a season: poorer families spend more of their money on food (Engel's law)" | |

### 3.6 Work and income

| Rule | Behaviour | Default |
| --- | --- | --- |
| A hand | His wage (spec 006) is his household's income | |
| Works outside | Every adult under 60 without a job in town works as a day labourer in the region on `outsideDaysShare` of days at `outsideWage` × index, paid at the daily pass | 60%; 6,000 a game day |
| Employment rate | Adults with a job in town ÷ adults of working age (a town statistic; the Town era needs 85%, Phase 3) | |
| Children | Never work (GDD: no child labour) | |
| Elders | Don't work; they may live on savings or with family | |

### 3.7 Households buy food at the local market (amends spec 004 §3.2; §10 G10)

| Rule | Behaviour | Default |
| --- | --- | --- |
| The basket | Per adult equivalent per real day: cereals 0.40 kg (wheat or rice, half each), lentils 0.05 kg, potato 0.10 kg, vegetables 0.15 kg (radish, spinach), milk 0.25 L; × 22.8 real days a game day | content |
| Demand | Each hour the town's households add `basket ÷ 24` to the local market's demand at the regional price, scaled by food met (a household short of money buys less): `D_ref(good) += Σ households` | |
| Effect | The founder's stall sells more as the town grows; a town of 80 eats about 2 acres of grain a season. Shown on the good's *Why?*: "The town buys about 640 kg a day" | |
| Milk | Bought from the co-op, not the market (spec 012) | |

### 3.8 Needs and happiness (§10 G5)

Each household's needs are scored 0–1 at each daily pass:

| Layer | Weight | Needs in Phase 2 | Met when |
| --- | --- | --- | --- |
| Survival | 0.40 | Food, shelter, water | Food met (§3.5); members ≤ capacity (scaled down above it); water always met (the well and the pond) |
| Security | 0.30 | A stable job, health care, rent paid | Share of working-age adults with a town job; the chemist in the village (0.5 until a clinic, Phase 3); rent not behind |
| Belonging | 0.20 | A place to meet and pray | The temple and the banyan square in the village centre: met (0.8; festivals later raise it) |
| Growth | 0.10 | School, leisure | No school yet: 0 until spec 018; leisure budget > 0 gives 0.3 |

```
met(survival) = the least of its needs (enough water doesn't make up for too little food); every other layer: the mean of its needs
gate(layer) = min(1, met(previous layer) ÷ 0.8)            (survival is never gated)
happiness   = 100 × Σ weight × met × gate                    (0–100)
town happiness = mean over citizens (each citizen takes their household's)
```

A Phase 2 household with enough food, a home, a hand's job and rent paid scores about 40 + 30 × (1 + 0.5 + 1) ÷ 3 + 20 × 0.8 + 10 × 0.3 = **84**; one living on outside work only scores about 40 + 15 + 16 + 3 = **74**; one with a hand's job but short of food (food met 0.6, so security is gated at 0.75) scores 24 + 18.75 + 16 + 3 = **62** (rounded) with health falling.

### 3.9 Births, marriages and deaths (§10 G3, G7)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Births | At each season close, a household with a couple whose woman is 20–40 has a baby with chance `birthChance` × (room in the home ? 1 : 0.3) × (0.6 + 0.4 × happiness ÷ 100), fewer than `maxChildren` children. Seeded (stream Births). Emits `CitizenBorn` | 0.10; 3 |
| Marriage | An adult child aged 22–30 marries with chance `marriageChance` a season; the spouse moves in from another village. If a family house is free the couple moves into it (a new household); otherwise they stay with the parents (crowding) | 0.25 |
| Deaths | Only elders: at each season close, chance `deathBase × 2^((age − 60) ÷ deathDoubling)`, capped at 0.5, × (1.5 if health < 0.7). Seeded (stream Deaths). The household mourns (happiness −10 for a season). A widowed elder stays with family. Emits `CitizenDied` | 0.015; 8 years |
| Life expectancy | About 72 with these defaults (rural India about 70); healthcare raises it in Phase 3 | |
| While away | All of these happen while away and show on the away card | |

### 3.10 Migration (§10 G6)

| Rule | Behaviour | Default |
| --- | --- | --- |
| A family moves in | While a family house stands empty, a family arrives every `familyEveryHours` ± seeded jitter: 2–5 people (a couple, 0–3 children, sometimes an elder), adults with skills and traits drawn as applicants (spec 006). They move in at once and pay rent from the next close. Live or away | 18 h ± 6 |
| A hand's family | A hand under contract whose household is just him, with a free family house: at the next season close he asks to bring his family (a card: Yes / Not now); yes moves him and a seeded wife and 0–2 children into the house; his hut is free for another hand | |
| Moving out | A household whose happiness stays below `leaveBelow` for `leaveSeasons` season closes, or is 2 seasons behind on rent, leaves (seeded, 50% a close once eligible). A hand's household leaving ends his contract (spec 006 dismissal rules, no severance). Emits `HouseholdLeft` with the reason | 35; 2 |
| Population | Citizens in the town; the HUD shows it with town happiness | |

### 3.11 Phase 1 hands (save migration)

A save's hands become citizens: a seeded age 22–45, gender male (Phase 1's hands were drawn as men's names; a woman's name keeps her gender), a household of one in their hut, education drawn, health 1. Applicants draw a gender and an age 20–45 from now on.

### 3.12 Processing order

Hourly pass, system Population: families arrive. Daily pass, system Population: outside work paid, needs and happiness updated, food bought (demand). Seasonal pass, after spec 005's settlement: rent, ageing, retirement, deaths, births, marriages, budgets for the new season, moving out, hands' family requests. Citizens and households in id order.

## 4. Data

**New definitions** (`content/population.json` unless noted): `lifeYearsPerSeason`, the life stages, `retireAge`, `remitShare`, `outsideDaysShare`, `outsideWage`, the Engel curve, budget shares, `foodBasketPerPerson`, the basket by good, adult equivalents, the needs weights and gate, `birthChance`, `maxChildren`, `marriageChance`, `deathBase`, `deathDoubling`, `familyEveryHours`, `leaveBelow`, `leaveSeasons`, the education draw; `family_house` in `buildings.json` (`value` 60,000, `buildHours` 48, `houseCapacity` 6, `housesPerAcre` 4, `rentPerSeason` 6,000); names with gender in `people.json`.

**New state** (save schema 8): `Citizen` (id, name, gender, birthSeason, householdId, home, education, skill, traits, health, job, parents, spouse, story lines, diedAt), `Household` (id, members, home, income, budget, savings, rentBehind, needs met, happiness, lowSeasons), `CitizensState` (citizens, households, next ids, the next family's time). The departed and the dead stay in the record for the family tree (up to `historyCap` 500, oldest first dropped).

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `BuildFamilyHouse` | `tile` | `NotOwned`, `TileOccupied` (growing), `NoRoom`, `InsufficientFunds` | §3.4 |
| `AnswerFamilyRequest` | `handId`, `yes` | `UnknownHand`, `NoEmptyHouse` | §3.10 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `FamilyHouseBuilt`, `FamilyMovedIn`, `HouseholdLeft` | household, reason | Map, away card, notifications |
| `CitizenBorn`, `CitizenMarried`, `CitizenDied`, `CitizenRetired` | citizen | Away card, the Chronicle later |
| `FamilyRequest` | hand | A card, alerts |
| `RentPaid`, `RentMissed` | household, amount | Money, away card |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Build a family house | 60,000; the acre stops farming | Rent 6,000 a season; a family who can work for you | More demand for your produce | No (Phase 2) |
| Let a hand bring his family | — | He stays longer (happier); his hut frees up | Children, a household to keep happy | No |
| Pay above the market (spec 006) | Wages | Happier households, fewer leave | | Next renewal |

## 7. UI

```
HUD:  [☀ 7:10 · Spring 2]  [👥 23 · 😊 78]  [₹ 14,200]

People sheet:  [Workers]  [Town]
Town tab:
| 23 people in 6 households · happiness 78 [?] · 4 of 9 adults work in town [?] |
| Manoj's family (4)  Family house 2 · income 32,000 a season · 😊 84  [›] |
| Rekha's family (5)  Family house 3 · works outside · 😊 72          [›] |
| [Build a family house 60,000]                                           |

Household card: members (age, stage, job), the budget pie with Engel's *Why?*, needs by layer with *Why?*, savings, rent.
Citizen card: portrait, age and stage, job, skill, traits, health, story lines, the family tree (parents, spouse, children; the departed greyed).
```

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | At each season close every citizen ages one year; a hand turning 60 isn't renewed and retires | `CitizenTests.Ageing_Retirement` |
| AC-2 | A family house on an owned empty acre costs 60,000, builds in 48 h, holds 6, 4 an acre; the acre can't be sown while one stands | `HouseTests.Build_Limits` |
| AC-3 | An empty house brings a family of 2–5 every 18 ± 6 h, live or away; they pay rent at the next close (4200) | `MigrationTests2.FamilyMovesIn` |
| AC-4 | The budget follows the Engel curve; food met = budget ÷ need; savings cover a short season first | `HouseholdTests.Budget_Engel` |
| AC-5 | Happiness follows the layers, weights and gate; the three examples in §3.8 score 84, 74 and 62 | `HappinessTests.Layers` |
| AC-6 | Births, marriages and deaths follow their chances (seeded, same seed same town); no child or adult under 60 dies | `PopulationTests.Flows` |
| AC-7 | A household below 35 happiness for 2 closes, or 2 seasons behind on rent, leaves (50% a close); a hand's leaving ends his contract | `MigrationTests2.MovingOut` |
| AC-8 | Households add their basket to the local market's demand each hour | `MarketTests.TownDemand` |
| AC-9 | A schema-7 save's hands become citizens in households of one; it advances a season and round-trips | `MigrationTests.Schema7_Citizens` |
| AC-10 | The golden replays are unchanged; a year of the bot gives the same hash in one chunk or many | `ReplayTests.*`, `CitizenTests.ChunkingInvariance` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Population when the Village unlocks (spec 018) | 15–25 | ADR 0028 pacing |
| Town happiness, competent bot | 70–85 | |
| Households leaving a year, competent bot | Under 1 in 10 | |
| Rent's share of the founder's income | Small | Under 15% |
| Town food demand vs the founder's grain | A town of 80 eats about 2 acres' grain a season | |

## 10. Open questions

- **G1. Who counts.** People on the founder's land only; the old village centre stays neighbours. Recommended.
- **G2. Homes.** A family house (60,000, 6 people, 4 an acre, rent 6,000 a season) built by the founder; a hand's hut stays single. Recommended.
- **G3. The life clock and lifespans.** 1 life-year a season (ADR 0002); retirement at 60; deaths only for elders, about 72 years' expectancy; births 0.10 a season for a couple, at most 3 children. Recommended.
- **G4. Engel's law and the budget.** The curve, the food basket and the shares above. Recommended.
- **G5. Needs and happiness in Phase 2.** The four layers with what exists by the Village (no school, no clinic yet). Recommended.
- **G6. Migration.** Families for empty houses live or away; a hand can bring his family; unhappy or rent-behind households leave. Recommended.
- **G7. Death and tone.** Only elders die, gently worded; no child or young adult deaths. Recommended.
- **G8. Work for everyone.** Adults without a town job work outside at 60% of days; employment rate counts only town jobs. Recommended.
- **G9. The family tree.** Parents, spouse and children on the citizen card; the departed kept up to 500. Recommended.
- **G10. Households buy food locally.** Their basket adds to the market's demand, so a growing town buys the founder's produce. Recommended.

## 11. Cut first

1. Marriages (households only arrive and leave).
2. The family tree (a list of relatives instead).
3. A hand bringing his family.
4. Households buying food at the local market (spec 017 instead).
