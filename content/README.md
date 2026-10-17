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
