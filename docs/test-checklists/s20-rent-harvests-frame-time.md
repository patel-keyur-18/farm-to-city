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
- **Balance (US-240).** Lower rents slow the Village: the bots reach it about 6 weeks in, not 4½ (the numbers are in the pull request, for your decision).

**Your questions:**
1. Does the renewal card read the way you approved in the preview? Is "Decide later" clear?
2. Is it clear why a raise stops where it does?
3. Can you find a family's lease and its choices on its sheet?

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Let a family house; wait for a family to move in; Menu → People → Town → the family | The sheet shows *Rent*, *Lease* ("n a season · n more seasons") and a **?** with the parts of the rent | | |
| 2 | In the lease's last season, wait for the close of the season before | A card "A lease is ending: …" with the choices; the bell shows the same question | | |
| 3 | Tap *Raise to … (+5%)* on the card | The family's sheet says "Renews at …, as you chose"; at the close the new rent starts and the away card says so | | |
| 4 | Tap *Decide later*, then open the family from the bell | The sheet has the same choices as buttons; with no answer the market rise applies at the close | | |
| 5 | Several leases end at once | The card says "1 of 3"; *Market rise for all 3 left* ends the questions | | |
| 6 | A family whose rent is over 30% of its income | The card says a raise would not be fair now and offers *Keep it* or *Lower it* | | |
