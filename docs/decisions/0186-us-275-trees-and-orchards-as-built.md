# 0186. US-275 the trees and orchards as built

- **Date:** 2026-11-28
- **Status:** Accepted (built; the owner's review pending)
- **Spec:** 032 §3.8, AC-15 and AC-16

## Context

US-275 is the third category of the redesign (spec 032 §12, 4 points): the five fruit trees at four stages, the trees nobody plants, and the orchards and groves that repeat them. The story's title said "with their shadows"; baked shadows were dropped by ADR 0183, so there are none. Three spec gaps were closed one per message before any code, and the owner approved the before/after preview before any render went into the game.

## The owner's decisions

| Gap | Options | Chosen |
| --- | --- | --- |
| How crowns are built | leaf by leaf; soft leaf clumps | **Leaf by leaf** |
| Size of ripe fruit | a bit larger than life; true to life; as big as the toon art | **A bit larger** (see "Fruit size" below) |
| Variety in repeated trees | a slight size mix; a second tree shape; identical | **A slight size mix** |

## As built

1. **Renderer:** `tools/blender/real_trees.py`. Each toon blob becomes a clump of leaf sprays with limbs and twigs showing through the gaps. Each kind has its own leaf: long dark mango leaves, oval guava and apple leaves, neem and babul midribs with leaflets (babul with thorns and yellow puffs), broad banyan leaves with prop and aerial roots, torn banana paddles, seven-lobed papaya leaves. It reuses the crops' leaf blade, leaf colour gains and frame lock (ADR 0185).
2. **Sizes:** 25 renders at 8×, every one within AC-1 (worst frame 4.1%, worst anchor 0.0095 units).
3. **No shade:** the neem's and village tree's flat shade disc is a scatter of fallen leaves; the sapling's watering basin and the banyan's floor and platform keep today's design, lit mostly by their own colour so cast shade barely marks them.
4. **Size mix:** `Sim.View.Views.TreeSizes.Factor(place, n)` gives 0.92 to 1.08 in 17 steps from a fixed hash. It applies to orchard trees, neighbours' mango groves, the babul on scrub and the mango on the far bank. Single trees (yard, field edge, village) are not scaled. Presentation only: no sim, save or content change, and the goldens don't move.
5. **Fruit** is smooth-shaded, in bunches on the side the camera sees; bananas in hands of curved fingers with the bud, papayas hugging the trunk.

## Fruit size

The question offered "about 1.5 times life size". That could not be met: at 1.5× a mango, guava or apple is a few pixels and does not show at the game's zoom. As drawn, they are **about half as long as the toon art's** and **about 2.5 to 4 times life size** beside their tree. The owner was told this with the preview and approved it. Banana and papaya fruit were not measured against real fruit.

## Measured (the Editor, the Village bot town, art check off; `docs/planning/render-stats.json` "US-275 trees…")

| View | Draw calls | Triangles (against US-274) | Visible sprites |
| --- | --- | --- | --- |
| Whole map | 1,357 (1,357) | 49,589 (**+14%**) | 1,708 |
| Village | 611 (611) | 21,132 (**+10%**) | 727 |
| Farm | 220 (220) | 7,536 (+5%) | 231 |

Draw calls are unchanged. The triangles come from ragged leaf outlines in the tight sprite meshes: 9 to 177 a tree (mostly under 90), against about 154 for a field of wheat. Left as they are. Trees go from about 2.5 to 10 MB; all rendered sprites from about 193 to **200 MB**.

**Editor check:** `SpriteSizeCheck` 0 problems; `SheetLayoutCheck` 86 sheets, 0 problems; no game errors in the console (the two logged errors are the Editor command line timing out). In a copy of the Village bot town, trees were planted (a mango and a banana orchard, four edge trees) and game time moved on 8 days; the ripe banana orchard, ripe yard and edge trees, the sapling orchard, a grove, the scrub's babul, the cattle acre and the banyan square were captured and looked at (`docs/art/s30-275-*.png`; the farm and grove captures use the developer panel's sun look). Scripted taps opened the banyan's panel (with its new picture), a guava tree's and the banana orchard's. A tree standing in front of an orchard takes the tap, as before. Saves restored identical.

## Tests

| AC | Test |
| --- | --- |
| AC-1 (size and anchor) | `Game.Editor.SpriteSizeCheck` |
| AC-15 (every tree picture at 8×) | `TreeArtTests.EveryTreePictureIsRenderedAt8x` |
| AC-16 (the size mix) | `TreeSizeTests.FactorIsSteadyWithinEightPercentAndMixed` |
| AC-8 | this ADR, the preview and the `RenderStats` readings |
| AC-9 | the golden replay tests |

## Flagged for the owner

- **The temple's peepal was missed in the plan check** (`village_peepal`, a picture of its own beside the temple). It is drawn realistic (`real_trees.peepal`, within AC-1) and added to the preview, but **is not in the game** until the owner has seen it.
- **The triangle growth** above.
- The art check's memory ballast still has to be recomputed before the phase's device build (ADR 0185).

## Consequences

- Spec 032 gains §3.8, AC-15 and AC-16. US-275 is the last story of Sprint 30. AC-2's `RenderedArtCheck` waits for the last category (Sprint 33).
- Buildings, people, animals and vehicles still draw today's art until their stories.
