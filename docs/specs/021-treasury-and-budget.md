# 021. The town treasury and its budget

- Status: Approved (owner, 2026-11-13; G1–G10 closed one per message, ADR 0108; the layout accepted, to be previewed as an artifact that looks exactly like the game before US-138's UI code)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Governance and public finance (the treasury's first day, revenue, spending, the budget cycle); Overview (two wallets); Community and civic services (the school, the clinic)
- Related ADRs: 0077 (the treasury comes with the Township), 0095 §5 (gifts to the village), 0101 §12 (confirm cards), 0107 §3, §6, §13 (the clinic, the treasury's first day, the scope), 0108 (this spec's decisions), 0004 (the player never loses), 0094 (frame time), 0019 (rolling wave)
- Depends on specs: 005 (the ledger), 008 (sheets, *Why?*), 016 (a business's own ledger, the pattern), 017 (the pitches' rent), 018 (the school, advisors, Codex), 019 (the statements' style), 014 (citizens, residents)
- Stories: US-138 (Treasury, 8 points), US-139 (Budget cycle, 5 points), both Sprint 21. Spec 022 (taxes and policies) comes second and feeds this one.

## 1. Goal

The player runs a second wallet that isn't theirs: a town treasury that lives on grants, rents and taxes, pays for public services in an order they set, and decays when it can't pay. They see every coin that moves in it, and never confuse it with their own money.

**Lesson:** public money versus personal money; a budget is a priority list; a grant can come with conditions; services decay when they are under-funded; a gift to the public isn't repaid.

## 2. Scope

**In:**
- The treasury as a second wallet from the Township unlock (§3.1): its own ledger, three statements in spec 019's style, 8 seasons kept.
- Income: a start-up grant, a per-resident grant (untied and tied to the clinic), the shops' pitch rents, the intake for spec 022's taxes, the row for spec 024's permit fees, your gifts.
- Spending by department (six, fixed order), paid in two passes at each close; capital works through a confirm card (the command; later specs add the works).
- Each department's *condition* (service level) and the named effect it has.
- The annual budget: a proposal, a set window and one revision, the variance report.
- The warning a day ahead of a close that would leave a department unpaid.
- The town planner (the fourth advisor), three Codex cards, the play log.
- The *Town treasury* sheet in the Menu, *The town's books*, the cards.

**Out** (and where it goes instead):
- Taxes' rates, bounds, bases and policies: spec 022 (US-140, US-141). This spec builds only the intake.
- The clinic, the link road, the old village's cohort and the real Township unlock: spec 023. Until then the developer panel's *Next era* opens the treasury (§3.1), and Health and Roads have no lines.
- Permit fees and land value: spec 024. The watchman's post, community hall, playground: spec 023. The water tower and power: spec 025.
- Bonds, public debt, a credit rating, a council vote, council mood, elections, conflicts of interest and scandal, sales tax: Phase 4 (ADR 0107 §13).
- Any payment from the treasury to you, your household, your businesses or your farm (§3.9): never in Phase 3.
- A department you can close, and loans to the treasury: not built.

## 3. Rules and formulas

Money is `long` in 1/100 coin; every coin amount is in today's coins (ADR 0034) and follows the price index through `MarketRules.Indexed`; ratios and conditions are `Fixed`; every division rounds down unless stated.

### 3.1 When the treasury opens (G2)

| Rule | Behaviour |
| --- | --- |
| Opens once | When the era reaches Township (`EraState.Current >= township`), checked with the eras' other checks (`EraRules`). Never closes, never resets. |
| Before spec 023 | The Township's gate isn't built yet: the developer panel's *Next era* (Development builds, ADR 0076) changes the era, and tests change it directly. When spec 023 adds the gate, nothing here changes. |
| On opening | `TreasuryOpened`; the Menu gains *Town treasury*; the start-up grant (`startUpGrant`, 1,00,000) is posted as the treasury's first income; a toast and a line in Dadi's letter say where the money came from and that it is public money, not yours. The first budget's window opens at once (§3.6). |
| Opening balance | The grant only. Nothing moves from your wallet. |
| Takeover | From the **first close after the opening** (the era changes at any moment, not at a close): the school's upkeep is the treasury's Education line and no longer the founder's (spec 018 §3.1: `SchoolRules.Close` skips its gift; *Next season, already due* drops its school row); the shops' pitch rent is credited to the treasury (§3.2). Nothing earlier is changed. |
| Old saves | A save from before the Township has no treasury state and hashes as before; schema 19 adds `TreasuryState`, which hashes only once the treasury has opened (the goldens are unchanged). |

### 3.2 Its books (G1)

The treasury has its own `Ledger` (the class a business uses, spec 016 §3.2) on the shared chart of accounts. Its balance is a **Fund balance** (equity), not net worth. Nothing posts between the treasury's ledger and yours except a gift (§3.8). New accounts, each with the save schema it arrived in (19) so an older save reads without them and a late account hashes only once used (`Accounts.SinceSchema`):

| Account | Name | Kind |
| --- | --- | --- |
| 4600 | District start-up grant | Income |
| 4610 | District grant, untied | Income |
| 4620 | District grant, tied to the clinic | Income |
| 4630 | Pitch rents | Income |
| 4640 | House tax | Income (posted by spec 022) |
| 4650 | Business tax | Income (posted by spec 022) |
| 4660 | Fees | Income (spec 024's permits) |
| 4670 | Gifts from the founder | Income |
| 5910–5960 | Council's office, Health, Education, Roads, Safety, Utilities | Expense, one a department |
| 3100 *Retained earnings* | The Fund balance, closed to at each season close | Equity |

It also uses Cash 1000, Buildings 1600, Accumulated depreciation 1650 and Depreciation 5500 (works the treasury commissions, §3.7). The ledger's rules are the founder's: balanced entries, cash never negative, reconciliation at every close.

**Statements (spec 019 §3.1's layout, public names)**, one set for each closed season, the last 8 kept and shown:

| Statement | Lines |
| --- | --- |
| *Money in and out* | Money in (each income account, by name), money out by department (each line by name), depreciation, what's left (the season's surplus or shortfall) |
| *Cash flow* | **The town's work** (every income and every department's spending), **Works the town built** (capital works paid), **Gifts** (shown within the first group, its own row); opening and closing cash equal the ledger's |
| *What the town has* | Cash, buildings at book, **Fund balance**; no debt, because it can't borrow (§2) |

Every row is itemised with a *Why?* listing each movement behind it (day, what, from or to whom, amount); there is never an *Other* row; each line sits beside the same season last year ("First year" until one has passed); the ledger keeps 8 seasons of entries.

### 3.3 Income (G3, G4)

| Income | Rule | Units |
| --- | --- | --- |
| Start-up grant | Once, at the opening (§3.1): `startUpGrant` | 1,00,000 |
| Untied grant | At each close, for the season that ended: `grantUntied` × residents | 100 a resident |
| Tied grant | The same, `grantTied` × residents, **only if the clinic is staffed** at the close (a doctor in post). Before the clinic exists (spec 023) it counts as not staffed, so the town gets the untied part only; the sheet says "Paid when the clinic is staffed". Otherwise the season's report says which part was paid and which was withheld and why | 50 a resident |
| Residents | Your citizens plus the old village's cohort once it has joined (spec 023, about 150 people). Before then, your citizens only | people, whole |
| Pitch rents | Posted by the shops' `PayRent` at the moment a rent is really paid, credited to 4630 (Dr treasury cash, Cr 4630) in the same step as the shop's own entry. A shop that can't pay (ADR 0096) credits nothing. A newcomer's pitch has no ledger and pays nothing | `pitchRent` 3,000 a pitch, up to 9,000 |
| Taxes | Spec 022 calls `TreasuryRules.CollectTax(sim, kind, payer, billed)` at the close; the office's collection factor (§3.5) applies to what is billed, and the payer pays only what is collected | spec 022 |
| Fees | `TreasuryRules.CollectFee` for spec 024's permits; 0 until then | |
| Gifts | §3.8 | |

Grants never follow your wealth: only residents and the price index. A grant only ever comes from the district to the treasury.

### 3.4 Departments, their lines and the order they are paid (G5)

| # | Department | Lines (content; indexed) | Opens with |
| --- | --- | --- | --- |
| 1 | Council's office | Clerk's wage `clerkWage` 30,000 a season (a line, not a citizen) | The treasury |
| 2 | Health | Doctor's allowance `doctorAllowance` 30,000; the clinic's upkeep (spec 023) | The clinic |
| 3 | Education | The school's upkeep (`population.json` `school.upkeep`, 2,000) | The treasury, if the school is open |
| 4 | Roads | The link road's and lanes' upkeep (specs 023, 025) | The road |
| 5 | Safety | The watchman's post, the community hall, the playground (spec 023) | Each building |
| 6 | Utilities | The water tower and the power (spec 025) | Each |
| 7 | Policies | The farm input subsidy, the small business grant, the night market's lights (spec 022 §3.5, added by spec 022) | Each policy enacted |

A department with no lines shows *Nothing to pay yet*. A department's **needs** are the sum of its lines at today's prices at the close. Spec 021 builds the structure and the lines that exist (office, education); later specs add lines to the same table (`treasury.json` `lines`), each with its wage-or-upkeep kind (§3.5).

**Payment at the close** (system `Treasury`, §3.10): for the season's close, with the budget in force (§3.6):
1. **Pass 1.** In department order, each pays `min(needs, budget)` from cash, wages before upkeep inside a department; stopping when cash runs out.
2. **Pass 2.** In the same order, each pays what its needs still lack, from the cash left: the **overspend**, shown on the report.
3. What cash can't pay is **unpaid for that season** and is not carried as a debt. Cash never goes below zero (ADR 0107 §6).

A department you under-budgeted is therefore paid after every other department's budget: the budget is your priority list. **Funding** = paid ÷ needs (1 when needs are 0).

### 3.5 Condition and its effects (G6)

Each department has a *condition* (`Fixed`, 0–1), starting at 1 the first time it has a line. At each close:

`condition ← condition + (funding − condition) × conditionStep`, `conditionStep` 0.5, rounded down. A missed season halves a service, never to nothing at once; a full season gives half the loss back.

| Dept | Effect at condition *c* (the specs that build each service read it) |
| --- | --- |
| Office | Tax collected = `officeBase` 0.70 + `officeWeight` 0.30 × *c* of what's billed |
| Health | The clinic's cut in sick days and its slowing of health loss × *c* (spec 023); the doctor leaves if *c* is under `healthLeaveBelow` 0.50 at two closes running: the clinic shuts and the tied grant stops until a new doctor comes (spec 023) |
| Education | A pupil's school year counts only while *c* ≥ `educationMinCondition` 0.50 ("the school is shut for repairs"); the teacher stays the district's (spec 018) |
| Roads | The road tier's gain at the farm gate × *c* (spec 023) |
| Safety, Utilities | In the same form, set by specs 023 and 025 |

Under-funding shows on the department's row ("Health · 60% · the clinic's hours are cut") with a *Why?*.

### 3.6 The budget (G7)

- **What it is:** for each department, the most you plan to spend a season, set for the year and applying to all four seasons. Capital works (§3.7) are outside it.
- **Proposal:** last year's average actual a season for each department, or in the first year its needs. Range when you change it: 0 to `budgetMaxOfNeeds` 1.20 of the department's needs now (the headroom is for the year's price rises: a flat budget falls behind indexed costs).
- **A new line in the year** is added to its department's budget at its needs at once, shown on the sheet; you may change it at the revision.
- **When:** I call a close by the season it starts. The *set* window opens at the opening, and at each Spring close (the close that starts the year). The *revise* window opens once at the Monsoon close (midyear), only if a budget was set earlier in the same year. A card after the season page offers *Accept* or *Change*; the proposal is **in force at once**, so ignoring the card leaves it as proposed (the sheet says "Proposed, not yet looked at"). A window stays open until you accept or the next close, then it is locked until the next window.
- **The report** at each close, for each department: budget, paid, variance, and one sentence from this list: *prices rose*, *a new line*, *cash ran out*, *your budget was below its needs*, *unspent*; each with a *Why?*. It is saved with the season's statements.
- A revision that would need a council vote is Phase 4's.

### 3.7 Capital works (G5, G8)

`CommissionWorks(workId)`: the treasury pays a cost from cash through a confirm card (ADR 0101 §12) to a **builder or supplier**, never to you or your businesses (§3.9). The asset sits at 1600 at cost and depreciates over its life at each close (Dr 5500, Cr 1650). The works themselves are content (`treasury.json` `works`: cost, life, effects) added by specs 023–025; spec 021 ships the command, the card and a fixture work for its tests. Buildings you give (the school, the clinic) belong to the district and are not on the treasury's books; only their running costs are.

**The card's land line (G8, shown, not blocked).** When the works would raise the land value of an acre you own (spec 024), the card says it: "This also raises the value of your acre 91 (about +4%)", with a *Why?*. Before land value exists the line doesn't show.

### 3.8 Gifts (G8)

`GiveToTreasury(amount)`: from the treasury sheet, a tap-to-spend confirm card showing cash before and after and "It's a gift: you can't take it back". At least `giftMinimum` 1,000; at most the cash you hold; the treasury must be open. Yours: Dr 5260 *Gifts to the village* / Cr Cash, outside farm profit (ADR 0095 §5): net worth falls and the bank's room isn't cut. The treasury's: Dr Cash / Cr 4670. Both sides are one movement for the reconciliation.

### 3.9 The wall (G8)

- No cash ever leaves the treasury to the founder, your household, your businesses, your farm, or your hands as your employees. Its counterparties are departments' lines and builders or suppliers for capital works.
- It never buys from your businesses: when the brick kiln exists (US-243), the treasury's bricks come from the trader at the regional price, even if your kiln is cheaper.
- Spec 022's subsidies and grants go to neighbours and townspeople only.
- Enforced in code: a treasury posting naming the founder or one of your businesses as the other side is a bug; a test sweeps every treasury entry of every bot (AC-11) and a *milk the treasury* stress bot joins spec 022's gate checks.

### 3.10 Processing order (system `Treasury`, after Civic, before Events)

At each season close, after spec 005's settlement, spec 016's businesses (the shops' rents, §3.3) and spec 014's flows:
1. Income: the grants, the tax bills (spec 022), fees; gifts are posted when given.
2. Depreciation of works.
3. Spending, §3.4's two passes; the conditions move (§3.5).
4. The statements are snapshotted and the variance report made (§3.6); unpaid departments raise their toast.
5. The windows open when due (§3.6); *Next season, already due* for the treasury is worked out (§3.11).

Hourly: nothing, except the day-ahead warning at 00:00 of a season's last day (§3.11). The treasury sheet is a view: it reads state and never changes it.

### 3.11 The warning (G6)

At 00:00 of a season's last day, if cash plus the income that is certain by the close (the grants from residents, the pitch rents your shops will pay, the tax bills from spec 022) is short of the budget, an alert and a held notification say what would go unpaid, in order ("Short by 12,000: Roads and Education would go unpaid") with three levers: **Give to the town** (§3.8), **Change a tax** (spec 022) and **Revise the budget** (only in a window; otherwise the lever is greyed with its reason). Something that changes the shortfall later that day raises it again. The notification kind `TreasuryShort` names no money and never falls in quiet hours (spec 007). A town in deficit slows, and nothing else happens in Phase 3: no collapse, no debt, no loss of the era.

### 3.12 Advisor, Codex, play log (G10)

- **The town planner** (spec 018's rules: explains, never decides or acts; a topic at most once in 24 h; never in the first session; one line a sheet): on the treasury sheet and the season page's treasury section. Four notes in `advisors.json`: a department over its budget at two closes running; a department's service under 70%; the tied grant at risk (the doctor's pay unpaid or Health under 50%); the balance under one season's spending with no tax or gift to make it up.
- **Three Codex cards** (`codex.json`, spec 018's rules): *Public money* (at the opening), *A budget is a priority list* (the first time a department is paid in pass 2 or goes unpaid), *Tied grants* (the first time the tied part is withheld).
- **The play log** (opt-in, ADR 0024) records the opening, each window, each unpaid department and each gift, with amounts.

## 4. Data

**New or changed definitions** (`content/treasury.json`, optional: content without it has no treasury, so the frozen golden content is unchanged):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| Treasury | `era` | string | `township` | an era id | When it opens |
| | `startUpGrant` | long | 1,00,000 | ≥ 0 | Indexed |
| | `grantUntied`, `grantTied` | long | 100, 50 | ≥ 0 | A resident a season, indexed |
| | `clerkWage` | long | 30,000 | ≥ 0 | A season, indexed |
| | `doctorAllowance` | long | 30,000 | ≥ 0 | A season, indexed; the Health line appears with the clinic (spec 023) |
| | `departments` | list | the six of §3.4 | | id, order, lines |
| | `conditionStep` | Fixed | 0.5 | 0–1 | |
| | `officeBase`, `officeWeight` | Fixed | 0.70, 0.30 | sum 1 | |
| | `healthLeaveBelow`, `educationMinCondition` | Fixed | 0.50 | 0–1 | |
| | `budgetMaxOfNeeds` | Fixed | 1.20 | 1–2 | |
| | `giftMinimum` | long | 1,000 | ≥ 1 | |
| | `retainSeasons` | int | 8 | 4–16 | |
| | `works` | list | empty | | Added by specs 023–025: id, cost, life, building, effects |
| Ledger | the accounts of §3.2 | | | | `Account` enum and `Accounts.SinceSchema` |
| Strings | `treasury.*`, `why.treasury.*`, `dept.*` | | | | `content/strings/en.json` |
| Codex, advisors | three cards, four notes | | | | `codex.json`, `advisors.json` |

**New state** (save schema 19, ADR 0108; the schema after US-240's 18; hashed only once the treasury has opened):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `TreasuryState` | `OpenedAt` | long | 0 until it opens |
| | `Ledger` | `Ledger` | The treasury's own |
| | `Budget[6]` | long | Per department, a season |
| | `Condition[6]` | `Fixed` | |
| | `Window` | enum + period + seen | None, set, revise |
| | `YearBudgeted`, `Revised` | int, bool | |
| | `Reports` | list | The last 8 seasons' statements and variance rows (saved, not hashed beyond the ledger) |
| | `ShortWarned` | bool | §3.11, once a close |
| | `Unpaid[6]` | counters | For the advisor and the doctor's rule (spec 023) |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `GiveToTreasury` | amount | `NoTreasury`, `BelowGiftMinimum`, `InsufficientFunds` | §3.8 |
| `SetBudget` | department, per-season amount | `NoTreasury`, `NoWindow`, `OutOfRange` | §3.6; marks the window seen |
| `AcceptBudget` | | `NoTreasury`, `NoWindow` | The proposal stays; the window is marked seen |
| `CommissionWorks` | work id | `NoTreasury`, `UnknownWork`, `InsufficientFunds` (the treasury's cash), `NotYours` is never a reason: a work never pays you | §3.7 |

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `TreasuryOpened` | grant | §3.1 | Toast, letter, Codex, play log |
| `GrantPaid` | untied, tied, withheld reason | Each close | The report, the away card |
| `DepartmentPaid` | dept, paid, needs, in pass 2 or not | Each close | The report, the Codex |
| `DepartmentUnpaid` | dept, amount | Each close | Toast, the report, the advisor, the Codex |
| `ConditionChanged` | dept, from, to | Each close | The sheet, the advisor |
| `BudgetWindowOpened` | kind | §3.6 | The card |
| `BudgetSet` | department, amount | `SetBudget` | Play log |
| `GiftGiven` | amount | §3.8 | Toast, play log |
| `WorksCommissioned` | work, cost | §3.7 | Map, the report |
| `TreasuryShortAhead` | short, first unpaid | §3.11 | Alert, notification |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Give to the town | Your cash; net worth falls | The treasury can pay what it couldn't | Never repaid; not farm profit | Never |
| Under-budget a department | Nothing now | Paid last in a tight season; its service decays | The planner warns | Revise at Monsoon |
| Accept the proposal | One tap | Departments paid as last year | Prices rise through the year | Revise once |
| Ignore the budget card | Nothing | The proposal applies | Shown as "not yet looked at" | Revise once |
| Commission works | The treasury's cash | An asset that depreciates; effects of the work | May raise your acre's value (shown) | Never |

## 7. UI

Menu → *Town treasury*: Store and People layout, two tabs (*This season*, *Budget*), a glance row with the balance, three picture buttons (*Give to the town*, *Taxes and policies* from spec 022, *The town's books*), the permanent line **"Town money: not yours"**, no HUD pill (your cash pill stays yours; the treasury's alerts use the existing bell). The mock-up accepted by the owner (2026-11-13):

```
Town treasury · Town money: not yours             Summer, year 3
| Balance 1,04,200 [?]            Short at the close? No ✓          |
| [🎁 Give to the town ] [📋 Taxes and policies] [📒 The town's books] |
| [ This season ] [ Budget ]                                         |
| Money in                                         59,300 [?]        |
|   🏛 District grant, untied (250 × 100)           25,000 [?]        |
|   🏛 District grant, tied to the clinic (250 × 50) 12,500 [?]        |
|   🧺 Pitch rents (3 of your shops)                 9,000 [?]        |
|   🏠 Taxes                                         12,800 [?]        |
|   🎁 Gifts from you                                     0           |
| Money out, by department                         61,950 [?]        |
|   🏢 Council's office     30,000   service 100% ▮▮▮▮▮             |
|   🩺 Health               30,000   service 100% ▮▮▮▮▮             |
|   📚 Education             1,950   service 100% ▮▮▮▮▮  [?]          |
| Due at the close: 61,950 · covered ✓ (or amber: "Short by 8,400")  |
```

- **Budget tab:** a pictured row for each department with a slider 0–120% of needs, its *Why?*, last season's paid and variance, *Accept the proposal*, and the window's state at the top ("Open until the next close", "Locked until Monsoon", "Proposed, not yet looked at").
- **The town's books:** the last 8 seasons, each opening *Money in and out*, *Cash flow*, *What the town has*, each line beside last year's, as spec 019.
- **Cards:** *Give to the town*, the budget window after the season page, *Commission* (with the land line), the shortfall alert's three levers.
- **Before the UI is coded** (owner, 2026-11-13) it is previewed as an artifact that looks exactly as it will in the game; and it is checked in the Editor by scripted touch.
- **Every number has a *Why?*** (§3.2: itemised; `WhyTests` sweeps them).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | The treasury opens once, when the era reaches Township, with the start-up grant and nothing from your wallet; before it there is no state and the hash is as before; it never reopens | `TreasuryTests.Opens_Once` |
| AC-2 | Each close pays untied × residents; tied × residents only when the clinic is staffed; the report names what was withheld and why; residents count your citizens plus the cohort when joined | `TreasuryTests.Grants` |
| AC-3 | A pitch rent really paid is credited to 4630 in the same step; one not paid credits nothing; before the opening it credits nothing | `TreasuryTests.PitchRents` |
| AC-4 | From the first close after the opening the school's upkeep is the Education line, the founder isn't charged, and *Next season, already due* has no school row | `TreasuryTests.SchoolTakeover` |
| AC-5 | Cash never goes below zero; income comes before spending at the close; what can't be paid is unpaid, not carried | `TreasuryTests.NeverBelowZero` |
| AC-6 | Pass 1 pays min(needs, budget) in department order (wages before upkeep), pass 2 pays the rest while cash lasts; an under-budgeted department is paid after the others' budgets; funding = paid ÷ needs | `TreasuryTests.TwoPasses` |
| AC-7 | Condition moves halfway toward funding each close, rounded down, and starts at 1; each effect reads it (the office's collection, the school's year, the doctor's two closes under 50%) | `TreasuryTests.Condition` |
| AC-8 | The proposal is last year's average or the needs; in force at once; the set window at the opening and each Spring close, one revision at Monsoon; ignoring the card leaves the proposal; a new line is added at its needs; the range is 0–120% | `BudgetTests.Windows`, `BudgetTests.Proposal`, `BudgetTests.NewLine` |
| AC-9 | Each close reports budget, paid, variance and one sentence from the fixed list per department | `BudgetTests.Variance` |
| AC-10 | A gift posts both sides in one movement, at least 1,000, never more than your cash, outside farm profit, never repaid | `TreasuryTests.Gift` |
| AC-11 | No treasury entry of any bot's run names the founder or your businesses; no command pays them; the treasury never buys from your businesses | `TreasuryTests.NeverPaysYou`, `TreasuryTests.NotFromYourBusinesses` |
| AC-12 | `CommissionWorks` pays from the treasury's cash to a supplier, books the asset at cost, depreciates it at each close; the card shows the land line when your acre gains | `TreasuryTests.Works`, `TreasuryViewTests.WorksCard` |
| AC-13 | Each closed season's three statements reconcile (opening + groups = closing cash; assets = fund balance), every cash movement falls in one row, 8 seasons are kept, each line sits beside last year's | `TreasuryStatementTests.Reconciles`, `TreasuryStatementTests.NothingUnsorted` |
| AC-14 | The warning is raised at 00:00 of the last day when the certain income can't cover the budget, again if it changes, with its three levers; the notification names no money and is never in quiet hours | `TreasuryTests.Warning` |
| AC-15 | The planner's four notes, the three Codex cards and the play log fire at their exact moments | `TreasuryAdvisorTests.Notes`, `CodexTests.TreasuryCards`, `PlayLogTests.Treasury` |
| AC-16 | A schema-18 save loads into 19 with no treasury and round-trips; a town with a treasury saves and loads to the same hash; replay gives the same state | `MigrationTests.Schema18_Treasury`, `TreasuryTests.SaveRoundTrip` |
| AC-17 | Every number on the treasury sheets has a *Why?* | `WhyTests.EveryTreasuryNumberHasWhy` |
| AC-18 | The Menu row, the sheet, *The town's books*, the budget tab and every card render and respond to scripted touch in the Editor | the story's Editor tour (checklist) |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Income at the opening (about 250 residents) | Grants 37,500 + rents up to 9,000, before taxes | Spec 022 sets taxes to about 12,000 |
| Spending at the opening | About 62,000 (clerk 30,000, doctor 30,000, school 2,000), plus the clinic's and road's upkeep (spec 023) | |
| A tax set to zero | A deficit the start-up grant carries for about 15 seasons | Measured with the bots, then set in content |
| The bots' treasury | Never below zero; the departments' unpaid count across 200 seeds to year 8 | A competent town at default taxes pays everything |
| A found-and-gift bot | A gift never returns to the donor (net worth falls by the gift) | Exact |

## 10. Open questions and clarifications

All ten gaps were closed with the owner one per message on 2026-11-13:

- **G1. Its books.** A second `Ledger` like a business's, three statements in spec 019's style under public names, a Fund balance (§3.2).
- **G2. When it opens.** At the Township era; the developer panel before spec 023; the grant only (§3.1).
- **G3. The grants.** 1,00,000 start-up; 100 untied and 50 tied to the clinic a resident (§3.3).
- **G4. What else comes in, and the order.** Pitch rents only as paid; taxes through an intake; fees empty until spec 024; gifts; income before spending (§3.3, §3.10).
- **G5. Departments and the order.** Six, fixed order, wages first, clerk and doctor 30,000; capital works through the card; given buildings aren't the treasury's (§3.4, §3.7).
- **G6. When a department isn't paid.** Condition by smoothing; named effects; the warning (§3.5, §3.11).
- **G7. The budget.** A priority list, two passes, the proposal in force at once, one revision (§3.4, §3.6).
- **G8. Gifts and the wall.** A gift is one-way; no flow to you, never from your businesses; the land line shown, not blocked (§3.7–§3.9).
- **G9. The screens.** The Menu sheet, *Not yours* line, no HUD pill; **previewed as an artifact exactly like the game before the UI code** (§7).
- **G10. The planner, Codex and log** (§3.12).

**Clarifications made while writing (for your review, none changes a decision):**
- **The "opening close" in G2 and G4.** The Township era changes at any moment, not at a close, so the takeover (school, pitch rents) begins at the **first close after the opening**, and the first grants are paid at that close for the season that ended (a part-season is paid in full; it's small, and one rule is clearer than two).
- **Capital works ship without content.** The command, the card and the books are built and tested with a fixture; the first real works come with spec 023.
- **The chart is shared.** Treasury accounts are new entries of the one `Account` enum (4600–4670, 5910–5960), arriving in schema 19 so older saves and the goldens are unchanged.
- **A tax's payer pays what is collected** (§3.3), so the office's leakage is the payer's saving; spec 022 must show it.

## 11. Cut first

If this spec runs over, drop these first, in order:

1. The three Codex cards.
2. The town planner's four notes (the report's variance sentences stay).
3. The *The town's books* year-on-year columns.
4. The Budget tab's last-season columns.
5. The land line on the works card (its numbers need spec 024 anyway).

## 12. As built (US-138, 2026-11-14, ADR 0114)

For your review; none changes a decision.

- **The Township era** is in `eras.json` with the condition `developerOnly` until spec 023 builds the gate; the treasury opens in `EraRules.Unlock`.
- **Seven departments** are in the state from schema 19 (spec 022's *Policies* is the seventh); the Health line and the tied grant wait for the clinic (`TreasuryRules.ClinicStaffed` is false until spec 023).
- **Pitch rents** paid at the boundary are credited after the old season's books are shut, in the new season (§3.3); a rent paid mid-season is credited at once.
- **The first statement** holds the start-up grant as its first income (opening cash 0).
- **Not in US-138:** the budget's windows, `SetBudget`, `AcceptBudget`, the warning, the planner, the Codex cards and the play log (US-139); taxes (US-140); the land line on a works card (needs spec 024). The condition (§3.5) and the two passes (§3.4) are built, and the budget is the proposal (the first year's needs) until US-139.
- **The works card:** `CommissionWorks` is a tap card by the standard rules; the content has no works yet, so tests add one by a content edit.
- **A department's row** shows its service in the label with a bar; its Why? lists its lines, budget and service.
