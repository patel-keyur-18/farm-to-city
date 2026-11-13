# 0108. The town treasury: its own books, grants in two parts, a budget that is a priority list, services that decay by condition, and a wall

- Status: Accepted (owner, 2026-11-13: ten gaps decided one at a time while spec 021 was written; the spec itself is in review)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: the GDD's *Governance and public finance* (the budget cycle and the treasury's first day, made exact); ADR 0107 §6 (the treasury's first day: the grants' two parts, the order, the condition)
- Related ADRs: 0077, 0095 §5, 0101 §12, 0107 §3, §6, §13
- GDD sections: Governance and public finance

## Context

ADR 0107 §6 set the treasury's first day in outline (grants first, takeover of the school, clinic and road, never below zero, a wall between the two wallets) and left amounts and mechanics to spec 021. Writing the spec raised ten gaps, each decided with the owner.

## Decisions

1. **Its books (G1).** The treasury has its own `Ledger`, the class a business uses, on the shared chart with new accounts (income 4600–4670, one expense account a department 5910–5960). Its balance is a *Fund balance*, not net worth. Three statements as spec 019's, under public names, 8 seasons, every row itemised, no *Other* row.
2. **When it opens (G2).** Once, when the era reaches Township; the developer panel's *Next era* opens it before spec 023 builds the real gate. It opens with the start-up grant only (1,00,000) and a toast. The school's upkeep and the pitch rents switch to it at the first close after the opening.
3. **The grants (G3).** 100 untied and 50 tied to the clinic being staffed, a resident a season (about 150, the old village's cohort counted once it has joined); never following your wealth.
4. **Income and order (G4).** Pitch rents only when really paid; taxes through an intake spec 022 uses; permit fees empty until spec 024; gifts; income comes before spending at the close.
5. **Departments (G5).** Council's office, Health, Education, Roads, Safety, Utilities, in that order, wages before upkeep; clerk 30,000 and doctor's allowance 30,000 a season; capital works through a confirm card (the command ships now, the works come with specs 023–025); buildings you give belong to the district.
6. **Condition (G6).** Each department has a condition that moves halfway toward its funding each close; named effects (the office's tax collection, the doctor leaving after two closes under 50%, a school year counting only at 50%, the road's gain); never below zero; a warning a day ahead with three levers; no collapse.
7. **The budget (G7).** A budget is a priority list: pass 1 pays min(needs, budget) in order, pass 2 the rest while cash lasts; the proposal is in force at once (ignoring the card is fine); set at the opening and each Spring close, one revision at the Monsoon close; a new line is added at its needs; 0–120% of needs.
8. **Gifts and the wall (G8).** A gift is one way, outside farm profit; nothing ever leaves the treasury to you or your businesses, and it never buys from your businesses (bricks from the trader); a test sweeps every bot's treasury entries. Works that raise your acre's value are **shown on the card, not blocked** (Phase 4's conflict-of-interest rule makes it a scandal; until then it is visible).
9. **The screens (G9).** *Town treasury* in the Menu with the permanent line "Town money: not yours", no HUD pill. **The owner's rule: before the UI is coded, an artifact that looks exactly like the in-game sheet is shown.**
10. **Advisor, Codex, log (G10).** The town planner with four notes, three Codex cards, play-log lines.

## Consequences

- Spec 021 carries the rules, data, state (save schema 19, after US-240's 18), commands, events, acceptance criteria and balancing hooks. Spec 022 builds on its intake; spec 023 adds the clinic's and road's lines and the cohort; specs 024–025 add works and fees.
- The GDD's *Governance and public finance* is updated in this commit. CLAUDE.md's table gains this ADR.
- The treasury's numbers are starting values in `content/treasury.json`, measured with the bots in Sprint 21; a miss comes to the owner with its numbers.
