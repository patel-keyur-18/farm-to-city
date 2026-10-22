# Sprint 11 follow-up build: the Orders sheet, the Store and a hand's order (ADR 0059)

- **Build:** the head of branch `sprint/11-phase2-planning` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build**, so the developer panel exists. **Install over the Sprint 11 app; don't delete it first**, or your town is lost. The save format is unchanged (schema 6): your town loads as it is.

**What this build changes** (the Sprint 11 device review, ADR 0059). Nothing else changed: the rules, prices and balance are as in the Sprint 11 build you checked.
- **The supermarket's Orders sheet:** one row per order. Its state is on the same row (when to sow, what's fresh with Deliver, how much is delivered), and a filled order shows a green **Delivered** tag.
- **The Store:** how full it is; three picture buttons (Supermarket orders with a red count, Buy inputs, Mandi trip); what the supermarket owes; **Sell to the trader**; **Seed and inputs**.
- **A hand's field order** starts on your sow sheet: the fertiliser choice (Normal, Compost, Chemical) and every crop's figures, with **Choose**.

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**.

| # | Story | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-196 | After 6:00, tap **Orders** | Three rows, one per order: the crop, "Wants … at … · the trader pays …", and under it in bold "Sow by HH:MM to harvest it today" or "Pick it from the garden today". No second row per order. The subtitle: "Harvest today, deliver by midnight, paid at 6:00 tomorrow" | | **Screenshot** |
| 2 | US-196 | Harvest an ordered crop today, open Orders | In green: "… harvested today, ready to deliver", with **Deliver** on the same row | | |
| 3 | US-196 | Deliver less than the order wants (a garden bed for coriander or spinach) | The row says "… · 1.5 kg of 4.5 kg delivered"; "Supermarket pays you … at 6:00 tomorrow" appears under the orders | | |
| 4 | US-196 | Deliver the whole of an order | The row says "All … delivered" in green with a green **Delivered** tag; the HUD's Orders count drops by one | | **Screenshot** |
| 5 | US-196 | **+1 hour** past midnight, open Orders | An undelivered order says "Missed: deliveries closed at midnight" in orange | | |
| 6 | US-197 | Open the Store | Top to bottom: "In the store"; three picture buttons (Supermarket orders with a red count, Buy inputs, Mandi trip); "Supermarket pays you" if owed; **Sell to the trader** (the truck, then a row per crop with Sell); **Seed and inputs** | | **Screenshot** |
| 7 | US-197 | Tap each picture button, then the back arrow | Each opens its sheet (Orders, Buy inputs, Mandi trip); the back arrow returns to the Store | | |
| 8 | US-197 | Open the Store after midnight | The orders button says "Closed till 6:00" with no count; "Back at 6:00", "Stall closed till 6:00", Sell greyed with its reason | | |
| 9 | US-198 | Tap an empty field, then **Give order** | Your sow sheet: "Fertiliser {hand} uses" with Normal, Compost and Chemical; "Let {hand} choose · Best here now: …"; every crop card with its harvest, spend, sells for, profit, inputs and work, soil before → after, and **Choose** | | **Screenshot** |
| 10 | US-198 | Tap **Compost** | Every card's figures change (spend includes compost; more harvest); "Best here now" may change | | |
| 11 | US-198 | Scroll to an out-of-season crop | It's faded with "Sown in …; {hand} picks the best crop in between", and still has **Choose** | | |
| 12 | US-198 | **Choose** a crop, then **Every season**, then a way to sell | The subtitle says "Keep Potato with compost · Every season". The hand sows at once with compost (your cash falls by about the card's Spend); the field sheet's order says "Keep Potato with compost · …" | | |
| 13 | US-198 | On that field, tap **Change** | The sheet opens on **Compost**, with "The order now" and **Remove the order** | | |
| 14 | Regression | Play your normal morning: sow by yourself (the sow sheet), sell, deliver, your hand's orders | Everything works as in the Sprint 11 build | | |

## Report

- Anything that blocks play: **BLOCKER** plus what you did.
- Screenshots for checks 1, 4, 6 and 9.
