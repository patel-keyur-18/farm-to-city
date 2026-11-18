# 0120. The Township as built: 15 households, a cheaper town hall, the safety rule

- Status: Accepted (owner, 2026-11-18)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0107, 0108, 0109, 0116

## Context

Sprint 22 built spec 023 (the clinic, the link road, the gate and unlock, the old village, the civic buildings). US-100's `TownshipBot` measured it against ADR 0107 §5 and spec 023 §3.8 and two things missed.

- **Pace.** The plan was day 63–77 for the competent bot and about 100–110 for the saver. Measured with 20 households as the last goal: day 136 and 160. Capital is the limit: a house costs 60,000 and an acre about 120,000, so 20 households cost about 1.8 million coins.
- **Solvency.** A Township left alone at default taxes was meant to break even. It has about 215 residents (not 250), two shop pitches (not three) and took in about 54,000 a season against 67,000 out; its treasury fell from about 150,000 to 4,000 in two years and services fell to 6–50%.

## Decisions

1. **The gate asks for 15 households, not 20** (the doctor's counts). With the bot also founding two shops on pitches beside the mill, the competent bot unlocks at a median of **day 101** (90–150) and the saver at **134** (125–148). The restated targets are day 85–125 and 115–160. The first targets were out of reach and the owner asked that the Township not be a burden to an average player.
2. **Three costs of the treasury are lowered**, in spec 023 §3.8's lever order and one more: the doctor's allowance 30,000 → 20,000 (his household still has 30,000; the rest is his patients'), the clinic's upkeep 3,000 → 1,500, the clerk's wage 30,000 → 20,000 (a part-time village clerk). Out is about 47,000 a season; the median Township's treasury ends its second year level or higher (−5,000 to +54,000), every service stays at 100%, the doctor stays, the old village stays. 200 seeds hands-off pass (`FTC_SEEDS=200`). Grants and default taxes are not touched.
3. **Safety is a fourth part of security only from the day a watchman's post stands** (0.5 with nobody in it, 1.0 with a watchman in reach). Spec 023 §3.7's literal rule would take every fully secure home from 1.0 to 0.875 (about −3.75 happiness) at the unlock and contradicted its own "doesn't lower anyone's happiness". A town that builds nothing loses nothing; the hall and playground only add.
4. **The watchman's and caretaker's wages are the old village's income** (into its savings), and appointments are `PostCivicJob` and `DismissStaff` (`PostJob` is the hiring board's). A post can't be cancelled while a villager is on his way.
5. **Tax checks on the bot's town:** with every tax at 0 the treasury runs dry in about 3.5 years (services to under 30%, happiness about 2 lower: spec 022's "at least 5" was too much, services weigh little against food, work and rent); at the cap the treasury is about 50% richer, nobody is driven out and the founder, who pays the tax, carries it.

## Consequences

- Spec 023 §3.4, §3.8, §9 and AC-15 are amended; `GateTests.Township`, `TownshipSolvent`, `TaxGateTests.EveryTaxAtZero` and `EveryTaxAtTheCap`, `TownshipExploits` enforce them.
- The Township is still 14 weeks away for the competent bot against ADR 0107's 9–11; Phase 4's plan builds on the longer road.
- Builders' houses (spec 024) may count toward the 15 households: left to that spec's review.
