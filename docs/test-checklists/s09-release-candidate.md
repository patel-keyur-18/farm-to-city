# Sprint 09 build: the release candidate and the playtest week

- **Build:** the release-candidate commit on branch `sprint/09-playtest` (Claude gives the commit in the chat and in `STATUS.md`). **Two builds from that one commit** (ADR 0047 §4):
  - **Part A, the Development build** (`--options '["Development"]'`): the full regression and the frame-time check.
  - **Part B, the Release build** (no options): the smoke list, then **Part C**, your 7-day playtest.
- **Device:** iPhone 13, iOS version: ____
- **Tested by / dates:** ____
- **How to install:** see `how-to-build-ios.md`. **Delete `unity/FarmToCity/Builds/iOS` before each build** (the macOS 27 crash). Both builds are the same app (`com.keyurpatel.farmtocity`), so installing one over the other keeps your town.
- **Your town:** from this build on, saves are **never refused** (ADR 0047 §3: `minSchema` stays 3). Your Sprint 8 town must open as it was.

**What's new in this build:**
- **Measure frame time (20 s)** in the developer panel (Development only): it closes, gives you 3 seconds to get to the view, counts down 20 seconds at the top left, then opens again with the result under its status line.
- **The play log** now records your time zone, whether a notification tap opened the app, and each first-session step (spec 007 AC-15).
- **After New game** the play log starts a new session line for the new town.

