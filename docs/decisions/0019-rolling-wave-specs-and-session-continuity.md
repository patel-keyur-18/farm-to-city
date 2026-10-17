# 0019. Rolling-wave specs and session continuity

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0017 (implementation model)
- GDD sections: Spec-driven workflow, Roadmap and milestones

## Context

Only spec 001 is written. Writing all specs for Phases 1–5 now (about 35 specs) would front-load review work, and Phase 1's playtests would make much of it obsolete.

The owner also plans to run each phase in a **new Claude Code session**, sometimes in a new context window. A new session starts with no memory except the repository. Cloud sessions clone the **default branch**, so work on an unmerged branch is invisible to the next session.

## Options

- **Specs:** (a) write all specs now; (b) rolling wave: write every Phase 1 spec before Phase 1 coding, and write later phases' specs at the start of each phase.
- **Continuity:** rely on chat history (not available across sessions), or keep the project state in the repository.

## Decision

### Specs: option (b), rolling wave

- Epics and features for **all phases** are planned now, in `docs/planning/`.
- **Phase 1 specs 002–009** are written and approved in **Sprint 0**, before Phase 1 coding starts.
- Specs for each later phase are written and approved in that phase's first sprint, using what earlier playtests taught us.

### Continuity: the repository is the only memory

1. **Session start routine** (written in `CLAUDE.md`). Read, in order:
   - `CLAUDE.md`
   - `docs/planning/STATUS.md`
   - the current sprint file
   - the specs and ADRs that sprint names

   Then continue from "Next up" in `STATUS.md`.
2. **Session end routine:**
   - update the story and task checkboxes in the feature files
   - update the sprint file
   - update `STATUS.md`: phase, sprint, last done, next up, open questions, branch, anything half-done
   - commit and push
   - open a pull request to `main`
3. **The owner merges the pull request to `main`** before starting the next session. No session may rely on unmerged work.
4. **A SessionStart hook** in `.claude/` installs the .NET SDK, so every new cloud session can build and test immediately. It's added in Sprint 0.
5. **Small changes,** so that a session that ends unexpectedly loses little. Commit and push after each task.

## Consequences

- `docs/planning/` holds `STATUS.md`, `epics.md`, one file per feature, and `sprints.md`.
- Every sprint ends with a pull request that the owner merges.
- Specs for Phases 2–5 don't exist yet, by design. Their epics and features do.
