# 0066. Fodder stands don't keep labourers hired; the People sheet in the Store's layout

- Status: Accepted
- Date: 2026-10-24
- Deciders: Keyur Patel
- Amends: ADR 0049 §3 (labourers hired till the crops growing now are in), spec 006 §3.2 and §7 (the People sheet)
- Related: ADR 0060 (berseem and sorghum cut daily), ADR 0059 (the Store in sections with picture buttons)

## Context

1. **Labour cost jumped about 10 × with fodder sown** (owner, 2026-10-24). Labourers are hired till the last growing crop's `readyAt` (ADR 0049 §3). A fodder stand's `readyAt` is the end of its last cut season (sorghum sown in Summer stands through Monsoon, berseem through Winter and Spring): up to 2 seasons, 8 game days. Wheat grows 6 hours, rice and potato 15, so with a fodder field two labourers cost about 1,50,000 instead of about 4,700 (13–32 ×).
2. **The People sheet** was the last hub still in plain label-and-value rows; the owner asked for it in the Store and Orders sheets' layout.

## Options (fodder)

1. **Harvest only:** labourers are hired till the last crop with a harvest is in; fodder never counts.
2. Fodder counts till its next 6:00 cut, so with only fodder growing labourers could be hired for at most a day.
3. Hire by the day for everything (literal dihadi): reverses ADR 0049, adds daily taps, and a 2-hour radish pays a full day.

## Decision

1. **Harvest only (option 1).** `PeopleRules.LabourUntil` skips fodder stands. In real villages berseem and jowar are cut and carried every morning by the household as part of looking after the animals; day labourers come for sowing and harvest peaks. That work is already in the Care demand (1.5 hours an animal, ADR 0056) and done by the founder or the hand's animals order. With only fodder growing, hiring is refused (`NotGrowing`) and the Hire sheet says "Labourers come for a harvest. Your fodder is cut each morning with the animals' work."; the hire *Why?* says a fodder field doesn't count.
2. **The People sheet in the Store's layout** (the owner chose the hub with sub-sheets): a Care row; three picture buttons (Hire labourers with how many are free, Build a hut with its cost and how many are built, greyed with its reason when there's no room or cash, Send word); pictured rows for the labourers at work (till when), the hands (portrait, stars, orders or unpaid), the people asking for work, and the huts (who lives there, being built). Hire labourers and Send word open their own sheets; a hire or a word sent returns to People. When US-210 adds the Town tab, this sheet becomes its Workers tab.

## Consequences

- Labour cost with fodder back to what the crops alone cost. Labourers already hired till a stand's end in a player's town stay till then (they were paid upfront); no save change.
- Balance re-measured: every gate passes (Hamlet day 5.4; day-labour spammer 0.812 × competent).
- Tests: `DayLabourTests.Hire_FodderDoesNotCount`; `PeopleViewTests` (the picture buttons, pictured rows, the Hire and Word sheets); `WhyTests` covers the new sheets.
