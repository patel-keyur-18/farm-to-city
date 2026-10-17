# 0010. Founder labour and daily routine

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Amends: ADR 0003 (the founder gains a minimal body state)
- Related: ADR 0008 (living costs paid upfront per season), ADR 0009 (day labour, subsistence)
- GDD sections: Player journey (the founder), Land and farming (Care), Economy (household spending), Data model

## Context

Yield depends on Care = labour delivered ÷ labour needed. In the Homestead era there are no workers, so the GDD didn't say where labour comes from (review gap #9).

The owner also wants the founder to live a realistic routine: food, clothes and other expenses. What the founder eats and how much they rest should decide how much work they deliver.

## Options

1. **Tap = labour.** Tapping sow, tend and harvest delivers labour. Care depends on tending tasks during growth. Rejected: tasks that pop up while the player is away push constant check-ins, which breaks "calm".
2. **The founder is the first worker**, with a labour capacity applied to their own fields in the background.

## Decision

Option 2, with a founder routine. Two guardrails apply:

- **Not an energy bar (pillar 4).**
  - Stamina only changes how much work the founder delivers in the background.
  - It never blocks a player action: sowing, harvesting, building and menus always work.
  - It can never be bought.
- **No chores.** The household runs on a plan the player sets once, paid upfront each season. There is no daily feeding.

### Founder as first worker

- The founder delivers labour-hours to the founder's own fields automatically, like a worker on standing orders.
- The player still taps to sow and to harvest.
- The base capacity is sized to fully tend about 2–3 acres. Beyond that, Care falls below 1, which is the natural reason to hire.
- The player splits the founder's labour between their own fields and **day labour** for neighbours (ADR 0009). Day labour earns cash and takes care away from the farm.

### Founder household

The founder household uses **the same household model as citizen households**: a spending plan across food, housing, clothing, education, health, leisure and savings. It's paid upfront each season as living costs (ADR 0008).

**Diet plan, which sets nutrition (values in data):**

| Plan | Cost | Nutrition |
| --- | --- | --- |
| Subsistence (automatic when broke, ADR 0009) | Lowest | 0.80 (floor) |
| Basic | Low | 0.90 |
| Balanced | Medium | 1.00 |
| Rich | High | 1.05 (cap) |

**Eat from own harvest.** The household can eat from the founder's own storage first. That reduces cash spending, but the goods are not sold.

### Working hours and rest, which set fatigue

| Hours | Effect |
| --- | --- |
| Short day (6 h) | Fatigue recovers |
| Normal (8 h) | Fatigue steady |
| Long day (10 h) | Fatigue builds over the following days |

- Rest days clear fatigue.
- Housing quality changes how much rest recovers: a hut recovers less than a house.
- While the player is away, the founder always works normal hours.

### Health

- Health falls with poor nutrition, sustained fatigue, unsafe water, and (in Winter) underfunded clothing.
- Low health brings a risk of sick days, when the founder delivers no labour.
- Medical spending speeds recovery.

### Formula

```
FounderLabourHours(day) = Hours × Nutrition × (1 − Fatigue) × Health
```

- Each factor is bounded, and every bound lives in data.
- The founder panel shows the *Why?* breakdown.
- This is the citizen productivity formula without skills and traits.

## Consequences

- **ADR 0003 is amended.** The founder still has no age, skills or traits, but now has `nutrition`, `fatigue` and `health` state.
- **New definitions:**
  - `DietPlanDef`: cost per season and nutrition
  - `WorkHoursDef`: hours and the fatigue change
  - `FounderDef`: base capacity, and fatigue and health parameters
- **New state:** `Founder` (diet plan, work hours, labour split, nutrition, fatigue, health, credit score).
- The nutrition floor of 0.80 keeps ADR 0009's never-stuck health check reachable.
- **The crops-and-yield spec** must define labour-hours needed per acre per cycle, on a per-season realism basis (ADR 0008).
- **The founder spec** (part of land and soil, or its own spec) must define the fatigue and health curves.
- **Lessons this teaches:**
  - cheap food saves cash but costs output
  - overwork wins today and loses this week
  - a better house pays back in productivity
  - your own labour is your first asset
