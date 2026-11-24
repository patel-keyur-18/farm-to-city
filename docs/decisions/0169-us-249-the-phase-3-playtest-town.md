# 0169. US-249 the Phase 3 playtest town: the Village bot to the close after the Village unlock

- Status: Accepted (the owner chose the option, 2026-11-24, gap 14)
- Date: 2026-11-24
- Deciders: Keyur Patel
- Related ADRs: 0047, 0101, 0107, 0166, 0168

## Context

Phase 3's playtest (US-250) is four weeks of the owner's play on a Release build. Phase 2's playtest town stopped at the Hamlet's third household (day 14). For Phase 3 the owner needs to reach the Township and live in it.

## Options

1. The Village bot (credit, homes first) plays to the close after the Village unlock (chosen).
2. A day-14 Hamlet town, as for Phase 2: more weeks spent before the Township, less time living in it.
3. A Township just reached: skips the Village-to-Township gate, which the playtest exists to check.

## Decision

`dotnet run --project tools/Tools.Balancer -- playtest-save <seed> <path> <commit> village` plays the Village bot to the first close after the Village era begins and writes the save, stamped with the wall time it is written (so it is imported the same day). Without the last argument it writes the Phase 2 town as before. Seed 1 gives day 44 (year 3, Winter), 6 households in your houses, one business with 7 profitable seasons, the school open, net worth 17,40,598 and debt to assets 8%. The town is shown to the owner in the Editor before the Release build is made from the final commit.

## Consequences

- `PlaytestTownTests.VillageTown_ImportsAndPlays` reads the village town back and plays a season on.
- The playtest town is a file, not a save schema: it is read by the build's own loader.
- The Release build carries no developer panel; the owner reaches the Township by play.
