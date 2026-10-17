# 0020. Definition classes live in Sim.Core; Sim.Content is the loader

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Supersedes: the part of ADR 0018 that placed definition classes in `Sim.Content`
- GDD sections: Technical architecture (project structure)

## Context

ADR 0018 put definition classes (`ClockDef`, `CropDef` and so on) in `Sim.Content`, alongside the JSON loader. But definitions use the `Fixed` type from `Sim.Core`, and `Sim.Core`'s systems read the definitions, so the two assemblies would depend on each other. Neither .NET nor Unity allows that.

## Options

1. Definition classes in `Sim.Core`, and `Sim.Content` only loads and validates JSON.
2. A third assembly for shared primitives (`Fixed`, ids) that both depend on.

## Decision

Option 1.
- `Sim.Core` contains the definition classes as plain C#, with no JSON or Newtonsoft dependency.
- `Sim.Content` depends on `Sim.Core` and Newtonsoft.Json. It parses JSON into definitions by hand-written mapping (no reflection-based deserialisation, per ADR 0018), validates them, and reports errors by field path.

## Consequences

- `Sim.Core` stays free of third-party dependencies.
- The JSON loader takes JSON **text**, not file paths. The host decides where files come from: the test project reads `content/`, and Unity reads its bundled copy.
