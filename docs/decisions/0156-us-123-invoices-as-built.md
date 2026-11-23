# 0156. US-123 invoices as built: terms between your businesses, the village shops' collection on terms, an Orders tab, a why-cash-moved bridge

- Status: Accepted (the owner approved Preview 16 and the four gaps, 2026-11-21; the story is for the owner's review)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related ADRs: 0074, 0091, 0101, 0151, 0153, 0155

## Context

US-123 (spec 028 §3.1, Sprint 25): from the Township a sale between two of your businesses, or to the village grocer and tea stall, becomes an invoice on a term the seller sets; the buyer pays at term end from cash, then its overdraft; unpaid, it ages, and an overdue invoice stops the seller's deliveries. Four gaps were closed with the owner before code (ADR 0155).

## Decisions (as built)

1. **Gap 1, the buyer's limit.** A purchase still needs the buyer's cash plus overdraft at the moment of buying (`CanSpend`), so every quantity is what it was; only the payment moves to term end. A buyer's payables are not counted against the limit.
2. **Gap 2, corrected while building.** The gap said the grocer and tea stall "never buy from you today". They do: the village shops' morning collection (ADR 0091 §2: what the village buys of your pav, paneer and curd, at the trader's price, before the trader) is a cash sale the code already made. Making them new buyers of 20 kg a day each would have added demand from nothing (pav's trader room is 0). So the collection itself is the invoice: each good goes to the shops that stock it (`invoices.json` `buyers`: the grocer pav, paneer and curd; the tea stall pav), split evenly, each on the term set for that shop. Quantities, prices and order are unchanged, so the bakery's and dairy's targets (ADR 0079, 0080) do not move. Spec 028 §3.1 and its approved text are amended; the 20 kg and the score share are dropped.
3. **Gaps 3 and 4.** Neighbours' milk to your dairy and a shop's purchase from your store stay cash (spec corrected).
4. **Accounting.** The seller books Dr *Owed by buyers* (1300, the account a business's books already had) and Cr Sales; the buyer Dr Stock and Cr *Trade payable* (new, 2350, schema 23). Paying: buyer Dr Trade payable, Cr Cash (drawing its overdraft if need be); seller Dr Cash, Cr Owed by buyers, then its overdraft is swept. A business's equity counts both, so between your businesses they cancel in net worth (tested: the same worth on terms or on cash). The founder's own ledger is untouched.
5. **One invoice a day** for each seller, buyer and good: the day's sales add to it; it is due a term after the first sale of the day. Late is any time past due up to `lateTerms` (1) terms; overdue after. No fee, no write-off.
6. **The stop rule.** While an invoice of a pair is overdue, `ToYourBusinesses` and `FromYours` skip the buyer, so the seller delivers nothing to it; paying it (the hourly pass, at the next hour) starts it again. `InvoiceOverdue` and `SupplyStopped` are emitted once, when it turns overdue.
7. **A business closing** collects what it is owed at once (a village shop pays; a business of yours pays if it can; what it cannot is forgiven between your two businesses, a loss on one side and a gain on the other, net worth unmoved) and pays what it owes from its proceeds (a guaranteed loan covers a shortfall, as for the overdraft). Tested; the ledgers reconcile.
8. **Save schema 23** (`TownshipState.GrowthSchema`): `InvoicesState` (open invoices and the terms set) hashed only once a sale was made on terms or a term was set; account 2350 since schema 23. Fixtures `schema23.ftc` (the bot town) and `schema23-growth.ftc` (a Township with a mill and a bakery trading on terms, the village shops invoiced, a season term set); `MigrationTests.Schema23_Growth` and `Schema22_Loads_Without_Invoices`. The growth fixture is regenerated as each Sprint 25 story adds to it (`FTC_UPDATE_SCHEMA23=1`) and frozen when the sprint merges. The goldens did not move.
9. **Screens (Preview 16, approved).** An *Orders* tab on a business sheet from the Township (after *Today*, after *Customers* for a shop): who buys on what term (a button that goes Cash, Half-season, Season), what is owed to it and what it owes, each invoice with its age and a *Why?*; an overdue invoice puts a warning first with a *Put in* button; an alert on the bell when a supplier has stopped. The Books tab gains *Owed to it by customers* and *Owed by it to suppliers* lines and, for a business that has traded on terms, a **Why cash moved** section (the profit, wear and tear, customers who have not paid, suppliers not paid, the change in stock, tax not paid, cash from running it, and what else moved), each with a *Why?* in words; the rows add up exactly to the cash that moved.
10. **Not built (cut as agreed or later):** `GateTests.InvoiceLoops` runs in the sprint-end exploit checks; the Season page's cash-flow reason for the business statements is the Books section above.

## Verified

Editor, by scripted touch on a Township town with a mill and a bakery (saves backed up and restored, no diff): the Orders tab, the terms button cycling, an invoice's age, the *Why?* cards, a bakery run dry (overdue after 71 hours, the mill stopped, the alert on the bell opening the tab), *Put in* and the next hour's payment, the Books bridge; captures `docs/art/s25-123-*.png`. Render statistics (US-123): all 1,100 draw calls, village 514, farm 365 against 1,062, 547 and 393 in Sprint 24 (no growth over 10%). Core 804 and View 385 tests pass.

## Consequences

- Nothing changes before the Township; at the Township the village shops' pav and paneer arrive a term late, and a bakery that cannot pay its mill is stopped: the lesson that profit is not cash.
- Spec 028 §3.1 changed (the village shops' wholesale); the owner approved the gap on a statement that was wrong, so this is flagged in the story's report.
- Alerts: a stopped supplier appears on the bell; the away card has no line for it (the alert stays while it holds).
