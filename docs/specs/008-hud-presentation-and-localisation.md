# 008. HUD, presentation and localisation

- Status: Approved (2026-10-18), all eight open questions answered as recommended (§10)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: UI/UX and screens; Art, audio and feel (Motion and feedback); Learning layer (reports); Offline play and saves (While you were away); Localisation
- Related ADRs: 0036 (fonts under SIL OFL, the low-poly art base), 0005 (landscape only), 0017 (code-first Unity, UI Toolkit, dev panel), 0024 (string tables, Indian number formatting), 0032 (report lines, plain-language labels and *Why?*), 0034 (ready grace, mandi labour, indexed amounts), 0002 (away rules, ready to harvest), 0007 (zero budget, CC0 art), 0015 (onboarding hooks), 0021 (crop calendar), 0026 (iPhone 13, half frame budget)
- Depends on specs: 001–007 (every screen shows their state and sends their commands). Spec 009 (onboarding) uses this spec's letter sheet, highlights and strings.

## 1. Goal

The player sees the farm at a glance, reaches any lever in at most three taps, and can ask *Why?* of every number. Every action answers with motion, every text comes from a string table, and every number reads naturally (1,20,000 coins, not 120000).

**Lessons:** the map and the numbers are the same world; every number has a cause you can open; the season report tells a story in plain words before it uses accounting terms.

## 2. Scope

**In:**
- **The view layer** (`Sim.View`, §3.1): pure C#, tested by `dotnet test`. It builds what each screen shows from the state and the events, and the commands each button sends.
- **Screens for Phase 1:** the map, the HUD, the bottom sheets (field and sow, the crop-calendar card, the garden, trees, the founder, the store and market, the mandi, hiring and the hand, finance: the season report and loans, buying land), the *While you were away* report, alerts, settings, and the letter sheet (content in spec 009).
- **The *Why?* system:** one sheet format, opened by tapping *Why?* or long-pressing any number, and every source in Phase 1.
- **The crop-calendar card** on the sow sheet (owner request, 2026-10-17).
- **The season report's labels and *Why?* text** for every line (ADR 0032).
- **Localisation groundwork** (ADR 0024): string tables, plurals, the locale formatter (Indian grouping with lakh and crore short forms, Western grouping elsewhere).
- **The motion catalogue:** which event or action triggers which motion, with durations, easing and reduced motion (GDD Art, audio and feel).
- **The map's look:** tiles and sprites by state, the day/night tint by sim hour, the season palette (US-024's style check feeds it).
- **The developer panel** (ADR 0017, spec 001 §7), compiled out of release builds.
- **Feedback sounds** (F08.5, Could): harvest, coins, notification, with mute.

**Out** (and where it goes instead):
- What each screen *contains* is defined by specs 001–007. This spec lays them out and adds the shared systems.
- Onboarding steps, letter text and the Hamlet celebration: spec 009.
- Overlays (fertility, land value), the Codex, the Chronicle, advisors' full inbox: Phase 2 and later (F14.2).
- Hindi and other languages: F19.1 (the string tables make them a translation job).
- Full accessibility (colour-blind palettes, screen readers): F19.2. Phase 1 has reduced motion and text size only (§3.13).
- The era-themed UI beyond Homestead (brass, glass): F19.3.
- Music and the soundscape: F19.6.

## 3. Rules and formulas

### 3.1 Architecture: a thin Unity layer over a tested view layer

