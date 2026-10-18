# 0029. Unity version pinned to 6000.3.26f1; the sim stays on C# 9

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0026 (latest LTS, pinned in an ADR, check the C# level), ADR 0018 (sim as a netstandard2.1 library), ADR 0017 (code-first Unity)
- GDD sections: Technical architecture (technology choices)

## Context

ADR 0026 asked for two things once the Unity project existed (US-018):

1. Pin the exact Unity version, so nobody upgrades mid-phase by accident.
2. Check whether that version allows a C# level above 9.0 for `sim/`, and raise `LangVersion` in `Directory.Build.props` if it does.

The owner created the project (PR #5) with Unity 6.3 LTS. `ProjectSettings/ProjectVersion.txt` reads `6000.3.26f1 (de8167e7440c)`.

**What the C# check found** (on the owner's Mac, with the project open):

- The editor ships Roslyn 4.3 (`Resources/Scripting/DotNetSdkRoslyn`), a compiler that understands up to C# 11.
- Unity still passes `-langversion:9.0` to that compiler for every assembly. All 165 compiler response files under `Library/Bee/artifacts/` contain it, and the generated `.csproj` files say `<LangVersion>9.0</LangVersion>`.
- Unity documents C# 9 as its supported level. A `csc.rsp` file with `-langversion:10` can force newer syntax, but Unity doesn't support it, some features need runtime types that Unity's .NET Standard 2.1 profile lacks, and IL2CPP compatibility isn't guaranteed.
- The API level is .NET Standard 2.1 (`apiCompatibilityLevel: 6`), and iOS always builds with IL2CPP.

## Options

1. **Pin 6000.3.26f1 and keep C# 9.0.**
2. **Pin 6000.3.26f1 and force C# 10 through `csc.rsp`.** This gains file-scoped namespaces and global usings, at the cost of an unsupported setup that could break on device.

## Decision

Option 1.

| Item | Pinned value |
| --- | --- |
| Unity editor | **6000.3.26f1** (Unity 6.3 LTS, revision `de8167e7440c`), with iOS Build Support |
| Packages | Exactly as in `unity/FarmToCity/Packages/packages-lock.json`. The sim is referenced as `file:../../../sim`, never by an absolute path |
| Development tooling | `com.unity.pipeline` 0.8.0-exp.1 (experimental) stays as an Editor-only tool: through the `unity` CLI, Claude compiles, plays, captures and starts builds while the owner's Editor is open. Its runtime part is off in player builds (no runtime config asset). It is removed before any store build (F19.5) (owner decision, 2026-10-17) |
| Sim language | **C# 9.0, netstandard2.1**, unchanged (ADR 0018). No `csc.rsp` overrides in the Unity project |
| Scripting | IL2CPP on iOS; .NET Standard 2.1 API level |

**Upgrade rule:**
- No editor or package upgrades during a phase.
- Between phases, an upgrade needs a new ADR that supersedes this one. A patch release inside 6000.3 LTS may be taken mid-phase only to fix a bug that blocks a build, and the ADR is written in the same pull request.
- `LangVersion` is re-checked whenever the editor is upgraded, using the same method: read `-langversion` from the response files in `Library/Bee/artifacts/`.

## Consequences

- `Directory.Build.props` keeps `LangVersion` 9.0, and `CLAUDE.md`'s C# 9 rule stands.
- Unity Hub on the owner's Mac must keep 6000.3.26f1 installed. If Hub offers a newer 6.3 patch, it's ignored unless the upgrade rule applies.
- The test checklists and the iOS build guide name this version.
- ADR 0026's Unity row is now fulfilled by this ADR.
