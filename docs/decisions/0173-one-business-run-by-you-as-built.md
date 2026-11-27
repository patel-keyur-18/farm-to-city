# 0173. US-266 one business run by you, as built

- Status: Accepted (Preview 22 approved by the owner, 2026-11-26; the story approved by the owner, 2026-11-26)
- Date: 2026-11-26
- Deciders: Keyur Patel
- Related ADRs: 0172, 0170, 0023

## Context

Spec 030 §3.1 and Preview 22: you can run only one business yourself, because your day has room for one. Until now `RunBusinessYourself` checked only that the business had no worker, and `FoundBusiness(runYourself: true)` checked nothing, so two businesses run by you both worked at full output on one set of hours.

## As built

1. **The rule.** `BusinessRules.RunByYou(sim)` lists your open or building businesses with `Worker = Founder`; `FoundBusiness` (and `FoundOnPitch`, which shares `BusinessRules.Found`) and `RunBusinessYourself` are refused with the new `RejectReason.AlreadyRunOne`. A closed business doesn't count. Nothing else changes: founder hours, output, wages and the plan's numbers are as before, and no save state is added (schema stays 24).
2. **The command for an older town.** `KeepRunningYourself(businessId)` keeps one of the businesses you run and sends every other you run to `Worker = None` (as when a worker is let go: it keeps its cash and stock and waits for a hire). It is refused unless you run two or more and the business is one of them. It emits `RunYourselfChosen` (kept id, stopped ids) and is written by the command codec (tag appended at the end).
3. **Where the player meets it.**
   - The plan sheet's *Who runs it* shows a hire and *Change* is closed with "You already run the tea stall: your day has room for one. Hire someone there first." (`runone.plan`).
   - A business nobody works: *Run it* is closed with "You already run the tea stall. Hire someone there first, then run this one." and the line under "Nobody works here" no longer says "or run it yourself".
   - The same rule answers a command sent anyway (`reject.AlreadyRunOne`).
   - To switch, hire someone for the business you run (its *Hire*), then run the other.
4. **The card at load** (`RunOne.Card`): a card with a row for each business you run (picture, name, profit this season, hours a day) and one *Keep running* button each, **no other button and no way round it**. `CardVm` got `Choices` and `Hint`; the presenter draws the rows. One tap answers (no second confirm: it can be undone by a hire). A toast says what happened ("You run the flour mill. The tea stall waits for a hire: use Hire on its card.").
5. **Time waits for the answer.** `GameRoot._choosing` is set at load (and after an import) when the town runs more than one; `Update` does not sync until it is answered, so the away hours since the save are counted with the answer applied, not with two businesses running on one set of hours. A town with the card up is saved as it is; the card comes again at the next launch.

## Differences from Preview 22

- The plan's *Who runs it* row has no ? (it was never a number; the preview drew one).
- The card's detail is on two lines ("Profit this season …" and "12 hours a day") so it never wraps mid-phrase.

## Tests

`RunOneTests` (Core: `OnlyOneRunByYou`, `ClosedDoesNotCount`, `KeepRunningYourself`, `KeepRoundTrips`), `RunOneViewTests` (View: `PlanBlocked`, `CardBlocked`, `RunTwoCard`); `MigrationTests.Schema13_Shops` founded a mill and a tea stall both run by you and now hires the tea stall's worker. The Editor tour by scripted touch (captures `docs/art/s28-266-*.png`): the plan, the business card with and without another run by you, the grey button doing nothing, the card at launch from a saved town running two, both answers, time held while the card is up.

## Consequences

- No bot and no existing test other than the one above ran two businesses itself, so no golden moved.
- Sprint 29's two towns show this card when a switched-to town needs it (`AskWhichToRun` is not tied to launch).
