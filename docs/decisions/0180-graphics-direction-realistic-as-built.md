# 0180. US-271 the graphics direction: a realistic valley (the samples, the pick and spec 032)

- Status: Accepted (the owner picked from the samples and approved spec 032, 2026-11-27)
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0007, 0036, 0041, 0094, 0172 (§7, §8 amended)

## Context

ADR 0172 §7 asked for three samples (a family house, a wheat field, a person) in a realistic and a rich stylised look, rendered in Blender for the owner to pick from, and then a spec. The inventory (605 sprites, eight kinds drawn in code) was done on 2026-11-26.

## What was built

`tools/blender/samples.py` renders each subject three ways from the same models, camera and light direction as the game's sprites, so only the style differs: **today** (the game's sprite as it is), **rich stylised** (painted surfaces, softened light bands and a warm rim, a thinner outline, a 4-head figure) and **realistic** (Cycles, physically based materials with a pattern, roughness and bump each, no outline, soft sun and sky light, a baked cast shadow, true proportions). The subjects: today's family house with the detail a real one has (bare brick where the lime fell, panelled doors, a barred window with shutters, tile ribs, a roof tank, a marigold garland, a tulsi on its chabutara, a charpai with rope, dung cakes, a pebbled yard); a wheat acre of 1,428 stalks with real ears and awns (today about 490); the farm hand and his wife (the Sprint 5 look: pagdi, dhoti and vest; a cotton saree with the head uncovered). `render.py` runs `main()` only when run directly so `samples.py` can import its models (the game's sprites are unchanged). `FastBatch` builds a field in 0.2 s instead of 30 s. Preview 27 (artifact, `docs/art/s29-preview27-art-samples.html`) showed the three looks at the opening view's true size, at the closest zoom's and as rendered, with each look's cost.

## Decision (the owner, one per message, 2026-11-27)

1. **Realistic, Option B** (the owner liked it and asked what it costs): baked shadows on buildings only, kept short (sun at 60°); not Option A (no baked shadows) and not C (shadows on everything, over the GPU budget at the crowded view by estimate: 12 to 13 ms against 11).
2. **The default zoom 3 acres across** (the owner's "render pixels" zoom is 1.2 acres across, too close to play; 3 acres is today's closest zoom), the closest zoom 2 acres across.
3. **A device check first** (US-283, 2 points): the realistic house, field and people at the new zoom on the iPhone before the other sprites are re-rendered; a GPU p95 over 11 ms means Option A.
4. **A building's shadow is its own sprite** so weather and night can fade it (the strength is the day's light times a weather value, placeholders tuned on the phone).
5. **People 6 heads tall** (not 7.5): a face just shows at the default zoom; portraits rendered realistic and close up for the cards.
6. **The UI is restyled as a whole**, in its own spec 033 written in Sprint 32; **art first, the UI after** (Sprints 34 and 35).

## Consequences

- Spec 032 (approved) is the redesign's spec: the style, the light, 8× resolution with ASTC on iOS (memory budget 320 MB), shadows, the camera, the frame-time guard, and the plan. Its plan replaces ADR 0172's: **55 points over Sprints 30 to 33** (was 45 over 30 to 32; people +5, shadows +4, the device check +2, the icons −1 to spec 033), then the UI. **Phase 4 starts about three sprints later than ADR 0172 planned.**
- ADR 0172 §7 is answered; §8's bar (about 60 fps, no dropped frames, no heating, `RenderStats` in every story, both p95s at the phase's build) stands and is made concrete in spec 032 §3.5.
- The GDD's art section names the realistic valley. No sim, content-balance or save change; goldens unchanged.
- Unknowns carried to the device check, not decided: the GPU cost of the 8× realistic sprites and their shadows (estimated 10 to 11 ms), ASTC 4×4 on fine detail, and how the cartoon UI sits on the realistic map until spec 033.
