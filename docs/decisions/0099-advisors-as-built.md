# 0099. The advisors as built: twelve notes, each condition exact, one line a sheet, a topic once a day

- Status: Accepted (the portraits approved by the owner on 2026-11-10; the rest recorded as built in US-134 for the owner's review)
- Date: 2026-11-10
- Deciders: Keyur Patel
- Amends: spec 018 §3.4 (each note's condition and line as built), §4 (the topic cap in `advisors.json`, not `ui.json`), §5 (what `GuidanceState` keeps), AC-7 (its tests)
- Related: ADR 0077 (spec 018, G6), 0095 §8 (the twelve notes, the caps), 0098 (the Codex: the same pattern)
- GDD sections: Learning layer (advisors)

## Context

US-134 built the advisors from spec 018 §3.4 and ADR 0095 §8. The ADR's table gives each note's condition in a few words and its line as a draft. Building it needed each condition exact, numbers that are true in the game, a way to remember what was said (the caps, the report's line when it opens again), and three conditions the sim couldn't answer: a field whose soil fell this season, a field that went dry, a worker tired most of the season. The owner approved the portraits (`docs/art/s17-advisors-preview-*.png`).

## Decision

1. **Three advisors and twelve notes** in `content/advisors.json`, in priority order (ADR 0095's table order). Each note has its advisor, whether it can be the season report's line, the alerts it attaches to, and a threshold where it takes one. A topic speaks again only after `topicHours` (24, a game day); this cap lives in `advisors.json` with the notes rather than in `ui.json`. Names stay the placeholders: **Sunita Verma** (the co-op bank's secretary), **Ramdeen Kaka** (a neighbour, a farmer all his life), **Masterji Hariprasad** (the retired headmaster).
2. **Each note's condition and line** (numbers from the player's town at that moment):

| # | Note | Where | Condition as built | Line |
| --- | --- | --- | --- | --- |
| 1 | Missed payment | `LoanPaymentMissed`, `FinalNotice` | the event | "Pay this first: a missed payment adds penalty interest and lowers your credit score." |
| 2 | Debt | Report | what you owe is over 0.5 of what you own (assets at the close) | "You owe {debt} against {assets} you own: over half. One bad season could cost you land." |
| 3 | Overdraft | Report, `WagesMissed` | a business's overdraft over 0.75 of its limit; or its wage missed | "The {business}'s overdraft is at {used} of its {limit}. Put cash in before the next wage is due." (After a missed wage: "…couldn't pay its wage, so its worker left…") |
| 4 | Profit, not cash | Report | the season left something and cash fell | "You made {profit} but your cash fell {fall}. The biggest reason: {biggest}. Profit isn't cash in hand." The reason is the season's largest use of cash outside costs (*why your cash moved*). |
| 5 | Pests | `PestWarning` | the event | "Spray now. Left alone, pests take 20% to 60% of the crop; sprayed, only 30% of that." (from `farming.json`'s outbreak and spray figures, not "a day late costs a fifth", which the game doesn't model) |
| 6 | Feed | `FeedLow` | the event (straw or feed) | "Fill the {feed} before it runs out. Hungry animals give less milk, and fall ill if it goes on." (the shed never "runs out" silently: hunger lowers health, ADR 0060) |
| 7 | Soil | Report | a field of yours whose fertility fell over the season and is under 40 | "{field} gave more than it got this season: its soil is down to {fertility}. Compost it, or rest it with lentils." |
| 8 | Dry field | Report | a rain-fed field (no well, pump, borewell or channel) with a crop below its moisture minimum for 24 hours or more this season, once the pump can be had (the Hamlet) | "{field} sat dry for {hours} under its crop. A canal channel or the pump would have kept it watered." |
| 9 | Raise | `HrEventRaised` (a raise asked) | the event | "Underpaid people leave. A raise usually costs less than finding and training someone new." |
| 10 | Tired | Report | a hand or worker whose fatigue is above `sickTiredAbove` (0.30, where falling sick doubles) at the close: six long days or more | "{name} is worn out from long days, and tired workers fall sick more often. Shorter days, or a second pair of hands." |
| 11 | Children | Report | children of school age who can't read, the school not started (once it can be built) | "{n} children aren't in school. The school plot is waiting." |
| 12 | Adult class | Report | the school open, no class running, grown-ups who can't read | "{n} of your grown-ups can't read. Readers earn 10% more at outside work, and a class costs {cost}." |

3. **What the sim adds for the farm advisor.** At each season close the land emits `FieldSeasonClosed` for each field of yours whose fertility fell or that sat dry: the season's net fertility change, the fertility now, and the hours a growing crop was below its moisture minimum. The dry hours are counted on the tile (`DrySeconds`, schema 15) and cleared at the close, as the season's fertility changes are.
4. **Remembered in the sim.** `HearAdvisors` records the notes said in a pass (the note, when, what it's about: the field, loan, business, feed or HR event, or the report's period; whether it's the report's line; its numbers), the latest one a topic, in `GuidanceState` (schema 15), so the caps and the report's line survive a restart and replay. It refuses an unknown note, one twice in a pass, or a note where its content says it can't speak. It changes nothing else: the advisors never act.
5. **Where they appear.** The season report: one panel under the title (portrait, name and role, the quote), the note picked at its close; the same line each time that report opens. The alert list: one indented line, "Name: “…”", with a small portrait, under the alert whose note ranks first among those an advisor spoke about. Nothing in the first session.

## Consequences

- Save schema 15 adds `GuidanceState`'s advice and a tile's dry seconds; the schema-15 fixture is re-pinned (schema 15 ships with this sprint's build). A tile's dry seconds are hashed once non-zero, so goldens 003 and 004 change hash (their crops sit dry); with that hash switched off they replay exactly as before, so only their `expectedHash` lines are re-pinned.
- In the Editor, the owner's town heard Ramdeen on pests at its away catch-up, and at the next close the season report showed Sunita: "You made 2,490 coins but your cash fell 5,767 coins. The biggest reason: what the supermarket still owes you." (`docs/art/s17-134-*.png`). The report's own rows read 62,091 to 56,323, a fall of 5,768: the line rounded the exact fall, the rows each end. The fall is now taken from the report's rounded start and end, so it adds up on the sheet.
- Tests: `AdvisorTests.Priority` (the report's line is the first note that holds, each report condition with its numbers, every line reads whole), `AdvisorTests.Caps` (one line a sheet, a topic once a game day, none in the first session, they never act, refusals, saves), `AdvisorTests.FieldSeason` (the sim's dry hours and close event), `AdvisorTests.Content`.
- The *Why?* quotes stay left out (ADR 0095 §8).
