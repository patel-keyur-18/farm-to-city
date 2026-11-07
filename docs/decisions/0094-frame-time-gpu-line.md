# 0094. Frame time: the GPU gets its own line (11 ms), the main thread keeps 8.3 ms

- Status: Accepted (owner, 2026-11-07: "don't be too rigid; we can increase a bit")
- Date: 2026-11-07
- Deciders: Keyur Patel
- Amends: ADR 0035 and ADR 0047 §4 (the pass was main and GPU p95 ≤ 8.3 ms); the GDD's Frame rate row
- Related: ADR 0026 (iPhone 13, half the frame budget), ADR 0083 §18 (the weather look's frame time)

## Context

The Sprint 16 Development build passed every device check except frame time. On the iPhone 13, on the owner's own town with the whole map in view (Hamlet, Monsoon, cloudy), the developer panel measured:

| | Measured | Line |
| --- | --- | --- |
| Main thread p95 | 4.93 ms (avg 3.83, max 12.39) | 8.3 ms |
| GPU p95 | 9.21 ms (avg 8.81) | 8.3 ms |
| Frames | 60.0 fps, 0.00% over 25 ms | about 60, under 1% |

The game ran at a steady 60 fps with no dropped frames. Only the GPU reading was over its line.

The GPU's time has grown with the map: 2.81 ms in the Phase 0 spike, 4.60 ms in Sprint 11 (the village map), 6.54 ms in Sprint 13 (the townspeople), 9.21 ms now. Sprint 15's weather look (a post-processing colour volume and full-screen wash, mist and cloud-shadow layers) and Sprint 16's machines, orchards, channels and shops all draw more. Sprint 15's device check passed without its numbers written down, so the size of each step isn't known.

ADR 0026's rule is that sim and rendering fit in half the 16.7 ms frame on the iPhone 13, so older phones still reach 60 fps. On iOS the GPU draws one frame while the CPU prepares the next, so their times don't add up: each only has to stay under 16.7 ms. The main thread is the one that also runs the sim and the catch-up after time away, so its headroom matters most.

## Options

1. **Keep 8.3 ms for both and optimise the GPU now** (fewer full-screen layers, post-processing only in weather that needs it). Costs a sprint's time for frames the player can't see.
2. **Drop the GPU line and judge by fps only.** Nothing would warn us until frames drop, by which point the design-minimum phone (iPhone 11 in the GDD) would already be dropping them.
3. **Give the GPU its own line at two-thirds of the frame, 11 ms, and keep the main thread's at 8.3 ms.** The iPhone 11's GPU is roughly a third slower than the iPhone 13's, so 11 ms here is about 15 ms there, still inside the frame.

## Decision

Option 3.

- **The pass is:** main thread p95 ≤ 8.3 ms, GPU p95 ≤ 11 ms, about 60 fps, and under 1% of frames over 25 ms. `DevTools.FrameRecorder` checks and prints both lines.
- **Sprint 16's frame time passes** under it (GPU p95 9.21 ms).
- **Every device check writes down both p95s**, so growth is seen sprint by sprint.
- **If the GPU's p95 passes 11 ms,** the first things to try are drawing the weather's colour volume and wash only in weather that changes the colour, and fewer full-screen layers. That's a task in the sprint that crosses the line, not now.

## Consequences

- The Township and Town maps (Phases 3 and 4) have about 1.8 ms of GPU room before the line, so their sprints should measure early.
- The main thread's line is unchanged, so the sim and catch-up budget is the same.
- Specs 008 (AC-21) and 010 already name only the main thread's line, and are unchanged.
