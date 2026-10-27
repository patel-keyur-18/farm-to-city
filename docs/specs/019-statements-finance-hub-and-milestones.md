# 019. Your statements, the finance hub and wealth milestones

- Status: Draft (gaps G1–G8 open, §10)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Assets and wealth (net worth, the personal finance screen, wealth milestones, asset classes); Learning layer (reports every player can read)
- Related ADRs: 0008 (rupee-scale coins), 0009 (credit from day one), 0032 (P&L grouping, home use), 0034 (inputs in store are an asset; the price index), 0038 (foreclosure), 0044 (net worth kept, land on its own row), 0068 (each business's own books), 0076 (schemas by sprint), 0077 (the Village; the Codex)
- Depends on specs: 005 (the ledger, credit, the season report), 008 (sheets and *Why?*), 016 (business books, net worth's businesses), 018 (the Codex, advisors)

## 1. Goal

The player reads their own money the way a business reads its books, profit and loss, cash flow and a balance sheet each season, and sees on one screen how their wealth is built, how much of it is borrowed, how long it would last in a bad season, and how much of it earns without their own work.

**Lesson:** profit is not cash; net worth is what you own minus what you owe; an emergency fund buys time; leverage cuts both ways; passive income is what frees you to build.

## 2. Scope

**In:**
- The founder's three statements each season: profit and loss (spec 005's season report, kept), a cash flow statement, a balance sheet; past seasons kept.
- The finance hub on Money and loans: net worth's trend, what your wealth is made of, debt to assets, the emergency fund, the passive income ratio, each with its *Why?*.
- Wealth milestones.
- (G4 open) A savings deposit at the co-op bank.

**Out** (and where it goes instead):
- Town bonds, shares and investments: Town era (Phase 4, F18.1).
- Leasing land out, rental property beyond family houses: Phase 3 (zoning, rentals).
- The town report and the treasury's budget-versus-actual: Phase 3 (F15.1).
- Scenarios, grades and difficulty: Phase 5.

## 3. Rules and formulas

Money is `long` 1/100 coin; ratios are `Fixed`, shown as whole percents or one decimal (§3.3); every coin amount is in today's coins (ADR 0034).

### 3.1 Your statements (§10 G1, G2)

At each season close the founder's ledger (spec 005) is reported as three statements, laid out as a business's Books (spec 016 §7), so a player who has read the mill's books reads their own the same way.

| Statement | Lines | Source |
| --- | --- | --- |
| Profit and loss | As spec 005's season report now (money from harvests, what farming cost, did the farm make money, other money, the family's living, interest, losses, what's left) | Unchanged |
| Cash flow | **Your work** (sales, wages paid, living, interest), **What you bought or sold** (land, buildings, animals, machines, cash put into or taken from businesses), **Borrowing** (loans taken, repaid); opening and closing cash | The ledger's cash postings by account group |
| Balance sheet | **What you own** (cash, deposit, produce and inputs in store at today's price, land at market, buildings and machines at book, animals, businesses at equity, owed to you); **What you owe** (each loan); **Net worth** | `NetWorthSnapshot` (ADR 0044), at the close |

- Interest is under *Your work* (a cost of running the farm), principal under *Borrowing*.
- Past seasons: the last 8 kept (2 game years), each opening its three statements.
- **Plan versus actual (G2 open).** Recommended: the founder's season report compares each line with **the same season last year** ("Winter: 46,200, last Winter 31,900"), since a farm's seasons differ too much to compare with the season before; a written farm plan stays with businesses (spec 016).

### 3.2 The finance hub (§10 G3)

**G3 open.** Recommended: Money and loans becomes the hub (no new screen); its *At a glance* section gains:

| Row | Formula | Shown as |
| --- | --- | --- |
| Net worth's trend | Net worth at each of the last 8 closes and now | A small line chart; its *Why?* names the biggest change each season |
| What your wealth is made of | Each asset class's share of what you own | A pie (as the household budget's), legend with amounts |
| Debt to assets | What you owe ÷ what you own | Percent; under 30% "comfortable", 30–60% "stretched", over 60% "risky" (rural lending norms) |
| Emergency fund | (cash + deposit) ÷ a season's unavoidable costs (living, wages, loan payments due, upkeep) | Seasons, one decimal ("1.4 seasons"); under 1 in amber |
| Passive income | Passive income a season ÷ the family's living a season (§3.4) | Percent; 100% is "passive income covers your living" |

Each row has a *Why?*; the accountant advisor (spec 018) may add one line.

### 3.3 Rounding

Percents are whole, rounded down; seasons to one decimal, rounded down (an emergency fund never looks bigger than it is).

### 3.4 Passive income (§10 G5)

**G5 open.** Recommended: income that comes without the founder's own labour in a season: rent received from family houses, interest on the deposit, and the profit of businesses run by a hired worker (not ones you run). The farm, even with hands, isn't counted (you still decide and sow).

### 3.5 A savings deposit (§10 G4)

**G4 open.** Recommended: the co-op bank takes deposits, as real rural co-op banks do: **Put in** / **Take out** any amount, any time, on Money and loans; interest 1.5% a season (about 6% a year, a rural savings rate) paid at each close; it counts in the emergency fund and passive income, never as collateral (it's yours to take). Inflation (2–6% a year) is shown beside it, so the player sees cash in hand lose value and the deposit roughly keep it.

### 3.6 Wealth milestones (§10 G6)

**G6 open.** Recommended, the GDD's five, each once, a toast and a line in the hub's *Milestones* list (with when), never a reward that tells the player what to do:

| Milestone | When |
| --- | --- |
| First lakh saved | Cash + deposit reach 1,00,000 at a close |
| Twice what you inherited | Net worth reaches twice its first value (the inherited estate) |
| First rent | The first rent received |
| Debt-free | Every loan repaid, after having had one |
| Passive income covers your living | The passive income ratio ≥ 100% at a close |

### 3.7 Other GDD reports (§10 G7, G8)

- **The year-end review (G7 open).** Recommended: a short sheet after each Winter close, *Your year*: the year's farm profit against last year's, net worth's change, the best and worst season, the biggest cost line; its "what to try next" is the accountant's one line. Best decision and costliest mistake (GDD) need a decision record: Phase 3 with the Chronicle.
- **The workforce report (G8 open).** Recommended: on the season report under *Your people*: wages paid against the market, who left and why, output in words; the full workforce report with spec 015's reputation trend in Phase 3.

### 3.8 Processing order

At each season close, after spec 005's settlement and spec 016's businesses: the deposit's interest, the statements' snapshot, net worth kept for the trend, milestones checked. The hub is a view: it reads state and never changes it.

## 4. Data

- `content/finance.json`: the debt-to-assets bands, the emergency fund's amber line, the trend's length (8), reports kept (8), the deposit's rate (G4).
- `content/ui.json`: milestones (id, condition, strings).

**New state** (save schema 14 in Sprint 18, ADR 0076): the founder's season statements (the last 8: cash flow by group, balance sheet lines), net worth at each close (the last 8), the deposit's balance (an account in the ledger: 1020 Bank deposit), milestones reached (id, at).

## 5. Commands and events

| Command | Parameters | Validation | Effect |
| --- | --- | --- | --- |
| `Deposit` | amount | `InsufficientFunds`, `BelowMinimum` (100) | Cash → 1020 |
| `Withdraw` | amount | `InsufficientDeposit` | 1020 → cash |

| Event | Payload | Consumers |
| --- | --- | --- |
| `InterestEarned` | amount | Away card, statements |
| `MilestoneReached` | id | Toast, play log, the hub |
| `YearReviewed` | year | The *Your year* sheet (G7) |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Put cash in the deposit | Cash out of hand | 1.5% a season | Counts as emergency fund; not collateral | Take it out any time |
| Borrow to buy an asset | Debt to assets up | Faster growth if it earns more than the interest | Emergency fund thinner | Repay |

## 7. UI

```
Money and loans (the hub)
| At a glance                                                   |
|  Cash 41,224 [?]   Deposit 20,000 [?]                         |
|  What you own minus what you owe 14,05,491 [?]  ╱╲_╱‾ (8 seasons) |
|  What it's made of  (pie: land 89%, buildings 4%, animals 3% …) [?] |
|  Debt to assets 12% · comfortable [?]                         |
|  Emergency fund 1.4 seasons [?]                               |
|  Passive income 18% of your living [?]                        |
| Your books: This season so far [See]  Summer, year 2 [See] …  |
| Milestones: First rent (Spring, year 2) …                     |
| Your loans … | Borrow … | Savings deposit [Put in] [Take out] |

A season's books (three tabs, as the mill's): Profit and loss | Cash flow | Balance sheet
```

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | At each close the founder's cash flow statement reconciles: opening + every group = closing, equal to the ledger's cash | `StatementTests.CashFlow_Reconciles` |
| AC-2 | The balance sheet's own minus owe equals net worth (ADR 0044) at the close | `StatementTests.BalanceSheet` |
| AC-3 | The last 8 seasons are kept and each opens its three statements | `MoneyViewTests.PastSeasons` |
| AC-4 | (G2) A season's lines compare with the same season last year | `MoneyViewTests.LastYear` |
| AC-5 | Debt to assets, the emergency fund and passive income follow §3.2 and §3.4, rounded down, each with its *Why?* | `HubTests.Ratios` |
| AC-6 | (G4) Deposit and withdraw move cash and 1020; interest at the close; never collateral | `DepositTests.Interest` |
| AC-7 | Each milestone fires once, by its condition, saved and replayed | `MilestoneTests.Once` |
| AC-8 | (G7) After each Winter close, *Your year* compares with last year | `MoneyViewTests.YearReview` |
| AC-9 | A schema-13 save loads into 14 with no deposit, statements from the next close, the trend from its net worth | `MigrationTests.Schema13_Statements` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Competent bot's debt to assets at the Village | 15–40% | Under 60% |
| Cautious saver's emergency fund at day 30 | 1–3 seasons | |
| Passive income at the Village | 5–30% of living (rent, a hired mill) | 100% is a late-game milestone |
| The deposit | Earns less than a family house's rent (10% a season) | No money loop: borrow at 2.5–3% to deposit at 1.5% loses |

## 10. Open questions

- **G1. Your statements.** The founder's season report as three statements laid out as a business's Books (P&L as now, cash flow in three groups, a balance sheet), the last 8 seasons kept.
- **G2. Plan versus actual for the farm.** Each line against the same season last year (recommended); or a written season plan; or none (businesses only).
- **G3. Where the hub lives.** Money and loans grows into the hub (recommended), or a new *Your wealth* screen from the Menu.
- **G4. A savings deposit.** At the co-op bank, any time, 1.5% a season, not collateral (recommended); or none in Phase 2 (the emergency fund is cash alone).
- **G5. What counts as passive income.** Rent, deposit interest and hired-run businesses' profit (recommended); the farm isn't counted.
- **G6. Milestones.** The GDD's five (first lakh saved, twice the inheritance, first rent, debt-free after a loan, passive income covers living), a toast and a list, no rewards.
- **G7. The year-end review.** *Your year* after each Winter close (numbers against last year, the accountant's one line), best decision and costliest mistake with the Chronicle in Phase 3.
- **G8. The workforce report.** A *Your people* section on the season report (wages against the market, who left and why, output); the full report in Phase 3.

## 11. Cut first

1. The year-end review.
2. The net worth trend chart (keep the number).
3. Milestones beyond the first lakh and debt-free.
