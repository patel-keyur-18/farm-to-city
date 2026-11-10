# 0100. Sprint 17 balance: literacy at the Village is about 45% (the target counted adults only), the rest holds

- Status: Accepted (owner, 2026-11-10, with the Sprint 17 device check)
- Date: 2026-11-10
- Deciders: Keyur Patel
- Amends: spec 018 §9 (literacy when the Village unlocks), spec 017's good-chai *Why?*
- Related: ADR 0093 (shop targets), 0095 §6 (happiness with the school), 0096 (rivals as built: good chai, the vegetable stall), 0097 (the Village's pace), 0098 (the Codex)

## Context

`sprints.md`'s balance step checks, with the bots: the Village's pace (ADR 0097), the shops with newcomers and price cuts, literacy at the Village (50–70%), Codex cards by the Village (12–18), town happiness about 72. ADR 0096 left two notes: good chai doesn't pay hired, and the vegetable stall's hired profit is thin.

## Measured (2026-11-10)

| Check | Target | Measured | |
| --- | --- | --- | --- |
| The Village, homes first | 28–40 days with loans, 50–64 saving | median 33 and 56; all six orders, both bots, every run (`GateTests.Village`) | holds |
| Tea stall, hired, usual price | 5,000–12,000 a season | 7,335; at 1.1, −294 | holds |
| Sweet shop, hired | 8,000–15,000 | 10,944; at 1.1, −5,972 | holds |
| Vegetable stall, hired | about 3,700 (ADR 0093) | 3,723 | holds |
| Newcomers when you open nothing | none | none | holds |
| Codex cards by the Village | 12–18 | 13–16, median 15 (12 runs, loans and saving; `CodexTests.ByTheVillage`) | holds |
| Town happiness, a season after the Village (the school open) | about 72 | 69–76, medians 72–74 by order | holds |
| Literacy when the Village unlocks | 50–70% | medians 38–63% by order and bot (about 48% homes first; school-first orders higher, as pupils finish); single towns 23–90% | **misses** |

## Decision

1. **Literacy's target is restated, not the content.** Spec 014 gives 60% of adults primary schooling (`primaryEducationShare`), and the village's children can't read because it has never had a school: they learn once the founder builds it (ADR 0011). Literacy counts everyone aged 7+, and children are about a fifth of them, so the town starts at about 45–48%. The 50–70% band counted adults only. A village without a school sitting below India's rural average (about 68% in 2011) is the story the school tells, so the content stays. §9 now reads **about 45% when the Village unlocks (single towns 25–70%: a few families decide it), rising as pupils finish and with adult classes**. The Township's 40% (Phase 3) then needs an adult class in about a third of towns; Masterji's note (ADR 0099) points to it. `GateTests.Village` checks each bot's median within 35–65% and happiness a season on within 70–76.
2. **Good chai hired is accepted as the lesson, and its *Why?* corrected.** At the usual price full-cream milk costs more than its extra customers bring (median 3,167, ADR 0096); above the usual price a newcomer may come. The *Why?* said "Charge a little more for it", which since ADR 0095 risks a newcomer; it now says so (an even chance each season) and that good chai wins customers and stars while standard makes the money.
3. **The vegetable stall's thin hired profit stays.** It is ADR 0093's measured target (about 3,700 hired, about 17,600 run by you), true to a sabzi-wala's thin margin on a 15% markup.

## Consequences

- Spec 018 §9's literacy row and this sprint's balance checks are recorded; the Phase 2 gate is unchanged.
- New measures: `Gate.Village` records happiness a season after the unlock; `CodexTests.ByTheVillage` plays the Village bot with the Codex watching (Sim.View.Tests now references the balancer).
