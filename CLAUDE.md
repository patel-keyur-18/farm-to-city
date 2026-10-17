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

## Workflow (spec-driven)

- **No game code until its feature spec is written and approved.** Specs follow `docs/specs/_TEMPLATE.md` and live in `docs/specs/NNN-name.md`.
- **Specs are written rolling-wave** (ADR 0019): all of a phase's specs are written and approved in that phase's first sprint, before its coding starts.
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
- **The sim's language level is C# 9.0 at netstandard2.1**, matching Unity 6 LTS. No file-scoped namespaces, global usings or record structs inside `sim/`.

## Repository layout

```
docs/GDD.md                 master game design spec (source of truth)
docs/specs/                 feature specs (NNN-name.md) and _TEMPLATE.md
docs/decisions/             ADRs (NNNN-title.md)
docs/test-checklists/       per-build device test checklists
docs/planning/              STATUS.md (session handoff), epics.md, features/, sprints.md
FarmToCity.sln              solution for the dotnet projects              (planned)
Directory.Build.props       LangVersion 9.0, warnings as errors,
                            output to /artifacts                          (planned)
sim/                        Unity package (UPM) + dotnet projects         (planned)
  Sim.Core/                 pure C# simulation: .csproj (netstandard2.1)
                            + .asmdef (noEngineReferences)
  Sim.Content/              JSON loader + validation (definition classes
                            live in Sim.Core, ADR 0020)
tests/Sim.Core.Tests/       NUnit tests (net8.0)                          (planned)
tools/Tools.Balancer/       headless balancer on a fake clock (net8.0)    (planned)
content/                    JSON definitions (balancing data)             (planned)
unity/FarmToCity/           Unity project, created by the owner in Unity Hub (planned)
  Game.Presentation         rendering and UI (asmdef), built code-first
  Game.Services             saves, platform services, IEntitlementService (asmdef)
```

**Dependency direction:** Presentation and Services depend on Sim. The Sim never depends on Unity.

**Unity is code-first** (ADR 0017): one bootstrap scene, with everything else created by C# at runtime and UI in UI Toolkit (UXML and USS). Avoid hand-editing scene and prefab YAML.

## Build and test

The .NET 8 SDK is required. In the cloud container, install it with `apt-get install -y dotnet-sdk-8.0`.

```
dotnet build FarmToCity.sln
dotnet test
dotnet run --project tools/Tools.Balancer
```

These commands work once the solution skeleton exists, which is the first code task after spec 001 is approved. CI (GitHub Actions) runs `dotnet build` and `dotnet test` on every push. Unity builds are made by the owner on a Mac with Xcode.

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
| 0008 | Rupee-scale "coins" (1/100 unit). Realism per season. Season contracts, costs paid upfront. Starting package |
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

## Current status

See `docs/planning/STATUS.md`. It is the single source of truth for where the work stands.
