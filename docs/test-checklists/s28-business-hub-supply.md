# Sprint 28 build: UI fixes, one business run by you, the Businesses hub, supply, and a hand growing for your vegetable stall

- **Build:** a Development build from the head of branch `sprint/28-business-hub`, made at the end of Phase 3.5 (with Sprint 29's work if the owner has approved it by then; the rows for it are added then). Until it exists, every row below is checked in the Editor.
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row not marked **Device only**. Captures are in `docs/art/s28-265-*.png` to `s28-269-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 24 to 25). Every business loads on **Automatic**, with nothing counted yet; nothing in your town moves. **A town saved by this build cannot go back to an older build.**
4. **If your town runs two or more businesses yourself,** a card asks which one you keep. Time waits for your answer; the others become *Nobody yet*, hire for them from the hiring board.

## What this build changes (story by story)

- **UI fixes (US-265).** *Collect all* is hidden when nothing is ready (the alerts bell drops into its corner); a sheet's tab row scrolls sideways with a soft fade at the edge and the tab you are on in view; the badges on People, Orders and alerts fit their number on one line ("99+" at most).
- **One business run by you (US-266).** You can run one business yourself at a time. Starting a second is refused with the reason.
- **The Businesses hub (US-267).** A fourth round button on the map opens one sheet with every business: profit, cash, stars, who works it, and *Needs you* when it does. Take money out and See from the row.
- **Supply (US-268).** A **Supply** tab on a business's card: what it took today, from you and bought (6:00 to 6:00), **Automatic** (as before) or **Manual** (an amount from your store for each good).
- **Grow for the vegetable stall (US-269).** A hand's crop list has *For your vegetable stall*: he grows what the stall will lack tomorrow, keeps it in your store, and the stall takes it at 6:00.
- **Without any of it** a town plays as before: Automatic is today's rule, untouched.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, nothing is lost, the console shows no errors; an older save loads on Automatic | | Editor ✓ (schema 24 and 23 fixtures; `MigrationTests`) |
| 2 | With nothing ready to harvest, look at the bottom left | No *Collect all* button; the alerts bell sits in the corner. With three things ready it reads "Collect all · 3" | | Editor ✓ (`s28-265-no-collect-all.png`) |
| 3 | Open a sheet with many tabs (a business card, the Money page) and swipe the tab row | It scrolls sideways, an edge fade shows there is more, and the tab you are on is in view | | Editor ✓ (`s28-265-tabs-*.png`, `s28-265-tab-in-view.png`) |
| 4 | With many people or orders waiting, read the badges | The number is on one line inside a circle or pill; more than 99 reads "99+" | | Editor ✓ (`s28-265-badges.png`) |
| 5 | Run the flour mill yourself, then try to found or run a second business yourself | Refused, saying you run one already; it can still be hired out | | Editor ✓ (`s28-266-card-blocked.png`, `s28-266-plan-blocked.png`) |
| 6 | Load a town that runs two businesses yourself | A card lists both; keep one and the other becomes *Nobody yet*; a toast confirms | | Editor ✓ (`s28-266-load-card.png`, `s28-266-answered-toast.png`) |
| 7 | Tap the **Businesses** button (round, bottom right) | One sheet: totals, then a row for each business with profit, cash, stars and who works it; a **!** and a badge on the button when one *Needs you* | | Editor ✓ (`s28-267-hub-*.png`, `s28-267-hud-button.png`) |
| 8 | On a hub row, take money out | An amounts list; the hub shows the new cash; Back returns to the hub | | Editor ✓ (`s28-267-take-out.png`, `s28-267-after-take-out.png`) |
| 9 | Tap the **?** on the hub's profit and cash | A *Why?* in words (each business's share) | | Editor ✓ (`s28-267-why-*.png`) |
| 10 | Open a vegetable stall's card → **Supply** | A mode row (**Automatic**), a row for each good with "From you … · bought …" and "Takes all it needs"; *Buy the rest at the market* is here and no longer in Settings | | Editor ✓ (`s28-268-supply-automatic.png`) |
| 11 | Press **Use Manual**, then **Change** on a good | A list of amounts (all it needs, half, a quarter, fixed kg, none); the one in force is marked; Choose sets it and goes back | | Editor ✓ (`s28-268-supply-manual.png`, `s28-268-amounts.png`) |
| 12 | Set half for a good; let a morning pass (6:00) | The stall takes at most half its day's amount from your store; the rest is bought at the market if that switch is on, none if not; what you did not send stays in your store | | Editor ✓ (`s28-268-supply-capped.png`); Core test (`SupplyTests.ManualCap`) |
| 13 | Open the mill's and a tea stall's **Supply** | No Automatic/Manual switch: their own *wheat a day* and *milk a day* row is the cap, with the same Change as before | | Editor ✓; View test (`SupplyViewTests.MillAndTea`) |
| 14 | A restaurant's **Supply** | Fixed kg, none or all it needs only (it has no fixed day) | | View test (`SupplyViewTests.RestaurantAmounts`) |
| 15 | The hub's row for a business that takes goods in | A line "Today: 38 kg from you, 12 kg bought" with a **?** | | Editor ✓ (`s28-268-hub-supply-line.png`) |
| 16 | Tap the **?** on a supply row, the mode and a cap | A *Why?* in words: the day, from you, bought, today's prices; never a raw formula | | Editor ✓; `WhyTests` |
| 17 | With a vegetable stall open, tap a field with a hand's order → **Change** | The crop list has **For your vegetable stall** under *Let him choose*, saying what he would sow now; with two stalls, a row for each, named by its place | | Editor ✓ (`s28-269-crop-list-row.png`) |
| 18 | Choose it, then **Every season** | One sell step only: **Keep in store** ("The stall takes it from your store at 6:00 … Nothing is sold to the trader") | | Editor ✓ (`s28-269-how-long.png`, `s28-269-sell-step.png`) |
| 19 | Set it; open the hand's sheet (People → the hand) | The order reads "For your vegetable stall … · Keep in store · Every season" and a line "Stall preorder: okra 61.4 kg, amaranth 30.7 kg" | | Editor ✓ (`s28-269-hand-sheet-preorder.png`) |
| 20 | Tap that line's **?** | A *Why?*: the stall's day of the vegetable, its own stock, what your store holds, what is already growing, and what it still needs of each | | Editor ✓ (`s28-269-why-pick.png`) |
| 21 | Let the field be harvested and sown again | He sows the vegetable the stall lacks most; a second field takes another one (what is growing counts against the need) | | Editor ✓; Core tests (`SupplyTests.StallOrderPick`) |
| 22 | Close or sell the stall | The order acts as *Let him choose*, and the hand's sheet says "Your vegetable stall is gone, so … chooses the crop now." | | Editor ✓ (`s28-269-stall-gone.png`) |
| 23 | Every sheet the town can reach | Nothing spills out of its sheet, no text wider than its box | | Editor ✓ (`SheetLayoutCheck`: 76 sheets, 0 problems) |
| 24 | Text size 125% on the Businesses hub, a Supply tab and the crop list | Nothing cut off | | Editor ✓ (`s28-end-125-*.png`; the hub, a Supply tab and the crop list at 125%) |
| 25 | Tap targets: the Businesses button, the tab row, Change, Choose | Every button easy to hit with a thumb; a button tucked under the top edge of a scrolled sheet may not take a tap until it is scrolled down (seen once in scripted touch; tell me if it happens by finger) | **Device only** | |
| 26 | Pan and zoom the biggest town you have for a minute | Smooth | **Device only** | |
| 27 | Developer panel → run the frame check on the Township map zoomed out and panned for a minute | Main-thread p95 ≤ 8.3 ms and GPU p95 ≤ 11 ms (ADR 0094); about 60 fps; under 1% of frames over 25 ms. Write both p95s down | **Device only** | |
| 28 | Developer panel → **Golden replays** | ALL PASS (they did not move: the supply state is hashed only once used) | **Device only** | |
| 29 | Developer panel → **Away cap (72 h)** with several businesses open | It finishes inside the budget shown (2,000 ms) with no hitch; the away card lists what happened | **Device only** | |

## Tests (2026-11-26, at the end of US-269)

| Project | Result |
| --- | --- |
| `Sim.Core.Tests` (not Balance, not Perf) | 968 passed |
| `Sim.Core.Tests` Perf | 6 passed |
| `Sim.View.Tests` | 454 passed |
| Balance | US-268's run: 68 passed; US-269's is running |
| `GateTests.SupplyExploits` | 200 towns: 3,526 supply commands in zero time moved no coin; 2,082 morning flips and 508 stall orders kept every ledger exact |
| `GateTests.StallOrderMeasured` (report) | 20 towns, 20 days, two fields ordered for the stall against none: from you 35.3 t against 23.7 t, bought at the mandi 237.9 t against 249.2 t, net worth the same within 0.04% |

## Render statistics (Editor, 2026-11-26, after US-269, against the last reading)

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| The whole map | 1,282 | 25,633 | 1,636 |
| The village | 566 | 11,624 | 676 |
| The farm | 353 | 7,331 (**+11%** against 6,594; draw calls down from 381; no map code changed, the town has more crops in view) | 398 |

## What to report

Anything that looks wrong, a tap that does nothing, a number without a **?**, a business that takes more from your store than its cap, a hand that sows something the stall does not sell, a red line on the console (send its top line), or a town that no longer loads.
