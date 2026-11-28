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
- **The pick (US-271, Preview 27):** the realistic style: physically lit materials, no outline. Option B (short baked shadows on buildings) was the first pick; **after the device check the owner dropped baked shadows altogether (ADR 0183, 2026-11-27): Option A.** The code-drawn contact oval under moving things stays.
- **The device check first (US-283, Sprint 30):** the realistic house, wheat acre and the hand and his wife in the game at the new zoom, read on the iPhone before the other sprites are re-rendered.
- **The redesign, one category a story (US-273 to US-282, Sprints 30 to 33):** every Blender sprite on the map re-rendered realistic at twice today's resolution; the map's code-drawn pictures to match.
- **The camera:** the default view 3 acres across; the closest zoom 2 acres across.

**Out:**
- **The UI** (sheets, buttons, fonts, the HUD and the 76 icons; but see §3.1: a person's picture in the UI is their portrait): restyled to match in its own spec, 033, written in Sprint 32 against the finished map, coded in Sprints 34 and 35 (the owner, 2026-11-27). Until then the icons stay as they are.
- **Any sim, content-balance or save change.** Goldens do not move.
- **New shaders, lights or particles in Unity.** Everything realistic is in the render; the game still draws sprites.
- **Time-of-day light on the buildings** (a sun that moves): the sun is fixed. **No baked shadows (ADR 0183).**

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
| Portraits | The card portraits rendered realistic and close up, in the same style, at today's 256 px. **Wherever the UI shows a person it shows their portrait** (the People button, list rows, sheet headers, person chips and buttons); full bodies are the map's. Day labourers get portraits of their own (a man and a woman): 15 portraits. Only which picture is loaded changes: no size, layout or colour (that is spec 033). | Faces show on the cards. The owner, 2026-11-28: a 6-head body's face is about 11 phone pixels on the People button and 7 on a chip (a portrait's head there is about 60, an estimate until rendered). |

### 3.2 Sizes and memory

| Rule | Behaviour | Why |
| --- | --- | --- |
| Render resolution | **8× the game's 1× density** (until every sprite is re-rendered the manifest keeps today's 528 and gives each realistic sprite its own `ppu` 1,056, ADR 0182) (`render.py` `SCALE` 8): 1,056 px an acre (today 528). The manifest's `pixelsPerUnit` becomes 1,056; `SpriteBank` reads it, so sprites keep their size on the map. | At the new default zoom an acre is 844 phone pixels on the iPhone 13: today's 528 would be stretched 1.6× and soft. |
| Largest sprite | `hill_2`, 2,318 × 1,608 at 8×: the import's maximum size becomes 4,096 for the hills only; everything else fits 2,048. | Measured: only one of today's 605 sprites is over 1,024 px at 4×. |
| Compression | iOS override on every rendered sprite: ASTC 4×4 (8 bits a pixel), mipmaps on. The default platform (the Editor) stays uncompressed. | 4× the pixels at a quarter of the bytes: about today's 195 MB (260 MB with mipmaps). |
| Memory budget | Rendered sprites aim at **320 MB** or less on the iPhone with mipmaps, read in Xcode's memory report at the device check. A target, not a hard limit (the owner, 2026-11-28, ADR 0184): each story reports its figure. | Today about 260 MB; Option B estimated about 300. |

### 3.3 Shadows (none baked: ADR 0183)

| Rule | Behaviour | Why |
| --- | --- | --- |
| Baked shadows | **None.** No `_shadow` sprites, no shadow layer, no weather `shadow` value. | The owner, 2026-11-27, after the device check: "I don't want shadows at all." (US-283 built the mechanism and counted it: +9.6% draw calls at the whole map, ADR 0182; it was removed.) |
| Contact shadow | Today's code-drawn soft oval under people, animals, vehicles and some buildings stays; US-281 makes it softer to match. | The owner's answer to ADR 0183: without it moving things float on the realistic ground. |
| Light | The Blender sun at 60° lights each render (its own shading, not a cast shadow on the ground). | Shading stays; the ground carries no shadow. |

### 3.4 The camera

