# 023. The Township: the clinic, the link road, the old village, civic buildings and the unlock

- Status: In review (G1–G10 closed one per message with the owner, 2026-11-14; ADR 0116; to be previewed as artifacts that look exactly like the game before any UI code)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Player journey and eras (the Township), Community and civic services, Governance and public finance, Economy (the road tier)
- Related ADRs: 0107 §2–§6, §13 (the plan and the gate), 0116 (this spec's decisions), 0108 and 0114/0115 (the treasury, spec 021), 0109 (spec 022), 0077 and 0097 (the school and the Village gate), 0063 and 0110 (households and rents), 0064 (maps previewed first), 0101 §12 (cards)
- Depends on specs: 021 (treasury, departments, conditions), 022 (taxes: the cohort pays them), 018 (the school, literacy, the Village gate), 014 (households, needs, happiness, rent), 015 (the hiring board, which this spec does not use for public posts), 004 (the road tier in prices)
- Stories: US-103 (the Township unlock, 5), US-146 (the link road, 5), US-148 (civic buildings, 8), US-100 (era-gate reachability, 3); Sprint 22

## 1. Goal

The player's village earns the Township: the school stands, a doctor works in a clinic the town helped build, a paved road reaches the highway, three businesses have made a profit and most of their people can read. Then the old village joins the town, the treasury opens in earnest and the player can add a watchman, a hall and a playground, paying for them from public money.

**Lesson:** public goods need public money and staff; a service that isn't staffed or reachable is only a building; the cost of a clinic is a gift but the reward is a town; an unstaffed or unpaid post stops serving.

## 2. Scope

**In:**
- The clinic and the doctor's quarters, the doctor, and what the clinic does (§3.1, §3.2).
- The petition for the paved link road and the road tier (§3.3).
- The Township gate, its progress sheet, the letter and the unlock (§3.4).
- The old village as an aggregate cohort (§3.5).
- Reach, capacity and staff for civic buildings, the school included (§3.6).
- The watchman's post, the community hall and the playground, and the appointments that staff them (§3.7).
- The treasury's lines for all of these and the break-even rule (§3.8), the doctor leaving and returning (§3.9).
- Bots, pace and exploit checks (§9).

**Out** (and where it goes instead):
- The map's ring, the new plots it brings and the highway tier: spec 025 (US-253) and Phase 4's bonds.
- Zoning, land value, private builders, rentals of land: spec 024.
- Festivals (US-149, Should): spec 026; the community hall is where they are held.
- A second doctor, a hospital, police and fire stations: the Town (Phase 4).
- Individual citizens for the old village: never (ADR 0107 §2).
- The Township businesses, the cold store: specs 025 and 028.

## 3. Rules and formulas

Money is `long` in 1/100 coin, indexed by `MarketRules.Indexed`, except the figures marked fixed. Rates and factors are `Fixed`; every division rounds down.

### 3.1 The clinic and the doctor (G1)

| Rule | Behaviour | Default |
| --- | --- | --- |
| When | From the Village; it is one of the Township's goals | |
| Where | Two new public plots in the village centre beside the school and the chemist: the clinic plot and the doctor's quarters plot. Their tiles are fixed with the owner from a map preview before the code (ADR 0064) | |
| The clinic | The district builds it; you pay a community share, a gift (Gifts to the village, outside farm profit, ADR 0095 §5), by tap card. A co-op loan within your room may pay it, as for the school | Share 48,000 (40% of 1,20,000); 48 h |
| The quarters | A larger family house on its plot, yours (an asset at cost, depreciated like any house), reserved for the doctor | Cost 90,000; rent 3,750 a season (the same 4.17% a season yield as a 60,000 kutcha house at 2,500); 48 h |
| The doctor | A migrant arrives 24 h after both stand and moves into the quarters. He is a literate household of one to three and counts as one of your households (the gate's 20) and for literacy (§3.4) | |
| Who pays | **Before the Township the district pays his salary, his allowance and the clinic's upkeep: you owe nothing recurring.** From the Township the treasury pays the allowance and the upkeep (§3.8) | |
| Rent | He pays the quarters' rent from his salary through spec 014's rent flow | |

### 3.2 What the clinic does (G2)

A household's **effect** is `e = condition × covered × scale`: the Health department's condition (spec 021 §3.5), 1 if its home is within reach (§3.6) else 0, and `scale = min(1, capacity ÷ people served)`. With a doctor in post and the clinic open, for every citizen of your households and (as happiness only) the cohort:

| Effect | No clinic | With a clinic, e = 1 |
| --- | --- | --- |
| Sick chance (workers, hands, the founder) | × 1 | × (1 − `clinic.sickCut` 0.5 × e) |
| Health loss when food is short a season | 0.20 | 0.20 × (1 − `clinic.lossCut` 0.5 × e) |
| Health recovery a season | 0.10 | 0.10 × (1 + `clinic.gainBoost` 0.5 × e) |
| Elders' chance of death a season | × 1 | × (1 − `clinic.deathCut` 0.25 × e) (about 3 more years of life, 72 to about 75) |
| The household's *Health care* need | 0.5 (the chemist) | 0.5 + 0.5 × e |

- **No visit fee**: a village health centre is free; medicine is bought at the chemist.
- **The benefit is shown**: the clinic's sheet lists "Sick days avoided this season: n" (a roll compared against both chances, so an exact count), "Health regained: n" and "Elders who stayed well: n", each with a *Why?*.
- The treasury's Health condition scales every row (a clinic not paid for helps less).

### 3.3 The link road (G3)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Petition | A row on the council hall's sheet, *Petition for a paved road*, from the Village and enabled at 10 households of yours (`road.petitionHouseholds`); disabled with its reason otherwise | 10 |
| Share | A gift on a tap card (cash before and after; "the district builds it in 4 days, the road is not yours"); a co-op loan may pay it | 25,000 |
| Build | The district builds it in `road.days` | 4 days |
| Effect | `roadTier` becomes `road` (`transportCostPerKg` 1.50 → 1.00): the trader pays 0.50 a kg more at the farm gate (potato about +7.7%, wheat about +2.1%) and bought inputs cost 0.50 a kg less. Every trader price's *Why?* names the road | |
| Benefit shown | Your season page gains "The road earned you about +n this season" = kilos sold × 0.50 × price index, with its *Why?* | |
| Upkeep | The district's until the Township, then the treasury's Roads line | 1,500 |
| Map | The district road on the south edge is drawn paved once built (previewed first, ADR 0064) | |

### 3.4 The Township gate (G4, US-103)

| Condition | Gate |
| --- | --- |
| Households | 20 households on your land (the doctor's counts) |
| The clinic | Built and open, the doctor in post |
| The road | The paved link road built |
| Businesses | 3 different businesses open at the check, each with at least one profitable season (net profit above 0 on its own books); a closed business doesn't count; hired or run by you both count |
| Literacy | 60% (`gate.literacy`) of your households' people aged 7 and over who can read |

- **Checked** at each season close and when a goal changes: the clinic opening, the road finishing, a household forming, a class ending, a child learning to read. **Unlocks once**: `EraUnlocked(township)`; goals never un-meet. The era condition `developerOnly` is replaced by this gate (the developer panel's *Next era* still works).
- **Progress:** the *Becoming a Township* sheet (Menu → Letter): a row for each goal, "n of target", each with a *Why?* naming the next step ("Petition for a paved road at the council hall"); a last line names the quickest order (road and homes first, the clinic last, as the cohort and doctor need the rest).
- **Celebration:** the `township` chapter (the grandmother's letter's voice), confetti, and what opens, read from content: the treasury and its budget, taxes and policies, zoning (spec 024), the cold store, pucca houses, the Township businesses and the map's ring (specs 025, 028).
- **At the unlock** (in order): `TreasuryRules.OnEra` opens the treasury (spec 021 §3.1); the cohort joins (§3.5); the takeover of lines begins at the first close (spec 021 §3.1); a Codex card *Public money* is found.
- **Target:** the competent bot at a median of day 63–77, the saver about day 100–110; every order reachable, never stuck (§9).

### 3.5 The old village as a cohort (G5)

At the unlock one record joins the town: `Cohort` (never citizens, no cards, no portraits):

| Field | Default |
| --- | --- |
| Households | 30 (`cohort.households`) |
| People | 5 a household, so 150: about 45 children under 14, 90 adults, 15 elders |
| School age | About 25 aged 6–13, about 10 not yet reading: they join the school free (a second teacher is the district's when pupils pass 30) |
| Literacy | 65% of those aged 7+ (`cohort.literacy`) |
| Income | About 20,000 a household a season at today's prices (`cohort.income`), indexed; the base for spec 022's tax from leisure and savings before food |
| Happiness | The four-layer formula (spec 014) from the town's real coverage: food, the clinic (§3.2), the school, belonging (§3.7) |
| Leaving | Under 35 for two closes running: one household (5 people) leaves at that close |
| Growing | Not in this spec (zoning's private builders add homes: spec 024) |

- **What it changes:** residents for the grants (spec 021), the house tax base (spec 022), literacy and population statistics, the school's pupils, the shops' demand (it is already in the mill's catchment).
- **What you see:** a row "The old village · 30 households · 150 people" on the Town sheet with its happiness, pupils and tax paid, each with a *Why?*.
- **A town left alone works:** at default taxes with the school and clinic open they stay happy and stay.

### 3.6 Reach, capacity and staff (G6)

One rule for every civic building, the school included: **a home is covered if its distance to the building (metres between two tiles, an acre is 100 m, Manhattan, as the shops') is within the building's reach.** Out of reach a home gets nothing, and its sheet says so ("The clinic is 1,900 m away; its reach is 1,500 m"). **Capacity** scales the benefit: `scale = min(1, capacity ÷ people in reach)`, a slow decline, never a cliff. **A building with no staff covers nobody.**

| Building | Reach | Capacity | Staff |
| --- | --- | --- | --- |
| School | 1,500 m | 30 pupils a teacher | The district's teacher |
| Clinic | 2,000 m | 400 people | The district's doctor |
| Watchman's post, hall, playground | §3.7 | §3.7 | §3.7 |

On today's 16 × 12 map every home is inside the school's and the clinic's reach; reach only bites with the ring (spec 025). A soft ring is drawn on the map around a civic building when its sheet is open (Should).

### 3.7 Civic buildings and their staff (G7, G7b)

From the Township the treasury builds three buildings on fixed public plots, previewed on the map first (spec 025's ring adds plots). Each is commissioned through spec 021 §3.7's confirm card (`CommissionWorks`), which gains a **budget guard** row: the added cost a season and whether the town would be short ("This adds 25,500 a season to Safety. With today's income the town would be short by 6,000.").

| | Watchman's post | Community hall | Playground |
| --- | --- | --- | --- |
| One-off | 30,000 | 80,000 | 20,000 |
| Upkeep a season | 1,500 | 3,000 | 1,000 |
| Staff | A watchman, 24,000 a season | A caretaker, 15,000 a season | None |
| Reach / capacity | 700 m / 300 people | 1,000 m / 250 people | 600 m / 60 children |
| Effect in reach | *Safety* 0.5 → 1.0 | *Belonging* 0.8 → 1.0; festivals held here (spec 026) | *Leisure* of households with children 0.30 → 0.60 |
| Department | Safety | Safety | Safety |

- **Safety** is a new part of the security layer from the Township: security = mean(job, health care, rent, safety), with safety 0.5 without a watchman. At today's numbers this doesn't lower anyone's happiness at the unlock.
- **Staff are appointed, not hired from the board** (ADR 0116): the building's sheet has **Post the job**; within 24 h a villager from the old village takes it, his name shown on the sheet ("Hari Singh, watchman since Spring") but part of the cohort. The pay is a fixed scale from content, indexed, no negotiation, a budget line in Safety, and the cohort's income. Unpaid at two closes running he leaves and the post covers nobody until posted again. **Let him go** frees it.
- The buildings are optional: a town that builds none works as §3.8 says.

### 3.8 The treasury's lines and the break-even rule (G8)

| Department | Line | A season | From |
| --- | --- | --- | --- |
| Office | clerk's wage | 30,000 | the opening |
| Health | doctor's allowance | 30,000 | the doctor in post |
| Health | clinic's upkeep | 3,000 | the clinic stands |
| Education | school's upkeep | 2,000 | the school is open |
| Roads | the link road's upkeep | 1,500 | the road is built |
| Safety | each civic building's upkeep, the watchman's and the caretaker's wages | §3.7 | each commissioned (and staffed) |

The takeover from the district begins at the first close after the unlock (spec 021 §3.1). A default Township (about 250 residents, 3 shops, default taxes) has about 63,100 in and 66,500 out: a gap of about 3,400 that the start-up grant carries while each new household adds about 500 in grants and 250 in tax. **Rule: the median Township bot's treasury must break even within two years of the unlock at default taxes and no civic buildings.** If a measurement misses, the levers in order: the doctor's allowance (30,000 toward 24,000), then the clinic's upkeep; never the grants or the default taxes (specs 021 and 022).

### 3.9 The doctor leaving and returning (G9)

- **Leaves** at the third unpaid close of his allowance (spec 021 §3.5: Health's condition under 50% at two closes running): the clinic covers nobody, the tied grant stops, the clinic's upkeep stays, his family leaves the quarters. Before the Township nothing can make him leave (the district pays).
- **The quarters stay reserved** for a doctor; you lose the rent until a new one comes.
- **A new doctor** comes at the first close where Health's service is 70% or more (`doctor.returnCondition`) and the cash covers the allowance; he arrives 24 h later.
- **Never a collapse:** the Township stays; no debt; a gift, a smaller office budget or a tax puts it right.
- **Warning:** at the first unpaid close an alert says "The doctor's allowance went unpaid. At two more closes he will leave", with spec 021 §3.11's levers; the planner's note *the tied grant at risk* (spec 021 §3.12) names it.

### 3.10 Processing order

Systems `Population` and `Civic` (new, after Finance, before `Treasury`, spec 021 §3.10): at each close the doctor's and posts' state, the gate check, the cohort's happiness and leaving. Hourly: building and arrival timers (48 h, 24 h, 4 days). The unlock runs inside the gate check and calls `TreasuryRules.OnEra`.

## 4. Data

**New or changed definitions** (`content/township.json`, new; `content/treasury.json`, `content/map.json`, `content/eras.json`):

| Definition | Field | Default | Notes |
| --- | --- | --- | --- |
| `clinic` | `share` 48,000; `buildHours` 48; `doctorArrivesHours` 24; `sickCut` 0.5; `lossCut` 0.5; `gainBoost` 0.5; `deathCut` 0.25; `reach` 2,000 m; `capacity` 400 | | |
| `quarters` | `cost` 90,000; `rent` 3,750 | | the house kind and its plot |
| `road` | `petitionHouseholds` 10; `share` 25,000; `days` 4 | | |
| `gate` | `households` 20; `businesses` 3; `literacy` 0.60 | | replaces `developerOnly` in `eras.json` |
| `cohort` | `households` 30; `peoplePerHousehold` 5; `literacy` 0.65; `income` 20,000; `leaveBelow` 35; `leaveCloses` 2 | | |
| `school` | `reach` 1,500 m | | `population.json` `school` |
| `civic` | the three buildings of §3.7: cost, upkeep, staff wage, reach, capacity, effect, plot | | `treasury.json` `works` |
| `treasury.json` `lines` | clinic upkeep, doctor's allowance, road upkeep, the civic lines | §3.8 | `LineRequires` gains Road, Quarters/Doctor, Post |
| `doctor` | `returnCondition` 0.70 | | |
| `map.json` | the clinic plot, the quarters plot, the three civic plots | | fixed with the owner from a preview |

**New or changed state** (save schema 20; hashed only once started, so earlier towns and the goldens are unchanged):

| Entity | Field | Notes |
| --- | --- | --- |
| `ClinicState` | status (none, building, open), ready at, doctor's name, doctor in post, unpaid closes, `QuartersId` | |
| `RoadState` | status (none, building, built), ready at | |
| `Cohort` | households, people, happiness, below-threshold closes, school pupils | opened at the unlock |
| `PostState` ×3 | status (none, vacant, filled), name, unpaid closes | |
| `GateState` | `Met` flags and the unlock time | |

## 5. Commands and events

| Command | Validation and rejection reasons | Effect |
| --- | --- | --- |
| `BuildClinic` | `NotUnlocked`, `PlotTaken`, `InsufficientFunds` | §3.1, the share is a gift |
| `BuildDoctorQuarters` | same | §3.1, a house of yours |
| `PetitionRoad` | `NotUnlocked`, `TooFewHouseholds`, `AlreadyBuilt`, `InsufficientFunds` | §3.3 |
| `PostJob(building)` | `NoBuilding`, `AlreadyFilled` | §3.7 |
| `DismissStaff(building)` | `NoStaff` | §3.7 |
| `CommissionWorks(workId)` (spec 021) | gains `PlotTaken` | the civic buildings |

| Event | Emitted when |
| --- | --- |
| `ClinicBuilt`, `QuartersBuilt`, `DoctorArrived`, `DoctorLeft` | §3.1, §3.9 |
| `RoadPetitioned`, `RoadBuilt` | §3.3 |
| `GateMet(goal)`, `EraUnlocked(township)`, `CohortJoined` | §3.4, §3.5 |
| `CohortHouseholdLeft` | §3.5 |
| `PostFilled`, `PostVacated` | §3.7 |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Pay the clinic's share | 48,000 gift | Health benefits, a gate goal | The doctor's household is one of yours | Never |
| Build the quarters | 90,000, an asset | Rent 3,750 a season | Reserved for a doctor | Sell it like any house |
| Petition for the road | 25,000 gift | About +1,500–3,000 a season at the gate | Prices' *Why?* names it | Never |
| Commission a civic building | The treasury's cash | A running cost, a small happiness gain | May push the budget short; the card says | Never |
| Post a job / let him go | Nothing | A wage in the budget | | Either way |

## 7. UI

Previewed as artifacts that look exactly like the game, in this order, before any UI code (the owner's rule): (1) the map with the clinic, quarters and civic plots and the paved road; (2) the clinic and quarters on the Build sheet and the council hall's *Petition for a paved road* with its card; (3) *Becoming a Township* and the Township letter; (4) the clinic's sheet with its benefit lines, the household card's coverage rows, the Town sheet's old village row; (5) the civic buildings' sheets and their commission cards with the budget guard.

Every number has a *Why?* (`WhyTests` sweeps them).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | The clinic and quarters build in 48 h from the share and the cost (both gifts or assets as §3.1); the doctor arrives 24 h after both stand; before the Township you owe nothing recurring | `ClinicTests.Build`, `ClinicTests.Doctor` |
| AC-2 | The five effects of §3.2 hold with `e` from condition, reach and capacity; "sick days avoided" is an exact count | `ClinicTests.Effects`, `ClinicTests.Avoided` |
| AC-3 | The petition needs 10 households; the share is a gift; the road is built in 4 days; the tier and prices follow; the season page's line is kilos × 0.50 × index | `RoadTests.Petition`, `RoadTests.Prices` |
| AC-4 | The gate checks the five goals at each close and when a goal changes; only open businesses with a profitable season count; the doctor's household counts; unlock once | `TownshipTests.Gate`, `TownshipTests.Businesses`, `TownshipTests.Once` |
| AC-5 | At the unlock the treasury opens, the cohort joins and the takeover begins at the first close | `TownshipTests.Unlock` |
| AC-6 | The cohort's household count, residents, tax base, pupils and literacy follow §3.5; a household leaves at the second close under 35 | `CohortTests.Join`, `CohortTests.Leaves` |
| AC-7 | Reach, capacity and staff gate every building's benefit (§3.6); a home out of reach says why | `CoverageTests.Reach`, `CoverageTests.Capacity`, `CoverageTests.Staff` |
| AC-8 | The three civic buildings' costs, upkeep, effects and the *Safety* part (§3.7); the card's budget guard says the shortfall | `CivicTests.Effects`, `CivicTests.Guard` |
| AC-9 | Appointments fill in 24 h, are paid as a Safety line and the cohort's income, leave after two unpaid closes, and can be dismissed | `CivicTests.Staff` |
| AC-10 | The treasury's lines of §3.8 start when their building and staff exist; a default town breaks even within two years | `TownshipTests.Lines`, `GateTests.TownshipSolvent` |
| AC-11 | The doctor leaves at the third unpaid close, returns at 70% with the cash, the quarters stay reserved, the era stays | `ClinicTests.Leaves`, `ClinicTests.Returns` |
| AC-12 | A schema-19 save loads into 20 with none of this and round-trips; a town with all of it saves and loads to the same hash | `MigrationTests.Schema19_Township`, `TownshipTests.SaveRoundTrip` |
| AC-13 | Every number on the new sheets has a *Why?*; strings are complete | `WhyTests.EveryTownshipNumberHasWhy` |
| AC-14 | Each sheet and card renders and responds to scripted touch in the Editor | the stories' Editor tours (checklist) |
| AC-15 | The competent bot unlocks at a median of day 63–77, the saver about 100–110, every order reachable, none stuck | `GateTests.Township` |
| AC-16 | The exploit checks hold: padding households, business churn, adult-class loops, milking the treasury | `GateTests.TownshipExploits` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Township pace, competent bot | Day 63–77 | A median under 55 or over 77 goes to the owner with its numbers before anything changes; under 55 proposes tightening a goal (for example 25 households) |
| Saver | About day 100–110 | Slowest order within 130 days with credit |
| The treasury at the unlock | In about 63,100, out about 66,500; break even within two years | Levers in §3.8's order |
| The road's gain | About +1,500–3,000 a season | Measured with the bots |
| Cohort | Stays at 30 households at default taxes | Under 35 happiness only when neglected |
| Frame cost | The cohort is one record; render statistics read at each visible story | Growth over 10% reported |

## 10. Open questions and clarifications

All ten gaps were closed with the owner one per message on 2026-11-14:
- **G1** the clinic and quarters (share 48,000, quarters 90,000 at 3,750 rent, no recurring bill before the Township; the burden question raised by the owner changed ADR 0107 §3).
- **G2** what the clinic does (§3.2, no visit fee, benefits shown).
- **G3** the road (share 25,000, benefits shown; ADR 0107 §4 had 50,000).
- **G4** the gate (open businesses only, the doctor's family in literacy, flag rule day 55 and 77).
- **G5** the cohort as one aggregate record.
- **G6** reach and capacity (one rule, 1,500 m and 2,000 m, 400 people).
- **G7** the three civic buildings, fixed plots, the *Safety* part, the budget guard.
- **G7b** appointments at a fixed scale (amends ADR 0107 §13).
- **G8** the treasury's lines and the break-even rule.
- **G9** the doctor's leaving and return.
- **G10** the bots, the previews and the build order (road, clinic, unlock and cohort, civic buildings, US-100).

## 11. Cut first

1. The soft reach ring on the map.
2. The Town sheet's old village *Why?* rows beyond happiness.
3. The playground (its effect is the smallest).
4. The road's season-page line (its *Why?* on prices stays).
Never cut: the gate, the unlock, the doctor's rules, the coverage rule, a hands-off town that works, every number's *Why?*.
