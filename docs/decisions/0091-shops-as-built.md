# 0091. Shops as built: the tea stall's milk, your products at the stall, the mill's distance, and choices made while coding

- Status: Accepted (owner, 2026-11-05: §1–§3 decided; §4 approved with US-120)
- Date: 2026-11-05
- Deciders: Keyur Patel
- Amends: spec 017 §3.1, §3.2, §3.4, §3.6, §4, §5, §7; spec 016 §3.5(b) (the catchment, ADR 0073's 350 to 375)
- Related: ADR 0074 (spec 017), 0073 (the mill's targets), 0080 (the dairy's neighbours' milk), 0068 (businesses)
- GDD sections: Businesses (customer choice, competition)

## Context

Coding US-120 turned up three gaps in spec 017. They were settled with the owner one at a time. A few smaller choices were made while coding and are listed for review.

## Decisions

1. **The tea stall's milk** (owner, 2026-11-05). The spec said a cup takes 0.06 L of milk but not where the milk comes from.
   - At the 6:00 milking the tea stall takes your herd's milk first, at the co-op's price, up to its milk a day. It comes before your dairy and the co-op.
   - Then it buys neighbours' milk at the co-op's price + 2, as the dairy does (ADR 0080). *Buy neighbours' milk and snacks* turns this off.
   - The snack (rusk or pav) comes from your bakery at the trader's price, otherwise at the market's import price.
2. **Your products at the vegetable stall** (owner, 2026-11-05). The spec gave no demand for the stall's products. A separate demand would have counted the same people twice.
   - The village shops' daily buying of pav, paneer and curd (spec 016 §3.9c) becomes retail demand. Your stall and the grocer share it by the attraction score, at the trader's price × the retail markup (1.25; 1.15 from Sprint 16's balance step, ADR 0093).
   - At 6:00 the stall takes its products from your bakery and dairy before the village shops buy. The village shops then buy that much less.
   - Atta isn't sold at the stall: the area grinds its own wheat, so retail atta would count that demand twice.
3. **The mill's distance** (owner, 2026-11-05). The spec measured the mill's distance from the square and assumed an inherited acre is 300–500 m away. On the map they are 800–1,200 m away. Past about 1 km, every shop is at the attraction's floor, as the chakki is, so a mill there got only 50–80% of the area at the chakki's fee.
   - The area's farm families live round your land. So the mill's distance runs from the middle of your houses, or your yard before you have any.
   - The chakki is 1.5 km from there.
   - At the chakki's fee, a mill on your land gets about 90–95% of the area. Every coin above takes a smooth share away: fee 5 about 80%, fee 6 about 50%.
   - The catchment rises from 350 to 375 people (owner, 2026-11-05), so the mill grinds about as much as before the share (350 ÷ 0.93). Measured by the business bots (8 seeds): hired, a median 5,901 a season (4,919 at 350); run by you about 36,000, payback 3.1. Both are inside ADR 0073's ranges.
   - The shops keep the spec's distances: the village from the square, your town from the middle of your houses.
4. **Choices made while coding, shown with US-120.**
   - **Pitches:** the square and its pitch count are in `content/shops.json` (`square`: the banyan, `pitches`: 3), not as three map places on one tile. A shop on a pitch stands on the banyan's tile but isn't "on" it, so the acre rules don't apply. Its rent is paid upfront when it opens and at each close.
   - **Vegetables:** a person's 0.20 kg a day is shared between the goods by `vegShares` in `shops.json` (potato 30% … coriander 2%).
   - **Sweets:** a person's sweets are shared evenly between rasgulla and atta laddoo. Their sugar is the recipes' `supplies` in coins, not a good of its own; the tea and sugar for chai are the cup's `supplies`.
   - **Snacks:** half of all cups (`snackShare` 0.5) are sold with a 50 g snack at the snack's price.
   - **Leftovers:** what a stall hasn't sold stays on the stall for the next day, spoiling at its rate; the morning only tops it up to the day's amount. The spec had it go back to the store.
   - **A new stall's first day** (owner, 2026-11-05, from the Editor tour): a stall gets its goods at the 6:00 round, so one opened at 14:00 stood "Open now" with no milk till the next morning. Now a stall that opens, or is first staffed, after 6:00 and before it shuts takes its stock that hour, once a day, by the morning's rules; the tea stall buys neighbours' milk up to its milk a day (your herd's went to the co-op at 6:00). `Business.StockedDay` in schema 14. The dairy and the bakery still start at the next 6:00.
   - **Prices:** a shop's price is one of four presets: the reference, 10% below, 10% or 20% above (`pricePresets`).
   - **The town's buying:** what your town buys at your stalls yesterday comes off its buying at the local market today.
   - **Not yet:** quality and stars stay at standard and 2.5 until US-121. Newcomers and the rivals' 5% price cut come with US-122. Customers are drawn at a pitch shop's counter while it serves.
   - **Rasgulla and atta laddoo** are produce goods: the trader buys none of either, and the market has a small demand for each.
   - **The plan:** a shop's plan reckons on a pitch in the square, against the rivals only.
   - **The shops' first figures:** the plan at today's prices, before the balance step, is below.

     | Shop | Profit a season | Spec 017 §9's target |
     | --- | --- | --- |
     | Tea stall (pitch, hired) | about 3,000 | 5,000–12,000 |
     | Sweet shop (pitch, hired) | about −11,000 | 8,000–15,000 |
     | Vegetable stall (all your produce, hired) | about 23,600 | — |

     Tuning these is the sprint's bot balance step (spec 017 §9).

## Consequences

- Spec 017 §3.1, §3.2, §3.4, §3.6, §4, §5 and §7 are updated, and the GDD's businesses line with them.
- Save schema 14 gains the shops on `Business` (pitch, price, quality, stars, goods a day, the hour's and day's figures) and `ShopsState` (the rivals by id, the day's lost customers, your town's buying at your stalls) as its last section. A schema-13 save loads with the rivals and no shops of yours. New command tags: `FoundOnPitch`, `SetShop`, `SetStallGoods`.
- The mill's area grinding is a share (`BusinessRules.AreaGramsThisHour`) instead of spec 016's cliff, with `catchmentPeople` 375 (was 350, ADR 0073). Spec 016 §3.5(b) and its targets table say so.
