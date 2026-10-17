# 0024. Phase 1 adds basic storage, localisation groundwork and a local play log

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Extends: ADR 0017 (Phase 1 scope)
- GDD sections: Land, farming and livestock (storage and selling), UI/UX (localisation), Balancing and testing (telemetry), Roadmap (Phase 1 scope)

## Context

The Phase 1 scope lacked three things the slice depends on:
1. **Storage.** Storage was listed under Hamlet, yet "sell or store?" is an early lesson, and fruit (ADR 0021) is perishable.
2. **Localisation.** The GDD requires string tables from day one, and ADR 0008 requires Indian number grouping. Both are expensive to retrofit.
3. **Measuring the exit gate.** There's no server, so "testers come back on day 2 and day 3" can't be checked without a local record.

## Decision

All three are added to Phase 1.

1. **Hut store:**
   - limited capacity (data)
   - a spoilage rate per good per day (`GoodDef.spoilagePerDay`): fruit and vegetables spoil fast, grain slowly
   - the household eats from it, and the player sells from it at any time

   The granary (more capacity, slower spoilage) stays a Hamlet building in Phase 2.
2. **Localisation groundwork:**
   - English only
   - every visible string comes from a string table
   - every number and money amount goes through a locale formatter (Indian grouping and L/Cr short forms for en-IN, Western grouping elsewhere)
3. **Local play log:**
   - opt-in
   - records session start and end times, plus milestones (first sale, first hire, first loan, Hamlet unlock)
   - stays on the device, and can be exported as a file from settings for the owner to review

## Consequences

**Phase 1 scope table rows:**

| Row | Gains |
| --- | --- |
| Crops | The hut store and spoilage |
| UI | String tables, the locale formatter, the play log and its export |

**Specs that cover this:**

| Spec | Covers |
| --- | --- |
| 003 | Spoilage per good |
| 004 | Store capacity and selling from the store |
| 008 (HUD) | String tables and the formatter |
| 007 (saves) | The play log file and its export |
