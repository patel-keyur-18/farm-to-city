# 0174. US-267 the Businesses hub, as built

- Status: Accepted (Preview 23 approved by the owner, 2026-11-26; the story awaits the owner's review)
- Date: 2026-11-26
- Deciders: Keyur Patel
- Related ADRs: 0172, 0173, 0068, 0092

## Context

Spec 030 §3.3, §7.1, §7.2 and Preview 23: one sheet with all your businesses, one round HUD button to open it. View and presentation only: the hub reads the books and the ledger and changes nothing, so no sim, save or golden change.

## As built

1. **The sheet** (`BusinessHub`, `Sim.View/Views/BusinessHubViews.cs`, kind `BusinessHub`). At the top, the profit this season so far and the cash across your businesses, each the sum of the rows below; *Start a business* (opens Menu → Businesses, the start list, with Back to the hub); then each business:
   - a head row: picture, name, a *Needs you* tag, stars (only where it earns them, the same *Why?* as the business card), "status · who runs it", *See* (its card);
   - when it needs you, an amber line with the reason ("Nobody works here: hire someone on the board." or "Overdraft used up: it can't buy inputs.");
   - *Profit this season* (its Books' `NetProfit` of `Report(b, sim.Period)`, the same figure as its Books tab);
   - *Cash in the …* (its own ledger cash) with *Take out*.
   Order: open first, then being built, each by id; closed businesses are not listed.
2. **Needs you** is `BusinessBooks.Short`: open with nobody working it, or its overdraft used to the limit. Spec 030 §3.3 also listed "you run more than one"; US-266's card makes that unreachable, so it is not a reason.
3. **Take out** opens the existing amounts sheet. It is closed with "Nothing to take out yet." when the business has nothing free to take (`BusinessBooks.Drawable` is 0: a business being built, or all of its cash owed to outside owners). After taking cash you are back on the hub (the sheet behind it) with the totals changed, so you can take from the next business; from a business card's own Take out the return to its Books is as before.
4. **The HUD button** (`HudVm.HasBusinesses`, `BusinessesBadge`): a fourth round button left of Orders (right 538, bottom 28, 150 round), shown once a business is open or being built; the red badge counts the businesses that need you (never wrapping, "99+" cap). The picture is the flour mill's, as Menu → Businesses (owner, 2026-11-26); Sprint 32 redraws every icon.
5. **Every number has a *Why?*:** `hubProfit` (id 0 the total with a row for each business; a business id the lines of its Books: income, cost of what was sold, running costs), `hubCash` (the total with a row for each), and the existing `businessCash` and `shopStars` for the rows.

## Differences from Preview 23

- The sheet uses the game's own row layout: a row with a button shows its name, then its value and note under it (the preview drew the value on the right). Plain rows (the two totals and each business's profit) show the value on the right as drawn; they carry no sub-line, so "all businesses" is in the total's label.
- The "Today: from you / bought" supply line is not here: it comes with US-268.

## Tests

`BusinessHubViewTests` (`Hub`, `HubWhys`, `HubNeedsYou`, `TakeOutAndOrder`, `ButtonWaitsForABusiness`). Editor by scripted touch (captures `docs/art/s28-267-*.png`): the HUD button with its badge, the hub with a business that needs you, Take out and back on the hub with the totals changed, See and Back, Start and Back, both *Why?*s; `SheetLayoutCheck` 78 sheets, 0 problems. Render statistics: all 1,297 draw calls / 25,683 triangles and the village 572 / 11,651 within 10%; the farm view's triangles +13% (6,539 → 7,364) because the test town was advanced (a new season's crops and two shops built), not the button (hiding it changes the farm view's triangles by 2).

## Consequences

- The hub is the place US-268 adds the day's supply to each row.
- The business sheet's *Take out* is unchanged.
