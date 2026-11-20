# 0147. US-149 festivals as built: one a season, a day of the clock, the Festivals account

- Status: Proposed (awaiting the owner's review of US-149)
- Date: 2026-11-20
- Deciders: Keyur Patel
- Related: spec 026 §3.1, §7, Preview 14, ADR 0108, 0127, 0145, 0146

## Context

Spec 026 §3.1 sets festivals; Preview 14 (approved) drew the sheets. Building them raised a few points the spec left open.

## Decisions

1. **The day is a fact of the clock.** `TownLifeState` holds the one scheduled festival (kind, venue, payer, day, cost, people). On the day (`today == FestivalDay`) `FestivalRules.Lift` adds 8 happiness points to a household whose home is within the venue's reach and 3 elsewhere, in `HappinessRules.Update` (after the inequality points, held at 100), and `ShopRules.DemandThisHour` adds 30% (chai, sweets) or 10% (vegetables). Nothing starts or stops it, so a 72 h absence cannot skip it, and the next day it is gone. A job at the day's end (`festival.held`) moves it to the record (the last 8) and emits `FestivalHeld` (an away line; the Chronicle's entry, spec 029). The old village's cohort is counted in the cost and gets no lift.
2. **One a season, by the season the festival was scheduled in.** The kind is the festival of the season it is scheduled in; a day up to 3 ahead may fall in the next season and is recorded by its own date.
3. **Cost:** `perPerson` × (your households' members + the old village's people) × the price index, paid at once. Summer 15,948 coins for 151 people in the dev town (100 a person).
4. **Payers.** The treasury pays from its cash (never below zero) into a new expense account **Festivals (5980, schema 22)**: it is spending, kept outside every department's lines so no budget proposal or variance reads it, and it shows on the statement's money out. A gift is your cash booked to Gifts to the village (outside farm profit); nothing flows back.
5. **The budget guard informs, it does not block**, as for works (ADR 0108): the card has a row "At the next close: The town still pays its bills / is short by X".
6. **The hall needs its caretaker; the square is always there.** The hall's festival choice is greyed with its reason until a caretaker is in post.
7. **Screens.** The community hall's and the banyan square's sheets carry "This season's festival" with a *Hold a festival* button (or what is planned) and the last four festivals; the button opens the planning sheet with three tab strips (where, who pays, which day), the cost with its *Why?* and what it does. Preview 14 put the choices inline on the hall's sheet; a separate sheet reuses the tab strip and keeps both venues' sheets short. A *Hold this festival?* card ends it. The bell carries "X is tomorrow / today"; a green strip at the top of the map says "X today · the venue" on the day.
8. **A festival's day is a day of rest (owner's rule, 2026-11-20, which amends spec 026 §3.1).** Nobody works; everyone rests and enjoys the festival at the venue. In the sim (`FestivalRules.Resting`, `WasFestival`, `ClosedForFestival`): hands take no field, tree or animal order (`Orders.Run`, `ActNow`); hired labourers pick and harvest nothing (their paid time still runs, no refund); the founder earns no day labour; nobody is paid outside day-labour wages for that day; every business that is not a stall (mill, bakery, dairy, kiln) makes nothing, and its morning batch is not made. **Only the stalls stay open** (owner's answer): the vegetable stall, the tea stall and the sweet shop, whose keepers and customers sell to the crowd at the +30% and +10% rise. Wages and rents already due are not cut and ready crops wait (they keep for 72 hours). **Not stopped:** the farm's shared Care ratio keeps ticking (crops keep growing), and the cattle are still fed and milked (they are not a person's hours in the sim); your own taps are not blocked. On the map from 7:00 to 22:00 the founder, hands, your people (everyone but a stall's keeper) stand in a crowd at the venue (at most 24 drawn, each at its own spot, walking there from home and back); the neighbours hired for the day are not drawn.

## Consequences

- The treasury statement gains a Festivals row; `Account.Festivals` joins schema 22 (the fixture is regenerated).
- Bots never hold festivals, so the balance pass sees no change; an *everything festival* bot is the pass's check.
