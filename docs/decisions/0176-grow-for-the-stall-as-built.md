# 0176. US-269 grow for the vegetable stall, as built

- Status: Accepted (Preview 25 approved by the owner, 2026-11-26; the story awaits the owner's review)
- Date: 2026-11-26
- Deciders: Keyur Patel
- Related ADRs: 0172, 0175, 0048, 0050, 0111, 0091

## Context

Spec 030 §3.6 and Preview 25: a hand told to grow for your vegetable stall. Two gaps were closed with the owner before the preview. **Which stall:** one row in the crop list for each open stall (named by its place when there are two or more). **How it sells:** the order is **Keep in store only**, never the trader, and the owner added the idea of a **preorder**: the stall's need for tomorrow steers what the hand sows, and the stall takes the harvest from your store at its 6:00 round, which is the hand's sale to the stall. The owner approved my reading (computed, not stored; spec 030 §3.6's ranking changed from last days' market buys to the unmet preorder, market buys kept as the tie-break).

## As built

1. **The order** (`StallOrderRules`, `OrderSpec.ForStall`). A field order whose crop id is `stall:<businessId>`; `SetStandingOrder` accepts it for an open vegetable stall of yours and only with the sell rule Keep in store (`InvalidArgument` otherwise). The order's `Crop` stays −1, the hand's choice, and `StandingOrder.ForBusiness` holds the stall's id (spec 030 had a crop index −2; this needs no second path where the code reads `Crop < 0`, and the command log needs no new tag, since the id rides in the crop string). Save schema 25 writes it after the order's other fields and hashes it only once non-zero, so no golden or fixture moved (`saves/schema25.ftc` was rewritten for the extra field; its pinned hash is unchanged).
2. **The preorder** (`StallOrderRules.Preorder`): `max(0, PutOut − the stall's Stock − your store's free stock − what is growing for it)`, never over what its Manual cap lets it take from you. *Growing for it* is the expected harvest (`FarmRules.Estimate`; a ready crop its harvest) of the crops on fields whose stall order is this stall's. Nothing is saved. `Wanted` lists what a hand can sow now (in season, not fodder, a good the stall sells), biggest first.
3. **The pick** (`StallOrderRules.Pick`, called from `Orders.CropToSow`): the in-season vegetable the stall sells, that `MaySow` on the field and whose seed the cash can pay, with the biggest preorder, then the most bought at the market over `supplyLookbackDays` (US-268's `BoughtOver`), then the biggest day's amount, then the earlier crop. A crop already growing counts against the preorder, so the fields spread across the vegetables by themselves. None fits: he sows his best pick (ADR 0050), as *Let him choose*.
4. **The stall gone.** `StallOrderRules.Stall` is null once the stall is closed or sold, so the order behaves as *Let him choose* from the next sowing, with no event and no state change; the hand's sheet and the field's sheet say "Your vegetable stall is gone, so Geeta chooses the crop now."
5. **The screens.** The crop list's row *For your vegetable stall* (with its *?*) under *Let him choose*; the sell step offers one row, *Keep in store*; the hand's and the field's order lines read "For your vegetable stall with compost (Radish now) · Keep in store · Every season" and "Stall preorder: okra 61.4 kg, amaranth 30.7 kg" (the three it lacks most); the *?* opens `stallPick` (id = stall id × 1000 + acre): the stall's day, its own stock, your store, what is growing, and what it still needs of each vegetable, then the rule in words.

## Differences from Preview 25

- The preorder line shows the three vegetables the stall lacks most (the preview's two were an example); the *?* lists them all. A first build listed every good the stall sells, trees and your bakery's products included; the Editor tour showed it, and it now lists only what a hand can sow now.
- The sheets stay in the game's own row layout (heading, then the note), as in the preview.

## Tests

Core `SupplyTests` (`StallOrderPick` AC-13, `StallOrderTieAndFallback`, `StallGone` AC-14, `StallOrderCommand`, `StallOrderSaves`), `GateTests.SupplyExploits` extended (508 stall orders over 200 towns, books exact) and `GateTests.StallOrderMeasured` (report only: 20 towns, 20 days, two fields ordered against none: the stall took 35.3 t from you against 23.7 t, bought 237.9 t at the mandi against 249.2 t, and the towns' net worth was the same within 0.04%); View `StallOrderViewTests` (`CropListRow`, `SellStepKeepsInStore`, `HandSheetLine`). Editor by scripted touch (captures `docs/art/s28-269-*.png`): the crop list's row and its *?*, the two steps, the order set, the hand sowing (radish, because the store already held okra), the hand's sheet with a preorder, the stall closed; `SheetLayoutCheck` 76 sheets, 0 problems; render statistics: all 1,282 draw calls / 25,633 triangles and village 566 / 11,624 within 10%, **farm triangles +11%** (6,594 to 7,331, draw calls down from 381 to 353; no map code changed, the owner's town has more crops in view than at the last reading). Editor saves restored and identical.

## Consequences

- Two stalls on the same vegetable both count the whole of your store's free stock; the fields spread anyway through what is growing for each.
- A preorder reads *Manual* caps: a good capped at none is never grown for.
