# Unity project: FarmToCity

Unity 6000.3.26f1 (ADR 0029). The game's rules live in the `sim/` package; this project only renders state and sends commands (ADR 0017). Build and install steps: `docs/test-checklists/how-to-build-ios.md`.

## Layout

```
Assets/
  Scenes/Bootstrap.unity        the only scene: a camera and a 2D light, nothing else
  Game/Presentation/            Game.Presentation.asmdef: composition root, HUD, content loading
  Game/Services/                Game.Services.asmdef: platform ports (IEntitlementService; saves later)
  Game/Editor/                  Game.Editor.asmdef (Editor only): the pre-build content copy
  Resources/UI/DefaultTheme.tss the UI Toolkit runtime theme
Packages/manifest.json          references the sim as "file:../../../sim"
```

## Unity concepts, in Java and Spring terms

| Unity | Closest Java / Spring idea | Here |
| --- | --- | --- |
| Assembly definition (`.asmdef`) | A Maven module with declared dependencies | `Game.Presentation` depends on `Sim.Core`, `Sim.Content` and `Game.Services`. `Sim.Core` can't reference Unity at all (`noEngineReferences`) |
| Package (`Packages/manifest.json`) | A Maven dependency; `file:` is like a local module path | The sim is a local package, so Unity compiles the same `.cs` files that `dotnet test` tests |
| `MonoBehaviour` | A Spring bean whose lifecycle the container drives (`Awake` ≈ constructor/`@PostConstruct`, `Update` ≈ a scheduled tick every frame) | `GameRoot` |
| `[RuntimeInitializeOnLoadMethod]` | An `ApplicationRunner` that runs at start-up | `GameRoot.Boot` creates the `Game` object in code after the bootstrap scene loads, so no scene file needs editing |
| `GameObject` | A container that beans (components) are attached to | `Game`, holding `GameRoot` and the `UIDocument` |
| UI Toolkit (`UIDocument`, `PanelSettings`, UXML/USS) | Server-side templates plus CSS | The hello HUD builds its elements in C#; later screens use UXML and USS text files (spec 008) |
| `Resources/` folder | Classpath resources loaded by name | `Resources.Load<ThemeStyleSheet>("UI/DefaultTheme")` |
| `StreamingAssets/` | Files copied verbatim into the app bundle | `content/*.json` is copied there before each build by `ContentBuildStep` |
| `.meta` files | Generated IDs that other assets reference; always committed | Every file and folder under `Assets/` and `sim/` has one |
| IL2CPP | Ahead-of-time compilation of the bytecode to C++ (no JIT on iOS) | Why the sim avoids reflection-based serialisation (CLAUDE.md) |

## Runtime flow

1. iOS launches the app; Unity loads `Bootstrap.unity`.
2. `GameRoot.Boot` creates the `Game` object; `GameRoot.Awake` reads the content JSON, starts a new game (`Simulation.NewGame`) and builds the HUD.
3. Every wall-clock second, `Update` sends `Sync(now)`; the sim advances and the HUD shows the new date, time and weather. Coming back from the background is the same `Sync`: the sim catches up (up to 72 hours).

There are no saves yet (spec 007), so every launch is a new game.
