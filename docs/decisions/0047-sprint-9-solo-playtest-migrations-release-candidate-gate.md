# 0047. A solo playtest, migrations from the release candidate, the gate split in two (Sprint 9 planning review)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0041 §2 (migrations start now; `minSchema` frozen at 3); ADR 0024 (the play log's fields); ADR 0025 (Sprint 9 alone runs 2 weeks); the GDD's Phase 1 exit gate; spec 007 §3.1, §3.6, §8 (AC-3, AC-14, AC-15), §10; spec 009 §10
- Related: ADR 0007 (zero budget, free Apple account), ADR 0026 (iPhone 13, half frame budget), ADR 0028 (the Hamlet at the end of the first week), ADR 0035 (the spike's frame-time method), ADR 0038 (the developer panel in Development builds only), ADR 0045 (the Phase 1 health checks)
- GDD sections: Roadmap (phase gates), Telemetry

## Context

The Sprint 9 planning review (2026-10-21) checked F10.1 (US-095 release candidate, US-096 playtest week, US-097 tune and decide) against the GDD's Phase 1 gate ("testers come back on day 2 and day 3 unprompted"), ADR 0007, ADR 0024, ADR 0041 §2 and the code. Six gaps were decided one by one.

Facts that shaped them:
- **A free Apple Personal Team** registers at most 3 devices, and registrations and profiles expire after 7 days ([Apple: compare memberships](https://developer.apple.com/support/compare-memberships/)). Every phone must be plugged into the owner's Mac. TestFlight needs the paid program, which ADR 0007 rules out.
- **The play log** (spec 007 §3.6) records UTC times only, nothing about notifications, and no end of the first session.
- **The save reader** already passes each save's schema to every `ReadFrom` (`SaveReader.Schema`), but nothing tests a migration, and `minSchema` has risen with every schema so far.
- **The developer panel** exists only in Development builds (ADR 0038), and the spike's frame-time recorder was deleted with the spike. Nothing measures frame time now.

## Decisions

1. **The owner is the only playtester**, on the iPhone 13, signed with the free account (owner's choice). No other testers, no family check, no Android. US-096.T1 (recruit testers) is not needed.
2. **The play log gets three additions** (opt-in stays, ADR 0024):
   - `sessionStart` gets `"tz"` (the device's UTC offset in minutes), so days are the player's calendar days.
   - `sessionStart` gets `"fromNote"`: true when the app was opened by tapping a notification.
   - Each first-session step logs once per town as `{"type":"step","id":...}`, then `firstSessionDone` (`"skipped":true` after *I'll find my own way*).
   The owner turns the log on right after installing, before **New game**, and exports it (Settings → Export play log → Files → AirDrop) into `docs/playtest/`. Feedback is a one-line daily diary in the playtest checklist: opened today? why (a notification, something ready, wanted to, testing)? what was done? what felt slow or confusing? Claude reads the log by hand; no report tool for one tester.
3. **Save migrations start with the release candidate** (supersedes ADR 0041 §2's "until Sprint 9" policy):
   - `minSchema` stays **3** from now on (`MigrationTests.MinSchemaFrozen`). Refusing old saves again needs a new ADR.
   - Each schema has a **frozen fixture** in `tests/Sim.Core.Tests/saves/`. `schema3.ftc` is generated once from a seeded `FarmBot` town after 2 game seasons with people, a hand and hut, an order, a loan, the garden, trees, the guidance and the era progress. `schema3.json` holds its state hash and about 15 plain values. The owner's playtest town is exported at the end of the week and added as `schema3-device.ftc`.
   - `MigrationTests.EverySchemaLoads`: every fixture loads with the current content. While the schema is unchanged its hash matches. After a migration its plain values are unchanged and the new fields hold their documented defaults. Then the town advances a game day and survives a save round trip.
   - `MigrationTests.SchemaHasFixture`: CI fails if `SchemaVersion` has no fixture, so a schema change brings a fixture and a tested migration step.
   - Content tuning needs nothing. A removed or renamed content id fails a fixture with `UnknownContentId` until a migration maps it. A migrated save's command log replays only on its own build and content, as before.
4. **Two builds from the release-candidate commit:**
   - A **Development build** runs the full regression (one checklist, `s09-release-candidate.md`, merging the s04–s08 checks by area, each row citing its source) and the frame-time check. The developer panel gets **Measure frame time (20 s)**, read from `FrameTimingManager`: main thread avg / p95 / max, GPU avg / p95, frames over 25 ms, fps. The iOS build step turns on frame-timing stats for Development builds. Four views are measured: the yard close up, the whole map, a full-height sheet, people walking. The pass is ADR 0035's: main and GPU p95 ≤ 8.3 ms and under 1% of frames over 25 ms. A Development build is slower, so a pass there holds in Release.
   - A **Release build** is the playtest build, with a short smoke list: no developer panel, the Sprint 8 town loads, export it, Play log on, New game.
   - **A blocker** is any of: a crash or freeze; a lost, refused or unreadable save; a first-session step that can't be completed; a wrong money amount; a notification in quiet hours or with wrong text; a frame-time fail; a golden FAIL; a number with no *Why?*. Blockers are fixed before the playtest. Anything else is a finding, fixed if it's cheap or logged for US-097.
5. **The Phase 1 gate is split in two.** The developer playing their own game can't show whether new players return unprompted.
   - **Part A, the Phase 1 gate**, judged on the owner's solo week. Every item must pass:

     | # | Criterion | Pass |
     | --- | --- | --- |
     | A1 | A reason to come back each day | On days 2–7, at least 4 days with a session of 2 minutes or more, **and** on at least 3 of them the diary gives the game as the reason (a notification, something ready, "wanted to"), not testing. Day 1 is the local day of **New game** |
     | A2 | Notifications bring the player back at sensible times | At least 2 sessions opened from a notification (`fromNote`); none in quiet hours |
     | A3 | The Hamlet in about a week | `hamletUnlocked` on day 5–9 (ADR 0028) |
     | A4 | Nothing broke | No blocker in the 7 days; the device town loads as a migration fixture |
     | A5 | The release candidate passes | Regression and frame time (US-095) |
     | A6 | CI stays green | The balance gate and the Phase 1 health checks, re-run after any tuning |

     The first session's length is recorded, not gated. The owner's time is a lower bound, so the first session is tuned only if the diary names a step that dragged or confused. Spec 009 §9's target stays judged by the bot (AC-2).
   - **Part B, retention by fresh players** (US-185, backlog): 3 or more players new to the game come back on day 2 and day 3 unprompted, from play logs, with at least 2 of 3 (60%) to pass. It runs the first time fresh testers are possible, before Phase 5 · Launch at the latest, and stays an open item in `STATUS.md` until then.
   - **Fail path:** if any of A1–A4 fails, US-097 tunes content (data only, the balance gate re-run) or proposes a rule change for re-approval. The owner then replays the affected part (a fresh New game for 3 days if A1 or A3 failed) in a Phase 1 fixes sprint, and Phase 2's spec sprint waits. A second fail gets an ADR with the owner's call: go on with known issues, or keep fixing. A5 or A6 failing blocks the playtest itself and is fixed inside Sprint 9.
6. **Sprint 9 runs 2 weeks** (owner's choice, a one-off exception to ADR 0025's 1-week sprints): the release candidate, the 7-day playtest, tuning and the gate decision in one sprint and one pull request. Points: US-095 2 → **4** (+1 migration fixtures and tests, +1 frame-time recorder and build step); US-096 **3** (+1 play log fields and diary, −1 recruiting); US-097 **3**. Sprint 9 is **10 points**. One device build pair (Development and Release from one commit), plus a tuned Release build only if tuning changes something.

## Consequences

- **Spec 007:** §3.1's Versions row (migrations from schema 3; `minSchema` frozen), §3.6's entries (`tz`, `fromNote`, `step`, `firstSessionDone`), AC-3 reworded, AC-14 (migrations) and AC-15 (the new log fields) added, §10 notes.
- **Spec 009** §10 records that the first session's length is measured by the play log and not gated.
- **The GDD:** the Phase 1 exit gate (the roadmap chart and table) and the Telemetry paragraph.
- **Planning:** F10.1 acceptance and tasks rewritten, US-185 added (2 points, backlog); epics.md's Phase 1 gate; sprints.md's Sprint 9 (2 weeks, 10 points); CLAUDE.md's ADR table.
- **Risk accepted:** whether new players come back unprompted, the riskiest unknown for a Hay Day-like game, is untested until part B runs. Phases 2–4 build on a hook no fresh player has tried.
