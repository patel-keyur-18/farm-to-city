# 0063. Spec 014: citizens, households and population

- Status: Accepted
- Date: 2026-10-23
- Deciders: Keyur Patel
- Amends: spec 006 (a hand is a citizen; retirement; a family house as part of his pay; labourers from the town first), spec 002 (the family house), spec 004 (the town's food demand), spec 005 (4200 Rent received), spec 007 (save schema 8), spec 008 (population and happiness on the HUD; the Town tab)
- Related: ADR 0002 (the life clock), 0003 (the founder doesn't age), 0022 (the first citizens are hands), 0057 (spec order), 0061 (the village is the town)
- GDD sections: Citizens and workforce; Economy (Household spending); Community and civic services (Needs layers)

## Context

Spec 014 makes the town a place of real families. Its ten gaps were closed with the owner one at a time (2026-10-22 and 2026-10-23), and a final review checked that every path is achievable and makes sense to an average player.

## Decision

1. **Who counts (G1, ADR 0061):** people living on the founder's land, in a hand's hut or a family house ("Your people"); the village's old families stay neighbours.
2. **Homes (G2):** a family house from the Hamlet, 60,000, 48 h, 6 people, 4 an acre (the acre stops farming); rent 6,000 × index a season.
3. **The life clock (G3):** 1 life-year a season; child 0–17, adult 18–59, elder 60+; a hand retires at 60; births 0.10 a season for a couple (at most 3); deaths only for elders (1.5% a year at 60, doubling every 8 years; about 72 years).
4. **The budget (G4):** set at each close from the season's income by the Engel curve; food need 4,500 an adult a season; savings cover a short season first.
5. **Needs and happiness (G5):** survival 40%, security 30%, belonging 20%, growth 10%, each gated by the layer below at 0.8.
6. **Migration (G6):** a family moves into an empty house every 18 ± 6 h, live or away; unhappy (below 35 for 2 closes) or rent-behind (2 seasons) households leave. **The owner's change:** a hand's family in a family house is housing in kind: his cash wage is 6,000 × index less from his next renewal, a loyal hand's kept wage included, never mid-season.
7. **Death and tone (G7):** only elders die, worded gently, information only (no sound or push); no child or young adult deaths, accidents or funeral costs.
8. **Work for everyone (G8):** adults without a town job work outside at the founder's day-labour rate on half the days (paid as the expected share, no daily roll); the founder's day labourers come from the town first; the employment rate counts town jobs only.
9. **The family tree (G9):** three levels (Town tab rows, the household's small tree, the citizen's close family with links across houses); the departed kept only while a living spouse, child or grandchild links to them.
10. **Food bought locally (G10):** the town's basket adds to the local market's demand (a town of 80 doubles it), the price stays under the import ceiling, the shops never run out.
11. **Every path achievable (§3.13):** a free house is offered to a hand living alone first; a retired hand alone goes home and frees his hut; a dismissed hand's family stays as tenants; an acre with a house can't be sold; foreclosure takes houses only on bought land; an elder alone behind on rent goes to family; a hand alone in his hut is a household (the Village's 5 households cost about 1,80,000); every needs row names its lever or says what isn't possible yet; cautious and random bots must reach 5 households in the Village pacing.

## Consequences

- Spec 014 approved; coded in Sprint 13 (F11.1–F11.3 broken into stories, ADR 0057).
- Save schema 9 (ADR 0064 took 8) migrates hands into households of one.
- New balance gates: population at the Village unlock, town happiness, households leaving, rent's share of income, every bot reaching 5 households.
