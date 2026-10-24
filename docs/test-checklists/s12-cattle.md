# Sprint 12 build: cattle, the village redrawn, a living valley

- **Build:** the head of branch `sprint/12-cattle-citizens` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build**, so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash).

## Before you install: keep your town safe

1. In the **old** app: Menu → Settings → **Export**. A copy of your town goes to the Files app (On My iPhone → Farm to City). Keep it until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 6 to 8): your fields, cash, hand, hut, orders, loans and trees stay as they were. You also inherit one more acre beside the yard, the cattle acre (rough, sandy ground that grows nothing). The first time it loads, the app also puts your town as it was before the update in the Files app as `FarmToCity-before-update-schema6.ftc`.

If anything looks wrong with your town after the update, stop and report it before playing on. Both copies can bring it back.

## What this build changes

- **Cattle (spec 012).** Once you're a Hamlet, tap the cattle acre beside your yard: build a shed (about 41,600 coins, ready in a day), then buy a buffalo or a cow at the fair. Every morning at 6:00 the dairy co-op collects the milk and pays by its fat. Animals eat green fodder (some from the commons, the rest from your fodder fields or bought from neighbours), straw and cattle feed. Grow berseem in Winter and fodder sorghum in Summer. Your hand can keep the straw and feed stocked. No animal dies.
- **The village redrawn (ADR 0061, 0065).** A main street and a side lane; every building faces its street; the dairy co-op on the side lane.
- **A living valley (ADR 0062).** Neighbours' land follows its soil (scrub with babul trees on sandy land, mustard in Winter, sugarcane, mango groves, tube-wells), villagers' farmhouses between the fields, and a hedge with corner stones round all your land.
- **Fixed:** buying land froze the map until the app restarted.

**Your questions:** (1) Is it clear what to do first with cattle, and why a buffalo pays? (2) Do the shed's numbers make sense (milk, feed, payback)? (3) Does the valley look natural, not cramped? (4) Is it clear at a glance which land is yours?

## Checks

The developer panel: hold the time card for 3 seconds. It has **+1 hour**, **+1 day**, **Away cap (72 h)**, and **Snapshot** / **Restore**. Take a **Snapshot** before the cattle checks: **Restore** puts your town back exactly as it was, so you can spend real coins on a shed and a buffalo to try them and get the coins back.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Migration (ADR 0064) | Launch with your town | Same cash, fields, hand, hut, orders as before. A new acre beside the yard with a hedge and a compost pit. In the Files app: `FarmToCity-before-update-schema6.ftc` | | **Screenshot** |
| 2 | US-207 | Pinch out to the whole map | Neighbours' land varies: scrub on sandy acres, ploughed and sown fields, a few farmhouses among the fields. Your land has a hedge round it with white-and-red corner stones | | **Screenshot** |
| 3 | US-206 | Pan to the village; tap the council hall, the grocer, the temple, the dairy co-op | Each opens its own sheet. Buildings face their street; nothing overlaps | | **Screenshot** |
| 4 | US-206 | Tap the dairy co-op | Pictured rows for buffalo and cow milk (fat and price a litre, with **?**), and a big **Your cattle yard** button like the Store's | | |
| 5 | US-213 | Tap the cattle acre | Before the Hamlet: "Your cattle yard", why it's there, the compost pit. After: what a shed costs and holds, and **Build the shed** | | |
| 6 | Spec 012 §3.13 | Menu → Becoming a Hamlet | Lists "A cattle shed: buffalo and cows" (under "When you're a Hamlet" or "Now open to you") | | |
| 7 | US-205 (Hamlet) | The first time you open the cattle acre as a Hamlet | Dadi's letter page "Your cattle yard" (once) | | |
| 8 | US-205 | **Snapshot**, then **Build the shed** | Coins go; "Your family is building the shed · Ready in 23 h 59 min". **+1 day**: the shed, trough, mangers, straw stack and neem appear on the acre | | **Screenshot** |
| 9 | US-205 | Open the shed: **Buy a buffalo** | A card: price, milk a day, fat and price a litre, what she eats, feed cost bought vs your own, work, "Pays for herself in about 43 days on bought feed · 21 days on your own". **Buy her** | | **Screenshot** |
| 10 | US-205, §3.7 | Right after buying, look at the bell | An alert "Straw for 0 days more" with **Fill**. **Fill** opens Fill it up / Enough for 2 days / 4 days | | |
| 11 | US-205 | Fill straw and feed for 4 days. On the shed, **Ask** your hand | The rows show "… of 10,000 kg · 4 days"; "Keeps straw and feed stocked: (your hand), for 3 days at a time" | | |
| 12 | US-205, §3.5 | **+1 day** past 6:00, open the shed | "Milk this morning: … L · the co-op paid … at 6:00". Her row: "In milk, day 2 of 13 · … L this morning". Tap her: her card; the **?** on milk explains every factor | | **Screenshot** |
| 13 | US-205 | On her card, **Sell at the fair** | It asks first. Keep her (or sell and check the coins) | | |
| 14 | US-205, §3.8b | Sow on an empty field: scroll to Berseem / Fodder sorghum | Out of season greyed with its season; in season "About … kg of green fodder a day", "Feeds about 6 buffalo" and what it saves | | |
| 15 | US-205 | Sow fodder sorghum in Summer; **+1 day**; tap the field | "Fodder sorghum for your animals": cut every morning, eaten and sold standing, cut till the end of Monsoon, **Your cattle shed**, **Clear field** | | |
| 16 | US-205, §7 | **Away cap (72 h)** | The away card lists the co-op's milk money first, spare fodder sold, green fodder bought | | **Screenshot** |
| 17 | US-205 | Tap Care's **?** on a field, and net worth's **?** in Money | Care shows "Of that, your animals (1)"; net worth has an **Animals** row | | |
| 18 | US-205 | **Restore** | Your town is back as at the Snapshot | | |
| 19 | Fix (US-207) | Buy a parcel next to your land (or after **Snapshot**), then sell it | The map keeps working; the hedge moves round the new land, then back | | |
| 20 | Frame time | Pinch out so the whole map shows. Developer panel → **Measure frame time (20 s)**, don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. Write the numbers in Notes | | **Numbers** |
| 21 | Regression | Play your normal morning: orders, sow, harvest, sell, your hand's orders | Everything works as in Sprint 11 | | |

## Report

- Anything that blocks play, or anything wrong with your town after the update: **BLOCKER** plus what you did.
- Screenshots for checks 1, 2, 3, 8, 9, 12 and 16; the numbers for check 20.
