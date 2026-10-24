# 0067. Two family houses an acre, sharing a wall; the homes and people art

- Status: Accepted
- Date: 2026-10-24
- Deciders: Keyur Patel
- Amends: spec 014 §3.4, §3.13 and AC-2 (`housesPerAcre` 4 → 2), ADR 0063 §2
- Related: ADR 0064 (map layouts never cramped, previewed to the owner first), ADR 0061 (the village redrawn), ADR 0039 (people about door height)

## Context

US-212 drew the family house in the approved low-poly style: a two-room pucca house with a verandah, a flat roof with a water tank, and a courtyard wall in front. Previewed in the Editor (`docs/art/s13-212-houses-4-an-acre.png`), four houses with courtyards on one acre overlapped and spilled over the bunds: buildings are drawn larger than life (a tile is an acre), and shrinking the house would make people taller than its doors. Two free-standing houses side by side still nearly filled the acre. The owner chose 2 an acre and asked for a more natural design.

## Decision

1. **Two family houses an acre** (`population.json` `house.perAcre` 2). The labour quarters still take a house in a free hut place.
2. **The row-house layout** (`docs/art/s13-212-houses-2-row.png`, approved): a compact house (same door height), the two homes sharing a wall at the back of the acre, as village houses stand along a lane; their courtyards open onto open ground in front with a charpai, a shade tree behind and a haystack at the side; nothing tall in front of a courtyard. The first home stands at the left end until the second is built. A homestead acre is drawn on village ground, not furrows.
3. **The people art:** townsmen and townswomen (sarees, head uncovered), boys and girls (about three quarters of an adult's height), elders (white or grey hair, a slight stoop, a stick), each with walk cycles; card portraits for every kind; population and happiness icons. By day (7:00 to 19:00) a woman or an elder and a child of each family are seen in their courtyard.

## Consequences

- An acre given to houses holds 2 households; the Village's 5 households (spec 018) is 2 huts and 3 houses on 2 acres. A house's land is about half an acre, which makes building a real choice against farming that acre.
- Tests: `HouseTests.Build_Limits` (2 an acre), `ContentTests.Population`.
