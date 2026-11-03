# Sprint 15b build: the crop ladder (new crops in every season)

> **Passed.** The owner ran every check on the iPhone 13 on 2026-11-03 and reported them all working ("all checklist works fine"). Build: the head of `sprint/15b-crop-ladder` at `0c9d369`, a Development build (Unity: Build Succeeded, 0 errors; the unsigned Xcode Release compile: no errors).

- **Build:** the head of branch `sprint/15b-crop-ladder` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build**, so the developer panel exists.

## Before you install: keep your town safe

1. In the **old** app: Menu → Settings → **Export**. Keep the copy until this build is checked.
2. **Install over the old app. Don't delete the app first.**
3. Your town loads as it was: fields, cash, people, animals and businesses stay the same. The new crops simply appear in the sow sheet.

## What this build changes

- **Six new crops**, real ones from the valley's seasons:
  - **Methi** (Spring, Winter) and **amaranth** (Summer, Monsoon): quick greens, ready in about an hour.
  - **Okra** (Spring, Summer, Monsoon) and **green peas** (Winter): vegetables, ready in about 4 hours.
  - **Maize** (Spring, Monsoon): about 6 hours.
  - **Mustard** (Winter): about 8 hours.
- **Every season now has a crop for about 1, 2, 4, 6–8 and 15 hours.**
- **The sow sheet** lists this season's crops from quickest to longest, with when each will be ready ("Ready at 16:40"), what it earns for each hour it grows ("About 250 an hour"), and a one-line note. Other seasons' crops have gone from it: **All crops ›** at its foot opens the new **crop calendar**.
- **The crop calendar** (also **Menu → Crop calendar**): every crop by season and by how long it grows; tap one for its page. A hand's order sheet has an **Other seasons** row for ordering a crop ahead of its season.
- **The first session's last letter** names two quicker crops and when they'd be ready (a new town only: your town's first session is long past).
- **The time card** says how long the season has left: "Spring · 3 days left · Year 6", and "last day" on the fourth day.
- **The valley's other farms** grow some maize in Spring and the Monsoon (sandy Monsoon land always).
- **Dry soil dries slower (US-233, ADR 0085):** in a dry Spring or Summer a rain-fed acre stays low but stops falling to 0; rain adds the same water as before. Your town keeps its fields' moisture: they won't jump up, but rain now lifts them for longer.
- **Old crops' numbers don't change:** yields, prices, Care, pests and loans work as before. A short crop earns about the same in a season as a long one, in smaller, more frequent harvests.

**Your questions:**
1. Does the sow sheet make it clear which crop to pick for when you'll be back?
2. Does "About N an hour" make a 1-hour crop and a 15-hour crop easy to compare?
3. Do the new crops look like themselves as they grow?
4. Does the first day feel livelier than before?

## Checks

The developer panel: hold the time card for 3 seconds (**+1 hour**, **+1 day**, **Away cap**, **Snapshot** / **Restore**). Take a **Snapshot** first.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-225 (AC-13) | Launch with your town | Same cash, fields, people and animals as before | | **Screenshot** |
| 2 | US-228 (AC-15) | In Spring, tap an empty acre | Methi (about 1 h), radish, okra, maize, potato in that order, each with "Ready at …" (potato "Ready tomorrow …"), and no other crop or disabled row | | **Screenshot** |
| 3 | US-228 (AC-16) | Tap the **?** on okra's "About N an hour" | It explains: this sowing's profit over its hours; a shorter crop earns about as much an hour if you come back when it's ready | | |
| 4 | US-228 | Settings → text size 125%, open the sow sheet again | Every row readable; notes wrap, nothing cut off | | **Screenshot** |
| 5 | US-229 | Sow methi on one acre, okra on another, maize on a third | Each shows its first growth stage | | **Screenshot** |
| 6 | US-229, US-225 | **+1 hour** | Methi ready (its ready look); harvest it: about 13–17 kg in the store | | **Screenshot** |
| 7 | US-225 | Sell the methi to the trader | Sold at the trader's price; nothing refused | | |
| 8 | US-229 | **+1 hour** three times | Okra goes through its stages and is ready at about 4 h; harvest: about 70–90 kg | | **Screenshot** |
| 9 | US-229 | **+1 hour** twice more | Maize ready at about 6 h; harvest | | |
| 10 | US-232 (AC-25) | **All crops ›** on the sow sheet, then **Menu → Crop calendar** | The grid shows every crop by season and time; peas under Winter only; a crop's page opens; ← comes back | | |
| 10b | US-232 (AC-24) | With a hand, in Spring: tap an acre → give an order. Then **Other seasons** → **Wheat** → Choose → every season → sell to the trader | The order sheet lists this season's crops, then *Let … choose* and *Other seasons*; the list shows each crop with its seasons and time (wheat "Winter · 6 h"); the order reads "Keep Wheat …" and till Winter the hand sows the best crop in season | | |
| 11 | US-225 (AC-6) | **+1 day** until Winter; sow peas and mustard; harvest the peas; open the acre's fertility **?** | "Legumes fed the soil this season" | | **Screenshot** |
| 12 | US-228 (AC-17) | Read the Winter hint (Menu → Dadi's letter, or the season's first page) | Names five crops, then "and 2 more" | | |
| 13 | US-225 (AC-7) | With mustard in the store and eating from the store on, **+1 day** | The household eats other food; the mustard stays | | |
| 14 | US-229 | **+1 day** to the Monsoon; sow amaranth and maize | Both look right; the valley's other farms show some maize (US-230, check 22) | | **Screenshot** |
| 15 | US-225 | With a hand on "Let him choose" on an empty acre | He sows a crop of this season (any, old or new) and the acre's sheet says which | | |
| 16 | Frame time | Play 5 minutes with the developer panel's frame graph | Within the Phase 2 budget (main p95 under about 7.6 ms) | | **Screenshot** |
| 17 | — | **Restore** the snapshot | Your town as it was before check 5 | | |
| 18 | US-233 (AC-17) | On a rain-fed acre below 50 moisture while it's sunny, tap the moisture row's **?** | A row "Below 50, dry soil gives up its last water slowly: dries slower by N%"; N is bigger the drier the acre (about 50% at 25). Above 50, no such row | | **Screenshot** |
| 19 | US-233 (ADR 0085) | Note a dry rain-fed acre's moisture, then **+1 day** through sun | It falls by less than 1 a sunny hour, much less when very dry, and never reaches 0 in a day; after rain it holds up for longer than before | | |

### The time card and the neighbours' maize (US-231, US-230)

Take a **Snapshot** first, and **Restore** it at the end (check 23).

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 20 | US-231 (AC-23) | Look at the time card (top left) on the season's first day; then **+1 day** three times, looking after each | "Spring · 3 days left · Year N", then "2 days left", "1 day left", and on the fourth day "Spring · last day · Year N". The clock and weather lines are unchanged | | **Screenshot** of day 1 and the last day |
| 21 | US-231 (AC-23) | Tap the time card (the forecast opens) and tap the **?** on its "Today" row; close it. Then Settings → text size **80%**, **100%** and **125%**, looking at the time card at each | The forecast's "Today" row keeps "Spring · Day 1 of 4 · Year N"; its *Why?* shows the same date and a line "A season lasts 4 days and a year 16.". At every text size the time card's first line fits on one line, nothing cut off. Set it back to 100% | | **Screenshot** at 125% |
| 22 | US-230 | In Spring, pan round the valley; then **+1 day** to the Monsoon (Dadi's letter and the season report first), pan again; then Summer and Winter | Spring: some neighbours' fields show maize among potato and radish. Monsoon: rice, with maize on the sandy acres and a few others. Summer: mostly fallow, some lentils (no maize). Winter: wheat, potato and mustard (no maize). The same field shows the same crop each time you come back to that season | | **Screenshot** of Spring and the Monsoon |
| 23 | — | **Restore** the snapshot | Your town as it was before check 20 | | |

## What to report

- Pass or fail for each check, with screenshots where marked.
- Your answers to the four questions.
- Anything that looks wrong or reads oddly, with a screenshot.
