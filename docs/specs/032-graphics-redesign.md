# 032. Graphics redesign: a realistic valley

- Status: Approved (the owner, 2026-11-27)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Art direction, Camera and map, Weather and the day's light
- Related ADRs: 0007, 0036, 0041 §8, 0043, 0088, 0094, 0172 §7 §8
- Depends on specs: 008 (the map, and the weather on the map, ADR 0083), 010 (the map's layout)

## 1. Goal

The valley looks real: lime-washed houses with their brick showing through, a wheat acre of thousands of stalks, people in their own clothes in the sun and the sky's light. A closer default view shows it, and the phone stays at about 60 fps without heating. No rule of the game changes.

## 2. Scope

**In:**
- **The pick (US-271, Preview 27):** the realistic style, Option B: physically lit materials, no outline, real cast shadows on buildings only, kept short. The owner, 2026-11-27.
- **The device check first (US-283, Sprint 30):** the realistic house, wheat acre and the hand and his wife in the game at the new zoom, read on the iPhone before the other sprites are re-rendered.
- **The redesign, one category a story (US-273 to US-282, Sprints 30 to 33):** every Blender sprite on the map re-rendered realistic at twice today's resolution; building shadows as their own sprites; the map's code-drawn pictures to match.
- **The camera:** the default view 3 acres across; the closest zoom 2 acres across.

**Out:**
- **The UI** (sheets, buttons, fonts, the HUD and the 76 icons): restyled to match in its own spec, 033, written in Sprint 32 against the finished map, coded in Sprints 34 and 35 (the owner, 2026-11-27). Until then the icons stay as they are.
- **Any sim, content-balance or save change.** Goldens do not move.
- **New shaders, lights or particles in Unity.** Everything realistic is in the render; the game still draws sprites.
- **Time-of-day light on the buildings** (a sun that moves): the sun is fixed; only the shadows' strength follows the day and the weather (§3.4).

## 3. Rules

### 3.1 The style

| Rule | Behaviour | Why |
| --- | --- | --- |
| Renderer | Blender Cycles on the CPU, 96 samples, denoised (OpenImageDenoise), AgX view transform with the *Medium High Contrast* look, exposure +0.15. Transparent film. | Preview 27's realistic sample. Measured 2 to 19 s a sprite on the Mac: about an hour for all of them. |
| Materials | Physically based (`samples.py` `real_material`): each surface's colour from today's palette, a procedural pattern for its detail (plaster, brick, tile, wood, earth, grass, soil, cloth, skin, hair, wheat, leaf), a roughness for what it is made of, a bump. Skin, wheat and leaves let light through (subsurface). | The sample's look; today's palette keeps the valley's colours. |
| Outline | None. | The realistic sample. |
| Light | One sun from the screen's upper left as today (azimuth unchanged), **elevation 60°** (Preview 27 used 50°), warm (1.00, 0.92, 0.80), energy 5.2, a 2° disc for soft edges; the sky's blue fill (0.55, 0.70, 0.92) at 0.85. | The owner asked for short shadows (Option B): at 60° a shadow is 0.58 × the height (0.84 at 50°), so a house's shadow stays inside its own acre. |
| Camera | Today's: orthographic, 2:1 dimetric, rotated 45°, tilted 60°. | The map's tiles and pivots don't change. |
| People | Realistic in material and light, **6 heads tall** at today's height (about the hut door's; ADR 0039): heads about a quarter bigger than true proportion so a face just shows at the default zoom. Clothes as approved in Sprint 5 (pagdi, dhoti and vest; a cotton saree with the head uncovered, a bun, bindi and bangles). New bodies for every kind, with walk (4 frames) and work frames as today. | The owner, 2026-11-27 (7.5 heads: a 6-point head at the default zoom, no face). |
| Portraits | The card portraits rendered realistic and close up, in the same style. | Faces show on the cards. |

### 3.2 Sizes and memory

| Rule | Behaviour | Why |
| --- | --- | --- |
| Render resolution | **8× the game's 1× density** (`render.py` `SCALE` 8): 1,056 px an acre (today 528). The manifest's `pixelsPerUnit` becomes 1,056; `SpriteBank` reads it, so sprites keep their size on the map. | At the new default zoom an acre is 844 phone pixels on the iPhone 13: today's 528 would be stretched 1.6× and soft. |
| Largest sprite | `hill_2`, 2,328 × 1,628 at 8×: the import's maximum size becomes 4,096 for the hills only; everything else fits 2,048. | Measured: only one of today's 605 sprites is over 1,024 px at 4×. |
| Compression | iOS override on every rendered sprite: ASTC 4×4 (8 bits a pixel), mipmaps on. The default platform (the Editor) stays uncompressed. | 4× the pixels at a quarter of the bytes: about today's 195 MB (260 MB with mipmaps). |
| Memory budget | Rendered sprites at most **320 MB** on the iPhone with mipmaps, read in Xcode's memory report at the device check. | Today about 260 MB; Option B estimated about 300. |

