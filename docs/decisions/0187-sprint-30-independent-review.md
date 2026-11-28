# 0187. Sprint 30 independent review

- **Date:** 2026-11-28
- **Status:** Accepted (fixes made; three questions are with the owner)

## Context

The owner's rule (2026-11-18): before the pull request, an independent agent (Opus 5.5, high effort) reviews the whole sprint against its plan. It was read-only: no edits, builds, tests, renders or Editor.

## What it covered

The plan, spec 032 and ADRs 0180 to 0186; the code diff against `main`; the art manifest against the size table (recomputed: 0 problems), the import settings and memory; a sample of renders and captures; the docs. It found no missed ground, crop or tree picture, no sim, save or balance change, and the removed shadow work gone from Release and from the manifest.

## Findings and what was done

No blocker. 8 important, 9 minor.

| # | Finding | Done |
| --- | --- | --- |
| I1 | A played town does not open 3 acres across (a saved camera reopens as saved, since Sprint 6); there is no home button, yet a device-check row said "Editor ✓" for it | Spec 032 §3.4 and AC-4 say what is built; both checklists and two code comments corrected. **Question to the owner** |
| I2 | In normal play the family houses and the standing hand and his wife are realistic while the hands walk and work in the old art; the checklist said otherwise | The checklist says so. **Question to the owner** |
| I3 | Only one device reading is recorded; the default view, the art-check-off reading, the 20-minute heat check and ASTC softness are not | Said in ADR 0183; on the phase build's list |
| I4–I7 | Stale lines in `sprints.md`, `STATUS.md`, `epics.md` and ADR 0185's status | Corrected |
| I8 | Follow-ups from ADRs 0183 to 0186 were not gathered anywhere a later session would look | F21.4 "The phase build's tasks", pointed to from Sprint 33's row |
| M1 | Spec 032 still mentioned shadows and an old hill size | Corrected, and the importer's comment |
| M2 | The soft sky haze of US-273 is never seen: the meadow colour replaces it before the first frame | Said in spec 032 and ADR 0184. **Question to the owner** |
| M3, M4 | A doc comment on the wrong member; shadow leftovers in `SpriteSizeCheck` and two wrong AC numbers | Corrected |
| M5 | The fodder picture ends at a fixed noon, which assumes feeding before noon | A test now fails if `feedHour` reaches 12 |
| M6 | Four weak tests | A morning-before-the-first-cut test; the farthest zoom pinned to its number; water frames compare their scale; ground variants checked along rows, columns and diagonals. A morning outside a cut season cannot be set up: a fodder stand ends with its last cut season |
| M7 | `render.py` would overwrite realistic sprites with toon ones, or rescale the manifest | It refuses both |
| M8 | Faint acre edges on the river bank's grass | Seen in the capture too; left (it is in the approved art), said in the checklist |
| M9 | Some apples show no stalk | Said in ADR 0186 and the checklist |

## Consequences

- No game behaviour changed by these fixes.
- The three questions are in `STATUS.md` ("Next up").
