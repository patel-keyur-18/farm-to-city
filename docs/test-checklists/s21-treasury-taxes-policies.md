# Sprint 21 build: the town treasury, the budget, taxes and policies

- **Build:** a Development build from the head of branch `sprint/21-treasury-and-taxes` (made at the end of the sprint; see the pull request).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s21-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 18 to 19): the town gets a treasury that has not opened yet. Nothing else changes. The Township has no gate until the next sprint: only the developer panel's **Next era** reaches it, and it opens the treasury once.

## What this build changes (grows story by story)

- **The town treasury (US-138).** From the Township the town has its own money, shown in Menu → **Town treasury**: a second wallet that is not yours. The district gives a start-up grant and each season a grant for every resident; your shops' pitch rents are its income; it pays the clerk and the school's upkeep (the school's upkeep stops being yours). It never goes below zero: what it cannot pay goes unpaid and the service slips. You may **give to the town** (a gift, never repaid) and read **the town's books**: money in and out, cash flow and what the town has, each season beside the one a year before, every number with a **?**.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Developer panel → Next era until Township; wait a moment | A toast says the district gave the town its start-up grant and that it is public money, not yours; the Menu gains **Town treasury** | | Editor ✓ (touch; toast and Menu row) |
| 2 | Menu → Town treasury | A green line "Town money: not yours"; the balance; "At the close: Covered ✓"; two picture buttons; money in; money out by department with each one's service | | Editor ✓ (`s21-138-treasury.png`) |
| 3 | Tap each **?** on that sheet | A Why? in words with the movements behind it; never a factor | | Editor ✓ (balance); `WhyTests`, `TreasuryViewTests` for every row |
| 4 | Give to the town: drag or type 1,000, tap *Give 1,000* | A card with your gift, the town's balance after and your cash after, "It's a gift: you can't take it back"; *Give for 1,000 coins* gives it; back on the treasury the balance rose and your cash fell | | Editor ✓ (touch; both ledgers checked) |
| 5 | Advance one season (panel); close the season page and the letter; Town treasury → The town's books → the season → three tabs | One season listed with its surplus and cash; money in and out, cash flow and what the town has, each row "First year", each with a **?** | | Editor ✓ (`s21-138-books.png`, `-statement.png`, `-has.png`) |
| 6 | A town whose treasury would come up short at the close | A banner and "At the close: Short by …" name the department that would go unpaid | | `TreasuryViewTests.Sheet_Short`; the Editor town is never short |
| 7 | Build the school, then reach the Township and wait for the close | The school's upkeep is the town's Education line: your *Next season, already due* has no school row and you are not charged | | `TreasuryTests.SchoolTakeover` |
| 8 | Both frame-time p95s (main ≤ 8.3 ms, GPU ≤ 11 ms), golden replays, a 72 h catch-up with a treasury open | Pass | **Device only** | |
| 9 | Render statistics at the three views (all, village, farm) | No metric over 10% above the last reading | | Editor ✓ (957 / 572 / 366 draw calls; none grew) |

**Your questions:**
1. Does the sheet read as the preview you approved? (The service bar is under the department's row, not beside it.)
2. Is it clear the money is the town's, not yours?
