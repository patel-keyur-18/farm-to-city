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
- **Local notifications** (opt-in): planned in the sim from a simulated absence within the away cap (ADR 0041), ranked by importance, grouped, outside quiet hours; scheduled by the platform when the app goes to the background.
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
| Codec | Hand-written, IL2CPP-safe (ADR 0018: no reflection, no `dynamic`, no code generation): each state class writes and reads its own fields in a fixed order through `SaveWriter` / `SaveReader` (little-endian integers, length-prefixed UTF-8 strings, `Fixed` as its raw `long`). Lists are written in id order. **One write path** serves the state hash and the save (ADR 0041), so every hashed field is saved |
| What a save holds | Everything the state hash covers (spec 001 to 006: clock, scheduler, jobs, weather, land, ledger, finance, market, farm, credit, people) plus the command log and the replay base. Not saved: where people stand on the map, the open sheet, pending events and the away card; the camera is in the settings file (ADR 0041) |
| Round trip | `Load(Save(state))` has the same `ComputeHash()`, and both continue identically: every later command gives the same state and the same events |
| References | A state field that points into content (a crop, a good, a building, a trait) is written as the definition's **id string** and resolved on load, so a build that reorders or adds content still loads older saves. An unknown id fails the load with `UnknownContentId` (a migration must map it) |
| Versions | `formatVersion` is the container (1). `schemaVersion` starts at 1 and rises whenever the state's fields change; the reader handles every schema from `SaveDef.minSchema` up, filling new fields with their documented defaults (migration in code, one step per version, each with a test). A newer schema than the build knows is refused with `SaveFromNewerVersion`. **Until the Phase 1 playtest build (Sprint 9)** a state change raises `schemaVersion` and `minSchema` together: an older save is refused with `SaveTooOld`, kept as `town.unreadable-<date>.ftc` (never deleted, exportable) and a new town starts with a notice; from the playtest build every state change ships a migration with a test (ADR 0041) |
| Header extras | The build (`simVersion`) and a content fingerprint (FNV-1a of the content files), so a save's command log is replayed only on matching content |
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
| Export | Writes a copy of the latest save as `FarmToCity-<yyyy-MM-dd-HHmm>.ftc` (towns have no names yet, ADR 0041) into the app's Documents folder, which iOS shows in the Files app (`UIFileSharingEnabled`, `LSSupportsOpeningDocumentsInPlace`, set by our build step); no plugin needed (ADR 0007). From Files the player can AirDrop, email or message it. A one-tap share sheet waits for store readiness (F19.5) |
| Import | Settings → *Import a town* lists every `.ftc` in the Documents folder (file name, game date, cash, saved at) with an *Import* button; a file that can't load shows why on its row. No pop-up at launch (ADR 0041). It must load (checksum, version, content ids); importing replaces the current town after a confirmation that offers to export the current one first (ADR 0023). An old export can roll back progress: accepted, because it's a deliberate manual act |
| New game | Replaces the current town after the same confirmation and export offer. The backups of the old town are deleted with it (one town at a time) |

### 3.5 Local notifications

```
plan = NotificationPlanner.Plan(sim, settings, deviceUtcOffset)   pure, in Sim.Core; the platform schedules what it returns
```

