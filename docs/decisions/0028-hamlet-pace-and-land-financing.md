# 0028. Hamlet at the end of the first week, land priced by earning power, and land-purchase loans

- Status: Accepted. The Hamlet no longer depends on buying land: ADR 0030 (inherited 6-acre farm, labour-based gate). The pacing targets and land-purchase loans stand. Point 2's "net farm income" is an owner-operated acre's (ADR 0033)
- Date: 2026-10-17
- Deciders: Keyur Patel
- Supersedes: the Hamlet, Village and Township pacing targets in ADR 0002 (and the GDD's balancing targets)
- Related: ADR 0008 (coin scale, starting package), ADR 0009 (credit, protected plot), ADR 0017 (Phase 1 ends at the Hamlet unlock after the guided first week), spec 002 (open question 1), spec 005 (open question 2)
- GDD sections: Balancing and testing (targets), Land, farming and livestock (land), Assets and wealth (credit)

## Context

Writing spec 002 exposed three rules that can't all hold:

1. **The pacing table** (ADR 0002, GDD) puts the Hamlet on day 1–2 and the Village at about 1 week.
2. **The Hamlet gate** needs 6 acres farmed, a house for a hired hand and one profitable season. The first season close is on day 4, so the gate can't be met before then. ADR 0017 already defines Phase 1 as "Homestead to the Hamlet unlock (the guided first week)".
3. **Land prices** are rupee-scale ("lakhs", ADR 0008), and the starting savings deliberately can't buy land. The 4 extra acres must therefore be borrowed for. But the bank lends only against collateral, and the inherited plot can never be collateral (ADR 0009). So no lender could fund the Hamlet's acres in the first days.

The Village target has the same problem: its gate needs a business that has been profitable for 2 seasons (8 days), and businesses arrive with the Hamlet.

## Options

1. **Keep the targets and make land cheap.** This breaks rupee-scale realism and makes land trivial.
2. **Add leasing to Phase 1.** It's realistic (theka) and cheap upfront, but it adds a feature to Phase 1.
3. **Move the targets to what the gates allow, price land by what it earns, and let a bank loan be secured by the land it buys.**

## Decision

Option 3.

1. **Pacing targets** (Standard difficulty, a competent player doing several short sessions a day):

   | Milestone | Target real time since start |
   | --- | --- |
   | Hamlet | End of the first week (about 2 seasons) |
   | Village | 2–3 weeks |
   | Township | 4–6 weeks |
   | Town | 2–3 months (unchanged) |
   | City | 4–6 months, about 10–12 game years (unchanged) |

   The Village and Township targets are provisional. The balancer's "reachable eras" health check (F20.1) confirms or adjusts them.
2. **Land is priced by earning power.** An average acre costs about **8–15 seasons of its net farm income**, so it pays back in 2–4 game years. The placeholder is 1,20,000 coins an acre, which keeps the inherited plot "worth lakhs" (ADR 0008). The economy model (F00.3) sets the tuned value.
3. **Land-purchase loans.** `BuyParcel` can be part-financed by a cooperative-bank loan. The parcel being bought counts as collateral for that loan (loan-to-value 0.60 by default). The inherited plot still never counts as collateral.
4. **No leasing in Phase 1.** Leasing is reconsidered for Phase 2.

## Consequences

- The GDD's balancing targets table and ADR 0002's pacing table are replaced by the table above. ADR 0002's status line points here.
- Spec 002 (§3.5) and spec 005 (§3.6) are approved with these rules.
- The economy model's anchor numbers (US-014) must respect the 8–15-season rule for land.
- The Hamlet unlock (F09.3) and the playtest gate (F10.1) measure against "end of the first week".
