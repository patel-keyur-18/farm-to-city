# 0189. US-277 the animals, vehicles and machines as built

- **Date:** 2026-11-28
- **Status:** Built; waiting for the owner's review (the owner approved the before/after preview, Preview 30, on 2026-11-28)
- **Spec:** 032 §3.9, AC-1, AC-20, AC-21

## Context

US-277 is the fifth category of the redesign (spec 032 §12, 3 points): the cattle, the machines, the trader's truck, the supermarket's van and the neighbours' tethered cow and buffalo (moved from US-280, ADR 0188). The plan check looked at every old-art map picture (one facing of each building and stall) and the code that draws them: 20 pictures.

## The owner's decisions (2026-11-28)

| Question | Chosen |
| --- | --- |
| The plough and the working tractor had nobody at them | **Somebody works both**, as art only |
| Hiring or assigning a person to them, and asking the player to hire first when nobody is free (a game rule and a save change) | **Backlog until all the art is done** (F21.4, "After the redesign") |
| The tractor: open, or with its roof (the roof hid the driver's face, and raising it put the frame 6.6% past today's) | **Open**, with a bar behind the seat |
| The ploughman: people's size, or the bullocks' scale (75%) | **People's size** |

## As built

1. **Renderer:** `tools/blender/real_animals.py`. An animal is one coat (barrel, neck, head and legs fused), with muzzle, eyes, ears, horns, hooves, udder and tail; the buffalo's coiled horns, the cow's patches (a procedural coat), the bullock's hump, dewlap and long horns. Vehicles are bevelled painted bodies on treaded tyres (the tractor's rear tyres with chevron lugs), with jute sacks in the truck and tempo. Built at the old models' places and measures.
2. **The tractor's driver** is in `tractor_cultivator` only: the farm hand's clothes, a new seated pose in `real_people.py` ("drive"), at 80% of walking people's size. The plain tractor (screens only) and the parked tractor with its trolley have nobody. The tractor's roof is gone from all three.
3. **The ploughman** is not in the plough's picture (he stood 23% above its frame in a trial). `MachineVm.Operator` names an existing person picture (`person_hand_m`) for the plough team at work; `MapPresenter.Machines` draws its front, at people's size with a contact shadow, 0.085 units right and 0.084 up from the team's pivot, behind the plough's handle. He is a picture only: a tap on him opens the field.
4. **Faults found by looking at the renders, and fixed:** lying animals cut off below the belly and their folded legs hidden; sacks reading as potatoes; the truck's saffron and green washed pale; the buffalo reading blue-grey; the roof over the driver's face; the cultivator, the truck's mirrors, the tempo's nose and the tether's peg past today's frames.
5. Every render in the game is pixel-identical to the approved preview's. People re-rendered after the pose was added are pixel-identical (seven checked).

## Known limits (told to the owner with the preview)

- At the default zoom the new cattle look slimmer and plainer than the toon ones, and their eyes barely show.
- Eating heads don't reach into the mangers (today's don't clearly either).
- The bullocks look small beside the ploughman: the plough team is drawn at 75% (ADR 0088), so their backs are about 0.61 of his height.
- The parked tempo overlaps the tractor's trolley, as before (their places are code, unchanged).
- The game draws the van only from the back; `van_front` is rendered and not drawn.

## Measured (the Editor, a fresh Village bot town, art check off; `docs/planning/render-stats.json` "US-277…")

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| Whole map | 1,199 | 45,984 | 1,492 |
| Village | 614 | 21,267 | 730 |
| Farm | 221 | 7,623 | 232 |

No metric grew over 10% against US-276's readings (the whole map's town stands in a different state, as before). Rendered sprites: about **212 MB** (from 209; the 20 pictures 1.1 to 4.3 MB).

**Editor check:** `SpriteSizeCheck` 0 problems; `SheetLayoutCheck` 86 sheets, 0 problems; the only console errors were the Editor command line timing out. In a copy of the Village bot town the Editor added test cash, built both sheds, bought the plough, three cattle, the tractor and (with the era raised for the one purchase) the tempo, cleared two ripe fields, sowed radish and moved the clock. Captured: the plough and ploughman at work, the tractor and driver at work, the cattle yard by day and night, the parked tractor and tempo, the neighbours' animals, the truck and van, and Menu → Equipment (opened by scripted touch). A scripted tap on the ploughman opened the field. Not captured: a young cow (70% in code, unchanged) and the truck driving in. Saves restored identical.

**Found in passing, not from this story:** after a season change two toasts (a story and the milestones) can be drawn over each other, hiding the story's text.

## Tests

| AC | Test |
| --- | --- |
| AC-1 (size and anchor) | `Game.Editor.SpriteSizeCheck` (the 20: frames within 3.6%, anchors within 0.0038) |
| AC-20 (20 pictures at 8×) | `AnimalArtTests.EveryAnimalAndVehiclePictureIsRendered` |
| AC-21 (a ploughman at work, nobody parked) | `EquipmentViewTests.Map` |
| AC-8 | this ADR, Preview 30 and the readings |
| AC-9 | the golden replay tests (no sim, rule, balance or save change) |

## Consequences

- The cattle yard's other old pictures are US-278's (the shed, mangers, water trough, straw stack, compost and silage pits, chaff cutter; Sprint 32) and US-282's (the padlock sign; Sprint 33). Its ground, hedges, gates and neem are already realistic.
- Who works the plough and the tractor stays a picture until its own spec, after all the art.
