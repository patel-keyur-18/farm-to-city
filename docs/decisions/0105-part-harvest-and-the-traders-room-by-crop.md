# 0105. A ready field crop can be harvested in parts, and the Store lists what the trader still takes of this season's crops

- Status: Accepted (owner request, 2026-11-12)
- Date: 2026-11-12
- Deciders: Keyur Patel
- Amends: spec 003 (the harvest, §3.8 and the field sheet), spec 004 §6 (*Harvest when the store is full*), spec 008 §3.9 (Collect all), spec 011 §7 (the Store's truck line)
- Related: ADR 0034 (a ready crop waits 72 h, then spoils), 0054 (the truck), 0090 §4 (orchards are picked in parts), 0041 and 0047 (saves: a schema per change, a fixture each)

## Context

1. **Harvest was all or nothing.** `Harvest` with `Overflow.StoreOnly` is rejected with `StoreFull` unless the whole crop fits. With a store 90% full and a 1,000 kg field ready, the player could not take even the 100 kg that would fit, and the crop waited (and after 72 h began to spoil). The only way out was *Store what fits, sell the rest*, which sells to the trader (not always wanted, and not at all while the truck is away). Orchards already pick in parts (ADR 0090 §4); field crops did not.
2. **The Store showed the truck's load only.** "Here till midnight · 4,000 kg room" says nothing about each crop's own daily room (`Region.TraderBuyPerDay`), so the player could not see in advance how much of the wheat, rice or radish the trader would still take today. The good's own sheet had it ("The trader can still take 2,000 kg"), one tap per crop.

## Decision

1. **A new overflow mode `Overflow.StorePart`.** `Harvest` with `StorePart` takes what fits the store (`Store.Fits`) now and leaves the rest standing in the field, **ready**; with all of it fitting it is the plain harvest (same state, same hash); with nothing fitting it is rejected with `StoreFull`. The crop keeps `TakenGrams` (the grams already in the store), so `HarvestGrams = yield − spoiled − taken`; the rest spoils on the same 72 h grace as before (what was taken is not counted as spoiled). The field stays occupied and the sowing-run, saved seed and era counters (`Progress.EraRules.OnHarvest`) are settled **once, on the last part**. Each part emits `CropHarvested` for its grams and gives its straw. Only the player's **Harvest** button and **Collect all** use `StorePart`.
2. **Nothing else changes.** Hands' orders, labourers and the bots keep `StoreOnly` or `SellRestToTrader` exactly as before, so the economy, the bots' numbers and the golden replays are untouched; the hand's waiting order ("the store is full and the truck is back at 6:00") is as it was. *Store what fits, sell the rest* stays beside it when the whole crop doesn't fit.
3. **The field sheet** shows the button as "Harvest · 40 kg of 1,056 kg" when only part fits (the plain kg when all of it does), a line *Already in your store: 40 kg* once a part is taken, and the *Why?* of the yield a row *Already taken into the store*. With no room the button is greyed with "The store is full."
4. **Save schema 17**: `Crop.TakenGrams` is written only when saving at schema ≥ 17 and hashed only when not 0 (every other crop hashes as before); a schema-16 save reads with 0. A fixture `schema17.ftc` and a migration test (`Schema16_PartHarvest`).
5. **The Store lists the trader's room by crop.** Under *Trader's truck*, while it is here: *The trader still takes today*, one row for each crop **sown this season** (the crop calendar's `SowSeasons`, fodder left out), "N kg more" or "Full for today": the smaller of the crop's daily room and the truck's room left (`MarketRules.TraderCapacityLeft`, the same number the sell button uses). Crops not sown in the season are not listed; while the truck is away the rows are not shown ("Back at 6:00"). Each row has a *Why?* (the day's room, bought today, the truck's room).

## Consequences

- Tests: `PartHarvestTests` (4: takes what fits and leaves the rest then finishes; all-fits equals the ordinary harvest by hash; the rest spoils as usual and taken is not spoiled; saves and reads), `MigrationTests.Schema16_PartHarvest`, `TruckHarvestViewTests` (4: the season's crops only, selling uses room, away shows none; the button and the field's lines; the plain button when all fits; Collect all takes parts), and two existing view tests updated (`SheetTests` for the new button, the Why coverage test).
- Docs: specs 003, 004, 008 and 011 carry an *As built* line; checklist `s18` checks 40–43; captures `docs/art/s18-truck-room.png`, `s18-harvest-part-*.png`.
- Open: hands still wait when the whole crop doesn't fit; letting them take parts too is a balance question (their time, the 72 h grace) and is left for the owner's call.
