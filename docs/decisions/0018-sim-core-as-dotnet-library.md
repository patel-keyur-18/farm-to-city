# 0018. Sim.Core as a plain .NET library, tested with the dotnet CLI

- Status: **Proposed**
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (scheduler, determinism), ADR 0017 (code-first, thin Unity layer)
- Also resolves GDD review items #2 (Burst and ScriptableObjects vs a pure sim), #10 (floats and determinism) and #15 (`.gitignore` drops `.csproj`)
- GDD sections affected: Technical architecture (technology choices, project structure), Data model (content pipeline), Balancing and testing (tests)

## Context

The GDD says `Sim.Core` is pure C# with no `UnityEngine` references, and plans its tests as "NUnit EditMode tests", which only run inside the Unity Editor. Claude writes all the code (ADR 0017) and can't run the Unity Editor. To develop the simulation test-first, it has to build and test from a terminal. Unity must still consume the same code later.

**Backend analogy:** this is like keeping the domain model in a plain Maven module with no Spring dependency. You test it with `mvn test`, and the Spring Boot app only depends on it. In this project, Unity plays the role of Spring Boot.

## Options

1. **Unity-only.** Sim code lives under `Assets/` and is tested with the Unity Test Framework in EditMode.
   - No tests run without the Editor, so Claude can't verify anything.
   - CI needs a Unity licence on every run, and runs are slow.
2. **A plain .NET library tested with the dotnet CLI, built into a DLL and copied into `Assets/Plugins`.**
   - Fast to build and test.
   - There's a copy step after every change, the DLL in the repo can go stale, and debugging inside Unity is harder.
3. **A plain .NET library tested with the dotnet CLI, whose source folder is also a Unity package (UPM).**
   - The same `.cs` files compile in two ways: with `dotnet` (via a `.csproj`) for tests and tools, and inside Unity (via an `.asmdef` with `noEngineReferences: true`) for the game.
   - There's no copy step, and stepping through code in Unity works.

## Proposed decision

Option 3.

### Projects and layout

```
FarmToCity.sln
Directory.Build.props        shared settings: LangVersion 9.0, warnings as errors,
                             build output redirected to /artifacts
sim/                         a Unity package (package.json) and dotnet projects
  Sim.Core/                  Sim.Core.csproj (netstandard2.1) + Sim.Core.asmdef
  Sim.Content/               Sim.Content.csproj (netstandard2.1) + Sim.Content.asmdef
tests/
  Sim.Core.Tests/            net8.0, NUnit 3, run with `dotnet test`
tools/
  Tools.Balancer/            net8.0 console app, headless runs on a fake clock
content/                     JSON definitions (crops, goods, clock, ...), diffable
unity/FarmToCity/            the Unity project, created later by the owner in Unity Hub;
                             Packages/manifest.json references "file:../../../sim"
```

**Commands:**
- `dotnet build FarmToCity.sln`
- `dotnet test`
- `dotnet run --project tools/Tools.Balancer`

**Unity terms:**
- An **asmdef** (assembly definition) is Unity's equivalent of a `.csproj` or a Maven module: it names a compiled assembly and its dependencies. Setting `noEngineReferences: true` makes Unity refuse any `UnityEngine` use inside it, so the "pure C#" rule is checked by the compiler on both sides.
- A **UPM package** is Unity's equivalent of a local Maven dependency.

### Rules this ADR sets

