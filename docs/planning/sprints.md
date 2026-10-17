# Sprint plan

> 1-week sprints (ADR 0025). Each sprint: at most 2 specs for the owner to review and 1 device build; ends with a test checklist (when there is a build) and one pull request that the owner merges. Dates are planned and shift if a sprint carries over. Phases 2–5 are planned in detail at the start of each phase (ADR 0019).
>
> **Notes:** Story points show relative size only. The real capacity limit is the owner's review and device-testing time. Phase 1 features (E01–E04) start in Phase 0 sprints as soon as their spec is approved, because they don't need Unity. Phase 0's exit gate is still checked at the end of Sprint 3.

## Overview

| Sprint | Dates (planned) | Phase | Goal | Points | Specs to review | Device build |
| --- | --- | --- | --- | --- | --- | --- |
| S0 | 19–25 Oct 2026 | 0 | Foundations in place and spec 001 fully implemented and tested from the terminal; specs 002 and 003 written. | 44 | 002, 003 | No (no Unity project yet) |
| S1 | 26 Oct – 1 Nov 2026 | 0 | Land and soil implemented; economy model running; specs 004 and 005 written. | 21 | 004, 005 | No |
| S2 | 2–8 Nov 2026 | 0 | Crops, fruit trees and founder labour implemented; anchor numbers in content; specs 006 and 007 written. | 43 | 006, 007 | No |
| S3 | 9–15 Nov 2026 | 0 | Market and storage implemented; Unity project created; performance spike and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review. | 42 | 008, 009 | Yes: performance spike build |
| S4 | 16–22 Nov 2026 | 1 | Ledger implemented; first playable build: the homestead map with sow and harvest, and the dev panel. | 24 | — | Yes: first playable |
| S5 | 23–29 Nov 2026 | 1 | Credit and foreclosure; people (day labourers, resident hand, standing orders); string tables and number formatting. | 32 | — | Yes |
| S6 | 30 Nov – 6 Dec 2026 | 1 | Saves, notifications and the play log; market, store and finance basics on screen. | 16 | — | Yes |
| S7 | 7–13 Dec 2026 | 1 | Full HUD, panels with Why?, While-you-were-away and settings. | 16 | — | Yes |
| S8 | 14–20 Dec 2026 | 1 | Onboarding: first session and letter chapters; Phase 1 balancer bots and health checks. | 16 | — | Yes: release candidate |
| S9 | 21–27 Dec 2026 | 1 | Playtest week, tuning and the Phase 1 gate review. | 8 | — | Yes: playtest build |
| S10+ | From 28 Dec 2026 | 2–5 | Planned at each phase start: first sprint(s) write that phase's specs, then implementation by feature priority | — | 2 per sprint | Yes |

