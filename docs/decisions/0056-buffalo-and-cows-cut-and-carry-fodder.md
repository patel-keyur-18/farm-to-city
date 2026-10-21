# 0056. Buffalo and cows: cut-and-carry fodder, a straw stack and a feed room

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: GDD Livestock (goats and poultry move later, no pigs); spec 003 (straw as a harvest by-product, dung compost); spec 006 (an "animals" order); spec 004 (milk sold to the dairy co-op)
- Related: ADR 0051 (compost), ADR 0053 (the grazing commons), ADR 0024 (the hut store), ADR 0034 (spoilage), ADR 0002 (72-hour away cap)
- GDD sections: Land, farming and livestock (Livestock); Assets and wealth (Livestock)

## Context

Livestock is in Phase 2's scope (F13.1). Dairy is how most Indian smallholders earn steady cash between harvests: a buffalo or two in the yard, milk sold every morning to the village dairy co-op, and fodder grown on part of the land. The GDD lists chickens, goats, cows and buffalo. The owner chose to start with the two that matter most to an Indian farm and to model their feeding realistically.

The game's clocks: a season is 4 real days (ADR 0002), and the game is realistic per season, so one game day stands for about one real month of an animal's life.

The owner decided the following with Claude on 2026-10-21.

## Decision

1. **Two animals:**
   - **Murrah buffalo:** about 1,00,000 coins; about 8 L of milk a day, high in fat.
   - **Crossbred cow:** about 70,000 coins; about 11 L a day, at a lower price per litre, and less milk in Summer.
   Goats and poultry come later. No pigs.
2. **Housing and buying.** One cattle shed for 4 animals in the yard. Animals are bought at the village cattle fair. A bank dairy loan can fund them.
3. **Milk.** The village dairy co-op collects every morning and pays by quality (fat). Milk can instead be kept for the household's diet.
4. **Feed: three kinds, every game day.**
   - **Green fodder:** berseem in Winter, sorghum in Summer and the Monsoon. It is cut and carried daily from a fodder field (about 16 t an acre a season, enough for 4–5 animals an acre) and never enters the store.
   - **Dry fodder:** wheat and rice straw, a new by-product of the harvest (about 1.5 kg of straw a kg of wheat). It goes on a **straw stack** in the yard (about 10 t).
   - **Bought cattle feed:** kept in the shed's **feed room** (about 2 weeks' worth for 4 animals).
   - Per buffalo a game day: about 800 kg green, 170 kg straw and 150 kg feed. The hut store (2,000 kg) can't hold feed, which is why each feed has its own place.
   - The **grazing commons** (ADR 0053) lower the green-fodder need, most in the Monsoon.
5. **Feeding runs automatically every morning,** also while the player is away.
   - A shortfall cuts milk. Green fodder and feed matter most.
   - Several hungry days lower an animal's health, and a vet costs money.
   - No animal dies of hunger in Phase 2, and none dies in a 72-hour absence.
   - A notification comes 2 days before any feed runs out.
6. **Dung becomes compost:** about 3–4 t an animal a year.
7. **Breeding.** About 10 game days in milk, then 2–3 dry, then a calf. For now calves are sold at the fair.
8. **Health.** A vaccination each season, and a seeded disease risk.
9. **Labour.** About 1.5 labour hours a day per animal, from the Care pool. The hand gets an "animals" order.
10. **The shed sheet** shows each feed's days left and the milk, with a *Why?* on every number.
11. **Money, per buffalo per game day:** milk about 13,200 coins; feed about 7,400 if bought, about 4,500 with home-grown fodder and straw.

## Consequences

- **Spec 012** (cattle and fodder) fixes the numbers with the balancer, the fodder crops, the straw by-product and the shed sheet.
- **Left for the spec:**
  - the exact numbers (balancer);
  - silage (later);
  - whether long neglect can lose an animal. The recommendation is no in Phase 2.
- Compost gets a source on the farm, which closes the loop ADR 0051 opened: animals turn straw and fodder into soil fertility.
- A fodder acre competes with a cash-crop acre. The lesson is that home-grown inputs lower cost per litre but use land and labour.
- Animals are assets on the balance sheet that can fall ill. Foreclosure can seize them (ADR 0038 already lists livestock).
