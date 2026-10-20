# 0043. Weather runs by season, the forecast as runs, *Why?* in words, the Menu and the full panels (Sprint 7 planning review)

- Status: Accepted
- Date: 2026-10-20
- Deciders: Keyur Patel
- Amends: spec 001 §3.5, §3.6, §4, §8 (AC-14, AC-16, AC-17, AC-19, AC-20), §9 (with ADR 0042); spec 008 §3.2, §3.3, §3.5, §3.6, §3.9, §3.13, §3.14, §7, §8 (AC-5, AC-8; AC-15 to AC-17 added); ADR 0042 §2 (the run-length targets per season); ADR 0039 §7 and ADR 0041 §4 (the People button and the gear)
- Related: ADR 0042 (1-hour spells), ADR 0032 (report labels), ADR 0028 (loans secured by the land they buy), ADR 0031 (inputs bought ahead, the mandi), ADR 0037 (the tractor-trolley), ADR 0007 (zero budget, CC0)
- GDD sections: Time and world simulation (Weather), UI/UX and screens (Mobile ergonomics, Accessibility), Art, audio and feel

## Context

The Sprint 7 planning review (2026-10-20) checked US-080 to US-084, US-087 and US-184 against specs 001 and 008, ADR 0042 and the code. The owner's rule for the sprint: every number the player sees gets a *Why?* that explains it in words, not raw factors. Eleven gaps were decided one by one, each as recommended unless noted.

## Decisions

1. **Weather runs per season (US-184).** Persistence is per weather and per season, chosen so the mean run lengths are:

   | Season | Sun | Cloudy | Rain | Heavy rain | Heatwave | Frost | Storm |
   | --- | --- | --- | --- | --- | --- | --- | --- |
   | Spring | 30 h | 10 h | 6 h | 1.5 h | 6 h | 3 h | — |
   | Summer | 35 h | 6 h | 4 h | — | 8 h | — | 1.8 h |
   | Monsoon | 6 h | 8 h | 18 h | 2.2 h | — | — | 1.5 h |
   | Winter | 40 h | 12 h | 6 h | — | — | 4 h | — |

   This refines ADR 0042 §2. Monsoon sun is a short break, not 1–2 days: at a 10% share a 30-hour run would turn up in only about 1 Monsoon in 3. Dry-season rain is a 4–6 hour run (about 4–6 world days, a western disturbance), so most Springs still get rain. `weights` stay the long-run shares; the loader derives the draw weights (ADR 0042 §3). A check of 200,000 hours a season reproduced every share exactly, and the season-to-season spread of the wet share does not grow (Spring standard deviation 0.16 → 0.14, Monsoon 0.23 → 0.18). There is no hour-of-day rule: 1 sim hour is about 1 world day, so a frost at 14:00 is a frosty day.
