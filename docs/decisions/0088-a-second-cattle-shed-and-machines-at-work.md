# 0088. A second cattle shed for the bullocks, machines drawn at work and smaller, US-122 to Sprint 17

- Status: Accepted (owner, 2026-11-04)
- Date: 2026-11-04
- Deciders: Keyur Patel
- Amends: spec 012 §2 (more than one shed), §3.3 (the shed); spec 013 §3.2 (the bullocks' places), §7 (the machines on the map)
- Related: ADR 0039, 0041 (sprites larger than life: a tile is an acre), 0064 (the cattle acre; layouts previewed, never cramped), 0075 (spec 013), 0087 (Sprint 16 plan check)
- GDD sections: Farming (cattle; the equipment ladder)

## Context

US-128's art preview put the bullocks in the cattle shed (spec 013: a pair takes 2 of its 4 places) and parked the bullock cart and the tractor on the cattle acre, the yard's front acre and the quarters. Every one of those acres was already full: the tractor stood on the compost pit and the garden beds, the cart against a hut and the trader's truck. The owner also found the machines too big for an acre and asked whether acres should be bigger.

An acre is about 64 m a side. The game draws everything about 5 to 7 times larger than life (a tile is an acre, ADR 0039 and 0041): at true scale a tractor would be about 7 px on the iPhone 13. Bigger acres would mean a map four times as large to scroll, every sprite at half the size, every approved layout redone, and no change to play.

## Decisions

1. **Acres stay as they are; the machines are drawn smaller:** the tractor and the harvester at 70% of their modelled size, the ploughing pair at 75%, so they sit inside a field.
2. **Machines are drawn at work, not parked:** by day (7:00–19:00) the tractor, or the bullock pair with the plough, works the acres it covers a field at a time; a hired harvester stands on the field it harvests. At night the bullocks rest in their shed and the tractor and its trolley park on the farm lane's acre 167, by the district road. The bullock cart isn't drawn (no room; it still carries 500 kg to the mandi).
3. **A second cattle shed (US-234, F13.1, 5 points, Should):** from the Hamlet, once the first shed stands, on any empty field you own, chosen on lit acres; that acre stops being farmed and becomes a second cattle acre. The same shed (40,000, built in 24 h, 4 places), laid out like the first cattle acre with room to spare, without a second compost pit (dung from both goes to the one pit). Its feed room and straw stack add to the farm's stores (2,400 kg of feed and 10,000 kg of straw more); both sheds feed from them as one.
4. **Bullocks live only in the second shed:** buying the ox plough needs it (refused "Build a second shed for the bullocks" until it stands); cows and buffalo fill the first shed, then the second. Hiring the plough by the day stays the affordable way in.
5. **Sprint 16 stays at 43 points:** US-234 (5) comes in before US-128; US-122 Rival entry (5, Should) moves to Sprint 17, where the Village gate makes rivals matter.

## Consequences

- Spec 012's "more than one shed: Phase 3" is brought forward to a second shed in Phase 2. Shed places, feeding and the shed sheet work over the sheds; the save (schema 14, still open on the branch) records each animal's shed.
- The second cattle acre's layout is previewed to the owner before its code (ADR 0064).
- No formula changes: the shed's price, places, stores and feeding are spec 012's.
