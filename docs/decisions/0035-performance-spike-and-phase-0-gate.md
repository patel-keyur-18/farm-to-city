# 0035. The performance spike's result and the Phase 0 gate

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: ADR 0034 §8 (the spike's pass rule: dropped frames over 25 ms; the render thread is reported for diagnosis only)
- Related: ADR 0026 (iPhone 13, half the frame budget), ADR 0027 (on-device determinism in the Phase 0 gate), ADR 0018 (Sim.Core as a .NET library in Unity), ADR 0016 (balance), ADR 0034 §7 and §8 (the gate checks and the spike)
- GDD sections: Technical architecture (performance budget); Roadmap and milestones (the Phase 0 gate)

## Context

US-021 to US-023 built a Release IL2CPP build with a "Phase 0 checks" bar (ADR 0034 §8). The owner ran it on the iPhone 13 (iOS, 2532 × 1170, on battery) in three rounds on 2026-10-18:

- **Round 1:** the CPU figure was wrong (a present wait was subtracted from a time that already excludes it), and checks overlapped. Both fixed.
- **Round 2:** golden replays, the spike and the heat run.
- **Round 3:** the spike with two more views to split the render thread's time: the map with no agents (the baseline), and the agents without the map. Dropped frames are counted over 25 ms.

## Results

**Determinism (US-023):** golden replays 001, 003 and 004 replayed inside the iPhone build on the frozen content: **all PASS, with exactly the hashes of `dotnet test`** (001 `0xAA0C65B7299B2C52`, 003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2`), in 4, 28 and 10 ms. A 72-hour away advance of the default farm takes **1.8 ms** (budget 2,000 ms).

**The spike (US-021), round 3, ms:**

| View (20 s each, 60 Hz) | Main thread avg / p95 / max | Render thread avg / p95 | GPU avg / p95 | Dropped (> 25 ms) | fps |
| --- | --- | --- | --- | --- | --- |
| Whole map, 2,000 sprite agents | 5.36 / 7.59 / 11.37 | 8.88 / 14.26 | 2.64 / 2.81 | 2 of 1,198 | 59.9 |
| Play zoom, sprite agents | 2.61 / 3.94 / 6.98 | 6.58 / 14.94 | 2.57 / 2.74 | 8 of 1,195 | 59.7 |
| Whole map, no agents (baseline) | 1.36 / 2.14 / 10.18 | 10.16 / 15.96 | 2.50 / 2.67 | 4 of 1,196 | 59.8 |
| Agents without the map | 4.96 / 7.00 / 17.59 | 13.26 / 14.34 | 2.22 / 2.37 | 0 of 1,199 | 59.9 |

The citizen stub (2,000 citizens' hourly pass in `Fixed` maths, run once a second, 3,600 times the real rate): average 1.84 ms, max 8.42 ms.

**The heat run (round 2, 5 minutes on battery):** the same numbers at the end as at the start; the phone stayed cool. No thermal throttling.

**Reading the render thread:** with no agents at all, the render thread shows the same time (10.2 ms average, 16 ms p95) as with them. So on iOS this figure is mostly the render thread **waiting for the next 60 Hz screen refresh** (it includes acquiring the drawable), not work. The GPU's 2.5–2.8 ms confirms the frame is cheap to draw, and the frame rate holds at 59.7–59.9 fps with under 1% of frames dropped.

## Decision

1. **The spike passes** the half-frame-budget rule of ADR 0026: in every view, the main thread's p95 (at most 7.59 ms) and the GPU's p95 (at most 2.81 ms) are within 8.3 ms, and under 1% of frames take over 25 ms.
2. **The pass rule is clarified** (amending ADR 0034 §8): a dropped frame is one over **25 ms** (a missed 60 Hz refresh is about 33 ms; 16.7 ms counted normal vsync jitter); the render thread's time is reported for diagnosis only, because on iOS it includes the wait for the screen.
3. **The Phase 0 gate is passed** (owner, 2026-10-18):

| Gate criterion | Evidence | Result |
| --- | --- | --- |
| No money loops; a viable start | US-015, US-016: 1,000 seeds, in CI on every push (ADR 0034 §7) | Pass |
| 2,000 agents at 60 fps within half the frame budget on the iPhone 13 | This ADR, round 3 | Pass |
| Golden replays match on the iPhone | This ADR, US-023 | Pass |

## Consequences and follow-ups

- **Phase 1 starts** with Sprint 4. The Phase 0 bar and its code (`Game.Presentation/Phase0/`) are throwaway: removed, or moved behind the developer panel, in Sprint 4 (F08.4).
- **Headroom to watch:** the whole-map main thread sits at p95 7.6 ms with 2,000 agents, close to the 8.3 ms line, and the real game will add the HUD and motion (spec 008 budgets 2 ms). Phase 1 has no walking agents, so it is safe; for Phase 3–4 (thousands of citizens):
  - move agents off GameObjects: GPU instancing (it didn't display in URP's 2D renderer in the spike; investigate a shader with a `Universal2D` pass, or the Entities Graphics package), or a single dynamic mesh;
  - time-slice the hourly citizen pass across frames (its max was 8.4 ms in one frame), or move it off the main thread, keeping determinism (the result is applied at a fixed sim time);
  - re-run this spike with real sprites and animation at each phase end (F20.1).
- **Determinism:** the golden replays stay in the device build's checklist at each phase end, and the frozen content is refreshed only with a rule change (ADR 0034 §9.7).
- The agents' random walk clamps at the map edge, so they bunch there over time; harmless for the measurement, noted for the real path-finder.
