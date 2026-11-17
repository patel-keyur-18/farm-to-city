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
2. **Paid in full or paused.** A policy line is paid whole or not at all: a close that cannot pay it in full *pauses* it (no effect, nothing charged, the slot kept; the row and the away card say so, and the planner's notes do not yet, which waits for spec 023's planner); it tries again at the next close. A repealed policy is never paused into silence: it ends.
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
- The three dearest policies together (meals about 28,000, subsidy about 15,400, grant about 10,000) cost about 53,400 a season on top of the clerk and the school. At a default Township income roughly equals spending, so **most policies need higher taxes or a gift to be paid**, and otherwise pause (they are the last department). The Policies tab and the tour say so plainly; spec 023's balance decides whether the default Township leaves room for one.

## Amendments after the independent review (2026-11-17)

13. **Keep it.** A policy that is ending (repealed, still running to the close) shows *Keep it* and keeps its paid seasons and lag; taking back a policy that was never paid is free and at once, with no card.
14. **The subsidy lowers field crops only** (`SalesKind.Crop`), not atta, milk or sweets.
15. **The minimum wage** also binds the low offer (10% less than the ask), cuts a newcomer shop's chance × 0.8 (`minWage.newcomerChance`), and its card names the clerk's raise (the town's own clerk is paid the minimum: about 3,000 more a season than the inspector's 2,000).
16. **`PolicyStarted`** fires when the lag ends. **An unused grant** (every kind has its newcomer already) says so in the away card instead of paying for nothing silently.
17. **The seen-lines flags are saved by line id**, so spec 023's new lines never shift them.
18. **Education gates school years** at the minimum condition (0.5): a school the treasury lets decay teaches nothing that year. Health's and tenants' happiness hooks, and a *Why?* naming the minimum wage on a wage ask, come with spec 023.
19. **A closed business pays its owed tax first**; newcomer shops pay tax on their reference profit.
20. **Known gap:** the founder's owed tax is not subtracted in net worth (it needs a field in the report's net-worth snapshot and a schema change); the amount is small and rare. It is listed for spec 023.
