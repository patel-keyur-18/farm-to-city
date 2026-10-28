# 0079. The bakery's pav demand, starting mix and targets from the bots

- Status: Accepted
- Date: 2026-10-28
- Deciders: Keyur Patel
- Amends: spec 016 §3.9, §3.9c, §9; content `businesses.json` (the bakery's `shops.pav`)
- Related: ADR 0068 (spec 016), ADR 0073 (the mill's targets), ADR 0076 (Sprint 15)

## Context

US-119's business bot (`BusinessBot`, `Gate.Business`, the balancer's `bakery` mode: 16 seeds, 16 seasons, the bakery unlocked with the Hamlet because the Village gate is spec 018's) measured the bakery at spec 016's placeholder of 0.03 kg of pav a person a day of life: hired, about 60,000 a season and payback in 2 seasons; run by you, about 95,000. That is six times the mill's hired figure, and 30 g of pav a person every day is a city habit, not a village's (villages mostly eat rotis).

The bots also showed where a bakery's profit really comes from. Rusk keeps, so the trader's truck takes up to 100 kg a day beyond what the village shops buy, and a hired bakery pays mainly through it. US-118's starting mix (and the plan's) counted only the shops, so at a village-sized pav demand the plan would have shown a hired bakery losing about 8,000 a season when a sensible owner makes about 20,000.

## Decision

1. **Pav:** the village shops buy 0.01 kg a person a day of life (about one pav every three days); rusk stays 0.01.
2. **The starting mix and the plan** are what the shops buy and, for what keeps, what the trader takes as well (rusk: the shops' amount plus 100 kg a day). Settings still offers the shops' amount alone, half, and none.
3. **The plan prices inputs as they'd really cost:** atta at the trader's price when a mill of yours stands, else the market's import price; supplies count with the goods' cost, firewood on its own line (as the books keep them).
4. **Targets** (`GateTests.Bakery`, 8 seeds, 16 seasons, on the median run):

| Bakery | Target | Measured (8 seeds) |
| --- | --- | --- |
| Hired baker | 15,000–25,000 a season, payback 5–8 seasons | 19,800, payback 6.5 |
| Hired, on a business loan | 15,000–25,000 | 19,500 |
| Run by you | 45,000–60,000, payback 2–3 seasons | 53,700, payback 2.4 |
| Never stuck | every run plays on with cash; overdrawn at most 3 closes in a row; hired never foreclosed, run by you in at most a quarter of runs | 0, 2 of 8 run by you |

Beside the mill (hired 5,000–10,000, run by you 30,000–40,000, ADR 0073), the bakery earns more for more money put in and a later era, and a hired one pays mainly by selling beyond the village: the lesson is to make what keeps and sell it further away.

## Consequences

- The bot draws cash from its businesses when the household's cash runs low, not only at a season's start. Without that, a founder running the bakery all day had its profit sitting in the bakery's cash while the farm's loans came due at the close (5 of 8 runs foreclosed with 2–4 lakh between them). With it, 2 of 8; the mill run by you went from 1 of 8 to 0 of 8.
- In one early season the trader's 4-tonne truck can fill with farm produce before the bakery's rusk reaches it; the rusk waits for the next morning. The mill-and-bakery bot measured lower for this reason; it's realistic, not a fault.
- The dairy (US-216) inherits the starting-mix rule: its ghee keeps, so its plan counts the trader's 20 kg a day, capped by the milk it keeps (US-216).
