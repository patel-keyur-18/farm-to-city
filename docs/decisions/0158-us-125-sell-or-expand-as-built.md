# 0158. US-125 sell or expand as built: a worth from the books, a sale as the close-down at the sale's price, tax at the sale, a branch as a second of a kind

- Status: Accepted (the owner approved Preview 17 and gap 9, 2026-11-22; the story is for the owner's review)
- Date: 2026-11-22
- Deciders: Keyur Patel
- Related ADRs: 0109, 0149, 0154, 0155, 0157

## Context

US-125 (spec 028 §3.3): learn what a business is worth, sell it, or open another of the kind. Gap 9 was closed with the owner before code (ADR 0155): the sale reuses the close-down path, the gain is taxed at the sale, a gone business drops its policy and the cards aimed at it, and no valuation history is saved.

## Decisions (as built)

1. **The worth** (`ExitRules.Valuation`, numbers in `content/exit.json`): `max(what it holds, multiple × a year's profit)`. A year's profit is the kept seasons' (up to four) scaled to a year, at least 1 coin; a business with no season yet uses its plan. The multiple is 4.5, +1 when the last two seasons earned 10% or more above the two before (−1 for 10% or more below; read only with four seasons of books), −1 when any kept season lost money or there are fewer than four, +1 when it has a manager and four profitable seasons, kept between 3 and 6. What it holds is its own books' equity, never below 0, so a sale never ends in a paper loss. Nothing is saved for it (no `ValueHistory`): it is computed from the four kept reports.
2. **Listing** (`ListBusiness`, `CancelBusinessSale`): open businesses only. A seeded draw (stream `Exit`) picks the kind of buyer (a townsperson, a rival or a regional buyer), the days (3 to 6) and the share (90 to 105%); a rushed sale is one day at 85%. The business runs until the day it closes; you can cancel any time before. The price is the worth *on the day* at that share, never under what it holds.
3. **A sale is the close-down at the sale's price.** Its worker and manager are paid severance (10% of the wage after a first season) from its cash and let go; what customers owe it is collected and what it owes other businesses is paid; the building and equipment go at book value and the stock at cost (not the 60% and the trader's price of a closing); its overdraft, loan and owed tax are repaid. The buyer pays the price for what the business holds, so the gain is the price less what it held; it is booked as the gain on sale, and **business tax is billed on the season's profit with the gain** (ADR 0109), the treasury is paid what is billed, and the rest comes to you as drawings (the part above your capital as drawings income). The acre is cleared and yours again; a pitch is freed. The card (`SaleRecord`, the last eight kept in the Township's `ExitState`, schema 23) says where every coin went; a bell alert opens it for two days.
4. **A gone business drops its policy and its cards** (ADR 0154's flagged items, fixed): `CloseDown`, which a closing and a sale both use, drops the business's insurance policy and ends quietly the waiting cards aimed at it (supplier strike, inspection, rush, a breakdown or fire repair of it). A warned fire already finds nothing to burn.
5. **A branch is a second business of a kind you already have.** There is no separate command: founding one on another acre or pitch makes it a branch of the first (`BranchOf`), starting as the first one's hours, fee, price, quality, mix and stock (its research and plan default), with its own books, manager and worth. At most two branches of a kind (`maxBranches`), from the Township; the third is refused (`TooManyBranches`). The Sell or expand sheet shows how many and opens the same research and founding screens. *Why:* the spec says "paid like the first"; one founding path means a branch can never be cheaper than a business.
6. **Screens** (Preview 17): Settings has a *Worth* section (the worth with its ?, and *Sell or expand*); the sheet shows the worth and what it holds, *List it* and *Rush it* (each a hold: a tap can never sell your business), the listing's days left and *Cancel the sale*, and the branch row. Four *Why?* sources: the worth, what it holds, how a sale is priced, branches. The *Who owns it* row of Preview 17 comes with US-247.

## Consequences

- A sale is a real money flow: the sprint-end balance pass runs `GateTests.SellBuyBack` (found, sell at book, found again) and, if it shows a loop, drops the floor to 80% of book (a number, no re-approval; ADR 0155).
- The rushed sale is cancellable (spec 028 §3.3 says any time before completion); Preview 17's words "no taking it back" are corrected in the sheet: "You can still cancel before it closes".
- The business loan is not on the business's books, so "what it holds" counts the loan's cash and the loan is repaid from the proceeds; you receive the price less the loan, as with a closing.
