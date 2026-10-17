# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 0 in progress (planned for 19–25 Oct 2026) |
| Branch with latest work | `sprint/00-foundations-time-weather` (not yet merged to `main`) |
| Last updated | 2026-10-17 |

## Done so far

- The GDD has been reviewed and updated (v1.2 plus later edits).
- ADRs 0001–0027 are accepted.
- `CLAUDE.md` exists, with the session routine.
- The spec template exists.
- **Spec 001 (time and weather)** is approved.
- **SDLC planning is created and audited:**
  - `epics.md`: 21 epics, 79 features
  - `features/`: one file per feature, 176 user stories. Phases 0–1 are broken down into tasks
  - `sprints.md`: Sprints 0–9 for Phases 0–1
  - An automated check confirmed that no story is scheduled before its spec is approved or before a feature it depends on (ADR 0027)
- PR #3 is merged to `main`.
- **Sprint 0 progress:**
  - US-001 to US-004 (F00.1 repository, build and CI): done. CI is green on the sprint branch.
  - Spec 001: F01.1 sim foundations done (US-025 to US-028: `Fixed`, RNG, state hash, clock and weather content).
  - US-029 calendar and life clock, US-030 scheduler done. `dotnet build` and `dotnet test` pass; a C# 10 construct in `sim/` fails to compile (checked by hand: CS8773).

## In progress

- Sprint 0, on `sprint/00-foundations-time-weather`. Next: US-025 to US-037 (spec 001).

## Next up

1. **Owner:** merge pull request #3 into `main`.
2. **Next session: start Sprint 0** (see `sprints.md`), in this order:
   - US-001 to US-004: the solution skeleton, CI, the SessionStart hook and repository hygiene
   - US-025 to US-037: implement spec 001 (foundations, calendar, scheduler, weather, forecast, live and away time, time travel, performance guard)
   - US-005 and US-006: write **spec 002 (land and soil)** and **spec 005 (ledger and credit)** for owner review

## Owner action checklist (Phases 0–1)

| When | Owner action |
| --- | --- |
| Now | Merge pull request #3 |
| Sprint 0 | Review and approve specs 002 and 005. Merge the sprint pull request |
| Sprint 1 (start) | Install the latest Unity LTS with iOS Build Support. Install Git LFS (`brew install git-lfs`, then `git lfs install`). Create the 2D URP project at `unity/FarmToCity/` and push it |
| Sprint 1 | Review specs 003 and 004. Install the "hello iPhone" build on the iPhone 13 using `docs/test-checklists/how-to-build-ios.md` |
| Sprint 2 | Review specs 006 and 007 |
| Sprint 3 | Review specs 008 and 009. Run the performance spike and the on-device determinism check, then report the results (Phase 0 gate) |
| Sprints 4–8 | Install each sprint's build, work through its test checklist, and report the results |
| Sprint 9 | Run the playtest week with 3–5 testers, collect their play logs, and make the gate decision |

## Open questions (with the owner)

None. All design questions are closed (ADRs 0019–0027).

## Half-done work

None.

## Environment notes

- The SessionStart hook installs the .NET 10 SDK in cloud sessions (US-003). It runs synchronously, so the session starts only once the SDK is ready.
- Android isn't tested for now (ADR 0026). All device testing happens on the owner's iPhone 13.
