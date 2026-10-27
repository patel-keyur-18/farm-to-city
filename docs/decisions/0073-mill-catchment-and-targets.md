# 0073. The flour mill's catchment and targets from the bots

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Supersedes: ADR 0072's target ranges (its reasoning stands)
- Amends: spec 016 §3.5(b), §9, §10 G5; content `businesses.json` (`catchmentPeople`)
- Related: ADR 0068 (spec 016), ADR 0072

## Context

US-117's business bot (`MillBot`, `Gate.Mill`: 8 seeds, 3 years) measured the mill at spec 016's catchment of 260 people plus the town:

| Mill | Measured | ADR 0072 |
| --- | --- | --- |
| Hired | −3,800 to −2,200 a season (little of the bot's own wheat) | 0–8,000 |
| Run by you | 25,800–29,400, payback 3.8–4.3 | 25,000–35,000, payback 3–6 |

The plan's new line, *Your hours there*, showed the second problem: run by you, the mill takes 8 of your hours a day, worth about 37,000 a season as day labour, so running it yourself paid less than labouring, and a hired mill lost money. Neither is a reason to found one.

The owner chose a busier mill (2026-10-27). At 400 people the bots measured hired 12,000–13,700 and run by you 37,200–42,900, more than estimated, with the area's grain crowding out the player's own wheat. The owner then chose 350.

## Decision

`catchmentPeople` 350 (plus your town): about 3,200 kg of the area's grain a real day, inside the mill's 3,800 with room for 200 kg of your own wheat. Measured at 350:

| Metric | Target (measured) |
| --- | --- |
| Hired, 200 kg a day of your wheat | 5,000–10,000 a season (6,400–8,000; payback about 15 seasons) |
| Hired, on a business loan | 5,000–10,000 (8,200–10,000) |
| Run by you | 30,000–40,000 a season, payback 3–4 seasons (33,700–37,400; payback 3.0–3.3), about what your hours earn as day labour |
| Milling market wheat | 1–2 a kg (unchanged) |

The plan never projects more grinding than the mill's day: your wheat fills only what the area leaves (a fix found with this measurement).

## Consequences

- `GateTests.Mill` prints these ranges; never stuck and the overdraft never permanent are asserted.
- A hired mill now earns a real, modest profit; running it yourself earns about a labourer's pay plus a return on the money put in, and the plan shows both, with what your hours are worth.
- Run by you while borrowing for houses and cattle, the bot was foreclosed on the farm's loans in 1 of 8 runs (the competent bot: 0 of 8): the farm hours it costs are visible in the plan.
- The Village's "a business profitable for 2 seasons" (spec 018) is met either way.
