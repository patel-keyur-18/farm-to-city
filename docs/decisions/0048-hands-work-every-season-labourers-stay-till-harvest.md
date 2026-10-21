# 0048. Hands work every season, labourers stay till harvest (Phase 1 playtest findings)

- Status: Accepted
- Date: 2026-10-21
- Deciders: Keyur Patel
- Amends: spec 006 §3.2, §3.3, §3.8, §5, §6, §8 (AC-2, AC-3, AC-13; AC-24 and AC-25 new); ADR 0039 §6 (orders); ADR 0040 §3 (people go home when idle); ADR 0022 (day labourers by the day)
- Related: ADR 0047 (Sprint 9, closed by the owner), ADR 0009 (orders never borrow), ADR 0032 (time-weighted labour)
- GDD sections: People and labour (Hiring, Standing orders)

## Context

The owner played the Phase 1 build on the iPhone 13 and closed Sprint 9 (2026-10-21). Three findings about the people:

1. **The hand doesn't move.** After an order, the hand stayed at his hut. He walked out only once the owner sowed the field by hand.
2. **The hand doesn't sow or care.** The owner expects a paid hand to run his fields: sow, care, harvest and sell, every season, without the player stepping in.
3. **Day labourers leave mid-crop.** They are hired for the rest of the day and go at the daily pass, with the money. The owner wants to hire them for a crop and keep them until its harvest.

What the code did:

- An order sows only its own crop, and only in that crop's seasons. An **every season** order for wheat (Winter only) sat waiting through Spring, Summer and the Monsoon, with `OrderWaiting(OutOfSeason)` and an empty field. In Spring only radish and potato can be sown.
- A hand's hours always went into the shared Care pool, so he did care for every growing field. But the map walked him only to his own order fields that had a crop or a sowing due. An order waiting for its season, or a hand with no orders, showed him standing at his hut while he was being paid and was working.
- Day labourers were paid up to the next daily pass and left there (spec 006 §3.2), so they might go after an hour or two.

## Options

**The hand's crop out of season:**

- **The valley's fixed rotation** (potato, lentils, rice, wheat).
- **A crop chosen for each season** in the order sheet.
- **The hand chooses the most profitable crop for the field's soil this season** (the owner's choice). The player can still name a crop, and the hand sows it whenever it is in season.

**Labourers:**

- **A crop contract** (recommended, chosen): hired from a growing field, they stay until its crop is ready, harvest it and go.
- A crop contract plus the old hire for the day.
- Hiring by the day, renewed automatically while the crop grows.

## Decisions

1. **The hand's choice (spec 006 §3.8).** A field order names a crop, or leaves it to the hand ("Let Ramesh choose"). When the hand sows an empty field, he uses the order's crop if it is in season. Otherwise, or when the choice is his, he sows the in-season crop with the highest **expected profit per season on this field**:

   ```
   one sowing  = Yield(seasonYieldPerAcre × cycleShare, soilFactor(tile), waterFactor(crop, tile moisture now),
                       care 1, seed 1, weather 1, fertiliser factor, health 1)
   profit      = value of that harvest at the trader's floor today − its seed and fertiliser at today's input prices
   per season  = profit ÷ cycleShare            (compared by cross-multiplying growth times: no rounding)
   ```

   This is the sow sheet's own estimate, so the hand picks what the player would see as best. The soil's fertility and the field's moisture against each crop's band are what fit the soil. Ties go to the earlier crop in `crops.json`. The order's seed source applies only to its own crop: the hand's other picks use certified seed. He uses the order's fertiliser. He never borrows (ADR 0009): with too little cash he waits (`OrderWaiting(InsufficientFunds)`). This-season-only orders follow the same rule.
2. **A hand under contract is seen working (presentation only, amends ADR 0040 §3).** His hours tend every growing field, so the map walks him round his own order fields that have work first, then the other growing fields. He goes home only when nothing grows, or while his contract has lapsed.
3. **Labourers stay till harvest (spec 006 §3.2, replaces the hire for the day).** `HireLabourers(tileId, count)` is used from a field with a growing crop:
   - **Rejections:** `NotGrowing`, `NoneAvailable` or `InsufficientFunds`.
   - **Length:** the first `count` free labourers stay until **`until` = the crop's `readyAt`, rounded up to the next whole hour**.
   - **Pay:** upfront, `dailyWage × count × (until − t) ÷ 86,400` at the price index, Dr 5100 Hired labour, Cr Cash. Like seed, it is expensed when it is paid.
   - **Work:** each labourer adds `hoursPerDay` to the Care pool until they go.
   - **Harvest:** at the first hourly pass at or after `until`, before the hands' orders run, they harvest the crop if it is ready. The harvest goes to the store, and any overflow goes to the trader, so nothing is lost. Then they go home (`LabourersLeft`).
   - **Crop lost early:** if the crop was cleared or lost first, they keep tending the other fields until `until`. They have been paid for that time.
   - **Who is free:** the daily draw's `availablePerDay` free labourers are drawn only from the neighbours not already on a crop.
   - **Regulars:** `secondsWorked` grows by `until − t`, so regulars come sooner.
   - **Cost:** examples at year-0 prices are about 9,375 per labourer for a 24-hour potato or rice crop, and about 2,350 for a 6-hour wheat crop.
   - **The old command:** `HireDayLabourers` is retired. Its command-log tag still decodes, but as a rejected `HireLabourers`.
4. **Save schema 4.** A neighbour holds `tile` (−1 for none) and `until` (0 when not hired) in place of `hiredToday`. Schema 3 migrates as follows: a neighbour hired today gets `tile` −1 and `until` = the next daily pass, which is exactly what they were paid for. The frozen fixture is `saves/schema4.ftc`. `minSchema` stays 3 (ADR 0047 §3).

## Consequences

- An **every season** order keeps its field producing all year. The hand follows the player's crop in its seasons and his own best pick in the others.
- The economics are unchanged. A labourer still costs 25% more an hour than a hand. Hiring for a long crop costs a long crop's wages, paid upfront, which may be as much as that acre's harvest. Labourers are for rescuing Care on a busy crop, not for routine work (spec 006 §9).
- The People sheet no longer hires. It shows who is free, who is on which field, and the hint "hire from a growing field". The field sheet of a growing crop lists **Hire N till harvest**, with the cost and the Care that results.
- The onboarding's `borrowing` trigger names `HireLabourers` in place of `HireDayLabourers`. The play log's first hire milestone follows `LabourersHired`.
- The goldens use the frozen content, which has no people, so they are unchanged. The balancer's bots hire through `HireLabourers` on their busiest growing field.
