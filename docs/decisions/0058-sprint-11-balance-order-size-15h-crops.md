# 0058. Sprint 11 balance: orders of half to one acre's sowing, the 15-hour crops' gain accepted, the long crop at 12 hours, the truckAway page

- Status: Accepted
- Date: 2026-10-22
- Deciders: Keyur Patel
- Supersedes: ADR 0055 §5 (an order's size); ADR 0041 §6 (the long crop is "at least a day")
- Amends: spec 011 §3.3, §3.6b, §4, §7, AC-7, AC-17, §9; spec 007 §3.5; spec 009 §3.2 and AC-14
- Related: ADR 0055 (supermarket orders, rice and potato in 15 hours), ADR 0016 (the balance rule), ADR 0028 and ADR 0033 (land priced by an acre's earnings)

## Context

US-194 re-measured the balance gate with the truck, daily prices and supermarket orders in. Until now the balancer's bots never finished the first session, so the trader bought from them at any hour (the first-sale exception) and no orders came. The bots now skip the first session and the competent bot fills orders.

1. **Orders' share.** At one or two acres' sowings each (ADR 0055 §5), orders were 27.4% of the competent bot's sales. Spec 011 §9 caps this at 20%, so orders stay a bonus. Measured over 50 seeds: 0.75–1.5 sowings gave 21.3%, 0.5–1.0 gave 14.9%, and one order a day at 1–2 sowings gave 15.1%.
2. **The 15-hour crops' gain.** With rice and potato at 15 hours, the median season farm profit rose from 33.5k to 45.6k before orders, although the yield per season is unchanged. The gain comes from selling. Six acres of potato every 15 hours is about 6,500 kg a harvest instead of 10,500 kg, so more fits the trader's 3,000 kg a day and the stall, and less spoils. The economy model's anchors are unchanged: an owner-run acre makes 17,057 a season (was 17,026), and an inherited acre costs 7.2 seasons of that (was 7.1).
3. **The long crop.** ADR 0041 §6 asks for notifications at the first field crop of "at least a day". With rice and potato at 15 hours no crop takes a day, so the ask would never come. The first session's `longCrop` step already uses 12 hours.
4. **The truckAway page (US-195).** Spec 011 §3.6b opens the `truckAway` letter page at the first `TraderAway` refusal. But at night the Store's Sell is greyed with its reason (spec 008 §3.6), so the player never sends a refused sale; only a night harvest into a full store is refused, and most players would never see the page.

## Decision

1. **An order is half to one acre's sowing** (`sowingsMin` 0.5, `sowingsMax` 1.0), two crop orders a day as before. A potato order is about 550–1,090 kg, a realistic daily buy from one farm, and one or two acres sown before 9:00 fill it. Measured: orders 14.8% of sales over 100 seeds.
2. **The gain from the 15-hour crops is accepted.** It is a real effect (smaller, more frequent lots sell better), it pays for the extra tending, and the gate holds: the median season farm profit is 46,092, net worth after a year is 1.4% up, the Hamlet comes on day 5.4 (median), and every stress bot is within 1.20 ×.
3. **The long crop is a field crop of at least 12 hours**, for the notification ask's fallback card as for the first session's step.
4. **The balancer's bots play after the first session** (they skip it), and the competent bot fills orders from fresh stock.
5. **The truckAway page also opens the first time the Store opens while the truck is away with produce in store** (trigger `storeTruckAway`), or at the first `TraderAway` refusal, whichever comes first (owner, 2026-10-22).

## Consequences

- Spec 011's amounts, example and §9 show the measured numbers. ADR 0055's other decisions stand.
- The truck never filled in the gate's runs (0.0% of days against the 10% bound). The 4 t load is kept, so a big harvest still needs the store or the mandi.
- Spec 007 §3.5 and spec 009 §3.2 and AC-14 say 12 hours for the long crop.
