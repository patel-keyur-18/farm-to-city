# 0015. Onboarding: a first session, then a guided first week

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (real-time clock), ADR 0009 (credit from day one), ADR 0010 (founder routine)
- GDD sections: UI/UX and screens (Onboarding)

## Context

The GDD planned a 20-minute prologue that guided the first sale, the first hire and the first loan. Under the real-time clock, hiring arrives on day 1–2 and the game is played in short sessions across days. Pushing a loan during the tutorial would also teach that debt is the default.

## Decision

1. **First session, about 15–20 minutes.** The grandparent's letter, the inherited 2 acres and the hut. The player:
   - sows a quick crop (about 5 minutes)
   - sets the household plan (diet and working hours)
   - harvests and makes the first sale to the travelling trader
   - sows a long crop (hours) and is invited to turn on notifications
   - closes the app with a clear reason to return
2. **A guided first week.** Each "first" is guided once, when it naturally happens, by a new page of the letter plus an advisor nudge:

   | Trigger | What is guided |
   | --- | --- |
   | First return | The *While you were away* report and *ready to harvest* |
   | First season close | The first P&L and prepaid costs |
   | Shortage of land or cash | The first loan offer, comparing the moneylender with the cooperative bank. Only when needed, never pushed |
   | Fields exceeding the founder's labour | The first hire, contracts and standing orders |
   | Village unlock | The first school |

3. **No pop-up walls.** Letter chapters can be reopened at any time.

## Consequences

- The onboarding spec (last in the vertical slice) defines each trigger as a condition on game state, and adds tests for "guided once".
- The Phase 1 gate (testers come back on day 2 and day 3) is measured against this flow.
