# Sprint 23 build: the ring, construction, the kiln, utilities, pucca houses, overlays, the cold store and the farm's promises

- **Build:** a Development build from the head of branch `sprint/23-utilities-ring` (made at the end of the sprint; see the pull request). It includes the crash fix (ADR 0139) and the mill's wheat cap (ADR 0140).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s23-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 20 to 21): the map grows from 16 × 12 to 24 × 18 (the ring is locked until the Township); nothing in your town moves. **A town saved by this build cannot go back to an older build.** A town whose hourly clock was lost to the earlier crash is mended on load.

## What this build changes (story by story)

- **The ring (US-253).** The map is 24 × 18; the new acres are locked until the Township, with lanes, hills (locked) and a highway. The lane build card makes a lane acre.
- **Construction (US-145).** Houses and civic buildings take bricks, cement, timber and masons; a build can be *held with a reason*; builders wait for bricks.
- **The brick kiln (US-243).** A business that fires bricks (not in the Monsoon), with its smoke footprint on the ghost; its bricks go to your builds at cost and to the trader at his price less a tenth.
- **Utilities (US-144).** Water tower, drains, collection post, diesel generator and grid, built by the treasury; going without costs health, land value and Belonging.
- **Pucca houses (US-152)** from the Township; **map overlays (US-153)**: fertility, land value, coverage (the kiln's smoke hatched), happiness, crime.
- **The cold store (US-242)**, rented by the tonne.
- **The farm's promises:** calves (US-254), silage and the chaff cutter (US-255), your tractor for neighbours (US-256), solar panels (US-257).
- **The flour mill** buys market wheat only while its unsold atta and bran are under 2 days of what the trader takes.
- **Without any of it** a Hamlet or Village town is as before.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, the map is bigger, nothing is lost, the console shows no errors | | Editor ✓ (`s23-end-map.png`) |
| 2 | Pan out and around a Village town | The ring is there, dimmed and locked; lanes and hills are drawn | | Editor ✓ (`s23-253-*.png`) |
| 3 | Tap a lane acre | A card to build the lane, with its cost | | Editor ✓ (`s23-253-lane-*.png`) |
| 4 | Developer panel → **Next era** to the Township, then Menu | **Town treasury** and **Building** (bricks, cement, timber, masons) are in the Menu | | Editor ✓ (`s23-end-menu.png`) |
| 5 | Build a family house on a free acre | A card lists bricks, cement, timber and masons; the build starts or says why it is held | | Editor ✓ (`s23-145-*.png`) |
| 6 | Found a **brick kiln** | The ghost shows its smoke footprint; the card lists the fuel; hire a kiln hand | | Editor ✓ (`s23-243-*.png`) |
| 7 | Let a day pass with the kiln open | Bricks in the yard, some sold to the trader at 6:00, none in the Monsoon | | Editor ✓ (`s23-243-*.png`) |
| 8 | Menu → Town treasury → **Infrastructure** | Four ladders (water, power, sanitation, waste) with a build card and a top-up | | Editor ✓ (`s23-144-*.png`) |
| 9 | Commission a **water tower** | The treasury builds it from materials; the reach and its upkeep are shown | | Editor ✓ (`s23-144-tower.png`) |
| 10 | Upgrade a house to **pucca** | A card with materials and masons; the rent follows at the renewal | | Editor ✓ (`s23-152-*.png`) |
| 11 | **Overlays** (right side) → each of the five | One hue in five steps; the coverage overlay hatches the kiln's smoke red; a tap card beside an acre | | Editor ✓ (`s23-153-*.png`) |
| 12 | Store → **Cold store** | Rent by the tonne, an honest "is it worth it?", potato and fruit slowed | | Editor ✓ (`s23-242-*.png`) |
| 13 | Cattle shed → **Keep heifer calves** | The switch, the calves grow on fed days, carried as herd growth | | Editor ✓ (`s23-254-*.png`) |
| 14 | Build a **silage pit** and a **chaff cutter** | The pit keeps the spare cut at 15% loss; the cutter saves a fifth of the roughage | | Editor ✓ (`s23-255-*.png`) |
| 15 | Tractor (Equipment sheet) → **Hire out to neighbours** | The Hiring out block, an honest "Does it pay?", a hand is needed; the season page shows *Machine hire received* | | Editor ✓ (`s23-256-*.png`) |
| 16 | A field the pump waters → **Solar panel for the pump** | A tap card; *The pump (solar)*; a *Why?* with the payback in seasons | | Editor ✓ (`s23-257-*.png`) |
| 17 | Flour mill → ? on **Buy wheat at the market** | It says the trader takes 400 kg of atta and 100 kg of bran a day, the village does not buy from the mill, and the 2-day cap | | Core + View tests (ADR 0140) |
| 18 | Switch **Buy wheat at the market** on; let several days pass | The mill's unsold atta stays under about 800 kg and its cash is not all in stock | | Core test (`MillCashTests`) |
| 19 | Developer panel **+1 hour** about 30 times, then **+1 day** twice | The HUD stays alive; if a step fails, copy the top line of the console | | Core test (`PassResilienceTests`) |
| 20 | Every number's **?** on the new sheets | A *Why?* in words, never a raw factor | | `WhyTests` |
| 21 | Pan and zoom the biggest town you have for a minute | Smooth; the ring's frame time is the one to watch (the zoom-out is capped) | **Device only** | |
| 22 | Tap targets on the new cards and sheets (the lane, build and overlay cards) | Every button easy to hit with a thumb | **Device only** | |
| 23 | Text size 125% on the Infrastructure and Building sheets | Nothing cut off | **Device only** | |

## Render statistics (Editor, 2026-11-20, three views, no metric grew over 10% against the last reading)

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| The whole map | 1,082 | 31,414 | 1,320 |
| The village | 480 | 13,848 | 572 |
| The farm | 351 | 9,471 | 397 |

## What to report

Anything that looks wrong, a tap that does nothing, a number without a **?**, a red line on the console (send its top line), or a town that no longer loads.
