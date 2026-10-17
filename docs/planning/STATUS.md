# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 1 in progress: 17 of 28 points done, 11 in review (owner device test and spec reviews) |
| Branch with latest work | `sprint/01-ledger-land-unity` (pull request to `main` open; must be merged before the next session) |
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
  - **US-041 tree slots: done** (spec 002 AC-10): yard and field-edge slots, shade from mature edge trees with the floor, `SetTree` and `SetTreeMature` for spec 003, slot ids never reused. **Spec 002 is implemented: all 16 ACs pass.** Implementation notes are in spec 002 §10.
  - **US-019 code-first bootstrap and US-020 build guide: in review.** `GameRoot` (created by `[RuntimeInitializeOnLoadMethod]` after `Bootstrap.unity` loads) reads the content, starts a new game, syncs once per wall-clock second and shows date, time and weather in a UI Toolkit card. `Game.Services` holds `IEntitlementService` with the always-unlocked implementation. `ContentBuildStep` copies `content/*.json` into StreamingAssets before each build. Verified in play mode (through the Unity Pipeline CLI) and by an unsigned Xcode Release build for the device (BUILD SUCCEEDED). The owner's device run and guide dry run are pending: `docs/test-checklists/how-to-build-ios.md` and `s01-hello-iphone.md`. `unity/FarmToCity/README.md` maps the Unity concepts to Java and Spring.
  - **US-007 spec 003 (crops, fruit trees and founder labour) and US-008 spec 004 (goods, market and storage): written, in review.** Every number is checked against ADR 0008's per-season realism (a crop cycle yields its share of a season) and ADR 0028's pacing; the pacing check found a conflict (open question A below).
  - The GDD's engine row names Unity 6000.3.26f1 (ADR 0029).

## In progress

Nothing half-built. Waiting on the owner: the device run (US-019, US-020) and the reviews of specs 003 and 004 (US-007, US-008).

## Next up

1. **Owner:** merge the Sprint 1 pull request; install the hello-iPhone build with `docs/test-checklists/how-to-build-ios.md` and work through `s01-hello-iphone.md`; review specs 003 and 004 and answer the open questions below.
2. **Next session (Claude):**
   - Apply the owner's answers: approve or revise specs 003 and 004 (update the GDD in the same commit). If open question A is decided, write ADR 0030 and update specs 005 and 009 and the GDD to match.
   - Record the device results in the Sprint 1 review section of `sprints.md`; fix anything that failed; then close Sprint 1.
   - Start Sprint 2 (`sprints.md`): crops, fruit trees and founder labour (spec 003), accrual accounting (US-060 to US-062), the economy model, specs 006 and 007.

## Owner action checklist (Phases 0–1)

| When | Owner action |
| --- | --- |
| Now | Merge the Sprint 1 pull request |
| Sprint 1 | Install the hello-iPhone build (`how-to-build-ios.md`, `s01-hello-iphone.md`) and report. Review specs 003 and 004 and answer the open questions |
| Sprint 2 | Review specs 006 and 007 |
| Sprint 3 | Review specs 008 and 009. Run the performance spike and the on-device determinism check, then report the results (Phase 0 gate) |
| Sprints 4–8 | Install each sprint's build, work through its test checklist, and report the results |
| Sprint 9 | Run the playtest week with 3–5 testers, collect their play logs, and make the gate decision |

## Open questions (with the owner)

Each has a recommendation; the details are in the spec named.

- **A. The Hamlet pace doesn't add up (spec 003 §9–10, question 1).** By the end of week 1 a competent player has about 90,000 coins; the 4 more acres the Hamlet gate needs cost about 4,60,000, and after the land loan and the moneylender about 2,28,000 must still come from cash. **Recommendation:** gate on 4 acres farmed, and raise the land-purchase loan's LTV to 0.80 (no score factor on the parcel being bought), via ADR 0030 and a spec 005 change.
- **B. Seasonal moisture is off the spec 002 hook** (spec 003 question 2): a loam acre averages about 55 in the Monsoon and 64 in Winter (targets 70–85 and 10–35). **Recommendation:** tune `moistureByWeather` and the climate weights in the economy model (US-014); no rule change.
- **C. Spec 003 questions 3–6:** saved-seed generation per crop kind; Phase 1 health from food and rest only; inputs bought at the moment of use; the 7 kg radish cycle. **Recommendation:** keep all four.
- **D. Spec 004 questions 1–4:** selling is mostly to the trader in Phase 1; inputs at the regional price; pro-rata selling; limit prices with spec 006's standing orders. **Recommendation:** keep all four.
- **E. Spec 002 question 6:** fertility also recovers on unowned land, so unbought parcels slowly get dearer. **Recommendation:** keep (implemented that way).
- **F. The `com.unity.pipeline` package** (experimental, 0.8.0-exp.1) is in the Unity manifest. It's what lets Claude compile, play, capture and build through the `unity` CLI while the Editor is open, and it's disabled in player builds (no runtime config asset). **Recommendation:** keep it as a development tool; remove it before any store build.

**Noted for later specs (no decision needed now):**
- Spec 007 (saves): the command log grows by one `Sync` per second of live play (the hello build syncs once per wall-clock second). Consecutive live `Sync`s can be merged without changing the result (chunking invariance), so the saves spec should compact them.
- NUnit 5.0 was released on 27 Sep 2026. ADR 0026 names NUnit 4, so the tests use 4.6.1. Moving to 5 would need a small ADR.
- `BuyParcel` and `SellParcel` aren't in `ReplayJson` yet; the spec 005 golden replay (Sprint 4) adds them.
- Spec 005 AC-4 (`LedgerTests.Reconciles_EverySeason`) covers land purchases and sales; US-060 and F05.2 add prepayments, loans and missed payments to it.

## Half-done work

None.

## Environment notes

- The SessionStart hook installs the .NET 10 SDK in cloud sessions (US-003). It runs synchronously, so the session starts only once the SDK is ready. If it ever fails, install by hand with `apt-get install -y dotnet-sdk-10.0`.
- **On the owner's Mac** the .NET 10 SDK is installed per user in `~/.dotnet` (not on the default PATH): run `export PATH=$HOME/.dotnet:$PATH` before `dotnet test`.
- **Unity from the terminal:** while the owner has the Editor open, the `unity` CLI (`~/.unity/bin/unity command …`, the Unity Pipeline package) can recompile (`recompile`, `recompile_status`), read the console (`console`), enter play mode (`editor_play`), capture the Game view (`capture_game_view --save_path` inside the project) and start builds (`build --target iOS --outputPath Builds/iOS`). Batch mode can't run while the Editor has the project open. UI Toolkit text needs an explicit font (`LegacyRuntime.ttf`); without it labels render empty.
- Tests and tools use the latest C# (NUnit 4.6 needs C# 10+ overload resolution); `sim/` stays on C# 9 and the compiler enforces it.
- Android isn't tested for now (ADR 0026). All device testing happens on the owner's iPhone 13.
