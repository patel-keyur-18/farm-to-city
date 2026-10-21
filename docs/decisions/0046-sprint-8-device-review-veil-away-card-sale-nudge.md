# 0046. Veils over the whole screen, the away card after 8 hours, the sale nudge only with something to sell (Sprint 8 device review)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0043 §8 (when the *While you were away* card shows); spec 008 §3.9, §8 (AC-8); spec 009 §10 (implementation notes)
- Related: ADR 0045 (the nudge pill), ADR 0041 (notifications from a simulated absence), ADR 0002 (the 72-hour away cap)
- GDD sections: Time and world simulation (Time while the app is closed), UI/UX and screens

## Context

The owner's device run of the Sprint 8 build (iPhone 13, 2026-10-21) passed every check. The first session (checks 1 to 12) took 10 minutes. Three points came back:

1. A letter page dimmed only a rectangle of the screen, not all of it. Every veil (cards, letter pages, *Why?*, the away card, the developer panel) sat inside the safe-area root, so the strips beside the notch and above the home indicator stayed bright.
2. After an hour away, the *While you were away* card showed, followed by "Welcome back". The card showed whenever anything happened (ADR 0043 §8), and something always happens: the house eats from the store every day. The owner: nobody plays continuously, so a card on every return makes no sense. The owner first asked for the card only at the 72-hour cap.
3. After **Away cap (72 h)**, the nudge read "Open the Store and sell to the trader" and the store pill kept bouncing with nothing to sell. In a town that never sold, the first-session step `firstSale` was still current, and the house had eaten the inherited wheat and lentils.

## Options for point 2

- **Only at the 72-hour cap** (the owner's first ask). Most returns, even after a day or two, would show nothing. A season can close, crops can spoil and pests can come with no summary, and most players would rarely see the card or "Welcome back".
- **After 8 hours away, or at the cap** (recommended, chosen). A night or a working day away brings the card; shorter absences show nothing. The HUD's Collect all count, the alerts bell and the toasts already cover short absences.
- **After 4 hours.** The same idea with a shorter threshold.

## Decisions

1. **Every veil covers the whole screen.** A veil reaches past the safe area on every side and pads back by the same amount, so what it holds stays centred in the safe area (`UiKit.Veil`). It applies to cards, letter pages, *Why?*, the away card and the developer panel. The developer panel also opens above the nudge pill.
2. **The *While you were away* card shows only after at least 8 hours away, or at the cap**, and only when something happened. The threshold is data: `awayCardMinHours` in `content/ui.json` (0 to 72). "Welcome back" still follows the first card, so it now comes after the first return of 8 hours or more. Notifications and the away advance itself are unchanged.
3. **The sale nudge needs something to sell.** The step's nudge, its arrow and the store pill's bounce show only while the store holds produce that isn't on offer and the trader still buys that good today. Otherwise the step waits quietly, and it never blocks anything (spec 009 AC-3). The developer panel's **Away +4 h** idea became **Away +1 hour**, because `DebugAdvance` accepts only an hour, a day, a season or the cap (spec 001).

## Consequences

- Spec 008 §3.9's "Shown" row and AC-8 add the threshold. `AwayViewTests.Summary` checks that the same news after 7 hours brings no card and after 8 hours does.
- Spec 009 §10 records the sale nudge rule. `FirstSessionTests.Nudges` checks that the sale nudge goes when everything is on offer.
- The GDD's "Time while the app is closed" paragraph says the report shows after 8 hours away.
- The device checklist `s08-onboarding-hamlet.md` gets checks 33 to 36 to re-check these on the phone.
- First session length: 10 minutes against a 15–20 minute target with ±5 tolerance (spec 009 §9), at the edge of the band. Nothing is tuned now; Sprint 9's playtest measures it with new players (US-096).
