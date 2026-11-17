# 0117. Taxes as built (US-140): rates in whole units, a bill that follows the office, tax carried when unpaid, a first-visit note on the sheet

- Status: Proposed (for the owner's review of US-140, 2026-11-17)
- Date: 2026-11-17
- Deciders: Keyur Patel
- Amends: spec 022 §3.1 (where the guard and the assessed-as live), §3.2 (when the treasury is paid), §3.7 (the first-visit tour's form), §7 (the status line)
- Related ADRs: 0109, 0108, 0114, 0115, 0116

## Context

Building US-140 (spec 022 §3.1–§3.4) raised small choices the spec leaves open. None changes a rule the owner approved.

## Decisions

1. **A rate is set in whole units**: a house rate in tenths of a percent a year (16 = 1.6%, 0 to 24), a business rate in whole percents (10, 0 to 20), so the slider can stand on every number it shows. A rate set for the next season is *pending* until the close rolls it into force; setting the rate in force again clears it.
2. **The bill follows the office.** The council's office collects `officeBase + officeWeight × condition` of what is billed and the payer pays only that. The treasury is paid what is actually paid: a payer who is short pays what his cash allows and the rest is carried as *Tax owed to the town* (2400) and paid first at the next close, so the treasury never books money it has not received.
3. **Two periods.** The treasury's income is of the season that ended (the money was billed for it). Your house tax is posted in the new season, like the school's upkeep and the other charges the close takes, so *Spent* and *Next season, already due* show it where the player pays it. A business's tax is posted in the closing season, so its report reads profit before tax, the tax, then profit after.
4. **The village's shops pay from outside the model.** The grocer, the tea stall and each newcomer pay the rate × a reference profit (indexed); the treasury is paid, no ledger is charged. The town's halwai is not the village's and pays nothing.
5. **Money is computed with `Money.MulRate`** (rounded once to the nearest hundredth), never by flooring a `Fixed`: a rate like 0.15 sits an epsilon below 15% and flooring dropped a hundredth.
6. **The guard is checked when content loads**: at the top rate a house's tax is no more than 15% of its kind's base rent (the loader refuses content past it), and per house it is capped at 15% of a let house's actual rent.
7. **Content.** `taxes` (rates, bounds, `maxShareOfRent`, `homeAssessedAs`, `referenceProfit`) is in `treasury.json`, not `shops.json` and `buildings.json`, so the optional treasury file holds everything the treasury needs.
8. **The status line has a third state.** "At the close" says *Covered ✓ (n to spare)* when the close's expected income meets what is due, *From the balance: lasts about n seasons* when it does not but the cash covers it, and *Short by n* otherwise. The expected income counts the grants, the pitch rents already paid and the taxes at the rates that will bill (your businesses on last season's profit). The warning (spec 021 §3.11) still counts only what is certain.
9. **The first-visit note is a banner at the top of the Taxes sheet with *Got it***, not a floating pill (`SeeTaxTour` keeps it from showing again). Spec 022 §3.7's first tour chapter for the treasury's opening stays with the Codex card *Public money*.
10. **Not yet**: the old village's rows and happiness in the preview and the bill (the cohort arrives with spec 023), and the Policies tab (US-141).

## Consequences

- Save schema 19 grows once more (the rates, the pending rates, the tour flag; accounts 2400, 5270 and 5830); the schema 19 fixture was regenerated. Schema 19 has not shipped.
- A town with a treasury now has the shops' tax as income (about 2,200 a season at today's prices), so the Township's break-even includes it.
