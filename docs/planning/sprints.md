# Sprint plan

> 1-week sprints (ADR 0025). Each sprint: at most 2 specs for the owner to review and 1 device build; ends with a test checklist (when there is a build) and one pull request that the owner merges. Dates are planned and shift if a sprint carries over. Phases 2–5 are planned in detail at the start of each phase (ADR 0019). Phase 2's specs are reviewed two per sprint, each a sprint before its code, with the owner's enhancements first (ADR 0057); points for Sprints 12–18 come from the feature files and are re-pointed when each spec is written.
>
> **Notes:** Story points show relative size only; the real capacity limit is the owner's review and device-testing time. Phase 1 features start in Phase 0 sprints as soon as their spec is approved, because most don't need Unity. Phase 0's exit gate is still checked at the end of Sprint 3. Corrections from the pre-implementation audit are recorded in ADR 0027.

## Overview

| Sprint | Dates (planned) | Phase | Goal | Points | Specs to review | Device build |
| --- | --- | --- | --- | --- | --- | --- |
| S0 | 19–25 Oct 2026 | 0 | Foundations in place and spec 001 fully implemented and tested from the terminal; specs 002 (land) and 005 (ledger and credit) written. | 44 | 002, 005 | No (no Unity project yet) |
| S1 | 26 Oct – 1 Nov 2026 | 0 | Ledger core and land implemented; Unity project created and a "hello iPhone" build running; specs 003 and 004 written. | 28 | 003, 004 | Yes: hello iPhone (date, time, weather) |
| S2 | 2–8 Nov 2026 | 0 | Crops, the kitchen garden, fruit trees, founder labour and accrual accounting implemented; economy model running with anchor numbers; specs 006 and 007 written. | 53 | 006, 007 | No |
| S3 | 9–15 Nov 2026 | 0 | Market, the mandi trip and storage implemented; performance spike, on-device determinism and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review. | 52 | 008, 009 | Yes: spike and determinism build |
| S4 | 16–22 Nov 2026 | 1 | Credit and foreclosure; first playable build: the homestead map with sow and harvest, a minimal HUD and Store sheet, string tables and number formatting, and the dev panel. | 24 | — | Yes: first playable |
| S5 | 23–29 Nov 2026 | 1 | People: day labourers, the resident hand and standing orders, with their UI. | 21 | — | Yes |
| S6 | 30 Nov – 6 Dec 2026 | 1 | Saves, notifications and the play log; regulars; every sprite, the ground, icons and walk cycles rendered in Blender (ADR 0041). | 26 | — | Yes |
| S7 | 7–13 Dec 2026 | 1 | Full HUD, panels with Why?, While-you-were-away and settings; hourly weather spells (ADR 0042). | 19 | — | Yes |
| S8 | 14–20 Dec 2026 | 1 | Onboarding: first session, letter chapters and the Hamlet unlock; Phase 1 balancer bots and health checks. | 21 | Spec 009 amendment (ADR 0045) | Yes: feature-complete Development build |
| S9 | 21–27 Dec 2026 | 1 | Playtest week, tuning and the Phase 1 gate review. | 8 | — | Yes: playtest build |
| S10 | 4–10 Jan 2027 | 1 | Playtest fixes: the hand works every season, labourers till harvest (ADR 0048). | 6 | Spec 006 amendment (ADR 0048) | Yes: Development build |
| S11 | 11–24 Jan 2027 (2 weeks, ADR 0057) | 2 | ADRs 0052–0057; specs 010 and 011 approved; the village map, the trader's truck, daily prices and supermarket orders built. | 35 | 010, 011 | Yes: the village map, the truck and orders |
| S12 | 25 Jan – 7 Feb 2027 (2 weeks, as S11) | 2 | Spec 012 approved (ADR 0060) and built: buffalo and cows, the shed, fodder, straw, milk to the co-op; the village redrawn with the dairy co-op and a living valley (ADR 0061, 0062). Spec 014 written and approved. | 52 (re-pointed with spec 012 and ADRs 0061–0062, 0064) | 012, 014 | Yes: cattle |
| S13 | 8–21 Feb 2027 (2 weeks, as S12) | 2 | Citizens and the life clock, the family house, homes and people art; households, needs, happiness, work and food, your people on screen (F11.1, F11.2; spec 014, ADR 0063). | 39 | 016, 015 | Yes |
| S14 | 22–28 Feb 2027 | 2 | Population flows and family trees, population balance (F11.3); founding a business and its books (F12.1). | 32 | 017, 013 | Yes |
| S15 | 1–7 Mar 2027 | 2 | Production chains: flour mill, bakery, dairy (F12.2); full hiring and HR (F11.4). | 21 | 018, 019 | Yes |
| S16 | 8–21 Mar 2027 (2 weeks) | 2 | Shops, customer choice and rivals (F12.3, F12.4); irrigation, equipment, orchards and the granary (F13.2, F13.3). | 26 | — | Yes |
| S17 | 22–28 Mar 2027 | 2 | The school and literacy, the Codex and advisors, the Village unlock (F14.1, F14.2, US-102). | 18 | — | Yes |
| S18 | 29 Mar – 4 Apr 2027 | 2 | Statements, the finance hub and milestones (F14.3, F14.4); Phase 2 playtest and gate review. | 12 | — | Yes: Phase 2 playtest build |

## Sprint 0 · 19–25 Oct 2026 · Phase 0

- **Status:** Done (2026-10-17). Specs 002 and 005 approved by the owner the same day
- **Branch:** `sprint/00-foundations-time-weather`
- **Done:** 44 of 44 points
- **Goal:** Foundations in place and spec 001 fully implemented and tested from the terminal; specs 002 (land) and 005 (ledger and credit) written.
- **Points:** 44
- **Specs for the owner to review:** 002, 005
- **Device build:** No (no Unity project yet)
- Owner: review and approve specs 002 and 005.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-001 | Solution skeleton | [F00.1](features/F00.1-repository-build-and-ci.md) | 3 | Must | Done |
| US-002 | Continuous integration | [F00.1](features/F00.1-repository-build-and-ci.md) | 2 | Must | Done |
| US-003 | SessionStart hook | [F00.1](features/F00.1-repository-build-and-ci.md) | 1 | Must | Done |
| US-004 | Repository hygiene | [F00.1](features/F00.1-repository-build-and-ci.md) | 1 | Must | Done |
| US-005 | Spec 002: land and soil | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-006 | Spec 005: ledger and credit | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-025 | Fixed-point numbers | [F01.1](features/F01.1-sim-foundations.md) | 3 | Must | Done |
| US-026 | Counter-based RNG | [F01.1](features/F01.1-sim-foundations.md) | 2 | Must | Done |
| US-027 | State hash | [F01.1](features/F01.1-sim-foundations.md) | 1 | Must | Done |
| US-028 | Clock and weather content | [F01.1](features/F01.1-sim-foundations.md) | 3 | Must | Done |
| US-029 | Calendar and life clock | [F01.2](features/F01.2-calendar-and-scheduler.md) | 2 | Must | Done |
| US-030 | Scheduler | [F01.2](features/F01.2-calendar-and-scheduler.md) | 5 | Must | Done |
| US-031 | Commands and replay | [F01.2](features/F01.2-calendar-and-scheduler.md) | 3 | Must | Done |
| US-032 | Weather spells | [F01.3](features/F01.3-weather-and-forecast.md) | 3 | Must | Done |
| US-033 | Forecast | [F01.3](features/F01.3-weather-and-forecast.md) | 3 | Must | Done |
| US-034 | Live, away and anomalies | [F01.4](features/F01.4-live-and-away-time.md) | 3 | Must | Done |
| US-035 | Upcoming jobs for notifications | [F01.4](features/F01.4-live-and-away-time.md) | 1 | Must | Done |
| US-036 | Time-travel commands | [F01.5](features/F01.5-developer-time-travel-sim.md) | 1 | Must | Done |
| US-037 | Performance guard | [F01.5](features/F01.5-developer-time-travel-sim.md) | 1 | Should | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025)
- [x] Test checklist written in `docs/test-checklists/` (if there is a build): no build this sprint
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** No device build this sprint. Terminal results: 72 tests pass in Debug and Release; CI green on every push; all 29 spec 001 acceptance criteria have their named tests. The owner approved specs 002 and 005 with every recommended answer, which produced ADR 0028 (Hamlet pace, land priced by earning power, land-purchase loans).

