# 0059. Sprint 11 device review: one row per supermarket order, the Store in sections, the hand's order shows the sow sheet

- Status: Accepted
- Date: 2026-10-22
- Deciders: Keyur Patel
- Amends: spec 011 §7 (the Orders sheet, the Store sheet); spec 006 §3.8 and spec 008 §3.5 (giving a hand a field order)
- Related: ADR 0043 §7 (the Store as a hub), ADR 0044 (Sprint 7 device review), ADR 0048 and ADR 0050 (the hand's best affordable crop), ADR 0051 (the soil before and after on the sow sheet)

## Context

The owner played the Sprint 11 Development build on the iPhone 13 for 30 minutes on 2026-10-22. Every check in `s11-village-truck-orders.md` passed. With the whole map in view the frame time was main thread 3.83 ms (p95 5.37 ms, max 9.41 ms) and GPU 4.43 ms (p95 4.60 ms), at 60 fps with 0.00% of frames over 25 ms (US-190.T6, ADR 0035's budget). The owner asked for three changes:

1. **The supermarket's Orders sheet is cramped and confusing.** Each order took two rows: the order, then a status row ("Nothing fresh yet · Sow by 17:59 · 10.0 kg delivered"). A filled order still showed a status row.
2. **The Store isn't easy to read or use at one glance.** Status rows, three identical "Open" buttons and the goods were in one list.
3. **A hand's field order doesn't show what the player's sow sheet shows.** The order's crop list had no fertiliser choice (the hand always sowed without fertiliser) and no harvest, money or soil figures.

## Decision

1. **One row per order.** The row shows the crop, "Wants 50.0 kg at 34.52 /kg · the trader pays 27.89 /kg" and its *Why?*. The order's state goes on the same row, in colour: when to sow ("Sow by 15:59 to harvest it today"), what's fresh ("83.9 kg harvested today, ready to deliver") with Deliver, how much is delivered so far ("1.5 kg of 4.5 kg delivered"), or "Missed: deliveries closed at midnight". A filled order shows "All 60.0 kg delivered" and a green **Delivered** tag where the button was. The subtitle says the rule once: "Harvest today, deliver by midnight, paid at 6:00 tomorrow".
2. **The Store in sections** (owner's choice of three layouts). At the top, how full the store is. Then three picture buttons: Supermarket orders (with how many are open), Buy inputs, and Mandi trip. Then what the supermarket owes. Then **Sell to the trader**: the truck's state and one row per crop with its Sell button. Then **Seed and inputs**: what's in store.
3. **A hand's field order starts with the sow sheet.** The same fertiliser choice (none, compost, chemical) and the same crop cards as the player's sow sheet: time, how well it suits the soil now, the harvest, spend, sells for, profit, inputs and work, and the soil before and after. Each card's button is **Choose** (sent to the next step: this season or every season, then how to sell). "Let him choose" uses the chosen fertiliser too, and names the crop he'd pick now. The order keeps the fertiliser: the hand sows with it every time and buys what the store lacks, as his sowings already do (spec 006 §3.8). An out-of-season crop can still be chosen; its card says the hand sows his best pick until its season.

## Consequences

- No rule, formula or balance number changes: the sim already stored a fertiliser on every order and sowed with it; only the sheet never offered one.
- `LineVm` gains a note line, its colour, and a done tag; the Store's picture buttons are a new sheet element.
- The device checklist for this follow-up build covers the three sheets; the Sprint 11 checklist stands as passed.
