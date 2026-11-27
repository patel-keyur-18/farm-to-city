# Sprint 30 build: the device check for the realistic valley (US-283)

> **Read by the owner on 2026-11-27** (ADR 0183): main p95 4.43 ms, GPU p95 7.94 ms, 59.3 fps, 2.03% of frames over 25 ms, memory ok. **Baked shadows were dropped afterwards**; the shadow rows below (the soft shadow under houses and trees, the weather fade of shadows, rows 10 and 13) describe the build as measured and no longer apply to the game. The next device build reads the frame time again.

- **Build:** the one Development build of Sprint 30, from the head of branch `sprint/30-art-land` (`BuildCli.IosDevelopment`). It has the realistic house, wheat acre and the hand and his wife at 8× with the new camera (3 acres across), and a **developer-panel switch, *Art check***, that stands those in for every house, crop and person on the map and for every other building's and tree's shadow, and loads a memory ballast (spec 032 §3.5, ADR 0182). **Off by default.**
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **What this decides:** if the GPU p95 is **over 11 ms** with the art check on, the redesign goes on as **Option A** (no baked shadows) and comes back to you. If it passes, the other art stories go ahead when you say so.
- **How to install:** `how-to-build-ios.md`. Quit Xcode before building; `Builds/iOS` is deleted first.

## Before you install: keep your town safe

1. In the old app: Menu → Settings → **Export**. Keep that copy.
2. **Install over the old app. Don't delete it first.**
3. This build changes no save. Your town loads as it is.

## The town for the check

The check should be read in a Village town (the most crowded view). **Import** `FarmToCity-art-check.ftc` (AirDrop it from the Mac: it is on the Desktop): Menu → Settings → **Import a town**. It is the Village bot's town (seed 1, game day 44, 6 households, a business, the school). **It replaces the open town** (your export is the way back).

## Numbers to write down

| Reading | Bar | Art check **off** | Art check **ON** |
| --- | --- | --- | --- |
| Main thread p95, default view | ≤ 8.3 ms | | |
| GPU p95, default view | ≤ 11 ms | | |
| Main thread p95, farthest zoom (whole map) | ≤ 8.3 ms | | |
| GPU p95, farthest zoom | ≤ 11 ms | | |
| Over 25 ms / fps | under 1% / about 60 | | |
| Memory (Xcode's *Memory* gauge, Debug navigator) | ≤ 320 MB for sprites (the app's whole figure is more: report both) | | |
| The panel's memory line | (total, graphics driver, ballast) | | |
| Heat after 20 minutes | warm, not hot; no iOS thermal warning | | |

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app, art check **off** | The valley as today, except: the camera opens **3 acres across** (closer than before), your family houses (if the town has any) are realistic with a soft shadow behind, the hand and his wife realistic, wheat fields realistic. The rest is today's art | | Editor ✓ (`s30-283-off-farm.png`) |
| 2 | Tap the home button / Menu → a place | The view returns to the same 3 acres across | | Editor ✓ |
| 3 | Pinch all the way in | The closest zoom is **2 acres across**; sprites stay sharp (the 8× ones look crisp, today's softer) | **Device only** | |
| 4 | Pinch all the way out | The whole map: the farthest zoom is as before | | Editor ✓ |
| 5 | Developer panel (hold the time card for 3 seconds) → scroll to **Art check** → tap it (it says ON) → **quit the app and open it again** | After the relaunch the town loads; every house is the realistic one, every field is realistic wheat, every person is the hand or his wife, buildings and trees have a soft shadow under them | **Device only** | Editor ✓ (`s30-283-on-farm.png`, `s30-283-on-village.png`, `s30-283-on-all.png`) |
| 6 | Look at the **iOS compression** (ASTC): old art (huts, trees, ground, the HUD's pictures) | Outlines and fine detail are as sharp as before. **Report any softness or banding** (spec 032 §10: ASTC 5×5 or 6×6 for the ground if memory needs it, 4×4 stays for detail) | **Device only** | |
| 7 | Art check **ON**, default view: the developer panel → **Measure frame time (20 s)**; don't touch the screen | The report prints main and GPU p95, over 25 ms, fps, **and a memory line**. Write them in the table | **Device only** | |
| 8 | Same, at the farthest zoom (pinch out first) | Same; this is the crowded view | **Device only** | |
| 9 | Repeat 7 and 8 with the art check **off** (panel → Art check → off → relaunch) | The baseline on the same town | **Device only** | |
| 10 | Weather look ▸ in the panel: step through sun, cloudy, rain, heavy rain, storm | A house's shadow is firm in sun, soft in cloud, faint in rain, almost gone in a storm; it fades with the change (3 s) | **Device only** | Editor ✓ (`WeatherLookTests.ShadowStrength`) |
| 11 | Play with the art check ON for **20 minutes** (pan, zoom, open sheets) | The phone is warm, not hot; no thermal warning; no memory warning; still about 60 fps | **Device only** | |
| 12 | Look at the realistic hand and wife close up (pinch in on a person) | Door-high, 6 heads tall, a face just shows; the man's pagdi and the woman's saree with the head uncovered. Figures slide when they walk (no leg swing: walk and work poses come in US-276) | **Device only** | |
| 13 | Check a house at dusk and at night (change the phone's clock, or wait) | The shadow fades with the day's light; none at night | **Device only** | |

## What to report

- Both p95s at both views, art check on and off; the fps and the over-25 ms share.
- The memory: Xcode's number and the panel's line. The sprite budget is 320 MB (spec 032 §3.2); the ballast brings sprite memory to the computed 271 MB of the finished redesign.
- Whether the 20-minute session got hot.
- Any softness in the ASTC-compressed art, any visual wart on the realistic house (its shadow, the courtyard), the wheat or the people.
- If the art check ON fails any bar: say which. That decides Option A.

## What the Editor showed (Village bot town, `RenderStats`, the Editor's numbers, not the phone's)

| View | Draw calls, off → ON | Visible sprites | Triangles |
| --- | --- | --- | --- |
| Whole map | 1,326 → 1,453 (+9.6%) | 1,677 → 1,804 | 31,957 → 40,819 (+28%, flagged) |
| Village | 594 → 636 (+7.1%) | 710 → 752 | 13,899 → 17,710 (+27%, flagged) |
| Farm (default view) | 207 → 216 (+4.3%) | 218 → 227 | 5,041 → 6,197 (+23%, flagged) |

The shadows are the extra draw calls (one a caster in view; 169 shadow sprites in the town). `spritePixels` (the screen area the visible sprites' frames cover, in thousands of pixels) moves under 2%: the realistic sprites keep their world size.
