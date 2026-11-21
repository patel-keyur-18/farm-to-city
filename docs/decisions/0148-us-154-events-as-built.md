# 0148. US-154 events as built: one roll at 6:00, cards with named defaults, two disasters warned two days ahead

- Status: Accepted (the owner approved US-154, 2026-11-21)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related: spec 027 §3.1–§3.4, §7, Preview 12 (approved 2026-11-21), ADR 0002, 0063, 0141, 0147

## Context

Spec 027 sets the engine, 19 events (18 built here; the investor's offer comes with spec 028), the fire and the epidemic. Preview 12 drew the cards and the owner approved it with two answers: the drought card's clock is 48 hours (the spec's 23 h sketch was a minor's), and the fire's repair card takes **Repair slowly** after 24 hours when unanswered.

## Decisions

1. **One roll a day at 6:00** by the hourly pass of a new `EventsSystem` (system order Events, after the treasury). A major (1 in 16) is drawn first; if one comes no minor that day. Seeded by the day (`RngStream.Events` = 21). The first 6:00 that sees the Township marks the start; no event in the next 2 days.
2. **Cards.** `EventsState` (inside `TownshipState`, schema 22) holds the cards waiting (at most 2; a third day's roll is skipped), the last 8 answers, the mercy rules' marks, what is running (price and cost shifts, happiness lifts, flags, shut buildings, the epidemic), the rain log and the one disaster signalled. Every choice is the command `AnswerEvent`; the first choice is the default and costs nothing (except the fire's repair, whose default is the slow one: half the bill, paid as far as cash goes).
3. **Away.** A minor takes its default at once and is on the away card; majors and disasters never roll while away, and a signalled disaster slides to the next live 6:00, at most 3 days, then passes (`DisasterPassed`).
4. **The mercy rules** are in `Draw`: a kind once a season, one major a season, the crop majors rest the next season, a major whose dearest choice costs over 40% of cash is not drawn, and no roll on a season's first day when a petition is promised. Weights follow §3.3 (modifier numbers live in `events.json`, the gates are code); the product is clamped 0.25–4 and each card says which modifier made it likely.
5. **Effects** are the closed list (§3.2) applied by `EventRules.Apply`: crop health is one multiplier, `Crop.EventHealth`, multiplied into the yield with the pest health; price shifts act in `MarketRules.RegionalPrice`; fuel spikes in diesel and pump power; a supplier strike in a business's import price; happiness lifts in `HappinessRules`; shops' demand in `ShopRules`; shut businesses in `BusinessRules.OpenAt`; the epidemic in `ClinicRules.SickChance`; a parked tractor in `HiringOut`.
6. **Where the spec was made exact:**
   - **Hail** lands as the card comes (a third of the standing fields, rounded up, seeded, −35%); the choice is to keep the crop or clear and resow the damaged fields (seed at today's price, the crop starts again).
   - **Viral rush:** carrying on serves about half of the extra customers (+20% demand), stocking up all (+40%).
   - **Supplier strike** raises only the struck business's inputs; a second supplier halves the next one.
   - **Epidemic** changes only sick days (no health dip), so nobody can die of it; closing the school and the shops cuts the shops' demand 30% for 3 days (lessons are not paused: the school has no lessons in the sim).
   - **Mediating a dispute** takes one hour off the founder's day (`TodayHours`).
   - **Skilled migrant** adds a four-star, skill-known applicant to the open post (no post fee).
   - **Regional grant**: 400 now; the grant is paid at the next close if the last season's farm profit was above 0 and no loan is in default, else it lapses with a line on the away card.
7. **The books.** Three new accounts (schema 22): *Disaster loss* (5280, a fire's repair), *What you paid for events* (5285) and *Regional grants* (4350, other income); none is farm profit.
8. **Screens (Preview 12).** An event card (amber for a disaster, with the countdown), the Events sheet in the Menu, a bell entry for each waiting card (urgent for a disaster), the amber banner "A fire risk in 1 d 23 h" over the clock, away lines, and a card that comes up on its own when no sheet is open. The map draws flames and smoke on a building a fire has shut (Blender: `fire_flames`, `fire_smoke`; icons `flame`, `health`, `shield`, `scroll`, the last two for US-155 and US-156). Paid choices ask on the usual card; a free one just answers. Every card number has a *Why?*.

## Consequences

- Save schema 22 gains `EventsState`, `Crop.EventHealth` and the three accounts; the fixture is regenerated. A town that has not reached the Township hashes as before.
- No event rolls before the Township, so the Village's and the Township's pace are unchanged; the balance pass measures a left-alone year, an *answer everything* and an *insure everything* bot (US-155).
- Not built: the investor's offer card (spec 028), insurance (US-155) and contracts (US-156); `eventPart` stays 1.5% for the balance pass.
- Carried, with owner's agreement (2026-11-21): the fire's flames are drawn at the acre's centre (US-154.T7 moves them onto the building, before the sprint review) and the shield icon's tick is redone with US-155 (US-155.T0). The epidemic's "lessons paused" was dropped from spec 027: the school has no lessons in the sim.
- Corrected in ADR 0149 §4: a business's building could not burn as first built (the fire chose only from the buildings on your own books); it can now.
