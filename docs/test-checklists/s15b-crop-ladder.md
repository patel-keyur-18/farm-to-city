# Sprint 15b build: the crop ladder (new crops in every season)

> **Draft**, written with the spec so the owner can see what will be tested. It's finalised at the end of the sprint with the build's real figures.

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
- **The sow sheet** lists this season's crops from quickest to longest, with when each will be ready ("Ready at 16:40"), what it earns for each hour it grows ("About 250 an hour"), and a one-line note.
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
| 2 | US-228 (AC-15) | In Spring, tap an empty acre | Methi (about 1 h), radish, okra, maize, potato in that order, each with "Ready at …" (potato "Ready tomorrow …"), then the other seasons' crops greyed | | **Screenshot** |
| 3 | US-228 (AC-16) | Tap the **?** on okra's "About N an hour" | It explains: this sowing's profit over its hours; a shorter crop earns about as much an hour if you come back when it's ready | | |
| 4 | US-228 | Settings → text size 125%, open the sow sheet again | Every row readable; notes wrap, nothing cut off | | **Screenshot** |
| 5 | US-229 | Sow methi on one acre, okra on another, maize on a third | Each shows its first growth stage | | **Screenshot** |
| 6 | US-229, US-225 | **+1 hour** | Methi ready (its ready look); harvest it: about 13–17 kg in the store | | **Screenshot** |
| 7 | US-225 | Sell the methi to the trader | Sold at the trader's price; nothing refused | | |
| 8 | US-229 | **+1 hour** three times | Okra goes through its stages and is ready at about 4 h; harvest: about 70–90 kg | | **Screenshot** |
| 9 | US-229 | **+1 hour** twice more | Maize ready at about 6 h; harvest | | |
| 10 | US-225 (AC-4) | Try to sow peas in Spring | Greyed: "Green peas: Winter only" | | |
| 11 | US-225 (AC-6) | **+1 day** until Winter; sow peas and mustard; harvest the peas; open the acre's fertility **?** | "Legumes fed the soil this season" | | **Screenshot** |
| 12 | US-228 (AC-17) | Read the Winter hint (Menu → Dadi's letter, or the season's first page) | Names five crops, then "and 2 more" | | |
| 13 | US-225 (AC-7) | With mustard in the store and eating from the store on, **+1 day** | The household eats other food; the mustard stays | | |
| 14 | US-229 | **+1 day** to the Monsoon; sow amaranth and maize | Both look right; the valley's other farms show some maize (if US-230 is in) | | **Screenshot** |
| 15 | US-225 | With a hand on "Let him choose" on an empty acre | He sows a crop of this season (any, old or new) and the acre's sheet says which | | |
| 16 | Frame time | Play 5 minutes with the developer panel's frame graph | Within the Phase 2 budget (main p95 under about 7.6 ms) | | **Screenshot** |
| 17 | — | **Restore** the snapshot | Your town as it was before check 5 | | |

## What to report

- Pass or fail for each check, with screenshots where marked.
- Your answers to the four questions.
- Anything that looks wrong or reads oddly, with a screenshot.
