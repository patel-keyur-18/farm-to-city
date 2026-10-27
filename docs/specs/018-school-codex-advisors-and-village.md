# 018. The school and literacy, the Codex, advisors and the Village unlock

- Status: In review (G1–G8 closed with the owner, 2026-10-27, §10)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Player journey and eras (Village); Community and civic services (literacy versus skills, education); Learning layer (advisors, the Concept Codex)
- Related ADRs: 0011 (literacy is separate from skilled graduates; the founder funds the first school), 0028 (the Village at 2–3 weeks), 0030 and 0032 (the Hamlet gate), 0045 (nudges and chapters), 0061 (the village is the town; the school plot), 0063 (spec 014: needs layers, education 5% of a budget), 0071 (5 households by day 16 with the home loan; spec 018 sets the Village from it), 0076 (Sprint 15 plan)
- Depends on specs: 009 (chapters, the Hamlet unlock), 010 (the school plot), 014 (citizens, households, needs), 015 (hiring, roles), 016 (businesses, a profitable season)

## 1. Goal

The player builds the village's first school and watches literacy rise year by year, grows a hamlet into a village by what it has become, and has the ideas they met in play named in their own numbers by a Codex and a few advisors who explain but never decide.

**Lesson:** education is a long-term investment whose payoff comes years later (ADR 0011); public goods pay back to the town, not the payer; a concept you felt is one you keep.

## 2. Scope

**In:**
- The first primary school on the village's school plot (ADR 0061): building, a teacher, seats, pupils, adult literacy classes.
- Literacy: who is literate, the town's literacy rate, what it changes in Phase 2.
- The growth need's education row (spec 014 §3.8 waits on this spec).
- The Concept Codex: the Phase 1–2 cards, their triggers, the player's own numbers.
- Advisors for Phase 2: who, when they speak, how often.
- The Village unlock (US-102): its gate, progress view, celebration and what it opens.
- Two settled conflicts: the treasury's era, and the Phase 2 exit gate's "a week of play" against ADR 0028.

**Out** (and where it goes instead):
- Secondary school, college, scholarships and education bonds (spec 015 G9): Town era (Phase 4).
- Skilled graduates (doctors, engineers) from local schooling: Town era (ADR 0011).
- The town treasury and later schools funded by it: Township (Phase 3, F15.1).
- The Town planner advisor: Phase 3, with the treasury.
- Statements, the finance hub and milestones: spec 019.
- Scenarios and difficulty modes: Phase 5 (F19.4).

## 3. Rules and formulas

Every coin amount is a year-0 price that follows the price index (ADR 0034). Life-years run at 1 a season (ADR 0002).

### 3.1 The school (§10 G1, G2)

