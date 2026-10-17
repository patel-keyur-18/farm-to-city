# Farm to City (working title)

[![CI](https://github.com/patel-keyur-18/farm-to-city/actions/workflows/ci.yml/badge.svg)](https://github.com/patel-keyur-18/farm-to-city/actions/workflows/ci.yml)

An offline life-and-business simulation for iOS and Android. Start with two acres
and a well; grow a hamlet, a village, a township and finally a city — by farming,
founding businesses, hiring and training people, and investing in your town.

Every number in the game can be explained, and every decision has a cost, a delay
and a consequence. Players come away understanding margins, cash flow, assets and
how to lead people.

## Status
Pre-production (Phase 0). Spec-driven: no feature is built before its spec is approved.
See [docs/planning/STATUS.md](docs/planning/STATUS.md) for where the work stands.

## Build and test
Requires the .NET 10 SDK.

```
dotnet build FarmToCity.sln
dotnet test
dotnet run --project tools/Tools.Balancer
```

## Docs
- [Game Design Spec](docs/GDD.md): the master spec
- [Feature specs](docs/specs/)
- [Architecture decisions](docs/decisions/)
- [Planning: status, epics, features, sprints](docs/planning/)
- [Instructions for Claude Code sessions](CLAUDE.md)

## Tech
Unity (latest LTS, 2D URP) · C# · pure-C# deterministic simulation core · Git LFS for assets

## License
© 2026 Keyur Patel. All rights reserved.
