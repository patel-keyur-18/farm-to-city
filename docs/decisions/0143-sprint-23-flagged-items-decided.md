# 0143. The four flagged items of Sprint 23's review: one brick limit, a smoke health cap, the kiln from the Village, a guard per system, zero tax restated

- Status: Accepted (the owner took the recommendations, 2026-11-20)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Related ADRs: 0129 (the kiln), 0130 (utilities), 0120 (the tax checks), 0139 (a pass that throws), 0142 (the review that flagged these)

## Context

The independent review of Sprint 23 (ADR 0142) flagged four things that were decisions, not fixes. The owner took the recommendation on each.

## Decisions

1. **One brick limit for the town; smoke health capped.** The trader's 5,000 bricks a day is for the whole town, shared by your kilns in id order (a second kiln fires for your own builds at cost, but does not sell him more). Smoke's health, like its land value, has a cap: `township.json` `smoke.healthCap` 0.08 (the generator's 5% and one kiln's 3%); kilns stack, the total does not. The kiln's plan (one kiln) is unchanged. `KilnTests.Two_Kilns_Share_The_Traders_Day`, `KilnTests.Smoke_Health_Is_Capped`.
2. **The kiln is a Village business.** The content (`era: "village"`) and F12.7's approved scope win; spec 025 §3.3 and the GDD's catalogue are corrected (the GDD moves the kiln to the Village row). Nothing in the content changes.
3. **A guard around each system.** `Simulation.SystemFailed` (static, set by the app in `GameRoot.Awake`) receives a system that throws in a pass; the systems after it still run that pass, and the next pass is scheduled as before (ADR 0139). The app logs the exception. With no handler (tests, the balancer) the exception propagates at once, so a bug still fails loudly. `PassResilienceTests.ASystemThatThrows_DoesNotStopTheSystemsAfterIt_WhenTheAppHandlesIt`.
4. **A zero-tax town is only a little less happy: restated, not changed.** ADR 0120 already measured about 2 lower; with Health held at 85% without drains (ADR 0130) it is 0.3. Services weigh little against food, work and rent, and the grants keep a town alone solvent (ADR 0108). Spec 022 §9's "at least 5 lower" becomes "lower": the cost of no tax shows in the services (under 30%), which `TaxGateTests.EveryTaxAtZero` still asserts.

## Consequences

- No save change (the cap is content; the guard is code). The kiln's balance targets hold for one kiln.
- A throwing system in a Development or Release build is logged once per pass and the town carries on, so a real bug can hide in the log: the owner reads the Editor or device log after a build.
