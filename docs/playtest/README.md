# Phase 2 playtest (Sprint 19, US-238)

What goes here, and who puts it here:

| File | What | Who |
| --- | --- | --- |
| `playlog-<date>.jsonl` | The exported play log (Settings → Export play log), the evidence for the gate | The owner sends it; Claude saves it here |
| `diary.md` | The diary, the three *why cash moved* sentences and *Your year*, copied from the filled `docs/test-checklists/s19-phase-2-playtest.md` | Claude, from what the owner sends |
| `findings.md` | Each finding: what happened, blocker or not, how it was fixed (data tuned with the bots re-run, or a rule change brought for approval), the commit | Claude |

The town played from is `FarmToCity-playtest.ftc` (seed 2, day 12, from commit `9974013`; regenerate it with `dotnet run --project tools/Tools.Balancer -- playtest-save 2 <path> 9974013`). The gate is in `docs/planning/features/F10.2-phase-2-playtest-and-gate.md` and ADR 0101 §9; its result is recorded in the table at the end of the checklist, then in an ADR.
