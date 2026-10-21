# 0050. The hand sows the best crop he can pay for (Sprint 10 review)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0048 §1 (the hand's choice); spec 006 §3.8 (Re-sow), §8 (AC-15; AC-26 new)
- Related: ADR 0009 (orders never borrow), ADR 0049

## Context

The owner reported that a hand given an every-season order still sat in his hut. Since ADR 0048 the hand chooses the most profitable in-season crop, but he chose it **whatever it cost**. In Spring that crop is potato. The inherited seed potato is used up in the first session, and after that potato seed costs about 3,400–3,900 coins a sowing. With less cash than that, the order waited with "not enough cash for seed", even though radish seed costs about 50 coins. The tests had always had plenty of cash or seed in the store, so they never showed this.

## Decision

- **The hand's choice considers only crops he can pay for now.** These are the crops whose certified seed and fertiliser, less what is already in the store, cash can cover (`FarmRules.SowingCost`, the same cost `Sow` checks). Among those, the best expected profit per season still wins.
- **The order's own crop:** it is sown when it is in season, the seed it names is at hand (saved seed in the store, if the order uses saved seed), and its cost can be paid. Otherwise the hand sows his best affordable pick, so a field is never left empty while some crop can be sown.
- **When he waits:** only when no in-season crop can be paid for (`OrderWaiting(InsufficientFunds)`) or none is in season (`OutOfSeason`). Orders still never borrow.

## Consequences

- Early in the game, with little cash, the hand keeps the fields going with cheap crops (radish) and moves to better ones as the harvests bring in cash.
- An order's crop can now be replaced by a cheaper one when cash is short. The order line shows this ("Keep Potato (Radish now)").
- AC-15 now empties the store's seed before it checks the wait. AC-26 is new: `OrderTests.NeverIdleWhileACropIsAffordable`.
