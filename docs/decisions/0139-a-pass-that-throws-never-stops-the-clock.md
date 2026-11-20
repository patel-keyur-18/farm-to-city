# 0139. A pass that throws never loses the pass after it

- Status: Accepted (device crash found by the owner, 2026-11-20; fixed on `sprint/23-utilities-ring`)
- Related: ADR 0002 (the scheduler drives the sim), 0041 (saves), 0043 (the forecast), 0132 (the 72 h perf guards)

## Context

On the owner's iPhone the HUD stopped drawing and the Development Console filled with `ArgumentOutOfRangeException: Index was out of range` from `WeatherSystem.ComputeForecast` (called by `ForecastView.Runs`, `HudView.Build`, `GameRoot.Refresh`, even from `GameRoot.Awake` at launch). The map still drew and sheets still opened, but the HUD (money, forecast) was dead.

`Simulation.RunPass` ran every system's `OnPass` and only then scheduled the same pass an hour (a day, a season, a year) later. If any system threw inside a pass, that pass was **never scheduled again**, while the clock went on (`ProcessUntil` sets `SimTime` to the target). With no hourly pass the weather buffer (generated 72 spells ahead by each hourly pass) stopped growing, and once the clock passed a spell boundary, `ComputeForecast` read past its end. The town was then **saved in that state**, so the crash came back at every launch (the `Awake` trace). A test reproduces the exact stack: a system after Weather throws once in an hourly pass, the app's retry advances the clock, and `Forecast()` throws at `WeatherOf` (`PassResilienceTests`).

The crash is therefore the second failure. The first is whatever threw inside the pass (it is in the earlier session's log, not in the screenshot; see Consequences).

## Decision

1. **`RunPass` schedules the next pass in a `finally`.** The order of scheduling on the happy path is unchanged, so sequence numbers, hashes, goldens and fixtures are identical. A system that throws no longer stops the hourly, daily, seasonal or yearly cadence: the exception still propagates (it is a bug and must show), the world goes on.
2. **A town saved without a pass is mended on load** (`Simulation.FromState`): any pass class with nothing scheduled is scheduled at its next boundary, and `WeatherSystem.Mend` brings the weather buffer and the forecast up to the clock with the same spells the lost passes would have generated (a spell's weather depends only on its index and the seed, so nothing is invented). A healthy town is not changed by loading (the hash is the same; tested).
3. No save schema change.

## Consequences

- The owner's broken town loads again with its progress. The hours the hourly pass missed (weather, market, hourly people and business work) are not replayed; crops and jobs (their own scheduled jobs) and the daily, seasonal and yearly passes are untouched.
- A system that still throws in a pass now throws once an hour (a visible error each hour) instead of freezing the town silently, and the systems after it skip that pass. Finding the first exception from the owner's log is a follow-up: the fix above is only what stops one bug from ending a town.
- `Scheduler.Has(ItemClass)` is new; `PassResilienceTests` (3 tests) pin all of it.
