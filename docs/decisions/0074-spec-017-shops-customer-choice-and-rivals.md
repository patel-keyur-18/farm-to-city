# 0074. Spec 017: shops, customer choice and rivals

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Amends: GDD Businesses (customer choice, competition: the Phase 2 shops), spec 016 §3.5(b) (the mill's fee cliff becomes customer choice), spec 010 (three shop pitches round the banyan), spec 014 (part of the households' food and leisure budget is spent at shops)
- Related: ADR 0053, 0061, 0065 (the village centre and its shops), ADR 0063 (household budgets), ADR 0068 and 0073 (businesses, the mill), ADR 0057 (spec order)

## Context

Spec 017 was written in Sprint 14 (ADR 0057) and its eight gaps closed with the owner one per message. The village already shows a grocer and a tea stall (ADR 0053, 0065), whose role as rivals spec 010 left to this spec; the mill's area customers left at a hard fee cliff (spec 016).

## Decision

1. **Shops (G1):** from the Village, a vegetable stall (your produce and products sold retail), a tea stall (a service) and a sweet shop (rasgulla from your paneer, atta laddoo from your atta and ghee), each a spec 016 business with its own books. The owner added the sweet shop to the recommendation; its rival is the town's halwai off the map.
2. **Customer choice (G2):** each customer group shares its demand among the open shops by an additive attraction score: 1 + price + quality + stars − distance − wait (floor 0.05), each part shown in words in the *Why?*.
3. **Location (G3):** a rented pitch among 3 round the banyan (3,000 a season) or your own acre at a distance cost; pitches' layout previewed to the owner (ADR 0064).
4. **Quality and stars (G4):** standard or good; stars 0–5 moving a tenth of the way a day to a day score of served share, quality and value for money.
5. **The village's shops (G5):** rivals on fixed terms that cut prices 5% when they lose a third of their customers, never below cost, and never close in Phase 2.
6. **Newcomers (G6):** a gap (under 80% served) or rich prices (over 1.25 × the reference) opens one of that kind on a free pitch with a seeded 50% chance a season; it closes after two seasons under 10%; buy-outs in Phase 3.
7. **The mill (G7):** its area customers choose between it and the next village's chakki by the same score.
8. **Your produce on your stall (G8):** an internal sale at today's market price, filled each morning to the amounts you set.

## Consequences

- Save schema 11 (with spec 013) holds shops' prices, quality, stars and the rivals.
- The mill's balance at the default fee is rechecked by the business bot when built (Sprint 16).
- F12.3 and F12.4 are broken into tasks; the shops' art (handcart, tea stall, sweet shop, pitches) is previewed to the owner before it's final.
