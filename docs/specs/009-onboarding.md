# 009. Onboarding: the letter, the first session, the guided first week and the Hamlet unlock

- Status: Approved (2026-10-18), all six open questions answered as recommended (§10); amended and re-approved 2026-10-20 (ADR 0045: step 6's goal, nudges, the ask, AC-11 to AC-16)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: UI/UX and screens (Onboarding); Player journey and eras (Homestead, Hamlet); Learning layer
- Related ADRs: 0045 (Sprint 8 review: nudges, the ask, new ACs), 0041 (no daily cap, the ask's fallback card), 0036 (the notification ask in the first session), 0015 (first session and guided first week), 0030 (the inherited farm, the Hamlet gate), 0031 (the kitchen garden, the mandi), 0032 (farm profit, the seed-keeping hint), 0021 (crop calendar), 0034 (ready grace, subsistence automatic), 0022 (hiring from Homestead), 0009 (loans only when needed), 0024 (the play log), 0001 (inclusive invented names)
- Depends on specs: 001–007 (every trigger reads their state and events); 008 (the letter sheet, highlights, strings, *While you were away*)

## 1. Goal

A new player plays within a minute, makes their first sale within 15 minutes, leaves the first session with a crop growing and a reason to return, and through the first week meets each new idea once, at the moment it matters, in a letter from the grandmother who left them the farm.

**Lessons:** the farm teaches itself when you meet each idea as it happens; nobody lectures; every page can be read again.

## 2. Scope

**In:**
- **The letter:** the grandmother's letter as chapters (pages) in a letter sheet; reopenable at any time from the menu.
- **The first session** (ADR 0015): a short sequence of steps, each a goal on the game state with a highlight and a page, never blocking any action.
- **The guided first week:** chapters triggered once, each by a condition on the state or an event: the first return, the first field harvest, the store or trader full, the first pest, the first season close, each new season's crop hints, the labour limit (hiring), a shortage of cash or land (loans, only when needed), the first sick day.
- **Seasonal crop hints** in the letters (owner request, 2026-10-17), from the crop calendar in content.
- **The seed-keeping hint** (ADR 0032): switch eating from the store off to keep grain for seed.
- **The notification prompt's moment,** reconciled with spec 007 (open question 2).
- **The Hamlet unlock** (F09.3): the era check of ADR 0030 and 0032, its progress view and the celebration.
- **Seen flags** in the saved state, so each chapter is guided once (and replays reproduce it).

**Out** (and where it goes instead):
- Later firsts (the first school, businesses, the Codex): Phase 2 (F09.4, F14.2).
- Advisors as characters with an inbox: Phase 2 (F14.2). Phase 1 uses the letter and one-line nudges.
- Hindi text: F19.1 (strings are in tables).
- What the Hamlet *adds* (new systems): Phase 2. In Phase 1 the unlock ends the slice and the town carries on.

## 3. Rules and formulas

### 3.1 The letter

| Rule | Behaviour |
| --- | --- |
| Who | **Kamla**, the founder's grandmother on the father's side, who farmed the valley all her life; she signs "Your Dadi, Kamla". Warm, plain English with a few village words explained the first time they appear (*beta*, *baadi*, the kitchen garden; *mandi*, the town market; *zaid*, *kharif*, *rabi*) |
| Shape | Chapters of one page each (about 60–120 words), shown in the letter sheet (spec 008) over the map, with **Continue** or **Show me** (opens the relevant sheet with a highlight) |
| Reopen | Menu → Letter lists every chapter seen so far, in order; any can be read again. Unseen ones stay hidden (no spoilers) |
| Never a wall | A chapter is one sheet; it closes with one tap; it never blocks a command; at most one chapter opens at a time and the rest queue |
| Queue | If two triggers fire together, the earlier in §3.3's order opens first and the next opens when it closes; a chapter that triggers while away opens on return, after *While you were away* |

### 3.2 The first session

Steps run in order. Each has a goal (a condition on the state), a highlight (what bounces or glows) and a chapter. A goal reached early (the player is ahead) skips its step. Every step can be skipped from the letter ("I'll find my own way"), which ends the first session and keeps the week's chapters.

| # | Step | Goal (condition) | Highlight | Chapter |
| --- | --- | --- | --- | --- |
| 1 | The inheritance | The chapter is closed | — | `welcome` |
| 2 | Sow the *baadi* | A garden bed is Growing | Garden bed 1 | `garden` |
| 3 | The household | The founder sheet was opened and closed | The hut | `household` (with the seed-keeping hint, §3.5) |
| 4 | First pick | A `GardenPicked` event | The ready bed (radish 10 min, spinach 15, coriander 5) | `firstPick` |
| 5 | First sale | Any sale event (`SoldToTrader`, `MarketSales`) | The store button, then "Sell to trader" | `firstSale` |
| 6 | A long crop | A `CropSown` of a field crop with growth ≥ 12 h on any owned acre (ADR 0045) | Well acre 20 | `longCrop` (potato: free from the inherited seed) |
| 7 | Come back | The chapter is closed | — | `comeBack`: when the potato is ready, and the first notification prompt ("Your potato will be ready at 14:05 tomorrow. Want a nudge when it's ready? Never at night."; ADR 0036, 0045) with **Yes, nudge me** / **Not now** (iOS's dialog only after a Yes); with notifications already on, no question and **Continue**. Declined: spec 007's second ask after the first season close |

While the garden grows (steps 2–4, 5–15 minutes) the `household` chapter and a short farm tour fill the wait: the store (the inherited seed and grain), the well acres versus the rain-fed acres, the founder's labour.

**Nudges and the tour (ADR 0045).** While a step is current and its chapter is closed, a one-line nudge (`nudge.<step>`) shows as a pill at the top of the map ("Tap a garden bed and sow radish"); tapping it replays the step's highlight; it goes when the step is done or the session is skipped. The tour is three lines (`tour.1`–`tour.3`: the store, the well and rain-fed acres, the founder's hours) that the pill rotates through while the first bed grows, each opening its sheet; no extra chapters.

**The ask's fallback (ADR 0045).** If `comeBack` hasn't asked (the session was skipped, or passed another way), spec 007's plain card asks at the first field crop of at least a day. Either counts as the first ask; the second comes after the first season close; then never.

**Timing target:** steps 1–7 in 15–20 minutes of play (ADR 0015), measured by a bot that follows the highlights (AC-2).

### 3.3 The guided first week: chapters

Each chapter triggers **once**, the first time its condition holds while the app is live, and is then marked seen. In priority order (the queue order of §3.1):

| Chapter | Trigger (condition on state or event) | It explains |
| --- | --- | --- |
| `firstReturn` | The first `AwayAdvanceCompleted` | *While you were away*; crops wait ready for 72 h and then start to spoil (ADR 0034); Collect all |
| `firstFieldHarvest` | The first `CropHarvested` | The yield and its *Why?*: the weakest factor hurts most |
| `pest` | The first `PestWarning` | Spray now to cut the damage by 70%, or accept it; rotation lowers the risk |
| `sellOrStore` | The store above 90% or the trader's capacity used up for a good, the first time | The trader (now, at the floor), the market (a little at a time, a minimum price), the mandi (a full cart, 6 hours, cart hire), storing (spoilage, seasons) |
| `seasonReport` | The first `SeasonClosed` | Farm profit in plain words, prepaid living costs, why cash and profit differ |
| `season.<season>` | The first start of each season (Summer, Monsoon, Winter; Spring at the next year) | The seasonal crop hint (§3.4) |
| `labour` | The first time the labour needed by growing crops exceeds the founder's field hours (the care ratio below 1.0) for a whole hour | Care, the founder's limits, day labourers and a resident hand (spec 006); the Hamlet needs a hand's house |
| `borrowing` | The first `InsufficientFunds` rejection of a sowing, a hut or a hire, or the first time cash is below the cost of the hand's hut while the labour chapter is seen | The moneylender and the bank compared (spec 005), only now that it's needed (ADR 0009, 0015) |
| `sick` | The first `FounderSick` | Health, food, rest, medicine |
| `hamlet` | `EraUnlocked(hamlet)` | The celebration (§3.6) |

**Never pushed:** the borrowing chapter only explains; it never takes a loan or opens a loan sheet by itself.

### 3.4 Seasonal crop hints

Built from the content (`CropDef.sowSeasons`, `RegionDef.seasonalFactor`), never hard-coded, so they stay true when the calendar is tuned:

| Season | Hint (English, generated from content) |
| --- | --- |
| Summer | "Zaid season. Sow lentils (moong) or early rice; radish grows all year. Lentils give back to the soil." |
| Monsoon | "Kharif. The rains favour rice. Fields drain slowly now: watch for waterlogging." |
| Winter | "Rabi. Wheat, potato and lentils. Wheat fetches its best price now; potato stored from Spring sells dearer in the Monsoon." |
| Spring | "Late rabi. Potato and radish; buy next season's seed now, before the sowing rush makes it dearer." |

The crop names and the "best price" and "dearer" remarks are computed: the crops whose `sowSeasons` include the season; the good whose seasonal factor is highest this season; the seed whose price rises next season.

### 3.5 The seed-keeping hint (ADR 0032)

In the `household` chapter: "Your grandfather's wheat and lentils are in the store. The household eats from the store first. If you'd rather sow them, switch 'eat from the store' off for now." Shown again inside `season.winter` if the store still holds wheat or lentils and eating from the store is on.

### 3.6 The Hamlet unlock (F09.3; ADR 0030, 0032)

| Condition | Exact rule (open question 3) |
| --- | --- |
| Six acres harvested at good care | In one season (period): field harvests on at least 6 **different** owned acres, and the mean Care factor of all that season's field harvests ≥ 0.9 |
| A house for a hand | A completed hand's hut (spec 006) owned at the check |
| A profitable season | At least one closed season with farm profit > 0 (spec 005 §3.4) |

- **Checked** after every field harvest and at every season close (system Population, after Finance); **unlocks once**, when all three hold: `EraUnlocked(hamlet)`, the play-log milestone (spec 007).
- **Progress** (always visible from Menu → Letter → "Becoming a Hamlet"): this season's acres harvested (n of 6) and their mean Care; the hut (none, building, done); profitable seasons so far.
- **Celebration:** the `hamlet` chapter, a short code-driven vignette (spec 008 motion: the map's tint warms, a banner drops in), and "Your farm is now a Hamlet. More of the valley opens in the next update." The town carries on.

### 3.7 Where it lives

- **State (`Sim.Core`):** `Guidance`: the first-session step, the seen chapter ids, skipped or not; `Era`: the current era, this season's harvested acres and Care sum, the unlock time. Saved like any state (spec 007), so a replay reproduces it.
- **Triggers (`Sim.View`):** conditions are evaluated in the view layer from the state and the events after each command and `Sync` (spec 008 §3.1); opening a chapter sends `SeeChapter(id)`, so "seen" is a command in the log.
- **The era check** is a sim rule (`Sim.Core`), deterministic and tested.

## 4. Data

**New definitions:**

`content/onboarding.json`:

| Definition | Field | Type | Default | Notes |
| --- | --- | --- | --- | --- |
| `StepDef` (list, in order) | `id`, `goal` (kind and parameters), `highlight`, `chapter` | | §3.2 | Goal kinds: `chapterClosed`, `bedGrowing`, `sheetVisited`, `event`, `cropSown` (min growth hours, tile set) |
| `ChapterDef` (list, in priority order) | `id`, `trigger` (kind and parameters), `textKey`, `action` (optional sheet to open) | | §3.3 | Trigger kinds: `event`, `storeAbovePercent`, `traderFull`, `seasonStart`, `careBelowForHours`, `rejection`, `cashBelowHutCost` |
| `HintDef` | per-season template keys | | §3.4 | Filled from crop and price content |

`content/eras.json` (`EraDef`): `id` (homestead, hamlet), `conditions`: `acresHarvestedInSeason` 6, `meanCareAtLeast` 0.9, `handHutsAtLeast` 1, `profitableSeasonsAtLeast` 1.

**New state:**

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| `Guidance` | `step` | int | The first-session step (7 = done) |
| | `seen` | bit set by `ChapterDef` order | Chapters seen |
| | `skipped` | bool | The first session skipped |
| `Era` | `current` | era id | |
| | `seasonAcres` | set of tile ids (sorted) | Acres harvested this season |
| | `seasonCareSum`, `seasonHarvests` | Fixed, int | For the mean Care |
| | `profitableSeasons` | int | |
| | `unlockedAt` | long or none | |

## 5. Commands and events

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| `SeeChapter` | `chapterId` | `UnknownChapter` | Marks it seen (idempotent: seeing it again changes nothing) |
| `CompleteStep` | `stepId` | `UnknownStep`; `NotCurrentStep` | Advances the first session |
| `SkipFirstSession` | — | — | Ends the first session; chapters still trigger |

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| `EraProgress` | acres, mean Care, hut, profitable seasons | After a field harvest or a season close, when anything changed | Progress view |
| `EraUnlocked` | era id | §3.6 | The celebration, the play log (spec 007) |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Skip the first session | None | Fewer guided steps; the week's chapters still come | — | Chapters can be read any time |
| Keep eating from the store, or keep the grain for seed | Grocery money or seed | Saved seed is free but ages; eating saves cash now | Store, the household's food | Switch any time |

## 7. UI

```
Letter sheet (over the map)                            Becoming a Hamlet
+------------------------------------------------+     +-----------------------------------------------+
| My dear,                                        |     | This season: 3 of 6 acres harvested           |
| The farm is yours now. Six acres by the river,  |     |   mean Care 1.02 (needs 0.9)                  |
| the hut your grandfather built, and the well    |     | A house for a hand: building, ready in 9 h    |
| that never failed us. The fields have rested    |     | A season with farm profit: yes (Spring, 19,800)|
| too long; the soil is tired but honest.         |     | [Why?]                                        |
| Start small. Sow something quick in the baadi   |     +-----------------------------------------------+
| by the door...                                  |
|                       [Show me]   [Continue]    |
| Letter: 1 of 7 · reopen from the menu           |
+------------------------------------------------+
```

The highlight: the target bounces once (spec 008 §3.12) and keeps a soft glow until its step is done.

**Letter text (English string table, `letter.<id>`):**

- `welcome`: "My dear *beta*, the farm is yours now. Six acres by the river, the hut your grandfather built, and the well that never failed us. The fields have rested too long and the soil is tired, but it is honest: it gives back what you put in. You'll find seed potato, some wheat and lentils, and greens seed in the store, and a little money to begin. Start small."
- `garden`: "By the door is the *baadi*, our kitchen garden. Radish, spinach, coriander: they grow in minutes and feed the house. Sow one bed now; by the time you've looked around, it will be ready."
- `household`: "A farm runs on the people who work it, and you are the first. Choose how the house eats and how long you work: good food and rest let you do more, day after day. The house eats from the store first. Your grandfather kept wheat and lentils from his last harvest; if you'd rather sow them than eat them, switch 'eat from the store' off for now."
- `firstPick`: "There: your first basket. Everything you pick goes to the store, where it waits for you. Greens don't keep long, so eat them or sell them soon."
- `firstSale`: "The trader comes to the gate every day and buys at a fair, low price. That's money today. Later you'll learn when it pays to wait, or to take a cart to the *mandi* in town."
- `longCrop`: "Now something that takes longer. Potato does well in Spring on the well acres, and the seed is already in the store. It will be ready this time tomorrow. Crops wait in the field when they're ready, but not forever: after three days they start to spoil."
- `comeBack`: "That's a good first day. The farm keeps going while you're away: the crops grow, the house eats, the trader buys. Your potato will be ready at {readyTime} tomorrow. Want a nudge when it's ready? Never at night." (buttons **Yes, nudge me** / **Not now**; without the question and with **Continue** when notifications are on)
- Signature (every chapter): "Your Dadi, Kamla"
- `firstReturn`: "Welcome back. Here's what happened while you were away. Tap Collect all to bring in what's ready."
- `firstFieldHarvest`: "Your first field harvest. Tap *Why?* to see what made it: the soil, the water, your care, the weather, the seed. The weakest of them holds the rest back."
- `pest`: "Pests on one of your fields. Spray now and you'll save most of the crop; leave it and you'll lose some. Fields that grow the same crop again and again draw more pests."
- `sellOrStore`: "Your store is filling up. Three ways to sell: the trader at the gate (today, at his price), the village market (a little at a time, at a better price), or a cart to the *mandi* (a full load, half a day away, after the cart's hire). Or keep it for a better season, if it keeps."
- `seasonReport`: "Your first season is done. The report shows whether the farm paid for itself. Money you paid ahead for next season isn't a cost yet, so cash and profit tell different stories."
- `season.<season>`: the §3.4 hint.
- `labour`: "Your fields need more hands than yours. A day labourer helps for a day; a farm hand stays for the season but needs a hut to live in. A Hamlet begins with a hand's house."
- `borrowing`: "Money is short for what you want to do next. The moneylender lends quickly, at a high price. The bank lends more, cheaper, if you can show land or a good season. Borrow only what the work will pay back."
- `sick`: "You're unwell today and the fields will wait. Eat well, rest, and the chemist in the village has medicine."
- `hamlet`: "Look at what you've built: six acres in good heart, a house for a hand, a season that paid its way. This isn't a homestead any more. It's a Hamlet."

## 8. Acceptance criteria

Tests in `tests/Sim.View.Tests` (triggers, steps, hints) and `tests/Sim.Core.Tests` (guidance state, the era check).

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | `onboarding.json` and `eras.json` load; an unknown goal or trigger kind, a chapter without a string, or a step naming an unknown chapter fails validation naming the field | `OnboardingTests.Content_Validation` |
| AC-2 | A bot that does only what each step highlights completes steps 1–7 from a new game within 20 minutes of sim time, using only commands a player can send | `FirstSessionTests.HighlightsComplete` |
| AC-3 | A goal reached early skips its step; skipping the first session leaves every chapter to trigger; no step ever rejects or delays a command | `FirstSessionTests.SkipAndNeverBlock` |
| AC-4 | Each chapter of §3.3 opens exactly once, at the first time its condition holds live, in priority order when several hold; a trigger during an away advance opens on return after *While you were away* | `ChapterTests.GuidedOnce` |
| AC-5 | Seen flags and the step survive a save and load (spec 007) and a replay of the command log | `ChapterTests.SeenSurvivesSaveAndReplay` |
| AC-6 | The seasonal hints name exactly the crops whose `sowSeasons` include the season, the good with the highest seasonal factor, and the seed that gets dearer next season; changing the content changes the hint | `HintTests.FromContent` |
| AC-7 | The seed-keeping hint appears in `household`, and again at the first Winter only while wheat or lentils are in store and eating from the store is on | `HintTests.SeedKeeping` |
| AC-8 | The Hamlet unlocks exactly when, in one season, 6 different acres were harvested with mean Care ≥ 0.9, a hand's hut is complete, and a season had farm profit > 0; 6 harvests of fewer acres, a hut under construction, or no profitable season keep it locked; it emits `EraUnlocked` once | `EraTests.HamletGate_*` (9 tests) |
| AC-9 | The progress view shows this season's acres and mean Care, the hut's state and the profitable seasons, equal to the state | `EraTests.Progress` |
| AC-10 | Every chapter, step and hint text key exists in `en.json` (spec 008 AC-3) | `StringTests.Coverage` |
| AC-11 | The letter sheet shows the chapter's text, the signature "Your Dadi, Kamla" and "n of m seen", and **Show me** only when the chapter has an action; Continue sends `SeeChapter` (ADR 0045) | `LetterTests.Sheet` |
| AC-12 | Menu → Letter lists exactly the seen chapters in the order seen, then "Becoming a Hamlet"; an unseen chapter never appears; reopening one sends no command | `ChapterTests.ReopenList` |
| AC-13 | While a step is current and its chapter is closed its nudge shows; the tour lines rotate only during the garden wait; nothing shows once the step is done or the session skipped | `FirstSessionTests.Nudges` |
| AC-14 | `comeBack` asks with Yes / Not now, and shows only Continue when notifications are on; after a skip the card asks at the first field crop of at least a day; at most 2 asks in all | `FirstSessionTests.ComeBackAsks` |
| AC-15 | Over 100 seeds the competent bot's `EraUnlocked(hamlet)` comes at a median of day 7 ± 2, from the sim's own rule | `GateTests.HamletPace` |
| AC-16 | In AC-2's run, the first sale comes within 15 minutes of sim time | `FirstSessionTests.HighlightsComplete` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| First session length (bot following the highlights) | 15–20 minutes | ±5 min |
| First sale | Within 15 minutes of a new game | — |
| Hamlet unlock, competent player with a hand | End of the first week (ADR 0028, 0030) | ±2 days (balancer F20.1, playtest) |
| Testers who return on day 2 and day 3 (play log) | The Phase 1 gate | Playtest |

## 10. Open questions

All six were answered in the review with the owner (2026-10-18), each as recommended:

1. **The letter's author:** Kamla, the founder's Dadi (father's mother), signing "Your Dadi, Kamla"; plain English with a few village words explained in place (§3.1).
2. **The notification prompt:** first asked in the `comeBack` chapter, right after the long crop is sown, with its ready time; spec 007's second ask after the first season close stays (ADR 0036 amends spec 007).
3. **The Hamlet gate's counting:** in one season, field harvests on at least 6 different owned acres, with the mean Care of all that season's field harvests at least 0.9 (§3.6).
4. **The profitable season:** any closed season so far with farm profit above 0, not necessarily the season of the six acres (§3.6).
5. **Skipping:** "I'll find my own way" ends the first session's steps and highlights; the week's chapters still appear once each (§3.2).
6. **Seen flags:** in the sim state (`Guidance`), set by the `SeeChapter` command, so they save, export and replay with the town (§3.7).

**Sprint 8 planning review (2026-10-20, ADR 0045),** each answered as recommended:

7. **Nudges:** a one-line pill per current step, tapping replays the highlight; the tour is three rotating lines during the garden wait (§3.2, AC-13).
8. **Step 6** accepts a long crop on any owned acre (rule change, re-approved); `comeBack` reads "Never at night." with Yes / Not now; the plain card is the fallback when `comeBack` hasn't asked (§3.2, §7, AC-14).
9. **Six new ACs** (AC-11 to AC-16). The Hamlet vignette stays on the device checklist.
10. **The Phase 1 health checks** (F20.1, not this spec): cautious saver and random bots, never stuck with credit refused, recovery after foreclosure, exact ledgers, bounded prices; thresholds measured first and set realistically.
11. **Sprint 8's build** is a feature-complete Development build; the release candidate is Sprint 9's (US-095).

**Implementation notes (Sprint 8, no rule changed):**

- A page is marked seen (`SeeChapter`) when it is closed, so a page open when the app is killed comes back on the next launch.
- `firstReturn` has the trigger `awayCard`: it follows a While-you-were-away card. A short absence with nothing new shows no card (ADR 0043 §8), and "Here's what happened while you were away" over no card read wrongly.
- `firstPick` and `firstSale` have `pageAfter`: their pages react to the goal ("There: your first basket", "That's money today"), so they open when the step is done; the other step pages open when their step starts.
- What happened since launch (events, sheets visited, hours of low Care) is the guide's memory only. After a relaunch a step whose event came before it waits for the next one; a chapter whose moment passed comes the next time it holds.
- The era is checked after field harvests and season closes only (§3.6): a hut finished last unlocks at the next harvest or close.
- `firstFieldHarvest` says "Tap the ? beside it" (a *Why?* opens only from its ?, ADR 0044 §3). The season pages' titles are the season ("Winter"); their text keeps §3.4's opening word.
- The `comeBack` page leaves out "I'll find my own way" (nothing is left to skip).
- The highlight is a gold arrow bobbing over the target (the store pill bounces every 2 s); the nudge pill sits at the bottom centre.

**Device review (2026-10-21, ADR 0046):**

- The `firstSale` nudge, its arrow and the store pill's bounce show only while the store holds produce not on offer that the trader still buys today; otherwise the step waits quietly (`FirstSessionTests.Nudges`). After a long absence the house may have eaten the inherited grain, and "sell to the trader" pointed at nothing.
- The *While you were away* card now needs 8 hours away (spec 008 §3.9), so `firstReturn` comes after the first return of 8 hours or more.
- The first session took the owner 10 minutes (checks 1 to 12), at the low edge of §9's 15–20 ± 5. Sprint 9's playtest measures it with new players before anything is tuned.

## 11. Cut first

1. The Hamlet celebration vignette (keep the chapter).
2. The progress view (keep the chapter and the unlock).
3. The farm tour during the garden wait.
4. Seasonal hints computed from prices (keep the crop list).
5. The `sick` and `pest` chapters (the alerts still show).
