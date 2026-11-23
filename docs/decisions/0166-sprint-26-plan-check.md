# 0166. Sprint 26 plan check: the Chronicle and business growth part 2 in seven stories, 31 points, save schema 24, fourteen gaps to close before their code

- Status: Accepted (the owner approved the plan and spec 029, 2026-11-23; gap 11 comes back for approval before US-159)
- Date: 2026-11-23
- Deciders: Keyur Patel
- Related ADRs: 0016, 0027, 0057, 0074, 0091 to 0096, 0098, 0107, 0141, 0148, 0153, 0155, 0162, 0163, 0164, 0165

## Context

Sprint 25 is merged (PR #36, `097c19fe`). Sprint 26 closes Phase 3's building: spec 029 (the Chronicle, the Almanac, citizen stories: US-157, US-158, US-159), spec 028's last two stories (US-259 supervisors, associations, conflicts and accidents; US-260 your kirana, marketing and khata), the Phase 3 exploit checks (US-248) and the playtest town and Release build (US-249). As for Sprints 24 and 25 (ADR 0144, 0155) every story was checked against its spec and the code at `main` before any code.

## What the code at `main` gives us (reuse, not rebuild)

| Need | Already there |
| --- | --- |
| The Chronicle's moments | `EraUnlocked`, `FestivalHeld`, `SchoolOpened`, `ClinicBuilt`, `RoadBuilt`, `OrchardBearing`, `BusinessOpened`, `BusinessSold`, `BusinessClosed`, `FireStruck`, `EpidemicEnded`, `LoanRepayment`, `PetitionEnded`, `CitizenLiterate` (with its `How`), `CropHarvested`, `StoryKind.Arrived` and `Born` |
| The toast | the Codex's toast (ADR 0098): 6 s, waits for sheets and letters |
| The year-end flow | *Your year* after Winter, Dadi's letters, the Codex toast (ADR 0101 §5): the Almanac goes between them |
| HR events | `HrEvent` and `HrRules.Raise/Answer/Gone`; `Worker`, `Hand`, motivation (`Hr.Mood`), employer reputation, benefits, night shift |
| Cards with a clock | `PendingEvent` with `ExpiresAt` and a named free default (spec 027) |
| Shops | `ShopKind`, `Attract`, stars, rivals (`grocer`, `village_tea`) and newcomers, pitches |
| Households | `Savings`, `Income`, the budgets |
| A playtest town | `tools/Tools.Balancer/Playtest.cs` (`playtest-save`), the Phase 2 checklist |
| Exploit bots | `GateTests`: `InvoiceLoops`, `SellBuyBack`, `InvestorDilution`, `StackBenefits`, `NightNoDemand`, `RiskExploits`, `TownshipExploits`, `LandStress`, `MoneyLoops_ZeroTime`, `ExactLedger`, `BoundedPrices`, `NeverStuck` |

## Gaps (each closed with the owner, one per message, with my recommendation, before its story's code)

Nothing here changes a rule the owner approved except gap 11 (a spec 029 rule, brought for re-approval). A decided gap is written into the spec's §10 and the story's ADR (as built).

**The plan**
1. **Spec 029 is still "In review".** Its seven gaps are closed (ADR 0165) but the owner has not approved the spec, and no game code starts before that. *Recommend:* the owner approves spec 029 with this plan; the GDD's two "first graduate" lines (§ Moments that matter, the Chronicle list) become "first adult reader" in the same commit (spec 029 §3.2 renamed it; reached here, as asked).
2. **Preview 19 is named twice.** Spec 029, F16.3 and ADR 0165 call the Chronicle's preview 19; spec 028 §7 and F11.4 call the supervisor and HR cards 19, and F12.3 calls the kirana's 20. *Recommend:* 19 is the Chronicle (Timeline, an Almanac card, a story's toast, the People notice), 20 the supervisor post and the accident, conflict and association cards, 21 the kirana, marketing and khata. The three files are corrected. Each preview is made at the start of its story's slot (not all at once), so a cut story wastes none.

**US-259 supervisors, associations, conflicts and accidents**
3. **"A place with 3 or more workers" is reachable only on the farm.** A business has one day worker (and a night worker, and a manager who works himself); the farm has up to two hut hands plus tenants hired from their cards. *Recommend:* keep the rules; the sheet says what each needs ("needs 3 hands"); supervisors and associations (4 or more) are the farm's, conflicts (2 or more) the farm's and a night-shift business's, accidents everywhere. Tests build the farm case. The alternative, lowering the thresholds for businesses, changes approved numbers.
4. **Cards with a 24 h default, on an engine with no deadline.** `HrEvent` waits for the player for ever (spec 015 §3.7); spec 028 §3.9 wants "default after 24 h". *Recommend:* the three new kinds (accident, conflict, association) get an `ExpiresAt` and are answered by their default in the hourly pass, live or away (the away card says what happened); the five old kinds keep no deadline; they do not count toward spec 027's two waiting cards; one open card a place for each kind; a "pay the wage" default the cash cannot cover falls back to the unpaid answer.
5. **The accident numbers are 90-day figures.** "0.02% a day", "out 7 days (3 with the clinic)", "severe 30 days" are for a 90-day season; the game's is 4 real days (the tempo and restaurant had the same, ADR 0162, 0163; `sickDays` is already 1). *Recommend:* rescale to the 4-day season: a roll of 0.45% a real day (still about 1.8% a season), out 8 hours (3 with the clinic), severe 32 hours, the wage for the hours out; the 2,000 and 8,000 bills and the 30,000 supervisor stay (one-off and per-season money).

**US-260 your kirana, marketing and khata**
6. **Khata needs a household, and a shop's demand is by group.** `ShopRules` demand is Town or Village in total; "a household that bought in 3 of the last 5 days" has no data. *Recommend:* a household of **yours** is a *regular* of the open shop of the kind with the highest attraction to its home (ties by the lowest id); the old village's families are never on khata (no ids); a regular's credit sale is its people's share of the group's demand; the state is only the households that owe (balance, limit, overdue, written off), no purchase history. The +0.10 score applies to your households with income under 20,000 and lifts their kilos 10%.
7. **The grocery kind and the grocer's entry.** Eight goods, "the rest 30%" has no split, and the grocer is one rival of the vegetable kind. *Recommend:* a new `ShopKind.Grocery`; the 30% split ghee 5, rusk 5, pav 10, paneer 5, curd 5; the grocer's staples entry is a second rival in `shops.json` (same place, kind grocery, reacts), leaving its vegetable entry as it is; khata's switch on the kirana, vegetable stall and tea stall as specified.
8. **The kirana's money is in game-day terms.** "0.25 kg a person a day", "about 80 kg a day", "about 18,000 a season" were written before the 4-day season. *Recommend:* the demand runs through the same formula as the vegetable stall; the kilos in content are tuned until the targets hold at the usual settings (hired 3,000 to 8,000, run by you 15,000 to 25,000), measured first, tuned without re-approval (ADR 0027). The spec's 18,000 is an estimate, not a rule.
9. **Bad debt has no account.** *Recommend:* a new expense account *Bad debt* on the shop's own books (schema 24), its own line in the shop's P&L and *Why?*; the 5% paid a season late is collected at the next close; a write-off at the close; the customer receivable is the existing 1300.

**US-157, US-158, US-159 the Chronicle, the Almanac, citizen stories**
10. **Events that lack what a line needs.** `EpidemicEnded` has no count of the sick, `LoanRepayment` no lender or "balance is now 0", a fire's repair is paid later than the strike. *Recommend:* add the fields to the events or read the state at the event; one `ChronicleSystem` listens to the day's events (away ones with their own times) and writes; nothing changes for the player.
11. **The hand's stall can strand a hand.** Newcomer stalls open only when your shop prices above the reference (ADR 0095), so "the next newcomer within 4 seasons is theirs" may never come, and the hand left for nothing. *Recommend (a rule change, for the owner's approval):* the story founds the stall itself at the leaving close, a newcomer of his trade's kind on a free pitch or the lane's verge, owned by him (a new `Owner` on the newcomer); if that kind already has a newcomer or no place is free he does not give notice (eligibility includes a free kind), so he is never stranded. `handWindow` goes; the rest of §3.6 stands.
12. **The costliest loss reads "the biggest single line" of a ledger list.** Ledgers are per business and the farm's, and spoilage is posted as a day's total. *Recommend:* the Almanac keeps a running worst loss for the year, updated at the moment it happens (a fire bill when paid, a loss on a sale or a closing, a day's spoiled goods, a seizure or default, a business's losing season as its report loss); no ledger scan; the spec's `lossLines` becomes a list of kinds.

**US-248, US-249**
13. **US-248 is nine checks and most exist.** *Recommend:* its first task is a table of the nine acceptance items against a named test each; what is missing is added (the treasury never reaching the founder's wallet, a landlord at the rent cap, a zero-tax town's services and happiness, a riskless forward contract, a found-and-sell bot within 20%, all at 200 seeds to game year 8, 15 to 20 minutes in the background, no content edits while it runs); `KhataWorst` and `MarketingSpiral` come with US-260.
14. **The playtest town stops at the third household.** `Playtest.cs` is the Phase 2 town. *Recommend:* a `village` mode that plays to the close after the Village unlock (the gate's "a town just past the Village"), shown to the owner before it goes to the phone, made after the review's fixes so its content is the final content; the Release build comes last, from the final commit.

**Carried, no decision needed**
- The Editor re-check of what Sprint 24's review changed (the event card, the banner, the Money page's premiums): the first Editor session.
- `WeatherPresenter.LateUpdate` throws `NullReferenceException` after big time jumps in the Editor: found and fixed with a null guard and a check by a big jump (a failure is not left; it was never on the phone).
- ADR 0154's flagged minors (the "about 0.9 events a day" wording; `LossTotal`, `FireBill`, `FireTarget` are read by the Chronicle and the Almanac now).
- The device-only rows of the Sprint 23 to 25 checklists stay with the owner.

## Decisions

1. **Order of work:** the Musts first. US-157 (the Chronicle; schema 24 begins) then US-158, US-159, US-260, US-259, US-248, then the balance pass, US-249 and the review. Gaps are closed one per message at the start of each story's slot (gaps 10 and 12 before US-157 and US-158, 11 before US-159, 6 to 9 before US-260, 3 to 5 before US-259, 13 before US-248, 14 before US-249).
2. **Cut line, in order:** US-259; spec 029 §11's items (the hand's stall and its notice, then the long-life arc, then the Almanac's crop returns); US-260 (marketing, then khata, then the kirana). **Never cut:** the Chronicle's record and its sheet, the Almanac card, US-248, US-249, every number's *Why?*, the schema 24 fixture, the exploit checks (`KhataWorst` and `MarketingSpiral` go with US-260 if it is cut).
3. **Points:** 31 = US-157 5, US-158 3, US-159 5, US-260 5, US-259 5, US-248 5, US-249 3. The carried items above are chores inside the first Editor session and the balance step, with no points.
4. **Save schema 24**: `ChronicleState`, `AlmanacState`, `StoriesState`, the shop's marketing and khata, the places' supervisors and HR events' deadlines, the newcomer's owner, the bad-debt account; each hashed only when used, so a town that reaches none hashes as before (the goldens do not move). Fixture `schema24-people.ftc` and `MigrationTests.Schema24_People` and `Schema24_Chronicle`; every older fixture loads and carries on through two closes.
5. **Sprint-end work** (as Sprint 25): the balance pass (the 20% rule; the pace; the kirana's targets; the hand's stall rate; the Chronicle's size; `KhataWorst`, `MarketingSpiral`; the 200-seed Township), render statistics for each visible story, Editor saves backed up before and restored and diffed after, the independent Opus 5.5 high-effort review, the checklist, the Development build, ADRs as built, `STATUS.md`, the pull request with labels, milestone and project.

## Consequences

- Fourteen small decisions come to the owner one per message, each before its story, so the owner is never waiting on one. Gap 11 changes a spec 029 rule and needs the owner's re-approval of §3.6.
- Nothing in this sprint starts before the Township except the Chronicle's firsts (read-only), so the earlier eras and the pace are unchanged by construction; the sprint-end pass proves it.
- The Chronicle, the Almanac and the stories are read-only for the player; the only new money paths are the kirana, marketing, khata and the accident bill, each with its own exploit bot or test.
