# 0089. Equipment as built: the combine to Phase 3, Tools kept from sowing, diesel upfront

- Status: Accepted (owner, 2026-11-04, the combine; the rest for review with US-128)
- Date: 2026-11-04
- Deciders: Keyur Patel
- Amends: spec 013 §3.2 (the combine harvester, coverage, running costs, leases and hires), §5, §8
- Related: ADR 0075 (spec 013), 0086, 0087, 0088 (machines drawn at work; the bullocks' own shed)
- GDD sections: Farming (the equipment ladder)

## Context

Coding US-128 turned up one gap in spec 013 and several details the spec left open.

The gap: the spec hires a combine harvester at 2,500 an acre so that "the harvest needs no labourers", with half the straw lost. In this game, though, a harvest is a free, instant tap, and the hand and labourers harvest at no extra cost. So the combine would cost 2,500 an acre for nothing, and no sensible player would ever use it.

## Decisions

1. **The combine harvester moves to Phase 3** (owner, 2026-11-04), with buying one. There, harvest work can be given a real cost. Phase 2 has the ox plough and the tractor.
2. **Tools is kept from sowing.** A crop sown on a field a machine works keeps that machine's Tools factor (1.03 or 1.06, "timely sowing") until its harvest. A crop sown by hand keeps hand tools' 1.00. Labour is cut on the field while a machine works it.
3. **Coverage.** Machines take your fields best first (the biggest labour cut first), in field order: the well acres first, then by id. A field is an owned farm acre that isn't a homestead, a business's yard or the second cattle acre. A machine hired for the day works every field until midnight.
4. **Running costs follow the irrigation pattern.**
   - Diesel (600 a field a season) is paid upfront at each close for the fields the machine works; bought or leased mid-season, it's paid for the days left. Unpaid, the machine stands idle until the next close.
   - Maintenance is paid at the close when on and cash allows; skipped, the breakdown chance is 4% a day.
   - A repair is paid when the machine breaks. If cash is short, the repair waits for cash and the machine stays broken until it's paid.
5. **Leases** are paid for the days left now, then a season upfront at each close until you give the machine back (no refund). A lease that can't be paid at a close ends.
6. **One machine of each kind,** bought or leased; you can't hire one you already have.
7. **The bullocks** come with the ox plough: 2 × 30,000 as livestock, named from their own list (Hira, Moti and others), living only in the second shed (`NoShedRoom` without 2 free places there). They give no milk and no calves, are fed and cared for like cattle (0.5 h a day each), and are bought and sold only with the plough (`PartOfMachine` at the fair). Without them the plough stands idle.
8. **Books.**
   - A bought machine is capitalised to 1610 Equipment and depreciated straight-line, Dr 5500, Cr 1610, so the account holds its book value. It sells at 80% of book.
   - Net worth gains *Machines at book value*.
   - Leases and day hires are Rent (5300). Diesel is 5810, shown as "Power, water and diesel". Maintenance and repairs are 5820.
   - The bank lends against a bought tractor at `equipmentLtv` 0.70 of its book value. Selling it repays the bank first.
   - Foreclosure takes bought machines, with their bullocks, after the animals and before stored produce.
9. **Mandi trips.** Your working cart (500 kg) or tractor-trolley (2,000 kg) takes a load that fits without hiring the trolley.

## Consequences

- Spec 013 §3.2, §5 and §8 and the GDD's equipment line are updated. F13.2's US-128 drops the harvester. Phase 3's buy-a-harvester item takes in the hire.
- Save schema 14 adds the equipment section (machines, the day's hires, crops' sowing Tools) and the net worth's machines. A schema-13 save loads with hand tools.
- No formula changes: labour × (1 − cut), Tools in the yield product and straight-line depreciation are spec 013's and spec 005's.
