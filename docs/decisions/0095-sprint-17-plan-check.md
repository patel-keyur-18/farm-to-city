# 0095. Sprint 17 plan check: newcomers against overpricing, rivals' price cuts, the adult class, the school as a gift, the Codex's bursts, the advisors' notes and the Village gate

- Status: Accepted (owner, 2026-11-07: each gap decided one at a time)
- Date: 2026-11-07
- Deciders: Keyur Patel
- Amends: spec 017 §3.7 (the newcomer's trigger and place, the rivals' reaction), §4, G6; spec 018 §3.1 and §5 (the adult class), §3.2 (the growth need), §3.3 (the Codex's toasts, one trigger), §3.4 (the advisors' notes), §3.6 (when the gate is checked), §4, §6 (the school's cost as a gift), AC-2; ADR 0077's consequence (save schema 15, not 13); the GDD's Competition and Community paragraphs
- Related: ADR 0074 (spec 017), 0077 (spec 018), 0088 (US-122 to Sprint 17), 0093 (overpricing pays until US-122), 0094 (the GPU line), 0071 (Village pacing), 0064 (layouts previewed, never cramped), 0065 (buildings face their street)
- GDD sections: Businesses (competition), Community and civic services (the first school), Learning layer (the Codex, advisors)

## Context

Sprint 17 builds spec 018 (F14.1 the school and literacy, F14.2 the Codex and advisors, US-102 the Village unlock) and US-122 (F12.4 rival entry, moved here by ADR 0088). Before any code, each story, task and acceptance criterion was checked against specs 017 and 018, ADRs 0076, 0077, 0088, 0093 and 0094, and the code after Sprint 16 (PR #22). As it stands:

- The school plot (tile 94) is drawn empty; there's no school, literacy, Codex or advisor code. `eras.json` marks the Village `"manual"`, so only the developer panel's Next era reaches it.
- The grocer, the village tea stall and the town's halwai already compete for customers (US-120). Their price cut and the newcomers aren't built.
- The era-id event filter of US-102.T2 was built in Sprint 15 (`GuideViews.Name`).
- Save schema is 14.

Eleven gaps came out of it.

## Decisions

1. **Newcomers stop overpricing from paying (G1).** Spec 017's newcomer opened on a gap (under 80% served) or a mean price over 1.25 × the reference. Neither can fire against a stall 10% above the reference: the highest preset is 1.2, and the grocer and halwai never run out while the village tea stall's 200 cups an hour exceed the demand (about 130). So ADR 0093's 12,757 a season (hired tea stall, 10% above) against 7,342 (at the reference) would stand. Now *rich prices* means one of your shops of that kind sold the season above the reference (its customer-weighted price factor over 1.0: the 1.1 and 1.2 presets). The seeded 0.5 chance and the gap rule are unchanged. `GateTests.Shops` holds that a hired tea stall, sweet shop and vegetable stall at 1.1 earn no more than at 1.0 (median, 8 seeds × 12 seasons); if the chance isn't enough, it is raised in `shops.json` (data). The price setting's *Why?* says a newcomer may set up when you price above the village's, and `RivalOpened`'s alert says why it opened.
2. **A newcomer when every pitch is yours (G2).** Nothing limits you to one shop a kind, so holding all three pitches (9,000 a season) would block every newcomer and undo decision 1. When no pitch is free, the newcomer sets up as a cart at one fixed spot on the village lane at the square's edge (a road acre, never for sale), 100 m from the square (−0.10 attraction). Its rules are otherwise the same: at most one of each kind, closing after two seasons under 10%. The spot's layout is previewed to the owner before code (ADR 0064).
3. **The rivals' price cut (G3).** Rivals have no books, so "never under its cost" had no cost; "fell by a third" had no baseline; a cut never came back; and the halwai 1.5 km away, serving a town, wouldn't cut for one village's trade. Now:
   - At each close, a rival whose share fell by more than a third from last season's cuts its price by 5% of the reference (1.00, 0.95, 0.90, 0.85), never under `rivalFloor` 0.85 (`shops.json`; chai at 0.85 is about 10.20 a cup, over its milk, tea and sugar of about 4.20).
   - A season its share rose by more than a third, it goes back 5% of the reference, never above the reference.
   - Only the village grocer and the village tea stall react; the halwai and newcomers keep their prices.
   - The cut fires when you open a shop beside them (their share halves), so the balance step re-measures every shop with it on, against ADR 0093's targets.
   - `RivalPriceCut`'s alert names the rival, its new price and why ("it lost customers to yours"); the Customers tab shows its price with a *Why?*.
4. **The adult class (G4).** The class costs 3,000 however many join, so a picker had no decision in it; and literacy "at the season's close" let a class started an hour before it work for 3,000. Now `StartAdultClass` takes no list: it enrols up to 15 citizens aged 14+ who can't read, working-age adults (14–59) first, then elders, in citizen id order. The sheet shows who would join. One class at a time (one teacher). It lasts 96 h from its start (a season, a life-year), and everyone still in it is literate when it ends. The 3,000 is paid at the start (price-indexed). Someone who leaves or dies leaves the class; newcomers wait for the next. Refusals: `NoSchool`, `ClassRunning`, `NobodyToTeach`, `InsufficientFunds` (`ClassFull` and `AlreadyLiterate` go).
5. **The school is a gift (G5).** It belongs to the district once built, so it isn't an asset; as a farm cost it would cut that season's farm profit by 1,05,000 and so the bank's room (`CreditRules.AverageFarmProfit`, a 4-season mean) by about 26,000 for a year. A new expense account, **Gifts to the village**, sits outside farm profit beside the household group (as living and health costs are). The school's 1,05,000, its 2,000 upkeep at each close and the adult class's 3,000 go there. Net worth falls by the whole amount; *Your season* shows it spent and *why cash moved* names it. With a loan, it's an ordinary co-op bank loan (`Financing`) within the bank's existing room; the school adds nothing to the room. The *Why?*: "A gift to the village: it's the district's school now. It pays back in your people's literacy and wages, not on your books."
6. **The growth need (G6, a confirmation).** Spec 014 averages a layer's needs; once the school opens, growth is the mean of *School* (1.0, for every household) and leisure (0.3 or 0). A household with leisure goes from 0.3 to 0.65 (+3.5 happiness), one without from 0 to 0.5 (+5). Town happiness rises from about 69 to about 72, inside ADR 0071's 65–85; the birth chance rises about 1–2% relative. The balance step expects it.
7. **The Codex's toasts (G7).** One card found in a pass gets its own toast, which opens it; two or more get one toast ("3 new in your Codex") that opens Menu → Codex with the new ones marked. That rule covers a season close and an older town's first load after the update. Nothing is back-filled: a card is found the next time its moment comes. Nothing is found during the first session; a moment then counts the next time it comes. *Fixed and variable costs* moves from the first business report (the same close as *Gross margin*) to the first business season whose sales fell from the last while its wage and rent didn't. Cards are recorded in the sim through a command, as the letter's chapters are, so replays find the same.
8. **The advisors' notes (G8).** Twelve notes in a new `content/advisors.json` (id, advisor, condition, priority, the alert it attaches to), their text in `strings/en.json`. The season report shows one line, the highest priority whose condition holds (table order); one advisor line a sheet; a note at most once a game day; none in the first session; they never act. The *Why?* quotes are left out (optional in spec 018, the first thing it cuts). Names are placeholders the owner may change: the accountant **Sunita Verma** (the co-op bank's secretary), the farm advisor **Ramdeen Kaka** (an old farmer neighbour), the people lead **Masterji Hariprasad** (a retired headmaster). Their portraits, in the approved people style (ADR 0067), are previewed first.

   | # | Advisor | When | Where | Line (draft) |
   | --- | --- | --- | --- | --- |
   | 1 | Accountant | A loan payment missed or a final notice | `LoanPaymentMissed`, `FinalNotice` | "Pay this first: a missed payment costs penalty interest and your score." |
   | 2 | Accountant | Loans over half of what you own | Season report | "Your loans are over half what you own. One bad season could cost land." |
   | 3 | Accountant | A business's overdraft over 75% of its limit, or wages missed | `WagesMissed`, season report | "Your mill's overdraft is near its limit: put cash in before the wage is due." |
   | 4 | Accountant | Profit above 0 but cash fell | Season report | "You earned {profit} but cash fell {fall}: it went into {biggest}." |
   | 5 | Farm | A pest warning | `PestWarning` | "Spray before it spreads; a day late can cost a fifth of the crop." |
   | 6 | Farm | Feed low | `FeedLow` | "Cut fodder or buy straw before the shed runs out." |
   | 7 | Farm | A field's fertility fell this season, below 40 | Season report | "{field} gave more than it got. Compost it, or rest it with lentils." |
   | 8 | Farm | A rain-fed field dried out under a crop this season | Season report | "{field} went dry. A channel or the pump would have saved it." |
   | 9 | People | A raise request | `HrEventRaised` (raise) | "Underpaid people leave. A raise costs less than finding another." |
   | 10 | People | A worker tired most of the season | Season report | "{name} is worn out. Shorter days or a second pair of hands." |
   | 11 | People | Children of school age and no school | Season report | "{n} children aren't in school. The school plot is waiting." |
   | 12 | People | Adults who can't read, the school open, no class running | Season report | "{n} adults would earn 10% more if they could read. A class is 3,000." |
9. **The Village gate (G9).** It is also checked when a household forms or moves in, so the Village opens the moment the last condition holds (still once). A business's profitable seasons are counted on the business (save schema 15; an existing business's from its own ledger in the migration); the gate needs one business with two. A `VillageBot` (the competent bot plus a mill at the Hamlet and the school on a co-op loan once its room allows) and a saver variant (no loans) measure `GateTests.Village` (14–21 and 28–42 days); a miss comes to the owner with its numbers, never a silent change. US-102 is 5 points (was 2): the three gate conditions, a general progress sheet, the Hamlet's celebration naming the goals, the `village` chapter (its text previewed), the celebration's list read from content that `unlockEra: "village"` opens, and the bots.
10. **Sprint 17 runs 2 weeks, 12–25 Apr 2027, 36 points** (was 1 week, 18; Sprint 16 was 43 in 2 weeks). Re-pointed: US-131 5 → 8, US-132 3 → 5, US-133 5 → 8, US-134 3 → 5, US-102 2 → 5; US-122 stays 5. Sprint 18 moves to 26 Apr – 2 May, its length decided at its planning. The order of work and the cut order are in `sprints.md`. US-122 stays though it's the only Should: ADR 0093 needs it.
11. **Stale statements (G11)** are fixed in this commit: the feature files' schema 13, the school's "four facings" (one facing, its building stage and pupils in its yard; ADR 0065), US-122.T1 and US-102.T2's built parts, F12.4's "Sprint 16", the features' points, the overview row, and the GDD's guided-week line "the first school, at the Village unlock" (the school is built from the Hamlet: the Hamlet's letter names the Village's three goals). ADR 0077's "save schema 13" is superseded: Sprint 17's save is **schema 15**. The school plot's and "No school yet" strings change in US-131, as spec 018 says.

## Kept from Sprint 16

- Every device check writes down both frame-time p95s (ADR 0094: main ≤ 8.3 ms, GPU ≤ 11 ms; about 1.8 ms of GPU room). This sprint adds one building, a few children and a cart: low risk, still measured.
- Save schema 15 grows on the branch and ships once, with the device build.
- Art and layouts are previewed to the owner before each story's code; each visible story is tested in the Editor by scripted touch.

## Consequences

- Spec 017 §3.7, §4, G6 and spec 018 §3.1–§3.6, §4–§6, §8 carry these decisions with a pointer here; the GDD's Competition and Community paragraphs too.
- New data: `shops.json` (`rivalFloor`, `rivalStep`, the cart spot, the margin trigger), `content/advisors.json`, `content/codex.json`, `eras.json`'s Village gate; a new account (Gifts to the village).
- New tests beyond the specs': `RivalTests.PriceCut`, `RivalTests.Cart`, `GateTests.Shops` (overpricing), `SchoolTests.AdultClass_96h`, `SchoolTests.Gift_NotFarmProfit`, `CodexTests.Burst`, `AdvisorTests.Priority`, `EraTests.Village_OnHousehold`.
- No formula changes to anything already built: the new rules are spec 017's and 018's, filled in.
