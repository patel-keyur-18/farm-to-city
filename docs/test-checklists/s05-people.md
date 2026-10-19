# Sprint 05 build: people

- **Build:** the head of branch `sprint/05-people` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (tick *Development Build*), so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).

**What this build is:** the first playable plus people (spec 006, ADR 0039). A **People** button (bottom right) opens the People sheet: hire day labourers for the rest of today, build a hand's hut, send word for a hand, talk to and hire applicants. A hand lives in a round thatched hut in the **labour quarters**, a fenced acre beside the yard (ADR 0040), and follows **standing orders** you give from a field or tree ("looks after this field"), even while the app is closed. The hut, the well and every person are now **Blender renders** (US-183); crops, trees and beds are still painted placeholders until Sprint 6. Coming back after more than a minute away shows a **While you were away** card with what your hand did.

**Not in this build (on purpose):** regulars who ask to move in (Sprint 6), saves (each launch is still a new game, Sprint 6), the loan screen (Sprint 7: use the developer panel's **Moneylender loan 20,000** to afford a hut on day one), *Why?* sheets (Sprint 7), walk and hoe animation frames (people move with code motion only).

**Your Sprint 5 questions:** (1) do the people and the hand's hut look right and make the farm feel alive? (2) is hiring clear: stars and words instead of numbers, the wage, Talk, Offer? (3) does giving orders from a field make sense? (4) does the hut ghost feel natural to drag?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **Away cap (72 h)**.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-183 | Open the app; pinch in on the yard | A roomy **2-acre yard** behind a low bamboo fence: the house (teal door, thatch, pots, charpai) and the well on the back acre; in front of the door a white tulsi planter, the two garden beds, a haystack and a woodpile; tree places on the corners. Beside it a sandy, fenced **labour quarters** acre. The founder stands at the door | | **Screenshot** |
| 2 | US-069 | Tap **People** (bottom right) | A full-height sheet: *Your people*, sections **Day labourers**, **Farm hands**, **Hands' huts**, **Send word for a hand**. Each choice is a row with its own button | | |
| 3 | US-066 | Sow two acres, then open People and look at **1 labourer** | "Care today: Fair → Good" (words, not numbers) and a **Hire** button with the cost for the rest of today. Tap it | Cash goes down; a labourer (a man in a banyan with a hoe, or a woman in a saree with a basket) walks in from the west and works in a field | | |
| 4 | US-066 | **+1 day** | The labourer walks off the map at midnight; People shows a new day's free count (2 in Spring) | | |
| 5 | US-068 | Developer panel → **Moneylender loan 20,000**; then People → **Build a hand's hut · Place it** | The sheet closes; a see-through **green** hut appears in the labour quarters, with a bar: *Place the hand's hut* · **Build here 15,000 coins** · **Cancel** | | **Screenshot** |
| 6 | US-068 | Drag your finger across the map | The ghost follows from acre to acre. Over a field, the yard or land you don't own it turns **red** and the bar says why ("A hand's hut goes in the labour quarters beside your yard."); back on the quarters it's green | | |
| 7 | US-068 | Put it on the quarters, tap **Build here** | A bamboo frame with half-built walls and thatch bundles appears; tapping the quarters shows "Hut place 1 · Being built · ready in 24 h". **+1 day**: the finished round hut pops in, and a note says the hut is ready. There's room for a second hut beside it | | |
| 8 | US-069 | Wait (or **+1 hour** a few times) | "Someone came asking for work"; a red badge with the number of applicants on **People** | | |
| 9 | US-069 | Open People → *Asking for work* | Each applicant: name, **stars** (solid = sure, faded = possible), words like "Capable to Skilled", one trait and a "?"; **Offer** shows the season wage and what you pay now. **Talk** reveals the second trait | | Is it clear? |
| 10 | US-069 | **Offer** | Most accept (about 85%): "Your farm hand moved in!", the badge clears, the hand appears on the map. A decline removes that applicant | | |
| 11 | US-070 | People → the hand's **See** | The hand's sheet: skill stars and word, "Tends about N acres", both traits with a line each, the contract (30,000-ish a season, paid to the season's end), paid ahead, standing orders, **Let go** with what you get back | | |
| 12 | US-070 | **Let go** | A card asks "Let *name* go? …" with a red **Let go** and **Keep them**. Keep them changes nothing | | Don't confirm unless you want to re-hire |
| 13 | US-071 | Tap an empty acre: the field sheet has *"name's orders: No order here"* with **Give order** | The order sheet lists every crop (sowable now or "Sown in Winter"). Choose one, then **Keep in store**, **Sell at the market, not below N** (− and + change N) or **Sell to the trader at once** → **Set** | | |
| 14 | US-072 | Give the hand orders on **two or three** fields | **At once** the hand walks to the first field and sows it (one field an hour after that: **+1 hour** for the next). They go round their fields, hoeing a few seconds at a couple of spots on each (bending over the hoe); when ready they harvest and sell or store as ordered. The field sheet shows "Last: …, … ago". With nothing to do they walk home to their hut | | |
| 15 | US-071 | Sow a *different* crop yourself on an ordered, empty field | A card asks "Change *name*'s order to *crop* too?" — **Change the order** or **Just this once**; either way your crop is sown and the hand never undoes it | | |
| 16 | US-182 | Developer panel → **Away cap (72 h)** | A **While you were away** card drops in: what the hand brought in (harvests, kg), sold for, sowed (fields, inputs), anything waiting, a season paid or unpaid | | **Screenshot** |
| 17 | US-070 | Spend cash down before a season ends (hire every free day labourer each day and sow every acre) until cash is below the hand's wage, then **+1 season** | "A hand's season couldn't be paid": the hand stops work, the People row says "Season unpaid" with **Renew** (the whole season's wage, waiting days included) | | |
| 18 | US-088 | Developer panel → **Golden replays** | `001`, `003`, `004`, `005-credit.json` each **PASS**, then **ALL PASS**. Hashes unchanged from Sprint 4: 001 `0xAA0C65B7299B2C52`, 003 `0x3298BCB6B36F689D`, 004 `0x42A935851A1C8BB2`, 005 `0x4D9D7611B2753AC9` | | **Screenshot** |
| 19 | ADR 0040 | Sow a few fields and hire a labourer | You, the labourers and the hand move between the growing fields and hoe; at midnight the labourers walk off; with no crops everyone goes home (you to the house door) | | |
| 20 | Feel | Play 10 minutes with a hand and orders | Taps respond at once (People button, sheet buttons, the ghost drag); people move smoothly; no stutter | | **Your view** |

## What to report

- Screenshots of checks 1, 5, 16 and 18.
- How the people and huts look at game scale (too small, too big, too busy?), and anything that looks broken or plain.
- Any tap that missed or hit the wrong thing, especially the People button, buttons inside the sheets and the ghost drag.
- Any **FAIL** in check 18: the file and the hash it showed.
- Crashes: what you did just before; with Xcode attached, the last lines of its console.
- Paste the results into the sprint review section of `docs/planning/sprints.md`, or into the chat with Claude.
