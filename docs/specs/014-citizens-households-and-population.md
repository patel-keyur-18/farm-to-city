# 014. Citizens, households and population

- Status: Approved 2026-10-23 (ADR 0063); amended by ADR 0067 (2026-10-24: 2 family houses an acre, the row-house layout) and the owner's §3.8 decision (the outside-work example scores 68)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Citizens and workforce (Citizen profile, Population flows); Economy (Household spending); Community and civic services (Needs layers); Time and world simulation (Life clock); UI/UX (People)
- Amends: spec 006 (a hand is a citizen with an age and a household; retirement; a family house as part of his pay; labourers hired from the town first), spec 002 (the family house on owned land), spec 004 (households' food adds to the local market's demand), spec 005 (account 4200 Rent received), spec 007 (save schema), spec 008 (the HUD's population and happiness; the Town tab)
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
- Child deaths, accidents, adults dying young, funeral costs: not planned (§10 G7).

## 3. Rules and formulas

Money is `long` in 1/100 coin; factors `Fixed`; seeded draws from new streams Citizens, Births, Deaths, Migration.

### 3.1 Who is a citizen (§10 G1)

| Rule | Behaviour |
| --- | --- |
| The town | People who live on the founder's land: in a hand's hut or a family house. They are the population |
| The founder | Not a citizen (ADR 0003): doesn't age, isn't counted |
| The village is the town (ADR 0061) | The farm is part of the village; the centre's places (temple, banyan square, grocer, tea stall, chemist, council hall, school plot, dealer, dairy co-op) are the town's and serve the player's citizens |
| The old families | The families already living in the centre's houses are neighbours (ADR 0053, 0061): drawn and named, never simulated as citizens, never counted. Their people are the day-labour roster and the market's existing demand. The HUD shows "Your people". They may join the citizen model in Phase 3 (noted, not decided) |
| Day labourers | Neighbours who commute: not citizens (spec 006) |

### 3.2 The life clock (ADR 0002; §10 G3)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Ageing | Every citizen ages `lifeYearsPerSeason` at each season close | 1 |
| Life stages | Child 0–17, adult 18–59, elder 60+ | |
| Birthdays | A citizen's age shows as whole years; their birth season is kept | |
| Retirement | An elder doesn't take a new job; a hand turning 60 finishes his season and retires (his contract isn't renewed): alone, he goes home to his village and frees the hut; with his family in a family house, he stays as its elder (§3.13) | 60 |
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
| Family (§10 G9) | Parents and spouse by id; children and siblings found from them. A spouse who married in shows "from Sitapur" (parents unknown); a Phase 1 hand shows "family in his home village" until his family moves in |
| The departed | Someone who passed away or moved away is kept only while a citizen living in town is their spouse, child or grandchild (or, for a daughter who married away, a parent: ADR 0070); dropped at the season close when none is. At most two generations back from anyone alive, so it never grows without end (about 0.5 departed a living citizen; a town of 80 keeps about 40, about 4 KB) |

### 3.4 Households and homes (§10 G2)

