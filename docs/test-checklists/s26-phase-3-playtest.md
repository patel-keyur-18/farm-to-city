# Sprint 26 build: the Phase 3 playtest (Release build) from a saved Village town

- **Build:** a **Release build** (no developer panel) from the commit named in `STATUS.md`, made after you saw the playtest town (US-249, ADR 0169). It has Sprint 26's screens (the Chronicle, the Almanac, citizen stories, your kirana, supervisors) and the game clock matched to your phone (ADR 0170). Phase 3's playtest (US-250) is judged on this.
- **Device:** iPhone 13, iOS version: ____
- **Tested by / dates:** ____ to ____
- **How to install:** see `how-to-build-ios.md`. **Quit Xcode, and delete `unity/FarmToCity/Builds/iOS-release` before building** (the macOS 27 crash). Release and Development are the same app (`com.keyurpatel.farmtocity`), so installing over keeps your town.
- **The playtest town:** `FarmToCity-playtest.ftc`, written on the Mac by `dotnet run --project tools/Tools.Balancer -- playtest-save <seed> <path> <commit> village`: the Village bot (credit, homes first) plays a new game to the close after the Village unlock (seed 1: day 44, year 3 Winter, 6 households in your houses, one business with 7 profitable seasons, the school open, net worth 17,40,598, debt to assets 8%). Its clock is matched to the Mac's time of day and stamped with when Claude wrote it, so **import it the same day**: the farm doesn't wait for you for more than 72 hours (the away cap) but a card of what happened will show.
- **How long it takes:** the bots reach the Township at about game day 102 (median; the 90th percentile 144). A game day is a real day, so from day 44 that is **about 8 weeks at the bots' pace, and the 4-week playtest ends near day 72**: you will most likely be building toward the Township, not living in it, at the end of the 4 weeks. Report how far you got and what held you back.

## Before you start: keep your own town safe

1. In the app, Menu → Settings → **Export town**. A copy of **your own town** goes to the Files app (On My iPhone → Farm to City). **Keep it.** The playtest town **replaces** the town in the app when you import it.
2. Install the Release build over the old app. **Don't delete the app first.**
3. Get the playtest file onto the phone (AirDrop it from the Mac, then Save to Files → On My iPhone → Farm to City).
4. In the app: Menu → Settings → **Import a town** → `FarmToCity-playtest`. **Settings → Play log: On** (the log is the evidence for the gate).
5. At the end: Settings → **Export play log**, then Settings → **Import a town** → your own export from step 1 to get your own town back.

## The smoke list (Release build, ADR 0047)

| # | What to try | Expected result | Device only | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| S1 | Launch; hold the time card for 3 seconds | No developer panel (nothing opens) | | | |
| S2 | Settings → **Import a town** → the playtest file | The town opens at about day 44 of the Village; the game clock reads the phone's time within a minute | | | |
| S3 | Settings → **Export town**, then Import it back | The same town | | | |
| S4 | Settings → **Play log** | On; "n entries" counts up as you play | | | |
| S5 | Menu → **Chronicle** and its **?** | A Timeline that begins at the load and the eras the town has reached | | | |
| S6 | Menu → Dadi's letter → *Becoming a Township* | Lists what the Township needs, with what you have | | | |
| S7 | Money and loans, the season page, Your wealth, Menu → Your land | Each opens and reads sensibly | | | |
| S8 | Close the app and come back | The same town; the away card only if you were away 8 hours or more | | | |
| S9 | Pan and zoom the village | Smooth | **Device only** | | |
| S10 | Tap targets on the cards and the toast | Easy to hit with a thumb | **Device only** | | |

## The playtest

Play as you would: a few short sessions a day. Aim for the Township (20 households, per `content/`, see the Dadi's letter for the live gate) and note what you build on the way.

**A diary line a day** (a line is enough; "didn't open" is a line too):

| Day | Opened? | Why (or why not) | What did you do? | Anything confusing or wrong? |
| --- | --- | --- | --- | --- |
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

**At three season closes**, before you read the *Why your cash moved* rows, write in one sentence **why your cash moved differently from your profit this season**, from the screens alone. Once, after a Winter close, write what the **Almanac** told you.

| Close | Profit | Cash moved by | Your one sentence | The screen's biggest row | Match? |
| --- | --- | --- | --- | --- | --- |
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |

**The Almanac said:** ____

## What counts as a blocker

A crash or freeze; a lost, refused or unreadable save; a wrong money amount; a statement that doesn't add up; a cash movement you weren't shown; savings taken without your yes; a notification in quiet hours or with wrong text; a number with no *Why?*; a town that stops growing (never stuck is a rule). Mark it **BLOCKER** and say what you did.
