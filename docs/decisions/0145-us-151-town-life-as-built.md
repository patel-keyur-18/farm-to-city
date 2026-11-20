# 0145. US-151 as built: reputation, crime and inequality

- Status: Accepted (owner approved Preview 14 and the story's rules, 2026-11-20; the story is for the owner's review)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Amends: spec 026 §3.5 and §3.6 (inequality counts income a person); ADR 0143 §4 (the zero-tax test)
- Related ADRs: 0127 (spec 026's gaps), 0143, 0144

## Context

US-151 reads the town at each close from the Township: the five reputation scores, crime and inequality, and what they do (the pull on migration, the grants' bonus, the Safety part, a land-value term, happiness and patience). Building it against the bots' Townships found two things the spec did not say.

## Decisions

1. **Inequality is income a person, among earning households.** Spec 026 §3.6 read "your households' season incomes". Measured in the bots' Townships, a household's income follows how many adults it has, and a retired couple earns nothing, so the richest tenth over the poorest tenth was capped at 10 in five of six towns. The ratio is now each household's income divided by its members, for households with someone of working age; a retired household is left out of the ratio and feels no effect. Measured: 2.5 to 3.0, the spec's "typical ratio of 3". *Why:* a family of five is not richer than a family of two for earning more in all.
2. **`crimeBase` stays 20.** Bot Townships measured a median crime of 49 two game years after the unlock (range 40 to 100: busy towns with 21 to 28% of adults without a town job run higher), against spec 026's target of about 50.
3. **A town not yet read plays as before.** Every effect (the pull, the grants' bonus, the Safety part, the land-value term, inequality's happiness) is off until the first close from the Township has read the town, so a town is unchanged until then and the first reading moves land value at the next close once.
4. **Reputation reads high in the bots' Townships** (a mean of about 77, a pull of 113 to 114: families about 14% sooner, the grants 5% higher), against spec 026 §9's "a town left alone 40 to 60 ±10". It is measured with built towns, not a hands-off one; the 200-seed hands-off Township in this sprint's balance pass decides whether the weights move.
5. **Strings and screens.** The tab is **Town life** on the town treasury's sheet (the owner's "Town hall"); its rows follow Preview 14. The *Why?* of a score lists its three parts, each with its weight; a part's contribution is shown in points. The crime overlay is lit once the town is read (a terracotta ramp, five steps 20 / 35 / 50 / 65) and its card says the town's crime and what the watchman takes off there. `township.json` carries the block (`townLife`) rather than a new file.
6. **The zero-tax test.** `TaxGateTests.EveryTaxAtZero` expected the zero-tax town to be less happy than the taxed one at the median. With reputation drawing families the two tie (79.3 and 79.3); the assertion is now "no happier", the services under 30% still assert the cost of no tax (ADR 0143 §4).

## Consequences

- Save schema 22 begins (`TownLifeState` inside `TownshipState`; hashed once a close has read the town); the fixture `schema22.ftc` is regenerated at the sprint's end.
- `TownLifeGateTests.Measure` prints the bots' numbers; the balance pass sets bands from it.
- Tests: `TownLifeTests` (11), `TownLifeViewTests` (4); Core 772+11, View 349.