| Rule | Behaviour | Default |
| --- | --- | --- |
| A household | A family sharing a home: a couple, their children, sometimes an elder parent. Each citizen belongs to exactly one | |
| A hand's hut | Houses one hand alone (spec 006): a household of one whose family stays in his home village; he sends half his wage home (`remitShare`) | 0.5 |
| The family house | `BuildFamilyHouse(tile)` on an owned acre that isn't growing anything: `BuildingDef(family_house).value` 60,000 (a two-room pucca house), 48 h to build, houses `houseCapacity` people; an acre takes `housesPerAcre` houses and is no longer farmed while any stands; on the map two homes share a wall at the back of the acre, their courtyards opening onto open ground (ADR 0067). Also on the labour quarters' free places | 6 people; 2 an acre (ADR 0067; was 4) |
| Rent | A household in a family house pays `rentPerSeason` × index at each season close (Dr Cash, Cr 4200 Rent received); a household that can't pay it falls behind and its security need is unmet (§3.8); after 2 seasons behind it leaves (§3.10). A hand's household in a family house pays no rent: the house is part of his pay instead (§3.6) | 6,000 |
| Moving up | A hand whose family house is free can "send for his family" (§3.10) | |
| The home loan (ADR 0071) | `BuildFamilyHouse(tile, financing)`: the co-op bank lends up to the house's cost, counting family houses at `houseLtv` 0.75 of their book value in its limit (as the dairy loan counts animals); the rest is paid down. A house on inherited land is never seized: the bank carries that risk | 0.75 |
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
| Housing in kind (§10 G6) | While a hand's family lives in your family house, his cash wage is cut by the house's rent: `rentPerSeason` × index off the wage asked at each renewal, from the first renewal after they move in (season wages are paid upfront, ADR 0008, so never mid-season). It applies to a loyal hand's kept wage too. If the family moves out, his full wage returns at the next renewal. The hand's card shows "Housing: family house (worth 6,000 a season)" and the wage's *Why?* shows "30,000 − 6,000 for the family house = 24,000 in cash". Only the house is deducted: the founder gives no food or care. For the founder it is the same as renting the house out (6,000 less rent, 6,000 less wage) | 6,000 |
| Works outside (§10 G8) | Every adult 18–59, men and women, without a job in town works as a day labourer in the region: `outsideWage` = the founder's day-labour rate (`dayLabourWagePerHour` × 8 h) × index, × `outsideDaysShare`, paid at each daily pass (the expected share, no daily roll, so luck never swings a family's season). *Why?*: "Finds work about half the days: 9,000 × 0.5". A season: 4 × 0.5 × 9,000 = 18,000 an adult (a hand earns 30,000) | 50%; 9,000 a game day |
| Your labourers first (amends spec 006) | When the founder hires day labourers, adults in town who work outside are taken first: they earn the labourer's day wage (9,375) for their household, in place of that day's outside work; neighbouring villages fill the rest as before | |
| Employment rate | Adults with a job in town (a hand now, businesses from spec 016) ÷ adults of working age; outside work and day labour don't count (a town statistic; the Town era needs 85%, Phase 3) | |
| Children | Never work (GDD: no child labour) | |
| Elders | Don't work; they may live on savings or with family | |

### 3.7 Households buy food at the local market (amends spec 004 §3.2; §10 G10)

| Rule | Behaviour | Default |
| --- | --- | --- |
| The basket | Per adult equivalent per real day: cereals 0.40 kg (wheat or rice, half each), lentils 0.05 kg, potato 0.10 kg, vegetables 0.15 kg (radish, spinach), milk 0.25 L; × 22.8 real days a game day | content |
| Demand | Each hour the town's households add `basket ÷ 24` to the local market's demand at the regional price, scaled by food met (a household short of money buys less): `D_ref(good) += Σ households` | |
| Effect | The local market is a village of about 80 (wheat demand about 300 kg a game day), so a town of 80 (about 65 adult equivalents) about doubles it: a game day's buying is wheat 296 kg, rice 296, potato 148, lentils 74, radish 111, spinach 111; a season's cereals about 2,370 kg, 2 acres of wheat. The founder's stall sells faster (demand above the valley's supply). Shown on the good's *Why?* and the Store's market row: "Your town buys about 296 kg of wheat a day" | |
| No spike | The price stays within the import ceiling (regional + transport, ADR 0012: wheat at most 25 → 26); merchants bring what the valley doesn't grow, so the shops never run out: a household's food met depends only on its budget (§3.5). The town lifts how much the founder sells, not the price | |
| Milk | Bought from the co-op, not the market (spec 012) | |

### 3.8 Needs and happiness (§10 G5)

Each household's needs are scored 0–1 at each daily pass:

