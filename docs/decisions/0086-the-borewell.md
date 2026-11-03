# 0086. The borewell: on your own acre, 4 acres you choose, like the well's pump

- Status: Accepted (owner, 2026-11-03)
- Date: 2026-11-03
- Deciders: Keyur Patel
- Amends: ADR 0075 §2 (G2: the borewell waters 4 more acres, not 6); spec 013 §3.1 (the borewell's rules), §5 (its commands), §8 (AC-10), §10 G2
- Related: ADR 0032 (the well acres), 0075 (the irrigation ladder), 0085 (dry soil dries slower)
- GDD sections: Farming (the irrigation ladder)

## Context

Before Sprint 16 the owner asked whether wells could water fields through Summer and heatwaves, one well for 4 acres, as most villages have. Spec 013 already plans this: a pump on the inherited well (Hamlet, 15,000) holds 4 acres at moisture 40 all year. For more acres it plans a borewell (Village, 30,000, 60 seasons) that "waters 6 more", but says nothing about where it stands, which acres it waters, how it waters them or what it costs to run, and no command, screen or test names it.

The owner has 10 acres (6 inherited, 4 bought) and asked how the borewell works.

## Options

1. **Several open wells from the Hamlet, one for each 4 acres.** Realistic, but a new rule, and it would leave the Village's borewell little to do. Not chosen; the pump and the canal cover a 6-acre farm's dry months until the Village.
2. **The borewell as spec 013 drafted it (6 acres), with its details filled in.**
3. **The borewell with its details filled in, watering 4 acres like the well's pump (chosen by the owner).**

## Decision

1. **Era and cost unchanged:** from the Village, 30,000, depreciated over 60 seasons (Dr Buildings at the build, depreciation at each close, as the pump).
2. **Where it stands:** on any acre you own, drawn as a small pump room at the field's edge; the acre is still farmed.
3. **Which acres it waters:** any **4** acres you own, chosen by you, as the well's pump (`borewellAcres` 4). With both, 8 acres are pumped.
4. **How it waters:** like the pump, it holds the soil at moisture 40 or more all year (Summer and heatwaves included) by flooding, with no bucket labour, and it can feed drip lines.
5. **Running cost:** power 250 a season for each acre watered, paid at the close, as the pump.
6. **Commands and test:** `DrillBorewell` (tile; `NotOwned`, `EraLocked`, `AlreadyBuilt`, `InsufficientFunds`) and `SetBorewellAcres` (tiles; `TooManyAcres`); AC-10 `IrrigationTests.Borewell`.

## Consequences

- No formula changes: the borewell uses the pump's floor, power and depreciation. A watered acre earns a little more in dry spells, as the owner accepted; spec 013 §9 measures it.
- Drip beyond the well's 4 acres needs the borewell, as before (AC-2), now on its 4 acres.
- US-127 gains the borewell's screen and test; its points are checked at Sprint 16's start (ADR 0075 already allowed for it).
- Land payback (ADR 0033) is already short of its 8–15 seasons on `main` (5.4); pumped acres shorten it a little more. Sprint 16 reports the figure; land prices are not changed here.
