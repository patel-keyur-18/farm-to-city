# 0078. Spec 019: your statements, the finance hub and wealth milestones

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Amends: GDD Assets and wealth (the savings deposit in Phase 2, the personal finance screen inside Money and loans, wealth milestones), Learning layer (the year-end review and the workforce report in Phase 2's form); spec 005 (the season report as three statements)
- Related: ADR 0009 (credit), ADR 0044 (net worth kept), ADR 0068 (business books), ADR 0076 (schemas by sprint), ADR 0077 (advisors)

## Context

Spec 019 was written in Sprint 15 (ADR 0057, 0076) and its eight gaps closed with the owner one per message. The founder already has a season report (profit and loss, one cash row, net worth) and Money and loans (cash, net worth, loans, borrowing); the mill has full Books.

## Decision

1. **Your books (G1):** the season report becomes three statements laid out as the mill's Books: profit and loss as now, cash flow in three groups (your work, what you bought or sold, borrowing), a balance sheet (what you own, what you owe, net worth); the last 8 seasons kept.
2. **Against last year (G2):** each line beside the same season a year earlier, with a *Why?* naming the biggest change; a written plan stays with businesses.
3. **The hub (G3):** Money and loans, redesigned in the Store and People layout (a glance row; picture buttons for Borrow, Savings deposit and Your books; sections for your wealth, your loans and milestones). Your wealth: net worth's trend (8 seasons), what it's made of (a pie), debt to assets (comfortable under 30%, stretched to 60%, risky above), the emergency fund (seasons of unavoidable costs), passive income against living.
4. **A savings deposit (G4):** at the co-op bank, any amount any time, 1.5% a season added at each close (it compounds), counted in the emergency fund and passive income, never collateral; inflation shown beside it.
5. **Passive income (G5):** rent, deposit interest and the profit of businesses run by a hired worker; the farm isn't counted.
6. **Milestones (G6):** first lakh saved (cash + deposit − debt), net worth twice the inheritance, first rent, debt-free after a loan, passive income covering living; a toast and a list, no rewards.
7. **Your year (G7):** after each Winter close, the year against last year, best and worst season, the biggest cost, the accountant's line; best decision and costliest mistake with the Chronicle in Phase 3.
8. **Your people on the report (G8):** wages against the market, who left and why, output, reputation's change; the full workforce report in Phase 3.

## Consequences

- Built in Sprint 18 with save schema 14 (ADR 0076): statements, the net worth trend, account 1020 Bank deposit, milestones.
- F14.3 and F14.4 are re-pointed: US-137 statements 5 → 8 (the last-year column, *Your year*, *Your people*), US-135 the hub 5 → 8 (the Money and loans redesign, the deposit). Sprint 18 grows from 12 to 18 points beside the Phase 2 playtest; its length is decided at Sprint 18's planning.
- The balancer checks no money loop through the deposit (borrowing costs more than it pays) and the hub's ratios for each bot (spec 019 §9).
