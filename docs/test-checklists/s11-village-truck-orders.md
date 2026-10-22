# Sprint 11 build: the village map, the trader's truck, supermarket orders

- **Build:** the head of branch `sprint/11-phase2-planning` (the commit is in the pull request)
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. **A Development build** (`--options '["Development"]'`), so the developer panel exists. Delete `unity/FarmToCity/Builds/iOS` before building (the macOS 27 crash). **Install over the old app; don't delete it first**, or your town is lost.
- **Your town:** this build moves saves from schema 4 to schema 6 (the 16 × 12 map, then the truck and orders). **Your Sprint 10 town loads** with its cash, fields, hand, orders, loans and trees. If you owned the acre the farm lane now runs over (`s5`), a card says it was bought back at twice its price, crop and trees included.

**What this build changes:**
- **The village (spec 010, ADR 0052–0053).** The map grows to 16 × 12 acres: your 8 × 6 farm in the middle, the village to the east, the commons and the river to the north, roads, neighbours' fields (some for sale, with signs), and the valley past the edge.
- **The trader's truck (spec 011, ADR 0054).** It comes at 6:00 along the district road and up the farm lane, parks by the labour quarters, and leaves at midnight or when its 4 t load is full. While it's away you can't sell to the trader: "The trader's truck is back at 6:00". Your stall is closed from midnight to 6:00 too. Its price is set each morning and holds all day; prices move a little each day.
- **Supermarket orders (ADR 0055, ADR 0058).** From the morning after your first day, a city supermarket posts two crop orders and a small spinach or coriander order at 6:00. They pay 15–30% over the valley's price for produce **harvested today**, delivered by midnight, and pay you at 6:00 the next morning. An order is half to one acre's harvest. Missing one costs nothing.
- **Rice and potato grow in 15 hours** (they took 24). A season's harvest per acre is the same, in more, smaller sowings.
- **Manoj fills orders first**, then sells the rest as his order says. What he can't sell while the truck is away, he keeps and sells to the truck at 6:00.

**Your questions:** (1) does the morning routine (check orders, sow by 9:00, deliver in the evening) feel worth coming back for? (2) is it clear why you can't sell at night? (3) do the order sizes feel right for your farm? (4) does the village feel alive, and is the map easy to move around?

## Checks

Use the developer panel (hold the time card for 3 seconds) for time: **+1 hour**, **+1 day**, **+1 season**, **Away cap (72 h)**.

| # | Story / AC | What to try | Expected result | Pass / Fail | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | US-189, spec 010 AC-6 | Launch with your Sprint 10 town | It loads with the same cash, fields, hand and orders. The camera opens on your yard. If you owned the lane acre, a card says what you were paid | | **Screenshot** |
| 2 | US-190, AC-9 | Pinch out all the way, then pan to each corner | The whole map fits; past the edge is the valley (fields, hills, scrub), never sky or blank | | **Screenshot** |
| 3 | US-190, AC-11 | Tap the chemist, the dealer, a house and the temple | The chemist offers medicine, the dealer opens Buy inputs, the others a short description | | |
| 4 | US-188, AC-4 | Tap a neighbour's field with a sign, then one without, then a road | The sign's field opens the land sheet with a price; the other says it isn't for sale yet; the road says it's the village's | | |
| 5 | US-190.T6 | Pinch out so the whole map shows. Developer panel → **Measure frame time (20 s)**, don't touch the screen | Main thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped (ADR 0026, 0035). Write the numbers in Notes | | **Numbers** |
| 6 | US-192 | Between 6:00 and midnight, look at the lane south of the labour quarters | The truck is parked there. Tap it: the Store opens with "Trader's truck · Here till midnight · … room" | | **Screenshot** |
| 7 | US-192, AC-2 | **+1 hour** until after midnight, watching the truck | At midnight it drives off down the lane and along the road east. Open the Store: "Back at 6:00", "Stall closed till 6:00", Sell greyed with "The trader's truck is back at 6:00." | | |
| 8 | ADR 0058 | Close the Store after check 7 | A letter page, "The trader's truck", explains the hours (once only) | | |
| 9 | US-192, AC-1 | **+1 hour** to 6:00 | The truck drives in from the east along the road and up the lane | | |
| 10 | US-193, AC-7 | At 6:00 | A toast "New supermarket orders"; the **Orders** button (a paper bag, left of People) shows a badge. The first time, a letter page "A buyer in the city" | | **Screenshot** |
| 11 | US-195 | Tap **Orders** | Two crop orders and a spinach or coriander order, each with its amount, price and the trader's price. "Nothing fresh yet · Sow by HH:MM". Potato and rice say "Sow by 08:59". The **?** on a price shows the morning price × the premium | | **Screenshot** |
| 12 | US-193, US-194 | Sow an ordered crop before its "sow by" time, wait for it to ripen (**+1 hour**), harvest it | The order shows "Fresh in store: … · Deliver". Deliver: the row shows what was delivered; "Supermarket pays you … at 6:00 tomorrow" appears on the Orders sheet, the Store and in Money. Cash doesn't change yet | | |
| 13 | US-193, AC-8 | Try to deliver produce harvested before 6:00 | It doesn't count: "Nothing fresh yet" | | |
| 14 | US-193, AC-9 | **+1 hour** past the next 6:00 | Coins fly to your cash: the supermarket paid. Yesterday's undelivered orders are gone; new ones posted | | |
| 15 | US-193, AC-10 | Give Manoj an order on a field whose crop is ordered today, with **Sell to the trader** | When he harvests, the order is filled first; only the rest goes to the trader | | |
| 16 | US-192, AC-5b | Let Manoj harvest at night with **Sell to the trader** (**+1 hour** steps after midnight), then pass 6:00 | The away card says "Your hand sold to the truck at 6:00" | | |
| 17 | US-195.T4 | Close the app for a few hours (or **Away cap**), come back | The away card lists what the truck bought, each order (delivered, part delivered, ran out) and the supermarket's payment | | **Screenshot** |
| 18 | US-195, AC-13 | With notifications on, close the app overnight | At 07:00 (after quiet hours): "3 supermarket orders: potato, radish and spinach" (no amounts) | | |
| 19 | US-192, §3.1b | Open a crop's sheet on two mornings | "The trader pays 5.62 /kg today (yesterday 5.30 /kg, ▲6%)". Its **?** has a "Today's market" row | | |
| 20 | US-194 | Sow potato and rice | They're ready about 15 hours later. The sow sheet shows the smaller harvest per sowing | | |
| 21 | Regression | Play your normal morning: collect, sell, sow, Manoj's orders, hire labourers | Everything works as in Sprint 10 | | |

## Report

- Anything that blocks play: **BLOCKER** plus what you did.
- Screenshots for checks 1, 2, 6, 10, 11 and 17; the numbers for check 5.
