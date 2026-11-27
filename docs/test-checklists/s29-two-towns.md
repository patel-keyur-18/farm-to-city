# Sprint 29 build: two towns, notifications from both, and the graphics direction

- **Build:** a Development build from the head of branch `sprint/29-two-towns`, made at the end of Phase 3.5 (with Sprint 28's work; its rows are in `s28-business-hub-supply.md`). Until it exists, every row below is checked in the Editor. **No Development build is made until the owner asks.**
- **Device:** iPhone 13, iOS version: ____
- **Tested by / date:** ____
- **How to install:** see `how-to-build-ios.md`. This is **a Development build**, so it has the developer panel. Delete `unity/FarmToCity/Builds/iOS` before building (this avoids the macOS 27 crash). **Quit Xcode before building.**
- **Run in the Editor first (scripted touch, on a copy of your town):** every row not marked **Device only**. Captures are in `docs/art/s29-270-*.png` and `s29-272-*.png`. **Only a device can check:** the rows marked **Device only**.

## Before you install: keep your town safe

1. In the **old** app, go to Menu → Settings → **Export**. Keep that copy until this build is checked.
2. **Install over the old app. Don't delete the app first**: deleting it deletes your town.
3. This build changes no save: your town loads as it is, in today's folder, and is **Town 1**. Nothing in it moves. A second town lives in its own folder and a second save. **A town saved by this build can still be opened by Sprint 28's build**, but only the first town; the second is invisible to it.

## What this build changes (story by story)

- **Two towns (US-270).** Settings has **Your towns**: a row for each town (name, era, day, net worth, when last played), **Switch** on the other, and **Start a second town** while the slot is empty. Settings, the notification permission and the sound are shared; everything else (save, clock, play log, camera, letters) is each town's own. With two towns the time card starts with the town ("Town 2 · Monsoon · …"). New game, Export and Import act on the open town and name it.
- **Notifications from both towns (US-272).** The other town's reminders still arrive while you play this one, each title starting with its town ("Town 2 · Your wheat is ready"). Tapping one opens that town. Each town has its own play log.
- **The graphics direction (US-271).** Nothing changes in the game yet. Preview 27 (the samples) and spec 032 (approved) set a realistic valley, drawn from Sprint 30. **Device check first (US-283):** Sprint 30 starts with a build that puts the realistic house, field and people in, to read the frame time on your phone.

## Checks

| # | What to try | What to expect | Device only | Result |
| --- | --- | --- | --- | --- |
| 1 | Open the app with your town | The HUD draws, nothing is lost, no errors; the time card shows no town name (one town) | | Editor ✓ (`TownOneInPlace`) |
| 2 | Menu → Settings → **Your towns** | One row, *Town 1* (open), with its era, day and net worth; below it **Start a second town** | | Editor ✓ (`s29-270-towns-one.png`) |
| 3 | Tap **Start a second town** | The open town is saved, a veil says "Opening Town 2…", and a new town starts like a first game (the same inheritance, the first session, Dadi's letters); the notification ask is not shown again | | Editor ✓ (`s29-270-veil.png`, `s29-270-second-town-first-session.png`) |
| 4 | Look at the time card | It starts with "Town 2 ·" | | Editor ✓ (`s29-270-large-hud.png`) |
| 5 | Settings → Your towns | Two rows; Town 2 open; Town 1's row shows its era, day, net worth and when it was last played; **Switch** on Town 1 | | Editor ✓ (`s29-270-towns-two.png`) |
| 6 | Tap the **?** on a town's net worth | A *Why?* in words, titled with the town | | Editor ✓ (`s29-270-why-town.png`) |
| 7 | Tap **Switch** on Town 1 | A card says Town 2 is saved and keeps its time; **Switch** opens Town 1 behind the veil, with its welcome-back (away) card for the time you were away | | Editor ✓ (`s29-270-switch-card.png`, `s29-270-after-switch.png`) |
| 8 | Switch back to Town 2 | It is exactly as you left it, plus the time you were away; the camera is where you left it | | Editor ✓ (`TownSlotTests.SwitchRoundTrip`) |
| 9 | Leave a town for over three days (Editor: change its saved time) and open it | It catches up 72 hours, as a closed app does, and no more | | Editor ✓ (`s29-270-away-capped.png`, `TownSlotTests.AwayCap`) |
| 10 | Settings → Files: read **Export**, **Import**, **New game** | Each names the open town ("Replaces Town 2 for good"); the other town is said not to be touched | | Editor ✓ (`s29-270-files-named.png`, `s29-270-new-game-card.png`) |
| 11 | Export from Town 2 | The file is named with the town: `FarmToCity-Town2-…ftc` | | Editor ✓ (`SaveServiceTests`) |
| 12 | Damage Town 2's save and every backup (Editor: replace the files), then switch to it | Town 1 stays open and as it was; the *Unreadable* card shows; the bad file is kept; Settings offers **Start a second town** again | | Editor ✓ (`s29-270-bad-town.png`) |
| 13 | Text size 125% on Your towns and the switch card | Nothing cut off | | Editor ✓ (`s29-270-large-text.png`) |
| 14 | Put the app in the background while playing Town 2 (notifications on) | Reminders are scheduled for both towns; each title starts with its town; iOS groups each town's in its own thread | | Editor ✓ (logging service; `NotificationMergeTests`) |
| 15 | Play Town 2 for a long while after Town 1's final notice came due | The final notice goes out once, a minute after you leave the app (after quiet hours at night); a minor "a calf was born" is dropped; neither repeats | | Editor ✓ (`NotificationMergeTests.PastDue_AfterQuietHours`) |
| 16 | While playing Town 2, tap a Town 1 notification on the lock screen | The app opens Town 1 behind the veil and a toast says "Town 1"; no leftover card or letter of Town 2 over it | **Device only** | Editor ✓ with a stubbed tap (`s29-272-tap-opens-town.png`, `TownSlotTests.OpenFromNote`) |
| 17 | Tap a notification of the town that is already open, or an old one from before this build | Nothing switches | | Editor ✓ (`TownSlotTests.OpenFromNote`) |
| 18 | Settings → turn the play log on, play both towns, export each log | Each log holds only its own sessions, balanced start and end; a tap-opened session says `fromNote` | | Editor ✓ (`TownSlotTests.PlayLogPerTown`) |
| 19 | Every sheet the town can reach | Nothing spills out of its sheet, no text wider than its box | | Editor ✓ (`SheetLayoutCheck`: 76 sheets, 0 problems) |
| 20 | Switch towns ten times fast, with a sheet open and with a card up | Never a card, letter or veil of the old town over the new one; no error on the console | | Editor ✓ (the bug the tour found, fixed: `SheetPresenter.DropVeils`) |
| 21 | Preview 27 (the artifact) on your phone or Mac | Realistic, rich stylised and today side by side at true size; the cost table reads right | | Owner reviewed 2026-11-27 |
| 22 | Switch to the other town and back, ten times | The switch takes under about two seconds on the iPhone 13 and nothing hitches afterwards | **Device only** | |
| 23 | Developer panel → run the frame check on the Township map zoomed out and panned for a minute, in each town | Main-thread p95 ≤ 8.3 ms and GPU p95 ≤ 11 ms (ADR 0094); about 60 fps; under 1% of frames over 25 ms. Write both p95s down (this is the last reading with today's art: the baseline for the device check) | **Device only** | |
| 24 | Developer panel → **Golden replays** | ALL PASS (no sim change) | **Device only** | |
| 25 | Developer panel → **Away cap (72 h)** in a big town, and read its time | Inside the budget shown (2,000 ms) with no hitch | **Device only** | |
| 26 | Each save writes a small summary (net worth is read from the town): save a large Village town a few times | No visible pause at a save | **Device only** | |

## Tests (2026-11-27, at the end of US-272)

| Project | Result |
| --- | --- |
| `Sim.Core.Tests` (not Balance, not Perf) | 974 passed |
| `Sim.Core.Tests` Perf | 6 passed |
| `Sim.View.Tests` | 460 passed |
| `Game.Services.Tests` (new: compiles the Unity-side `SaveService.cs`) | 7 passed |
| Goldens | unmoved |

## Render statistics (Editor, 2026-11-27, after US-272, against the last reading)

| View | Draw calls | Triangles | Visible sprites |
| --- | --- | --- | --- |
| The whole map | 1,150 | 33,224 | 1,436 |
| The village | 576 | 16,251 | 675 |
| The farm | 364 | 10,634 | 398 |

**Flagged:** triangles are +40 to 46% against the US-269 reading (draw calls and sprites unchanged, no art code touched; weather off does not explain it, so the cause is not found). US-272 added nothing on top of US-270's reading.

## What to report

Anything that looks wrong, a switch that loses time or shows the wrong town, a notification that names the wrong town or arrives twice, a tap that opens the wrong town, a number without a **?**, a red line on the console (send its top line), or a town that no longer loads.
