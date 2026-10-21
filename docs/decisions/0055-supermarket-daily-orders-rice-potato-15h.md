# 0055. Daily supermarket orders, fresh today; rice and potato grow in 15 hours

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: spec 003 §3.1 and `content/crops.json` (rice and potato growth time); spec 004 (a new buyer); spec 006 (the hand fills orders first, and his crop choice counts the order price); spec 007 (a morning notification)
- Related: ADR 0054 (the trader's day starts at 6:00), ADR 0048 and ADR 0050 (the hand's crop choice), ADR 0002 (real-time clocks, no speed controls)
- GDD sections: Land, farming and livestock (Storage and selling); Offline play (notifications)

## Context

The owner wanted a reason to come back each morning that rewards planning the day's sowing, in the spirit of Hay Day's orders but realistic: city supermarkets buy fresh produce directly from farmers, at a premium over the trader, in amounts and on days they choose.

Phase 1's rice and potato take 24 hours. A crop sown in the morning then can't ripen the same day, so an order for either could only be filled from stock harvested earlier.

The owner proposed the orders and decided their shape with Claude on 2026-10-21.

## Decision

1. **Two orders at 6:00** each day from an off-screen city supermarket. Each names one crop, an amount and a price.
2. **A morning notification** announces them at 6:00, or when the player's quiet hours end.
3. **Delivery by midnight.** An order not filled by midnight lapses. There is **no penalty** for missing one.
4. **Which crops.** The crops are chosen at random with the seeded RNG, among the crops that can be ready by midnight: in season, with a growth time that lets a crop sown in the morning ripen that day.
5. **How much.** The amount is sized to about one or two sowings of an acre.
6. **The price** is the regional price plus 15–30% (content).
7. **Fresh today.** Only produce harvested after 6:00 that day counts towards an order, so stock can't be hoarded for orders.
8. **The hand delivers first.** While an order is open for a crop he sells, the hand fills the order before selling to the trader.
9. **The hand's crop choice counts today's order price** when the crop can ripen by midnight (it extends ADR 0048 and ADR 0050).
10. **Rice and potato grow in 15 hours** (they took 24). A crop's harvest and seed per sowing already scale with its growth time (`cycleShare`, spec 003), so profit per season is unchanged: there are more, smaller sowings. A crop sown at 9:00 can fill the day's order. Crops already growing when the change ships keep their time.

## Consequences

- **Spec 011** fixes the order's state and generation, the delivery command and who carries the goods, how fresh-today is tracked in the store, the notification and the hand's rules. Open gaps include: whether fruit and the kitchen garden can be ordered, and what happens to an order half filled at midnight.
- **Accepted costs:** re-tuned numbers and the tests that pin rice and potato times, and the balance gate re-measured with orders in. The golden replays run on frozen content and don't change.
- Rice and potato now need tending more often (more sowings, each smaller). Labour per season is unchanged, because labour needs are per acre per day.
- The orders add income at a premium without risk. The balancer checks they stay a bonus, not the main source of profit (ADR 0016's 20% band).
