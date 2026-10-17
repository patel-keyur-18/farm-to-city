# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 0 in review: all code stories done; specs 002 and 005 await the owner's approval |
| Branch with latest work | `sprint/00-foundations-time-weather` (Sprint 0 pull request). Must be merged to `main` before the next session starts |
| Last updated | 2026-10-17 |

## Done so far

- **Before Sprint 0:** GDD reviewed (v1.2 plus later edits); ADRs 0001–0027 accepted; `CLAUDE.md` with the session routine; spec template; spec 001 approved; SDLC planning created and audited (21 epics, 79 features, 176 stories, Sprints 0–9). PR #3 merged.
- **Sprint 0 (branch `sprint/00-foundations-time-weather`):**
  - **F00.1 repository, build and CI (US-001 to US-004): done.**
    - `FarmToCity.sln`, `Directory.Build.props` (C# 9, warnings as errors, output to `/artifacts`), the `sim/` UPM package with `Sim.Core` and `Sim.Content` (`.csproj` + `.asmdef` with `noEngineReferences`), `tests/Sim.Core.Tests` (NUnit 4.6.1), `tools/Tools.Balancer`, `content/`.
    - CI (`.github/workflows/ci.yml`) builds and tests in Release on every push and pull request; green on every push this sprint.
    - The SessionStart hook (`.claude/hooks/session-start.sh`) installs the .NET 10 SDK and restores packages in cloud sessions.
    - `.gitignore` re-rooted for Unity, `ASSETS_LICENSES.md`, the device test checklist template.
  - **Spec 001 fully implemented (US-025 to US-037, F01.1 to F01.5): done.**
    - All 29 acceptance criteria have their named tests, passing; 72 tests in total.
    - `Fixed` (Q31.32), the counter-based SplitMix64 RNG, the FNV-1a state hash, clock and weather content with a validating loader, the calendar and life clock, the scheduler and settlement passes, commands and replay, weather spells and the forecast, `Sync` with the away cap and clock anomalies, upcoming jobs for notifications, `DebugAdvance`, and the performance guard.
    - Measured: a 72 h advance takes about 0.2 ms and a game year about 1.2 ms (guards: 20 ms and 100 ms).
    - The golden replay `tests/Sim.Core.Tests/golden/001-basic.json` covers every spec 001 command and is **frozen** (hash `0x0A11B9AED3656FC1`). Re-pin it with `FTC_UPDATE_GOLDEN=1 dotnet test` only when a later spec deliberately changes the state hash, and say so in the commit.
    - `dotnet run --project tools/Tools.Balancer [years] [seed]` prints spec 001's balancing hooks.
    - Implementation clarifications (four owner decisions and two implementation choices) are recorded in spec 001 §10. ADR 0018's status line notes the SplitMix64 RNG and that C# 9 applies to `sim/` only.
  - **US-005 spec 002 (land and soil) and US-006 spec 005 (ledger and credit): written, in review.** The GDD is updated to match both drafts.

## In progress

- Specs 002 and 005 are waiting for the owner's review. Their open questions are listed below.

## Next up

1. **Owner:**
   - Review spec 002 (`docs/specs/002-land-and-soil.md`) and spec 005 (`docs/specs/005-ledger-and-credit.md`), and answer their open questions (§10 of each). Spec 002 question 1 is the important one.
   - Merge the Sprint 0 pull request into `main`.
2. **Next session:**
   - Apply the review: update specs 002 and 005 (and the GDD if answers differ from the drafts), set them to Approved, tick US-005.T4 and US-006.T3, and close Sprint 0 in `sprints.md`.
   - Start **Sprint 1** (see `sprints.md`): US-007 and US-008 (write specs 003 and 004), US-059 (exact ledger, spec 005), US-038 to US-041 (land, spec 002). The owner creates the Unity project (US-017 to US-020).

## Owner action checklist (Phases 0–1)

| When | Owner action |
| --- | --- |
| Now | Review specs 002 and 005. Merge the Sprint 0 pull request |
| Sprint 1 (start) | Install the latest Unity LTS with iOS Build Support. Install Git LFS (`brew install git-lfs`, then `git lfs install`). Create the 2D URP project at `unity/FarmToCity/` and push it |
| Sprint 1 | Review specs 003 and 004. Install the "hello iPhone" build on the iPhone 13 using `docs/test-checklists/how-to-build-ios.md` |
| Sprint 2 | Review specs 006 and 007 |
| Sprint 3 | Review specs 008 and 009. Run the performance spike and the on-device determinism check, then report the results (Phase 0 gate) |
| Sprints 4–8 | Install each sprint's build, work through its test checklist, and report the results |
| Sprint 9 | Run the playtest week with 3–5 testers, collect their play logs, and make the gate decision |

## Open questions (with the owner)

**Spec 002 (land and soil), §10:**
1. **Land prices and the Hamlet pace.** The GDD says Hamlet on "Day 1–2", ADR 0017 says the end of the guided first week, and the gate needs a profitable season (the first close is on day 4). *Recommendation:* Hamlet target "end of the first week"; land priced by earning power (placeholder 1,20,000 coins an acre); land-purchase loans.
2. The well gives the inherited acres a moisture floor (bucket watering). *Recommendation:* yes.
3. Field-edge shade folded into the Soil factor, so ADR 0014's formula is unchanged. *Recommendation:* yes.
4. A quick sale of bought land at 80% in Phase 1. *Recommendation:* yes.
5. A hand-authored 6 × 5 map. *Recommendation:* yes.

**Spec 005 (ledger and credit), §10:**
1. Season-close payment priority: loans, then living costs, then wages, then rent. *Recommendation:* yes.
2. Land-purchase loans secured by the parcel bought. *Recommendation:* yes.
3. Credit score 300–900 (CIBIL-like). *Recommendation:* yes.
4. Foreclosure never while away: a final notice on return, then a day to cure. *Recommendation:* yes.
5. Inventory off the ledger in Phase 1 (stored goods only in net worth). *Recommendation:* yes.
6. Moneylender bullet loans, bank equal-principal loans. *Recommendation:* yes.
7. Foreclosure takes all non-protected assets, and any surplus is returned as cash (ADR 0009). *Recommendation:* yes.

**Noted for later specs (no decision needed now):**
- Spec 007 (saves): `Sync` runs every frame and every accepted command is logged, so the command log grows by about 3,600 entries an hour of live play. Consecutive live `Sync`s can be merged without changing the result (chunking invariance), so the saves spec should compact them.
- NUnit 5.0 was released on 27 Sep 2026. ADR 0026 names NUnit 4, so the tests use 4.6.1. Moving to 5 would need a small ADR.

## Half-done work

None.

## Environment notes

- The SessionStart hook installs the .NET 10 SDK in cloud sessions (US-003). It runs synchronously, so the session starts only once the SDK is ready. If it ever fails, install by hand with `apt-get install -y dotnet-sdk-10.0`.
- Tests and tools use the latest C# (NUnit 4.6 needs C# 10+ overload resolution); `sim/` stays on C# 9 and the compiler enforces it.
- Android isn't tested for now (ADR 0026). All device testing happens on the owner's iPhone 13.
