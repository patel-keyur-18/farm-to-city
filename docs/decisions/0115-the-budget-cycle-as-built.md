# 0115. The budget cycle as built: a proposal that follows prices, a window until accepted, five variance sentences, a day's warning and the town planner

- Status: Proposed (for the owner's review of US-139, 2026-11-14)
- Date: 2026-11-14
- Deciders: Keyur Patel
- Amends: spec 021 §3.6 (the proposal's formula, what ends a window, the window card), §3.11 (what the warning counts before taxes exist), §3.12 (where the planner speaks)
- Related ADRs: 0108, 0114, 0095 §7 and §8 (Codex and advisors), 0107 §13

## Context

Building US-139 (spec 021 §3.6) raised choices the spec leaves open. The owner asked (2026-11-14) that the budget not become a burden for an average player without taking realism out of it. Each choice below favours a town left alone working, and keeps what a player can decide real.

## Decisions

1. **The proposal is last year's actuals at today's prices.** For a department: its needs now × (what it was paid ÷ what it needed over the last four closes); its needs now when there is no history. A department paid in full last year is proposed at its needs now, so a flat budget never falls behind prices by itself, and a department that went short keeps the share it got. It is in force at once; ignoring it leaves it.
2. **A window lasts until it is accepted or the next close.** `SetBudget` changes a department and leaves the window open (it marks it seen); `AcceptBudget` closes it. After a close the window is gone, and the set window returns at each Spring close and the revision once at Monsoon, only in a year that has had a set window. The range is 0 to 120% of the department's needs now, rounded to the nearest coin.
3. **A new window announces itself once on a card, and after that on the bell.** *(Proposed, to confirm: this changes spec 021 §3.6's "a card after the season page".)* The first window, when the treasury opens, shows a card with the proposal, **Accept** and **Change**. Every later window is a *Decide* row on the bell and a dot on the Budget tab, so a player who never opens the sheet is never stopped by it.
4. **Five sentences, in this order.** A line that joined this close: *a new line*. Unpaid money: *cash ran out*. Needs over the budget: *your budget was below its needs* when the budget was set below the department's needs then (`BudgetNeeds`), else *prices rose*. Budget over what was paid: *unspent*. Otherwise none. Each has its *Why?*.
5. **The warning counts the grants only, until taxes.** At 00:00 of a season's last day, `Forecast` runs the close's two passes on the cash and the grants certain by then and raises `TreasuryShortAhead` with the short amount and the first department unpaid. A change in the shortfall raises it again; a shortfall that closes resets it. Spec 022 adds the tax bills to the income it counts. The notification `TreasuryShort` names no money and goes through the same quiet-hours rule as the founder's `CloseShort`.
6. **The town planner is a fourth advisor with four notes of her own.** Meena Rao (a new portrait, `portrait_advisor_planner`) speaks about the town's money only, from the treasury's last closed season, one line at a close, a topic once in 24 hours. Her line shows on the treasury sheet, never as the founder's one season-report line. The notes: a department over its budget two closes running; a service under 70%; the tied grant at risk (Health has a line and is unpaid or under 50%: it cannot hold before spec 023's clinic); the balance under one season's spending with no tax or gift in the last close.
7. **Three Codex cards and the play log.** *Public money* (the opening), *A budget is a priority list* (a department paid in pass 2 or left unpaid), *Tied grants* (the tied part withheld). The play log records the opening, each window, each unpaid department and each gift, with amounts.
8. **Schema 19 grows once more, before it ships:** the state holds `BudgetNeeds`. No version bump (schema 19 has not been released).

## Consequences

- `SetBudget` and `AcceptBudget` need no confirm card (they plan, they spend nothing).
- Spec 023 inherits a clinic line whose planner note (4's third) can fire; spec 022 adds tax income to the warning and to the planner's balance note.
- Spec 021 §3.12 says the planner speaks on "the season page's treasury section"; there is none, so she speaks on the treasury sheet only.
