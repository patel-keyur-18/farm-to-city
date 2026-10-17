# 0006. Business model: free with a one-time unlock

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (real-time pacing: Village in about 1 week)
- GDD sections: Art, audio and feel (Monetisation), Offline play and saves, Open decisions and risks

## Context

The GDD left the business model open: free with a one-time unlock, or premium upfront. Ads, energy timers and paid speed-ups are already ruled out. Under ADR 0002's real-time pacing, Homestead and Hamlet together last about one week of real play.

## Options

1. **Free with a one-time unlock.** Many more downloads than a paid app, and a week-long free trial that builds attachment. The cost is entitlement logic and a free/paid gate to design.
2. **Premium upfront.** Simplest to build, with no gate. However, paid mobile apps get far fewer downloads, and players buy blind.

## Decision

Option 1. Homestead and Hamlet are free, and one non-consumable purchase unlocks the full game.

- **Soft gate.** The unlock offer appears when the player meets the Village unlock conditions. If they don't buy:
  - the town keeps running as a Hamlet indefinitely
  - the save never stalls, locks or loses progress
  - the offer can be reopened at any time from the Village unlock card
- **Purchase:**
  - StoreKit 2 on iOS and Google Play Billing on Android, with *Restore purchases*
  - the entitlement is cached locally, so after one successful purchase the game is fully playable offline for good
- **Price** is decided near launch and kept out of the design.
- **Optional extras** stay as the GDD lists them: cosmetic packs, map biomes and scenario packs. None of them touches the core economy.

## Consequences

- The first-week experience is the sales pitch. Phase 1's gate (testers come back on day 2 and day 3) is also the commercial gate.
- The simulation stays free of purchase logic. The entitlement is a platform-service flag that the era-unlock check reads through a port interface. The sim never calls a store.
- The onboarding and Village-unlock specs must include the offer screen. It explains what the full game adds, without pressure tactics.
