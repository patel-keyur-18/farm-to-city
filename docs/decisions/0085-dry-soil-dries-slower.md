# 0085. Dry soil dries slower: rain-fed acres stop falling to nothing in a dry Spring or Summer

- Status: Accepted (owner, 2026-11-02)
- Date: 2026-11-02
- Deciders: Keyur Patel
- Amends: spec 002 §3.2 (the drying step), §4 (`LandDef.dryingSlowsBelow`; sun and heatwave drying), §8 (AC-3's example; AC-17, AC-18 added), §9 (the dry-season hook)
- Related: ADR 0042 and 0043 (weather runs, shares unchanged), 0032 (the well acres and bucket labour), 0051 (compost holds water), 0075 (the irrigation ladder), 0084 (the crop ladder), 0027 (specs fix formulas, the economy model tunes values), 0016 and 0045 (the balance gates)
- GDD sections: Land, farming and livestock (Land: moisture)

## Context

After a couple of weeks of play the owner found the rain-fed acres at moisture 3, 5 and 10 through a Spring, with rain barely lifting them, so little was worth sowing and the season earned little. The owner asked for rain to add 7–10 an hour (heavy rain 12–15) instead of 3 (heavy rain 8).

Measured before any change (60 years of the game's own hourly weather; then `MoistureTests` and the balancer on 100 years):

- **Rain wasn't reduced.** Each season's share of rain, heavy rain and storm is still exactly `weather.json`'s (ADR 0042 §3): Spring 16% rain and 2% heavy rain, Summer 6% rain and 8% storm. Since ADR 0042 and 0043 rain comes in runs (4–6 hours outside the Monsoon), between 30–40-hour runs of sun.
- **The average isn't the problem; the bad seasons are.** On an unwatered loam acre, okra's water factor averages 0.80 in Spring and 0.75 in Summer, but in the worst season in 10 it is 0.51 and 0.46: the soil sits near 0 for days. Sandy soil is at the 0.40 floor. Every hour of sun took the full 0.7 off, however dry the soil already was, so a long sun run always ended at 0.
- **Sowing itself is never blocked.** Water's floor is 0.40, and at moisture 0 every crop in season but Spring potato still earns more than its seed. But it earns about a third as much an hour, so a hand's wage or hired labourers on dry rain-fed acres can turn a season into a loss.
- **Sowing doesn't use up moisture.** Only the hour's weather, drainage and the well change it (spec 002 §3.2).

## Options

1. **Rain 7–10 and heavy rain 12–15 (the owner's first idea).** It helps least where it's needed: rain is 6–16% of a dry season's hours, so stronger rain pushes the soil past 70, drainage removes the excess, and the next sun run dries it to 0 again (loam Spring's worst season 0.51 → 0.57). Most rain falls in the Monsoon, so that is where the boost lands: the Monsoon waterlogs (okra, maize, amaranth and radish 0.81 → 0.76, about 6% less yield) and its mean nears spec 002's ceiling of 85. Heavy rain above 10 also breaks spec 002 §4's bound of −10 to 10. Rejected.
2. **More rain in the dry seasons' climate tables.** It changes the weather itself (the forecast, pests' wet share, ADR 0042's realism, every balance gate) for a soil problem. Rejected.
3. **Dry soil gives up its last water slowly (chosen).** Real evaporation falls as the soil dries: FAO-56's water-stress coefficient falls in a straight line once the readily available water is used up, so a rain-fed field rarely becomes bone dry. Below a moisture level, drying is scaled by the water left: × m ÷ level. Rain, drainage, the Monsoon and Winter are untouched. It lifts the bad seasons without making irrigation pointless.
4. **Do nothing and wait for irrigation (Sprint 16).** The pump and canal are the designed answer to a dry season, but a rain-fed acre that drops to 0 for days is unrealistic before and after them.

## Decision

1. **The rule (spec 002 §3.2, step 1):** when the weather dries the soil (`d < 0`), `m ← m + d × lossFactor × dryingFactor × drySoil`, where `drySoil = min(1, m ÷ LandDef.dryingSlowsBelow)`. A field at 25 dries half as fast as one at 50 or more; one at 10 a fifth as fast; one at 0 stays at 0. Rain soaking in, drainage and the well are unchanged. Compost's `dryingFactor` (ADR 0051) still multiplies.
2. **The values (content, tuned with the economy model, ADR 0027):** `dryingSlowsBelow` 50; sun dries 0.9 an hour (was 0.7) and a heatwave 1.6 (was 1.2), so moist soil dries a little faster and the seasonal averages stay inside spec 002 §9's realistic ranges. Rain (3), heavy rain (8), storm (5), cloudy and frost are unchanged. `dryingSlowsBelow` is optional: absent or 0, the rule is off (the frozen golden content, so the golden replays keep their hashes).
3. **Measured on an unwatered loam acre (seed 1, 100 years, `MoistureTests.DrySeasons_WorstSeason` and `SeasonalMeans_Realistic`; `dotnet run --project tools/Tools.Balancer -- moisture` prints the same for every soil):**

   | Season | Mean moisture: before → after (spec 002 §9's range) | Water factor, mean / worst season in 10: before → after |
   | --- | --- | --- |
   | Spring | 38.7 → 40.4 (30–50) | okra 0.80 / 0.51 → 0.84 / 0.63 |
   | Summer | 31.7 → 34.5 (15–35) | okra 0.75 / 0.46 → 0.81 / 0.59 |
   | Monsoon | 75.5 → 76.5 (65–85) | okra 0.81 / 0.76 → 0.82 / 0.77 |
   | Winter | 57.6 → 55.1 (40–60) | wheat 0.95 / 0.92 → 0.95 / 0.91 |

   Clay holds more and sandy less, in the same order as before (sandy Spring 0.73 / 0.50 after). The worst Spring in 10 loses about a quarter less of its yield to dry soil, and Summer's about a quarter less.
4. **The dry-season hook (spec 002 §9):** okra's water factor on unwatered loam, averaged over each Spring and Summer: Spring mean ≥ 0.82 and worst season in 10 ≥ 0.60; Summer ≥ 0.78 and ≥ 0.55 (between the old and new values, so the check fails if the rule is off); Monsoon ≥ 0.80 and Winter (wheat) ≥ 0.93, so they don't fall. `MoistureTests.DrySeasons_WorstSeason` (AC-18).
5. ***Why?* in words (ADR 0043):** the moisture *Why?* gets a row while the weather dries soil below the level: "Below 50, dry soil gives up its last water slowly: dries slower by 50%".
6. **No new state:** no save schema change, no migration. An existing town follows the new rule from its next hour. Its fields keep their moisture: rain lifts them as before, and they no longer fall back to 0 as fast.

## Consequences

- **Spec 002** §3.2, §4, §8 and §9 are amended, with a note in §10. The GDD's Land paragraph says dry soil dries slower. `content/README.md` lists the field.
- **The well acres** (ADR 0032) keep their floor of 30, but the soil reaches it less often and dries more slowly below 50, so bucket labour in a heatwave falls (at the floor: 1.2 → 0.96 an hour lifted). Care and the founder's day are a little easier in dry seasons. `CareTests.SharedPool_TimeWeighted` follows the new numbers.
- **The irrigation ladder** (ADR 0075, Sprint 16) still pays. A canal acre (≥ 45) or the pump (≥ 40) holds water a rain-fed acre only reaches in good seasons, and spec 013 §9's targets are measured on this rule when they are built.
- **The crop ladder** (ADR 0084): drought-tolerant crops (amaranth, mustard, lentils) keep their edge in bad seasons; nothing in its rule changes.
- **Balance gates:** see the table below. `GateTests.RecoverableBankruptcy` needs 50 foreclosures to measure recovery from. The defaulter now earns more and was foreclosed in 47 of 100 runs, so the sample is raised to 120 seeds. This is a sample size, not a rule, as spec 020 §9 allows, and the recovery thresholds are unchanged.
- **Story US-233** (F02.1, 3 points) in Sprint 15b, ad hoc (owner, 2026-11-02). Editor check and device rows in `docs/test-checklists/s15b-crop-ladder.md`.

**The balance gate before and after** (`dotnet run --project tools/Tools.Balancer -- gate`, 1,000 seeds where the gate uses them; 2026-11-02). Every gate holds; nothing moves by more than a few percent:

| Measure | Before | After | Limit |
| --- | --- | --- | --- |
| Competent bot's farm profit a season, median (5th / 95th percentile) | 45,209 (17,884 / 75,839) | 46,669 (17,913 / 76,353) | — |
| Net worth after a year, median | 13,63,451 | 13,65,224 | — |
| Stress bots against competent (field-holder, subsistence-dodger, land-flipper, offer-churner, mandi-every-day, leverage, defaulter) | 0.885, 1.039, 1.000, 0.994, 0.992, 0.988, 0.955 | 0.880, 1.042, 1.000, 0.996, 0.992, 0.993, 0.958 | ADR 0016's |
| People stress bots (solo, day-labour spammer, hand-churner, lapse-gamer, absentee) | 0.934, 0.797, 0.978, 1.000, 0.906 | 0.932, 0.796, 0.976, 1.005, 0.909 | ADR 0039's |
| Cautious saver, random (Phase 1 health checks) | 0.964, 0.838 | 0.967, 0.835 | ≤ 1.20, < 1 |
| Hamlet unlocked: day (10th / median / 90th) | 5.3 / 5.4 / 5.7 | 5.3 / 5.4 / 5.6 | 7 ± 2 |
| Town: 5 households by day | 16.3 | 16.2 | about 21 |
| Mill, hired / run by you / hired on a loan: median profit a season | 7,536 / 36,821 / 7,411 | 6,585 / 36,549 / 7,646 | ADR 0073 |
| Mill run by you: foreclosed | 0 of 12 | 1 of 12 | ≤ a quarter |
| Recovery after foreclosure: foreclosed; profitable within a season; the Hamlet again (median days) | 54 of 100; 54; 5.3 | 47 of 100; 47; 4.3 | 95% recover |
| Supermarket orders' share of sales; the truck full | 15.2%; 0.0% of days | 14.9%; 0.0% | < 20%; < 10% |
| Land-flipper over 4 years | 0.977 | 0.974 | ADR 0016's |

The defaulter is foreclosed less often (54 → 47 of 100) because its rain-fed acres earn more in dry seasons; hence `RecoverableBankruptcy`'s 120 seeds. `dotnet test` (with the Balance category: the bakery, the dairy, HR, the crop ladder and every gate above) passes.
