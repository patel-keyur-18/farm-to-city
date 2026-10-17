# Sprint plan

> 1-week sprints (ADR 0025). Each sprint: at most 2 specs for the owner to review and 1 device build; ends with a test checklist (when there is a build) and one pull request that the owner merges. Dates are planned and shift if a sprint carries over. Phases 2–5 are planned in detail at the start of each phase (ADR 0019).
>
> **Notes:** Story points show relative size only; the real capacity limit is the owner's review and device-testing time. Phase 1 features start in Phase 0 sprints as soon as their spec is approved, because most don't need Unity. Phase 0's exit gate is still checked at the end of Sprint 3. Corrections from the pre-implementation audit are recorded in ADR 0027.

## Overview

| Sprint | Dates (planned) | Phase | Goal | Points | Specs to review | Device build |
| --- | --- | --- | --- | --- | --- | --- |
| S0 | 19–25 Oct 2026 | 0 | Foundations in place and spec 001 fully implemented and tested from the terminal; specs 002 (land) and 005 (ledger and credit) written. | 44 | 002, 005 | No (no Unity project yet) |
| S1 | 26 Oct – 1 Nov 2026 | 0 | Ledger core and land implemented; Unity project created and a "hello iPhone" build running; specs 003 and 004 written. | 28 | 003, 004 | Yes: hello iPhone (date, time, weather) |
| S2 | 2–8 Nov 2026 | 0 | Crops, fruit trees, founder labour and accrual accounting implemented; economy model running with anchor numbers; specs 006 and 007 written. | 50 | 006, 007 | No |
| S3 | 9–15 Nov 2026 | 0 | Market and storage implemented; performance spike, on-device determinism and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review. | 45 | 008, 009 | Yes: spike and determinism build |
| S4 | 16–22 Nov 2026 | 1 | Credit and foreclosure; first playable build: the homestead map with sow and harvest, string tables and number formatting, and the dev panel. | 23 | — | Yes: first playable |
| S5 | 23–29 Nov 2026 | 1 | People: day labourers, the resident hand and standing orders, with their UI. | 19 | — | Yes |
| S6 | 30 Nov – 6 Dec 2026 | 1 | Saves, notifications and the play log; market, store and finance basics on screen. | 16 | — | Yes |
| S7 | 7–13 Dec 2026 | 1 | Full HUD, panels with Why?, While-you-were-away and settings. | 18 | — | Yes |
| S8 | 14–20 Dec 2026 | 1 | Onboarding: first session, letter chapters and the Hamlet unlock; Phase 1 balancer bots and health checks. | 21 | — | Yes: release candidate |
| S9 | 21–27 Dec 2026 | 1 | Playtest week, tuning and the Phase 1 gate review. | 8 | — | Yes: playtest build |
| S10+ | From 28 Dec 2026 | 2–5 | Planned at each phase start: first sprint(s) write that phase's specs, then implementation by feature priority | — | 2 per sprint | Yes |

## Sprint 0 · 19–25 Oct 2026 · Phase 0

- **Status:** In review (started and coded 2026-10-17). All code stories are done; specs 002 and 005 await the owner's approval
- **Branch:** `sprint/00-foundations-time-weather`
- **Done:** 38 of 44 points. US-005 and US-006 (6 points) are written and in review
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
| US-005 | Spec 002: land and soil | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | In review |
| US-006 | Spec 005: ledger and credit | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | In review |
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

- [ ] All Must stories meet the Definition of Done (ADR 0025). Code stories: yes. US-005 and US-006: waiting for the owner's approval
- [x] Test checklist written in `docs/test-checklists/` (if there is a build): no build this sprint
- [x] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** No device build this sprint. Terminal results: 72 tests pass in Debug and Release; CI green on every push; all 29 spec 001 acceptance criteria have their named tests.

