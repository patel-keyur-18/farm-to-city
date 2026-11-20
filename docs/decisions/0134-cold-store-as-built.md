# 0134. The cold store as built: space rented like the warehouse's, potato and fruit at a tenth and a quarter of their spoilage, and an honest "is it worth it?"

- Status: Accepted (owner approved Preview 7, with the five recommendations, 2026-11-19)
- Date: 2026-11-19
- Deciders: Keyur Patel
- Amends: spec 025 §3.6, ADR 0107 §9
- Related: ADR 0087, 0101 §1 and §12, 0103, 0125 (G8), 0130; spec 013 §3.4, spec 025 §3.6

## Context

Spec 025 §3.6 (ADR 0107 §9, ADR 0125 G8) gives the Township a cold store in the mandi town: space rented by the tonne for a season at 1.00 a kg, upfront at the close and in *Next season, already due*, keeping potato and seed potato at a tenth of their spoilage and fruit at a quarter. Preview 7 showed the Store's button, the sheet before and after renting, its two *Why?* pages and the close's due line.

## Decision

1. **Rented like the warehouse's (ADR 0087).** `RentColdStore(tonnes)` books whole tonnes, 1 to 20: more from now (paid at once for the days left in the season), less from the next close, never below what the goods kept there need (`StockTooHigh`); the close rents the tonnes booked, or what the goods in it need, upfront (`ColdStoreRules.Close`, after the warehouse's), a prepayment expensed through the season, listed in *Next season, already due* (`DueKind.ColdStore`). Unpaid, the space ends and the goods go back to the hut (`ObligationUnpaid`). It is a Tap confirm card with the cost (stopping or cutting spends nothing: no card). From the Township (`TownshipRules.Reached`).
2. **Content, not code:** `township.json` `coldStore`: `rentPerKgSeason` 1.00, `maxRentTonnes` 20, and `goods`: potato 0.1, mango 0.25, guava 0.25 (each good's share of its spoilage rate there). Apples join in Sprint 24 with one more line. **Seed potato is left out** (the owner approved the cut): it spoils 0.1% a day already, so a tonne's rent buys under a kilo saved a season.
3. **What it holds is worked out from the stock** each time (`ColdStoreRules.Cold`), as the godown's `Dry` is: the goods it keeps fill the rented space fastest-spoiling first (guava, mango, potato), then by id. `Store.UsedGrams` counts neither the godown's nor the cold store's goods as in the hut, `Store.Fits` adds the cold store's free room for a good it keeps, and the daily spoilage (`MarketSystem.Spoil`) takes the cold store's share of the rate for what is in it. With no space rented `Cold` is null, so the hourly passes allocate nothing.
4. **The worked answer is the sim's own arithmetic** (`ColdStoreRules.KeptShare`, `BestHold`): for each kept good the best hold of 1 to 3 seasons (a kg kept from one season's price to a later one, less the rent for those seasons and what spoils in the cold, against selling at once), at today's index. Today: potato Spring to Monsoon about +0.19 a kg (2.7% on the money kept), mango Summer to Monsoon about +2.99 (6.4%), guava none (its price falls as it keeps). It is an estimate, never a rule, and says so: a slump loses it, and the deposit pays 1.5% a season with no risk. Each number has a *Why?* (`coldRent`, `coldGood`, `coldWorth`), in words.
5. **Where it is.** A fifth picture button in the Store (`Cold store in town`, from the Township) opens `ColdStoreSheet`, built like the godown's (How goods get here, the rented space and its status, *Rent 1 t more*, *1 t less*, *Stop renting* / *Keep renting*, what goes here with "keeps X of every 100 kg over a season (Y in the hut)", then *Is it worth it?*). The Store's potato row says how much is in the cold store; the godown's "Stay in the hut" note points here. The sheet stays open after a rent.
6. **Art.** `cold_store` in Blender: an insulated white warehouse with a blue band, a steel cold-room door, a compressor on the roof and crates of potatoes; it is the sheet's and the button's picture.

## Consequences

- Save schema 21 gains `ColdRentedTonnes` and `ColdRentNextTonnes` in the godown's state (written from schema 21, hashed once cold space is rented); the schema 21 fixtures were regenerated; old saves read unchanged. `RentColdStore` is a new command tag; an older log reads as before.
- Tests: `ColdStoreTests` (8: from the Township, the close, a tenth of potato's spoilage, the fill order, never below the goods, the due line, the worked answer, the save), `ColdStoreViewTests` (8), `ConfirmTests` and the string coverage. Editor tour by scripted touch: `docs/art/s23-242-*.png`; the render statistics are unchanged (the story draws only UI).
- Balance to read at the sprint-end pass: how often a player rents the cold store at a thin potato margin (the sheet says it is thin), and mango's 6.4% against the deposit's 1.5% a season.
