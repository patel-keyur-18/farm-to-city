# 0040. A 2-acre yard, labour quarters for hands' huts, orders that start at once, and people who work (Sprint 5 device review)

- Status: Accepted
- Date: 2026-10-19
- Deciders: Keyur Patel
- Supersedes: ADR 0039 §2 (hands' huts on the edge of a farm acre)
- Amends: ADR 0030 (the inheritance: 6 farm acres, now a 2-acre yard and the labour quarters), spec 006 §3.4, §3.8, §4, §5, §8 (AC-5, AC-13), spec 008 §3.4, the GDD
- Related: ADR 0039 (Sprint 5 review), ADR 0007 and 0036 (art), ADR 0009 (protected land)
- GDD sections: Land, farming and livestock (the inheritance), Citizens and workforce (the hand's hut), Art, audio and feel

## Context

The owner ran the Sprint 5 build on the iPhone 13 (2026-10-19). Every check passed (golden replays 001, 003, 004, 005 PASS on the device; 72 h in 2.9 ms), but five things looked or felt wrong:

1. The Blender hut, the well, the beds and the tree places crowded one yard acre.
2. A hand's hut at the head of a farm acre made no sense (farm land is for crops) and was drawn half behind the field.
3. A hand given an order didn't move until the next hourly pass, up to an hour of real time later.
4. People who reached a field just stood there.
5. A hand with several ordered fields stayed on the latest one.

## Decisions

1. **The yard is 2 acres.** Tile 29 (south of the old yard, beside field 28) joins it, so the homestead is a neat block: 6 farm acres and a 2-acre yard. The back acre holds the house and the well; the front acre, where the door faces, holds the kitchen garden, a tulsi planter, a haystack, a woodpile and two of the four yard trees. A low bamboo fence runs wherever the yard meets land that isn't the player's; it stays open to the fields.
2. **Hands' huts stand in the labour quarters,** a third inherited acre (tile 37, terrain `quarters`, beaten earth, fenced) beside the front yard. It holds `huts.placesPerAcre` (2) huts; at most `maxHandHuts` (2) in Phase 1. `BuildHandHut` is rejected with `NotQuarters` anywhere else, `NoRoom` when the quarters are full, `TooManyHuts` over the cap. The quarters can't be sold or foreclosed (ADR 0009). Tiles 29 and 37 form the inherited parcel `home_c` (the old parcel `e2`). The opening balance sheet gains their land value.
3. **An order starts at once.** `SetStandingOrder` lets the hand act on that target immediately when they still have this sim hour's action (a per-hand counter, part of the state hash). One action per hand per hour still holds across orders and the hourly pass.
4. **People work, and move between their fields** (presentation only, never state). The founder and day labourers go round the growing fields; a hand goes round their ordered fields that need work (a crop on it, or due a sowing, or a tree with fruit), starting with the one they just acted on. On each field they hoe at two spots for a few seconds each (stand and work frames from the Blender rig alternate), then move on. With nothing to do they go home: the founder to the house door, a hand to their hut, day labourers to the yard; day labourers still walk in from the valley's edge and leave at midnight.
5. **Art:** the rig renders a work pose for every person (bent over a hoe, two facings) and the yard props (fence segments, haystack, tulsi planter, woodpile). Frame-by-frame walk cycles stay a later story.

6. **Orders are this season only or every season,** chosen per order (owner, after device run 2): the order sheet asks crop, then "This season only" (sow, tend, harvest and sell or store once, then the order clears with `OrderCompleted` and the field shows *Give order* again) or "Every season" (the standing order of spec 006, which keeps the farm running while away). The sow sheet's "change the order too?" keeps the order's kind.
7. **Work given to a hand is theirs:** the founder never walks to a field a hand has an order for, and day labourers go to unassigned growing fields first (to any growing field only when every one is assigned). This is who walks where; the sim's Care pool is still shared.

## Consequences

- **Content:** `map.json` (tile 29 yard, tile 37 quarters, parcel `home_c`), `start.json` (three inherited parcels), `people.json` (2 hut places). Golden replays run on the frozen content and keep their hashes.
- **Spec 006** §3.4, §3.8, AC-5 and §10 are amended; **spec 008** §10 notes the yard layout and people's work motion; **the GDD**'s inheritance and hut lines are updated.
- **Gate checks:** the competent bot builds its hut in the quarters; all bounds hold (stress bots 0.89–1.00, the Hamlet's conditions on day 5.4).
- **Planning:** inside Sprint 5's points (US-068, US-072, US-183); no story moves.
