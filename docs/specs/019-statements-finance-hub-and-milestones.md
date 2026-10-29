# 019. Your statements, the finance hub and wealth milestones

- Status: Approved (2026-10-27, ADR 0078; G1–G8 closed with the owner, §10). **Amended 2026-10-29 (ADR 0083, G9–G14): Money and loans opens on *This season*; awaiting the owner's re-approval**
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Assets and wealth (net worth, the personal finance screen, wealth milestones, asset classes); Learning layer (reports every player can read)
- Related ADRs: 0083 (*This season*, built in Sprint 15), 0008 (rupee-scale coins), 0009 (credit from day one), 0032 (P&L grouping, home use), 0034 (inputs in store are an asset; the price index), 0038 (foreclosure), 0044 (net worth kept, land on its own row), 0068 (each business's own books), 0076 (schemas by sprint), 0077 (the Village; the Codex)
- Depends on specs: 005 (the ledger, credit, the season report), 008 (sheets and *Why?*), 016 (business books, net worth's businesses), 018 (the Codex, advisors)

## 1. Goal

The player reads their own money the way a business reads its books, profit and loss, cash flow and a balance sheet each season, and sees on one screen how their wealth is built, how much of it is borrowed, how long it would last in a bad season, and how much of it earns without their own work.

**Lesson:** profit is not cash; net worth is what you own minus what you owe; an emergency fund buys time; leverage cuts both ways; passive income is what frees you to build.

## 2. Scope

**In:**
- The founder's three statements each season: profit and loss (spec 005's season report, kept), a cash flow statement, a balance sheet; past seasons kept.
- **Your season** (§3.9, ADR 0083; built in Sprint 15, US-221): Money and loans opens on what you earned and spent this season, line by line, why your cash moved differently from your profit, and what's already due next season.
- The finance hub on Money and loans' *Your wealth* tab: net worth's trend, what your wealth is made of, debt to assets, the emergency fund, the passive income ratio, each with its *Why?*.
- Wealth milestones.
- A savings deposit at the co-op bank (G4).

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
| Balance sheet | **What you own** (cash, deposit, produce and inputs in store, land at market, buildings and machines at book, animals, businesses at equity, owed to you, each valued as net worth values it); **What you owe** (each loan); **Net worth** | `NetWorthSnapshot` (ADR 0044), at the close |

- Interest is under *Your work* (a cost of running the farm), principal under *Borrowing*.
- Past seasons: the last 8 kept (2 game years), each opening its three statements.
- **Against last year (G2).** The founder's season report shows each line beside **the same season last year** ("Winter: 46,200 · last Winter 31,900"), with a *Why?* naming the biggest change; before a year has passed it says "First Winter". A farm's seasons differ too much to compare with the season before; a written plan stays with businesses (spec 016).

### 3.2 The finance hub (§10 G3)

Money and loans becomes the hub (G3: no new screen, reached from the cash pill as now) and is redesigned in the Store and People layout (§7). It has two tabs (G9): **This season** (§3.9), where it opens, and **Your wealth**, which holds the hub:

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

Income that comes without the founder's own labour in a season (G5): rent received from family houses, interest on the deposit, and the profit of businesses run by a hired worker (not ones you run). The farm, even with hands, isn't counted (you still decide and sow).

### 3.5 A savings deposit (§10 G4)

The co-op bank takes deposits, as real rural co-op banks do (G4): **Put in** / **Take out** any amount, any time, from Money and loans; interest 1.5% a season (about 6% a year, a rural savings rate) added to the deposit at each close, so it compounds; it counts in the emergency fund and passive income, never as collateral (it's yours to take). Inflation (2–6% a year) is shown beside it, so the player sees cash in hand lose value and the deposit roughly keep it.

### 3.6 Wealth milestones (§10 G6)

The GDD's five (G6), each once: a toast and a line in Money and loans' *Milestones* (with its season); never a reward, so they never steer play:

| Milestone | When |
| --- | --- |
| First lakh saved | Cash + deposit − what you owe reach 1,00,000 at a close (borrowed cash isn't savings) |
| Twice what you inherited | Net worth reaches twice its first value (the inherited estate) |
| First rent | The first rent received |
| Debt-free | Every loan repaid, after having had one |
| Passive income covers your living | The passive income ratio ≥ 100% at a close |

### 3.7 Other GDD reports (§10 G7, G8)

- **The year-end review (G7).** A short sheet after each Winter close, *Your year*: the year's farm profit against last year's, net worth's change, the best and worst season, the biggest cost line; its "what to try next" is the accountant's one line. Best decision and costliest mistake (GDD) need a decision record: Phase 3 with the Chronicle.
- **The workforce report (G8).** The season report gets a *Your people* section: wages paid against the market wage, who left and why, each worker's output in words and stars, employer reputation's change, each with a *Why?*; the full workforce report with supervisors in Phase 3.

### 3.8 Processing order

At each season close, after spec 005's settlement and spec 016's businesses: the deposit's interest, the statements' snapshot, net worth kept for the trend, milestones checked, then the sales record turns over (§3.9: this season's becomes last season's). The hub is a view: it reads state and never changes it.

### 3.9 Your season (§10 G9–G14, ADR 0083)

Money and loans opens on the **This season** tab, a switch at its top: **This season** | **Last season** (only *This season* before the first close). Every line has its *Why?*; a line shows only when it isn't zero.

| Block | Lines | Source |
| --- | --- | --- |
| **Earned** | Each good sold, by name (its *Why?* says where: "trader 22,000 · mandi 16,200 · market 3,400"); supermarket orders; milk to the co-op; calves and animals sold; rent from families; your businesses' drawings; your day labour; interest on the deposit (from Sprint 18) | The sales record (by good and where sold) for goods; the ledger's income accounts for the rest |
| **Spent** | Seed and planting; fertiliser and sprays; each hand's wage by name; day labourers; feed; the vet; the family's living; health; rent you pay; interest; selling costs (commission, transport); wear on buildings and machines | The ledger's expense accounts as costs are used up (G10); a hand's wage by the `hand:` reference of its accrual |
| **Profit this season** | Earned − spent: the season report's *what's left*, so it matches the report at the close; its *Why?* names the farm's own profit (the Hamlet gate's) inside it | |
| **Why your cash moved differently** | Paid ahead (wages and living for the rest of the season, inputs into the store); wear (a cost, no cash); paid back on loans ("paying back what you borrowed, not a cost"); borrowed; bought and still yours, one row each: buildings, animals, trees, land, machines, put into your businesses ("You still own it: it's in *Your wealth*, not a cost"); sold. Cash at the season's start + profit + these = cash now | The ledger's cash postings to prepaid, asset and loan accounts (G10, G14) |
| **Next season, already due** (this season only) | Each hand's renewal by name (the wage less the house, spec 014 §3.6); each loan's payment as interest and principal; the family's living; rent you pay; all at today's prices (the *Why?*: "the close's price index may add a little"). Then "Cash now covers it", or in amber "Short by 8,400: Ravi's renewal would lapse" | What the close would take if nothing changed (G12). A business's worker is paid by the business; seed and feed are bought as you go: neither is counted |

*Last season* shows the same blocks, finished, without *next season due*, and ends with **See the season report** (spec 005's report until US-137 makes it *Your books*). The same season a year earlier stays with *Your books* (G2).

**The sales record** (G11): this season's and last season's sales value by good and by where sold (trader, mandi, market, orders, your businesses), written where each sale is posted; at each close this season's becomes last season's.

## 4. Data

- `content/finance.json`: the debt-to-assets bands, the emergency fund's amber line, the trend's length (8), reports kept (8), the deposit's rate (G4).
- `content/ui.json`: milestones (id, condition, strings).

**New state, Sprint 15** (save schema 12, ADR 0083): the sales record (§3.9), on `FinanceState`.

**New state, Sprint 18** (save schema 15; 14 before ADR 0083 shifted the schemas by one): the founder's season statements (the last 8: cash flow by group, balance sheet lines), net worth at each close (the last 8), the deposit's balance (an account in the ledger: 1020 Bank deposit), milestones reached (id, at).

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

Money and loans in the Store and People layout (G3): a glance row, picture buttons, then pictured sections. Every number has a *Why?*.

```
Money and loans                     [ This season ]  [ Your wealth ]
| [This season] [Last season]   Spring, year 2 · day 3    Cash 41,224 [?] |
| Earned                                                    62,480 [?] |
|   🌾 Wheat                                                  38,200 [?] |
|   🥛 Milk to the co-op                                      14,880 [?] |
|   🏠 Rent from 2 families                                    9,400 [?] |
| Spent                                                     47,915 [?] |
|   🌱 Seed and planting                                       6,300 [?] |
|   👤 Ravi's wage                                            22,500 [?] |
|   🍲 Your family's living                                  12,000 [?] |
|   …                                                                   |
| Profit this season                                        14,565 [?] |
| Why your cash moved differently                                       |
|   Paid ahead (wages, living, inputs)                       −9,000 [?] |
|   🏠 Buildings bought: still yours                         −60,000 [?] |
|   Paid back on loans: not a cost                           −8,000 [?] |
|   Cash: 1,03,659 at the start → 41,224 now                         [?] |
| Next season, already due                                  41,430 [?] |
|   👤 Ravi's renewal 24,000 · 🏦 Bank: interest 1,430, principal 4,000 |
|   🍲 Your family's living 12,000                                       |
|   Cash now covers it ✓                                                 |

The Your wealth tab (US-135, Sprint 18):
| Cash 41,224 [?]                    Net worth 14,05,491 [?]          |
| [🏦 Borrow        ] [💰 Savings deposit ] [📒 Your books        ]   |
|   up to 35,600      20,000 · 1.5% a season  Summer, year 2          |
| Your wealth                                                          |
|  ╱╲_╱‾ net worth, last 8 seasons [?]                                 |
|  (pie) land 89% · buildings 4% · animals 3% · cash 2% … [?]          |
|  Debt to assets   12% · comfortable [?]                              |
|  Emergency fund   1.4 seasons [?]                                    |
|  Passive income   18% of your living [?]                             |
| Your loans                                                           |
|  🏦 Cooperative bank · owes 56,000 · 1,430 due at the season's end [Repay] |
|  🏭 Cooperative bank, for the flour mill · owes 64,000      [Open]   |
| Milestones                                                           |
|  ⭐ First rent · Spring, year 2                                      |

Borrow: a sheet of lenders (pictured rows, each with its limit, rate and Borrow).
Savings deposit: balance, interest this season, inflation beside it; Put in / Take out amounts.
Your books: this season so far and past seasons, each opening three tabs as the mill's:
  Profit and loss | Cash flow | Balance sheet, each line beside the same season last year.
```

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | At each close the founder's cash flow statement reconciles: opening + every group = closing, equal to the ledger's cash | `StatementTests.CashFlow_Reconciles` |
| AC-2 | The balance sheet's own minus owe equals net worth (ADR 0044) at the close | `StatementTests.BalanceSheet` |
| AC-3 | The last 8 seasons are kept and each opens its three statements | `MoneyViewTests.PastSeasons` |
| AC-4 | A season's lines compare with the same season last year | `MoneyViewTests.LastYear` |
| AC-5 | Debt to assets, the emergency fund and passive income follow §3.2 and §3.4, rounded down, each with its *Why?* | `HubTests.Ratios` |
| AC-6 | Deposit and withdraw move cash and 1020; interest at the close; never collateral | `DepositTests.Interest` |
| AC-7 | Each milestone fires once, by its condition, saved and replayed | `MilestoneTests.Once` |
| AC-8 | After each Winter close, *Your year* compares with last year | `MoneyViewTests.YearReview` |
| AC-9 | A schema-14 save loads into 15 with no deposit, statements from the next close, the trend from its net worth | `MigrationTests.Schema14_Statements` |
| AC-10 | The season report's *Your people* section lists wages against the market, who left and why, output and reputation's change | `MoneyViewTests.YourPeople` |
| AC-11 | Money and loans opens on *This season*; *Your wealth* holds net worth, land, businesses, owed, loans and borrowing | `MoneyViewTests.OpensOnSeason` |
| AC-12 | Earned − spent equals the season report's *what's left* at the close; cash at the start + profit + every *why your cash moved* line = cash now, to the coin; bought assets and loan principal are never under *Spent* | `MoneyViewTests.Season_Reconciles` |
| AC-13 | Each good sold has its own line, summing the sales record, with where it sold in its *Why?*; orders have their own line | `MoneyViewTests.Season_ByGood` |
| AC-14 | *Next season, already due* equals what the next close takes when nothing else changes (hands' renewals less the house, loan payments, living, rent); the cover line warns when cash is short | `MoneyViewTests.NextSeasonDue` |
| AC-15 | *Last season* shows the finished season's figures (equal to its report) without *next season due*; before the first close only *This season* shows | `MoneyViewTests.LastSeason` |
| AC-16 | A schema-11 save loads into 12 with an empty sales record that fills from the next sale, and round-trips | `MigrationTests.Schema11_Sales` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Competent bot's debt to assets at the Village | 15–40% | Under 60% |
| Cautious saver's emergency fund at day 30 | 1–3 seasons | |
| Passive income at the Village | 5–30% of living (rent, a hired mill) | 100% is a late-game milestone |
| The deposit | Earns less than a family house's rent (10% a season) | No money loop: borrow at 2.5–3% to deposit at 1.5% loses |

## 10. Open questions

- **G1. Your statements.** **Closed (owner, 2026-10-27):** the season report becomes three statements laid out as the mill's Books: profit and loss as now, cash flow in three groups (your work, what you bought or sold, borrowing), a balance sheet (what you own, what you owe, net worth); the last 8 seasons kept (§3.1).
- **G2. Plan versus actual for the farm.** **Closed (owner, 2026-10-27):** each line of the season report beside the same season a year earlier, with a *Why?* naming the biggest change; "First Winter" until a year has passed; a written plan stays with businesses (§3.1).
- **G3. Where the hub lives.** **Closed (owner, 2026-10-27):** the hub is Money and loans, reached from the cash pill as now, and the sheet is redesigned in the Store and People layout: a glance row, picture buttons (Borrow, Savings deposit, Your books), pictured sections for your wealth, your loans and milestones (§3.2, §7).
- **G4. A savings deposit.** **Closed (owner, 2026-10-27):** a savings deposit at the co-op bank: put in or take out any amount any time, 1.5% a season (about 6% a year) added to the deposit at each close (so it compounds), counted in the emergency fund and passive income, never collateral; inflation shown beside it; no money loop (borrowing costs 2.5–3% a season) (§3.5).
- **G5. What counts as passive income.** **Closed (owner, 2026-10-27):** rent from family houses, interest on the deposit, and the profit of businesses run by a hired worker (not those you run); the farm isn't counted, even with hands (§3.4).
- **G6. Milestones.** **Closed (owner, 2026-10-27):** the GDD's five (first lakh saved, net worth twice the inheritance, first rent, debt-free after a loan, passive income covering living), each once: a toast and a line in Money and loans' Milestones with its season; no rewards, so they never steer play (§3.6).
- **G7. The year-end review.** **Closed (owner, 2026-10-27):** after each Winter close a short *Your year* sheet: the year's farm profit against last year's, net worth's change, the best and worst season, the biggest cost line, and the accountant's one line on what to try next; best decision and costliest mistake with the Chronicle in Phase 3 (§3.7).
- **G8. The workforce report.** **Closed (owner, 2026-10-27):** a *Your people* section on the season report: wages paid against the market wage, who left and why, each worker's output in words and stars, employer reputation's change, each with a *Why?*; the full workforce report with supervisors in Phase 3 (§3.7).

- **G9. Where *your season* sits** (US-221, owner, 2026-10-29, ADR 0083). **Closed:** Money and loans has two tabs and opens on *This season*; *Your wealth* holds net worth, land, businesses, owed, loans and borrowing, and becomes the hub in Sprint 18 (§3.2, §3.9).
- **G10. What profit means there.** **Closed:** earned and spent as the books count them (a cost when used up); profit equals the report's *what's left*, the farm's profit in its *Why?*; *why your cash moved differently* reconciles to cash now (§3.9).
- **G11. Each crop by name.** **Closed:** a sales record in state, by good and by where sold, this season and last (save schema 12; golden replays 003 and 004 re-pinned once after showing only the record changed); orders their own line; the later schemas shift by one (§3.9, §4).
- **G12. Next season, already due.** **Closed:** each hand's renewal by name, loan payments as interest and principal, the family's living and rent you pay, at today's prices, with the cover line; no business worker, no seed or feed (§3.9).
- **G13. Which seasons.** **Closed:** a *This season* | *Last season* switch; last season ends with the season report; the same season last year stays with *Your books* (§3.9).
- **G14. Things you bought.** **Closed:** rows per kind under *why your cash moved differently*, never under *spent*, with loan principal beside them (§3.9).

## 11. Cut first

1. The year-end review.
2. The net worth trend chart (keep the number).
3. Milestones beyond the first lakh and debt-free.
