# Epics

> All epics for all phases. Each feature has its own file in `features/` with its user stories and tasks. Sprint plans are in `sprints.md`. Rolling wave (ADR 0019): Phases 0–1 are broken down to tasks; later phases have stories, and their tasks are added when each phase's specs are written.

## Phase map

| Phase | Name | Epics | Exit gate |
| --- | --- | --- | --- |
| 0 | Pre-production | E00, E20 (start) | Economy model has no money loops; 2,000 agents at 60 fps (half frame budget on iPhone 13) |
| 1 | Vertical slice: Homestead to Hamlet unlock | E01–E10 | Part A (owner's solo week, ADR 0047): a reason to return each day, the Hamlet on day 5–9, no blocker, RC and CI pass. Part B (US-185, before Launch): fresh players back on day 2 and day 3 unprompted |
| 2 | Hamlet and Village | E11–E14, plus F02.4, F02.5 and F04.3 (the owner's enhancements, ADR 0052–0057, 0084) | Part A (owner's playtest from a day-14 Hamlet town, ADR 0101): the bots' Village pace, the Village within 3 weeks, a reason to return each day, no blocker, profit versus cash explained from the screens, build health. Part B (before Launch): fresh players explain profit versus cash |
| 3 | Township | E15–E16, plus F12.5–F12.7, US-103 and F10.3 (ADR 0107) | Part A (the owner's 4 weeks from a town just past the Village, ADR 0107 §11): the bots reach the Township in 9–11 weeks using credit, the owner within 4 weeks of the saved town, a reason to return each day, no blocker, public versus personal money explained from the screens, build health, no exploits (ADR 0016's rule and Phase 3's checks). Part B (before Launch): fresh players explain public versus personal money |
| 4 | Town and City | E17–E18 | 20,000 population at 60 fps on the reference phone |
| 5 | Polish | E19 | Owner's call |

## Epics

| Epic | Title | Phase | Goal | Features | Points |
| --- | --- | --- | --- | --- | --- |
| E00 | Delivery foundations | 0 | Everything needed before Phase 1 coding: build and CI, the Phase 1 specs, the economy model, the Unity project, the performance spike and the art style check. | [F00.1](features/F00.1-repository-build-and-ci.md), [F00.2](features/F00.2-phase-1-feature-specs.md), [F00.3](features/F00.3-economy-model.md), [F00.4](features/F00.4-unity-project-and-bootstrap.md), [F00.5](features/F00.5-performance-spike.md), [F00.6](features/F00.6-art-style-check.md) | 63 |
| E01 | Time and world | 1 | The real-time clock, calendar, scheduler, weather, forecast, away rules and developer time travel (spec 001). | [F01.1](features/F01.1-sim-foundations.md), [F01.2](features/F01.2-calendar-and-scheduler.md), [F01.3](features/F01.3-weather-and-forecast.md), [F01.4](features/F01.4-live-and-away-time.md), [F01.5](features/F01.5-developer-time-travel-sim.md) | 31 |
| E02 | Land and farming | 1 | Land and soil, crops with seasonal sowing windows, the yield formula, inputs, pests, the kitchen garden and fruit trees. | [F02.1](features/F02.1-land-and-soil.md), [F02.2](features/F02.2-crops-and-yield.md), [F02.3](features/F02.3-fruit-trees.md), [F02.4](features/F02.4-village-map.md), [F02.5](features/F02.5-crop-ladder.md) (Phase 2) | 55 |
| E03 | Founder household | 1 | The founder as the first worker: labour, diet, rest, health and day labour (ADR 0010). | [F03.1](features/F03.1-founder-labour-and-routine.md) | 11 |
| E04 | Market and storage | 1 | Tradeable goods, the hourly market, the regional trader, the mandi trip, buying inputs, the hut store and spoilage. | [F04.1](features/F04.1-goods-market-and-regional-trader.md), [F04.2](features/F04.2-hut-store-and-spoilage.md), [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) (Phase 2) | 38 |
| E05 | Money and credit | 1 | The ledger with accrual accounting, seasonal P&L, net worth, lenders, credit score and foreclosure. | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md), [F05.2](features/F05.2-credit-and-foreclosure.md) | 23 |
| E06 | People | 1 | Day labourers from neighbouring villages, the first resident hand, season contracts and standing orders (ADR 0022). | [F06.1](features/F06.1-day-labourers.md), [F06.2](features/F06.2-resident-farm-hand.md), [F06.3](features/F06.3-standing-orders.md) | 32 |
| E07 | Saves and platform services | 1 | Safety save points, export and import, local notifications and the local play log. | [F07.1](features/F07.1-save-system.md), [F07.2](features/F07.2-local-notifications.md), [F07.3](features/F07.3-local-play-log.md) | 16 |
| E08 | Presentation | 1 | The code-first Unity layer: map, field interaction, HUD, panels with Why?, localisation groundwork and the developer panel. | [F08.1](features/F08.1-map-and-field-interaction.md), [F08.2](features/F08.2-hud-and-panels.md), [F08.3](features/F08.3-localisation-and-formatting.md), [F08.5](features/F08.5-basic-feedback-sounds.md), [F08.4](features/F08.4-developer-panel-unity.md), [F08.6](features/F08.6-living-weather.md) (Phase 2, pending) | 42 |
| E09 | Onboarding and progression | 1 | The first session, the grandparent's letter chapters across the first week (ADR 0015), and the Hamlet unlock that ends Phase 1. | [F09.1](features/F09.1-first-session.md), [F09.2](features/F09.2-letter-chapters.md), [F09.3](features/F09.3-hamlet-unlock.md), [F09.4](features/F09.4-era-unlocks-village-to-city.md) | 31 |
| E10 | Playtests and tuning | 1–3 | Release candidate, testers, play logs, tuning, and the Phase 1, 2 and 3 exit gates. | [F10.1](features/F10.1-playtest-and-tuning.md), [F10.2](features/F10.2-phase-2-playtest-and-gate.md) (Phase 2), [F10.3](features/F10.3-phase-3-playtest-and-gate.md) (Phase 3) | 30 |
| E11 | Citizens and households | 2 | Citizens with skills, traits and life stages; households, needs and happiness; births, migration and family trees; full hiring. | [F11.1](features/F11.1-citizens-and-life-clock.md), [F11.2](features/F11.2-households-needs-and-happiness.md), [F11.3](features/F11.3-population-flows-and-family-trees.md), [F11.4](features/F11.4-full-hiring-and-hr.md) | 85 |
| E12 | Businesses | 2–3 | Founding, running and failing businesses with their own books; production chains; services and customer choice; rivals. | [F12.1](features/F12.1-business-founding-and-books.md), [F12.2](features/F12.2-production-chains.md), [F12.3](features/F12.3-services-and-customer-choice.md), [F12.4](features/F12.4-rivals-and-competition.md), [F12.5](features/F12.5-invoices-and-payment-terms.md), [F12.6](features/F12.6-managers-valuation-and-exit.md), [F12.7](features/F12.7-township-businesses.md) (Phase 3) | 88 |
| E13 | Farm expansion | 2 | Livestock, the irrigation and equipment ladders, whole-acre orchards and the granary. | [F13.1](features/F13.1-livestock.md), [F13.2](features/F13.2-irrigation-and-equipment.md), [F13.3](features/F13.3-orchards-and-granary.md) | 74 |
| E14 | Education and learning layer | 2 | The founder-funded school, literacy, the Codex, advisors, the full financial statements and the personal finance hub. | [F14.1](features/F14.1-school-and-literacy.md), [F14.2](features/F14.2-codex-and-advisors.md), [F14.4](features/F14.4-personal-finance-hub-and-milestones.md), [F14.3](features/F14.3-financial-statements.md) | 66 |
| E15 | Town government | 3 | The treasury, budget, taxes, policies, zoning, land value, utilities, construction, roads, map expansion, civic services, petitions and building upgrades. | [F15.1](features/F15.1-treasury-and-budget.md), [F15.2](features/F15.2-taxes-and-policies.md), [F15.3](features/F15.3-zoning-and-land-value.md), [F15.4](features/F15.4-utilities-and-construction.md), [F15.5](features/F15.5-roads-highway-and-hills.md), [F15.6](features/F15.6-civic-services-and-coverage.md), [F15.7](features/F15.7-petitions-reputation-crime-and-inequality.md), [F15.8](features/F15.8-building-upgrades-and-overlays.md) | 94 |
| E16 | Events and history | 3 | The event engine, insurance, forward contracts, the Chronicle and the Town Almanac. | [F16.1](features/F16.1-event-engine.md), [F16.2](features/F16.2-insurance-and-forward-contracts.md), [F16.3](features/F16.3-chronicle-and-almanac.md), [F16.4](features/F16.4-citizen-stories.md) | 29 |
| E17 | Town and city scale | 4 | Cohort simulation for 20,000 people, transport and logistics, industry and pollution. | [F17.1](features/F17.1-cohort-simulation.md), [F17.2](features/F17.2-transport-and-logistics.md), [F17.3](features/F17.3-industry-and-pollution.md) | 18 |
| E18 | Wealth and politics | 4 | Investments, elections with council mood, town identity, districts, public debt and trade with other cities. | [F18.1](features/F18.1-investments.md), [F18.2](features/F18.2-elections-and-council-mood.md), [F18.3](features/F18.3-town-identity-and-districts.md), [F18.4](features/F18.4-public-debt-and-conflicts-of-interest.md), [F18.5](features/F18.5-trade-with-other-cities.md) | 24 |
| E19 | Polish and release readiness | 5 | Hindi, accessibility, the Blender art pipeline, scenarios and difficulty modes; store readiness if the owner decides. | [F19.1](features/F19.1-hindi-localisation.md), [F19.2](features/F19.2-accessibility.md), [F19.3](features/F19.3-art-pipeline.md), [F19.6](features/F19.6-audio-and-music.md), [F19.4](features/F19.4-scenarios-and-difficulty.md), [F19.5](features/F19.5-store-readiness-deferred.md) | 48 |
| E20 | Balancing and quality | 0–5 | The headless balancer, bots and health checks that gate every phase (ADR 0016). | [F20.1](features/F20.1-balancer-and-health-checks.md) | 22 |

## Features

| Feature | Title | Epic | Phase | Spec | Status | Stories | Points |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [F00.1](features/F00.1-repository-build-and-ci.md) | Repository, build and CI | E00 | 0 | ADR 0018, 0020, 0026 | Done | US-001–US-004 | 7 |
| [F00.2](features/F00.2-phase-1-feature-specs.md) | Phase 1 feature specs | E00 | 0 | GDD, ADR 0019 | Done | US-005–US-012 | 24 |
| [F00.3](features/F00.3-economy-model.md) | Economy model | E00 | 0 | GDD Balancing, ADR 0008, 0016 | Done | US-013–US-016 | 14 |
| [F00.4](features/F00.4-unity-project-and-bootstrap.md) | Unity project and bootstrap | E00 | 0 | ADR 0017, 0018, 0026 | Done | US-017–US-020 | 7 |
| [F00.5](features/F00.5-performance-spike.md) | Performance spike | E00 | 0 | GDD performance budget, ADR 0026 | Done | US-021–US-023 | 9 |
| [F00.6](features/F00.6-art-style-check.md) | Art style check | E00 | 0 | ADR 0007, 0017 | Done | US-024–US-024 | 2 |
| [F01.1](features/F01.1-sim-foundations.md) | Sim foundations | E01 | 1 | Spec 001 §3.1 | Done | US-025–US-028 | 9 |
| [F01.2](features/F01.2-calendar-and-scheduler.md) | Calendar and scheduler | E01 | 1 | Spec 001 §3.2–3.3 | Done | US-029–US-031 | 10 |
| [F01.3](features/F01.3-weather-and-forecast.md) | Weather and forecast | E01 | 1 | Spec 001 §3.5–3.6 | Done | US-032–US-033, US-184 | 10 |
| [F01.4](features/F01.4-live-and-away-time.md) | Live and away time | E01 | 1 | Spec 001 §3.4, §3.7 | Done | US-034–US-035 | 4 |
| [F01.5](features/F01.5-developer-time-travel-sim.md) | Developer time travel (sim) | E01 | 1 | Spec 001 §3.8 | Done | US-036–US-037 | 2 |
| [F02.1](features/F02.1-land-and-soil.md) | Land and soil | E02 | 1 | [Spec 002](../specs/002-land-and-soil.md) | Done | US-038–US-041 | 10 |
| [F02.2](features/F02.2-crops-and-yield.md) | Crops and yield | E02 | 1 | [Spec 003](../specs/003-crops-trees-and-founder-labour.md) (approved) | Done | US-042–US-046, US-177 | 20 |
| [F02.3](features/F02.3-fruit-trees.md) | Fruit trees | E02 | 1 | [Spec 003](../specs/003-crops-trees-and-founder-labour.md) (approved) | Done | US-047–US-049 | 7 |
| [F02.4](features/F02.4-village-map.md) | The village map | E02 | 2 | [Spec 010](../specs/010-map-village-and-edge.md) (approved) | Done | US-188–US-191 | 18 |
| [F02.5](features/F02.5-crop-ladder.md) | The crop ladder | E02 | 2 | [Spec 020](../specs/020-crop-ladder.md) (approved) | Done | US-224–US-232 | 27 |
| [F03.1](features/F03.1-founder-labour-and-routine.md) | Founder labour and routine | E03 | 1 | [Spec 003](../specs/003-crops-trees-and-founder-labour.md) (approved) | Done | US-050–US-053 | 11 |
| [F04.1](features/F04.1-goods-market-and-regional-trader.md) | Goods market and regional trader | E04 | 1 | [Spec 004](../specs/004-goods-market-and-storage.md) (approved) | Done | US-054–US-056, US-178–US-181 | 17 |
| [F04.2](features/F04.2-hut-store-and-spoilage.md) | Hut store and spoilage | E04 | 1 | [Spec 004](../specs/004-goods-market-and-storage.md) (approved) | Done | US-057–US-058 | 4 |
| [F04.3](features/F04.3-trader-truck-and-supermarket-orders.md) | The trader's truck and supermarket orders | E04 | 2 | [Spec 011](../specs/011-trader-truck-and-supermarket-orders.md) (approved) | Done | US-192–US-195 | 17 |
| [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | Ledger and seasonal P&L | E05 | 1 | [Spec 005](../specs/005-ledger-and-credit.md) | Done | US-059–US-062 | 14 |
| [F05.2](features/F05.2-credit-and-foreclosure.md) | Credit and foreclosure | E05 | 1 | [Spec 005](../specs/005-ledger-and-credit.md) | Done | US-063–US-065 | 9 |
| [F06.1](features/F06.1-day-labourers.md) | Day labourers | E06 | 1 | [Spec 006](../specs/006-people.md) (approved) | Done | US-066–US-067 | 5 |
| [F06.2](features/F06.2-resident-farm-hand.md) | Resident farm hand | E06 | 1 | [Spec 006](../specs/006-people.md) (approved) | Done | US-068–US-070 | 9 |
| [F06.3](features/F06.3-standing-orders.md) | Standing orders | E06 | 1 | [Spec 006](../specs/006-people.md) (approved); US-241 ADR 0107 §10 | Done; US-241 Phase 3 (S20) | US-071, US-072, US-186, US-198, US-241 | 15 |
| [F07.1](features/F07.1-save-system.md) | Save system | E07 | 1 | [Spec 007](../specs/007-saves-notifications-and-play-log.md) (approved) | Done | US-073–US-075 | 11 |
| [F07.2](features/F07.2-local-notifications.md) | Local notifications | E07 | 1 | [Spec 007](../specs/007-saves-notifications-and-play-log.md) (approved) | Done | US-076–US-076 | 3 |
| [F07.3](features/F07.3-local-play-log.md) | Local play log | E07 | 1 | [Spec 007](../specs/007-saves-notifications-and-play-log.md) (approved) | Done | US-077–US-077 | 2 |
| [F08.1](features/F08.1-map-and-field-interaction.md) | Map and field interaction | E08 | 1 | Spec 008 (approved) | Done | US-078–US-079 | 8 |
| [F08.2](features/F08.2-hud-and-panels.md) | HUD and panels | E08 | 1 | Spec 008 (approved) | Done | US-080–US-084, US-182 | 21 |
| [F08.3](features/F08.3-localisation-and-formatting.md) | Localisation and formatting | E08 | 1 | Spec 008 (approved) | Done | US-085–US-086 | 4 |
| [F08.5](features/F08.5-basic-feedback-sounds.md) | Basic feedback sounds | E08 | 1 | Spec 008 (approved), ADR 0007 | Done | US-087 | 2 |
| [F08.4](features/F08.4-developer-panel-unity.md) | Developer panel (Unity) | E08 | 1 | Spec 001 §3.8, ADR 0023 | Done | US-088–US-088 | 2 |
| [F09.1](features/F09.1-first-session.md) | First session | E09 | 1 | Spec 009 (approved) | Done | US-089–US-090 | 7 |
| [F09.2](features/F09.2-letter-chapters.md) | Letter chapters | E09 | 1 | Spec 009 (approved) | Done | US-091–US-092 | 6 |
| [F09.3](features/F09.3-hamlet-unlock.md) | Hamlet unlock | E09 | 1 | Spec 009 (approved), GDD eras | Done | US-093–US-094 | 5 |
| [F10.1](features/F10.1-playtest-and-tuning.md) | Playtest and tuning | E10 | 1 | GDD Phase 1 gate, ADR 0047 | Done (Sprint 9; US-185 before Launch) | US-095–US-097, US-185 | 12 |
| [F10.2](features/F10.2-phase-2-playtest-and-gate.md) | Phase 2 playtest and gate | E10 | 2 | GDD Phase 2 gate, ADR 0101, ADR 0106 | Sprints 18–19 (closed 2026-11-13; A2–A5 in parallel) | US-237–US-239 | 9 |
| [F10.3](features/F10.3-phase-3-playtest-and-gate.md) | Phase 3 playtest and gate | E10 | 3 | GDD Phase 3 gate, ADR 0107 §11 | Backlog | US-249–US-251 | 9 |
| [F20.1](features/F20.1-balancer-and-health-checks.md) | Balancer and health checks | E20 | 0–5 | GDD Balancing, ADR 0016, 0045, 0107 §11–§12 | In progress (US-098 done; the rest in Phase 3, ADR 0107) | US-098, US-099, US-100, US-101, US-248 | 22 |
| [F09.4](features/F09.4-era-unlocks-village-to-city.md) | Era unlocks: Village to City | E09 | 2–4 | Village: spec 018 (approved, ADR 0077); Township: spec 023 (to write, ADR 0107); Town and City in Phase 4 | In progress (US-102 done; US-103 Phase 3, US-104 Phase 4) | US-102, US-103, US-104 | 13 |
| [F11.1](features/F11.1-citizens-and-life-clock.md) | Citizens and life clock | E11 | 2 | Spec 014 (approved, ADR 0063) | Done | US-105, US-106, US-208, US-212 | 18 |
| [F11.2](features/F11.2-households-needs-and-happiness.md) | Households, needs and happiness | E11 | 2 | Spec 014 (approved, ADR 0063); US-240 ADR 0107 §8 | Done; US-240 Phase 3 (S20) | US-107, US-108, US-209, US-210, US-217, US-240 | 27 |
| [F11.3](features/F11.3-population-flows-and-family-trees.md) | Population flows and family trees | E11 | 2 | Spec 014 (approved, ADR 0063) | Done | US-109–US-111, US-211 | 19 |
| [F11.4](features/F11.4-full-hiring-and-hr.md) | Full hiring and HR | E11 | 2 | Spec 015 (approved, ADR 0069) | Done | US-112–US-114 | 13 |
| [F12.1](features/F12.1-business-founding-and-books.md) | Business founding and books | E12 | 2 | Spec 016 (approved, ADR 0068) | Done | US-115–US-117 | 16 |
| [F12.2](features/F12.2-production-chains.md) | Production chains | E12 | 2 | Spec 016 (approved, ADR 0068) | Done | US-118, US-119, US-216 | 11 |
| [F12.3](features/F12.3-services-and-customer-choice.md) | Services and customer choice | E12 | 2 | Spec 017 (approved, ADR 0074) | Done | US-120–US-121 | 11 |
| [F12.4](features/F12.4-rivals-and-competition.md) | Rivals and competition | E12 | 2 | Spec 017 (approved, ADR 0074) | Done | US-122–US-122 | 5 |
| [F12.5](features/F12.5-invoices-and-payment-terms.md) | Invoices and payment terms | E12 | 3 | Spec 028 (to write, ADR 0107) | Backlog | US-123 | 5 |
| [F12.6](features/F12.6-managers-valuation-and-exit.md) | Managers, valuation and exit | E12 | 3 | Spec 028 (to write, ADR 0107) | Backlog | US-124, US-125, US-247 | 18 |
| [F12.7](features/F12.7-township-businesses.md) | Township businesses | E12 | 3 | Specs 025 and 028 (to write, ADR 0107 §7) | Backlog | US-243, US-244, US-245, US-246 | 18 |
| [F13.1](features/F13.1-livestock.md) | Livestock | E13 | 2 | Spec 012 (approved, ADR 0060) | Done | US-126–US-126, US-234 | 10 |
| [F13.2](features/F13.2-irrigation-and-equipment.md) | Irrigation and equipment | E13 | 2 | Spec 013 (approved, ADR 0075) | Done | US-127–US-128 | 16 |
| [F13.3](features/F13.3-orchards-and-granary.md) | Orchards and granary | E13 | 2 | Spec 013 (approved, ADR 0075); US-242 spec 025 (ADR 0107 §9) | Done; US-242 Phase 3 (S23) | US-129, US-130, US-242 | 14 |
| [F14.1](features/F14.1-school-and-literacy.md) | School and literacy | E14 | 2 | Spec 018 (ADR 0077, 0095) | Done (Sprint 17) | US-131–US-132 | 13 |
| [F14.2](features/F14.2-codex-and-advisors.md) | Codex and advisors | E14 | 2 | Spec 018 (ADR 0077, 0095) | Done (Sprint 17) | US-133–US-134 | 13 |
| [F14.4](features/F14.4-personal-finance-hub-and-milestones.md) | Personal finance hub and milestones | E14 | 2 | Spec 019 (ADR 0078, 0083, 0101) | Done (Sprint 18) | US-135, US-136, US-221, US-235, US-236 | 30 |
| [F14.3](features/F14.3-financial-statements.md) | Financial statements | E14 | 2 | Spec 019 (ADR 0078, 0101) | Done (Sprint 18) | US-137 | 10 |
| [F15.1](features/F15.1-treasury-and-budget.md) | Treasury and budget | E15 | 3 | Spec 021 (to write, ADR 0107) | Backlog | US-138, US-139 | 13 |
| [F15.2](features/F15.2-taxes-and-policies.md) | Taxes and policies | E15 | 3 | Spec 022 (to write, ADR 0107) | Backlog | US-140, US-141 | 13 |
| [F15.3](features/F15.3-zoning-and-land-value.md) | Zoning and land value | E15 | 3 | Spec 024 (to write, ADR 0107) | Backlog | US-142, US-143 | 13 |
| [F15.4](features/F15.4-utilities-and-construction.md) | Utilities and construction | E15 | 3 | Spec 025 (to write, ADR 0107) | Backlog | US-144, US-145 | 16 |
| [F15.5](features/F15.5-roads-highway-and-hills.md) | Roads, highway and hills | E15 | 3 | Specs 023 and 025 (to write, ADR 0107) | Backlog | US-146, US-147 | 10 |
| [F15.6](features/F15.6-civic-services-and-coverage.md) | Civic services and coverage | E15 | 3 | Specs 023 and 026 (to write, ADR 0107) | Backlog | US-148, US-149 | 11 |
| [F15.7](features/F15.7-petitions-reputation-crime-and-inequality.md) | Petitions, reputation, crime and inequality | E15 | 3 | Spec 026 (to write, ADR 0107) | Backlog | US-150, US-151 | 8 |
| [F15.8](features/F15.8-building-upgrades-and-overlays.md) | Building upgrades and overlays | E15 | 3 | Spec 024 (to write, ADR 0107) | Backlog | US-152, US-153 | 10 |
| [F16.1](features/F16.1-event-engine.md) | Event engine | E16 | 3 | Spec 027 (to write, ADR 0107) | Backlog | US-154 | 8 |
| [F16.2](features/F16.2-insurance-and-forward-contracts.md) | Insurance and forward contracts | E16 | 3 | Spec 027 (to write, ADR 0107) | Backlog | US-155, US-156 | 8 |
| [F16.3](features/F16.3-chronicle-and-almanac.md) | Chronicle and Almanac | E16 | 3 | Spec 029 (to write, ADR 0107) | Backlog | US-157, US-158 | 8 |
| [F16.4](features/F16.4-citizen-stories.md) | Citizen stories | E16 | 3 | Spec 029 (to write, ADR 0107) | Backlog | US-159 | 5 |
| [F17.1](features/F17.1-cohort-simulation.md) | Cohort simulation | E17 | 4 | To write at the start of Phase 4 | Backlog | US-160–US-160 | 8 |
| [F17.2](features/F17.2-transport-and-logistics.md) | Transport and logistics | E17 | 4 | To write at the start of Phase 4 | Backlog | US-161–US-161 | 5 |
| [F17.3](features/F17.3-industry-and-pollution.md) | Industry and pollution | E17 | 4 | To write at the start of Phase 4 | Backlog | US-162–US-162 | 5 |
| [F18.1](features/F18.1-investments.md) | Investments | E18 | 4 | To write at the start of Phase 4 | Backlog | US-163–US-163 | 5 |
| [F18.2](features/F18.2-elections-and-council-mood.md) | Elections and council mood | E18 | 4 | To write at the start of Phase 4 | Backlog | US-164–US-164 | 3 |
| [F18.3](features/F18.3-town-identity-and-districts.md) | Town identity and districts | E18 | 4 | To write at the start of Phase 4 | Backlog | US-165–US-165 | 3 |
| [F18.4](features/F18.4-public-debt-and-conflicts-of-interest.md) | Public debt and conflicts of interest | E18 | 4 | To write at the start of Phase 4 | Backlog | US-166–US-167 | 8 |
| [F18.5](features/F18.5-trade-with-other-cities.md) | Trade with other cities | E18 | 4 | To write at the start of Phase 4 | Backlog | US-168–US-168 | 5 |
| [F19.1](features/F19.1-hindi-localisation.md) | Hindi localisation | E19 | 5 | To write at the start of Phase 5 | Backlog | US-169–US-169 | 5 |
| [F19.2](features/F19.2-accessibility.md) | Accessibility | E19 | 5 | To write at the start of Phase 5 | Backlog | US-170–US-170 | 5 |
| [F19.3](features/F19.3-art-pipeline.md) | Art pipeline | E19 | 5 | ADR 0039 (Phase 1 sprites); the rest at the start of Phase 5 | In progress | US-171–US-172, US-183 | 18 |
| [F19.6](features/F19.6-audio-and-music.md) | Audio and music | E19 | 5 | To write at the start of Phase 5 | Backlog | US-173–US-173 | 5 |
| [F19.4](features/F19.4-scenarios-and-difficulty.md) | Scenarios and difficulty | E19 | 5 | To write at the start of Phase 5 | Backlog | US-174–US-175 | 8 |
| [F19.5](features/F19.5-store-readiness-deferred.md) | Store readiness (deferred) | E19 | 5 | To write at the start of Phase 5 | Backlog | US-176–US-176 | 8 |

## Conventions (ADR 0025)

- **IDs:** epics `E00`, features `F01.2`, stories `US-001` (numbered across the project), tasks `US-001.T1`.
- **Points:** 1, 2, 3, 5, 8 (relative size). **Priority:** MoSCoW.
- **Status values:** To do · In progress · Done · Carried over. Feature status: Backlog · Ready · In progress · Done.
- **Definition of Ready:** spec approved, dependencies done, acceptance criteria mapped to named tests.
- **Definition of Done:** code written, acceptance tests pass, CI green, feature file and `STATUS.md` updated (GDD if changed), pushed; visible changes confirmed by the owner on the iPhone 13.
