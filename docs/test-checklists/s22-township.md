# Sprint 22 build: the Township (road, clinic, unlock and old village, civic buildings), land value, zoning and leasing

- **Build:** a Development build from the head of branch `sprint/22-the-township` (made at the end of the sprint; see the pull request).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s22-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 19 to 20): the Township's parts (road, clinic, old village, land values, zones, leases) start empty; nothing changes in a Hamlet or Village town. **Schema 20 is not released yet**, so a town saved by an earlier Sprint 22 Development build is not readable by this one: start from your Export.

## What this build changes (grows story by story)

- **The paved link road (US-146).** From the Village with 10 households, the council hall takes your petition; the district builds the road for a 25,000 share in about 4 days. Every trader price's *Why?* names the road.
- **The clinic and its doctor (US-148).** The district builds the clinic for your 48,000 share; the doctor's quarters (90,000) are a rented asset. A migrant doctor comes once both stand. The clinic cuts sick days; households' *Healthcare* and *School* rows are met by reach, capacity and staff.
- **Becoming a Township (US-103).** The gate asks 15 households, the clinic, the road, 3 businesses each with a profitable season and literacy 60%. At the unlock the old village joins as about 30 households, the treasury opens, and a letter comes.
- **The civic buildings (US-148 part 2).** The watchman's post, the community hall and the playground, built by the treasury; *Safety* counts only once a watchman's post stands.
- **Land value (US-143).** Every acre has a land value from what is near it (road, school, clinic, square, water, jobs, the town's size); it sets rent's place, the house tax's base and land prices. Sell slowly (100% in 2 days) or at once (80%).
- **Zoning and builders (US-142).** Zone a parcel for houses; when families have been turned away, builders buy one parcel a season and build homes that are not yours (no rent, no tax, not your households).
- **Leasing land out (US-261).** Lease an empty parcel to a neighbour: rent 2.5% of its land value upfront at every close, the acre locked, ended at a close.
- **Without any of it** a Hamlet or Village town is as before.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Village with 10 households → the council hall → **Petition for a paved road** | A card: your share, the days, what the road does to prices; a loan option; after the share the road is built in about 4 days and the south road is drawn paved | | Editor ✓ (`s22-146-*.png`) |
| 2 | A trader price's **?** after the road | A row naming the road tier and its 0.50 a kg | | Editor ✓ (`s22-146-why.png`) |
| 3 | The village clinic: **Build the clinic** and **the doctor's quarters** | Cards with the 48,000 share and the quarters' cost; the clinic stands on its plot, a doctor arrives | | Editor ✓ (`s22-148-*.png`) |
| 4 | A household's card: *Healthcare* and *School* rows | Met or not, with a **?** naming reach, capacity and staff | | Editor ✓ (`s22-148-household.png`) |
| 5 | Let the doctor's allowance go unpaid (treasury empty) | An alert and the planner's note; after two more closes the doctor leaves | | Unit-tested (`ClinicTests`); the alert in the Editor (`s22-148-unpaid.png`) |
| 6 | Menu → **Becoming a Township** | A sheet of the five goals with progress and a **?** on each; the old village is not counted in your 15 households | | Editor ✓ (`s22-103-goals*.png`) |
| 7 | Meet the gate (or developer panel → Next era twice) | The letter (the `township` chapter); the treasury opens; the Town sheet gains the old village's row; a Codex card | | Editor ✓ (`s22-103-letter.png`, `-old-village.png`) |
| 8 | Town treasury → build the **watchman's post**, **hall**, **playground** | Cards with the budget guard (what the upkeep does to the close); each appears on its fixed plot; *Safety* rises only with the watchman's post | | Editor ✓ (`s22-148b-*.png`) |
| 9 | An empty field of yours → the **Land value** row and its **?** | "×1.04" and a *Why?* in words (what is near, what is not near enough yet) | | Editor ✓ (`s22-143-why.png`) |
| 10 | Menu → Your land → **Sell this land** on a bought parcel | A sheet with the quick sale (80%) and the slow sale (100% in 2 days) and the difference in coins; **Sell slowly** locks the acre and Your land says "For sale · … · locked"; **Take it off** unlocks | | Editor ✓ (`s22-143-*.png`) |
| 11 | An empty field → **Offer to builders** | A card (what you receive, what you lose, nothing happens until families are waiting); the sheet shows *Families waiting for a home* and its **?** | | Editor ✓ (`s22-142-card.png`, `-why.png`) |
| 12 | Wait for a close with families turned away | A builder buys the parcel (yours: you receive its land value); homes appear on bare ground; the Town sheet's *Builders* section; a builder's family has a tag and "paid to the builder" | | Editor ✓ (`s22-142-*.png`) |
| 13 | An empty field → **Lease out** | A card with the rent a season, the first rent at the next close, "you keep the land"; the sheet says *locked*; **Cancel the lease** is free | | Editor ✓ (`s22-261-*.png`) |
| 14 | The close after leasing | The rent arrives upfront; Money and loans → Your season shows *Rent from your leased land*; the sheet shows *Leased out*, the next rent and **End lease** | | Editor ✓ (`s22-261-leased.png`, `-season.png`) |
| 15 | **End lease** → the close | The card says nothing is refunded; the tenant leaves at the close; the acre is yours again; fertility is as it was | | Editor ✓ (`s22-261-end-card.png`) |
| 16 | Zone a parcel, then look at **Lease out** | Greyed with "Take it back to farming first." | | Editor ✓ (`s22-261-greyed.png`) |
| 17 | A town left alone with every tax at 0 | The town keeps working (builders' families bring grants); no service goes below 100% in the bots' runs | | `TaxGateTests` (bots) |
| 18 | Both frame-time p95s (main ≤ 8.3 ms, GPU ≤ 11 ms), golden replays, a 72 h catch-up with a Township open | Pass | **Device only** | |
| 19 | Render statistics at the three views (all, village, farm) | No metric over 10% above the last reading | | Editor ✓ (see the pull request) |

**Your questions:**
1. Is it clear which rent is a house's and which is leased land's?
2. Does a builder's family read as a resident of the town and not as yours?
3. Does anything in the Township ask you to do something you cannot find?
