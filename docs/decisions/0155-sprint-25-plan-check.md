# 0155. Sprint 25 plan check: business growth in eight stories, 39 points, save schema 23, ten gaps to close before their code

- Status: Proposed (shown to the owner before any code; accepted when the owner approves it)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related ADRs: 0027, 0057, 0068, 0069, 0074, 0079, 0080, 0091 to 0093, 0107, 0109, 0141, 0144, 0148, 0151, 0153, 0154

## Context

Sprint 24 is merged (PR #35). Sprint 25 builds spec 028 (approved, ADR 0153) from the Township's businesses outward: invoices, managers, sell or expand, investors, benefits and night shifts, the tuition centre, the tempo and the restaurant. As for Sprint 24 (ADR 0144) every story was checked against the spec and the code at `main` (c32cecb4) before any code. Both test projects pass on the branch (`Sim.View.Tests` 381, `Sim.Core.Tests` 794 without Balance and Perf).

## What the code at `main` gives us (reuse, not rebuild)

| Need | Already there |
| --- | --- |
| Business books | each `Business` has its own `Ledger`; `BusinessBooks.Equity` is assets minus liabilities, so a receivable and a payable between your own businesses cancel in net worth by themselves |
| Internal sales | `BusinessRules.ToYourBusinesses` (mill to bakery) and `ShopRules.FromYours` (product to your stalls), both bounded by `CanSpend`; `Sold` books the seller's side |
| Closing and settling | `BusinessBooks.CloseDown` pays the overdraft, the loan and the owed tax, sweeps cash to you, clears the acre; a sale is the same path with a better price |
| Reports for a valuation | `BusinessBooks.KeptReports = 4`, exactly the four seasons the valuation reads |
| Cards with numbers | `PendingEvent` carries `Target`, `Target2`, `TargetTable` and `Param`; the engine already raises cards it makes itself (`Rolled = false`: the fire's repair) |
| Hiring | the board, `Worker` (skill, wage, `Hr`), contracts, raises, poaching (5%), sick leave, employer reputation |
| Machines | `farming.json` `machines[]` with `cartKg`, `HireOut`, a park tile, breakdowns, leases and loans |
| Shops | `ShopKind`, pitches and rent, `Attract` and the score, stars, rivals and newcomers |
| Households | `Schooling` and `Leisure` budgets per household; `Savings` |
| Tax | `TaxRules.BillBusiness`, `PayOwed` |

## Spec findings and gaps (each closed with the owner, one per message, with my recommendation, before its story's code)

Nothing here changes a rule the owner approved; these are holes between the spec and the code. A decided gap is written into spec 028 §10 and the story's ADR (as built).

**US-123 invoices**
1. **Credit limit.** A buyer's purchases today stop at `CanSpend` (cash plus overdraft headroom). *Recommend:* keep that test at the moment of purchase, so every quantity is what it is today and only the payment moves to the term's end; a buyer turns late when its cash and overdraft cannot cover the invoice at term end. (The alternative, payables outside the limit, lets a buyer take unlimited credit.) Balance targets stay as they are.
2. **Wholesale buyers are new demand.** The grocer and tea stall never buy from you today. *Recommend:* the order of ADR 0151: your own shops, then the trader (cash, his daily room), then the grocer and tea stall for what is left, 20 kg a day each at the trader's price, on the buyer's terms. A wholesale invoice then never replaces a cash sale; it sells what the trader's capacity could not. The bakery and dairy targets (ADR 0079, 0080) are re-measured.
3. **"Neighbours' milk to your dairy" is on §3.1's invoiced list, but neighbours are not businesses and farmers sell for cash.** *Recommend:* keep it cash (as the 6:00 pickup is today) and correct the spec line.
4. **The restaurant's ingredients.** §3.7 calls a purchase from your store "an invoiced sale under §3.1", but §3.1 keeps the farm's own sales cash. *Recommend:* farm to restaurant stays cash like every shop (`FromStore`); correct §3.7's parenthesis.

**US-124 managers**
5. **What a style sets on each kind.** The spec names price, quality and stock days, but a mill has a fee, a bakery or dairy a mix and sells to the trader, a kiln neither. *Recommend:* every kind may have a manager; a style sets what that kind has (shops: price preset, quality, the morning's stock; mill: fee preset and quality; bakery, dairy, sweet shop: the plan's mix kept and days of inputs bought ahead; kiln: the hire rule and mis-set days only), and the sheet shows only the lines that apply.
6. **A manager on a business you run yourself.** *Recommend:* he takes your place at once and hires the worker his style says (a job posted on his first morning, the wage rule of §3.2), so you pay his wage and a worker's, and your hours are freed.
7. **A mis-set day.** *Recommend:* a seeded draw by the stars (§3.2's 20% to 2%); on a mis-set day that day's sales are halved (a shop is out of stock, a batch mistimed), so one star loses about 10% of sales, five about 1%, as the spec says.
8. **"Managed profit at least the hired profit"** cannot hold for a cost-cutter on a good business (he costs 4,000 plus 10%). *Recommend:* §9 says "measured, reported" and the sheet shows his wage against what his style gained; the balance pass reports the gap honestly and tunes the wage or the styles without re-approval (ADR 0027).

**US-125 sell or expand**
9. **Sale, tax and insurance.** The sale reuses `CloseDown` with the sale price; a sold business leaves the close's billing, so the gain over book is taxed **at the sale** (business tax, ADR 0109) and a pending gain tax is paid first. A sold or closed business drops its policy: **ADR 0154's flagged item** (`InsuranceState.Businesses` keeping closed businesses' policies) is fixed here, with a test. Cards and effects aimed at a business that is gone (a fire, a buy-out, an investor) end quietly (the second flagged item's pattern, tested). `ValueHistory` is **dropped from schema 23**: a valuation is computed from the four kept reports, so nothing new is saved for it.

**US-247 investors, partners, buy-outs**
10. **Offers on the engine, outside the cap.** Investor, partner and buy-out cards are raised by the growth pass at the close (live only; never while away, spec 027's mercy rules), as non-rolled cards through the engine's card plumbing (the 24 or 48 h default, the record, the *Why?*), and **do not count toward the engine's two waiting cards**; one such offer a business at a time. A share is carried as a liability at the cash received; buying it back at valuation × share books the difference as a cost or gain in the season it is bought back; a partner's savings move from the household to the business's capital and his profit share is paid into the household's savings at the close.

**Risks noted, not gaps**
- **US-258** is 3 points, but a night shift needs a second worker slot in each production business (`WorkerCitizen` is one). Benefits stand alone; the night shift is the last task in the story and is cut first if it overruns (the story then ships benefits and the night shift moves to Sprint 26).
- **US-244 and US-246** spend the `Schooling` and `Leisure` budgets, which today are set aside and spent nowhere (a household's savings are income less those lines). Spending them on tuition and meals adds revenue without touching a household's savings, so the Township's pace stays as measured (day 102 and 137). Your households only (named children, so the skill gain is real); the old village cohort's budgets are left unspent. At Township size (about 20 households) a centre fills slowly: its targets (§9) are tuned from the bots, the fee presets and capacity are the knobs.
- **US-245** needs a vehicle per mandi trip (`MandiTrip` is one trip of the global `TripHours` today): a `tripKg`, `tripHours`, `tripCost` and `perKg` on the machine content, two trips a day for the tempo, one as before without it.
- **US-125's "never under book"** makes a loss-making business free to sell at its book value. `GateTests.SellBuyBack` tests the loop; if one shows, the floor drops to 80% of book (a number, no re-approval).

## Decisions

1. **Order of work** (spec 028 §11, the owner's list): US-123, US-124, US-125, US-247, US-258, US-244, US-245, US-246. Previews 16, 17 and 18 are exact artifacts, each approved before its screens; I make them first, and the stories' sim code does not wait for them.
2. **Cut line** (spec 028 §11, unchanged): US-246, US-245, US-244, US-258, US-247, US-125. **Never cut:** US-123, US-124, every number's *Why?*, the schema 23 fixture, the exploit checks (AC-31 for this sprint's flows).
3. **Points:** 39 = US-123 5, US-124 8, US-125 5, US-247 5, US-258 3, US-244 5, US-245 5, US-246 3. Sprint 26 keeps US-259 and US-260 (schema 24).
4. **Save schema 23**: every new field is hashed only when used, so a town that uses none of it hashes as before (the goldens do not move). New ledger accounts (a trade receivable, a trade payable, outside equity, the sale's gain and the benefits' costs) are appended at the end of their groups. Fixture `schema23-growth.ftc` (invoices, a manager, a listing, an investor and a partner, a tuition pupil, a tempo, a benefit) and `MigrationTests.Schema23_Growth`; every older fixture loads.
5. **Left from Sprint 24 and what happens to it:**
   - ADR 0154's flagged minors: the closed business's policy and the gone-target card, fixed in US-125 and US-247 (finding 9); the "about 0.9 events a day" wording and the saved `LossTotal`, `FireBill` and `FireTarget` stay flagged (spec 029 reads them in Sprint 26).
   - **The Editor re-check of what the review changed** (the event card, the banner, the Money page's premiums, checklist rows 7 and 11 to 12): done in Sprint 25's first Editor session, before US-123's tour.
   - **Device-only rows of the Sprint 23 and 24 checklists** (10, 23 to 27; frame time, golden replays, the 72 h cap): the owner's, with Sprint 25's Development build; Sprint 25's checklist carries them forward.
   - The unexplained first throw behind the 2026-11-20 device crash (ADR 0139) stays open; the per-system guard now names it if it recurs.
6. **Sprint-end work** (as Sprint 24): the balance pass (the 20% rule; the Village and Township pace; the new shops' targets; managed against hired; invoices' cash gap; exploit bots `InvoiceLoops`, `SellBuyBack`, `InvestorDilution`, `StackBenefits`, `NightNoDemand`; a 200-seed hands-off Township with the new features unanswered), render statistics for each visible story, Editor saves backed up and restored and diffed, the independent Opus 5.5 high-effort review, the checklist, ADRs as built, `STATUS.md`, the pull request with labels, milestone and project.

## Consequences

- Ten small decisions come to the owner one per message, each before its story, so the owner is never waiting on one.
- Nothing in this sprint starts before the Township, so the earlier eras and the pace are unchanged by construction; the sprint-end pass proves it.
- Spec 028 gets three line corrections (neighbours' milk cash, the restaurant's store purchase cash, the dropped `ValueHistory`) when the owner approves them.