**Retro (Claude's draft, owner to amend):**
- Went well: asking the four spec 001 questions up front kept coding unblocked, and pinning the RNG and hash golden values from an independent Python implementation verified them on the first run.
- Improve: NUnit 4.6 needs C# 10+ in tests, which ADR 0018 didn't foresee (now noted in ADR 0018's status line). Golden replay hashes moved three times during the sprint; later specs should add their state before pinning their golden files.

## Sprint 1 · 26 Oct – 1 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/01-ledger-land-unity` (or the branch the session assigns)
- **Goal:** Ledger core and land implemented; Unity project created and a "hello iPhone" build running; specs 003 and 004 written.
- **Points:** 28
- **Specs for the owner to review:** 003, 004
- **Device build:** Yes: hello iPhone (date, time, weather)
- Owner: install Unity (latest LTS) and Git LFS, and create the Unity project at the start of the sprint.
- This de-risks Unity + Xcode 27.1 + free signing early (ADR 0027).

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-007 | Spec 003: crops, fruit trees and founder labour | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-008 | Spec 004: goods, market and storage | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-017 | Create the Unity project | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | To do |
| US-018 | Pin the Unity version | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | To do |
| US-019 | Code-first bootstrap | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 3 | Must | To do |
| US-020 | Build-and-install guide | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 2 | Must | To do |
| US-038 | Inherited land | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | To do |
| US-039 | Soil properties | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | To do |
| US-040 | Buy adjacent land | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | To do |
| US-041 | Tree slots | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | To do |
| US-059 | Exact ledger | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 5 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 2 · 2–8 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/02-crops-trees-founder` (or the branch the session assigns)
- **Goal:** Crops, fruit trees, founder labour and accrual accounting implemented; economy model running with anchor numbers; specs 006 and 007 written.
- **Points:** 50
- **Specs for the owner to review:** 006, 007
- **Device build:** No
- Economy model v0 prints seasonal cash flow from content data.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-009 | Spec 006: people | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-010 | Spec 007: saves, notifications and play log | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-013 | Seasonal farm cash-flow model | [F00.3](features/F00.3-economy-model.md) | 5 | Must | To do |
| US-014 | Anchor numbers in content | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-042 | Sow in season | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-043 | Growth and ready to harvest | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-044 | Yield formula | [F02.2](features/F02.2-crops-and-yield.md) | 5 | Must | To do |
| US-045 | Seeds and fertiliser | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-047 | Plant a sapling | [F02.3](features/F02.3-fruit-trees.md) | 2 | Must | To do |
| US-048 | Maturity and fruiting | [F02.3](features/F02.3-fruit-trees.md) | 3 | Must | To do |
| US-050 | Labour capacity | [F03.1](features/F03.1-founder-labour-and-routine.md) | 3 | Must | To do |
| US-051 | Household plan | [F03.1](features/F03.1-founder-labour-and-routine.md) | 3 | Must | To do |
| US-053 | Day labour and subsistence | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Must | To do |
| US-060 | Accrual and prepaid costs | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |
| US-061 | Season close and P&L | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |
| US-062 | Net worth | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 3 · 9–15 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/03-market-spike-gate` (or the branch the session assigns)
- **Goal:** Market and storage implemented; performance spike, on-device determinism and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review.
- **Points:** 45
- **Specs for the owner to review:** 008, 009
- **Device build:** Yes: spike and determinism build
- Phase 0 gate: money-loop checks pass, the spike fits 8.3 ms, and golden replays match on the iPhone.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-011 | Spec 008: HUD, presentation and localisation | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-012 | Spec 009: onboarding | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-015 | Money-loop checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-016 | Viability checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-021 | 2,000-agent spike | [F00.5](features/F00.5-performance-spike.md) | 5 | Must | To do |
| US-022 | Record the result | [F00.5](features/F00.5-performance-spike.md) | 1 | Must | To do |
| US-023 | On-device determinism | [F00.5](features/F00.5-performance-spike.md) | 3 | Must | To do |
| US-024 | CC0 style check | [F00.6](features/F00.6-art-style-check.md) | 2 | Should | To do |
| US-046 | Pests and spraying | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Should | To do |
| US-049 | Tree value and payback | [F02.3](features/F02.3-fruit-trees.md) | 2 | Should | To do |
| US-052 | Health and sick days | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Should | To do |
| US-054 | Goods and regional prices | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | To do |
| US-055 | Hourly clearing | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 5 | Must | To do |
| US-056 | Sell and buy | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | To do |
| US-057 | Store and capacity | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | To do |
| US-058 | Spoilage | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 4 · 16–22 Nov 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/04-credit-first-playable` (or the branch the session assigns)
- **Goal:** Credit and foreclosure; first playable build: the homestead map with sow and harvest, string tables and number formatting, and the dev panel.
- **Points:** 23
- **Specs for the owner to review:** —
- **Device build:** Yes: first playable

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-063 | Borrow | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-064 | Repayments and penalties | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-065 | Foreclosure | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-078 | Homestead map | [F08.1](features/F08.1-map-and-field-interaction.md) | 5 | Must | To do |
| US-079 | Tap to act | [F08.1](features/F08.1-map-and-field-interaction.md) | 3 | Must | To do |
| US-085 | String tables | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | To do |
| US-086 | Number formatting | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | To do |
| US-088 | Dev panel | [F08.4](features/F08.4-developer-panel-unity.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 5 · 23–29 Nov 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/05-people` (or the branch the session assigns)
- **Goal:** People: day labourers, the resident hand and standing orders, with their UI.
- **Points:** 19
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-066 | Hire a day labourer | [F06.1](features/F06.1-day-labourers.md) | 3 | Must | To do |
| US-067 | Regulars | [F06.1](features/F06.1-day-labourers.md) | 2 | Should | To do |
| US-068 | Build a hand's hut | [F06.2](features/F06.2-resident-farm-hand.md) | 2 | Must | To do |
| US-069 | Hire a resident hand | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | To do |
| US-070 | Season contract | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | To do |
| US-071 | Define standing orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | To do |
| US-072 | Execute orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 6 · 30 Nov – 6 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/06-saves-notifications` (or the branch the session assigns)
- **Goal:** Saves, notifications and the play log; market, store and finance basics on screen.
- **Points:** 16
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-073 | Serialise the game | [F07.1](features/F07.1-save-system.md) | 5 | Must | To do |
| US-074 | Safety save points | [F07.1](features/F07.1-save-system.md) | 3 | Must | To do |
| US-075 | Save now, export, import, new game | [F07.1](features/F07.1-save-system.md) | 3 | Must | To do |
| US-076 | Ready notifications | [F07.2](features/F07.2-local-notifications.md) | 3 | Must | To do |
| US-077 | Record and export | [F07.3](features/F07.3-local-play-log.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 7 · 7–13 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/07-hud-panels` (or the branch the session assigns)
- **Goal:** Full HUD, panels with Why?, While-you-were-away and settings.
- **Points:** 18
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-080 | HUD | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-081 | Why? breakdowns | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-082 | Panels | [F08.2](features/F08.2-hud-and-panels.md) | 5 | Must | To do |
| US-083 | While you were away | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-084 | Settings | [F08.2](features/F08.2-hud-and-panels.md) | 2 | Must | To do |
| US-087 | Feedback sounds | [F08.5](features/F08.5-basic-feedback-sounds.md) | 2 | Could | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 8 · 14–20 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/08-onboarding-hamlet` (or the branch the session assigns)
- **Goal:** Onboarding: first session, letter chapters and the Hamlet unlock; Phase 1 balancer bots and health checks.
- **Points:** 21
- **Specs for the owner to review:** —
- **Device build:** Yes: release candidate

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-089 | The letter | [F09.1](features/F09.1-first-session.md) | 2 | Must | To do |
| US-090 | Guided first actions | [F09.1](features/F09.1-first-session.md) | 5 | Must | To do |
| US-091 | Guided-once triggers | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | To do |
| US-092 | Chapters | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | To do |
| US-093 | Era unlock check | [F09.3](features/F09.3-hamlet-unlock.md) | 3 | Must | To do |
| US-094 | Hamlet celebration | [F09.3](features/F09.3-hamlet-unlock.md) | 2 | Must | To do |
| US-098 | Phase 1 bots and checks | [F20.1](features/F20.1-balancer-and-health-checks.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 9 · 21–27 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/09-playtest` (or the branch the session assigns)
- **Goal:** Playtest week, tuning and the Phase 1 gate review.
- **Points:** 8
- **Specs for the owner to review:** —
- **Device build:** Yes: playtest build
- Gate: testers come back on day 2 and day 3 unprompted (from play logs).

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-095 | Release candidate | [F10.1](features/F10.1-playtest-and-tuning.md) | 2 | Must | To do |
| US-096 | Playtest week | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | To do |
| US-097 | Tune and decide | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [x] Pull request opened (#4); owner merges to `main`

**Review (owner's device results):** —

**Retro:** —
