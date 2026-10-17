# 0027. Plan corrections from the pre-implementation audit

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Amends: ADR 0017 (spec order), ADR 0019 (Sprint 0 contents), ADR 0025 (sprint plan)
- GDD sections: Spec-driven workflow, Roadmap and milestones

## Context

Before merging the plan and starting implementation, an audit of the GDD, the ADRs, spec 001 and the planning files found four sequencing problems, two uncovered risks, nine GDD areas with no feature, and a few small errors.

## Decision

### Sequencing

1. **Spec writing order follows what code needs first.** The spec numbers stay as they are.
   - Sprint 0 writes **002 (land and soil)** and **005 (ledger and credit)**.
   - Sprint 1 writes **003 (crops, trees, founder)** and **004 (market and storage)**.

   The ledger core is built in Sprint 1, because buying land, seeds, living costs and sales all move money.
2. **Specs fix formulas, bounds and placeholder values.** The economy model (Sprints 2–3) tunes the values in `content/`, and spec defaults are then updated to match. A value change needs no re-approval. A formula or rule change does.
3. **The Unity project and a "hello iPhone" build move to Sprint 1.** This tests the riskiest pipeline early: the latest Unity LTS, Xcode 27.1, free signing and the iPhone 13.
4. **String tables and number formatting move to Sprint 4**, before the first screens with text.

### Risk coverage

5. **On-device determinism.** The golden replays run inside the iPhone (IL2CPP) build, and their state hashes must match `dotnet test`. This is part of the Phase 0 gate.
6. **The Hamlet unlock** has its own feature (F09.3). It's the goal that ends Phase 1.
7. **Two smaller gaps:**
   - building placement gets a ghost preview
   - the build guide includes Git LFS on the Mac, a fixed bundle ID, and committing the package `.meta` files

### Coverage of later phases

8. **New features:**
   - personal finance hub and milestones (Phase 2)
   - civic services and coverage (Phase 3)
   - petitions, reputation, crime and inequality (Phase 3)
   - invoices and payment terms (Phase 3)
   - managers, valuation and exit (Phase 3)
   - building upgrades and overlays (Phase 3)
   - citizen stories (Phase 3)
   - public debt and conflicts of interest (Phase 4)
   - trade with other cities (Phase 4)

**Final completeness sweep** (every GDD section checked against the features):
- era unlocks for Village to City (F09.4)
- audio and music (F19.6), plus Phase 1 basic feedback sounds (F08.5, Could)
- era UI themes (in F19.3)
- the `IEntitlementService` port, added to the Unity bootstrap
- a device performance check at the end of every phase (F20.1)

### Small fixes

9. **Text corrections:**
   - spec 001 referred to onboarding as spec 015; it's spec 009
   - the README now links `docs/planning/` and says "latest LTS"

## Consequences

- The plan has 79 features and 176 stories.
- Sprint dates are unchanged. Sprint 9 still ends on 27 Dec 2026.
- An automated check confirmed that no story is scheduled before its spec is approved or before a feature it depends on.
