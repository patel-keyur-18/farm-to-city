# 001. Time and weather

- Status: In review
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Time and world simulation; Offline play and saves (time while closed, clock safety); Technical architecture (simulation core, determinism rules)
- Related ADRs: 0002 (real-time clock model), 0017 (time-travel panel), 0018 (dotnet library, determinism rules)
- Depends on specs: none (this is the first spec)

## 1. Goal

The town lives on a real clock. The player plants, closes the app, and comes back to find that time has really passed, with seasons turning and the weather changing in 6-hour spells they can plan around using a forecast.

**Lesson:** time is a resource, and preparing for the weather you can see coming beats reacting to the weather that arrives.

## 2. Scope

**In:**
- **Foundations every later spec uses:**
  - the `Fixed` fixed-point number type
  - the deterministic, counter-based RNG
  - the `StateHash` used by determinism tests
- **Sim time** and the **calendar** (hour, day, season, year), plus the **life-clock** age helper.
- **The scheduler:** jobs with due times, the hourly, daily, seasonal and yearly settlement passes, a fixed processing order, and timestamped commands.
- **Advancing time:**
  - one `AdvanceTo` path for live play and for returning to the app
  - **away mode**, the **72-hour away cap**, and **clock-anomaly** handling
- **Weather:**
  - 6-hour spells drawn from per-season climate tables, with persistence
  - a **3-day forecast** that is less certain further ahead
- **Queries the platform layer needs** to schedule local notifications: upcoming jobs, and mapping sim time to wall time.
- **Developer time travel:** the commands and their rules. The panel itself is built when the Unity project exists.

**Out** (and where it goes instead):
- What weather *does* (crop yield, soil moisture, foot traffic, construction, energy): 002 land and soil, 003 crops and yield, and later specs.
- Droughts, floods and other multi-day weather regimes: the Events spec (Phase 3).
- Notification wording and the OS scheduling call: 008 HUD and the platform services.
- Saving and loading the clock state to disk: 007 saves. This spec defines the state fields and an in-memory round-trip only.
- The *While you were away* report screen: 008 HUD. This spec provides the event list it's built from.
- `Fixed` `Pow`, `Exp` and `Log`: 004 goods and market, where they're first needed.

## 3. Rules and formulas