**Retro (Claude's draft, owner to amend):**
- Went well: asking the four spec 001 questions up front kept coding unblocked, and pinning the RNG and hash golden values from an independent Python implementation verified them on the first run.
- Improve: NUnit 4.6 needs C# 10+ in tests, which ADR 0018 didn't foresee (now noted in ADR 0018's status line). Golden replay hashes moved three times during the sprint; later specs should add their state before pinning their golden files. Writing specs 002 and 005 exposed a pacing conflict in the GDD (ADR 0028); future specs should check their numbers against the pacing targets.

## Sprint 1 · 26 Oct – 1 Nov 2026 · Phase 0

- **Status:** Done (2026-10-18). 28 of 28 points. Specs 003 and 004 approved in the open-question review (2026-10-17), with ADRs 0030 and 0031; the hello-iPhone build confirmed on the device (2026-10-18)
- **Branch:** `sprint/01-ledger-land-unity`
- **Goal:** Ledger core and land implemented; Unity project created and a "hello iPhone" build running; specs 003 and 004 written.
- **Points:** 28
- **Specs for the owner to review:** 003, 004
- **Device build:** Yes: hello iPhone (date, time, weather)
- Owner: install Unity (latest LTS) and Git LFS, and create the Unity project at the start of the sprint.
- This de-risks Unity + Xcode 27.1 + free signing early (ADR 0027).

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-007 | Spec 003: crops, fruit trees and founder labour | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-008 | Spec 004: goods, market and storage | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-017 | Create the Unity project | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | Done |
| US-018 | Pin the Unity version | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | Done |
| US-019 | Code-first bootstrap | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 3 | Must | Done |
| US-020 | Build-and-install guide | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 2 | Must | Done |
| US-038 | Inherited land | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | Done |
| US-039 | Soil properties | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | Done |
| US-040 | Buy adjacent land | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | Done |
| US-041 | Tree slots | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | Done |
| US-059 | Exact ledger | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 5 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025)
- [x] Test checklist written in `docs/test-checklists/` (if there is a build): `s01-hello-iphone.md`, with `how-to-build-ios.md`
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Review (owner's device results, 2026-10-18, iPhone 13):** installed with `how-to-build-ios.md` alone, no issues. The app opens in landscape at the native 2532 × 1170 and shows `Year 1 · Spring · Day 1`, the live time (08:17) and the weather (Sun) on the cream card, clear of the notch. Checks 1, 2, 3 and 6 of `s01-hello-iphone.md` pass; checks 4, 5 and 7–10 weren't reported and are re-checked with the next device build.

**Retro:** The Unity Pipeline CLI let Claude verify Unity work (compile, play mode, screenshots, the iOS build) without waiting for the owner, and caught the missing-font bug before the device run. The open-question review turned up a pacing conflict that reshaped the start of the game (ADR 0030); doing that review before merging kept all the decisions in one pull request.

## Sprint 2 · 2–8 Nov 2026 · Phase 0

- **Status:** Done (58 of 58 points, 2026-10-18). Specs 006 and 007 approved with all 13 open questions answered
- **Branch:** `sprint/02-crops-trees-founder` (or the branch the session assigns)
- **Goal:** Crops, the kitchen garden, fruit trees, founder labour and accrual accounting implemented; economy model running with anchor numbers; specs 006 and 007 written.
- **Points:** 58 (53 planned; +5 from the Sprint 2 planning review, ADR 0032: the spec 004 core and the health formula moved in)
- **Specs for the owner to review:** 006, 007
- **Device build:** No
- Economy model v0 prints seasonal cash flow from content data.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-009 | Spec 006: people | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-010 | Spec 007: saves, notifications and play log | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-013 | Seasonal farm cash-flow model | [F00.3](features/F00.3-economy-model.md) | 5 | Must | Done |
| US-014 | Anchor numbers in content | [F00.3](features/F00.3-economy-model.md) | 3 | Must | Done |
| US-042 | Sow in season | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | Done |
| US-043 | Growth and ready to harvest | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | Done |
| US-044 | Yield formula | [F02.2](features/F02.2-crops-and-yield.md) | 5 | Must | Done |
| US-045 | Seeds and fertiliser | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | Done |
| US-047 | Plant a sapling | [F02.3](features/F02.3-fruit-trees.md) | 2 | Must | Done |
| US-048 | Maturity and fruiting | [F02.3](features/F02.3-fruit-trees.md) | 3 | Must | Done |
| US-050 | Labour capacity | [F03.1](features/F03.1-founder-labour-and-routine.md) | 3 | Must | Done |
| US-051 | Household plan | [F03.1](features/F03.1-founder-labour-and-routine.md) | 4 | Must | Done |
| US-053 | Day labour and subsistence | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Must | Done |
| US-060 | Accrual and prepaid costs | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | Done |
| US-061 | Season close and P&L | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | Done |
| US-062 | Net worth | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | Done |
| US-177 | Kitchen garden | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | Done |
| US-181 | Goods, prices and inputs at use (core) | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 2 | Must | Done |
| US-057 | Store and capacity (from S3) | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025)
- [x] Test checklist written in `docs/test-checklists/` (if there is a build): no device build this sprint
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Review (owner's device results):** no device build this sprint. Specs 006 and 007 reviewed and approved (2026-10-18).

**Retro:**
- A planning review before coding found 10 gaps (8 before, 2 from the economy model); answering them one by one with the owner (ADRs 0032 and 0033) kept the code free of rework.
- Pulling the spec 004 core forward was right: crops, the household and net worth all needed goods, prices and the store.
- The real-sim economy model (a bot on the actual simulation) and the scratch moisture model found what tables couldn't: the land-payback measure and that moisture needed realistic climate weights, not a rule change.
- To watch: `git add -A` swept an Editor-local Unity file into a commit; stage paths explicitly when the Editor may be open.

## Sprint 3 · 9–15 Nov 2026 · Phase 0

- **Status:** Done (2026-10-18). **Phase 0 gate passed** (ADR 0035)
- **Branch:** `sprint/03-market-spike-gate` (or the branch the session assigns)
- **Goal:** Market, the mandi trip and storage implemented; performance spike, on-device determinism and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review.
- **Points:** 47 (52 planned; −5 to Sprint 2, ADR 0032)
- **Specs for the owner to review:** 008, 009
- **Device build:** Yes: spike and determinism build
- Phase 0 gate: money-loop checks pass, the spike fits 8.3 ms, and golden replays match on the iPhone.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-011 | Spec 008: HUD, presentation and localisation | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-012 | Spec 009: onboarding | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | Done |
| US-015 | Money-loop checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | Done |
| US-016 | Viability checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | Done |
| US-021 | 2,000-agent spike | [F00.5](features/F00.5-performance-spike.md) | 5 | Must | Done |
| US-022 | Record the result | [F00.5](features/F00.5-performance-spike.md) | 1 | Must | Done |
| US-023 | On-device determinism | [F00.5](features/F00.5-performance-spike.md) | 3 | Must | Done |
| US-024 | CC0 style check | [F00.6](features/F00.6-art-style-check.md) | 2 | Should | Done |
| US-046 | Pests and spraying | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Should | Done |
| US-049 | Tree value and payback | [F02.3](features/F02.3-fruit-trees.md) | 2 | Should | Done |
| US-052 | Health and sick days | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Should | Done |
| US-054 | Price index and fixed-point powers | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 2 | Must | Done |
| US-055 | Hourly clearing | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 5 | Must | Done |
| US-056 | Sell and buy | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | Done |
| US-058 | Spoilage | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | Done |
| US-178 | Mandi trip | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | Done |
| US-179 | Buy inputs ahead | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 1 | Must | Done |
| US-180 | Minimum-price offers | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 1 | Should | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025): 47 of 47 points
- [x] Test checklist written in `docs/test-checklists/` (`s03-spike-determinism.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (PR #15); owner merges to `main`

**Planning review (2026-10-18):** 9 gaps closed with the owner, ADR 0034.

**Done this session (Claude):** US-054, 055, 056, 058, 178, 179, 180 (spec 004 fully implemented); US-046, 049, 052 (spec 003 fully implemented); goldens 003 and 004; US-015 and US-016 (the money-loop and viability gate checks pass on 1,000 seeds); the economy model on real commands; the spike and determinism build (Unity Editor: all goldens pass; Xcode Release compiles); the style check; specs 008 and 009 written. 182 tests.

**Review (owner's device results):**
- *Round 1 (2026-10-18, iPhone 13):* the spike runs smoothly with all 2,000 agents on the whole map; GPU 2.7 ms (whole map) and 3.4 ms (during the heat run), well inside 8.3 ms. The CPU figure was negative (a measurement bug: the present wait was subtracted from a time that already excludes it), and the buttons didn't stop a running check, so the style check drew over the heat run. The style check itself renders as designed. Golden replays and the result panels weren't captured. Both bugs fixed (4d9199a).
- *Round 2 (2026-10-18):* **golden replays 001, 003 and 004 all PASS on the iPhone 13 with the exact `dotnet test` hashes** (IL2CPP determinism proven); 72 h catch-up 1.8 ms (budget 2,000). Spike: main thread p95 7.45 ms (whole map), 3.5 ms (play zoom); GPU p95 2.8 ms; render thread p95 14.5–15.2 ms; "frames over 16.7 ms" 36% (the threshold counted normal 60 Hz vsync jitter). Heat run (5 min): the same numbers, the phone stayed cool. Style check approved (the low-poly look). Citizen stub avg 2.1–2.6 ms, max 11.6 ms.
- *Round 3 (2026-10-18):* every view about 60 fps with under 1% of frames over 25 ms; main thread p95 at most 7.59 ms, GPU p95 at most 2.81 ms. The map with **no agents** shows the same render-thread time (10.2 ms avg), so that figure is the wait for the 60 Hz screen, not work. **The spike passes; the Phase 0 gate passes** (owner, ADR 0035).

**Retro:**
- *Went well:* closing the 9 planning gaps before code meant no rework on the market; the gate checks and the stress bots found real design issues (subsistence dominance, free field storage, a dead mandi, edge-tree dominance) before any player could; determinism held on IL2CPP first time.
- *To improve:* my device measurements needed three rounds (a wrong CPU field, overlapping checks, a bad dropped-frame threshold). Next time: validate a measurement's semantics in the Editor against a known baseline before it goes to the phone, and design each check to stop the previous one.
- *Carry over:* remove or hide the Phase 0 bar (Sprint 4, F08.4); instancing for agents later (ADR 0035).

## Sprint 4 · 16–22 Nov 2026 · Phase 1

- **Status:** Done (24 of 24 points; device runs 2 and 3 passed on the iPhone 13, 2026-10-19). Planning review (2026-10-18): 12 gaps closed with the owner, ADR 0038
- **Branch:** `sprint/04-credit-first-playable` (or the branch the session assigns)
- **Goal:** Credit and foreclosure; first playable build: the homestead map with sow and harvest, a minimal HUD and Store sheet, string tables and number formatting, and the dev panel.
- **Points:** 24 (23 planned; US-078 +1 minimal HUD with Collect all, US-079 +1 Store sheet −1 ghost preview moved to US-068, ADR 0038)
- **Specs for the owner to review:** —
- **Device build:** Yes: first playable

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-063 | Borrow | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | Done |
| US-064 | Repayments and penalties | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | Done |
| US-065 | Foreclosure | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | Done |
| US-078 | Homestead map and minimal HUD | [F08.1](features/F08.1-map-and-field-interaction.md) | 6 | Must | Done |
| US-079 | Tap to act and Store sheet | [F08.1](features/F08.1-map-and-field-interaction.md) | 3 | Must | Done |
| US-085 | String tables | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | Done |
| US-086 | Number formatting | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | Done |
| US-088 | Dev panel | [F08.4](features/F08.4-developer-panel-unity.md) | 2 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the UI stories are verified in the Editor, the device run is the owner's
- [x] Test checklist written in `docs/test-checklists/` (`s04-first-playable.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Review (owner's device results):** run 1 found three touch and layout bugs (fixed the same day); run 2 passed all checks but 11 (empty tree places not tappable; fixed, with the owner's sheet changes); run 3 passed all checks, with golden replays 001, 003, 004 and 005 PASS on the device. The owner rates the art as placeholder and wants it improved by all means (free only, ADR 0007).

**Retro (Claude):**
- *Went well:* the gap review found real loopholes before code: collateral could be sold while the loan stayed, and the protected plot made unsecured default nearly free. The stress bots then measured the default loophole at about +4% (not +20%), so ADR 0038's rules are a safety margin. `Simulation.Preview` made every greyed button show the sim's own reason with no duplicated rules. The painter gave a cohesive look with no art assets; the Editor loop (taps and captures through the `unity` CLI) caught layout problems before any phone build.
- *To improve:* the Editor stops the game when unfocused and reloads the domain when sim sources change, so each check cost restarts; Unity's post-build step crashes on macOS 27 over an existing Xcode project (a batch build with `Builds/iOS` deleted works). The look is a stand-in: judge it on the device, then decide how much waits for the Blender sprites. Sprint 4 was far larger than its 23 planned points of work: plan sheets and art as their own stories.
- *Carry over:* the placement ghost (US-068, Sprint 5); Blender pipeline (US-171, Sprint 6; owner installs Blender); the loan, *Why?* and season-report screens (Sprint 7).

## Sprint 5 · 23–29 Nov 2026 · Phase 1

- **Status:** Done (25 of 25 points; device runs 1 and 2 passed on the iPhone 13, PR #10 merged). Planning review 2026-10-19: 11 decisions with the owner (ADR 0039)
- **Branch:** `sprint/05-people`
- **Goal:** People: day labourers, the resident hand and standing orders, with their UI; the first Blender renders.
- **Points:** 25 (US-069 +1 for `PostJob`; US-068 +1 for the placement ghost, ADR 0038; ADR 0039: US-183 +5, US-182 +1, US-067 −2 to Sprint 6)
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-066 | Hire a day labourer | [F06.1](features/F06.1-day-labourers.md) | 3 | Must | Done |
| US-068 | Build a hand's hut | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | Done |
| US-069 | Hire a resident hand | [F06.2](features/F06.2-resident-farm-hand.md) | 4 | Must | Done |
| US-070 | Season contract | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | Done |
| US-071 | Define standing orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | Done |
| US-072 | Execute orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | Done |
| US-182 | People-only While you were away | [F08.2](features/F08.2-hud-and-panels.md) | 1 | Must | Done |
| US-183 | Blender pipeline slice | [F19.3](features/F19.3-art-pipeline.md) | 5 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the UI is verified in the Editor with touch (mouse removed), the device run is the owner's
- [x] Test checklist written in `docs/test-checklists/` (`s05-people.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Review (owner's device results):** the owner approved the Blender style checkpoint and the redone people (2026-10-19). Device run 1: all checks passed (golden replays 001, 003, 004, 005 PASS on the iPhone 13; 72 h in 2.9 ms); the owner asked for a roomier yard, hands' huts off the fields, orders that start at once and people who work: built the same day as ADR 0040 (2-acre yard, labour quarters, immediate orders, work poses and patrols). Device run 2 passed (golden replays 001, 003, 004, 005 PASS on the device); the ADR 0040 changes approved (2026-10-19).

**Retro (Claude):**
- *Went well:* the gap review caught two money loopholes (part-day regulars, lapse-and-renew) before any code, and the lapse-gamer stress bot then proved the fix (0.997 × competent). The gate checks found a real balance problem (a hand didn't pay over 11 years) and the owner settled it with numbers in hand (30,000 a season, ADR 0039 §11). The Blender rig renders in seconds, so the owner's feedback on people (size, clothes, men and women) was turned around in one round.
- *To improve:* three Editor-only traps cost time: fast enter-play-mode keeps static caches whose runtime textures are gone (fixed in the sprite banks), the Game view's toolbar skews `Screen.height` for touch aiming (fixed in `TouchDriver`), and a UI bar built inside a click handler never got touch clicks (placement now starts on the next UI tick). `DebugAdvance` only takes fixed steps: scripts must check its result.
- *Carry over:* regulars (US-067) and the full Blender re-render with icons (US-171) to Sprint 6; walk and hoe animation frames later; the loan screen (Sprint 7) replaces the dev panel's loan button.

## Sprint 6 · 30 Nov – 6 Dec 2026 · Phase 1

- **Status:** Done (26 of 26 points; device runs 1 and 2 passed, 2026-10-20). Planning review 2026-10-19: 8 gaps decided with the owner (ADR 0041)
- **Branch:** `sprint/06-saves-notifications`
- **Goal:** Saves, notifications and the play log; regulars; every map sprite, the ground, every icon and the walk cycles rendered in Blender (ADR 0039, ADR 0041).
- **Points:** 26 (16 + US-067 2 + US-171 8; ADR 0041: US-171 4 → 8 for the ground and walk cycles)
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-073 | Serialise the game | [F07.1](features/F07.1-save-system.md) | 5 | Must | Done |
| US-074 | Safety save points | [F07.1](features/F07.1-save-system.md) | 3 | Must | Done |
| US-075 | Save now, export, import, new game | [F07.1](features/F07.1-save-system.md) | 3 | Must | Done |
| US-076 | Ready notifications | [F07.2](features/F07.2-local-notifications.md) | 3 | Must | Done |
| US-077 | Record and export | [F07.3](features/F07.3-local-play-log.md) | 2 | Must | Done |
| US-067 | Regulars | [F06.1](features/F06.1-day-labourers.md) | 2 | Should | Done |
| US-171 | Blender pipeline: the full re-render, ground, icons and walk cycles | [F19.3](features/F19.3-art-pipeline.md) | 8 | Should | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the UI is verified in the Editor with touch (mouse removed), the device run is the owner's
- [x] Test checklist written in `docs/test-checklists/` (`s06-saves-notifications.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#11); owner merges to `main`

**Done this sprint (Claude):** the save codec (one write path with the hash, content by id, gzip and checksum; a 10-year save is 116 KB, written in 12 ms and read in 3 ms), live-`Sync` compaction and the log checkpoint, atomic save points with hourly and daily backups and recovery, autosave, export, import from a list, new game, notifications planned from a simulated absence (ranked, grouped, quiet hours, no daily cap), the first-ask card, the play log, regulars, a gear button and Settings sheet; every sprite, the ground, the icons and 4-frame walk cycles rendered in Blender (the style checkpoint approved). 249 `Sim.Core` and 46 `Sim.View` tests pass; golden hashes unchanged. Verified in the Editor with touch: save and relaunch, Settings, export, import and replace, new game, the notification ask and scheduling (moved out of quiet hours), the restored and unreadable cards, the play log, the art and walk cycles. **Not verified in the Editor:** the regular's card (covered by the sim tests; check 19 on the device).

**Review (owner's device results):** device run 1 (2026-10-20): golden replays 001, 003, 004, 005 PASS on the iPhone 13 (72 h in 1.4 ms); saves, relaunch, export and import, the renders and Settings work; notifications to be confirmed later (not a blocker). Owner's changes, built the same day: smaller, spaced hands' huts (and a loaded town no longer drew each hand's hut twice); a Store row for goods at the market with **Take off market** (a hand's market order in a glut had left the player with a greyed *Sell* and a wrong reason) and the order's default minimum rounded down; Settings switches as a green **On** / red **Off**; short texts no longer wrap over the line below (Settings, the sow sheet). Then: the sow sheet shows one sowing's Spend, Sells for and Profit (it showed a season's earnings per acre) with fertiliser cards that say what each does; people no longer go under the land at a tile's back or the map's edge. **Device run 2 (2026-10-20): every check passed**, notifications included (checks 12–17). The first build of run 2 had no developer panel: it was made without the Development option (a Release build compiles the panel out); rebuilt with `--options '["Development"]'`.

**Retro (Claude):**
- *Went well:* one write path for the hash and the save means a field can't be hashed and not saved, and the round-trip property test proved it on 50 towns. Planning notifications on a copy of the town made them exact (AC-13 matches a real absence event for event) with no new rules. The Blender rig took the full re-render in a few rounds after the style checkpoint.
- *To improve:* the device build's Development flag: `unity command build --options` needs a JSON array (`'["Development"]'`). A plain string is rejected, and `build_status` then still shows the previous build's success. Check a build's options and time before handing it over.
- *Carry over:* migrations with tests from the Sprint 9 playtest build (ADR 0041 §2); the letter takes over the notification ask (Sprint 8); the Menu entry for Settings (Sprint 7).

## Sprint 7 · 7–13 Dec 2026 · Phase 1

- **Status:** Done (25 of 25 points; PR #12 merged). Device run 1 passed with seven changes, built the same day (ADR 0044); device run 2 (checks 29–34) passed 2026-10-20. Planning review 2026-10-20 (ADR 0043: 11 gaps); spec 001's weather sections re-approved the same day
- **Branch:** `sprint/07-hud-panels`
- **Goal:** Full HUD, panels with Why?, While-you-were-away and settings.
- **Points:** 25 (19, then ADR 0043: US-081 3 → 5 for *Why?* on every number, US-082 4 → 6 for the founder, money-and-loans, market and mandi panels, US-084 1 → 3 for text size and the iOS plugin). Order: US-184, US-081, US-082, US-080, US-083, US-084, US-087; US-087 is cut first if time runs short
- **Specs for the owner to review:** spec 001 §3.5–3.6, §4, §8, §9 (the US-184 amendment, ADR 0042 and ADR 0043)
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-080 | HUD | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | Done |
| US-081 | Why? breakdowns | [F08.2](features/F08.2-hud-and-panels.md) | 5 | Must | Done |
| US-082 | Panels | [F08.2](features/F08.2-hud-and-panels.md) | 6 | Must | Done |
| US-083 | While you were away | [F08.2](features/F08.2-hud-and-panels.md) | 2 | Must | Done |
| US-084 | Settings | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | Done |
| US-087 | Feedback sounds | [F08.5](features/F08.5-basic-feedback-sounds.md) | 2 | Could | Done |
| US-184 | Hourly weather spells, per-weather persistence (ADR 0042) | [F01.3](features/F01.3-weather-and-forecast.md) | 4 | Should | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the UI is verified in the Editor with touch (mouse removed), the device run is the owner's
- [x] Test checklist written in `docs/test-checklists/` (`s07-hud-panels.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Done this sprint (Claude):**
- **US-184:** 1-hour weather spells with persistence per weather and season, draw weights derived from the unchanged shares, the forecast rolled per run (spec 001 amended, re-approved); save schema 2; goldens re-pinned in their own commit; the balance gate re-run on 1,000 seeds (every check passes; loam moisture in band in every season; rainy Monsoon hours 51.1%, as before the change).
- **US-081:** `WhyView`, a *Why?* in words for every number (causes with "N% more / less / no change", never a factor), swept over four seeded situations by `WhyTests.EveryNumberHasWhy`.
- **US-082:** the Menu, Buy land with Show, Money and loans (borrow by amount then term with payments and total interest, repay, the report, buy with a bank loan), You and your family, a good's sheet with offers and minimums, Buy inputs in acres, the mandi with load rows.
- **US-080:** the HUD's weather run and forecast sheet, cash and store tap / hold, the Menu, alerts (the ! button and list). **US-083:** the full away card (only when something new happened), the report at a live season close, Collect all's sell-the-rest card, the event map. **US-084:** numbers, text size, reduced motion (iOS default), sound, haptics (an iOS plugin). **US-087:** seven Kenney CC0 sounds.
- 249 `Sim.Core` and 57 `Sim.View` tests pass. **Verified in the Editor with touch:** every new sheet and card (captures `docs/art/s07-*.png`), text at 125% and 150%, the old save refused and kept. Development build: Unity build succeeded (the developer panel, golden files and the plugin are in it).

**Review (owner's device results):** device run 1 (2026-10-20): golden replays 001, 003, 004, 005 PASS on the iPhone 13 with the re-pinned hashes (72 h in 3.1 ms); everything else fine apart from seven points, built the same day as **ADR 0044**:
1. A **←** back button on every sheet opened from another sheet (the Store from the Menu, the mandi trip from the Store), returning the way it came.
2. Sounds only for town events (sowing, harvest, coins, ready, urgent): the tap and sheet sounds and their clips are gone.
3. A *Why?* opens only from its **?**. It had popped up in the Store from a finger resting on a row while scrolling, and from a slow tap on the store pill. The store's *Why?* moved to an "In the store" row.
4. Repaying all of a loan left it open with 0 coins owing: the amount was fixed when the sheet was drawn while interest accrued each second. It is now worked out at the press.
5. Net worth checked: no error. 9 acres at today's price are 10,55,664 of a new town's 11,50,735. Money and loans now shows "Of that, your land" under it.
6. Text size Small 80% / Normal / Large 125% (150% removed).

**Device run 2 (2026-10-20): checks 29–34 (the ADR 0044 changes) all pass.** Back button, borrow return, repay all, *Why?* only from its ?, net worth's land row, silent taps.

**Retro (Claude):**
- *Went well:* a coverage test over every row made "every number has a *Why?*" checkable instead of a promise; it found the job-post and garden rows nobody had listed. Pricing Collect all's leftovers on a copy of the town made the prompt exact with no new rules.
- *To improve:* editing any script while the Editor is playing reloads the domain and breaks the session: batch the fixes, stop, recompile, play. Captures can be a frame stale: capture twice. A card built inside a click handler misses its first touch: open it on the next UI tick (now done for *Why?*).
- *Carry over:* alert dismissals if playtesters ask (spec 008 §10 note 8); Blender icons for the Menu, alerts and the trolley (the sheets use the basket for now).
- *Device review lesson:* a view model that bakes a money amount into a button goes stale while time runs, and an unchanged sheet isn't redrawn. Commands that settle "everything owed" compute it at the press. Sheets reached from another sheet need a way back from the start.

## Sprint 8 · 14–20 Dec 2026 · Phase 1

- **Status:** Done. Device run passed 2026-10-21; three findings fixed (ADR 0046), re-check passed (checks 33–36, 2026-10-21); PR #13 merged. Planning review 2026-10-20 (ADR 0045: 5 gaps); spec 009 amended and re-approved the same day
- **Branch:** `sprint/08-onboarding-hamlet`
- **Goal:** Onboarding: first session, letter chapters and the Hamlet unlock; Phase 1 balancer bots and health checks.
- **Points:** 21
- **Specs for the owner to review:** spec 009 §3.2, §7, §8, §10 (the ADR 0045 amendment; re-approved)
- **Device build:** Yes: a feature-complete Development build (ADR 0045 §5; the release candidate is Sprint 9's US-095)

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-089 | The letter | [F09.1](features/F09.1-first-session.md) | 2 | Must | Done |
| US-090 | Guided first actions | [F09.1](features/F09.1-first-session.md) | 5 | Must | Done |
| US-091 | Guided-once triggers | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | Done |
| US-092 | Chapters | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | Done |
| US-093 | Era unlock check | [F09.3](features/F09.3-hamlet-unlock.md) | 3 | Must | Done |
| US-094 | Hamlet celebration | [F09.3](features/F09.3-hamlet-unlock.md) | 2 | Must | Done |
| US-098 | Phase 1 bots and checks | [F20.1](features/F20.1-balancer-and-health-checks.md) | 3 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the UI is verified in the Editor with touch (mouse removed), the device run is the owner's
- [x] Test checklist written in `docs/test-checklists/` (`s08-onboarding-hamlet.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merges to `main`

**Done this sprint (Claude):**
- **Sprint 7 closed** (checks 29–34 passed). **Planning review** (ADR 0045, spec 009 re-approved): nudges and the tour, the long crop on any acre, the `comeBack` ask with the card as fallback, AC-11 to AC-16, the Phase 1 health checks with measured thresholds, a feature-complete Development build.
- **US-093:** `content/eras.json`, `EraState`, `EraRules` (after each field harvest and season close), `EraProgress` and `EraUnlocked`; save schema 3. The onboarding and era content load with the people content, so the frozen golden content has neither and **the golden hashes are unchanged**. `GateTests.HamletPace`: 100 of 100 seeds unlock, median day 5.4 (target 7 ± 2).
- **US-098:** the cautious saver, random and recovering bots; `NeverStuck` (credit refused: 1,000 of 1,000 profitable by season 2), `RecoverableBankruptcy` (84 of 84 foreclosed towns profitable the next season; the Hamlet's conditions again in a median of 8 days, max 9.6), `ExactLedger`, `BoundedPrices` (rail 0.000%). Median season farm profit 26,048, inside spec 003 §9's band: nothing tuned.
- **US-089 to US-092, US-094 (`Sim.View`):** `Guide` (steps, chapters once in priority order, nudges, the tour), `Letter.Page` and `Letter.List`, `Hints.Season` from content, `ProgressSheet` with its *Why?*, the notification card as the fallback. **Unity:** the letter page, the nudge pill, the gold arrow, Menu → Dadi's letter, Becoming a Hamlet, the warm light and banner.
- **Editor tour with touch** (captures `docs/art/s08-*.png`) found and fixed: "Welcome back" over no away card (trigger `awayCard`), the first-basket and trader pages before their moment (`pageAfter`), the footer wrapping beside three buttons, the pill over the arrow (now bottom centre), the arrow over the ready marker, Show me when reading again, "ready at", the season titles, a Sprint 7 "work work".
- **Carry-over:** Blender icons for the letter, the Menu (a *bahi-khata* ledger), the alerts (a bell) and the mandi trolley.
- 264 `Sim.Core` and 69 `Sim.View` tests pass. Development build: Unity build succeeded (the developer panel, the golden files, the new content).

**Review (owner's device results, 2026-10-21):** every check passed; the first session (checks 1 to 12) took 10 minutes (target 15–20 ± 5). Three findings, fixed the same day (**ADR 0046**; spec 008 §3.9 and AC-8, spec 009 §10, the GDD):
1. A letter page dimmed only the safe area, not the whole screen. Every veil now reaches past the safe area (`UiKit.Veil`): cards, letter pages, *Why?*, the away card, the developer panel.
2. The away card and "Welcome back" came after an hour away: the house eats every day, so something always "happened". The owner asked for the card only at the 72 h cap; on the recommendation, the card now needs **8 hours away** or the cap (`awayCardMinHours` in `content/ui.json`), so a day or two away still gets its summary.
3. After **Away cap**, "Open the Store and sell to the trader" stayed with the store bouncing and nothing to sell (the house had eaten the inherited grain). The sale nudge now shows only while the trader would buy something. Also: the developer panel opens above the nudge, and its new **Away +1 hour** tests the threshold.
- Verified in the Editor with touch: the veil past a simulated inset (`docs/art/s08-letter-veil-full-screen.png`), no card after an hour away, the card after a day. Re-check on the phone: checks 33–36 **PASS** (2026-10-21).

**Retro (Claude):**
- *Went well:* gating the onboarding content on the people content kept the goldens untouched with no special case. Measuring each health check before fixing its threshold gave thresholds that are realistic and still fail on a regression. The bot that follows the highlights made the first session testable end to end.
- *To improve (device review):* the Editor has no notch, so a veil inside the safe area looked right there; check overlays with a simulated inset. A step goal that the world can make impossible (the store emptied by the house) needs its nudge to check that it's still possible.
- *To improve:* the spec's step pages mixed "before" and "after" texts; only playing them showed it. Read each page against the moment it opens when a spec gives texts. Editing any file Unity watches during play mode reloads the domain (the sim's sources included): stop first, always.
- *Carry over:* save migrations from the Sprint 9 playtest build (ADR 0041 §2); alert dismissals if playtesters ask; the guide's memory of events is per launch (spec 009 §10 notes) if playtesters hit it.

## Sprint 9 · 21 Dec 2026 – 3 Jan 2027 · Phase 1 (2 weeks, ADR 0047 §6)

- **Status:** Done. Closed by the owner on 2026-10-21 (PR #14 merged): Phase 1 declared complete from the owner's own play. The play log and diary weren't filed; the playtest's findings came as a bug report, fixed in Sprint 10 (ADR 0048)
- **Branch:** `sprint/09-playtest`
- **Goal:** Release candidate, the owner's solo playtest week, tuning and the Phase 1 gate review (part A).
- **Points:** 10
- **Specs for the owner to review:** — (spec 007 §3.1, §3.6, AC-14, AC-15 and spec 009 §10 amended with ADR 0047)
- **Device build:** Yes: a Development and a Release build from the release-candidate commit; a tuned Release build only if tuning changes something
- **Gate (ADR 0047 §5, part A):** A1 a reason to return on at least 4 of days 2–7 (3 by the game, from the diary); A2 at least 2 notification returns, none in quiet hours; A3 the Hamlet on day 5–9; A4 no blocker; A5 the RC passes; A6 CI green. Part B (fresh players) is US-185, later.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-095 | Release candidate | [F10.1](features/F10.1-playtest-and-tuning.md) | 4 | Must | Done |
| US-096 | Playtest week | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | Done (owner's play; no log filed) |
| US-097 | Tune and decide | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | Done (owner: Phase 1 complete) |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025), as closed by the owner
- [x] Test checklist written in `docs/test-checklists/` (`s09-release-candidate.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; owner merged to `main` (PR #14)

**Planning review (2026-10-21, ADR 0047):** the owner is the only playtester (free account, iPhone 13); the play log adds `tz`, `fromNote` and the first-session steps, with a daily diary; migrations from the RC (`minSchema` frozen at 3, a fixture per schema); a Development build for regression and frame time, a Release build for the playtest, and the blocker definition; the gate split into part A (this sprint) and part B (US-185); 2 weeks, 10 points.

**Done this sprint (Claude):**
- **Sprint 8 closed** (checks 33–36 passed). **Planning review** (ADR 0047).
- **US-095:** `MigrationTests` on the frozen `saves/schema3.ftc` (spec 007 AC-14); the developer panel's **Measure frame time (20 s)** and frame-timing stats in Development builds; `s09-release-candidate.md` (Part A: 57 rows from s04–s08; Part B: the Release smoke list).
- **US-096:** the play log's `tz`, `fromNote`, `step` and `firstSessionDone` (spec 007 AC-15), a session line for a new town; Part C, the diary.
- Verified in the Editor with touch: Play log On → New game → Replace → Continue (the log's `newGame`, `step`, `tz`); the frame-time button, countdown and result.
- 268 `Sim.Core` and 69 `Sim.View` tests pass. Development and Release Unity builds succeeded from `33afedb`.

**Carry over (from Sprint 8):** the first session's 10 minutes (recorded by the play log, not gated: ADR 0047 §5); alert dismissals and the guide's per-launch memory only if the playtest hits them.

**Review (owner's device results, 2026-10-21):** the owner played the build on the iPhone 13 and declared Phase 1 complete. Gate part A wasn't measured from a log (A1–A3 by the owner's judgement; A4–A6: no blocker reported, the RC built, CI green). Three people findings, fixed in Sprint 10 (ADR 0048): the hand stayed at his hut until the owner sowed by hand; he didn't sow or care in other seasons; day labourers left mid-crop.

**Retro (Claude):**
- *Went well:* the migration fixture paid off at once: Sprint 10's schema 4 migration was tested against the frozen RC town on day one.
- *To improve:* every-season orders were only tested with crops sown in every season (test content), so the real calendar's idle seasons never showed. Test rules on the real content's calendar too. A hand's work that the map didn't show read as no work at all: what the sim does must be visible.
- *Carry over:* gate part B (US-185) before Launch.

## Sprint 10 · 4–10 Jan 2027 · Phase 1 (playtest fixes)

- **Status:** Done. The owner's device check passed on 2026-10-21 (ADRs 0048–0051); the owner merges PR #15
- **Branch:** `sprint/10-people-fixes`
- **Goal:** The owner's Phase 1 playtest findings: the hand works every season and is seen working; labourers stay till harvest.
- **Points:** 6
- **Specs for the owner to review:** spec 006 §3.2, §3.3, §3.8, §3.9, §5, §6, §8 (ADR 0048; decided with the owner in the session)
- **Device build:** Yes: Development build (`s10-people-fixes.md`)

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-186 | The hand works every season | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | Done |
| US-187 | Labourers till harvest | [F06.1](features/F06.1-day-labourers.md) | 3 | Must | Done |

**Sprint end checklist**

- [x] All Must stories meet the Definition of Done (ADR 0025); the owner's device check passed
- [x] Test checklist written in `docs/test-checklists/` (`s10-people-fixes.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (PR #15); owner merges to `main`

**Done this sprint (Claude):**
- **Sprint 9 closed** at the owner's word (Phase 1 complete). **Root cause** of the idle hand: an every-season order sowed only its own crop, so out of season it waited all season (wheat waits through Spring, Summer and the Monsoon); and the map walked a hand only to his own order fields with work, though his hours tended every growing field. Labourers were paid to the daily pass and left there.
- **US-186:** the hand's best in-season pick by profit per season on the field's soil and moisture (`FarmRules.BestCrop` on the sow sheet's `FarmRules.Estimate`), "Let Manoj choose" in the order sheet, the map walking a paid hand round every growing field.
- **US-187:** `HireLabourers(tile, count)` until the crop's ready hour; they harvest it and go; hired from the growing field's sheet; save schema 4 (schema 3 migrated; `saves/schema4.ftc`).
- Unity compiles the sim (C# 9). **Editor tour with touch** on the RC town imported 72 hours on: the hand had sowed 14 fields and brought in 14 harvests, because lentils are out of season in the Monsoon. Then: the field sheet's hire rows, cost and *Why?*; two labourers hired; the order sheet's **Let Manoj choose** → Every season → Sell to the trader; the map with Manoj and both labourers working; 4 hours on, the labourers harvested field 19 and left, and Manoj re-sowed it; the People and hand sheets. No console errors. Captures are in `docs/art/s10-*.png`.
- **Fixed during the tour:**
  - Hiring closed the field sheet. It now stays open, like Spray, to show "Labourers on this crop".
  - Dadi's "More hands" page and three labels still described hiring for a day.
  - An order line now names the hand's pick when it differs from the order's crop ("Keep Lentils (Rice now)").
- 270 `Sim.Core` (balance gate and goldens unchanged) and 70 `Sim.View` tests pass.

**Review (owner, 2026-10-21; ADR 0049):** three changes, made the same day.
1. **People were drawn under the crops near a field's edges.** Crops now have their own layer, below everyone and everything standing on the land.
2. **An order on a second field waited up to an hour**, because a hand did one action an hour. `orderActionsPerHour` is now 6, so every order acts at once; Care still limits what a hand manages. This exposed a bug: sowing by hand and changing the order lost the player's sowing. The player's sowing now runs first.
3. **Hiring is back in People** ("till the crops are in"). Labourers stay until the last growing crop's ready hour and bring in every ripe field that has no hand's order. The field sheet no longer hires.
- **Verified in the Editor with touch:** the People hire rows and costs; 2 labourers hired, standing on top of the rice at the field's edge (`docs/art/s10-people-on-top-of-crops.png`); a field sheet without hire rows; two orders given through the order sheet, one after the other, both sown at once. No console errors.
- All 270 `Sim.Core` tests (balance gate included) and 70 `Sim.View` tests pass.

**The owner's follow-up: "the hand still waits in his hut" (ADR 0050).** Reproduced: the hand's choice ignored cost, so once the inherited seed potato was gone, a Spring order waited for potato seed (about 3,400 coins) that cash couldn't cover, while radish seed costs about 50. The hand now picks the best crop he can pay for, and an order's own crop falls back to that pick. Verified in the Editor with touch at 2,000 coins: an every-season wheat order through the order sheet in the Monsoon was sown with rice at once, and Manoj walked straight to it (`docs/art/s10-hand-works-with-little-cash.png`). 271 `Sim.Core` and 70 `Sim.View` tests pass.

**The owner's follow-up: "compost doesn't improve the field" (ADR 0051).** Checked: the rules ran as specified, but compost's +2 a season was less than wheat, rice or potato take out (4–5), so a composted field still fell, and whole-number fertility hid every change. Approved fix: compost +6 a season; the soil under a growing composted crop dries 20% more slowly; fertility to one decimal; each sow row shows the soil before and after ("Soil 46.5 → 46.6", amber when it falls); the fertiliser chips say "+5% harvest · holds water" and "+25% harvest". Chemical unchanged. The gate's defaulter check now asks that every default is foreclosed (with richer soil the bot repays in some runs). Verified in the Editor: the sow sheet with compost and chemical, the moisture *Why?* ("Compost holds water: dries slower by 20%") and the fertility *Why?* in points. 272 `Sim.Core` and 70 `Sim.View` tests pass.

**Review (owner's device check, 2026-10-21):** passed on the iPhone 13 ("looks good"). Sprint 10 is closed when PR #15 is merged.

**Phase 2 planning started in the same session.** The owner's enhancement requests were decided one at a time and recorded in `STATUS.md` (Next up) for the Phase 2 ADRs and specs: a 16 × 12 map growing by era; the village layout with road acres and neighbours not for sale; the trader's 4 t truck (6:00 to midnight, a daily price, the stall paused at night); daily supermarket orders (fresh today, rice and potato in 15 h, the hand delivering first); and buffalo and cows (cut-and-carry green fodder, a straw stack, a feed room).

## Sprint 11 · 11–24 Jan 2027 · Phase 2 (planning, the village, the truck and orders; 2 weeks, ADR 0057)

- **Status:** The owner's device check passed (2026-10-22, frame time p95 5.37 ms). The device review's US-196–US-198 (ADR 0059) done and Editor-tested; follow-up Development build made (`s11b-device-review-sheets.md`); the owner's check and the PR merge next
- **Branch:** `sprint/11-phase2-planning`
- **Goal:** Phase 2's decisions recorded (ADRs 0052–0057), specs 010 and 011 approved, the Phase 2 sprints planned, and the village map, the trader's truck, daily prices and supermarket orders built and playable on the iPhone.
- **Points:** 42 (35 planned + 7 from the device review)
- **Specs for the owner to review:** 010 (approved 2026-10-21), 011 (approved 2026-10-21)
- **Device build:** Yes: Development build (`s11-village-truck-orders.md`)

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-188 | The 16 × 12 map and the village layout | [F02.4](features/F02.4-village-map.md) | 3 | Must | Done |
| US-189 | Save schema 5: remap tile ids, the farm lane takes `s5` | [F02.4](features/F02.4-village-map.md) | 5 | Must | Done |
| US-190 | The village on screen: camera, edge, places, neighbours' fields | [F02.4](features/F02.4-village-map.md) | 5 | Must | Done |
| US-191 | Village and edge art in Blender | [F02.4](features/F02.4-village-map.md) | 5 | Must | Done |
| US-192 | The truck's day, the daily price and the stall at night | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 5 | Must | Done |
| US-193 | Supermarket orders | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 5 | Must | Done |
| US-194 | Rice and potato in 15 hours; balance re-measured | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 2 | Must | Done |
| US-195 | Orders and truck on screen | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 5 | Must | Done |
| US-196 | One row per supermarket order (device review, ADR 0059) | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 1 | Must | Done |
| US-197 | The Store in sections (device review, ADR 0059) | [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | 3 | Must | Done |
| US-198 | A hand's field order starts with the sow sheet (device review, ADR 0059) | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | Done |

**Order of work:** US-188 → US-189 (the map and its migration) → US-192 → US-193 → US-194 (the sim) → US-191 → US-190 → US-195 (art and screens) → the device build.

**Sprint end checklist**

- [x] ADRs 0052–0057 written; GDD and `CLAUDE.md` updated
- [x] Specs 010 and 011 written, every gap closed with the owner, approved
- [x] Phase 2 sprints planned (this file), feature files F02.4 and F04.3 with stories and tasks; `epics.md` updated
- [x] All Must stories meet the Definition of Done (ADR 0025), Editor-tested with touch; [x] the owner's device check passed (US-190.T6 measured there, 2026-10-22)
- [x] Test checklists written in `docs/test-checklists/` (`s11-village-truck-orders.md`; follow-up `s11b-device-review-sheets.md`)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened; [ ] owner merges to `main`

**Done this sprint (Claude):**
- ADRs 0052–0057 (the map grows by era, the village layout, the truck, supermarket orders, cattle, the Phase 2 spec cadence); GDD and `CLAUDE.md` updated.
- Spec 010 (map, village and edge): 6 gaps closed with the owner (the for-sale layout; a full river-bank strip with the commons in its west half; `s5` paid at 2 × its price; soils and location by geography; the dealer and tappable places; neighbours' seasonal fields). Specs 002 and 008 amended.
- Spec 011 (the trader's truck and supermarket orders): 6 gaps closed (a daily price at 6:00; the market clears overnight; delivery at the collection point, paid at 6:00 next morning, shown in the Store; two crop orders plus a small greens order; the hand sells held produce at 6:00; the first sale at any hour, orders after the first session). Specs 003–007 and 009 amended.
- US-188–US-191 (the 16 × 12 village map, its migration, the village on screen, the Blender village art), US-192 (the truck, daily prices, the stall at night), US-193 (supermarket orders), US-194 (15-hour rice and potato; ADR 0058 balance: orders 0.5–1 acre's sowing, 14.8% of sales), US-195 (orders and the truck on screen; the truck and van in Blender). 303 + 80 tests; Editor tours with touch; Development build.

## Sprint 12 · 25–31 Jan 2027 · Phase 2 (cattle; spec 014 written)

- **Status:** Done. The owner's device check on the iPhone 13 passed (2026-10-24); PR #17 merged to `main`. Spec 012 approved 2026-10-22 (ADR 0060); US-126 done and approved by the owner (2026-10-22); US-206 added (ADR 0061); US-126 and US-199–US-202 done and approved; US-203 done and approved (2026-10-23); ADR 0064 the cattle acre (US-213) and US-204 done and approved; US-206 done and approved (ADR 0065; the dairy sheet restyled like the Orders and Store sheets); US-207 done and approved; US-205 done (owner to review on the device); the Development build made (owner), device check next
- **Branch:** `sprint/12-cattle-citizens`
- **Goal:** Spec 012 approved and built: buffalo and cows in a shed, milk to the co-op every morning, fodder, straw and feed, playable on the iPhone. Spec 014 (citizens, households and population) written and approved; its code comes in Sprints 13–14.
- **Points:** 49 (spec 012 re-pointed the ~15 estimate to 36; US-206 adds 8 (ADR 0061), US-207 5 (ADR 0062); runs 2 weeks as Sprint 11 did)
- **Specs for the owner to review:** 012 (approved 2026-10-22), 014
- **Device build:** Yes: Development build (`s12-cattle.md`)

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-126 | Raise animals: the shed, buying and selling, milk and the lactation cycle | [F13.1](features/F13.1-livestock.md) | 5 | Must | Done |
| US-199 | Feeding: the feed room, the straw stack, the compost pit, hunger and health | [F13.1](features/F13.1-livestock.md) | 5 | Must | Done |
| US-200 | Green fodder: berseem and sorghum cut daily, sold standing, the fodder order | [F13.1](features/F13.1-livestock.md) | 5 | Must | Done |
| US-201 | Health and help: vaccination, disease and the vet, the animals order | [F13.1](features/F13.1-livestock.md) | 3 | Must | Done |
| US-202 | The books: accounts, the dairy loan, foreclosure, save schema 7 | [F13.1](features/F13.1-livestock.md) | 3 | Must | Done |
| US-203 | Balance: a dairy bot and payback | [F13.1](features/F13.1-livestock.md) | 2 | Must | Done |
| US-213 | The cattle acre (ADR 0064) | [F13.1](features/F13.1-livestock.md) | 3 | Must | Done |
| US-204 | Cattle art in Blender | [F13.1](features/F13.1-livestock.md) | 5 | Must | Done |
| US-205 | Cattle on screen | [F13.1](features/F13.1-livestock.md) | 8 | Must | Done (passed the device check 2026-10-24) |
| US-206 | The village redrawn: a middle street, buildings from all sides, the dairy co-op (ADR 0061, 0065) | [F02.4](features/F02.4-village-map.md) | 8 | Must | Done |
| US-207 | A living valley: varied neighbours' land, farmsteads, your land at a glance (ADR 0062) | [F02.4](features/F02.4-village-map.md) | 5 | Must | Done |

**Order of work:** US-126 → US-199 → US-200 → US-201 → US-202 (the sim, each reviewed by the owner) → US-203 (balance) → US-204, US-206 and US-207 (art, the village redrawn, the valley) → US-205 (screens, Editor tour) → spec 014 written alongside, its gaps closed with the owner → the device build.

**Sprint end checklist**

- [x] Spec 012 written, every gap closed with the owner, approved (ADR 0060); GDD, `CLAUDE.md` and the amended specs updated
- [x] Spec 014 written, every gap closed with the owner, approved (ADR 0063); F11.1–F11.3 broken into stories and tasks (55 points: S13 39, S14 16)
- [x] All Must stories meet the Definition of Done (ADR 0025), Editor-tested with touch
- [x] Test checklist `docs/test-checklists/s12-cattle.md`; Development build (made by the owner 2026-10-23, checked: content identical, `UNITY_DEVELOPER_BUILD`, same bundle id); the owner's device check passed (2026-10-24)
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened with labels and the milestone; owner merged it to `main` (PR #17)

## Sprint 13 · 8–21 Feb 2027 · Phase 2 (citizens; specs 015 and 016 written; 2 weeks, as S12)

- **Status:** In progress. Before the sprint's stories, two owner requests (2026-10-24): fodder stands no longer keep labourers hired (US-214) and the People sheet redesigned like the Store and Orders sheets (US-215); the sprint's stories start once the owner approves both
- **Branch:** `sprint/13-citizens`
- **Goal:** Spec 014 built (ADR 0063): citizens and the life clock, the family house, homes and people art, household budgets, needs and happiness, work and food, your people on screen; save schema 9 with its migration and fixture. Specs 015 (full hiring and HR) and 016 (businesses and production) written and approved; their code comes later.
- **Points:** 39 planned + the owner's two requests
- **Specs for the owner to review:** 015, 016
- **Device build:** Yes: Development build

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-214 | Fodder stands don't keep labourers hired (owner, 2026-10-24) | [F06.1](features/F06.1-day-labourers.md) | 1 | Must | To do |
| US-215 | The People sheet like the Store and Orders sheets (owner, 2026-10-24) | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-105 | Citizens: the model, Phase 1 hands as citizens, save schema 9 | [F11.1](features/F11.1-citizens-and-life-clock.md) | 5 | Must | To do |
| US-106 | The life clock: ageing and retirement | [F11.1](features/F11.1-citizens-and-life-clock.md) | 3 | Must | To do |
| US-208 | The family house: build, rent, unlock and placement | [F11.1](features/F11.1-citizens-and-life-clock.md) | 5 | Must | To do |
| US-212 | Homes and people art in Blender | [F11.1](features/F11.1-citizens-and-life-clock.md) | 5 | Must | To do |
| US-107 | Household budgets: Engel's law, food met, savings | [F11.2](features/F11.2-households-needs-and-happiness.md) | 3 | Must | To do |
| US-108 | Needs and happiness | [F11.2](features/F11.2-households-needs-and-happiness.md) | 5 | Must | To do |
| US-209 | Work and food: outside work, your labourers first, the town's food demand | [F11.2](features/F11.2-households-needs-and-happiness.md) | 5 | Must | To do |
| US-210 | Your people on screen | [F11.2](features/F11.2-households-needs-and-happiness.md) | 8 | Must | To do |

**Order of work:** US-214 and US-215 (approved by the owner first) → US-105 → US-106 → US-107 → US-108 → US-209 (the sim, each reviewed by the owner) → US-208 and US-212 (the family house and its art; the layout previewed to the owner first) → US-210 (screens, Editor tour) → specs 015 and 016 written alongside, their gaps closed with the owner one per message → the device build.

**Sprint end checklist**

- [ ] US-214 and US-215 approved by the owner
- [ ] Specs 015 and 016 written, every gap closed with the owner, approved; GDD and `CLAUDE.md` updated
- [ ] All Must stories meet the Definition of Done (ADR 0025), Editor-tested with touch
- [ ] Save schema 9: migration, `saves/schema9` fixture, every older schema loads
- [ ] Test checklist in `docs/test-checklists/`; Development build
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened with labels and the milestone `Phase 2 · Hamlet & Village`; owner merges to `main`