| Layer | Weight | Needs in Phase 2 | Met when |
| --- | --- | --- | --- |
| Survival | 0.40 | Food, shelter, water | Food met (§3.5); members ≤ capacity (scaled down above it); water always met (the well and the pond) |
| Security | 0.30 | A stable job, health care, rent paid | Share of working-age adults with a town job (met when nobody in the household is of working age); the chemist in the village (0.5 until a clinic, Phase 3); rent not behind |
| Belonging | 0.20 | A place to meet and pray | The temple and the banyan square in the village centre: met (0.8; festivals later raise it) |
| Growth | 0.10 | School, leisure | No school yet: not counted until spec 018 builds one (the layer is leisure alone until then); leisure budget > 0 gives 0.3 |

```
met(survival) = the least of its needs (enough water doesn't make up for too little food); every other layer: the mean of its needs
gate(layer) = min(1, met(previous layer) ÷ 0.8)            (survival is never gated)
happiness   = 100 × Σ weight × met × gate                    (0–100)
town happiness = mean over citizens (each citizen takes their household's)
```

A Phase 2 household with enough food, a home, a hand's job and rent paid scores about 40 + 30 × (1 + 0.5 + 1) ÷ 3 + 20 × 0.8 + 10 × 0.3 = **84**; one living on outside work only scores about 40 + 15 + 10 + 3 = **68** (its security of 0.5 gates belonging at 0.625; corrected from 74 by the owner, 2026-10-24, keeping the rule); one with a hand's job but short of food (food met 0.6, so security is gated at 0.75) scores 24 + 18.75 + 16 + 3 = **62** (rounded) with health falling.

### 3.9 Births, marriages and deaths (§10 G3, G7)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Births | At each season close, a household with a couple whose woman is 20–40 has a baby with chance `birthChance` × (room in the home ? 1 : 0.3) × (0.6 + 0.4 × happiness ÷ 100), fewer than `maxChildren` children. Seeded (stream Births). Emits `CitizenBorn` | 0.10; 3 |
| Marriage (ADR 0070) | An unmarried adult child aged 22–30 living with a parent marries with chance `marriageChance` a season. Patrilocal, as in rural India: a son's bride moves in from another village (0–4 years younger); if a family house is free the couple moves into it (a new household), otherwise they stay with his parents (crowding). A daughter marries away to her husband's village ("Asha married and moved to her husband's village, Rampur") and is kept in the tree while a parent lives. Emits `CitizenMarried` | 0.25 |
| Deaths | Only elders: at each season close, chance `deathBase × 2^((age − 60) ÷ deathDoubling)`, capped at 0.5, × (1.5 if health < 0.7). Seeded (stream Deaths). The household mourns (happiness −10 for a season). A widowed elder stays with family; an elder living alone leaves the house empty for a new family (§3.10); savings stay with the household; the departed stay in the family tree, greyed. Emits `CitizenDied` | 0.015; 8 years |
| Tone (§10 G7) | Worded gently and without blame: "Kamla Devi (76), Manoj's mother, passed away peacefully. Her family mourns this season." On the away card and the Town tab only, as information: no sound, no push notification. Her *Why?*: "At 76, about 4% a season; health 0.9". A hand never dies under contract (he retires at 60) | |
| Life expectancy | About 72 with these defaults (rural India about 70); healthcare raises it in Phase 3 | |
| While away | All of these happen while away and show on the away card | |

### 3.10 Migration (§10 G6)

