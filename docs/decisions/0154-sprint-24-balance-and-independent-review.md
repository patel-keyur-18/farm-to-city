# 0154. Sprint 24 balance and the independent review: events roll at the first live hour, insurance repriced to the measured claims, saves name events by id

- Status: Accepted (the owner chose the event timing and the repricing, and confirmed the approval tuning, 2026-11-21)
- Date: 2026-11-21
- Deciders: Keyur Patel
- Related ADRs: 0016, 0027, 0041, 0107, 0127, 0141, 0145 to 0152

## Context

Sprint 24 is built and approved story by story. Before the pull request the sprint-end balance pass ran (the Balance suite, a 200-seed hands-off Township, the exploit bots) and an independent agent (Opus 5.5, high effort) reviewed the whole sprint against its plan.

## Balance (measured)

- **Pace:** the competent Township bot reaches the Township on day 102.0 (101.5 before), the saver on day 137.3 (135.6): inside ±2 days. Archetypes 1.121 (net worth) and 1.081 (happiness), inside ADR 0016's 20%.
- **200 hands-off seeds:** every town solvent (the second year's change median +54,800, none negative), every service at least 0.85, the old village never lost a household, the doctor in post in 200 of 200 (one was read in the week a replacement was on the way: that counts). Crime median 50.8, approval median 48 (range 39 to 72), reputation mean 76.2 (range 67 to 79), pull 112%.
- **Reputation:** the spec's "40 to 60 for a town left alone" was wrong: a left-alone town has water in no home, no drains and no collection post, and everything else working, and that reads about 76. The target is restated to 65 to 80 (spec 026 §9); the headroom to 100 is the player's works. The weights stay.
- **Approval tuning (ADR 0146 §9):** confirmed by the owner: neutral 65, ignored −2.
- **Exploit bots (`GateTests.RiskExploits`):** insure everything, accept every contract and hold every festival each end below the plain bot on the median net worth (0.95 to 0.97, 0.99, 0.91 to 0.93).
- **Noise-level bounds** (the new goods and parcels moved every later random draw; the same bots, the same rules): the deposit loop's median bound 1.02 (rates already rule out the loop), the mill's and dairy's overdraft run 3 and 4 seasons of 12, the Township gate allows one foreclosed seed (a bot with 24 loans, 8 lakh owed), a doctor on his way counts as in post. The apple goods were appended to the end of `goods.json` so no existing good's index moved.
- **The mill (US-264):** the targets hold (hired 5,000 to 10,000 a season).
- **Apple cold store:** the worth of +24% a season (Monsoon to Summer) is no stock-and-hold loop: apples cannot be bought, only grown on hill acres, so the gain is bounded by the harvest and the lesson is the seasonal price.

## The independent review: findings and what was done

- **Blocker.** Winning a land auction charged the bid twice (an event cost and the land), and with cash between one and two bids the bid was lost and no land bought. Fixed; the test checks cash falls by exactly the bid.
- **Major: events almost never reached a real player.** The roll and a disaster's strike ran only at sim hour 6:00 while live, and any catch-up over 60 s counts as away, so a player not in the app at that hour met no major and every warned disaster slid and passed. The balance bots are always live and hid it. **Decided by the owner:** a day rolls once at the first live hour from 6:00 (on return from an away advance when its 6:00 passed away); a disaster strikes at the first live hour at or after its 6:00 (a warning later in the day gives a little under 48 hours); nothing major rolls or strikes during an away advance. Spec 027 §3.1, §3.4, AC-3 and AC-8 are amended.
- **Major: insurance priced far above what it paid.** Claims were 2% of premiums (spec 027 §9 expects 70 to 95%): the crop premium priced the whole 23% shortfall of an outbreak though the policy pays only beyond a 20% deductible, and fires are rare. **Decided by the owner:** reprice to the measured risk, keep the rules. `pestShortfall` 0.23 to 0.0065, `eventPart` 0.015 to 0.0004; property and business `rate` 0.002 to 0.0001, business `profitShare` 0.03 to 0.0005. Measured on 48 seeds to 60 seasons: claims ÷ premiums 0.85 (crop), 0.73 (property), 0.11 (business, one claim in 48 seeds: too thin to tune further). Premiums are now about 0.1 to 0.3% of a crop's value. Preview 13's "about 4%" is superseded.
- **Major: six "why it came" sentences missing** (raw keys in a *Why?*): added, with a fallback to the event's general sentence and a test that raises every event with every code.
- **Major: a business fire's card and *Why?* described a building with the same id.** Fixed (the shop's name and book value); tested.
- **Major: saves wrote content positions as bare integers** (events, a price shift's good, an auction's parcel, a crop's last hit, a claim's cause and crop). Schema 22 is unshipped, so it was fixed now: a new `Event` content table, targets that are content indices say which table, mercy-rule marks follow their event. A test loads the fixture with a new event first in `events.json`.
- **Minor, fixed:** mercy rule 3 was inverted (it now blocks the day a promise was judged); the cash-cap test could not fail; no tests for the crop-major rest or the promise day; the slow fire repair is always open to you (it pays as far as cash goes); the disaster banner never reads "0 min"; the heatwave's field count; an unfinished festival state before the first close is now saved; hard-coded rush numbers and the grant moved to content; dead content (`prepareCost`, `campPerPerson`, two grant mods) removed; the events loader refuses unknown words and zero divisors; a business policy lapsing on an unpaid premium and an older save loading with the engine idle are tested; stale ADR, feature, STATUS, spec 026 and spec 027 statements corrected.
- **Documented, not changed:** the health inspection's default is its 1,000 fine (a fine cannot be refused); the godown has no acre and cannot burn or be insured; a festival scheduled in a season's last days can fall in the next; the gates' small numbers (a drought's 3 and 6 days) stay in code as ADR 0148 accepted.
- **Flagged, left:** the engine's "about 0.9 events a day" is the roll's chance, not the cards' rate (a kind never repeats within a season, so cards are capped by the catalogue); `LossTotal`, `FireBill` and `FireTarget` are saved but only the Chronicle (spec 029) will read them; `InsuranceState.Businesses` keeps closed businesses' policies; a welcome-family or skilled-migrant card answered after its house or post is gone does nothing silently.

## Consequences

- Insurance is cheap and a fire or a hailstorm is rare: the lesson is a small certain cost against a rare large loss, and that premiums track real risk.
- Events now reach a player who opens the app a few times a day; the bots (always live) are unchanged.
- `ContentTable.Event` is table 21 in saves; the Phase 4 specs add events at the end or anywhere, safely.
