# 0114. The treasury as built: the Township reached by the developer panel only, seven departments from the start, pitch rents counted in the season they are paid for, and the books' first statement

- Status: Proposed (for the owner's review of US-138, 2026-11-14)
- Date: 2026-11-14
- Deciders: Keyur Patel
- Amends: spec 021 §3.1 (how the Township is reached before spec 023), §3.2 and §3.3 (when a pitch rent is the town's income), §4 (seven departments in the state); spec 022 §3.5 (the seventh department, already added there)
- Related ADRs: 0108, 0109, 0107 §6, 0094

## Context

Building US-138 (spec 021) raised four small choices that the spec leaves open or states loosely. None changes a rule the owner approved; they are recorded so spec 023 and the later stories build on the same ground.

## Decisions

1. **A Township era with no gate yet.** `eras.json` gains `township` with the condition `developerOnly`: no condition is ever met, so only the developer panel's *Next era* reaches it (Development builds). Spec 023 replaces the condition with the real gate. The treasury opens in `EraRules.Unlock`, the one place the era changes, once.
2. **Seven departments from the first schema 19.** The state holds seven budgets, conditions and counters (office, health, education, roads, safety, utilities, policies), so spec 022's *Policies* department needs no second schema. The clinic's line and the tied grant exist in content but count as "not staffed" until spec 023 (`TreasuryRules.ClinicStaffed`).
3. **A pitch rent is income of the season it pays for.** A rent paid at a season boundary is for the new season, so the treasury holds it until the old season's books are shut and credits it in the new one; a rent paid mid-season (a shop founded) is credited at once. Grants and taxes, billed for the season that ended, are income of that season. So every statement's money in, its cash flow and its opening and closing cash agree.
4. **The first statement holds the start-up grant.** The grant is the first season's first income, with opening cash 0, so that statement shows the grant, the first grants and the clerk's wage, and its surplus is the grant less the season's costs. It is not a profit.
5. **A department row shows its service in its label** ("Council's office · service 100%") with a bar, and a Why? that lists its lines, budget and service; the planner and the warning (US-139) add to it. Rows of a department with no lines say "Nothing to pay yet".

## Consequences

- `content/treasury.json` (era, grants, the clerk's and doctor's pay, lines, works, bounds); `Account` 4600–4670 and 5910–5970 arrive in schema 19; the golden replays' frozen content has no `township` era and so no treasury and keep their hashes.
- Spec 021 §10 lists these as as-built notes. Spec 023 must keep the default town solvent with the clinic's and the road's upkeep added (spec 022 §9: about 67,000 in against 62,000 out before them).
