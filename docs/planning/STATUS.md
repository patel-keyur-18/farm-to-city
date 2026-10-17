# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 1 in progress (started 2026-10-17) |
| Branch with latest work | `sprint/01-ledger-land-unity` |
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
  - **US-005 spec 002 (land and soil) and US-006 spec 005 (ledger and credit): approved** (2026-10-17), with every open question answered as recommended (§10 of each). The GDD matches both.
  - **ADR 0028:** the Hamlet target is the end of the first week (Village 2–3 weeks, Township 4–6 weeks, both provisional); land is priced at about 8–15 seasons of an acre's net farm income; bank loans can be secured by the land they buy; no leasing in Phase 1.

- **Sprint 1 (branch `sprint/01-ledger-land-unity`):**
  - **US-017 Unity project: done by the owner** (PR #5): Unity 6.3 LTS (6000.3.26f1), Universal 2D template, iOS, bundle ID `com.keyurpatel.farmtocity`, landscape only, Input System only, sim package referenced as `file:../../../sim`.
  - **Housekeeping:** the manifest and lock file use only the relative sim path; `.gitignore` now ignores `.vscode/*` at any depth except `extensions.json`, and the Unity-generated `launch.json` and `settings.json` were removed from git (Unity regenerates them). The GDD's Art section records Hay Day-like, code-driven motion and feedback; spec 008 (US-011.T3) and the art check (US-024.T4) carry it.

  - **US-018 Unity version pinned: done.** ADR 0029: Unity 6000.3.26f1. Unity still compiles every assembly with `-langversion:9.0` (checked in the response files under `Library/Bee/`), so the sim stays on C# 9.

  - **US-059 exact ledger: done** (spec 005 AC-1 to AC-5): the chart of accounts, balanced entries, cash never negative, `MulRate`, the opening balance sheet, the reconciliation check and entry retention at every season close. AC-4 covers land purchases and sales for now; US-060 and F05.2 add their activity to the same test.
  - **US-038 inherited land: done** (spec 002 AC-1, AC-2). New content: `land.json`, `map.json` (6 × 5, 13 parcels), `buildings.json`, `start.json`, `finance.json`. Land and ledger are optional `GameDefs` in `SimConfig`: a sim without them hashes exactly as before, so golden 001 is unchanged.
  - **US-039 soil properties: done** (spec 002 AC-3 to AC-9, AC-15, AC-16). Hourly moisture with drainage and the well floor, daily fallow recovery, `AdjustFertility` with per-reason season totals, the Soil factor. 72 h with land advances in about 0.4 ms (budget 20 ms). AC-6 buys its tile once `BuyParcel` exists (US-040).
  - **US-040 buy adjacent land: done** (spec 002 AC-11 to AC-14): `BuyParcel` and `SellParcel` (quick sale at 80%), with ledger postings (land at cost; gain or loss on sale), adjacency, the in-use and stranding guards. Financing joins with credit (Sprint 4).

## In progress

Sprint 1: US-041 (tree slots) next.

## Next up

1. **Owner:** merge pull request #4 into `main`.
2. **Next session: start Sprint 1** (see `sprints.md`, branch `sprint/01-ledger-land-unity`):
   - US-007 and US-008: write **spec 003** (crops, fruit trees and founder labour) and **spec 004** (goods, market and storage) for owner review. Spec 003 must charge the well's bucket-watering labour (spec 002 §3.2) and use the Soil factor from spec 002 §3.4; spec 004 must give the trader's buying price that net worth uses (spec 005 §3.5).
   - US-059: the exact ledger (spec 005 §3.1, ACs 1–5), first, because land buying posts through it.
   - US-038 to US-041: land and soil (spec 002).
   - The owner creates the Unity project at the start of the sprint (US-017 to US-020).

## Owner action checklist (Phases 0–1)

| When | Owner action |
| --- | --- |
| Now | Merge pull request #4 |
| Sprint 1 (start) | Install the latest Unity LTS with iOS Build Support. Install Git LFS (`brew install git-lfs`, then `git lfs install`). Create the 2D URP project at `unity/FarmToCity/` and push it |
| Sprint 1 | Review specs 003 and 004. Install the "hello iPhone" build on the iPhone 13 using `docs/test-checklists/how-to-build-ios.md` |
| Sprint 2 | Review specs 006 and 007 |
| Sprint 3 | Review specs 008 and 009. Run the performance spike and the on-device determinism check, then report the results (Phase 0 gate) |
| Sprints 4–8 | Install each sprint's build, work through its test checklist, and report the results |
| Sprint 9 | Run the playtest week with 3–5 testers, collect their play logs, and make the gate decision |

## Open questions (with the owner)

None. Specs 002 and 005 were approved with every recommendation (2026-10-17).

**Noted for later specs (no decision needed now):**
- Spec 007 (saves): `Sync` runs every frame and every accepted command is logged, so the command log grows by about 3,600 entries an hour of live play. Consecutive live `Sync`s can be merged without changing the result (chunking invariance), so the saves spec should compact them.
- NUnit 5.0 was released on 27 Sep 2026. ADR 0026 names NUnit 4, so the tests use 4.6.1. Moving to 5 would need a small ADR.

## Half-done work

None.

## Environment notes

- The SessionStart hook installs the .NET 10 SDK in cloud sessions (US-003). It runs synchronously, so the session starts only once the SDK is ready. If it ever fails, install by hand with `apt-get install -y dotnet-sdk-10.0`.
- Tests and tools use the latest C# (NUnit 4.6 needs C# 10+ overload resolution); `sim/` stays on C# 9 and the compiler enforces it.
- Android isn't tested for now (ADR 0026). All device testing happens on the owner's iPhone 13.
