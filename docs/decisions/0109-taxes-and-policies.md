# 0109. Taxes and policies: the owner pays a house tax capped at 15% of rent, a business tax on profit, rates from the next season, five policies with named winners and losers, and hands-off play that always works

- Status: Accepted (owner, 2026-11-13: ten gaps decided one at a time while spec 022 was written; spec 022 approved the same day)
- Date: 2026-11-13
- Deciders: Keyur Patel
- Amends: the GDD's *Governance and public finance* (revenue and policies, made exact); spec 021 §3.4 (a seventh department, *Policies*)
- Related ADRs: 0107 §6, §8, §13; 0108; 0101 §12; 0103
- GDD sections: Governance and public finance

## Context

ADR 0107 §13 set the Phase 3 taxes (property tax on every house, business tax on profit, fees; no sales tax) and five policies (at most 3 active, never paying the founder). Spec 022 needed the bases, rates, bounds, incidence, timing, each policy's numbers and the gate's checks. Ten gaps were decided with the owner.

## Decisions

1. **Property tax (G1).** The owner pays (a gram panchayat's house tax): you for every house you own, the old village for its own; hands' huts exempt. On each kind's assessed value (land value from spec 024), 1.6% a year, bounds 0–2.4%. **Owner's rule: a house's tax never exceeds 15% of its kind's base rent, nor of a let house's actual rent**, so a landlord always keeps a profit (a kutcha house: 2,500 rent − 240 tax − 500 wear = 1,760 a season; 1,640 at the cap). A content guard and a test enforce it.
2. **Business tax (G2).** 10% of each business's positive season profit (0–20%) from its own books; the village's grocer, tea stall and newcomers on a reference profit, so the tax doesn't fall only on you; unpaid tax is carried as owed, so emptying a business before the close never dodges it.
3. **Who feels a rate (G3).** The old village through its budget (leisure and savings before food); your tenants only through services (you pay their house tax); builders through spec 024. No approval or protests in Phase 3.
4. **Setting a rate (G4).** It applies from the next season (a cut before a close saves nothing); pending with undo; no confirm card.
5. **Policies (G5–G7).** Free school meals (700 a pupil, lag 1); farm input subsidy for neighbours' acres (250 an acre, the valley's supply +5%, your local price dips, −3% for 2 seasons on repeal); small business grant (10,000 a season to one of your townspeople, 40% wasted, a competitor to your shops); minimum wage (+10% floor at each renewal, the treasury's clerk included); rent control (renewals capped at 3%, new lettings at market, a rent never falls); the night market (Could, needs the watchman).
6. **Slots (G8).** At most 3 active; costs in departments (Education, the office, a seventh *Policies* paid last); enact on a tap-to-spend card, repeal hold-to-end; unpaid pauses; a repealed policy waits a season before re-enacting.
7. **The screens (G9).** *Taxes and policies* in two tabs with a preview row under each slider and helps and costs on each policy; your share on your own screens; previewed as an exact artifact before UI code.
8. **The gate (G10).** Zero-tax, max-tax, rate-dodger, policy-flipper, milk-the-treasury and business-emptier bots; the default town's income about 67,000 against spending about 62,000.
9. **Clear for an average player (owner, 2026-11-13).** A town left alone stays solvent with every service at full on the defaults (`TaxGateTests.HandsOff`); a first-visit tour on each sheet; one plain line under every control and the numbers under **?**; nothing fails hard.

## Consequences

- Spec 022 carries the rules; spec 021 gains the seventh department. The GDD is updated in this commit; CLAUDE.md's table gains this ADR.
- Hooks built with US-140 and US-141 in Sprint 21: spec 014 (meals), specs 006 and 015 (the minimum wage at renewal), spec 017 (newcomers, reference profits), US-240 (rent control's cap).
- Numbers are starting values in `content/treasury.json`, measured with the bots in Sprint 21; a miss comes to the owner with its numbers.
