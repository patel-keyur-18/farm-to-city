# Sprint 07 build: the full HUD, *Why?* everywhere, the panels, the away card, Settings, sounds and hourly weather

- **Build:** the head of branch `sprint/07-hud-panels` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (`--options '["Development"]'`, or tick *Development Build*), so the developer panel and the golden replays exist. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash). Quick check that it is a Development build: holding the time card for 3 seconds opens the developer panel.
- **Your town:** this build changes the town's data (the weather now remembers the current run), so **your Sprint 6 town is refused and kept** with a notice ("A new town has started"), as ADR 0041 §2 planned until the Sprint 9 playtest build. You don't need to delete the app.

**What this build is (ADR 0043):**
- **The weather** changes hour by hour in runs: calm weather lasts (sunny spells of a day or more), storms and heavy rain pass in an hour or two, the Monsoon has long rains and short sunny breaks. The time card shows the run: "Sunny until 14:00 · then heavy rain about 1 h". **Tap the time card** for the 3-day forecast, run by run, with tomorrow's and the day after's marked "likely".
- **Every number has a *Why?***. A row with a number has a small round **?**: tap it to see the causes in words, like "Soil is tired: 12% less". Only the **?** opens one (ADR 0044).
- **The Menu** (bottom-right, ≡): Store, Money and loans, People, Buy land, You and your family, Settings. The gear is gone. **People** stays beside it.
- **Money and loans:** cash, net worth, this season so far, the last season's report, your loans with **Repay**, and **Borrow** (choose an amount, then a term; each term shows its payments and the total interest). Land you can buy has **Buy with a bank loan**.
- **You and your family** (tap the house, or Menu): your work today, health, tiredness, the food plan, work hours, day labour, eating from the store and medicine.
- **The Store** has **Buy inputs** (seed in "to sow N acres") and the **Mandi trip** (load the trolley and see the sum against the trader). Tap a good for its sheet: **Offer at the market** (how much, then the lowest price), **Take off market**, **Sell to the trader**.
- **Alerts:** a round **!** above Collect all when something needs you (red for urgent).
- **While you were away:** one card with what ripened, sales, day labour, home use, spoilage, pests, people, a closed season; only when something happened. **The season report** opens by itself at each season's end.
- **Back:** a sheet opened from another sheet has a **←** at the top left that goes back the way you came (ADR 0044).
- **Settings:** numbers (Indian or Western), text size (Small, Normal, Large), reduced motion (iOS's own setting at first), sound and haptics.
- **Sounds:** only for what happens in the town: sowing, harvest, coins in and out, something ready, an urgent alert (Kenney CC0). No sound on button taps or sheets (ADR 0044).

**Not in this build (on purpose):** the letter and the guided first week (Sprint 8); alert dismissals (an alert lasts while its cause does); migrations of old saves (Sprint 9).

**Your Sprint 7 questions:** (1) does the weather feel like a real valley now (long calm spells, short storms)? (2) does every *Why?* read like a sentence you'd say to a farmer, with no strange numbers? (3) can you reach every action in three taps? (4) are the new sheets clear: borrowing, the founder, the market, the mandi? (5) are the sounds and haptics pleasant, not tiring?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-184 | Launch | The "A new town has started" card (your Sprint 6 town is kept). Then the HUD: the date, the clock and the weather, and under them the run: "Sunny until 14:00 · then …" or "Sunny all day" | | |
| 2 | US-184 | Tap the time card (a quick tap) | The forecast sheet: Today's date first, then one row per run with its times (today's times, "tomorrow", then the weekday) and a weather picture; rows after tomorrow begin say "likely". The developer panel does **not** open | | **Screenshot** |
| 3 | US-184 | **+1 hour** a few times, then **+1 day**, watching the time card | The weather changes hour by hour in runs; a run of sunshine lasts many hours; a storm or heavy rain lasts an hour or two | | Your view: does it feel real? |
| 4 | US-081 | On the forecast sheet tap the **?** on a "likely" row; then tap **?** on Today | "Forecasts for tomorrow are right 80%"; the calendar's *Why?* (a season is 4 days, the clock is your phone's) | | |
| 5 | US-080 | Tap cash, slowly or quickly | Money and loans opens, never a *Why?*; the **?** on Cash shows today's cash movements | | |
| 6 | US-080 | Tap the store, slowly or quickly | The Store opens, never a *Why?*; the **?** on "In the store" shows what fills it | | |
| 7 | US-080 | Tap **≡ Menu** | Six rows with pictures: Store, Money and loans, People, Buy land, You and your family, Settings; each **Open** works | | **Screenshot** |
| 8 | US-082 | Menu → **Money and loans** → **Borrow** on the moneylender → 12,000 → look at the terms | Amounts are a quarter, half, three quarters and all of what you can borrow; each term shows "First …, last … · interest … in all", and a longer term costs more interest in all. **Borrow** over 2 seasons | | **Screenshot** of the terms |
| 9 | US-082 | Back on Money and loans | The loan: "Owes 12,000 coins · N due at the season's end" (N is the interest to the close, not 0). **Repay**: the next payment, all of it, and **Ask** greyed with the lender's reason | | |
| 10 | US-081 | On Money and loans tap the **?** on "What you own minus what you owe" | Cash, paid ahead, land at today's price, buildings, goods in store, loans; then the same in your books; a lesson in italics. **OK** closes it with one tap | | **Screenshot** |
| 11 | US-082 | Tap the house (or Menu → You and your family) | Your work today, health, tiredness as words; food plans (Bare minimum explained, not offered: it only comes when food can't be paid); work hours; day labour with "About N coins a day"; eat from the store On/Off; medicine | | |
| 12 | US-082 | Choose **Balanced** food, then **A quarter** day labour | Each change shows as Chosen; the cash and the day's work change | | |
| 13 | US-082 | Store → tap a good's row (not its button) | The good's sheet: in store, on offer, spoils by tomorrow, the market price, the trader's price and room, each with a **?** | | |
| 14 | US-082 | **Offer at the market** → Half → At least today's price | Back on the good: on offer shows the half; **Take off market** appears | | |
| 15 | US-082 | Store → **Buy inputs** → Seed potato | "To sow 1 / 2 / 6 acres" with the kg and the price; a row you can't afford is grey with the reason. Buy one | | |
| 16 | US-082 | Store → **Mandi trip** → Change a good → load it | The sum: at the mandi, commission, trolley hire, your time, you get, the trader pays now, better or worse by. A small load is worse than the trader (the trolley costs 1,000) | | **Screenshot** |
| 17 | US-082 | Menu → **Buy land** → **Show** on a parcel | The map pans to it and its sheet opens. Land not next to yours: both Buy buttons grey with the same reason. Next to yours: **Buy with a bank loan** shows what you pay now, and opens one row per term | | |
| 18 | US-080 | Sow a radish; **+1 hour** ×3 | The **!** appears with a count; tapping it lists "1 ready to collect" with **Collect all** | | |
| 19 | US-083 | Fill the store near full (buy compost), sow radish on several fields, **+1 hour** ×3, **Collect all** | A card: "N kg won't fit. Sell the rest to the trader for N coins?" **Sell** adds exactly that to cash | | **Screenshot** |
| 20 | US-082 | **+1 season** | The season's report opens by itself; each line's **?** lists its accounts | | **Screenshot** |
| 21 | US-083 | **Away cap (72 h)** with crops growing | The away card: "Away 3 d", what ripened, sales, spoilage…, money rows biggest first, **Collect all** and **See the report** if a season closed. Close it; leave the app for a minute and come back with nothing new: **no** card | | **Screenshot** |
| 22 | US-084 | Menu → Settings → **Western** | Numbers become 120,000-style everywhere (HUD and sheets); switch back to Indian | | |
| 23 | US-084 | Settings → **Small**, then **Large**, then **Normal** | The whole UI shrinks, then grows; sheets still fit and scroll; nothing overlaps on the HUD | | **Screenshots** at Small and Large |
| 24 | US-084 | Turn on iPhone Settings → Accessibility → Motion → **Reduce Motion**, then start the game with no choice made in Settings | Reduced motion is **On** in the game's Settings; sheets fade instead of sliding | | |
| 25 | US-087 | Play with the sound on: tap buttons, open sheets, sow, harvest, sell | Silence on taps and sheets; a rustle when sowing, a pop and rustle on harvest, coins (higher for big sales); no sound piles up | | Your view |
| 26 | US-084 | Settings → **Sound Off**; **Haptics** On, harvest and sell | Silence; a light tap in the hand on harvest and sale. Haptics Off: no tap | | |
| 27 | US-088 | Developer panel → **Golden replays** | `001`, `003`, `004`, `005-credit.json` each **PASS**, then **ALL PASS**, with the re-pinned hashes: 001 `0x9803A63478EDC81C`, 003 `0xCA6205EDED737A6C`, 004 `0x98D9EE70B32631A0`, 005 `0x3FEB89B591B5F831` | | **Screenshot** |
| 28 | Feel | Play 10 minutes | Taps respond at once; no stutter when the forecast or a sheet updates every second | | Your view |

**Device review changes (ADR 0044), built after device run 1:**

| # | Change | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 29 | Back | Menu → **Store** → **Mandi trip** → **←** → **←** | Mandi trip, then the Store, then the Menu (no **←** on the Menu); the sheet never closes on the way | | **Screenshot** of the mandi trip |
| 30 | Back | Menu → Money and loans → **Borrow** → an amount → a term | The loan is taken and the sheet returns to Money and loans, whose **←** still leads to the Menu | | |
| 31 | Repay | **Repay** → **All of it** → Pay; or wait a minute on the Repay sheet first, then pay | The loan is gone: "No loans · You owe nothing."; no 0-coin payment left | | |
| 32 | *Why?* | In the Store, scroll the list with a finger resting on rows; tap a row's text | No *Why?* opens; only a **?** opens one | | |
| 33 | Net worth | Money and loans | "Of that, your land" under "What you own minus what you owe": most of what you own is your 9 acres at today's price | | |
| 34 | Sounds | Tap around the Menu and sheets; then sow a field | Taps and sheets are silent; sowing rustles | | |

## What to report

- Screenshots of checks 2, 7, 8, 10, 16, 19, 20, 21, 23, 27 and 29.
- Any *Why?* that reads oddly, uses a number you don't understand, or is missing on a number.
- Anything you couldn't reach in three taps.
- How the weather felt over a day (check 3).
- Any **FAIL** in check 27: the file and the hash it showed.
- Crashes: what you did just before; with Xcode attached, the last lines of its console.
- Paste the results into the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
