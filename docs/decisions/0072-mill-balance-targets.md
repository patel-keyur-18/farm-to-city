# 0072. The flour mill's balance targets: realistic, hired or run by you

- Status: Accepted
- Date: 2026-10-25
- Deciders: Keyur Patel
- Amends: spec 016 §9
- Related: ADR 0068 (spec 016)

## Context

Built in US-115 at spec 016's defaults, the plan screen projects a flour mill with a hired miller at about 1,500 a season (payback about 70 seasons) and the same mill run by the founder at about 29,600 (payback about 4). Spec 016 §9 asked 5,000–10,000 a season and payback in 6–12 seasons for the hired mill, two targets that disagree (6–12 seasons on 1,00,000 needs 8,000–17,000). The miller's wage (27,000 a season at year-0 prices) takes most of a village chakki's margin, as in real life, where the owner usually runs it.

## Decision

Keep the economics and retune §9 to what is realistic:

| Metric | Target |
| --- | --- |
| A mill grinding for the area, a hired miller, 200 kg a day of your wheat | 0–8,000 a season (more as more of your own wheat is milled, e.g. 400 kg a day in Winter) |
| The same mill run by you | 25,000–35,000 a season; payback 3–6 seasons; your farm hours less |
| Milling market wheat | 1–2 a kg |

The plan screen shows both ways (hired and run by you), so the lesson is visible before the player commits: a business pays its owner's labour, and hiring it out leaves a thin margin.

## Consequences

- No content value changes; the balance tests (US-117's business bot) check these ranges.
- The Village's "a business profitable for 2 seasons" (spec 018) is met by a run-by-you mill easily, and by a hired one only with your own wheat: spec 018 measures it.
