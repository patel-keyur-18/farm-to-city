# 0038. Credit rules that close the default loopholes, and the first playable's scope (Sprint 4 planning review)

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Amends: spec 005 §3.4 step 8, §3.6, §3.7, §3.9 (and ADR 0009's "personal bankruptcy" list is unchanged), spec 002 §3.5 (`SellParcel`), spec 008 §3.3, §3.11, §3.14, ADR 0036 (display font), ADR 0035 follow-ups (the Phase 0 code), spec 001 §7 (dev panel)
- Related: ADR 0009 (credit, foreclosure, never stuck), ADR 0016 (balance rule, stress bots), ADR 0028 (land-purchase loans), ADR 0017 (code-first Unity), ADR 0007 and 0036 (zero-budget art), ADR 0027 (data-only tuning needs no re-approval)
- GDD sections: Assets and wealth (Credit, Personal bankruptcy), UI/UX, Art, audio and feel

## Context

The Sprint 4 planning review (2026-10-18) checked the credit stories (US-063 to US-065), the Sim.View stories (US-085, US-086) and the first-playable stories (US-078, US-079, US-088) against specs 002–005 and 008–009, the code and each other before any code was written. It found twelve gaps. The owner decided each one by one.

## Decisions

### Credit (spec 005)

1. **Foreclosure also seizes stored produce.** Protected parcels were the only shelter from an unsecured default, so a founder with no bought land could borrow from the moneylender, hold the harvest unsold across two closes, default, and have the debt written off, then borrow again. Foreclosure now takes, in order: bought parcels (with everything on them), then **produce in the store** at the trader's Floor, highest value first, then good id. It stops once the debt is covered; trader capacity is ignored (a forced sale); any surplus returns to cash. Inputs in store, ready crops on the inherited acres, the inherited plot and cash left after step 4 stay protected. The moneylender's limit is not scaled by score for now; the new debt-fuelled stress bot is the judge, and if it still beats the competent bot by more than 20% (ADR 0016) a score-scaled moneylender limit is added.
2. **Selling land that secures a bank loan repays the bank first.** `SellParcel` computes `required = max(0, bankOwed − collateralLtv × remaining non-protected collateral at market value)`, where `bankOwed` is principal, arrears and interest accrued to now. The proceeds pay `required` (interest, then arrears, then principal, loans in id order) and the surplus goes to cash. If the proceeds are less than `required` the sale is rejected with `LoanSecured`. Moneylender loans are unaffected.
3. **Lender numbers (data):** moneylender 6% a season (about 24% a year, the low end of real) with penalty +3%; bank unchanged (2.5% at score 750, +0.5 points per 100 below, penalty +1.5%) with its maximum term raised from 12 to 16 seasons (4 years). Everything else as in spec 005; the economy model may tune the rest without re-approval (ADR 0027).
4. **Every default raises a `FinalNotice`,** whether reached live or while away. Foreclosure runs once the game has been live for `finalNoticeLiveHours` (default 24) since the notice, unless the arrears are cured; time away does not count. State: `noticeLiveSeconds` per defaulted loan set, counted in hourly passes that run live. This replaces "next daily pass that runs live" and "immediate at a live close".
5. **Score rules tightened.** +10 needs at least one scheduled payment due at that close, all paid in full; +5 needs no loan outstanding at any moment of the season; a loan taken and repaid inside one season earns nothing. `LenderDef.minAmount` (moneylender 1,000; bank 10,000) and `TakeLoan` rejects less with `BelowMinimum`.
6. **`BuyParcel` with financing:** `amount` is a positive whole number of coins and at most the parcel price (else `InvalidAmount`); any lender may finance; the parcel counts as collateral only for secured lenders; cash needed is `price − amount`, checked before anything changes; both succeed or neither.

### First playable (specs 008, F08.x)

7. **A minimal Store sheet joins Sprint 4:** the goods in store, the trader's price and capacity, and **Sell to trader** per good. No offers, mandi or buying (Sprint 7). Taken from US-082 (5 → 4 points); +1 point in US-079.
8. **The ghost preview for building placement (US-079.T4) moves to US-068 (Sprint 5),** where `BuildHandHut` and the yard layout (spec 006) exist. US-079 3 → 3 (T4 out, Store sheet in); US-068 2 → 3.
9. **Art:** ground tiles from the Kenney landscape pack in the repo; everything else is flat placeholder sprites painted in C# at runtime: supersampled, outlined, two-tone shaded, with soft ground shadows. The aim is a polished, cohesive toy-like look in the Hay Day family, never copying its assets. Feel comes from the spec 008 §3.12 motion catalogue (squash and stretch, pop-in, bobbing markers, flying icons, count-ups) and a warm UI (cream and wood panels, thick rounded outlines, glossy 44 pt buttons, bottom sheets). **Fonts:** Baloo 2 (SIL OFL, rounded, Latin and Devanagari) for headings and buttons, Noto Sans for body text (amends ADR 0036's single font). **US-171 (the Blender sprite pipeline) is pulled forward to Sprint 6;** the owner installs Blender before then. Blender renders replace the placeholders through the one sprite lookup.
10. **The dev panel is `DEVELOPMENT_BUILD` only** and holds time travel, snapshot and restore, and the golden replays (ADR 0035 requires them on device at each phase end). The spike, citizen stub, style check and the Kenney miniature-farm assets are deleted (kept in git at commit `3b1c81c`); the spike is rebuilt on the real game at each phase end. From Sprint 4 the owner's device builds are Development builds.
11. **A minimal HUD joins Sprint 4:** the time strip, cash (whole coins), store %, and **Collect all** (`Harvest`, `PickGarden`, `PickFruit` with StoreOnly; what doesn't fit stays; the "sell the rest" prompt, alerts and the Menu wait for Sprint 7). +1 point in US-078.
12. **No saves until Sprint 6** (spec 007). Each launch is a new game. The checklist says so and uses the dev panel for away testing.

## Implementation defaults (no rule change)

- **Credit is optional content.** `finance.json`'s `credit` block and `lenders.json` are optional together; a game without them has no credit state and hashes as before, so goldens 001, 003 and 004 keep their pins. Golden 005 runs on the frozen content plus an overlay folder `golden/content-005/` holding `finance.json` and `lenders.json`.
- **Penalty interest:** scheduled principal accrues at `rate`; arrears (unpaid principal and unpaid interest) accrue at `rate + penaltyRate`, the penalty part posted to 5410 (spec 005 §3.6 as written).
- **Early repayment** pays interest accrued to now (posted then), then arrears, then principal, and shortens the schedule from the end. A restructured loan's remaining term is `min(remaining + restructureExtraSeasons, maxTerm)`.
- **Net worth** includes interest accrued to now, not only posted interest.
- **`Sim.View`** (`sim/Sim.View/`, netstandard2.1, C# 9) depends on `Sim.Core` and `Sim.Content` (for the JSON helpers); `UiDef` and the string tables live in `Sim.View`, not `Sim.Core` (view data, not rules; amends ADR 0020 only in that these definitions are view-side). The string coverage test finds ledger and report keys by scanning the sim sources, and enums and events by reflection.

## Consequences

- **Spec 005** gets the changes above and new ACs (see its §8 and §10). **Spec 002 §5** `SellParcel` gains `LoanSecured`. **Spec 008** §3.3 (HUD scope), §3.11 (fonts), §3.14 (dev panel) are amended. **The GDD**'s credit and bankruptcy text, dev-panel line and font line are updated.
- **Planning:** Sprint 4 is 24 points (US-078 6, US-079 3, others as planned); Sprint 5 gains the ghost preview (US-068 3, 21 points); Sprint 7 loses the store sheet (US-082 4, 17 points); US-171 moves to Sprint 6 (16 + 8 = 24 points, the owner to decide at Sprint 6 planning what slips).
- **Gate checks:** a debt-fuelled stress bot (leverage, and a strategic defaulter) joins the Phase 0 gate checks and must stay within 1.20 × the competent bot's median net worth (ADR 0016).
