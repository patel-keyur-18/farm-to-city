# 0131. Pucca upgrades as built: a build with materials and masons, the rent follows at the renewal, builders upgrade one home a close

- Status: Accepted (owner approved Preview 5, with the five recommendations, 2026-11-19)
- Date: 2026-11-19
- Deciders: Keyur Patel
- Amends: spec 024 §3.7
- Related: ADR 0107 §13, 0110, 0119, 0122, 0125, 0128, 0130; spec 014 §3.14, spec 024 §3.7

## Context

Spec 024 §3.7 offers two upgrades: a hand's hut becomes a kutcha house (45,000), and a kutcha family house becomes pucca (1,80,000 of the pucca house's 2,40,000) where land value is 1.15 or more, clean water is near and the family can bear the pucca rent. Preview 5 showed the offer, the card, the work under way, the made-pucca family sheet, the reasons a house is not offered, the Builders count and the map. Four things in the spec could not be built as written.

## Decision

1. **The upgrade is a build.** `UpgradeHouse` takes the difference in bricks, cement, timber and mason-days (`pucca_upgrade` in `township.json` construction: 8,000 bricks, 80 bags, 2.4 m³, 55 mason-days, 48 hours: 1,79,800 at the base prices, 1,80,000 within 2%). It is paid in full at the start from your cash, goes into the house's book value (and so its depreciation), and draws the trader's stock like any build, so a brick shortage slows it. The family stays. The house is pucca when the work is done (`Building.Pucca`, save schema 21, hashed once a house is pucca).
2. **The rent follows at the renewal.** A pucca house's market rent is the pucca base (7,000) × the place × prices, in place of 2,500. The lease keeps its rent; at its end the usual capped step moves it (the 10% cap, then no more than 30% of the family's income) toward the pucca rent, and on at each renewal. Nothing else changes in the family's life.
3. **Offered when** the acre's land value is 1.15 or more, clean water is near (the pond, a well or the tower: one rule, ADR 0130), the acre is not listed or leased out, and the family can bear the pucca rent (30%, `burdenLimit`). An empty house or a hand's house needs no tenant test: the card says nothing comes back in rent.
4. **Builders upgrade by themselves**, at the close after they buy: one home a season, the first in building order that is offered by the same rules, with bricks after yours and the treasury's. Their tenants' rent follows the market rise. A builder's home is not offered to you.
5. **Cut: the hut → kutcha upgrade.** It is offered when the hut's household is a family, but a hand's hut holds one hand alone (a hand's family lives in a family house, ADR 0063), so it would never be offered; making it work means rebuilding how a hand and his hut are linked. The family-house upgrade is the one that grows the skyline.
6. **The payback is said as the sum:** the cost over the rise in rent once the pucca rent is reached, about 40 seasons, not the spec's 48.
7. **Why not offered** is said with its numbers in the row's *Why?* (land value against 1.15, clean water, the family's income against the rent), not as a separate sheet.
8. **Art.** `pucca_house_{s,w,e,n}` in Blender: the family house raised a floor, a balcony with a grille, a pale-blue upper floor, glazed windows; `pucca_house_building_*` is the family's house in bamboo scaffolding with a brick stack and a sand heap.
9. **Found on the way:** the land value's *Why?* had no words for the smoke and the missing drains (US-143, US-144), so its **?** threw on an acre they touched. The words are added and `LandValueViewTests.EveryPartHasWords` guards the table.

## Consequences

- Save schema 21 gains `Building.Pucca` (the fixtures `schema21.ftc` and `schema21-ring.ftc` are regenerated). The command log gains the `UpgradeHouse` tag; older logs read as before.
- The upgrade pays back in about 40 seasons (a slow, honest choice), so a player who wants rent income builds more houses first; the builders' pucca count is on the Town tab.
- Balance to read at the end of the sprint: the brick limit with upgrades queued behind the tower (the kiln is the cure), the builders' one-a-season pace against the town's growth, and the rent's step against the 20% rule.
