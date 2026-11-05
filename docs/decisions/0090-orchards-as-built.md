# 0090. Orchards as built: shade, the Hamlet, the intercrop, and a batch picked in parts or sold on the trees

- Status: Accepted (owner, 2026-11-05: §1–§4; §5 with US-129's review, approved the same day)
- Date: 2026-11-05
- Deciders: Keyur Patel
- Amends: spec 013 §3.3, §4, §5, §7, §8 (AC-6, AC-13)
- Related: ADR 0075 (spec 013), 0034 (ripe fruit waits 72 h, then spoils), 0037 (shade per tree), 0089 (equipment as built)
- GDD sections: Farming (fruit trees: whole-acre orchards)

## Context

Coding US-129 turned up four gaps in spec 013 §3.3, settled with the owner one at a time.

The fourth gap was the biggest. A batch is every tree's fruit at once:

| Orchard | One batch | The trader buys a day |
| --- | --- | --- |
| Banana | 3,200 kg | 150 kg |
| Papaya | 4,800 kg | 100 kg |
| Mango | 4,800 kg | 400 kg |
| Guava | 3,000 kg | 150 kg |

The hut store holds 2,000 kg, and ripe fruit spoils after 72 hours (ADR 0034). If a pick had to take the whole batch, it was refused (`TraderFull`) and the fruit rotted on the trees. A field crop never hits this, because a harvest is one short cycle's yield.

In India, orchard fruit is mostly sold on the trees to a fruit contractor (*bagicha thekedar*). He picks and markets it himself, and pays the grower well below what the grower would get selling it, in return for certainty and no picking work. Growers who pick their own fruit do it in rounds.

## Decisions

1. **Shade** (owner, 2026-11-05). A bearing orchard shades each field beside it (sharing a side) as one mature edge tree of its kind would: −8% for mango or guava, −3% for banana or papaya. The 0.80 shade floor holds (ADR 0037). Young trees cast no shade.
2. **Unlock** (owner, 2026-11-05). Orchards come with the Hamlet. Before that, the field card's *Plant an orchard* is greyed with "Comes once you're a Hamlet".
3. **The intercrop** (owner, 2026-11-05).
   - Between young trees, only the short crops are sown (radish, methi, amaranth, lentils, peas: `orchard.intercrops`). They give `intercropShare` 0.5 of the acre's yield and take 0.5 of its labour. The sow sheet lists only those crops, with halved figures.
   - A crop already growing when the trees start bearing finishes at that share.
   - Once the trees bear, nothing more is sown: "The trees are bearing: no crop between the rows now" (`TreesBearing`). A crop that isn't a short one is refused with `NotAnIntercrop`.
4. **A batch is picked in parts or sold on the trees** (owner, 2026-11-05).
   - **Pick what can go today** (`PickOrchard`) takes as much of the ripe batch as fits the store, plus what the trader can still take today with *sell the rest*. What neither can take waits on the trees, on the usual clock: safe for 72 h, then spoiling day by day.
   - **Sell on the trees** (`SellOrchardOnTrees`) sells what's left of the batch to a fruit contractor, at any hour, with no store or truck limits. He pays `contractorShare` 0.7 of the trader's price for that weight (Indian pre-harvest contracts run at 60–75% of what the grower would get).
   - The button reads *Sell it all on the trees* before any picking and *Sell the rest on the trees* after. So the player can sell the whole batch to the contractor, or pick some for the trader and sell the rest.
   - In the books: Dr Cash, Cr 4010 Fruit sales. It goes on the sales record as "Sold on the trees to a fruit contractor" (`SoldTo.Contractor`).
   - A batch counts as a harvest once it's gone (picked or sold). Banana is spent after 4 harvests and papaya after 8: written off to 5600 Loss, after which the acre can be replanted.
   - Hands with an order on the field, and labourers on a field with no order, pick what can go each hour. Only the player sells to the contractor.
   - *Collect all* picks what fits the store, and its *sell the rest* card offers the trader what's left.
5. **Choices made while coding, shown with US-129.**
   - **Entity:** an orchard is one entity per acre (`OrchardState`, save schema 14's last section), not 400 trees.
   - **A batch:** trees × the tree's batch kg × the acre's average Water, Care and Weather since the last batch (or the planting). It is capped like a crop (`yieldCap`). Spec 003's soil, seed, tools and fertiliser factors don't apply to trees.
   - **When fruit comes:** a batch ripens at the season's start in each fruiting season, and at maturity if that falls in one.
   - **Moisture:** the trees have their own moisture band (`moistureMin`/`moistureMax`: mango 25–70, guava 25–75, banana 50–85, papaya 40–70).
   - **Selling or losing the land:** an orchard goes with its land at book value (Dr Cash, Cr 1700 Trees) when the parcel is sold or seized, like the acre's irrigation.
   - **Machines and buildings:** machines don't work an orchard acre. Houses, businesses and the second shed can't be built on one. The acre's own irrigation stays.
   - **Payback:** returns count each part picked at the trader's price and the contractor's money as paid.
   - **Alerts and notifications:** a ripe batch 12 h from spoiling is an alert that opens the field. While you are away it raises the *fruit is ripe* notification, unless a hand will pick it.

## Consequences

- Spec 013 §3.3, §5 (`PickOrchard`, `SellOrchardOnTrees` and their refusals), §7 and §8 are updated: AC-6 names its tests, and the new AC-13 covers picking in parts and the contractor. The GDD's orchard line is updated too.
- Save schema 14 gains the orchards (a schema-13 save loads with none). New command tags: `PlantOrchard`, `PickOrchard`, `ClearOrchard`, `SellOrchardOnTrees`.
- No formula changes. Yield is spec 003's product with the factors trees have. The share and the contractor's share are data in `content/farming.json`.
- The balancer's mango glut (spec 013 §9) is measured in Sprint 16's balance step.
