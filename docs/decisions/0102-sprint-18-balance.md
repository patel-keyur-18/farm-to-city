# 0102. Sprint 18 balance: the hub's numbers on the bots, the deposit is no money loop, and passive income covers living by day 20–40

- Status: Proposed (2026-11-11). §1, §2 and §4 are measured facts and a restated target; §3 (passive income) waits for the owner
- Date: 2026-11-11
- Deciders: Keyur Patel
- Amends: spec 019 §9 (the balancing hooks)
- Related: ADR 0063 (rent), 0071 (the home loan), 0078, 0097 (the Village's pace), 0101 (Sprint 18's plan check)

## Context

`sprints.md`'s balance step for Sprint 18 reads spec 019 §9 on the bots: debt to assets at the Village, the cautious saver's emergency fund on day 30, passive income at the Village, and the deposit's no money loop; plus every bot's books (`StatementTests.NothingUnsorted`) and the save's size with eight seasons of entries. The balancer gained `FinanceGate` (`Measure`, `EveryBot`), the `DepositLoopBot` stress bot and savings commands in the zero-time loop check; `FinanceGateTests` asserts them.

## Measured (2026-11-11, 8 seeds, the live content)

| Check | Spec 019 §9 | Measured | |
| --- | --- | --- | --- |
| Debt to assets at the Village, the competent bot | 15–40% (under 60%) | 6–8% (the saving bot 3–4%) | **restated** (§1) |
| The cautious saver's emergency fund on day 30 | 1–3 seasons | 1.0–1.9, median 1.3 (the competent bot 0.5–1.2, amber) | holds |
| Passive income a season after the Village | 5–30% of living | 130–190% (rent 26,900 + a hired mill 5,000–12,000 against a living of about 20,500) | **misses** (§3) |
| The deposit earns less than a house's rent and than the cheapest loan | 1.5% against 10% a season, 2.5% | holds | holds |
| Borrow at the co-op bank to deposit | loses | net worth 94–98.5% of the competent bot's after 12 seasons, every seed, with the most generous bank (`DepositLoopBot`) | holds |
| Every bot's books, 27 bots × 2 seeds, 594 reports, 122,810 cash entries | nothing unsorted | none (`NothingUnsorted_EveryBot`) | holds |
| A Village town's save after 24 seasons (8 seasons of entries and cash rows) | under 200 KB (spec 007 AC-6) | 136 KB | holds |

Milestones on the bots (median day): *First rent* 16–28, *First lakh saved* 16–60, *Passive income covers your living* 20–40, *Twice what you inherited* about 84, *Debt-free* 72–88 (5 of 8 competent runs; the saver 8 of 8).

## Decision

1. **Debt to assets at the Village: the target is restated, the content is not.** The 15–40% was a guess. The bots' wealth is land (about 90% of what they own), loans are a few lakh against twenty, and the hub's bands (comfortable under 30%, risky over 60%) are the player's to find, not the bots'. §9 now reads **2–20% (measured 6–8%); under 60% for every run**. `FinanceGateTests.Hub_OnTheBots` checks the median within 2–20% and the maximum under 60%.
2. **The deposit stays as designed.** 1.5% a season against a house's 10% and the co-op bank's 2.5% at its best: borrowing to deposit loses in every seed even with a bank that lends 10,00,000 coins without collateral. The zero-time loop check now includes deposit and withdraw.
3. **Passive income covers the founder's living at the Village; whether that is right is the owner's call.** Five rented houses pay 26,900 a season (rent 6,000 indexed) and a hired mill 5,000–12,000, against a living of 18,000 indexed (the basic plan): the ratio reaches 100% on day 20–40, so *Passive income covers your living* is an early milestone, not the late-game one spec 019 meant. The cause is the rent, not the milestone: a family house costs 60,000 and lets for 6,000 a season, a gross yield of 10% a season (about 40% a year), where a village house in India yields a few percent a year (ADR 0063, 0071). **Options:** (a) leave the economy and the milestone: they are both true to the game as built, and the milestone marks the first time rent feeds the household; (b) raise the milestone's bar in `finance.json` (`milestones.passivePercent`), which the hub row would then contradict at 100%; (c) lower the rent (ADR 0063), which slows every bot's Village (ADR 0097) and needs a re-measure of the home loan (ADR 0071). **Recommended: (a) now, and (c) as its own decision in the Phase 3 balance** (realism: a rent about a third of today's, with house prices nearer the real cost). Until the owner decides, the test prints passive income and asserts nothing.
4. **The save is within its budget with eight seasons kept.** `retainSeasons` 8 (ADR 0101) adds cash rows and people rows to each report: a Village town's save is 136 KB of the 200 KB target.

## Consequences

- Spec 019 §9's table carries the measured values and this ADR's pointer.
- New measures: `FinanceGate.Measure` (debt at the Village, passive income a season on, the emergency fund on day 30, each milestone's day), `FinanceGate.EveryBot`, `DepositLoopBot`; `FinanceGateTests` (`Deposit_NoMoneyLoop`, `NothingUnsorted_EveryBot`, `SaveSize_VillageTown`, `Hub_OnTheBots`).
- If the owner picks (b) or (c), only content changes (`finance.json`, `population.json`) and §9; no rule changes.
