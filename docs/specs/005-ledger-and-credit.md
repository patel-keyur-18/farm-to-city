# 005. Ledger and credit

- Status: Approved (2026-10-17); report lines added by ADR 0032 (2026-10-18)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Assets and wealth (net worth, credit, personal bankruptcy, never stuck, liquidity); Time and world simulation (settlement cadence); Businesses (books); Learning layer (cash ledger, balance sheet); Balancing and testing (exact ledgers, never stuck, recoverable bankruptcy); Data model (`LedgerEntry`, `Loan`, `LenderDef`, `StartDef`)
- Related ADRs: 0032 (report lines, farm profit), 0030 (inherited farm, starting cash 20,000), 0031 (new expense accounts), 0028 (land-purchase loans, Hamlet pace), 0008 (coin scale, payment timing, accrual), 0009 (credit from day one, foreclosure, never stuck), 0002 (away rules), 0017 (Phase 1 scope)
- Depends on specs: 001 (scheduler, passes). Uses 002 (land prices, protected parcel, seizure). Spec 003 (living costs, inputs, trees, day labour), 004 (sales, stored goods value) and 006 (wages) post through this ledger.

## 1. Goal

Every coin the founder earns, spends, borrows or owes is recorded exactly, and each season closes like a business quarter: a P&L, a cash-flow summary and a net worth, so the player can see why profit and cash differ and what borrowing really costs.

**Lessons:** profit is not cash (prepaid costs), interest compounds against you when an asset earns less than it costs, and missed payments have lasting consequences, yet a mistake is never the end.

## 2. Scope

**In:**
- **The founder's ledger** (the personal wallet): double-entry, exact to 1/100 coin, with a fixed chart of accounts.
- **The opening balance sheet** from `StartDef`: starting cash, the inherited land, the hut and the well.
- **Accrual:** prepaid costs (living costs, wages, rent) expensed day by day; refunds of the unearned part.
- **The season close:** depreciation, interest, the P&L, the cash-flow summary, net worth, a reconciliation check, then settlement of what's owed and next season's upfront payments, in a fixed priority order.
- **Credit:** the village moneylender and the cooperative bank (ADR 0009), credit limits, a credit score, loan schedules, early repayment, missed payments, penalty interest, restructuring and default.
- **Foreclosure:** seizure, the protected plot, debt write-off and credit damage.
- **Never stuck:** unpaid living costs never create debt.

**Out** (and where it goes instead):
- What the founder's living costs, inputs, trees and day labour cost or earn: 003. Sales and stored-goods prices: 004. Wages and contract lapses: 006. They all post through the operations defined here.
- The finance hub screens: 008 HUD (Phase 1 shows the season report and loans); the full statements and the personal finance hub: Phase 2 (F14.3, F14.4).
- Business books, invoices and payment terms: Phase 2–3 (each business gets its own ledger using this design).
- The town treasury, taxes, savings deposits, bonds and shares: later phases.
- Inflation and the price index: 004.
- Larger bank loans from the Village era: the era cap is data here; the Village unlock is F09.4.
- Valuing stored goods in the ledger (inventory accounting): Phase 2 statements (F14.3). In Phase 1 stored goods appear only in net worth.

## 3. Rules and formulas

Money is `long` in 1/100 coin. Rates are `Fixed`. **Money × rate** (`MulRate`) is computed exactly and rounded once to the nearest 1/100 coin, ties away from zero. Pro-rata splits use integer division as stated per rule, with the remainder landing on the last step so totals are exact.

### 3.1 Ledger

| Rule | Behaviour |
| --- | --- |
| Journal entry | `(id, simTime, period, descriptionKey, reference, postings[])`. Each posting is `(account, amount)`: positive = debit, negative = credit. **The postings of an entry always sum to zero**; an unbalanced entry is a bug and throws |
| Cash never goes negative | There is no overdraft. Every command checks `CanAfford` before it pays; a posting that would make Cash negative throws (a bug). Borrowing is the only way to spend money you don't have |
| Period | Each entry belongs to a season (`period` = season index since t = 0). Accruals posted by the season close belong to the closing season; every other entry at or after a season boundary belongs to the new season (§3.4) |
| Balances | Kept per account, updated on every posting |
| Reconciliation | At every season close: the trial balance sums to zero, and assets = liabilities + equity. A failure throws in tests and is logged in builds |
| Retention | Entries of the current season and the last `LedgerDef.retainSeasons` closed seasons are kept for *Why?*; older entries are dropped (their season reports keep the totals) |