## Sprint 0 · 19–25 Oct 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/00-foundations-time-weather` (or the branch the session assigns)
- **Goal:** Foundations in place and spec 001 fully implemented and tested from the terminal; specs 002 and 003 written.
- **Points:** 44
- **Specs for the owner to review:** 002, 003
- **Device build:** No (no Unity project yet)
- Owner: review and approve specs 002 and 003.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-001 | Solution skeleton | [F00.1](features/F00.1-repository-build-and-ci.md) | 3 | Must | To do |
| US-002 | Continuous integration | [F00.1](features/F00.1-repository-build-and-ci.md) | 2 | Must | To do |
| US-003 | SessionStart hook | [F00.1](features/F00.1-repository-build-and-ci.md) | 1 | Must | To do |
| US-004 | Repository hygiene | [F00.1](features/F00.1-repository-build-and-ci.md) | 1 | Must | To do |
| US-005 | Spec 002: land and soil | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-006 | Spec 003: crops, fruit trees and founder labour | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-024 | Fixed-point numbers | [F01.1](features/F01.1-sim-foundations.md) | 3 | Must | To do |
| US-025 | Counter-based RNG | [F01.1](features/F01.1-sim-foundations.md) | 2 | Must | To do |
| US-026 | State hash | [F01.1](features/F01.1-sim-foundations.md) | 1 | Must | To do |
| US-027 | Clock and weather content | [F01.1](features/F01.1-sim-foundations.md) | 3 | Must | To do |
| US-028 | Calendar and life clock | [F01.2](features/F01.2-calendar-and-scheduler.md) | 2 | Must | To do |
| US-029 | Scheduler | [F01.2](features/F01.2-calendar-and-scheduler.md) | 5 | Must | To do |
| US-030 | Commands and replay | [F01.2](features/F01.2-calendar-and-scheduler.md) | 3 | Must | To do |
| US-031 | Weather spells | [F01.3](features/F01.3-weather-and-forecast.md) | 3 | Must | To do |
| US-032 | Forecast | [F01.3](features/F01.3-weather-and-forecast.md) | 3 | Must | To do |
| US-033 | Live, away and anomalies | [F01.4](features/F01.4-live-and-away-time.md) | 3 | Must | To do |
| US-034 | Upcoming jobs for notifications | [F01.4](features/F01.4-live-and-away-time.md) | 1 | Must | To do |
| US-035 | Time-travel commands | [F01.5](features/F01.5-developer-time-travel-sim.md) | 1 | Must | To do |
| US-036 | Performance guard | [F01.5](features/F01.5-developer-time-travel-sim.md) | 1 | Should | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 1 · 26 Oct – 1 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/01-land-soil` (or the branch the session assigns)
- **Goal:** Land and soil implemented; economy model running; specs 004 and 005 written.
- **Points:** 21
- **Specs for the owner to review:** 004, 005
- **Device build:** No
- Economy model v0 prints seasonal cash flow from content data.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-007 | Spec 004: goods, market and storage | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-008 | Spec 005: ledger and credit | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-013 | Seasonal farm cash-flow model | [F00.3](features/F00.3-economy-model.md) | 5 | Must | To do |
| US-037 | Inherited land | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | To do |
| US-038 | Soil properties | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | To do |
| US-039 | Buy adjacent land | [F02.1](features/F02.1-land-and-soil.md) | 3 | Must | To do |
| US-040 | Tree slots | [F02.1](features/F02.1-land-and-soil.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 2 · 2–8 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/02-crops-trees-founder` (or the branch the session assigns)
- **Goal:** Crops, fruit trees and founder labour implemented; anchor numbers in content; specs 006 and 007 written.
- **Points:** 43
- **Specs for the owner to review:** 006, 007
- **Device build:** No

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-009 | Spec 006: people | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-010 | Spec 007: saves, notifications and play log | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-014 | Anchor numbers in content | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-041 | Sow in season | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-042 | Growth and ready to harvest | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-043 | Yield formula | [F02.2](features/F02.2-crops-and-yield.md) | 5 | Must | To do |
| US-044 | Seeds and fertiliser | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Must | To do |
| US-045 | Pests and spraying | [F02.2](features/F02.2-crops-and-yield.md) | 3 | Should | To do |
| US-046 | Plant a sapling | [F02.3](features/F02.3-fruit-trees.md) | 2 | Must | To do |
| US-047 | Maturity and fruiting | [F02.3](features/F02.3-fruit-trees.md) | 3 | Must | To do |
| US-048 | Tree value and payback | [F02.3](features/F02.3-fruit-trees.md) | 2 | Should | To do |
| US-049 | Labour capacity | [F03.1](features/F03.1-founder-labour-and-routine.md) | 3 | Must | To do |
| US-050 | Household plan | [F03.1](features/F03.1-founder-labour-and-routine.md) | 3 | Must | To do |
| US-051 | Health and sick days | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Should | To do |
| US-052 | Day labour and subsistence | [F03.1](features/F03.1-founder-labour-and-routine.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 3 · 9–15 Nov 2026 · Phase 0

- **Status:** Planned
- **Branch:** `sprint/03-market-unity-spike` (or the branch the session assigns)
- **Goal:** Market and storage implemented; Unity project created; performance spike and art check on the iPhone 13; specs 008 and 009 written; Phase 0 gate review.
- **Points:** 42
- **Specs for the owner to review:** 008, 009
- **Device build:** Yes: performance spike build
- Owner: create the Unity project (US in F00.4) at the start of the sprint.
- Phase 0 gate: money-loop checks pass and the spike fits 8.3 ms.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-011 | Spec 008: HUD, presentation and localisation | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-012 | Spec 009: onboarding | [F00.2](features/F00.2-phase-1-feature-specs.md) | 3 | Must | To do |
| US-015 | Money-loop checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-016 | Viability checks | [F00.3](features/F00.3-economy-model.md) | 3 | Must | To do |
| US-017 | Create the Unity project | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | To do |
| US-018 | Pin the Unity version | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 1 | Must | To do |
| US-019 | Code-first bootstrap | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 3 | Must | To do |
| US-020 | Build-and-install guide | [F00.4](features/F00.4-unity-project-and-bootstrap.md) | 2 | Must | To do |
| US-021 | 2,000-agent spike | [F00.5](features/F00.5-performance-spike.md) | 5 | Must | To do |
| US-022 | Record the result | [F00.5](features/F00.5-performance-spike.md) | 1 | Must | To do |
| US-023 | CC0 style check | [F00.6](features/F00.6-art-style-check.md) | 2 | Should | To do |
| US-053 | Goods and regional prices | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | To do |
| US-054 | Hourly clearing | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 5 | Must | To do |
| US-055 | Sell and buy | [F04.1](features/F04.1-goods-market-and-regional-trader.md) | 3 | Must | To do |
| US-056 | Store and capacity | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | To do |
| US-057 | Spoilage | [F04.2](features/F04.2-hut-store-and-spoilage.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 4 · 16–22 Nov 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/04-ledger-first-playable` (or the branch the session assigns)
- **Goal:** Ledger implemented; first playable build: the homestead map with sow and harvest, and the dev panel.
- **Points:** 24
- **Specs for the owner to review:** —
- **Device build:** Yes: first playable

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-058 | Exact ledger | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 5 | Must | To do |
| US-059 | Accrual and prepaid costs | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |
| US-060 | Season close and P&L | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |
| US-061 | Net worth | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | 3 | Must | To do |
| US-077 | Homestead map | [F08.1](features/F08.1-map-and-field-interaction.md) | 5 | Must | To do |
| US-078 | Tap to act | [F08.1](features/F08.1-map-and-field-interaction.md) | 3 | Must | To do |
| US-086 | Dev panel | [F08.4](features/F08.4-developer-panel-unity.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 5 · 23–29 Nov 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/05-credit-people` (or the branch the session assigns)
- **Goal:** Credit and foreclosure; people (day labourers, resident hand, standing orders); string tables and number formatting.
- **Points:** 32
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-062 | Borrow | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-063 | Repayments and penalties | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-064 | Foreclosure | [F05.2](features/F05.2-credit-and-foreclosure.md) | 3 | Must | To do |
| US-065 | Hire a day labourer | [F06.1](features/F06.1-day-labourers.md) | 3 | Must | To do |
| US-066 | Regulars | [F06.1](features/F06.1-day-labourers.md) | 2 | Should | To do |
| US-067 | Build a hand's hut | [F06.2](features/F06.2-resident-farm-hand.md) | 2 | Must | To do |
| US-068 | Hire a resident hand | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | To do |
| US-069 | Season contract | [F06.2](features/F06.2-resident-farm-hand.md) | 3 | Must | To do |
| US-070 | Define standing orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | To do |
| US-071 | Execute orders | [F06.3](features/F06.3-standing-orders.md) | 3 | Must | To do |
| US-084 | String tables | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | To do |
| US-085 | Number formatting | [F08.3](features/F08.3-localisation-and-formatting.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

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
| US-072 | Serialise the game | [F07.1](features/F07.1-save-system.md) | 5 | Must | To do |
| US-073 | Safety save points | [F07.1](features/F07.1-save-system.md) | 3 | Must | To do |
| US-074 | Save now, export, import, new game | [F07.1](features/F07.1-save-system.md) | 3 | Must | To do |
| US-075 | Ready notifications | [F07.2](features/F07.2-local-notifications.md) | 3 | Must | To do |
| US-076 | Record and export | [F07.3](features/F07.3-local-play-log.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 7 · 7–13 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/07-hud-panels` (or the branch the session assigns)
- **Goal:** Full HUD, panels with Why?, While-you-were-away and settings.
- **Points:** 16
- **Specs for the owner to review:** —
- **Device build:** Yes

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-079 | HUD | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-080 | Why? breakdowns | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-081 | Panels | [F08.2](features/F08.2-hud-and-panels.md) | 5 | Must | To do |
| US-082 | While you were away | [F08.2](features/F08.2-hud-and-panels.md) | 3 | Must | To do |
| US-083 | Settings | [F08.2](features/F08.2-hud-and-panels.md) | 2 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —

## Sprint 8 · 14–20 Dec 2026 · Phase 1

- **Status:** Planned
- **Branch:** `sprint/08-onboarding-rc` (or the branch the session assigns)
- **Goal:** Onboarding: first session and letter chapters; Phase 1 balancer bots and health checks.
- **Points:** 16
- **Specs for the owner to review:** —
- **Device build:** Yes: release candidate

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-087 | The letter | [F09.1](features/F09.1-first-session.md) | 2 | Must | To do |
| US-088 | Guided first actions | [F09.1](features/F09.1-first-session.md) | 5 | Must | To do |
| US-089 | Guided-once triggers | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | To do |
| US-090 | Chapters | [F09.2](features/F09.2-letter-chapters.md) | 3 | Must | To do |
| US-094 | Phase 1 bots and checks | [F20.1](features/F20.1-balancer-and-health-checks.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

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
| US-091 | Release candidate | [F10.1](features/F10.1-playtest-and-tuning.md) | 2 | Must | To do |
| US-092 | Playtest week | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | To do |
| US-093 | Tune and decide | [F10.1](features/F10.1-playtest-and-tuning.md) | 3 | Must | To do |

**Sprint end checklist**

- [ ] All Must stories meet the Definition of Done (ADR 0025)
- [ ] Test checklist written in `docs/test-checklists/` (if there is a build)
- [ ] `STATUS.md`, feature files and this file updated
- [ ] Pull request opened; owner merges to `main`

**Review (owner's device results):** —

**Retro:** —
