# 0075. Spec 013: irrigation, equipment, orchards and the granary

- Status: Accepted
- Date: 2026-10-27
- Deciders: Keyur Patel
- Amends: GDD Farming (the irrigation and equipment ladders, whole-acre orchards, the granary), spec 002 (moisture floors from the pump, canal and drip), spec 003 (the Tools factor and labour with machines; orchards), spec 004 (the granary), spec 005 (machines and irrigation as assets; leases), spec 012 (bullocks in the cattle shed)
- Related: ADR 0014 (the yield formula's Tools factor), ADR 0021 and 0033 (trees), ADR 0024 and 0034 (the store, spoilage), ADR 0032 (the well acres), ADR 0057 (spec order), ADR 0064 (layouts previewed)

## Context

Spec 013 was written in Sprint 14 (ADR 0057) and its eight gaps closed with the owner one per message. The farm has two bucket-watered well acres, hand tools (Tools 1.00), trees only in yard and field-edge slots, and a 2,000 kg hut store.

## Decision

1. **Irrigation (G1):** rain-fed; the well and buckets; a pump on the well (Hamlet, 15,000: no bucket labour, 4 acres at moisture 40 or more; the owner added this rung); a canal channel (Hamlet, 6,000 an acre and a 400 fee a season, moisture 45 or more, dry in Summer); drip (Village, 40,000 an acre, half subsidised on the first 2, holding each crop's band on pumped water, +5% with fertiliser).
2. **Pumped water (G2):** the well's pump waters 4 acres by flooding or drip; a borewell (Village, 30,000) 6 more; no water budget in Phase 2.
3. **Machines (G3):** an ox plough (Hamlet, 70,000; 6 acres, labour −15%, Tools 1.03) to buy or hire, a tractor (Village, 6,00,000; 20 acres, labour −35%, Tools 1.06) to buy, lease or hire, a combine harvester hired by the acre at harvest.
4. **Bullocks (G4):** in the cattle shed, 2 of its 4 places, fed like cattle, no milk.
5. **Breakdowns (G5):** maintenance on by default; a seeded daily chance of 0.5% maintained or 4% skipped; 24 h out and 5% of the price to repair; nothing worse.
6. **Orchards (G6):** whole acres of mango (40 trees), guava (100), banana and papaya (400), by spec 003's tree rules.
7. **Intercropping (G7):** a short crop at half the acre's yield and labour until the first fruit.
8. **The granary (G8):** one on the yard from the Hamlet (45,000), 10,000 kg of grains, pulses, atta and seed at a fifth of their spoilage; cold storage in Phase 3.

## Consequences

- Save schema 11 (with spec 017) holds each acre's irrigation, machines, orchards and the granary's goods.
- The balancer measures each rung's payback (spec 013 §9), and the glut a large mango planting causes.
- New art in Blender (the pump house, channels, drip lines, bullocks, the tractor, orchard rows, the granary), layouts previewed to the owner first (ADR 0064).
- F13.2 and F13.3 are broken into tasks; the pump and borewell may raise F13.2's points at Sprint 16 planning.
