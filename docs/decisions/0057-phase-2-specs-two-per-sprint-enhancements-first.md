# 0057. Phase 2 specs two per sprint, enhancements first

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: ADR 0019 (rolling-wave specs: all of a phase's specs approved in its first sprint), for Phase 2
- Related: ADR 0025 (at most 2 specs to review and 1 device build per sprint), ADRs 0052–0056 (the enhancements)
- GDD sections: Spec-driven workflow; Roadmap and milestones

## Context

Phase 2 has 10 specs: the owner's enhancements (map and village, the trader's truck and supermarket orders, cattle) and the existing Hamlet and Village scope in `epics.md` (citizens, hiring, businesses, shops, farm expansion, school and Codex, statements).

ADR 0019 says a phase's specs are all approved in its first sprint. ADR 0025 allows at most 2 spec reviews a sprint. Both can't hold for 10 specs. Writing all 10 up front would also mean writing specs 014–019 before the gaps in 010–012 are settled, which would mean rework.

The owner also wants to play the enhancements early.

## Decision

1. **Two specs a sprint.** Phase 2's specs are written and reviewed two per sprint (ADR 0025). Each spec is approved at least one sprint before its code, and no code is written before its own spec is approved. Epics, features and stories for the whole phase are planned in Sprint 11.
2. **Enhancements first.** Specs 010 and 011 are written in Sprint 11. Once the owner approves them, their coding starts in Sprint 11, and the sprint ends with a device build of the big map, the village, the truck and the orders. Spec 012 (cattle) is reviewed and coded in Sprint 12.
3. **Spec list and order:**

| Spec | Title | Features | Reviewed in |
| --- | --- | --- | --- |
| 010 | Map, village and edge | F08.1 extended, map growth (new) | Sprint 11 |
| 011 | The trader's truck and supermarket orders | F04.1 extended (new stories) | Sprint 11 |
| 012 | Cattle and fodder | F13.1 | Sprint 12 |
| 014 | Citizens, households and population | F11.1–F11.3 | Sprint 12 |
| 016 | Businesses and production | F12.1, F12.2 | Sprint 13 |
| 015 | Full hiring and HR | F11.4 | Sprint 13 |
| 017 | Shops, customer choice and rivals | F12.3, F12.4 | Sprint 14 |
| 013 | Irrigation, equipment, orchards and granary | F13.2, F13.3 | Sprint 14 |
| 018 | School, literacy, Codex, advisors and the Village unlock | F14.1, F14.2, US-102 | Sprint 15 |
| 019 | Statements, finance hub and milestones | F14.3, F14.4 | Sprint 15 |

   Numbers follow the subject, not the review order. Specs 014 onward may be re-ordered at sprint planning if a build teaches something.
4. **Older specs are amended in the same commit** when a Phase 2 spec changes them (as ADR 0048 did with spec 006).

## Consequences

- Phase 2's specs are approved over Sprints 11–15 rather than all in Sprint 11. Each is written with what the previous builds taught, which is what rolling wave intends.
- Sprint 11 carries the ADRs, two specs, their code and a device build. It runs until the owner's device check passes, as Sprint 9 ran two weeks (ADR 0047 §6).
- The owner plays the map, the village, the truck and the orders at the end of Sprint 11, and the cattle at the end of Sprint 12.
