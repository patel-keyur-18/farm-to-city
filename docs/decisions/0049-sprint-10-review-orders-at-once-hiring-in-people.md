# 0049. Every order acts at once, labourers hired from People till the crops are in, people drawn above crops (Sprint 10 review)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0048 §3 (labourers hired from a field); ADR 0040 and ADR 0039 §6 (one action per hand per hour); spec 006 §3.2, §3.8, §5, §8 (AC-3, AC-25)
- Related: ADR 0048 (the hand's choice), spec 008 §3.4 (the map)
- GDD sections: People and labour (Hiring, Standing orders)

## Context

The owner reviewed Sprint 10's first build and reported three problems:

1. People look as if they are under the field near its edges.
2. An order given to a second field isn't taken up. A hand who can care for two acres should work both orders at once.
3. Hiring belongs in the People menu, not on each field.

What the code did:

1. Crop sprites were sorted by tile and people by their screen height. A person near a tile's back edge sorted below their own tile's crop, and near the front edge below the crop of the tile in front, so they disappeared into tall rice.
2. A hand did at most `orderActionsPerHour` = 1 action an hour. The first order used it, so the second field waited until the next sim hour, which can be up to a real hour.
3. ADR 0048 had moved hiring onto a growing field's sheet.

## Decisions

1. **Crops are drawn in their own layer**, above all the land and below everything that stands on it: people, buildings, trees, fences and markers. People are never hidden by a crop.
2. **Every order acts at once.** `orderActionsPerHour` becomes 6, the same as `maxOrders`, so every order a hand holds acts in the same hour and as soon as it is set. What a hand can manage is modelled by his hours in the Care pool (spec 006 §3.1). A hand with more fields than he can tend lowers Care, and with it the yield. The cap stays in content.
3. **Labourers are hired from People, till the crops are in** (the owner chose this over picking a crop in People, and over going back to day hire):
   - **The command:** `HireLabourers(count)`. It is rejected with `NotGrowing` when nothing grows, `NoneAvailable` or `InsufficientFunds`.
   - **Length and pay:** the labourers stay until the readyAt of the **last crop growing now**, rounded up to the hour. They are paid upfront to then, at the same daily wage as before.
   - **Work:** they tend every field.
   - **Harvest:** at each hourly pass, before the hands' orders run, they harvest every ripe field that has **no hand's order**. The harvest goes to the store, with any overflow going to the trader, and each one emits `LabourersHarvested`. A hand's ordered fields stay his.
   - **Leaving:** when their time is up they go home (`LabourersLeft`).
   - **The People sheet:** shows "N labourers till harvest", with the cost and the Care that results. When nothing grows, it says "Sow something first".
   - **The field sheet:** no longer hires.
4. **Saves keep the schema-4 layout.** A neighbour's `tile` stays in the save, always −1 from now on, so towns saved by Sprint 10's first build still load. The command log's `HireLabourers` tag keeps its unused field slot.
5. **Sowing by hand on an ordered field.** When the player changes the order, the player's own sowing now runs first, then the order changes. Otherwise the new order would act at once and take the field.

## Consequences

- Two or more orders given together are all sown at once, and each field is worked on its own.
- Labourers bring in the player's own fields as they ripen, which is the "responsible till harvest" the owner asked for, without picking a field.
- Spec 006: AC-3 is restated, AC-25 is replaced (`DayLabourTests.TillCropsIn`, `PeopleViewTests.PeopleSheet_HiresTillCropsIn`), and `OrderTests.StartsAtOnce_OneActionAnHour_PlayerWins` checks both the default and a content cap of 1.
