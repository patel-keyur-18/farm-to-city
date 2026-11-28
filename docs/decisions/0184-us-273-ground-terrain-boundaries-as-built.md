# 0184. US-273 the ground, terrain, boundaries, sky and river banks as built

- Status: Accepted (the owner approved the before/after preview, 2026-11-28; the story awaits the owner's review)
- Date: 2026-11-28
- Deciders: Keyur Patel
- Related ADRs: 0180 (the direction), 0182 (the 8× pipeline, per-sprite `ppu`), 0183 (no baked shadows)

## Context

US-273 is the first category of the redesign (spec 032 §12, 5 points): every ground sprite of the map re-rendered realistic at 8× (1,056 px an acre). Plan check and spec gaps were closed one per message before any code (the owner's answers are below), and the owner approved the before/after preview (Preview 28, the 3-acre default zoom) before any render went into the game.

## Decisions (the owner, 2026-11-27 and 28)

1. **Three variants** of each big ground kind (grass, earth, soil, scrub), picked per acre by a fixed hash of its place (`GroundVariants.Pick`), so a field of acres doesn't repeat like wallpaper. No sim, content or save state.
2. **The river flows**: "make water flowing naturally, with animation, realistic", then "12 frames, 10 fps (smoother)". 12 rendered frames of `ground_water` and of `edge_water`, one loop 1.2 s; reduced motion holds frame 0.
3. **The sky**: a soft haze, no new sprite and no shader. The map's background starts as the haze colour (`0xC9DCD6`); the meadow colour past the map's edge is unchanged.
4. **The hills at 8×** with a 4,096 px import limit (`hill_2` is 2,318 × 1,608).
5. **Boundaries** (hedge, fence, bund, worn bund, gates, corner stones, street edges): the same designs, realistic detail.
6. The approved preview's look: the grass, earth, soil and scrub from palette colours through the same sun and AgX as the sprites, a seamless procedural texture, soil furrows, a rippled river with paler crests, banks with reeds and the washing steps.

## What was built

- **Rendering** (`tools/blender/real_ground.py` new, `render_real.py`): the ground's materials are procedural and *seamless* (a periodic noise over the acre blended in four lookups; a macro layer shared by the three variants so they line up, mid, fine and grain layers per variant). Per-channel albedo gains were calibrated so the rendered colour matches the palette under AgX (grass 0.387/0.536/0.178 and so on, listed in `real_ground.py`). The river's ripples are a sum of sines with whole wavelengths per acre and whole wavelengths moved per 12 frames, warped by seamless noise, so frame 11 leads into frame 0 and each tile joins its neighbours. 80 sprites rendered: 48 re-rendered, 32 new (6 variants, 12 `ground_water_N`, 12 `edge_water_N`, 2 more variants).
- **Render bugs found by looking and fixed**: overlapping coplanar patch discs and road arms render black in Cycles (each patch disc now has its own 0.4 mm height step; the road acre is built from non-overlapping boxes); 18 mm soil ridges made grooves so deep and narrow that they rendered black (now 10 mm, grooves stay brown); the corner stone drew 6% smaller than today's without the outline (modelled 1.7× instead of 1.6×).
- **Unity**: `SpriteBank` (`GroundVariant`, `WaterFrame`, frame substitution only where the framed name exists in the manifest), `MapPresenter.Water.cs` (`AnimateWater` in `LateUpdate`, `WaterFlow.Frame` from `Time.unscaledTime`), `MapPresenter.Valley.cs` and the yard/quarters/default-soil sites use `GroundVariant`; the importer sets the tiles up as sprites (below); `SpriteSizeCheck` judges a variant or a frame against its base name.
- **The sprite mesh of a tile** (found by `RenderStats`: triangles +245%): Unity's tight mesh of an 8× ground tile follows the staircase of its edge, about 190 triangles a tile (8 for a hexagon will do). `BlenderSpriteImport` now imports `ground_*`, `edge_*` and `hill_*` as Sprites (pivot and `ppu` from `sprites.json`, mesh type Tight, tessellation detail 0): 8 triangles for `ground_grass`, at most 24 for any tile. `Sprite.OverrideGeometry` is refused for runtime sprites in this Unity, so a convex-hull mesh made at runtime was dropped. `GetVersion` is 4 (every render re-imports once).

## Measured (the Editor, the Village bot town, art check off, the three views; `docs/planning/render-stats.json` "US-273 ground…")

| View | Draw calls | Triangles | Visible sprites | spritePixels |
| --- | --- | --- | --- | --- |
| Whole map | 1,365 (US-283 off: 1,326, +2.9%) | 31,568 (31,957, −1.2%) | 1,708 (1,677) | 34,355 (34,517, −0.5%) |
| Village | 619 (594, +4.2%) | 13,650 (13,899, −1.8%) | 727 (710) | 40,746 (41,004, −0.6%) |
| Farm (default view) | 228 (207, **+10.1%**, flagged) | 5,784 (5,041, **+14.7%**, flagged) | 231 (218) | 47,292 (47,588, −0.6%) |

The farm's growth comes with 13 more visible sprites (231 against 218) and is not isolated further; nothing grew on the whole map, the most crowded view, and the readings vary about 4% between runs. Before the tile mesh was coarse the triangles read 110,156 (+245%). Memory: the rendered sprites on the iPhone (ASTC 4×4, mipmaps) now total about **153 MB** (ground 59, edges 19, crops 16, hills 10, the river's 24 frames **27.8**); spec 032 §3.2's budget is 320 MB and the finished redesign was computed at 271 MB **before** the river's frames, so about 299 MB with them: under the budget with 21 MB to spare, and a watch for the later stories (US-274, US-275 and the buildings re-render at 8×). `SpriteSizeCheck` 0 problems (every sprite within AC-1's tolerances and every tile at most 24 triangles); `SheetLayoutCheck` 89 sheets, 0 problems; the Editor tour by scripted touch (pan, Menu, the closest and the whole-map zoom, the river's frames differing between captures, every capture looked at); the saves restored and identical.

## Tests (spec 032)

| AC | Test |
| --- | --- |
| AC-1 (size and anchor) | `Game.Editor.SpriteSizeCheck` (0 problems) |
| AC-10 (variants) | `GroundVariantTests.PickIsSteadyEvenAndNotStriped`, `WaterFlowTests.EveryGroundVariantIsRenderedAtTheBaseSizeAndPivot` |
| AC-11 (the river) | `WaterFlowTests.TheLoopIsTwelveFramesAtTenAndComesBack`, `ReducedMotionHoldsFrameZero`, `EveryWaterFrameIsRenderedAtOneSizeAndPivot` |
| AC-12 (tile mesh) | `Game.Editor.SpriteSizeCheck` (a tile over 24 triangles is a problem) |
| AC-8 | this ADR, the preview and the `RenderStats` readings |

## Flagged for the owner

- The farm view's draw calls +10.1% and triangles +14.7% against the same town with the old art (the whole map and the village shrank slightly).
- Memory: about 299 MB projected at the end of the redesign against 320 MB (the river's frames are 27.8 MB). If US-274 to US-282 push past it, the river can drop to 8 frames or the ground to ASTC 6×6 (spec 032 §10).
- The Editor's numbers are not the phone's: the next Development build (the phase's last) reads the frame time again (ADR 0183's 2.03% of frames over 25 ms).

## Consequences

- Spec 032 §3.2 and §3.6 and AC-10 to AC-12 are added; US-274 and US-275 follow the same rules (a category's preview, then its renders).
- Crops, trees and buildings still draw today's art on the realistic ground until their stories.
