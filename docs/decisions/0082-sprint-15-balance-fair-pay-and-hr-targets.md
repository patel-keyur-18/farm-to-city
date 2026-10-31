# 0082. Sprint 15 balance: a refused raise loses the worker, a day away, and the businesses' targets with fair pay

- Status: Accepted
- Date: 2026-10-29
- Deciders: Keyur Patel
- Amends: spec 015 §3.5 (a course's days), §3.7 (raise request, sick leave), §9 (measured); spec 016 §9 (the dairy's bounds); content `people.json` (`work.courseDays`, `hrEvents.sickDays`)
- Related: ADR 0069 (spec 015), ADR 0081 (renewals at the current wage), ADR 0079 (the bakery), ADR 0080 (the dairy)

## Context

Sprint 15's balance step measured spec 015 §9 with the bots for the first time (`Gate.Hr`, the balancer's `hr` mode, 16 seeds × 12 seasons). The baseline is Phase 1's output: motivation fixed at 1, no fatigue and, of the HR events, only raise requests, paid, because Phase 1's renewals raised wages with skill.

| Bot | Motivation | Farm profit over the run | Hands stayed (median) |
| --- | --- | --- | --- |
| Phase 1's output | 1.00 | 5,62,680 | — |
| Fair: pays every raise | 1.00 | 5,58,946 (−0.7%) | 5.3 seasons |
| Hires 10% low, refuses every event (rules as approved) | 0.82 | 5,75,061 | 7.7 seasons |

Underpaying earned more than paying fairly, so §9's lesson failed. The approved rules can't make an underpaid worker quit: after two refused raises, only a worker of skill 60+ gets an outside offer, and most hands are skill 40–55.

US-114's competent bot also refused a hand's raise whenever paying would eat into the season's reserve. That held its motivation at 0.95 (below §9's 1.0–1.1) and, with 8 seeds, put the hired bakery on a loan at about 13,500 a season (ADR 0079's 15,000–25,000).

With a 4-day season, the 2 days of sick leave or a course was half a season. Unpaid sick leave gave back half the season's wage, and a course took half the season's work.

## Decision

1. **A worker refused a raise who is still underpaid at the next close leaves instead of renewing,** whatever their skill (`HrRules.Leaves`). This applies to hands and to business workers. The raise card says so ("Refused, they work less keenly and leave at the season's end"), and the hand's contract row and the business's worker row show "Leaving at the season's end". Being paid at least what they ask by the close (a matched offer from elsewhere) clears it. Workers of skill 60+ still also get outside offers as before.
2. **Sick leave and a course take 1 day** (`hrEvents.sickDays` 1, `work.courseDays` 1). Unpaid sick leave gives back a quarter of the season's wage.
3. **The bots answer HR events as a fair employer:** yes to everything they can pay. A refusal now loses the worker, so refusing to protect the reserve no longer saves anything.
4. **The bakery keeps ADR 0079's targets.** On 16 seeds it is inside them: hired 18,240 a season with a payback of 7.1 seasons, and 18,849 on a business loan. US-114's 13,500 came from 8 seeds and from the bot refusing raises. `GateTests.Bakery` and `GateTests.DairyBusiness` now run 16 seeds.
5. **The dairy's bounds widen for fair pay's cost** (owner, 2026-10-29). The hired dairy's payback is 5–10 seasons (was 5–9). Run by you, the farm may be foreclosed in at most 10 of 16 runs (was half). The hired dairy's profit target of 10,000–20,000 stays.

## Measured (16 seeds)

| Check | Target | Measured |
| --- | --- | --- |
| Fair bot: motivation | 1.0–1.1 | 1.000 |
| Fair bot: farm profit against Phase 1's output | within 2% | 5,61,247 vs 5,64,114 (−0.5%) |
| Low-pay bot: wages | saves at least 10% | −21% (10% low and no raises) |
| Low-pay bot: farm profit | below the fair bot's | 5,40,285 (−3.7%) |
| Low-pay bot: hands stay | about 2 seasons | 2.6 seasons (median) |
| HR events | 0–2 a season for 3 workers | 0.19 a worker a season (0.56 for 3) |
| Bakery hired / on a loan | 15,000–25,000, payback 5–8 | 18,240, payback 7.1 / 18,849 |
| Dairy hired | 10,000–20,000, payback 5–10 | 12,138, payback 9.4 |
| Dairy run by you | 35,000–50,000; foreclosed ≤ 10 of 16 | 43,273; 9 of 16 |

## Consequences

- "Pay fairly or lose them" is now a real choice, as ADR 0081 intended. A player who refuses a raise gets one season's notice on screen.
- Hiring low still saves money for a season. The worker either asks for a raise or leaves within about 2 seasons, and replacing them costs a post, empty days and a probation.
- `GateTests.Hr` holds spec 015 §9 on every build. `LowPayBot` is the low-pay stress bot.
- A founder who runs the dairy alongside 6 acres and a herd is stretched: the farm foreclosed in 9 of 16 runs. That is the price of the founder's hours plus fair pay for the hands, and it is recorded here as accepted.