| Rule | Behaviour | Why |
| --- | --- | --- |
| Default view | **3 acres across the screen** for a new town, a reset and a town with no saved camera (the content value; it replaces `MapInput.OpeningHalfHeight` 1.4, about 6 acres across the iPhone). | The owner, 2026-11-27. |
| Played town | A town with a saved camera reopens where it was left, zoom included, at launch and on a town switch; each town keeps its own (so since Sprint 6, spec 031). There is no home button. | The owner, 2026-11-28 (Sprint 30 review, question 1): the player's own zoom is not reset at every launch. A home button would be new UI. |
| Closest zoom | **2 acres across** (`MapCamera.MinHalfHeight`, in `IsoGrid.cs`, from 3 to 2 acre widths). | The owner, 2026-11-27: room to look closer. At 2 across an acre is 1,266 phone pixels against 1,056 rendered: 1.2× stretched, still sharp enough. |
| Farthest zoom | Unchanged (the whole map). | The crowded view stays the worst case the frame-time guard reads. |
| Saved camera | A town saved with a zoom outside the new bounds opens clamped into them (already the rule, `IsoGrid.Clamp`). | No migration needed. |

### 3.5 The frame-time guard (ADR 0094, ADR 0172 §8)

| Rule | Behaviour |
| --- | --- |
| The bar | On the iPhone 13, at the most crowded view: main thread p95 ≤ 8.3 ms, GPU p95 ≤ 11 ms, about 60 fps, under 1% of frames over 25 ms, no heating over a 20-minute session (the phone warm, not hot; no iOS thermal warning). |
| The device check (US-283, read 2026-11-27: GPU p95 7.94 ms, main 4.43 ms, 59.3 fps, 2.03% over 25 ms; ADR 0182, 0183) | The realistic house, field and the hand and his wife replace today's in a branch build, with their shadows and the new zoom. One Development build (`Game.Editor.BuildCli`, Xcode quit first, `Builds/iOS` deleted first), both p95s and the memory read at the default view and at the farthest zoom with a Village bot town. **The GPU p95 passed; the owner chose no baked shadows anyway (ADR 0183).** The build stands the realistic house, wheat and people in for every house, crop and person, gives every other caster the house's shadow scaled, and loads a 271 MB ASTC ballast, behind a Development-only switch (*Art check*, off by default). |
| Every story | `Game.Editor.RenderStats` at all three views, growth over 10% reported (sprite pixels count too: a new column, `spritePixels`, the sum of visible sprites' screen area). |
| The phase's build | Both p95s read again on the iPhone before the last art pull request. |

### 3.6 The ground and the river (US-273, ADR 0184)

| Rule | Behaviour | Why |
| --- | --- | --- |
| Ground texture | Seamless and procedural (no image textures): grass, earth, soil and scrub from the palette's colours through the same sun and AgX as every sprite (per-channel gains calibrated). Never two coplanar overlapping tops (Cycles renders the overlap black): patches step 0.4 mm, a road acre is built from boxes that don't overlap. | A tile must join its neighbours; the palette keeps the valley's colours. |
| Variants | Grass, earth, soil and scrub each have 3 variants (`ground_grass`, `_1`, `_2`), the same size and pivot; an acre's variant is `GroundVariants.Pick(x, y)`, a fixed hash of its place. | A field of acres doesn't repeat like wallpaper. No sim or save state. |
| The river | `ground_water` and `edge_water` are 12 frames each (`_0` to `_11`) played at 10 a second (`WaterFlow`); each frame leads into the next and frame 11 into 0; reduced motion holds frame 0. Presentation only: the clock is the app's. | The owner: "flowing naturally, with animation, realistic", "12 frames, 10 fps". About 28 MB of the budget. |
| Hills | Rendered at 8× with a 4,096 px import (`hill_2` 2,318 × 1,608). | §3.2. |
| Tile mesh | Ground, edge and hill sprites are imported as Sprites with a coarse mesh (at most 24 triangles): the tight mesh of an 8× render is about 190. | Measured: +245% triangles otherwise. |

### 3.7 The crops and the kitchen garden (US-274, ADR 0185)

| Rule | Behaviour | Why |
| --- | --- | --- |
| Canopy | A full dense canopy: about three times the toon art's plants and leaves, a second set between the ridges once grown. | The owner, 2026-11-28. |
| Leaves and colour | Pointed folded blades; green materials take a per-channel gain and a matt surface so AgX renders the palette's own green; a few crops have their own gain; sarson's flowers have a faint light of their own in the render only. | Raw palette leaves render pale; reflected yellow renders cream. |
| Paddy | Still water. | The owner: real paddy water barely moves; no memory. |
| Beds | The same plank bed, 5 × 5 plants. | The owner: easy to find and tap. |
| Frame | Each render is held to the recorded frame and anchor. | AC-1. |
| Fodder cut | Once a fodder field's first cut is due it is drawn tall, whatever its stand's progress. After a morning's real cut it is drawn half cut (`crop_berseem_cut`, `crop_sorghum_cut`) until 12:00. A picture only: nothing in the sim changes. | The owner, 2026-11-28: you see the morning's cut, and the field doesn't look half empty all day. |

### 3.8 Trees and orchards (US-275, ADR 0186)

| Rule | Behaviour | Why |
| --- | --- | --- |
| Crowns | Built leaf by leaf, each kind with its own leaf shape, limbs showing through the gaps. | The owner, 2026-11-28: matches the dense crops; the kinds can be told apart. |
| Fruit | In bunches on the side the camera sees, about half as long as the toon art's; about 2.5 to 4 times life size beside the tree. | A ripe tree must read at the game's zoom; closer to life size does not show. |
| Repeated trees | Orchards, neighbours' groves, the scrub's babul and the far bank's mango are each drawn 92% to 108% of size, fixed by their place. Single trees are not scaled. | The owner: break up identical copies at no memory cost. |
| Shade | None (ADR 0183): fallen leaves under the neem and the village tree; the basin's and the banyan's floors barely take shade. | The owner wants no shadows. |
| Frame | Each render is held to the recorded frame and anchor. | AC-1. |

## 4. Data

**Changed definitions** (`content/ui.json`, the map's look; not balance):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `camera` (new) | `defaultAcresAcross` | float | 3 | 2 to 6 | The default view. |
| `camera` | `closestAcresAcross` | float | 2 | 1.5 to 3 | The closest zoom. |

**State:** none. No save change; goldens unchanged.

**Art files:** every `Resources/Art/Blender/*.png` re-rendered at 8×; `sprites.json`'s `pixelsPerUnit` 1,056.

## 5. Commands and events

None: presentation only.

## 6. Decision card

No player decisions.

## 7. UI

The map only, as in Preview 27's realistic column; every story shows a before and after of its category at the new default zoom, published as an exact-looking artifact, before its renders go in. No sheet changes (spec 033).

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given each realistic sprite at 1,056 px a unit (its own `ppu`), when any rendered sprite is placed, then its anchor stays within 0.01 world units of today's (its pivot's distance from the frame's middle across and from its bottom up) and its frame within 5% (people 15%: new bodies). *Amended by ADR 0182 §7: 1% was not reachable for a realistic silhouette (no outline, real stalks, slimmer figures).* | `Game.Editor.SpriteSizeCheck` (new, Editor: every sprite's world size and anchor against `docs/art/sprite-world-sizes.json`, the table of today's recorded before any new art) |
| AC-2 | Every map sprite in `SpriteBank` has its realistic render. | `Game.Editor.RenderedArtCheck` (new, Editor: every name `SpriteBank` asks for has its file). Added with the last art category (Sprint 33). |
| AC-3 | *Withdrawn (ADR 0183): no baked shadows.* | |
| AC-4 | For a new town, a reset or a town with no saved camera (the default half-height moves from `MapInput.OpeningHalfHeight` into `MapCamera`, read from `content/ui.json`) the view is 3 acres across; the closest zoom is 2 acres across; the farthest is unchanged. A town with a saved camera reopens as saved (AC-5). | `IsoGridTests.DefaultAndClosestZoom` (new, Sim.View) |
| AC-5 | A saved camera inside the bounds opens as saved; one outside them opens clamped. | `IsoGridTests.SavedZoomClamped` (new, Sim.View) |
| AC-6 | `content/ui.json` validates the camera fields and their bounds. | `UiContentTests.Camera` (new, Sim.View.Tests: `ui.json` is loaded by `Sim.View`'s `UiLoader`) |
| AC-7 | On the iPhone 13 the device check meets §3.5's bar, or the redesign switches to Option A. | Device: the US-283 test checklist (both p95s, memory, 20 minutes of play) |
| AC-8 | Every category's before/after preview approved before its renders go in; each story's `RenderStats` reading recorded. | Review: the story's ADR |
| AC-9 | Goldens unchanged. | The golden replay tests |
| AC-10 | The ground's three variants: each has the base's size and pivot and an acre's pick is steady and not striped. | `GroundVariantTests.PickIsSteadyEvenAndNotStriped`, `WaterFlowTests.EveryGroundVariantIsRenderedAtTheBaseSizeAndPivot` |
| AC-11 | The river is 12 frames at 10 a second that loop, all at one size and pivot; reduced motion holds frame 0. | `WaterFlowTests.TheLoopIsTwelveFramesAtTenAndComesBack`, `ReducedMotionHoldsFrameZero`, `EveryWaterFrameIsRenderedAtOneSizeAndPivot` |
| AC-12 | Every ground, edge and hill sprite has at most 24 triangles. | `Game.Editor.SpriteSizeCheck` (Editor) |
| AC-13 | Every field crop and garden crop of `content/crops.json` at all four stages, the empty bed, the planting pit and the neighbours' sugarcane is rendered at 8×. | `CropArtTests.EveryCropStageAndBedIsRenderedAt8x` |
| AC-14 | A fodder field is drawn tall once its first cut is due, half cut from a morning's cut until 12:00, then tall again. | `CattleViewTests.Fodder_LooksCut_TillNoon` (the two cut pictures: AC-13's test) |
| AC-15 | Every fruit tree of `content/trees.json` at all four stages, the neem, babul, village tree, the banyan, its panel picture and the temple's peepal is rendered at 8×. | `TreeArtTests.EveryTreePictureIsRenderedAt8x` |
| AC-16 | A repeated tree's size factor is the same every time for its place, within 0.92 to 1.08, and one acre's trees differ. | `TreeSizeTests.FactorIsSteadyWithinEightPercentAndMixed` |
| AC-17 | Every person's map picture (11 kinds, front and back: standing and 4 walk frames; a work frame for the founder, hands, labourers and townspeople: 124) is rendered at 8×, and every one of the 15 portraits is there (US-276). | `PeopleArtTests.EveryPersonPictureIsRendered` (new, Sim.View.Tests) |
| AC-18 | Every person picture the UI asks for (a full-body name, `people` or the founder) resolves to a portrait that is there; any other picture name is unchanged. | `PeopleArtTests.EveryUiPersonPictureIsAPortrait` (new, Sim.View.Tests) |

## 9. Balancing hooks

None: no rule or number of the game changes.

## 10. Open questions

- **ASTC 4×4 on fine detail** (wheat awns, saree borders): looked at on the phone at the device check; ASTC 5×5 or 6×6 for the ground if memory needs it.
- **Spec 033 (the UI restyle)** written in Sprint 32: panels, buttons, fonts and icons to sit with the realistic map.

## 11. Cut first

1. The work frames' extra poses (keep one work frame a kind).
2. *(Shadows: gone, ADR 0183.)*
3. The 4,096 import for the hills (render them at 6× instead).

## 12. The plan (re-sized after the pick)

| Sprint | Stories | Points |
| --- | --- | --- |
| 30 | US-283 the device check (2); US-273 ground, terrain and boundaries, with the river's banks (5); US-274 crops and the kitchen garden (5); US-275 trees and orchards (4) | 16 |
| 31 | US-276 people and portraits: new 6-head bodies, walk and work frames, portraits (13); US-277 animals, vehicles and machines (3) | 16 |
| 32 | US-278 your farm (6); US-279 businesses and stalls (6); US-280 the village and town (6); spec 033 written | 18 |
| 33 | US-281 the map's code-drawn pictures (contact shadow, ring, lit acre, arrow, overlays) (2); US-282 effects, markers and the weather's look (3); the phase's device build; spec 033 reviewed | 5 + review |
| 34–35 | The UI restyle (spec 033) | Sized in spec 033 |

Was 45 points over Sprints 30 to 32; now 55 over Sprints 30 to 33 (people +5 for new bodies, shadows +4 across buildings and trees (dropped by ADR 0183; the points stay as slack), the device check +2, US-281's icons moved to spec 033 −1). Phase 4 starts about three sprints later than ADR 0172 planned.
