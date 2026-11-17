# 0118. Policies as built (US-141): a policy is a line in a department, paid in full or paused, one effect each, the night market left out

- Status: Proposed (for the owner's review of US-141, 2026-11-17)
- Date: 2026-11-17
- Deciders: Keyur Patel
- Amends: spec 022 §3.5 (the subsidy's price an acre, the grant's winner, the night market), §3.6 (when a slot is free), §7 (the sheet's shape)
- Related ADRs: 0109, 0108, 0114, 0115, 0117

## Context

Building US-141 (spec 022 §3.5–§3.6, AC-9 to AC-11, AC-13, AC-15, AC-16) raised choices the spec leaves open. None changes a rule the owner approved; each keeps a town left alone working, and keeps the owner's rule that a policy is a trade an average player can read in one line.

## Decisions

1. **A policy is a line in its department.** The treasury's close already pays lines in two passes (a budget is a priority list), so each policy is one more line (`policy:meals` and so on) in Education, the Council's office or Policies, as the spec says. Nothing new pays money.
2. **Paid in full or paused.** A policy line is paid whole or not at all: a close that cannot pay it in full *pauses* it (no effect, nothing charged, the slot kept, the planner and the away card say so); it tries again at the next close. A repealed policy is never paused into silence: it ends.
3. **Lag counts from the first payment.** A policy is paid at the next close for the season ahead, and its effect starts after `lag` more closes (lag 1: "Setting up: works from Monsoon"; lag 0 for the minimum wage and rent control, which work at renewals anyway). A policy repealed before its first payment ends at once and owes no wait; one that has run ends at the next close and waits a season.
4. **A repealing policy frees its slot at once** (it still runs to the close), so "three at a time" never makes the player wait to swap one.
5. **The subsidy costs 125 an acre, not 250.** Spec 022 priced it at 250 an acre on "neighbours' farmed acres" and quoted about 15,000 a season; the map counts 123 acres of farm land you don't own, so 250 would be 30,750. The cost an acre is content: 125 gives about 15,400. The count is farm land not owned by you.
6. **The grant is one roll a season, not a named person.** The spec names the first adult with no town job and a shop "run by that person". Here the grant, once running, rolls its 40% waste each close (seeded stream `Grants`); a success opens a door for a newcomer (the newcomer's chance × 1.5, and a newcomer may open on a grant without a price gap), a failure is spent for nothing. A town job and a person are for spec 024's rentals and spec 023's cohort. A grant costs nothing when there are no residents.
7. **The minimum wage is one floor in one place.** Every asked wage (a hand, a business worker, an HR raise request) and every renewal is at least the market wage + 10%, in `PeopleRules.AskedWage`, `PeopleSystem.RenewalWage`, `BusinessRules.PayUpfront` and the treasury's clerk line; a labourer's day wage is read at hire. Wages already paid stay until their renewal.
8. **Rent control changes one number**: the rise a renewal may take is `min(usual cap, 3%)` (`LeaseRules.Options`). A new family still pays the market rent and a rent never goes down.
9. **Free school meals need the school** (a pupil is a child of school age who cannot read, while the school is open). Households' food need falls by the meals' share of a child's food for each pupil, and the children's health gains an extra 0.05 a season.
10. **The night market (Could) is not built.** It needs the watchman's post from spec 023. Spec 022 §11 listed it first to cut; it comes back with the Township.
11. **The sheet.** Policies is a tab beside Taxes on one *Taxes and policies* sheet (tabs "Taxes" and "Policies · 1 of 3"); each row is the policy's name and cost, where it stands in one line, *Helps:* and *Costs:* in one line each, a **?** and *Enact* or *Repeal* (the reason when it cannot). A first-visit note with *Got it* shows once (`SeePolicyTour`). Enacting is a tap card (cost, when it is paid, when it works, the town's bill before and after with a ✓ or a warning); repealing is a hold card (what stops and what stays behind).
12. **Gate checks without bots.** `TaxGateTests` checks the hands-off Township (never below zero, every book reconciles, no policy runs unenacted), the rate dodger (a cut before a close saves nothing; one season at the cut rate costs exactly one season), the policy flipper (pays, never gets the effect) and the treasury-milker (nothing reaches your cash). Spec 023's cohort and clinic make the full 200-seed solvency check meaningful; it moves to that sprint.

## Consequences

- Save schema 19 grows once more (five policy states and the tour flag) and has not shipped; no fixture change was needed because no fixture holds an open treasury.
- Six new events (`PolicyEnacted`, `PolicyRepealed`, `PolicyStarted`, `PolicyPaused`, `GrantWasted`, `GrantOpenedShop`), three commands (`EnactPolicy`, `RepealPolicy`, `SeePolicyTour`), five reject reasons, one RNG stream.
- A town with three policies at once pays about 33,000 a season in lines on top of the clerk and the school; spec 023's income must carry that or the planner will say so (policies pause first: they are the last department).
