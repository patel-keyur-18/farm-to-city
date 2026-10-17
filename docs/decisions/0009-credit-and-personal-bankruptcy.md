# 0009. Credit from day one, and personal bankruptcy as foreclosure

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0003 (ageless founder), ADR 0008 (upfront costs, starting package)
- GDD sections: Player journey (era systems), Businesses (failure), Assets and wealth (leverage), Decisions and consequences, Balancing and testing

## Context

ADR 0008 makes costs upfront, so a player who spends badly can run out of cash. The owner wants three things:
- to be able to borrow at interest from the start
- real stakes for over-borrowing
- a way back for a broke player

The GDD offered loans only from the Village era, and it said bankruptcy never ends a save. The owner first proposed that going past a debt cap should end the game and force a new save.

## Options

1. **Hard game over.** Past the cap the save ends. These are real stakes, but in a real-time game a dead save can mean losing weeks or months of real play, and it conflicts with "mistakes are recoverable".
2. **Foreclosure and a fresh start in the same save.** Collateral is seized, the debt is written off, the credit score is damaged, and the inherited plot is protected. The town and the Chronicle carry on.
3. **Both, chosen per save.** Hard mode is only available on Realistic.

## Decision

Option 2, plus a **never-stuck** rule.

### Lenders from day one

| Lender | Available from | Rate (data, indicative) | Terms |
| --- | --- | --- | --- |
| Village moneylender | Homestead | High, about 3–5% a season | Small amounts, no paperwork, no collateral needed |
| Cooperative bank | Homestead for small farm loans, larger loans from Village | Low, about 2–3% a season | Needs collateral and a credit score; better rates with good books |

### Credit rules

- **Credit limit.** Each lender offers up to a limit set by:
  - collateral value (land, buildings, equipment)
  - the credit score
  - income over recent seasons

  The player can't borrow past the limit.
- **Repayments** fall due at season close, in arrears (ADR 0008).
- **Missed payments** add penalty interest and lower the credit score.
- **Default.** A payment that stays overdue past a grace period (data) is a default.

### Personal bankruptcy (foreclosure)

When the founder defaults and can't cure it by selling assets or restructuring:

1. **Seized:** the lenders take collateral. That means bought land, buildings, equipment, livestock and the founder's businesses, in the order the loan agreements set out.
2. **Protected:** the **inherited 2-acre plot and hut can never be seized**, in the same way real-world law protects a family's home farm.
3. **Written off:** any debt left after the seizure is written off.
4. **Credit damage:** the credit score drops sharply and recovers slowly. Loans stay expensive for several game years.
5. **Carries on:**
   - the town, its citizens, the treasury and the Chronicle
   - the era stays at whatever level the town has reached
   - the founder's personal holdings fall back to Homestead level
6. **Chronicle:** the event is recorded as "the year the founder lost everything".

The save never ends. A new game is always the player's choice, never forced.

### Never stuck

- The founder can always **work as a day labourer** for neighbouring farms. This earns a small daily income at the unskilled market wage, as a real-time job like any other.
- If the founder can't pay living costs upfront, the household drops to **subsistence level**: the cost is covered by day-labour earnings, and no debt builds up for living costs.
- The balancer gets a new health check. A bot with no cash and no credit, holding only the inherited plot, gets back to a profitable season of farming within a set number of seasons (data).

## Consequences

- Credit moves forward from Village to Homestead. Village unlocks larger cooperative-bank loans.
- The day-labour job gives the founder a labour capacity. That feeds into the open question of who supplies farm labour before the first hire (GDD review gap #9).
- Business failure (overdraft, then restructure or close) stays as the GDD describes, with the business's own books. Personal bankruptcy happens only when the founder's personal debts default.
- **New state:**
  - `Loan.lenderType`
  - `Founder.creditScore`
  - a protected flag on the inherited parcels
  - a `DayLabour` job type
- **New definition:** `LenderDef` (rate band, limit formula inputs, grace period, penalty rate).
