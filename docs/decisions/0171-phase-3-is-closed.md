# 0171. Phase 3 is closed: the gate's playtest and device evidence are the owner's, A2 to A5 run in parallel, the frame-time numbers are deferred

- Status: Accepted (owner, 2026-11-24)
- Date: 2026-11-24
- Deciders: Keyur Patel
- Amends: ADR 0107 §11 (the Phase 3 gate, part A)
- Related: ADR 0106 (Phase 2's close, the model), 0047, 0094, 0120, 0168, 0169, 0170

## Context

ADR 0107 §11 set the Phase 3 gate as Phase 2's: part A on the owner's 4 weeks from a town just past the Village (A1 the bots' pace, A2 the Township within 4 weeks of the saved town, A3 a session every day, A4 no blocker, A5 at two closes after the Township why the treasury's balance moved and one budget variance, from the screens, A6 build health: both frame-time p95s, golden replays on the phone, CI, A7 the exploit checks); part B before Launch. Sprint 27 was planned as the 4-week playtest and the gate decision (US-250, US-251). All of Phase 3's building is done and merged (Sprints 20 to 26, PR #37 last). The owner asked whether Phase 3 can be closed as Phase 2 was (ADR 0106): everything Claude can test was tested in the Unity Editor by scripted touch and with dotnet tests, and what only a phone can check the owner has been checking.

## What the evidence is (2026-11-24)

| Gate item | Result | Evidence |
| --- | --- | --- |
| A1 the bots' pace | **Pass** | The competent bot reaches the Township at median day 102, 90th percentile 144 (48 seeds); the 200-seed hands-off Township stays solvent (ADR 0168, `GateTests.Township`, `TownshipHealth`) |
| A7 the exploit checks | **Pass** | Five new tests at 200 seeds (zero-time commands, found-and-sell, landlord cap, late invoices, Township health) found no money loop; the ADR 0016 20% rule holds (ADR 0168) |
| A2 the Township within 4 weeks of the saved town | **Open, parallel** | The playtest town (ADR 0169, seed 1, day 44) is on the Release build (`508bb12a`). **Known risk:** the bots need until about day 102, so 4 weeks from day 44 ends near day 72 and A2 may not pass as written; the owner then judges by how far the town got and what held it back, from the log and diary |
| A3 a session every day | **Open, parallel** | The same play |
| A4 no blocker | **No blocker reported so far** | The owner installed the Development build twice and reported it working; the independent review's 18 findings are fixed (ADR 0168); the clock drift the owner found is fixed (ADR 0170) |
| A5 treasury and budget variance from the screens | **Open, parallel** | `docs/test-checklists/s26-phase-3-playtest.md` |
| A6 tests | **Pass** | Core 947 (no Balance or Perf), View 436, Balance 66, Perf 6 at the Sprint 26 head; CI green on `main` at Sprint 26's merge (PR #37) |
| A6 device checks | **Reported good** | The owner on the iPhone, 2026-11-24, with the second Development build (including the matched game clock); the device-only rows of `s26-chronicle-business-growth-2.md` are the owner's to tick |
| A6 both frame-time p95s, golden replays on the phone | **Deferred** | Measured when the owner asks for a Development build (ADR 0094's limits: main thread 8.3 ms, GPU 11 ms, about 60 fps, under 1% over 25 ms) |
| Part B (fresh players explain public versus personal money) | **Open, before Launch** | Needs testers |

## Decision

1. **Phase 3 is closed.** The gate is passed logically on the evidence above. Phase 4's planning and spec writing begin only when the owner says so (the owner has one ad hoc phase to run first); Phase 4 code still waits for its specs to be approved (ADR 0019).
2. **A2 to A5 continue in parallel.** The owner confirms only if something does not check out. Anything found is an **ad hoc task** (a fix, or a rule change brought for approval), recorded in `docs/playtest/phase-3-findings.md`; it does not hold Phase 4. Sprint 27 becomes the owner's parallel play, not a sprint with a plan.
3. **The frame-time p95s and the phone's golden replays are deferred** until the owner asks for a Development build; a failure then is a Phase 3 fix, whatever Phase 4 has added.
4. **Part B stays open** with Phases 1 and 2's, before Launch.
5. **Flagged, not fixed (Sprint 26's review finding 16):** the khata's credit split and the extra kilos shared with every outlet; an ad hoc task if the owner wants it.

## Consequences

- Risk accepted by the owner: later work starts before A2, A3 and A5 are confirmed. If the play shows that public versus personal money is not clear from the screens, the fix touches Phase 3's treasury screens, and anything built on them will want the same fix.
- US-250 is the owner's ongoing play; US-251's gate decision is this ADR.
- STATUS, `sprints.md`, F10.3, `epics.md`, the GDD's phase table and the CLAUDE.md table carry this result.
