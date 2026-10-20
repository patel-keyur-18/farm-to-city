# content/

Balancing data for the simulation, as JSON (ADR 0018, 0020). Every number the game is tuned with lives here, never in code.

- Files are loaded by `Sim.Content` from JSON **text**; the host decides where the text comes from.
- Decimal numbers are read as `decimal` and converted to `Fixed` exactly; they never pass through `double`.
- Each file is validated on load. Errors name the field path, for example `clock.weatherSpellHours`.
- The spec that owns each file lists its fields, defaults and bounds.

| File | Spec | Contents |
| --- | --- | --- |
| `clock.json` | 001 | `ClockDef`: season length, weather spell length, life clock, away cap and threshold, forecast |
| `weather.json` | 001 | `WeatherTypeDef` list and one `ClimateDef` per season (weights, persistence) |
| `land.json` | 002 | `SoilDef` and `DrainageDef` lists, `LandDef` (moisture by weather, fertility curve, fallow, prices, tree slots, shade) and the well's moisture floor |
| `map.json` | 002 | `MapDef`: the hand-authored homestead grid (tiles in id order) and its `ParcelDef`s |
| `buildings.json` | 002, 005 | `BuildingDef`: yard tree slots, opening value, useful life |
| `start.json` | 002, 005 | `StartDef`: the inherited parcel, the buildings placed on it, starting cash |
| `finance.json` | 005 | `LedgerDef` (entry retention); `CreditScoreDef` joins in Sprint 4 |
| `saves.json` | 007 | `SaveDef` (backups, autosave, debounce, log cap, `minSchema`), `NotifyDef` (pending limit, grouping, quiet hours), `PlayLogDef`; none of it changes the town |

Money amounts (`acreBasePrice`, `value`, `startingCash`) are in coins with at most 2 decimal places, and are stored as 1/100 coin.