| Rule | Behaviour | Default |
| --- | --- | --- |
| Where | The school plot in the village centre (tile 94); one school in Phase 2 | |
| When | From the Hamlet (it is part of the Village gate, §3.6) | |
| Build | Two classrooms, a verandah and a playground, funded by the founder from cash or a co-op bank loan (as the home loan, ADR 0071; never seized: the plot is the village's) | 90,000 building, 15,000 benches, slates and books; 48 h |
| Teacher | Once built and given to the district, the district posts a teacher and pays the wage (a government primary school, as in a real village; G2); the founder pays upkeep | Upkeep 2,000 a season |
| Seats | One teacher for up to 30 pupils (the Right to Education norm); a second teacher when over 30 (Phase 2 rarely gets there) | 30 a teacher |
| Pupils | Every child aged 6–9 in the town goes (free, so attendance is full); four life-years of primary (ADR 0011: about 16 real days) | Ages 6–9 |
| Adult classes | An evening class of up to 15 adults, started by the founder; literate at the season's close (ADR 0011, G3); the founder pays the teacher's extra hours | 3,000 a class a season |

### 3.2 Literacy (§10 G3, G4)

- **Literate:** a citizen who finished primary school, finished an adult class, or arrived literate (spec 014 draws 60% primary, 40% none for incoming adults).
- **Literacy rate:** literate citizens aged 7+ ÷ all citizens aged 7+ (whole percent, rounded down). Shown on the Town tab with its *Why?*; it gates the Township (40%, Phase 3).
- **What it changes in Phase 2 (G4).** A literate adult's outside work earns `literatePremium` 10% more (real rural wage premia for literacy are 10–20%; about 1,900 a season an adult today), and the growth need counts the school (below). No skill change: skills come from work and courses (spec 015).
- **Growth need:** a household with a child of school age counts *School* met (1.0) once the school is open; with no children it counts as met when the school is open (the town has one). Until then spec 014's "No school yet" row stays.

### 3.3 The Concept Codex (§10 G5)

- **Cards:** about 24 in Phase 2, each the first time the player meets the idea in play. A card has a plain definition, the player's own numbers as the example, and one line linking it to the real world.
- **Shown after the result** (decide, see, then name it): a small toast "New in your Codex: Profit is not cash" that opens the card; never a sheet over play, never during the first session. Menu → **Codex** lists the cards found (unfound ones hidden, as the letter's chapters).
- **The Phase 2 set** (triggers in `content/codex.json`):

| Group | Cards (trigger) |
| --- | --- |
| Farming | Diminishing returns (fertiliser on a field past its peak), soil fertility (a crop that took more than it gave), opportunity cost (day labour while a field waits) |
| Money | Profit is not cash (a profitable season with less cash), interest and compounding (the first interest charge), credit score (the score moves), leverage (a loan bigger than a season's profit), liquidity (an `InsufficientFunds` with assets in hand), inflation (the price index past 5%), working capital (a business's overdraft) |
| Business | Gross margin (the first business season), fixed and variable costs (the first business report), break-even (a business's plan), forecast versus actual (plan vs actual after 4 seasons), unit economics (a batch's cost per kg) |
| Assets | Depreciation (the first machine or building's wear), appreciation (land bought, its value up), yield (a rent or interest received) |
| People | Hiring under uncertainty (a reference check), market wage (a low offer refused), retention (a poaching offer), training ROI (a course finished), employer reputation (the first step) |
| Public | Public goods (the school opens), long-term investment (the first child literate from your school) |

### 3.4 Advisors (§10 G6)

- **Three in Phase 2:** the farm advisor (soil, crops, weather), the accountant (margins, cash flow, loans, assets) and the people lead (hiring, motivation, training). The town planner comes with the treasury (Phase 3).
- **Who (G6).** People of the village, drawn and named, each with a portrait: an old farmer neighbour, the co-op bank's secretary, a retired school headmaster.
- **When they speak:** one line on the season report (the most useful of their notes, ranked); one line on an alert whose cause they know ("Your mill's overdraft is near its limit: put cash in before the wage is due."); a *Why?* row may quote them. They warn and explain; they never act or press a choice.
- **How often:** at most one advisor line a sheet; a warning on the same topic at most once a game day; none in the first session.

### 3.5 Conflicts settled (§10 G7, G8)

- **The treasury (G7).** The GDD gave the Village "the treasury"; F15.1 is Phase 3. The treasury opens with the Township (Phase 3), with taxes; the Village era row lists larger co-op loans (the bank's cap rises from 5,00,000 to 25,00,000, `lenders.json` already) and the Village businesses and shops instead.
- **The Phase 2 exit gate (G8).** The roadmap said "a week of play"; ADR 0028 puts the Village at 2–3 weeks. The gate is: the Village reached in 2–3 weeks by a competent player using credit (4–6 weeks saving), the owner's playtest from the Hamlet to the Village, and players can explain profit versus cash.

### 3.6 The Village unlock (US-102; §10 G1)

The GDD's gate is 5 households, a market stall, 1 business profitable for 2 seasons, and a primary school. A market stall comes *with* the Village (spec 017), so it can't be part of its gate.

| Condition | Gate (G1, closed) | Measured |
| --- | --- | --- |
| Households | 5 households on your land (spec 014 §3.13 counts a hand alone in his hut) | Competent bot day 16 (15–22), saver day 33 (ADR 0071) |
| A business | A business with a profitable season twice (not necessarily in a row): spec 016's "profitable season" (net profit > 0 on its own P&L) | To measure: the mill bot (ADR 0073: a hired mill 6,400–8,000 a season) |
| The school | The primary school built and open | |
| (dropped) | ~~A market stall~~: stalls come with the Village | |

- **Checked** at each season close and when the school opens (system Population, after Finance); **unlocks once**: `EraUnlocked(village)`, the play-log milestone.
- **Progress:** Menu → Letter → "Becoming a Village": households (n of 5), businesses' profitable seasons (n of 2), the school (none, building, open); each row's *Why?*.
- **Celebration:** the `village` chapter (the grandmother's letter's voice), confetti as the Hamlet's, and a list of what opens: the bakery and the dairy (spec 016), shops on the pitches (spec 017), drip and the borewell, the tractor (spec 013), the co-op bank's larger loans.
- **Target:** the competent bot within 2–3 weeks, the cautious saver within 4–6 (ADR 0028, 0071); measured in the balancer (§9).

### 3.7 Processing order

- Hourly (system Population): the school's construction finishes; adult classes count hours.
- Season close (after spec 014's flows): pupils' years counted, those finishing primary become literate, adult classes finish; literacy recomputed; the Village gate checked.
- Codex triggers: after the event or state that triggers them, in the view layer (the sim records found cards so replays reproduce them).

## 4. Data

**New definitions:**
- `content/buildings.json`: `school` (cost, equipment, build hours, era hamlet, the plot).
- `content/population.json`: `school` (seats a teacher, pupil ages, primary years, upkeep, adult class size, cost and length, the literate outside-work premium).
- `content/codex.json` (new): cards (id, group, trigger kind and parameters, the numbers it quotes) with strings in `strings/en.json`.
- `content/eras.json`: the Village's gate (households, business seasons, school).
- `content/ui.json`: advisor caps (a line a sheet, a topic a day).

**New state** (save schema 13 in Sprint 17; ADR 0076 numbers schemas by sprint):
- `Citizen`: literate (bool), schooling years, in an adult class until.
- `CitizensState`: the school (status, built at, ready at, teachers), adult classes.
- `GuidanceState`: Codex cards found (ids, found at), advisor topics last spoken.

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `BuildSchool` | financing (cash or a co-op loan, term) | `EraLocked` (before the Hamlet), `AlreadyBuilt`, `InsufficientFunds`, the loan's reasons | §3.1 |
| `StartAdultClass` | citizens (up to 15) | `NoSchool`, `ClassFull`, `AlreadyLiterate`, `InsufficientFunds` | §3.1 |

| Event | Payload | Consumers |
| --- | --- | --- |
| `SchoolOpened` | at | Away card, notification, the Village gate, Codex (public goods) |
| `CitizenLiterate` | citizen, how | Away card, the town's literacy |
| `CodexCardFound` | card | Toast, Menu → Codex |
| `EraUnlocked(village)` | | The celebration, play log |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Build the school | 1,05,000 (or a loan) | Literate children after 4 seasons; the growth need met | The Village gate | Never (a gift to the village) |
| An adult class | 3,000 a season | Literate adults earn more outside (G4) | Teacher's hours | Stop after the season |

## 7. UI

```
The school plot (tap)                      Village progress (Menu → Letter)
| The school plot                     |    | Becoming a Village                        |
| A school for the village's children |    | Households            4 of 5   [?]         |
| Cost 1,05,000 · ready in 2 days     |    | A business, profitable seasons 1 of 2 [?] |
| [Build from cash] [With a loan]     |    | The school            Building · 1 day [?]|

The school (once open)                     Town tab
| Pupils 6 of 30 · a teacher [?]      |    | Literacy        58%  [?]                  |
| Next to finish primary: Raju, 1 season | | Codex (Menu)    14 of 24 found            |
| Adult class: 4 learning · [Start]   |
```

Every number has a *Why?*. Advisors' lines appear under the season report's title and on alerts, with the advisor's portrait and name.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | From the Hamlet, `BuildSchool` on the plot costs its price (cash or a loan), opens after 48 h, and is refused before the Hamlet or twice | `SchoolTests.Build` |
| AC-2 | Children aged 6–9 attend; after 4 life-years of school a child is literate; an adult class makes its adults literate in a season | `SchoolTests.Literacy` |
| AC-3 | The literacy rate counts citizens aged 7+, rounded down, with its *Why?* | `SchoolTests.Rate`, `TownViewTests.Literacy` |
| AC-4 | The growth need counts the school once open | `HappinessTests.School` |
| AC-5 | A literate adult's outside work earns 10% more | `HouseholdTests.LiteracyPremium` |
| AC-6 | Each Phase 2 Codex card is found once, by its trigger, after the result, with the player's numbers; found cards are saved and replay | `CodexTests.Triggers` |
| AC-7 | Advisors speak at most one line a sheet and once a day a topic, never in the first session | `AdvisorTests.Caps` |
| AC-8 | The Village unlocks once when its three conditions hold; the progress view shows each | `EraTests.Village` |
| AC-9 | The competent bot reaches the Village in 14–21 days, the cautious saver in 28–42 | `GateTests.Village` |
| AC-10 | A schema-12 save loads into 13 with no school, literacy from spec 014's education, no cards found | `MigrationTests.Schema12_School` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Village day, competent bot (credit) | 14–21 | ADR 0028, 0071 |
| Village day, cautious saver | 28–42 | |
| Literacy when the Village unlocks | 50–70% (adults' 60% primary, children not yet finished) | Township needs 40% |
| Codex cards found by the Village | 12–18 of 24 | Not a flood |
| The school's cost | Under one season's farm profit of a competent Hamlet farm | Affordable without the loan by day 21 |

## 10. Open questions

- **G1. The Village gate.** **Closed (owner, 2026-10-27):** 5 households, a business with 2 profitable seasons (not necessarily in a row), and the primary school built and open; the market stall dropped (stalls come with the Village, spec 017); the school buildable from the Hamlet (the school plot's line changes to say so); 2–3 weeks with credit, 4–6 saving (§3.6).
- **G2. Who pays the teacher.** **Closed (owner, 2026-10-27):** the founder builds the school and gives it to the district (1,05,000, or a co-op loan); the district posts a teacher and pays the wage, as a government primary school; the founder pays upkeep, 2,000 a season (§3.1).
- **G3. Adult literacy classes.** **Closed (owner, 2026-10-27):** the founder starts an evening class of up to 15 adults for a season and pays the teacher's extra hours, 3,000 a season; everyone in it is literate at the close (§3.1).
- **G4. What literacy changes in Phase 2.** **Closed (owner, 2026-10-27):** a literate adult's outside work earns 10% more (rural India's literacy premium is 10–20%): about 1,900 a season an adult at today's rate (an adult earns about 19,000 a season outside), so a class of 3 pays the town back in under a season; no skill change (skills come from work and courses, spec 015) (§3.2). The question put to the owner said about 450 a season; corrected here.
- **G5. The Codex set and its moment.** **Closed (owner, 2026-10-27):** about 24 Phase 2 cards in six groups (§3.3), each found the first time its idea is met, after the result; a toast that opens the card and Menu → Codex (unfound cards hidden); never a sheet over play, never in the first session.
- **G6. Advisors.** **Closed (owner, 2026-10-27):** three people of the village, named, with Blender portraits: an old farmer neighbour (farm), the co-op bank's secretary (accountant), a retired headmaster (people lead); one line on the season report and on an alert whose cause they know; at most one line a sheet, a topic once a game day, none in the first session; they never act (§3.4).
- **G7. The treasury's era.** **Closed (owner, 2026-10-27):** the treasury opens with the Township (Phase 3), with taxes; the GDD's Village row lists what Phase 2 opens instead: the co-op bank's larger loans (cap 5,00,000 → 25,00,000), the bakery and the dairy, shops on the pitches, drip, the borewell and the tractor (§3.5).
- **G8. The Phase 2 exit gate.** **Closed (owner, 2026-10-27):** the competent bot reaches the Village in 14–21 days using credit, the cautious saver in 28–42; the owner's Phase 2 playtest plays from the Hamlet to the Village, and players can explain profit versus cash; the roadmap's "a week of play" is replaced (§3.5).

## 11. Cut first

1. Advisors' alert lines (keep the season report's line).
2. Adult literacy classes.
3. Codex cards beyond the Money and Business groups.
