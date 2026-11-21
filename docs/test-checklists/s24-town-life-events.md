# Sprint 24 build: town life, events, insurance, forward contracts, the mill's atta, and hills and apples

- **Build:** a Development build from the head of branch `sprint/24-town-life-events` (made at the end of the sprint; see the pull request).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s24-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 21 to 22): nothing in your town moves. A Hamlet or Village town is unchanged; the new parts start at the Township. **A town saved by this build cannot go back to an older build.**

## What this build changes (story by story)

- **Town life (US-151).** From the Township, a *Town life* tab on the town sheet: approval, five reputation scores (each with its parts under a **?**), crime and inequality. Reputation draws families faster and lifts the treasury's grants a little. A crime overlay.
- **Petitions (US-150).** Eight needs the town raises (a clinic, rent, jobs…); you promise by a date or decline; doing the thing answers it. Approval moves with the town's happiness and the answers, and only draws or turns away families.
- **Festivals (US-149).** One a season at the community hall or the banyan square, paid by the treasury or as your gift. On the day nobody works except the stalls, and everyone is at the venue.
- **Events (US-154).** One roll a day at 6:00: at most two cards wait, each with a named free default after 24 or 48 hours. 18 events; a fire and an epidemic are warned 48 hours ahead. Nothing major happens while you are away. A fire's flames are drawn on the burning house or business.
- **Insurance (US-155).** Crop insurance (a switch that covers later sowings), property and business insurance bought at the close; claims are paid when the repair is paid.
- **Forward contracts (US-156).** The trader offers a fixed price for 40% of last season's harvest of your two biggest staples, 3% under the expected price.
- **The mill's atta (US-264).** The mill's unsold atta is offered to the village after your bakery and the trader.
- **Hills and apples (US-147).** The hills open with the Township. Apple orchards (100 trees an acre, bearing in 32 days, 3,000 kg in the Monsoon) grow on hill land only; hill acres take farming and orchards, not houses or shops. Apples keep in the cold store.
- **Without any of it** a Hamlet or Village town is as before.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, nothing is lost, the console shows no errors | | Editor ✓ |
| 2 | Developer panel → **Next era** to the Township; Menu → **Town treasury** → **Town life** | Approval, five score bars, crime and inequality; tap a bar's **?** | | Editor ✓ (`s24-151-life-tab.png`, `s24-151-why-education.png`) |
| 3 | **Overlays** → **Crime**; tap an acre | A terracotta ramp; a card with the acre's number and a **?** | | Editor ✓ (`s24-151-overlay-*.png`) |
| 4 | Let a close pass with idle households | A petition appears on the bell; **Promise** or **Decline** | | Editor ✓ (`s24-150-*.png`) |
| 5 | Community hall or banyan square → **Hold a festival** | Venue, payer and day; a confirm card with the cost and the guard; a banner on the day | | Editor ✓ (`s24-149-*.png`) |
| 6 | On the festival day | Nobody works but the stalls; everyone is drawn at the venue | | Editor ✓ (`s24-149-rest-day.png`, `s24-149-square.png`) |
| 7 | Wait for an event card (or the developer panel) | A card with its cost per choice, the default and its clock; **?** says why it came | | Editor ✓ (`s24-154-card.png`, `s24-154-why.png`) |
| 8 | Menu → **Events** | What is waiting and your last answers | | Editor ✓ (`s24-154-events.png`) |
| 9 | A fire warning, then the fire | An amber card and countdown; flames on the house; a repair card (at once or slowly) | | Editor ✓ (`s24-154-warning.png`, `s24-154-fire-on-house.png`, `s24-154-repair.png`) |
| 10 | Close the app for a few hours, open it | The away card lists any default taken; no disaster struck while you were away | **Device only** | |
| 11 | Menu → **Money** → **Insurance**: switch crop and property insurance on | The premium shown; the sow sheet has an *Insured* line; a business sheet has a *Business insurance* row | | Editor ✓ (`s24-155-*.png`) |
| 12 | After a fire with property insurance | 90% of the repair bill comes back at once and says so | | Editor ✓ (`s24-155-repair.png`) |
| 13 | Store → **Trader's offers** | Offers for your two biggest staples; **Accept** shows a binding confirm card | | Editor ✓ (`s24-156-*.png`) |
| 14 | Let a contracted crop be sold; then the delivery close | The first kilos go at the contract price; a shortfall is settled and shown on the season page | | Editor ✓ (`s24-156-running.png`, `s24-156-settled.png`) |
| 15 | Flour mill → Today | A line for the atta offered to the village, with its **?** | | Editor ✓ (`s24-264-mill.png`) |
| 16 | Your land → **For sale** (Township town) | Hill rows named "Hills", priced lower, with a note on what grows there | | Editor ✓ (`s24-147-lands-hills.png`) |
| 17 | Buy a hill acre beside your land; tap it | An orchard sheet: apple first, no house, shop or pump rows | | Editor ✓ (`s24-147-plant-on-hill.png`) |
| 18 | Try to plant apple on a valley acre | Apple is greyed with "Hill land only" | | Editor ✓ (`s24-147-plant-on-valley.png`) |
| 19 | Plant apples; look at the orchard sheet | "Bears fruit in", a payback "around the 1st harvest" | | Editor ✓ (`s24-147-orchard-young.png`) |
| 20 | Store → **Cold store** | An *Apple, Monsoon to Summer* line with its worth | | Editor ✓ (`s24-147-cold-store-*.png`) |
| 21 | Every number's **?** on the new sheets | A *Why?* in words, never a raw factor | | `WhyTests`, view tests |
| 22 | Text size 125% on the Town life tab and an event card | Nothing cut off | | Editor ✓ |
| 23 | Tap targets on the cards (event, petition, festival, confirm) | Every button easy to hit with a thumb | **Device only** | |
| 24 | Pan and zoom a Township town with an orchard for a minute | Smooth | **Device only** | |
| 25 | Developer panel → run the frame check on the Township map zoomed out and panned for a minute | Main-thread p95 ≤ 8.3 ms and GPU p95 ≤ 11 ms (ADR 0094); about 60 fps; under 1% of frames over 25 ms. Write both p95s down | **Device only** | |
| 26 | Developer panel → **Golden replays** | ALL PASS | **Device only** | |
| 27 | Developer panel → **Away cap (72 h)** with a Township open | It finishes inside the budget shown (2,000 ms) with no hitch | **Device only** | |

## Render statistics (Editor, 2026-11-21, a Township town in a Monsoon rain with the ring open and 68 people)

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| The whole map | 1,062 | 21,363 | 1,255 |
| The village | 547 | 11,052 | 620 |
| The farm | 393 | 8,021 | 411 |

The growth over the Sprint 23 readings is the town's (a Township town with 68 people), not the orchard's: the same town without the orchard measured 394 draw calls and 411 visible sprites on the farm against 393 and 411 with it.

## What to report

Anything that looks wrong, a tap that does nothing, a number without a **?**, a red line on the console (send its top line), or a town that no longer loads.
