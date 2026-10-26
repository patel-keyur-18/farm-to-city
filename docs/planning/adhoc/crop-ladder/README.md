# Ad hoc phase: the crop ladder

The owner's enhancement for a livelier start (2026-10-26), chosen from `docs/planning/proposals/first-day-pacing.md` (Idea 2). It runs as one sprint **after Phase 2's gate**.

**Why it's staged here.** Phase 2 is in progress on another branch, and it will use more spec, ADR and story numbers. To avoid clashes, every document for this phase sits in this folder with provisional numbers. At the phase's start (US-L1) the files move to their usual homes with real numbers.

## Status

| Document | File | Status |
| --- | --- | --- |
| Spec (rules, data, ACs, gaps G1–G14) | [spec-crop-ladder.md](spec-crop-ladder.md) | Every gap closed (2026-10-26); approved at the phase's start |
| ADR | [adr-crop-ladder.md](adr-crop-ladder.md) | Proposed; accepted with the spec |
| Feature F02.5 (7 stories, 23 points) | [F02.5-crop-ladder.md](F02.5-crop-ladder.md) | Backlog |
| Sprint 19 (ad hoc) plan | [sprint-crop-ladder.md](sprint-crop-ladder.md) | Planned (draft) |
| Device test checklist | [s19-crop-ladder-checklist.md](s19-crop-ladder-checklist.md) | Draft, finalised at the sprint's end |

**Next step:** none until Phase 2's gate. All 14 gaps were closed with the owner on 2026-10-26 (spec §10). At the phase's start: promote the files (below), the owner approves the spec and ADR as a whole, then code.

## In one paragraph

Six real Indian crops: methi, amaranth, okra, green peas, maize and mustard. Every season then has a crop for about 1, 2, 4, 6–8 and 15 hours, so a player can sow something that's ready when they'll be back. Every crop number is per acre per season (ADR 0008), so a short crop earns the same season's worth in smaller, more frequent harvests: no farm formula changes. A balance rule keeps every new crop at or below its season's existing leader (potato in Spring and Winter, rice in the Monsoon) on the regional price and on the trader's floor per growing hour, and the balancer's gates must all hold. The sow sheet orders crops by growing time and shows each one's ready time and what it earns an hour.

## Promotion checklist (US-L1.T2, at the start of the phase)

1. Take the next free numbers after Phase 2: the spec `NNN`, the ADR `NNNN`, story ids for `US-L1`–`US-L7`, and the sprint number.
2. Move and rename:
   - `spec-crop-ladder.md` → `docs/specs/NNN-crop-ladder.md`
   - `adr-crop-ladder.md` → `docs/decisions/NNNN-crop-ladder.md`
   - `F02.5-crop-ladder.md` → `docs/planning/features/F02.5-crop-ladder.md`
   - the sprint section → `docs/planning/sprints.md` (overview row and section)
   - `s19-crop-ladder-checklist.md` → `docs/test-checklists/sNN-crop-ladder.md`
3. Replace every provisional reference (`NNN`, `NNNN`, `US-L*`, `S19`) and drop the "Draft" banners.
4. `epics.md`: F02.5 in E02's feature list and the features table; E02's points.
5. `CLAUDE.md`: the ADR's row in the key-decisions table.
6. Specs 003, 008 and 009: a line in each header ("amended by spec NNN: …").
7. `STATUS.md`: phase, sprint, branch, next up.
8. Delete this folder.

## GDD edits (in the ADR's commit, on approval)

| Where | Change |
| --- | --- |
| Time and world simulation → Task clock table | Replace the first two rows with: "Kitchen-garden greens · minutes"; "Quick field greens (methi, amaranth) · about an hour"; "Vegetables (okra, green peas) · about 4 hours"; "Wheat, maize, lentils, mustard; later onion · 6–8 hours" |
| Seasons table, "Crops sowable" | Spring: "Methi, radish, okra, maize, potato". Summer: "Amaranth, radish, okra, lentils (moong-type pulses), early rice". Monsoon: "Amaranth, radish, okra, maize, rice; later cotton, sugarcane". Winter: "Methi, radish, green peas, wheat, lentils, mustard, potato; later onion" |
| After the seasons table | One sentence: "Every season has a crop for about 1, 2, 4, 6–8 and 15 hours, so the player can sow for when they'll be back (ADR NNNN)." |
| Land, farming and livestock → Crops (launch set) | Add methi, amaranth, okra, green peas and mustard to the list (maize is already there) |
| Roadmap | The ad hoc sprint between Phase 2 and Phase 3, with the crop ladder |

## Not done here (by design)

- No changes to `docs/GDD.md`, `sprints.md`, `epics.md`, `CLAUDE.md` or any spec: those wait for approval and the promotion, so the Phase 2 branch merges cleanly.
- No code (the owner's rule: no code until the spec is approved and the owner says so).
