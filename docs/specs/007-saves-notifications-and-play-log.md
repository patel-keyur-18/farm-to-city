# 007. Saves, notifications and the play log

- Status: Approved (2026-10-18), with the review answers in §10
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: Offline play and saves (Save system, Local notifications, Clock safety, Performance budget); Technical architecture; Roadmap (Phase 1 exit gate)
- Related ADRs: 0023 (one town, safety save points, no undo), 0024 (the local play log), 0002 (away rules, the 72-hour cap), 0007 (zero budget, personal builds), 0018 (Sim.Core is pure C#, IL2CPP-safe), 0026 (iPhone 13), 0029 (Unity pin)
- Depends on specs: 001 (state hash, `UpcomingJobs`, `Sync`, the command log, chunking invariance). Saves every later spec's state; reads events of 003–006 for notifications and milestones.

## 1. Goal

The player never loses their town and never has to think about saving: it saves itself continuously, recovers by itself after a crash, can move to another phone, and taps them on the shoulder when a field is ready; the owner gets an honest record of how testers play.

**Lessons:** none taught to the player directly; this spec protects the trust every other lesson needs (no lost progress, no rewinding: decisions stick, ADR 0023).

## 2. Scope

**In:**
- **The save codec** in `Sim.Core.Saves`: the whole game state to bytes and back, byte-identically (the same state hash), versioned, compressed and checksummed. Definitions are referenced by id, not index, so content can be reordered between builds.
- **The command log** in the save, with consecutive live `Sync`s compacted into one entry (owner requirement), and a checkpoint at a size cap so a save stays replayable.
- **Save points** (ADR 0023): atomic writes, the latest save plus rotating backups (3 hourly, 3 daily), automatic recovery from the newest valid one with a notice, *Save now*, never a rewind.
- **When to save:** every real hour, after a player command (debounced), and when the app goes to the background.
- **Export and import** of a town file, and **New game** replacing the town after a confirmation (with an offer to export first).
- **Local notifications** (opt-in): planned in the sim from upcoming jobs within the away cap, grouped, at most 2 a day, outside quiet hours; scheduled by the platform when the app goes to the background.
- **The play log** (ADR 0024, opt-in): session starts and ends and the Phase 1 milestones, kept on the device and exportable.

**Out** (and where it goes instead):
- Cloud backup (iCloud, Google Play Games): only if the game is published (ADR 0007, F19.5).
- Android notifications and file access: Android is untested for now (ADR 0026); the interfaces allow it later.
- Screens (settings, the recovery notice, the export and new-game confirmations, the opt-in prompts): spec 008 lays them out; this spec defines their content.
- Dev-build snapshot and restore in the time-travel panel (ADR 0023): F08.4. It uses this codec.
- Analytics or any network upload: never (GDD: no account, no server).

## 3. Rules and formulas

### 3.1 The save file

```
file      := header payload
header    := magic "FTC1" (4 bytes) | formatVersion (int32) | schemaVersion (int32) | simVersion (string)
             | payloadLength (int64) | payloadChecksum (uint64, FNV-1a 64 of the compressed payload)
payload   := gzip( state section | replay-base section (empty until the first checkpoint) | command-log section )
```

| Rule | Behaviour |
| --- | --- |
| Codec | Hand-written, IL2CPP-safe (ADR 0018: no reflection, no `dynamic`, no code generation): each state class writes and reads its own fields in a fixed order through `SaveWriter` / `SaveReader` (little-endian integers, length-prefixed UTF-8 strings, `Fixed` as its raw `long`). Lists are written in id order |
| Round trip | `Load(Save(state))` has the same `ComputeHash()`, and both continue identically: every later command gives the same state and the same events |
| References | A state field that points into content (a crop, a good, a building, a trait) is written as the definition's **id string** and resolved on load, so a build that reorders or adds content still loads older saves. An unknown id fails the load with `UnknownContentId` (a migration must map it) |
| Versions | `formatVersion` is the container (1). `schemaVersion` starts at 1 and rises whenever the state's fields change; the reader handles every schema from `SaveDef.minSchema` up, filling new fields with their documented defaults (migration in code, one step per version, each with a test). A newer schema than the build knows is refused with `SaveFromNewerVersion` |
| Checksum | FNV-1a 64 of the compressed payload (the hash spec 001 already uses). A mismatch, a short file or a parse error is `CorruptSave` |
| Compression | `System.IO.Compression.GZipStream` (in .NET Standard 2.1 and Unity's IL2CPP) at the fastest level |
| Size | Phase 1 target: under 200 KB for a town after 10 game years with the command log; GDD target under 5 MB at City scale |
| Not saved | Pending events and the last *While you were away* report (they are transient UI input); the content itself (the build provides it) |

### 3.2 The command log and Sync compaction

The command log (spec 001 §4) is the record of what the player did. Saving it lets a save be replayed for bug reports and determinism checks.

| Rule | Behaviour |
| --- | --- |
| Live runs | When an accepted `Sync` advanced **live** (0 ≤ Δ ≤ `awayThresholdSeconds`) and the previous log entry is a live run with no other command since, the entry is extended instead of appending: `LiveRun(fromWallUtc, toWallUtc, simSeconds)` |
| Replaying a run | Advances live by `simSeconds` and sets `lastWallUtc = toWallUtc`. Live advances are chunking-invariant (spec 001 AC), so one run of 3,600 one-second Syncs gives exactly the state of the 3,600 Syncs |
| What breaks a run | Any other command (a sow, a harvest, a dev advance), an away `Sync` (Δ above the threshold: it stays a `Sync`, so the away rules and the cap replay exactly), and a backwards clock (`ClockAnomalyDetected`: stays a `Sync`) |
| Effect | The log grows by about one entry per player action, not one per second: a 2-hour session with 300 actions is about 600 entries instead of 7,200 |
| Checkpoint | When a save finds the log at `SaveDef.maxLogEntries` (100,000, about 2 MB uncompressed, months of play) or more, the current state is stored in the save as the **replay base** and the log restarts empty from there. The town itself is never touched: the log is only history. A save then holds at most two copies of the state, and replay starts from the base (or from the new game while there is no base), so a reported bug can always be reproduced |

### 3.3 Save points (ADR 0023)

| Rule | Behaviour |
| --- | --- |
| Files | `town.ftc` (the latest), `town.hourly.1–3.ftc`, `town.daily.1–3.ftc`, in the app's persistent data folder |
| Atomic write | Write `town.tmp`, flush it to disk, read it back and verify the checksum, then rename it over `town.ftc`. A crash at any point leaves the previous `town.ftc` intact |
| Rotation | Before replacing `town.ftc`, if the newest hourly backup is at least 1 real hour old, the current `town.ftc` becomes `town.hourly.1` (the others shift; the 4th is deleted). Likewise daily backups at least 24 real hours apart (`SaveDef.hourlyBackups` 3, `dailyBackups` 3) |
| Load | At start the game loads `town.ftc`; if it fails (`CorruptSave`, a missing file), it tries the backups from newest to oldest and loads the first valid one. The player sees a notice naming it ("Your town was restored from the save of 14:00 today"). Nothing loads an older save point by choice: there is no undo |
| When to save | (a) every real hour of play; (b) 5 seconds after the last player command (not after a live `Sync`), so a burst of taps is one save; (c) when the app goes to the background (`OnApplicationPause`), synchronously before the OS can suspend it; (d) *Save now* |
| Save now | Saves at once; never rewinds or loads anything |
| Where the code lives | The codec and the rotation and recovery policy are pure C# in `Sim.Core.Saves`, working on an `IFileStore` (read, write, rename, delete, list with timestamps); `Game.Services` implements it with `System.IO` on `Application.persistentDataPath`. So every rule is tested with `dotnet test` on an in-memory store |

### 3.4 Export, import and a new game

| Rule | Behaviour |
| --- | --- |
| Export | Writes a copy of the latest save as `FarmToCity-<town>-<date>.ftc` into the app's Documents folder, which iOS shows in the Files app (`UIFileSharingEnabled`, `LSSupportsOpeningDocumentsInPlace`, set by our build step); no plugin needed (ADR 0007). From Files the player can AirDrop, email or message it. A one-tap share sheet waits for store readiness (F19.5) |
| Import | Any `.ftc` the player puts into the Documents folder is offered on the title screen ("Import <file>?"). It must load (checksum, version, content ids); importing replaces the current town after a confirmation that offers to export the current one first (ADR 0023). An old export can roll back progress: accepted, because it's a deliberate manual act |
| New game | Replaces the current town after the same confirmation and export offer. The backups of the old town are deleted with it (one town at a time) |

### 3.5 Local notifications

```
plan = NotificationPlanner.Plan(sim, settings)        pure, in Sim.Core; the platform schedules what it returns
```

| Rule | Behaviour |
| --- | --- |
| Opt-in | Off until the player agrees. The game's own prompt first appears in the first session, in the letter's `comeBack` chapter right after the long crop is sown ("Your potato will be ready at 14:05 tomorrow. Want a nudge when it is?"; ADR 0036, spec 009); iOS's one-time permission dialog follows only if they say yes. If they decline, it asks once more after the first season close, then never; Settings always has the switch |
| Sources | `UpcomingJobs(simTime + awayCap)` (spec 001 §3.7): crop ready, garden bed ready, tree matured, hut built. Derived from known season boundaries within the cap: fruit ripening at the next season start for a mature tree whose fruiting season it is; contract renewals and loan payments due (from spec 006 and F05.2, when built) |
| When | The platform asks for a plan when the app goes to the background and cancels every pending notification when it returns. Each item's wall time is `lastWallUtc + (due − simTime)` (spec 001) |
| Grouping | Items of the same kind within `NotifyDef.groupWindowMinutes` (30) are one notification ("3 fields are ready") |
| Quiet hours | Nothing between `quietFrom` and `quietTo` (22:00–07:00 device time): an item due then moves to `quietTo` and groups with others there |
| Cap | At most `NotifyDef.maxPerDay` (2) a device-calendar day, the earliest first; the rest are dropped (the *While you were away* report still lists everything) |
| Text | From the string table (spec 008): a kind key and a count; never a name or a number that could be wrong by the time it's read |
| Platform | iOS local notifications through Unity's free Mobile Notifications package (`com.unity.mobile.notifications`), pinned, behind `INotificationService` in `Game.Services` (approved; an ADR, the next free number, records the pin when it's added in Sprint 6). Only that one class touches the package; if the package fails on the device or is abandoned, a hand-written Objective-C plugin replaces it behind the same interface. Notifications never change the game state, so saves and determinism can't be affected |

### 3.6 The play log (ADR 0024)

| Rule | Behaviour |
| --- | --- |
| Opt-in | Off by default; a settings switch ("Help the developer: keep a local play log"). Nothing is ever sent anywhere |
| Entries | One JSON object per line in `playlog.jsonl`: `{ "t": wallUtc, "sim": simTime, "type": ..., ... }`. Types: `sessionStart` (build, device model), `sessionEnd` (seconds played), `away` (seconds away, applied, capped), and milestones, each once per town: `firstHarvest`, `firstGardenPick`, `firstSale`, `firstHire` (day labourer or hand), `firstHand`, `firstLoan`, `firstSeasonClose`, `hamletUnlocked`, and `newGame` |
| Source | Sessions from the app lifecycle; milestones from sim events in `Game.Presentation` (the sim stays unaware of the log) |
| Size | Capped at `PlayLogDef.maxBytes` (1 MB): the oldest lines are dropped |
| Export | Like a save: `FarmToCity-playlog-<date>.jsonl` into Documents (Files app) |
| Owner's use | Session days and lengths and milestone times show whether testers come back on day 2 and day 3 and reach the Hamlet in about a week (the Phase 1 gate, F10.1) |

## 4. Data

`content/saves.json` (`SaveDef`, `NotifyDef`, `PlayLogDef`):

| Definition | Field | Type | Default | Bounds |
| --- | --- | --- | --- | --- |
| `SaveDef` | `minSchema` | int | 1 | ≥ 1 |
| | `hourlyBackups`, `dailyBackups` | int | 3, 3 | 0–10 |
| | `autosaveMinutes` | int | 60 | 5–240 |
| | `commandDebounceSeconds` | int | 5 | 1–60 |
| | `maxLogEntries` | int | 100,000 | 1,000–1,000,000 |
| `NotifyDef` | `maxPerDay` | int | 2 | 0–10 |
| | `groupWindowMinutes` | int | 30 | 0–240 |
| | `quietFrom`, `quietTo` | hh:mm | 22:00, 07:00 | |
| `PlayLogDef` | `maxBytes` | int | 1,048,576 | ≥ 10,000 |

**State:** none new in `GameState` beyond the command log's `LiveRun` entries; the save adds the optional replay base. Player settings (notifications on, play log on, the opt-in prompt shown) live in a small settings file beside the save, not in the town.

## 5. Commands and events

No new sim commands: saving, loading, exporting and importing happen outside the simulation, in `Game.Services`, through these operations:

| Operation | Where | Result |
| --- | --- | --- |
| `SaveCodec.Write(state)` / `Read(bytes, config)` | `Sim.Core.Saves` | Bytes / a state or `CorruptSave`, `SaveFromNewerVersion`, `UnknownContentId` |
| `SavePoints.Save(state)` / `LoadNewestValid()` | `Sim.Core.Saves` over `IFileStore` | Writes and rotates / the state and which file it came from |
| `NotificationPlanner.Plan(sim, settings)` | `Sim.Core.Saves` (pure) | A list of (wall time, kind, count) |
| `ExportTown()`, `ImportTown(file)`, `NewTown()` | `Game.Services` | Files in Documents; confirmations in spec 008 |

**Events** (to the UI, from the services):

| Event | Payload | When | Consumers |
| --- | --- | --- | --- |
| `TownRestored` | file name, saved-at time | Load fell back to a backup | Recovery notice (008) |
| `TownSaved` | when, size | After each save | Settings (last saved), dev panel |
| `ImportAvailable` | file name | A `.ftc` is found in Documents | Title screen |

## 6. Decision card

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| Turn notifications on | An iOS permission | Up to 2 nudges a day | Returning sooner keeps fields busy | Turn off any time |
| Export the town | None | A copy to move or keep | An old export can roll back progress (deliberately) | — |
| Import a town | Replaces the current town | — | The current town is lost unless exported | Only via another export |
| Start a new game | Replaces the town | — | Backups of the old town are deleted | No (export first) |
| Keep a play log | None | The owner can read how they played | — | Turn off; delete the file |

## 7. UI

Layouts are spec 008's. Content:

```
Settings                                       After a crash
+------------------------------------------+   +------------------------------------------+
| Saved automatically - last at 14:02      |   | Your town was restored from the save     |
| [Save now]                               |   | of 13:00 today. A few minutes of play    |
| Notifications: on   (at most 2 a day)    |   | may be missing.               [OK]       |
| Play log: off  (stays on this phone)     |   +------------------------------------------+
| [Export town]  [Export play log]         |
| [New game...]                            |   New game
+------------------------------------------+   +------------------------------------------+
                                               | This replaces your town for good.        |
                                               | [Export it first] [Replace] [Cancel]     |
                                               +------------------------------------------+
```

## 8. Acceptance criteria

All tests are in `tests/Sim.Core.Tests`.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Property: for 50 seeded states (crops, garden, trees, the founder, the ledger with reports and prepayments, the store, weather), `Read(Write(state))` has the same hash, and running the same 200 random commands on both gives the same hash and the same event lists | `SaveTests.RoundTrip_Property` |
| AC-2 | Flipping any single byte of a save, or cutting it short, makes `Read` fail with `CorruptSave`; never a wrong state | `SaveTests.Corruption_Detected` |
| AC-3 | A save written at schema 1 loads in a build at schema 2 with the new field's default; a save from a newer schema is refused with `SaveFromNewerVersion` | `SaveTests.Schema_MigrateAndRefuseNewer` |
| AC-4 | A save loads correctly with content whose goods, crops and buildings are listed in a different order; a save naming an id the content lacks fails with `UnknownContentId` | `SaveTests.ContentIds_NotIndices` |
| AC-5 | 3,600 one-second live Syncs leave one `LiveRun` entry; replaying the compacted log gives exactly the hash of the uncompacted run; a sow, an away Sync and a backwards clock each break the run | `SaveTests.SyncCompaction` |
| AC-6 | After 10 game years of bot play (spec 003 `FarmBot`) with per-second Syncs, the save is under 200 KB and the log under `maxLogEntries`; with a small cap in test content, a save at the cap checkpoints (replay base = the state, the log empty), the town's hash is unchanged, and replaying base + log reproduces the latest hash | `SaveTests.Size_AndCheckpoint` |
| AC-7 | On an in-memory store, a crash injected at each step of an atomic write (after writing the temp file, before the rename) leaves the previous save loadable | `SavePointTests.AtomicWrite_CrashSafe` |
| AC-8 | Over 5 simulated real days of hourly saves, exactly 1 latest, 3 hourly and 3 daily files exist with the right ages; with the latest corrupted, `LoadNewestValid` returns the newest valid backup and says which | `SavePointTests.Rotation_AndRecovery` |
| AC-9 | Save triggers: a burst of 10 commands within 5 seconds causes one save; live Syncs alone cause none until the hourly save | `SavePointTests.Debounce` |
| AC-10 | The planner returns only items within the away cap, grouped within 30 minutes, none in quiet hours (moved to 07:00), at most 2 a calendar day, and nothing when notifications are off | `NotificationTests.Plan_GroupCapQuiet` |
| AC-11 | The play log writes session and milestone lines once each per town, nothing when it is off, and stays under its byte cap | `PlayLogTests.Entries_OnceAndCapped` |
| AC-12 | Performance (CI, Release): writing and reading the 10-year save takes under 50 ms each | `PerfTests.Save_WriteRead` (category `Perf`) |

The atomic rename, `OnApplicationPause` and iOS notifications are checked on the device with the Sprint 6 test checklist (`docs/test-checklists/`), since `Game.Services` is Unity code.

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Progress lost to a crash | At most the play since the last save: a few seconds after a command, at most an hour of idle live play | — |
| Notifications a day | 0–2; most days 1 (a long crop finishing) | Shape |
| Command-log entries per hour of play | About 2 per player action, not 3,600 | Shape |
| Save size, Phase 1 | Under 200 KB after 10 game years | ×2 |

## 10. Open questions

All six were answered in the review with the owner (2026-10-18):

1. **The serializer:** hand-written binary with GZip and the FNV-1a checksum in `Sim.Core.Saves` (US-010.T2). It stores state only, never content; values are exact (`Fixed` raw, money in 1/100 coin); references are content ids; AC-1 proves the round trip and identical play after it.
2. **The command log in saves:** kept, compacted, with a checkpoint at 100,000 entries (the state becomes the replay base and the log restarts), so progress is never touched and a save stays replayable (§3.2).
3. **Export and import:** through the Files app in Phase 1; a share sheet at store readiness (F19.5) (§3.4).
4. **Notifications package:** Unity's Mobile Notifications, pinned behind `INotificationService`; an ADR (the next free number) when it's added (Sprint 6). The risks and their containment are in §3.5.
5. **Quiet hours:** 22:00–07:00 device time, in data; items move to 07:00 and group.
6. **When to ask:** after the first field harvest; once more after the first season close if declined. *Changed by ADR 0036 (2026-10-18): the first ask is in the first session, right after the long crop is sown.*

## 11. Cut first

1. Quiet hours (keep the 2-a-day cap).
2. The command-log checkpoint (only matters after months of play).
3. Daily backups (keep hourly ones).
4. Import (keep export, which also serves bug reports).
5. Grouping (one notification per item, still capped at 2).
