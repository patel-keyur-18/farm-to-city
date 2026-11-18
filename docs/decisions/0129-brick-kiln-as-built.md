# 0129. The brick kiln as built (US-243)

- Status: Accepted (owner, 2026-11-18: Preview 3 approved with the four changes below)
- Date: 2026-11-18
- Deciders: Keyur Patel
- Amends: spec 025 §3.3 (as-built note there)
- Related: ADR 0068, 0069, 0125, 0126, 0128

## Context

Spec 025 §3.3 asked for a brick kiln: a Township business with two workers, a *kiln fuel* good, a 3,00,000 cost and smoke that lowers land value and health. Preview 3 showed nine sheets; the owner approved them and the four places where the build differs from the spec.

## Decisions

1. **One kiln hand, not two.** The hiring board hires one worker for each business. The hand's rate is 500 bricks an hour, so a day of ten open hours is 5,000 bricks at skill 50; his skill moves it as it moves every worker's output. Run by you, it is the same rate.
2. **Fuel is a cost in the brick, not a good.** The kiln buys fuel as it burns it, 0.4 kg a brick at 4 coins a kg (indexed), books it into the bricks' cost, and the clay is free. No *kiln fuel* in Buy inputs or the Store.
3. **Its own building takes its build hours (72), like the mill, bakery and dairy.** The kiln is not a construction project: it would otherwise wait for the very bricks it exists to make.
4. **It stops in the Monsoon.** Wet clay can't be fired: nothing is made, the wage, wear and repairs still come, the yard is still sold, and the plan's payback counts the year (three seasons that fire, one that costs).
5. **Where the bricks go.** Your own builds take bricks from the yard first, at what they cost to fire (the card shows the saving). Each morning at 6:00 the trader buys up to 5,000 from each kiln at his price less a tenth, and what he buys is added to his daily stock for every build in town: the treasury's, the builders' and yours. The yard holds 10,000; weather spoils none; a closing kiln's yard goes to the trader. The treasury never buys from your kiln (ADR 0108: nothing flows to you).
6. **Smoke is one shared rule** (`SmokeRules`, `township.json` `smoke`): the kiln takes 0.04 of land value from its acre and the four beside it (150 m, an acre is 100 m, Manhattan) and 3% of health from the people of homes there each season; the generator (US-144) takes 0.06 and 5% within 250 m; the total for an acre is capped at 0.10. The placement ghost draws the footprint in red and the card counts what it touches.
7. **Numbers are placeholders for the balance pass:** 500 bricks an hour, trader takes 5,000 a day, fuel 4, building 1,60,000 and equipment 1,40,000, wage 30,000. At today's prices the plan reads about 7 seasons hired. With a kiln, cement (30 bags a day, 1.9 houses a day) becomes the next limit.

## Consequences

- Save schema 21 gains six numbers on every business (the yard, its cost and the day's and season's counts) and `KilnBricks`/`KilnDay` on the Township's construction block; both are hashed only once used. The schema 21 fixtures were regenerated (the schema is this sprint's).
- Content: `businesses.json` `brick_kiln` with a `kiln` block; `township.json` `smoke`. Art: `brick_kiln` and `brick_kiln_building` sprites rendered in Blender (four facings).
- Tests: `KilnTests` (7), `KilnViewTests` (6); every kiln number has a *Why?*.
