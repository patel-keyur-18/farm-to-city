# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Planning (before Sprint 0) |
| Branch with latest work | `claude/gifted-wright-v54560`. Must be merged to `main` before the next session starts |
| Last updated | 2026-10-17 |

## Done so far

- The GDD has been reviewed and updated to v1.2.
- ADRs 0001–0020 are accepted.
- `CLAUDE.md` and the spec template exist.
- **Spec 001 (time and weather)** is approved. No code has been written yet.

## In progress

- **Final GDD review.** The open questions below are being resolved with the owner, one at a time.
- **SDLC planning files** (`epics.md`, `features/`, `sprints.md`) are created once those questions are settled.

## Next up

1. Resolve the open questions below and record each answer as an ADR or GDD change.
2. Create `docs/planning/epics.md`, `docs/planning/features/*.md` and `docs/planning/sprints.md`.
3. Open a pull request to `main`. The owner merges it.
4. **Sprint 0** (next session): write specs 002–009, build the economy model, add the SessionStart hook, and create the solution skeleton.

## Open questions (with the owner)

| # | Question | Status |
| --- | --- | --- |
| 1 | When feature specs are written | Decided: rolling wave (ADR 0019) |
| 2 | Seasons table vs real-time crops; sowing windows (plus fruit trees) | Decided (ADR 0021) |
| 3 | Hiring before Hamlet: who the first worker is and where they live | Decided (ADR 0022) |
| 4 | Save slots vs undoing mistakes | Open |
| 5 | Phase 1 scope additions: storage and spoilage, localisation groundwork | Open |
| 6 | Sprint mechanics: length, estimation, definition of done, merging | Open |
| 7 | Owner's test devices and the Unity version | Open |

## Half-done work

None.

## Environment notes

- The .NET 8 SDK isn't preinstalled in cloud sessions. Install it with `apt-get install -y dotnet-sdk-8.0` until the SessionStart hook exists (Sprint 0).
