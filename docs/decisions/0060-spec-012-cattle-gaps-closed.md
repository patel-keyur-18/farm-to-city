# 0060. Spec 012 gaps closed: 22.8 days a game day, cattle from the Hamlet, fodder sold standing

- Status: Accepted
- Date: 2026-10-22
- Deciders: Keyur Patel
- Amends: ADR 0056 (the time scale, lactation lengths, what happens to spare fodder); specs 002, 003, 004, 005, 006, 007, 008, 009 as listed in spec 012's header
- Related: ADR 0008 (per-season realism), ADR 0009 (never stuck), ADR 0028 and 0038 (loans secured by what they buy), ADR 0032 (home use), ADR 0048 (the hand's pick), ADR 0053 (the commons)
- GDD sections: Land, farming and livestock (Livestock); Player journey and eras (Hamlet)

## Context

ADR 0056 set the cattle design and left the numbers, silage and neglect to spec 012. Writing the spec raised nine gaps. The owner closed them one by one on 2026-10-22, then approved the rest together and asked that everything stay realistically achievable for an average player and make sense on screen.

## Decision

1. **Time scale (G1).** One game day stands for 22.8 real days (365.25 ÷ 16), matching per-season realism and the labourer's wage, not ADR 0056's "about a month". Content gives amounts per real day ("8 L a day"); a buffalo gives about 182 L a game day. In milk 13 game days (about 300 real days), dry 5 (buffalo) or 3 (cow): about one calf a game year.
2. **Unlock (G2).** The shed can be built from the Hamlet, with a `cattle` letter chapter, a locked note on the yard before it, and cattle listed among the Hamlet's rewards.
3. **Green fodder (G3).** Berseem (sown Winter, cut Winter–Spring) and multi-cut sorghum (sown Summer, cut Summer–Monsoon) are cut every morning from 6 h after sowing. The commons cover a share of the need by season (Monsoon 40%). Any shortfall is bought from neighbours (on by default). **Changed in the review for sense:** what the animals don't eat is sold standing to neighbours at 1 coin a kg instead of being lost; a hand's fodder order rotates berseem and sorghum through the year.
4. **Straw (G4).** Wheat and rice harvests add straw to the stack once the shed exists; the rest is ploughed in. Straw can be bought.
5. **Stocks (G5).** A feed room of 2.4 t and a 10 t straw stack with the shed; a compost pit in every yard. These goods live in their own place, not the hut store. The player fills them, or the hand's animals order keeps them stocked. A low-feed warning at purchase, on the sheet and as a notification.
6. **Milk (G6).** The co-op pays at 6:00 by fat (12 + 6.5 × fat% a litre); the household drinks 1.5 L a real day as home use inside its food allowance.
7. **Health (G7).** Vaccination each season automatically when cash allows; a seeded disease with the vet called automatically (no vet when cash is short: a longer illness, nothing owed); hunger lowers health; no animal dies.
8. **Money (G8).** Animals booked at cost, not depreciated (they don't age in Phase 2); sold at 80% × health; a bank dairy loan counts the animal at 0.75; calves sold at birth; foreclosure seizes animals after bought land.
9. **Neglect and silage (G9).** No animal is lost to neglect in Phase 2. Silage comes with the equipment ladder.

## Consequences

- Spec 012 is approved and is built in Sprint 12 (F13.1, US-126 and new stories).
- Save schema 7.
- A fodder acre is never wasted, but as a cash crop it pays about half of wheat, so it only pays to grow for your own animals.
- The balancer adds a dairy bot and checks payback (spec 012 §9).
