# Sprint 04 build: the first playable

- **Build:** the head of branch `sprint/04-credit-first-playable` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **This is a Development build** (tick *Development Build* in Unity's Build Profiles): the developer panel only exists in development builds (ADR 0038).

**What this build is:** the homestead as a playable farm. A painted isometric map of the valley (8 × 6 acres, your 6 acres, the hut, the well and the kitchen garden), a small HUD (time, cash, store, **Collect all**), and sheets that open when you tap things. You can sow, spray, harvest, plant trees, tend the garden, sell to the trader and buy land. Hold the time strip for 3 seconds to open the developer panel.

**Not in this build (on purpose):** loans on screen (credit works in the simulation, tested by 205 tests, but has no screen until Sprint 7), the founder's and hiring screens (Sprint 5), saves (**each launch is a new game**, Sprint 6), the letter and onboarding (Sprint 8), the *While you were away* card and *Why?* sheets (Sprint 7). Pictures are painted placeholders: Blender renders replace them in Sprint 6 (ADR 0038).

**Your Sprint 4 questions:** (1) does it feel good and look like a cheerful farm game (be blunt: what looks broken or plain)? (2) is every tap quick and obvious? (3) are the numbers readable (1,20,000 style)?

## Checks

Time passes in real time (a potato takes 24 hours). Use the developer panel to jump ahead: **+1 hour**, **+1 day**, **Away cap (72 h)**.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-078 | Open the app | The valley fills the screen: green land, a brown ploughed block of 6 acres, a thatched hut with a well and two garden beds, small signboards on land next to yours. Top-left a brown time card (date, clock, weather icon), top-right cash and the store bar, bottom-left **Collect all · 0** (grey) | | **Screenshot** |
| 2 | US-078 | Pinch to zoom in and out; drag with one finger | Smooth zoom between the whole valley and about 3 acres; it never leaves the map | | |
| 3 | US-079 | Tap a ploughed acre | The acre gets a white ring, bounces a little, and a sheet slides up: *Loam field*, fertility, moisture, soil factor, a **Sow** button. The map shifts so the acre stays visible above the sheet | | |
| 4 | US-079 | Tap **Sow** | A list of crops for this season with **good / fair / poor**, "about N" per acre, hours a day, and what sowing buys. Wheat is greyed with "Winter only" | | Is it clear which crop to pick? |
| 5 | US-079 | Tap **Sow Radish** | The sheet closes; the acre pops with little sprouts; cash counts down | | |
| 6 | US-079 | Tap a garden bed (beside the hut) and sow spinach or radish; wait 10 to 15 minutes (or use +1 hour) | The bed grows; when ready a white bubble with a green tick bobs above it | | |
| 7 | US-079 | Tap a ready field or bed, then **Harvest** / **Pick** | The picture flies to the store button, the store bar grows and the button bounces | | |
| 8 | US-078 | Use the panel's **+1 day** until the potato or radish is ready; watch the stages | Sprouts, bigger plants, then full plants with a bubble; the colour of the map warms at dusk and goes blue at night, and changes with the season | | |
| 9 | US-079 | Tap **Collect all · N** | Everything ready is picked in turn, nearest to spoiling first; the icons fly to the store. If the store is nearly full, what doesn't fit stays and a note says so | | |
| 10 | US-079 | Tap the store bar (or the hut) | The **Store** sheet (full height): one row per good with what you hold and the trader's price, and a **Sell** button on each produce row. Tap one | Coins fly to the cash and it counts up | | |
| 11 | US-079 | Tap an empty tree place (a pit ringed with stones: in the yard next to the hut, and at the left and right corners of each acre) and plant a banana or mango | The sheet lists 4 trees, one row each with when it fruits, how long it grows and a **Plant** button with the price; planting closes the sheet and a small sapling appears. A grown tree shows fruit and a bubble when ripe | | |
| 12 | US-079 | Tap land you don't own, next to yours | **Land for sale**: price, your cash, quick resale, a **Buy this land** button greyed with the reason ("You don't have enough coins for that.") | | |
| 13 | US-079 | Tap a growing crop | Time left; Care, Water and Weather as **Good**, **Fair** or **Poor**; pests; **Spray** and **Clear field** buttons | | |
| 14 | US-079 | Tap the map while a sheet is open | The sheet closes (a second tap selects) | | |
| 15 | US-086 | Read every number on screen | Indian grouping (1,20,000), coins, kg, hours and minutes read naturally; the developer panel's **Switch number format** changes it to 120,000 | | |
| 16 | US-088 | Hold the time card for 3 seconds | The developer panel opens: time travel, snapshot and restore, number format, reduced motion, **Golden replays** | | |
| 17 | US-088 | In the panel, tap **Golden replays** | `001`, `003`, `004` and `005-credit.json` each **PASS**, then **ALL PASS**. Expected hashes: 001 `0xAA0C65B7299B2C52`, 003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2`, 005 `0x4D9D7611B2753AC9`; and a 72 h catch-up time well under 2,000 ms | | **Screenshot** |
| 18 | US-088 | **Snapshot**, sow and harvest a few things, then **Restore** | Everything is back as it was at the snapshot | | |
| 19 | US-088 | **Away cap (72 h)** | Crops that finished wait ready; nothing is lost (they spoil only after 72 h of waiting); the HUD shows the new time | | |
| 20 | US-078 | Turn the phone to both landscape directions; look at the corners | Nothing hides under the notch or the home indicator | | |
| 21 | Feel | Play for 10 minutes | Taps respond at once; no stutter; the motion (bounces, flying icons, counting coins, sheets sliding) feels like a cheerful farm game | | **Your view** |

## What to report

- Screenshots of checks 1, 3, 4 and 17 (or type the numbers).
- Anything that looks broken, too plain or confusing, with the screen it was on. The picture style is a stand-in; say what matters most before the Blender sprites (Sprint 6).
- Taps that missed or hit the wrong thing (map taps are the risk on a phone).
- Any **FAIL** in check 17: which file and the hash it showed. This is the most important result.
- Crashes: what you did just before. If Xcode is attached, copy the last lines of its console.
- Paste the results back in the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
