# 0165. Spec 029: the Chronicle, the Almanac and citizen stories (seven gaps closed one per message)

- Status: Accepted (the owner closed the seven gaps on my recommendations, 2026-11-23; the spec itself is for the owner's review)
- Date: 2026-11-23
- Deciders: Keyur Patel
- Related ADRs: 0036, 0043, 0057, 0063, 0098, 0101, 0107, 0145 to 0148, 0153

## Context

Spec 029 is written in Sprint 25 for Sprint 26 (ADR 0057): US-157 the Chronicle (5 points), US-158 the Almanac (3) and US-159 citizen stories (5), save schema 24 shared with spec 028's US-259 and US-260. The GDD promises an automatic town history in a warm voice, a year-end Almanac (best decision, costliest mistake, a citizen spotlight) and small arcs for notable citizens that arise from the simulation. Seven gaps stood between that and something buildable.

## Decision

1. **What the Chronicle keeps.** Compact entries (when, kind, up to three numbers or ids), the sentence made from a string template when the sheet opens, 400 entries with the firsts, the eras and the founding pinned. Wording and language can change later; a save carries 24 bytes a line, not 120.
2. **Which moments.** 22 kinds: 14 once only (the founding, three eras, the first settler, child, literate child, adult reader, festival, the school, clinic, road, orchard bearing and profitable business) and 8 repeating at most one of a kind a season (a record harvest, a business founded, sold or closed, a fire or epidemic survived, a loan cleared, a petition kept). Each reads from an event the sim already raises. "First graduate" became "first adult reader": a graduate and a literate child are the same moment.
3. **A record harvest.** A crop's season total of at least 1,000 kg and 5% over its best, one line a season.
4. **The Almanac's verdicts.** Best return is a ratio (a business's year of profit over the money put in, a crop's income over its seed and planting cost; at least 10,000 coins of profit, else "a quiet year"); costliest loss is the biggest line of a closed list of one-off losses, so wages and seed never win it.
5. **The spotlight.** A fixed order (a person whose arc moved, the first literate child, the youngest baby, the longest-serving worker, the oldest elder), ties by the lowest id, never random, so the same person every time the card opens.
6. **The arcs.** Four, over real events, with one new rule: a hand of three seasons with savings of at least 10,000 and fair terms has a seeded 20% chance a close of giving a season's notice to start a stall of their own, and the next newcomer stall that opens within four seasons is theirs. At most one step a season.
7. **The screens.** A Menu sheet with a Timeline and an Almanacs tab, the Almanac as a card at the Winter close, a six-second toast for a story step, a notice line on the People sheet. No animation or music (Phase 5).

## Consequences

- Schema 24 adds three states, each hashed only when used, so a town that has not reached them keeps its hash.
- The hand's stall is the one rule that touches the economy (a hand leaves); the player sees it coming a season ahead and can answer with benefits or a replacement. It is the first thing cut if Sprint 26 runs over (spec 029 §11).
- The accumulators (this year's crop income and sowing cost) are new small state for the Almanac.
- Preview 19 is made and approved before any UI code.