| Rule | Behaviour |
| --- | --- |
| Opt-in | Off until the player agrees. The game's own prompt first appears in the first session, in the letter's `comeBack` chapter right after the long crop is sown ("Your potato will be ready at 14:05 tomorrow. Want a nudge when it is?"; ADR 0036, spec 009); iOS's one-time permission dialog follows only if they say yes. If they decline, it asks once more after the first season close, then never; Settings always has the switch. **Until the letter exists (Sprint 8)** the first ask is a plain card the first time a field crop of at least a day is sown (ADR 0041) |
| Sources | **A simulated absence** (ADR 0041): the planner runs the away advance on a copy of the state up to the away cap (`Sync` rules, spec 001 §3.4) and reads the events it emits, with their sim times. Deterministic, so they are exactly what happens if the player stays away; about 3 ms for 72 h on the iPhone 13. The copy is thrown away; the real state is never touched |
| Kinds and importance | **Urgent:** a loan payment missed or its final notice (`LoanPaymentMissed`, `FinalNotice`), a contract lapsed (`ContractLapsed`). **Needs you:** a field crop, garden bed or ripe fruit ready that no standing order covers (`CropReady`, the garden job, `TreeEvent` ripe); the hand's hut built (`HandHutBuilt`). **Information:** an order's harvest or pick (`OrderExecuted`) |
| When | The platform asks for a plan when the app goes to the background and cancels every pending notification when it returns. Each item's wall time is `lastWallUtc + (due − simTime)` (spec 001) |
| Grouping | Items of the same kind within `NotifyDef.groupWindowMinutes` (30) are one notification ("3 fields of wheat are ready"); information items group within `NotifyDef.infoGroupMinutes` (240), at most one hands' note per 4 hours |
| Quiet hours | Nothing between `quietFrom` and `quietTo` (22:00–07:00 device time): an item due then moves to `quietTo` and groups with others there |
| Cap | **No daily cap** (owner, ADR 0041). iOS keeps at most 64 pending local notifications per app: the plan keeps the first `NotifyDef.maxPending` (64) by importance, then time |
| Text | From the string table (spec 008): a kind key, a count and, for crops and fruit, the crop's name ("2 fields of wheat are ready"); never a money amount |
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
| `NotifyDef` | `maxPending` | int | 64 | 1–64 (the iOS limit) |
| | `groupWindowMinutes` | int | 30 | 0–240 |
| | `infoGroupMinutes` | int | 240 | 0–1,440 |
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
| Turn notifications on | An iOS permission | A nudge when something is ready or needs you (no daily cap, never at night) | Returning sooner keeps fields busy | Turn off any time |
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
| Notifications: on   (never at night)      |   | may be missing.               [OK]       |
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
| AC-3 | A save written at schema 1 loads in a build at schema 2 with the new field's default; a save from a newer schema is refused with `SaveFromNewerVersion`; a save below `minSchema` is refused with `SaveTooOld` and the store keeps it as `town.unreadable-<date>.ftc` (ADR 0041) | `SaveTests.Schema_MigrateAndRefuseNewer` |
| AC-4 | A save loads correctly with content whose goods, crops and buildings are listed in a different order; a save naming an id the content lacks fails with `UnknownContentId` | `SaveTests.ContentIds_NotIndices` |
| AC-5 | 3,600 one-second live Syncs leave one `LiveRun` entry; replaying the compacted log gives exactly the hash of the uncompacted run; a sow, an away Sync and a backwards clock each break the run | `SaveTests.SyncCompaction` |
| AC-6 | After 10 game years of bot play (spec 003 `FarmBot`) with per-second Syncs, the save is under 200 KB and the log under `maxLogEntries`; with a small cap in test content, a save at the cap checkpoints (replay base = the state, the log empty), the town's hash is unchanged, and replaying base + log reproduces the latest hash | `SaveTests.Size_AndCheckpoint` |
| AC-7 | On an in-memory store, a crash injected at each step of an atomic write (after writing the temp file, before the rename) leaves the previous save loadable | `SavePointTests.AtomicWrite_CrashSafe` |
| AC-8 | Over 5 simulated real days of hourly saves, exactly 1 latest, 3 hourly and 3 daily files exist with the right ages; with the latest corrupted, `LoadNewestValid` returns the newest valid backup and says which | `SavePointTests.Rotation_AndRecovery` |
| AC-9 | Save triggers: a burst of 10 commands within 5 seconds causes one save; live Syncs alone cause none until the hourly save | `SavePointTests.Debounce` |
| AC-10 | The planner returns only items within the away cap, grouped within 30 minutes (information within 240), none in quiet hours (moved to 07:00), at most `maxPending` ranked urgent, needs you, information, and nothing when notifications are off; a field with a standing order gives no "ready" item | `NotificationTests.Plan_GroupQuietRank` |
| AC-13 | The planner's items match the real away advance: for a seeded town with a hand, orders and a loan, every planned item due by time T is among the events of an actual away advance to T, at the same sim time, and the real state is unchanged by planning (ADR 0041) | `NotificationTests.Plan_MatchesRealAbsence` |
| AC-11 | The play log writes session and milestone lines once each per town, nothing when it is off, and stays under its byte cap | `PlayLogTests.Entries_OnceAndCapped` |
| AC-12 | Performance (CI, Release): writing and reading the 10-year save takes under 50 ms each | `PerfTests.Save_WriteRead` (category `Perf`) |

