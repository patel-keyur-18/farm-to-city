# 0151. US-264 the mill's atta at the village market as built: after the bakery and the trader, never below the truck, only what the village can take

- Status: Proposed (awaiting the owner's review of US-264)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related: spec 016 §3.5c, ADR 0140, ADR 0144 §3, ADR 0073

## Context

Spec 016 §3.5c said atta also sells at the local market; the code sold it to the bakery and the trader only (found 2026-11-20). The owner decided the code follows the spec (ADR 0144 §3) and closed the gap on sharing the village's demand (2026-11-21): the mill never sells below the truck, the order is bakery, trader, village.

## Decisions

1. **What is offered.** A mill's unsold atta less what your other businesses still need today (a bakery's mix) and less the trader's room (what he still takes today while the truck is here, else tomorrow's whole day). Only goods listed in `businesses.json` `area.villageGoods` (atta) are offered; bran is not.
2. **Never below the truck.** The mill's lot joins the hourly clearing of the good only while the market price is at or above the trader's floor. It is the founder's offer mechanism, so the clearing, its spreading over the day and the unsold part going on to the trader's floor are unchanged. Your own offers cannot clash: the store never holds atta (a kirana of your own, US-260, will share the day's demand in proportion to kilos offered; nothing built now).
3. **Only what the village can take.** The first build let the whole surplus in, and the price overshot under the floor, closed the gate for hours and sold in bursts. The lot is now capped by the supply at which the price just reaches the floor (demand × (regional ÷ floor)^elasticity), less what the valley and imports already supply. The window is small: the floor is the regional price less transport, 5%, and atta's elasticity is 0.4, so a village of 120 kg a day with 100 supplied has room for a few kilos a day until the town grows and buys more. This is the model's honest answer: the village is a thin outlet, not a second truck.
4. **The books.** Each mill's share of the hour's sales is posted as its own sale (its Sales and cost of goods, at the price the market paid, 5% more with a creative worker like every sale) and counted in `Business.VillageToday` (schema 22, hashed only once used, reset each working day). Nothing posts to the founder's books.
5. **The screens.** The mill's Today tab has *Atta sold in the village today*, with a *Why?* (the village's price, the trader's price it never goes under, the village's demand, the order of sale).
6. **The targets hold.** `GateTests.Mill` re-measured: hired median 6,452 a season (target 5,000–10,000), run by you 36,402 (30,000–40,000), payback 3.1 (3–4). The 2-day stock limit on the mill's wheat buying (ADR 0140) is unchanged: the village takes too little to count.

## Consequences

- Save schema 22 gains `Business.VillageToday`; fixtures unchanged in content. Without a mill the clearing is byte-for-byte as before.
- `VillageAttaTests` (4) and `VillageAttaViewTests` (1).
