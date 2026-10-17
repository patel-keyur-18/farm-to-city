# Epics

> All epics for all phases. Each feature has its own file in `features/` with its user stories and tasks. Sprint plans are in `sprints.md`. Rolling wave (ADR 0019): Phases 0–1 are broken down to tasks; later phases have stories, and their tasks are added when each phase's specs are written.

## Phase map

| Phase | Name | Epics | Exit gate |
| --- | --- | --- | --- |
| 0 | Pre-production | E00, E20 (start) | Economy model has no money loops; 2,000 agents at 60 fps (half frame budget on iPhone 13) |
| 1 | Vertical slice: Homestead to Hamlet unlock | E01–E10 | Testers come back on day 2 and day 3 unprompted |
| 2 | Hamlet and Village | E11–E14 | A week of play; players can explain profit versus cash |
| 3 | Township | E15–E16 | 4 weeks of play; the balancer finds no exploits |
| 4 | Town and City | E17–E18 | 20,000 population at 60 fps on the reference phone |
| 5 | Polish | E19 | Owner's call |

## Epics

| Epic | Title | Phase | Goal | Features | Points |
| --- | --- | --- | --- | --- | --- |
| E00 | Delivery foundations | 0 | Everything needed before Phase 1 coding: build and CI, the Phase 1 specs, the economy model, the Unity project, the performance spike and the art style check. | [F00.1](features/F00.1-repository-build-and-ci.md), [F00.2](features/F00.2-phase-1-feature-specs.md), [F00.3](features/F00.3-economy-model.md), [F00.4](features/F00.4-unity-project-and-bootstrap.md), [F00.5](features/F00.5-performance-spike.md), [F00.6](features/F00.6-art-style-check.md) | 60 |
| E01 | Time and world | 1 | The real-time clock, calendar, scheduler, weather, forecast, away rules and developer time travel (spec 001). | [F01.1](features/F01.1-sim-foundations.md), [F01.2](features/F01.2-calendar-and-scheduler.md), [F01.3](features/F01.3-weather-and-forecast.md), [F01.4](features/F01.4-live-and-away-time.md), [F01.5](features/F01.5-developer-time-travel-sim.md) | 31 |
| E02 | Land and farming | 1 | Land and soil, crops with seasonal sowing windows, the yield formula, inputs, pests and fruit trees. | [F02.1](features/F02.1-land-and-soil.md), [F02.2](features/F02.2-crops-and-yield.md), [F02.3](features/F02.3-fruit-trees.md) | 34 |
| E03 | Founder household | 1 | The founder as the first worker: labour, diet, rest, health and day labour (ADR 0010). | [F03.1](features/F03.1-founder-labour-and-routine.md) | 10 |
| E04 | Market and storage | 1 | Tradeable goods, the hourly market, the regional trader, the hut store and spoilage. | [F04.1](features/F04.1-goods-market-and-regional-trader.md), [F04.2](features/F04.2-hut-store-and-spoilage.md) | 15 |
| E05 | Money and credit | 1 | The ledger with accrual accounting, seasonal P&L, net worth, lenders, credit score and foreclosure. | [F05.1](features/F05.1-ledger-and-seasonal-p-l.md), [F05.2](features/F05.2-credit-and-foreclosure.md) | 23 |
| E06 | People | 1 | Day labourers from neighbouring villages, the first resident hand, season contracts and standing orders (ADR 0022). | [F06.1](features/F06.1-day-labourers.md), [F06.2](features/F06.2-resident-farm-hand.md), [F06.3](features/F06.3-standing-orders.md) | 19 |
| E07 | Saves and platform services | 1 | Safety save points, export and import, local notifications and the local play log. | [F07.1](features/F07.1-save-system.md), [F07.2](features/F07.2-local-notifications.md), [F07.3](features/F07.3-local-play-log.md) | 16 |
| E08 | Presentation | 1 | The code-first Unity layer: map, field interaction, HUD, panels with Why?, localisation groundwork and the developer panel. | [F08.1](features/F08.1-map-and-field-interaction.md), [F08.2](features/F08.2-hud-and-panels.md), [F08.3](features/F08.3-localisation-and-formatting.md), [F08.4](features/F08.4-developer-panel-unity.md) | 30 |
| E09 | Onboarding | 1 | The first session and the grandparent's letter chapters across the first week (ADR 0015). | [F09.1](features/F09.1-first-session.md), [F09.2](features/F09.2-letter-chapters.md) | 13 |
| E10 | Phase 1 playtest and tuning | 1 | Release candidate, testers, play logs, tuning, and the Phase 1 exit gate. | [F10.1](features/F10.1-playtest-and-tuning.md) | 8 |
| E11 | Citizens and households | 2 | Citizens with skills, traits and life stages; households, needs and happiness; births, migration and family trees; full hiring. | [F11.1](features/F11.1-citizens-and-life-clock.md), [F11.2](features/F11.2-households-needs-and-happiness.md), [F11.3](features/F11.3-population-flows-and-family-trees.md), [F11.4](features/F11.4-full-hiring-and-hr.md) | 38 |
| E12 | Businesses | 2 | Founding, running and failing businesses with their own books; production chains; services and customer choice; rivals. | [F12.1](features/F12.1-business-founding-and-books.md), [F12.2](features/F12.2-production-chains.md), [F12.3](features/F12.3-services-and-customer-choice.md), [F12.4](features/F12.4-rivals-and-competition.md) | 37 |
| E13 | Farm expansion | 2 | Livestock, the irrigation and equipment ladders, whole-acre orchards and the granary. | [F13.1](features/F13.1-livestock.md), [F13.2](features/F13.2-irrigation-and-equipment.md), [F13.3](features/F13.3-orchards-and-granary.md) | 18 |
| E14 | Education and learning layer | 2 | The founder-funded school, literacy, the Codex, advisors and the full financial statements. | [F14.1](features/F14.1-school-and-literacy.md), [F14.2](features/F14.2-codex-and-advisors.md), [F14.3](features/F14.3-financial-statements.md) | 21 |
| E15 | Town government | 3 | The treasury, budget, taxes, policies, zoning, land value, utilities, construction, roads and map expansion. | [F15.1](features/F15.1-treasury-and-budget.md), [F15.2](features/F15.2-taxes-and-policies.md), [F15.3](features/F15.3-zoning-and-land-value.md), [F15.4](features/F15.4-utilities-and-construction.md), [F15.5](features/F15.5-roads-highway-and-hills.md) | 40 |
| E16 | Events and history | 3 | The event engine, insurance, forward contracts, the Chronicle and the Town Almanac. | [F16.1](features/F16.1-event-engine.md), [F16.2](features/F16.2-insurance-and-forward-contracts.md), [F16.3](features/F16.3-chronicle-and-almanac.md) | 19 |
| E17 | Town and city scale | 4 | Cohort simulation for 20,000 people, transport and logistics, industry and pollution. | [F17.1](features/F17.1-cohort-simulation.md), [F17.2](features/F17.2-transport-and-logistics.md), [F17.3](features/F17.3-industry-and-pollution.md) | 18 |
| E18 | Wealth and politics | 4 | Investments, elections with council mood, town identity and districts. | [F18.1](features/F18.1-investments.md), [F18.2](features/F18.2-elections-and-council-mood.md), [F18.3](features/F18.3-town-identity-and-districts.md) | 11 |
| E19 | Polish and release readiness | 5 | Hindi, accessibility, the Blender art pipeline, scenarios and difficulty modes; store readiness if the owner decides. | [F19.1](features/F19.1-hindi-localisation.md), [F19.2](features/F19.2-accessibility.md), [F19.3](features/F19.3-art-pipeline.md), [F19.4](features/F19.4-scenarios-and-difficulty.md), [F19.5](features/F19.5-store-readiness-deferred.md) | 34 |
| E20 | Balancing and quality | 0–5 | The headless balancer, bots and health checks that gate every phase (ADR 0016). | [F20.1](features/F20.1-balancer-and-health-checks.md) | 11 |

