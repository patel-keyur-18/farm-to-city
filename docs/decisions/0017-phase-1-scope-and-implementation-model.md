# 0017. Phase 1 scope and the implementation model

- Status: Accepted. Phase 1 scope extended by ADR 0021 (fruit trees).
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0007 (zero budget, personal builds), ADR 0015 (onboarding)
- GDD sections: Technical architecture, Spec-driven workflow, Roadmap and milestones, Open decisions and risks (Scope)

## Context

The GDD's roadmap assumed 15–20 hours a week of solo work by the owner. In practice, Claude writes all the code and docs, and the owner reviews specs and tests builds on devices.

That moves the bottleneck to three places:
- whatever Claude can't verify without the Unity Editor (scenes, rendering, touch input)
- the owner's device test-and-report loop
- spec reviews

Scope remains the biggest risk.

## Decision

### Implementation model

1. **A big simulation and a thin Unity layer.** All rules, numbers and decisions live in `Sim.Core`, fully tested from the terminal. The Unity layer only renders state and sends commands.
2. **Code-first Unity.**
   - The project has one bootstrap scene. Everything else is created by C# at runtime.
   - UI is built with UI Toolkit, whose UXML and USS files are text.
   - Hand-edited scene and prefab files are avoided.
3. **Developer time-travel panel.** In development builds only: +1 hour, +1 day, +1 season, and jump to the away cap. It is compiled out of release builds.
4. **A test checklist with every build:** what to try, what to expect, what to report.
5. **CI for Unity (later, optional).** GameCI on GitHub Actions to compile and run Unity tests. It needs a Unity licence secret from the owner, and is decided when the Unity project is created.
6. **Art.** Phase 1 uses Kenney CC0 assets, tinted and combined. A scripted, headless Blender pipeline for setting-specific assets comes later as a separate spike.
7. **Continuity.** `CLAUDE.md` and `docs/` are the project's memory across sessions, and are kept current with every change.

### Roadmap

Phases are gated by exit criteria and device testing, not by weeks. The time estimates are removed.

### Phase 1 scope: Homestead to the Hamlet unlock (the guided first week)

| System | In Phase 1 |
| --- | --- |
| Time | Scheduler, calendar, weather spells, away rules, local notifications, dev time-travel panel |
| Land | 2 inherited acres plus buying adjacent parcels, fertility, moisture, soil type |
| Crops | Radish, wheat, rice, potato, lentils; full yield formula; *ready to harvest* |
| Founder | Labour, diet plan, work hours, health, day labour |
| Inputs | Certified or saved seed, compost and chemical fertiliser, pest risk and spraying |
| People | Hiring a farm hand, season contracts with prepaid wages, standing orders |
| Market | Tradeable goods market, regional trader |
| Money | Cash ledger, seasonal P&L, prepaid accounting, moneylender and bank loans, foreclosure |
| Saves | Atomic saves, autosave, export and import |
| UI | Homestead HUD, *While you were away* report, field and founder panels with *Why?* |
| Onboarding | First session plus the letter chapters up to the Hamlet unlock |

**Not in Phase 1:**
- livestock
- irrigation beyond the well
- the equipment ladder
- forward contracts
- insurance
- businesses (including the flour mill)
- the Codex
- the Chronicle
- events beyond weather and pests
- the remaining crops

### Spec workflow

- **The spec template gains section 11, "Cut first":** what to drop if the spec runs over.
- **The vertical-slice spec order:**
  1. time and weather
  2. land and soil
  3. crops and yield (including founder labour)
  4. goods and the local market
  5. the ledger
  6. people: hiring and contracts
  7. saves
  8. the Homestead HUD
  9. onboarding

## Consequences

- Phase 2 loses hiring, which moved into Phase 1, and keeps citizens, needs, businesses, the school and the Codex.
- Every spec's UI section must stay buildable code-first.
- Every user-facing build comes with a checklist in `docs/test-checklists/`.
