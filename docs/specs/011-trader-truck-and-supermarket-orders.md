# 011. The trader's truck and supermarket orders

- Status: Approved (owner, 2026-10-21). All gaps closed 2026-10-21
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Land, farming and livestock (Storage and selling); Economy (Regional trader); Offline play (notifications)
- Amends: spec 004 §3.1 (the daily factor), §3.2 (the night pause), §3.3 (capacity resets at 6:00; the truck); spec 006 §3.8 (the hand delivers first, BestCrop); spec 005 §3.2 (account 1300 Owed by buyers); spec 007 §3.5 (a notification kind); spec 009 (the first sale at any hour, two chapters, the `comeBack` text); spec 003 §3.1 and content (rice and potato times)
- Related ADRs: 0054 (the trader's truck), 0055 (supermarket orders, rice and potato in 15 h), 0034 (a glut drains hour by hour), 0037 (the mandi trip), 0048 and 0050 (the hand's crop choice), 0053 (the roads)
- Depends on specs: 004 (market and store: amended here), 003 (crops: rice and potato times), 006 (the hand's orders: amended here), 007 (notifications), 010 (the roads and the truck stop)

## 1. Goal

Every morning the trader's truck pulls up at the farm and a city supermarket posts two orders, so the player plans the day: what to sow by breakfast, what to harvest fresh, and where each kilo goes.

**Lesson:** a buyer who pays more wants something in return (freshness, a set amount, a deadline), and a price fixed for the day is a known price.

## 2. Scope

**In:**
- The truck's day: 6:00 to midnight or till full, the reset at 6:00, the price held all day, the total load, "back at 6:00" while away.
- The stall's night pause (midnight to 6:00).
- Supermarket orders: generation at 6:00, fresh today, delivery, lapse at midnight, the hand delivering first and counting the order price.
- Rice and potato in 15 hours.
- The morning notification, the Orders sheet, the truck on the map, the away card lines.

**Out** (and where it goes instead):
- Forward contracts with the trader: Phase 3 (F16.2).
- Orders from village shops and businesses: specs 016 and 017.
- Orders for milk or dairy: spec 012 (the co-op buys milk).
- The highway link and a lower transport cost: Phase 3.
- Penalties, reputation with the supermarket, or a contract tier: not planned (ADR 0055: no penalty).

## 3. Rules and formulas

Money is `long` in 1/100 coin, quantities in grams, factors `Fixed` (ADR 0018). "Hour h" means the sim's hour of day, `(t mod 86 400) ÷ 3 600` (spec 001), which matches the device's local time.

### 3.1 The truck's day (ADR 0054)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Arrival | At the hourly pass at hour `TruckDef.arriveHour` (6), system Market, before the market clears: the per-crop counts `traderBoughtToday` reset to 0, the load `truckLoadToday` resets to 0, and the truck is **here**. Emits `TruckArrived` | 0–23 |
| Here | From arrival until it leaves. `TruckHere` is state (a bool) | |
| Leaving | When `truckLoadToday ≥ TruckDef.loadKg` after any purchase (emits `TruckLeft(Full)`), or at the hourly pass at hour `TruckDef.leaveHour` (0, midnight; emits `TruckLeft(Day)`), whichever is first | |
| The day's price | The truck buys at the **Floor** (spec 004 §3.1). P_regional changes only at a season start or an index step (both at midnight) and with the daily factor (§3.1b, at 6:00), so the Floor is constant from 6:00 to midnight: the truck's price is set at 6:00 and held all day without extra state | |
| Capacity | `TraderCapacityLeft(good) = TruckHere ? min(traderBuyPerDay(good) − traderBoughtToday(good), loadKg − truckLoadToday) : 0`. Every trader purchase goes through it: `SellToTrader`, harvest overflow (`SellRestToTrader`), the trader holding the floor on the stall (spec 004 §3.2), the hand's SELL TO TRADER, labourers' harvests. Every purchase adds to both counts | `loadKg` 4,000 kg (tuned so a normal day rarely fills it) |
| First sale | Until the first session's first-sale step is done (spec 009 step 5), `TraderCapacityLeft` treats the truck as here, so a first session played at night can sell (§10 G6). The load still counts | |
| Away | While the truck isn't here, `SellToTrader` is rejected with **`TraderAway`** ("Back at 6:00"). A harvest with `SellRestToTrader` whose rest doesn't fit the store is rejected with `TraderAway` and waits in the field, as with `TraderFull` (spec 003). The trader doesn't hold the floor on the stall | |
| The hand's held sales | When a hand's SELL TO TRADER order stores a harvest because the truck is away or full, the stored amount is added to `heldForTruck[good]`. At the arrival pass, right after the reset, he sells `min(heldForTruck, stock not on offer)` of each good (good id order) to the truck at the new day's price, up to its room, and the count drops by what he sold; what doesn't fit stays held for the next morning. If the player sells, eats or offers that stock first, `heldForTruck` is cut to what's left (`min(heldForTruck, stock not on offer)` after every removal). A night harvest is not fresh for that day's orders (§3.3), so it goes to the truck. Emits `HandSoldHeld(good, q, amount)` (§10 G5) | |
| Away while you're away | The truck runs the same live and away. The away card says what it bought ("The trader bought 1,200 kg of potato") | |

The trader's per-crop counts used to reset at the midnight daily pass (spec 004 §3.3). They now reset at the truck's arrival.

### 3.1b The daily price (amends spec 004 §3.1; §10 G1)

At the hourly pass at `arriveHour`, before the truck arrives, each produce good in id order draws its daily factor:

```
f ← f + MarketDef.dailyReversion × (1 − f) + GoodDef.dailyVolatility × (2u − 1)     u: seeded draw (stream DailyPrice, counter = day index × goods + good)
f ← clamp(f, 1 − GoodDef.dailyBand, 1 + GoodDef.dailyBand)
P_regional(good, t) = GoodDef.basePrice × PriceIndex × SeasonalFactor(good, season(t)) × f
```

- Produce only: inputs (seed, fertiliser, spray, saplings) keep their seasonal price, and price-controlled goods ignore it.
- Every price built on P_regional follows: the truck's Floor, the stall's market (it already rescales when P_regional changes, spec 004 §3.2), the Ceiling, order prices, the mandi, and stored produce in net worth.
- Placeholders: wheat, rice and lentils volatility 0.015, band 0.05; potato, radish, fruit and greens volatility 0.04, band 0.12; reversion 0.3. The mean of `f` is 1, so prices are neutral on average.
- The good's sheet shows today's change: "Potato 5.62 today (yesterday 5.30, ▲6%)", with a *Why?* ("Season price 6.80 × today's market +4%").

### 3.2 The stall's night pause (ADR 0054)

| Rule | Behaviour |
| --- | --- |
| Pause | At the hourly passes for the hours from `TruckDef.leaveHour` to `arriveHour` (the passes at 1:00 … 6:00, each clearing the hour that just ended), the founder's offer adds nothing to supply: `S_founder = 0`, so it sells nothing and the trader takes none of it. The offer stays in the store (and keeps spoiling) |
| The price overnight | The valley's market keeps clearing without the stall (spec 004 §3.2 with `S_founder = 0`), so a glutted price recovers towards P_regional overnight: about 80% of the gap by 6:00 at damping 0.25 (§10 G2) |
| Labels | The good's sheet and the Store show "Stall closed till 6:00" in those hours |

### 3.3 Supermarket orders (ADR 0055)

| Rule | Behaviour | Bounds |
| --- | --- | --- |
| Posting | At the hourly pass at `arriveHour` (after the truck arrives), any open orders lapse and `OrderDef.perDay` (2) new orders are drawn. Emits `OrdersPosted`. Orders start at the first `arriveHour` pass after the first session's last step (`comeBack`, spec 009) is done (§10 G6) | 0–4 a day |
| Eligible crops | The `perDay` crop orders: field crops (§10 G4) in season now whose `growthSeconds ≤ (24 − arriveHour) × 3 600` (18 h), so one sown at 6:00 can ripen by midnight. Rice and potato (15 h) sown before 9:00 can | |
| Drawing | Seeded stream `Orders`, counter = day index. Each order draws a crop from the eligible list (crop id order) without repeats while two or more are eligible; with one eligible, both orders may name it; with none, no orders that day | |
| A greens order | Also at posting, `OrderDef.greensPerDay` (1) small order for a kitchen-garden green (spinach or coriander), drawn the same way among the greens: `amount = round_down_to(greensLotKg, harvestKg × (greenPickingsMin + (greenPickingsMax − greenPickingsMin) × u))`, at least one lot: spinach 8–16 kg, coriander 3–6 kg. Priced like a crop order. Garden pickings count as fresh (a picking is a harvest) | 0–1 a day; 2–4 pickings |
| Amount | `amount = round_down_to(OrderDef.lotKg, BaseSowing(crop) × (OrderDef.sowingsMin + (sowingsMax − sowingsMin) × u))`, where `BaseSowing = seasonYieldPerAcre × cycleShare` (one acre's base harvest, spec 003), `u` a seeded draw, `lotKg` 10 kg, at least one lot | 1–2 sowings |
| Price | `orderPrice = P_regional(crop, at posting) × (1 + premium)`, `premium = OrderDef.premiumMin + (premiumMax − premiumMin) × u` rounded to 0.01 | 15–30% |
| Fresh today | The store keeps `freshToday[good]`: grams added by a **harvest** (`StoreAdd` from a crop, by anyone) since the last `arriveHour` pass, reset there. Stock leaves oldest first, so the fresh stock is `min(freshToday, stock not on offer)` | |
| Delivering | `DeliverOrder(orderId, q)` takes q of fresh stock to the supermarket's collection point at the truck stop (no cost, no labour; a van collects) and lowers what's left of the order. The sale is booked at delivery: Dr 1300 Owed by buyers, Cr 4000 Crop sales, `Value(q, orderPrice)`. Partial deliveries are allowed. Emits `OrderDelivered` | `q ≤ min(left, fresh stock)` |
| Payment | At the next `arriveHour` pass (6:00) the supermarket pays everything it owes: Dr Cash, Cr 1300. Emits `OrderPaid(amount)`. It never defaults in Phase 2. What is owed counts in net worth as an asset (spec 005 §3.5) |
| Lapse | At the next `arriveHour` pass whatever is left lapses (emits `OrderLapsed` with the amount left). Delivery closes at the midnight pass (`leaveHour`): from then until 6:00 `DeliverOrder` is rejected with `OrderClosed`. No penalty | |
| The hand delivers first | When a hand's order harvests a crop that has an open order and his sell rule sells it (SELL TO TRADER or OFFER AT), the fresh harvest goes to the supermarket order first, up to what's left; the rest follows the sell rule. STORE keeps it in store (the player can deliver it). A careless hand delivers first too | |
| The hand's choice counts the order price | In `FarmRules.BestCrop` (ADR 0048, 0050), for a crop with an open order whose harvest, sown now, would be ready before midnight, the expected harvest is valued at `orderPrice` up to what's left of the order and at the Floor beyond it | |
| Away | Orders post and lapse while away. The hand delivers as above. The away card lists each order: delivered, part delivered, or lapsed | |

Example (Spring, potato at 15 h, index 1.00): `BaseSowing` = 7,000 × 15 ÷ 96 = 1,093.75 kg. With `u` = 0.5 the amount is 1,640 kg. Premium 0.20 gives 6.80 × 1.20 = 8.16 a kg against the truck's 5.30, so the order pays 13,382 against the truck's 8,692.

### 3.4 Rice and potato in 15 hours (ADR 0055)

| Rule | Behaviour |
| --- | --- |
| Growth | `CropDef.growthMinutes`: rice 900, potato 900 (were 1,440) |
| Per sowing | Harvest, seed, fertiliser, spray and fertility change per sowing scale with `cycleShare` (spec 003 §3.1), so a season's totals are unchanged. Potato per acre per sowing: 1,094 kg (was 1,750); seed 109 kg (was 175) |
| Crops already growing | Keep their `readyAt` (it is stored when sown) |
| Labour | Unchanged per acre per day |

### 3.5 Notifications (amends spec 007 §3.5)

| Rule | Behaviour |
| --- | --- |
| New kind | `OrdersPosted`, importance **Needs you**, at the 6:00 posting. Quiet hours (22:00–07:00) move it to 07:00 as usual. Text: "3 supermarket orders: potato, radish and spinach" (no money amounts, spec 007) |
| No orders | No notification |

### 3.6b Onboarding (amends spec 009; §10 G6)

| Rule | Behaviour |
| --- | --- |
| `firstOrders` chapter | Triggered by the first `OrdersPosted`. "A shop in the city buys fresh produce straight from farms. It pays more than the trader, but only for what you harvest today, delivered by midnight, and it pays the next morning." Action: the Orders sheet |
| `truckAway` chapter | Triggered by the first `TraderAway` rejection. "The trader's truck comes at 6:00 and leaves at midnight, or when it's full. The stall closes at night too. Keep it in the store, or take a cart to the mandi." Action: the Store sheet |
| `comeBack` text | "{readyWhen}" replaces "at {readyTime} tomorrow": "tonight at 23:40", "tomorrow at 05:10", by the ready date in local time |

### 3.6 Processing order

At the hourly pass at `arriveHour`, system Market: (0) the supermarket pays what it owes, then the daily factors are drawn (§3.1b), (1) the truck arrives (resets), (1b) the hands sell what they held (§3.1), (2) open orders lapse, (3) new orders are drawn, (4) the market clears the hour that just ended (paused, §3.2). At the midnight pass: the truck leaves (if here), delivery closes, then the market clears. Orders are processed in id order.

## 4. Data

**New or changed definitions:**

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| `TruckDef` (in `market.json`) | `arriveHour` | int | 6 | 0–23 | |
| | `leaveHour` | int | 0 | 0–23 | Midnight |
| | `loadKg` | long (kg) | 4,000 | > 0 | Total load a day |
| `OrderDef` (in `market.json`) | `perDay` | int | 2 | 0–4 | |
| | `sowingsMin`, `sowingsMax` | Fixed | 1.0, 2.0 | 0 < min ≤ max | Acres' sowings |
| | `lotKg` | long (kg) | 10 | > 0 | |
| | `premiumMin`, `premiumMax` | Fixed | 0.15, 0.30 | 0 ≤ min ≤ max | Over P_regional |
| | `greensPerDay` | int | 1 | 0–1 | The small garden order |
| | `greenPickingsMin`, `greenPickingsMax` | Fixed | 2.0, 4.0 | 0 < min ≤ max | One bed's pickings |
| | `greensLotKg` | Fixed (kg) | 0.5 | > 0 | |
| `CropDef` | `growthMinutes` | int | rice 900, potato 900 | | Were 1,440 |
| `GoodDef` | `dailyVolatility` | Fixed | §3.1b | 0–0.1 | Produce only; 0 = no daily move |
| | `dailyBand` | Fixed | §3.1b | 0–0.25 | |
| `MarketDef` | `dailyReversion` | Fixed | 0.3 | 0–1 | |
| `NotifyDef` | kinds | | adds `OrdersPosted` (Needs you) | | |

**New or changed state** (save schema 5, with spec 010):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `MarketState` | `truckHere` | bool | Migrated as `true` if the save's hour is 6–23, else `false` |
| | `truckLoadToday` | long (g) | Migrated as the sum of `traderBoughtToday` |
| | `freshToday` | long[] per good (g) | Migrated as 0 |
| | `dailyFactor` | Fixed[] per good | Migrated as 1.0 |
| | `heldForTruck` | long[] per good (g) | Migrated as 0 |
| `SupermarketOrder` | `id`, `crop`, `amount`, `delivered`, `price`, `postedAt` | | In id order; `NextOrderId` |

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `SellToTrader` (changed) | as spec 004 | adds `TraderAway` (checked first) | as spec 004; adds to `truckLoadToday`; may make the truck leave |
| `DeliverOrder` | `orderId`, `q` | `UnknownOrder`; `OrderClosed` (lapsed, or between midnight and 6:00); `InsufficientStock` (more than the fresh stock); `OrderFilled` (q more than what's left) | §3.3 |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `TruckArrived` | — | The arrival pass | Map (the truck drives in), play log |
| `TruckLeft` | `reason` (Full, Day), `loadKg` | It leaves | Map, the away card, HUD |
| `OrdersPosted` | `orders` (crop, amount, price) | The posting | Notifications, HUD badge, away card |
| `OrderDelivered` | `orderId`, `q`, `amount`, `byHand` | A delivery | HUD (coins fly), away card, play log |
| `OrderLapsed` | `orderId`, `left` | The next posting | Away card |
| `OrderPaid` | `amount` | The 6:00 payment | HUD (coins fly), away card, play log |
| `HandSoldHeld` | `good`, `q`, `amount` | A hand sells held produce at 6:00 | Away card, hands' notes (spec 007) |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Sow an order's crop in the morning | Seed and fertiliser | 15–30% over the regional price for the order's amount, if it ripens by midnight | The field isn't free for a longer crop; labour | No |
| Deliver to the supermarket or sell to the truck | — | — | The truck's load and limits are kept for other crops | No |
| Leave an offer on the stall overnight | — | It sells from 6:00 | Spoils overnight like any stock | Withdraw any time |

## 7. UI

```
HUD:  [☀ 7:10 · Spring 2]  [🛒 2]  [₹ 14,200]  [Store 61%]

Orders sheet                                      [×]
+--------------------------------------------------+
| City supermarket · today's orders · till 24:00    |
| Potato   1,640 kg  at 8.16  (trader 5.30)  [Why?] |
|          fresh in store: 900 kg  [Deliver 900]    |
| Radish    120 kg   at 8.40  (trader 5.50)  [Why?] |
|          fresh in store: 0 · sow by 22:00         |
| Spinach    12 kg   at 26.40 (trader 20.50) [Why?] |
|          fresh in store: 4 kg  [Deliver 4]        |
+--------------------------------------------------+
| Only produce harvested today counts               |
+--------------------------------------------------+

Store sheet, the trader's row and, while anything is owed, the supermarket's:
| Trader's truck · here till midnight · 2,800 kg room   |
| Trader's truck · back at 6:00                          |
| Supermarket pays you 13,382 at 6:00 tomorrow   [Why?]  |
```

- The truck drives in along the district road and up the farm lane at 6:00, parks at the stop by the labour quarters, and drives off when it leaves. Tapping it opens the Store sheet.
- *Why?* on the order price: "Regional price 6.80 × 1.20 (supermarket premium 20%)". On the truck's room: per-crop room and the total load left.
- **Money owed by the supermarket** shows on the Store sheet ("Supermarket pays you 13,382 at 6:00 tomorrow", or "at 6:00" after midnight), on the Orders sheet under the delivered order, and on the Money sheet as a row of net worth. Its *Why?*: "Delivered today: potato 1,640 kg × 8.16. Booked as a sale today; the cash comes at 6:00".
- "Sow by HH:MM" is the latest sowing time at which the crop ripens before midnight.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | At the 6:00 pass the truck arrives: per-crop counts and the load reset, `TruckArrived` is emitted; at the midnight pass it leaves with `TruckLeft(Day)` | `TruckTests.Day_ArrivesAndLeaves` |
| AC-2 | A sale that fills the load makes the truck leave at once with `TruckLeft(Full)`; after that, and between midnight and 6:00, `SellToTrader` is rejected with `TraderAway` and nothing changes | `TruckTests.Full_LeavesAndRefuses` |
| AC-3 | Capacity is the lesser of the crop's room and the load's room; every kind of trader purchase (sell, overflow, floor hold, hand's rule, labourers) adds to both | `TruckTests.Capacity_SharedLoad` |
| AC-4 | The truck's price is the same at every hour from 6:00 to 23:59 of a day, including a day that starts a new season | `TruckTests.Price_HeldAllDay` |
| AC-4b | At each 6:00 pass every produce good's daily factor follows §3.1b and stays inside its band; inputs and price-controlled goods don't move; over 1,000 days the mean factor is within 1% of 1; the same seed gives the same factors | `MarketTests.DailyPrice_BoundedAndNeutral` |
| AC-5 | A harvest that doesn't fit the store while the truck is away is rejected with `TraderAway` and stays ready in the field | `TruckTests.Overflow_WaitsWhileAway` |
| AC-5b | A hand's SELL TO TRADER harvest at 2:00 is stored and held; at the 6:00 pass he sells it to the truck at the day's price; if the player sold part of that stock first, he sells only what's left; what exceeds the truck's room stays held | `TruckTests.HandSellsHeldAtSix` |
| AC-6 | From the midnight pass to the 6:00 pass an offer sells nothing and the trader takes none of it; a glutted price moves towards P_regional in those hours; from the 7:00 pass it sells again | `MarketTests.Stall_PausedAtNight` |
| AC-7 | At 6:00 two orders are posted for distinct eligible crops (in season, growth ≤ 18 h), with amounts of 1–2 sowings in 10 kg lots and prices 15–30% over P_regional, plus one greens order (spinach or coriander, 2–4 pickings in 0.5 kg lots); the draws are the same for the same seed; with no eligible crop no crop orders are posted | `OrderTests.Posting` |
| AC-8 | Only produce harvested since 6:00 counts as fresh; selling or eating older stock first leaves the fresh amount unchanged; `DeliverOrder` beyond the fresh stock is rejected with `InsufficientStock` | `OrderTests.FreshToday` |
| AC-9 | A delivery books `Value(q, orderPrice)` as a sale owed (Dr 1300, Cr 4000) and the ledger balances; cash is unchanged until the next 6:00 pass, which pays it all (Dr Cash, Cr 1300, `OrderPaid`); net worth counts what's owed; partial deliveries add up; after midnight `DeliverOrder` is rejected with `OrderClosed`; at the next 6:00 what's left lapses with `OrderLapsed` and no cost | `OrderTests.DeliverAndLapse` |
| AC-10 | A hand's SELL TO TRADER or OFFER AT harvest of an ordered crop fills the order first and sends only the rest to the trader or the stall; a STORE harvest stays in store | `OrderTests.HandDeliversFirst` |
| AC-11 | With an open potato order and potato ripening before midnight, the hand's `BestCrop` values its harvest at the order price up to what's left; after midnight it uses the Floor | `OrderTests.BestCrop_CountsOrderPrice` |
| AC-12 | Rice and potato take 15 h; their per-sowing harvest and seed scale by `cycleShare`; one acre's season totals equal the 24 h version's; a crop sown before the change keeps its `readyAt` | `CropTests.RiceAndPotato_15h` |
| AC-13 | The notification planner adds one `OrdersPosted` item at 6:00, moved to 07:00 by quiet hours, with the crops' names and no amount | `NotificationTests.Plan_OrdersPosted` |
| AC-14 | A schema-4 save loads at schema 5 with `truckHere` by its hour, `truckLoadToday` = the sum of its trader counts, no orders and no fresh stock; it advances a day and round-trips | `MigrationTests.Schema5_TruckAndOrders` |
| AC-15 | A year with the bot under the new rules gives the same hash in one chunk or many; the golden replays on frozen content are unchanged | `TruckTests.ChunkingInvariance`, `ReplayTests.*` |
| AC-16 | In a new game started at 2:00, selling the first garden basket to the trader succeeds; after the first-sale step the same sale at 2:00 is rejected with `TraderAway` | `TruckTests.FirstSale_AnyHour` |
| AC-17 | No orders are posted before the first session's last step is done; the first `arriveHour` pass after it posts them and triggers `firstOrders` once; the first `TraderAway` triggers `truckAway` once; `comeBack` reads "tonight" or "tomorrow" by the ready date | `OrderTests.StartAfterFirstSession`, `FirstSessionTests.TruckAndOrdersChapters` |

## 9. Balancing hooks

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| Days the truck fills | Rare for the competent bot | Under 1 day in 10 |
| Order income share | A bonus, not the main income | Under 20% of the competent bot's crop sales |
| Competent bot's net worth after 2 years | Up a little (orders' premium) | Stress bots within 1.20 × (ADR 0016) |
| Hamlet unlock day (median) | Unchanged or a little earlier | Day 5–9 |

## 10. Open questions (closed with the owner, 2026-10-21)

- **G1. A daily price.** **Closed (owner, 2026-10-21):** prices move daily at 6:00 by a bounded, mean-reverting factor per produce good (§3.1b).
- **G2. The price overnight.** **Closed (owner, 2026-10-21):** the valley's market keeps clearing while the stall is closed; the player's supply is 0, so a glut recovers by morning (§3.2).
- **G3. Who carries an order.** **Closed (owner, 2026-10-21):** delivered to the supermarket's collection point at the truck stop (no cost, a van collects), partial deliveries allowed, booked as a sale owed at delivery and paid at 6:00 the next morning; the Store sheet says "Supermarket pays you X at 6:00 tomorrow" (§3.3, §7).
- **G4. Which goods can be ordered.** **Closed (owner, 2026-10-21):** the two daily orders are field crops only (no fruit); a third, small daily order asks for a garden green (spinach or coriander, 2–4 pickings) (§3.3).
- **G5. The hand's sales overnight.** **Closed (owner, 2026-10-21):** what his SELL TO TRADER orders store while the truck is away or full is held and sold to the truck at 6:00, never more than is still in store (§3.1).
- **G6. The first session and the first week.** **Closed (owner, 2026-10-21):** until the first-sale step is done the trader buys at any hour; orders start at the first 6:00 after the first session's last step; two letter chapters (`firstOrders`, `truckAway`); the `comeBack` text says tonight or tomorrow by the date (§3.7).

## 11. Cut first

1. The truck driving in and out (it just appears and disappears at the stop).
2. "Sow by HH:MM" on the Orders sheet.
3. The hand counting the order price in his choice (he still delivers first).