1. **The language matches Unity.** `LangVersion` is pinned to **9.0** and the API level is **netstandard2.1**, which is what Unity 6 LTS supports. The compiler then rejects C# 10+ features in the sim, such as file-scoped namespaces, global usings and record structs, so code that builds with `dotnet` also builds in Unity.
2. **No floats in sim state or rules (#10):**
   - Money is a `long` in 1/100 coin (ADR 0008).
   - Quantities are `long` in each good's smallest unit (for example grams).
   - Rates and factors use a `Fixed` 64-bit fixed-point type in `Sim.Core`, with deterministic `Mul`, `Div`, `Pow`, `Exp` and `Log` (integer maths, with lookup tables where needed).

   This matters because tests run on .NET 8 (CoreCLR) while the game runs on Mono or IL2CPP. Integer maths gives the same results on all of them. Floats might not.
3. **Our own RNG.** The sim uses a seeded PCG32 or xoshiro generator in `Sim.Core`, never `System.Random`, whose seeded output isn't guaranteed to match across runtimes.
4. **No order taken from hash tables.** The sim never iterates a `Dictionary` or `HashSet` to decide an order. It processes entities in id order, using arrays or sorted collections. Hash ordering differs between runtimes.
5. **IL2CPP-safe C#.** IL2CPP is Unity's ahead-of-time compiler for iOS: C# is compiled to C++ with no JIT, so code generated at runtime fails. The sim uses no `dynamic`, no `Reflection.Emit`, no runtime code generation and no reflection-based serialisation. Save serialisation uses generated or hand-written formatters, decided in the saves spec.
6. **Content is plain data (#2).**
   - Definitions are plain C# classes in `Sim.Content`, loaded from JSON in `content/` by a Newtonsoft.Json loader (Unity ships an official package for this library).
   - The sim never sees ScriptableObjects. If they're useful later, they're editor-only authoring wrappers.
   - The content linter is a `dotnet test` suite over `content/`.
7. **No Burst or Job System in the sim (#2).** They are Unity-only, so they'd break purity, and Burst's floating-point maths isn't guaranteed identical across platforms. The sim uses plain C# struct arrays. If profiling ever demands parallelism, it must be deterministic: fixed partitions, merged in id order. Burst and Jobs remain allowed in Presentation, for example for rendering helpers.
8. **Build output outside the package.** Unity imports every file under a package folder, including stray `bin/` and `obj/` DLLs, which would conflict. `Directory.Build.props` redirects all build output to `/artifacts/`, and Unity never sees it.
9. **`.gitignore` fixes (#15):**
   - add exceptions so our hand-written `.csproj`, `.sln` and `Directory.Build.props` files are committed (Unity's template ignores all `*.csproj` and `*.sln`)
   - re-root the Unity ignore rules (`Library/`, `Temp/` and so on) to `unity/FarmToCity/`
   - ignore `/artifacts/`
10. **Unity-side tests.** Unity's Test Framework is used only for thin Presentation smoke tests later. All simulation acceptance criteria are `dotnet test` tests.

### CI (zero cost)

A GitHub Actions workflow on `ubuntu-latest` runs `dotnet build` and `dotnet test` on every push and pull request. No Unity licence is needed. The Unity compile check (GameCI) stays optional, as in ADR 0017.

## Trade-offs

| For | Against |
| --- | --- |
| Claude can build and test everything in the sim without Unity: real test-driven development | Two build systems see the same code (`.csproj` and `.asmdef`), so they must be kept in step. Mitigated by the shared folder and by rule 1 |
| Tests run in seconds. The balancer runs years of game time headless | Tests run on CoreCLR, the game on Mono or IL2CPP. Mitigated by rules 2–5 and by golden replay files that Unity can also verify later |
| The compiler enforces "no UnityEngine" in both builds | We maintain our own fixed-point maths and RNG (a few hundred lines, fully unit-tested) |
| Free CI with no Unity licence | Limited to C# 9 inside the sim. Newer syntax is unavailable until Unity moves to modern .NET |
| The same sim powers the game, the balancer, the economy model and replay tools | Unity package folders need `.meta` files once the owner creates the Unity project, and those must be committed |
| Future-proof: Unity is moving to CoreCLR, and netstandard2.1 libraries keep working | |

## Consequences if accepted

- **GDD updates:**
  - the tests row of the technology table becomes "NUnit via `dotnet test`; Unity Test Framework only for Presentation smoke tests"
  - the content row becomes JSON plus a Newtonsoft loader, with no runtime ScriptableObjects
  - the async-jobs row moves Burst and Jobs to Presentation only
  - the project structure gains the layout above
  - the content pipeline is amended to match
- **First code**, after spec 001 is approved:
  - the solution skeleton
  - `Fixed`, the RNG and the scheduler in `Sim.Core`, with their tests
  - the CI workflow
  - the `.gitignore` fixes
- **`CLAUDE.md`** records the build and test commands.
- **The .NET 8 SDK** is installed in the dev container from Ubuntu's apt archive. This has been checked: package `dotnet-sdk-8.0` is available, and NuGet is reachable.
