# 0127. Spec 026: town life, gaps closed

- Status: Accepted (owner, 2026-11-18, G1–G8 closed one per message, each on my recommendation)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0004, 0043, 0057, 0063, 0107, 0108, 0109, 0120, 0121, 0125

## Context

ADR 0107 §13 fixed the scope of festivals, petitions, reputation, crime and inequality. Spec 026 (written at the start of Sprint 23, ADR 0057, for Sprint 24) turns it into rules and numbers.

## Decisions (each a recommendation, with the reason an average player can read)

1. **Festivals (G1).** Four festivals, one a season, the kind fixed by the season (Seed Fair, Mango Mela, Rain Night, Harvest Mela). You choose the venue (the staffed hall or the banyan square), the payer (the treasury, never below zero, or your gift, outside farm profit) and the day (one of the next three). Cost a person (80, 100, 80, 120) times the town's people. One day: happiness +8 in the venue's reach and +3 elsewhere, chai and sweets +30%, vegetables +10%. No undo. *Why:* about four-fifths of a season's untied grant, so it is a choice and never free.
2. **Petitions (G2).** Nine topics (clinic, school, hall, watchman, playground, water, rent too high, no work). Raised at a close when 3 households, or a fifth, share the need; one open a topic; the old village's cohort never raises one. Two answers, *Promise* (1, 2 or 4 closes) or *Decline*; doing the thing ends it *Done* by itself. Approval +4 done, +6 kept, −2 declined (topic quiet 4 closes), −4 ignored, −8 broken. *Why:* a broken promise is worse than silence.
3. **Approval (G3).** Starts at 50, moves by (happiness − 70) ÷ 5 up to ±4 a close plus the petition endings, within 0–100, and in Phase 3 changes migration only (no tax, slot or election; ADR 0004). *Why:* 70 is neutral happiness, so a content town drifts up.
4. **Reputation (G4).** Five scores (livability, business climate, education, health, environment) each from named parts and weights (spec 026 §3.4). A family's wait is 18 ± 6 hours × 100 ÷ pull, with pull = 100 + (approval − 50) ÷ 2 + (the scores' mean − 50) ÷ 2, within 50–150; the grants rise by (mean − 50) ÷ 5 percent above a mean of 50, never lower. *Why:* the parts are what a visitor notices first, and a town left alone is unchanged.
5. **Crime (G5).** crime = 20 + 2 × unemployed% + three-tenths of the inequality score, within 0–100. The Safety part becomes 1 − crime ÷ 100 for a home (the watchman cuts it by up to 50 points in his reach): today's 0.5 at crime 50. A land-value term of ±0.02 a 10 points, at most ±0.10; the crime overlay lights up. A measured pressure, never a theft. *Why:* jobs lower it town-wide and a post only in reach.
6. **Inequality (G6).** The richest tenth's mean income over the poorest tenth's, from your households only, from 10 households, capped at 10. Below the median a household loses 1 happiness point for each whole point above 4 (at most 6); at 6 the poorest leave a season sooner. *Why:* 4 is a comfortable village.
7. **Screens (G7).** A *Town life* tab on the Town hall, *Hold a festival* on the hall's and the square's sheets, a bell alert for a petition, the crime overlay; previewed as artifacts first.
8. **Scale (G8).** Sprint 24 keeps its three stories (11 points) with spec 027 and the hills; one schema bump (22) for all of it; the cut line is spec 026 §11.

## Consequences

- Spec 026 is approved; its tests are named in §8 and written in Sprint 24.
- Spec 024's crime overlay is lit by spec 026 (Sprint 24), not spec 027.
- The balance pass in Sprint 24 sets `crimeBase` so a typical town sits near 50, and re-measures the Township's pace, land payback and the archetypes.
