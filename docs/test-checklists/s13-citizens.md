# Sprint 13 build: your people (citizens, family houses, households, happiness)

- **Build:** the head of branch `sprint/13-citizens` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build**, so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).

## Before you install: keep your town safe

1. In the **old** app: Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 8 to 9): your fields, cash, hand, hut, orders, loans, trees and animals stay as they were. Your hand becomes one of **your people**: a citizen with an age (22 to 45), living alone in the hut. The first time it loads, the app also puts your town as it was before the update in the Files app as `FarmToCity-before-update-schema8.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Both copies can bring it back.

## What this build changes

- **Your people (spec 014).** Everyone living on your land is a citizen with an age, a home, a job and a family. People age one year each season; a hand retires at 60 and goes home, which frees the hut.
- **Family houses (ADR 0063, 0067).** From the Hamlet, build a family house (about 60,000 coins, ready in 2 days) on an empty field or a free place in the labour quarters, 2 to an acre, sharing a wall. A family in it pays rent (about 6,000) each season. An acre with houses is never sown and can't be sold.
- **Households.** Each family has a season's budget: food first (Engel's law: the poorer the family, the bigger the food share), then rent, clothing, health, schooling, leisure and savings. Adults without a job with you work outside on about half the days, and when you hire labourers, your own town's people come first.
- **Happiness.** Each family's needs in four layers (food and a home, a safe living, belonging, getting on), each row saying what would help or that it can't be changed yet.
- **On screen (US-210).** A *Your people* pill on the HUD (how many, how happy). The People sheet has **Workers** and **Town** tabs. A household's card shows a small family tree, a budget pie, savings, rent and needs; a person's card shows age, work, skill, health, story and close family with links.
- **Your town eats.** Your people's food adds to the local market's demand (each good's price **?** says how much).
- **Earlier this sprint (already on your phone):** fodder no longer keeps labourers hired (ADR 0066); the People sheet in the Store's layout.
- **Not yet (Sprint 14):** new families moving into empty houses, births, marriages, elders passing away, families leaving, and a hand asking to bring his family. An empty house in this build says "Waiting for a family" and stays empty.

**Your questions:** (1) Is it clear what a family house costs, earns and needs? (2) Do the household card's tree, pie and needs make sense at a glance? (3) Does the homestead (two houses sharing a wall) look natural on the map? (4) Is anything on the Town tab confusing?

## Checks

The developer panel: hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **Away cap (72 h)**, and **Snapshot** / **Restore**. Take a **Snapshot** before check 6: **Restore** puts your town back exactly as it was, so you can spend real coins on a house and get them back. Checks 6–11 need the Hamlet: before it, **Build a family house** is greyed with "Family houses come with the Hamlet" (check that instead and skip to 12).

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (US-105, AC-9) | Launch with your town | Same cash, fields, hand, hut, orders, animals as before. In the Files app: `FarmToCity-before-update-schema8.ftc` | | **Screenshot** |
| 2 | US-210 | Look at the HUD under the store | A pill with the people icon and a number (1 with one hand), and a smiley with the town's happiness (one line, not wrapped) | | **Screenshot** |
| 3 | US-210 | Tap the pill | People opens on the **Town** tab: "1 person in 1 household", Happiness and Work in town (each with **?**), a big **Build a family house** button, your hand's row ("Hut 1 · income … a season", Happiness …) | | **Screenshot** |
| 4 | US-210 | Tap **Workers**, then **Town** | The tabs switch in place (no back button appears) | | |
| 5 | US-210, T4 | Workers → your hand → **See** | A *Lives in · Hut 1 · alone* row; its **?** says the share of the wage sent home to the family's village (no "his" for a woman hand) | | |
| 6 | US-208, US-210 (Hamlet) | **Snapshot**, then Town → **Build a family house** | The map lights every acre a house can go on (empty fields you own and the quarters); a ghost house, a bar with **Build here** and the cost | | **Screenshot** |
| 7 | §3.13 | Tap a field with a crop growing | The ghost turns red, "Something is growing here", **Build here** greyed | | |
| 8 | US-208 | Tap a lit empty acre, **Build here** | Coins go; a house being built (brick walls, scaffolding) at the back of the acre; the acre turns to village ground with a haystack, a charpai and a shade tree | | **Screenshot** |
| 9 | US-208 | Tap an empty owned field (no house) | Its sheet has **Build a family house** with the cost beside Sow | | |
| 10 | US-212 | **+1 day** twice | The house is finished: a small pucca house with a verandah, a water tank on the roof and a courtyard | | **Screenshot** |
| 11 | US-210 | Tap the house acre | *Family houses*: "Family house 1 · Empty · Waiting for a family", "Room for 1 more house", **Build a family house**; no **Sow** | | |
| 12 | US-210 | Town tab, tap your hand's row **See** | The household card: who lives here, income, the budget pie with a legend (shares add to about 100%), savings, rent ("None: a hand's hut is part of the pay"), happiness and each need with what would help | | **Screenshot** |
| 13 | US-210 | On the card, tap the **?** on Budget | "Engel's law: …" first, then each share in coins | | |
| 14 | US-210 | Tap the hand's name in **Who lives here** | The person's card: portrait, age and stage (its **?**: "People age a year each season"), Lives in, Work, From, skill, traits, health, schooling, their story | | **Screenshot** |
| 15 | US-209 | Store → a good (wheat) → the market price **?** | A row "Your town buys about this much a day" | | |
| 16 | US-214 | With only fodder growing, People → **Hire labourers** | "Nothing to harvest · Labourers come for a harvest. Your fodder is cut each morning with the animals' work." | | |
| 17 | US-106, US-210 | **Away cap (72 h)** past a season close | The away card; your hand's season paid; if a hand turned 60: "… retired and went home". If a family house had a household: "Rent from 1 family" among the money rows | | **Screenshot** |
| 18 | | **Restore** | Your town is back as at the Snapshot (no house, the coins back) | | |
| 19 | Frame time | Pinch out so the whole map shows. Developer panel → **Measure frame time (20 s)**, don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write the numbers in Notes | | **Numbers** |
| 20 | Regression | Play your normal morning: orders, sow, harvest, sell, the shed, your hand's orders | Everything works as in Sprint 12 | | |

## Report

- Anything that blocks play, or anything wrong with your town after the update: **BLOCKER** plus what you did.
- Screenshots for checks 1, 2, 3, 6, 8, 10, 12, 14 and 17; the numbers for check 19.
- Answers to the four questions above.
