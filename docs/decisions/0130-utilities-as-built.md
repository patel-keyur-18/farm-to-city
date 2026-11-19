# 0130. Utilities as built: five works, one department, going without costs health, land value and Belonging

- Status: Accepted (owner approved Preview 4 and then the story, 2026-11-19)
- Date: 2026-11-19
- Deciders: Keyur Patel
- Amends: spec 025 §3.1, §5
- Related: ADR 0108, 0116, 0121, 0125, 0126, 0128, 0129; spec 021 §3.7, spec 023 §3.6, spec 024 §3.1

## Context

Spec 025 §3.1 sets four ladders (water, power, sanitation, waste) and five steps the treasury builds: the water tower, the diesel generator, the grid connection, the drains and the collection post. ADR 0125 closed the open questions (the treasury only, a top-up and an upkeep-aware guard, fuel 8,000, one water reach, the generator on an edge plot). Preview 4 showed every sheet and card; four rules in the spec could not be built as written.

## Decision

1. **Five works, one command.** Each step is a `treasury.json` work paid from the town's cash through `CommissionWorks`; there is no `ConnectGrid` (the lanes have no `BuildLane` either). The tower, drains and collection post take bricks, cement, timber and masons (rows in `township.json` `construction`, set so materials plus masons equal the cost within 2%: the post is 3,000 bricks, 20 bags, 1.3 m³, 12 mason-days, 48 hours); the generator and the grid have no building to raise and are fitted the same day.
2. **One Utilities department.** Its condition (spec 021 §3.4) is every utility's service; under `minCondition` (50%) a utility covers nobody. Lines open when the utility stands: upkeep for each, the generator's fuel (8,000), the collector's wage (12,000, paid first in the department and credited to the old village's income as the civic posts' wages are). The grid switches a standing generator off: its lines stop and its smoke goes.
3. **The top-up is part of the command.** `CommissionWorks` carries an optional gift (saved as its own codec tag, so every older log reads as before): given first only if the work then goes ahead, otherwise nothing moves. The sheet and the card offer the shortfall rounded up to the next 10,000. The card's guard counts the whole ladder's upkeep against what the next close brings in; the grid's card says what it saves and in how many seasons its extra cost comes back (about 18).
4. **Going without costs real things, as the spec says, with three changes:**
   - *Sickness.* Households have no sick-day roll (only the founder, hands and workers do), so the spec's "+20% sickness" is health lost each season: 4 points without clean water, 2 without drains (a fifth and a tenth of the 20 a season that hunger costs). The founder's side always has the pond and wells.
   - *Evening hours.* "2 more shop hours" did nothing (customers come 8:00–20:00 whatever), so with power in reach a business's usual day runs two hours later: those hours are not a long day's overtime.
   - *Drains' land value.* −0.04 only on acres with a home standing in them, not the whole map.
   Drains and collection are missed only once the town has 40 residents (the old village counts). Without drains the Health department's condition is capped at 85%; without collection every household's Belonging is 5 points lower.
5. **One water reach.** Clean water is the pond or a well within 300 m, or a working tower within 400 m. Builders and land value (spec 024) use the same rule (`LandValueRules.HasWater`).
6. **The generator's smoke is the kiln's rule** (`SmokeRules`): land value −0.06 and health −5% within 250 m, the total capped at −0.10. The sheet and the card count what it touches. The map's footprint is the US-153 overlay's job: the sheet covers the map, so a footprint drawn under it would not be seen.
7. **Art.** Five plot sprites and five standing sprites in Blender, one facing like the civic buildings, and a smokeless generator for the one the grid switched off.

## Consequences

- Save schema 21 gains `UtilitiesBuilt` (hashed once a utility stands); `schema21-ring.ftc` now has a generator standing and a tower under way. The old logs read unchanged.
- A town left alone is not made worse before the Township, and in a town of fewer than 40 residents nothing is missed. In a big town with no drains or collection homes lose health and Belonging a little every season: slowly, never stuck.
- The tower takes 6,000 bricks and the trader sells 2,000 a day to the whole town: it waits about 8 days with builders also drawing. The kiln (ADR 0129) is the cure, and this is a balance question for the sprint-end pass.
- Balance to read at the end of the sprint: the 20% rule with the water strain and the Belonging cut; a town left alone on the ring (`BuildersCarryAZeroTaxTown`); the budget guard against the ladder's upkeep (40,000–65,000 a year short, ADR 0125 G1).
