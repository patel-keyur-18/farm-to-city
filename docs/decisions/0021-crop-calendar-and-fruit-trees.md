# 0021. Crop calendar by Indian cropping seasons, and fruit trees

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0001 (setting), ADR 0002 (real-time clock, ready to harvest), ADR 0010 (diet plan), ADR 0017 (Phase 1 scope)
- GDD sections: Time and world simulation (Seasons), Land, farming and livestock, Roadmap (Phase 1 scope)

## Context

1. The seasons table described the old compressed clock ("Spring is sowing, Monsoon is the main harvest"). Under ADR 0002, most crops are sown and harvested within a single 4-day season.
2. A new game starts on Spring, Day 1. Strictly real sowing windows would leave a new player with nothing long to plant in their first week.
3. The owner wants fruit trees around the house and farm (apple, banana, mango) that can be harvested and sold.

## Decision

### Crop calendar

Sowing windows follow India's cropping seasons, so every season has something to plant:

| Game season | Cropping season | Phase 1 crops sowable | Later crops |
| --- | --- | --- | --- |
| Spring | Late rabi and early zaid | Radish, potato | |
| Summer | Zaid | Radish, lentils (moong-type pulses), early rice | |
| Monsoon | Kharif | Radish, rice | Maize, cotton, sugarcane |
| Winter | Rabi | Radish, wheat, potato, lentils | Onion |

1. **Radish can be sown all year** (kitchen garden and onboarding crop).
2. **The window applies only to sowing.** A crop keeps growing across a season boundary, and its yield uses the weather spells during its growth.
3. **A crop can't be sown outside its window.** The Sow action is disabled with a *Why?* explanation.
4. **Onboarding's long crop is potato**, because the game starts in Spring.

### Fruit trees

| Tree | First fruit (indicative, data) | Fruiting seasons | Lifespan |
| --- | --- | --- | --- |
| Banana | About 1 week | All | Short. Replant after a set number of harvests |
| Papaya | About 1 week | All | Short |
| Guava | About 2 weeks | Monsoon, Winter | Whole game |
| Mango | About 3–4 weeks | Summer | Whole game |
| Apple | About 3–4 weeks | Monsoon | Whole game. **Hill parcels only**, available from the phase that adds hill terrain |

1. **Slots:**
   - **yard slots** around the house (about 4, with more for bigger houses)
   - **field-edge slots** (about 2 per acre), which don't use field area but cast a small shade penalty on the adjacent crop, shown in *Why?*
2. **Lifecycle:** sapling, then growing (task clock, continues while away), then mature. A mature tree produces one fruit batch per fruiting season. Ripe fruit waits in *ready to harvest*, and workers can pick it under standing orders.
3. **Fruit is a perishable tradeable good.** It can be sold, eaten by the household (diet plan) or stored.
4. **Trees count towards net worth.** The *Why?* panel shows the cost, the returns to date, and the expected payback season.
5. **Whole-acre orchards** (an acre planted solid with trees) come in Phase 2.

## Consequences

- **New definitions:**
  - `CropDef.sowSeasons`
  - `TreeDef` (id, sapling cost, minutes to maturity, fruiting seasons, yield per batch, harvest limit or lifespan, terrain requirement)
  - `TreeSlotDef` (yard and field-edge slot counts, shade penalty)
- **Phase 1 scope:** the Crops row adds banana, papaya, guava and mango in yard and field-edge slots. Apple and whole-acre orchards are out of Phase 1.
- **Specs:** spec 003 (crops and yield) covers trees. Spec 002 (land and soil) covers the slots.