| Rule | Behaviour | Default |
| --- | --- | --- |
| A family moves in | While a family house stands empty, a family arrives every `familyEveryHours` ± seeded jitter: 2–5 people (a couple, 0–3 children, sometimes (`familyElderChance` 0.25) a widowed elder), adults with skills and traits drawn as applicants (spec 006). They move in at once and pay rent from the next close. Live or away | 18 h ± 6 |
| A hand's family | A hand under contract whose household is just him is offered a family house first when one is finished or falls empty (§3.13; a card: Yes / Not now); yes moves him and a seeded wife and 0–2 children into the house; his hut is free for another hand. The card says what changes: "From his next renewal his wage is 6,000 less: the house is part of his pay" (§3.6) | |
| Moving out | A household whose happiness stays below `leaveBelow` for `leaveSeasons` season closes, or is 2 seasons behind on rent, leaves (seeded, 50% a close once eligible). A hand's household leaving ends his contract (spec 006 dismissal rules, no severance). Emits `HouseholdLeft` with the reason | 35; 2 |
| Population | Citizens in the town; the HUD shows it with town happiness | |

### 3.11 Phase 1 hands (save migration)

A save's hands become citizens: a seeded age 22–45, gender male (Phase 1's hands were drawn as men's names; a woman's name keeps her gender), a household of one in their hut, education drawn, health 1. Applicants draw a gender and an age 20–45 from now on.

### 3.12 Processing order

Hourly pass, system Population: families arrive. Daily pass, system Population: outside work paid, needs and happiness updated, food bought (demand). Seasonal pass, after spec 005's settlement: rent, ageing, retirement, deaths, births, marriages, budgets for the new season, moving out, hands' family requests. Citizens and households in id order.

### 3.13 Every path achievable (owner, 2026-10-23)

A whole-spec review for dead ends an average player could hit. Each row closes one.

