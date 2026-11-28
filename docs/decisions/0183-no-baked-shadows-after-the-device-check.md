# 0183. After the device check: no baked shadows (Option A), a gap between two houses on an acre

- Status: Accepted (the owner, 2026-11-27)
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0094, 0180 (Option B superseded), 0182 (the shadow mechanism removed)

## Context

The US-283 Development build (ADR 0182) was read on the iPhone 13 by the owner, art check ON, in the Village bot town, at the farthest zoom with the ballast loaded:

| Reading | Result | Bar |
| --- | --- | --- |
| Main thread | avg 3.54 ms, p95 **4.43 ms**, max 27.23 ms | p95 ≤ 8.3 ms |
| GPU | avg 6.99 ms, p95 **7.94 ms** | p95 ≤ 11 ms |
| Frames | 59.3 fps, **2.03% over 25 ms** (1,185 frames) | under 1% |
| Memory | total 216 MB, graphics driver 471 MB (with the 271 MB ballast) | sprites ≤ 320 MB |

Both p95s pass with room, so the realistic art itself is affordable (the memory figure is the ballast plus the stand-ins). The panel said FAIL because of the share of frames over 25 ms (2.03%), not because of the GPU.

## Decisions (the owner, 2026-11-27)

1. **No baked shadows at all** ("I don't want shadows at all"). Option A of ADR 0180: the building shadows of US-283 (`_shadow` sprites, `ShadowLayer`, the weather `shadow` values, `WeatherFrame.Shadow`) are removed, with AC-3 and the shadow part of AC-6 (`UiContentTests.CameraAndShadow` is now `UiContentTests.Camera`) and the shadow stand-ins of the art check. The render script no longer has a shadow pass. The measured cost it would have had, +9.6% draw calls, is not paid.
2. **Today's contact oval stays** under people, animals, vehicles and some buildings (asked, one message): US-281 softens it to match, as spec 032 planned.
3. **Everything else of US-283 is approved**: the realistic house, wheat and hands, the camera (3 acres across, closest 2), ASTC on iOS, the per-sprite `ppu`, the art check and ballast for the later device builds. "Everything looks good and I am satisfied."
4. **A gap between two houses on one acre.** They were attached (ADR 0067 had them sharing a wall). The two plots now stand ±0.24 of an acre from its middle instead of ±0.168, so about 0.12 of an acre of open ground shows between the courtyard walls, and the outer walls stay inside the acre (`MapPresenter.Houses.cs` `HousePlots`). Presentation only; no sim, content or save change.

## Not recorded at this check (Sprint 30 review)

Only one reading is on record: art check on, the whole map. The default view's reading, the reading with the art check off, the 20-minute heat check (part of spec 032 §3.5's bar) and the look at ASTC softness (§10) are not recorded; the owner approved with "everything else looks good". All four are on the phase build's list (F21.4).

## Not decided here

- **The 2.03% of frames over 25 ms** is not a GPU problem (p95 7.94 ms) and removing shadows is not known to fix it: the max main-thread frame was 27 ms, so single hitches (the ballast and the realistic textures loading, the garbage collector, the 20-second window's first seconds) are likelier than sustained load. It is read again at the phase's last Development build (spec 032 §3.5, ADR 0172 §8) with the art check on and off; if it stays over 1% there, finding the hitches is a task before that build's pull request.

## Consequences

- Spec 032 §3.3, §2, §4, §11, AC-2, AC-3, AC-6 and the plan are corrected; the GDD's art line says no cast shadows. ADR 0180's Option B is superseded; ADR 0182's mechanism is removed (its other parts stand).
- The sprint's points do not change (the shadow work was inside US-283's 2 and the later stories' sizes; the freed effort is slack).
- US-273 (ground, terrain, boundaries, sky, river banks), US-274 (crops, kitchen garden) and US-275 (trees, orchards) go ahead, each with a plan check, spec gaps one per message and a before/after preview at the new default zoom before its renders go in.
