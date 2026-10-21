# Sprint 08 build: Dadi's letter, the first session, the guided first week and the Hamlet

- **Build:** the head of branch `sprint/08-onboarding-hamlet` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (`--options '["Development"]'`), so the developer panel and the golden replays exist. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash). Quick check: holding the time card for 3 seconds opens the developer panel.
- **Your town:** this build adds the letter's and the era's state, so **your Sprint 7 town is refused and kept** ("A new town has started"), as ADR 0041 §2 planned until the Sprint 9 playtest build. A new town starts with the letter.
- **Notifications:** to see the letter ask, turn the game's notifications **off** in Settings first if they are on. (iOS asks for permission only once per install; if you allowed it in Sprint 6, *Yes* just turns them on.)

**What this build is (spec 009, ADR 0045). It is feature-complete for Phase 1: Sprint 9 only fixes and tunes.**
- **Dadi's letter.** Your grandmother Kamla's letter comes a page at a time, each when it matters. A page has **Continue**, sometimes **Show me** (opens the sheet it talks about), and in the first session **I'll find my own way** (ends the guided steps; the later pages still come). Every page can be read again: **Menu → Dadi's letter**.
- **The first session** (about 15–20 minutes): the farm is yours → sow the *baadi* (kitchen garden) → the household → pick it → sell to the trader → sow a long crop (potato) → "a good first day", with the notification question.
- **A nudge** at the bottom of the screen says what to do now, and a **gold arrow** bobs over the place to tap. While the first bed grows, the nudge shows three short tour lines in turn (tap one to open it).
- **The first week:** a page the first time you come back to news, harvest a field, see pests, fill the store or the trader's day, close a season, start each new season (with that season's crops and prices), need more hands, run short of money, fall sick, and reach the Hamlet.
- **Becoming a Hamlet** (Menu → Dadi's letter → last row): this season's harvested acres (of 6) and their care, the hand's house, a profitable season, each with a **?**. When all three hold, the map's light warms, a banner drops in, and Dadi's Hamlet page follows.
- **New icons:** Dadi's letter (an envelope), the Menu (a red *bahi-khata* ledger), the alerts (a brass bell), the mandi trip (a tractor-trolley).

**Not in this build (on purpose):** what the Hamlet adds (Phase 2: "More of the valley opens in the next update"); migrations of old saves (Sprint 9); alert dismissals (Sprint 9, only if testers ask).

**Your Sprint 8 questions:** (1) does the letter feel warm and short, never a wall? (2) did you always know what to do next in the first session? (3) did any page come at a wrong moment, or twice? (4) how long did your first session take (from the first page to "a good first day")? (5) do the new icons fit the game's look?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**.

### The first session (US-089, US-090)

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-089, AC-11 | Launch, tap **OK** on "A new town has started" | The first page, "The farm is yours", over the map: Dadi's text, signed "Your Dadi, Kamla", "Page 1 of 1 · read it again from the Menu", and **Continue** only. Note the time on the clock | | **Screenshot** |
| 2 | US-090, AC-13 | **Continue** | "The baadi" page with **I'll find my own way**, **Show me** and **Continue**; no text wraps beside the buttons | | |
| 3 | US-090, AC-13 | **Continue** | No page. A nudge at the bottom: "Tap a garden bed by the hut and sow radish", and a gold arrow bobbing over the first garden bed | | **Screenshot** |
| 4 | US-090 | Tap that bed → **Sow** radish | "The household" page opens | | |
| 5 | US-090 | **Show me** | You and your family opens. Close it | | |
| 6 | US-090, AC-13 | Watch the nudge while the radish grows (10 min) | "Pick the bed when it's ready", then every few seconds a tour line (the Store, the well acres, your day); tapping a tour line opens it. When the bed is ready, only the pick line stays | | |
| 7 | US-090 | Pick the bed (tap it → **Pick**, or **Collect**) | "Your first basket" opens **after** the pick, never before | | |
| 8 | US-090 | **Continue** | Nudge: "Open the Store and sell to the trader"; the store pill bounces every few seconds | | |
| 9 | US-090, AC-16 | Store → **Sell** the radish | "The trader at the gate" page. The first sale came within 15 minutes of the first page (check the clock) | | |
| 10 | US-090 | **Continue** | "Something that takes longer" (potato on a well acre). **Show me** opens the well acre | | |
| 11 | US-090 | **Sow** → **Sow Potato** on that acre | "A good first day": "Your potato will be ready at HH:MM tomorrow. Want a nudge when it's ready? Never at night." with **Yes, nudge me** / **Not now**; no "twice a day" | | **Screenshot** |
| 12 | US-090, AC-14 | **Yes, nudge me** | iOS asks for permission (first time on this install) or notifications simply turn on; Settings shows notifications **On**. No nudge or arrow any more | | |
| 13 | US-090, AC-2 | — | How long from check 1 to check 12? (target 15–20 minutes) | | Minutes: |

### Skipping and the notification fallback (US-090, AC-3, AC-14)

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 14 | US-090, AC-3 | Settings → turn notifications **off**; Settings → **New game** → replace. On the garden page tap **I'll find my own way** | No more nudges or arrows; you can play freely | | |
| 15 | US-090, AC-14 | Sow potato on any field | The plain notification card asks (the letter skipped, so the card is the first ask) | | |
| 16 | US-090, AC-3 | New game again (notifications off). On "The baadi" tap **Continue**; tap the house first (You and your family), close it; then sow a garden bed | The household step is skipped (you're ahead): no "The household" page; the nudge goes straight to "Pick the bed when it's ready" | | |

### The guided first week (US-091, US-092)

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 17 | US-091, AC-4 | Sow radish on a field; **+1 hour** ×3; harvest it | "Your first field harvest" (tap the **?** beside a harvest). A second field harvest brings no page | | |
| 18 | US-091, AC-4 | With nothing growing, close the app for a minute and reopen | No away card and no "Welcome back" page (under 8 hours away, ADR 0046) | | |
| 19 | US-091, AC-4 | Developer panel → **Away cap (72 h)** | The While-you-were-away card first; after you close it, "Welcome back" | | **Screenshot** |
| 20 | US-091, US-092, AC-4, AC-6 | **+1 season** | The season report opens; after it, "Your first season", then "Summer": "Zaid season. You can sow radish, rice and lentils now. Radish fetches its best price this season." | | |
| 21 | US-091, AC-4 | In Summer sow lentils on all 6 acres; **+1 hour** | "More hands" (your care is short of what six acres need) | | |
| 22 | US-091, AC-4 | With little cash, try something you can't pay for (sow, the hand's hut, hiring) | The rejection, then "Borrowing" (it only explains; no loan is taken, no loan sheet opens by itself) | | |
| 23 | US-092, AC-6, AC-7 | **+1 season** twice (to Winter) | "The monsoon" (waterlogging), then "Winter": wheat, potato and lentils; "Wheat fetches its best price"; and, if the house still eats grandfather's wheat or lentils, the seed-keeping line | | |
| 24 | US-092, AC-12 | Menu → **Dadi's letter** | Every page you've seen, in the order it came, then "Becoming a Hamlet"; no page you haven't seen. **Read** one: Continue (and Show me works); reading sends nothing | | **Screenshot** |
| 25 | US-091, AC-5 | Close and reopen the app; Settings → **Export**, then import it | No page you've seen comes again; the list is the same | | |

### The Hamlet (US-093, US-094)

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 26 | US-094, AC-9 | Menu → Dadi's letter → **Becoming a Hamlet** | "This season's harvested acres 0 of 6", the hand's house, a season with farm profit; each **?** explains in words; after a harvest, the care row shows "NN% (needs 90%)" | | **Screenshot** |
| 27 | US-093, AC-8 | Build the hand's hut, hire a hand, give orders on the inherited acres; in one season harvest all 6 acres with good care; have a season with farm profit (use **+1 hour** / **+1 day**) | The moment the last condition holds (at a field harvest or a season close): the map warms, "A Hamlet!" banner, then Dadi's "A Hamlet" page. It never comes again | | **Screenshot** of the banner |
| 28 | US-094 | Settings → export the play log | It has a `hamletUnlocked` line | | |

### Icons and regression

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 29 | carry-over | Look at the HUD's Menu and alerts buttons, Menu → Dadi's letter, Store → Mandi trip | A red ledger on the Menu button, a brass bell on alerts, an envelope beside Dadi's letter, a trolley on the mandi trip | | |
| 30 | regression | You and your family | The food plans read "18,000 coins a season · 10% less work" (no "work work") | | |
| 31 | regression | Developer panel → **Golden replays** | 001, 003, 004, 005 PASS (their hashes are unchanged this sprint) | | |
| 32 | US-098 | — | Nothing to do on the phone: the Phase 1 health checks run in CI (never stuck, recovery after foreclosure, exact ledger, bounded prices, the stress bots, the Hamlet's pace) | n/a | |

### Device review fixes: re-check (ADR 0046, 2026-10-21)

First run (2026-10-21): every check passed; checks 1 to 12 took 10 minutes. These re-check the three findings. **Re-check (2026-10-21): checks 33–36 PASS.** A fresh town (Settings → **New game**) makes 33 and 34 easiest.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 33 | ADR 0046 §1 | Any letter page, a card (New game's question), a **?**, and the developer panel | The dim covers the **whole** screen, edge to edge, including beside the notch and along the bottom; the page stays centred | PASS | **Screenshot** |
| 34 | ADR 0046 §2, spec 008 AC-8 | Developer panel → **Away +1 hour** (new button), close it | No away card and no "Welcome back" | PASS | |
| 35 | ADR 0046 §2 | Developer panel → **Away +1 day** | The away card ("Away 1 d"); in a town that hasn't had one, "Welcome back" after it | PASS | |
| 36 | ADR 0046 §3 | In a town still on "Open the Store and sell to the trader": put everything in the Store on offer at the market (each good → **Offer**), or play until the house eats it | The nudge and the store's bouncing stop; when there's something the trader buys again, they come back. The developer panel opens above the nudge | PASS | |

## What to report

- Screenshots of checks 1, 3, 11, 19, 24, 26, 27 and 33.
- The first session's length (check 13) and any step where you didn't know what to do.
- Any page that came at the wrong moment, twice, or with words that read oddly.
- Any **FAIL** in check 31: the file and the hash it showed.
- Crashes: what you did just before; with Xcode attached, the last lines of its console.
- Paste the results into the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
