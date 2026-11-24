# 0170. The game's time of day matches the device's

- Status: Accepted (the owner asked for it from the Sprint 26 Development build, 2026-11-24)
- Date: 2026-11-24
- Deciders: Keyur Patel
- Related ADRs: 0002, 0023, 0169

## Context

On the phone the game showed 16:48 when the device said 17:53. Sim time runs one second for each wall second, so the only way to differ is to start differently: a town made by a bot (the playtest save) or restored from a snapshot starts at an arbitrary time of day, and nothing ever aligned it. Reading the owner's Editor town and my own bot towns confirmed the gap (3 to 6 hours in them; 65 minutes on the phone).

## Options

1. Move the sim forward when it is behind and wait when it is ahead, at load (chosen).
2. Show the device's time on the HUD and leave the sim: the day and night, shop hours and closes would still run on the other clock.
3. Rewrite the sim time: breaks "time never goes backwards" and would lose or repeat what happened.

## Decision

`ClockAlign.Offset` (Sim.Core, pure) gives the nearest way to match, within 30 s counting as matched. At a load, an import or a developer snapshot restore, before the first `Sync`, the platform layer measures it at the sim's last wall anchor: behind, an away advance by the offset (at most 12 h, after a save); ahead, the sim waits (no `Sync` until the anchor plus the offset; the saved anchor is not changed). The playtest tool also matches the town to the machine's time of day when it writes the file. Spec 001 §3.4 is amended.

## Consequences

- No data is lost: a forward move is the same as being away that long (the 72 h cap is far larger), and a wait only pauses.
- A town that the owner's device already has, behind by 65 minutes, is moved forward 65 minutes when this build loads it, and a toast says so.
- Developer time travel by an hour is matched away at the next load.
- `ClockAlignTests`, and `PlaytestTownTests` check that the written town matches the machine's time of day. Editor check by scripted touch: a town 65 minutes behind caught up at load, a town 3 minutes ahead waited 3 minutes and then ran, an old town that had been ahead 5 h 40 min came out matched after its away advance.
- The build tool: `Game.Editor.BuildCli.StartIosDevelopment` and `StartIosRelease` start a build on the Editor's next update, so the pipeline's eval returns at once. The two console errors of earlier builds were that eval's 5 s timeout, counted in the build's own error total; the build still succeeded. `GameRoot.Update` also returns while the town is not loaded (a domain reload in play mode nulled it).