| Situation | Rule | Why |
| --- | --- | --- |
| When houses unlock | `BuildFamilyHouse` from the Hamlet (`unlockEra`), like the cattle shed; before it the Town tab says "Family houses come with the Hamlet" | The Hamlet's role is landowner; a Homestead has no money for houses |
| Placing a house | "Build a family house" opens the map with the acres it can go on lit (owned, nothing growing, room for another house; the quarters' free places); an empty field's sheet also offers it. Refusals in words: "Something is growing here", "This acre has 2 houses" | Nobody has to guess where a house may go |
| A hand's family and a free house | A family house that is finished or falls empty is offered first to a hand living alone (a card: "Manoj asks to bring his family here": Yes / Not now). Strangers arrive only after "Not now", or once the offer has stood 18 h unanswered (live or away; US-110). Replaces "at the next season close" in §3.10 | Families move in every 18 h, so a house would almost never still be free at a close and the player could never say yes |
| Remittance | A hand's household stops sending half his wage home once his family lives in town | His family is here |
| A hand retires | A retired hand living alone leaves for his home village (his hut is free for a new hand, `CitizenRetired`, "Ramu (60) has gone home to his village"). One whose family lives in your family house stays as an elder of that household | Otherwise a retired man would hold the hut for ever and the player could never hire again |
| A hand leaves the job | Dismissed or not renewed, a hand whose family lives in a family house stays as a tenant: he works outside, the household pays rent from the next close, and his housing deduction ends. A household with nobody working for you pays rent | A home isn't lost with a job; the founder still gets the house's value |
| Selling land | An acre with a family house can't be sold (`ParcelInUse`, as with huts and the shed) | No family is made homeless by a tap |
| Foreclosure | A family house stands on the land it was built on: the inherited acres are protected, so houses there are never seized. One on seized bought land goes with the land at book value, and its household leaves ("the bank took the land", `HouseholdLeft`); a hand's household leaving ends his contract (§3.10) | Foreclosure stays the one hard loss, and it is explained |
| A widowed elder alone | Pays rent from savings; behind 2 seasons, leaves for family elsewhere ("Kamla has gone to live with her son in Rampur") | Never evicted harshly, never a house stuck for ever |
| Counting households | A household is any family living on your land, a hand alone in his hut included, so the Village era's "5 households" (spec 018) is 2 hands' huts and 3 family houses: about 1,80,000 coins | Reachable in the Village pacing (2–3 weeks, ADR 0028) |
| Raising happiness | Every lever is shown on the household card's needs *Why?* with what to do: "Give someone here a job (your hand)", "Build another house (7 people live in a house for 6)", "Food: their income buys 83% of what they need" | The player always sees what would help |
| What can't be changed yet | Rows that the player can't move yet say so: "No school yet (the Village builds one)", "Health care: the chemist (a clinic comes with the Township)" | Never a red number with no way to fix it |

## 4. Data

**New definitions** (`content/population.json` unless noted): `lifeYearsPerSeason`, the life stages, `retireAge`, `remitShare`, `outsideDaysShare` (the outside wage is the founder's `dayLabourWagePerHour` × 8), the Engel curve, budget shares, `foodBasketPerPerson`, the basket by good, adult equivalents, the needs weights and gate, `birthChance`, `maxChildren`, `marriageChance`, `deathBase`, `deathDoubling`, `familyEveryHours`, `familyElderChance`, `leaveBelow`, `leaveSeasons`, the education draw; `family_house` in `buildings.json` (`value` 60,000, `buildHours` 48); the house's rules in `population.json`'s `house` block (`unlockEra` hamlet, `capacity` 6, `perAcre` 4, `rentPerSeason` 6,000); home villages (`villages`, saved by id); names with gender in `people.json`.

**New state** (save schema 9, ADR 0064 took 8): `Citizen` (id, name, gender, the season they were 0 (`bornSeason`: age = seasons since × `lifeYearsPerSeason`), householdId, education, skill, traits, health, job and hand, parents, spouse, home village, gone (died or left) and when, story lines), `Household` (id, members, home, income, budget, savings, rentBehind, needs met, happiness, lowSeasons), `CitizensState` (citizens, households, next ids, the next family's time). The departed stay in the record while a living citizen links to them (§3.3), never by a count cap.

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `BuildFamilyHouse` | `tile`, `financing` (optional, ADR 0071) | `EraLocked`, `NotOwned`, `TileOccupied` (growing), `NoRoom`, `InsufficientFunds`; with financing `InvalidAmount` (more than the cost) and the bank's checks | §3.4, §3.13 |
| `AnswerFamilyRequest` | `handId`, `yes` | `NoOffer`, `NoEmptyHouse` | §3.10 |

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
| Let a hand bring his family | No rent from that house | He stays longer (happier); his cash wage is 6,000 less from his next renewal; his hut frees up | Children, a household to keep happy | No |
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
```

Household card: members (age, stage, job), the budget pie with Engel's *Why?*, needs by layer with *Why?*, savings, rent.
Citizen card: portrait, age and stage, job, skill, traits, health, story lines, and the person's close family only: parents, spouse, siblings, children, grandchildren (name, age, job). A relative in another house shows "→ lives in Family house 3" and opens their card (back button, ADR 0044); grandparents are one tap away on a parent's card. The departed fold into one line, "2 who have passed ›", closed by default, greyed with "passed away at 70" or "moved to Rampur" when opened.

Never a whole-town tree (it doesn't fit a landscape phone): the Town tab has one row per household, with "related: Rekha's" for families linked by marriage or birth, sortable by happiness, size or job; the household card draws its own family as a small tree (an elder above, the couple, the children below):

```
            Kamla (74)
                |
      Manoj (38) ── Sita (35, from Sitapur)
          |              |
      Ravi (12)      Asha (9)
  Budget [?] · Needs [?] · Rent paid ✓
```

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | At each season close every citizen ages one year; a hand turning 60 isn't renewed and retires | `CitizenTests.Ageing_Retirement` |
| AC-2 | A family house on an owned empty acre costs 60,000, builds in 48 h, holds 6, 2 an acre (ADR 0067); the acre can't be sown while one stands | `HouseTests.Build_Limits` |
| AC-3 | An empty house brings a family of 2–5 every 18 ± 6 h, live or away; they pay rent at the next close (4200) | `MigrationTests2.FamilyMovesIn` |
| AC-4 | The budget follows the Engel curve; food met = budget ÷ need; savings cover a short season first | `HouseholdTests.Budget_Engel` |
| AC-5 | Happiness follows the layers, weights and gate; the three examples in §3.8 score 84, 68 and 62 | `HappinessTests.Layers` |
| AC-6 | Births, marriages and deaths follow their chances (seeded, same seed same town); no child or adult under 60 dies | `PopulationTests.Flows` |
| AC-7 | A household below 35 happiness for 2 closes, or 2 seasons behind on rent, leaves (50% a close); a hand's leaving ends his contract | `MigrationTests2.MovingOut` |
| AC-8 | Households add their basket to the local market's demand each hour | `MarketTests.TownDemand` |
| AC-9 | A schema-8 save's hands become citizens in households of one; it advances a season and round-trips | `MigrationTests.Schema8_Citizens` |
| AC-10 | The golden replays are unchanged; a year of the bot gives the same hash in one chunk or many | `ReplayTests.*`, `CitizenTests.ChunkingInvariance` |
| AC-11 | A hand whose family lives in a family house asks 6,000 × index less cash at his next renewal (a loyal hand's kept wage too), pays no rent, and asks his full wage again at the renewal after they move out; never mid-season | `MigrationTests2.HandFamily_HousingInKind` |
| AC-12 | An adult without a town job earns the day-labour rate × 0.5 × index each daily pass; children and elders earn nothing; hired labourers are the town's outside workers first, paid 9,375 in place of that day's outside work | `HouseholdTests.OutsideWork_LabourersFirst` |
| AC-13 | A departed citizen stays while a living citizen is their spouse, child or grandchild (a daughter who married away, while a parent lives: ADR 0070) and is dropped at the close after the last one goes; a town run for 20 years holds under 1 departed a living citizen | `PopulationTests.FamilyTree_Bounded` |
| AC-15 | The home loan (ADR 0071): the bank counts family houses at 0.75 of their book value, lends at most the house's cost, the rest is paid down; the financing is kept in the command log | `HouseTests.HomeLoan`, `PeopleViewTests.HomeLoan_Sheet` |
| AC-14 | Every row of §3.13: houses from the Hamlet; a free house offered to a hand alone first; a retired hand alone frees his hut; a dismissed hand's family stays as tenants; an acre with a house can't be sold; a seized house's household leaves; an elder alone behind on rent leaves; every needs row names its lever or says what isn't possible yet | `MigrationTests2.EveryPath`, `PeopleViewTests.NeedsLevers` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Population when the Village unlocks (spec 018) | 12–25 (5 households) | Measured (US-211, ADR 0071): 16 at day 21 (10–20) |
| 5 households | The competent bot by day 21 (ADR 0028 pacing, on the home loan); the cautious saver, which never borrows, by day 48 | Measured: day 16 (15–22); the saver day 33 (29–43). The random bot never reaches the Hamlet, so it isn't measured (ADR 0071) |
| Town happiness, competent bot | 65–85 (ADR 0071: renters with outside work score about 68 by §3.8 until businesses bring town jobs) | Measured 69 (68–70) |
| Households leaving a year, competent bot | Under 1 in 10 | Measured 0 |
| Rent's share of the founder's income | Under 15% | Measured 12% (at most 14%) |
| Departed a living citizen over 20 years (AC-13) | About 0.3 | Measured 0.27–0.33, worst 0.58–0.90 (a 6-house town; US-111) |
| Town food demand vs the founder's grain | A town of 80 eats about 2 acres' grain a season | |

## 10. Open questions

- **G1. Who counts.** **Closed (owner, 2026-10-22; ADR 0061):** the village is the town and its places serve the citizens; the population is the people on the founder's land ("Your people"); the old families stay neighbours, not counted (the owner accepted the pushback: counting them would start the town at about 35 and pass the Homestead, Hamlet and Village thresholds at once).
- **G2. Homes.** **Closed (owner, 2026-10-22):** a family house the founder builds (60,000, 48 h, 6 people, 4 an acre (2 since ADR 0067, owner 2026-10-24: 4 were cramped on the map) or the quarters' free places; the acre stops farming), rent 6,000 a season, a family 2 seasons behind leaves owing nothing; a hand's hut stays single (§3.4).
- **G3. The life clock and lifespans.** **Closed (owner, 2026-10-22):** 1 life-year a season (ADR 0002); child 0–17, adult 18–59, elder 60+; a hand retires at 60 after his season; births 0.10 a season for a couple (woman 20–40), at most 3, less when crowded or unhappy; deaths only for elders, 1.5% a year at 60 doubling every 8 years (about 72 years' expectancy) (§3.2, §3.9).
- **G4. Engel's law and the budget.** **Closed (owner, 2026-10-22):** the budget set at each close from the season's income; the Engel curve [[0, 0.65], [8,000, 0.55], [20,000, 0.40], [60,000, 0.25]] by income a person; rent, clothing 8%, health 5%, education 5%, the rest half leisure, half savings; food need 4,500 an adult a season (child 0.6, elder 0.8); savings cover a short season first (§3.5).
- **G5. Needs and happiness in Phase 2.** **Closed (owner, 2026-10-22):** survival 40% (the least of food, shelter, water), security 30% (town jobs, the chemist at 0.5, rent paid), belonging 20% (temple and square 0.8), growth 10% (no school yet; leisure 0.3); each layer gated by the one below at 0.8; the examples score 84, 68 and 62 (74 corrected to 68 by the owner, 2026-10-24: the rule kept); nobody above about 90 until the school (§3.8).
- **G6. Migration.** **Closed (owner, 2026-10-23, with a change):** a family moves into an empty family house every 18 ± 6 h, live or away; a hand can bring his family into a free family house (Yes / Not now at a season close); a household below 35 happiness for 2 closes or 2 seasons behind on rent leaves (50% a close), and a hand's leaving ends his contract. The owner's change: the house is part of the hand's pay, so his cash wage is cut by its rent (6,000 × index) from his next renewal, a loyal hand's kept wage included; full wage again after they move out (§3.6, §3.10).
- **G7. Death and tone.** **Closed (owner, 2026-10-23):** only elders die (§3.9's chances, about 72 years' expectancy; ×1.5 in poor health); worded gently ("passed away peacefully"), information only, no sound or push; a season's mourning (−10); no child or young adult deaths, no accidents and no funeral costs (the owner agreed with both pushbacks).
- **G8. Work for everyone.** **Closed (owner, 2026-10-23):** adults 18–59 without a town job work outside at the founder's day-labour rate (9,000 a game day) on half the days, paid as the expected share daily (no daily roll, the owner accepted the pushback); the founder's day labourers come from the town first; the employment rate counts town jobs only (§3.6).
- **G9. The family tree.** **Closed (owner, 2026-10-23):** three levels, each a tap deeper (the Town tab's household rows with "related" links, the household card's small tree, the citizen card's close family with siblings and links across houses); the departed folded into one line; kept only while a living citizen is their spouse, child or grandchild (no count cap; the owner asked that it never pile up) (§3.3, §7).
- **G10. Households buy food locally.** **Closed (owner, 2026-10-23):** the basket (cereals 0.40 kg, lentils 0.05, potato 0.10, radish and spinach 0.15, milk 0.25 L a real day, × 22.8) adds to the local market's demand hourly, live or away; a town of 80 about doubles it, so the founder sells faster; the price stays under the import ceiling and the shops never run out; milk from the co-op; direct sales to townspeople are spec 017's shops (§3.7). The owner asked that everything stay achievable and make sense to an average player: §3.13.

## 11. Cut first

1. Marriages (households only arrive and leave).
2. The family tree (a list of relatives instead).
3. A hand bringing his family.
4. Households buying food at the local market (spec 017 instead).