### 3.3 Shadows

| Rule | Behaviour | Why |
| --- | --- | --- |
| What casts a baked shadow | Buildings and big fixed things: houses, huts, sheds, stores, businesses, the village's and town's buildings, wells, trees and orchards, the kiln, the tower. | Option B: shadows where they show most. |
| What doesn't | People, animals, vehicles and machines (they move): today's code-drawn contact shadow, made softer to match (US-281). Crops, ground and boundaries: none of their own (their texture already has the light). | Option B; a moving sprite with a baked shadow would drag it through walls. |
| A shadow is its own sprite | Rendered in a second pass (the model hidden from the camera, a shadow catcher seen): `<name>_shadow.png`, with **its own frame** (a shadow falls outside its building's: Preview 27's house is 316 px wide with its shadow against 252 px without) and its pivot at the same world anchor as the building's, so the two line up; `sprites.json` gives each its own `w`, `h` and pivot. Drawn on the ground layer under its building, black, with alpha = its render's × the strength below. | The owner, 2026-11-27: so weather and night can fade it. About one more draw call for each shadow-casting sprite in view (buildings, trees, orchards, wells, the kiln, the tower): **not yet counted**; US-283 counts them with `RenderStats` at the crowded view and restates this estimate. The most crowded view draws about 1,150 calls today (`render-stats.json`, US-272). |
| Strength | `strength = day × weather`. `day` is the weather presenter's day light (`WeatherLook` `Light` mapped 0 at night to 1 by day, as the ground's tint already is). `weather` is a new `shadow` field on each weather style in `content/ui.json`: sun 1.0, heatwave 1.0, frost 0.8, cloudy 0.45, rain 0.25, heavy rain 0.15, storm 0.10. It moves with the weather's cross-fade. | Shadows go soft under cloud and vanish at night. Placeholder values, tuned on the phone at the device check (art, not balance). |

### 3.4 The camera

| Rule | Behaviour | Why |
| --- | --- | --- |
| Default view | **3 acres across the screen** at launch, when a town opens, and on the home button (today `MapInput.OpeningHalfHeight` 1.4, about 6 acres across the iPhone; it becomes the content value). | The owner, 2026-11-27. |
| Closest zoom | **2 acres across** (`MapCamera.MinHalfHeight`, in `IsoGrid.cs`, from 3 to 2 acre widths). | The owner, 2026-11-27: room to look closer. At 2 across an acre is 1,266 phone pixels against 1,056 rendered: 1.2× stretched, still sharp enough. |
| Farthest zoom | Unchanged (the whole map). | The crowded view stays the worst case the frame-time guard reads. |
| Saved camera | A town saved with a zoom outside the new bounds opens clamped into them (already the rule, `IsoGrid.Clamp`). | No migration needed. |

### 3.5 The frame-time guard (ADR 0094, ADR 0172 §8)

