# 0003. Player lifetime: ageless founder

- Status: Accepted. Amended by ADR 0010: the founder is the first worker and has a minimal body state (nutrition, fatigue, health).
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (life clock: citizens age 1 life-year per season)
- GDD sections: Player journey and eras, Citizens and workforce, Open decisions and risks

## Context

The GDD left the player's lifetime open: one lifetime with a retirement ending, or generational heirs. Under ADR 0002's life clock, a player who starts at about 25 would be about 65–70 by the City milestone (4–6 months of real time), so the player's lifespan matters for the whole campaign.

## Options

1. **One lifetime with a retirement ending.** This gives a clear end. However, it conflicts with "mistakes never end the save" and with an open-ended real-time town, and ending a months-long save is harsh.
2. **Generational heirs.** The player picks an heir at the elder stage, and play continues. This is the richest option, but it needs a family tree, succession and inheritance rules, and edge cases such as having no heir. The scope is large for something that only matters late in the game.
3. **An ageless founder.** The player is not simulated as an ageing citizen. It costs nothing now and doesn't block heirs later.

## Decision

Option 3 for v1. Generational heirs are a candidate feature after launch.

- The player is **the founder**: a named household that owns the personal wallet. The founder is not part of the citizen simulation. They don't age, they have no skills or traits, and they never die.
- The founder household has **personal living costs** each season, paid from the personal wallet. These give a baseline for the emergency-fund and passive-income measures.
- The founder appears as a name and a household crest, not as an ageing portrait.
- Citizens still age, retire and die on the life clock. Generational stories come from them, through the Chronicle and citizen arcs.

## Consequences

- No retirement ending and no succession rules in v1. The save is open-ended.
- The founder doesn't count towards the Population figure.
- **Still open: who supplies farm labour before the first hire** (gap #9 in the GDD review). It must be decided before the crops-and-yield spec.
- Adding heirs later needs a founder-to-citizen bridge: an heir is a citizen who takes over the founder's wallet. Nothing in this decision rules that out.
