# 0042. Weather in 1-hour spells with per-weather persistence: calm weather lasts, severe weather passes

- Status: Accepted (implementation in Sprint 7, US-184)
- Date: 2026-10-20
- Deciders: Keyur Patel
- Amends: spec 001 §3.5 (spell length, persistence), §3.6 (the forecast shown as runs), §4 (`ClockDef.weatherSpellHours`, the climate tables); ADR 0002 (the weather clock)
- Related: ADR 0034 (the Monsoon's wet share, pests), spec 002 (moisture by weather), spec 003 (the weather factor in the yield)

## Context

After the Sprint 6 device run the owner asked whether the weather could change less often (12, 24 or 48 hours), with good weather (sun, rain) lasting long and severe weather (heavy rain) lasting about an hour, and whether a sudden change ruins a crop that takes more than 8 hours.

How it works today: 6-hour spells; each spell keeps the previous weather with the season's persistence (0.50 to 0.60), so a weather lasts about 18 hours on average. A season is 4 real days for about 3 real months, so **1 real hour is about 1 real-world day**: a 6-hour spell is already almost a week of weather. The weather's effect on a crop is its weather factor **averaged over the crop's growth** (spec 003), so a 6-hour heatwave during a 24-hour potato costs about 4%, a heavy rain about 2%.

## Options

1. **Fixed 12, 24 or 48-hour spells.** Simple, but a 48-hour heatwave is six weeks of world time and costs a whole potato crop about 15%; a severe spell could not be short.
2. **6-hour spells with per-weather persistence.** A small change: calm weather repeats more, but severe weather still lasts at least 6 hours.
3. **1-hour spells with per-weather persistence (chosen).** Calm weather lasts days, severe weather passes in an hour or two, as in the real valley at the game's time scale.
4. **No change.**

## Decision

1. **Spells are 1 hour** (`weatherSpellHours` 1).
2. **Persistence is per weather and season** (data): the chance that the next hour keeps this hour's weather. Targets for the average run: **sun about 1–2 days, rain about 1 day, cloudy about 12 hours, heatwave about 6 hours, frost a few night hours, heavy rain and storm 1–2 hours.**
3. **The seasonal climate stays as tuned.** The draw weights are derived so each season's long-run share of every weather is unchanged (for this chain the long-run share of weather i is proportional to wᵢ / (1 − pᵢ), so the stored weights are the tuned shares × (1 − pᵢ), normalised). Moisture, the Monsoon's wet share, pests and the balance gate keep their numbers; the gate is re-run to prove it.
4. **The forecast shows runs, not spells:** "Sun until Tue 14:00, then heavy rain about 1 h", with the same accuracy buckets by hours ahead.
5. **The weather's effect on crops is unchanged:** averaged over growth, so a 1-hour storm during a 24-hour crop costs about 0.6%.

## Consequences

- Spec 001 §3.5, §3.6 and §4 are rewritten for Sprint 7 and re-approved (a rule change, ADR 0027); the golden replays 001 to 005 are re-pinned with the change, and the on-device determinism check is repeated.
- The weather generation buffer holds 72 hourly spells for the 3-day forecast (cheap).
- A new story **US-184** (F01.3, Sprint 7, 4 points).