## Features

| Feature | Title | Epic | Phase | Spec | Status | Stories | Points |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [F00.1](features/F00.1-repository-build-and-ci.md) | Repository, build and CI | E00 | 0 | ADR 0018, 0020, 0026 | Ready | US-001–US-004 | 7 |
| [F00.2](features/F00.2-phase-1-feature-specs.md) | Phase 1 feature specs | E00 | 0 | GDD, ADR 0019 | In progress | US-005–US-012 | 24 |
| [F00.3](features/F00.3-economy-model.md) | Economy model | E00 | 0 | GDD Balancing, ADR 0008, 0016 | Backlog | US-013–US-016 | 14 |
| [F00.4](features/F00.4-unity-project-and-bootstrap.md) | Unity project and bootstrap | E00 | 0 | ADR 0017, 0018, 0026 | Backlog | US-017–US-020 | 7 |
| [F00.5](features/F00.5-performance-spike.md) | Performance spike | E00 | 0 | GDD performance budget, ADR 0026 | Backlog | US-021–US-022 | 6 |
| [F00.6](features/F00.6-art-style-check.md) | Art style check | E00 | 0 | ADR 0007, 0017 | Backlog | US-023–US-023 | 2 |
| [F01.1](features/F01.1-sim-foundations.md) | Sim foundations | E01 | 1 | Spec 001 §3.1 | Ready | US-024–US-027 | 9 |
| [F01.2](features/F01.2-calendar-and-scheduler.md) | Calendar and scheduler | E01 | 1 | Spec 001 §3.2–3.3 | Ready | US-028–US-030 | 10 |
| [F01.3](features/F01.3-weather-and-forecast.md) | Weather and forecast | E01 | 1 | Spec 001 §3.5–3.6 | Ready | US-031–US-032 | 6 |
| [F01.4](features/F01.4-live-and-away-time.md) | Live and away time | E01 | 1 | Spec 001 §3.4, §3.7 | Ready | US-033–US-034 | 4 |
| [F01.5](features/F01.5-developer-time-travel-sim.md) | Developer time travel (sim) | E01 | 1 | Spec 001 §3.8 | Ready | US-035–US-036 | 2 |
| [F02.1](features/F02.1-land-and-soil.md) | Land and soil | E02 | 1 | Spec 002 (to write) | Backlog | US-037–US-040 | 10 |
| [F02.2](features/F02.2-crops-and-yield.md) | Crops and yield | E02 | 1 | Spec 003 (to write) | Backlog | US-041–US-045 | 17 |
| [F02.3](features/F02.3-fruit-trees.md) | Fruit trees | E02 | 1 | Spec 003 (to write) | Backlog | US-046–US-048 | 7 |
| [F03.1](features/F03.1-founder-labour-and-routine.md) | Founder labour and routine | E03 | 1 | Spec 003 (to write) | Backlog | US-049–US-052 | 10 |
| [F04.1](features/F04.1-goods-market-and-regional-trader.md) | Goods market and regional trader | E04 | 1 | Spec 004 (to write) | Backlog | US-053–US-055 | 11 |
| [F04.2](features/F04.2-hut-store-and-spoilage.md) | Hut store and spoilage | E04 | 1 | Spec 004 (to write) | Backlog | US-056–US-057 | 4 |
| [F05.1](features/F05.1-ledger-and-seasonal-p-l.md) | Ledger and seasonal P&L | E05 | 1 | Spec 005 (to write) | Backlog | US-058–US-061 | 14 |
| [F05.2](features/F05.2-credit-and-foreclosure.md) | Credit and foreclosure | E05 | 1 | Spec 005 (to write) | Backlog | US-062–US-064 | 9 |
| [F06.1](features/F06.1-day-labourers.md) | Day labourers | E06 | 1 | Spec 006 (to write) | Backlog | US-065–US-066 | 5 |
| [F06.2](features/F06.2-resident-farm-hand.md) | Resident farm hand | E06 | 1 | Spec 006 (to write) | Backlog | US-067–US-069 | 8 |
| [F06.3](features/F06.3-standing-orders.md) | Standing orders | E06 | 1 | Spec 006 (to write) | Backlog | US-070–US-071 | 6 |
| [F07.1](features/F07.1-save-system.md) | Save system | E07 | 1 | Spec 007 (to write) | Backlog | US-072–US-074 | 11 |
| [F07.2](features/F07.2-local-notifications.md) | Local notifications | E07 | 1 | Spec 007 (to write) | Backlog | US-075–US-075 | 3 |
| [F07.3](features/F07.3-local-play-log.md) | Local play log | E07 | 1 | Spec 007 (to write) | Backlog | US-076–US-076 | 2 |
| [F08.1](features/F08.1-map-and-field-interaction.md) | Map and field interaction | E08 | 1 | Spec 008 (to write) | Backlog | US-077–US-078 | 8 |
| [F08.2](features/F08.2-hud-and-panels.md) | HUD and panels | E08 | 1 | Spec 008 (to write) | Backlog | US-079–US-083 | 16 |
| [F08.3](features/F08.3-localisation-and-formatting.md) | Localisation and formatting | E08 | 1 | Spec 008 (to write) | Backlog | US-084–US-085 | 4 |
| [F08.4](features/F08.4-developer-panel-unity.md) | Developer panel (Unity) | E08 | 1 | Spec 001 §3.8, ADR 0023 | Backlog | US-086–US-086 | 2 |
| [F09.1](features/F09.1-first-session.md) | First session | E09 | 1 | Spec 009 (to write) | Backlog | US-087–US-088 | 7 |
| [F09.2](features/F09.2-letter-chapters.md) | Letter chapters | E09 | 1 | Spec 009 (to write) | Backlog | US-089–US-090 | 6 |
| [F10.1](features/F10.1-playtest-and-tuning.md) | Playtest and tuning | E10 | 1 | GDD Phase 1 gate | Backlog | US-091–US-093 | 8 |
| [F20.1](features/F20.1-balancer-and-health-checks.md) | Balancer and health checks | E20 | 0–5 | GDD Balancing, ADR 0016 | Backlog | US-094–US-096 | 11 |
| [F11.1](features/F11.1-citizens-and-life-clock.md) | Citizens and life clock | E11 | 2 | To write at the start of Phase 2 | Backlog | US-097–US-098 | 8 |
| [F11.2](features/F11.2-households-needs-and-happiness.md) | Households, needs and happiness | E11 | 2 | To write at the start of Phase 2 | Backlog | US-099–US-100 | 8 |
| [F11.3](features/F11.3-population-flows-and-family-trees.md) | Population flows and family trees | E11 | 2 | To write at the start of Phase 2 | Backlog | US-101–US-103 | 9 |
| [F11.4](features/F11.4-full-hiring-and-hr.md) | Full hiring and HR | E11 | 2 | To write at the start of Phase 2 | Backlog | US-104–US-106 | 13 |
| [F12.1](features/F12.1-business-founding-and-books.md) | Business founding and books | E12 | 2 | To write at the start of Phase 2 | Backlog | US-107–US-109 | 16 |
| [F12.2](features/F12.2-production-chains.md) | Production chains | E12 | 2 | To write at the start of Phase 2 | Backlog | US-110–US-111 | 8 |
| [F12.3](features/F12.3-services-and-customer-choice.md) | Services and customer choice | E12 | 2 | To write at the start of Phase 2 | Backlog | US-112–US-113 | 8 |
| [F12.4](features/F12.4-rivals-and-competition.md) | Rivals and competition | E12 | 2 | To write at the start of Phase 2 | Backlog | US-114–US-114 | 5 |
| [F13.1](features/F13.1-livestock.md) | Livestock | E13 | 2 | To write at the start of Phase 2 | Backlog | US-115–US-115 | 5 |
| [F13.2](features/F13.2-irrigation-and-equipment.md) | Irrigation and equipment | E13 | 2 | To write at the start of Phase 2 | Backlog | US-116–US-117 | 8 |
| [F13.3](features/F13.3-orchards-and-granary.md) | Orchards and granary | E13 | 2 | To write at the start of Phase 2 | Backlog | US-118–US-119 | 5 |
| [F14.1](features/F14.1-school-and-literacy.md) | School and literacy | E14 | 2 | To write at the start of Phase 2 | Backlog | US-120–US-121 | 8 |
| [F14.2](features/F14.2-codex-and-advisors.md) | Codex and advisors | E14 | 2 | To write at the start of Phase 2 | Backlog | US-122–US-123 | 8 |
| [F14.3](features/F14.3-financial-statements.md) | Financial statements | E14 | 2 | To write at the start of Phase 2 | Backlog | US-124–US-124 | 5 |
| [F15.1](features/F15.1-treasury-and-budget.md) | Treasury and budget | E15 | 3 | To write at the start of Phase 3 | Backlog | US-125–US-126 | 8 |
| [F15.2](features/F15.2-taxes-and-policies.md) | Taxes and policies | E15 | 3 | To write at the start of Phase 3 | Backlog | US-127–US-128 | 8 |
| [F15.3](features/F15.3-zoning-and-land-value.md) | Zoning and land value | E15 | 3 | To write at the start of Phase 3 | Backlog | US-129–US-130 | 8 |
| [F15.4](features/F15.4-utilities-and-construction.md) | Utilities and construction | E15 | 3 | To write at the start of Phase 3 | Backlog | US-131–US-132 | 10 |
| [F15.5](features/F15.5-roads-highway-and-hills.md) | Roads, highway and hills | E15 | 3 | To write at the start of Phase 3 | Backlog | US-133–US-134 | 6 |
| [F16.1](features/F16.1-event-engine.md) | Event engine | E16 | 3 | To write at the start of Phase 3 | Backlog | US-135–US-135 | 5 |
| [F16.2](features/F16.2-insurance-and-forward-contracts.md) | Insurance and forward contracts | E16 | 3 | To write at the start of Phase 3 | Backlog | US-136–US-137 | 6 |
| [F16.3](features/F16.3-chronicle-and-almanac.md) | Chronicle and Almanac | E16 | 3 | To write at the start of Phase 3 | Backlog | US-138–US-139 | 8 |
| [F17.1](features/F17.1-cohort-simulation.md) | Cohort simulation | E17 | 4 | To write at the start of Phase 4 | Backlog | US-140–US-140 | 8 |
| [F17.2](features/F17.2-transport-and-logistics.md) | Transport and logistics | E17 | 4 | To write at the start of Phase 4 | Backlog | US-141–US-141 | 5 |
| [F17.3](features/F17.3-industry-and-pollution.md) | Industry and pollution | E17 | 4 | To write at the start of Phase 4 | Backlog | US-142–US-142 | 5 |
| [F18.1](features/F18.1-investments.md) | Investments | E18 | 4 | To write at the start of Phase 4 | Backlog | US-143–US-143 | 5 |
| [F18.2](features/F18.2-elections-and-council-mood.md) | Elections and council mood | E18 | 4 | To write at the start of Phase 4 | Backlog | US-144–US-144 | 3 |
| [F18.3](features/F18.3-town-identity-and-districts.md) | Town identity and districts | E18 | 4 | To write at the start of Phase 4 | Backlog | US-145–US-145 | 3 |
| [F19.1](features/F19.1-hindi-localisation.md) | Hindi localisation | E19 | 5 | To write at the start of Phase 5 | Backlog | US-146–US-146 | 5 |
| [F19.2](features/F19.2-accessibility.md) | Accessibility | E19 | 5 | To write at the start of Phase 5 | Backlog | US-147–US-147 | 5 |
| [F19.3](features/F19.3-art-pipeline.md) | Art pipeline | E19 | 5 | To write at the start of Phase 5 | Backlog | US-148–US-148 | 8 |
| [F19.4](features/F19.4-scenarios-and-difficulty.md) | Scenarios and difficulty | E19 | 5 | To write at the start of Phase 5 | Backlog | US-149–US-150 | 8 |
| [F19.5](features/F19.5-store-readiness-deferred.md) | Store readiness (deferred) | E19 | 5 | To write at the start of Phase 5 | Backlog | US-151–US-151 | 8 |

## Conventions (ADR 0025)

- **IDs:** epics `E00`, features `F01.2`, stories `US-001` (numbered across the project), tasks `US-001.T1`.
- **Points:** 1, 2, 3, 5, 8 (relative size). **Priority:** MoSCoW.
- **Status values:** To do · In progress · Done · Carried over. Feature status: Backlog · Ready · In progress · Done.
- **Definition of Ready:** spec approved, dependencies done, acceptance criteria mapped to named tests.
- **Definition of Done:** code written, acceptance tests pass, CI green, feature file and `STATUS.md` updated (GDD if changed), pushed; visible changes confirmed by the owner on the iPhone 13.
