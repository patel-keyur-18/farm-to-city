# 0182. US-283 the device check as built: the realistic house, wheat and hands at 8×, shadows as their own sprites, stand-ins and a memory ballast

- Status: Accepted as built (the plan check's seven gaps answered by the owner, one per message, 2026-11-27); the device reading and the owner's review of the story are pending
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0094, 0172 (§8), 0180; spec 032 §3.2 §3.3 §3.4 §3.5 (AC-1 amended below)

## Context

ADR 0180 picked the realistic valley (Option B: baked shadows on buildings only) and asked for a device check before the other sprites are re-rendered: a GPU p95 over 11 ms on the iPhone 13 means Option A. Only a few sprites exist realistic (Preview 27's samples), so the check had to decide what the phone draws, how it measures memory and how the shadows work.

## Decisions (the owner, one per message)

1. **Stand-in per category.** In the check build every house sprite (family, pucca, village) draws the realistic family house of its facing, every crop the realistic wheat of its stage and every person the realistic hand or his wife by sex, so the frame time is the worst case, not a few sprites' worth. Development builds and the Editor only; a developer-panel switch (*Art check*), **off by default**, taking effect on the next launch.
2. **The real shadow mechanism now**, not a fixed alpha: each weather's `shadow` in `content/ui.json` (sun 1, heatwave 1, frost 0.8, cloudy 0.45, rain 0.25, heavy rain 0.15, storm 0.10), `WeatherFrame.Shadow` = the day's light (0 at night, 1 by day) × the weather's, cross-fading with the weather, and a `ShadowLayer` on the map. `camera.defaultAcresAcross` 3 and `closestAcresAcross` 2 in the same file.
3. **A memory ballast** in the Development build (art check on): dummy ASTC 4×4 textures (2048 × 1024, mips) until 271 MB are held, the computed size of the finished redesign (605 sprites, 51 Mpx today, × 4 pixels at 8×, 1 byte a pixel, × 1.33 for mips), so Xcode's gauge, the heat and the 20-minute session test the real load.
4. **ASTC 4×4 on every rendered sprite on iOS** (spec 032 §3.2), the old 4× ones too; the Editor stays uncompressed. The importer's version was raised so Unity re-imports all 609.
5. **The frames:** the house's four facings (the done house) with their four shadows, wheat's four stages, the hand and his wife standing, front and back. In the check every walk and work frame uses the standing sprite (figures slide); walk cycles stay in US-276.
6. **Every shadow caster has a shadow in the check:** houses their own; trees and orchards, huts, sheds, stores, businesses, village and town buildings, wells, the kiln and the tower draw the house's shadow scaled to their width. Crops, people, animals, vehicles: none. This counts the shadows' draw calls (spec 032 §3.3 left it "not yet counted").
7. **AC-1 amended.** The frame of a realistic sprite cannot stay within 1% of today's (measured: houses 1 to 2.5%, wheat up to 3.4%, people 5 to 13% narrower: no outline, real stalks, slimmer bodies). AC-1 now reads: a sprite keeps its **anchor** (its pivot's distance from the frame's middle across and from its bottom up) within **0.01 world units** (about 5 px at 4×) and its **frame** within **5%** (people 15%). `Game.Editor.SpriteSizeCheck` reads `docs/art/sprite-world-sizes.json` (today's size and pivot of all 605, recorded before any new art) against `sprites.json`: 0 problems. People get an invisible one-vertex foot pad in the render so their anchor keeps today's distance from the frame's bottom.

## What was built

- **`tools/blender/render_real.py`**: realistic sprites at 8× (`FTC_SCALE`, `render.py`'s `SCALE` reads it; 4 by default), the sun at 60° elevation, and **each building's shadow rendered as its own sprite** (the building hidden from the camera over a shadow catcher; flat ground such as a yard's pad casts nothing; **only the sun's shadow**, the sky's light off, so no ambient-occlusion smudge; alpha under 6% dropped, the colour black, the frame cut to the shadow with its own pivot at the building's world anchor).
- **`sprites.json`**: a per-sprite `ppu` (1,056 for the realistic ones while the rest stay at 528; the global value becomes 1,056 when every sprite is re-rendered) and a `shadow` flag; `SpriteBank` reads both (`ShadowOf`, `WorldWidth`) and names each sprite.
- **`ShadowLayer`** (map): finds every sprite with a shadow by a sweep twice a second, draws it as a child just under the sprite, black at `strength × the sprite's alpha`; `WeatherPresenter` feeds it `WeatherFrame.Shadow`.
- **The camera**: `MapCamera` takes the default and closest zoom (`DefaultHalfHeight`, `MinHalfHeight`) from `ui.json`; `MapInput.OpeningHalfHeight` is gone. A saved zoom outside the bounds is clamped as before.
- **`ArtCheck`**, the developer panel's switch, the memory line in the frame-time report (total, graphics driver, ballast), the `spritePixels` column in `RenderStats`.
- Tests: `WeatherLookTests.ShadowStrength` (AC-3), `IsoGridTests.DefaultAndClosestZoom` (AC-4), `IsoGridTests.SavedZoomClamped` (AC-5), `UiContentTests.CameraAndShadow` (AC-6), `Game.Editor.SpriteSizeCheck` (AC-1). AC-2 (`RenderedArtCheck`) is written when the last category is re-rendered. Goldens untouched (no sim, content-balance or save change).

## The Editor's reading (Village bot town, seed 1, the Editor's numbers, `render-stats.json`)

| View | Draw calls off → ON | Visible sprites | Triangles | spritePixels (k px) |
| --- | --- | --- | --- | --- |
| Whole map | 1,326 → 1,453 (+9.6%) | 1,677 → 1,804 | 31,957 → 40,819 (+28%) | 34,517 → 34,777 |
| Village | 594 → 636 (+7.1%) | 710 → 752 | 13,899 → 17,710 (+27%) | 41,004 → 41,252 |
| Farm, default view | 207 → 216 (+4.3%) | 218 → 227 | 5,041 → 6,197 (+23%) | 47,588 → 48,183 |

The shadows are about 130 more draw calls at the whole map (169 shadow sprites in the town): **spec 032 §3.3's "about one more draw call per caster in view" is confirmed at +9.6%, under the 10% flag.** The triangle growth is the shadow sprites' meshes and the 8× sprites' tighter hulls. Against the Sprint 29 baseline (the owner's town, 6 acres across at the farm view) the default view's counts fall (the camera shows fewer acres), so the comparison that counts is this town, off against on. Sheets: `SheetLayoutCheck` 86 sheets, 0 problems.

## Consequences

- **Pending on the iPhone 13:** both p95s, memory and the 20-minute heat check (`docs/test-checklists/s30-device-check.md`). Over 11 ms on the GPU means Option A (no baked shadows), back to the owner. US-273 to US-275 start only after that passes and the owner says so.
- Known visual warts to look at on the phone: a hard dark patch of shadow left of the house's courtyard (the sun's shadow of the verandah and walls onto ground the sprite leaves transparent), and the wheat canopy's flat tubes at the field's edge: both are US-278 and US-274's to redo.
- Spec 032 is amended: AC-1 (above), §3.3's draw-call count, §3.2 (a per-sprite `ppu` until all are re-rendered), §3.5 (the stand-in, the ballast and the memory line).
- The art check is compiled out of Release builds; `ArtCheck` costs nothing there.
