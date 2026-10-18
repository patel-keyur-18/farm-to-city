# Sprint 03 build: performance spike and on-device determinism (Phase 0 gate)

- **Build:** the head of branch `sprint/03-market-spike-gate` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`, with the two settings below

**What this build is:** the hello card from Sprint 1 plus a **Phase 0 checks** bar with three buttons. They answer the two device questions of the Phase 0 gate (ADR 0027, ADR 0034 §8):

- **Spike:** a throwaway scene with a 64 × 64 isometric map and 2,000 walking figures, plus a stand-in for the later city simulation, timed frame by frame. On the iPhone 13, the work in a frame must fit in half the frame budget: **8.3 ms** (ADR 0026).
- **Golden replays:** the three recorded test games (001 time and weather, 003 the farm, 004 the market) replayed inside the phone's build. Each must end in exactly the same state as on the Mac (`dotnet test`), which proves the simulation is deterministic on real hardware.

The spike's look is placeholder shapes on purpose; the art check (US-024) is separate.

## Two build settings for this build only

1. **Unity → File → Build Profiles → iOS:** make sure **Development Build is off**. Development builds run slower and would give false numbers.
2. **Xcode → Product → Scheme → Edit Scheme… → Run → Info → Build Configuration: Release.** (Same reason. Change it back to Debug later if you like; Release is fine for every build.)

Then build and install as usual. The build step copies the golden files into the app (the Unity Console shows `ContentBuildStep: copied …`).

## Checks

Before you start: charge the phone above 50%, **unplug it** (on battery), close other apps, and turn off Low Power Mode.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-019 | Open the app | The cream date-and-time card as in Sprint 1, and below it four buttons: **Spike**, **Heat run (5 min)**, **Golden replays**, **Style check** | | |
| 2 | US-023 | Tap **Golden replays** and wait (a few seconds; the screen may freeze briefly while it runs) | A panel lists `001-basic.json`, `003-farm.json` and `004-market.json`, each **PASS** with a hash, then **ALL PASS**. Expected hashes: 001 `0xAA0C65B7299B2C52`, 003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2` | | **Screenshot the panel** |
| 3 | US-023 | Read the last line of the same panel | `72 h away advance: N ms (budget 2,000)`, with N well under 2,000 | | Note N |
| 4 | US-021 | Tap **Spike**. Don't touch the phone for about 75 seconds | The card disappears; a large diamond map fills with small red figures walking about. A line at the top counts down: about 10 s warm-up, 30 s **Whole map**, then the camera zooms in, 30 s **Play zoom**. The figures should move smoothly | | Note any stutter |
| 5 | US-021 | When the countdown ends, read the result panel | Two blocks, **Whole map** and **Play zoom**, each with **PASS** or **OVER BUDGET**, CPU and GPU times (avg, p95, max) and "Frames over 16.7 ms", then a line for the citizen stub. **Pass rule:** p95 CPU ≤ 8.3 ms and p95 GPU ≤ 8.3 ms, and under 1% of frames over 16.7 ms, in both blocks | | **Screenshot the panel** |
| 6 | US-021 | Swipe the app away, wait a minute, open it, tap **Heat run (5 min)**, and leave the phone on a table for 5 minutes | Same as check 4, with two 2½-minute blocks. Then a result panel as in check 5 | | **Screenshot**; note whether the phone feels warm |
| 7 | US-021 | In check 5 or 6, if the panel shows "(no frame timing)" next to the times | It shouldn't: the build turns on frame timing. If it does, the CPU number is the whole frame time instead of the work time | | |
| 8 | US-024 | Swipe the app away, open it, tap **Style check**, watch for 20 seconds | A small farm: grass yard, two soil acres with crops, a fence, hay and a sack, a sandy bank and a river. The season name (top left) changes every 4 s and the ground's colour with it. Every 4 s: a crop bounces, a sack arcs from the field to the store, the coins (top right) count up, and a panel slides up and back down | | **Your view:** does the look suit the valley? The grass, soil and water tiles (one style) against the crops, hay and sacks (another, more realistic style): which do you prefer? Is the motion Hay Day-like enough? |

## What to report

- The screenshots of checks 2, 5 and 6 (or type out the numbers).
- Any **FAIL** in check 2: which file, and the hash it showed. This is the most important result: a mismatch means the simulation gives different answers on the phone, and the Phase 0 gate stops until it's fixed.
- **OVER BUDGET** in check 5 or 6 isn't a failure of the build: it's a measurement. Claude records the numbers in ADR 0035 (US-022) with what to do about them, and you decide at the gate review.
- Crashes: what you did just before. If Xcode is attached, copy the last lines of its console.
- Paste the results back in the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
