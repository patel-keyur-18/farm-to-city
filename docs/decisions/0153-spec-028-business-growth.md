# 0153. Spec 028: business growth, gaps closed

- Status: Proposed (the owner closed G1–G11 one per message, 2026-11-20 and 2026-11-21, each on my recommendation; accepted when the owner approves the whole spec)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related ADRs: 0057, 0068, 0069, 0074, 0082, 0107, 0141, 0144

## Context

ADR 0107 §13 and §15 fixed the scope of the business-growth stories (US-123, US-124, US-125, US-247, US-244, US-245, US-246, US-258, US-259, US-260). Spec 028 turns it into rules and numbers. ADR 0144 moved the spec to Sprint 24 and US-247 into it.

## Decisions

1. **Invoices (US-123).** From the Township, sales between businesses are invoiced on Cash, Half-season or Season terms; the village grocer and tea stall buy wholesale on terms and never default; an invoice ages current, late, overdue; an overdue one stops its seller; receivable and payable cancel between your own businesses in net worth. The farm's own sales stay cash.
2. **Managers (US-124).** Wage 4,000 + 10% of positive profit; three styles; skill 1 to 5 stars sets the mis-set days from 20% to 2%; a target and a close report; one manager a business; a managed business is passive income.
3. **Valuation, selling, branches (US-125).** 3 to 6 times a year's profit, adjusted for growth and risk, never under book; a sale takes 3 to 6 days at 90 to 105%, or 1 day at 85%; a branch of the same kind (at most 2).
4. **Investors, partners, buy-outs (US-247).** An investor's cash for a share of equity and profit (bought back after 4 seasons), a partner with a fixed profit share, a rival's offer at 90 to 110% of the value; none can be sold around.
5. **Township businesses.** A tuition centre (800 a pupil, skill +1 a season up to +10), a tempo (3,50,000, 1,000 kg, 4 hours, 300 a trip, let out to neighbours), a restaurant (two dishes, Could), your kirana (US-260).
6. **People.** Four benefits per employer and a night shift on the grid (US-258); a supervisor, accidents, conflicts and associations on spec 015's HR-event engine (US-259, Could).
7. **Shops.** Marketing (0.019 a 1,000, capped at 0.15) and khata (losses 8, 4 and 1% by income) (US-260).
8. **Scale and schema.** Sprint 25 carries 39 points and Sprint 26 the last 10; the cut line is in the spec §11; save schema 23 in Sprint 25 and 24 in Sprint 26, each hashed only when used, with fixtures; five previews (16 to 20) before the UI code; exploit bots for every new money flow.

## Consequences

- Placeholders the spec names (§10) are tuned in the balance pass without re-approval (ADR 0027); a rule change needs re-approval.
- The GDD changes in the same commit as the stories that change it (managers, investors, the Township's businesses).
