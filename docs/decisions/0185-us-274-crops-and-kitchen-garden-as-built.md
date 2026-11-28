# 0185. US-274 the crops, the kitchen garden's beds and the planting pit as built

- Status: Accepted (the owner approved the before/after preview and the built story, 2026-11-28)
- Date: 2026-11-28
- Deciders: Keyur Patel
- Related ADRs: 0180 (the direction), 0182 (the 8× pipeline, the art check), 0183 (no baked shadows), 0184 (the ground)

## Context

US-274 is the second category of the redesign (spec 032 §12, 5 points): every field crop at every stage, the kitchen garden's beds and the planting pit, rendered realistic at 8×. Wheat had been rendered and approved at the device check (US-283). Its three spec gaps were closed one per message before any code, and the owner approved the before/after preview before any render went into the game.

## Decisions (the owner, 2026-11-28)

1. **A full dense canopy** (picked over the recommendation, "match the wheat"): about three times today's plants and leaves; once grown the fields close over.
2. **The paddy's water is still** (the recommendation): muddy, glossy, no frames, no memory.
3. **The same plank bed** (the recommendation): the design kept, with realistic wood, crumbly soil and 5 × 5 plants (was 3 × 3), so a bed stays easy to find and tap.
4. **Wheat is re-rendered dense too** (put to the owner in the preview, with the approved wheat as *Today*, and approved): beside the other crops the device-check wheat looked sparse and its old rounded bands showed as cream tubes. It keeps the same clumps and colours.

## What was built

- `tools/blender/real_crops.py` (new; `render_real.py` calls it): each crop keeps today's plant shapes and colours with pointed leaf blades folded along the midrib (broad leaves barely folded) in place of flat ellipsoids; plants along the seven ridges and, once grown, a second set in the furrows; a scatter of small leaves in place of the toon "mat" bands. New plants for **rice** (blades and drooping panicles of small grains), **potato** (stems with three broad leaflets, a few flowers, tubers when ripe) and **radish** (ripe: white tops with the furrows left open, so the stage reads). Beds: plank sides that stop at each other (overlapping faces render black), 5 × 5 plants.
- **Colour** (found by looking): under AgX the raw palette's leaves render pale and milky, so green materials take a per-channel gain and a matt surface, as the ground did (ADR 0184); chaulai's red, ripe masoor and ripe sarson have their own gains (ripe sarson browner than ripe wheat); sarson's flowers also carry a faint light of their own **in the render only** (reflected bright yellow comes out cream). No Unity shader, light or particle.
- **Size and anchor** (spec 032 AC-1): `hold_frame` widens a render with points the camera never draws until its frame is today's, and reports a model that reaches past it. All 69 sprites (65 of this story and 4 of wheat) are within the limits: the worst frame 3.4% off (limit 5%), the worst anchor 0.0073 units (limit 0.01).
- **The art check** no longer stands wheat in for every crop (`ArtCheck.StandIn`): the crops are real now.
- No sim, content, save or presentation-code change beyond the art check; the goldens do not move.

## Measured (the Editor, the Village bot town, art check off; `docs/planning/render-stats.json` "US-274 crops…")

| View | Draw calls (US-273) | Triangles (US-273) | Visible sprites |
| --- | --- | --- | --- |
| Whole map | 1,357 (1,365, −0.6%) | 43,432 (31,568, **+38%**, flagged) | 1,708 (1,708) |
| Village | 611 (619, −1.3%) | 19,135 (13,650, **+40%**, flagged) | 727 (727) |
| Farm (default view) | 220 (228, −3.5%) | 7,182 (5,784, **+24%**, flagged) | 231 (231) |

The triangles are the crops' tight sprite meshes following a leafier outline: crops are about 23,000 of the 42,000 visible triangles at the whole map, about 156 a crop sprite (one sparse seedling stage, methi's first, is about 750). **Left as it is:** draw calls and the sprites on screen did not grow, a tight outline leaves less transparent area for the GPU to fill than a coarse one (the GPU is the tighter budget, ADR 0094), and 43,000 triangles is small for the iPhone 13; a coarse mesh as the tiles have (ADR 0184) is the remedy if the phase's device build shows otherwise.

**Memory:** the rendered sprites in ASTC 4×4 with mipmaps go from about 153 MB to about **193 MB** (320 MB is a target, not a hard limit, ADR 0184).

**Editor checks:** `SpriteSizeCheck` 0 problems; `SheetLayoutCheck` 89 sheets, 0 problems; the tour by scripted touch at the opening view, the village, the whole map and the closest zoom, with taps that opened a garden bed, a planting pit, a field of the player's, a neighbour's land and a tree; every capture looked at (`docs/art/s30-274-*.png`); no console errors; the saves restored and identical.

## Tests

| AC | Test |
| --- | --- |
| AC-1 (size and anchor) | `Game.Editor.SpriteSizeCheck` (0 problems) |
| AC-13 (every crop picture exists at 8×) | `CropArtTests.EveryCropStageAndBedIsRenderedAt8x` |
| AC-8 | this ADR, the preview and the `RenderStats` readings |
| AC-9 | the golden replay tests |

## Flagged for the owner

- **The "fodder cut" pictures are never shown by the game.** `crop_berseem_cut` and `crop_sorghum_cut` are re-rendered (and were in the preview), but no code asks for them: `SpriteBank.Crop` knows only the four stages, today as before this story. A field being cut draws its normal stage. Showing the cut state would be a small presentation change; it is not made without the owner's word.
- **The triangle growth** above.
- **For the phase's device build:** the art check's 271 MB memory ballast (US-283) was sized before the ground, the river and the crops were real; it must be recomputed from `sprites.json` before that build, or it counts them twice.

## The owner's review (2026-11-28)

Approved, on the condition that the game matches the approved preview. The one difference is the fodder-cut state (flagged above): the preview showed it and the game does not draw it. The owner chose **half cut from the 6:00 cut until noon** (over two hours, all day, or leaving it out).

- Built: `TileVm.FodderCut` and `MapView.FodderCutUntilHour` (12), `SpriteBank.Crop(…, fodderCut)`. Once the first cut is due the field is drawn tall (before this, its picture followed the whole stand's progress, which would have shown seedlings after a cut). No sim, save or content change.
- A "real cut" is a morning pass after the first cut is due, in a cut season, today.
- Tested: spec 032 AC-14; in the Editor a berseem field sown in the Village bot town and moved to the next day: half cut at 7:00 (`docs/art/s30-274-fodder-cut.png`), tall at 12:01, a scripted tap opened its panel, saves restored identical.

## Consequences

- Spec 032 gains §3.7 and AC-13. US-275 (trees and orchards) follows the same rules.
- Trees, buildings and people still draw today's art on the new ground and fields until their stories.
