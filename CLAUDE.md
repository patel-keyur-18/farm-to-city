# CLAUDE.md

## Project

Farm to City is an offline, single-player farming, township and city simulation for iOS and Android, built in Unity (C#).
- **The player** is an ageless founder who inherits 2 acres in a fictional, Indian-inspired river valley, and grows it through six eras (Homestead to City) by farming, hiring, borrowing, founding businesses and running the town.
- **The world** runs on a real-time clock like Hay Day's: work continues while the app is closed, up to 72 hours.
- **Every number** can be explained with a *Why?* breakdown, and the game teaches real business and finance concepts through play.
- **`docs/GDD.md`** is the master spec and the source of truth.
- **`docs/decisions/`** holds the ADRs, which record why the GDD says what it says.
- **Roles:** Claude writes all code and docs. The owner, Keyur, reviews specs and tests builds on devices. Keyur is a senior Java/Spring Boot developer who is new to Unity, so explain Unity concepts briefly and map them to backend equivalents.

## Session routine (ADR 0019): read this first

Every session starts with no memory. The repository is the only memory.

**At session start:**
1. Read this file.
2. Read `docs/planning/STATUS.md`: the current phase, sprint, next story, open questions and branch.
3. Read the current sprint file named in `STATUS.md` (`docs/planning/sprints.md`) and the feature file(s) for the next story (`docs/planning/features/`).
4. Read the spec(s) and ADR(s) those stories name.
5. Continue from "Next up" in `STATUS.md`. If anything in it is unclear or contradicts the repository, ask the owner before acting.

**At session end, or after each finished story:**
1. Tick the story and task checkboxes in the feature file. Update the sprint file.
2. Update `docs/planning/STATUS.md`: last done, next up, open questions, branch, anything half-done.
3. Commit and push.
4. At the end of a session or sprint, open a pull request to `main` and ask the owner to merge it. The next session clones `main`, so unmerged work is invisible to it.
5. **Every pull request gets labels and a milestone** (owner rule, 2026-10-17). Use only labels and milestones that already exist in the repository:
   - **Milestone:** the phase the sprint belongs to (`Phase 0 · Pre-production`, `Phase 1 · Homestead slice`, `Phase 2 · Hamlet & Village`, `Phase 3 · Township`, `Phase 4 · Town & City`, `Phase 5 · Launch`).
   - **`type:` labels**, one for each kind of change in the pull request: `type:feature`, `type:spec`, `type:adr`, `type:bug`, `type:balance`.
   - **`area:` labels**, one for each area touched: `area:sim`, `area:content`, `area:tooling`, `area:ui`, `area:art`.
   - **`documentation`** when docs change.
   - Re-check the labels whenever later pushes change what the pull request contains.

## Workflow (spec-driven)

- **No game code until its feature spec is written and approved.** Specs follow `docs/specs/_TEMPLATE.md` and live in `docs/specs/NNN-name.md`.
- **Specs are written rolling-wave** (ADR 0019): all of a phase's specs are written and approved in that phase's first sprint, before its coding starts.
- **Specs fix formulas, bounds and placeholder values** (ADR 0027). The economy model tunes values in `content/`, and spec defaults are then updated without re-approval. A formula or rule change needs re-approval.
- **Every decision of lasting importance becomes an ADR** in `docs/decisions/NNNN-title.md` (context, options, decision, consequences). ADR numbers are never reused. To change a decision, write a new ADR that supersedes the old one.
- **When a spec or ADR changes the GDD,** update `docs/GDD.md` in the same commit.
- **Sprints (ADR 0025)** are 1 week long, with at most 2 specs to review and 1 device build per sprint. Hierarchy: Epic → Feature → User story → Tasks. Story points, MoSCoW priority, and the Definition of Ready and Definition of Done in ADR 0025.
- **Work on one branch per sprint** (`sprint/NN-short-name`), unless the session assigns a branch. Use small conventional commits (`docs:`, `feat:`, `test:`, `fix:`, `chore:`). One pull request per sprint, merged by the owner.
- **Stop for the owner's review at the end of each task.** For anything ambiguous, give a recommendation and ask. Push back when something doesn't make sense.
- **Every build handed to the owner comes with a test checklist** in `docs/test-checklists/`: what to try, what to expect, what to report.
- **This file and `docs/` are the project's memory across sessions.** Keep them current.

## Engineering rules (non-negotiable)

- **`Sim.Core` is pure C#** with zero `UnityEngine` references. The compiler enforces this through the `.asmdef` setting `noEngineReferences: true` (ADR 0018).
- **Determinism:** the same seed plus the same timestamped commands always gives the same state.
  - Sim time is a `long` count of seconds, separate from wall-clock time.
  - A fixed processing order: time, then system order, then entity id.
  - No floats in sim state or rules. Use the `Fixed` fixed-point type for rates and factors.
  - Our own seeded RNG, never `System.Random`.
  - Never take an order from iterating a `Dictionary` or `HashSet`.
  - IL2CPP-safe code: no `dynamic`, no `Reflection.Emit`, no reflection-based serialisation.
- **State changes only through commands.** The simulation emits domain events. The UI only reads state.
- **Money** is `long` in 1/100 coin, never floats. **Quantities** are `long` in each good's smallest unit.
- **All balancing numbers live in data** (`content/*.json`), never hard-coded.
- **Every acceptance criterion in a spec maps to an automated test**, named in the spec.
- **The sim's language level is C# 9.0 at netstandard2.1**, matching Unity 6000.3.26f1 (ADR 0029). No file-scoped namespaces, global usings or record structs inside `sim/`.

## Repository layout

```
docs/GDD.md                 master game design spec (source of truth)
docs/specs/                 feature specs (NNN-name.md) and _TEMPLATE.md
docs/decisions/             ADRs (NNNN-title.md)
docs/test-checklists/       per-build device test checklists
docs/planning/              STATUS.md (session handoff), epics.md, features/, sprints.md
FarmToCity.sln              solution for the dotnet projects
Directory.Build.props       LangVersion 9.0, warnings as errors,
                            output to /artifacts
sim/                        Unity package com.farmtocity.sim (package.json)
                            + dotnet projects
  Sim.Core/                 pure C# simulation: .csproj (netstandard2.1)
                            + .asmdef (noEngineReferences)
  Sim.Content/              JSON loader + validation (definition classes
                            live in Sim.Core, ADR 0020)
tests/Sim.Core.Tests/       NUnit 4 tests (net10.0)
tools/Tools.Balancer/       headless balancer on a fake clock (net10.0)
content/                    JSON definitions (balancing data)
unity/FarmToCity/           Unity project (Unity 6000.3.26f1, ADR 0029)
  Assets/Scenes/Bootstrap   the only scene; GameRoot builds everything else in C#
  Assets/Game/Presentation  Game.Presentation asmdef: composition root, UI (UI Toolkit)
  Assets/Game/Services      Game.Services asmdef: saves, platform ports, IEntitlementService
  Assets/Game/Editor        Game.Editor asmdef: copies content/*.json into the build
```

**Dependency direction:** Presentation and Services depend on Sim. The Sim never depends on Unity.

**Unity is code-first** (ADR 0017): one bootstrap scene, with everything else created by C# at runtime and UI in UI Toolkit (UXML and USS). Avoid hand-editing scene and prefab YAML.

## Build and test

The .NET 10 SDK (LTS) is required (ADR 0026). In cloud sessions the SessionStart hook (`.claude/settings.json` → `.claude/hooks/session-start.sh`) installs `dotnet-sdk-10.0` when it's missing and restores NuGet packages, so `dotnet test` works straight away. If the hook ever fails, install by hand with `apt-get install -y dotnet-sdk-10.0`.

```
dotnet build FarmToCity.sln
dotnet test
dotnet run --project tools/Tools.Balancer
```

Build output goes to `/artifacts/` (never committed). CI (GitHub Actions) runs `dotnet build` and `dotnet test` on every push. Unity builds are made by the owner on a MacBook Air M2 with Xcode 27.1, and tested on an iPhone 13 (ADR 0026).

## Key decisions (see `docs/decisions/` for full text)

| ADR | Decision |
| --- | --- |
| 0001 | Setting: Indian-inspired, fictional river valley. Seasons are Spring, Summer, Monsoon, Winter |
| 0002 | Real-time three clocks. Task clock in real time; 1 season = 4 real days; 1 year = 16 days; citizens age 1 life-year per season. No speed controls. 72 h away cap. A scheduler drives the sim |
| 0003 | The player is an ageless founder household (amended by 0010) |
| 0004 | The player never loses an election. The result sets the council mood |
| 0005 | Landscape only |
| 0006 | Free with one-time unlock (implementation deferred) |
| 0007 | Zero budget: CC0 art plus Blender sprites, personal builds only, no purchases (`IEntitlementService` is always unlocked) |
| 0008 | Rupee-scale "coins" (1/100 unit). Realism per season. Season contracts, costs paid upfront. Starting package (superseded by 0030) |
| 0009 | Moneylender and bank from day one. Bankruptcy is foreclosure, and the inherited plot is protected. The founder can do day labour |
| 0010 | The founder is the first worker: labour = Hours × Nutrition × (1 − Fatigue) × Health. Never an energy bar |
| 0011 | Literacy is separate from skilled graduates. The founder funds the first school. The first doctor is a migrant |
| 0012 | Price bounds come from regional trade. Inflation indexes the regional price |
| 0013 | Market clearing: demand curve, supply includes the trader, damped hourly adjustment, services are separate |
| 0014 | Yield formula adds Fertiliser and CropHealth, capped at 1.6× base |
| 0015 | Onboarding: a first session plus a guided first week |
| 0016 | Balance rule: archetype bots within 20% of each other; stress bots as exploit detectors |
| 0017 | Phase 1 runs from Homestead to the Hamlet unlock (hiring included). Code-first Unity. Dev time-travel panel |
| 0018 | `Sim.Core` is a netstandard2.1 library tested with `dotnet test` and consumed by Unity as a local package |
| 0019 | Rolling-wave specs (a phase's specs are written in its first sprint). The repository is the only memory: the session routine, `STATUS.md`, and a pull request merged to `main` after each session |
| 0020 | Definition classes live in `Sim.Core`. `Sim.Content` is only the JSON loader and validator |
| 0021 | Crop calendar follows Indian cropping seasons (radish all year). Fruit trees in yard and field-edge slots; apple on hills only, later |
| 0022 | Hiring from Homestead: day labourers from neighbouring villages, and a resident hand who needs a hut. Regulars can move in |
| 0023 | One town at a time. Save points are a crash-safety guard only, never an undo |
| 0024 | Phase 1 adds the hut store and spoilage, string tables and Indian number formatting, and an opt-in local play log |
| 0025 | Sprint mechanics: 1-week sprints, story points, MoSCoW, Definition of Ready and Done, a pull request per sprint |
| 0026 | Test device: iPhone 13 (half-frame-budget headroom). Mac: M2 Air with Xcode 27.1. Android untested for now. Latest LTS Unity, .NET 10 for tests and tools |
| 0027 | Audit corrections: specs 002 and 005 first, the ledger core in Sprint 1, Unity and a hello-iPhone build in Sprint 1, on-device determinism in the Phase 0 gate, the Hamlet-unlock feature, and later-phase coverage |
| 0028 | Hamlet at the end of the first week (Village 2–3 weeks, Township 4–6 weeks). Land priced at 8–15 seasons of an owner-operated acre's net income (ADR 0033). Bank loans can be secured by the land they buy. No leasing in Phase 1 |
| 0029 | Unity pinned to 6000.3.26f1 (6.3 LTS). Unity still compiles with `-langversion:9.0`, so the sim stays on C# 9. No upgrades mid-phase. `com.unity.pipeline` is an Editor-only dev tool |
| 0030 | Inherit 6 protected acres and the yard, 20,000 coins and a store of seed and grain. Hamlet gate: 6 acres harvested in a season at Care ≥ 0.9, a hand's house, a profitable season. 8 × 6 map. Unowned land keeps its fertility |
| 0031 | Phase 1 adds the kitchen garden, the mandi trip, buying inputs ahead (seasonal input prices; fertiliser price-controlled), minimum prices on offers, warm clothing and medicine |
| 0032 | The well waters 2 well acres (the rest rain-fed). The household eats from the store daily, credited as home use. Founder nutrition and day labour are time-weighted. P&L grouping; the Hamlet gate needs farm profit > 0. Inherited seed age 2.0 |
| 0033 | Land payback measured on an owner-operated acre (1,20,000 stays; hired acres net 2,000–5,000). Saplings priced with planting: banana 60, papaya 60, guava 180, mango 350 |
| 0034 | Sprint 3 review: mandi trip costs 0.5 h of labour; a glut drains hour by hour; every coin amount follows the price index (not loans or fertiliser); inputs in store are an asset (1200); ready crops and fruit spoil after 72 h; subsistence automatic only; Phase 0 gate checks and the spike defined |
| 0035 | Performance spike passed on the iPhone 13 (main thread p95 ≤ 7.6 ms, GPU ≤ 2.8 ms, about 60 fps, under 1% dropped frames); golden replays match on IL2CPP; the Phase 0 gate is passed |
| 0036 | Fonts may be SIL OFL (Noto Sans, Devanagari for Hindi); the low-poly Kenney landscape style is the art base; the first notification ask comes in the first session after the long crop is sown. (0035 is reserved for the spike result) |
| 0037 | Mandi cart is a tractor-trolley (2,000 kg, 1,000 hire); shade per tree (mango, guava 0.08; banana, papaya 0.03; floor 0.80); first-season pacing accepted (the first loan before hiring) |
| 0038 | Credit loopholes closed: foreclosure also seizes stored produce, selling collateral repays the bank first, a 24-live-hour final notice for every default; moneylender 6% a season; minimum loans; first playable scope (minimal HUD and Store sheet, painted placeholder sprites, Baloo 2 headings, dev builds, Phase 0 spike deleted, Blender pipeline in Sprint 6) |
| 0039 | Sprint 5 review: hands' huts on the edge of an inherited acre (at most 2); regulars counted by time hired; back pay when renewing after a lapse; orders never undo the player's crop, one action per hand per hour; stars and words for skill; a People button and a people-only away card; four people stress bots; a Blender slice in Sprint 5, every sprite rendered by the end of Sprint 6; the hand's wage 30,000 a season (day 9,375) |
| 0040 | Device review: a 2-acre yard (tile 29) and the labour quarters (tile 37) for hands' huts (supersedes 0039 §2); an order starts at once (one action per hand per hour); people go round their fields, hoe, and go home when idle; yard props and fence |
| 0041 | Sprint 6 review: a save holds everything hashed (content by id, one write path); old saves refused and kept until the Sprint 9 playtest build, migrations after; notifications from a simulated absence, ranked, no daily cap, quiet hours, hands' work per 4 h; a gear and Settings sheet; import from a list; regulars always accept; every sprite, the ground, icons and walk cycles in Blender now |
| 0042 | Weather in 1-hour spells with per-weather persistence: calm weather lasts (sun 1–2 days), severe passes (heavy rain, storm 1–2 h); seasonal shares unchanged; the forecast as runs (Sprint 7, US-184) |
| 0043 | Sprint 7 review: run lengths per season (Monsoon sun 6 h breaks, dry-season rain 4–6 h); the forecast rolled per run; *Why?* in words on every number (a ? on rows, a coverage test); the Menu (gear gone, People shortcut, ! alerts); Money and loans, founder, good, Buy inputs, mandi panels; the full away card only when something happened; text size, reduced motion, sound, haptics; 7 Kenney CC0 sounds; 25 points |
| 0044 | Sprint 7 device review: a back button on sheets opened from another sheet; sounds only for town events (no tap or sheet sounds); a *Why?* opens only from its ?; text size 80/100/125%; loan payments priced at the press; net worth kept, with the land on its own row |
| 0045 | Sprint 8 review: a one-line nudge pill per first-session step, the tour during the garden wait; a long crop on any acre; `comeBack` asks (Yes / Not now, never at night), the card as fallback; AC-11 to AC-16; Phase 1 health checks (cautious saver, random, never stuck with credit refused, recovery after foreclosure, exact ledger, bounded prices) with thresholds measured then set realistically; Sprint 8's build feature-complete Development |

## Current status

See `docs/planning/STATUS.md`. It is the single source of truth for where the work stands.
