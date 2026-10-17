# 0002. Real-time clock model (task clock, calendar, life clock)

- Status: Accepted. Refined by spec 001: day/night follows the sim's time of day, which starts aligned to the device's local time; the away cap drops whole days so the alignment holds.
- Date: 2026-10-17
- Deciders: Keyur Patel
- Supersedes: GDD v1.0 "Clock and speeds" (1 game day = 2 real minutes, speeds pause/1x/2x/3x) and the "keeps living for up to 8 hours" offline default
- GDD sections: Vision and pillars, Player journey, Time and world simulation, Land and farming, Economy, Citizens and workforce, Events, Learning layer, UI/UX, Offline play and saves, Technical architecture, Data model, Balancing and testing, Roadmap

## Context

GDD v1.0 ran a compressed clock: 1 game day = 2 real minutes, 15-day seasons, a 2-hour game year, with speed controls and an 8-hour offline catch-up. That had three problems:

1. **Offline time exploded.** 8 real hours away was 240 game days, which is 4 game years, with no natural limit.
2. **Crop durations didn't fit.** Real crops take 90–150 days, but a season was only 15 days.
3. **The play style didn't fit.** The owner wants a Hay Day–style loop: start work, close the app, get a notification when it's done, come back and collect. Hired workers keep the town running in the background.

## Options

1. **Keep the compressed clock and pause when the app closes.** This is simple and deterministic, but you lose the "come back to progress" loop.
2. **Keep the compressed clock and let the world run until started jobs finish (capped at 1 season).** This works, but every absence still means a jump of up to 15 game days, and the speed controls and away logic stay complex.
3. **Use a real-time model with three clocks** (task clock, calendar, life clock), in the style of Hay Day and Township.

## Decision

Option 3.

### Task clock (real time)

Every job has a duration in real time, taken from data: crops, construction, production batches, training. The clock runs whether the app is open or closed. Typical ranges, all tunable in data:

| Job | Real-time duration |
| --- | --- |
| Onboarding crops (radish, greens) | 5–15 min |
| Wheat, onion | 4–8 h |
| Rice, potato | about 1 day |
| Sugarcane, cotton | 2–3 days |
| Orchard, first fruit | about 2 weeks |
| Bakery batch | about 10 min |
| House construction | hours |

- **Ready waits for you.** A finished crop sits in a *ready to harvest* state. It doesn't spoil or lose quality until it's harvested, but it keeps its field busy, which is a natural opportunity cost.
- **Standing orders.** Hired workers follow standing orders, such as "keep wheat on the north field; sell above 20 coins, otherwise store". They work in the background, including while the app is closed. Fields without an assigned worker wait in *ready to harvest*.
- **Weather changes yield, not duration.** Growth time is fixed by data. That keeps finish times predictable for notifications.

### Calendar (real time, compressed seasons)

- 1 season = **4 real days**, and 1 game year = 4 seasons = **16 real days**. Seasons: Spring, Summer, Monsoon, Winter.
- Weather changes in **6-hour spells** (4 a day), drawn from the season's climate table, with a 3-day forecast.
- The day/night lighting follows the device's local time.
- Settlement cadence:

  | Cadence | What settles |
  | --- | --- |
  | Every real hour | Production, movement, sales, and market prices clearing |
  | Every real day | Wages are paid and needs update |
  | Every season (4 days) | Accounts close, loan payments fall due, taxes are collected |
  | Every game year (16 days) | Year-end review, the Town Almanac |

### Life clock

- Citizens age **1 life-year per season**, which is 4 life-years per game year.
- Childhood (0–17) lasts about 72 real days. A school pays off in about 40–60 real days. Citizens born early in a playthrough reach retirement near its end.
- The UI shows each citizen's age and life stage, and avoids presenting calendar years and ages side by side.
- The rate is a data value (`lifeYearsPerSeason`), so it can be tuned.

### No speed controls, no pause

- The live game has no 1x/2x/3x and no pause. No decision needs to be made within seconds.
- **Scenarios** (short graded challenges) run in their own saves on an accelerated clock.

### Away rules

- Time passes while the app is closed, **capped at 72 real hours** per absence. After the cap, the world freezes, and when the player returns it resumes from the frozen point. Sim time is then behind wall-clock time, and that's intended.
- While the player is away, disasters never fire. Events that need a decision queue and wait. If cash runs out, workers stop working rather than pushing the player into debt.
- A *While you were away* report shows harvests, sales, wages paid and events.
- Local notifications are opt-in, grouped, and capped at 2 a day. Example: "Wheat in the north field is ready".

### Clock safety

- If the wall clock jumps backward, the player gets no time. If it jumps forward, the gain is limited by the 72-hour cap.
- Changing the device clock can't be fully prevented offline. With no paid speed-ups, a player who does it only cheats themselves, and we accept that.

### Simulation model

- **Sim time** is a 64-bit integer count of seconds since the save began. It's separate from wall-clock time.
- The core is a **scheduler**. Jobs carry due timestamps, and the hourly, daily, seasonal and yearly settlement passes run at fixed sim times.
- Advancing time processes every due item in timestamp order. Ties are broken by a fixed system order, then by entity id.
- Every command carries the sim time it applies at. Resuming the app issues `AdvanceTo(simTime)`, computed once from the wall clock.
- So the same seed, the same commands and the same timestamps always give the same result.

## Consequences

**Pillar 1 is reworded:** "Timers measure time; they never decide outcomes." Yield, prices and profit still come from the simulation.

**Pacing becomes calendar time** (Standard difficulty, a player doing several short sessions a day):

| Milestone | Real time |
| --- | --- |
| Hamlet | Day 1–2 |
| Village | About 1 week |
| Township | 3–4 weeks |
| Town | 2–3 months |
| City | 4–6 months (about 10–12 game years) |

**Offline catch-up gets much cheaper.** 72 hours is 72 hourly passes, well within the 2-second budget, so no separate coarse aggregate model is needed. Live play and catch-up run the same code.

**The balancer and tests use a fake clock.** Headless runs still simulate years in seconds.

**Data definitions use real-time durations** (minutes) in place of game days: `growthMinutes`, `buildMinutes`, recipe `durationMinutes`.

**Elsewhere in the GDD:**
- Payment terms become half a season or a full season.
- Probation lasts one season.
- Event frequency is per real day and per game year.
- Elections every 5 game years are 80 real days apart.

**Removed from the GDD:** speed controls, "pause anywhere", Guided-mode auto-pause, and the separate coarse catch-up model.

**Spec impact:** spec 001 covers the scheduler, the three clocks, calendar, weather and the away rules. Standing orders and *ready to harvest* belong to the crops and workforce specs.
