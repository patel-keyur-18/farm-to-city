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
| 10 | Developer panel → Next era until Township | A card "Set the town's budget" with each department's budget, what the town holds, a green note ("ignore this and nothing breaks"), **Change** and **Accept** | | Editor ✓ (`s21-139-window-card.png`) |
| 11 | Card → **Change**, or Town treasury → **Budget** tab | A strip "Year n budget · proposed" with **Accept** and a **?**; a row for each department with budget, needs, a **Change** button and a **?** | | Editor ✓ (`s21-139-budget-tab.png`) |
| 12 | **Change** on the office; drag or type; *Set …* | A slider 0 to 120% of its needs with the needs marked; under its needs an amber line saying it is paid last; the tab shows the new figure and "open" | | Editor ✓ (`s21-139-set-budget.png`) |
| 13 | **Accept** | The strip reads "locked until the Monsoon close"; no Change buttons | | Editor ✓ (`s21-139-locked.png`) |
| 14 | The strip's **?** and a department's **?** | The window's rules in words; the department's budget, need, pay and the sentence for the last close | | Editor ✓ (`s21-139-window-why.png`, `-dept-why.png`) |
| 15 | Last day of a season with the town short; open the bell | An urgent row with Give to the town, Change a tax (greyed, with its reason) and Revise the budget (greyed with "Locked until the Monsoon close") | | Editor ✓ (`s21-139-short-alert.png`) |
| 16 | After that close: the treasury sheet and the Budget tab | Meena Rao, the town planner, speaks under the banners; the office row says "Cash ran out: … went unpaid" | | Editor ✓ (`s21-139-planner.png`, `-variance.png`) |
| 17 | The Monsoon close | The bell has a Decide row "The midyear budget revision is open"; **Open** shows "Midyear revision · open" | | Editor ✓ (`s21-139-window-bell.png`, `-revision.png`) |
| 18 | Town treasury → **Taxes and policies** (the middle picture button) | A note "Taxes you set apply from next season" with **Got it** (once); the town's cash, "At the close"; a slider for the house tax and one for the business tax, each with its **?** | | Editor ✓ (`s21-140-tour.png`, `-taxes.png`) |
| 19 | Drag the house tax slider and let go | Under it: the change a season and who pays ("+125 · you 125"), the house still leaves you a profit, and whether the close is covered; a line "From Summer: 2.4% a year · now 1.6% a year" with **Undo** | | Editor ✓ (`s21-140-pending.png`) |
| 20 | **Undo** | The pending line goes; the slider stands on the rate in force | | Editor ✓ |
| 21 | Money and loans → Your season → *Next season, already due* | A row "House tax, n houses" with its **?** | | Editor ✓ (`s21-140-due.png`) |
| 22 | Advance one season; Town treasury → The town's books → the season | *House tax* and *Business tax* rows in the money in, each with a **?** | | Editor ✓ (`s21-140-statement.png`) |
| 23 | The **?** on a tax | Your houses and what the office collected, "never more than 15% of a house's rent", and why you pay it | | Editor ✓ (`s21-140-why.png`) |
| 24 | Town treasury → **Taxes and policies** → the **Policies** tab | Two tabs, *Taxes* and *Policies · 0 of 3*; a note "Policies: a trade, never a gift" with **Got it** (once); five rows, each with a cost a season, *Helps:* and *Costs:*, a **?** and **Enact** (greyed with a reason when it cannot) | | Editor ✓ (`s21-141-policies-tab.png`, `-rows.png`) |
| 25 | **Enact** on the Farm input subsidy | A card: costs the town, first paid at the close of (this season), works from (the season after the next), the town's bill at the close before and after with a ✓; **Enact** | | Editor ✓ (`s21-141-enact-card.png`) |
| 26 | After enacting | The row says "Paid at the next close; works from Monsoon" with **Repeal**; the tab says *Policies · 1 of 3* | | Editor ✓ (`s21-141-enacted.png`) |
| 27 | Advance two seasons | The row says *Working*; the valley's supply of each crop is 1.05 × | | Editor ✓ (`s21-141-working.png`) |
| 28 | **Repeal**, hold the red button | A card: stops at the next close, saves the town, afterwards "crop prices dip a little for two seasons"; hold a second to repeal; the policy waits a season before it can be enacted again | | Editor ✓ (card, `s21-141-repeal-card.png`) |
| 29 | The **?** on a policy | Its cost and how it is counted, when it starts, who it helps, who pays, and a plain paragraph | | Editor ✓ (`s21-141-why.png`) |
| 30 | Enact three, then try a fourth; enact free school meals with no school | The fourth is greyed: "Three policies are enacted already. Repeal one first." Meals: "Free school meals need the school." | | Sim and view tests ✓ (`PolicyTests.Slots`, `PolicyViewTests.States`) |
| 8 | Both frame-time p95s (main ≤ 8.3 ms, GPU ≤ 11 ms), golden replays, a 72 h catch-up with a treasury open | Pass | **Device only** | |
| 9 | Render statistics at the three views (all, village, farm) | No metric over 10% above the last reading | | Editor ✓ for US-138 (957 / 572 / 366 draw calls); US-139: 1038 / 558 / 346, set-pass calls +11–20% and triangles +19–28% flagged (a Monsoon map against a Spring reading; the sheets are UI Toolkit) |

**Your questions:**
1. Does the sheet read as the preview you approved? (The service bar is under the department's row, not beside it.)
2. Is it clear the money is the town's, not yours?
