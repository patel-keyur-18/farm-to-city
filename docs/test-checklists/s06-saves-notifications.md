# Sprint 06 build: saves, notifications, regulars and the full render

- **Build:** the head of branch `sprint/06-saves-notifications` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (tick *Development Build*), so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash). **Delete the old Farm to City app from the phone first**: this build saves for the first time, and a clean install starts with no files.

**What this build is:** the town now **saves itself** (spec 007, ADR 0041). It saves every hour, 5 seconds after your last tap, and when the app goes to the background. Each launch continues your town; it no longer starts a new game. A **gear** button under the Store chip opens **Settings**, with one row per choice: Save now, Notifications, Play log, Export town, Import a town, Export play log and New game. **Notifications** are off until you say yes. The game asks the first time you sow a field crop that takes a day or more. While you're away it nudges you when a field, bed or tree is ready, a loan is missed or a hand's contract lapses. It never sends anything at night (22:00–07:00) and never mentions money. **Regulars:** a day labourer you've hired for 6 days in all asks to move in when a hand's hut is empty. **Art:** every sprite (crops at each stage, trees, beds, the ground, the yard props), every icon and the people's walk cycles are now Blender renders (US-171).

**Not in this build (on purpose):** the full HUD, *Why?* sheets, the loan screen and the full *While you were away* card (Sprint 7); the letter and its notification ask (Sprint 8; a plain card asks for now); migrations of old saves (from the Sprint 9 playtest build, so until then a build that changes the town's data refuses older saves, keeps them, and starts fresh with a notice).

**Your Sprint 6 questions:** (1) does the town always come back exactly as you left it? (2) are the notifications useful, not noisy? (3) does the rendered art look like one game, and do people walk naturally? (4) is Settings clear?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **Away cap (72 h)**.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-171 | Open the app; look around the map, then pinch in on the yard | Rendered ground (grass, tilled acres, paths), the house, well, fence, tulsi planter, haystack and woodpile, all in one low-poly style with outlines. HUD icons (cash, store, gear, People) are rendered icons too | | **Screenshot** |
| 2 | US-171 | Sow wheat, potato and radish; plant a mango; sow a garden bed; use **+1 hour** and **+1 day** to watch them grow | Each crop, tree and bed shows rendered stages from seed to ready, and the ready bubble appears. Nothing is left as a painted placeholder | | **Screenshot** of a growing farm |
| 2b | US-171 | Plant all four trees in the yard and on field edges; use **+1 day** until they are grown and fruiting | Saplings stand in a dug watering basin; the banana's leaves fan round it with no blob in the middle; papaya, guava and mango fruit is easy to see. In the yard no tree hides behind the house roof. Ready bubbles sit just on top of the bed, field or tree they mark | | **Screenshot** of the yard |
| 2c | US-171 | Open **Store**, a tree place and a garden bed | Each good, tree and garden crop row shows its picture (a bowl of rice, a bunch of spinach…); the *Sell* and *Plant* buttons line up in one column. A field's sheet says "Soil for crops: Good" (a word, not a number) | | |
| 3 | US-171 | Hire a day labourer and watch people move between the fields | The founder, labourers and hands walk with a **4-frame walk cycle** (legs and arms move), facing the way they walk (front or back), and hoe at the fields | | Natural? |
| 4 | US-073 | Sow two fields and note the cash. Press Home, then swipe the app away in the app switcher. Open it again | The same town: the same fields, crops, cash and people. No new game | | |
| 5 | US-074 | Tap something (sow a field), then **at once** press Home and swipe the app away | On relaunch the sowing is there (the background save caught it) | | |
| 6 | US-075 | Tap the **gear** under the Store chip | A full-height **Settings** sheet: *Saved automatically · Last at HH:MM today* **Save now**; *Notifications* with a red **Off**; *Play log* with a red **Off**; *Export town* **Export**; *Import a town* **Choose**; *Export play log* **Export** (greyed while the log is empty); *New game* (red). No text runs into the line below | | **Screenshot** |
| 7 | US-075 | **Save now** | "Last at" changes to now. Nothing else changes: no rewind | | |
| 8 | US-075 | **Export town**. Then open the **Files** app → *On My iPhone* → *Farm to City* | A file named like `FarmToCity-2026-10-20-1430.ftc` | | |
| 9 | US-075 | Play on a little (sow a field). Settings → **Import a town · Choose** | A list of the town files, newest first: the date in the game, cash and "saved HH:MM today", each with **Import** | | |
| 10 | US-075 | **Import** the file from check 8 | A card: *Import FarmToCity-….ftc?* with **Export first**, **Replace** (red) and **Cancel**. **Cancel** changes nothing. **Replace** brings back the town as it was at the export (the field from check 9 is gone) | | |
| 11 | US-075 | Settings → **New game…** → **Export first**, then **Replace** | **Export first** writes a new file to Files. **Replace** starts a fresh town: the inherited 6 acres, 20,000 coins, the store as at a new game | | |
| 12 | US-076 | Sow **potato** (or any field crop of a day or more) | A card: *"Potato will be ready at HH:MM tomorrow. Want a nudge when it is?"*, **Yes, nudge me** and **Not now**. **Yes**: then iOS's own permission dialog; allow it | | |
| 13 | US-076 | Sow **radish** (2 hours). Go to the Home screen and leave the phone locked for about 2 hours (outside 22:00–07:00) | A notification such as "Radish is ready" arrives at the radish's ready time. No money amounts in any text | | **Screenshot** of the notification |
| 14 | US-076 | Sow radish late in the evening so it's ready after 22:00; leave the app | No notification at night: it comes at **07:00**, grouped with anything else due then | | |
| 15 | US-076 | Sow radish, leave the app, and come back **before** it's ready; then leave again after it's ready | No stale notification from the first time. Each return cancels pending ones, and each leave plans afresh | | |
| 16 | US-076 | Give a hand an order on a field and leave the app for a few hours | No "ready" for that field (the hand harvests it). At most one hands' note every 4 hours: "Harvests in: …" | | |
| 17 | US-076 | Settings → **Notifications · Turn off**; leave the app with a crop growing | No notifications | | |
| 18 | US-077 | Settings → **Play log · Off** (it turns into a green **On**). Harvest, sell, hire, then close and reopen the app. Settings → **Export play log** | Settings shows the entry count. Files has `FarmToCity-playlog-<date>.jsonl` with a session start, a session end and each first (harvest, sale, hire) once | | Send Claude the file if you like |
| 18b | US-072 | Give a hand an every-season order with **Sell at the market, not below N** and let it harvest; open **Store** | The good's row says how much is *at the market, not below N*. When all of it is on offer the button is **Take off market**; tap it, and the row's **Sell** to the trader works again | | |
| 18c | US-068 | Build two hands' huts; close and reopen the app | Two smaller round huts side by side in the labour quarters, not crowding; no third hut anywhere after reopening | | **Screenshot** |
| 18d | US-079 | Tap an empty field and open its sow sheet; tap **Normal**, **Compost**, **Chemical** in turn | Three cards, each saying what it does; under each crop: *Harvest about N kg*, then **Spend** (red), **Sells for** (green) and **Profit**, with "seed … + compost …" and hours; the numbers change with the fertiliser; the Sow button says what you pay now | | **Screenshot** |
| 18e | US-078 | Watch people walk to the far side of a field and in from the map's edge | They always stand on top of the land, never half under it | | |
| 19 | US-067 | Build a hand's hut (developer panel → **Moneylender loan 20,000** if needed) and leave it empty. Hire day labourers each day for about a week (**+1 day** between) | When one of them has worked 6 days in all: a card *"<name> would like to move in"* with **See people** and **Later**. **See people** opens People, where they're under *Asking for work* | | **Screenshot** of the card |
| 20 | US-067 | **Offer** to the regular | They always accept and move into the empty hut | | |
| 21 | US-088 | Developer panel → **Golden replays** | `001`, `003`, `004`, `005-credit.json` each **PASS**, then **ALL PASS**. Hashes unchanged from Sprint 5: 001 `0xAA0C65B7299B2C52`, 003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2`, 005 `0x4D9D7611B2753AC9` | | **Screenshot** |
| 22 | US-073 | Developer panel → **Away cap (72 h)** with a hand and orders; then close and reopen the app | The catch-up has no visible pause. After reopening, the town is the one from after the 72 h (the date, the crops, the hand's work) | | |
| 23 | Feel | Play 10 minutes | Saving never makes the game stutter (a save takes about 12 ms). Taps respond at once | | **Your view** |

## What to report

- Screenshots of checks 1, 2, 2b, 6, 13, 19 and 21.
- Whether the town ever came back different from how you left it (check 4, 5, 22): what was different.
- The notifications you got over a day: how many, when, and whether any felt like noise or came at night.
- How the rendered art looks at game scale: anything that looks off, too small, too busy, or still placeholder.
- Any **FAIL** in check 21: the file and the hash it showed.
- Crashes: what you did just before; with Xcode attached, the last lines of its console.
- Paste the results into the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
