# 0106. Phase 2 is closed: the gate's device evidence is the owner's, A2–A5 run in parallel, the frame-time numbers are deferred

- Status: Accepted (owner, 2026-11-13)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: ADR 0101 §9 (the Phase 2 gate, part A)
- Related: ADR 0047 (Phase 1's gate, the same split), 0094 (frame time), 0102

## Context

ADR 0101 §9 set the Phase 2 gate's part A: A1 the bots' pace; A2 the owner reaches the Village from the day-14 town within 3 weeks; A3 a session every day; A4 no blocker; A5 at three closes the owner explains profit versus cash from the screens, and what *Your year* said; A6 the Release smoke list, both frame-time p95s, golden replays on the phone, CI green. Sprint 19 is the playtest. The owner asked whether Phase 2 can be closed logically now: everything that Claude can test was tested in the Unity Editor by scripted touch (every sprint, every capture looked at) and with dotnet tests; what only a person on a phone can check, the owner has checked.

## What the evidence is (2026-11-13)

| Gate item | Result | Evidence |
| --- | --- | --- |
| A1 the bots' pace | **Pass** | `GateTests.Village` (2026-11-12): homes first with credit day 28.8–39.0, saver day 52.3–57.3 over 8 seeds |
| A2 the Village within 3 weeks | **Open, parallel** | The owner is playing the Release build (`9974013`) from the playtest town |
| A3 a session every day | **Open, parallel** | The same play |
| A4 no blocker | **No blocker reported so far** | The owner installed the Release build and reported everything good; the Editor passes found and fixed what they found |
| A5 profit versus cash from the screens | **Open, parallel** | The three sentences and *Your year* in `docs/test-checklists/s19-phase-2-playtest.md` |
| A6 CI green, tests | **Pass** | Core 498, View 192 pass; CI green on `main` |
| A6 the Release smoke list | **Reported good** (not itemised) | The owner, 2026-11-12/13 |
| A6 device checks | **Pass, owner on the iPhone, 2026-11-13** | Reduced motion; a normal morning of play; rotating or locking the phone; Collect all with a nearly full store; the hut's confirm card; notifications, sound and haptics ("mostly good"); real time away |
| A6 both frame-time p95s | **Deferred** | Needs a Development build; the owner will measure later |
| A6 golden replays on the phone | **Deferred** | The same Development build |
| Part B (fresh players explain profit versus cash; Phase 1's day 2 and 3 returns) | **Open, before Launch** | Needs testers |

## Decision

1. **Phase 2 is closed.** The gate is passed logically on the evidence above. Phase 3's planning and spec writing may begin when the owner says so; Phase 3 code still waits for its specs to be approved (ADR 0019).
2. **A2–A5 continue in parallel** with Phase 3's planning. The owner confirms only if something does not check out. Anything found is an **ad hoc task** (a fix, or a rule change brought for approval), recorded in `docs/playtest/findings.md`; it does not hold Phase 3.
3. **The frame-time p95s and the phone's golden replays are deferred** until the owner asks for a Development build. ADR 0094's limits (main thread 8.3 ms, GPU 11 ms, about 60 fps, under 1% over 25 ms) still apply when it is measured; the last measured values are Sprint 17's (main 5.09 ms, GPU 8.72 ms). A failure then is a Phase 2 fix, whatever Phase 3 has added.
4. **Part B stays open** with Phase 1's, before Launch.

## Consequences

- Risk accepted by the owner: Phase 3 starts before A2, A3 and A5 are confirmed. If the play shows that profit versus cash isn't clear from the screens, the fix touches Phase 2's money screens, and Phase 3's treasury and tax screens will want the same fix.
- STATUS, `sprints.md`, F10.2, `epics.md`, the GDD's phase table and both checklists carry this result; the deferred items are in STATUS's open list.
- US-239.T2 is recorded as done on this evidence; US-238 stays the owner's ongoing play.
