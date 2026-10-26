# Sprint 19 (ad hoc) · the crop ladder

> **Draft.** Planned to run right after Phase 2's gate (Sprint 18 today, 29 Mar – 4 Apr 2027). If Phase 2's sprints move, this sprint moves with them and takes the next number. At promotion this section goes into `docs/planning/sprints.md` (the overview table and a section of its own).

## Overview row

| Sprint | Dates (planned) | Phase | Goal | Points | Specs to review | Device build |
| --- | --- | --- | --- | --- | --- | --- |
| S19 | 5–11 Apr 2027 | Ad hoc (after Phase 2) | Six real crops give every season a crop for every return (about 1, 2, 4, 6–8 and 15 h); the sow sheet shows when each is ready and what it earns an hour; no formula changes, every gate holds. | 23 | Crop-ladder spec and ADR | Yes: Development build |

## Sprint 19 · 5–11 Apr 2027 · Ad hoc (the crop ladder)

- **Status:** Planned (draft). Spec and ADR in review
- **Branch:** `sprint/19-crop-ladder`, from `main` after Phase 2's last pull request is merged
- **Goal:** Every season has a crop for every return. Six real Indian crops, no formula changes, the sow sheet compares crops fairly, every balance gate holds.
- **Points:** 23 (Must 22, Could 1)
- **Specs for the owner to review:** the crop-ladder spec and ADR (gaps G1–G14), before any code (ADR 0019's rule: no code until the spec is approved)
- **Device build:** Yes, a Development build on the iPhone 13 with `docs/test-checklists/s19-crop-ladder.md`
- **Milestone and labels** (`CLAUDE.md`): the milestone per spec G13 (recommended `Phase 2 · Hamlet & Village`); `type:spec`, `type:adr`, `type:feature`, `type:balance`; `area:content`, `area:sim`, `area:ui`, `area:art`, `area:tooling`; `documentation`
- Owner: answer G1–G14 (can be done any time before the sprint; the spec is ready now); approve the Blender previews (US-L6.T2); run the device checklist; merge the pull request.

| Story | Title | Feature | Points | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| US-L1 | The crop-ladder spec and ADR approved, the GDD updated | [F02.5](F02.5-crop-ladder.md) | 2 | Must | To do |
| US-L2 | Six crops in content, true to the farm's rules | [F02.5](F02.5-crop-ladder.md) | 5 | Must | To do |
| US-L3 | Balanced: the rule, the ladder and diversifier bots, every gate | [F02.5](F02.5-crop-ladder.md) | 5 | Must | To do |
| US-L4 | No crop named in code | [F02.5](F02.5-crop-ladder.md) | 2 | Must | To do |
| US-L5 | The sow sheet as a ladder: order, ready time, an hour's earnings, notes | [F02.5](F02.5-crop-ladder.md) | 3 | Must | To do |
| US-L6 | The new crops drawn in Blender, seen in the Editor | [F02.5](F02.5-crop-ladder.md) | 5 | Must | To do |
| US-L7 | Neighbours grow maize and mustard | [F02.5](F02.5-crop-ladder.md) | 1 | Could | To do |

### Order of work

| Day | Work | Owner stop |
| --- | --- | --- |
| 1 | US-L1: gaps closed (if not already), files numbered and moved, GDD. US-L2.T7 first: the old save loads with the new content (decides G14) | Approve spec and ADR |
| 2 | US-L2: content and sim tests. US-L4: crop lists out of code | — |
| 3 | US-L3: the balance rule, the two bots, every gate before and after; tune inside the rules | Only if a gate fails (US-L3.T6) |
| 4 | US-L6: Blender renders and previews | Approve the previews |
| 5 | US-L5: the sow sheet and the hint (view, then Unity). US-L7 if there's room | — |
| 6 | Editor tour with scripted touch (US-L6.T3), fixes, captures | — |
| 7 | Development build handed over with the checklist; `STATUS.md`; pull request with labels and milestone | Device run, merge |

### Definition of Ready (ADR 0025)

- [ ] The spec and ADR approved; G1–G14 answered
- [ ] Phase 2's gate passed and its last pull request merged (the sprint branches from `main`)
- [ ] Each story's acceptance criteria name their tests (done in the feature file)

### Definition of Done (ADR 0025, plus this sprint)

- [ ] `dotnet build` and `dotnet test` green in CI; every existing gate at its threshold; the golden replays unchanged
- [ ] The balancer's before-and-after numbers recorded in the spec's §9 (implementation notes)
- [ ] The Editor tour done with scripted touch and every capture looked at
- [ ] The device checklist passed on the iPhone 13 (frame time within the Phase 2 budget)
- [ ] Feature file ticked, `sprints.md` and `STATUS.md` updated, the pull request opened with labels and milestone

### Risks

| Risk | Likelihood | Effect | Plan |
| --- | --- | --- | --- |
| A gate moves (the hand picks okra and Care falls; orders crowd) | Medium | A rule question mid-sprint | Tune inside §3.3 and §3.4 first; if not enough, stop and ask (US-L3.T6). Cut-first list in the spec |
| Six crops' art takes longer than a day | Medium | US-L6 slips | Previews early (day 4); cut amaranth, then mustard (spec §11) |
| An old save's per-good arrays need a schema step | Low | +2 points | Run US-L2.T7 on day 1 (G14) |
| The sow sheet gets crowded at 125% text | Low | Layout work | The note line wraps; out-of-season crops stay one line |
