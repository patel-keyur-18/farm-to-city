# 0032. The well waters two acres, the household eats daily, and the Hamlet gate uses farm profit

- Status: Accepted
- Date: 2026-10-18
- Deciders: Keyur Patel
- Supersedes: ADR 0030 point 4 (the well keeps every inherited acre above its moisture floor)
- Amends: ADR 0030 point 5 ("one profitable season" means a season with a positive farm profit); spec 003 §3.6, §3.7, §3.9, §3.11; spec 002 §3.2 step 4; spec 005 §3.4 step 3
- Related: ADR 0008 (per-season realism, upfront living costs), ADR 0010 (founder labour), ADR 0024 (hut store, spoilage), ADR 0031 (kitchen garden)
- GDD sections: Land (moisture and the well), Player journey and eras (Hamlet gate)

## Context

The Sprint 2 planning review (2026-10-18) checked specs 003 to 005 against each other, against the ADR 0030 pacing and against real farming before any code was written. It found five gaps:

1. **The well and the Hamlet gate.** The well kept all 6 inherited acres at moisture 30 or above, and spec 003 charges the bucket-carrying labour. In Summer that is about 0.75 hours a day per acre, so lentils on 6 acres need about 16.5 hours a day against about 14 supplied by the founder and a hand: Care about 0.85, below the gate's 0.9. A hand-drawn dug well also realistically waters only 1–2 acres.
2. **Eating from the store.** The household ate from the store only at each season close. Greens and fruit spoil 20–35% a day, so garden produce picked during the season was mostly lost before it was eaten.
3. **Plan changes.** The founder's daily hours were fixed at the daily pass, but diet plans are charged by the minute and day-labour pay read the share in force at the pass. Switching to a rich diet or to full day labour just before the pass gained a whole day's benefit for a minute's cost: a money loop.
4. **"Profitable season".** The Hamlet gate, the bank's credit limit and the season report all said "profit" without saying which accounts count.
5. **Inherited seed.** The inherited wheat and lentils could be sown as saved seed, but saved seed takes its age from the last harvest, and a new game has none.

## Options

For the well: (a) keep it and tune the labour numbers down; (b) water the inherited acres at no labour cost; (c) the well waters a few named acres and the rest are rain-fed.
For eating: (a) keep eating at the season close; (b) eat daily, with living costs still prepaid; (c) pay the food part of living costs daily.
For plan changes: (a) time-weight nutrition and day labour like Care; (b) delay every change to the next daily pass.

## Decision

1. **The well waters two "well acres"** (`WellDef.irrigatedTiles` in data: tiles 20 and 28, the inherited acres nearest the well). Only these keep the moisture floor and need bucket labour. The other four inherited acres are rain-fed, like bought land. The founder alone tends about 2 acres, so the well acres suit the first crops; the rain-fed acres make the value of irrigation (Phase 2) visible.
2. **The household eats daily.** At each daily pass it takes food from the store up to the day's allowance, `foodShare × planCost ÷ daysPerSeason`, valued at the trader's buying price, most perishable first. The value eaten returns as grocery money not spent (Dr Cash) and is credited to the good's sales account as **home use** (4000 or 4010), so eating or selling gives the same farm profit. Living costs stay prepaid upfront at the gross plan cost (ADR 0008 unchanged).
3. **Founder labour is a rate, time-weighted like Care.** Hours, fatigue and health are fixed at the daily pass; nutrition follows the current diet plan at once; day-labour hours accrue as `rate × share × elapsed` and are paid at the daily pass. Daily health uses the time-weighted mean nutrition of the day that ended. A change gains only the time it was in force.
4. **The P&L grouping and the gate.** Farm income is 4000 and 4010; farm costs are 5000, 5010, 5100, 5300, 5500 (all depreciation) and 5700; farm profit is their difference. Other income (4100, 4800, 4900), household costs (5200, 5250), finance costs (5400, 5410) and other losses (5600) complete the net result. **The Hamlet gate's "profitable season" is a season with farm profit above 0.** Each report line carries a plain-language label and a *Why?* explanation for the player (spec 008).
5. **Inherited seed has an age:** `StartDef.savedSeedAge` per crop (wheat 2.0 and lentils 2.0 seasons of growth, seed factor 0.96). There is no seed-reserve rule: the player keeps grain for seed by switching eating from the store off, and a letter in spec 009 says so.

Two clarifications from the same review (recorded in the specs, no rule change):

6. **Stored inputs** count in net worth at today's input price (replacement cost); produce at the trader's price. Buying inputs ahead is then an asset swap, like buying land.
7. **Fatigue** changes at the daily pass by the option the founder worked in the day that ended.

## Consequences

- **Spec 002:** the well floor applies to `WellDef.irrigatedTiles` (validated as inherited farm tiles); AC-6 covers a well acre against a rain-fed inherited acre.
- **Spec 003:** bucket labour on the well acres only; founder labour as a rate with time-weighted nutrition and day labour (new AC `FounderTests.PlanChanges_TimeWeighted`); daily eating replaces eating at the payment (AC-23, AC-36); the start's seed age; the §9 pacing table re-checked by the economy model.
- **Spec 005:** the season report's lines and labels follow the grouping; the credit limit's "farm profit" is the same line.
- **Spec 008** gets the report labels and *Why?* text as a requirement; **spec 009** gets the seed-keeping hint.
- **Scope (no rule change):** a minimal spec 004 core (goods, regional prices with the index fixed at its start value, the store, trader capacity, `UseInput`) moves from Sprint 3 to Sprint 2, because crops need it. The health formula moves into US-051; sick days, warm clothing and medicine stay in US-052. Pests, the golden replay 003 and tree payback stay in Sprint 3.
