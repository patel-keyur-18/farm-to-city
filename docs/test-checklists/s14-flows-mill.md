# Sprint 14 build: families come and go, the home loan, the flour mill

- **Build:** the head of branch `sprint/14-flows-and-mill` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build**, so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).

## Before you install: keep your town safe

1. In the **old** app: Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 9 to 10): your fields, cash, people, houses, orders, loans, trees and animals stay as they were. You have no business yet. The first time it loads, the app also puts your town as it was before the update in the Files app as `FarmToCity-before-update-schema9.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Both copies can bring it back.

## What this build changes

- **Families come and go (spec 014, US-109–US-111).** Empty family houses fill: a family of 2–5 arrives within about a day. Babies are born, sons marry and their bride moves in (into a free house if there is one), daughters marry into another village, and old people pass away gently. A family that is unhappy for two seasons, or two seasons behind on rent, may leave.
- **A hand's family (US-110).** A hand living alone may ask to bring his family into a free house: **Yes** moves them in, and the house becomes part of his pay (housing in kind, no rent). The offer lapses after about 18 hours.
- **The family tree (US-111).** Brides show where they came from ("Usha (35, from Rampur)"); a hand living alone shows where his family lives.
- **The home loan (US-211, ADR 0071).** Build a family house **With a home loan**: the bank counts the house at 75% of its value.
- **"Lives in" (US-217):** someone living alone says *alone*.
- **The flour mill (spec 016, US-115–US-117; ADR 0072, 0073).** From the Hamlet: Menu → **Businesses** → Start a flour mill → market research → the plan (who runs it, how you pay for it: your cash or a business loan, the season ahead, your hours if you run it) → choose an empty acre you own → it's built in a day. It grinds the area's grain for a fee and mills your wheat into atta and bran. Its card has **Today**, **Books** (profit and loss, cash in and out, what it owns and owes, past seasons, plan vs actual after 4 full seasons) and **Settings** (hours, fee, your wheat a day, buying wheat at the market, put cash in, take cash out, close it). Short of cash, it uses its overdraft; past the limit it stops buying wheat and an unpaid worker leaves, with an alert.

**Your questions:** (1) Is it clear what the mill costs and earns before you build it, hired or run by you? (2) Do the Books tab's three statements make sense at a glance? (3) Is it clear what to do when the mill is short of cash? (4) Does the mill look right on its acre?

## Checks

The developer panel: hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**, and **Snapshot** / **Restore**. Take a **Snapshot** before check 9: **Restore** puts your town back exactly as it was, so you can spend real coins on a mill and get them back. Checks 9–25 need the Hamlet: before it, *Start a flour mill* is greyed with "Comes with the Hamlet" (check that and skip to 26).

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (spec 016 AC-10) | Launch with your town | Same cash, fields, people, houses, orders, animals as before. In the Files app: `FarmToCity-before-update-schema9.ftc` | | **Screenshot** |
| 2 | US-217 | People → Town → a hand living alone → his card | *Lives in · Hut 1 · alone* | | |
| 3 | US-110 | If an empty house stands: **+1 hour** a few times | A family arrives (an away row or the Town tab shows them); their household card fills | | |
| 4 | US-110 | With a hand alone and a free house: wait, or **+1 hour** | A card: "… would like to bring his family" with **Yes** and **Not now**; the bell has it too | | **Screenshot** |
| 5 | US-110 | **Yes** | His wife and children move into the house; his card's wage row's **?** shows "housing in kind"; no rent from that house | | |
| 6 | US-109 | **+1 season** twice | The away card or the Town tab's *This season*: a birth, a marriage or a passing, each in words | | |
| 7 | US-111 | A married son's household → a person's card | The bride's row says where she came from ("…, from Rampur") | | |
| 8 | US-211 | Town → **Build a family house** → **With a home loan** | The home loan sheet: the loan, its payments, **Build**; the house is built with the loan on Money and loans | | **Screenshot** |
| 9 | US-115 | **Snapshot**, then Menu → **Businesses** | "Start a flour mill" with the building and machine's cost; *Your businesses: None yet* | | **Screenshot** |
| 10 | US-115 | **Start a flour mill** | Market research: the area's grain a day (about 3,200 kg), the next village's chakki's fee, atta and bran at the trader, a miller's wage, your wheat, the cost; each with **?** | | |
| 11 | US-115, US-117 | **Make a plan**; **Change** *How you pay for it* | A business loan of about 62,000; *Cash to put in* drops; *Interest on the loan* appears | | **Screenshot** |
| 12 | US-117 | **Change** *Who runs it* to you | *Your hours there: 8 h a day: about … a season as day labour*; no miller's wage; a higher profit | | |
| 13 | US-115 | **Choose the acre** | Empty acres you own light up; a ghost mill; the bar's **Build here** shows what you put in | | |
| 14 | US-115 | Tap a field with a crop | "Something is growing here", **Build here** greyed | | |
| 15 | US-115 | Tap a lit acre, **Build here** | The mill being built on the acre (its building stage) | | **Screenshot** |
| 16 | US-115 | **+1 day**, then tap the mill (during 8:00–18:00) | The shed with the chakki and people waiting with grain; its card: *Open now*, the area ground and its fees, your wheat milled, atta and bran, who works here, the mill's cash, the overdraft, the loan | | **Screenshot** |
| 17 | US-115 | **Settings** → **Change** the hours, then the fee | 8:00–14:00, then a fee of 3; the card stays open | | |
| 18 | US-116 | **Books** | Profit and loss this season so far, cash this season, what it owns and owes; **?** on every line; *Plan vs actual: 0 of 4 so far* | | **Screenshot** |
| 19 | US-116 | **+1 season**, **Books** | A *Past seasons* row; **See report** opens that season's three statements | | |
| 20 | US-116 | **Settings** → **Take out** | Amounts to take, "Your capital back"; the subtitle says what to keep for the next wage. Take one: back on Books, your cash up by that much | | |
| 21 | US-116 | Money and loans | *Of that, your businesses* in net worth; the mill's loan listed as "…, for the flour mill" | | |
| 22 | US-117 | Settings → fee and hours to their lowest, take out all its cash, **+1 season** | If it couldn't pay its worker: the bell, "The flour mill has no worker: the wage went unpaid"; the card's red note | | **Screenshot** |
| 23 | US-117 | **Put in** 50,000 | The overdraft is paid off; a worker again | | |
| 24 | US-117 | Settings → **Close the flour mill** → **See what it means** | Its cash, stock, the building and machine at 60%, debts, and what comes to you or becomes your loan, each with **?** | | **Screenshot** |
| 25 | US-117 | **Close the flour mill**, then **Restore** | Closing: the acre is empty, *None open*; Restore: your town is back as at the Snapshot | | |
| 26 | Frame time | Pinch out so the whole map shows. Developer panel → **Measure frame time (20 s)**, don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write the numbers in Notes | | **Numbers** |
| 27 | Regression | Play your normal morning: orders, sow, harvest, sell, the shed, your people | Everything works as in Sprint 13 | | |

## Report

- Anything that blocks play, or anything wrong with your town after the update: **BLOCKER** plus what you did.
- Screenshots for checks 1, 4, 8, 9, 11, 15, 16, 18, 22 and 24; the numbers for check 26.
- Answers to the four questions above.
