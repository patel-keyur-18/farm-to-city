# Sprint 26 build: the Chronicle, the Almanac, citizen stories, your kirana, supervisors and accidents, and the playtest town

- **Build:** a Development build from the head of branch `sprint/26-chronicle-business-growth-2` (made at the end of the sprint; see the pull request). The playtest town (US-249) is a separate Release build made from the final commit.
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s26-*.png`. **Only a device can check:** the rows marked **Device only** (tap size, smoothness, the toast on a small screen, notifications). This list **carries the device-only rows of Sprint 25's list** that were not yet checked: see `s25-business-growth.md`, rows 28 and 29.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 23 to 24): nothing in your town moves. A Hamlet or Village town is unchanged; the Chronicle begins at the load with the eras your town has reached. **A town saved by this build cannot go back to an older build.**

## What this build changes (story by story)

- **The Chronicle (US-157).** Menu → **Chronicle**: a Timeline of what the town has lived through (firsts, record harvests, fires survived, a business sold), each line in a warm voice, with a **?** that says where it came from. 400 entries are kept; the firsts are pinned.
- **The Almanac (US-158).** After each Winter's *Your year*, a card: the year's best return (a crop's counts its seed, fertiliser and its share of hired labour), its costliest loss, and a person to remember. Past Almanacs sit on the Chronicle's second tab.
- **Citizen stories (US-159).** Four small arcs over real events: a hand who starts a stall of their own (**only once the Township is reached, and never against your shop's first profitable season**), a family's first reader, a migrant family that stays, an elder's long life. A short toast for each step, at most one a season; a Story line in the Chronicle; the People sheet says who will leave at the close.
- **Your kirana, marketing and khata (US-260).** A grocery shop from the Township on a pitch or an acre, competing with the village grocer's staples. **Marketing** is paid at once and again at each close; **khata** lets your regular households take goods on credit up to a limit, with the losses by income shown. Its **Books** carry *Bad debt* and *Marketing*.
- **Supervisors, accidents, quarrels and associations (US-259).** A sixth post on the hiring board for a place with 3 or more hands; **15,000 a season** (tuned at the balance pass: a hand's output measured 48,000 a season), +1.2% output a star on up to 5 hands, half the accidents, quarrels settled for you. Three cards with a 24 hour deadline and a free default (accident: pay the hours; quarrel: let it be; association: refuse). A Workplace section on the farm's People sheet and on each business.
- **The exploit checks (US-248).** Nothing to see on screen: five new tests found no money loop (zero-time loops over 29 Township commands, a found-and-sell bot, a landlord at the rent cap, late invoices, the Township's health at 200 seeds). They found a rule problem, now fixed: a citizen's stall could stand in the way of the gate's three businesses.
- **The game's clock matches your phone's (ADR 0170).** A town whose game time was behind the phone (yours was 65 minutes behind) is moved forward when it loads, like time away, nothing lost; one that was ahead waits until the phone catches up. A toast says which.
- **Without any of it** a Hamlet or Village town is as before.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, nothing is lost, the console shows no errors | | Editor ✓ |
| 2 | Menu → **Chronicle** | A Timeline of lines in order, newest first; the first line says the Chronicle begins | | Editor ✓ (`s26-us157-chronicle-timeline.png`) |
| 3 | Tap the **?** on a Chronicle line | A *Why?* in words: what happened and where it was read from | | Editor ✓ (`s26-us157-chronicle-why.png`) |
| 4 | Chronicle → **Almanacs** | One card a year, newest first | | Editor ✓ (`s26-us157-chronicle-almanacs.png`) |
| 5 | Play through a Winter close | After *Your year*, the Almanac card: best return, costliest loss, a person | | Editor ✓ (`s26-us158-almanac-card.png`, `s26-us158-almanac-card-scrolled.png`) |
| 6 | The **?** on the best return | Says it counts the crop's seed, fertiliser and its share of hired labour | | Editor ✓ (`s26-us158-almanac-why.png`) |
| 7 | A Township with a hand who has worked 3 seasons and saved 10,000 | At a close they give notice (the People sheet says they will leave); at the next close they go and a stall of their own opens | | Editor ✓ (`s26-us159-people-notice.png`) and `StoryTests.Hand_Stall` |
| 8 | A **Story** toast | One short line, 6 seconds, waits for sheets and letters; never two in a season | | Editor ✓ (`s26-us159-toast.png`) |
| 9 | A Village town (before the Township) with a hand who qualifies | **Nobody gives notice.** Stalls of their own start at the Township | | `StoryTests.Hand_Stall_Gates` |
| 10 | Your new vegetable stall with no profitable season yet, and a qualifying hand | The hand waits; he gives notice once your stall has had a profitable season | | `StoryTests.Hand_Stall_Gates` |
| 11 | Menu → Businesses → **Start a kirana** | Research with the village grocer's staples as rival; a plan; hired and run-by-you figures | | Editor ✓ (`s26-us260-kirana-research.png`) |
| 12 | The kirana's **Today** and **Customers** | The day's sales; which households come and why others do not | | Editor ✓ (`s26-us260-today.png`, `s26-us260-customers.png`) |
| 13 | **Marketing**: raise the budget | A tap card with the cost; paid at once, the difference when raised; the **?** says it adds about half a star at most | | Editor ✓ (`s26-us260-marketing.png`, `s26-us260-marketing-card.png`, `s26-us260-why.png`) |
| 14 | **Khata**: switch on, choose a limit | Regular households may take goods on credit up to it; the losses by income are shown | | Editor ✓ (`s26-us260-khata-limit.png`) |
| 15 | The kirana's **Books** | *Bad debt* and *Marketing* lines; what is owed is a receivable | | Editor ✓ (`s26-us260-books.png`) |
| 16 | The kirana on the map | Your shop on its pitch, a megaphone sign | | Editor ✓ (`s26-us260-map.png`) |
| 17 | The farm's **People** with 3 or more hands | A **Workplace** section: supervisor, accidents, quarrels and association, each with a **?** and what it needs | | Editor ✓ (`s26-us259-workplace-section.png`) |
| 18 | Hiring board → **Supervisor** → **Send word** | Migrants apply (no townsperson); the post says what it costs a season (about 15,000 at base prices) and that he pays for himself only at five hands | | Editor ✓ (`s26-us259-supervisor-board.png`, `s26-us259-supervisor-offer.png`, `s26-us259-supervisor-hired.png`) |
| 19 | With fewer than 3 hands | The Supervisor post is greyed: *Needs 3 hands: you have N* | | `WorkplaceViewTests` |
| 20 | An **accident** card | Who, how many hours out, the bill; **Pay the hours** or **Don't pay**; a 24 hour deadline; the **?** says the chance and its multipliers | | Editor ✓ (`s26-us259-accident-card.png`, `s26-us259-accident-why.png`) |
| 21 | A **quarrel** card | **Talk to both**, **Move one apart**, **Let it be**; a card says how it went | | Editor ✓ (`s26-us259-quarrel-card.png`, `s26-us259-outcome-card.png`) |
| 22 | An **association** card | **Accept**, **Negotiate**, **Refuse**; refusing cuts output for a season | | Editor ✓ (`s26-us259-association-card.png`) |
| 23 | Leave a card unanswered and go away | The default runs (pay the hours / let it be / refuse); the *While you were away* card says what it did | | `WorkplaceViewTests.Away_Line` |
| 24 | Dismiss the supervisor | A hold-to-confirm card | | `ConfirmCards` tests |
| 25 | Every number's **?** on the new sheets | A *Why?* in words, never a raw factor | | `WhyTests`, view tests |
| 26 | Text size 125% on the Chronicle, the Almanac card and the kirana's sheets | Nothing cut off | | Editor ✓ |
| 27 | A Hamlet town loads and plays as before | Nothing new shows before the Township; the Chronicle begins at the load | | `MigrationTests.Schema24_Chronicle` |
| 28 | An older save (schema 23 and every older one) | Loads, upgrades, plays | | `MigrationTests.EverySchemaLoads`, `Schema24_People` |
| 29 | Tap targets on the new cards (the three HR cards, the marketing card, the toast) | Every button easy to hit with a thumb | **Device only** | |
| 30 | Pan and zoom a Township with a kirana, 4 or more hands and the Chronicle open and shut | Smooth; frame time under the limits of ADR 0094 | **Device only** | |
| 31 | The Story toast on the phone's small screen | Readable, not under the notch, gone after 6 seconds | **Device only** | |
| 32 | Open the app and read the game's time (the HUD clock) against the phone's | They match within a minute. A town that was behind says *Your game clock was N min behind your phone. It has caught up; nothing was lost.* once | **Device only** | |

## What to report

- Any screen that looks wrong or overflows, with a screenshot.
- A card whose default did something you did not expect.
- A town that **stops** growing in the Village or the Township (never stuck is a rule): its day, what it had, what it lacked.
- The supervisor's price: does 15,000 feel right for what he gives?
- Anything in the Chronicle's wording that reads wrong.
