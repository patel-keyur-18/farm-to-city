# 0164. Sprint 25 balance and the independent review: an investor never paid less than he put in at a buy-back, a manager who works the business himself, no sale for a plan, no drawing an investor's cash

- Status: Accepted (the owner closed gaps 25 and 26 and the two review gaps one per message, 2026-11-23)
- Date: 2026-11-23
- Deciders: Keyur Patel
- Related ADRs: 0016, 0027, 0112, 0120, 0124, 0142, 0153, 0155, 0157, 0158, 0159, 0160, 0161, 0162, 0163

## Context

Sprint 25's last step is the balance pass (ADR 0016: archetypes within 20%, the exploit bots, the Township's pace and solvency, every new business's targets), then the independent review (owner rule, 2026-11-18). Eight stories were built and approved; this ADR records what the pass measured, the four rules it changed, and what the review found and fixed.

## Measured

- **Archetypes (ADR 0016, 0112):** best against worst net worth 1.121 and happiness 1.081 at year 8 (the rule is 1.20). The Township's pace holds: the competent bot reaches it about day 101 and the saver about day 135 (ADR 0120). A town left alone stays solvent; 200 seeds of a hands-off Township close the second year at +22,484 (worst), +54,905 (median) and +127,270 (best), services at 0.85 or better, the doctor in post in 200 of 200.
- **Exploit bots** (`GateTests.Growth`, two towns that differ only by the trick, compared by net worth): `InvoiceLoops`, `StackBenefits`, `NightNoDemand`, `Growth` (zero-time loops) and `SellBuyBack` found no loop; `InvestorDilution` found one (gap 25) and the review found two more (gaps 27 and 28 below). `KhataWorst` and `MarketingSpiral` wait for US-260 (Sprint 26).
- **Managed against hired** (`GateTests.Managed`, 12 seeds, 60 seasons, a manager of each style): the first draft, a flat 4,000 on top of a worker's wage with the worker still there, measured 8 to 10% *below* hired. After gap 26 a managed business is worth 1.08 to 1.13 times a hired one and stays under one you run yourself.
- **The new businesses' targets** (`GateTests.Targets`, a Township bot's town at 30 seasons, 3 seeds, the first two seasons left out): tuition centre hired 5,000 to 9,000 and run by you 29,000 to 33,000; restaurant hired 13,000 to 15,000 and run by you 34,000 to 35,000. Both run above spec §9's first guesses (hired 3,000 to 8,000, run by you 18,000 to 28,000), so the targets are restated in spec 028 (ADR 0027: values follow the measurement without re-approval): tuition run by you 25,000 to 35,000; restaurant hired 10,000 to 17,000 and run by you 30,000 to 36,000, both under the bakery's (45,000 to 60,000 run by you). The tempo promises no payback: ADR 0162 measured that the let-out nets 300 a leg and never pays the standing cost on its own, and spec §9 now says so.

## Decisions

1. **Gap 25, the buy-back is never less than the cash he put in (owner, "Floor at his cash").** The `InvestorDilution` bot ran the profit down and bought the investor out at a third of what he put in. `OwnerRules.BuyBackPrice` is the larger of his share of the worth now and his stake's `Put`.
2. **Gap 26, a manager works the business himself (owner: "if a manager is not generating profit there is no use of a manager").** A managed business has no other worker: `HireManager` is refused (`AlreadyStaffed`) while a worker is there, and a worker cannot be let go while a manager is (`HasManager`). His base wage is 60% of what a worker of the trade earns (`managers.json` `wageOfRole`) plus a tenth of the profit when positive. The three styles set price, quality and stock only, and the manager's own style is the applicant's. ADR 0157 is amended.
3. **Gap 27, a business with no closed season is worth what it holds (review finding 1, owner: "Book only").** The worth used the plan's profit times four when no season had closed. A plan for a business you run yourself carries no wage, so a bakery built for 145,000 listed at 681,000 (+432,000 in 61 hours, repeatable). With no season closed there is no profit to count: the worth is the book, and a buyer pays for proven earnings. `GateTests.SellNew` founds at the least capital, run by you and hired, and checks the sale makes no gain.
4. **Gap 28, your drawings stop at the cash beyond the investors' money (review finding 2, owner: "make it more realistic: an investor in a business that loses also loses some of his money").** Taking an investor's cash, drawing it all out and closing kept about 248,000 of his money. The settlement at a close or sale stays as it was (his share of what is left, so a business that loses costs him part of his stake, as a real investor's), and `TakeCash` is refused past the business's cash less the investors' money (`BusinessBooks.Drawable`); the cash sheet says how much is yours. `GateTests.InvestorDrawAndClose` checks that taking, drawing and closing never beats simply selling.

## The review (Opus 5.5, high effort)

It read the sprint against its plan in `sprints.md`, every `WorkerAt`, `StaffAt`, `HiredWorker` and `ManagerAt` site after the manager rework, the ledger flows of invoices, buy-back and settlement, the schema 23 reader and writer, the strings, and ran both test projects in a copy of the repository. It found nine things; all are fixed or answered.

| # | Finding | Result |
| --- | --- | --- |
| 1 | A new business sells for its plan's profit (high) | Fixed: gap 27 |
| 2 | Take an investor's cash, draw it, close (high) | Fixed: gap 28 |
| 3 | `ShortDay` was kept every day for every mill, bakery, dairy and sweet shop from the Hamlet, so the schema 23 block was hashed for towns that use no feature | Fixed: nothing is kept before the Township (`NightRules.EndOfDay`) |
| 4 | The manager offer sheet could default to a business with a worker, so every offer was greyed | Fixed: the default is the first business he can take; a staffed business still lists, with its offers greyed and the reason in words (the owner approved that design in the Editor tour) |
| 5 | A manager worked long days and was paid overtime but never tired | Fixed: `WorkRules.LongDays` no longer skips managers |
| 6 | The balance items were not evidenced | Fixed: the measurements above; the targets restated; the tempo's payback corrected |
| 7 | Stale text in spec 028 (AC-5, AC-6, the content table, the decisions table) and ADR 0157 | Fixed |
| 8 | A citizen manager who left through the worker path (retired, or his business closing) left without `ManagerLeft`, and `KeepStaffed` named worker 0 | Fixed: the event and the id follow the manager |
| 9 | A manager's counter-offer on the board and the *Why?* of his pay on the board showed a figure from the hand's wage | Fixed: the board shows the share, the offer sheet shows the wage for the business |

## Consequences

- Both test projects pass after the fixes (View 418, Core 892 without Balance and Perf, the Balance suite and Perf alone).
- A manager now costs a worker's wage less 40%, which is why a business can have one without a net loss; the three styles are measured, not enforced.
- A player can no longer find a business worth more than it holds before it has traded, or keep an investor's money by drawing it out.
- Open, for Sprint 26: `KhataWorst` and `MarketingSpiral` need US-260; the weather presenter's `NullReferenceException` after a very long time jump (Editor only, before this sprint) is not a sim fault.
