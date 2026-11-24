# Phase 3 playtest findings

Started 2026-11-24 (Sprint 26's Release build and playtest town, ADR 0169; Phase 3 closed by ADR 0171). One row per finding. A **blocker** is anything on the blocker list in `docs/test-checklists/s26-phase-3-playtest.md` (a crash or freeze, a lost or refused save, a wrong money amount, a statement that does not add up, a cash movement you were not shown, savings taken without your yes, a notification in quiet hours or with wrong text, a number with no *Why?*, a town that stops growing). Each finding is an ad hoc task: a fix, or a rule change brought for approval.

| # | Date | Found by | What happened | Blocker? | Fix (data tuned and bots re-run, or a rule change for approval) | Commit |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 2026-11-24 | Owner (Development build) | The game's time was 65 minutes behind the phone's | No | The game clock is matched to the device's at a load (ADR 0170) | 33a87392 |
