# 0133. Overlays as built: one hue in five steps, a tap card beside the acre, smoke hatched red, drawn as one mesh

- Status: Accepted (owner approved Preview 6, with the five recommendations, 2026-11-19)
- Date: 2026-11-19
- Deciders: Keyur Patel
- Amends: spec 024 §3.8
- Related: ADR 0043, 0119, 0121, 0125, 0129, 0130, 0131; spec 024 §3.8

## Context

Spec 024 §3.8 gives the map five overlays (fertility, land value, coverage, happiness, crime), one at a time from a button, one hue in five steps, a small card on a tap with its *Why?*, and the generator's smoke hatched red in coverage. Preview 6 showed the button, the chooser, every overlay on the real 24 × 18 map (from a built Township with a generator running), the tap card and a smoky acre's *Why?*. Three things needed deciding while building.

## Decision

1. **What the overlays read (`OverlayView`, `Sim.View`).** Nothing is added to the sim: an overlay is a view of the state, so it changes no state and costs no time (`OverlayViewTests.Overlays_ChangeNothing` hashes the state around every overlay and every acre's card).
   - *Fertility:* farm acres (not roads, river, hills, the highway, a closed parcel), in steps of 20.
   - *Land value:* every acre of land (`OverlayView.IsLand`: farm, yard, quarters, village, commons, bank, cattle, not closed), read at the last close. The fourth step starts at `upgrade.puccaMinLandValue` (1.15, ADR 0131), so its edge is the pucca offer and cannot drift from it; the other edges are 1.00, 1.10 and 1.25.
   - *Coverage:* how many of the four services reach the acre: the school and the clinic open, the watchman's post staffed, clean water (`UtilityRules.HasWater`). The same rules the land value and the households use (`CoverageRules.Within`, `CivicRules.Covered`), so the overlay and the sim cannot disagree (`EveryAcre_ShowsTheSimsNumber`).
   - *Happiness:* only acres with a household of yours; the mean when two homes share an acre (their *Why?* lists each).
   - *Crime:* one grey step on the land and "No crime yet" in the key and on the card; it opens like the others (it is only drawn muted in the chooser) so the button is the same in Sprint 24.
   - The key's edges are display scales, not balance, and live in `OverlayView`; every wording is a string row.
2. **The smoke** is the footprint of every source in `SmokeRules.Sources` (the generator and the open kilns), hatched red over coverage's blue; the grid switching the generator off clears it (`Smoke_StopsWhenTheGridReplacesTheGenerator`). The footprint is the one the kiln's placement ghost already shows (ADR 0129).
3. **The tap card.** With an overlay open a tap finds the acre under the finger (`MapPresenter.TileAt`) and opens a small card beside it, never over it and above the key: the acre's number, its value, a line in words, a **?** and **Open the acre**. The **?** opens the acre's *Why?*: the existing fertility and land value sources, and two new ones, *Services at this acre* (each service Yes or No with its distance and reach, the water's source, the smoke's cost) and *Happiness at this acre*. An acre with no number (a river, an acre with no household) says why and has no **?** (`EveryNumberHasWhy` checks every acre of every overlay). **Open the acre** is offered only where a plain tap would open a sheet. Opening any sheet ends the overlay (`Display`); a *Why?* from the card is not a sheet and leaves it up.
4. **Drawn as two meshes, not 432 sprites.** The map's sprites do not batch in the 2D renderer (a draw call each), so a tint sprite on every acre cost 282 draw calls on the whole-map view (+31%). The tint is one mesh of coloured diamonds (vertex colours, the sprite shader the weather's particles already use) and the hatch a second small one: **+4 draw calls and +4% triangles** with the overlay open at the same camera (911 → 915; 1,109 → 1,113). Both are built once and recoloured, so reading an overlay again (every 5 seconds while it is up) makes no garbage.
5. **Where the button is.** A round **Overlays** button above the Menu, from the Township (`HudVm.OverlaysAvailable`); green and reading **Close** while an overlay is up. The chooser opens beside it; the key sits at the bottom, centred between Collect all and the shortcuts.

## Consequences

- No save change, no golden change. Tests: `OverlayViewTests` (11), the view-keys test now covers the new rows. Editor tour by scripted touch: `docs/art/s23-153-*.png` (the button and chooser, the five overlays, the smoky acre's card and *Why?*, the fertility, land value and happiness cards, a river acre's card, **Open the acre**, the Menu ending an overlay); render statistics above.
- The grey of "dimmed" acres is a dark wash, not a grey: the keys say "Dimmed" (Preview 6 said grey).
- The map's sprites not batching is a finding for the frame-time work: any whole-map effect should be a mesh, not a sprite an acre.
- Land values are those of the last close, so the overlay never moves while the player looks; fertility and happiness are read again every 5 seconds.