| Rule | Behaviour |
| --- | --- |
| The bar | On the iPhone 13, at the most crowded view: main thread p95 ≤ 8.3 ms, GPU p95 ≤ 11 ms, about 60 fps, under 1% of frames over 25 ms, no heating over a 20-minute session (the phone warm, not hot; no iOS thermal warning). |
| The device check (US-283) | The realistic house, field and the hand and his wife replace today's in a branch build, with their shadows and the new zoom. One Development build (`Game.Editor.BuildCli`, Xcode quit first, `Builds/iOS` deleted first), both p95s and the memory read at the default view and at the farthest zoom with a Village bot town. **If the GPU p95 is over 11 ms, the redesign goes on as Option A** (no baked shadows) and comes back to the owner. |
| Every story | `Game.Editor.RenderStats` at all three views, growth over 10% reported (sprite pixels count too: a new column, `spritePixels`, the sum of visible sprites' screen area). |
| The phase's build | Both p95s read again on the iPhone before the last art pull request. |

## 4. Data

**Changed definitions** (`content/ui.json`, the map's look; not balance):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `weatherLook.weathers.<kind>` | `shadow` | float | §3.3 | 0 to 1 | The buildings' shadow strength in that weather. |
| `camera` (new) | `defaultAcresAcross` | float | 3 | 2 to 6 | The default view. |
| `camera` | `closestAcresAcross` | float | 2 | 1.5 to 3 | The closest zoom. |

**State:** none. No save change; goldens unchanged.

**Art files:** every `Resources/Art/Blender/*.png` re-rendered at 8×; `<name>_shadow.png` for each building; `sprites.json`'s `pixelsPerUnit` 1,056 and a `shadow` flag on each sprite that has one.

## 5. Commands and events

None: presentation only.

## 6. Decision card

No player decisions.

## 7. UI

The map only, as in Preview 27's realistic column; every story shows a before and after of its category at the new default zoom, published as an exact-looking artifact, before its renders go in. No sheet changes (spec 033).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given the manifest at 1,056 px a unit, when any rendered sprite other than a shadow is placed, then it covers the same world size as before (its pivot and its bounds within 1%); a shadow's pivot is at its building's world anchor. | `Game.Editor.SpriteSizeCheck` (new, Editor: every sprite's world bounds against a recorded table of today's) |
| AC-2 | Every map sprite in `SpriteBank` has its realistic render, and every building's has its `_shadow`. | `Game.Editor.RenderedArtCheck` (new, Editor: every name `SpriteBank` asks for has its file, and every building its `_shadow`) |
| AC-3 | A building's shadow alpha is `day × weather.shadow`: 1 in sun by day, 0.25 in rain by day, 0 at night, cross-fading with the weather. | `WeatherLookTests.ShadowStrength` (Sim.View) |
| AC-4 | At launch (the default half-height moves from `MapInput.OpeningHalfHeight` into `MapCamera`, read from `content/ui.json`) the view is 3 acres across; the closest zoom is 2 acres across; the farthest is unchanged. | `IsoGridTests.DefaultAndClosestZoom` (new, Sim.View) |
| AC-5 | A saved camera outside the new bounds opens clamped. | `IsoGridTests.SavedZoomClamped` (new, Sim.View) |
| AC-6 | `content/ui.json` validates the new fields and their bounds. | `UiContentTests.CameraAndShadow` (new, Sim.View.Tests: `ui.json` is loaded by `Sim.View`'s `UiLoader`) |
| AC-7 | On the iPhone 13 the device check meets §3.5's bar, or the redesign switches to Option A. | Device: the US-283 test checklist (both p95s, memory, 20 minutes of play) |
| AC-8 | Every category's before/after preview approved before its renders go in; each story's `RenderStats` reading recorded. | Review: the story's ADR |
| AC-9 | Goldens unchanged. | The golden replay tests |

## 9. Balancing hooks

None: no rule or number of the game changes.

## 10. Open questions

- **ASTC 4×4 on fine detail** (wheat awns, saree borders): looked at on the phone at the device check; ASTC 5×5 or 6×6 for the ground if memory needs it.
- **Spec 033 (the UI restyle)** written in Sprint 32: panels, buttons, fonts and icons to sit with the realistic map.

## 11. Cut first

1. The work frames' extra poses (keep one work frame a kind).
2. Shadows on trees (keep buildings').
3. The 4,096 import for the hills (render them at 6× instead).

## 12. The plan (re-sized after the pick)

| Sprint | Stories | Points |
| --- | --- | --- |
| 30 | US-283 the device check (2); US-273 ground, terrain and boundaries, with the sky and the river's banks (5); US-274 crops and the kitchen garden (5); US-275 trees and orchards, with their shadows (4) | 16 |
| 31 | US-276 people and portraits: new 6-head bodies, walk and work frames, portraits (13); US-277 animals, vehicles and machines (3) | 16 |
| 32 | US-278 your farm, with shadows (6); US-279 businesses and stalls, with shadows (6); US-280 the village and town, with shadows (6); spec 033 written | 18 |
| 33 | US-281 the map's code-drawn pictures (contact shadow, ring, lit acre, arrow, overlays) (2); US-282 effects, markers and the weather's look (3); the phase's device build; spec 033 reviewed | 5 + review |
| 34–35 | The UI restyle (spec 033) | Sized in spec 033 |

Was 45 points over Sprints 30 to 32; now 55 over Sprints 30 to 33 (people +5 for new bodies, shadows +4 across buildings and trees, the device check +2, US-281's icons moved to spec 033 −1). Phase 4 starts about three sprints later than ADR 0172 planned.
