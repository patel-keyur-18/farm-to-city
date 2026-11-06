# 0093. Sprint 16 balance: the shops' prices, good quality's cost, the vegetable stall at the mandi, a shop on your acre, the canal, drip and the granary

- Status: Accepted (owner, 2026-11-05 and 2026-11-06: each item decided one at a time with the numbers)
- Date: 2026-11-06
- Deciders: Keyur Patel
- Amends: spec 013 §3.1 (drip's power and fertigation), §9 (the canal's, drip's and the granary's targets) and the godown's row in §6; spec 017 §3.3 (reference prices), §3.4 (an acre's distance), §3.5 (good chai's cost), §9 (targets restated and measured), G8 (the stall's stock); ADR 0091 §2 (the retail markup); ADR 0092 §1 (good chai); content `shops.json`, `businesses.json`, `goods.json`, `farming.json`
- Related: ADR 0074 (spec 017), 0091 (shops as built), 0092 (stars and quality), 0073 (the mill's targets)
- GDD sections: Businesses (customer choice)

## Context

Sprint 16's balance step measured spec 017 §9 with the business bots for the first time (the balancer's `shops` and `shopbooks` modes, 8 seeds × 12 seasons, the shops moved to the Hamlet by `Gate.ShopsInHamlet` because the Village gate is spec 018's, Sprint 17). On the approved content:

- The tea stall at the reference of 10 coins lost money hired: the village tea stall serves at 10, so a new stall couldn't charge more, and 0.06 L of milk, tea, sugar and a wage left nothing.
- Good chai never paid at any price. Its +1.20 a cup for fresh snacks cost more than good quality's extra customers brought.
- The sweet shop's reference (240 and 280 a kg) sat at its cost of making: rasgulla and laddoo were priced below their paneer, ghee and sugar.
- The vegetable stall sold only what your store held. The bot (and a real player) sells most of the harvest to the trader, so the stall stood empty most days, and its 1.25 markup over the market priced it out against the grocer.
- Spec 017 §9 expected a shop on an inherited acre to get 25–35% of the village. It assumed the acre is 300–500 m from the square; on the map it is 800–1,200 m (ADR 0091 §3), where the village's share is at the attraction's floor, about 5%.
- Spec 013 §9 was measured with a new farm bot (the balancer's `farm` mode: the competent bot with and without each investment on the same 64 seeds × 16 seasons). The mango orchard and the tractor met their targets; the canal (+4% against +10–25%), drip (payback 16–20 seasons against 4–8) and the granary (wheat loses by waiting) did not.
- Making a good batch could spend more than the shop's cash (`BatchCost` priced held inputs at the market ceiling but the books at cost), and the balancer crashed on a negative cash.

## Decision

1. **Chai's reference price is 12 coins** (owner, 2026-11-05). A cup of chai in a village is 10–15 rupees. The village tea stall charges the reference too.
2. **Good chai has no surcharge** (owner, 2026-11-05). Good chai is full-cream milk, 0.08 L a cup (0.06 standard). The +1.20 for fresh snacks is dropped. §9's tea target is restated as good quality at 10% above the reference: good quality's cost comes back in its price, as it would in a real stall.
3. **The sweet shop's references are rasgulla 300 and atta laddoo 360 a kg** (owner, 2026-11-05), and their base market prices rise in step to 240 and 290 (were 220 and 250). Real halwai prices are 250–400 a kg.
4. **The vegetable stall buys at the mandi** (owner, 2026-11-06). A sabzi-wala buys at the mandi each morning. What your store lacks, the stall buys at the local market's price (*Buy at the market*, on by default; off, it sells only your store). The retail markup is 1.15 (was 1.25): the stall's reference is 15% above the market. Your own produce still goes first, at today's market price as an internal sale (spec 017 G8).
5. **A shop on your own acre keeps the model** (owner, 2026-11-06). Location matters: a stall a kilometre from the square sees few passers-by, which is the lesson ("no rent; fewer pass by"). The place sheet shows the share your nearest free acre would get beside the pitches' share (*Your nearest: about 10% of the village's customers* for the vegetable stall, about 5% for the tea stall), so it is a number before you choose. §9's target is restated as about 5–10% of the village from an inherited acre, plus your own town. An acre shop pays once your town grows round it.
6. **The canal's target is restated to the model's** (owner, 2026-11-06). Measured by the farm bot (the competent bot with and without a channel on a rain-fed acre it farms, 64 seeds × 16 seasons), a canal acre earns about 860 a season more than a rain-fed one (+4%), payback 8 seasons, against spec 013 §9's +10–25% and 2–4. Outside Summer the rain keeps a loam acre's potato, rice and lentils mostly in their moisture band (the water factor at harvest is 0.94–1.00), and the canal is shut in Summer, the dry season. No data change reaches the target: a 150 fee, the canal flowing in Summer, a floor of 55, or all together top out at +6%, payback 5. Forcing it would need a harsher dry-soil penalty, which would move every Phase 1 farm figure. The rules stay; §9 reads +3–8%, payback 5–8; the canal's *Why?* says it mostly saves dry Springs, a small, steady gain for its fee. The pump, which waters through Summer too, earns about 1,440 an acre a season.
7. **Drip runs on the pump's water** (owner, 2026-11-06). Two subsidised drip acres on top of the pump cost about 55,600 and earned about 3,400 a season, a payback of 16–20 seasons against spec 013 §9's 4–8. The pump already holds those acres at 40, inside potato's and lentils' bands, so drip added only fertigation (+5%, on fertilised crops) and half an hour of Care a day, and it charged 300 power a season on top of the pump's 250, though drip really uses less water and power. Drip's power is now 0 (its upkeep, 1% of its cost, stays) and fertigation +15% (the low end of the real 20–40%). Measured: about 5,100 a season, payback 11.2 seasons, about 3 years, realistic for drip on field crops (1–2 years is for vegetables). §9 reads about 12 seasons on field crops; the drip *Why?* says it pays back slowly on field crops and fastest on thirsty crops and fertiliser given through it. A yield bonus of its own (a new rule) was not taken: it would make drip pay where it really doesn't.
8. **The granary's target names the grains that gain by waiting** (owner, 2026-11-06). Spec 013 §9 had wheat stored from Winter to Summer. Wheat's seasonal prices follow the real calendar (Spring 0.95, the valley's April harvest; Winter 1.10, before it), but in the game wheat is sown and ripens in Winter, so it already sells at the year's top and waiting for Summer loses about 9%. Rice (Monsoon 0.95 to Spring 1.05), maize (Monsoon 0.90 to Summer 1.05) and mustard (Winter 0.95 to Summer 1.05) gain by waiting. The prices stay (shifting wheat's would cut the hand's Winter wheat by about 14% and move every Phase 1 check); §9 names those grains, the godown's *Why?* says what gains by waiting and that wheat is sold at its harvest. Measured (`GranaryTests.Rice_Waits_Pays`): 5 t of rice sold to the trader at the Monsoon's end fetched 97,874; kept in your godown and sold in Spring, 1,08,978 (+11,104), against 1,125 of the godown's wear or 3,000 of rent for 5 t over the two seasons. The competent bot, which grows about 3.5 t of rice a year, only breaks even on a godown (−1,689 a season of farm profit, the 45,000 out of its farm's cash): a godown pays on 5 t or more.
9. **A batch never spends more than the shop has** (a fix found in the balance step). `BatchCost` prices held inputs at their book cost and bought ones at the market ceiling, plus good sweets' 8%; `Make` trims the batch until it can pay.

## Measured (8 seeds × 12 seasons, median profit a season)

| Bot | Target (spec 017 §9) | Measured |
| --- | --- | --- |
| Tea stall on a pitch, hired, good, 10% above | 5,000–12,000 | 8,580 |
| Tea stall, hired, standard at the reference | — | 7,342 |
| Tea stall, hired, standard, 10% above | — | 12,757 (overpricing pays until US-122's newcomers, Sprint 17) |
| Tea stall, run by you | — | 21,770 |
| Tea stall on your acre, hired | — | −11,880 (about 5% of the village) |
| Sweet shop, hired | 8,000–15,000 | 11,773 |
| Sweet shop, hired, good, 10% above | — | 11,372 |
| Sweet shop beside your dairy | 8,000–15,000; the milk worth more as sweets | about 10,000 once running (the bot founds it in season 10; 3,071 over 12 seasons counting the build) |
| Vegetable stall, hired | 15% above the market, less the wage | 3,714, payback 5.2 seasons |
| Vegetable stall, run by you | — | 17,609, payback 1.0, no foreclosures |
| Share on the square at equal terms | about 50% | 51% |
| Share from an inherited acre | about 5–10% of the village (restated) | tea about 5%, vegetables about 10% (the place sheet, in the Editor) |
| Flour mill with stars (ADR 0092 §2), hired | 5,000–10,000 (ADR 0073) | 6,070 |
| Flour mill with stars, run by you | 30,000–40,000, payback 3–4 | about 36,450, payback 3.1 |

| Canal acre against rain-fed (farm bot, 64 seeds × 16 seasons) | +3–8%, payback 5–8 (restated) | +4%, payback 8 |
| Pump on the well, 2 more acres | — | about 1,440 an acre a season, payback 5.5 |
| Drip on 2 subsidised acres against the pump | about 12 seasons (restated) | payback 11.2 (16.6 before) |
| Mango orchard against crops on the acre | behind about 6 seasons, ahead within 12 | behind 8 seasons, ahead from season 8 |
| Tractor on a 6-acre farm, bought against hired | hiring cheaper below about 10 acres | hiring +3,767 a season; buying affordable in 13 of 64 runs |
| 5 t of rice kept in your godown from the Monsoon to Spring | more than its spoilage and the granary's cost | +11,104 against 1,125 wear or 3,000 rent |

`GateTests.Shops` holds the tea stall's and the sweet shop's targets. The farm figures come from the balancer's `farm` mode (`FarmToolBot`; drip and the tractor moved to the Hamlet for the run, `FTC_FARM_EDIT` tries a variant of `farming.json`). The newcomers metric (0–1 of each kind by the Township) waits for US-122 in Sprint 17.

## Consequences

- Spec 017 §3.3, §3.4, §3.5, §9 and G8 carry the new values and as-built notes; ADR 0091 §2 and ADR 0092 §1 point here; the GDD's customer-choice paragraph says what was balanced.
- Content: `shops.json` (chai `reference` 12, sweets `references` 300 and 360, `retailMarkup` 1.15), `businesses.json` (good chai `milkLitres` 0.08, no `supplies`), `goods.json` (rasgulla 240, atta laddoo 290).
- The vegetable stall's settings say *Buy at the market*; its *Why?* texts name the mandi.
- Tests: `ShopTests.Mandi`, `GateTests.Shops`, `BusinessViewTests.ShopPlace_AcreShare`, `GranaryTests.Rice_Waits_Pays`.
- US-122's newcomers must stop overpricing from paying (a stall 10% above the reference earns more than at it until then).