### 3.1 Foundations

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| `Fixed` representation | Signed 64-bit, **Q31.32**: 32 integer bits and 32 fractional bits, stored in a `long` | Range about ±2.1 × 10⁹. Resolution 2⁻³² |
| `Fixed` add and subtract | Exact. Overflow throws in debug builds and is checked in tests | |
| `Fixed` multiply and divide | 128-bit intermediate (manual 64×64 multiply, since netstandard2.1 has no `Int128`). The result is **rounded to nearest, with ties away from zero**. Dividing by zero throws | |
| `Fixed` from data | JSON numbers are read as `decimal` (Newtonsoft `FloatParseHandling.Decimal`) and converted exactly, with the same rounding. **Values never pass through `double`** | Up to 9 decimal places in content |
| RNG | Counter-based: `Next(seed, stream, key, counter)` = SplitMix64 mixing of the four inputs. Each system has a fixed **stream id**, so adding a system never changes another system's random sequence | Output is a 64-bit unsigned integer |
| Uniform integer in `[0, n)` | `(Next × n) >> 64`, using a 128-bit multiply-high (Lemire's method) | Bias below 2⁻⁵⁰ for n < 2¹⁴ |
| Weighted pick | Draw u in `[0, ΣWeights)`, then walk the weights in declared order | Weights are non-negative integers, and the sum is greater than 0 |
| `StateHash` | 64-bit FNV-1a over a canonical byte stream of the state, written in a fixed field order with entities in id order. Each later spec extends the stream | |

### 3.2 Sim time and calendar

- **Sim time** `t` is a `long` count of seconds. `t = 0` is **Spring, Day 1, Year 1, 00:00**.
- **A new game starts at** `t = localSecondsSinceMidnight` at the moment the save is created. So the in-game time of day matches the device's local time on day 1.
- **Each real second of play advances `t` by one second.** Sim time is driven by UTC wall-clock *deltas*, so timezone and daylight-saving changes never move it.

| Quantity | Formula | Range |
| --- | --- | --- |
| Seconds per day | `D = 86 400` (fixed: it's real time) | |
| Days per season | `ClockDef.daysPerSeason` | 1–15, default 4 |
| Seconds per season | `S = D × daysPerSeason` | |
| Seasons per year | 4 (Spring, Summer, Monsoon, Winter; the order is fixed by ADR 0001) | |
| Day index | `day = t / D` (integer division; t ≥ 0) | ≥ 0 |
| Hour of day | `(t % D) / 3600` | 0–23 |
| Season index | `(t / S) % 4` | 0–3 |
| Year | `t / (4 × S) + 1` | ≥ 1 |
| Day of season | `(t % S) / D + 1` | 1 to daysPerSeason |
| Spell length | `W = ClockDef.weatherSpellHours × 3600` | Hours must divide 24. Default 6 |
| Spell index | `spell = t / W` | ≥ 0 |
| Life-clock age | `ageLifeYears = (t − birthTime) × lifeYearsPerSeason / S`, computed as `Fixed` | `lifeYearsPerSeason` 0.25–4, default 1 |

### 3.3 Scheduler and processing order

- **The scheduler** holds *items*, each with a due time:
  - **jobs**: a type, a target entity id, a start time and a due time
  - **settlement passes**: at every hour, day, season and year boundary
- **`AdvanceTo(target)`** processes every item with `dueTime ≤ target`, in ascending order of the key:

  **`(dueTime, class, systemOrder, entityId, sequence)`**

  - **class:** Job = 0, Hourly = 1, Daily = 2, Seasonal = 3, Yearly = 4. At a timestamp that is both an hour and a day boundary, jobs due then are processed first, then the hourly pass, then the daily pass, and so on.
  - **systemOrder:** the GDD's fixed order: Time, Weather, Agriculture, Production, Market, Labour, Population, Business finance, Civic, Treasury, Events.
  - **sequence:** a counter assigned when the item is scheduled, which breaks any remaining tie.
- **Commands** are applied at their stamped time `t_c`, **after** every item with `dueTime ≤ t_c`. A command stamped earlier than the current sim time is rejected with `TimeInPast`. Commands with the same timestamp are applied in the order they were submitted.
- **Chunking doesn't matter.** Advancing to T in one call, or in any sequence of smaller calls that ends at T, must produce the same state and the same ordered event list.

### 3.4 Advancing time: live, away, and anomalies

The platform layer calls **`Sync(nowWallUtc)`** every frame (or at least once a second) and on every resume. The sim keeps `lastWallUtc` and `simTime`.

| Case | Condition (`Δ = nowWallUtc − lastWallUtc`) | Applied sim seconds | Mode |
| --- | --- | --- | --- |
| Live | `0 ≤ Δ ≤ ClockDef.awayThresholdSeconds` (default 60) | `Δ` | Live |
| Away | `Δ > awayThresholdSeconds` and `Δ ≤ C` (C = `awayCapHours × 3600`, default 72 h) | `Δ` | Away |
| Away, capped | `Δ > C` | `Δ − k × D`, where k is the **smallest whole number of days** that brings the result to ≤ C. The result lies in (C − 24 h, C] | Away |
| Clock anomaly | `Δ < 0` | 0. Emits `ClockAnomalyDetected` | none |

- **After every `Sync`,** `lastWallUtc = nowWallUtc`. This *re-anchoring* means a backward jump grants nothing now. A later forward jump is bounded by the cap, which is accepted by ADR 0002.
- **Capping drops whole days only.** That keeps the sim's time of day aligned with the device's local time, so the town's day/night and work hours keep matching the player's real day.
  - Example: 100 h away with a 72 h cap. k = 2 (48 h is dropped), so the town lives 52 h, and the time of day is unchanged.
- **During an away advance,** `Clock.IsAway` is true. Other systems read it to apply the away rules from ADR 0002: no disasters, decisions queue, workers keep to normal hours.
- **An away advance** ends with one `AwayAdvanceCompleted` event. The events emitted during the away advance are collected for the *While you were away* report.

### 3.5 Weather

- **Weather types (7):** Sun, Cloudy, Rain, Heavy rain, Heatwave, Frost, Storm.
- **Spells** start at sim hours 00, 06, 12 and 18. The weather for spell `n` is generated once, in spell order, and doesn't change afterwards:
  1. Let `c` be the climate table for the season that contains the start of spell n.
  2. If n = 0, or the previous spell's weather has weight 0 in `c`: draw from `c.weights` (weighted pick).
  3. Otherwise, with probability `c.persistence`, keep the previous spell's weather. Else draw from `c.weights`.
  - Draws use the RNG with stream `Weather`, key `n`, counters 0 and 1.
- **Long-run behaviour:** the stationary distribution of this process equals `c.weights` (normalised), so weights are the long-run frequencies. The chance that two consecutive spells match is `p + (1 − p) × Σ wᵢ²`.
- **Generation buffer:** the sim keeps actual weather generated for at least the next `forecastDays × 24 / weatherSpellHours` spells (12 by default).

### 3.6 Forecast

The forecast covers the next 3 days, which is 12 spells by default. Each future spell `s` is in an accuracy **bucket** by how far ahead it is:

| Bucket | Spells ahead | What the forecast shows |
| --- | --- | --- |
| Day 1 | 1–4 | The actual weather, always |
| Day 2 | 5–8 | The actual weather with probability `forecastAccuracy[1]` (default 0.80). Otherwise an *alternative* |
| Day 3 | 9–12 | The actual weather with probability `forecastAccuracy[2]` (default 0.65). Otherwise an alternative |

- **The alternative** is a weighted pick from the season's weights with the actual weather removed. If nothing is left, the actual weather is shown.
- **The forecast is deterministic and stable.** It uses the RNG with stream `Forecast`, key `s`, and the bucket number as the counter. Asking twice gives the same answer. The forecast for a spell can change only when that spell moves into a nearer bucket. When it does, the sim emits `ForecastRevised`.

### 3.7 Upcoming jobs and wall-time mapping (for notifications)

- `UpcomingJobs(untilSimTime)` returns the scheduled jobs with `dueTime ≤ untilSimTime`, in processing order.
- **Wall time for a sim time:** `wallDue = lastWallUtc + (dueTime − simTime)`.
- **The platform layer schedules notifications only for jobs with `dueTime ≤ simTime + C`.** Anything later can't complete during a single absence, because of the cap.

### 3.8 Developer time travel (development builds only)

| Command | Effect |
| --- | --- |
| `DebugAdvance(seconds, Live)` | Advances sim time by `seconds` as if played live. Allowed steps are +1 h, +1 day and +1 season |
| `DebugAdvance(seconds, Away)` | Advances sim time as an away advance, with the cap applied. Used for "jump to away cap" |

- **These commands are rejected** with `DevCommandsDisabled` unless the sim was created with `SimOptions.DevCommandsEnabled = true`. Release builds never set it.
- **`lastWallUtc` isn't changed,** so the next `Sync` continues normally from the new sim time.
- Debug commands are recorded in the command log, so replays stay exact.

## 4. Data

**Definitions** (`content/clock.json` and `content/weather.json`):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `ClockDef` | `daysPerSeason` | int | 4 | 1–15 | ADR 0002 |
| | `weatherSpellHours` | int | 6 | divides 24 | |
| | `lifeYearsPerSeason` | Fixed | 1.0 | 0.25–4 | Life clock |
| | `awayCapHours` | int | 72 | 24–168 | |
| | `awayThresholdSeconds` | int | 60 | 5–600 | Live vs away |
| | `forecastDays` | int | 3 | 1–3 | |
| | `forecastAccuracy` | Fixed[] | [1.0, 0.80, 0.65] | each 0–1, non-increasing | Index = bucket |
| `WeatherTypeDef` | `id` | string | | | `sun`, `cloudy`, `rain`, `heavy_rain`, `heatwave`, `frost`, `storm` |
| | `nameKey` | string | | | String-table key |
| | `isSevere` | bool | | | Read by later specs (crop health, away rules) |
| `ClimateDef` | `season` | enum | | | One per season |
| | `weights` | map of weather id to int | see below | ≥ 0, sum > 0 | |
| | `persistence` | Fixed | see below | 0–0.95 | |

**Starting climate tables** (weights; tuned later in the economy model and playtests):

| Season | Sun | Cloudy | Rain | Heavy rain | Heatwave | Frost | Storm | Persistence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Spring | 45 | 30 | 20 | 3 | 0 | 2 | 0 | 0.50 |
| Summer | 50 | 15 | 5 | 0 | 25 | 0 | 5 | 0.55 |
| Monsoon | 10 | 25 | 35 | 20 | 0 | 0 | 10 | 0.60 |
| Winter | 45 | 30 | 5 | 0 | 0 | 20 | 0 | 0.50 |

**Example `content/clock.json`:**

```json
{
  "daysPerSeason": 4,
  "weatherSpellHours": 6,
  "lifeYearsPerSeason": 1.0,
  "awayCapHours": 72,
  "awayThresholdSeconds": 60,
  "forecastDays": 3,
  "forecastAccuracy": [1.0, 0.80, 0.65]
}
```

**State** (part of `GameState`, persisted by spec 007):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Clock` | `simTime` | long | Seconds |
| | `lastWallUtc` | long | Unix seconds, UTC |
| | `isAway` | bool | True only during an away advance |
| `World` | `seed` | ulong | Set at new game |
| `WeatherState` | `generated` | list of (spellIndex, weatherId) | From the current spell to at least current + 12 |
| | `forecastShown` | list of (spellIndex, bucket, weatherId) | Used to detect `ForecastRevised` |
| `Scheduler` | `items` | priority queue of (dueTime, class, systemOrder, entityId, sequence, payload) | Jobs and passes |
| | `nextSequence` | long | |
| `Job` | `id`, `type`, `targetId`, `startTime`, `dueTime`, `state` | | `state` is Running, Ready or Done (ADR 0002). Concrete job types come from later specs |
| `CommandLog` | entries | list of (simTime, command) | For replay and golden tests |

## 5. Commands and events

**Commands:**

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `NewGame` | `seed`, `startWallUtc`, `localSecondsSinceMidnight` | `localSecondsSinceMidnight` in 0–86 399 | Creates the clock at `t = localSecondsSinceMidnight`, generates the weather buffer, and schedules the passes |
| `Sync` | `nowWallUtc` | none | §3.4. Advances live or away, or records an anomaly |
| `AdvanceTo` | `targetSimTime`, `mode` | `TimeInPast` if the target is earlier than `simTime` | Processes due items in order (§3.3). Used internally by `Sync` and by tests and tools |
| `DebugAdvance` | `seconds`, `mode` | `DevCommandsDisabled`; `InvalidStep` if not +1 h, +1 day, +1 season or the away cap | §3.8 |

**Domain events:**

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `HourStarted` | `simTime` | At every hour boundary | Systems (internal); not shown in the UI |
| `DayStarted` | `day`, `dayOfSeason`, `season`, `year` | At every day boundary | HUD, ledger (later) |
| `SeasonStarted` | `season`, `year` | At every season boundary | HUD, ledger (season close, later), advisors |
| `YearStarted` | `year` | At every year boundary | Almanac (later) |
| `WeatherSpellStarted` | `spellIndex`, `weatherId` | At every spell boundary | HUD, crops and land (later) |
| `ForecastRevised` | `spellIndex`, `oldWeatherId`, `newWeatherId` | When a spell moves to a nearer bucket and its shown weather changes | HUD |
| `AwayAdvanceCompleted` | `fromSimTime`, `toSimTime`, `requestedSeconds`, `appliedSeconds`, `capped` | At the end of every away advance | *While you were away* report |
| `ClockAnomalyDetected` | `lastWallUtc`, `nowWallUtc` | When `Sync` sees a backward jump | HUD notice, telemetry log |

## 6. Decision card

Time itself isn't a decision, but it shapes two player choices:

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Plan around the forecast (for example, sow before rain, or hold off before a heatwave) | Waiting can leave a field idle | Better yields when the forecast is right. Days 2–3 can be wrong, which teaches acting under uncertainty | Crops, moisture and pests (later specs) | Each choice is per field and per cycle |
| Stay away beyond the cap | None | The town stops after about 2–3 days, and anything that would have finished later waits | Only work scheduled within the cap completes. Notifications only cover that window | Fully: the town resumes on return, nothing lost |

## 7. UI

The HUD itself is spec 008. This spec only defines what the HUD's time-and-weather strip shows, plus the developer panel.

```
HUD top strip (landscape, top-left)
+------------------------------------------------------------------+
| Spring · Day 2 of 4 · Year 1   14:05   [sun] Sun                 |
| Next:  [rain] 18:00   [cloudy] 00:00   [sun] 06:00   [sun] 12:00 |  <- Day 1: certain
| Day 2: [rain] likely     Day 3: [cloudy] likely                  |  <- uncertain, per bucket
+------------------------------------------------------------------+
  Tap the strip to open the 3-day forecast sheet.
  Why? on a forecast: "Day 2 forecasts are right about 80% of the time."

Developer panel (development builds only; opened by a hidden gesture)
+-------------------------------+
| Sim time: Y1 Spring D2 14:05  |
| [+1 hour] [+1 day] [+1 season]|
| [Jump: away cap (72 h)]       |
| Seed: 42   isAway: false      |
+-------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given `Fixed` 1.5 and 1.5, when multiplied, then the result is exactly 2.25. Given 1 ÷ 3, then the result is the nearest Q31.32 value. Given −1.5 × 1.5, then −2.25 | `FixedTests.Mul_Div_RoundToNearestTiesAwayFromZero` |
| AC-2 | Given the JSON number `0.65`, when loaded as `Fixed`, then it equals `Fixed.FromRatio(65, 100)` exactly, and no `double` is used | `FixedTests.FromJsonDecimal_IsExact` |
| AC-3 | Given seed 42, stream `Weather`, key 0, counters 0–4, then the outputs match pinned golden values. Different streams with the same key give different values | `RngTests.CounterBased_GoldenValues_AndStreamIndependence` |
| AC-4 | Given 100 000 draws of `Uniform(10)`, then a chi-square test passes at p = 0.001 | `RngTests.Uniform_IsUnbiased` |
| AC-5 | Given t = 0, then the calendar reads Spring, Day 1 of 4, Year 1, 00:00 | `CalendarTests.Epoch_IsSpringDay1Year1` |
| AC-6 | Given t = 4 days, then Summer Day 1. Given t = 16 days, then Spring Day 1, Year 2. Given t = 16 days − 1 s, then Winter Day 4, 23:59:59, Year 1 | `CalendarTests.SeasonAndYearBoundaries` |
| AC-7 | Given `daysPerSeason = 5` in data, then Summer starts at day 5, with no code change | `CalendarTests.SeasonLength_IsDataDriven` |
| AC-8 | Given a citizen born at t = 0, when t = 18 seasons, then their age is 18.0 life-years. With `lifeYearsPerSeason = 2`, it's 36.0 | `CalendarTests.LifeClock_Age` |
| AC-9 | Given a job, an hourly pass, a daily pass and a seasonal pass all due at the same t, then they're processed in that order. Two jobs at the same t are processed by system order, then entity id | `SchedulerTests.SameTimestamp_ProcessingOrder` |
| AC-10 | Given a job due at t, when advancing to t − 1, then it hasn't fired. When advancing to t, then it fires exactly once | `SchedulerTests.Job_FiresOnceAtDueTime` |
| AC-11 | Given seed 7 and a world with weather and three dummy jobs, when advancing to 1 game year in one call, versus 500 random seeded chunks, then the `StateHash` and the ordered event list are identical | `SchedulerTests.ChunkingInvariance` |
| AC-12 | Given a command stamped at t and an item due at t, then the item is processed first. A command stamped earlier than `simTime` is rejected with `TimeInPast` | `SchedulerTests.Commands_ApplyAfterDueItems_RejectPast` |
| AC-13 | Given a fixed seed and the stored command log `golden/001-basic.json`, when replayed, then the final `StateHash` equals the stored golden hash | `ReplayTests.Golden_001_Basic` |
| AC-14 | Given a day advanced from 00:00, then `WeatherSpellStarted` is emitted exactly 4 times, at 00, 06, 12 and 18 | `WeatherTests.SpellBoundaries` |
| AC-15 | Over 10 000 Summer spells, Frost never occurs (weight 0). Over 10 000 Monsoon spells, Heatwave never occurs | `WeatherTests.ZeroWeight_NeverDrawn` |
| AC-16 | Over 10 000 spells per season, each weather type's frequency is within ±3 percentage points of its normalised weight | `WeatherTests.Frequencies_MatchWeights` |
| AC-17 | Over 10 000 spells per season, the share of consecutive identical spells is within ±3 points of `p + (1 − p)Σwᵢ²` | `WeatherTests.Persistence_MatchesFormula` |
| AC-18 | Given Spring's last spell is Frost, then Summer's first spell is drawn from Summer weights and is never Frost | `WeatherTests.SeasonChange_DropsInvalidCarryOver` |
| AC-19 | The forecast for spells 1–4 ahead always equals the actual weather. Over 10 000 samples, spells 5–8 match 77–83% of the time, and spells 9–12 match 62–68% | `ForecastTests.AccuracyByBucket` |
| AC-20 | Asking for the forecast twice at the same sim time gives identical results. A spell's forecast changes only on a bucket change, and when it changes, `ForecastRevised` is emitted | `ForecastTests.Stable_RevisedOnlyOnBucketChange` |
| AC-21 | Given `lastWallUtc` = W, when `Sync(W + 30)`, then sim time advances 30 s in Live mode, and no `AwayAdvanceCompleted` is emitted | `AwayTests.SmallDelta_IsLive` |
| AC-22 | When `Sync(W + 5 h)`, then sim time advances exactly 5 h in Away mode, and `AwayAdvanceCompleted` reports applied = 5 h and capped = false | `AwayTests.Away_UnderCap` |
| AC-23 | With a 72 h cap, when `Sync(W + 100 h)`, then 52 h are applied, capped = true, and the hour of day after the advance equals the hour it would have been after 100 h | `AwayTests.Away_OverCap_DropsWholeDays` |
| AC-24 | When `Sync(W − 2 h)`, then 0 s are applied, `ClockAnomalyDetected` is emitted, and `lastWallUtc` is re-anchored. A following `Sync(+30 s)` advances 30 s | `AwayTests.BackwardJump_GrantsNothing` |
| AC-25 | A probe system records `IsAway` while processing: true for every item during an away advance, false during live | `AwayTests.IsAway_VisibleToSystems` |
| AC-26 | `UpcomingJobs(simTime + cap)` returns only jobs due within the cap, in processing order, and their wall times equal `lastWallUtc + (due − simTime)` | `SchedulerTests.UpcomingJobs_ForNotifications` |
| AC-27 | With dev commands disabled, `DebugAdvance` is rejected with `DevCommandsDisabled`. With them enabled, +1 day advances exactly 86 400 s in Live mode, and "jump to away cap" behaves like AC-22 and AC-23. Both are recorded in the command log | `DebugTests.TimeTravel_GatedAndExact` |
| AC-28 | Performance (CI, Release build): advancing 72 h with clock and weather only takes under 20 ms, and 1 game year under 100 ms | `PerfTests.Advance72h_And1Year` (category `Perf`) |
| AC-29 | Loading `content/clock.json` with `weatherSpellHours = 5` (doesn't divide 24), or with forecast accuracies that increase, fails validation with a message naming the field | `ContentTests.ClockDef_Validation` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Weather frequency per season | Equals the normalised climate weights | ±3 points over 10 000 spells |
| Rainy spells per Monsoon (Rain + Heavy rain) | About 55% of spells (8–9 of 16) | ±5 points |
| Measured forecast accuracy, days 2 and 3 | 0.80 and 0.65 | ±0.03 |
| Away advance cost | Well under the GDD's 2 s budget on device. CI guard in AC-28 | — |

## 10. Open questions

1. **HUD time display.** Should the HUD show the sim clock (14:05) or only the time of day as a sky icon? I recommend showing the clock: it matches the device's time, and it makes "your wheat is ready at 18:00" readable.
2. **Daylight saving and travel.** Sim time follows UTC deltas, so after a daylight-saving change the town's clock is 1 hour off the phone's clock. I recommend accepting this for v1, and adding a gentle realignment (for example, shift by up to 1 h during the next away advance) only if testers notice.
3. **Real-time crops in the first session.** The first session uses a 5-minute crop (spec 015 onboarding). Nothing in this spec blocks that. I'm flagging it only so we remember the clock has no "tutorial speed": every duration is real.

## 11. Cut first

1. Forecast uncertainty: make days 2–3 exact.
2. `ForecastRevised` events.
3. Weather persistence: draw each spell independently.
4. The `ClockAnomalyDetected` notice (keep the zero-grant rule, drop the event).
