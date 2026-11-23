# 0163. US-246 the restaurant as built: a shop that cooks to order, diners from the households' leisure, a dhaba to compete with

- Status: Accepted (the owner closed gaps 21 to 24 before code, 2026-11-23; the story is for the owner's review)
- Date: 2026-11-23
- Deciders: Keyur Patel
- Related ADRs: 0074, 0091, 0092, 0093, 0095, 0096, 0153, 0155, 0161, 0162

## Context

US-246 (spec 028 §3.7, Could, Preview 18): a restaurant from the Township, on a rented pitch or an acre, with two dishes, bought from the store and then the market, with diners from the households' leisure budget, open at lunch and dinner. The spec's numbers were set for a 90-day season (120 portions a day, about 1,000 portions a season). A season here is **4 real days**, so 120 a day is 480 portions and the cook's wage (20,000) could never be paid: the money did not work as written. Four gaps were closed with the owner before code.

## Decisions (as built)

1. **Gap 21, capacity.** A kitchen serves **30 portions an hour, 240 a day** (the town wants about 250 a day with 35 households and the old village): about 960 a season, so the spec's money works as written. The spec's 15 an hour was the 90-day figure.
2. **Gap 22, the cook's support staff.** The 20,000 wage is the **kitchen crew's**: a cook and a kitchen hand, one hire and one wage. The board, the sheets and the *Why?* say so. The plan reads in profit at the usual prices and demand: hired **3,000 to 8,000 a season**, run by you **18,000 to 28,000** (`RestaurantTests.Money`: 25 households, hired about 6,100, run by you about 26,000).
3. **Gap 23, a shop of the kind "meals".** It reuses the shops machinery (price presets, quality, stars, wait, distance, the hourly share, newcomers) rather than its own rules. The **town dhaba** (`shops.json`, 300 m from the square on the district road, standard, 3 stars, 60 portions an hour, fixed terms) is the rival; a newcomer's food cart may open on a price above the usual, as for the other shops. Diners are worked out **only while a restaurant of yours is open**, so a town without one is unchanged.
4. **Gap 24, the menu.** Two dishes, diners split 6 to 4 between the thali (60) and the paratha plate (45); each dish has a switch (a dish off hands its diners to the other, and the last stays); one price setting for the restaurant (the shops' four presets); good quality as the other shops have it; "vegetables" are potatoes. A portion is **cooked as ordered**, so nothing is left over.
5. **Demand from the leisure budget** (`MealsRules.PerDay`): a household earning **20,000 a season or more** sets aside **30%** of its leisure and buys as many portions as that pays for over the season, spread over its 4 days; the old village counts as its households at its own budget. The 30% was 25% in the spec: with the portion's price tied to the budget the demand is a fixed pool of money, and 25% left a hired restaurant at a loss in a town of 20 to 25 households, so the number was tuned (spec §10, ADR 0027). Price moves how many portions that pool buys, not the money: a dearer portion sells fewer, so a price above the usual wins only if the diners stay.
6. **Buying.** Each portion's ingredients come from **your store at the trader's price** (a cash sale to the farm, as for every shop), then **your businesses** (your mill's atta, your dairy's ghee and curd at the trader's price, on terms like any invoice sale), then the **market at the retail price** (the trader's price and the shops' retail markup). The kitchen cooks only what its cash can pay for, at the dearest price. The dish's ingredients were tuned so a portion costs about 19 to 21 at that price: the paratha's ghee is 6 g (it was 20 g, dearer than the plate) and its curd 50 g; fuel and supplies are 4 and 7 for a thali and 3 and 3 for a plate.
7. **Good quality** cooks with finer ingredients, costing **a tenth more** (the sweet shop's rule, ADR 0092); the spec's "+10% on the price" is read as that cost. It wins customers and stars.
8. **Hours.** Two sittings, **11:00 to 15:00 and 18:00 to 22:00**, fixed (like the tuition centre's evening: no hours switch). The break is the hours between; your day there is the 8 open hours, not 11. No night shift (it is not a maker).
9. **Pitch or acre.** On the square's pitch (rent 3,000) or an acre; the building is 30,000 and the equipment 20,000 on either.
10. **Save schema 23** (hashed only when used): `Business.DishesOff`; the by-kind customer counts grow from four to five slots with the meals' slot and the dhaba's figures in a section written only once the restaurant has served, so every earlier hash and save is unchanged. Fixtures regenerated; the growth fixture holds a restaurant with its thali switched off.
11. **Screens** (Preview 18): the research (households that eat out, portions they'd want, each dish, the kitchen, the sittings, the dhaba and your expected share), the plan and books, Today (each dish's price and the diners' part, the kitchen's rate), Settings (the sittings and a switch for each dish), the *Why?* on every number (`meals`). Art: an eating house with a striped awning for the acre, a kiosk with a handi and thalis for the pitch, a food cart for the newcomer, drawn in Blender.

## Consequences

- Balance pass: capacity and the leisure share are the two numbers to read from the measured diners (`RestaurantTests.Diners`: a Township bot's town at 40 seasons has about 35 households that can eat out, 200 portions a day, and the old village 165); a town of fewer than about 20 households leaves a hired restaurant near zero.
- Exploit bots: the restaurant buys from your own businesses at the trader's price (no free goods) and the leisure pool is fixed, so a higher price sells fewer portions for the same money.
