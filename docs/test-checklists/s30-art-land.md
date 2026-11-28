# Sprint 30 build: the realistic land (ground, crops, trees)

- **Build:** none yet. The sprint's one Development build was the device check (`s30-device-check.md`), made before US-273 to US-275. This list is for the phase's next Development build (Sprint 33, or earlier if the owner asks). **No Development build is made until the owner asks.** Until then, every row below is checked in the Editor.
- **Before that build:** the art check's 271 MB memory ballast must be recomputed from `sprites.json`, or it counts the new ground, crops and trees twice (ADR 0185).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** `how-to-build-ios.md`. Quit Xcode before building; `Builds/iOS` is deleted first.

## Before you install: keep your town safe

1. In the old app: Menu → Settings → **Export**. Keep that copy.
2. **Install over the old app. Don't delete it first.**
3. This sprint changes no save, no rule and no content number. Your town loads as it is.

## What this build changes (story by story)

- **The device check (US-283).** The camera opens 3 acres across (closest 2). No baked shadows (your call); the soft ovals under people, animals and vehicles stay. Two houses on one acre stand apart.
- **Ground (US-273).** Realistic grass, earth, soil, scrub, roads, banks, village ground, hills and boundaries; each big kind has three looks so acres don't repeat; the river flows (still with *Reduced motion* on).
- **Crops (US-274).** Every crop at every stage, the garden beds and the planting pit, as a dense canopy; wheat is dense too. A fodder field is drawn tall once cutting starts, and half cut from the 6:00 cut till noon.
- **Trees (US-275).** Every fruit tree at every stage, the neem, babul, village tree, banyan and the temple's peepal, built leaf by leaf. Repeated trees (orchards, groves, scrub, the far bank) vary a little in size.
- **Not yet:** buildings, people, animals and vehicles keep today's art (Sprints 31 and 32); the HUD, pop-up screens and icons come with spec 033 (Sprints 34 and 35).

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | It opens 3 acres across on your yard; nothing is lost; no errors | | Editor ✓ (US-283 tour; `IsoGridTests`) |
| 2 | Pinch in and out to both limits | Closest shows 2 acres across; farthest shows the whole map as before | | Editor ✓ (`s30-273-closest.png`, `s30-273-all.png`) |
| 3 | Look for shadows under buildings and trees | None. Soft ovals stay under people, animals, vehicles and some buildings | | Editor ✓ (ADR 0183) |
| 4 | Find an acre with two houses | A strip of open ground between their courtyard walls | | Editor ✓ (`s30-283-houses-with-gap.png`) |
| 5 | Pan across fields, grass and scrub | No wallpaper repeat, no seams or black patches, roads and boundaries join | | Editor ✓ (`s30-273-farm.png`, `s30-273-pan.png`) |
| 6 | Watch the river for a minute | It flows without a jump where the loop restarts | | Editor ✓ (`s30-273-river.png`, `WaterFlowTests`) |
| 7 | Settings → *Reduced motion* on, watch the river | The water holds still | | Editor ✓ (`WaterFlowTests.ReducedMotionHoldsFrameZero`) |
| 8 | Look at your fields at different stages, then the garden beds | Each crop is recognisable; ready looks different from growing; people stand above the plants | | Editor ✓ (`s30-274-fields.png`, `s30-274-village.png`) |
| 9 | Tap a field, a garden bed and a planting pit | Each opens its own panel with the new picture | | Editor ✓ (`s30-274-bed-sheet.png`) |
| 10 | With berseem or sorghum past its first cut, look at 8:00 and again after 12:00 | Half cut with bundles in the morning; tall after noon, never back to seedlings | | Editor ✓ (`s30-274-fodder-cut.png`, `CattleViewTests.Fodder_LooksCut_TillNoon`) |
| 11 | Look at an orchard, your yard trees and a neighbour's grove | Leaf-by-leaf crowns; kinds can be told apart; ripe fruit shows and the ripe marker floats over it; copies differ a little in size | | Editor ✓ for a ripe banana orchard, ripe mango yard trees, ripe papaya edge tree, mango saplings, a grove (`s30-275-farm.png`, `s30-275-grove.png`). **A grown or ripe mango orchard and any apple tree were seen only in the preview** |
| 12 | Tap a yard tree, then an orchard where no tree stands in front | Its panel opens. A tree in front of an orchard takes the tap, as before | | Editor ✓ (`s30-275-orchard-sheet.png`) |
| 13 | Look at the village: the banyan on the square, the peepal by the temple, the scrub's babul | All in the new style; tap the banyan and the temple, their panels open | | Editor ✓ (`s30-275-banyan.png`, `s30-275-temple.png`) |
| 14 | Wait for a gust or windy weather | Trees lean gently, crops ripple, as before | | |
| 15 | Rain, storm, frost, heatwave over the new art | Readable; nothing flashes | | |
| 16 | Text size 125% and a tour of the sheets | Nothing cut off | | Editor ✓ at the sheet level (`SheetLayoutCheck`: 86 sheets, 0 problems) |
| 17 | Judge the art on the real screen: guava fruit on its leaves, the thin babul and neem, rice and its water | Readable at the opening zoom | **Device only** | |
| 18 | Developer panel → frame check, default view and farthest zoom, art check off | Main-thread p95 ≤ 8.3 ms, GPU p95 ≤ 11 ms, about 60 fps, under 1% of frames over 25 ms (the device check read 2.03%) | **Device only** | |
| 19 | Xcode's Memory gauge | Write the sprite and app figures down (sprites are computed at about 200 MB now; 320 MB is a target) | **Device only** | |
| 20 | Play 20 minutes | No heat warning, no growing stutter, no crash | **Device only** | |
| 21 | Developer panel → **Golden replays** | ALL PASS (no sim change) | **Device only** | |

## Tests (at the sprint's end)

| Project | Result |
| --- | --- |
| `Sim.Core.Tests` (not Balance, not Perf) | ____ |
| `Sim.Core.Tests` Perf | ____ |
| `Sim.Core.Tests` Balance | ____ |
| `Sim.View.Tests` | ____ |
| `Game.Services.Tests` | ____ |
| Goldens | ____ |

## Render statistics (Editor, the Village bot town, art check off; `docs/planning/render-stats.json`)

| View | Draw calls (device check → sprint end) | Triangles (device check → sprint end) |
| --- | --- | --- |
| The whole map | 1,326 → 1,357 | 31,957 → 49,589 (+55%) |
| The village | 594 → 611 | 13,899 → 21,132 (+52%) |
| The farm | 207 → 220 | 5,041 → 7,536 (+49%) |

The triangles come from the leafier outlines of the crops and trees; draw calls moved 2 to 6%. The numbers are small for the phone, and the phone's frame time (row 18) is the real test.

## What to report

Anything that looks wrong or toy-like beside the rest, a picture that is the wrong size or floats off its spot, a seam, a black patch, a tap that opens the wrong thing, a red line on the console (send its top line), a stutter, or a town that no longer loads.
