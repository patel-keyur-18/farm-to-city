# 0142. Sprint 23 balance and the independent review (Opus 5.5): pace held, twelve findings fixed or flagged

- Status: Accepted (the owner reads the flagged items, 2026-11-20)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Related ADRs: 0124 (Sprint 22's balance), 0125 to 0141, `CLAUDE.md` (the independent sprint review rule)

## Context

Sprint 23 built eleven stories (the ring, construction, the kiln, utilities, pucca houses, overlays, the cold store, calves, silage, your tractor for neighbours, solar panels) and two crash fixes (ADR 0139, 0140). Its end asks for the balance pass, an Editor tour with the rendering statistics, a checklist, a Development build and an independent review.

## Balance (run alone, 2026-11-20; all the gates pass)

| Measure | Result | Target |
| --- | --- | --- |
| Archetype bots (24 seeds, 32 seasons) | net worth best/worst **1.107**, happiness **1.076** | within 20% (ADR 0016) |
| The Village | day 36–46 (competent), 61–69 (saver) | 28–45 competent |
| The Township's pace | competent median **day 101.5**, saver **135.6** | 101 and 135 (ADR 0124): unchanged |
| A town left alone | treasury 124,000–146,000 at the unlock, 177,000–220,000 a year on | solvent, levelled |
| The five policies | all affordable, nothing broken | ADR 0109 |
| The land bots (zone, lease, hold) | exactly **1.000×** the plain bot | none beats plain play |
| The kiln's plan | hired: a firing season 73,700, a year's average 45,700, payback **6.6** seasons; run by you 103,700, 75,700, **4.0** (`KilnTests.Plan_Payback_CountsTheMonsoon`) | ADR 0129 said about 7 hired |
| Solar panels | payback 75 seasons against a 40-season pump (ADR 0138), kept as the owner chose | later |
| Rendering (Editor) | whole map 1,082 draw calls / 31,414 triangles; village 480 / 13,848; farm 351 / 9,471; no metric grew over 10% | within 10% |

## The review: twelve findings

Fixed (with the tests that pin them):
1. **Raw string keys** for a held water tower, drains and collection post (queue, bell, away card): the three `build.kind.*` strings; the queue row's time has a *Why?*; `BuildKindStringTests`.
2. **Schema 21 fixtures** held almost none of the sprint's saved parts: the ring fixture now also carries a kiln, the cold store, a shed with heifers, the silage pit and chaff cutter, a hired-out tractor and a solar panel, each asserted on load (`MigrationTests.Schema21_Ring`).
3. **The *Why?* sweep did not reach the new sheets:** `Situations.All` gains the kiln and the farm's promises, and the sweep gains the Building, Equipment and shed sheets. It at once found three real gaps, fixed: the kiln's brick cost had no unit, the hire fee had no unit, and a *Why?* used the sign ×.
4. **The checklist** lacked the device checks the plan names (both p95s, golden replays, a 72 h catch-up with a Township open) and had a wrong path to Infrastructure: fixed.
5. **Spec 025** named tests that do not exist and described the plan where the build differs (brick, cement and timber are not goods; the state, commands and events; the cold store's figures): corrected, with an *As built* block.
6. **The GDD** said calves and silage come later (they are built): fixed.
7. **Stale planning docs** (STATUS, the sprint table, the feature files' statuses, the epics): brought up to date.
8. **The tractor's hire-out** could post a negative net with no cash (the same class as ADR 0140): a day's diesel with no cash to pay it is not driven.
9. **A loosened balance assertion's message** ("every service at 100%") said the opposite of its bound: corrected.
10. **The balance pass** was not all done: the kiln's targets are now a test; the generator's and the cold store's are arithmetic in spec 025.

Flagged to the owner (a decision, not a fix): the kiln has no cap on how many you found and the trader buys 5,000 bricks a day from each, so two or three kilns multiply the profit (and the smoke's health effect stacks); the kiln is **from the Village** in the content and in F12.7's scope, but a Township business in spec 025's §3.3 and the GDD; a throwing system still skips the systems after it for that pass (a per-system guard); a zero-tax town is now only 0.3 happiness below a taxed one (spec 022 expected 5).

## Consequences

- The Township's pace, the archetypes and the land payback are unchanged by the sprint.
- The 200-seed hands-off Township on the ring was not run; `TownshipSolvent` (16 seeds) passes. It goes to Sprint 24's balance pass with spec 026 and 027's.