| Layer | Assembly | What it holds | Tested by |
| --- | --- | --- | --- |
| Simulation | `Sim.Core` (exists) | State, rules, commands, events | `dotnet test` |
| **View** | **`Sim.View`** (new, `sim/Sim.View/`, netstandard2.1, C# 9, `noEngineReferences`) | View models built from the state and events: the HUD, each sheet's rows, *Why?* sheets, the crop-calendar card, the season report, *While you were away*, alerts; string tables and the formatter; the motion catalogue as data; which commands a button sends | `dotnet test` (new `tests/Sim.View.Tests`) |
| Presentation | `Game.Presentation` (Unity) | Draws the view models with UI Toolkit and sprites, plays motions, forwards taps | The device checklist each sprint |

- Like a Spring MVC controller with a tested service layer: `Sim.View` is the testable "view model", and Unity is the template that renders it.
- **Read only:** building a view never changes the state (AC-1 checks the state hash). Buttons send ordinary commands through `Simulation.Execute`; there are no view-only shortcuts.
- **Events drive motion,** state drives text: after each `Execute` or `Sync`, Presentation drains the events, plays their motions (§3.12) and redraws from fresh view models. The sim never waits for a motion.
- **Refresh:** views rebuild after any command, after each `Sync` (once a second), and when a sheet opens. A view model is a plain object; no view keeps references into `GameState`.

### 3.2 Layout (ADR 0005)

| Rule | Behaviour |
| --- | --- |
| Orientation | Landscape only, both directions |
| Reference | UI Toolkit scales from 1920 × 1080 (match 0.5); layouts hold from 16:9 to 21:9 phones and 4:3 tablets |
| Safe area | Every edge-anchored element sits inside the device's safe area (notch, home indicator), both landscape directions |
| Touch targets | At least 44 pt; spacing at least 8 pt |
| Thumb zones | Main actions in the bottom corners: **Collect all** bottom-left, **Menu** (store, finance, people, settings) bottom-right |
| Sheets | Every panel is a bottom sheet the full height of the screen with the same margin top and bottom (owner, device run 2, 2026-10-19: half height was cramped), 78% of the width so the map shows at the sides; swipe down or tap the map to close. A list of choices is one row per choice with its button on the row (the store's goods, the trees, the garden crops). At most one sheet open; a *Why?* sheet stacks on top of it |
| Three taps | Any Phase 1 lever: tap a field (1), an action (2), confirm (3); or Menu (1), a section (2), an action (3) |

### 3.3 The HUD

```
+-----------------------------------------------------------------------------------+
| Spring · Day 2 of 4 · Year 1  14:05  [sun] Sun   (tap: forecast)   34,790 coins ▸ |
|                                                                    Store 62% ▸    |
|                                                                                   |
|                             (the isometric map)                                   |
|                                                                                   |
| [! 2]  alerts                                                                     |
| [Collect all · 5]                                                     [Menu]      |
+-----------------------------------------------------------------------------------+
```

| Element | Shows | Tap |
| --- | --- | --- |
| Time strip (spec 001 §7) | Season, day of season, year, sim time, weather of the spell | The 3-day forecast sheet |
| Cash | Cash (whole coins) | The finance sheet; long-press: *Why?* of today's cash movements |
| Store | Store fill (% of capacity by weight) | The store and market sheet |
| Alerts | The count of open alerts (§3.10), urgent first | The alert list |
| Collect all | The count of ready things: ready crops, ready garden beds, ripe fruit | Collects them (§3.9) |
| Menu | — | Store, finance, people, buy land, the letter, settings |

### 3.4 The map

| Rule | Behaviour |
| --- | --- |
| Grid | The 8 × 6 map (ADR 0030) on an isometric tilemap; the camera starts on the inherited parcels; pinch zoom between the whole map and about 3 acres; drag to pan, clamped to the map |
| Tiles | By state: unowned (desaturated), owned farm (soil type), vacant, growing (3 stages by progress: under a third, under two thirds, beyond), ready (full stage plus a bobbing "ready" marker), waiting past the grace (a "spoiling" marker, ADR 0034), the yard (grass), the river |
| Slots | Tree sprites in yard and field-edge slots: sapling, young, mature, ripe |
| Buildings | Hut, well, the hand's hut (and its construction stage), the mandi cart away (the cart sprite leaves the yard) |
| Selection | Tap a tile, slot, bed or building: it bounces (§3.12) and its sheet opens |
| Placement | The hand's hut: a ghost preview follows the finger over yard tiles, green where `BuildHandHut` would be accepted and red with the rejection reason otherwise (F08.1 US-079.T4) |
| Day and night | A tint by the sim hour, from `UiDef.dayTint`, interpolated between hours: night 0.55 blue, dawn and dusk warm, day white (`content/ui.json`) |
| Season palette | The ground tint × the season tint (US-024's check: a warm valley tint, green Spring, golden Summer, lush Monsoon, hazy Winter) |

### 3.5 Sheets (content from specs 002–007)

| Sheet | Opens from | Content (spec) | Actions |
| --- | --- | --- | --- |
| Field | A farm tile | Spec 002 §7 field panel; a growing or ready crop's panel (spec 003 §7) with Care, Water, Weather, pest risk, time left or "ready · spoils in N h" | Sow, Spray, Harvest, Clear, Sell parcel |
| Sow | Field → Sow | The crop-calendar card (§3.7), seed (certified with stock, or saved with age), fertiliser (none, compost, chemical with stock), the expected yield and the cost, what is bought | Sow |
| Garden | A bed | Spec 003 §7 | Sow (radish, spinach, coriander), Pick |
| Tree | A slot | Spec 003 §7 plus payback (US-049) | Plant (a sapling list with costs), Pick, Remove |
| Founder | The hut | Spec 003 §7: labour today, the diet plan, hours, the day-labour split, fatigue, health, eating from the store | Change plan, hours, split, eat-from-store, Buy medicine |
| Store and market | HUD store, or the hut | Spec 004 §7: each good in store, on offer, the market price, the floor, the trader's capacity, spoilage tomorrow | Offer (with a minimum), Withdraw, Sell to trader, Buy inputs |
| Mandi | Store → Mandi | Up to 1,000 kg of loads, the regional price at return, commission, cart hire, the founder's 0.5 h, the comparison with the trader | Go to the mandi |
| Hiring and hand | Menu → People, or a hut | Spec 006 §7 | Hire, Interview, Offer, Renew, Let go, Orders, Build the hand's hut |
| Finance | Cash, or Menu → Finance | The latest season report (§3.8), net worth, loans (spec 005 §7), the ledger for the current season | Borrow, Repay |
| Buy land | An unowned parcel | Spec 002 §7 buy sheet | Buy, Buy with a loan |
| Settings | Menu → Settings | Spec 007 §7 plus number format, text size, reduced motion, sound (§3.13) | As spec 007 |

### 3.6 The *Why?* system

**One format.** A *Why?* sheet has a title, the value, an optional formula line, one row per term (label, value, and an optional note), and an optional footnote with the lesson. Every label and note is a string key with arguments; every value goes through the formatter.

```
Why? Yield of North acre: 70.7 kg
  = base x soil x water x care x seed x weather x tools x fertiliser x health
  base         75.0 kg      1,200 kg a season x 1/16 (6 h of a 4-day season)
  soil         0.875        loam 1.00 x fertility 0.875 x shade 1.00
  water        1.00         moisture stayed in wheat's band (35-65)
  care         1.10         you supplied more hours than the crop needed (cap 1.10)
  ...
  cap 120.0 kg (1.6 x base) not reached
  "The weakest factor hurts most: raise soil fertility to lift every harvest."
```

**Sources in Phase 1** (each is a `Sim.View` builder with a test, AC-5):

| *Why?* of | Rows | Spec |
| --- | --- | --- |
| A harvest's yield | §3.4 factors, the cap | 003 |
| A crop's Care, Water, Weather now | The care ratio (hours supplied ÷ needed, incl. bucket hours, a mandi trip), the water band, the weather table | 003 |
| Pest risk | Base, repeats, wet share, spray | 003 |
| Labour today | Hours × nutrition × (1 − fatigue) × health, the split, a mandi trip's cut, a sick day | 003 |
| Health and fatigue | The day's change by term | 003 |
| Fertility and the Soil factor | The season's changes by reason; soil × fertility × shade | 002 |
| A price | Regional = base × index × season; floor and ceiling = regional ∓ transport; the market price's supply and demand | 004 |
| Spoilage | Rate, tomorrow's loss; a waiting crop's grace | 004, 003 |
| The mandi | Gross, commission, cart hire, the trader alternative, the founder's 0.5 h | 004 |
| A land price | Base × index × soil × drainage × fertility × location; quick sale | 002 |
| Each season-report line | §3.8 | 005 |
| Net worth | Each term; book equity beside it | 005 |
| Credit limit | Base, collateral, income, score | 005 |
| A tree's payback | Cost, returns, the payback season or "doesn't pay back" | 003 |
| A hand's hours, an asked wage, a lapse | Spec 006 §7 | 006 |
| A rejected action | The rejection reason as a sentence ("Wheat can be sown in Winter only") | all |

### 3.7 The crop-calendar card (owner request)

On the sow sheet for a farm tile, for the current season, one row per crop:

| Column | Rule |
| --- | --- |
| Crop and time | Name and growth time ("Potato · 24 h") |
| Sow now? | Sowable this season, or greyed with its seasons ("Wheat: Winter only") |
| Suits now | Good when the tile's moisture now is inside the crop's band (water factor 1.00), fair when the factor is at least 0.85, poor below; with the weather's yield factor for the coming spells |
| This sowing (owner, Sprint 6) | **Harvest** `Yield(seasonYieldPerAcre × cycleShare, Soil, Water now, Care 1, Seed 1, Weather 1, Fertiliser chosen, Health 1)`; **Spend** its seed and fertiliser at today's input prices (from the store or bought); **Sells for** the harvest × Floor(good, now), and **Profit** = Sells for − Spend, both rounded to 10 coins and labelled "about". The trader's floor is used because it is what a whole harvest reliably fetches (ADR 0012). The Sow button says what is paid now. *Replaces "earns per acre a season", which read as one sowing's money for a 2-hour crop.* |
| Labour | `labourPerAcrePerDay` plus bucket hours on a well acre, against the founder's free hours today |

```
Sow on North acre (loam, fertility 45, moisture 45)            Spring
+------------------------------------------------------------------------+
| Crop      Time   Sow now        Suits now   Earns/acre/season  Labour  |
| Potato    24 h   yes            good        about 17,060       3.5 h   |
| Radish    2 h    yes            good        about 16,850       3.0 h   |
| Rice      24 h   Summer, Monsoon                                       |
| Lentils   8 h    Summer, Winter                                        |
| Wheat     6 h    Winter only                                           |
| Free hours today: 3.4 of 7.2                                  [Why?]   |
+------------------------------------------------------------------------+
```

Example (tile 20, Spring, loam at fertility 45, Soil 0.875, moisture 45, inside both bands so water 1.00): potato 7,000 × 0.875 × 1.00 × 5.30 − 700 × 22 = 32,462.5 − 15,400 = **17,060** (rounded to 10); radish 4,000 × 0.875 × 1.00 × 5.50 − 8 × 300 = 19,250 − 2,400 = **16,850**. At moisture 38 both would be "fair" (water 0.40 + 0.60 × 38 ÷ 40 = 0.97).

### 3.8 The season report: labels and *Why?* text (ADR 0032)

The report opens as a sheet at each `SeasonClosed` (and from Finance). Each line shows its plain label first; the accounting name is in its *Why?*.

| Line (key) | Label (English) | *Why?* text (English; arguments in braces) |
| --- | --- | --- |
| `farmIncome` | Money from your harvests | "What your crops and fruit earned this season: sold {sold}, eaten at home {homeUse}. Food your family ate from the store counts too: it's grocery money you didn't spend. Accountants call this revenue." |
| `farmCosts` | What farming cost you | "Seed {seed}, fertiliser and spray {fert}, hired help {wages}, wear and tear on buildings {depreciation}, carting and market fees {transport}. Inputs count when used, not when bought: buying ahead is a swap of cash for stock." |
| `farmProfit` | Did the farm pay for itself? | "Money from your harvests minus what farming cost you: {profit}. Above zero means the farm paid its way this season. The Hamlet needs one such season. Accountants call this farm profit." |
| `otherIncome` | Other money earned | "Day labour on neighbours' farms {dayLabour}, gains on things you sold {gains}." |
| `household` | Your family's living costs | "Food and basics on the {plan} plan {living}, medicine {health}. Paid at the start of the season and counted day by day." |
| `finance` | Interest | "What borrowing cost this season: {interest}. A loan helps only if what it buys earns more than this." |
| `other` | Losses on assets | "Trees removed or spent, land sold below what you paid: {loss}." |
| `netResult` | What's left after everything | "Everything earned minus everything spent: {net}. This is your profit for the season." |
| Cash flow | Cash: start, in, out, end | "Cash can move differently from profit: {upfront} was paid now for next season's living costs or wages, so cash fell by more than the costs." |
| Net worth | What you own minus what you owe | "Cash {cash}, prepaid {prepaid}, land at today's price {land}, buildings {buildings}, trees {trees}, goods in store {goods}, minus loans {loans}. Land and goods are at market value; your books show them at cost ({bookEquity})." |
| Home use | (inside Farm income) | "Home-grown food paid {homeUse} of your living costs; your garden gave {garden} of it." |

### 3.9 *While you were away* and Collect all

| Rule | Behaviour |
| --- | --- |
| When | On the first live `Sync` after an away advance (`AwayAdvanceCompleted`), as a card over the map |
| Content | Time away (and "the world paused after 72 hours" when capped); what became ready; sales by way (market, trader, mandi) with totals; day labour paid; home use; spoilage (store, field, tree) in kg; pest warnings; sick days; season closes (with a link to each report); the price index change |
| Rows | Grouped by kind, most valuable first; at most 8 rows, then "and N more" |
| Collect all | Sends `Harvest`, `PickGarden` and `PickFruit` for every ready thing, nearest to spoiling first, with **StoreOnly**. Whatever doesn't fit stays where it is, and one prompt offers "Sell the rest to the trader: N coins" (the same commands with SellRestToTrader) |

### 3.10 Alerts

| Level | Alert | When |
| --- | --- | --- |
| Urgent | Living costs at risk | Cash below the next season's plan cost within the last day of a season |
| Urgent | Loan payment missed / final notice | Spec 005 events |
| Decision | Pest outbreak on {field}: spray now? | `PestWarning`, until sprayed or ready |
| Decision | {crop} starts spoiling in {h} h | A ready crop or ripe batch within 12 h of its grace end |
| Decision | Store nearly full | Store above 90% |
| Info | {n} ready to collect | Ready count > 0 (the HUD button carries it) |
| Info | You're sick today | `FounderSick` |

Alerts are derived from state each refresh (not stored), except dismissals, which last until the condition clears.

### 3.11 Localisation (ADR 0024)

**String tables.** `content/strings/en.json`: a flat object of key → text. Keys are dotted, lower camel case (`report.farmProfit`, `report.farmProfit.why`, `reject.OutOfSeason`). Arguments are named in braces: `"Wheat can be sown in {seasons} only"`.

| Rule | Behaviour |
| --- | --- |
| Lookup | `Strings.Get(key, args)`: a missing key renders `⟦key⟧` in development builds and throws in tests (AC-3) |
| Plurals | A key with `.one` and `.other` forms (`alert.ready.one`: "1 ready to collect", `.other`: "{n} ready to collect"), chosen by the count |
| Arguments | Formatted by the formatter before insertion (money, kg, percent, time) |
| Coverage | Every `descriptionKey`, report key, rejection reason, event kind, good, crop, tree, plan and hours option has a string (AC-3) |
| Names | Invented, inclusive names only (ADR 0001); the founder's family name is a setting (spec 009) |

**The formatter.**

| Kind | en-IN | Western | Rule |
| --- | --- | --- | --- |
| Integer | 12,34,567 | 1,234,567 | Indian: the last three digits, then pairs |
| Money | 1,20,000 coins | 120,000 coins | Whole coins, rounded half away from zero; paise only in the ledger's detail rows (2 decimals) |
| Short money (HUD only, from 1 lakh / 1 million) | 1.2 L · 3.45 Cr | 1.2M · 12.34M | Up to 2 decimals, trailing zeros trimmed, rounded down so the HUD never overstates cash; below 1 lakh / 1 million, full numbers |
| Negative | −3,200 | −3,200 | A true minus sign; red in reports |
| kg | 1,850 kg · 70.7 kg · 0.4 kg | same grouping | Whole kg from 100 kg; one decimal below |
| Price | 5.30 /kg | 5.30 /kg | Two decimals |
| Percent | 62% · 3.5% | same | Whole from 10%; one decimal below |
| Factor | 0.875 | 0.875 | Three decimals, trailing zeros trimmed to two |
| Duration | 3 h 12 min · 2 d 4 h · 45 s | same | The two largest units |
| Date | Spring · Day 2 of 4 · Year 1 | same | — |

**Locale choice:** the device's region: India → en-IN, elsewhere Western; a settings switch overrides it.

**Fonts:** Baloo 2 (SIL OFL, rounded; headings, buttons and numbers on the HUD) and Noto Sans (body text), both bundled (ADR 0036, amended by ADR 0038); Noto Sans Devanagari joins with Hindi (F19.1). Layouts leave about 30% room for longer translations.

### 3.12 The motion catalogue

All motion is code-driven tweens on transforms and UI Toolkit styles (ADR 0017); none blocks input or the sim. Easing names: `OutBack` (overshoot 1.70158), `OutCubic`, `InOutCubic`, `InCubic`.

| Trigger | Motion | Duration | Easing | Reduced motion |
| --- | --- | --- | --- | --- |
| Tap a tile, slot, bed, building | Squash and stretch: scale 1 → 1.15 → 1 | 0.12 s up, 0.23 s back | OutCubic, OutBack | A 0.1 s highlight flash |
| Button press | Scale 0.95 while pressed, back on release | 0.08 s | OutCubic | None |
| `CropSown` | The field's sprite pops in from 0.6 scale | 0.25 s | OutBack | Instant |
| `CropReady`, `FruitRipe`, garden ready | The ready marker bobs (±4 pt) until collected | 1.2 s loop | InOutCubic | A static marker |
| `CropHarvested`, `GardenPicked`, `FruitPicked` | Up to 5 produce icons fly on an arc to the store button; the store button bounces; the store % counts up | 0.6 s (staggered 0.05 s) | InOutCubic | The store % updates; a 0.1 s flash |
| `MarketSales`, `SoldToTrader`, `MandiTripReturned`, `DayLabourPaid`, home use | Up to 5 coins fly to the cash; the cash counts up | 0.6 s flight, 0.8 s count | InOutCubic, OutCubic | The number changes at once |
| Cash spent (a command that pays) | The cash counts down; a coin falls from the cash to the target | 0.5 s | OutCubic | At once |
| `PestWarning` | The field shakes (±3 pt, 3 cycles); the alert badge pops | 0.3 s | linear | The badge only |
| `GoodsSpoiled` (field or tree) | The spoiling marker droops and fades | 0.4 s | InCubic | At once |
| `MandiTripStarted` / returned | The cart rolls out of the yard / back in | 1.0 s | InOutCubic | Hidden / shown |
| `TreeMatured` | The tree grows from its young sprite | 0.5 s | OutBack | Instant |
| Sheet open / close | Slide up from the bottom and fade in / slide down | 0.25 s / 0.18 s | OutCubic / InCubic | 0.1 s fade |
| *Why?* sheet | Scale from 0.92 and fade in over the sheet | 0.2 s | OutBack | Fade |
| `SeasonClosed` | The report card scales in; each line counts up in turn | 0.35 s card, 0.4 s per line | OutBack, OutCubic | Shown at once |
| `AwayAdvanceCompleted` | *While you were away* card drops in | 0.35 s | OutBack | Fade |

**Caps:** at most 5 flying icons per event and 20 at once; a motion triggered again restarts rather than stacks; the whole motion system stays under 0.5 ms a frame (checked in the next device build).

### 3.13 Settings, accessibility and sound (Phase 1)

| Setting | Values | Default |
| --- | --- | --- |
| Number format | Indian (lakh, crore) · Western | By the device's region |
| Text size | 100% · 125% · 150% | 100% |
| Reduced motion | on · off | The device's setting |
| Sound | on · off | on |
| Haptics | on · off | on (a light tap on harvest and sale) |

Settings live in the settings file beside the save (spec 007), never in the town. Feedback sounds (Could, F08.5): harvest, coins, notification; CC0 sources logged in `ASSETS_LICENSES.md`.

### 3.14 The developer panel (ADR 0017, spec 001 §7)

Opened by holding the time strip for 3 seconds, in development builds only (`DEVELOPMENT_BUILD`, or the Editor); the code is excluded from release builds by a compile define. Buttons: +1 hour, +1 day, +1 season, jump to the away cap (`DebugAdvance`), snapshot and restore (in memory), the seed and `isAway`, the golden-replay check (ADR 0035), and a number-format switch (en-IN / Western). The Phase 0 spike and style check are removed (ADR 0038); the spike is rebuilt on the real game at each phase end.

## 4. Data

**New definitions:**

`content/strings/en.json`: the English string table (§3.11).

`content/ui.json` (`UiDef`):

| Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- |
| `dayTint` | 24 RGB triples | night 0.55/0.60/0.80 (21:00–04:00), dawn 05:00–06:00 warm, day 1/1/1, dusk 18:00–20:00 warm | 0–1 | Interpolated between hours |
| `seasonTint` | 4 RGB triples | Spring 1/1/1, Summer 1.06/0.94/0.72, Monsoon 0.82/1/0.84, Winter 0.93/0.95/1 | 0–1.2 | From US-024 |
| `valleyTint` | RGB | 1/0.97/0.88 | 0–1.2 | |
| `motions` | list of (trigger, kind, durationMs, easing, reducedKind) | §3.12 | 0–3,000 ms | Tuned without re-approval |
| `maxFlyingPerEvent`, `maxFlyingTotal` | int | 5, 20 | 1–50 | |
| `spoilWarningHours` | int | 12 | 1–72 | §3.10 |
| `storeNearlyFullPercent` | int | 90 | 50–100 | |
| `awayRowsMax` | int | 8 | 3–20 | |

**State:** none in `GameState`. Player settings (§3.13) and alert dismissals are in the settings file (spec 007).

## 5. Commands and events

**Commands:** none new. Every button sends an existing command (specs 001–007); **Collect all** is a sequence of `Harvest`, `PickGarden` and `PickFruit` (§3.9). Presentation never changes state any other way.

**Events consumed:** every domain event of specs 001–007 is either in the motion catalogue (§3.12), in *While you were away* (§3.9), in alerts (§3.10), or listed as "state only" in `Sim.View`'s event map (AC-9 checks that none is forgotten).

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Collect all into a nearly full store | None | What doesn't fit waits (and may spoil after its grace) or sells at the floor | Store space, trader capacity | Sell or offer later |
| Number format | None | — | — | Any time |

## 7. UI

Mock-ups for the sheets not already drawn in specs 002–007:

```
Store and market sheet (bottom sheet)
+--------------------------------------------------------------------------------+
| Store 1,240 of 2,000 kg (62%)                         [Buy inputs] [Mandi]   x |
| Good       In store   On offer   Market   Trader   Trader can take   Spoils    |
| Potato     850 kg     80 kg      6.43     5.30     2,200 kg today    26 kg/day |
|   [Offer...] [Withdraw] [Sell 770 kg to trader: 4,081]                [Why?]   |
| Wheat      310 kg     0          26.10    22.25    2,000 kg today    1.6 kg    |
| Seed potato 175 kg (input, booked 3,850)                                       |
+--------------------------------------------------------------------------------+

Mandi sheet
+--------------------------------------------------------------------------------+
| Cart to the town mandi: up to 1,000 kg · back in 6 h · takes 0.5 h of your day |
| Load: Potato [ 1,000 kg ]                                                      |
| At the mandi 6.80 x 1,000 = 6,800  - commission 3% 204  - cart 800 = 5,796     |
| The trader would pay now: 5,300 (up to 2,200 kg today)        better by 496    |
| [Go to the mandi]                                                     [Why?]   |
+--------------------------------------------------------------------------------+

While you were away (6 h 40 min)
+---------------------------------------------------+
| Ready: 2 potato fields, 1 garden bed              |
| Sold: market 1,920 · trader 9,540                 |
| Day labour: 2,025      Home use: 2,700            |
| Spoiled: 12 kg spinach                            |
| Pest outbreak on South acre: spray now?           |
| [Collect all · 3]                       [Close]   |
+---------------------------------------------------+

Alerts
+---------------------------------------------------+
| ! Potato on North acre starts spoiling in 9 h     |
| ! Pest outbreak on South acre: spray now? [Spray] |
| i 3 ready to collect              [Collect all]   |
+---------------------------------------------------+
```

## 8. Acceptance criteria

Tests are in `tests/Sim.View.Tests` (new, NUnit, `dotnet test`). Layout, safe areas and motion feel are checked on the device each sprint (the test checklist), since they need Unity.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Building every view model (HUD, every sheet, every *Why?*, the report, *While you were away*, alerts) from a game in every Phase 1 situation leaves the state hash unchanged | `ViewTests.ReadOnly` |
| AC-2 | The string table loads; a duplicate key, a key with an unknown argument, or a plural without `.other` fails validation naming the key | `StringTests.Load_Validation` |
| AC-3 | Every key the sim and `Sim.View` use (ledger descriptions, report lines and *Why?*, rejection reasons, event kinds, goods, crops, trees, plans, hours options, alerts, motions) exists in `en.json`; a missing key throws in tests | `StringTests.Coverage` |
| AC-4 | The formatter gives exactly §3.11's examples: 1234567 → "12,34,567" (en-IN) and "1,234,567" (Western); 1,20,000.40 coins → "1,20,000 coins"; 12,345,678 coins short → "1.23 Cr" and "12.34M"; −3,200; 70.74 kg → "70.7 kg"; 5.3 → "5.30 /kg"; 0.0625 → "6.3%"; 11,520 s → "3 h 12 min" | `FormatTests.Examples` |
| AC-5 | Each *Why?* source of §3.6 lists exactly the terms of its spec's formula for a known state (the yield example of spec 003 §3.4, labour 7.2 h, potato's price band in Spring, a mandi trip of 1,000 kg, the land example of spec 002 §3.5, net worth, a tree's payback) | `WhyTests.Sources` |
| AC-6 | The crop-calendar card for tile 20 in Spring gives the §3.7 rows: potato "Harvest about 1,531 kg · Spend 3,850 coins · Sells for about 8,120 coins · Profit about 4,270 coins" (a quarter of the season's 17,060), radish "Profit about 350 coins", wheat greyed "Winter only"; at moisture 38 both rate "fair"; the fertilisers show their effect | `CalendarTests.CropCalendarCard` |
| AC-7 | The season report view shows every ADR 0032 line with its label and *Why?* key in §3.8's order, its amounts equal the report's, and the home-use and cash-flow notes appear when non-zero | `ReportViewTests.Labels_AndWhy` |
| AC-8 | *While you were away* built from the events of a 30-hour away advance groups ready things, sales by way, day labour, home use, spoilage, pests, sick days and season closes, with totals equal to the events' sums, and caps rows at 8 | `AwayViewTests.Summary` |
| AC-9 | Every domain event type in `Sim.Core` is mapped by `Sim.View`'s event map to a motion, an away row, an alert, or "state only"; a new event type without a mapping fails | `MotionTests.EveryEventMapped` |
| AC-10 | With reduced motion on, every motion's duration is at most 0.1 s and none bounces or flies | `MotionTests.ReducedMotion` |
| AC-11 | Collect all with room for everything collects every ready crop, bed and batch (nearest to spoiling first); with a full store it collects what fits and offers the rest to the trader for exactly the floor value | `CollectTests.CollectAll` |
| AC-12 | The alerts of §3.10 appear exactly when their conditions hold and clear when they don't (spoiling within 12 h, store over 90%, a pest warning until sprayed, cash short on the last day) | `AlertTests.Conditions` |
| AC-13 | A disabled action's reason renders as a sentence for every `RejectReason` the Phase 1 commands can return | `StringTests.RejectionSentences` |
| AC-14 | The HUD view's cash, store %, ready count and alert count equal the state's values in a seeded game at 10 sample times | `ViewTests.Hud` |

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Taps to any Phase 1 action | At most 3 | — (device checklist) |
| Presentation cost a frame on the iPhone 13 with the farm map | Under 2 ms (HUD, sheets, up to 20 flying icons), so with the sim it fits the 8.3 ms half budget | Device build |
| Players reading the season report (play log, Sprint 9) | Most open at least one *Why?* in the first week | Playtest |

## 10. Open questions

All eight were answered in the review with the owner (2026-10-18), each as recommended:

1. **The view layer:** a pure C# `Sim.View` assembly (`sim/Sim.View/`, netstandard2.1, C# 9, no Unity references) with its own test project `tests/Sim.View.Tests`; Unity only draws (§3.1).
2. **Money on screen:** whole coins everywhere, rounded half away from zero; paise only in the ledger's detail rows (§3.11).
3. **Number format:** by the device's region (Indian grouping with lakh and crore for India, Western elsewhere), with a Settings switch (ADR 0024 as written).
4. **Collect all with a full store:** collect what fits, nearest to spoiling first; one prompt offers the rest to the trader; never sells without asking (§3.9).
5. **"Earns per acre"** on the crop-calendar card: at the trader's floor, minus certified seed, for an acre kept busy all season, before labour (§3.7).
6. **Art style:** Kenney's low-poly *Isometric Landscape* is the base; setting-specific assets are made in Blender to match it (F19.3), with flat placeholders until then (ADR 0036).
7. **Fonts:** SIL OFL is allowed for fonts only (ADR 0036): **Noto Sans**, and Noto Sans Devanagari when Hindi arrives. The game is mostly English; Hindi is supported (F19.1), so every string stays in the tables and every layout leaves room for longer text.
8. **Phase 1 accessibility and sound:** reduced motion and text size in Phase 1; feedback sounds and haptics as Could; the rest in F19.2 (§3.13).

### Changes after approval (Sprint 4 planning review, 2026-10-18, ADR 0038)

- **First playable scope:** a minimal HUD (time strip, cash, store %, **Collect all** with StoreOnly) and a minimal **Store sheet** (goods in store, trader price and capacity, Sell to trader) are built in Sprint 4; the rest of §3.3 and §3.5 stays in Sprint 7. The ghost preview for placement (§3.4) is built with the hand's hut in Sprint 5.
- **Look:** flat painted placeholder sprites (supersampled, outlined, two-tone, soft shadows) over Kenney ground tiles until the Blender pipeline (US-171, Sprint 6) replaces them; the motion catalogue (§3.12) carries the feel.
- **Dev panel and Phase 0 code:** see §3.14. Fonts: see §3.11.

### Implementation notes (Sprint 4, 2026-10-18)

None of these changes a rule or a lesson.

1. **`Sim.View`** (`sim/Sim.View/`, netstandard2.1, C# 9) depends on `Sim.Core` and `Sim.Content` (whose JSON helpers it reuses); `tests/Sim.View.Tests` holds its tests. `UiDef` and its loader live in `Sim.View` (view data, not rules).
2. **Disabled actions use the sim's own answer.** `Simulation.Preview(command)` runs a command on a copy of the state (no log, no events out) and returns the result, so a greyed button shows the sim's rejection as a sentence and can never disagree with it. `GameState.Clone(withCommandLog: false)` backs it.
3. **Built in Sprint 4:** string tables and the formatter (AC-2, AC-3, AC-4, AC-13), the HUD (AC-14), Collect all with StoreOnly (AC-11 without the trader prompt), the crop-calendar card (AC-6), the map view model, isometric geometry, the camera limits, tints, the field/garden/tree/store sheets, read-only checks (AC-1) and reduced motion (AC-10). **Left for Sprint 7:** the *Why?* sheets (AC-5; the sheets carry `WhyKey` ids), the season-report view (AC-7), *While you were away* (AC-8), the event map (AC-9), alerts (AC-12) and the "sell the rest" prompt of Collect all.
4. **Coverage:** `StringTests.Coverage` finds ledger and score keys by scanning the sim sources, events and rejections by reflection and names from the content; `SheetTests.ViewKeys_AreAllInTheTable` scans `Sim.View`'s sources. Rejection sentences take optional `{crop}` and `{seasons}` arguments.
5. **The Unity layer** (`Game.Presentation`): `Art/` (the sprite painter, `SpriteBank`, `IconBank`), `Map/` (`MapPresenter`, `MapInput`), `Ui/` (`UiKit`, `HudPresenter`, `SheetPresenter`), `Motion/` (`Tweens`, `Fx`), `DevTools/` (the panel and the golden check, development builds and the Editor only). Fonts: Baloo 2 Bold and Noto Sans in `Resources/Fonts`. Taps pick trees, beds, the hut, then the tile; a tap on the map while a sheet is open only closes it; the view lifts the selection above the sheet.
6. **Verified in the Editor (Sprint 4):** play mode through the `unity` CLI (taps through `GameRoot.OnTap`, captures of the Game view): the map, the stages, ready bubbles, the sow, garden, store and land sheets, flying produce, the developer panel, and the golden replays (all four PASS with the `dotnet test` hashes under Mono). The device build is made with the Editor's Build button or the batch method `Game.Editor.BuildCli.IosDevelopment` (see the build guide). Screenshots: `docs/art/s04-*.png`.
7. **Short money** has no unit word (the HUD adds the coin); `Formatter.Coins` takes the singular for one coin.
8. **Device runs (2026-10-19).** Run 1 found touch broken (the input code waited for a mouse; fixed). Run 2's owner feedback: sheets are **full height** (§3.2 table above); the Store, tree and garden sheets list **one row per choice with its own button** (`LineVm.Action`); a growing crop's **Care, Water and Weather read Good, Fair or Poor** (`ViewContext.Rating`: good from `ratingGoodPercent` 95, fair from `ratingFairPercent` 80, in `content/ui.json`; the *Why?* sheet in Sprint 7 shows the numbers); empty tree places on the player's land are drawn as **stone-ringed pits** and can be tapped (they had no sprite, so a tap fell through to the land under them).


**Sprint 5 planning review (2026-10-19, ADR 0039):**
- **People button:** a round button bottom-right on the HUD with a badge counting waiting applicants; it becomes a Menu entry in Sprint 7 and stays as a shortcut. The People and hand sheets follow §3.2's one row per choice (content in spec 006 §7 and ADR 0039 §8). Orders are given from the field and tree sheets.
- **Placement (§3.4):** the hand's hut ghost drags over the map and snaps to the labour quarters' next free place (ADR 0040), green there and red with the reason elsewhere; a bar shows the cost.
- **Skill as stars and band words** (`ui.json`: Beginner < 35, Capable 35–54, Skilled 55–74, Expert ≥ 75; one star per 20 points), hours as "tends about N acres" (reference acre: wheat, 3 h a day), the day's Care in Good, Fair or Poor.
- **A people-only *While you were away* card** (§3.9's people rows: hands' actions, day labour, contracts) is built in Sprint 5 (US-182) on the away view model US-083 completes in Sprint 7.
- **People on the map** (presentation only): the founder and hands stand on tended fields, a hand walks to the field an order acts on, day labourers walk in from the map edge and leave at the daily pass.
- **Device review (ADR 0040):** the yard is drawn over two acres (house and well behind, garden, tulsi, haystack and woodpile in front) with a bamboo fence where it meets other land; hands' huts stand in the fenced labour quarters; people go round the fields with work, hoe at two spots on each (stand and work frames alternate), and go home when idle.


### Sprint 6 planning review (2026-10-19, ADR 0041)

- **Settings before the Menu:** a round gear button under the Store chip opens a full-height Settings sheet (one row per choice with its button: last saved and *Save now*, Notifications, Play log, *Export town*, *Import a town*, *Export play log*, *New game…* with *Export first*, *Replace*, *Cancel*). Sprint 7 adds the Menu entry and the rest of §3.13.
- **Art:** every map sprite, the ground, every icon and the people's walk cycles are Blender renders by the end of Sprint 6; the seasonal tint stays in code.

### Implementation notes (Sprint 6 Editor art review, 2026-10-20)

1. **Pictures on rows:** a good's rendered icon before the label on the Store's rows, the tree sheet's planting rows (the fruit) and the garden bed's sowing rows (the produce) (`LineVm.Icon`). The sow sheet's crop cards stay text for now.
2. **Soil as a word:** the field sheet's soil row reads "Soil for crops: Good / Fair / Poor" (the `ui.json` rating thresholds), no longer the raw soil factor; the *Why?* key stays `soil`.
3. **Markers stand on what they mark:** a ready, spoiling or pest bubble sits at the top of its sprite (a bed, a field's plants, a tree's crown) whatever the stage, and nearer bubbles draw in front of farther ones.
4. **Yard tree places:** two on the house acre (its left corner, and in front of the house beside the well) and two on the front acre (its left corner, and its bottom corner clear of the beds and the woodpile), so no grown tree hides behind the roof.
5. **Row buttons** have one minimum width, so a column of *Sell* or *Plant* buttons lines up.
6. **Switches show their state** (owner, Sprint 6 device run): Notifications and Play log are a green **On** or a red **Off** button, and tapping flips it; the line under says what it means. Every other Settings action is green (*New game…* stays red).
8. **The sow sheet shows one sowing's money** (owner, Sprint 6 device run): *Spend* (red), *Sells for* (green), *Profit*, with the seed and fertiliser behind the spend and the hours; a line explains them (§3.7, AC-6). The fertiliser choice is three cards, **Normal**, **Compost**, **Chemical**, each with its effect from the data ("+25% harvest · wears out the soil").
9. **People are always on the land:** the ground is drawn below every object, so a person on a tile's back half or at the map's edge no longer goes under the land.
7. **Short texts never wrap:** any label of up to 28 characters on one line is laid out without wrapping (`UiKit.Text`), because on the phone's fractional UI scale a measured width can round a hair short and the second line then drew over the text below ("Export play / log", "about / 16,850"). Labels made empty and filled later (toasts) still wrap.
## 11. Cut first

1. Feedback sounds and haptics.
2. The *Why?* of a rejected action as a sentence (keep the reason code's string).
3. Motion beyond the tap bounce, flying items and count-ups (keep those three: they carry the Hay Day feel).
4. The crop-calendar card's "suits now" column.
5. Short money forms (show full numbers).
6. Day/night tint (keep the season palette).
