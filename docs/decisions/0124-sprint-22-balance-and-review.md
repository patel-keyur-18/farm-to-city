# 0124. Sprint 22 balance and the independent review: what was measured, what was fixed, what is flagged

- Status: Accepted (owner, 2026-11-18: "go with your recommendations", independent review by Opus 5.5 at high effort)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Related ADRs: 0016, 0027, 0028, 0107, 0112, 0119, 0120, 0121, 0122, 0123

## Context

Sprint 22 built the Township: the gate (US-100), land value (US-143), zoning and builders (US-142), the road and clinic follow-ups (US-146, US-148) and leasing land out (US-261). Before the pull request the owner asked for two things: a balance pass over everything the sprint added (US-263), and an independent review by a separate model (Opus 5.5, high effort) that looks for stale logic, things a player cannot achieve and stories that are not finished with quality. This ADR records both and what became of each finding.

## Balance (US-263), measured

Numbers are medians unless stated; every run is on the shipped `content/`.

1. **The Township's pace:** the competent bot reaches it about **day 101** (range 90–150), the saver about **day 135** (125–148); the Village about **day 41** and **day 65**. ADR 0120's pace stands.
2. **A town left alone** (200 seeds, no command after the unlock, default taxes, no civic works): the treasury never goes below zero, every book reconciles, every service stays at 100% and the doctor stays. The second year's change is **+30,723 at the worst, +59,054 at the median, +123,231 at the best** (spec 023 AC-10: it must not be negative).
3. **The archetypes** (200 seeds, 8 years): best over worst net worth **1.132**, happiness **1.068**, inside ADR 0016's 20% and ADR 0112's 1.13. Nothing in Sprint 22 moved them.
4. **Taxes:** a town with every tax at its cap earns the treasury more and strands nobody; with every tax at zero and no builders the services waste away to under 30% (the lesson). With builders (US-142) a zero-tax town is carried by their families' grants: every service stays above 90% (`BuildersCarryAZeroTaxTown`). This is flagged below.
5. **Policies (US-263 item 8):** each of the five policies enacted alone by the Township bot (12 seeds, two years on): the treasury never goes below zero and every service stays at 100% (`GateTests.PolicyAffordability`). The cheapest to keep up is rent control (no cost, about 220,000 in the purse); the dearest the subsidy (about 80,000 left). A policy never breaks a town that left to itself would be sound.
6. **Leasing against hiring (US-261):** the lease yield, 2.5% of the land value a season, stays **below hiring a hand** (a hired acre nets 5,834 a season on the same soil). Leasing stays the safe, small choice, so `leaseYield` is not tuned. The stress bot that leases its idle parcels ends **1.064 × the plain bot** in net worth (16 seeds, year 15), well inside the 20% bound.
7. **Zone-and-flip and buy-and-hold** stress bots cannot act: the serviced neighbour acres are taken by builders (one a season) before a bot has the cash for one (about 100,000 against 600,000 and up), so both end exactly level with the plain bot. They prove no exploit but also test none; a later balance pass can hand them cash with a developer command.
8. **Land payback** (ADR 0028 asks 8–15 seasons of an owner-operated acre's net): **6.0 seasons** at land value 1.0 and **about 10.9 at 1.8**. The 6.0 is below the target; it is the Phase 2 economy (richer than ADR 0033's), not a Sprint 22 change, and the land value now lifts the price it is measured against. **Flagged to the owner**; no number was changed.

## The independent review: findings and what was done

The reviewer read the whole branch against the stories, the specs and the code. Twelve findings; all addressed:

1. **The school's reach hit Phases 1 and 2.** Homes out of reach were refused the school before the Township. Now `SchoolRules.HomeCovered` is true for every home until the Township is reached, and a household with children counts for growth only when its home is covered (`SchoolRules.Met`); the citizen card says "The school is N m away; its reach is M m".
2. **The old village could miss its cohort.** A game that reached the Township by a path that skipped the unlock never joined it. `CohortRules.EnsureJoined` runs at the close before land value is read.
3. **Taking land back left its zone and its slow sale.** `LandSystem.Release` now clears the zone, cancels the sale job and clears its fields.
4. **A slow sale could be listed on land with an order.** `ListParcel` is refused with `ParcelInUse` when a hand's order stands on it.
5. **A doctor who died left the clinic "staffed".** `ClinicState.DoctorId` (schema 20, fixtures regenerated) names him; his death ends the post, his household leaves and another is called (`ClinicRules.CitizenDied`; `ClinicTests.DoctorDies`).
6. **Builders' homes were counted as missed rent.** They are not your houses and pay you nothing; no `RentMissed` is raised for them now.
7. **Road upkeep said "the district's"** after the treasury opened. It now reads "The town's treasury, in its Roads line".
8. **Water for land value** looked for a hard-coded well; it uses the well's id from the building definition.
9. **The sell-land sheet's "more" row had no *Why?***. It has one now, and the sell-land sheets are in the *Why?* sweep.
10. **Stale text:** the Township letter said "Twenty families" (the gate asks fifteen); the businesses row said "different"; dead `house_1…house_5` sprites in the map presenter; comments in five files; spec 023 and 024 test names; spec 024 AC-5 marked cut; the GDD's 20-household figures; ADR statuses 0118–0123.
11. **Tests that proved nothing:** `Avoided` asserted `>= 0`; the 2% permit fee was not checked to the coin; "a town left alone" did not check that no sale or waiting family appeared; the policy and land stress checks did not exist. All now assert what they say, and the two stress checks were added.
12. **Smaller:** the school gate's tests call the Township first; literacy skips builders' households.

Two more were found by the new tests while closing these: seven treasury lines (clinic, road, watchman, caretaker, post, hall, playground) had no name string, so their *Why?* would have failed in game; and the season report's land-rent line had no *Why?* text. Both are fixed, and `TownshipBuilt` (a Township with works built and land leased, zoned and listed) is now a sweep Situation so this cannot come back.

## Flagged for the owner (no change made)

- **Land payback 6.0 seasons** is under ADR 0028's 8–15. Raising the land price, or reading it against the lower-yield land only, is a Phase 3 balance choice.
- **Builders carry a zero-tax town.** Their families bring the district's grants, so a town with no tax is solvent. That is fine for "a town left alone always works" but weakens taxes as a lever in a town with builders.
- **Builders take one neighbouring parcel a season for good**, so a player with little cash cannot buy the serviced acres beside their land after the first few seasons.
- **The rent place's jump at the unlock** (inner acre 0.85 to about 1.13) lifts renewals; the 30% rule and the market rise cap it.
- **Old-village households that leave never return.**
- The `"village"` era id is still written into `RoadRules.Unlocked`.
- Carried to later sprints: the commercial zone, builders' pucca houses, the overlay (US-153), smoke and crime parts of land value.

## Consequences

- No content number changed in this ADR. Ten strings added and two corrected (`en.json`).
- Schema 20 stays unreleased; `schema20.ftc` and `schema20-township.*` were regenerated for `ClinicState.DoctorId`.
- Spec 025 (utilities, construction, the ring, hills, the cold store) goes to the owner for review next, one gap at a time.