**Rows:** every check from Sprints 4–8 is merged here by area. The *From* column names the source checks (for example s06 #4). A **blocker** (ADR 0047 §4) is any of: a crash or freeze; a lost, refused or unreadable save; a first-session step that can't be completed; a wrong money amount; a notification in quiet hours or with wrong text; a frame-time FAIL; a golden FAIL; a number with no *Why?*. Mark it **BLOCKER** in the Notes. Blockers are fixed before Part C starts.

## Part A: the Development build (US-095)

Developer panel: hold the time card for 3 seconds. Time travel: **+1 hour**, **+1 day**, **+1 season**, **Away +1 hour**, **Away +1 day**, **Away cap (72 h)**.

### Launch, look and saves

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | ADR 0047 §3 | Install over your Sprint 8 build and launch | **Your Sprint 8 town** opens: no "A new town has started" card, the same fields, cash and people | | |
| 2 | s04 #1, #20; s06 #1 | Look around; turn the phone to both landscape directions | Rendered ground, house, well, fence, yard props; the HUD (time card, cash, store, Collect all, alerts, People, Menu); nothing under the notch or the home indicator | | |
| 3 | s04 #2 | Pinch and drag | Smooth zoom between the whole valley and about 3 acres; never leaves the map | | |
| 4 | s06 #4, #5 | Sow a field, then **at once** press Home and swipe the app away; reopen | The same town with that sowing | | |

### Farming

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 5 | s04 #3–5 | Tap a ploughed acre → **Sow** → a crop | Field sheet (fertility, moisture, soil factor); crops for the season with good / fair / poor, "about N", hours a day; out-of-season crops grey with why; sowing pops sprouts and cash counts down | | |
| 6 | s04 #6, #7 | Sow a garden bed; **+1 hour** until ready; **Pick** | The bed grows; the ready bubble bobs; the picture flies to the store and the bar grows | | |
| 7 | s04 #13 | Tap a growing crop | Time left; Care, Water and Weather as words; pests; **Spray** and **Clear field** | | |
| 8 | s04 #8; s06 #2 | **+1 day** a few times | Rendered stages to ready; dusk warms, night goes blue; season tint changes | | |
| 9 | s04 #9; s07 #19 | **Collect all · N**; then with the store nearly full (buy compost), sow radish on several fields, **+1 hour** ×3, **Collect all** | Ready things picked nearest-to-spoiling first; when it won't fit: "N kg won't fit. Sell the rest to the trader for N coins?" and **Sell** adds exactly that | | |
| 10 | s04 #11 | Tap an empty tree pit, plant a banana or mango | 4 trees, one row each with fruiting, growing time and price; a sapling appears | | |
| 11 | s04 #12; s07 #17 | Menu → **Buy land** → **Show** on a parcel next to yours and one not next to yours | The map pans and its sheet opens; not next to yours: both Buy buttons grey with the same reason; next to yours: **Buy with a bank loan** shows what you pay now and a row per term | | |
| 12 | s04 #14 | Tap the map with a sheet open | The sheet closes; a second tap selects | | |

### The store and the market

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 13 | s04 #10; s07 #6 | Tap the store bar (quick and slow) | The Store opens (never a *Why?*): one row per good, **Sell** on produce; selling flies coins to cash | | |
| 14 | s07 #13, #14, #32 | Store → tap a good's row text; then **?**; then **Offer at the market** → Half → At least today's price | The row opens the good's sheet, never a *Why?*; only **?** opens one; on offer shows the half and **Take off market** appears | | |
| 15 | s07 #15 | Store → **Buy inputs** → Seed potato | "To sow 1 / 2 / 6 acres" with kg and price; a row you can't afford is grey with why; buy one | | |
| 16 | s07 #16, #29 | Store → **Mandi trip** → load a good; then **←** **←** | The sum (mandi, commission, trolley hire, your time, you get, the trader pays now, better or worse by); back goes Mandi → Store → Menu, never closing | | |
| 17 | s08 #36 | In a town on "Open the Store and sell to the trader", put everything on offer | The sale nudge and the store's bounce stop; they come back when the trader would buy something | | |

### Money and loans

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 18 | s07 #5, #8, #30 | Tap cash → **Borrow** on the moneylender → 12,000 → a term | Money and loans opens (never a *Why?*); amounts are a quarter, half, three quarters and all; each term shows first, last and interest in all; the loan is taken and **←** returns to Money and loans, then the Menu | | |
| 19 | s07 #9, #31 | The loan row → **Repay** → **All of it** | "No loans · You owe nothing."; no 0-coin payment left | | |
| 20 | s07 #10, #33 | **?** on "What you own minus what you owe" | Cash, paid ahead, land, buildings, goods, loans, the lesson; "Of that, your land" under it; **OK** closes with one tap | | |
| 21 | s07 #20 | **+1 season** | The season report opens by itself; each line's **?** lists its accounts | | |

### You and your family

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 22 | s07 #11, #12; s08 #30 | Tap the house → **Balanced** food → **A quarter** day labour | Work, health, tiredness as words; Bare minimum explained, not offered; "18,000 coins a season · 10% less work" (no "work work"); each choice shows Chosen and cash and the day's work change | | |

### People

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 23 | s05 #2–4 | Sow two acres → **People** → hire 1 labourer → **+1 day** | "Care today: Fair → Good"; cash goes down; a labourer walks in and hoes; at midnight walks off; the next day's free count shows | | |
| 24 | s05 #5–7 | People → **Build a hand's hut · Place it**; drag; **Build here**; **+1 day** (borrow first if short) | Green ghost on the labour quarters, red with a reason elsewhere; a half-built hut, "ready in 24 h", then the finished hut and a note | | |
| 25 | s05 #8–10 | **+1 hour** until someone asks; People → *Asking for work* → **Talk** → **Offer** | A badge with the count; stars, words, a trait and **?**; Talk shows the second trait; most accept and the hand moves in | | |
| 26 | s05 #11, #12 | The hand's **See** → **Let go** → **Keep them** | Skill, acres tended, traits, contract, paid ahead, orders; Keep them changes nothing | | |
| 27 | s05 #13–15, #19; s06 #3 | Give orders on two or three fields; then sow a different crop yourself on an ordered empty field | The hand starts **at once**, one field an hour; walks with a 4-frame cycle and hoes; "Change *name*'s order…?" — either way your crop stays; everyone goes home when there's nothing to do | | |
| 28 | s05 #17 | Run cash below the hand's wage before a season ends, **+1 season** | "A hand's season couldn't be paid"; the row says "Season unpaid" with **Renew** | | Optional if it takes long |
| 29 | s06 #19, #20 | With an empty hut, hire day labourers each day for about a week | "*name* would like to move in" (**See people** / **Later**); **Offer** always accepted | | |

### The HUD, alerts and the away card

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 30 | s07 #2–4 | Quick tap on the time card; **?** on a "likely" row and on Today; **+1 hour** a few times | The forecast sheet (no developer panel); the *Why?* texts; weather changes in runs | | |
| 31 | s07 #7 | **Menu** | Store, Money and loans, People, Buy land, You and your family, Dadi's letter, Settings; each **Open** works | | |
| 32 | s07 #18 | Sow radish, **+1 hour** ×3 | The **!** with a count; it lists "1 ready to collect" with **Collect all** | | |
| 33 | s08 #34 | **Away +1 hour** | No away card, no "Welcome back" | | |
| 34 | s08 #35; s07 #21; s05 #16; s06 #22 | With a hand and orders, **Away +1 day**, then **Away cap (72 h)**; close and reopen | The away card each time ("Away 1 d", "Away 3 d": ripened, the hand's harvests and sales, spoilage, money rows biggest first); no visible pause; after reopening, the same town | | |
| 35 | s08 #33 | A letter page, a card (New game's question), a **?**, the developer panel | Each dim covers the whole screen past the notch and the bottom | | |

### Settings

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 36 | s07 #22 | Settings → **Western**, then **Indian** | 120,000-style everywhere, then back to 1,20,000 | | |
| 37 | s07 #23 | Text **Small**, **Large**, **Normal** | The UI shrinks and grows; sheets fit and scroll; nothing overlaps | | |
| 38 | s07 #24 | iPhone Reduce Motion on, game Settings untouched | Reduced motion On in the game; sheets fade | | |
| 39 | s07 #25, #26, #34 | Sound on: tap around, sow, harvest, sell; then Sound Off and Haptics on/off | Taps and sheets silent; rustle on sow, pop on harvest, coins on sale; haptics follow the switch | | |
| 40 | s06 #7–10 | **Save now**; **Export town**; play a little; **Import a town** → the export → **Cancel**, then **Replace** | Last saved changes, no rewind; the file in Files; the list shows date, cash, saved at; Replace brings the export back | | |
| 41 | s06 #12–17 | Notifications on. Sow radish; leave the app ~2 hours (daytime); come back before a second crop is ready, leave again; one sown to be ready after 22:00; an order on a field | "Radish is ready" on time, no money in text; nothing stale; the night one at 07:00; no "ready" for the ordered field, at most one hands' note per 4 hours; Notifications off: none | | **Screenshot** |

### The first session and the week (new town)

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 42 | s06 #11, #18; s08 #1 | Settings → **Play log** On → **New game…** → **Export first**, then **Replace** | A fresh town (6 acres, 20,000 coins, the inherited store); "The farm is yours" with **Continue** only | | |
| 43 | s08 #2–12 | Play the first session: the baadi, sow a bed, the household (**Show me**), the tour lines while it grows, pick, the first basket, sell to the trader, the long crop (**Show me**), sow potato, **Yes, nudge me** | Each page at its moment, never before; the nudge and the gold arrow on each step; a tour line opens its sheet; "A good first day" asks about notifications | | |
| 44 | s08 #13 | — | Minutes from check 42's first page to "A good first day": ____ (recorded, not gated: ADR 0047 §5) | n/a | |
| 45 | s08 #14–16 | New game with notifications off: **I'll find my own way**, sow potato. New game again: Continue, tap the house first, then sow a bed | No more nudges; the plain notification card asks; with the house seen first, "The household" is skipped | | |
| 46 | s08 #17–23 | In one town: harvest a field; **Away cap**; **+1 season**; sow lentils on all 6 acres, **+1 hour**; try something you can't pay for; **+1 season** twice | "Your first field harvest" (once); away card, then "Welcome back"; the report, "Your first season", "Summer"; "More hands"; the rejection, then "Borrowing" (no loan taken); "The monsoon", "Winter" (and the seed-keeping line if grandfather's grain is eaten) | | |
| 47 | s08 #24, #25 | Menu → **Dadi's letter** → **Read** a page; close and reopen the app; export and import | Every seen page in order, then "Becoming a Hamlet"; reading sends nothing; no seen page comes again | | |
| 48 | s08 #26, #27 | Becoming a Hamlet; then meet all three conditions | Acres of 6, the hand's house, a profitable season, each with a **?**; at the last one the map warms, the banner drops, the Hamlet page follows | | |
| 49 | s08 #29 | The Menu and alerts buttons, Dadi's letter, Mandi trip | The ledger, the bell, the envelope, the trolley | | |

### The play log (spec 007 AC-11, AC-15)

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 50 | s06 #18; s08 #28; AC-15 | Settings → **Export play log**; open it in Files | Lines for `newGame`, `sessionStart` with `"tz":330` (India) and `"fromNote":false`, one `step` per first-session step in order, `firstSessionDone` (`"skipped":false`, or true in check 45's town), `firstHarvest`, `firstSale`, `firstHire`, `firstHand`, `firstLoan`, `firstSeasonClose`, `hamletUnlocked` | | |
| 51 | AC-15 | Leave the app with a crop growing; **tap the notification** when it comes; later open the app from its icon; export the log | The session opened from the notification has `"fromNote":true`; the one opened from the icon has `false` | | |

### Determinism and performance

| # | From | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 52 | s07 #27; s08 #31 | Developer panel → **Golden replays** | 001 `0x9803A63478EDC81C`, 003 `0xCA6205EDED737A6C`, 004 `0x98D9EE70B32631A0`, 005 `0x3FEB89B591B5F831` each **PASS**, **ALL PASS**; 72 h catch-up well under 2,000 ms | | |
| 53 | US-095.T2 | **Measure frame time (20 s)**, then in 3 s go to: **the yard close up** | **PASS** (main and GPU p95 ≤ 8.3 ms, under 1% over 25 ms) | | Numbers: |
| 54 | US-095.T2 | The same, **zoomed out to the whole map** | **PASS** | | Numbers: |
| 55 | US-095.T2 | The same, with **a full-height sheet open** (the Store) | **PASS** | | Numbers: |
| 56 | US-095.T2 | The same, with **people walking** (a hand on orders, labourers, you) | **PASS** | | Numbers: |
| 57 | Feel (s04–s08) | Play 10 minutes | Taps respond at once; no stutter when sheets update or saves run | | **Your view** |

## Part B: the Release build, smoke list (US-095)

Build the same commit **without** Development, install it over Part A's build.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| R1 | ADR 0047 §3, §4 | Launch | The town from Part A opens as it was (no new-town card) | | |
| R2 | ADR 0047 §4 | Hold the time card for 5 seconds; then tap it | Nothing on the hold (no developer panel in Release); a tap opens the forecast | | |
| R3 | US-096 | Settings → **Export town** (keep it) → **Play log** On → **New game…** → **Replace** | A fresh town and Dadi's first page. **This is day 1 of the playtest** | | Date and time: |
| R4 | AC-15 | Settings → **Export play log** | `newGame`, then a `sessionStart` with `tz` | | |

## Part C: the playtest week, on the Release build (US-096)

**Rules:** play as you'd naturally play; don't use the developer panel (there isn't one) or change the phone's date; keep the play log on; one diary row a day, even on a day you didn't open it. The free signing lasts 7 days from the install: if the app stops opening on day 7, Run it again from Xcode (same commit; your town is kept).

| Day | Date | Opened today? | Why? (a notification / something ready / I wanted to / testing) | What I did | What felt slow or confusing |
| --- | --- | --- | --- | --- | --- |
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6 | | | | | |
| 7 | | | | | |

**At the end of day 7:** Settings → **Export play log** and **Export town**; AirDrop both to the Mac and put them in `docs/playtest/` (or send them to Claude). The town becomes the migration fixture `schema3-device.ftc` (US-097).

## The gate (US-097, ADR 0047 §5: Claude fills it from the log, the diary and CI)

| # | Criterion | Pass | Result |
| --- | --- | --- | --- |
| A1 | A reason to come back each day | On days 2–7, at least 4 days with a session of 2 minutes or more, and on at least 3 of them the diary gives the game as the reason | |
| A2 | Notifications bring you back at sensible times | At least 2 sessions with `fromNote` true; none in quiet hours | |
| A3 | The Hamlet in about a week | `hamletUnlocked` on day 5–9 | |
| A4 | Nothing broke | No blocker in the 7 days; the device town loads as a fixture | |
| A5 | The release candidate passes | Parts A and B | |
| A6 | CI stays green | The balance gate and the Phase 1 health checks after any tuning | |

## What to report

- Parts A and B: every **Fail** and **BLOCKER** with what you saw; the four frame-time results (checks 53–56); screenshots of checks 41 and 57's view if anything stutters.
- Check 44's minutes.
- After Part B: tell Claude the playtest started (R3's date and time).
- After day 7: the play log, the town file and the diary (paste the diary into the chat or into this file).
- Crashes: what you did just before; with Xcode attached, the last lines of its console.
