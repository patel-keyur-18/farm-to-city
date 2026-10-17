# 0025. Sprint mechanics

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0017 (roles, test checklists), ADR 0019 (session continuity)
- GDD sections: Spec-driven workflow

## Context

The work is planned as epics, features, user stories and tasks, delivered in sprints. Claude writes the code, and the owner reviews specs and tests builds on devices part-time. So the owner's review and testing time sets the pace, not coding speed.

## Decision

| Topic | Rule |
| --- | --- |
| Sprint length | 1 week. Each sprint ends with a device build, its test checklist, and one pull request |
| Hierarchy | Epic (`E01`) → Feature (`F01.1`, usually one feature spec) → User story (`US-001`, "As a …, I want …, so that …") → Tasks (checkboxes) |
| Estimation | Story points (1, 2, 3, 5, 8), relative size only |
| Capacity | At most 2 specs for the owner to review and 1 device build per sprint |
| Priority | MoSCoW for each story (Must, Should, Could, Won't-this-phase), then ordered by dependencies. "Cut first" items are Could |
| Definition of Ready | Spec approved; dependencies done; acceptance criteria mapped to named tests |
| Definition of Done | Code written; the story's acceptance-criteria tests pass; `dotnet test` green in CI; docs updated (feature file, `STATUS.md`, GDD if changed); pushed. Anything visible on screen is in the build's test checklist **and** confirmed by the owner on a device |
| Branches | One branch per sprint, `sprint/NN-short-name`, unless the session assigns one |
| Merging | One pull request per sprint, merged by the owner after the sprint review |
| Ceremonies | Planning (Claude proposes, the owner approves), review (device results), retro (2–3 lines). All recorded in `docs/planning/sprints.md` |

## Consequences

- `docs/planning/epics.md`, `docs/planning/features/F*.md` and `docs/planning/sprints.md` follow this structure.
- A story whose device check fails stays in the sprint, or carries over. It isn't "done".
