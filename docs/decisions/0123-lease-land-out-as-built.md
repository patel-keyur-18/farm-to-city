# 0123. Leasing land out as built: per parcel, rent 2.5% of its land value upfront at the close, a locked acre

- Status: Proposed (2026-11-18); the owner reviews US-261 with the story
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0030, 0039, 0078, 0119, 0121, 0122

## Context

Spec 024 §3.6 (ADR 0119) lets you lease land out to a neighbour farmer. US-261 built it; the owner approved Preview 8 and the six choices below (2026-11-18).

## Decisions

1. **Per parcel,** like zoning: the sim sells and zones whole parcels, so `LeaseOutLand` and `EndLease` take a parcel and the sheet says how many acres. The inherited acres can be leased (never sold); a parcel with a crop, tree or building, a hand's standing order, a pump, borewell, channel or drip on it, or watered by the pump or the borewell, is refused with a reason in words (the sim never takes an order or a machine off silently, ADR 0039).
2. **Zone, lease and sale are one choice at a time.** A parcel zoned for builders can't be leased until taken back to farming; a leased parcel can't be zoned, listed or sold, farmed, built on or irrigated (`Parcel.Locked`: listed or leased; the reason names which). Builders skip a leased parcel.
3. **Timing.** Tapping *Lease out* locks the acre at once (`Starting`); at the next close the lease runs (`Active`) and the first rent is paid **upfront**, and again at every close. *End lease* on a running lease ends it at the next close (`Ending`: no rent for the season after, the rent already paid stays yours); on one that has not started it cancels at once, free, and unlocks the acre.
4. **Rent** = the parcel's land value × `leaseYield` (**2.5%** a season, `township.json` `landValue.leaseYield`, 0–10%), to the coin. Land value is read at the close before the rent is paid, so the rent follows the town (it rose from 9,587 to 9,857 in the Editor as the town grew). Its *Why?* says it in words: "1 coin in every 40" (no decimals, ADR 0043).
5. **A separate account,** 4210 *Rent received: land* (schema 20), so the season report says "Rent from your leased land" instead of "Rent from your families". It counts as other income, outside farm profit and so outside the Hamlet and Village gates, and as passive income (spec 019: the hub's rent line and the "first rent" milestone count it with the houses' and the godown's).
6. **The soil stays as it was:** a leased acre does not recover its fertility while fallow (the tenant neither wears it out nor builds it up, as for land you don't own, ADR 0030). A seized parcel's lease goes with it.
7. **Screens (Preview 8):** a *Lease out* button with the rent beneath on an empty field (greyed with the reason when it can't), a tap card, the leased sheet (rent with its *Why?*, next rent, what is locked; no irrigation cards), *End lease* with its card ("Keep leasing"), *Cancel the lease* before the start, a note and the button on Your land, a line on the away card for each rent and each ended lease.

## Consequences

- Schema 20 gains `Parcel.Lease` and account 4210 (hashed once used); fixtures regenerated.
- Leasing is the safe, small choice (about 10% a year on the land's value, no work). If the bots show it beating hiring a hand on the same acre, the yield is tuned (ADR 0027) after telling the owner: the balance pass measures it.
- Spec 024 §3.6 is amended in place.
