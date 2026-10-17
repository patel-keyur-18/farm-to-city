# 0016. Balance rule: archetype bots and stress bots

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (City benchmark at about 12 game years)
- GDD sections: Decisions and consequences (No single best strategy), Balancing and testing

## Context

The GDD stated the balance rule two ways:
- "No archetype may finish more than 20% ahead of the others": best against worst, archetypes only.
- "No strategy beats the second best by more than 20%": first against second, across all six bots, including random.

These are different tests. The second one also treats bots that aren't meant to be viable as balance targets.

## Decision

1. **Archetype bots (the viability target):**

   | Bot | Archetype |
   | --- | --- |
   | farm-only | agri-hub |
   | business-heavy | merchant town |
   | education-first | education town |

   Run over 1,000 seeds and measured at 12 game years: the best archetype's **median net worth** is at most **20% above the worst** archetype's, and the same holds for **median happiness**.
2. **Stress bots (exploit detectors):**
   - The stress bots are debt-fuelled expansion, cautious saver and random.
   - No stress bot's median net worth or happiness may exceed the best archetype's by more than 20%.
   - Random must finish below every archetype on both measures.
3. **Time to City** is reported for each archetype as information, not as a gate.

Thresholds and the benchmark year live in the balancer configuration.

## Consequences

- The balancer reports per-bot medians and the two ratios, and fails the build when a rule is broken.
- A dominant stress bot, for example debt-fuelled expansion, is treated as an exploit and fixed in data.