The atomic rename, `OnApplicationPause` and iOS notifications are checked on the device with the Sprint 6 test checklist (`docs/test-checklists/`), since `Game.Services` is Unity code.

## 9. Balancing hooks

| Metric | Expected | Tolerance |
| --- | --- | --- |
| Progress lost to a crash | At most the play since the last save: a few seconds after a command, at most an hour of idle live play | — |
| Notifications a day | Most days 1–4 (crops finishing, a hands' note); no cap (ADR 0041) | Shape |
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

### Sprint 6 planning review (2026-10-19, ADR 0041)

1. **What a save holds:** everything the state hash covers (people, orders, the 2-acre yard and the quarters included) plus the command log; one write path for the hash and the save; content by id. People's map positions, the open sheet and the away card are not saved; the camera is in the settings file (§3.1).
2. **Old saves:** until the Sprint 9 playtest build a state change refuses older saves (`SaveTooOld`), keeps the file as `town.unreadable-<date>.ftc` and starts a new town with a notice; migrations with tests from the playtest build on (§3.1, AC-3).
3. **Notifications from a simulated absence,** ranked urgent, needs you, information; **no daily cap** (owner); quiet hours; information grouped per 4 hours; the iOS 64-pending limit; the crop named in the text (§3.5, AC-10, AC-13).
4. **Settings in Sprint 6:** a gear button under the Store chip and a full-height Settings sheet; the Menu entry comes in Sprint 7 (spec 008 §10).
5. **Import** from a list in Settings, no launch pop-up; exports named by date (§3.4).
6. **The first ask** is a plain card the first time a field crop of at least a day is sown, until the letter (Sprint 8) takes it over; the second ask after the first season close (§3.5).
7. The notifications package is `com.unity.mobile.notifications` 2.5.0 (bundled with Unity 6000.3.26f1).

### Sprint 6 implementation notes (2026-10-20)

1. **Migration tests wait for Sprint 9.** Until the playtest build, `schemaVersion` and `minSchema` rise together, so AC-3 tests the refusals (`SaveTooOld`, `SaveFromNewerVersion`) and the kept `town.unreadable-<local date>.ftc`. The "schema 1 loads at schema 2" part gets its test with the first real migration.
2. **The settings file is `PlayerPrefs`** (iOS's own preferences file): `notify.on`, `notify.asked` (0–2), `playlog.on` and the camera (`camera.x`, `camera.y`, `camera.h`). None of it is in the town or its hash.
3. **Saves live in iOS `Library/Saves`.** On iOS, `Application.persistentDataPath` is Documents, which the Files app shows, so the town is kept elsewhere: the player can't move or delete `town.ftc`, its backups or `playlog.jsonl` by accident. Documents holds only exports and files to import.
4. **The unreadable copy is also copied to Documents,** so the player can export it from Files and the developer can read it. The original stays in `Library/Saves`.
5. **AC-6 uses one-minute live Syncs,** not one-second ones, to keep the 10-year test under a few seconds. Live runs compact them the same way, and AC-5 covers the one-second case. Measured: a 10-year save is 116 KB, writes in 12 ms and reads in 3 ms.
6. **The content fingerprint** in the header is FNV-1a over the content files. It is recorded only: a load never checks it, because content is referenced by id. When a bug report replays a save's log, compare it by hand.

## 11. Cut first

1. Information notifications (keep urgent and needs-you items).
2. The command-log checkpoint (only matters after months of play).
3. Daily backups (keep hourly ones).
4. Import (keep export, which also serves bug reports).
5. Grouping (one notification per item, still within the 64-pending limit).
