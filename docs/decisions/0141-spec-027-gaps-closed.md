# 0141. Spec 027: events, insurance and forward contracts, gaps closed

- Status: Accepted (owner, 2026-11-20, G1–G9 closed one per message, each on my recommendation)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Related ADRs: 0002, 0004, 0014, 0042, 0043, 0057, 0063, 0107, 0125, 0127, 0139

## Context

ADR 0107 §13 fixed the scope of the event engine, insurance and forward contracts. Spec 027 (written in Sprint 23, ADR 0057, for Sprint 24) turns it into rules and numbers.

## Decisions (each a recommendation, with the reason an average player can read)

1. **The engine (G1).** One seeded roll a day at 6:00 (a minor about 9 days in 10, a major about 1 in 16); at most 2 cards wait; every card has a named, free default taken after 24 hours (a minor) or 48 hours (a major); away, a minor takes its default and is reported, and majors and disasters never roll. Every choice is a command. *Why:* it mirrors how HR events and the trader's daily price are rolled; the cap and the timeouts keep a busy town from becoming a chore; never rolling a major away keeps ADR 0002's 72 h rule.
2. **The catalogue (G2).** 19 events in seven families (the GDD's table), each a row of data whose effects come from a closed list of nine kinds, each with a preparation that softens it and a free default. Crop damage is one number, an *event health* multiplier. *Why:* the catalogue can grow without code.
3. **Weights and the start (G3).** Base weight 10 (4 for disasters) times named modifiers from the town's state, clamped 0.25–4; four mercy rules (no repeat in a season, one major a season, no event on a promise day, a major over 40% of cash is not drawn); **events start at the Township**. *Why:* the Village's pace is measured against Phase 2's gates (day 101 and 135), and an event's default costs would shift them.
4. **Disasters (G4).** Fire (35% of a building's book value, halved by the water tower's reach and by preparation, a business building shut 2 days, two repair choices) and an epidemic (sick chance ×2.5, the clinic halves it, health never below 0.75, nobody dies), each warned 48 hours ahead and slid to the next live 6:00 while you are away. *Why:* a disaster is a bill and lost days, never a game over (ADR 0004, 0063).
5. **Crop insurance (G5).** An insured yield index of luck (`Weather × CropHealth × EventHealth`), a 0.80 threshold, a premium from the crop's own pest risk plus a flat event part with a 1.30 loading, bought by one switch that covers later sowings. *Why:* insurance covers luck, not neglect; pricing by the crop's pest risk teaches that sowing the same crop again is a riskier bet.
6. **Property and business insurance (G6).** Two policies and every building in one; season policies bought at the close; 0.2% of book value (plus 3% of last season's profit for a business); 90% of a fire's repair bill and 80% of lost days; a risk already signalled is not insurable. *Why:* you carry part of any loss, and each business keeps its own books (ADR 0068).
7. **Forward contracts (G7).** Staples only, the trader's offer for the top 2 crops (40% of last season's harvest, 3% below the expected price, delivery next season), delivered by normal selling at the contract price, a shortfall settled at the difference to the season's average price. *Why:* never riskless; a contract protects you if prices fall and costs you if they rise.
8. **Screens (G8).** Event cards and an Events sheet, the insurance section on the Money page and on the business and sow sheets, the trader's offers on the Store sheet; Previews 12 and 13 first.
9. **Scale (G9).** 16 points beside spec 026's 11 and the hills' 5 (32 for Sprint 24); one schema bump (22); the cut line in spec 027 §11.

## Consequences

- Spec 027 is approved; its tests are named in §8 and written in Sprint 24.
- The balance pass in Sprint 24 sets `eventPart` from the measured hail, drought and waterlogging shortfall and re-measures the Township's pace, the archetypes, an *insure everything* bot and an *accept every contract* bot.
- The investor's offer event comes with spec 029 (US-247).