2. **The forecast is rolled per run (US-184).** One accuracy roll per actual run and day bucket (key: the run's first hour; counter: the bucket), so a wrong guess replaces the whole run part and days 2–3 never flicker hour by hour. The accuracy per hour stays 100% / 80% / 65%. The HUD shows the current run and the next ("Sun until 14:00 · then heavy rain about 1 h"; "Sun all day" when the run passes the day). Tapping the time card opens the forecast sheet: one row per shown run over 72 hours, days 2 and 3 marked "likely". Times read today, tomorrow, then the weekday (the game clock matches the phone's), computed as the phone's now plus the hours ahead.
3. ***Why?* in words (US-081).** A *Why?* row is a cause in a plain sentence with its effect as "N% more", "N% less" or "no change" (for sums: a label and its coins). There is no formula line and no factor anywhere. Rows with a *Why?* carry a small round **?** and tapping the row opens it; a long-press on any number also works. **Every number on every sheet and the HUD has a *Why?***, enforced by a test that fails for any row whose value contains a digit and has no *Why?* source. US-081 grows from 3 to 5 points.
4. **The Menu (US-080).** A Menu button bottom-right opens a full-height sheet: Store, Money and loans, People, Buy land (each parcel for sale with its price and **Show**, which pans to it and opens its sheet), Settings; the Letter joins in Sprint 8. The People button stays beside it as a shortcut with its badge. **The gear button goes** (Settings is in the Menu). A round **!** button with the alert count sits above Collect all when there are alerts, red and popping once for an urgent one; it opens the alert list, one row per alert with its action. A tap on the time card opens the forecast (the 3-second hold still opens the developer panel); a tap on cash opens Money and loans, a long-press its *Why?*; the same for the store.
5. **Money and loans (US-082).** One full-height sheet: cash, net worth and this season so far; the last season's report (**See report**; the report also opens by itself at a live season close, and a season closed while away adds a "See the report" row to the away card); one row per loan with **Repay…** (the next payment, all of it, or **Ask for more time** where restructuring is allowed); one row per lender with the limit and rate and **Borrow…**, which lists amounts (a quarter, a half, three quarters or all of the limit, rounded down to 1,000 and at least the minimum loan) and then terms, each with its payments and **total interest**. The land sheet gets **Buy with a bank loan** (ADR 0028). The developer panel's loan button goes.
6. **You and your family (US-082).** Tapping the house or the founder opens the founder's sheet: today's work, health and tiredness as words (each with *Why?*), the four food plans with cost, effect and **Choose**, the four work-hour options, the day-labour share in quarters (none, a quarter, a half, three quarters) with "about N coins a day", **Eat from the store** as an On / Off switch, **Buy medicine**, and **Open the store**.
7. **The market (US-082).** Tapping a good in the Store opens its sheet: in store, on offer, spoilage tomorrow, the market price, the trader's price and room (each with *Why?*); **Offer at the market** (all, a half or a quarter of what's free, then the minimum: any price, at least the trader's price, at least today's price), **Take off market**, **Sell to the trader**. The Store gets **Buy inputs** (one row per input with this season's price and how it compares; seed and fertiliser in "enough for 1 / 2 / 6 acres", the rest by count) and **Mandi trip** (load rows of none, 500, 1,000 or 2,000 kg per good, capped by the trolley and the stock; the sum written out against the trader's price; **Go to the mandi**).
8. **The full *While you were away* card (US-083).** One card with the farm and people rows, grouped and most valuable first, at most 8 and then "and N more" (tap for all), money rows counting up; **Collect all · N**, **See the report** when a season closed, **Close**. It shows only when something happened. Collect all into a full store ends with one prompt: "N kg won't fit. Sell the rest to the trader: N coins" (**Sell** / **Leave it**).
9. **Settings (US-084).** Numbers (Indian / Western, default by region), **text size (100% / 125% / 150%) in this sprint** (owner: no more deferring), reduced motion (default from iOS's *Reduce Motion*), sound, haptics (a light tap on harvest and sale). Reduced motion and haptics use one small iOS plugin (`Assets/Plugins/iOS`). Stored in the settings file, never in the town. The developer panel's format and motion toggles go. US-084 grows from 1 to 3 points.
10. **Feedback sounds (US-087).** Seven sounds from Kenney's CC0 packs: harvest (a pop and rustle), coins in (a jingle, pitch rising in three steps with the amount), coins out (a clink), button tap (a wooden click), sheet open and close (a swish), something ready (a chime), an urgent alert (a two-note bell). One per event, never stacking; the Sound switch mutes them all; licences in `ASSETS_LICENSES.md`.
11. **Scope: 25 points** (US-081 3 → 5, US-082 4 → 6, US-084 1 → 3). Order: US-184, US-081, US-082, US-080, US-083, US-084, US-087. If time runs short, US-087 is cut first.

## Consequences

- **Spec 001** §3.5, §3.6, §4, §8 and §9 are rewritten (a rule change, ADR 0027) and re-approved by the owner; the golden replays 001 to 005 and their frozen content are re-pinned in a commit that says so; the balance gate is re-run.
- **Spec 008** §3.2, §3.3, §3.5, §3.6, §3.9, §3.13, §3.14 and §8 follow these decisions (§10 lists them).
- **The GDD**'s Weather paragraph, Mobile ergonomics and Accessibility lines are updated.
- **Planning:** Sprint 7 is 25 points; F08.2, F08.5 and F01.3 get their tasks.
- **Players** see more numbers explained, with more taps on rows; the HUD has one button fewer.
