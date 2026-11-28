# 0188. US-276 the people and portraits as built

- **Date:** 2026-11-28
- **Status:** Accepted (the owner approved US-276 on 2026-11-28, after approving the before/after preview, Preview 29)
- **Spec:** 032 §3.1, AC-17, AC-18, AC-19

## Context

US-276 is the fourth category of the redesign (spec 032 §12, 13 points): new realistic bodies 6 heads tall for 11 kinds, their walk and work frames, and the portraits. Before its code the owner settled the three questions left by Sprint 30's review and four gaps, one per message.

## The owner's decisions (2026-11-28)

| Question | Chosen |
| --- | --- |
| Review 1: a played town's zoom | It reopens as saved; the spec says so (no behaviour change) |
| Review 2: the sky haze, never seen | Removed (the line in `GameRoot` and the spec's row) |
| Review 3: mixed art | Every person redrawn in US-276 (AC-17); the scaffolded family house and the pucca houses wait for US-278 |
| People shown as UI icons (a 6-head face is about 11 phone pixels on the People button) | **Portraits everywhere in the UI**, with two new labourer portraits |
| The device check's body (a jointed wooden doll up close) | **Rebuilt** before the kinds were dressed |
| The neighbours' tethered cow and buffalo | Moved from US-280 into US-277 |
| The story toast's `portrait_adult_m`, which had no picture | The Chronicle's scroll |

After the preview: the owner asked only that the word "People" on its button be easy to read.

## As built

1. **Renderer:** `tools/blender/real_people.py`. One body for every kind, built from the pose's joints: limbs as single tubes (no ball joints); the head, nose and ears, and a garment with its sleeves, each fused into one surface; a face with lids, brows and lips; cloth laid on the body's surface (pallu, vest, gamchha, vest straps) or made with folds (saree skirt with pleats, frock, kurta hem, dhoti, pyjama, the wound pagdi). Walk, hoeing and the elders' stoop move the one body.
2. **Faults found by looking at the renders, and fixed:** a swinging leg cut through the saree, pyjama and dhoti (the covered leg is now a thin core, and under a saree only the ankle is drawn); hair through the pagdi and sausage-thick cloth (`R.group` resets every solidify to the toon outline's width, so the figure is turned by its own few lines); skin through the shoulders; a pinhole at the crown; the vest's stair-step neckline; tool-holding arms in portraits; a pointed Gandhi topi.
3. **Sizes:** 124 map pictures at 8×, each held to today's frame (crops' method). 15 portraits at 256 px with their own `ppu` 528, the same as before, so that `render.py` skips them (it tells realistic art by that field). Each portrait's bottom tenth is clear, with rounded corners. The two new labourer portraits are in `docs/art/sprite-world-sizes.json` with the other portraits' size.
4. **UI:** `Sim.View.Views.PersonPictures.InUi` turns any person picture into its portrait; `Thumbnail`, `IconBank.Good` and the People button use it. Only the picture changes. The People button's picture is 96 units and lifted 18, as the Orders bag is, so the word stays on the same line as "Orders" and "Menu", clear below the portrait.
5. **The art check** no longer stands anyone in for people (`ArtCheck.StandIn`).
6. **The toast fix** is in `StoryToastVm` (`scroll` when the person is gone).

## Known limits (told to the owner with the preview)

- At the default zoom the new people read much smaller than the toon ones: about the same height, far slimmer, faces barely visible.
- Men share one face and women another; clothes, hair, headwear, moustache and glasses tell them apart.
- Children look like small adults (6 heads, as spec 032 says).

## Measured (the Editor, a fresh Village bot town, art check off; `docs/planning/render-stats.json` "US-276 people…")

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| Whole map | 1,254 | 47,410 | 1,558 |
| Village | 614 | 21,197 | 730 |
| Farm | 221 | 7,585 | 232 |

No metric grew over 10% against US-275's readings. The whole map shows 150 fewer sprites than then: this town was generated afresh and stands in a different state, so its drop is not put down to the people. Rendered sprites: about **209 MB** (from 200; people and portraits 3.9 to 11.9 MB).

**Editor check:** `SpriteSizeCheck` 0 problems; `SheetLayoutCheck` 86 sheets, 0 problems; the only console errors were the Editor command line timing out. By scripted touch in the Village bot town: People, Hire labourers (hired), the Town tab, a family and its tree, the Menu, the school (a tap on the map), and the People button at 100% and 125% text. Townspeople and neighbouring labourers were captured walking and hoeing. The two story toasts (a living person, a person gone) were raised through `QueueStory` by the Editor, not by play. Test cash was added for the second hire. Pupils in the school yard and an elder walking were not captured. Saves restored identical.

## Tests

| AC | Test |
| --- | --- |
| AC-1 (size and anchor) | `Game.Editor.SpriteSizeCheck` |
| AC-17 (124 pictures at 8×, 15 portraits) | `PeopleArtTests.EveryPersonPictureIsRendered` |
| AC-18 (the UI's person pictures are portraits) | `PeopleArtTests.EveryUiPersonPictureIsAPortrait` |
| AC-19 (the toast's picture) | `ChronicleViewTests.Toast_PictureIsAlwaysThere` |
| AC-8 | this ADR, Preview 29 and the readings |
| AC-9 | the golden replay tests (no sim, rule, balance or save change) |

## Consequences

- The art check's memory ballast still has to be recomputed before the phase's device build (ADR 0185).
- Old art still drawn: buildings (US-278 to US-280), animals and vehicles (US-277), effects and code-drawn pictures.
