# 0112. The archetype bots and ADR 0016's 20% rule, measured on Phase 2's systems

- Status: Accepted (owner, 2026-11-13, at US-099's review)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: ADR 0016 (built at last, US-099); ADR 0107 §11 (200 seeds to game year 8)
- Related ADRs: 0016, 0090 (orchards), 0097 (the Village), 0107

## Context

ADR 0016 set the balance rule (the three archetypes within 20% of each other on median net worth and happiness; no stress bot over 20% above the best archetype; random below all) but only the stress bots existed. US-099 builds the archetypes on Phase 2's systems and measures the rule over 200 seeds to game year 8 (32 seasons); the full 1,000 seeds at year 12 stay for the Phase 4 gate.

## Decisions

1. **Three archetype bots**, each the competent founder (homes first, a hand with orders) putting what he earns into a different town:
   - **`AgriHubBot`**: pump, canal, drip, the mango orchard, a godown, hired tractor days, and land bought with spare cash (the cheapest parcel above 1,50,000 spare) and farmed with the season's best crop.
   - **`MerchantBot`**: hired businesses on their loans as their eras come (mill, bakery, dairy, then the village shops).
   - **`EducationBot`**: homes, then the school as soon as it can be built, its adult classes whenever one can start, then the mill.
2. **Every archetype plants the mango orchard** on the last inherited acre once 30,000 coins are spare, and picks it. Measured on 16 seeds: the orchard alone lifts the competent bot's median net worth by 24% by year 8 (about 25,000 coins a season from one acre, which is what a real mango acre earns), more than any other 30,000-coin use in the game. With it left to the agri-hub, that archetype finished 30% above the education town (3.60M against 2.77M) and the rule failed; any player of any town would plant it, so the bots do. The stress bots (leverage, cautious saver, random) do not: they are exploit detectors, not players.
3. **The rule as measured** (200 seeds, year 8, net worth at year-0 prices, town happiness at the end):

   | Bot | Median net worth | Median happiness |
   | --- | --- | --- |
   | agri-hub | 36,67,667 | 68.8 |
   | merchant | 33,71,317 | 68.8 |
   | education | 32,40,991 | 73.5 |
   | leverage (stress) | 28,66,670 | 68.8 |
   | cautious saver (stress) | 29,61,338 | 68.9 |
   | random (stress) | 13,70,155 | 0 (no town) |

   The best archetype is 1.132 times the worst on net worth (limit 1.2) and 1.068 on happiness; no stress bot comes near the best archetype; random is below every archetype on both.
4. **`ArchetypeTests.Archetypes_WithinTwentyPercent`** (category Balance) runs 24 seeds by default (about a minute) so CI stays inside its time limit; `FTC_SEEDS=200` is ADR 0107 §11's run (about 10 minutes here, the numbers above); `FTC_SEEDS` and `FTC_SEASONS` override. Each Phase 3 spec that adds a way to make money extends the bots and re-runs it.

## Consequences

- The orchard is the one strongly dominant investment on Phase 2's systems. It is realistic (ADR 0090's mango acre) and open to every archetype, so the rule holds; if the owner finds it too strong, the lever is the orchard's price or yield in `trees.json`/`farming.json`, and the rule is re-measured.
- The education town finishes last on net worth but first on happiness: its payback (a literate workforce, +10% outside pay) is small before Phase 3's tuition and clinic; the margin to the 20% limit is 6.8 points, to watch as Phase 3 systems arrive.
- `FarmToolBot` and `VillageBot` are no longer sealed so the archetypes can build on them.
