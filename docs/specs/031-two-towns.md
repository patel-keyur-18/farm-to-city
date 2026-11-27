# 031. Two towns

- Status: Approved (the owner, 2026-11-26)
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Offline play and saves (save system), UI/UX (settings and saves), Notifications
- Related ADRs: 0023 (superseded in part), 0041, 0046, 0047, 0170, 0172
- Depends on specs: 007 (saves, notifications, play log), 008 (HUD)

## 1. Goal

One player keeps two towns on one phone and plays a second strategy beside the first, switching between them in two taps; each town lives its own life, as if it were the only one. The lesson is the game's own: two strategies, two outcomes, side by side.

## 2. Scope

**In:**
- **Two towns (US-270):** two save slots; Settings → *Your towns* with a row per town and *Switch*; *Start a second town*; the switch (save, load, the usual away catch-up); the open town's name on the time card once two exist; New game, Export and Import on the open town.
- **Notifications and play logs (US-272):** notifications from both towns, each naming its town, a tap opening that town; a play log for each town.

**Out:**
- No sim change and no save schema change: a town is a whole save, as today.
- More than two towns; removing a town (New game replaces the open one); naming a town yourself (ADR 0172 §5).
- Sharing anything between towns (money, goods, people): never.

## 3. Rules

| Rule | Behaviour |
| --- | --- |
| Slots | Two: *Town 1* and *Town 2*. Town 1 is today's town, in today's folder (`Saves/`), so nothing moves for an existing player. Town 2 lives in `Saves/town2/`. A town's backups and recovery (spec 007) stay inside its own folder; Town 1's file operations never touch `town2/`. |
| The open town | The one last open is opened at launch (`PlayerPrefs` `town.open`, default 1). |
| Each town's own | Its save and backups, its clock (its own `LastWallUtc`: time passes for it while you play the other, and catches up when you open it, capped at 72 h like a closed app, ADR 0002), its away card, its guide and letters, its play log, its camera position. |
| Shared | Settings (notifications on or off, play log on or off, text size, reduced motion, sound, haptics, number format), the notification permission and its ask (asked once for the phone). |
| Start a second town | From an empty slot: saves the open town, then makes a new town in Town 2 exactly as a first game (the same inheritance, the first session, Dadi's letters, ADR 0172 gap 2); the notification ask isn't repeated. |
| Switch | A confirm card ("Switch to Town 2? Town 1 is saved and keeps its time"). Then: save the open town; plan its notifications (§3.1) and keep them; write its play log and camera; load the other town (its recovery and unreadable cards as at launch); the first Sync is its away advance, with its away card; the run-two card (spec 030) if it applies. A veil with "Opening Town 2…" covers the load. **The target is loaded first** (before the open town is torn down): if it can't be read (save and every backup bad), nothing changes, the open town stays, the usual *Unreadable* card shows (the bad file kept and copied to Documents), and the slot is empty again so Settings offers *Start a second town* (owner, 2026-11-27). |
| Town name on the HUD | With two towns, the time card's top line starts with the name: "Town 2 · Monsoon · 2 days left · Year 1". With one, as today. |
| New game | Replaces the open town only (the card names it: "Replaces Town 2 for good"), offering an export first, as today. |
| Export | The open town, the file named with it: `FarmToCity-Town2-2026-11-26-1430.ftc`. |
| Import | Into the open town, replacing it after the card (names it), as today. |
| The other town's row | Era, day, net worth and when it was last played, from a small summary file written beside its save at each save (`summary.json`: era, day, net worth, last saved; not part of the save, not hashed). |
| Both towns' clocks | The game-time-matches-the-phone rule (ADR 0170) applies to each town at its load. |

### 3.1 Notifications from both towns (US-272)

| Rule | Behaviour |
| --- | --- |
| The open town | Planned when the app goes to the background, as today (`NotificationPlanner.Plan`). |
| The other town | Planned once when you leave it (at a switch), from its own simulated absence, and kept in `Saves/town<n>/notes.json` (wall times, kinds, words). Nothing changes that town until it is opened again, so the plan stays right. Opening it deletes its kept plan. |
| Scheduling | At each backgrounding: the open town's plan plus the other's kept plan, merged. Over iOS's 64 pending (`maxPending`) the same rule as one town (ADR 0041) keeps the most important first (urgent, needs you, information; soonest first within each) and schedules them in time order (owner, 2026-11-27; this amends the first draft's "soonest 64"). The other town's kept notes that came due while you played this one (owner, 2026-11-27, gap 7): the urgent and needs-you ones go out once, a minute after you leave the app (after quiet hours at night) and leave its kept file; information ones are dropped; its future notes stay kept. Quiet hours apply to both. |
| Kept even when off | The leaving town's plan is always planned and kept at a switch, whether notifications are on or not; the toggle only decides whether anything is scheduled. Turning them on later then covers the other town too. |
| Words | With two towns, each title starts with its town: "Town 2 · Your wheat is ready". With one, as today. |
| A tap | Opens the town the notification came from (its id is in the notification's data). If it isn't the open one, the app switches to it at launch, with a toast "Town 2". |
| Turned off | Notifications off: none for either town. |

### 3.2 Play logs (US-272)

| Rule | Behaviour |
| --- | --- |
| Where | Each town's own folder (`playlog.jsonl`). |
| Lines | A switch writes `sessionEnd` in the town left and `sessionStart` in the town opened (with `fromNote` when a tap caused it). |
| Export | *Export play log* exports the open town's log, named with it. |

## 4. Data

**New or changed definitions:** none (the slot count, 2, is a constant: ADR 0172 §5).

**New or changed state:** none in the sim. Files outside the save: `summary.json` and `notes.json` per town folder; `PlayerPrefs`: `town.open`, and per town the camera (`camera.x.<n>`, `.y`, `.h`; Town 1 keeps today's keys).

## 5. Commands and events

No sim commands. Settings actions (presentation ids, like *Save now*): `townSwitch:<n>`, `townStart`.

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects | Reversibility |
| --- | --- | --- | --- | --- |
| Start a second town | none | a second town to look after; its time runs as you play the other | notifications from both | New game replaces it |
| Switch | none | the town left keeps its time; more than 72 h away is capped | its notifications planned | switch back |

## 7. UI

### 7.1 Settings → Your towns (top of Settings)

```
Your towns
  (house) Town 1 · Village · day 64 · net worth 14,20,000      Open now
  (house) Town 2 · Hamlet · day 6 · net worth 2,10,000          [Switch]
          Last played yesterday 21:40
```

With one town, the second row reads "Start a second town · a new town beside this one · [Start]".

### 7.2 The switch card

```
+------------------------------------------------+
| Switch to Town 2?                               |
| Town 1 is saved and its time keeps running.     |
| Town 2 catches up on the time since you left    |
| (at most 3 days).                               |
|              [Not now]   [Switch]               |
+------------------------------------------------+
```

### 7.3 The time card

```
Town 2 · Monsoon · 2 days left · Year 1
```

Previews of 7.1 and 7.2 (exact-looking artifacts) come before their UI code.

## 8. Acceptance criteria

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given one town, Settings shows it and *Start a second town*; starting it saves Town 1 and opens a new town with the first session | `TownSlotTests.StartSecond` (Editor), `SettingsViewTests.Towns` |
| AC-2 | Given two towns, a switch saves the open one and opens the other with its away card; switching back finds the first exactly as saved, plus its time away | `TownSlotTests.SwitchRoundTrip` (in `Game.Services.Tests`) |
| AC-3 | A town left more than 72 h catches up 72 h, as a closed app | `TownSlotTests.AwayCap` (in `Game.Services.Tests`) |
| AC-4 | Town 1's backups, recovery and New game never touch Town 2's folder, and the reverse | `SaveServiceTests.SlotsApart` (in `Game.Services.Tests`) |
| AC-5 | An existing player's Town 1 loads from today's folder unchanged | `SaveServiceTests.TownOneInPlace` |
| AC-6 | With two towns the time card and notification titles start with the town's name; with one, they don't | `HudViewTests.TownName`, `SettingsViewTests.NotificationTextTownName` |
| AC-7 | Backgrounding schedules the open town's plan and the other's kept plan, merged, at most 64 | `NotificationMergeTests.Merge`, `PastDue_AfterQuietHours`, `OverTheLimit_ImportanceFirst`, `KeptFile_RoundTrips` |
| AC-8 | A tap on Town 2's notification opens Town 2 | Editor (notification data stubbed), `TownSlotTests.OpenFromNote` (in `Game.Services.Tests`) |
| AC-9 | Each town's play log holds only its own sessions; a switch ends one and starts the other | `TownSlotTests.PlayLogPerTown` (in `Game.Services.Tests`) |
| AC-10 | Export and New game name the open town and act on it only | `SettingsViewTests.TownNamed` |
| AC-11 | Goldens and every test unchanged (no sim change) | the full suite |

## 9. Balancing hooks

None: no sim change.

## 10. Open questions

- None open: gaps closed with the owner (ADR 0172 §4–§5; spec 031 gaps 1–4, 2026-11-26: a tap opens its town, Town 2 starts with the first session, the name on the time card with two towns, no remove; gaps 5–7, 2026-11-27, Sprint 29's plan check: a switch to an unreadable town stays in the open town, merged plans keep importance first, the other town's past-due urgent and needs-you notes go out once).
- Implementation calls (no owner decision needed): `SaveServiceTests` run the real `SaveService.cs`, linked into a new `Game.Services.Tests` project (it uses only `System.IO` and `Sim.Core`; its `Game.Services` namespace would shadow `Sim.Core.Tests`' `Game(...)` helper, so it has a project of its own); a missing `summary.json` falls back to the save's header (saved-at, no era); *Start a second town* has no confirm card (nothing is lost; the veil covers it).

## 11. Cut first

1. The *Last played* line on the other town's row.
2. The "Opening Town 2…" veil's words (a plain veil).
