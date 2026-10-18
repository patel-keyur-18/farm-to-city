# Project status (session handoff)

> Read this at the start of every session, right after `CLAUDE.md` (ADR 0019). Update it at the end of every session and after every finished story.

| Field | Value |
| --- | --- |
| Phase | 0 · Pre-production |
| Sprint | Sprint 2 in progress (58 points after the planning review) |
| Branch with latest work | `sprint/02-crops-trees-founder` |
| Last updated | 2026-10-18 |

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
  - **US-019 code-first bootstrap and US-020 build guide: done** (the owner confirmed the build on the iPhone 13 and installed it with the guide alone, 2026-10-18). `GameRoot` (created by `[RuntimeInitializeOnLoadMethod]` after `Bootstrap.unity` loads) reads the content, starts a new game, syncs once per wall-clock second and shows date, time and weather in a UI Toolkit card. `Game.Services` holds `IEntitlementService` with the always-unlocked implementation. `ContentBuildStep` copies `content/*.json` into StreamingAssets before each build. Verified in play mode (through the Unity Pipeline CLI) and by an unsigned Xcode Release build for the device (BUILD SUCCEEDED). The owner's device run and guide dry run are pending: `docs/test-checklists/how-to-build-ios.md` and `s01-hello-iphone.md`. `unity/FarmToCity/README.md` maps the Unity concepts to Java and Spring.
  - **US-007 spec 003 and US-008 spec 004: approved** in the open-question review with the owner (2026-10-17). All 12 open questions were answered; the owner asked for realism throughout:
    - **ADR 0030:** the founder inherits 6 protected farm acres and the yard (two parcels on an 8 × 6 map), 20,000 coins and a store (certified seed potato, wheat, lentils, garden seed). Hamlet gate: 6 acres harvested in one season at average Care ≥ 0.9, a hand's house, a profitable season. Unowned land keeps its map fertility. **Implemented and tested** (96 tests).
    - **ADR 0031:** Phase 1 adds the kitchen garden (2 beds, realistic season output), the mandi trip, buying inputs ahead (seasonal input prices; chemical fertiliser price-controlled), minimum prices on offers, warm clothing and medicine. New stories US-177 (S2) and US-178 to US-180 (S3); US-052 grows to 3 points.
    - Saved seed ages per season of growth (radish certified only); radish is a 2-hour field crop; the moisture hook follows real Indian seasons (tuned in US-014); pro-rata selling stays; `com.unity.pipeline` stays as an Editor-only tool (ADR 0029), removed before any store build.
    - The GDD, specs 002–005, CLAUDE.md and the planning files match the decisions. The crop guide the owner asked for is a crop-calendar card (US-011.T4) and seasonal hints in the letters (US-012.T3).
  - The GDD's engine row names Unity 6000.3.26f1 (ADR 0029).

- **Sprint 2 (branch `sprint/02-crops-trees-founder`):**
  - **Planning review with the owner (2026-10-18), before coding.** Six gaps closed, recorded in **ADR 0032** and in §10 of specs 002–005: the well waters 2 well acres (tiles 20, 28), the rest rain-fed; the household eats from the store daily, credited to sales as home use; founder nutrition and day labour are time-weighted (closes a money loop); the P&L grouping (farm profit, net result) with plain-language labels, and the Hamlet gate on farm profit > 0; inherited seed age 2.0 for wheat and lentils.
  - **US-181 goods, prices and `UseInput`: done** (spec 004 AC-2, AC-1's goods checks). `goods.json`, `region.json`, `market.json`; P_regional, Floor, Ceiling, input prices (index fixed at 1.00); the trader's daily capacity. `LoadGame` now takes a file reader. **US-057 store: done** (spec 004 AC-12, AC-14, AC-23): capacity by weight, both overflow rules, the inherited store, `StoreValue` (inputs at replacement cost), `UseInput`. 105 tests.
  - **US-060 to US-062 accrual, season close, net worth: done** (spec 005 AC-6 to AC-12, AC-14, AC-15; AC-4 gains prepayments). Prepayments expensed daily, the season close (depreciation, the report with ADR 0032's lines and plain-language keys, close to retained earnings, reconciliation), net worth with inputs at replacement cost. Accounts 5250 and 5700 added. 114 tests.
  - **Well acres (ADR 0032) and spec 003 content: done.** **US-042 to US-045 and US-177 crops and the garden: done** (spec 003 AC-1 to AC-7, AC-9, AC-11 to AC-13, AC-17, AC-31 to AC-33): `Sow`, `Harvest`, `ClearField`, `SowGarden`, `PickGarden`; growth jobs; one accrual engine for crops and the founder (§3.7); the yield formula with the cap and breakdown; seeds (stock first, saved-seed age) and fertiliser; fertility effects; bucket labour on the well acres. CropHealth is 1.00 until US-046. 130 tests.
  - **US-047 and US-048 fruit trees: done** (spec 003 AC-24 to AC-26): `PlantTree` (capitalised to 1700), maturity jobs and shade, one batch per fruiting season, `PickFruit` with returns at the floor, lifespan and `RemoveTree` (book value to 5600). 133 tests.
  - **US-050, US-051, US-053 founder: done** (spec 003 AC-8, AC-18, AC-20 to AC-23, AC-36, AC-37; spec 005 AC-28): labour as a time-weighted rate, day labour paid daily, diet plans paid upfront and changed with refunds, daily eating as home use, fatigue from the day worked, the health formula, automatic subsistence and recovery, the first season's living costs at the new game. With the crops: AC-10 chunking invariance over a game year of bot play, AC-28 ledger reconciliation, AC-29 performance (72 h in about 0.6 ms). 145 tests.
  - **Scope moved to Sprint 2:** the spec 004 core (new US-181: goods, regional prices with a fixed index, trader capacity, `UseInput`) and US-057 (the store), because crops need them; the health formula moves from US-052 into US-051. Pests, golden 003 and tree payback stay in Sprint 3. Sprint 2 is 58 points, Sprint 3 47.

## In progress

Sprint 2. Order: US-181 and US-057 (spec 004 core), US-060 to US-062 (accrual, season close, net worth), US-042 to US-045 and US-177 (crops, garden), US-047 and US-048 (trees), US-050, US-051, US-053 (founder), US-013 and US-014 (economy model, anchors, moisture tuning), then specs 006 and 007 for the owner's review.

## Next up

1. **Claude:** US-013 and US-014 (economy model and anchor numbers, the moisture tuning), then specs 006 and 007.
2. Spec 006 must price the hand's hut (about 15,000 for the ADR 0030 pacing) and reuse spec 004's minimum price in standing orders. Spec 007 must compact the per-second `Sync` entries in the command log.

## Owner action checklist (Phases 0–1)

| When | Owner action |
| --- | --- |
| Sprint 2 | Review specs 006 and 007; merge the Sprint 2 pull request |
| Sprint 3 | Review specs 008 and 009. Run the performance spike and the on-device determinism check, then report the results (Phase 0 gate) |
| Sprints 4–8 | Install each sprint's build, work through its test checklist, and report the results |
| Sprint 9 | Run the playtest week with 3–5 testers, collect their play logs, and make the gate decision |

## Open questions (with the owner)

None. The Sprint 2 planning review's six gaps were answered on 2026-10-18 (ADR 0032).

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
