# 0113. Orders go to the hand you pick; skill and Care are shown, not left to guess

- Status: Accepted (owner, 2026-11-14: the screens previewed as an artifact and approved)
- Date: 2026-11-14
- Deciders: Keyur Patel
- Amends: spec 006 §7 (the order flow), AC-29 to AC-31; spec 014 §7 (the citizen card's skill); ADR 0039 §8 (orders from the map)
- Related ADRs: 0039, 0048, 0059, 0083 (a tenant hired as a hand)

## Context

The owner hired a family-house tenant as a second hand and could not give them an order. Cause: orders were only started from a field's sheet, and a field with no order always offered the first hand hired (`PeopleWords.OrderHand` returned `Hands[0]`). The hand's own sheet had no way to give one, though the People sheet said "See to give one". Two smaller gaps came with it: a star changes about every 7 seasons (skill +3 a season, a star is 20 points), and nothing on screen showed the slow growth; a tenant's citizen card read the skill the person had when hired, not the hand's grown skill. And Care, one pool for the whole farm, was never said to be a pool: the *Why?* lumped every hand into one row.

## Decisions

1. **A hand's sheet has *Give an order*.** It opens a list of the farm's fields (nobody's first, each with what grows there; then fields another hand has, with *Give to Manoj*, which moves the order and says who stops). The sheet's header counts "Orders · 2 of 6"; with six the button is greyed with the reason. Trees and the animals keep their own entry (tap them on the map; the animals row stays on the hand's sheet).
2. **A field or tree with no order asks who** when there are two or more hands: each hand's stars, band, orders used and acres tended; a hand with six orders is greyed ("Has 6 orders already. Remove one first."). With one hand nothing changes. A field that has an order keeps going to its hand; to move it, use *Give to* on the other hand.
3. **The chosen hand travels in the sheet's Arg** as a prefix (`@12;radish+compost|once`), set only when the target would not go to that hand anyway, so a single hand never sees it. No command or save change: `SetStandingOrder` already carried the hand.
4. **Skill is shown with its growth:** "41 of 100 · grows 3 a season · the next star at 50, in 3 seasons", using the hand's traits' gain. The citizen card of someone working as a hand reads the hand's skill (the hand's is the one that grows; it is copied back when they leave).
5. **Care is said to be a pool.** The People sheet's Care line adds "Your crops need 6.5 hours a day. You and your helpers give 19.6."; a hand's sheet has a Care row with the same sentence and what a hand does ("their hours join everyone's"); the *Why?* has one row for each hand (with "away on a course" or "off sick" when they add nothing), the labourers together, and a line that a hand with no orders still adds to Care.

## Consequences

- Spec 006 AC-29 (who looks after it), AC-30 (a hand gives an order), AC-31 (skill and Care shown); tests in `HandOrdersViewTests`.
- Sim.View and presentation only (`GameRoot` routes `OrderWho` and `OrderFields`, keeps the chosen hand through a fertiliser tap); no sim, content-numbers or save change; no golden moved.
- Orders still act at once and Care is still one pool: a hand's orders choose what is grown, never where their hours go.