**Chart of accounts** (fixed ids; the *Why?* text comes from string tables):

| Group | Accounts |
| --- | --- |
| Assets | 1000 Cash · 1100 Prepaid living costs · 1110 Prepaid wages · 1120 Prepaid rent · 1500 Land · 1600 Buildings · 1650 Accumulated depreciation (contra) · 1700 Trees |
| Liabilities | 2100 Moneylender loans · 2200 Bank loans · 2300 Interest payable |
| Equity | 3000 Opening equity (the inheritance) · 3100 Retained earnings |
| Income | 4000 Crop sales · 4010 Fruit sales · 4100 Day-labour earnings · 4800 Gain on asset sales · 4900 Debt written off |
| Expenses | 5000 Seeds and planting · 5010 Fertiliser and spraying · 5100 Hired labour · 5200 Living costs · 5250 Health costs · 5300 Rent · 5400 Interest · 5410 Penalty interest · 5500 Depreciation · 5600 Loss on asset sales and seizure · 5700 Transport and selling costs |

**Opening balance sheet** (at `NewGame`): Dr Cash `StartDef.startingCash`, Dr Land (the inherited parcels' market value, spec 002), Dr Buildings (`BuildingDef.value` of the hut and the well), Cr Opening equity (their sum).

### 3.2 Accrual: prepaid costs

A **prepayment** is cash paid upfront for a period of time: `(id, expenseAccount, prepaidAccount, total, start, end, expensed)`.

| Rule | Formula or behaviour |
| --- | --- |
| Payment | Dr prepaid account, Cr Cash, for `total`, at the payment time |
| Daily expensing | At every daily pass, for each open prepayment in id order: `target = total × (min(t, end) − start) ÷ (end − start)` (integer division, rounded down); post Dr expense, Cr prepaid for `target − expensed`; set `expensed = target` |
| Exact total | At `end`, `target = total`, so the full amount is expensed exactly once, with any remainder on the last day |
| Early close | `ClosePrepayment(id, t)`: first expense up to `t` as above; then refund `total − expensed` to Cash (Dr Cash, Cr prepaid). Spec 006 deducts any severance separately |

Example: 36,000 coins of wages prepaid for a 4-day season are expensed 9,000 at each daily pass. 10,000.01 coins are expensed 2,500.00, 2,500.00, 2,500.00 and 2,500.01.

### 3.3 Fixed assets and depreciation

| Rule | Formula or behaviour |
| --- | --- |
| Land | Recorded at cost (purchase price, or the opening value for the inherited parcel). Never depreciated |
| Buildings | Straight-line: `perSeason = cost ÷ BuildingDef.usefulLifeSeasons` (rounded down), posted at each season close (Dr Depreciation, Cr Accumulated depreciation); the final season takes the remainder, so book value reaches exactly 0 and never goes below it. Example: the hut, 40,000 over 80 seasons, is 500 a season |
| Trees | Saplings are capitalised at cost (spec 003). Not depreciated in Phase 1; a removed tree's book value goes to 5600 |
| Selling or seizing an asset | Dr Cash (proceeds), Cr the asset at book value; the difference goes to 4800 (gain) or 5600 (loss) |

### 3.4 Season close and settlement order

At each season boundary, the seasonal pass of the finance system (system order Business finance) runs these steps in order. The daily pass at the same time has already expensed prepayments up to the boundary (spec 001: Daily runs before Seasonal).

**Close the season** (accruals; no cash moves):
1. **Depreciation** for the season (§3.3).
2. **Interest** accrued to the boundary for every loan, including penalty interest on arrears (§3.6): Dr Interest / Penalty interest, Cr Interest payable.
3. **Report:** the season's P&L by account, the cash-flow summary (opening cash, inflows, outflows, closing cash, with prepaid payments shown as cash out but not as expense), and the net worth snapshot (§3.5). Income and expense balances move to Retained earnings. The reconciliation check runs.

**Settle and pay upfront** (cash moves; these entries belong to the new season), in this priority order, each item in id order:
4. **Loan payments due:** arrears first, then this season's interest and principal (§3.6). A payment takes whatever cash there is, up to the amount due; any shortfall is a missed payment.
5. **Living costs** for the new season (spec 003). If they can't be paid in full, the household drops to subsistence (ADR 0009); no debt is created.
6. **Contract renewals** (spec 006), in contract id order. A renewal that can't be paid lapses.
7. **Rent** (later specs).
8. **Credit outcomes:** score changes (§3.7), default detection (§3.8) and, if needed, foreclosure (§3.9).
9. Emit `SeasonClosed` with the report and the settlement results.

**Report lines** (ADR 0032). The P&L groups the accounts like this; each line carries a plain-language label key and a *Why?* explanation key (text in spec 008), plus its accounts for drill-down:

| Line | Accounts | Label (English) |
| --- | --- | --- |
| Farm income | 4000 Crop sales (including home use, spec 003), 4010 Fruit sales | Money from your harvests |
| Farm costs | 5000, 5010, 5100, 5300, 5500 (all depreciation), 5700 | What farming cost you |
| **Farm profit** | Farm income − farm costs | Did the farm pay for itself? |
| Other income | 4100, 4800, 4900 | Other money earned |
| Household | 5200, 5250 | Your family's living costs |
| Finance | 5400, 5410 | Interest |
| Other | 5600 | Losses on assets |
| **Net result** | All income − all expenses | What's left after everything |

Farm profit is the "farm profit" of the credit limit (§3.6) and the Hamlet gate's "profitable season" (farm profit above 0, ADR 0032).

Other systems never move money at the seasonal pass themselves; they register their season obligations (items 5–7) with the finance system, which pays them in this order and reports `ObligationPaid` or `ObligationUnpaid` back. This keeps one place in charge of who gets the cash when it runs short.

### 3.5 Net worth

```
NetWorth = Cash + Prepaid (1100–1120)
         + Σ land at market value (spec 002 ParcelPrice)
         + Σ buildings at book value (cost − accumulated depreciation)
         + Σ trees at book value
         + Σ stored goods × the trader's buying price (spec 004)
         − Σ loan principal outstanding (including principal arrears)
         − Interest payable (accrued and in arrears)
```

- The snapshot is taken at each season close and can be computed at any time for the HUD.
- The *Why?* breakdown lists each term. Book equity (assets − liabilities on the ledger) is shown next to it; the difference is land and stored goods at market value, which is the GDD's "market value can differ from book value".

### 3.6 Loans

**Lenders** (`content/lenders.json`, defaults are placeholders; ADR 0009 bands):

| Field | Moneylender | Cooperative bank |
| --- | --- | --- |
| Rate per season | 4% (fixed) | 2.5% at score 750, plus 0.5 points per 100 score points below 750 |
| Penalty rate per season (extra, on arrears) | 2% | 1.5% |
| Schedule | Interest-only, principal due at the end (bullet) | Equal principal instalments |
| Term | 1–4 seasons | 2–12 seasons |
| Secured | No | Yes: non-protected land and buildings |
| Base limit | 25,000 coins | 0 |
| Collateral loan-to-value | 0 | 0.60 |
| Income multiple | 0.5 | 1.0 |
| Minimum score | 300 (none) | 550 |
| Grace period | 1 season close | 2 season closes |
| Era cap (total owed to this lender) | Homestead 50,000 | Homestead 5,00,000; Village 25,00,000 |

**Credit limit** (per lender, recomputed whenever asked):

```
collateral   = Σ market value of non-protected land + Σ book value of non-protected buildings   (secured lenders only)
avgIncome    = max(0, mean farm profit of the last up to 4 closed seasons)                       (0 before the first close)
scoreFactor  = 0                                              if score < minScore
             = 0.5 + 0.5 × min(1, (score − minScore) ÷ (fullScore − minScore))   otherwise       (fullScore 750; moneylender: always 1)
rawLimit     = baseLimit + collateralLtv × collateral + incomeMultiple × avgIncome
limit        = max(0, min(eraCap, rawLimit × scoreFactor) − owedToThisLender), rounded down to a whole 100 coins
```

- The inherited parcel and everything on it is never collateral (it can never be seized, ADR 0009).
- **Land-purchase loans** (ADR 0028): when a loan finances `BuyParcel`, the parcel being bought counts as collateral for that limit.
- Example (bank, score 650, nothing owed, 2 bought acres worth 2,40,000, average farm profit 20,000): scoreFactor = 0.5 + 0.5 × 100 ÷ 200 = 0.75; rawLimit = 0 + 0.60 × 2,40,000 + 1.0 × 20,000 = 1,64,000; limit = 1,23,000.

**Rate** is fixed when the loan is taken: moneylender `ratePerSeason`; bank `baseRate + rateSlope × max(0, refScore − score) ÷ 100` (Fixed). At score 650 the bank charges 3.0% a season.

**Interest** accrues on outstanding principal in segments of constant principal: `MulRate(principal, rate) × seconds ÷ S`, computed as one exact product and rounded once per segment. A new segment starts whenever principal changes (a repayment) and at each season close. Arrears (unpaid principal and interest) accrue at `rate + penaltyRate` the same way, posted to Penalty interest.

**Schedules:**
- **The first season close** after the loan is taken charges the interest accrued so far. If the loan was taken at the very start of a season (the boundary time), that close is also the first instalment; otherwise the instalments start at the next close.
- **Equal principal (bank):** each instalment is `principal ÷ term` (rounded down), the last one takes the remainder, plus that season's interest.
  - Example: 1,00,000 at 2.5% over 4 seasons, taken at a season start: 27,500 · 26,875 · 26,250 · 25,625 (interest 6,250 in total).
- **Bullet (moneylender):** interest each season, all principal at the last instalment.
  - Example: 20,000 at 4% over 2 seasons, taken at a season start: 800, then 20,800.

**Early repayment:** `RepayLoan(loanId, amount)` at any time pays accrued interest to date first, then arrears, then principal. There is no early-repayment penalty. Repaying principal early shortens the schedule from the end (later instalments shrink).

**Restructuring:** `RestructureLoan(loanId)`, once per loan, only while it has arrears and hasn't defaulted. The arrears are added to principal, the remaining term grows by up to `LenderDef.restructureExtraSeasons` (default 4), the grace counter resets, and the score drops by `restructurePenalty` (default 20). The moneylender doesn't restructure.

### 3.7 Credit score

A single founder credit score, 300–900, like India's CIBIL score.

| Event (at a season close, once per season, not per loan) | Change | Default value |
| --- | --- | --- |
| All payments due this season paid in full | `+onTimeGain` | +10 |
| Any payment missed (partly or fully) | `−missedPenalty` | −40 |
| No loans and no arrears all season | `+cleanSeasonGain` | +5 |
| A loan defaults | `−defaultPenalty` | −150 |
| Restructuring | `−restructurePenalty` | −20 |
| Foreclosure | Set to `foreclosureScore` | 350 |

- The starting score is `startScore` (650, "no history").
- The score is clamped to [300, 900] after each change.
- Each change is recorded with its reason for *Why?*.

### 3.8 Missed payments and default

| Rule | Behaviour |
| --- | --- |
| Missed payment | The unpaid part of a due payment becomes arrears on that loan (principal arrears stay in the loan account; interest arrears stay in Interest payable). `LoanPaymentMissed` is emitted |
| Arrears count | `arrearsCloses` counts consecutive season closes at which the loan still has arrears after step 4. It resets to 0 when the arrears are cleared |
| Curing | Paying the arrears (`RepayLoan`), selling land (spec 002 quick sale) or restructuring before the next close stops the count |
| Default | When `arrearsCloses > LenderDef.graceCloses`, the loan defaults: `LoanDefaulted`, score −150, and foreclosure starts (§3.9) |

### 3.9 Foreclosure (personal bankruptcy, ADR 0009)

Foreclosure runs when any loan defaults. All the founder's lenders take part.

1. **Seize** every non-protected asset, in this order (each group in id order):
   - bought parcels, with everything on them (buildings, trees, and any crop, which is lost)
   - non-protected buildings elsewhere (none in Phase 1)

   Equipment, livestock and businesses join this list in later phases. Each item raises its quick-sale value (spec 002: 80% of market value).
2. **Protect** the inherited parcel and everything on it: the hut, the well, its trees and its crops. Stored goods and cash are not seized, but cash was already applied to the payments at step 4.
3. **Repay:** proceeds pay the secured lender (the bank) first, then unsecured lenders in proportion to what each is owed (rounded down, the remainder to the lowest loan id). Any surplus goes to the founder's cash.
4. **Write off** what's still owed: Dr loans and Interest payable, Cr 4900 Debt written off. Every loan is closed.
5. **Credit damage:** the score is set to `foreclosureScore` (350). That is below the bank's minimum, so only the moneylender lends until the score recovers. At +10 a season of on-time payments, recovery takes about 5 game years.
6. **Carries on:** the town and every other system. The founder's holdings are back at Homestead level. Emit `ForeclosureCompleted` with the seized items, proceeds and amount written off.

**Not while away (ADR 0002: no disasters while away).** If a default is reached during an away advance, foreclosure is deferred. On the first live `Sync` after the away advance, a `FinalNotice` event is emitted, and foreclosure runs at the next daily pass that runs live, unless the arrears have been cured by then.

### 3.10 Never stuck

- An unpaid living-cost obligation never creates a liability. The household falls to subsistence (spec 003), and nothing is owed.
- Day-labour earnings (spec 003) post straight to Cash (Dr Cash, Cr 4100) when earned.
- Nothing in this spec takes a loan automatically.

## 4. Data

**Definitions:**

`content/finance.json` (`LedgerDef`, `CreditScoreDef`):

| Definition | Field | Type | Default | Bounds |
| --- | --- | --- | --- | --- |
| `LedgerDef` | `retainSeasons` | int | 4 | 1–16 |
| `CreditScoreDef` | `min`, `max` | int | 300, 900 | min < max |
| | `startScore` | int | 650 | min–max |
| | `fullScore` | int | 750 | min–max |
| | `onTimeGain`, `cleanSeasonGain` | int | 10, 5 | 0–100 |
| | `missedPenalty`, `defaultPenalty`, `restructurePenalty` | int | 40, 150, 20 | 0–600 |
| | `foreclosureScore` | int | 350 | min–max |

`content/lenders.json` (`LenderDef`, one per lender; defaults in §3.6):

| Field | Type | Bounds |
| --- | --- | --- |
| `id`, `nameKey` | string | `moneylender`, `coop_bank` |
| `baseRate` | Fixed | 0–0.25 per season |
| `rateSlopePer100`, `refScore` | Fixed, int | slope 0–0.05; refScore in score range |
| `penaltyRate` | Fixed | 0–0.25 |
| `schedule` | enum | `bullet`, `equalPrincipal` |
| `minTerm`, `maxTerm` | int | 1 ≤ min ≤ max ≤ 40 |
| `secured` | bool | |
| `baseLimit` | money | ≥ 0 |
| `collateralLtv`, `incomeMultiple` | Fixed | 0–1, 0–4 |
| `minScore` | int | in score range |
| `graceCloses` | int | 1–8 |
| `restructureExtraSeasons` | int | 0–12 (0 = doesn't restructure) |
| `eraCaps` | map era → money | Homestead required |

`content/start.json` (`StartDef`, finance part): `startingCash`, money, placeholder **20,000 coins** (ADR 0030: with the inherited store of seed and grain it pays the first season's basic living costs and a little seed; less than a hand's season wage; tuned by the economy model). The inherited store itself is spec 004's.

**State** (part of `GameState`):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Ledger` | `balances` | long per account | In account order |
| | `entries` | list of `JournalEntry` | Retained seasons only |
| | `nextEntryId`, `currentPeriod` | long | |
| | `reports` | list of `SeasonReport` | P&L lines, cash flow, net worth snapshot, credit summary |
| `Prepayment` | `id`, `expenseAccount`, `prepaidAccount`, `total`, `start`, `end`, `expensed`, `ownerRef` | | |
| `Obligation` | `id`, `kind` (Living, Contract, Rent), `ownerRef`, `amount`, `periodStart`, `periodEnd` | | Registered by 003 and 006 |
| `Loan` | `id`, `lenderId`, `originalPrincipal`, `principal`, `rate`, `penaltyRate`, `schedule`, `term`, `takenAt`, `instalmentsPaid`, `arrearsPrincipal`, `arrearsInterest`, `arrearsCloses`, `segmentStart`, `restructured`, `state` (Active, Repaid, Defaulted, WrittenOff) | | |
| `Credit` | `score`, `scoreHistory` (time, change, reason), `pendingForeclosure` (bool), `foreclosures` (count) | | |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `TakeLoan` | `lenderId`, `amount`, `termSeasons` | `UnknownLender`; `InvalidAmount` (≤ 0 or not whole coins); `InvalidTerm`; `ScoreTooLow`; `OverLimit` | Cash in (Dr Cash, Cr loans), schedule fixed. Emits `LoanTaken` |
| `BuyParcel` with `financing` | spec 002 parameters plus `lenderId`, `amount`, `termSeasons` | As `TakeLoan`, with the parcel counted as collateral; plus spec 002's checks. Both succeed or neither | The loan and the purchase in one step |
| `RepayLoan` | `loanId`, `amount` | `UnknownLoan`; `LoanClosed`; `InvalidAmount`; `InsufficientFunds`. Amounts above what's owed are capped | §3.6 early repayment. Emits `LoanRepayment` |
| `RestructureLoan` | `loanId` | `UnknownLoan`; `NoArrears`; `AlreadyRestructured`; `LenderDoesNotRestructure`; `LoanClosed` | §3.6. Emits `LoanRestructured` |

**Internal operations** (used by other specs): `Post(entry)`, `CanAfford(amount)`, `Pay(expenseOrAssetAccount, amount, ref)`, `Receive(incomeAccount, amount, ref)`, `OpenPrepayment(...)`, `ClosePrepayment(id, t)`, `RegisterObligation(...)`, `CapitaliseAsset(...)`, `DisposeAsset(...)`.

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `SeasonClosed` | `period`, report (P&L lines, cash flow, net worth), settlement results | Every season close, after settlement | HUD season report, advisors, play log, onboarding (first season close) |
| `ObligationPaid` / `ObligationUnpaid` | `obligationId`, `kind`, `ownerRef`, `amount` | Settlement steps 5–7 | Spec 003 (subsistence), 006 (lapse) |
| `LoanTaken` | `loanId`, `lenderId`, `amount`, `rate`, `term` | `TakeLoan` succeeds | HUD, play log (first loan) |
| `LoanRepayment` | `loanId`, `interest`, `arrears`, `principal` | A scheduled or early payment | HUD |
| `LoanPaymentMissed` | `loanId`, `unpaid`, `arrearsCloses` | Settlement step 4 leaves a shortfall | HUD (urgent), notifications |
| `LoanRestructured` | `loanId`, `newTerm`, `newPrincipal` | Restructuring | HUD |
| `LoanDefaulted` | `loanId` | §3.8 | HUD, Chronicle (later) |
| `FinalNotice` | `loanIds`, `deadline` | A default reached while away, on return | HUD (urgent) |
| `ForeclosureCompleted` | seized items, `proceeds`, `writtenOff`, `newScore` | §3.9 | HUD, Chronicle (later), play log |
| `CreditScoreChanged` | `old`, `new`, `reason` | Any score change | Finance hub |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Borrow from the moneylender | None now; easy to get | 4% a season, and all the principal due at once at the end | Score rises if paid on time | Repay early at any time, no penalty |
| Borrow from the bank | Needs collateral and a score | Cheaper (2.5–3%), but principal is due every season | The land becomes collateral; default means losing all bought land | Repay early at any time |
| Buy land with a loan | A smaller down payment | More acres sooner; interest every season | Labour and care (ADR 0010); foreclosure risk | Quick sale at 80% |
| Hire or prepay for the season | Cash leaves now | The cost appears in the P&L day by day | The P&L shows profit while cash is low | Early termination refunds the unearned part |
| Restructure a loan in arrears | Score −20 | Longer term, more interest in total | Stops the slide to default | Once per loan |

## 7. UI

The finance hub is built in spec 008; this spec defines the season report, the loan sheet and their *Why?* lines.

```
Season report (opens at each season close)
+--------------------------------------------------------+
| Spring, Year 1 closed                     [Why?]       |
| Farm income          crops 18,400   fruit 0             |
| Farm costs           seeds -3,200   labour -2,000       |
|                      depreciation -625                 |
| Farm profit                                12,575      |
| Day labour                                  1,600      |
| Living costs                              -15,000      |
| Interest                                     -800      |
| Net result                                 -1,625      |
| --------------------------------------------------     |
| Cash: start 20,000  in 40,000  out 31,600  end 28,400  |
|   "Wages paid upfront: 9,000 is next season's cost,    |
|    so cash fell more than profit."                     |
| Net worth 6,92,400  (+3,100)              [Why?]       |
+--------------------------------------------------------+

Loan sheet
+--------------------------------------------------------+
| Borrow                       Moneylender | Bank        |
| You can borrow up to          25,000     | 1,23,000    |
|   [Why?] base 25,000         | land 2,40,000 x 0.60    |
|                              | + income 20,000         |
|                              | x score 650 -> 0.75     |
| Rate per season               4.0%       | 3.0%        |
| Repay                         at the end | every season|
| Amount [ 50,000 ]  Term [ 4 seasons ]   Lender [Bank]  |
| Bank payments: 14,000 · 13,625 · 13,250 · 12,875 [Borrow]|
+--------------------------------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Posting an entry whose postings don't sum to zero throws and changes nothing; a balanced entry updates every account balance | `LedgerTests.Post_MustBalance` |
| AC-2 | A posting that would make Cash negative throws; `CanAfford` is true exactly when cash ≥ the amount | `LedgerTests.Cash_NeverNegative` |
| AC-3 | At a new game with the default content, Cash = 20,000 coins, Land = the inherited parcels' market value, Buildings = 55,000 coins, Opening equity = their sum, and the balance sheet balances | `LedgerTests.NewGame_OpeningBalanceSheet` |
| AC-4 | Over 3 game years of seeded random valid activity (purchases, sales, prepayments, loans, repayments, missed payments), the reconciliation check passes at every season close, and the sum of all postings to Cash equals the Cash balance | `LedgerTests.Reconciles_EverySeason` |
| AC-5 | `MulRate(1,000.01 coins, 4%)` = 40.00 coins; `MulRate(1.25 coins, 10%)` = 0.13; `MulRate(−1.25 coins, 10%)` = −0.13 | `MoneyTests.MulRate_RoundsHalfAwayFromZero` |
| AC-6 | 36,000 coins prepaid over a 4-day season is expensed 9,000 at each daily pass and the prepaid balance is 0 at the end; 10,000.01 coins is expensed 2,500.00 three times then 2,500.01 | `AccrualTests.Prepaid_ExpensedDaily_Exact` |
| AC-7 | A prepayment from mid-season to mid-next-season: the closing season's P&L includes exactly the part up to the boundary, and the rest stays prepaid | `AccrualTests.Prepaid_SpansSeasonClose` |
| AC-8 | Closing a prepayment early refunds exactly `total − expensed to date` to cash | `AccrualTests.Prepaid_EarlyClose_RefundsUnearned` |
| AC-9 | Entries posted before a boundary and the close's own accruals belong to the closing season; payments made at the boundary (loan payments, upfront costs) belong to the new season | `SeasonCloseTests.CutOff` |
| AC-10 | The season report lists P&L lines by account with farm profit, living costs, interest and net result; Retained earnings rise by the net result; all income and expense balances are 0 after the close | `SeasonCloseTests.PnL_ClosesToRetainedEarnings` |
| AC-11 | Opening cash + inflows − outflows = closing cash; a season with 36,000 of next-season wages prepaid shows them as cash out but not as expense | `SeasonCloseTests.CashFlow_ProfitIsNotCash` |
| AC-12 | The hut (40,000 over 80 seasons) depreciates 500 a season; a building whose cost doesn't divide evenly reaches exactly 0 in its last season and never below | `SeasonCloseTests.Depreciation_StraightLine` |
| AC-13 | With cash for only part of the season's obligations, loan payments are paid first, then living costs, then contracts in id order; the rest are reported `ObligationUnpaid`, and cash ends at ≥ 0 | `SeasonCloseTests.Obligations_PriorityOrder` |
| AC-14 | Net worth equals the §3.5 sum for a state with cash, prepaid costs, bought land, buildings, a tree, stored goods and a loan, and the snapshot is stored in the season report | `NetWorthTests.Composition` |
| AC-15 | Buying a parcel for cash at its market price leaves net worth unchanged at that moment | `NetWorthTests.BuyLand_IsAnAssetSwap` |
| AC-16 | The §3.6 limit example gives exactly 1,23,000 for the bank; the moneylender's limit at a new game is 25,000; borrowing 1 coin over the limit is rejected with `OverLimit` | `CreditTests.Limit_Formula` |
| AC-17 | At a new game the bank's limit is 0 (the inherited plot is never collateral) | `CreditTests.InheritedPlot_NotCollateral` |
| AC-18 | `TakeLoan` within the limit adds the amount to cash and to the lender's loan account, fixes the rate (bank at score 650: 3.0%), and emits `LoanTaken`; terms outside the lender's bounds are rejected with `InvalidTerm` | `CreditTests.TakeLoan` |
| AC-19 | The §3.6 schedule examples are paid exactly (27,500 · 26,875 · 26,250 · 25,625 and 800 · 20,800); a loan taken half-way through a season pays exactly half a season's interest at the first close, and its first instalment at the next | `CreditTests.Schedules_Exact` |
| AC-20 | Early repayment pays accrued interest first, then principal; later instalments shrink; the total interest paid is less than on schedule | `CreditTests.RepayEarly` |
| AC-21 | With cash short at a close, the payment takes all available cash (interest first), the rest becomes arrears, penalty interest accrues on the arrears at `rate + penaltyRate` the next season, and `LoanPaymentMissed` is emitted | `CreditTests.MissedPayment_ArrearsAndPenalty` |
| AC-22 | The score changes by +10 for a season paid on time, −40 for a missed season, +5 for a clean season with no loans, never once per loan, and stays within [300, 900] | `CreditTests.Score_Changes` |
| AC-23 | A bank loan with arrears at 3 consecutive closes (grace 2) defaults at the third; clearing the arrears after the second close resets the count | `CreditTests.Default_AfterGrace` |
| AC-24 | Restructuring adds arrears to principal, extends the term by up to 4 seasons, resets the count and costs 20 points; a second restructure, or one on a moneylender loan, is rejected | `CreditTests.Restructure` |
| AC-25 | Foreclosure seizes every bought parcel (with its trees and crops) at quick-sale value, leaves the inherited parcel, hut, well and their trees untouched, repays the bank first and then the moneylender pro rata, writes off the rest, closes every loan, sets the score to 350, and the ledger reconciles | `ForeclosureTests.SeizeRepayWriteOff` |
| AC-26 | If proceeds exceed all debts, the surplus is paid into the founder's cash | `ForeclosureTests.Surplus_ReturnsToFounder` |
| AC-27 | A default reached during an away advance doesn't foreclose during it; `FinalNotice` is emitted on the first live `Sync`; foreclosure runs at the next live daily pass unless the arrears are paid first | `ForeclosureTests.NotWhileAway` |
| AC-28 | An unpaid living-cost obligation creates no liability and reports `ObligationUnpaid` | `NeverStuckTests.UnpaidLivingCosts_CreateNoDebt` |
| AC-29 | Loading `lenders.json` with a rate above 0.25, `minTerm > maxTerm`, a missing Homestead era cap or a grace of 0 fails validation naming the field | `ContentTests.Lender_Validation` |
| AC-30 | The spec 001 chunking-invariance check holds with the ledger, two loans, a missed payment and a foreclosure over 2 game years; and the golden replay `golden/005-credit.json` reproduces its stored hash | `LedgerTests.ChunkingInvariance`, `ReplayTests.Golden_005_Credit` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Ledger reconciliation | Exact at every season close for every bot and seed | 0 coins |
| Never stuck (GDD) | A bot with no cash, no credit and only the inherited plot reaches a profitable farming season | Within the seasons set in data (economy model) |
| Recoverable bankruptcy (GDD) | A foreclosed bot regains bank credit (score ≥ 550) | Within 3–5 game years |
| Moneylender vs bank cost for the same loan | The bank is cheaper in total interest whenever the player qualifies | Always |
| Debt-fuelled stress bot | Does not beat the best archetype by more than 20% (ADR 0016) | ADR 0016 |

## 10. Open questions

All seven were resolved at approval (2026-10-17), following the recommendations:

1. **Settlement priority at a season close:** loan payments, then living costs, then wages, then rent (§3.4). Subsistence catches unpaid living costs, so no debt is ever created for them (ADR 0009).
2. **Land-purchase loans** are secured by the parcel bought (LTV 0.60), through `BuyParcel` with financing (ADR 0028).
3. **The credit score runs from 300 to 900**, like India's CIBIL score.
4. **Foreclosure never happens while away.** A final notice appears on return, and foreclosure runs at the next live daily pass unless the default is cured.
5. **Inventory stays off the ledger in Phase 1.** Sales are income when sold, inputs are expenses when bought, and stored goods count only in net worth. Inventory accounting comes with the Phase 2 statements (F14.3).
6. **Two schedules:** the moneylender's interest-only bullet loan, and the bank's equal-principal loan.
7. **Foreclosure takes all non-protected assets** (ADR 0009). A surplus is returned as cash.

### Changes after approval (Sprint 1 review, 2026-10-17)

- **Starting cash 20,000** and the opening Land term covers every inherited parcel (ADR 0030). Implemented: AC-3 passes with the new values.
- **Two expense accounts** join the chart (ADR 0031): 5250 Health costs (medicine, spec 003) and 5700 Transport and selling costs (cart hire, mandi commission and fees, spec 004). No formula changes.
- The inherited land is never collateral (unchanged), so early in the game only the moneylender lends; land-purchase loans serve growth after the Hamlet.

### Changes after approval (Sprint 2 planning review, 2026-10-18)

- **Report lines** (§3.4, ADR 0032): the P&L grouping into farm profit and net result, with plain-language labels and *Why?* keys for the player. Farm profit is the credit limit's income measure and the Hamlet gate's test. No formula changes.
- **Home use:** food the household eats from the store is credited to the good's sales account (spec 003), so it counts in farm income.

## 11. Cut first

1. `RestructureLoan`.
2. Pro-rata interest for the first partial season (charge from the next season start instead).
3. `FinalNotice` (keep "not while away": foreclose at the first live season close instead).
4. Per-reason credit-score history (keep the score).
5. Net worth snapshot per season (compute on demand only).
