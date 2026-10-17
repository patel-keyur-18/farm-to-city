# 0026. Toolchain versions and test devices

- Status: Accepted. The Unity version is pinned by ADR 0029 (6000.3.26f1; C# stays 9.0)
- Date: 2026-10-17
- Deciders: Keyur Patel
- Supersedes: the "net8.0" target for tests and tools in ADR 0018
- GDD sections: Offline play and saves (performance budget), Technical architecture (technology choices)

## Context

The performance budget names reference devices the owner doesn't have. The owner wants the latest tool versions, not outdated ones.

## Decision

### Devices

| Role | Device |
| --- | --- |
| Primary test device | **iPhone 13** (A15, 4 GB RAM, 128 GB), latest iOS |
| Build machine | **MacBook Air M2** (Apple silicon), **Xcode 27.1** |
| Android | **Not tested for now.** The design stays cross-platform, and Android builds and testing are deferred until the owner chooses to add them |
| Tablet | **None.** Tablet layouts (4:3) stay supported in the design, but they're untested and low priority |

**Performance headroom.** The GDD's 60 fps minimum spec remains the design target. The iPhone 13 is faster than that minimum, so on the iPhone 13 a frame's simulation and rendering work must fit in **half the frame budget (8.3 ms)**.

### Versions: latest stable at all times

| Tool | Version rule |
| --- | --- |
| Unity | **The latest LTS** available in Unity Hub when the project is created (start of Phase 1). The exact version is pinned in an ADR, with no upgrades mid-phase. Install with iOS Build Support |
| .NET SDK | **.NET 10 (LTS)**, `dotnet-sdk-10.0` |
| Tests and tools | Target **net10.0**. NUnit 4 (latest), the latest test adapter and test SDK |
| Sim library | **netstandard2.1, C# 9.0**, as Unity requires (ADR 0018). At Unity project creation, check the latest LTS's supported C# version, and raise `LangVersion` if it allows |
| NuGet packages | The latest stable version when added, pinned in the project files |

## Consequences

- The `CLAUDE.md` build instructions install `dotnet-sdk-10.0`.
- Phase 0's performance-spike gate is measured on the iPhone 13 with the half-budget rule.
- Android-specific stories (signing, testing) sit in the Phase 5 backlog, marked deferred.
