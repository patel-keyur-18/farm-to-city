# Sprint 19 build: the Phase 2 playtest (Release build) from a saved Hamlet town

- **Build:** a **Release build** (no developer panel) from the commit named in `STATUS.md`, made after you've seen the playtest town (US-237). It has Sprint 18's screens. Part A of the Phase 2 gate is judged on this (ADR 0101 §9).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / dates:** ____ to ____
- **How to install:** see `how-to-build-ios.md`. **Quit Xcode, and delete `unity/FarmToCity/Builds/iOS-release` before building** (the macOS 27 crash).
- **Build made:** 2026-11-12, Release, from commit `9974013` (Xcode unsigned compile BUILD SUCCEEDED), in `unity/FarmToCity/Builds/iOS-release`. Release and Development are the same app (`com.keyurpatel.farmtocity`), so installing over keeps your town.
- **The playtest town:** `FarmToCity-playtest.ftc`, written on the Mac by `dotnet run --project tools/Tools.Balancer -- playtest-save <seed> <path> <commit>`: the Village bot (credit, homes first) plays a new game to the close after its third household (about day 14): Hamlet, 3 of 5 households with a home loan, a hand, the cattle, no business, no school. Its time stamp is when Claude wrote it, so **import it the same day**: the farm doesn't wait for you for more than 72 hours (the away cap) but a card of what happened will show.

## Before you start: keep your own town safe

1. In the app, Menu → Settings → **Export town**. A copy of **your own town** goes to the Files app (On My iPhone → Farm to City). **Keep it.** The playtest town **replaces** the town in the app when you import it.
2. Install the Release build over the old app. **Don't delete the app first.**
3. Get the playtest file onto the phone (AirDrop it from the Mac, then Save to Files → On My iPhone → Farm to City).
4. In the app: Menu → Settings → **Import a town** → `FarmToCity-playtest`. **Settings → Play log: On** (the log is the evidence for the gate).
5. At the end (the Village, or 3 weeks): Settings → **Export play log**, then Settings → **Import a town** → your own export from step 1 to get your own town back.

## The smoke list (Release build, ADR 0047)

| # | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- |
| S1 | Launch; hold the time card for 3 seconds | No developer panel (nothing opens) | | |
| S2 | Settings → **Import a town** → the playtest file | The town opens at about day 14 of the Hamlet; a card of what happened if some hours passed | | |
| S3 | Settings → **Export town**, then Import it back | The same town | | |
| S4 | Settings → **Play log** | On; "n entries" counts up as you play | | |
| S5 | Money and loans, the season page, Your wealth, the Savings sheet, Menu → Your land | Each opens and reads sensibly | | |
| S6 | Buy something that lasts | A card with its numbers; *Not now* changes nothing | | |
| S7 | Close the app and come back | The same town; the away card only if you were away 8 hours or more | | |

## The playtest

Play as you would: a few short sessions a day. Aim for the Village within about 2–2.5 weeks from this town (3 weeks at most). The Village needs 5 households, a business with 2 profitable seasons and the school open (Menu → Dadi's letter → *Becoming a Village*).

**A diary line a day** (a line is enough; "didn't open" is a line too):

| Day | Opened? | Why (or why not) | What did you do? | Anything confusing or wrong? |
| --- | --- | --- | --- | --- |
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

**At three season closes**, before you read the *Why your cash moved* rows, write in one sentence **why your cash moved differently from your profit this season**, from the screens alone (the season page, the cash flow, Your wealth). Then compare: it passes if your biggest reason is the biggest row. Once, after a Winter close, write what *Your year* told you.

| Close | Profit | Cash moved by | Your one sentence | The screen's biggest row | Match? |
| --- | --- | --- | --- | --- | --- |
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |

**Your year said:** ____

## What counts as a blocker

A crash or freeze; a lost, refused or unreadable save; a wrong money amount; **a statement that doesn't add up; a cash movement you weren't shown; savings taken without your yes**; a notification in quiet hours or with wrong text; a number with no *Why?*. Mark it **BLOCKER** and say what you did.

## The gate, part A (Claude fills in at US-239)

| # | Check | Result | Evidence |
| --- | --- | --- | --- |
| A1 | The bots' pace: homes first 28–40 days with credit, any order up to 50; saving 50–64, any order up to 80 | | `GateTests.Village` |
| A2 | You reach the Village from the day-14 town within 3 weeks, or the log and diary show why | | |
| A3 | A session every day; the diary's reason is not only "testing" on most days | | |
| A4 | No blocker | | |
| A5 | At three closes, your sentence matches the biggest row; once, what *Your year* told you | | |
| A6 | The Release smoke list, both frame-time p95s (from the Sprint 18 Development build), golden replays on the phone, CI green | | |
