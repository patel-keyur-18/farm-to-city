# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 0 is next (planned for 19–25 Oct 2026) |
| Branch with latest work | `claude/gifted-wright-v54560`. Must be merged to `main` before the next session starts |
| Last updated | 2026-10-17 |

## Done so far

- The GDD has been reviewed and updated (v1.2 plus later edits).
- ADRs 0001–0026 are accepted.
- `CLAUDE.md` exists, with the session routine.
- The spec template exists.
- **Spec 001 (time and weather)** is approved.
- **SDLC planning is created:**
  - `epics.md`: 21 epics, 66 features
  - `features/`: one file per feature, 151 user stories. Phases 0–1 are broken down into tasks
  - `sprints.md`: Sprints 0–9 for Phases 0–1; later phases are planned at their start
- No code has been written yet.

## In progress

None.

## Next up

1. **Owner:** review the planning files, then merge the pull request from `claude/gifted-wright-v54560` into `main`.
2. **Next session, Sprint 0** (see `sprints.md`), in this order:
   - US-001 to US-004: the skeleton, CI, the SessionStart hook and repository hygiene
   - US-024 to US-036: implement spec 001
   - US-005 and US-006: write specs 002 and 003 for owner review

## Open questions (with the owner)

None. The final GDD review closed these:

| # | Question | Decision |
| --- | --- | --- |
| 1 | When feature specs are written | Rolling wave (ADR 0019) |
| 2 | Seasons vs real-time crops; sowing windows; fruit trees | Indian crop calendar, fruit trees (ADR 0021) |
| 3 | Hiring before Hamlet | Day labourers and a resident hand from Homestead (ADR 0022) |
| 4 | Save slots vs undoing mistakes | One town; safety save points; no undo (ADR 0023) |
| 5 | Phase 1 scope additions | Hut store and spoilage, localisation groundwork, play log (ADR 0024) |
| 6 | Sprint mechanics | 1-week sprints, story points, MoSCoW, DoR and DoD (ADR 0025) |
| 7 | Test devices and versions | iPhone 13, M2 Air with Xcode 27.1, latest LTS tools (ADR 0026) |

## Half-done work

None.

## Environment notes

- The .NET 10 SDK isn't preinstalled in cloud sessions. Install it with `apt-get install -y dotnet-sdk-10.0` until the SessionStart hook exists (US-003, Sprint 0).
- Android isn't tested for now (ADR 0026). All device testing happens on the owner's iPhone 13.
