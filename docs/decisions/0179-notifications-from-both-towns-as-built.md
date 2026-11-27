# 0179. US-272 notifications from both towns and a play log each, as built

- Status: Accepted (approved by the owner, 2026-11-27)
- Date: 2026-11-27
- Deciders: Keyur Patel
- Related ADRs: 0172, 0178, 0041, 0047

## Context

Spec 031 §3.1 and §3.2, on the seams US-270 built (a town's own folder, `LeaveOpenTown`, `town.open`). Preview 26 already showed the screens (the lock screen with the town in each title, the toast after a tap), so there was no new preview. The plan check found one gap and the owner decided it.

## Decision (the owner, 2026-11-27)

**The other town's notes that came due while you played the open town** (a Town 2 final notice during a long Town 1 session would otherwise never be announced): the urgent and needs-you ones go out once, one minute after you leave the app (after quiet hours at night), merged by importance with the rest, and then leave the town's kept file so they never repeat. Information ones (a calf born, a house built) are dropped. This replaces the plan check's first call ("past-due notes are dropped").

## As built

1. **The kept plan** (`KeptNotes`, Sim.Core.Saves; `SaveService.ReadNotes/WriteNotes/DeleteNotes`). When you leave a town (`LeaveOpenTown`: a switch or *Start a second town*) its plan is made from a simulated absence, as at a backgrounding, and written to `notes.json` in its own folder, whether notifications are on or not (so turning them on later covers both towns). The file is valid JSON with one note a line (wall time, kind, town, title, body); the words are made then, from that town's own view, so it needn't be loaded when they go out. Opening a town deletes its file (at launch, at a switch, when a second town starts): the town itself is the truth again.
2. **The merge** (`KeptNotes.Merge`). At each backgrounding the open town's plan and the other's kept plan are merged: future notes of both; the other's due notes by the owner's rule above. Over iOS's 64 pending (`maxPending`) the most important stay (urgent, needs you, information; soonest first within each) and the kept 64 are scheduled in time order, as for one town. What the other town keeps is its future notes only (those cut by the limit stay in the file). Past-due times move out of quiet hours with the planner's own rule (`NotificationPlanner.OutOfQuiet`, now public).
3. **The words** (`NotificationText.For(ctx, n, town)`, string `notify.townTitle`): with two towns each title starts with the town, "Town 2 · Your wheat is ready"; the body is unchanged; with one town, as before. iOS groups a town's notes in a thread of its own (`town2-CropReady`).
4. **The tap** (`NoteOpened`, `ScheduledNote.Town`, `GameRoot.OpenFromNote`). A note's data is now `<town>|<wall>:<n>`. A fresh tap on the other town's note makes the app open that town once the scene is up (the veil, the switch, then a toast "Town 2"); a tap on the open town's own note, an old note without a town, or a town that is gone, switches nothing. A town that can't be read leaves the open town as it is (ADR 0178 §1). `LoggingNotificationService.StubbedTap` stands in for a tap in the Editor.
5. **The play logs.** Each town's log is in its own folder (US-270); a switch ends the session in the town left (`sessionEnd`) and starts the one in the town opened (`sessionStart`, with `fromNote` when a tap caused it; the town the app opened on before the switch logs `fromNote: false`, so it is balanced and not counted as a tap).
6. **A bug found by the tour and fixed.** A switch with a card, a letter or the away card open (a tap on a note at launch can come while the welcome-back letter is up) left the old town's veil over the new town: Town 1 opened under Town 2's "The farm is yours" letter. A switch now drops every card, letter, away card and *Why?* popup of the old town, the waiting toasts, and the letter and celebration flags (`SheetPresenter.DropVeils`).

7. **A note the platform already delivered is not sent again (independent review, 2026-11-27).** `Merge` takes `sinceWallUtc`, the time the app last came to the foreground (`notify.foreground` in `PlayerPrefs`, set at launch and at each return): a kept note due at or before it was due while the app was closed, so the platform delivered it (or notifications were off) and it is dropped; only notes due since then count as past due. `NotificationMergeTests.DeliveredWhileClosed_NotSentAgain`. Known edge, accepted: a past-due note sent "a minute after you leave" is lost if you come back inside that minute (the return cancels it and moves the time forward).

## Tests

Core `NotificationMergeTests` (`Merge`: due urgent and needs-you at +60 s, due information dropped, the other town keeps only its future notes; `PastDue_AfterQuietHours`; `OverTheLimit_ImportanceFirst`; `KeptFile_RoundTrips`: quotes, a new line, Hindi, a backslash, a damaged line); View `SettingsViewTests.NotificationTextTownName` (AC-6); `Game.Services.Tests` `TownSlotTests.OpenFromNote` (the data's town, which taps switch, the kept file in the town's own folder), `PlayLogPerTown` (AC-9). Core 974 (not Balance, not Perf), Perf 6, View 460, Services 7; goldens unmoved.

Editor by scripted touch and the logging notification service: Town 1 left for Town 2, so `notes.json` held Town 1's eight notes titled "Town 1 · …"; backgrounding in Town 2 scheduled Town 1's and Town 2's notes together (threads `town1-HandsWork`, `town2-CloseShort`, "Town 2 · The season's close is short of cash"); Town 1's file made past due (a final notice, a finished house): the final notice went out "in 60 s" once, the house was dropped, the file was rewritten without both; a stubbed tap on Town 1's note while Town 2 was open (with Town 2's letter up) opened Town 1 with the toast "Town 1" and no letter over it (captures `docs/art/s29-272-*.png`); the play logs: Town 2 `sessionStart` (`fromNote:false`) then `sessionEnd`, Town 1 `sessionStart` with `fromNote:true`. `SheetLayoutCheck` 76 sheets, 0 problems. Render statistics: draw calls 1,150 / 576 / 364 and triangles 11% below US-270's reading (37,330 to 33,224); the Editor town's state moves these readings, not the code (the review's A/B, ADR 0178). Editor saves restored and identical.

## Consequences

- With notifications off nothing is scheduled for either town, but both towns' plans are still kept.
- The kept plan was made when you left the town, so it is right until you open it; a note that fired while you were in the app (the app cancels all pending notes on return) is not repeated: only what the merge schedules at the next backgrounding goes out.
- Not tested on the phone: the real iOS data round trip (`NoteOpened.TownOfData` is unit-tested; the query of the last responded notification is the existing code).
