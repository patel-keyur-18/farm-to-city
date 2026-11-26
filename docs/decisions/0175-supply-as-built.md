# 0175. US-268 today's supply, Automatic or Manual, as built (save schema 25)

- Status: Accepted (Preview 24 approved by the owner, 2026-11-26; the story awaits the owner's review)
- Date: 2026-11-26
- Deciders: Keyur Patel
- Related ADRs: 0172, 0174, 0091, 0093, 0140

## Context

Spec 030 §3.4 and §3.5 and Preview 24: each business shows what it took today, from you and bought, and can be set to take no more than a cap of your produce a day. Two gaps were closed with the owner before the preview: the **restaurant** has no day's amount, so its Manual is **None, All it needs and fixed kg only**; and the *Why?* shows **today's price** with **grams only** in the state (no coins paid kept).

## As built

1. **Counting** (`SupplyRules`, sim). Every gram a business takes from you (`ShopRules.FromStore` and `FromYours`, the mill's own wheat, `MilkIn`, your mill's atta to the bakery in `ToYourBusinesses`) and every gram it buys (`FromMarket`, the mill's market wheat, a recipe's `Batch`, a manager's `BuyAhead`, `Neighbours`) is added to `FromYouToday` or `BoughtToday` for the **supply day**, 6:00 to 6:00. The day turns lazily: the first count or read of a new day starts it, so it does not depend on the order of the morning round and the herd's milking. Yesterday's buying is kept in `BoughtDays` (up to `supplyLookbackDays`, 2) for US-269's order; spec 030's separate "yesterday" arrays are not kept (nothing reads them).
2. **Automatic** is today's rule, untouched: `Allowed` returns the whole need when the business is not Manual, so no existing number moves.
3. **Manual** (`SetSupplyMode`, `SetSupplyCap`). A good's cap is a fixed amount (a step from the content's `supplySteps`, 5, 10, 20, 50 kg) or a share of its day's amount (none, a quarter, half, all: a stall's put out, a recipe's inputs for its mix; a restaurant only none or all). `Allowed` = `min(need, cap − taken from you today)`, applied inside `FromStore`, `FromYours` and `ToYourBusinesses`, so it never takes more than it needs or than your store holds, and one cap covers your store and your other businesses together. What isn't sent stays in your store; the rest is bought if *Buy at the market* is on. Switching to Manual starts every good at all it needs (also when coming back from Automatic).
4. **Who can be set** (`SupplyRules.HasMode`): the vegetable stall, the kirana, the sweet shop, the bakery (its atta from your mill) and the restaurant. The mill, dairy and tea stall have no switch: their *a day* row (wheat or milk) is their cap, now on their Supply tab.
5. **The tab and the sheet** (`SupplySheet`, `Sim.View`): a **Supply** tab on a business's card (the mode row, a row for each good with today's count and its cap, *Change* opening the amounts list; *Buy the rest at the market*, the mill's wheat a day and the tea stall's and dairy's milk a day moved here from Settings). The amounts list marks the one in force; *Choose* sets it and returns to the tab. The hub's row gets "Today: 38 kg from you, 12 kg bought" (milk in litres when that is all a business takes).
6. **Why?s**: `supplyToday` (the good's day, both prices today), `supplyMode`, `supplyCap`, `supplyBusiness` (the hub's line).
7. **Save schema 25** (`SupplyRules.Schema`): per business `SupplyManual`, `SupplyUsed`, `SupplyDay`, the two counters and the cap arrays by good, `BoughtDays`. **Goldens do not move:** the block is hashed only once `SupplyUsed` (Manual or a cap was ever set), the way schemas 22–24 did; a save always writes it. An older save loads on Automatic with nothing counted. Fixtures: `saves/schema25.ftc` (the bot town) and `saves/schema25-supply.ftc` (a Manual stall with a half cap and a fixed 10 kg cap, a bakery with none of your atta, a day of buying kept). Content: optional `supplySteps` and `supplyLookbackDays` in `businesses.json` (defaults as spec 030).

## Differences from Preview 24

- The mill's wheat a day is its own row under the counted wheat, not inside it; the tea stall's and dairy's milk a day likewise.
- Quantities read with one decimal ("5.0 kg a day") as the game writes kg everywhere.

## Tests

Core `SupplyTests` (`Counts`, `AutomaticIsToday`, `ManualCap`, `CapNeverForces`, `Commands`, `ManualStartsAtAll`, `MillCounts`, `BakeryAttaCap`, `RoundTripAndHash`, `CommandsRoundTrip`), `MigrationTests.Schema25_Supply` and `Schema24_LoadsAutomatic`, `GateTests.SupplyExploits` (200 Township towns: 30 random supply commands in zero time move no coin, three mornings of flipping keep every ledger exact); View `SupplyViewTests` (`TabAutomatic`, `ManualAndAmounts`, `RestaurantAmounts`, `MillAndTea`, `HubLineAndWhys`). Editor by scripted touch (captures `docs/art/s28-268-*.png`): the hub's supply line, the Supply tab on Automatic and Manual, the amounts list, a half cap in force, a *Why?*; `SheetLayoutCheck` 78 sheets, 0 problems; render statistics within 10% in all three views.

## Consequences

- US-269 ranks its order by `BoughtOver` (today and the days before).
- `Business.Clone` and the save carry the new arrays; a test business built without `Stock` never touches supply.
