# 0178. US-270 two towns, as built

- Status: Accepted (Preview 26 approved by the owner, 2026-11-27; the story awaits the owner's review)
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0172, 0023 (superseded in part), 0041, 0046, 0047, 0170

## Context

Spec 031 and Preview 26: two towns on one phone, each a whole save with its own clock, play log, camera and away advance, switched from Settings → Your towns. The plan check (2026-11-27) closed two gaps with the owner, one per message, and made six calls of its own; all are in spec 031 §3 and §10.

## Decisions (the owner, 2026-11-27)

1. **A switch to a town that can't be read stays put.** The target is loaded first; if it is unreadable (save and every backup bad) nothing changes: the open town stays, a card says "Town 2 couldn't be opened" (the bad file kept and copied to Documents as `Town2-town.unreadable-…`), and the slot is empty again, so Settings offers *Start a second town*. At launch an unreadable town still becomes a new town, as before.
2. **Merged notification plans keep importance first** (US-272's rule, recorded here with the spec): the one-town ranking (urgent, needs you, information; soonest first within each), the kept 64 scheduled in time order; past-due kept notes are dropped at the merge; the leaving town's plan is always kept, even with notifications off.

## As built

1. **Slots** (`SaveService`, Game.Services). One service for each slot: Town 1 in today's folder (`Saves/`, so no existing player's town moves), Town 2 in `Saves/town2/`. `DeleteTown` removes the files of its own folder only, which is what keeps one town's New game, backups and recovery away from the other's. `SaveService.SlotHasTown` asks without creating a folder. A town's copies in Documents carry its slot (`FarmToCity-Town2-2026-11-27-1409.ftc`, `…playlog-Town2-…`; the one-time "before update" copy and the unreadable copy too, so two towns on an older schema don't share one name).
2. **The summary** (`TownSummary`, Sim.Core.Saves): era id, game day, net worth and when saved, written as `summary.json` beside a save at each save (not part of the save, never hashed). A missing or damaged summary falls back to the save's header (the day and when; no era, no net worth). The open town's row is read from the town itself.
3. **The screens** (`SettingsSheet.Towns`, `Cards.SwitchTown`, `Cards.TownNotOpened`). *Your towns* at the top of Settings: a row for each town (a house, era · day · net worth, *Open now* or *Switch*, "Last played …" on the other, a *?*), or *Start a second town* in an empty slot (no confirm card: nothing is lost). *Switch* asks first. Export, Import, Export play log and New game name the open town; the New game and Import cards say what they replace and that the other town is not touched. The time card leads with the open town once two towns exist (`ViewContext.TownName`). Action ids are `townSwitch:<n>` and `townStart` (spec §5 said `town.switch:<n>` and `town.start2`; the view-key test reads dotted ids as string keys).
4. **The switch** (`GameRoot.Towns.cs`). Veil ("Opening Town 2…", a ring that turns unless reduced motion) shown first, the work 120 ms later, the veil lifts 250 ms after. The target is loaded; then the open town is saved and its session ends (play log line, camera, summary); `town.open` is set; the other town's play log is read from its own folder; the first Sync is its away advance, capped at 72 h, with its own away card, guide and letters; the camera comes back to where that town was left. Sheets, overlay, the waiting codex, milestone and story toasts of the old town are dropped. A new second town starts exactly as a first game (the first session, Dadi's letters).
5. **Per-town keys.** `PlayerPrefs`: `town.open`; the camera as `camera.x/y/h` for Town 1 and `camera.x.2/y.2/h.2` for Town 2.

## Differences from Preview 26

- None of the screens. The *?* on a town row is titled "Town 1 · net worth" (the first build's title was generic).

## Tests

`Game.Services.Tests` (a new project, because the Unity-side `SaveService.cs` is compiled into it as a linked file and its `Game.Services` namespace would shadow `Sim.Core.Tests`' `Game(...)` helper): `SaveServiceTests.SlotsApart` (AC-4), `TownOneInPlace` (AC-5), `ExportNamedAndSummary`; `TownSlotTests.SwitchRoundTrip` (AC-2), `AwayCap` (AC-3). View `SettingsViewTests.Towns`, `TownNamed` (AC-10), `SwitchCard`, `TownNotOpenedCard`, `HudViewTests.TownName` (AC-6, the time card). Existing tests updated for the named labels. Core 970 (not Balance, not Perf), Perf 6, View 459, Services 5; goldens unmoved (AC-11).

Editor by scripted touch (captures `docs/art/s29-270-*.png`): one-town Settings, Start a second town, Town 2's first session (letters, "Town 2 · Spring · 3 days left · Year 1"), two-town Settings, the switch card and the switch both ways (the time card's name, the camera, Town 1 as it was), the veil, the 72 h cap (a town anchored five days back reopens with "The farm paused after 72 hours."), Export names (`FarmToCity-Town2-…`), the play log counted per town (13 entries in Town 2), the New game card, text at 125% on Settings and the HUD, a switch to an unreadable Town 2 (stays in Town 1, the card, the slot empty). `SheetLayoutCheck` 76 sheets, 0 problems. Editor saves (the whole `Saves/` folder, `town2/` removed) restored and identical; the test exports removed from Documents.

Render statistics (Town 1, Frost): drawCalls 1,293 / 576 / 364 (all / village / farm), unchanged within 1%, sprites unchanged; **triangles +46% / +40% / +45%** (25,633 / 11,624 / 7,331 to 37,330 / 16,247 / 10,638). No map or sprite code changed. Switching the weather presenter off took 37,330 to 36,514, so the weather is not the cause, and earlier readings of the same view ranged from 19,625 to 32,056. The cause isn't found; flagged for the sprint's end. The count that matters for the frame budget, draw calls, did not move.

## Consequences

- Spec 031 §5 and §8 updated for the ids and the test project; the GDD's save section says two towns (ADR 0023 superseded in part).
- US-272 builds on `LeaveOpenTown`, the per-folder play log and `town.open`: the notification plan kept in `notes.json`, the merge, the town name in titles, the tap that opens its town.
- A town's summary costs a net-worth reading at each save (hourly and after a command): not measured on a large town yet.
