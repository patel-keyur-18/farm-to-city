# Sprint 20 build: rent by place and lease, part-harvests for hands, a mandi trip with several crops, frame time

- **Build:** a Development build from the head of branch `sprint/20-treasury-and-taxes` (made at the end of the sprint; see the pull request).
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash).
- **Run in the Editor first (scripted touch, on a copy of your town):** every row below that is not marked **Device only**. Captures are in `docs/art/s20-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build upgrades your town as it loads (save schema 17 to 18): each tenant's present rent becomes a lease that starts now (so an old 6,000 rent stays until its lease ends, then moves to the market rent), and every household counts as settled from now. Nothing else changes.

## What this build changes

- **Rent (US-240).** A family house is now a *kutcha house*: it lets for about 2,500 a season, by its place (the district road acre 1.20, a lane 1.00, an inner acre 0.85) and the prices. A household's rent is fixed in a **lease of 4 seasons**. In the lease's last season a card asks how to renew it: **the market rise** (the default, applies if you don't answer), **a smaller rise**, **keep the rent**, or, if the family is over 30% of its income, **lower it**. A raise never goes above what 30% of the family's income allows, so a raise never pushes a family out. Smaller rises and kept rents make the family happier. A family that has lived here for years is patient before it leaves unhappy.
- **Part-harvests (US-241, ADR 0111).** Your hand's orders and hired labourers now take what fits the store each hour and leave the rest standing in the field (it waits under the 72-hour grace); nothing is sold to the trader unless the order's sell rule says so. Labourers you have paid stay and work their paid time even when nothing fits. The bell says "Store full: 1,122 kg of potato still standing on Field 5. It will be taken as room appears." with *Sell some* (and *Rent space* for grain); the away card has a *Still standing* line.
- **Mandi trip with several crops (US-252).** Store → Mandi trip: a slider and a kg box for each crop in your store, best gain over the trader first; each stops at the room the other crops leave in the 2,000 kg trolley; *Fill with what pays best* loads the best payers first; the sum has a line for each crop.
- **Balance (US-240).** Lower rents slow the Village: the bots reach it about 6 weeks in, not 4½ (the numbers are in the pull request, for your decision).

**Your questions:**
1. Does the renewal card read the way you approved in the preview? Is "Decide later" clear?
2. Is it clear why a raise stops where it does?
3. Can you find a family's lease and its choices on its sheet?

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Let a family house; wait for a family to move in; Menu → People → Town → the family | The sheet shows *Rent*, *Lease* ("n a season · n more seasons") and a **?** with the parts of the rent | | Editor ✓ (touch) |
| 2 | In the lease's last season, wait for the close of the season before | A card "A lease is ending: …" with the choices; the bell shows the same question | | Editor ✓ (the card came at the close, over the season page) |
| 3 | Tap *Raise to … (+5%)* on the card | The family's sheet says "Renews at …, as you chose"; at the close the new rent starts and the away card says so | | Editor ✓ (touch) |
| 4 | Tap *Decide later*, then open the family from the bell | The sheet has the same choices as buttons; with no answer the market rise applies at the close | | Editor ✓ (touch: Decide later, the bell row, Answer opened the family) |
| 5 | Several leases end at once | The card says "1 of 3"; *Market rise for all 3 left* ends the questions | | Unit test only (`RentViewTests`): one lease in the Editor town |
| 6 | A family whose rent is over 30% of its income | The card says a raise would not be fair now and offers *Keep it* or *Lower it* | | Unit test only (`RentTests.AffordableCap`, `RentViewTests`) |
| 7 | Fill the store, have a hand with a keep-crop order (or hire labourers) and let the crop ripen | The hand takes what fits each hour; the rest stands in the field; the bell says "Store full: … still standing on …" with *Sell some* | | Editor ✓ (the hand took its 20 kg part, the rest stood; the alert) |
| 8 | Tap *Sell some* in that alert, sell something, wait an hour | The Store opens; after room appears the standing crop is taken (the alert goes) | | Editor ✓ for the alert and Sell some; the room appearing is `PartHarvestTests` |
| 9 | Come back after being away with the store full | The away card has *Still standing* with the kg and the field | | Editor ✓ (the away card) |
| 10 | Store → Mandi trip with two or more crops in the store; tap *Fill with what pays best* | The best-paying crops load first until the trolley is full; the bar is full; the sum lists each crop | | Editor ✓ (touch) |
| 11 | Drag one crop's coin, then type a kg in another's box | The trolley bar, the other crops' limits and the sum change at once; a typed amount past the room is cut to what fits | | Editor ✓ (a drag; a typed kg set in code) |
| 12 | Tap *Go to the mandi* | One trip with every load; the hire is taken; the Store shows when it pays | | Editor ✓ (touch: the hire was taken, a trip started) |
| 13 | Pinch out so the whole map shows (the village, the farm and the ring of land), at night as in Sprint 17. Developer panel → **Measure frame time (20 s)**; don't touch the screen | **Device only.** Main thread p95 ≤ 8.3 ms and GPU p95 ≤ 11 ms (ADR 0094), about 60 fps, under 1% of frames over 25 ms; write both p95s here (Sprint 17: main 5.09, GPU 8.72) | Yes | |
| 14 | Developer panel → **Golden replays** | **Device only.** All three PASS with the `dotnet test` hashes (Phase 2's deferred check, ADR 0106) | Yes | |
| 15 | Developer panel → **Away cap (72 h)** | **Device only.** The catch-up finishes in under 2 s (count it; the panel shows the result) | Yes | |
