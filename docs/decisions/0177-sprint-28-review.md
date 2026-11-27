# 0177. Sprint 28 independent review (Opus 5.5, high effort)

- Status: Accepted
- Date: 2026-11-26
- Deciders: Keyur Patel
- Related ADRs: 0172 to 0176

## Context

The owner asked for an independent review of the whole sprint (US-265 to US-269) against its plan, before any more work: nothing broken, every feature as documented. An agent (Opus 5.5, high effort, read only) read the diff, the plan, spec 030, ADRs 0172 to 0176 and the checklist, and ran both non-Balance test projects (View 454, Core 968). Verdict: no blocker; Automatic with no stall order is untouched; every supply path counted once; schema 25 and the codec correct. Ten findings.

## Findings and what was done

1. **Major, US-269: a stall order sowed even when the stall needed nothing, kept it all in store and never sold it** (a field gives hundreds of kg a day against a stall's tens; the store would fill and the hand wait on StoreFull). **Fixed on the owner's choice (2026-11-26): the hand waits.** When some vegetable fits but none has a preorder, `StallOrderRules.Pick` reports `needless`, `Orders` waits with the new `OrderWait.NothingNeeded` ("Waiting: the stall needs nothing more"), the crop list row says "The stall needs nothing more now: Geeta waits, and sows again when it does", and the *Why?* explains. The gone-stall line now says he keeps it in store and to change the order to sell it. Test `StallOrderWaitsWhenNothingNeeded`.
2. **Major: a good capped at None could still be sown** (ranked by market buys when all preorders were 0). Fixed by the same change: a capped good has no preorder and a 0 preorder is never sown.
3. **Minor: the supply *Why?* showed the wrong price** for milk (the co-op's `MilkPrice`, not the trader's floor), for the restaurant (the retail `MealsRules.MarketPrice`) and for a stall's non-produce buys (the import price without the events factor). `SupplyPrices` now gives what each path pays.
4. **Minor: the hub's and the Books' profit *Why?* rows did not add up** with marketing, bad debt, tax or losses on sales. A row "Marketing, bad debts, tax and losses on sales" is added when non-zero, so the rows sum to the headline (the Books' older *Why?* had the same gap).
5. **Minor: AC-17's coverage test did not build the new screens.** `WhyTests.AllSheets` now has the hub, each business's Supply tab, each amounts list and the crop list; every number on them already had a *Why?*. **Flagged, not fixed:** the one-business card's two numbers (profit this season, hours a day) have no *?* (a `CardChoice` has no Why field and the card is a modal shown once at load for an older town). The same profit has its *Why?* on the hub and the Books. Adding a chip needs the presenter's card layout and a preview; the owner decides.
6. **Minor: weak tests.** `Counts` now expects exactly 6,000 (today and yesterday); `StallGone`'s always-true assert is a real one; `KeepRunningYourself` advances two days and checks the stopped business serves no one; `Schema24_LoadsAutomatic` checks no stall orders; new `StallPavFromYourBakeryCap` covers the cap on the `FromYours` path. **Not added:** a cap test on the restaurant's `Cook` path and a multi-day gap in the lazy roll (the roll's gap loop is exercised by `SupplyExploits`' three mornings; the restaurant path goes through the same `Allowed`).
7. **Minor: `BoughtOver` undercounted after a quiet morning.** It now reads the window by day number (`DayIndex − SupplyDay`), so yesterday's counters still count; tested.
8. **Minor: stale docs** (feature file statuses, the ADRs' "awaits the owner's review", STATUS.md's branch and date, its Next-up row, `sprints.md`'s Sprint 28 status). All corrected.
9. **Minor: spec 030 not in line with what was built** (AC test names, the Needs-you reason dropped by ADR 0174, "yesterday kept", the cap's type, the hand's line). Corrected in the spec with "as built" notes.
10. **Nits.** Fixed: a fixed-kg cap at or over the day's amount is now listed and marked *In force*; two tab rows on one sheet each keep their scroll offset through a refresh; `StallOrderMeasured` reads `…TodayOf`. **Flagged:** the stall *Why?* uses the field's existing order's fertiliser while the crop list row uses the one picked on the sheet (the two can name different crops when they differ; the *Why?* has no way to carry the choice).

## Also flagged (the review's note)

- A stall order's gone-stall fallback keeps `Sell = Store`, as any *Let him choose* with Keep in store does (the line now says so).
- The plan said the review and pull request come at the end of Phase 3.5; CLAUDE.md says one of each per sprint. The owner asked for this review now; the pull request waits for the owner's word.
