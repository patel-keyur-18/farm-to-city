# 0098. The Concept Codex as built: each card's moment and numbers, the toast that waits and goes, the card as a popup

- Status: Accepted (the art and layout approved by the owner on 2026-11-10, with "the toast never stays on screen"; the rest recorded as built in US-133 for the owner's review)
- Date: 2026-11-10
- Deciders: Keyur Patel
- Amends: spec 018 §3.3 (the moments and numbers as built, the toast's timing), §4 (`codex.json`), §7 (the card's layout), AC-6 (its tests)
- Related: ADR 0077 (spec 018, G5), 0095 §7 (bursts, no back-fill, nothing in the first session)
- GDD sections: Learning layer (the Concept Codex)

## Context

US-133 built the Codex from spec 018 §3.3 and ADR 0095 §7. The spec's table names each card's moment in a few words. Building it needed each moment exact and testable, the numbers each card quotes, how the toast behaves next to the season report and the letter, and how a card opens. The owner approved the look (`docs/art/s17-codex-preview-*.png`) and asked that the toast not stay on screen.

## Decision

1. **25 cards in six groups**, in `content/codex.json`. Each card has its group, its trigger kind (one per card), a threshold where it takes one, and the formats of the numbers it quotes. Its words (title, idea, your numbers, real world) are in the string table.
2. **A card's moment** comes after the result, from the events the sim already emits (or a refusal for money):

| Card | Moment | Your numbers |
| --- | --- | --- |
| Diminishing returns | A harvest at the yield cap with chemical fertiliser | the uncapped factor, the cap |
| Soil fertility | A harvest of a soil-using crop leaving its acre under 50 | the acre's fertility, the crop's use a season |
| Opportunity cost | Day labour paid while an acre you own stands empty | the pay, the empty acres |
| Profit is not cash | A season with a profit and less cash at its end | the profit, the change in cash |
| Interest and compounding | A season with interest charged | the interest, the loans owed |
| Credit score | The score moves | before, after |
| Leverage | A loan bigger than the last season's farm profit | the loan, the profit |
| Liquidity | A payment refused for money while you own more than your cash | the cash, the assets |
| Inflation | The price index changes while 5% or more above the start | the rise |
| Working capital | A business on its overdraft | the overdraft used, its limit |
| Gross margin | A business's season close with sales | the sales, the goods' cost, the margin |
| Fixed and variable costs | A business's sales fall from the last season while its wage and rent don't | sales before and now, the wage and rent |
| Break-even | A business founded on its plan | the plan's sales, its costs |
| Forecast and actual | A business with 4 seasons and a plan, at a close | the planned profit, the average made |
| Unit economics | A batch made by a recipe business | a kilo's cost, its price |
| Depreciation | A season with buildings or machines worn | the wear, their book value |
| Appreciation | A close with land you bought worth more than you paid | the cost, the value |
| Yield | The first rent | the rent, as a share of the house's cost |
| Hiring under uncertainty | A reference check | its cost |
| The market wage | A low offer countered | the offer, the asking wage |
| Retention | Another employer's offer to your worker | the wage, the offer |
| Return on training | A course finished | the skill now |
| Employer reputation | Your reputation moves | the reputation |
| Public goods | The school opens | its cost, the pupils |
| Long-term investment | A child made literate by your school | the years, the school's cost |

3. **Numbers are kept as they were then.** `FindCodexCards` records each card with its numbers, so a card reads the same later and a replay finds the same. The sim refuses a card already found, an unknown card, or numbers that don't match its formats. `ReadCodexCard` takes the New tag off. Both are saved commands. The found cards (id, when, numbers, read) are in `GuidanceState`, schema 15.
4. **The toast (owner, 2026-11-10: it never stays):**
   - It shows for 6 seconds, then slides away; with reduced motion it simply goes.
   - It waits while a sheet, a card, a letter page or a *Why?* is open: the season report opens with a close, and the away card on return. What's found meanwhile joins the same toast.
   - One card names itself and opens it; two or more say "4 new in your Codex" and open the Codex. A card missed keeps its New tag.
   - A sheet, card, letter or *Why?* opened while it shows sends it away at once, so it never covers a sheet's buttons (found in the Sprint 17 checklist run, 2026-11-10).
5. **The card opens as a popup** over what's open, in the *Why?* popup's frame: the group and its icon over the title, the idea, "Your numbers", "In the real world", OK. Menu → Codex lists the found cards by group (groups with none found hidden), each with Read and an orange New tag until read. The Town tab shows "Codex · n of 25 ideas found" with its *Why?*.
6. **Icons:** the Codex is a green cloth-bound book lying open (not the Menu's red khata), and the Assets group a rolled land deed. The other groups reuse wheat, the coin, the flour mill, the townspeople and the school.

## Consequences

- Save schema 15 adds the found cards; a schema-14 town has none (nothing back-filled) and finds each the next time its moment comes. In the Editor, the owner's town found *Opportunity cost* on its away catch-up and four more at the next close.
- Tests: `CodexTests.Triggers` stages every card's moment and checks its numbers, that it's found once and that each card reads with no placeholder left. `CodexTests.Burst` covers one toast for several, the first session, the New tag, saves and replays. `CodexTests.Content` covers `codex.json`'s checks and every card's words.
- The Menu's letter row no longer says "becoming a Hamlet" (left from before ADR 0097).
- Sprint 17 checklist run: the *Public goods* card's numbers read "Children at it now, free: {n}" (was "{n} children go there", wrong for 0 or 1), and its group is *Community* (it repeated the card's title).
