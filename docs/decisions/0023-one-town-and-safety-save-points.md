# 0023. One town at a time; save points are a safety guard, never an undo

- Status: Accepted
- Date: 2026-10-17
- Deciders: Keyur Patel
- Related: ADR 0002 (real-time clock), ADR 0009 (mistakes are recoverable), ADR 0017 (dev time-travel panel)
- GDD sections: Offline play and saves (save system), UI/UX (settings and saves)

## Context

The GDD planned 3 rotating autosaves plus 5 manual save slots. In a real-time game, loadable save points work as an undo button. A player could load a save from before a bad loan or a pest outbreak, which cancels the core lesson of recovering from mistakes. Loading an old save would also trigger confusing catch-up from that point.

## Options

1. Up to 3 separate towns, each continuous with no rewind.
2. One town at a time, with save points kept only for crash recovery.
3. Keep loadable save points (an undo).

## Decision

Option 2.

- **One town at a time.** Starting a new game replaces the current town after a confirmation, and the game offers to export the old town first.
- **Automatic save points are a safety guard.**
  - Saving happens continuously: every real hour, after every command batch, and when the app goes to the background.
  - The game keeps the latest save plus rotating backups: the last 3 hourly saves, plus one per day for the last 3 days. The counts live in data.
  - A **Save now** button saves immediately and never rewinds.
  - If the app crashes, or the latest save fails its checksum, the game automatically loads the newest valid save point and tells the player which one it used.
  - Players can never choose to load an older save point. There is no undo.
- **Export and import** move a town between devices. Importing replaces the current town after a confirmation. An old export could be used to roll back, which is accepted because it's a deliberate manual act.
- **Development builds only:** snapshot and restore in the time-travel panel, for testing.

## Consequences

- The save spec (007) defines the backup rotation, checksum verification, automatic recovery, and the recovery notice.
- The settings screen shows "Save now", "Export town", "Import town" and "New game". It has no slot list.
