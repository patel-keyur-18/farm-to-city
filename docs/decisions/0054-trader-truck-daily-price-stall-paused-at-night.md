# 0054. The trader's truck: 6:00 to midnight, a price set each morning, the stall paused at night

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: spec 004 §3.2 (the trader's floor on the stall), §3.3 (selling to the trader), §3.5 (the overflow rule), §3.8 (when the trader's capacity resets); spec 006 (the hand's selling); spec 008 (the map shows the truck)
- Related: ADR 0031 (where to sell), ADR 0034 (a glut drains hour by hour), ADR 0037 (the mandi trip), ADR 0053 (the roads)
- GDD sections: Land, farming and livestock (Storage and selling); Economy (Regional trader)

## Context

In Phase 1 the trader is always there: it buys at the floor at any hour, up to a per-crop daily limit that resets at the midnight daily pass, and its floor follows the regional price. Nothing on the map shows it. A real village trader comes by truck in the morning, buys at a price fixed for the day, and leaves when the truck is full or the day ends. The local market (the player's stall) also kept selling overnight, so an offer left on the stall at night sold into an empty market at the glut price.

The owner decided the following with Claude on 2026-10-21.

## Decision

1. **A small truck** of about 4 t (content) arrives at **6:00** along the district road and up the farm lane, and parks at the stop by the labour quarters. It leaves when its load is full or at **midnight**, whichever comes first, and comes back at 6:00.
2. **The trader's day starts at 6:00.** Its per-crop limits reset then, not at the midnight daily pass.
3. **The buying price is set once at 6:00** from the regional price (today's floor formula, spec 004) and held all day. The local market's price still moves every hour.
4. **A total load** of 4 t is added on top of the per-crop limits. It is tuned in the balance runs so that a normal day rarely reaches it. Big harvests are what the mandi trip is for (ADR 0037).
5. **While the truck is away** (full, or between midnight and 6:00):
   - selling to the trader is refused with "Back at 6:00";
   - a harvest that doesn't fit the store waits in the field, as it does today when the trader is full for the day.
6. **The stall pauses from midnight to 6:00.** The player's offers on the village market don't sell overnight, so no glut is sold cheap at night.

## Consequences

- **Spec 011** fixes the truck's state, the reset at 6:00, the total load, the refusal, the stall's pause and how the truck is drawn. It also settles what the stall's price does overnight and how the hand's sell orders behave while the truck is away (gaps to close with the owner).
- The price being fixed for the day is a small lesson in itself: sell to the trader in the morning and you know the price; offer on the stall and you take the hour's price.
- While the player is away the truck runs as usual. The away card and the notifications report what it bought.
- The balance gate is re-measured. The total load and the night pause cut sales a little for players who harvest big batches late; the mandi trip and the store absorb it.
