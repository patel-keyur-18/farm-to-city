# 0132. The 72 h catch-up guards fail for a reason, not for the machine: four hot paths fixed, results bit-identical

- Status: Accepted (owner, 2026-11-19: "there should not be any failure; find the root cause and fix it properly")
- Date: 2026-11-19
- Deciders: Keyur Patel
- Related: spec 001 AC-28, spec 006 AC-17, ADR 0018

## Context

`PerfTests.Advance72h_WithPeople` (20 ms for 72 hours of a farm with two hands and 12 orders) failed on every local run from Sprint 21 on, and on `main`. It had been carried as "local-only" (the Mac is slower than CI). Measured on the Mac it was 19–27 ms; the other guards were 15–22 ms and the Sprint 23 ring made several worse. A guard that fails for months is not a guard, and 20 ms for 72 hours is a lot for a farm with nothing happening: a per-hour profile (timing each system's pass) showed where it went.

## Decision

Fix what the profile found, keeping every result bit-identical (the golden replays and every saved fixture still match):

1. **The weather forecast diff was quadratic.** Each hour the new 72-spell forecast was compared with the last by searching the whole old list for each entry (about 2,600 comparisons an hour). The spells are consecutive, so the old entry is at a fixed offset: one lookup. The rolls for a run and bucket are made once, not once a spell. Weather's hourly pass went from 4 ms to 0.6 ms for 72 hours.
2. **`Fixed` division allocated.** `/`, `FromRatio`, `MulRatio` and `RoundToLong` went through `BigInteger` (130–230 ns and garbage each). They now divide a 128-bit magnitude by a 64-bit one in integer arithmetic (`Wide.Divide`, Hacker's Delight `divlu`), 13–30 ns, rounding half away from zero and throwing on overflow exactly as before. `FixedDivisionTests` compares them with the old `BigInteger` arithmetic on edge values and 200,000 seeded random pairs.
3. **`Fixed.Pow` recomputed the same powers every hour.** A market good's demand and supply barely move between hours, so the same `Pow` (745 ns) came round again and again. Pow remembers its last answers (a pure function: a 1,024-slot direct-mapped, per-thread memory keyed on both arguments), 11 ns on a hit.
4. **The market counted the town's eaters once for every good, every hour** (a pass over all households for each good in the basket, so it grew with the town), and asked for the regional price three times a good. Eaters are counted once an hour and the floor and ceiling are taken from one regional price. The land pass reads what grows on each acre in one pass, not once a tile.

## Consequences

- Release on the Mac, before and after: 72 h with land 17.5 → 8.4 ms, with crops 18.5 → 9.5, with the market 18.6 → 14.4, with people 25 → 15.7, the plain 72 h 3.1 → 0.5 and a game year 16.8 → 2.5. In the default (Debug) test build: people 27 → 14.3. All the guards pass locally with about 25–30% headroom against the 20 ms budget, and the 20 ms and 100 ms budgets are unchanged.
- No result changed: all 701 Core and 303 View tests pass, golden replays included, with no re-pinned hash.
- What is left (the village shops' hourly share, the hands' orders, the market's sales) is the simulation's real work, not waste. If a guard fails again, profile first (time each `ISimSystem.OnPass`), then fix the cause; do not raise the budget or carry the failure.
