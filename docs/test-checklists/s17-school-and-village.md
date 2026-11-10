# Sprint 17 build: the school and literacy, rivals, the Village unlock, the Codex and the advisors

- **Build:** the head of branch `sprint/17-school-and-village`, a Development build made from the open Editor on 2026-11-10 (Unity: Build Succeeded; the unsigned Xcode Release compile: BUILD SUCCEEDED). Rebuild from the merged `main` if you prefer, with the guide
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (2026-11-10, scripted touch, on a copy of your town):** see the Notes column. Captures are in `docs/art/s17-check-*.png`, and each story's own tour is in `docs/art/s17-1xx-*.png`. Set up by script, in the session only: 5,00,000 test coins, **Next era**, a tea stall founded and priced, dev time travel. **Only a device can check:** 1 (your own town's update on the phone), 34–37 (reduced motion, text size and rotation on the phone), 38 (frame time), 39 (notifications), 40 (your normal morning) and 41 (Snapshot and Restore).

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 14 to 15):
   - Your fields, cash, people, houses, animals, loans, businesses and shops stay as they were.
   - There's no school yet. People who could read still can. No Codex cards are found yet (nothing is back-filled); each is found the next time its moment comes.
   - A business keeps its profitable seasons, counted from its own books.
   - The first time it loads, the app also saves your town as it was before the update in the Files app, as `FarmToCity-before-update-schema14.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Either copy can bring it back.

## What this build changes

- **The village school (US-131, ADR 0095 §5).** Tap the school plot on the main street (from the Hamlet): **Build the school** (1,05,000, two days) or **With a loan** from the co-op bank. It's a gift to the village: it becomes the district's school, so its cost isn't in your farm's profit. The district posts the teacher; you pay 2,000 upkeep a season. Pupils play in its yard 9:00–14:00.
- **Literacy (US-132).** A child reads after 4 whole seasons at school. The school's sheet has an **Adult class** (3,000: up to 15 people aged 14+, readers 96 hours after it starts). A grown-up who can read earns 10% more at outside work. The Town tab shows **Literacy** (everyone 7+) with its *Why?*.
- **Rivals (US-122, ADR 0096).** Price a shop above the usual and a newcomer's cart may set up against you (an even chance each season), on a free pitch or by the square. The village grocer and tea stall cut their price 5% when they lose a third of their customers, and go back when they win them back.
- **The Village (US-102, ADR 0097).** Three goals: **5 households**, **a business with 2 profitable seasons**, and **the school open**. Menu → Dadi's letter → *Becoming a Village* shows each with a *Why?*. When all three hold, the Village opens with a celebration and Dadi's `village` page. Expect 4–5 weeks from the Hamlet with loans; homes first is quickest.
- **The Codex (US-133, ADR 0098).** 25 ideas from your own play (opportunity cost, profit isn't cash, interest…). When you meet one, a toast shows for 6 seconds above the bottom buttons, then goes. Menu → **Codex** keeps them, each with your numbers and a real-world line.
- **The advisors (US-134, ADR 0099).** Sunita Verma (the co-op bank), Ramdeen Kaka (your farmer neighbour) and Masterji Hariprasad (the retired headmaster). One line on the season report, and one under an alert whose cause they know. At most one a sheet, a topic once a day, none in your first session. They never do anything for you.
- **Balance (ADR 0100):** good chai's *Why?* now says a higher price can bring a newcomer; literacy at the Village is about 45% (the village has had no school).

**Your questions:**
1. Does the progress sheet make it clear what to do next for the Village?
2. Do the Codex toasts feel like a reward, or like interruptions?
3. Are the advisors' lines useful and believable? Too many, too few?
4. Is it clear that the school is a gift and not an asset of yours?
5. Do the newcomer's cart and the village shops' price cuts make sense when they happen?

## Checks

**The developer panel:** hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**, **Next era**, and **Snapshot** / **Restore**.
- Take a **Snapshot** after check 1. **Restore** at check 41 puts your town back.
- Checks 2–24 need the Hamlet; checks 25–30 need the Village. If your town is earlier, **Next era** moves it on.
- Several checks spend money. If cash runs short, play a day on (**+1 day**) or skip that check and say so.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (spec 018 AC-10) | Launch with your town | Same cash, fields, people, houses, animals, loans, businesses and shops; no school; Menu → Codex empty. In the Files app: `FarmToCity-before-update-schema14.ftc` | | **Screenshot**. Editor: the owner's town copy migrated, all as before |
| 2 | US-131, AC-1 | Tap the school plot on the main street | *The school plot*: the district sends the teacher; cost (a gift to the village), ready in, upkeep, each with **?**; **Build the school** and **With a loan** | Pass (Editor) | **Screenshot**. Editor (`s17-check-c02`). Found and fixed: a Codex toast that came just before a sheet opened covered its buttons; it now goes when a sheet opens. The teacher is in the sheet's line |
| 3 | US-131, ADR 0095 §5 | The cost's **?** | Building, benches and books, 48 h; "A gift to the village… your farm's profit doesn't [fall]" | Pass (Editor) | Editor: 93,666 building + 15,611 benches and books, 2 d, the gift and teacher lines |
| 4 | US-131 | **Build the school** | *The school, being built*, ready in 2 days; the building stage drawn on the plot | Pass (Editor) | **Screenshot**. Editor: 1,09,277 paid; the building stage drawn |
| 5 | US-131, AC-4 | **+1 day** twice | The school drawn finished; the away line or alert "The school is open"; at 9:00–14:00 pupils in its yard | Pass (Editor) | **Screenshot**. Editor (`s17-check-c05`): drawn open with its teacher; *Public goods* found. No pupils: this town has no children (US-131's tour showed them) |
| 6 | US-131, AC-4 | **+1 season**, then Money and loans → *Last season* → **See the report** | A *Gifts to the village* line with the school's cost and upkeep; *Did the farm pay for itself?* doesn't include it | Pass (Editor) | Editor: *Gifts to the village* −1,09,277 on its own line; the farm's 4,364 without it |
| 7 | US-132, AC-3 | People → **Town** tab → *Literacy* and its **?** | A whole percent; "Can read, aged 7 and older" over "Everyone aged 7 and older"; "The Township will need 40%" | Pass (Editor) | **Screenshot**. Editor (`s17-check-c07b`): 100% (1 of 1 aged 7+), with the 40% and 10% lines |
| 8 | US-132 | The school's sheet | Pupils "n of 30 seats", *Next to finish primary* with a name and seasons; *Adult class*: *Would join n of 15 places* | Pass (Editor) | Editor: 0 of 30 seats, 1 teacher; *Start the class* greyed: "Everyone 14 and older can read" |
| 9 | US-132, AC-2 | *Would join*'s **?**, then **Start the class** | The names who'd join; 3,000 paid as a gift; *Learning to read · n · ends in 4 d* | | Not in this town (nobody to teach); US-132's tour ran it with a set-up family (`s17-132-*`) |
| 10 | US-132, AC-2 | **+1 day** ×4 | Readers: the away card names them; *Literacy* up | | As 9 |
| 11 | US-132, AC-5 | A reader's citizen card → *Schooling* and its **?** | "Can read"; outside work pays 10% more | | As 9 |
| 12 | US-122, ADR 0096 | (Village) a tea stall of yours at **10% above the usual price**, then **+1 season** a few times | Most seasons: "Someone from outside set up a chai cart…": the cart drawn on a free pitch or by the square, with an orange umbrella | Pass (Editor) | **Screenshot**. Editor (`s17-check-c29`): at 1.1 a newcomer came at the first close: the chai cart with its orange umbrella on Pitch II |
| 13 | US-122 | Your shop's card → **Customers** | The newcomer listed with its share and **?** | Pass (Editor) | Editor (`s17-check-c13`): you 34%, the village tea stall 34%, the newcomer's chai cart 33%, each with **?** |
| 14 | US-122, AC-4 | Price back at the usual; keep playing | The village tea stall may cut its price: "…now sells at …, below its usual: it lost customers to you." | | Not seen in three seasons here; US-122's tour ran it (`s17-122-*`); `RivalTests.PriceCut` |
| 15 | US-102, AC-8 | Menu → **Dadi's letter** → *Becoming a Village* | Households n of 5; *A business, profitable seasons* "n · its name"; *The school*; each row's **?** says what to do (homes first is quickest) | Pass (Editor) | **Screenshot**. Editor (`s17-check-c15`): households 1 of 5, business 0 of 2, the school Open, each with **?** |
| 16 | US-102 | Scroll to *When you're a Village* | The bakery, dairy, shops, drip, borewell, tractor, a bigger co-op bank loan… | Pass (Editor) | Editor: bakery and dairy, the three shops, drip and borewell, the tractor, the co-op bank up to 25,00,000 |
| 17 | US-102, AC-9 | When all three hold (or set up: a profitable business, 5 households, the school open) | The celebration, then Dadi's `village` page naming your business; the sheet now *Your Village* | Pass (Editor) | **Screenshot**. Editor: **Next era** showed the celebration and *Your Village* (`s17-check-c17`); the real gate's unlock is in US-102's tour (`s17-102-*`) |
| 18 | US-133, AC-6 | Play on until a toast appears above the bottom buttons (a season close usually brings one) | "New in your Codex · <idea>" or "n new in your Codex"; it goes after about 6 seconds; it waits while a sheet or letter is open | Pass (Editor) | **Screenshot**. Editor (`s17-check-c18`): "4 new in your Codex" waited for the report and Dadi's page, then went after 6 s |
| 19 | US-133 | Tap a toast | One idea: its card opens; several: Menu → Codex opens | Pass (Editor) | Editor: one card's toast opened *Public goods* |
| 20 | US-133 | Menu → **Codex** | Ideas by group with icons; an orange **New** tag on unread ones; "n of 25 ideas found" | Pass (Editor) | **Screenshot**. Editor (`s17-check-c20`): 5 of 25, groups with icons, orange **New** tags |
| 21 | US-133 | Open a card | The group over the title, the idea, *Your numbers* (your own), *In the real world*; OK. Its **New** tag goes | Pass (Editor) | Editor (`s17-check-c21`). Fixed after: *Public goods* read "0 children go there" (now "Children at it now, free: 0"), and its group repeated the title (now *Community*) |
| 22 | US-133 | People → Town tab | *Codex · n of 25 ideas found* with its **?** | Pass (Editor) | Editor: *Codex · 6 of 25 ideas found* with **?** |
| 23 | US-134, AC-7 | **+1 season** (or wait for a close) | The season report: an advisor's panel under the title: portrait, name and role, one line with your numbers. Read it against the report's rows | Pass (Editor) | **Screenshot**. Editor (`s17-check-c23`, US-134's `s17-134-report`): Ramdeen: "Field 5 sat dry for 61 hours under its crop…" (rain-fed, Summer). A close where nothing held showed no line |
| 24 | US-134, AC-7 | Open the same report again (Money and loans → *Last season* → **See the report**) | The same line | Pass (Editor) | US-134's tour: the same line on reopening |
| 25 | US-134, AC-7 | When pests strike (or a payment is missed, feed runs low, a raise is asked) → the bell | One advisor line, "Name: “…”", under that alert; never more than one on the list | Pass (Editor) | **Screenshot**. US-134's tour (`s17-134-alert`): Ramdeen under the pests alert. Here Masterji's raise note was recorded; the raise was answered from its card, so no alert stood |
| 26 | US-134 | Another pest alert on the same day | No second line for pests that day | Pass (Editor) | `AdvisorTests.Caps` |
| 27 | US-134 | Spray from the alert | The spray works as before; the advisor did nothing on their own | Pass (Editor) | US-134's tour: Spray as before |
| 28 | Balance, ADR 0100 | A tea stall's card → *Quality* **?** | "…a price above the usual can bring a newcomer… Good chai wins customers and stars; standard chai makes the money." | Pass (Editor) | Editor (`s17-check-c28`): the new wording; 80 ml against 60 |
| 29 | Map | Pinch out over the village | The school, its pupils, the newcomer's cart, the shops; nothing overlapping a road or a house | Pass (Editor) | **Screenshot**. Editor (`s17-check-c29`) |
| 30 | US-102 | Menu → Businesses (Village) | The bakery, dairy and the three shops can be started | Pass (Editor) | Editor: all six; your tea stall listed |
| 31 | Regression | Collect all, sow, harvest, sell to the trader, the shed, hire | As in Sprint 16 | Pass (Editor) | Editor: Collect all · 2 |
| 32 | Regression | The away card after a long absence (**Away cap**) | Lines for the school, readers, newcomers and found ideas when they happened | Pass (Editor) | Editor: *Away 3 d* with its rows and *See the report* |
| 33 | Regression | Settings → Text size **Large** and back to **Normal** | The advisor panel, the Codex card and the school's sheet wrap and stay readable | Pass (Editor) | Editor (`s17-check-c33a`, `-c23`): the school's sheet and Ramdeen's panel wrap at Large; set back to Normal |
| 34 | Regression | Settings → **Reduced motion** on | The Codex toast just appears and goes; no slide | | Not run in the Editor: check on the phone (36) |
| 35 | Device | Text size Large on the phone | As 33 | | |
| 36 | Device | iOS Reduce Motion on | As 34 | | |
| 37 | Device | Rotate or lock the phone | Landscape only (ADR 0005) | | |
| 38 | Frame time | Pinch out so the whole village and farm show, with the school open and shops. Developer panel → **Measure frame time (20 s)**; don't touch the screen | Main thread p95 at or under 8.3 ms, GPU p95 at or under 11 ms (ADR 0094), about 60 fps, under 1% dropped. Write both p95s in Notes | | |
| 39 | Notifications | Close the app with the school being built; come back after it opens | A notification "The school is open" (and others as before), never at night | | |
| 40 | Regression | Play your normal morning on the phone | Everything works as in Sprint 16 | | |
| 41 | Restore | **Restore** | Your town is back as it was at the Snapshot | | |

## Report

- **Blockers:** anything that stops play, or anything wrong with your town after the update. Mark it **BLOCKER** and say what you did.
- **Screenshots:** for checks 1, 2, 4, 5, 7, 12, 15, 17, 18, 20, 23, 25 and 29.
- **Numbers:** both frame-time p95s from check 38.
- **Answers:** to the five questions above.
