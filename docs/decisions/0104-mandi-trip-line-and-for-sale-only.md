# 0104. The Store shows what a mandi trip pays and when, and *Your land → For sale* lists only land that is for sale

- Status: Accepted (owner request, 2026-11-12)
- Date: 2026-11-12
- Deciders: Keyur Patel
- Amends: spec 004 §3.6 (the mandi trip, as the player sees it); spec 008 §3.5 (Your land, the For sale tab)
- Related: ADR 0031 (the mandi trip), 0054, 0055 (the supermarket's "pays you at 6:00"), 0101 §13 (Your land), 0103

## Context

1. **The cart's money was invisible.** The Store shows "Supermarket pays you 5,936 coins at 6:00" for money that is coming, but a cart sent to the mandi simply took the goods out of the store, and nothing said what it would bring back or when. The player had to remember (or open the Mandi sheet's sum, which is the *plan*, not the trip under way).
2. **For sale listed everything unowned.** `LandsSheet`'s For sale tab filtered on `!Owned` only, so it listed the village's land and neighbours who are not selling yet (`ParcelDef.ForSale == false`), which `BuyParcel` rejects with `NotForSale`.

## Decision

1. **A line under the supermarket's, in the same style:** *Mandi trip pays you · about 4,104 coins at 6:27* (or "… at 6:27 tomorrow" when the cart is back after midnight). It shows only while a cart is away, and goes when it is back. The amount is the loads at the mandi's price of this moment less the commission agent's share (the trolley's hire was paid at departure), `MarketRules.MandiTripPays`. It says **about** because the sale is made at the price when the cart arrives (the day's market factor can change), unlike the supermarket's fixed price. Its *?* says what the cart carries (a row per good) and why the figure is an estimate.
2. **For sale lists only parcels with `ForSale`.** Land you can't buy yet because it is not next to your land stays listed (it is for sale) with the note "Not next to your land yet", since `BuyParcel` requires adjacency. With none, the sheet says "There's no land for sale right now."

No rule, price or state change; no save change.

## Consequences

- `MandiLandViewTests` (new, 4): no line without a trip and the line while away and gone after; the figure equals the net the return pays (`MandiTripReturned.Net`); "tomorrow" after midnight; For sale only lists `ForSale` parcels and marks one that is not adjacent.
- Checklist `s18` checks 38–39; captures `docs/art/s18-store-mandi-trip*.png`, `s18-lands-for-sale.png`.
- Spec 004 and spec 008 carry an *As built* line.
