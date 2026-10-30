# 0083. Owner requests in Sprint 15: your townspeople at work, your season earned and spent, the weather on the map

- Status: Accepted
- Date: 2026-10-29
- Deciders: Keyur Patel
- Amends: spec 014 §3.6 (housing in kind from the first renewal, once a house; your labourers on the map and the People sheet), spec 015 §3.1 (offering work from a citizen's card), spec 019 §3.2, §4, §7, §8 (Money and loans opens on *This season*; a sales record; awaiting re-approval), spec 008 §3.4 (the working day on the map; the weather on the map), §3.13 (weather sound), §3.14 (the developer panel's *Weather look*); ADR 0044 §2 (sound: weather ambience allowed); ADR 0076 §2 (later save schemas shift by one)
- Related: ADR 0063 (housing as part of a hand's pay), ADR 0069 (spec 015), ADR 0078 (spec 019), ADR 0042 and 0043 (weather spells), ADR 0026 and 0035 (the half-frame budget)

## Context

On 2026-10-28 the owner raised four requests (US-219 to US-222). On 2026-10-29 the owner put them into Sprint 15, on the same branch and pull request, before the device check, so that one build covers everything. Claude listed 18 gaps, contradictions and pushbacks, and the owner closed each one in turn. Two came from reading the code:

- **Hiring a tenant cancels the season's rent at once.** `CitizenRules.TownHandHired` resets the household's budget. With a hand in the household, its rent becomes 0 for the whole season that's running, while his wage for the rest of the season is still paid upfront in full cash.
- **Two hands from one house both have the rent deducted.** `MigrationRules.HousingInKind` takes 6,000 off each hand whose household lives in a family house, so one house's rent is counted twice.

Two more came from the views:

- **Townspeople hired as labourers are invisible.** The map and the People sheet's *Labourers at work* draw and count labourers from the neighbours' roster. They leave out townspeople (`Citizen.LabourUntil`), whom spec 014 §3.6 hires first.
- **"Each crop sold" can't be read from the books.** Every crop sale posts to one account. The mandi trip is one entry, and a hand's sale and an order don't name the good. The journal also keeps only 4 seasons.

Claude also withdrew one of its own pushbacks. Frost "at 3 pm" isn't unrealistic: spec 001 §3 makes one weather spell (1 sim hour) a world day, with no hour-of-day rule.

## Decision

### US-219 Your townspeople at work on your fields (3 points)

1. **The hire season (G1).** A tenant hired as a hand still owes the household's rent for the season that's running, and his wage for that season is paid in full cash. Housing counts as part of his pay from his first renewal (spec 014 §3.6: never mid-season). From then on there is no rent, and his cash wage is the wage less 6,000 × the price index. The board's offer row and the citizen card say so before you offer: "30,000 a season. From next season: 24,000 cash + the house (6,000)".
2. **Once a house (G2).** A house's rent is part of the pay of the first hand hired from that household (by hand id). Any other hand from the house is paid his full wage in cash. If that first hand leaves, the next one's deduction starts at his next renewal. Each hand's card says whose pay the house is counted in.
3. **Offering work from the card (G3).** An adult aged 18 to 59 who works outside has **Offer work as a hand** on their citizen card. It uses the board's offer: the asked wage, 10% less or 10% more, answered by spec 015 §3.3 (a townsperson always accepts at the asked wage or above, and accepts 0.6 of the time below it). It shows the housing line from §1. There is no post fee, interview or reference check, because you know them. The new command is `OfferTownsperson(citizenId, level)`. When it can't be offered, the button says why (works in town, too young, retired). Business jobs stay on the board.
4. **Scope (G4).** Long days at 1.5× with fatigue are spec 015's and already built (US-113). Unpaid work for housing is never possible. Work in return for housing alone would be bonded labour, abolished by the Bonded Labour System (Abolition) Act, 1976. A deduction for housing the employer provides is lawful (Payment of Wages Act 1936 s.7(2)(d); Code on Wages 2019 s.18), and shown.

### US-220 Your townspeople seen at work as labourers (2 points; presentation only)

5. **The working day (G5).** On the map everyone keeps the village's day, 7:00 to 19:00, which townspeople at home already use (US-212). The founder, hands and labourers go round the fields in those hours and go home outside them: the founder to the house door, a hand to his hut or family house, a townsperson to their family house. Neighbours' labourers walk in from the west edge in the morning and out in the evening. The sim's hours, Care and pay don't change.
6. **Who is at work (G6).** A townsperson hired as a labourer is drawn as that citizen (their look), walking from their family house to the fields, and isn't also drawn at home. The People sheet counts them. It shows one pictured row for each townsperson at work (name, house, till when, opening their card) and one row for neighbours together. People on the map stay untappable.

### US-221 Your season, earned and spent (7 points; amends spec 019, awaiting re-approval)

7. **Where it sits (G7).** Money and loans has two tabs and opens on **This season**: earned, spent, profit, why your cash moved differently, and next season already due. **Your wealth** holds net worth, land, businesses, what you're owed, your loans and borrowing. US-135 (Sprint 18) redesigns *Your wealth* as spec 019's hub. *Your books* (US-137) stays in Sprint 18.
8. **Profit as the books count it (G8).** *Earned* and *spent* follow the ledger: a cost counts when it's used up, not when the cash is paid. *Profit this season* equals the season report's last line (*what's left*: it includes the family's living and interest). The farm's own profit appears in its *Why?*. **Why your cash moved differently** lists:
   - paid ahead (wages and living for the rest of the season, inputs in store)
   - wear on buildings and machines
   - paid back on loans
   - borrowed
   - bought and still yours
   - sold

   Cash at the season's start + profit + these lines = cash now.
9. **A sales record (G9).** State keeps this season's and last season's sales, by good and by where sold (trader, mandi, market, orders, your businesses), written at each place a sale is posted. It needs save schema 12, a migration (an empty record), the `saves/schema12` fixture and `MigrationTests.Schema11_Sales`. Golden replays 003 and 004 are re-pinned once, after showing that their old hashes hold with the record left out of the hash. Orders get their own line, and each crop's *Why?* says where it sold. **The later schemas shift by one** (ADR 0076 §2): spec 013's and 017's become 13 (Sprint 16), spec 018's 14 (Sprint 17), spec 019's 15 (Sprint 18).
10. **Next season, already due (G10).** Only what the close will take: each hand's renewal by name (less the house, §2), loan payments as interest and principal (the principal row says "paying back what you borrowed, not a cost"), the family's living, and rent you pay. All at today's prices; the *Why?* says the close's index may add a little. A business's worker is paid by the business, and seed and feed are bought as you go, so neither is counted. Below it: "Cash now covers it", or in amber "Short by 8,400: Ravi's renewal would lapse".
11. **This season and last (G11).** A switch: **This season** | **Last season**. Last season shows its finished breakdown without *next season due* and ends with *See the season report*. Before the first close, only *This season* shows. The same season last year stays with US-137 (spec 019 G2).
12. **Things you bought (G12).** Rows in *Why your cash moved differently*, one per kind: buildings, animals, trees, land, machines, put into your businesses, and *Sold* for the reverse. They come from the asset accounts' cash postings, and each row's *Why?* says "You still own it: it's in *Your wealth*, not a cost". Loan principal also sits here, so *Spent* lists costs only. A row shows only when it isn't zero.

### US-222 The weather on the map (6 points)

13. **The preview (G13).** Before any code, the look is shown to the owner on an interactive preview page over real captures of the farm by day and by night. It has the 7 weathers, day and night, and reduced motion, at iPhone 13 size. Stills are kept in `docs/art/s15-222-weather-*.png`. The approved numbers (streak counts, opacities, timings) go into `content/ui.json`.
14. **Sound (G14; amends ADR 0044 §2).** A soft rain loop plays during rain and heavy rain, and distant thunder now and then in a storm, behind the Sound switch, fading in and out with the weather. The sounds are CC0 only, logged in `ASSETS_LICENSES.md`, and the owner judges the mix on the iPhone. Taps and sheets stay silent.
15. **No flashing; reduced motion (G15).** For everyone, lightning is a soft glow that rises over at least 0.3 s and fades over about 1 s, at most once in 15 to 25 s, and never more than 3 times in a second (WCAG 2.3.1). Thunder follows it 1 to 3 s later. With reduced motion on, rain is still, lighter streaks, cloud shadows stand still, and there are no gusts, no shimmer and no glow. Thunder still plays if Sound is on. A change of weather still fades over a few seconds.
16. **Frost (G16).** Rime shows for the whole frost spell, because a spell is a world day (spec 001 §3). The night tint darkens it like everything else. US-222.T1's "melting by mid-morning" is withdrawn. Each weather holds for its whole spell and eases in and out at changes.
17. **Wet ground (G17).** Darker ground is part of the rain look and eases back when the spell changes. Nothing lingers. Showing each field's real soil moisture on the map would be its own story, not planned.
18. **Frame time (G18).** The developer panel gets a look-only **Weather look ▸** picker (Development builds only). It sets what the map draws, not the sim's weather: no command, no state, and it resets on leaving play. Frame time is measured in every weather in the Editor, then on the iPhone 13 in storm at night and in heavy rain with the whole map showing. The pass line is the usual one: main-thread p95 at or under 8.3 ms, about 60 fps, under 1% dropped. The number of rain streaks is capped in `ui.json`.

## Approval and clarity (owner, 2026-10-30)

The owner approved the amended specs 008, 014, 015 and 019, asking that everything makes sense to and is achievable by an average player. Two lines were added for that:
- Outside 7:00–19:00 the People sheet's hand and labourer rows say "Home for the night · back at 7:00" (spec 008 §3.4), so an empty farm at night never looks like work stopped.
- *Next season, already due*'s amber line names its levers ("Sell from the store, or borrow on *Your wealth*") and opens the Store (spec 019 §3.9).

## Implementation notes (US-221, 2026-10-30)

- The tabs read *Your season* | *Your wealth* and the switch *This season* | *Last season*, so two buttons never both say "This season" (spec 019 §10, US-221 build notes).
- Golden replay **005** was re-pinned with 003 and 004: schema 12 also keeps each season's first ledger entry, which is in every game's hash. All three were first shown to hold with the new state left out of the hash.

## Implementation notes (US-222, 2026-10-30)

- The owner approved the preview's look as it was (2026-10-30). The game renders in linear colour, so each pale or dark layer's alpha is converted to look as the preview did; the colour, saturation and brightness need URP post-processing on the map camera (the cost to watch on the iPhone 13).
- Heat shimmer is a swaying haze, not a screen distortion (a distortion would copy the screen every frame in every weather). The weather sounds are our own, synthesized and dedicated CC0.

## Consequences

- Sprint 15 grows from 37 to **55 points**: US-219 3, US-220 2, US-221 7, US-222 6 (owner, 2026-10-29).
- Save schema 12 arrives in Sprint 15. Specs 013, 017, 018 and 019 and their feature files move to schemas 13, 13, 14 and 15. ADRs 0076–0078 keep their text as history.
- Spec 019 is amended and goes back to the owner for approval before US-221's code. Its hub and statements are still built once, in Sprint 18, inside the tabs set up here.
- US-222's code waits for the owner's approval of the preview.
- The map now has one clock for people (7:00–19:00), so an evening visit shows an empty farm and people at home. That's intended.
